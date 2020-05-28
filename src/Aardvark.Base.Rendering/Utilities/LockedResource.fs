namespace Aardvark.Base

open System
open FSharp.Data.Adaptive

type ResourceUsage =
    | Access = 1
    | Render = 2

type ResourceLock = ColoredLock<ResourceUsage>

type ILockedResource =
    abstract member Lock : ResourceLock
    abstract member OnLock : usage : Option<ResourceUsage> -> unit
    abstract member OnUnlock : usage : Option<ResourceUsage> -> unit


module LockedResource =

    //possibly broken.
    let inline render (r : ILockedResource) (f : unit -> 'x) =
        r.Lock.Enter(ResourceUsage.Render, r.OnLock)
        try f()
        finally r.Lock.Exit(r.OnUnlock)

    let inline access (r : ILockedResource) (f : unit -> 'x) =
        r.Lock.Enter(ResourceUsage.Access, r.OnLock)
        try f()
        finally r.Lock.Exit(r.OnUnlock)

    let inline update (r : ILockedResource) (f : unit -> 'x) =
        r.Lock.Enter(r.OnLock)
        try f()
        finally r.Lock.Exit(r.OnUnlock)

open FSharp.Data.Traceable
type RenderTaskLock() =
    let lockObj = obj()
    let mutable lockedResources : CountingHashSet<ILockedResource> = CountingHashSet.empty

    member x.Run f = 
        let res = lock lockObj (fun () -> CountingHashSet.toArray lockedResources)
        for l in res do l.Lock.Enter(ResourceUsage.Render, l.OnLock)
        try f()
        finally for l in res do l.Lock.Exit(l.OnUnlock)

        

    [<Obsolete>]
    member x.Update f = failwith ""
    
    member x.Add(r : ILockedResource) =
        lock lockObj (fun () -> lockedResources <- lockedResources.Add r)

    member x.Remove(r : ILockedResource) =
        lock lockObj (fun () -> lockedResources <- lockedResources.Remove r)

