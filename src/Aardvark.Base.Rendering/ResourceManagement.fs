namespace Aardvark.Base.Rendering

open System
open System.Threading
open System.Collections.Generic
open System.Collections.Concurrent
open Aardvark.Base
open Aardvark.Base.Incremental
open Microsoft.FSharp.NativeInterop

#nowarn "9"

type IOutputMod =
    inherit IMod
    abstract member Acquire : unit -> unit
    abstract member Release : unit -> unit
    abstract member GetValue : AdaptiveToken * RenderToken -> obj

type IOutputMod<'a> =
    inherit IMod<'a>
    inherit IOutputMod
    abstract member GetValue : AdaptiveToken * RenderToken -> 'a

[<AbstractClass>]
type AbstractOutputMod<'a>() =
    inherit AdaptiveObject()
    let mutable cache = Unchecked.defaultof<'a>
    let mutable refCount = 0

    abstract member Create : unit -> unit
    abstract member Destroy : unit -> unit
    abstract member Compute : AdaptiveToken * RenderToken -> 'a


    member x.Acquire() =
        if Interlocked.Increment(&refCount) = 1 then
            x.Create()

    member x.Release() =
        if Interlocked.Decrement(&refCount) = 0 then
            x.Destroy()

    member x.GetValue(token : AdaptiveToken, rt : RenderToken) =
        x.EvaluateAlways token (fun token ->
            if x.OutOfDate then
                cache <- x.Compute (token, rt)
            cache
        )

    member x.GetValue(token : AdaptiveToken) =
        x.GetValue(token, RenderToken.Empty)

    interface IMod with
        member x.IsConstant = false
        member x.GetValue(c) = x.GetValue(c) :> obj

    interface IMod<'a> with
        member x.GetValue(c) = x.GetValue(c)

    interface IOutputMod with
        member x.Acquire() = x.Acquire()
        member x.Release() = x.Release() 
        member x.GetValue(c,t) = x.GetValue(c,t) :> obj

    interface IOutputMod<'a> with
        member x.GetValue(c,t) = x.GetValue(c,t)

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
    abstract member Update : token : AdaptiveToken * rt : RenderToken -> unit
    abstract member Kind : ResourceKind
    abstract member IsDisposed : bool
    abstract member Info : ResourceInfo

type IResource<'h when 'h : equality> =
    inherit IResource
    abstract member Handle : IMod<'h>

type IResource<'h, 'v when 'h : equality and 'v : unmanaged> =
    inherit IResource<'h>
    abstract member Pointer : nativeptr<'v>

type ResourceDescription<'d, 'h, 'v when 'h : equality and 'v : unmanaged> =
    {
        create : 'd -> 'h
        update : 'h -> 'd -> 'h
        delete : 'h -> unit
        view   : 'h -> 'v
        info   : 'h -> ResourceInfo
        kind   : ResourceKind
    }

