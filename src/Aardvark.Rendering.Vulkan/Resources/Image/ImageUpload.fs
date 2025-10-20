namespace Aardvark.Rendering.Vulkan

open System
open System.Runtime.InteropServices
open System.Runtime.CompilerServices

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan

[<AutoOpen>]
module ImageUploadExtensions =

    [<AutoOpen>]
    module private UploadImplementation =

        module PixFormat =

            let toTextureFormat (info : TextureParams) (format : PixFormat) =
                let baseFormat = TextureFormat.ofPixFormat format info
                if info.HasCompress then
                    match TextureFormat.toCompressed baseFormat with
                    | Some fmt -> fmt
                    | _ ->
                        Log.warn "[Vulkan] Texture format %A does not support compression" baseFormat
                        baseFormat
                else
                    baseFormat

        module ImageBuffer =

            let ofNativeTensor4 (mirrorY : bool) (textureFormat : TextureFormat) (format : Col.Format)
                                (src : NativeTensor4<'T>) (device : Device) =
                let size = V3i src.Size.XYZ
                let compression = textureFormat.CompressionMode

                if compression = CompressionMode.None then
                    let img = device.ReadbackMemory.CreateTensorImage<'T>(size, TextureFormat.toColFormat textureFormat, textureFormat.IsSrgb)
                    img.Write(format, if mirrorY then src.MirrorY() else src)
                    img :> ImageBuffer

                else
                    let blockSize =
                        CompressionMode.blockSize compression

                    let alignedSize =
                        let blocks = compression |> CompressionMode.numberOfBlocks size
                        blocks * blockSize

                    let sizeInBytes =
                        uint64 <| CompressionMode.sizeInBytes size compression

                    let buffer = device.ReadbackMemory.CreateImageBuffer(textureFormat, size, alignedSize.XY, sizeInBytes)

                    buffer.Memory.Mapped (fun dst ->
                        let srcInfo = src.Info.SubXYWVolume(0L).Transformed(ImageTrafo.MirrorY)
                        BlockCompression.encode compression src.Address srcInfo dst
                    )

                    buffer

            let ofPixImage (dstFormat : TextureFormat) (pix : PixImage) (device : Device) =
                    pix.Visit
                        { new PixVisitors.PixImageVisitor<ImageBuffer>() with
                            member x.Visit(img : PixImage<'T>) =
                                NativeVolume.using img.Volume (fun src ->
                                    let src = src.ToXYWTensor4'()
                                    device |> ofNativeTensor4 true dstFormat pix.Format src
                                )
                        }

            let ofPixVolume (info : TextureParams) (pix : PixVolume) (device : Device) =
                let textureFormat = PixFormat.toTextureFormat info pix.PixFormat

                pix.Visit
                    { new PixVisitors.PixVolumeVisitor<ImageBuffer>() with
                        member x.Visit(img : PixVolume<'T>) =
                            NativeTensor4.using img.Tensor4 (fun src ->
                                device |> ofNativeTensor4 false textureFormat pix.Format src
                            )
                    }

        type ImageBufferArray =
            { Buffers : ImageBuffer[]
              Levels  : int }

            member x.TextureFormat =
                x.Buffers.[0].TextureFormat

            member x.BaseSize =
                x.Buffers.[0].ImageSize

            member x.Slices =
                x.Buffers.Length / x.Levels

            member x.Item(level : int, slice : int) =
                x.Buffers.[slice * x.Levels + level]

            member x.Dispose() =
                x.Buffers |> Array.iter Disposable.dispose

            interface IDisposable with
                member x.Dispose() = x.Dispose()

        module ImageBufferArray =

            let create (levels : int) (buffers : #ImageBuffer[]) =
                assert (buffers.Length % levels = 0)

                { Buffers = buffers |> Array.map unbox
                  Levels  = levels }

            let ofNativeTexture (texture : INativeTexture) (device : Device) =
                let compression = TextureFormat.compressionMode texture.Format
                let blockSize = compression |> CompressionMode.blockSize

                let levelCount =
                    if texture.WantMipMaps then texture.MipMapLevels else 1

                let isCubeOr2D =
                    match texture.Dimension with
                    | TextureDimension.Texture2D | TextureDimension.TextureCube -> true
                    | _ -> false

                let buffers =
                    Array.init (texture.Count * levelCount) (fun i ->
                        let slice = i / levelCount
                        let level = i % levelCount

                        let data = texture.[slice, level]

                        let alignedSize =
                            let blocks = compression |> CompressionMode.numberOfBlocks data.Size
                            blocks * blockSize

                        let buffer = device.ReadbackMemory.CreateImageBuffer(texture.Format, data.Size, alignedSize.XY, data.SizeInBytes)

                        buffer.Memory.Mapped (fun dst ->
                            data.Use (fun src ->
                                if compression <> CompressionMode.None && isCubeOr2D then
                                    BlockCompression.mirrorCopy compression data.Size.XY src dst
                                else
                                    Marshal.Copy(src, dst, data.SizeInBytes)
                            )
                        )

                        buffer
                    )

                buffers |> create levelCount

            let ofPixImageMipMaps (info : TextureParams) (pix : PixImageMipMap[]) (device : Device) =
                let baseSize = pix.[0].BaseSize
                let baseFormat = pix.[0].BaseImage.PixFormat
                let format = PixFormat.toTextureFormat info baseFormat

                let levelCount =
                    if info.HasWantMipMaps then Fun.MipmapLevels(pix.[0].BaseSize) else 1

                if format |> TextureFormat.supportsMipmapGeneration device |> not then
                    for i = 0 to pix.Length - 1 do
                        if pix.[i].LevelCount < levelCount then
                            pix.[i] <- PixImageMipMap.Create(pix.[i].BaseImage)

                let levels =
                    pix |> Array.map _.LevelCount |> Array.min

                let buffers = Array.zeroCreate (pix.Length * levels)

                try
                    for i = 0 to buffers.Length - 1 do
                        let slice = i / levels
                        let level = i % levels

                        pix.[slice].[level] |> ResourceValidation.Textures.validatePixImage baseFormat baseSize slice level
                        buffers.[i] <- device |> ImageBuffer.ofPixImage format pix.[slice].[level]
                with _ ->
                    for b in buffers do if notNull b then b.Dispose()
                    reraise()

                buffers |> create levels


        let ofImageBufferArray (dimension : TextureDimension) (wantMipmap : bool) (export : bool)
                               (buffers : ImageBufferArray) (device : Device) =
            let slices = buffers.Slices

            let uploadLevels =
                if wantMipmap then buffers.Levels
                else 1

            let mipMapLevels =
                if wantMipmap then
                    let levels = Fun.MipmapLevels buffers.BaseSize

                    if buffers.TextureFormat |> TextureFormat.supportsMipmapGeneration device then
                        levels
                    else
                        if uploadLevels < levels then
                            Log.warn "[Vk] Format %A does not support mipmap generation" buffers.TextureFormat

                        uploadLevels
                else
                    1

            let generateMipmap =
                uploadLevels < mipMapLevels

            let exportMode =
                if export then
                    ImageExport.Enable false
                else
                    ImageExport.Disable

            let count = if dimension = TextureDimension.TextureCube then slices / 6 else slices

            let image = device.CreateImage(buffers.BaseSize, mipMapLevels, count, 1, dimension, buffers.TextureFormat, Image.defaultUsage, exportMode)
            let imageRange = image.[TextureAspect.Color]

            match device.UploadMode with
            | UploadMode.Async ->
                image.Layout <- VkImageLayout.TransferDstOptimal

                device.CopyEngine.RunSynchronously [
                    yield CopyCommand.TransformLayout(imageRange, VkImageLayout.Undefined, VkImageLayout.TransferDstOptimal)

                    for slice = 0 to slices - 1 do
                        for level = 0 to uploadLevels - 1 do
                            let src = buffers.[level, slice]
                            let dst = imageRange.[level, slice]

                            yield CopyCommand.Copy(src, dst, V3i.Zero)

                    yield CopyCommand.Release(imageRange, VkImageLayout.TransferDstOptimal, device.GraphicsFamily)
                    yield CopyCommand.Callback buffers.Dispose
                ]

                device.eventually {
                    do! Command.Acquire(imageRange, VkImageLayout.TransferDstOptimal, device.TransferFamily)

                    if generateMipmap then
                        do! Command.GenerateMipMaps(imageRange, uploadLevels - 1)

                    do! Command.TransformLayout(image, VkImageLayout.ShaderReadOnlyOptimal)
                }

            | _ ->
                device.eventually {
                    try
                        do! Command.TransformLayout(image, VkImageLayout.TransferDstOptimal)

                        for slice = 0 to slices - 1 do
                            for level = 0 to uploadLevels - 1 do
                                let src = buffers.[level, slice]
                                let dst = image.[TextureAspect.Color, level, slice]

                                do! Command.Copy(src, dst, V3i.Zero)

                        if generateMipmap then
                            do! Command.GenerateMipMaps(image.[TextureAspect.Color], uploadLevels - 1)

                        do! Command.TransformLayout(image, VkImageLayout.ShaderReadOnlyOptimal)

                    finally
                        buffers.Dispose()
                }

            image

        let ofImageBuffer (dimension : TextureDimension) (wantMipmap : bool) (buffer : ImageBuffer) (export : bool) (device : Device) =
            let buffers = [| buffer |] |> ImageBufferArray.create 1
            device |> ofImageBufferArray dimension wantMipmap export buffers

        let uploadNativeTensor4<'T when 'T : unmanaged> (dst : ImageSubresource) (offset : V3i) (size : V3i)
                                                        (format : Col.Format) (src : NativeTensor4<'T>) =
            let device = dst.Image.Device
            let textureFormat = VkFormat.toTextureFormat dst.Image.Format

            let src =
                src.SubTensor4(V4i.Zero, V4i(size, int src.SW))

            let offset =
                if dst.Image.IsCubeOr2D then
                    V3i(offset.X, dst.Size.Y - offset.Y - int src.SY, offset.Z) // flip y-offset
                else
                    offset

            let buffer =
                device |> ImageBuffer.ofNativeTensor4 dst.Image.IsCubeOr2D textureFormat format src

            let layout = dst.Image.Layout

            device.eventually {
                try
                    do! Command.TransformLayout(dst.Image, VkImageLayout.TransferDstOptimal)
                    do! Command.Copy(buffer, dst, offset)
                    do! Command.TransformLayout(dst.Image, layout)
                finally
                    buffer.Dispose()
            }


    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Image =

        let ofNativeTexture (data : INativeTexture) (export : bool) (device : Device) =
            let buffers = device |> ImageBufferArray.ofNativeTexture data
            device |> ofImageBufferArray data.Dimension data.WantMipMaps export buffers

        let ofPixImageMipMap (data : PixImageMipMap) (info : TextureParams) (export : bool) (device : Device) =
            let buffers = device |> ImageBufferArray.ofPixImageMipMaps info [| data |]
            device |> ofImageBufferArray TextureDimension.Texture2D info.HasWantMipMaps export buffers

        let ofPixVolume (data : PixVolume) (info : TextureParams) (export : bool) (device : Device) =
            let buffer = device |> ImageBuffer.ofPixVolume info data
            device |> ofImageBuffer TextureDimension.Texture3D info.HasWantMipMaps buffer export

        let ofPixCube (data : PixCube) (info : TextureParams) (export : bool) (device : Device) =
            let buffers = device |> ImageBufferArray.ofPixImageMipMaps info data.MipMapArray
            device |> ofImageBufferArray TextureDimension.TextureCube info.HasWantMipMaps export buffers

        let ofStreamWithLoader (stream : IO.Stream) (loader : IPixLoader) (info : TextureParams) (export : bool) (device : Device) =
            let pix =
                if info.HasWantMipMaps then
                    PixImageMipMap.Load(stream, loader)
                else
                    PixImageMipMap [| PixImage.Load(stream, loader) |]

            device |> ofPixImageMipMap pix info export

        let ofStream (stream : IO.Stream) (info : TextureParams) (export : bool) (device : Device) =
            ofStreamWithLoader stream null info export device

        let ofFileWithLoader (path : string) (loader : IPixLoader) (info : TextureParams) (export : bool) (device : Device) =
            use stream = IO.File.OpenRead(path)
            ofStreamWithLoader stream loader info export device

        let ofFile (path : string) (info : TextureParams) (export : bool) (device : Device) =
            ofFileWithLoader path null info export device

        let rec internal ofTextureInternal (texture : ITexture) (properties : ImageProperties voption) (export : bool) (device : Device) : Image =
            match texture with
            | :? PixTexture2d as t ->
                device |> ofPixImageMipMap t.PixImageMipMap t.TextureParams export

            | :? PixTextureCube as c ->
                device |> ofPixCube c.PixCube c.TextureParams export

            | :? NullTexture ->
                let properties =
                    match properties with
                    | ValueSome p -> p
                    | _ -> failf "cannot prepare null texture without properties"

                device |> Image.getNull properties

            | :? PixTexture3d as t ->
                device |> ofPixVolume t.PixVolume t.TextureParams export

            | :? StreamTexture as t ->
                use stream = t.Open(true)
                let initialPos = stream.Position

                // Always try to load compressed data first
                let compressed =
                    stream |> DdsTexture.tryLoadCompressedFromStream t.TextureParams.HasWantMipMaps

                match compressed with
                | Some t ->
                    device |> ofTextureInternal t properties export

                | _ ->
                    stream.Position <- initialPos
                    device |> ofStreamWithLoader stream t.PreferredLoader t.TextureParams export

            | :? INativeTexture as nt ->
                device |> ofNativeTexture nt export

            | :? ExportedImage as t when export ->
                device |> ofTextureInternal t properties false

            | :? Image as t ->
                if export then
                    failf "cannot export image after it has been created"
                t.AddReference()
                t

            | null ->
                failf "texture data is null (use NullTexture if this is intended)"

            | _ ->
                failf $"unsupported texture type: {texture.GetType()}"

        let ofTexture (texture : ITexture) (export : bool) (device : Device) =
            ofTextureInternal texture ValueNone export device

        let uploadLevel (offset : V3i) (size : V3i) (format : Col.Format)
                        (src : NativeTensor4<'T>) (dst : ImageSubresource) (device : Device) =
            src |> uploadNativeTensor4 dst offset size format

    [<AbstractClass; Sealed; Extension>]
    type DeviceImageUploadExtensions private() =

        [<Extension>]
        static member inline CreateImage(this : Device, pi : PixImageMipMap, info : TextureParams,
                                         [<Optional; DefaultParameterValue(false)>] export : bool) =
            this |> Image.ofPixImageMipMap pi info export

        [<Extension>]
        static member inline CreateImage(this : Device, file : string, info : TextureParams,
                                         [<Optional; DefaultParameterValue(false)>] export : bool) =
            this |> Image.ofFile file info export

        [<Extension>]
        static member inline internal CreateImage(this : Device, texture : ITexture, properties : ImageProperties,
                                                  [<Optional; DefaultParameterValue(false)>] export : bool) =
            this |> Image.ofTextureInternal texture (ValueSome properties) export

        [<Extension>]
        static member inline CreateImage(this : Device, texture : ITexture,
                                         [<Optional; DefaultParameterValue(false)>] export : bool) =
            this |> Image.ofTexture texture export

        [<Extension>]
        static member inline UploadLevel(this : Device, dst : ImageSubresource, src : NativeTensor4<'T>,
                                         format : Col.Format, offset : V3i, size : V3i) =
            this |> Image.uploadLevel offset size format src dst