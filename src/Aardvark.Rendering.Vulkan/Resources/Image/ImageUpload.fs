namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open System
open System.Runtime.InteropServices
open System.Runtime.CompilerServices

[<AutoOpen>]
module ImageUploadExtensions =

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Image =

        let ofPixImageMipMap (pi : PixImageMipMap) (info : TextureParams) (device : Device) =
            if pi.LevelCount <= 0 then failf "empty PixImageMipMap"

            let format = pi.ImageArray.[0].PixFormat
            let size = pi.ImageArray.[0].Size

            let format = device.GetSupportedFormat(VkImageTiling.Optimal, format, info)
            let expectedFormat = PixFormat(VkFormat.expectedType format, VkFormat.toColFormat format)

            let uploadLevels =
                if info.wantMipMaps then pi.LevelCount
                else 1

            let mipMapLevels =
                if info.wantMipMaps then
                    if TextureFormat.isFilterable (VkFormat.toTextureFormat format) then
                        Fun.MipmapLevels(size)
                    else
                        uploadLevels
                else
                    1

            let generateMipMaps =
                uploadLevels < mipMapLevels

            let image =
                Image.create
                    size.XYI
                    mipMapLevels 1 1
                    TextureDimension.Texture2D
                    format
                    (VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.SampledBit ||| VkImageUsageFlags.StorageBit)
                    device

            let tempImage =
                device.CreateTensorImage2D(pi, uploadLevels, info.wantSrgb)

            match device.UploadMode with
            | UploadMode.Async ->
                let imageRange = image.[TextureAspect.Color]
                image.Layout <- VkImageLayout.TransferDstOptimal

                device.CopyEngine.EnqueueSafe [
                    yield CopyCommand.TransformLayout(imageRange, VkImageLayout.Undefined, VkImageLayout.TransferDstOptimal)
                    yield! tempImage.ImageArray |> Array.mapi (fun level src -> CopyCommand.Copy(src, imageRange.[level, 0]))

                    yield CopyCommand.SyncImage(imageRange, VkImageLayout.TransferDstOptimal, VkAccessFlags.TransferWriteBit)

                    yield CopyCommand.Release(imageRange, VkImageLayout.TransferDstOptimal, device.GraphicsFamily)
                    yield CopyCommand.Callback (fun () -> tempImage.Dispose())
                ]

                device.eventually {
                    do! Command.Acquire(imageRange, VkImageLayout.TransferDstOptimal, device.TransferFamily)
                    if generateMipMaps then
                        do! Command.GenerateMipMaps(image.[TextureAspect.Color], uploadLevels - 1)
                    do! Command.TransformLayout(image, VkImageLayout.ShaderReadOnlyOptimal)
                }

            | _ ->
                device.eventually {
                    try
                        do! Command.TransformLayout(image, VkImageLayout.TransferDstOptimal)

                        // upload the levels
                        let mutable level = 0
                        for temp in tempImage.ImageArray do
                            do! Command.Copy(temp, image.[TextureAspect.Color, level, 0])
                            level <- level + 1

                        // generate the mipMaps
                        if generateMipMaps then
                            do! Command.GenerateMipMaps(image.[TextureAspect.Color], uploadLevels - 1)

                        do! Command.TransformLayout(image, VkImageLayout.ShaderReadOnlyOptimal)

                    finally
                        tempImage.Dispose()
                }

            image

        // TODO: check CopyEngine
        let ofPixVolume (pi : PixVolume) (info : TextureParams) (device : Device) =
            let format = pi.PixFormat
            let size = pi.Size

            let format = device.GetSupportedFormat(VkImageTiling.Optimal, format, info)
            let expectedFormat = PixFormat(VkFormat.expectedType format, VkFormat.toColFormat format)


            let mipMapLevels =
                if info.wantMipMaps && TextureFormat.isFilterable (VkFormat.toTextureFormat format) then
                    Fun.MipmapLevels(size)
                else
                    1

            let image =
                Image.create
                    size
                    mipMapLevels 1 1
                    TextureDimension.Texture3D
                    format
                    (VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.SampledBit ||| VkImageUsageFlags.StorageBit)
                    device

            let temp = device.CreateTensorImage(pi.Size, expectedFormat, info.wantSrgb)
            temp.Write(pi, false)

            match device.UploadMode with
            | UploadMode.Async ->
                let imageRange = image.[TextureAspect.Color]
                image.Layout <- VkImageLayout.TransferDstOptimal

                device.CopyEngine.EnqueueSafe [
                    CopyCommand.TransformLayout(imageRange, VkImageLayout.Undefined, VkImageLayout.TransferDstOptimal)
                    CopyCommand.Copy(temp, imageRange.[0,0])

                    CopyCommand.SyncImage(imageRange, VkImageLayout.TransferDstOptimal, VkAccessFlags.TransferWriteBit)

                    CopyCommand.Release(imageRange, VkImageLayout.TransferDstOptimal, device.GraphicsFamily)
                    CopyCommand.Callback(fun () -> temp.Dispose())
                ]

                device.eventually {
                    do! Command.Acquire(imageRange, VkImageLayout.TransferDstOptimal, device.TransferFamily)

                    // generate the mipMaps
                    if mipMapLevels > 1 then
                        do! Command.GenerateMipMaps image.[TextureAspect.Color]

                    do! Command.TransformLayout(image, VkImageLayout.ShaderReadOnlyOptimal)
                }

            | _ ->
                device.eventually {
                    try
                        do! Command.TransformLayout(image, VkImageLayout.TransferDstOptimal)

                        // upload the level 0
                        do! Command.Copy(temp, image.[TextureAspect.Color, 0, 0])

                        // generate the mipMaps
                        if mipMapLevels > 1 then
                            do! Command.GenerateMipMaps image.[TextureAspect.Color]

                        do! Command.TransformLayout(image, VkImageLayout.ShaderReadOnlyOptimal)

                    finally
                        temp.Dispose()
                }

            image

        // TODO: check CopyEngine
        let ofPixImageCube (pi : PixImageCube) (info : TextureParams) (device : Device) =
            let face0 = pi.MipMapArray.[0]
            if face0.LevelCount <= 0 then failf "empty PixImageMipMap"

            let format = face0.ImageArray.[0].PixFormat
            let size = face0.ImageArray.[0].Size

            let format = device.GetSupportedFormat(VkImageTiling.Optimal, format, info)
            let expectedFormat = PixFormat(VkFormat.expectedType format, VkFormat.toColFormat format)

            let uploadLevels =
                if info.wantMipMaps then face0.LevelCount
                else 1

            let mipMapLevels =
                if info.wantMipMaps then
                    if TextureFormat.isFilterable (VkFormat.toTextureFormat format) then
                        Fun.MipmapLevels(size)
                    else
                        uploadLevels
                else
                    1

            let generateMipMaps =
                uploadLevels < mipMapLevels

            let image =
                Image.create
                    size.XYI
                    mipMapLevels 6 1
                    TextureDimension.TextureCube
                    format
                    (VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.SampledBit ||| VkImageUsageFlags.StorageBit)
                    device

            let tempImages =
                List.init uploadLevels (fun level ->
                    List.init 6 (fun face ->
                        let data = pi.MipMapArray.[face].ImageArray.[level]
                        let temp = device.CreateTensorImage(V3i(data.Size.X, data.Size.Y, 1), expectedFormat, info.wantSrgb)
                        temp.Write(data, true)
                        temp
                    )
                )

            match device.UploadMode with
            | UploadMode.Async ->
                let imageRange = image.[TextureAspect.Color]
                image.Layout <- VkImageLayout.TransferDstOptimal
                device.CopyEngine.EnqueueSafe [
                    yield CopyCommand.TransformLayout(imageRange, VkImageLayout.Undefined, VkImageLayout.TransferDstOptimal)

                    for (level, faces) in Seq.indexed tempImages do
                        for (face, temp) in Seq.indexed faces do
                            yield CopyCommand.Copy(temp, image.[TextureAspect.Color, level, face])

                    yield CopyCommand.SyncImage(imageRange, VkImageLayout.TransferDstOptimal, VkAccessFlags.TransferWriteBit)

                    yield CopyCommand.Release(imageRange, VkImageLayout.TransferDstOptimal, device.GraphicsFamily)
                    yield CopyCommand.Callback (fun () -> tempImages |> List.iter (List.iter Disposable.dispose))
                ]

                device.eventually {
                    do! Command.Acquire(imageRange, VkImageLayout.TransferDstOptimal, device.TransferFamily)
                    // generate the mipMaps
                    if generateMipMaps then
                        do! Command.GenerateMipMaps(image.[TextureAspect.Color], uploadLevels - 1)

                    do! Command.TransformLayout(image, VkImageLayout.ShaderReadOnlyOptimal)
                }

            | _ ->
                device.eventually {
                    try
                        do! Command.TransformLayout(image, VkImageLayout.TransferDstOptimal)

                        // upload the levels
                        for (level, faces) in Seq.indexed tempImages do
                            for (face, temp) in Seq.indexed faces do
                                do! Command.Copy(temp, image.[TextureAspect.Color, level, face])

                        // generate the mipMaps
                        if mipMapLevels > 1 then
                            do! Command.GenerateMipMaps(image.[TextureAspect.Color], uploadLevels - 1)

                        do! Command.TransformLayout(image, VkImageLayout.ShaderReadOnlyOptimal)

                    finally
                        tempImages |> List.iter (List.iter Disposable.dispose)
                }

            image

        let ofNativeTexture (texture : INativeTexture) (device : Device) =
            let size = texture.[0, 0].Size
            let format = VkFormat.ofTextureFormat texture.Format

            let uploadLevels =
                if texture.WantMipMaps then texture.MipMapLevels
                else 1

            let mipMapLevels =
                if texture.WantMipMaps then
                    if texture.Format.IsFilterable then
                        Fun.MipmapLevels(size)
                    else
                        uploadLevels
                else
                    1

            let generateMipMaps =
                uploadLevels < mipMapLevels

            let isCubeOr2D =
                match texture.Dimension with
                | TextureDimension.Texture2D | TextureDimension.TextureCube -> true
                | _ -> false

            let image =
                Image.create size mipMapLevels texture.Count 1
                       texture.Dimension format
                       (VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.SampledBit ||| VkImageUsageFlags.StorageBit)
                       device

            let tempImages =
                let compression = texture.Format.CompressionMode
                let blockSize = compression |> CompressionMode.blockSize

                Array.init texture.Count (fun slice ->
                    Array.init uploadLevels (fun level ->
                        let data = texture.[slice, level]
                        let buffer = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferSrcBit data.SizeInBytes

                        buffer.Memory.Mapped (fun dst ->
                            data.Use (fun src ->
                                if compression <> CompressionMode.None && isCubeOr2D then
                                    BlockCompression.mirrorCopy compression data.Size.XY src dst
                                else
                                    Marshal.Copy(src, dst, data.SizeInBytes)
                            )
                        )

                        let alignedSize =
                            let blocks = compression |> CompressionMode.numberOfBlocks data.Size
                            blocks * blockSize

                        buffer, data.Size, alignedSize
                    )
                )

            match device.UploadMode with
            | UploadMode.Async ->
                let imageRange = image.[TextureAspect.Color]
                image.Layout <- VkImageLayout.TransferDstOptimal

                device.CopyEngine.EnqueueSafe [
                    yield CopyCommand.TransformLayout(imageRange, VkImageLayout.Undefined, VkImageLayout.TransferDstOptimal)

                    for slice = 0 to texture.Count - 1 do
                        for level = 0 to uploadLevels - 1 do
                            let src, size, alignedSize = tempImages.[slice].[level]
                            let dst = image.[TextureAspect.Color, level, slice]
                            yield CopyCommand.Copy(src, dst, V3i.Zero, src.Size, alignedSize.XY, size)

                    yield CopyCommand.SyncImage(imageRange, VkImageLayout.TransferDstOptimal, VkAccessFlags.TransferWriteBit)

                    yield CopyCommand.Release(imageRange, VkImageLayout.TransferDstOptimal, device.GraphicsFamily)
                    yield CopyCommand.Callback (fun () -> tempImages |> Array.iter (Array.iter ((fun (d, _, _) -> d.Dispose()))))
                ]

                device.eventually {
                    do! Command.Acquire(imageRange, VkImageLayout.TransferDstOptimal, device.TransferFamily)
                    if generateMipMaps then
                        do! Command.GenerateMipMaps(image.[TextureAspect.Color], uploadLevels - 1)
                    do! Command.TransformLayout(image, VkImageLayout.ShaderReadOnlyOptimal)
                }

            | _ ->
                device.eventually {
                    try
                        do! Command.TransformLayout(image, VkImageLayout.TransferDstOptimal)

                        for slice = 0 to texture.Count - 1 do
                            for level = 0 to uploadLevels - 1 do
                                let src, size, alignedSize = tempImages.[slice].[level]
                                let dst = image.[TextureAspect.Color, level, slice]
                                do! Command.Copy(src, 0L, alignedSize.XY, dst, V3i.Zero, size)

                        // generate the mipMaps
                        if generateMipMaps then
                            do! Command.GenerateMipMaps(image.[TextureAspect.Color], uploadLevels - 1)

                        do! Command.TransformLayout(image, VkImageLayout.ShaderReadOnlyOptimal)

                    finally
                        tempImages |> Array.iter (Array.iter (fun (d, _, _) -> d.Dispose()))
                }

            image

        let ofStream (stream : IO.Stream) (info : TextureParams) (device : Device) =
            let temp = device |> TensorImage.ofStream stream info.wantSrgb
            let size = temp.Size

            let mipMapLevels =
                if info.wantMipMaps && TextureFormat.isFilterable (VkFormat.toTextureFormat temp.ImageFormat) then
                    Fun.MipmapLevels(size)
                else
                    1

            let image =
                Image.create
                    size
                    mipMapLevels 1 1
                    TextureDimension.Texture2D
                    temp.ImageFormat
                    (VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.SampledBit ||| VkImageUsageFlags.StorageBit)
                    device

            match device.UploadMode with
            | UploadMode.Async ->
                let imageRange = image.[TextureAspect.Color]

                image.Layout <- VkImageLayout.TransferDstOptimal
                device.CopyEngine.EnqueueSafe [
                    CopyCommand.TransformLayout(imageRange, VkImageLayout.Undefined, VkImageLayout.TransferDstOptimal)
                    CopyCommand.Copy(temp, imageRange.[0,0])
                    CopyCommand.SyncImage(imageRange, VkImageLayout.TransferDstOptimal, VkAccessFlags.TransferWriteBit)

                    CopyCommand.Release(imageRange, VkImageLayout.TransferDstOptimal, device.GraphicsFamily)
                    CopyCommand.Callback(fun () -> temp.Dispose())
                ]

                device.eventually {
                    do! Command.Acquire(imageRange, VkImageLayout.TransferDstOptimal, device.TransferFamily)

                    // generate the mipMaps
                    if info.wantMipMaps then
                        do! Command.GenerateMipMaps image.[TextureAspect.Color]

                    do! Command.TransformLayout(image, VkImageLayout.ShaderReadOnlyOptimal)

                }


            | _ ->
                device.eventually {
                    try
                        do! Command.TransformLayout(image, VkImageLayout.TransferDstOptimal)

                        // upload the levels
                        do! Command.Copy(temp, image.[TextureAspect.Color, 0, 0])

                        // generate the mipMaps
                        if info.wantMipMaps then
                            do! Command.GenerateMipMaps image.[TextureAspect.Color]

                        do! Command.TransformLayout(image, VkImageLayout.ShaderReadOnlyOptimal)

                    finally
                        temp.Dispose()
                }
            image

        let ofFile (path : string) (info : TextureParams) (device : Device) =
            use stream = IO.File.OpenRead(path)
            ofStream stream info device

        let rec ofTexture (t : ITexture) (device : Device) : Image =
            match t with
            | :? PixTexture2d as t ->
                device |> ofPixImageMipMap t.PixImageMipMap t.TextureParams

            | :? PixTextureCube as c ->
                device |> ofPixImageCube c.PixImageCube c.TextureParams

            | :? NullTexture as t ->
                device |> ofPixImageMipMap (PixImageMipMap [| PixImage<byte>(Col.Format.RGBA, V2i.II) :> PixImage |]) TextureParams.empty

            | :? PixTexture3d as t ->
                device |> ofPixVolume t.PixVolume t.TextureParams

            | :? StreamTexture as t ->
                use stream = t.Open()

                // Always try to load compressed data first
                let compressed =
                    stream |> DdsTexture.tryLoadCompressedFromStream

                match compressed with
                | Some t ->
                    device |> ofTexture t

                | _ ->
                    device |> ofStream stream t.TextureParams

            | :? INativeTexture as nt ->
                device |> ofNativeTexture nt

            | :? Image as t ->
                t.AddReference()
                t

            | _ ->
                failf "unsupported texture-type: %A" t

        let uploadLevel (offset : V3i) (size : V3i) (src : NativeTensor4<'T>) (dst : ImageSubresource) (device : Device) =
            if dst.Image.Samples > 1 then
                raise <| InvalidOperationException("Cannot upload to multisampled image")

            let format = dst.Image.Format
            let dstPixFormat = PixFormat(VkFormat.expectedType format, VkFormat.toColFormat format)

            let src, offset =
                if dst.Image.IsCubeOr2D then
                    src.SubTensor4(V4l.Zero, V4l(V3l size, src.SW)).MirrorY(),
                    V3i(offset.X, dst.Size.Y - offset.Y - int src.SY, offset.Z) // flip y-offset
                else
                    src, offset

            let temp = device.CreateTensorImage(V3i src.Size, dstPixFormat, VkFormat.isSrgb dst.Image.Format)
            temp.Write(src.Format, src)

            let layout = dst.Image.Layout
            device.eventually {
                try
                    do! Command.TransformLayout(dst.Image, VkImageLayout.TransferDstOptimal)
                    do! Command.Copy(temp, dst, offset, temp.Size)
                    do! Command.TransformLayout(dst.Image, layout)
                finally
                    temp.Dispose()
            }


    [<AbstractClass; Sealed; Extension>]
    type DeviceImageUploadExtensions private() =

        [<Extension>]
        static member inline CreateImage(this : Device, pi : PixImageMipMap, info : TextureParams) =
            this |> Image.ofPixImageMipMap pi info

        [<Extension>]
        static member inline CreateImage(this : Device, file : string, info : TextureParams) =
            this |> Image.ofFile file info

        [<Extension>]
        static member inline CreateImage(this : Device, t : ITexture) =
            this |> Image.ofTexture t

        [<Extension>]
        static member inline UploadLevel(this : Device, dst : ImageSubresource, src : NativeTensor4<'T>, offset : V3i, size : V3i) =
            this |> Image.uploadLevel offset size src dst