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
                if info.wantCompressed then
                    match TextureFormat.toCompressed baseFormat with
                    | Some fmt -> fmt
                    | _ ->
                        Log.warn "[Vulkan] Texture format %A does not support compression" baseFormat
                        baseFormat
                else
                    baseFormat

        module ImageBuffer =

            let ofNativeTensor4 (mirrorY : bool) (dstFormat : TextureFormat) (srcFormat : Col.Format) (src : NativeTensor4<'T>) (device : Device) =
                let size = V3i src.Size.XYZ
                let compression = dstFormat.CompressionMode

                if compression = CompressionMode.None then
                    let img = device.CreateTensorImage<'T>(size, TextureFormat.toColFormat dstFormat, dstFormat.IsSrgb)
                    img.Write(srcFormat, if mirrorY then src.MirrorY() else src)
                    img :> ImageBuffer

                else
                    let blockSize =
                        CompressionMode.blockSize compression

                    let alignedSize =
                        let blocks = compression |> CompressionMode.numberOfBlocks size
                        blocks * blockSize

                    let sizeInBytes =
                        int64 <| CompressionMode.sizeInBytes size compression

                    let buffer = device.CreateImageBuffer(dstFormat, size, alignedSize.XY, sizeInBytes)

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

                        let buffer = device.CreateImageBuffer(texture.Format, data.Size, alignedSize.XY, data.SizeInBytes)

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
                let format = PixFormat.toTextureFormat info pix.[0].PixFormat

                let levels =
                    pix |> Array.map (fun i -> i.LevelCount) |> Array.min

                let buffers =
                    Array.init (pix.Length * levels) (fun i ->
                        let slice = i / levels
                        let level = i % levels

                        device |> ImageBuffer.ofPixImage format pix.[slice].[level]
                    )

                buffers |> create levels


        let ofImageBufferArray (dimension : TextureDimension) (wantMipmap : bool)
                               (buffers : ImageBufferArray) (sharing : bool) (device : Device) =
            let slices = buffers.Slices

            let uploadLevels =
                if wantMipmap then buffers.Levels
                else 1

            let mipMapLevels =
                if wantMipmap then
                    if TextureFormat.isFilterable buffers.TextureFormat then
                        Fun.MipmapLevels(buffers.BaseSize)
                    else
                        uploadLevels
                else
                    1

            let generateMipmap =
                uploadLevels < mipMapLevels

            let isArray = if dimension = TextureDimension.TextureCube then slices > 6 else slices > 1

            let image = device.CreateImage(buffers.BaseSize, mipMapLevels, slices, 1, dimension, buffers.TextureFormat, Image.defaultUsage, isArray, sharing)
            let imageRange = image.[TextureAspect.Color]

            match device.UploadMode with
            | UploadMode.Async ->
                image.Layout <- VkImageLayout.TransferDstOptimal

                device.CopyEngine.EnqueueSafe [
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

        let ofImageBuffer (dimension : TextureDimension) (wantMipmap : bool) (buffer : ImageBuffer) (sharing : bool) (device : Device) =
            let buffers = [| buffer |] |> ImageBufferArray.create 1
            device |> ofImageBufferArray dimension wantMipmap buffers sharing

        let uploadNativeTensor4<'T when 'T : unmanaged> (dst : ImageSubresource) (offset : V3i) (size : V3i) (src : NativeTensor4<'T>) =
            let device = dst.Image.Device
            let textureFormat = VkFormat.toTextureFormat dst.Image.Format
            let format = TextureFormat.toColFormat textureFormat

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

        let ofNativeTexture (data : INativeTexture) (sharing : bool) (device : Device) =
            let buffers = device |> ImageBufferArray.ofNativeTexture data
            device |> ofImageBufferArray data.Dimension data.WantMipMaps buffers sharing

        let ofPixImageMipMap (data : PixImageMipMap) (info : TextureParams) (sharing : bool) (device : Device) =
            let buffers = device |> ImageBufferArray.ofPixImageMipMaps info [| data |]
            device |> ofImageBufferArray TextureDimension.Texture2D info.wantMipMaps buffers sharing

        let ofPixVolume (data : PixVolume) (info : TextureParams) (sharing : bool) (device : Device) =
            let buffer = device |> ImageBuffer.ofPixVolume info data
            device |> ofImageBuffer TextureDimension.Texture3D info.wantMipMaps buffer sharing

        let ofPixImageCube (data : PixImageCube) (info : TextureParams) (sharing : bool) (device : Device) =
            let buffers = device |> ImageBufferArray.ofPixImageMipMaps info data.MipMapArray
            device |> ofImageBufferArray TextureDimension.TextureCube info.wantMipMaps buffers sharing

        let ofStream (stream : IO.Stream) (info : TextureParams) (sharing : bool) (device : Device) =
            let temp = device |> TensorImage.ofStream stream info.wantSrgb
            device |> ofImageBuffer TextureDimension.Texture2D info.wantMipMaps temp sharing

        let ofFile (path : string) (info : TextureParams) (sharing : bool) (device : Device) =
            use stream = IO.File.OpenRead(path)
            ofStream stream info sharing device

        let rec ofTexture (t : ITexture) (sharing : bool) (device : Device) : Image =
            match t with
            | :? PixTexture2d as t ->
                device |> ofPixImageMipMap t.PixImageMipMap t.TextureParams sharing

            | :? PixTextureCube as c ->
                device |> ofPixImageCube c.PixImageCube c.TextureParams sharing

            | :? NullTexture as t ->
                device |> ofPixImageMipMap (PixImageMipMap [| PixImage<byte>(Col.Format.RGBA, V2i.II) :> PixImage |]) TextureParams.empty sharing

            | :? PixTexture3d as t ->
                device |> ofPixVolume t.PixVolume t.TextureParams sharing

            | :? StreamTexture as t ->
                use stream = t.Open(true)
                let initialPos = stream.Position

                // Always try to load compressed data first
                let compressed =
                    stream |> DdsTexture.tryLoadCompressedFromStream

                match compressed with
                | Some t ->
                    device |> ofTexture t sharing

                | _ ->
                    stream.Position <- initialPos
                    device |> ofStream stream t.TextureParams sharing

            | :? INativeTexture as nt ->
                device |> ofNativeTexture nt sharing

            | :? Image as t ->
                if sharing && t.ShareInfo.IsNone then
                    failwith "cannot prepare already preparted texture with different sharing option"
                t.AddReference()
                t

            | _ ->
                failf "unsupported texture-type: %A" t

        let uploadLevel (offset : V3i) (size : V3i) (src : NativeTensor4<'T>) (dst : ImageSubresource) (device : Device) =
            src |> uploadNativeTensor4 dst offset size

    [<AbstractClass; Sealed; Extension>]
    type DeviceImageUploadExtensions private() =

        [<Extension>]
        static member inline CreateImage(this : Device, pi : PixImageMipMap, info : TextureParams, sharing : bool) =
            this |> Image.ofPixImageMipMap pi info sharing

        [<Extension>]
        static member inline CreateImage(this : Device, file : string, info : TextureParams, sharing : bool) =
            this |> Image.ofFile file info sharing

        [<Extension>]
        static member inline CreateImage(this : Device, t : ITexture, sharing : bool) =
            this |> Image.ofTexture t sharing

        [<Extension>]
        static member inline UploadLevel(this : Device, dst : ImageSubresource, src : NativeTensor4<'T>, offset : V3i, size : V3i) =
            this |> Image.uploadLevel offset size src dst