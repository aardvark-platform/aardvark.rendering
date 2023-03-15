namespace Aardvark.Rendering.GL

open System
open OpenTK.Graphics.OpenGL4

[<AutoOpen>]
module ARB_clip_control =

    type GL private() =
        static let supported = ExtensionHelpers.isSupported (Version(4,5,0)) "GL_ARB_clip_control"
        static member ARB_clip_control = supported

    type GL.Dispatch with
        static member ClipControl(origin : ClipOrigin, depth : ClipDepthMode) =
            if GL.ARB_clip_control then
                GL.ClipControl(origin, depth)
            else
                failwith "glClipControl is not available!"