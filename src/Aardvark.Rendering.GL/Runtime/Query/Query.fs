namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4

[<RequireQualifiedAccess>]
type internal QueryStatus =
    | Inactive
    | Pending
    | Available
    | Completed of result: int64

type internal QueryObject(target : QueryTarget) =
    let mutable status = QueryStatus.Inactive

    let handle = GL.GenQuery()
    do GL.Check "failed to generate query"

    member x.HasResult() =
        match status with
        | QueryStatus.Inactive ->
            false

        | QueryStatus.Available | QueryStatus.Completed _ ->
            true

        | QueryStatus.Pending ->
            let mutable available = 0L
            GL.GetQueryObject(handle, GetQueryObjectParam.QueryResultAvailable, &available)
            GL.Check "could not retrieve query status"

            if available > 0L then
                status <- QueryStatus.Available
                true
            else
                false

    member x.GetResult() =
        match status with
        | QueryStatus.Completed result ->
            result

        | QueryStatus.Pending | QueryStatus.Available ->
            let mutable result = 0L
            GL.GetQueryObject(handle, GetQueryObjectParam.QueryResult, &result)
            GL.Check "could not retrieve query result"
            status <- QueryStatus.Completed result
            result

        | QueryStatus.Inactive ->
            raise <| InvalidOperationException($"Cannot get result of an inactive query (target = {target}).")

    member x.Timestamp() =
        assert (target = QueryTarget.Timestamp)
        status <- QueryStatus.Pending
        GL.QueryCounter(handle, QueryCounterTarget.Timestamp)
        GL.Check "failed to write timestamp"

    member x.Begin() =
        status <- QueryStatus.Inactive
        GL.BeginQuery(target, handle)
        GL.Check "could not begin query"

    member x.End() =
        status <- QueryStatus.Pending
        GL.EndQuery(target)
        GL.Check "could not end query"

    member x.Dispose() =
        GL.DeleteQuery handle
        GL.Check "failed to delete query"

    interface IDisposable with
        member x.Dispose() = x.Dispose()

type internal QueryHandle(context : Context, targets : QueryTarget[]) =
    let owner = ContextHandle.Current.Value
    let mutable queries = targets |> Array.map (fun t -> new QueryObject(t))
    let mutable refCount = 1

    member x.Owner = owner

    member x.Queries = queries

    member x.AddReference() =
        Interlocked.Increment(&refCount) |> ignore

    member x.Dispose() =
        if Interlocked.Decrement(&refCount) = 0 then
            use __ = context.RenderingLock owner
            for q in queries do q.Dispose()
            queries <- null

    interface IDisposable with
        member x.Dispose() = x.Dispose()

[<AbstractClass>]
type internal Query<'Parameter, 'Result>(context : Context, targets : QueryTarget[]) =

    // The most recent handle of the query.
    // A handle is only valid on the context it was created on.
    let mutable handle : ValueOption<QueryHandle> = ValueNone

    // Lock used to make sure a context is current during the Begin-End calls.
    let mutable resourceLock = new ResourceLockDisposable()

    // The current level of the queue, i.e. of the nested Begin-End pairs.
    // Only the most outer calls are relevant here.
    let mutable currentLevel = 0

    let createHandle() =
        let h = new QueryHandle(context, targets)
        handle <- ValueSome h
        h

    new (context : Context, target : QueryTarget) =
        new Query<_, _>(context, [| target |])

    abstract member Compute : 'Parameter * int64[] -> 'Result

    abstract member BeginQueries : QueryObject[] -> unit
    default x.BeginQueries(queries) =
        for q in queries do q.Begin()

    abstract member EndQueries : QueryObject[] -> unit
    default x.EndQueries(queries) =
        for q in queries do q.End()

    // Tries to get the currently active handle and performs an action on it.
    // The handle is retrieved while holding the lock on the query.
    // To avoid deadlocks the lock is released before performing the action.
    // The lock of the context ensures mutual exclusion on the handle itself.
    member private x.TryPerform(fallback : 'T, reset : bool, action : QueryHandle -> 'T) =
        let handle =
            lock x (fun _ ->
                match handle with
                | ValueSome h -> h.AddReference()
                | _ -> ()

                let res = handle
                if reset then handle <- ValueNone
                res
            )

        match handle with
        | ValueSome h ->
            use __ = context.RenderingLock h.Owner

            try
                action h
            finally
                h.Dispose() // Remove added reference
                if reset then h.Dispose()
        | _ ->
            fallback

    member x.HasResult() =
        x.TryPerform(false, false, fun h ->
            h.Queries |> Array.forall (fun h -> h.HasResult())
        )

    member x.TryGetResult(parameter : 'Parameter, [<Optional; DefaultParameterValue(false)>] reset : bool) =
        x.TryPerform(None, reset, fun h ->
            if h.Queries |> Array.forall (fun h -> h.HasResult()) then
                let results = h.Queries |> Array.map (fun q -> q.GetResult())
                Some <| x.Compute(parameter, results)
            else
                None
        )

    member x.GetResult(parameter : 'Parameter, [<Optional; DefaultParameterValue(false)>] reset : bool) =
        let handle =
            lock x (fun _ ->
                // Wait until query is active (i.e. Begin / End is called)
                while handle = ValueNone do
                    Monitor.Wait x |> ignore

                let h = handle.Value
                if reset then handle <- ValueNone
                h.AddReference()
                h
            )

        use __ = context.RenderingLock handle.Owner

        try
            let results = handle.Queries |> Array.map (fun q -> q.GetResult())
            x.Compute(parameter, results)
        finally
            handle.Dispose()
            if reset then handle.Dispose()

    member x.Reset() =
        let handle =
            lock x (fun _ ->
                let h = handle
                handle <- ValueNone
                h
            )

        match handle with
        | ValueSome h -> h.Dispose()
        | _ -> ()

    member x.Begin() =
        Monitor.Enter x
        assert (currentLevel >= 0)
        inc &currentLevel

        if currentLevel = 1 then

            // Make sure there is a context current
            resourceLock <- context.ResourceLock

            // If there is already a handle and it is owned by the current context, we can reuse it.
            // Otherwise create a new one.
            let handle =
                match handle with
                | ValueSome h when h.Owner = ContextHandle.Current.Value ->
                    h

                | ValueSome h ->
                    h.Dispose()
                    createHandle()

                | _ ->
                    createHandle()

            x.BeginQueries handle.Queries

    member x.End() =
        assert (Monitor.IsEntered x)
        dec &currentLevel

        if currentLevel = 0 then
            x.EndQueries handle.Value.Queries

            // Notify potentially waiting threads that the query is pending now.
            Monitor.PulseAll x

            // Release context if needed
            resourceLock.Dispose()

        assert (currentLevel >= 0)
        Monitor.Exit x

    member x.Dispose() =
        x.Reset()

    interface IQuery with
        member x.Reset() = x.Reset()
        member x.Begin() = x.Begin()
        member x.End() = x.End()

    interface IQuery<'Parameter, 'Result> with
        member x.HasResult() = x.HasResult()
        member x.TryGetResult(parameter, reset) = x.TryGetResult(parameter, reset)
        member x.GetResult(parameter, reset) = x.GetResult(parameter, reset)
        member x.Dispose() = x.Dispose()