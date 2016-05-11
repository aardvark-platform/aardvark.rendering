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
    abstract member Update : caller : IAdaptiveObject -> FrameStatistics
    abstract member Kind : ResourceKind

type IResource<'h when 'h : equality> =
    inherit IResource

    abstract member Handle : IMod<'h>

type ResourceDescription<'d, 'h when 'h : equality> =
    {
        create : 'd -> 'h
        update : 'h -> 'd -> 'h
        delete : 'h -> unit
        kind : ResourceKind
    }

[<AbstractClass>]
type Resource<'h when 'h : equality>(kind : ResourceKind) =
    inherit AdaptiveObject()

    let mutable current = None
    let handle = Mod.init Unchecked.defaultof<'h>

    let mutable refCount = 0
    let onDispose = new System.Reactive.Subjects.Subject<unit>()
    let updateStats = { FrameStatistics.Zero with ResourceUpdateCount = 1.0; ResourceUpdateCounts = Map.ofList [kind, 1.0] }

    abstract member Create : Option<'h> -> 'h * FrameStatistics
    abstract member Destroy : 'h -> unit

    member x.Kind = kind

    member internal x.OnDispose = onDispose :> IObservable<_>

    member x.AddRef() =
        if Interlocked.Increment(&refCount) = 1 then
            let (h,_) = x.EvaluateAlways null (fun () -> x.Create None)
            current <- Some h
            transact (fun () -> handle.Value <- h)

    member x.RemoveRef() =
        if Interlocked.Decrement(&refCount) = 0 then
            onDispose.OnNext()
            x.Destroy handle.Value
            current <- None
            transact (fun () -> handle.Value <- Unchecked.defaultof<_>)

    member x.Update(caller : IAdaptiveObject) =
        x.EvaluateIfNeeded caller FrameStatistics.Zero (fun () ->
            if refCount <= 0 then
                failwithf "[Resource] cannot update unreferenced resource"

            let (h, stats) = x.Create current
            match current with
                | Some old when old = h -> updateStats + stats
                | _ -> 
                    current <- Some h
                    if h <> handle.Value then
                        transact (fun () -> handle.Value <- h)

                    updateStats + stats
        )
    
    member x.Handle = handle :> IMod<_>

    member x.Dispose() = x.RemoveRef()

    interface IDisposable with
        member x.Dispose() = x.RemoveRef()

    interface IResource<'h> with
        member x.Kind = kind
        member x.AddRef() = x.AddRef()
        member x.RemoveRef() = x.RemoveRef()
        member x.Handle = x.Handle
        member x.Update caller = x.Update caller

