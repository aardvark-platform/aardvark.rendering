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

type internal QueryHandle(target : QueryTarget) =
    let mutable status = QueryStatus.Inactive

    let handle = GL.GenQuery()
    do GL.Check "failed to generate query"

    member x.IsActive =
        status <> QueryStatus.Inactive

    member x.Reset() =
        status <- QueryStatus.Inactive

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

    member x.GetResult(reset : bool) =
        let result =
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

        if reset then
            status <- QueryStatus.Inactive

        result

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

[<AbstractClass>]
type internal Query<'Parameter, 'Result>(context : Context, targets : QueryTarget[]) =
    let mutable owner = ValueNone
    let mutable handles : QueryHandle[] = null

    // The current level of the queue, i.e. of the nested Begin-End pairs.
    // Only the most outer calls are relevant here.
    let mutable currentLevel = 0

    let isActive() =
        if handles = null then false
        else handles |> Array.forall (fun h -> h.IsActive)

    new (context : Context, target : QueryTarget) =
        new Query<_, _>(context, [| target |])

    abstract member Compute : 'Parameter * int64[] -> 'Result

    abstract member BeginQuery : QueryHandle[] -> unit
    default x.BeginQuery(handles) =
        for h in handles do h.Begin()

    abstract member EndQuery : QueryHandle[] -> unit
    default x.EndQuery(handles) =
        for h in handles do h.End()

    // Query objects can only be used on the context that created them.
    // Makes the context that owns the handles (if there are any) current.
    // Leaves the lock of the query temporarily to avoid deadlocks.
    member private x.MakeOwnerCurrent() : IDisposable =
        assert (Monitor.IsEntered x)

        match owner with
        | ValueSome ctx when owner <> ContextHandle.Current ->
            Monitor.Exit x
            try
                context.RenderingLock ctx
            finally
                Monitor.Enter x

        | _ ->
            Disposable.empty

    member x.HasResult() =
        lock x (fun _ ->
            if handles = null then
                false
            else
                use __ = x.MakeOwnerCurrent()
                handles |> Array.forall (fun h -> h.HasResult())
        )

    member x.TryGetResult(parameter : 'Parameter, [<Optional; DefaultParameterValue(false)>] reset : bool) =
        lock x (fun _ ->
            if handles = null then
                None
            else
                use __ = x.MakeOwnerCurrent()

                if handles |> Array.forall (fun h -> h.HasResult()) then
                    let results = handles |> Array.map (fun h -> h.GetResult(reset))
                    Some <| x.Compute(parameter, results)
                else
                    None
        )

    member x.GetResult(parameter : 'Parameter, [<Optional; DefaultParameterValue(false)>] reset : bool) =
        lock x (fun _ ->
            // Query is not active yet (i.e. Begin / End has not been called)
            // Block until it is actually safe to retrieve the results from the query objects.
            if not <| isActive() then
                Monitor.Wait x |> ignore

            use __ = x.MakeOwnerCurrent()
            let results = handles |> Array.map (fun h -> h.GetResult(reset))
            x.Compute(parameter, results)
        )

    member x.Reset() =
        lock x (fun _ ->
            if handles <> null then
                for h in handles do h.Reset()
        )

    member x.Begin() =
        Monitor.Enter x
        assert (currentLevel >= 0)
        inc &currentLevel

        if currentLevel = 1 then
            if handles = null then
                owner <- ContextHandle.Current
                handles <- targets |> Array.map (fun t -> new QueryHandle(t))

            use __ = x.MakeOwnerCurrent()
            x.BeginQuery handles

    member x.End() =
        assert (Monitor.IsEntered x)
        dec &currentLevel

        if currentLevel = 0 then
            use __ = x.MakeOwnerCurrent()
            x.EndQuery handles

            // Notify potentially waiting threads that the query is pending now.
            Monitor.PulseAll x

        assert (currentLevel >= 0)
        Monitor.Exit x

    member x.Dispose() =
        lock x (fun _ ->
            if handles <> null then
                use __ = x.MakeOwnerCurrent()
                for h in handles do h.Dispose()

            handles <- null
        )

    interface IQuery with
        member x.Reset() = x.Reset()
        member x.Begin() = x.Begin()
        member x.End() = x.End()

    interface IQuery<'Parameter, 'Result> with
        member x.HasResult() = x.HasResult()
        member x.TryGetResult(parameter, reset) = x.TryGetResult(parameter, reset)
        member x.GetResult(parameter, reset) = x.GetResult(parameter, reset)
        member x.Dispose() = x.Dispose()