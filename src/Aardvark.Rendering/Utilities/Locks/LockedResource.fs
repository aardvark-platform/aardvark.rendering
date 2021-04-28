namespace Aardvark.Rendering

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