namespace Aardvark.Rendering.GL

open Aardvark.Base
open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4

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
            GL.GetQueryObject(h, GetQueryObjectParam.QueryResult, &value)
            GL.Check "could not retrieve query result"

            Some (uint64 value)
        else
            None

    override x.DeleteHandle(h : int) =
        GL.DeleteQuery(h)
        GL.Check "failed to delete query"


type InternalQuery(ctx : Context, count : int) =
    inherit MultiQuery<QueryHandle, uint64>()

    // The handle of the context the query is being used on
    let mutable owner = ValueNone

    /// Gets the GL handles of the queries.
    member x.NativeHandles =
        if not x.IsActive then
            owner <- ctx.CurrentContextHandle
            x.Handles.AddRange <| Seq.init count (fun _ -> new QueryHandle())

        x.Handles |> Seq.map (fun x -> x.Native) |> Seq.toArray

    /// Gets the results of the queries.
    override x.GetResults() =
        use __ = ctx.RenderingLock owner.Value
        base.GetResults()

    /// Tries to get the results of the queries.
    override x.TryGetResults() =
        use __ = ctx.RenderingLock owner.Value
        base.TryGetResults()

    override x.Dispose() =
        use __ = ctx.RenderingLock owner.Value
        base.Dispose()


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
    inherit ConcurrentQuery<InternalQuery, uint64>()

    /// Creates a query for a single target
    new(ctx : Context, target : QueryTarget, queryCount : int) =
        new Query(ctx, QueryType.Single (target, queryCount))

    override x.CreateQuery() =
        new InternalQuery (ctx, typ.Count)

    override x.BeginQuery(query : InternalQuery) =
        let handles = query.NativeHandles
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