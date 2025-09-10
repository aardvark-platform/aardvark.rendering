namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Traceable

type RenderTaskLock() =
    let lockObj = obj()
    let mutable lockedResources : CountingHashSet<ILockedResource> = CountingHashSet.empty

    member x.Run f =
        let res = lock lockObj (fun () -> if lockedResources.IsEmpty then null else CountingHashSet.toArray lockedResources)
        if notNull res then for l in res do l.Lock.Enter(ResourceUsage.Render, l.OnLock)
        try f()
        finally if notNull res then for l in res do l.Lock.Exit(l.OnUnlock)

    member x.Add(r : ILockedResource) =
        lock lockObj (fun () -> lockedResources <- lockedResources.Add r)

    member x.Remove(r : ILockedResource) =
        lock lockObj (fun () -> lockedResources <- lockedResources.Remove r)