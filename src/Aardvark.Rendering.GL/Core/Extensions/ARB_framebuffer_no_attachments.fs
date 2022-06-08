namespace Aardvark.Rendering.GL

open System
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL

[<AutoOpen>]
module ARB_framebuffer_no_attachments =

    type GL private() =

        static let supported = ExtensionHelpers.isSupported (Version(4,3)) "GL_ARB_framebuffer_no_attachments"

        static member ARB_framebuffer_no_attachments = supported

    type GL.Dispatch with

        static member GetFramebufferParameter(target : FramebufferTarget, pname : GetFramebufferParameter) =
            if GL.ARB_framebuffer_no_attachments then
                GL.GetFramebufferParameter(target, unbox pname)
            else
                failwith "glGetFramebufferParameter is not available."