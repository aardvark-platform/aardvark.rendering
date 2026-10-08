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

type ConcurrentDeltaPriorityQueue<'a, 'b when 'b : comparison>(getPriority : SetOperation<'a> -> 'b) =

    let heap = List<DeltaHeapEntry<'a, 'b>>()
    let entries = Dict<'a, DeltaHeapEntry<'a, 'b>>()
    let mutable adds = 0
    let mutable rems = 0

    let swap (l : DeltaHeapEntry<'a, 'b>) (r : DeltaHeapEntry<'a, 'b>) =
        let li = l.Index
        let ri = r.Index
        heap.[li] <- r
        heap.[ri] <- l
        l.Index <- ri
        r.Index <- li

    let rec pushDown (acc : int) (e : DeltaHeapEntry<'a, 'b>) =
        let l = 2 * e.Index + 1
        let r = 2 * e.Index + 2

        let cl = if l < heap.Count then compare e.Priority heap.[l].Priority <= 0 else true
        let cr = if r < heap.Count then compare e.Priority heap.[l].Priority <= 0 else true

        match cl, cr with
            | true, true ->
                acc

            | false, true ->
                swap heap.[l] e
                pushDown (acc + 1) e

            | true, false ->
                swap heap.[r] e
                pushDown (acc + 1) e

            | false, false ->
                let c = compare heap.[l].Priority heap.[r].Priority
                if c < 0 then
                    swap heap.[l] e
                else
                    swap heap.[r] e

                pushDown (acc + 1) e

    let rec bubbleUp (acc : int) (e : DeltaHeapEntry<'a, 'b>) =
        if e.Index > 0 then
            let pi = (e.Index - 1) / 2
            let pe = heap.[pi]

            if compare pe.Priority e.Priority > 0 then
                swap pe e
                bubbleUp (acc + 1) e
            else
                acc
        else
            acc

    let enqueue (e : DeltaHeapEntry<'a, 'b>) =
        e.Index <- heap.Count
        heap.Add(e)
        bubbleUp 0 e

    let changeKey (e : DeltaHeapEntry<'a, 'b>) (newKey : 'b) =
        if e.Index < 0 then
            e.Priority <- newKey
            enqueue e
        else
            let c = compare newKey e.Priority
            e.Priority <- newKey

            if c > 0 then pushDown 0 e
            elif c < 0 then bubbleUp 0 e
            else 0

    let dequeue() =
        if heap.Count <= 1 then
            let e = heap.[0]
            entries.Remove e.Value |> ignore
            heap.Clear()
            adds <- 0
            rems <- 0
            SetOperation(e.Value, e.RefCount)
        else
            let e = heap.[0]
            let l = heap.[heap.Count - 1]
            heap.RemoveAt (heap.Count - 1)
            heap.[0] <- l
            l.Index <- 0
            pushDown 0 l |> ignore
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

    member x.AddCount = adds
    member x.RemoveCount = rems

    member x.Count = heap.Count

    member x.Enqueue (a : SetOperation<'a>) : unit =
        if a.Count <> 0 then
            lock x (fun () ->
                let entry = entries.GetOrCreate(a.Value, fun v -> DeltaHeapEntry<'a, 'b>(a.Value, Unchecked.defaultof<'b>, -1, 0))
                let oldCount = entry.RefCount
                entry.RefCount <- entry.RefCount + a.Count

                if entry.RefCount = 0 then
                    if oldCount > 0 then adds <- adds - 1
                    elif oldCount < 0 then rems <- rems - 1

                    entries.Remove a.Value |> ignore
                    remove entry
                else
                    if oldCount <= 0 && entry.RefCount > 0 then adds <- adds + 1
                    elif oldCount >= 0 && entry.RefCount < 0 then rems <- rems + 1

                    changeKey entry (SetOperation(entry.Value, entry.RefCount) |> getPriority) |> ignore
                    Monitor.Pulse x

                assert(adds + rems = heap.Count)
            )

    member x.EnqueueMany (a : seq<SetOperation<'a>>) : unit =
        lock x (fun () ->
            for a in a do
                let entry = entries.GetOrCreate(a.Value, fun v -> DeltaHeapEntry<'a, 'b>(a.Value, Unchecked.defaultof<'b>, -1, 0))
                let oldCount = entry.RefCount
                entry.RefCount <- entry.RefCount + a.Count

                if entry.RefCount = 0 then
                    if oldCount > 0 then adds <- adds - 1
                    elif oldCount < 0 then rems <- rems - 1

                    entries.Remove a.Value |> ignore
                    remove entry
                else
                    if oldCount <= 0 && entry.RefCount > 0 then adds <- adds + 1
                    elif oldCount >= 0 && entry.RefCount < 0 then rems <- rems + 1

                    changeKey entry (SetOperation(entry.Value, entry.RefCount) |> getPriority) |> ignore

            Monitor.Pulse x
            assert(adds + rems = heap.Count)
        )

    member x.Pulse() =
        Monitor.Enter x
        Monitor.PulseAll x
        Monitor.Exit x


    member x.Dequeue (ct : CancellationToken) : SetOperation<'a> =
        Monitor.Enter x
        while heap.Count = 0 do
            if ct.IsCancellationRequested then
                Monitor.Exit x
                raise <| OperationCanceledException()

            Monitor.Wait(x, 100) |> ignore

        let e = dequeue()
        assert(adds + rems = heap.Count)
        Monitor.Exit x
        e

    member x.Dequeue () : SetOperation<'a> =
        Monitor.Enter x
        while heap.Count = 0 do
            Monitor.Wait(x) |> ignore
        let e = dequeue()
        assert(adds + rems = heap.Count)
        Monitor.Exit x
        e