and ResourceCache<'h when 'h : equality>(parent : Option<ResourceCache<'h>>, renderTaskLock : Option<RenderTaskLock>) =
    let store = ConcurrentDictionary<list<obj>, Resource<'h>>()

    let acquire (old : Option<'h>) (m : IMod<'a>) =
        match old with
            | None ->
                match m with
                    | :? IOutputMod<'a> as om -> om.Acquire()
                    | _ -> ()

                match renderTaskLock, m with
                    | Some l, (:? ILockedResource as r) -> r.AddLock l
                    | _ -> ()

            | _ ->
                ()

    let release  (m : IMod<'a>) =
        match m with
            | :? IOutputMod<'a> as om -> om.Release()
            | _ -> ()

        match renderTaskLock, m with
            | Some l, (:? ILockedResource as r) -> r.RemoveLock l
            | _ -> ()

    let stats (m : IMod<'a>) =
        match m with
            | :? IOutputMod<'a> as om -> om.LastStatistics
            | _ -> FrameStatistics.Zero

    let tryGetParent (key : list<obj>) =
        match parent with
            | Some v -> v.TryGet key
            | None -> None

    member x.TryGet(key : list<obj>) =
        match store.TryGetValue(key) with
            | (true,v) -> Some v
            | _ -> None
     
    member x.GetOrCreate(key : list<obj>, create : unit -> Resource<'h>) =
        let resource =
            match tryGetParent key with
                | Some r -> r
                | None ->
                     store.GetOrAdd(key, fun _ -> 
                        let res = create()
                        res.OnDispose.Add(fun () -> store.TryRemove key |> ignore)
                        res
                     )
        resource.AddRef()
        resource :> IResource<_>

    member x.GetOrCreate<'a>(dataMod : IMod<'a>, desc : ResourceDescription<'a, 'h>) =
        let key = [dataMod :> obj]
        let resource = 
            match tryGetParent key with
                | Some v -> 
                    match dataMod with
                        | :? ILockedResource as r ->
                            { new Resource<'h>(v.Kind) with
                                member x.Create(old : Option<'h>) =
                                    acquire old dataMod
                                    v.Create old
                                member x.Destroy(h : 'h) =
                                    release dataMod
                                    v.Destroy h 
                            }
                        | _ -> v 
                | None ->
                    store.GetOrAdd(key, fun _ -> 
                        let mutable ownsHandle = false
                
                        { new Resource<'h>(desc.kind) with
                            member x.Create(old : Option<'h>) =
                                acquire old dataMod
                                let data = dataMod.GetValue x
                                let stats = stats dataMod

                                match old with
                                    | Some old ->
                                        match data :> obj with
                                            | :? 'h as handle ->
                                                if ownsHandle then desc.delete old
                                                ownsHandle <- false
                                                handle, stats

                                            | _ ->
                                                if ownsHandle then
                                                    let newHandle = desc.update old data
                                                    newHandle, stats
                                                else
                                                    let newHandle = desc.create data
                                                    ownsHandle <- true
                                                    newHandle, stats

                                    | None -> 


                                        match data :> obj with
                                            | :? 'h as handle -> 
                                                ownsHandle <- false
                                                handle, stats
                                            | _ ->
                                                let handle = desc.create data
                                                ownsHandle <- true
                                                handle, stats

                            member x.Destroy(h : 'h) =
                                release dataMod
                                if ownsHandle then
                                    ownsHandle <- false
                                    desc.delete h
                        }
                    )
        resource.AddRef()
        resource :> IResource<_>

    member x.Count = store.Count
    member x.Clear() = store.Clear()


type ResourceInputSet() =
    inherit DirtyTrackingAdaptiveObject<IResource>()

    let all = ReferenceCountingSet<IResource>()
//    let mutable dirty = HashSet<IResource>()

    let updateDirty(x : ResourceInputSet) =
        let rec run (level : int) (stats : FrameStatistics) = 
            let dirty = 
                let d = x.Dirty
                x.Dirty <- HashSet()
                d

            if level = 0 then
                dirty.IntersectWith all

            if level > 4 && dirty.Count > 0 then
                Log.warn "nested shit"

            let mutable stats = stats
            if dirty.Count > 0 then
                for d in dirty do
                    stats <- stats + d.Update x

                run (level + 1) stats
            else
                stats

        run 0 FrameStatistics.Zero


//    override x.InputChanged(i : IAdaptiveObject) =
//        match i with
//            | :? IResource as r ->
//                lock all (fun () ->
//                    if all.Contains r then dirty.Add r |> ignore
//                )
//            | _ ->
//                ()
//
//    

    member x.Count = all.Count

    member x.Add (r : IResource) =
        let needsUpdate =
            lock all (fun () ->
                if all.Add r then
                    lock r (fun () ->
                        if r.OutOfDate then 
                            x.Dirty.Add r |> ignore
                            true

                        else 
                            r.Outputs.Add x |> ignore
                            false
                    )
                else
                    false
            )

        if needsUpdate then
            Log.warn "adding outdated resource: %A" r.Kind
            updateDirty x |> ignore

    member x.Remove (r : IResource) =
        lock all (fun () ->

            if all.Remove r then
                x.Dirty.Remove r |> ignore
                lock r (fun () -> r.Outputs.Remove x |> ignore)
               
        )

    member x.Update (caller : IAdaptiveObject) =
        x.EvaluateIfNeeded caller FrameStatistics.Zero (fun () ->
            updateDirty x
        )

    member x.Dispose () =
        lock all (fun () ->
            for r in all do
                lock r (fun () -> r.Outputs.Remove x |> ignore)

            all.Clear()
            x.Dirty.Clear()
        )

    interface IDisposable with
        member x.Dispose() = x.Dispose()