#if COMPILED
namespace Aardvark.Base
#else
#r @"E:\Development\aardvark-base\bin\Release\Aardvark.Base.dll"
open Aardvark.Base
#endif

open System.Collections.Generic

type HeapEntry<'k, 'v> internal(key : 'k, value : 'v, index : int) =
    let mutable index = index
    let mutable value = value

    member x.Key = key

    member internal x.Index
        with get() = index
        and set v = index <- v

    member x.Value
        with get() = value
        and set v = value <- v

type Heap<'k, 'v>(comparer : IComparer<'k>) =
    let store = List<HeapEntry<'k, 'v>>()

    let parent (i : int) =
        (i - 1) / 2

    let swap (i : int) (j : int) =
        let vi = store.[i]
        let vj = store.[j]
        vi.Index <- j
        vj.Index <- i
        store.[i] <- vj
        store.[j] <- vi

    let rec bubbleUp (entry : HeapEntry<'k, 'v>) (i : int) =
        if i > 0 then
            let p = parent i
            let c = comparer.Compare(store.[p].Key, entry.Key)
            if c > 0 then 
                swap i p
                bubbleUp entry p

    let rec pushDown (entry : HeapEntry<'k, 'v>) (i : int) =
        let lc = 2 * i + 1
        let rc = 2 * i + 2

        let ccl = if lc < store.Count then comparer.Compare(entry.Key, store.[lc].Key) else -1
        let ccr = if rc < store.Count then comparer.Compare(entry.Key, store.[rc].Key) else -1

        match ccl, ccr with
            | 1, 1 ->
                let c = comparer.Compare(store.[lc].Key, store.[rc].Key)
                if c < 0 then
                    swap lc i
                    pushDown entry lc
                else
                    swap rc i
                    pushDown entry rc

            | 1, _ ->
                swap lc i
                pushDown entry lc

            | _, 1 ->
                swap rc i
                pushDown entry rc

            | _ -> ()

    member x.Count = store.Count

    member x.Enqueue(key : 'k, value : 'v) =
        let entry = HeapEntry(key, value, store.Count)
        store.Add(entry)
        bubbleUp entry entry.Index
        entry

    member x.Dequeue() =
        if store.Count > 0 then
            let entry = store.[0]
            let last = store.Count - 1
            swap 0 last
            store.RemoveAt last
            if store.Count > 0 then
                pushDown store.[0] 0

            entry.Index <- -1
            entry
        else
            failwith "sadasd"

    member x.Remove(e : HeapEntry<'k, 'v>) =
        if e.Index >= 0 && e.Index < store.Count then
            let index = e.Index
            let last = store.Count - 1
            if index = last then
                store.RemoveAt last
            else
                swap index last
                store.RemoveAt last
                pushDown store.[index] index

            e.Index <- -1
            true
        else
            false

type HashHeap<'k, 'v when 'k : equality>(comparer : IComparer<'k>) =
    let h = Heap<'k, 'v>(comparer)
    let entries = Dict<'k, HeapEntry<'k, 'v>>()

    member x.Count = entries.Count

    member x.Enqueue(key : 'k, value : 'v) =
        let mutable success = false
        let entry = 
            entries.GetOrCreate(key, fun key ->
                let e = h.Enqueue(key, value)
                success <- true
                e
            )

        entry.Value <- value
        success

    member x.Dequeue() =
        let e = h.Dequeue()
        entries.Remove e.Key |> ignore
        e.Value

    member x.Remove(key : 'k) =
        match entries.TryRemove key with
            | (true, e) ->
                h.Remove e
            | _ ->
                false



module HeapTest =
    let run() =
        let h = HashHeap<int, string>(Comparer<int>.Default)
        
        h.Enqueue(1, "1") |> ignore
        h.Enqueue(5, "5") |> ignore
        h.Enqueue(4, "4") |> ignore
        h.Enqueue(2, "2") |> ignore
        h.Enqueue(3, "3") |> ignore
        h.Enqueue(3, "33") |> ignore

        while h.Count > 0 do
            let v = h.Dequeue()
            printfn "%s" v

               