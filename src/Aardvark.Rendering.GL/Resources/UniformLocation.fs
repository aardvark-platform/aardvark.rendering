#if INTERACTIVE
#I @"E:\Development\Aardvark-2015\build\Release\AMD64"
#r "Aardvark.Base.TypeProviders.dll"
#r "Aardvark.Base.dll"
#r "Aardvark.Base.FSharp.dll"
#r "OpenTK.dll"
#r "FSharp.PowerPack.dll"
#r "FSharp.PowerPack.Linq.dll"
#r "FSharp.PowerPack.Metadata.dll"
#r "FSharp.PowerPack.Parallel.Seq.dll"
#r "Aardvark.Rendering.GL.dll"
open Aardvark.Rendering.GL
#else
namespace Aardvark.Rendering.GL
#endif
open System
open System.Threading
open System.Collections.Concurrent
open System.Runtime.InteropServices
open Aardvark.Base
open OpenTK
open OpenTK.Platform
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Linq
open Aardvark.Base.Incremental
open Aardvark.Base.ShaderReflection
open Aardvark.Rendering.GL


type UniformLocation(ctx : Context, size : int, uniformType : ShaderParameterType) =
    let data = Marshal.AllocHGlobal(size)

    member x.Free() =
        Marshal.FreeHGlobal data

    member x.Context = ctx
    member x.Size = size
    member x.Data = data
    member x.Type = uniformType

[<AutoOpen>]
module UniformLocationExtensions =
    type Context with
        member x.CreateUniformLocation(size : int, uniformType : ShaderParameterType) =
            UniformLocation(x, size, uniformType)

        member x.Delete(loc : UniformLocation) =
            loc.Free()

    module ExecutionContext =
        let bindUniformLocation (l : int) (loc : UniformLocation) =
            [
                match loc.Type with
                    | Vector(Float, 1) | Float  -> yield Instruction.Uniform1fv l 1 loc.Data
                    | Vector(Int, 1) | Int      -> yield Instruction.Uniform1iv l 1 loc.Data
                    | Vector(Float, 2)          -> yield Instruction.Uniform2fv l 1 loc.Data
                    | Vector(Int, 2)            -> yield Instruction.Uniform2iv l 1 loc.Data
                    | Vector(Float, 3)          -> yield Instruction.Uniform3fv l 1 loc.Data
                    | Vector(Int, 3)            -> yield Instruction.Uniform3iv l 1 loc.Data
                    | Vector(Float, 4)          -> yield Instruction.Uniform4fv l 1 loc.Data
                    | Vector(Int, 4)            -> yield Instruction.Uniform4iv l 1 loc.Data
                    | Matrix(Float, 2, 2, true) -> yield Instruction.UniformMatrix2fv l 1 1 loc.Data
                    | Matrix(Float, 3, 3, true) -> yield Instruction.UniformMatrix3fv l 1 1 loc.Data
                    | Matrix(Float, 4, 4, true) -> yield Instruction.UniformMatrix4fv l 1 1 loc.Data
                    | _                         -> failwithf "no uniform-setter for: %A" loc
            ]