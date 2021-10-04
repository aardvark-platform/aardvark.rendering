namespace Aardvark.Rendering.GL

open Aardvark.Base
open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4
open Microsoft.FSharp.NativeInterop
open System.Runtime.CompilerServices
open Aardvark.Rendering.GL

#nowarn "9"

[<AutoOpen>]
module internal TextureDownloadImplementation =

    [<AutoOpen>]
    module private Utilities =

        let getOffsetAndSlice (texture : Texture) (offset : V3i) =
            match texture.Dimension with
            | TextureDimension.Texture1D -> V3i(offset.X, 0, 0), offset.Y
            | _ -> offset, offset.Z

        let getArrayDimension (texture : Texture) =
            getOffsetAndSlice texture >> snd

    module Texture =

        let private readTextureLayer (texture : Texture) (target : TextureTarget) (level : int)
                                     (offset : V3i) (pixels : nativeint) (info : GeneralPixelDataInfo) (data : GeneralPixelData) =

            let fbo = GL.GenFramebuffer()
            GL.Check "could not create framebuffer"

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fbo)
            GL.Check "could not bind framebuffer"

            let offset, baseSlice =
                getOffsetAndSlice texture offset

            let count =
                data.Size |> getArrayDimension texture

            // Size of a slice in the output data
            let bytesPerSlice =
                info.ElementSize * info.Channels * data.Size.X * data.Size.Y

            let attachment, readBuffer =
                if TextureFormat.isDepthStencil texture.Format then
                    FramebufferAttachment.DepthStencilAttachment, ReadBufferMode.None
                elif TextureFormat.isDepth texture.Format then
                    FramebufferAttachment.DepthAttachment, ReadBufferMode.None
                else
                    FramebufferAttachment.ColorAttachment0, ReadBufferMode.ColorAttachment0

            GL.ReadBuffer(readBuffer)
            GL.Check "could not set readbuffer"

            for i = 0 to count - 1 do
                match texture.Dimension, texture.IsArray with
                | TextureDimension.Texture1D, true
                | TextureDimension.Texture2D, true
                | TextureDimension.TextureCube, true
                | TextureDimension.Texture3D, false ->
                    GL.FramebufferTextureLayer(FramebufferTarget.ReadFramebuffer, attachment, texture.Handle, level, baseSlice + i)

                | TextureDimension.Texture1D, false ->
                    GL.FramebufferTexture1D(FramebufferTarget.ReadFramebuffer, attachment, target, texture.Handle, level)

                | TextureDimension.Texture2D, false
                | TextureDimension.TextureCube, false ->
                    GL.FramebufferTexture2D(FramebufferTarget.ReadFramebuffer, attachment, target, texture.Handle, level)

                | _ ->
                    failwithf "[GL] cannot attach %A%s to framebuffer" texture.Dimension (if texture.IsArray then "[]" else "")

                GL.Check "could not attach texture to framebuffer"

                let fboCheck = GL.CheckFramebufferStatus(FramebufferTarget.ReadFramebuffer)
                if fboCheck <> FramebufferErrorCode.FramebufferComplete then
                    failwithf "could not create input framebuffer: %A" fboCheck

                let dstOffset = nativeint <| i * bytesPerSlice
                GL.ReadPixels(offset.X, offset.Y, data.Size.X, data.Size.Y, data.Format, data.Type, pixels + dstOffset)
                GL.Check "could not read pixels"

            GL.ReadBuffer(ReadBufferMode.None)
            GL.Check "could not unset readbuffer"

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0)
            GL.Check "could not unbind framebuffer"

            GL.DeleteFramebuffer(fbo)
            GL.Check "could not delete framebuffer"

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
                            (i, d) ||> readTextureLayer texture targetSlice level offset src

                        | _ ->
                            failwithf "[GL] Cannot download subwindow of compressed textures without glGetCompressedTextureSubImage (not available)"

                GL.Check "could not get texture image"

                (info, data) ||> PixelData.copy src

            finally
                NativePtr.free buffer

            GL.BindTexture(target, 0)
            GL.Check "could not unbind texture"

        let downloadNativeTensor4<'T when 'T : unmanaged> (texture : Texture) (level : int) (offset : V3i)
                                                          (size : V3i) (dst : NativeTensor4<'T>) =

            let pixelFormat, pixelType =
                PixFormat.toFormatAndType texture.Format dst.PixFormat

            let flipY =
                Texture.shouldFlipY texture

            let offset =
                if flipY then offset |> Texture.flipOffsetY texture level size
                else offset

            let copy (channels : int) (elementSize : int) (alignedLineSize : nativeint) (sizeInBytes : nativeint) (src : nativeint) =
                let srcTensor =
                    let dy = int64 alignedLineSize / int64 elementSize
                    let info =
                        Tensor4Info(
                            0L,
                            V4l(int64 size.X, int64 size.Y, int64 size.Z, dst.SW),
                            V4l(int64 channels, dy, dy * int64 size.Y, 1L)
                        )
                    NativeTensor4<'T>(NativePtr.ofNativeInt src, info)

                let dst = dst.SubTensor4(V4i.Zero, V4i(size, channels))
                NativeTensor4.copy (if flipY then srcTensor.MirrorY() else srcTensor) dst

            let pixelData =
                PixelData.General {
                    Size   = size
                    Type   = pixelType
                    Format = pixelFormat
                    Copy   = copy
                }

            downloadPixelData texture level offset pixelData

        let downloadPixImage (texture : Texture) (level : int) (slice : int) (offset : V2i) (image : PixImage) =
            let pixelFormat, pixelType =
                PixFormat.toFormatAndType texture.Format image.PixFormat

            let offset =
                offset |> Texture.flipOffsetY2D texture level image.Size

            let copy (channels : int) (elementSize : int) (alignedLineSize : nativeint) (sizeInBytes : nativeint) (src : nativeint) =
                let srcInfo =
                    let viSize = V3l(int64 image.Size.X, int64 image.Size.Y, int64 channels)
                    VolumeInfo(
                        int64 alignedLineSize * (int64 image.Size.Y - 1L),
                        viSize,
                        V3l(int64 channels * int64 elementSize, int64 -alignedLineSize, int64 elementSize)
                    )

                TextureCopyUtils.Copy(src, srcInfo, image)

            let offset = V3i(offset, slice)
            let data =
                PixelData.General {
                    Size   = V3i(image.Size, 1)
                    Type   = pixelType
                    Format = pixelFormat
                    Copy   = copy
                }

            downloadPixelData texture level offset data

        let downloadPixVolume (texture : Texture) (level : int) (offset : V3i) (volume : PixVolume) =
            let pixelFormat, pixelType =
                PixFormat.toFormatAndType texture.Format volume.PixFormat

            let copy (channels : int) (elementSize : int) (alignedLineSize : nativeint) (sizeInBytes : nativeint) (src : nativeint) =
                let srcInfo =
                    let rowPixels = int64 alignedLineSize / int64 elementSize
                    let tiSize = V4l(volume.SizeL, int64 channels)

                    Tensor4Info(
                        0L,
                        tiSize,
                        V4l(int64 channels, rowPixels, rowPixels * tiSize.Y, 1L)
                    )

                TextureCopyUtils.Copy(src, srcInfo, volume)

            let data =
                  PixelData.General {
                      Size   = volume.Size
                      Type   = pixelType
                      Format = pixelFormat
                      Copy   = copy
                  }

            downloadPixelData texture level offset data


[<AutoOpen>]
module ContextTextureDownloadExtensions =

    let private resolve (src : Texture) (level : int) (slice : int) (offset : V2i) (size : V2i) (f : Texture -> unit) =
        let context = src.Context
        let temp = context.CreateTexture2D(size, 1, src.Format, 1)
        try
            context.Blit(src, level, slice, offset, size, temp, 0, 0, V2i.Zero, size, true)
            f temp
        finally
            context.Delete(temp)

    [<Extension; AbstractClass; Sealed>]
    type ContextTextureDownloadExtensions =

        [<Extension>]
        static member Download(this : Context, texture : Texture, level : int,
                               offset : V3i, size : V3i, target : NativeTensor4<'T>) =
            using this.ResourceLock (fun _ ->
                // Multisampled texture requires blit
                if texture.IsMultisampled then
                    resolve texture level offset.Z offset.XY size.XY (fun temp ->
                        Texture.downloadNativeTensor4 temp 0 offset size target
                    )

                // Download directly
                else
                    Texture.downloadNativeTensor4 texture level offset size target
            )

        [<Extension>]
        static member Download(this : Context, texture : Texture, level : int, size : V3i, target : NativeTensor4<'T>) =
            this.Download(texture, level, V3i.Zero, size, target)

        [<Extension>]
        static member Download(this : Context, texture : Texture, level : int, slice : int, offset : V2i, target : PixImage) =
            using this.ResourceLock (fun _ ->
                // Multisampled texture requires resolve
                if texture.IsMultisampled then
                    resolve texture level slice offset target.Size (fun resolved ->
                        Texture.downloadPixImage resolved 0 0 V2i.Zero target
                    )

                // Download directly
                else
                    Texture.downloadPixImage texture level slice offset target
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
            using this.ResourceLock (fun _ ->
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
            let image =
                let img : PixImage<int> = PixImage<int>()
                img.Volume <- target.AsVolume()
                img.Format <- Col.Format.Stencil
                img

            this.Download(texture, level, slice, offset, image)

        [<Extension>]
        static member DownloadStencil(this : Context, texture : Texture, level : int, slice : int, target : Matrix<int>) =
            this.DownloadStencil(texture, level, slice, V2i.Zero, target)

        [<Extension>]
        static member DownloadDepth(this : Context, texture : Texture,
                                    level : int, slice : int, offset : V2i, target : Matrix<float32>) =
            let image =
                let img : PixImage<float32> = PixImage<float32>()
                img.Volume <- target.AsVolume()
                img.Format <- Col.Format.Depth
                img

            this.Download(texture, level, slice, offset, image)

        [<Extension>]
        static member DownloadDepth(this : Context, texture : Texture, level : int, slice : int, target : Matrix<float32>) =
            this.DownloadDepth(texture, level, slice, V2i.Zero, target)