namespace Aardvark.Rendering.GL

open Aardvark.Base
open OpenTK.Graphics.OpenGL4

[<AutoOpen>]
module private ``Query Helpers`` =

    type QueryHandle(handle : int) =
        inherit AbstractQueryHandle<int, uint64>(handle)

        new() =
            let h = GL.GenQuery()
            GL.Check "failed to generate query"
            new QueryHandle(h)

        override x.GetValues(h : int) =
            let mutable value = 0L

            GL.GetQueryObject(h, GetQueryObjectParam.QueryResult, &value)
            GL.Check "could not retrieve query result"

            uint64 value

        override x.TryGetValues(h : int) =
            let mutable value = 0L

            GL.GetQueryObject(h, GetQueryObjectParam.QueryResultAvailable, &value)
            GL.Check "could not retrieve query status"

            if value > 0L then
                GL.GetQueryObject(h, GetQueryObjectParam.QueryResultNoWait, &value)
                GL.Check "could not retrieve query result"

                Some (uint64 value)
            else
                None

        override x.DeleteHandle(h : int) =
            GL.DeleteQuery(h)
            GL.Check "failed to delete query"

type InternalQuery(ctx : Context, count : int) =
    inherit RefCountedQuery<uint64[]>()

    // The handle of the context the query is being used on
    let mutable owner = None

    // The handles of the queries
    let mutable handles : QueryHandle[] = Array.empty

    // Deletes the query handles
    let deleteHandles() =
        handles |> Array.iter (fun x -> x.Dispose())
        handles <- Array.empty

    // Gets or creates query handles
    let createHandles() =
        if handles.Length <> count then
            owner <- ctx.CurrentContextHandle
            handles <- Array.init count (fun _ -> new QueryHandle())

        handles

    /// Gets the handles of the queries.
    member x.Handles =
        createHandles() |> Array.map (fun h -> h.Native)

    // GL queries are always active.
    override x.IsActive = true

    /// Clears the cached results
    override x.Reset() =
        handles |> Array.iter (fun h -> h.Reset())

    /// Gets the results of the queries.
    override x.GetResults() =
        use __ = ctx.RenderingLock owner.Value
        handles |> Array.map (fun r -> r.GetResult())

    /// Tries to get the results of the queries.
    override x.TryGetResults() =
        use __ = ctx.RenderingLock owner.Value
        let r = handles |> Array.choose (fun r -> r.TryGetResult())

        if r.Length < handles.Length then
            None
        else
            Some r

    /// Disposes the query.
    override x.Dispose() =
        deleteHandles()

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
    inherit ConcurrentQuery<InternalQuery, uint64[]>()

    /// Creates a query for a single target
    new(ctx : Context, target : QueryTarget, queryCount : int) =
        new Query(ctx, QueryType.Single (target, queryCount))

    override x.CreateQuery() =
        new InternalQuery (ctx, typ.Count)

    override x.BeginQuery(query : InternalQuery) =
        let handles = query.Handles
        let mutable idx = 0

        for target in typ.Targets do
            GL.BeginQuery(target, handles.[idx])
            GL.Check "could not begin query"

            inc &idx

        assert (idx = typ.Count)

    override x.EndQuery(_ : InternalQuery) =
        for target in typ.Targets do
            GL.EndQuery(target)
            GL.Check "could not end query"

    interface IQuery with

        member x.Reset() = x.Reset()

        member x.Begin() = x.Begin()

        member x.End() = x.End()