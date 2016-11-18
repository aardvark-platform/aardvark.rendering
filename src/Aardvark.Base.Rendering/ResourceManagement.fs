namespace Aardvark.Base.Rendering

open System
open System.Threading
open System.Collections.Generic
open System.Collections.Concurrent
open Aardvark.Base
open Aardvark.Base.Incremental



type IOutputMod<'a> =
    inherit IMod<'a>
    abstract member LastStatistics : FrameStatistics
    abstract member Acquire : unit -> unit
    abstract member Release : unit -> unit



type ResourceInfo = 
    struct
        val mutable public AllocatedSize : Mem
        val mutable public UsedSize : Mem

        static member Zero = ResourceInfo(Mem.Zero, Mem.Zero)

        static member (+) (l : ResourceInfo, r : ResourceInfo) =
            ResourceInfo(
                l.AllocatedSize + r.AllocatedSize,
                l.UsedSize + r.UsedSize
            )

        static member (-) (l : ResourceInfo, r : ResourceInfo) =
            ResourceInfo(
                l.AllocatedSize - r.AllocatedSize,
                l.UsedSize - r.UsedSize
            )

        static member (*) (l : ResourceInfo, r : int) =
            ResourceInfo(
                l.AllocatedSize * r,
                l.UsedSize * r
            )

        static member (*) (l : ResourceInfo, r : float) =
            ResourceInfo(
                l.AllocatedSize * r,
                l.UsedSize * r
            )

        static member (*) (l : int, r : ResourceInfo) =
            ResourceInfo(
                l * r.AllocatedSize,
                l * r.UsedSize
            )

        static member (*) (l : float, r : ResourceInfo) =
            ResourceInfo(
                l * r.AllocatedSize,
                l * r.UsedSize
            )

        static member (/) (l : ResourceInfo, r : int) =
            ResourceInfo(
                l.AllocatedSize / r,
                l.UsedSize / r
            )

        static member (/) (l : ResourceInfo, r : float) =
            ResourceInfo(
                l.AllocatedSize / r,
                l.UsedSize / r
            )


        new(a,u) = { AllocatedSize = a; UsedSize = u }
        new(s) = { AllocatedSize = s; UsedSize = s }
    end


type IResource =
    inherit IAdaptiveObject
    inherit IDisposable  
    abstract member AddRef : unit -> unit
    abstract member RemoveRef : unit -> unit
    abstract member Update : caller : IAdaptiveObject -> FrameStatistics
    abstract member Kind : ResourceKind
    abstract member IsDisposed : bool
    abstract member Info : ResourceInfo


type IResource<'h when 'h : equality> =
    inherit IResource

    abstract member Handle : IMod<'h>

type ResourceDescription<'d, 'h when 'h : equality> =
    {
        create : 'd -> 'h
        update : 'h -> 'd -> 'h
        delete : 'h -> unit
        info   : 'h -> ResourceInfo
        kind   : ResourceKind
    }

