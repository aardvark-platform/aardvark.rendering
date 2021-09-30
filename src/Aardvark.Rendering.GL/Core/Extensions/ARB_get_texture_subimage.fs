namespace Aardvark.Rendering.GL

open System
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Aardvark.Base
open Aardvark.Rendering.GL

[<AutoOpen>]
module ARB_get_texture_subimage =

    // Arb functions are in GL type, need alias to prevent recursive calls
    [<AutoOpen>]
    module private ArbFunctions =
        module GL =
            type Arb = OpenGL4.GL

    type GL private() =

        static let supported = ExtensionHelpers.isSupported (Version(4,5)) "GL_ARB_get_texture_sub_image"

        static member ARB_get_texture_subimage = supported

        static member GetTextureSubImage(texture : int,
                                         level : int, xoffset : int, yoffset : int, zoffset : int,
                                         width : int, height : int, depth : int, format : PixelFormat,
                                         typ : PixelType, bufferSize : int, pixels : nativeint) =
            if supported then
                GL.Arb.GetTextureSubImage(texture, level, xoffset, yoffset, zoffset,
                                          width, height, depth,
                                          format, typ, bufferSize, pixels)
            else
                failwith "glGetTextureSubImage is not available!"

        static member GetTextureSubImage(texture : int,
                                         level : int, offset : V3i, size : V3i,
                                         format : PixelFormat, typ : PixelType,
                                         bufferSize : int, pixels : nativeint) =
            GL.GetTextureSubImage(texture, level,
                                  offset.X, offset.Y, offset.Z,
                                  size.X, size.Y, size.Z,
                                  format, typ, bufferSize, pixels)

        static member GetCompressedTextureSubImage(texture : int, level : int,
                                                   xoffset : int, yoffset : int, zoffset : int,
                                                   width : int, height : int, depth : int,
                                                   bufferSize : int, pixels : nativeint) =
            if supported then
                GL.Arb.GetCompressedTextureSubImage(texture, level, xoffset, yoffset, zoffset,
                                                    width, height, depth,
                                                    bufferSize, pixels)
            else
                failwith "glGetCompressedTextureSubImage is not available!"

        static member GetCompressedTextureSubImage(texture : int, level : int, offset : V3i, size : V3i,
                                                   bufferSize : int, pixels : nativeint) =
            GL.GetCompressedTextureSubImage(texture, level,
                                            offset.X, offset.Y, offset.Z,
                                            size.X, size.Y, size.Z,
                                            bufferSize, pixels)