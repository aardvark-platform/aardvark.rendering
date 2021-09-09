namespace Aardvark.Rendering.GL

open System
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Aardvark.Base
open Aardvark.Rendering.GL

[<AutoOpen>]
module ARB_copy_image =

    // Arb functions are in GL type, need alias to prevent recursive calls
    [<AutoOpen>]
    module private ArbFunctions =
        module GL =
            type Arb = OpenGL4.GL

    type GL private() =

        static let supported = ExtensionHelpers.isSupported (Version(4,3)) "GL_ARB_image_copy"

        static member ARB_image_copy = supported

        static member CopyImageSubData(srcName : int, srcTarget : ImageTarget, srcLevel : int,
                                       srcX : int, srcY : int, srcZ : int,
                                       dstName : int, dstTarget : ImageTarget, dstLevel : int,
                                       dstX : int, dstY : int, dstZ : int,
                                       width : int, height : int, depth : int) =
            if supported then
                GL.Arb.CopyImageSubData(srcName, srcTarget, srcLevel, srcX, srcY, srcZ,
                                        dstName, dstTarget, dstLevel, dstX, dstY, dstZ,
                                        width, height, depth)
            else
                failwith "Not implemented"

        static member inline CopyImageSubData(srcName : int, srcTarget : ImageTarget, srcLevel : int, srcOffset : V3i,
                                              dstName : int, dstTarget : ImageTarget, dstLevel : int, dstOffset : V3i,
                                              size : V3i) =
            GL.CopyImageSubData(srcName, srcTarget, srcLevel, srcOffset.X, srcOffset.Y, srcOffset.Z,
                                dstName, dstTarget, dstLevel, dstOffset.X, dstOffset.Y, dstOffset.Z,
                                size.X, size.Y, size.Z)