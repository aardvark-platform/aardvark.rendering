namespace Aardvark.Rendering

type ResourceUsage =
    | Access = 1
    | Render = 2

type ResourceLock = ColoredLock<ResourceUsage>

/// A resource whose render/access lifetimes are coordinated by a colored lock.
type ILockedResource =
    /// Gets the lock shared by all operations on this resource.
    abstract member Lock : ResourceLock
    /// Called when the first logical owner of a usage starts, or when exclusive
    /// ownership starts for None. Successful calls are paired with OnUnlock;
    /// the first and final shared owners may run on different threads.
    abstract member OnLock : usage : Option<ResourceUsage> -> unit
    /// Called when the final logical owner of a usage ends, or when exclusive
    /// ownership ends for None. Nested mode changes preserve this pairing.
    abstract member OnUnlock : usage : Option<ResourceUsage> -> unit

module LockedResource =

    /// Runs an action while sharing the resource with render operations.
    let inline render (r : ILockedResource) (f : unit -> 'x) =
        r.Lock.Enter(ResourceUsage.Render, r.OnLock)
        try f()
        finally r.Lock.Exit(r.OnUnlock)

    /// Runs an action while sharing the resource with access operations.
    let inline access (r : ILockedResource) (f : unit -> 'x) =
        r.Lock.Enter(ResourceUsage.Access, r.OnLock)
        try f()
        finally r.Lock.Exit(r.OnUnlock)

    /// Runs an action with exclusive resource ownership.
    let inline update (r : ILockedResource) (f : unit -> 'x) =
        r.Lock.Enter(r.OnLock)
        try f()
        finally r.Lock.Exit(r.OnUnlock)
