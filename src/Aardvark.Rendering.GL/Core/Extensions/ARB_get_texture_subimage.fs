namespace Aardvark.Rendering.GL

open System
open OpenTK.Graphics.OpenGL4
open Aardvark.Base
open Aardvark.Rendering.GL

[<AutoOpen>]
module ARB_get_texture_subimage =

    type GL private() =

        static let supported = ExtensionHelpers.isSupported (Version(4,5)) "GL_ARB_get_texture_sub_image"

        static member ARB_get_texture_subimage = supported

    type GL.Dispatch with

        static member GetTextureSubImage(texture : int,
                                         level : int, xoffset : int, yoffset : int, zoffset : int,
                                         width : int, height : int, depth : int, format : PixelFormat,
                                         typ : PixelType, bufferSize : int, pixels : nativeint) =
            if GL.ARB_get_texture_subimage then
                GL.GetTextureSubImage(texture, level, xoffset, yoffset, zoffset,
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
            if GL.ARB_get_texture_subimage then
                GL.GetCompressedTextureSubImage(texture, level, xoffset, yoffset, zoffset,
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