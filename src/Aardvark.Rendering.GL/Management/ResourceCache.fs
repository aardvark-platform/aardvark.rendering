namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Collections.Concurrent
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open FSharp.NativeInterop

#nowarn "9"

type internal ResourceDescription<'Data, 'Handle, 'View when 'View : unmanaged> =
    {
        create : 'Data -> 'Handle
        update : 'Handle -> 'Data -> 'Handle
        delete : 'Handle -> unit
        view   : 'Handle -> 'View
        info   : 'Handle -> ResourceInfo
        kind   : ResourceKind
    }

[<AbstractClass>]
type internal Resource<'Handle, 'View when 'View : unmanaged>(kind : ResourceKind) =
    inherit AdaptiveObject()
    static let handleType = typeof<'Handle>

    let mutable current = None
    let handle = AVal.init Unchecked.defaultof<'Handle>
    let pointer : nativeptr<'View> = NativePtr.alloc 1

    let mutable refCount = 0
    let onDispose = new Event<unit>()

    let mutable info = ResourceInfo.Zero
    let lockObj = obj()
    let mutable wasDisposed = 0

    let t = new Transaction() // reusable transaction

    let destroy(x : Resource<_,_>) =
        let alreadyDisposed = Interlocked.CompareExchange(&wasDisposed,1,0)
        if alreadyDisposed = 1 then failwithf "doubleFree"

        onDispose.Trigger()
        x.Destroy handle.Value
        current <- None
        info <- ResourceInfo.Zero
        NativePtr.free pointer

        lock x (fun () ->
            x.Outputs.Clear()
            x.OutOfDate <- true
        )

    let setHandle (x : Resource<'Handle, 'View>) (h : 'Handle) : unit =
        let v : 'View = x.View(h)
        NativePtr.write pointer v

        handle.Level <- max (1 + x.Level) handle.Level
        if not (Unchecked.equals h handle.Value) then
            useTransaction t (fun () -> handle.Value <- h)
            t.Commit()
            t.Dispose()

    let id = newId()

    abstract member Create : AdaptiveToken * RenderToken * Option<'Handle> -> 'Handle
    abstract member Destroy : 'Handle -> unit
    abstract member GetInfo : 'Handle -> ResourceInfo
    abstract member View : 'Handle -> 'View


    member x.HandleType = handleType
    member x.IsDisposed = Option.isNone current

    member x.Info = info

    member x.Kind = kind

    member internal x.OnDispose = onDispose.Publish :> IObservable<_>

    member private x.PerformUpdate(token : AdaptiveToken, t : RenderToken) =
        if refCount <= 0 then
            // [hs 21.12.2020] was (and should be) exn. https://github.com/vrvis/PRo3D/issues/3
            // untill no artificial repro is found, we need to go with this.
            Log.warn "[Resource] cannot update unreferenced resource (refCount = %d, x.IsDisposed = %b, th = %A)" refCount x.IsDisposed handleType
        else
            let h = x.Create(token, t,current)
            info <- x.GetInfo h
            setHandle x h

            match current with
            | Some old when Unchecked.equals old h ->
                t.InPlaceResourceUpdate(kind)

            | Some old ->
                current <- Some h
                t.ReplacedResource(kind)

            | None ->
                current <- Some h
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

    member x.Handle = handle :> aval<_>

    member x.Dispose() = x.RemoveRef()

    member x.ForceDispose() =
        lock lockObj (fun () ->
            if Interlocked.Exchange(&refCount, 0) <> 0 then
                destroy x
        )

    interface IDisposable with
        member x.Dispose() = x.RemoveRef()

    interface IResource<'Handle, 'View> with
        member x.Id = id
        member x.HandleType = handleType
        member x.IsDisposed = x.IsDisposed
        member x.Kind = kind
        member x.AddRef() = x.AddRef()
        member x.RemoveRef() = x.RemoveRef()
        member x.Handle = x.Handle
        member x.Update(token, rt) = x.Update(token, rt)
        member x.Info = x.Info
        member x.Pointer = pointer

and internal ResourceCache<'Handle, 'View when 'View : unmanaged>(parent : Option<ResourceCache<'Handle, 'View>>, renderTaskLock : Option<RenderTaskLock>) =
    let store = ConcurrentDictionary<list<obj>, Resource<'Handle, 'View>>()

    static let handleNonPrimitive =
        not typeof<'Handle>.IsPrimitive && not typeof<'Handle>.IsEnum

    let acquireLock (data : 'Data) =
        match renderTaskLock, data :> obj with
        | Some rt, (:? ILockedResource as l) -> rt.Add l
        | _ -> ()

    let releaseLock (data : 'Data) =
        match renderTaskLock, data :> obj with
        | Some rt, (:? ILockedResource as l) -> rt.Remove l
        | _ -> ()

    let tryGetParent (key : list<obj>) =
        match parent with
        | Some v -> v.TryGet key
        | None -> None

    member x.TryGet(key : list<obj>) =
        match store.TryGetValue(key) with
        | (true, v) -> Some v
        | _ -> None

    member x.GetOrCreateLocal(key : list<obj>, create : unit -> Resource<'Handle, 'View>) =
        let resource =
            store.GetOrAdd(key, fun _ ->
                let res = create()
                res.OnDispose.Add(fun () -> store.TryRemove key |> ignore)
                res
            )
        resource.AddRef()
        resource :> IResource<_,_>

    member x.GetOrCreate(key : list<obj>, create : unit -> Resource<'Handle, 'View>) =
        match tryGetParent key with
        | Some r ->
            r.AddRef()
            r :> IResource<_,_>
        | None ->
            x.GetOrCreateLocal(key, create)

    member x.GetOrCreate<'Data>(dataMod : aval<'Data>, additionalKeys : list<obj>, creator : unit -> ResourceDescription<'Data, 'Handle, 'View>) =
        let key = (dataMod :> obj)::additionalKeys
        match tryGetParent key with
        | Some v ->
            match dataMod with
            | :? ILockedResource as r ->
                x.GetOrCreateLocal(key, fun () ->
                    v.AddRef()
                    let mutable oldData = None
                    { new Resource<'Handle, 'View>(v.Kind) with
                        member x.View (h : 'Handle) =
                            v.View(h)

                        member x.GetInfo(h : 'Handle) =
                            v.GetInfo h

                        member x.Create(token : AdaptiveToken, rt : RenderToken, old : Option<'Handle>) =
                            let newData = dataMod.GetValue token

                            match oldData with
                            | Some d -> releaseLock d
                            | None -> ()

                            oldData <- Some newData
                            acquireLock newData

                            if old.IsNone then
                                dataMod.Acquire()

                            let stats = v.Update(token, rt)
                            v.Handle.GetValue()

                        member x.Destroy(h : 'Handle) =
                            match oldData with
                            | Some d -> releaseLock d; oldData <- None
                            | None -> ()

                            dataMod.Release()
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
                let desc = creator()
                let mutable ownsHandle = false
                let mutable oldData = None

                let resource =
                    { new Resource<'Handle, 'View>(desc.kind) with
                        member x.View (h : 'Handle) =
                            desc.view h

                        member x.GetInfo (h : 'Handle) =
                            desc.info h

                        member x.Create(token : AdaptiveToken, rt : RenderToken, old : Option<'Handle>) =
                            if old.IsNone then
                                dataMod.Acquire()

                            let data = dataMod.GetValue(token, rt)

                            match oldData with
                            | Some d -> releaseLock d
                            | None -> ()

                            acquireLock data
                            oldData <- Some data

                            match old with
                            | Some old ->
                                match data :> obj with
                                | :? 'Handle as handle when handleNonPrimitive ->
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
                                | :? 'Handle as handle when handleNonPrimitive ->
                                    ownsHandle <- false
                                    handle
                                | _ ->
                                    let handle = desc.create data
                                    ownsHandle <- true
                                    handle

                        member x.Destroy(h : 'Handle) =
                            match oldData with
                            | Some d -> releaseLock d
                            | _ -> ()

                            dataMod.Release()
                            if ownsHandle then
                                ownsHandle <- false
                                desc.delete h
                    }

                resource
            )

    member x.GetOrCreate<'Data>(dataMod : aval<'Data>, creator : unit -> ResourceDescription<'Data, 'Handle, 'View>) =
        x.GetOrCreate(dataMod, [], creator)

    member x.Count = store.Count
    member x.Clear() =
        let remaining = store |> Seq.map (fun (KeyValue(_,r)) -> r) |> Seq.toArray
        for r in remaining do
            Log.warn "leaking resource: %A" r
            r.ForceDispose()
        store.Clear()