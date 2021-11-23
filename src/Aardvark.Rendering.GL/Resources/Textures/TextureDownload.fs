namespace Aardvark.Rendering.GL

open System
open Aardvark.Base
open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4
open Microsoft.FSharp.NativeInterop
open System.Runtime.CompilerServices
open Aardvark.Rendering.GL

#nowarn "9"

[<AutoOpen>]
module internal TextureDownloadImplementation =

    module Texture =

        let private readTextureLayer (texture : Texture) (level : int) (offset : V3i)
                                     (pixels : nativeint) (info : GeneralPixelDataInfo) (data : GeneralPixelData) =

            // Size of a slice in the output data
            let bytesPerSlice =
                info.ElementSize * info.Channels * data.Size.X * data.Size.Y

            let image = Image.Texture texture

            Image.readLayers image level offset.Z data.Size.Z (fun slice ->
                let dstOffset =
                    let index = slice - offset.Z
                    nativeint <| index * bytesPerSlice

                GL.ReadPixels(offset.X, offset.Y, data.Size.X, data.Size.Y, data.Format, data.Type, pixels + dstOffset)
                GL.Check "could not read pixels"
            )

        let downloadPixelData (texture : Texture) (level : int) (offset : V3i) (data : PixelData) =
            let target = texture |> TextureTarget.ofTexture
            let targetSlice = target |> TextureTarget.toSliceTarget offset.Z

            let info =
                data |> PixelData.getInfo texture.Context.PackAlignment

            GL.BindTexture(target, texture.Handle)
            GL.Check "could not bind texture"

            let buffer = NativePtr.alloc<byte> (int info.SizeInBytes)

            try
                let src = NativePtr.toNativeInt buffer

                // In case we download the whole texture and it isn't arrayed, we can
                // avoid GL.GetTextureSubImage() which is not available on all systems! (e.g. MacOS)
                if offset = V3i.Zero && data.Size = texture.GetSize(level) && not texture.IsArray then
                    match data with
                    | PixelData.General d ->
                        GL.GetTexImage(targetSlice, level, d.Format, d.Type, src)
                    | PixelData.Compressed _ ->
                        GL.GetCompressedTexImage(targetSlice, level, src)

                else
                    if GL.ARB_get_texture_subimage then
                        let offset =
                            if texture.Dimension = TextureDimension.Texture1D then offset.XZO
                            else offset

                        match data with
                        | PixelData.General d ->
                            GL.GetTextureSubImage(texture.Handle, level, offset, d.Size, d.Format, d.Type, int info.SizeInBytes, src)
                        | PixelData.Compressed d ->
                            GL.GetCompressedTextureSubImage(texture.Handle, level, offset, d.Size, int info.SizeInBytes, src)

                    // We want to download a subregion but don't have GL_ARB_get_texture_subimage
                    // Use readPixels with FBO as fallback
                    else
                        match data, info with
                        | PixelData.General d, PixelDataInfo.General i ->
                            (i, d) ||> readTextureLayer texture level offset src

                        | _ ->
                            failwithf "[GL] Cannot download subwindow of compressed textures without glGetCompressedTextureSubImage (not available)"

                GL.Check "could not get texture image"

                (info, data) ||> PixelData.copy src

            finally
                NativePtr.free buffer

            GL.BindTexture(target, 0)
            GL.Check "could not unbind texture"

        let private downloadGeneralPixelData (texture : Texture) (level : int) (slice : int) (offset : V3i) (size : V3i)
                                             (pixelFormat : PixelFormat) (pixelType : PixelType)
                                             (copy : int -> int -> nativeint -> nativeint -> nativeint -> unit) =
            let offset =
                let flipped = texture.WindowOffset(level, offset, size)
                if texture.Dimension = TextureDimension.Texture3D then flipped
                else V3i(flipped.XY, slice)

            let pixelData =
                PixelData.General {
                    Size   = size
                    Type   = pixelType
                    Format = pixelFormat
                    Copy   = copy
                }

            downloadPixelData texture level offset pixelData

        let downloadNativeTensor4<'T when 'T : unmanaged> (texture : Texture) (level : int) (slice : int)
                                                          (offset : V3i) (size : V3i) (dst : NativeTensor4<'T>) =

            let pixelFormat, pixelType =
                PixFormat.toFormatAndType texture.Format dst.PixFormat

            let copy (channels : int) (elementSize : int) (alignedLineSize : nativeint) (sizeInBytes : nativeint) (src : nativeint) =
                let srcTensor =
                    let info =
                        let rowPixels = alignedLineSize / nativeint elementSize
                        Tensor4Info.deviceLayout texture.IsCubeOr2D 1 rowPixels channels (V3l size)

                    NativeTensor4<'T>(NativePtr.ofNativeInt src, info)

                let dst = dst.SubTensor4(V4i.Zero, V4i(size, channels))
                NativeTensor4.copy srcTensor dst

            downloadGeneralPixelData texture level slice offset size pixelFormat pixelType copy

        let private downloadPixImageInternal (texture : Texture) (pixelFormat : PixelFormat) (pixelType : PixelType)
                                             (elementType : Option<Type>) (elementOffset : int) (level : int) (slice : int) (offset : V2i)
                                             (image : PixImage) =

            let elementType =
                elementType |> Option.defaultValue image.PixFormat.Type

            let copy (channels : int) (elementSize : int) (alignedLineSize : nativeint) (sizeInBytes : nativeint) (src : nativeint) =
                let srcInfo = VolumeInfo.deviceLayoutWithOffset true elementOffset elementSize alignedLineSize channels image.SizeL
                TextureCopyUtils.Copy(elementType, src, srcInfo, image)

            let size = V3i(image.Size, 1)
            let offset = V3i(offset, slice)
            downloadGeneralPixelData texture level slice offset size pixelFormat pixelType copy

        let downloadPixImage (texture : Texture) (level : int) (slice : int) (offset : V2i) (image : PixImage) =
            let pixelFormat, pixelType =
                PixFormat.toFormatAndType texture.Format image.PixFormat

            downloadPixImageInternal texture pixelFormat pixelType None 0 level slice offset image

        let downloadPixVolume (texture : Texture) (level : int) (offset : V3i) (volume : PixVolume) =
            let pixelFormat, pixelType =
                PixFormat.toFormatAndType texture.Format volume.PixFormat

            let copy (channels : int) (elementSize : int) (alignedLineSize : nativeint) (sizeInBytes : nativeint) (src : nativeint) =
                let srcInfo =
                    let rowPixels = alignedLineSize / nativeint elementSize
                    Tensor4Info.deviceLayout texture.IsCubeOr2D 1 rowPixels channels volume.SizeL

                TextureCopyUtils.Copy(src, srcInfo, volume)

            downloadGeneralPixelData texture level 0 offset volume.Size pixelFormat pixelType copy

        let downloadDepth (texture : Texture) (level : int) (slice : int) (offset : V2i) (matrix : Matrix<float32>) =
            match texture.Format with
            | TextureFormat.DepthComponent16
            | TextureFormat.DepthComponent24
            | TextureFormat.DepthComponent32
            | TextureFormat.DepthComponent32f ->
                let image =
                    let img : PixImage<float32> = PixImage<float32>()
                    img.Volume <- matrix.AsVolume()
                    img.Format <- Col.Format.Depth
                    img

                downloadPixImage texture level slice offset image

            | TextureFormat.Depth24Stencil8 ->
                let copy (channels : int) (elementSize : int) (alignedLineSize : nativeint) (sizeInBytes : nativeint) (src : nativeint) =
                    let srcInfo =
                        let rowPixels = alignedLineSize / nativeint elementSize
                        VolumeInfo.deviceLayout true 1 rowPixels channels matrix.Size

                    let vSrc = NativeVolume<uint32>(NativePtr.ofNativeInt src, srcInfo)

                    NativeVolume.using (matrix.AsVolume()) (fun vDst ->
                        (vSrc, vDst) ||> NativeVolume.copyWith (fun value ->
                            float32 (value >>> 8) / 16777215.0f // Upper 24bit divided by 2^24 - 1
                        )
                    )

                let pixelFormat = PixelFormat.DepthStencil
                let pixelType = PixelType.UnsignedInt248

                let size = V3i(V2i matrix.Size, 1)
                let offset = V3i(offset, slice)
                downloadGeneralPixelData texture level slice offset size pixelFormat pixelType copy

            | TextureFormat.Depth32fStencil8 ->
                let image =
                    let img : PixImage<float32> = PixImage<float32>()
                    img.Volume <- matrix.AsVolume()
                    img.Format <- Col.Format.Depth
                    img

                let pf = PixelFormat.DepthStencil
                let pt = PixelType.Float32UnsignedInt248Rev
                downloadPixImageInternal texture pf pt None 0 level slice offset image

            | fmt -> failwithf "[GL] %A is not a supported depth format" fmt

        let downloadStencil (texture : Texture) (level : int) (slice : int) (offset : V2i) (matrix : Matrix<int>) =
            match texture.Format with
            | TextureFormat.StencilIndex8 ->
                let info = VolumeInfo(0L, V3l (matrix.Size, 1L), V3l (4L, 4L * matrix.SX, 1L))
                let volume = Volume<uint8>(matrix.Array.UnsafeCoerce<uint8>(), info)

                let image =
                    let img : PixImage<uint8> = PixImage<uint8>()
                    img.Volume <- volume
                    img.Format <- Col.Format.Stencil
                    img

                downloadPixImage texture level slice offset image
                matrix.Array.UnsafeCoerce<int>() |> ignore

            | TextureFormat.Depth24Stencil8
            | TextureFormat.Depth32fStencil8 ->
                let image =
                    let img : PixImage<int> = PixImage<int>()
                    img.Volume <- matrix.AsVolume()
                    img.Format <- Col.Format.Stencil
                    img

                let pt, elementOffset =
                    if texture.Format = TextureFormat.Depth24Stencil8 then PixelType.UnsignedInt248, 0
                    else PixelType.Float32UnsignedInt248Rev, 4

                let pf = PixelFormat.DepthStencil
                let elementType = Some typeof<uint8>
                downloadPixImageInternal texture pf pt elementType elementOffset level slice offset image

            | fmt -> failwithf "[GL] %A is not a supported stencil format" fmt


[<AutoOpen>]
module ContextTextureDownloadExtensions =

    let private download (texture : Texture) (level : int) (slice : int) (offset : V3i) (size : V3i)
                         (dl : int -> int -> V3i -> V3i -> Texture -> unit) =
        let context = texture.Context

        using context.ResourceLock (fun _ ->
            // Multisampled texture requires blit
            if texture.IsMultisampled then
                let temp = context.CreateTexture2D(size.XY, 1, texture.Format, 1)
                try
                    context.Blit(texture, level, slice, offset.XY, size.XY, temp, 0, 0, V2i.Zero, size.XY, false)
                    temp |> dl 0 0 V3i.Zero size
                finally
                    context.Delete(temp)

            // Download directly
            else
                texture |> dl level slice offset size
        )

    [<Extension; AbstractClass; Sealed>]
    type ContextTextureDownloadExtensions =

        [<Extension>]
        static member Download(this : Context, texture : Texture, level : int, slice : int,
                               offset : V3i, size : V3i, target : NativeTensor4<'T>) =
            download texture level slice offset size (fun level slice offset size texture ->
                Texture.downloadNativeTensor4 texture level slice offset size target
            )

        [<Extension>]
        static member Download(this : Context, texture : Texture, level : int, slice : int, size : V3i, target : NativeTensor4<'T>) =
            this.Download(texture, level, slice, V3i.Zero, size, target)

        [<Extension>]
        static member Download(this : Context, texture : Texture, level : int, slice : int, offset : V2i, target : PixImage) =
            download texture level slice (V3i offset) (V3i target.Size) (fun level slice offset _ texture ->
                Texture.downloadPixImage texture level slice offset.XY target
            )

        [<Extension>]
        static member Download(this : Context, texture : Texture, level : int, slice : int, target : PixImage) =
            this.Download(texture, level, slice, V2i.Zero, target)

        [<Extension>]
        static member Download(this : Context, texture : Texture, level : int, slice : int) : PixImage =
            let fmt = TextureFormat.toDownloadFormat texture.Format
            let levelSize = texture.GetSize level
            let img = PixImage.Create(fmt, int64 levelSize.X, int64 levelSize.Y)
            this.Download(texture, level, slice, img)
            img

        [<Extension>]
        static member Download(this : Context, texture : Texture, level : int) : PixImage =
            this.Download(texture, level, 0)

        [<Extension>]
        static member Download(this : Context, texture : Texture) : PixImage =
            this.Download(texture, 0, 0)

        [<Extension>]
        static member Download(this : Context, texture : Texture, level : int, offset : V3i, target : PixVolume) =
            download texture level 0 offset target.Size (fun level _ offset _ texture ->
                Texture.downloadPixVolume texture level offset target
            )

        [<Extension>]
        static member Download(this : Context, texture : Texture, level : int, target : PixVolume) =
            this.Download(texture, level, V3i.Zero, target)

        [<Extension>]
        static member Download(this : Context, texture : Texture, target : PixVolume) =
            this.Download(texture, 0, V3i.Zero, target)

        [<Extension>]
        static member DownloadStencil(this : Context, texture : Texture,
                                      level : int, slice : int, offset : V2i, target : Matrix<int>) =
            download texture level slice (V3i offset) (V3i target.Size) (fun level slice offset _ texture ->
                Texture.downloadStencil texture level slice offset.XY target
            )

        [<Extension>]
        static member DownloadStencil(this : Context, texture : Texture, level : int, slice : int, target : Matrix<int>) =
            this.DownloadStencil(texture, level, slice, V2i.Zero, target)

        [<Extension>]
        static member DownloadDepth(this : Context, texture : Texture,
                                    level : int, slice : int, offset : V2i, target : Matrix<float32>) =
            download texture level slice (V3i offset) (V3i target.Size) (fun level slice offset _ texture ->
                Texture.downloadDepth texture level slice offset.XY target
            )

        [<Extension>]
        static member DownloadDepth(this : Context, texture : Texture, level : int, slice : int, target : Matrix<float32>) =
            this.DownloadDepth(texture, level, slice, V2i.Zero, target)