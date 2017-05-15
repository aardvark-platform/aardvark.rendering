namespace Aardvark.SceneGraph

open System
open System.Threading
open System.Collections.Generic
open System.Collections.Concurrent
open Aardvark.Base
open Aardvark.Base.Incremental

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
    
    type AsyncReader<'a>(inner : ISetReader<'a>) =
        inherit AbstractReader<hdeltaset<'a>>(Ag.emptyScope, HDeltaSet.monoid)

        let sem = new SemaphoreSlim(1)

        member x.GetOperationsAsync() =
            async {
                do! Async.SwitchToThreadPool()
                let! _ = Async.AwaitIAsyncResult(sem.WaitAsync())
                let d = EvaluationUtilities.evaluateTopLevel (fun () -> x.GetOperations())
                return d
            }

        override x.Mark() =
            sem.Release() |> ignore
            true

        override x.Compute(token) =
            inner.GetOperations(token)

        override x.Release() =
            sem.Dispose()
            inner.Dispose()

        override x.Inputs = Seq.singleton (inner :> IAdaptiveObject)

    let ofASet (s : aset<'a>) =
        let queue = new ConcurrentDeltaQueue<'a>()
         
        let reader = new AsyncReader<_>(s.GetReader())

        let pull =
            async {
                do! Async.SwitchToThreadPool()
                while true do
                    let! deltas = reader.GetOperationsAsync()
                    for d in deltas do queue.Enqueue d |> ignore
            }

        let cancel = new CancellationTokenSource()
        let task = Async.StartAsTask(pull, cancellationToken = cancel.Token)

        let disposable =
            { new IDisposable with
                member x.Dispose() =
                    cancel.Cancel()
                    reader.Dispose()
            }

        queue.Subscription <- disposable
        queue
