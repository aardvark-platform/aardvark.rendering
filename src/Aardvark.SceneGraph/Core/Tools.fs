namespace Aardvark.SceneGraph

open System
open System.Threading
open System.Collections.Generic
open System.Collections.Concurrent
open Aardvark.Base
open FSharp.Data.Adaptive
open FSharp.Data.Traceable

[<ReferenceEquality; NoComparison>]
type private Entry<'a> = { value : 'a; count : int }


type ConcurrentDeltaQueue<'a>() =
    let refCount = Dict<'a, ref<int>>()
    let queue = Queue<'a>()

    let mutable subscription =  { new IDisposable with member x.Dispose() = () }

    member x.Subscription
        with get() = subscription
        and set s = subscription <- s

    member x.Dispose() =
        subscription.Dispose()
        refCount.Clear()
        queue.Clear()

    
    

    member x.Add(a : 'a) =
        lock x (fun () ->
            let mutable isNew = false
            let r = refCount.GetOrCreate(a, fun _ -> isNew <- true; ref 0)
            r := !r + 1
            
            if !r > 1 then
                Log.warn "bad add"

            if isNew then
                queue.Enqueue(a)
                Monitor.Pulse x
                true
            else
                false
        )

    member x.Remove(a : 'a) =
        lock x (fun () ->
            let mutable isNew = false
            let r = refCount.GetOrCreate(a, fun _ -> isNew <- true; ref 0)
            r := !r - 1
            if !r < -1 then
                Log.warn "bad rem"
            if isNew then
                queue.Enqueue(a)
                Monitor.Pulse x
                true
            else
                false
        )


    member x.Enqueue(d : SetOperation<'a>) =
        match d with
            | Add(_,v) -> x.Add v
            | Rem(_,v) -> x.Remove v

    member x.EnqueueMany(d : seq<SetOperation<'a>>) =
        lock x (fun () ->
            for e in d do x.Enqueue e |> ignore
        )

    member x.TryGetRefCount(v : 'a) =
        lock x (fun () ->
            match refCount.TryGetValue v with
                | (true, r) -> Some !r
                | _ -> None
        )

    member x.Dequeue() =
        Monitor.Enter x
        while queue.Count = 0 do
            Monitor.Wait x |> ignore

        let e = queue.Dequeue()
        match refCount.TryRemove e with
            | (true, r) ->
                Monitor.Exit x
                let r = !r
                if r = 1 then 
                    Add(e)
                elif r = -1 then 
                    Rem(e)
                elif r = 0 then
                    x.Dequeue()
                else
                    Log.warn "bad"
                    x.Dequeue()
            | _ ->
                Log.warn "asdaksbdjasndlkas"
                Monitor.Exit x
                x.Dequeue()
                



    interface IDisposable with
        member x.Dispose() = x.Dispose()

module ConcurrentDeltaQueue =
    
    type AsyncReader<'a>(inner : IHashSetReader<'a>) =
        inherit AbstractReader<HashSetDelta<'a>>(HashSetDelta.empty)

        let sem = new SemaphoreSlim(1)

        member x.GetChangesAsync() =
            async {
                do! Async.SwitchToThreadPool()
                let! _ = Async.AwaitIAsyncResult(sem.WaitAsync())

                // TODO: evaluateTopLevel
                //let d = EvaluationUtilities.evaluateTopLevel (fun () -> x.GetChanges())
                let d = x.GetChanges()
                return d
            }

        override x.MarkObject() =
            sem.Release() |> ignore
            true

        override x.Compute(token) =
            inner.GetChanges(token)

        override x.Finalize() =
            sem.Dispose()

    let ofASet (s : aset<'a>) =
        let queue = new ConcurrentDeltaQueue<'a>()
         
        let reader = new AsyncReader<_>(s.GetReader())

        let pull =
            async {
                do! Async.SwitchToThreadPool()
                while true do
                    let! deltas = reader.GetChangesAsync()
                    for d in deltas do queue.Enqueue d |> ignore
            }

        let cancel = new CancellationTokenSource()
        let task = Async.StartAsTask(pull, cancellationToken = cancel.Token)

        let disposable =
            { new IDisposable with
                member x.Dispose() =
                    cancel.Cancel()
            }

        queue.Subscription <- disposable
        queue


type private DeltaHeapEntry<'a, 'b> =
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
            SetOperation(e.Value, e.RefCount)

    let rec remove (e : DeltaHeapEntry<'a, 'b>) =
        if e.Index > 0 then
            let pi = (e.Index - 1) / 2
            let pe = heap.[pi]
            swap pe e
            remove e
        else
            dequeue() |> ignore

    member x.Count = heap.Count

    member x.Enqueue (a : SetOperation<'a>) : unit =
        if a.Count <> 0 then
            lock x (fun () ->
                let entry = entries.GetOrCreate(a.Value, fun v -> DeltaHeapEntry<'a, 'b>(a.Value, Unchecked.defaultof<'b>, -1, 0))
                entry.RefCount <- entry.RefCount + a.Count

                if entry.RefCount = 0 then
                    entries.Remove a.Value |> ignore
                    remove entry
                else
                    changeKey entry (SetOperation(entry.Value, entry.RefCount) |> getPriority) |> ignore
                    Monitor.Pulse x
            )

    member x.EnqueueMany (a : seq<SetOperation<'a>>) : unit =
        lock x (fun () ->
            for a in a do
                let entry = entries.GetOrCreate(a.Value, fun v -> DeltaHeapEntry<'a, 'b>(a.Value, Unchecked.defaultof<'b>, -1, 0))
                entry.RefCount <- entry.RefCount + a.Count

                if entry.RefCount = 0 then
                    entries.Remove a.Value |> ignore
                    remove entry
                else
                    changeKey entry (SetOperation(entry.Value, entry.RefCount) |> getPriority) |> ignore

            Monitor.Pulse x
        )

    member x.UpdatePriorities() =
        let mutable maxSteps = 0
        let hist = Array.zeroCreate 128
        for e in entries.Values do
            let steps = changeKey e (getPriority (SetOperation(e.Value, e.RefCount)))
            inc &hist.[steps]
            if steps > maxSteps then maxSteps <- steps

        Array.take (maxSteps + 1) hist

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
        Monitor.Exit x
        e

    member x.Dequeue () : SetOperation<'a> = 
        Monitor.Enter x
        while heap.Count = 0 do
            Monitor.Wait(x) |> ignore
        let e = dequeue()
        Monitor.Exit x
        e


[<AllowNullLiteral>]
type private DeltaQueueEntry<'a> =
    class
        val mutable public Value : 'a
        val mutable public Prev : DeltaQueueEntry<'a>
        val mutable public Next : DeltaQueueEntry<'a>
        val mutable public RefCount : int

        new(v,r,p,n) = { Value = v; RefCount = r; Prev = p; Next = n }
    end

type ConcurrentDeltaQueue2<'a>() =
    
    let mutable first : DeltaQueueEntry<'a> = null
    let mutable last : DeltaQueueEntry<'a> = null
    let entries = Dict<'a, DeltaQueueEntry<'a>>()

    let splice (e : DeltaQueueEntry<'a>) =
        if entries.Remove e.Value then
            if isNull e.Prev then first <- e.Next
            else e.Prev.Next <- e.Next

            if isNull e.Next then last <- e.Prev
            else e.Next.Prev <- e.Prev

            e.Prev <- null
            e.Next <- null


    let enqueue (v : SetOperation<'a>) =
        match entries.TryGetValue v.Value with
            | (true, e) ->
                e.RefCount <- e.RefCount + v.Count
                if e.RefCount = 0 then
                    splice e
                    true
                else
                    false
            | _ ->
                let e = DeltaQueueEntry<'a>(v.Value, v.Count, last, null)
                if isNull last then first <- e
                else last.Next <- e
                last <- e
                true
    
    let dequeue () =
        let e = first
        let v = e.Value
        if entries.Remove v then
            if isNull e.Next then last <- null
            else e.Next.Prev <- null
            first <- e.Next
            e.Prev <- null
            e.Next <- null
        SetOperation(v, e.RefCount)


    member x.Enqueue(v : SetOperation<'a>) =
        Monitor.Enter x
        if enqueue v then
            Monitor.Pulse x
        Monitor.Exit x

    member x.EnqueueMany(s : seq<SetOperation<'a>>) =
        Monitor.Enter x
        let mutable changed = false
        for v in s do 
            let res = enqueue v
            changed <- changed || res

        if changed then Monitor.Pulse x
        Monitor.Exit x

    member x.DequeueMany() =
        Monitor.Enter x
        while isNull first do
            Monitor.Wait x |> ignore
        
        let all = System.Collections.Generic.List<SetOperation<'a>>()
        let mutable current = first
        while not (isNull current) do
            all.Add(SetOperation(current.Value, current.RefCount))
            current <- current.Next

        first <- null
        last <- null
        entries.Clear()

        Monitor.Exit x
        all

    member x.DequeueMany(ct : CancellationToken) =
        Monitor.Enter x
        while isNull first do
            Monitor.Wait(x, 100) |> ignore
            if ct.IsCancellationRequested then
                Monitor.Exit x
                raise <| OperationCanceledException()
        
        let all = System.Collections.Generic.List<SetOperation<'a>>()
        let mutable current = first
        while not (isNull current) do
            all.Add(SetOperation(current.Value, current.RefCount))
            current <- current.Next

        first <- null
        last <- null
        entries.Clear()

        Monitor.Exit x
        all

    member x.Dequeue() =
        Monitor.Enter x
        while isNull first do
            Monitor.Wait x |> ignore

        let res = dequeue()
        Monitor.Exit x
        res

    member x.Dequeue(ct : CancellationToken) =
        Monitor.Enter x
        while isNull first do
            if ct.IsCancellationRequested then
                Monitor.Exit x
                raise <| OperationCanceledException()

            Monitor.Wait(x, 100) |> ignore

        let res = dequeue()
        Monitor.Exit x
        res