[<AbstractClass>]
type Resource<'h, 'v when 'h : equality and 'v : unmanaged>(kind : ResourceKind) =
    inherit AdaptiveObject()

    let mutable current = None
    let handle = Mod.init Unchecked.defaultof<'h>
    let pointer : nativeptr<'v> = NativePtr.alloc 1

    let mutable refCount = 0
    let onDispose = new System.Reactive.Subjects.Subject<unit>()




    let mutable info = ResourceInfo.Zero
    let lockObj = obj()

    let destroy(x : Resource<_,_>) =
        onDispose.OnNext()
        x.Destroy handle.Value
        current <- None
        info <- ResourceInfo.Zero
        NativePtr.free pointer

        lock x (fun () ->
            let mutable foo = 0
            x.Outputs.Consume(&foo) |> ignore
            x.OutOfDate <- true
            handle.UnsafeCache <- Unchecked.defaultof<_>
        )

    let setHandle (x : Resource<'h, 'v>) (h : 'h) : unit =
        let v : 'v = x.View(h)
        NativePtr.write pointer v
        if h <> handle.Value then
            transact (fun () -> handle.Value <- h)

    abstract member Create : AdaptiveToken * RenderToken * Option<'h> -> 'h
    abstract member Destroy : 'h -> unit
    abstract member GetInfo : 'h -> ResourceInfo
    abstract member View : 'h -> 'v



    member x.IsDisposed = Option.isNone current

    member x.Info = info

    member x.Kind = kind

    member internal x.OnDispose = onDispose :> IObservable<_>

    member private x.PerformUpdate(token : AdaptiveToken, t : RenderToken) =
        if refCount <= 0 then
            failwithf "[Resource] cannot update unreferenced resource"

        let oldInfo = info
        let h = x.Create(token, t,current)
        info <- x.GetInfo h
        let memDelta = info.AllocatedSize - oldInfo.AllocatedSize

        match current with
            | Some old when old = h -> 
                t.InPlaceResourceUpdate(kind)

            | Some old ->  
                current <- Some h
                setHandle x h
                t.ReplacedResource(kind)

            | None -> 
                current <- Some h
                setHandle x h
                t.CreatedResource(kind)

    member x.AddRef() =
        lock lockObj (fun () -> 
            if Interlocked.Increment(&refCount) = 1 then
                x.ForceUpdate(AdaptiveToken.Top, RenderToken.Empty) |> ignore
        )

    member x.RemoveRef() =
        lock lockObj (fun () -> 
            if Interlocked.Decrement(&refCount) = 0 then
                destroy x
        )

    member x.Update(token : AdaptiveToken, rt : RenderToken) =
        x.EvaluateAlways token (fun token ->
            if x.OutOfDate then
                x.PerformUpdate(token, rt)
        )
  
    member x.ForceUpdate(token : AdaptiveToken, rt : RenderToken) =
        x.EvaluateAlways token (fun token ->
            x.PerformUpdate(token, rt)
        )
    
    member x.Handle = handle :> IMod<_>

    member x.Dispose() = x.RemoveRef()

    member x.ForceDispose() =
        lock lockObj (fun () -> 
            if Interlocked.Exchange(&refCount, 0) <> 0 then
                destroy x
        )

    interface IDisposable with
        member x.Dispose() = x.RemoveRef()

    interface IResource<'h, 'v> with
        member x.IsDisposed = x.IsDisposed
        member x.Kind = kind
        member x.AddRef() = x.AddRef()
        member x.RemoveRef() = x.RemoveRef()
        member x.Handle = x.Handle
        member x.Update(token, rt) = x.Update(token, rt)
        member x.Info = x.Info
        member x.Pointer = pointer

and ResourceCache<'h, 'v when 'h : equality and 'v : unmanaged>(parent : Option<ResourceCache<'h, 'v>>, renderTaskLock : Option<RenderTaskLock>) =
    let store = ConcurrentDictionary<list<obj>, Resource<'h, 'v>>()
    static let hNonPrimitive =
        not typeof<'h>.IsPrimitive && not typeof<'h>.IsEnum


    let acquireOutput (old : Option<'x>) (m : IMod<'a>) =
        match old with
            | None ->
                match m with
                    | :? IOutputMod<'a> as om -> om.Acquire()
                    | _ -> ()

            | _ ->
                ()

    let releaseOutput  (m : IMod<'a>) =
        match m with
            | :? IOutputMod<'a> as om -> om.Release()
            | _ -> ()

    let acquireLock (v : 'a) =
        match renderTaskLock, v :> obj with
            | Some rt, (:? ILockedResource as l) -> rt.Add l
            | _ -> ()

    let releaseLock (v : 'a) =
        match renderTaskLock, v :> obj with
            | Some rt, (:? ILockedResource as l) -> rt.Remove l
            | _ -> ()
        

    let eval (m : IMod<'a>) (rt : RenderToken) (token : AdaptiveToken) =
        match m with
            | :? IOutputMod<'a> as om -> om.GetValue(token, rt) 
            | _ -> m.GetValue(token)

    let tryGetParent (key : list<obj>) =
        match parent with
            | Some v -> v.TryGet key
            | None -> None

    member x.TryGet(key : list<obj>) =
        match store.TryGetValue(key) with
            | (true,v) -> Some v
            | _ -> None
     
    member x.GetOrCreateLocalWrapped<'x, 'y when 'x : equality and 'y : unmanaged>(key : list<obj>, create : unit -> Resource<'x, 'y>, wrap : Resource<'x, 'y> -> Resource<'h, 'v>) =
        let resource = 
            store.GetOrAdd(key, fun _ -> 
                let res = create()
                res.OnDispose.Add(fun () -> store.TryRemove key |> ignore)
                wrap res
            )
        resource.AddRef()
        resource :> IResource<_,_>

    member x.GetOrCreateLocal(key : list<obj>, create : unit -> Resource<'h, 'v>) =
        x.GetOrCreateLocalWrapped<'h, 'v>(key, create, id)

    member x.GetOrCreate(key : list<obj>, create : unit -> Resource<'h, 'v>) =
        match tryGetParent key with
            | Some r -> 
                r.AddRef()
                r :> IResource<_,_>
            | None -> x.GetOrCreateLocal(key, create)

    member x.GetOrCreateWrapped<'x, 'y when 'x : equality and 'y : unmanaged>(key : list<obj>, create : unit -> Resource<'x, 'y>, wrap : Resource<'x, 'y> -> Resource<'h, 'v>) =
        match tryGetParent key with
            | Some r -> 
                r.AddRef()
                r :> IResource<_,_>
            | None -> 
                x.GetOrCreateLocalWrapped(key, create, wrap)

    member x.GetOrCreateWrapped<'a, 'x, 'y when 'x : equality and 'y : unmanaged>(dataMod : IMod<'a>, additionalKeys : list<obj>, desc : ResourceDescription<'a, 'x, 'y>, wrap : Resource<'x, 'y> -> Resource<'h, 'v>) =
        let key = (dataMod :> obj)::additionalKeys
        match tryGetParent key with
            | Some v -> 
                match dataMod with
                    | :? ILockedResource as r ->
                        x.GetOrCreateLocal(key, fun () ->
                            v.AddRef()
                            let mutable oldData = None
                            { new Resource<'h, 'v>(v.Kind) with
                                member x.View (h : 'h) =
                                    v.View(h)

                                member x.GetInfo(h : 'h) =
                                    v.GetInfo h

                                member x.Create(token : AdaptiveToken, rt : RenderToken, old : Option<'h>) =
                                    let newData = dataMod.GetValue token
                                    match oldData with
                                        | Some d -> releaseLock d
                                        | None -> ()
                                    oldData <- Some newData
                                    acquireLock newData
                                    acquireOutput old dataMod

                                    let stats = v.Update(token, rt)
                                    v.Handle.GetValue()

                                member x.Destroy(h : 'h) =
                                    match oldData with
                                        | Some d -> releaseLock d; oldData <- None
                                        | None -> ()
                                    releaseOutput dataMod
                                    lock v (fun () -> 
                                        v.Outputs.Remove x |> ignore
                                        v.Handle.Outputs.Remove x |> ignore
                                    )
                                    v.RemoveRef()
                            }
                        )
                    | _ ->
                        v.AddRef()
                        v :> IResource<_,_>
            | None ->
                x.GetOrCreateLocal(key, fun _ -> 
                    let mutable ownsHandle = false
                    let mutable oldData = None
                    let xNonPrimitive = not typeof<'x>.IsPrimitive && not typeof<'x>.IsEnum


                    let resource = 
                        { new Resource<'x, 'y>(desc.kind) with
                            member x.View (h : 'x) =
                                desc.view h

                            member x.GetInfo (h : 'x) =
                                desc.info h

                            member x.Create(token : AdaptiveToken, rt : RenderToken, old : Option<'x>) =
                                acquireOutput old dataMod
                                let data = eval dataMod rt token

                                match oldData with
                                    | Some d -> releaseLock d
                                    | None -> ()
                                acquireLock data
                                oldData <- Some data

                                match old with
                                    | Some old ->
                                        match data :> obj with
                                            | :? 'x as handle when xNonPrimitive ->
                                                if ownsHandle then desc.delete old
                                                ownsHandle <- false
                                                handle

                                            | _ ->
                                                if ownsHandle then
                                                    let newHandle = desc.update old data
                                                    newHandle
                                                else
                                                    let newHandle = desc.create data
                                                    ownsHandle <- true
                                                    newHandle

                                    | None -> 
                                        match data :> obj with
                                            | :? 'x as handle when xNonPrimitive -> 
                                                ownsHandle <- false
                                                handle
                                            | _ ->
                                                let handle = desc.create data
                                                ownsHandle <- true
                                                handle

                            member x.Destroy(h : 'x) =
                                match oldData with
                                    | Some d -> releaseLock d
                                    | _ -> ()

                                releaseOutput dataMod
                                if ownsHandle then
                                    ownsHandle <- false
                                    desc.delete h
                        }

                    wrap resource
                )

    member x.GetOrCreate<'a>(dataMod : IMod<'a>, additionalKeys : list<obj>, desc : ResourceDescription<'a, 'h, 'v>) =
        x.GetOrCreateWrapped<'a, 'h, 'v>(dataMod, additionalKeys, desc, id)

    member x.GetOrCreateDependent<'a, 'b when 'a : equality and 'b : unmanaged>(res : IResource<'a, 'b>, additionalKeys : list<obj>, desc : ResourceDescription<'a, 'h, 'v>) =
        let key = (res :> obj)::additionalKeys
        match tryGetParent key with
            | Some v -> 
                v.AddRef()
                v :> IResource<_,_>
            | None ->
                x.GetOrCreateLocal(key, fun _ -> 
                    let mutable ownsHandle = false
                
                    { new Resource<'h,'v>(desc.kind) with
                        member x.View(h : 'h) =
                            desc.view h

                        member x.GetInfo (h : 'h) =
                            desc.info h

                        member x.Create(token : AdaptiveToken, rt : RenderToken, old : Option<'h>) =
                            
                            let stats = res.Update(token, rt)
                            let data = res.Handle.GetValue()

                            match old with
                                | Some old ->
                                    match data :> obj with
                                        | :? 'h as handle when hNonPrimitive ->
                                            if ownsHandle then desc.delete old
                                            ownsHandle <- false
                                            handle

                                        | _ ->
                                            if ownsHandle then
                                                let newHandle = desc.update old data
                                                newHandle
                                            else
                                                let newHandle = desc.create data
                                                ownsHandle <- true
                                                newHandle

                                | None -> 
                                    match data :> obj with
                                        | :? 'h as handle when hNonPrimitive -> 
                                            ownsHandle <- false
                                            handle
                                        | _ ->
                                            let handle = desc.create data
                                            ownsHandle <- true
                                            handle

                        member x.Destroy(h : 'h) =
                            res.RemoveRef()
                            res.RemoveOutput x
                            if ownsHandle then
                                ownsHandle <- false
                                desc.delete h
                    }
                )


    member x.GetOrCreate<'a>(dataMod : IMod<'a>, desc : ResourceDescription<'a, 'h, 'v>) =
        x.GetOrCreate(dataMod, [], desc)

    member x.Count = store.Count
    member x.Clear() = 
        let remaining = store |> Seq.map (fun (KeyValue(_,r)) -> r) |> Seq.toArray
        for r in remaining do
            Log.warn "leaking resource: %A" r
            r.ForceDispose()
        store.Clear()

type ConstantResource<'h, 'v when 'h : equality and 'v : unmanaged>(kind : ResourceKind, handle : 'h, v : 'v) =
    inherit ConstantObject()

    let h = Mod.constant handle
    let ptr = NativePtr.alloc 1
    do NativePtr.write ptr v

    member x.Handle = handle

    override x.GetHashCode() = handle.GetHashCode()

    override x.Equals o =
        match o with
            | :? ConstantResource<'h, 'v> as o -> o.Handle = x.Handle
            | _ -> false

    interface IDisposable with
        member x.Dispose() = ()

    interface IResource<'h, 'v> with
        member x.IsDisposed = false
        member x.Kind = kind
        member x.AddRef() = ()
        member x.RemoveRef() = ()
        member x.Handle = h
        member x.Update(token, caller) = ()
        member x.Info = ResourceInfo.Zero
        member x.Pointer = ptr

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


    let updateOne (token : AdaptiveToken) (r : IResource) (t : RenderToken) =
        let oldInfo = r.Info
        r.Update(token, t)
        let newInfo = r.Info
        ()

    let updateDirty (x : ResourceInputSet) (token : AdaptiveToken) (rt : RenderToken) =
        let rec run (level : int) (token : AdaptiveToken) (rt : RenderToken) = 
            let dirty = 
                let d = x.Dirty
                x.Dirty <- HashSet()
                d

//            if level = 0 then
//                dirty.IntersectWith all

            if level > 4 && dirty.Count > 0 then
                Log.warn "nested shit"

            if dirty.Count > 0 then
                for d in dirty do
                    if not d.IsDisposed then
                        updateOne token d rt

                run (level + 1) token rt

        run 0 token rt

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
            x.EvaluateAlways AdaptiveToken.Top (fun token -> 
                updateDirty x token RenderToken.Empty
            )

    member x.Remove (r : IResource) =
        lock all (fun () ->
            if all.Remove r then
                x.Dirty.Remove r |> ignore
                lock r (fun () -> r.Outputs.Remove x |> ignore)
        )

    member x.Update (token : AdaptiveToken, rt : RenderToken) =
        x.EvaluateAlways token  (fun token -> 
            if x.OutOfDate then
                updateDirty x token rt
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