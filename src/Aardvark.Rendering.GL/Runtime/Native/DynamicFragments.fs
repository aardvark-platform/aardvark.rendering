namespace Aardvark.Rendering.GL

open System
open System.Collections.Generic
open Aardvark.Base
open Aardvark.Rendering

type NativeDynamicFragment<'a>(f : Fragment<'a>) =
    member x.Fragment = f

    interface IDynamicFragment<NativeDynamicFragment<'a>> with
        member x.Next
            with get() = NativeDynamicFragment(f.Next)
            and set n = f.Next <- n.Fragment

        member x.Prev
            with get() = NativeDynamicFragment(f.Prev)
            and set n = f.Prev <- n.Fragment

        member x.Append(i : seq<Instruction>) =
            let compiled = i |> Seq.map (fun i -> let a = ExecutionContext.compile i in a.functionPointer, a.args)
            f.Append compiled

        member x.Update(id : int) (i : seq<Instruction>) =
            let compiled = i |> Seq.map (fun i -> let a = ExecutionContext.compile i in a.functionPointer, a.args)
            f.Update(id, compiled)

        member x.Clear() =
            f.Clear()

