namespace Aardvark.Base

open System
open Aardvark.Base.Incremental
open System.Runtime.InteropServices
open Aardvark.Base.Rendering
open Microsoft.FSharp.NativeInterop

#nowarn "9"

module NewCompute =
    
    type IComputeShader =
        abstract member LocalSize : V3i

    type IComputeShaderInputBinding =
        inherit IDisposable
        abstract member Shader : IComputeShader
        abstract member Item : string -> obj with set
        abstract member Flush : unit -> unit


    type IComputeRuntime =
        abstract member CompileCompute : FShade.ComputeShader -> IComputeShader
        abstract member DeleteCompute : IComputeShader -> unit
        abstract member NewInputBinding : IComputeShader -> IComputeShaderInputBinding
        
    type ComputeCommand =
        | Copy of IBuffer
        | Dispatch of groups : V3i * shader : IComputeShader



    open Microsoft.FSharp.Quotations


    type Call =
        {
            shader : FShade.ComputeShader
            groups : V3i
            args : Map<string, obj>
        }

    let invoke3 (a : 'a -> 'b) (groups : V3i) (args : list<string * obj>) : Call =
        {
            shader = FShade.ComputeShader.ofFunction (V3i(4096, 4096, 4096)) a
            groups = groups
            args = Map.ofList args
        }

    let invoke (a : 'a -> 'b) (groups : int) (args : list<string * obj>) : Call =
        invoke3 a (V3i(groups, 1, 1)) args
    
    type CommandBuilder(r : IComputeRuntime) =
        member x.Bind(e : Call, f : unit -> 'b) =
            f()
            
        member x.Return(u : unit) = ()
        member x.Zero() = ()

        member x.Delay(f : unit -> 'a) = f()
        member x.Combine(l, r) = r


        member x.Quote() =
            ()

        //member x.Run(f : Expr<unit -> 'a>) = f()
    let command = CommandBuilder(Unchecked.defaultof<_>)

    open FShade
    let test (a : int[]) =
        compute {
            let id = getGlobalId().X
            a.[id] <- 2 * a.[id]
        }

    let dupl (a : BufferView) =
        command {
            do! invoke test 64 [
                    "a", a :> obj
                ]
        }