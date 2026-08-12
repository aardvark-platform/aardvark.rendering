namespace Aardvark.Rendering

open System
open System.Threading
open System.Collections.Generic
open Aardvark.Base
open FSharp.Data.Adaptive

type internal DeltaHeapEntry<'a, 'b> =
    class
        val mutable public Priority : 'b
        val mutable public Value : 'a
        val mutable public Index : int
        val mutable public RefCount : int

        new(v,p,i,r) = { Value = v; Priority = p; Index = i; RefCount = r }
    end

/// A priority queue with synchronized enqueue and dequeue operations.
/// Operations for the same value are combined by count, and zero-count operations are ignored.
/// Dequeue returns the pending operation with the minimum priority.
type ConcurrentDeltaPriorityQueue<'a, 'b when 'b : comparison>(getPriority : SetOperation<'a> -> 'b) =
    let lockObj = obj()
    let heap = List<DeltaHeapEntry<'a, 'b>>()
    let entries = Dict<'a, DeltaHeapEntry<'a, 'b>>()
    let mutable adds = 0
    let mutable rems = 0

    let updateCounts (oldCount : int) (newCount : int) =
        if oldCount > 0 && newCount <= 0 then adds <- adds - 1
        elif oldCount < 0 && newCount >= 0 then rems <- rems - 1

        if newCount > 0 && oldCount <= 0 then adds <- adds + 1
        elif newCount < 0 && oldCount >= 0 then rems <- rems + 1

    let swap (l : DeltaHeapEntry<'a, 'b>) (r : DeltaHeapEntry<'a, 'b>) =
        let li = l.Index
        let ri = r.Index
        heap.[li] <- r
        heap.[ri] <- l
        l.Index <- ri
        r.Index <- li

    let pushDown (e : DeltaHeapEntry<'a, 'b>) =
        let mutable running = true
        while running do
            let left = 2 * e.Index + 1
            if left >= heap.Count then
                running <- false
            else
                let right = left + 1
                let child =
                    if right < heap.Count && compare heap.[left].Priority heap.[right].Priority >= 0 then
                        heap.[right]
                    else
                        heap.[left]

                if compare e.Priority child.Priority <= 0 then
                    running <- false
                else
                    swap child e

    let rec bubbleUp (e : DeltaHeapEntry<'a, 'b>) =
        if e.Index > 0 then
            let pi = (e.Index - 1) / 2
            let pe = heap.[pi]

            if compare pe.Priority e.Priority > 0 then
                swap pe e
                bubbleUp e

    let enqueue (e : DeltaHeapEntry<'a, 'b>) =
        e.Index <- heap.Count
        heap.Add(e)
        bubbleUp e

    let changeKey (e : DeltaHeapEntry<'a, 'b>) (newKey : 'b) =
        if e.Index < 0 then
            e.Priority <- newKey
            enqueue e
        else
            let c = compare newKey e.Priority
            e.Priority <- newKey

            if c > 0 then pushDown e
            elif c < 0 then bubbleUp e

    let dequeue() =
        if heap.Count <= 1 then
            let e = heap.[0]
            entries.Remove e.Value |> ignore
            heap.Clear()
            e.Index <- -1
            adds <- 0
            rems <- 0
            SetOperation(e.Value, e.RefCount)
        else
            let e = heap.[0]
            let l = heap.[heap.Count - 1]
            heap.RemoveAt (heap.Count - 1)
            heap.[0] <- l
            l.Index <- 0
            pushDown l
            e.Index <- -1
            entries.Remove e.Value |> ignore

            if e.RefCount < 0 then rems <- rems - 1
            elif e.RefCount > 0 then adds <- adds - 1

            SetOperation(e.Value, e.RefCount)

    let rec remove (e : DeltaHeapEntry<'a, 'b>) =
        if e.Index > 0 then
            let pi = (e.Index - 1) / 2
            let pe = heap.[pi]
            swap pe e
            remove e
        else
            dequeue() |> ignore

    /// Number of distinct pending values with a positive coalesced count.
    member x.AddCount = adds

    /// Number of distinct pending values with a negative coalesced count.
    member x.RemoveCount = rems

    /// Number of distinct pending values. Equivalent to AddCount + RemoveCount.
    member x.Count = heap.Count

    /// Adds an operation, coalescing its count with an existing operation for the same value.
    /// Operations with a count of zero are ignored. This operation is synchronized with enqueue and dequeue operations.
    member x.Enqueue (a : SetOperation<'a>) : unit =
        if a.Count <> 0 then
            lock lockObj (fun () ->
                let entry = entries.GetOrCreate(a.Value, fun v -> DeltaHeapEntry<'a, 'b>(a.Value, Unchecked.defaultof<'b>, -1, 0))
                let oldCount = entry.RefCount
                entry.RefCount <- entry.RefCount + a.Count
                updateCounts oldCount entry.RefCount

                if entry.RefCount = 0 then
                    entries.Remove a.Value |> ignore
                    remove entry
                else
                    changeKey entry (SetOperation(entry.Value, entry.RefCount) |> getPriority)
                    Monitor.Pulse lockObj

                assert(adds + rems = heap.Count)
            )

    /// Adds a sequence of operations, coalescing counts for equal values.
    /// Individual operations with a count of zero are ignored. This operation is synchronized with enqueue and dequeue operations.
    member x.EnqueueMany (a : seq<SetOperation<'a>>) : unit =
        lock lockObj (fun () ->
            for a in a do
                if a.Count <> 0 then
                    let entry = entries.GetOrCreate(a.Value, fun v -> DeltaHeapEntry<'a, 'b>(a.Value, Unchecked.defaultof<'b>, -1, 0))
                    let oldCount = entry.RefCount
                    entry.RefCount <- entry.RefCount + a.Count
                    updateCounts oldCount entry.RefCount

                    if entry.RefCount = 0 then
                        entries.Remove a.Value |> ignore
                        remove entry
                    else
                        changeKey entry (SetOperation(entry.Value, entry.RefCount) |> getPriority)

            Monitor.Pulse lockObj
            assert(adds + rems = heap.Count)
        )

    [<Obsolete>]
    member x.Pulse() =
        let mutable lockTaken = false
        try
            Monitor.Enter(lockObj, &lockTaken)
            Monitor.PulseAll lockObj
        finally
            if lockTaken then Monitor.Exit lockObj

    /// Removes and returns the pending operation with the minimum priority, waiting until one is available or cancellation is requested.
    member x.Dequeue (ct : CancellationToken) : SetOperation<'a> =
        let mutable lockTaken = false
        try
            Monitor.Enter(lockObj, &lockTaken)
            while heap.Count = 0 do
                if ct.IsCancellationRequested then
                    raise <| OperationCanceledException()
                Monitor.Wait(lockObj, 100) |> ignore

            let e = dequeue()
            assert(adds + rems = heap.Count)
            e
        finally
            if lockTaken then Monitor.Exit lockObj

    /// Removes and returns the pending operation with the minimum priority, waiting until one is available.
    member x.Dequeue () : SetOperation<'a> =
        let mutable lockTaken = false
        try
            Monitor.Enter(lockObj, &lockTaken)
            while heap.Count = 0 do
                Monitor.Wait lockObj |> ignore
            let e = dequeue()
            assert(adds + rems = heap.Count)
            e
        finally
            if lockTaken then Monitor.Exit lockObj