[<AbstractClass>]
type Resource<'h when 'h : equality>(kind : ResourceKind) =
    inherit AdaptiveObject()

    let mutable current = None
    let handle = Mod.init Unchecked.defaultof<'h>

    let mutable refCount = 0
    let onDispose = new System.Reactive.Subjects.Subject<unit>()
    //let updateStats = ResourceDeltas(kind, ResourceDelta(1, 0, 0, 0,  { FrameStatistics.Zero with ResourceUpdateCount = 1.0; ResourceUpdateCounts = Map.ofList [kind, 1.0] }

    let inPlaceStats (oldInfo : ResourceInfo) (newInfo : ResourceInfo) =
        { FrameStatistics.Zero with
            ResourceDeltas = ResourceDeltas(kind, ResourceDelta(0.0, 0.0, 1.0, 0.0, newInfo.AllocatedSize - oldInfo.AllocatedSize))
        }

    let replaceStats (oldInfo : ResourceInfo) (newInfo : ResourceInfo) =
        { FrameStatistics.Zero with
            ResourceDeltas = ResourceDeltas(kind, ResourceDelta(0.0, 0.0, 0.0, 1.0, newInfo.AllocatedSize - oldInfo.AllocatedSize))
        }

    let createStats (newInfo : ResourceInfo) =
        { FrameStatistics.Zero with
            ResourceDeltas = ResourceDeltas(kind, ResourceDelta(1.0, 0.0, 0.0, 0.0, newInfo.AllocatedSize))
        }

    let mutable info = ResourceInfo.Zero
    let lockObj = obj()

    abstract member Create : Option<'h> -> 'h * FrameStatistics
    abstract member Destroy : 'h -> unit
    abstract member GetInfo : 'h -> ResourceInfo

    member x.IsDisposed = Option.isNone current

    member x.Info = info

    member x.Kind = kind

    member internal x.OnDispose = onDispose :> IObservable<_>

    member private x.PerformUpdate() =
        if refCount <= 0 then
            failwithf "[Resource] cannot update unreferenced resource"

        let oldInfo = info
        let (h, stats) = x.Create current
        info <- x.GetInfo h
        let memDelta = info.AllocatedSize - oldInfo.AllocatedSize

        match current with
            | Some old when old = h -> 
                stats + inPlaceStats oldInfo info

            | Some old ->  
                current <- Some h
                if h <> handle.Value then transact (fun () -> handle.Value <- h)
                stats + replaceStats oldInfo info

            | None -> 
                current <- Some h
                if h <> handle.Value then transact (fun () -> handle.Value <- h)
                stats + createStats info

    member x.AddRef() =
        lock lockObj (fun () -> 
            if Interlocked.Increment(&refCount) = 1 then
                x.ForceUpdate null |> ignore
        )

    member x.RemoveRef() =
        lock lockObj (fun () -> 
            if Interlocked.Decrement(&refCount) = 0 then
                onDispose.OnNext()
                x.Destroy handle.Value
                current <- None
                info <- ResourceInfo.Zero
                transact (fun () -> 
                    x.MarkOutdated()
                    handle.Value <- Unchecked.defaultof<_>
                )
        )

    member x.Update(caller : IAdaptiveObject) =
        x.EvaluateIfNeeded caller FrameStatistics.Zero (fun () ->
            x.PerformUpdate()
        )
  
    member x.ForceUpdate(caller : IAdaptiveObject) =
        x.EvaluateAlways caller (fun () ->
            x.PerformUpdate()
        )
    
    member x.Handle = handle :> IMod<_>

    member x.Dispose() = x.RemoveRef()

    member x.ForceDispose() =
        lock lockObj (fun () -> 
            if Interlocked.Exchange(&refCount, 0) <> 0 then
                onDispose.OnNext()
                x.Destroy handle.Value
                current <- None
                info <- ResourceInfo.Zero
                transact (fun () -> 
                    x.MarkOutdated()
                    handle.Value <- Unchecked.defaultof<_>
                )
        )

    interface IDisposable with
        member x.Dispose() = x.RemoveRef()

    interface IResource<'h> with
        member x.IsDisposed = x.IsDisposed
        member x.Kind = kind
        member x.AddRef() = x.AddRef()
        member x.RemoveRef() = x.RemoveRef()
        member x.Handle = x.Handle
        member x.Update caller = x.Update caller
        member x.Info = x.Info

