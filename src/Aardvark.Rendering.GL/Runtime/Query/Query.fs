namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Collections.Generic
open Aardvark.Base
open OpenTK.Graphics.OpenGL4

[<AutoOpen>]
module private ``Query Helpers`` =

    [<AbstractClass>]
    type RefCounted() =
        let mutable refCount = 0

        /// Called when the reference count is decremented to zero.
        abstract member Destroy : unit -> unit

        /// Indicates whether the object is currently in use (i.e. reference counter is greater than zero).
        member x.InUse =
            refCount > 0

        /// Increments the reference count.
        member x.Acquire() =
            inc &refCount

        /// Decrements the reference count.
        member x.Release() =
            dec &refCount
            if refCount = 0 then
                x.Destroy()

        /// Sets the reference count to zero.
        member x.ReleaseAll() =
            if refCount > 0 then
                x.Destroy()

            refCount <- 0

    type GLQuery(ctx : Context, count : int, onDestroy : GLQuery -> unit) =
        inherit RefCounted()

        // The handle of the context the query is being used on
        let mutable owner = None

        // The handles of the queries
        let mutable handles = None

        // The cached results
        let results : uint64 option[] = None |> Array.create count

        // Deletes the query handles
        let deleteHandles() =
            handles |> Option.iter (fun (arr : int[]) ->
                GL.DeleteQueries(count, arr)
                GL.Check "failed to delete query"
            )

            handles <- None

        // Gets or creates query handles
        let createHandles() =
            handles |> Option.defaultWith (fun _ ->
                owner <- ctx.CurrentContextHandle

                let arr = Array.zeroCreate<int> count
                GL.GenQueries(count, arr)
                GL.Check "failed to generate query"

                handles <- Some arr
                arr
            )

        // Blocks until the query result can be retrieved
        let getResult (query : int) =
            let mutable value = 0L

            GL.GetQueryObject(query, GetQueryObjectParam.QueryResult, &value)
            GL.Check "could not retrieve query result"

            uint64 value

        // Tries to retrieve the result of the given query
        let tryGetResult (query : int) =
            let mutable value = 0L

            GL.GetQueryObject(query, GetQueryObjectParam.QueryResultAvailable, &value)
            GL.Check "could not retrieve query status"

            if value > 0L then
                GL.GetQueryObject(query, GetQueryObjectParam.QueryResultNoWait, &value)
                GL.Check "could not retrieve query result"

                Some (uint64 value)
            else
                None

        /// Gets the handles of the queries.
        member x.Handles =
            createHandles()

        /// Clears the cached results
        member x.Reset() =
            None |> Array.fill results 0 count

        /// Gets the results of the queries.
        member x.GetResults() =
            use t = ctx.RenderingLock owner.Value

            results |> Array.mapi (fun i ->
                Option.defaultWith (fun _ ->
                    let r = getResult handles.Value.[i]
                    results.[i] <- Some r
                    r
                )
            )

        /// Tries to get the results of the queries.
        member x.TryGetResults() =
            use t = ctx.RenderingLock owner.Value

            let r =
                results |> Array.mapi (fun i -> function
                    | Some _ as r -> r
                    | None ->
                        let r = tryGetResult handles.Value.[i]
                        results.[i] <- r
                        r
                )

            if r |> Array.forany Option.isNone then
                None
            else
                r |> Array.map Option.get |> Some

        /// Disposes the query.
        member x.Dispose =
            deleteHandles

        override x.Destroy() =
            onDestroy x

        interface IDisposable with
            member x.Dispose() = x.Dispose()

[<RequireQualifiedAccess>]
type QueryType =
    | Single of target : QueryTarget * count : int
    | Multiple of targets : Set<QueryTarget>

    with

    /// Targets of this query type.
    member x.Targets =
        match x with
        | Single (t, _) -> Set.singleton t
        | Multiple t -> t

    /// Total number of required query handles.
    member x.Count =
        match x with
        | Single (_, n) -> n
        | Multiple t -> t.Count

[<AbstractClass>]
type Query(ctx : Context, typ : QueryType) =

    // List of all queries that have been allocated.
    let queries = List<GLQuery>()

    // Queries that are not currently used.
    let availableQueries = Queue<GLQuery>()

    // Flag to indicate if query was inactive when Begin() was called.
    let mutable wasInactive = true

    // The currently active query
    let mutable activeQuery : GLQuery option = None

    // The current level of the queue, i.e. of the nested Begin-End pairs
    let mutable currentLevel = 0

    // Resets the active query. If it is not in use, it is returned to
    // the queue of available queries so it can be reused.
    let resetActive() =
        match activeQuery with
        | Some q when not q.InUse ->
            q.Reset()
            availableQueries.Enqueue q
        | _ -> ()

        activeQuery <- None

    // Gets or creats a query that is returned to the queue
    // once all references are released.
    let getQuery() =
        let returnToQueue (q : GLQuery) =
            q.Reset()
            availableQueries.Enqueue q

        if availableQueries.IsEmpty() then
            let q = new GLQuery (ctx, typ.Count, returnToQueue)
            queries.Add q
            q
        else
            availableQueries.Dequeue()

    /// Creates a query for a single target
    new(ctx : Context, target : QueryTarget, queryCount : int) =
        new Query(ctx, QueryType.Single (target, queryCount))

    abstract member BeginQuery : int[] -> unit
    default x.BeginQuery(queries : int[]) =
        let mutable idx = 0

        for target in typ.Targets do
            GL.BeginQuery(target, queries.[idx])
            GL.Check "could not begin query"

            inc &idx

        assert (idx = typ.Count)

    abstract member EndQuery : int[] -> unit
    default x.EndQuery(_ : int[]) =
        for target in typ.Targets do
            GL.EndQuery(target)
            GL.Check "could not end query"

    /// Waits until there is an active query, increments the ref counter, releases the global lock,
    /// calls the function on the query, and finally decrements the ref counter.
    member private x.UseActiveQuery(reset : bool, f : GLQuery -> 'a) =
        let query =
            lock x (fun _ ->
                while activeQuery.IsNone do
                    Monitor.Wait x |> ignore

                let q = activeQuery.Value
                q.Acquire()

                if reset then
                    activeQuery <- None

                q
            )

        try
            f query
        finally
            lock x query.Release

    /// Resets the query.
    member x.Reset() =
        lock x resetActive

    /// Begins the query.
    member x.Begin() =
        Monitor.Enter x
        inc &currentLevel

        if currentLevel = 1 then
            wasInactive <- activeQuery.IsNone
            resetActive()
            activeQuery <- Some <| getQuery()
            x.BeginQuery activeQuery.Value.Handles

    /// Ends the query.
    member x.End() =
        try
            if currentLevel = 1 then
                x.EndQuery activeQuery.Value.Handles

                if wasInactive then
                    Monitor.PulseAll x

            dec &currentLevel
        finally
            Monitor.Exit x

    /// Trys to get the result from the queries.
    member x.TryGetResults(reset : bool) =
        x.UseActiveQuery(reset, fun query ->
            query.TryGetResults()
        )

    /// Blocks until the result of every query is available.
    member x.GetResults(reset : bool) =
        x.UseActiveQuery(reset, fun query ->
            query.GetResults()
        )

    /// Deletes the query handles.
    member x.Dispose() =
        lock x (fun _ ->
            queries |> Seq.iter (fun x -> x.ReleaseAll())
        )

    interface IQuery with

        member x.Reset() = x.Reset()

        member x.Begin() = x.Begin()

        member x.End() = x.End()

    interface IDisposable with

        member x.Dispose() = x.Dispose()