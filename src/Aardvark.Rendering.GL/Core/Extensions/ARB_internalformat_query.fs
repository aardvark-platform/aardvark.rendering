namespace Aardvark.Rendering.GL

open System
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL

[<AutoOpen>]
module ARB_internalformat_query =

    type GL private() =

        static let supported = ExtensionHelpers.isSupported (Version(4,2)) "GL_ARB_internalformat_query"

        static member ARB_internalformat_query = supported

    type GL.Dispatch with

        static member GetInternalformat(target : ImageTarget, internalFormat : SizedInternalFormat, pname : InternalFormatParameter, buffer : int[]) =
            if GL.ARB_internalformat_query then
                GL.GetInternalformat(target, internalFormat, pname, buffer.Length, buffer)
            else
                failwith "glGetInternalformat is not available!"

        static member GetInternalformat(target : ImageTarget, internalFormat : SizedInternalFormat, pname : InternalFormatParameter, count : int) =
            let buffer = Array.zeroCreate count
            GL.Dispatch.GetInternalformat(target, internalFormat, pname, buffer)
            buffer

        static member GetInternalformat(target : ImageTarget, internalFormat : SizedInternalFormat, pname : InternalFormatParameter) =
            GL.Dispatch.GetInternalformat(target, internalFormat, pname, 1) |> Array.head