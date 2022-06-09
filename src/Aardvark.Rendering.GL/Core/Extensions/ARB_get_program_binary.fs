namespace Aardvark.Rendering.GL

open System
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL

[<AutoOpen>]
module ARB_get_program_binary =

    type GetProgramParameterName with
        static member ProgramBinaryLength = unbox<GetProgramParameterName> 0x8741

    type GL private() =

        static let supported = ExtensionHelpers.isSupported (Version(4,1)) "GL_ARB_get_program_binary"

        static member ARB_get_program_binary = supported

    type GL.Dispatch with

        static member GetProgramBinary(program : int, bufSize : int, length : byref<int>, binaryFormat : byref<BinaryFormat>, binary : 'T[]) =
            if GL.ARB_get_program_binary then
                GL.GetProgramBinary(program, bufSize, &length, &binaryFormat, binary)
            else
                failwith "glGetProgramBinary is not available."