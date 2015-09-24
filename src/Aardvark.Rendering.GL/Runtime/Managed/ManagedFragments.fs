namespace Aardvark.Rendering.GL

open System
open System.Collections.Generic
open Aardvark.Base
open Aardvark.Rendering

[<AllowNullLiteral>]
type private InstructionListFragment(l : Instruction[]) =
    let mutable next : InstructionListFragment = null
    let mutable prev : InstructionListFragment = null
    let mutable instructions = l


    member x.Prev
        with get() = prev
        and set p = prev <- p

    member x.Next 
        with get() = next
        and set n = next <- n

    member x.Instructions
        with get() = instructions
        and set i = instructions <- i

    member x.RunSelf() =
        for i in instructions do
            ExecutionContext.run i

[<AllowNullLiteral>]
type ManagedDynamicFragment() =
    let mutable next : ManagedDynamicFragment = null
    let mutable prev : ManagedDynamicFragment = null
    let mutable first = null
    let mutable last = null
    let cache = Dict<int, InstructionListFragment>()
    let mutable currentId = 0
    let mutable instructions = null
    let mutable statistics = FrameStatistics.Zero
    let cachedStats = Dictionary<int, FrameStatistics>()

    let newId() =
        System.Threading.Interlocked.Increment &currentId

    member x.Next
        with get() = next
        and set v = 
            next <- v

    member x.Prev
        with get() = prev
        and set v = 
            prev <- v

    member private x.First = first
    member private x.Last = last

    member x.Append(i : seq<Instruction>) =
        let stats = i |> Seq.map InstructionStatistics.toStats |> Seq.sum
        statistics <- statistics + stats
        let id = newId()
        cachedStats.[id] <- stats
        let f = InstructionListFragment(i |> Seq.toArray)
        cache.[id] <- f

        if last = null then
            first <- f
            last <- f
        else
            last.Next <- f
            f.Prev <- last
            last <- f

        id

    member x.Update (id : int) (value : seq<Instruction>) =
        let newStats = value |> Seq.map InstructionStatistics.toStats |> Seq.sum
        let oldStats = cachedStats.[id]
        cachedStats.[id] <- newStats
        statistics <- statistics - oldStats + newStats
        let f = cache.[id]
        f.Instructions <- value |> Seq.toArray


    member x.Clear() =
        statistics <- FrameStatistics.Zero
        cachedStats.Clear()
        first <- null
        last <- null
        cache.Clear()
        currentId <- 0


    member x.AllInstructions =
        seq {
            let current = ref x
            while !current <> null do
                let inner = ref current.contents.First
                while !inner <> null do
                    yield! inner.contents.Instructions
                    inner := inner.contents.Next
                current := current.contents.Next
        }

    member x.RebuildCache() =
        instructions <- null

    member x.RunAll() =
        let all = x.AllInstructions |> Seq.toArray
        for i in all do
            ExecutionContext.debug i

    interface IDynamicFragment<ManagedDynamicFragment> with
        member x.Statistics = statistics
        member x.Next
            with get() = next
            and set v = next <- v

        member x.Prev
            with get() = prev
            and set v = prev <- v

        member x.Append(i : seq<Instruction>) =
            x.Append i

        member x.Update (id : int) (value : seq<Instruction>) =
            x.Update id value

        member x.Clear() =
            x.Clear()

        member x.RunAll() =
            x.RunAll()


