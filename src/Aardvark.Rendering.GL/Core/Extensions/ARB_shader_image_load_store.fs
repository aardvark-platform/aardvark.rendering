namespace Aardvark.Rendering.GL

open System
open OpenTK.Graphics.OpenGL4

[<AutoOpen>]
module ARB_shader_image_load_store =

    type GL private() =
        static let supported = ExtensionHelpers.isSupported (Version(4, 2)) "GL_ARB_shader_image_load_store"
        static member ARB_shader_image_load_store = supported

    type GL.Dispatch with
        static member BindImageTexture(unit : int, texture : int, level : int, layered : bool, layer : int, access : TextureAccess, format : SizedInternalFormat) =
            if GL.ARB_shader_image_load_store then
                GL.BindImageTexture(unit, texture, level, layered, layer, access, format)
            else
                failwith "glBindImageTexture is not available!"