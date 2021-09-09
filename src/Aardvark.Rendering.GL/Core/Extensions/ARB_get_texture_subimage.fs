namespace Aardvark.Rendering.GL

open System
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL

[<AutoOpen>]
module ARB_get_texture_subimage =

    // Arb functions are in GL type, need alias to prevent recursive calls
    [<AutoOpen>]
    module private ArbFunctions =
        module GL =
            type Arb = OpenGL4.GL

    type GL private() =

        static let supported = ExtensionHelpers.isSupported (Version(4,5)) "GL_ARB_get_texture_subimage"

        static member ARB_get_texture_subimage = supported

        static member GetTextureSubImage(texture : int,
                                         level : int, xoffset : int, yoffset : int, zoffset : int,
                                         width : int, height : int, depth : int, format : PixelFormat,
                                         pixelType : PixelType, bufferSize : int, pixels : nativeint) =
            if supported then
                GL.Arb.GetTextureSubImage(texture, level, xoffset, yoffset, zoffset,
                                          width, height, depth,
                                          format, pixelType, bufferSize, pixels)
            else
                failwith "glGetTextureSubImage is not available!"