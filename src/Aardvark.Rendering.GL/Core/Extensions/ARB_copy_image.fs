namespace Aardvark.Rendering.GL

open System
open OpenTK.Graphics.OpenGL4
open Aardvark.Base
open Aardvark.Rendering.GL

[<AutoOpen>]
module ARB_copy_image =

    type GL private() =

        static let supported = ExtensionHelpers.isSupported (Version(4,3)) "GL_ARB_copy_image"

        static member ARB_copy_image = supported

    type GL.Dispatch with

        static member CopyImageSubData(srcName : int, srcTarget : ImageTarget, srcLevel : int,
                                       srcX : int, srcY : int, srcZ : int,
                                       dstName : int, dstTarget : ImageTarget, dstLevel : int,
                                       dstX : int, dstY : int, dstZ : int,
                                       width : int, height : int, depth : int) =
            if GL.ARB_copy_image then
                GL.CopyImageSubData(srcName, srcTarget, srcLevel, srcX, srcY, srcZ,
                                        dstName, dstTarget, dstLevel, dstX, dstY, dstZ,
                                        width, height, depth)
            else
                failwith "glCopyImageSubData is not available!"

        static member inline CopyImageSubData(srcName : int, srcTarget : ImageTarget, srcLevel : int, srcOffset : V3i,
                                              dstName : int, dstTarget : ImageTarget, dstLevel : int, dstOffset : V3i,
                                              size : V3i) =
            GL.CopyImageSubData(srcName, srcTarget, srcLevel, srcOffset.X, srcOffset.Y, srcOffset.Z,
                                dstName, dstTarget, dstLevel, dstOffset.X, dstOffset.Y, dstOffset.Z,
                                size.X, size.Y, size.Z)