namespace Aardvark.Rendering

open System
open System.Runtime.ExceptionServices
open Aardvark.Base
open FSharp.Data.Traceable

type RenderTaskLock() =
    let lockObj = obj()
    let mutable lockedResources : CountingHashSet<ILockedResource> = CountingHashSet.empty

    /// Runs the action with a snapshot of the counted resources locked for rendering.
    /// Every successfully acquired resource is released in snapshot order, even after failure.
    /// A single acquisition, action or release failure retains its original identity and stack.
    /// Multiple failures are aggregated with acquisition/action failure first, followed by
    /// cleanup failures in release order. Failed or unattempted acquisitions are not exited.
    member x.Run f =
        let res = lock lockObj (fun () -> if lockedResources.IsEmpty then null else CountingHashSet.toArray lockedResources)
        if isNull res then f()
        else
            let mutable acquired = 0
            let mutable result = Unchecked.defaultof<_>
            let mutable failure : ExceptionDispatchInfo = null
            let mutable failures : ResizeArray<exn> = null
            try
                try
                    while acquired < res.Length do
                        let resource = res.[acquired]
                        resource.Lock.Enter(ResourceUsage.Render, resource.OnLock)
                        acquired <- acquired + 1
                    result <- f()
                with error ->
                    failure <- ExceptionDispatchInfo.Capture error
            finally
                for index = 0 to acquired - 1 do
                    let resource = res.[index]
                    try resource.Lock.Exit(resource.OnUnlock)
                    with error ->
                        if isNull failure then
                            failure <- ExceptionDispatchInfo.Capture error
                        else
                            if isNull failures then
                                failures <- ResizeArray<exn>()
                                failures.Add failure.SourceException
                            failures.Add error
            if notNull failures then raise <| AggregateException(failures)
            elif notNull failure then failure.Throw()
            result

    member x.Add(r : ILockedResource) =
        lock lockObj (fun () -> lockedResources <- lockedResources.Add r)

    member x.Remove(r : ILockedResource) =
        lock lockObj (fun () -> lockedResources <- lockedResources.Remove r)