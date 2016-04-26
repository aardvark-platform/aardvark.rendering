namespace Aardvark.Base.Rendering

open System
open System.Threading
open System.Collections.Generic
open System.Collections.Concurrent
open Aardvark.Base
open Aardvark.Base.Incremental


type IResource =
    inherit IAdaptiveObject
    inherit IDisposable  
    abstract member AddRef : unit -> unit
    abstract member RemoveRef : unit -> unit
    abstract member Update : caller : IAdaptiveObject -> unit

type IResource<'h when 'h : equality> =
    inherit IResource

    abstract member Handle : IMod<'h>

[<AbstractClass>]
type Resource<'h when 'h : equality>() =
    inherit AdaptiveObject()

    let handle = Mod.init Unchecked.defaultof<'h>
    let mutable refCount = 0
    let onDispose = new System.Reactive.Subjects.Subject<unit>()

    abstract member Create : Option<'h> -> 'h
    abstract member Destroy : 'h -> unit


    member internal x.OnDispose = onDispose :> IObservable<_>

    member x.AddRef() =
        if Interlocked.Increment(&refCount) = 1 then
            let h = x.Create None
            transact (fun () -> handle.Value <- h)

    member x.RemoveRef() =
        if Interlocked.Decrement(&refCount) = 0 then
            onDispose.OnNext()
            x.Destroy handle.Value
            transact (fun () -> handle.Value <- Unchecked.defaultof<_>)

    member x.Update(caller : IAdaptiveObject) =
        x.EvaluateIfNeeded caller () (fun () ->
            if refCount <= 0 then
                failwithf "[Resource] cannot update unreferenced resource"

            let h = x.Create (Some handle.Value)
            if h <> handle.Value then
                transact (fun () -> handle.Value <- h)
        )
    
    member x.Handle = handle :> IMod<_>

    member x.Dispose() = x.RemoveRef()

    interface IDisposable with
        member x.Dispose() = x.RemoveRef()

    interface IResource<'h> with
        member x.AddRef() = x.AddRef()
        member x.RemoveRef() = x.RemoveRef()
        member x.Handle = x.Handle
        member x.Update caller = x.Update caller

and ResourceCache<'h when 'h : equality>() =
    let store = ConcurrentDictionary<list<obj>, Resource<'h>>()

    member x.GetOrCreate(key : list<obj>, create : unit -> Resource<'h>) =
        let resource = 
            store.GetOrAdd(key, fun _ -> 
                let res = create()
                res.OnDispose.Add(fun () -> store.TryRemove key |> ignore)
                res
            )
        resource.AddRef()
        resource :> IResource<_>

    member x.Count = store.Count
    member x.Clear() = store.Clear()


type ResourceInputSet() =
    inherit AdaptiveObject()

    let all = ReferenceCountingSet<IResource>()
    let mutable dirty = HashSet<IResource>()

    override x.InputChanged(i : IAdaptiveObject) =
        match i with
            | :? IResource as r ->
                lock all (fun () ->
                    if all.Contains r then dirty.Add r |> ignore
                )
            | _ ->
                ()

    member x.Add(r : IResource) =
        lock all (fun () ->
            if all.Add r then
                lock r (fun () ->
                    if r.OutOfDate then dirty.Add r |> ignore
                )
               
        )

    member x.Remove(r : IResource) =
        lock all (fun () ->
            if all.Remove r then
                dirty.Remove r |> ignore
                lock r (fun () -> r.Outputs.Remove x |> ignore)
               
        )

    member x.Update (caller : IAdaptiveObject) =
        x.EvaluateIfNeeded caller () (fun () ->
            let rec run() = 
                let dirty = 
                    lock all (fun () ->
                        let d = dirty
                        dirty <- HashSet()
                        d
                    )

                if dirty.Count > 0 then
                    for d in dirty do
                        d.Update x

                    run()

            run()

        )

    member x.Dispose() =
        lock all (fun () ->
            for r in all do
                lock r (fun () -> r.Outputs.Remove x |> ignore)

            all.Clear()
            dirty.Clear()
        )

    interface IDisposable with
        member x.Dispose() = x.Dispose()