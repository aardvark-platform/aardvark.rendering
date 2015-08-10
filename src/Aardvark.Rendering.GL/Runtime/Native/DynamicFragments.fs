namespace Aardvark.Rendering.GL

open System
open System.Collections.Generic
open Aardvark.Base
open Aardvark.Rendering

[<AllowNullLiteral>]
type NativeDynamicFragment<'a>(f : Fragment<'a>) =
    let mutable statistics = FrameStatistics.Zero
    let mutable entry : Option<nativeint * (unit -> unit)> = None
    let cachedStats = Dictionary<int, FrameStatistics>()

    member x.Fragment = f

    interface IDynamicFragment<NativeDynamicFragment<'a>> with
        member x.Statistics = statistics

        member x.RunAll() =
            failwith "native fragments cannot be invoked directly"

        member x.Next
            with get() = NativeDynamicFragment(f.Next)
            and set n = f.Next <- n.Fragment

        member x.Prev
            with get() = NativeDynamicFragment(f.Prev)
            and set n = f.Prev <- n.Fragment

        member x.Append(i : seq<Instruction>) =
            let add = i |> Seq.map InstructionStatistics.toStats |> Seq.sum
            statistics <- { (statistics + add) with ProgramSize = uint64 f.SizeInBytes }
            let compiled = i |> Seq.map (fun i -> let a = ExecutionContext.compile i in a.functionPointer, a.args)
            let id = f.Append compiled
            cachedStats.[id] <- add

            id

        member x.Update(id : int) (i : seq<Instruction>) =
            let oldStats = cachedStats.[id]
            let newStats = i |> Seq.map InstructionStatistics.toStats |> Seq.sum
            statistics <- { (statistics - oldStats + newStats) with ProgramSize = uint64 f.SizeInBytes }
            let compiled = i |> Seq.map (fun i -> let a = ExecutionContext.compile i in a.functionPointer, a.args)
            f.Update(id, compiled)
            cachedStats.[id] <- newStats

        member x.Clear() =
            statistics <- { FrameStatistics.Zero with ProgramSize = uint64 f.SizeInBytes }
            cachedStats.Clear()
            f.Clear()

