namespace Aardvark.Rendering.GL

open Aardvark.Base
open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL

[<RequireQualifiedAccess>]
type internal Image =
    | Texture       of Texture
    | Renderbuffer  of Renderbuffer

    member x.Handle =
        match x with
        | Texture t -> t.Handle
        | Renderbuffer rb -> rb.Handle

    member x.Dimension =
        match x with
        | Texture t -> t.Dimension
        | Renderbuffer _ -> TextureDimension.Texture2D

    member x.Format =
        match x with
        | Texture t -> t.Format
        | Renderbuffer rb -> rb.Format

    member x.Target =
        match x with
        | Texture t -> unbox<ImageTarget> <| TextureTarget.ofTexture t
        | Renderbuffer _ -> ImageTarget.Renderbuffer

    member x.GetSize(level : int) =
        match x with
        | Texture t -> t.GetSize(level)
        | Renderbuffer rb -> V3i(rb.Size, 1)

    member x.Samples =
        match x with
        | Texture t -> t.Multisamples
        | Renderbuffer rb -> rb.Samples

    member x.IsMultisampled =
        x.Samples > 1

    member private x.IsDepth =
        match x with
        | Texture t -> t.Format.IsDepth
        | Renderbuffer rb -> rb.Format.IsDepth

    member private x.IsStencil =
        match x with
        | Texture _ -> false
        | Renderbuffer rb -> rb.Format.IsStencil

    member private x.IsDepthStencil =
        match x with
        | Texture t -> t.Format.IsDepthStencil
        | Renderbuffer rb -> rb.Format.IsDepthStencil

    member x.Attachment =
        if x.IsDepth then FramebufferAttachment.DepthAttachment
        elif x.IsStencil then FramebufferAttachment.StencilAttachment
        elif x.IsDepthStencil then FramebufferAttachment.DepthStencilAttachment
        else FramebufferAttachment.ColorAttachment0

    member x.Mask =
        if x.IsDepth then ClearBufferMask.DepthBufferBit
        elif x.IsStencil then ClearBufferMask.StencilBufferBit
        elif x.IsDepthStencil then ClearBufferMask.DepthBufferBit ||| ClearBufferMask.StencilBufferBit
        else ClearBufferMask.ColorBufferBit

    member x.WindowOffset(level : int, window : Box3i) =
        match x with
        | Image.Texture t -> t.WindowOffset(level, window)
        | Image.Renderbuffer rb -> window |> WindowOffset.flipY rb.Size.Y

    member x.WindowOffset(level : int, offset : V3i, size : V3i) =
        x.WindowOffset(level, Box3i.FromMinAndSize(offset, size))

    member x.WindowOffset(level : int, window : Box2i) =
        let window = Box3i(V3i(window.Min, 0), V3i(window.Max, 1))
        x .WindowOffset(level, window).XY

    member x.WindowOffset(level : int, offset : V2i, size : V2i) =
        x.WindowOffset(level, Box2i.FromMinAndSize(offset, size))

module internal Image =

    /// Attaches an image to the framebuffer bound at the given framebuffer target
    let attach (framebufferTarget : FramebufferTarget) (level : int) (slice : int) (image : Image) =
        let attachment = image.Attachment

        match image with
        | Image.Texture texture ->
            let target = texture |> TextureTarget.ofTexture
            let targetSlice = target |> TextureTarget.toSliceTarget slice

            match texture.Dimension, texture.IsArray with
            | TextureDimension.Texture1D, true
            | TextureDimension.Texture2D, true
            | TextureDimension.TextureCube, true
            | TextureDimension.Texture3D, false ->
                GL.FramebufferTextureLayer(framebufferTarget, attachment, texture.Handle, level, slice)

            | TextureDimension.Texture1D, false ->
                GL.FramebufferTexture1D(framebufferTarget, attachment, targetSlice, texture.Handle, level)

            | TextureDimension.Texture2D, false
            | TextureDimension.TextureCube, false ->
                GL.FramebufferTexture2D(framebufferTarget, attachment, targetSlice, texture.Handle, level)

            | d, a ->
                failwithf "[GL] cannot attach %A%s to framebuffer" d (if a then "[]" else "")

        | Image.Renderbuffer renderBuffer ->
            GL.FramebufferRenderbuffer(framebufferTarget, attachment, RenderbufferTarget.Renderbuffer, renderBuffer.Handle)

        GL.Check "could not attach texture to framebuffer"

    /// Uses a framebuffer to the read the image layers of the given level from slice baseSlice to baseSlice + slices.
    let readLayers (image : Image) (level : int) (baseSlice : int) (slices : int) (f : int -> unit) =
        let attachment = image.Attachment

        let readBuffer =
            if attachment = FramebufferAttachment.ColorAttachment0 then ReadBufferMode.ColorAttachment0
            else ReadBufferMode.None

        Framebuffer.temporary FramebufferTarget.ReadFramebuffer (fun fbo ->
            GL.ReadBuffer(readBuffer)
            GL.Check "could not set buffer"

            try
                for slice = baseSlice to baseSlice + slices - 1 do
                    image |> attach FramebufferTarget.ReadFramebuffer level slice
                    Framebuffer.check FramebufferTarget.ReadFramebuffer
                    f slice

            finally
                GL.ReadBuffer(ReadBufferMode.None)
        )