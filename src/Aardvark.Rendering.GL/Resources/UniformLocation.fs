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
open Microsoft.FSharp.Linq.QuotationEvaluation
open Aardvark.Base.Incremental


type UniformLocation(ctx : Context, size : int, uniformType : ActiveUniformType) =
    let data = Marshal.AllocHGlobal(size)

    member x.CompileSetter (m : IMod) : unit -> unit =
        let t = m.GetType()

        match t with
            | ModOf(t) ->
                let write = ValueConverter.compileSetter ValueConverter.ConversionTarget.ConvertForLocation [{path = ValuePath "value"; offset = 0; uniformType = uniformType; count = 1}] t

                fun () -> 
                    write (m :> obj) data
            | _ ->
                failwith "unsupported mod-type"

    member x.Free() =
        Marshal.FreeHGlobal data

    member x.Context = ctx
    member x.Size = size
    member x.Data = data
    member x.Type = uniformType

[<AutoOpen>]
module UniformLocationExtensions =

    type Context with
        member x.CreateUniformLocation(size : int, uniformType : ActiveUniformType) =
            UniformLocation(x, size, uniformType)

        member x.Delete(loc : UniformLocation) =
            loc.Free()

    module ExecutionContext =
        let bindUniformLocation (l : int) (loc : UniformLocation) =
            [
                match loc.Type with
                    | FloatVectorType 1 ->
                        yield Instruction.Uniform1fv l 1 loc.Data
                    | IntVectorType 1 ->
                        yield Instruction.Uniform1iv l 1 loc.Data

                    | FloatVectorType 2 ->
                        yield Instruction.Uniform2fv l 1 loc.Data
                    | IntVectorType 2 ->
                        yield Instruction.Uniform2iv l 1 loc.Data

                    | FloatVectorType 3 ->
                        yield Instruction.Uniform3fv l 1 loc.Data
                    | IntVectorType 3 ->
                        yield Instruction.Uniform3iv l 1 loc.Data

                    | FloatVectorType 4 ->
                        yield Instruction.Uniform4fv l 1 loc.Data
                    | IntVectorType 4 ->
                        yield Instruction.Uniform4iv l 1 loc.Data


                    | FloatMatrixType(2,2) ->
                        yield Instruction.UniformMatrix2fv l 1 0 loc.Data

                    | FloatMatrixType(3,3) ->
                        yield Instruction.UniformMatrix3fv l 1 0 loc.Data

                    | FloatMatrixType(4,4) ->
                        yield Instruction.UniformMatrix4fv l 1 0 loc.Data

                    | _ ->
                        failwithf "no uniform-setter for: %A" loc
            ]