and ResourceCache<'h when 'h : equality>(parent : Option<ResourceCache<'h>>, renderTaskLock : Option<RenderTaskLock>) =
    let store = ConcurrentDictionary<list<obj>, Resource<'h>>()

    let acquire (old : Option<'x>) (m : IMod<'a>) =
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
     
    member x.GetOrCreateLocalWrapped<'x when 'x : equality>(key : list<obj>, create : unit -> Resource<'x>, wrap : Resource<'x> -> Resource<'h>) =
        let resource = 
            store.GetOrAdd(key, fun _ -> 
                let res = create()
                res.OnDispose.Add(fun () -> store.TryRemove key |> ignore)
                wrap res
            )
        resource.AddRef()
        resource :> IResource<_>

    member x.GetOrCreateLocal(key : list<obj>, create : unit -> Resource<'h>) =
        x.GetOrCreateLocalWrapped<'h>(key, create, id)

    member x.GetOrCreate(key : list<obj>, create : unit -> Resource<'h>) =
        match tryGetParent key with
            | Some r -> 
                r.AddRef()
                r :> IResource<_>
            | None -> x.GetOrCreateLocal(key, create)

    member x.GetOrCreateWrapped<'x when 'x : equality>(key : list<obj>, create : unit -> Resource<'x>, wrap : Resource<'x> -> Resource<'h>) =
        match tryGetParent key with
            | Some r -> 
                r.AddRef()
                r :> IResource<_>
            | None -> 
                x.GetOrCreateLocalWrapped(key, create, wrap)

    member x.GetOrCreateWrapped<'a, 'x when 'x : equality>(dataMod : IMod<'a>, additionalKeys : list<obj>, desc : ResourceDescription<'a, 'x>, wrap : Resource<'x> -> Resource<'h>) =
        let key = (dataMod :> obj)::additionalKeys
        match tryGetParent key with
            | Some v -> 
                match dataMod with
                    | :? ILockedResource as r ->
                        x.GetOrCreateLocal(key, fun () ->
                            v.AddRef()
                            { new Resource<'h>(v.Kind) with
                                member x.GetInfo(h : 'h) =
                                    v.GetInfo h

                                member x.Create(old : Option<'h>) =
                                    acquire old dataMod
                                    let stats = v.Update(x)
                                    v.Handle.GetValue(), stats

                                member x.Destroy(h : 'h) =
                                    release dataMod
                                    lock v (fun () -> 
                                        v.Outputs.Remove x |> ignore
                                        v.Handle.Outputs.Remove x |> ignore
                                    )
                                    v.RemoveRef()
                            }
                        )
                    | _ ->
                        v.AddRef()
                        v :> IResource<_>
            | None ->
                x.GetOrCreateLocal(key, fun _ -> 
                    let mutable ownsHandle = false
                
                    let resource = 
                        { new Resource<'x>(desc.kind) with
                            member x.GetInfo (h : 'x) =
                                desc.info h

                            member x.Create(old : Option<'x>) =
                                acquire old dataMod
                                let data = dataMod.GetValue x
                                let stats = stats dataMod

                                match old with
                                    | Some old ->
                                        match data :> obj with
                                            | :? 'x as handle ->
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
                                            | :? 'x as handle -> 
                                                ownsHandle <- false
                                                handle, stats
                                            | _ ->
                                                let handle = desc.create data
                                                ownsHandle <- true
                                                handle, stats

                            member x.Destroy(h : 'x) =
                                release dataMod
                                if ownsHandle then
                                    ownsHandle <- false
                                    desc.delete h
                        }

                    wrap resource
                )

    member x.GetOrCreate<'a>(dataMod : IMod<'a>, additionalKeys : list<obj>, desc : ResourceDescription<'a, 'h>) =
        x.GetOrCreateWrapped<'a, 'h>(dataMod, additionalKeys, desc, id)

    member x.GetOrCreateDependent<'a when 'a : equality>(res : IResource<'a>, additionalKeys : list<obj>, desc : ResourceDescription<'a, 'h>) =
        let key = (res :> obj)::additionalKeys
        match tryGetParent key with
            | Some v -> 
                v.AddRef()
                v :> IResource<_>
            | None ->
                x.GetOrCreateLocal(key, fun _ -> 
                    let mutable ownsHandle = false
                
                    { new Resource<'h>(desc.kind) with
                        member x.GetInfo (h : 'h) =
                            desc.info h

                        member x.Create(old : Option<'h>) =
                            
                            let stats = res.Update(x)
                            let data = res.Handle.GetValue()

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
                            res.RemoveRef()
                            res.RemoveOutput x
                            if ownsHandle then
                                ownsHandle <- false
                                desc.delete h
                    }
                )


    member x.GetOrCreate<'a>(dataMod : IMod<'a>, desc : ResourceDescription<'a, 'h>) =
        x.GetOrCreate(dataMod, [], desc)

    member x.Count = store.Count
    member x.Clear() = 
        let remaining = store |> Seq.map (fun (KeyValue(_,r)) -> r) |> Seq.toArray
        for r in remaining do
            Log.warn "leaking resource: %A" r
            r.ForceDispose()
        store.Clear()

type ConstantResource<'h when 'h : equality>(kind : ResourceKind, handle : 'h) =
    inherit ConstantObject()

    let h = Mod.constant handle

    member x.Handle = handle

    override x.GetHashCode() = handle.GetHashCode()

    override x.Equals o =
        match o with
            | :? ConstantResource<'h> as o -> o.Handle = x.Handle
            | _ -> false

    interface IDisposable with
        member x.Dispose() = ()

    interface IResource<'h> with
        member x.IsDisposed = false
        member x.Kind = kind
        member x.AddRef() = ()
        member x.RemoveRef() = ()
        member x.Handle = h
        member x.Update caller = FrameStatistics.Zero
        member x.Info = ResourceInfo.Zero

type InputSet(o : IAdaptiveObject) =
    let l = obj()
    let inputs = ReferenceCountingSet<IAdaptiveObject>()

    member x.Add(m : IAdaptiveObject) = 
        lock l (fun () ->
            if inputs.Add m then
                m.Outputs.Add o |> ignore
        )

    member x.Remove (m : IAdaptiveObject) = 
        lock l (fun () ->
            if inputs.Remove m then
                m.Outputs.Remove o |> ignore
        )

type ResourceInputSet() =
    inherit DirtyTrackingAdaptiveObject<IResource>()

    let all = ReferenceCountingSet<IResource>()
    let mutable resourceCounts = ResourceCounts.Zero

    let applyResourceDeltas (deltas : ResourceDeltas) =
        resourceCounts <- resourceCounts + deltas

    let updateOne (x : ResourceInputSet) (r : IResource)  =
        let oldInfo = r.Info
        let ret = r.Update x
        let newInfo = r.Info
        applyResourceDeltas ret.ResourceDeltas
        ret

    let updateDirty(x : ResourceInputSet) =
        let rec run (level : int) (stats : FrameStatistics) = 
            let dirty = 
                let d = x.Dirty
                x.Dirty <- HashSet()
                d

//            if level = 0 then
//                dirty.IntersectWith all

            if level > 4 && dirty.Count > 0 then
                Log.warn "nested shit"

            let mutable stats = stats
            if dirty.Count > 0 then
                for d in dirty do
                    if not d.IsDisposed then
                        stats <- stats + updateOne x d

                run (level + 1) stats
            else
                stats

        run 0 FrameStatistics.Zero

    member x.ResourceCounts = resourceCounts

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
            x.EvaluateAlways null (fun () -> 
                updateDirty x |> ignore
            )

    member x.Remove (r : IResource) =
        lock all (fun () ->

            if all.Remove r then
                let deltas = ResourceDeltas(r.Kind, ResourceDelta(0.0, 1.0, 0.0, 0.0, -r.Info.AllocatedSize))

                x.Dirty.Remove r |> ignore
                lock r (fun () -> r.Outputs.Remove x |> ignore)
                applyResourceDeltas deltas
        )

    member x.Update (caller : IAdaptiveObject) =
        let updateStats = 
            x.EvaluateIfNeeded caller FrameStatistics.Zero (fun () -> updateDirty x)

        { updateStats with
            PhysicalResourceCount = float all.Count 
            ResourceCounts = resourceCounts
        }

    member x.Dispose () =
        lock all (fun () ->
            for r in all do
                lock r (fun () -> r.Outputs.Remove x |> ignore)

            all.Clear()
            x.Dirty.Clear()
        )

    interface IDisposable with
        member x.Dispose() = x.Dispose()