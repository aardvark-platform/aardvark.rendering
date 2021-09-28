namespace Aardvark.Rendering.GL

open Aardvark.Base
open Aardvark.Base.NativeTensors
open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4
open Microsoft.FSharp.NativeInterop
open System.Runtime.CompilerServices
open Aardvark.Rendering.GL

#nowarn "9"

[<AutoOpen>]
module internal TextureDownloadImplementation =

    module Texture =
        let private readTexture2D (texture : Texture) (targetSlice : TextureTarget) (level : int) (slice : int)
                                  (offset : V2i) (size : V2i) (pixelFormat : PixelFormat)
                                  (pixelType : PixelType) (dst : nativeint) =
            let fbo = GL.GenFramebuffer()
            GL.Check "could not create framebuffer"

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fbo)
            GL.Check "could not bind framebuffer"

            let attachment, readBuffer =
                if TextureFormat.isDepthStencil texture.Format then
                    FramebufferAttachment.DepthStencilAttachment, ReadBufferMode.None
                elif TextureFormat.isDepth texture.Format then
                    FramebufferAttachment.DepthAttachment, ReadBufferMode.None
                else
                    FramebufferAttachment.ColorAttachment0, ReadBufferMode.ColorAttachment0

            if texture.IsArray then
                GL.FramebufferTextureLayer(FramebufferTarget.ReadFramebuffer, attachment, texture.Handle, level, slice)
            else
                GL.FramebufferTexture2D(FramebufferTarget.ReadFramebuffer, attachment, targetSlice, texture.Handle, level)

            GL.Check "could not attach texture to framebuffer"

            let fboCheck = GL.CheckFramebufferStatus(FramebufferTarget.ReadFramebuffer)
            if fboCheck <> FramebufferErrorCode.FramebufferComplete then
                failwithf "could not create input framebuffer: %A" fboCheck

            GL.ReadBuffer(readBuffer)
            GL.Check "could not set readbuffer"

            GL.ReadPixels(offset.X, offset.Y, size.X, size.Y, pixelFormat, pixelType, dst)
            GL.Check "could not read pixels"

            GL.ReadBuffer(ReadBufferMode.None)
            GL.Check "could not unset readbuffer"

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0)
            GL.Check "could not unbind framebuffer"

            GL.DeleteFramebuffer(fbo)
            GL.Check "could not delete framebuffer"


        let download2D (texture : Texture) (level : int) (slice : int) (offset : V2i) (image : PixImage) =
            let target = texture |> TextureTarget.ofTexture
            let targetSlice = target |> TextureTarget.toSliceTarget slice
            GL.BindTexture(target, texture.Handle)
            GL.Check "could not bind texture"

            let pixelFormat, pixelType =
                PixFormat.toFormatAndType texture.Format image.PixFormat

            let elementSize = pixelType.Size
            let channelCount = pixelFormat.Channels

            let lineSize = image.Size.X * channelCount * elementSize
            let packAlign = texture.Context.PackAlignment

            let alignedLineSize = (lineSize + (packAlign - 1)) &&& ~~~(packAlign - 1)
            let targetSize = alignedLineSize * image.Size.Y

            let buffer = NativePtr.alloc<byte> targetSize

            try
                let src = NativePtr.toNativeInt buffer

                // In case we download the whole texture and it isn't arrayed, we can
                // avoid GL.GetTextureSubImage() which is not available on all systems! (e.g. MacOS)
                if offset = V2i.Zero && image.Size = texture.GetSize(level).XY && not texture.IsArray then
                    GL.GetTexImage(targetSlice, level, pixelFormat, pixelType, src)
                else
                    if GL.ARB_get_texture_subimage then
                        GL.GetTextureSubImage(texture.Handle, level, offset.X, offset.Y, slice,
                                                image.Size.X, image.Size.Y, 1,
                                                pixelFormat, pixelType, targetSize, src)

                    // Use readPixels with FBO as fallback
                    else
                        readTexture2D texture targetSlice level slice offset image.Size pixelFormat pixelType src

                GL.Check "could not get texture image"

                let dstInfo = image.VolumeInfo
                let dy = int64(alignedLineSize / elementSize)
                let srcInfo =
                    VolumeInfo(
                        dy * (dstInfo.Size.Y - 1L),
                        dstInfo.Size,
                        V3l(dstInfo.SZ, -dy, 1L)
                    )

                NativeVolume.copyNativeToImage src srcInfo image

            finally
                NativePtr.free buffer

            GL.BindTexture(target, 0)
            GL.Check "could not unbind texture"

        let download (texture : Texture) (level : int) (slice : int) (offset : V2i) (image : PixImage) =
            match texture.Dimension with
            | TextureDimension.Texture2D
            | TextureDimension.TextureCube ->
                download2D texture level slice offset image

            | _ ->
                failwithf "cannot download textures of kind: %A" texture.Dimension

[<AutoOpen>]
module ContextTextureDownloadExtensions =

    [<Extension; AbstractClass; Sealed>]
    type ContextTextureDownloadExtensions =

        [<Extension>]
        static member Download(this : Context, texture : Texture, level : int, slice : int, offset : V2i, target : PixImage) =
            using this.ResourceLock (fun _ ->
                let levelSize = texture.GetSize level
                let offset = V2i(offset.X, levelSize.Y - offset.Y - target.Size.Y) // flip y-offset

                // Multisampled texture requires resolve
                if texture.IsMultisampled then
                    let resolved = this.CreateTexture2D(target.Size, 1, texture.Format, 1)
                    try
                        let region = Box2i.FromMinAndSize(offset, target.Size)
                        this.Blit(texture, level, slice, region, resolved, 0, 0, Box2i(V2i.Zero, target.Size), false)
                        Texture.download2D resolved 0 0 V2i.Zero target
                    finally
                        this.Delete resolved

                // Download directly
                else
                    Texture.download texture level slice offset target
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