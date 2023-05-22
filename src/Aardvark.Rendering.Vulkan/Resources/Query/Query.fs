namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Collections.Generic
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Rendering

type IVulkanQuery =
    inherit IQuery

    /// Begins the query.
    abstract member Begin : CommandBuffer -> unit

    /// Ends the query.
    abstract member End : CommandBuffer -> unit


[<RequireQualifiedAccess>]
type QueryType =
    | Occlusion
    | Timestamp
    | PipelineStatistics of flags : VkQueryPipelineStatisticFlags * valuesPerQuery : int

    /// Returns the number of values returned by the query.
    member x.ValuesPerQuery =
        match x with
        | PipelineStatistics (_, n) -> n
        | _ -> 1

    /// The native VkQueryType.
    member x.Native =
        match x with
        | Occlusion -> VkQueryType.Occlusion
        | Timestamp -> VkQueryType.Timestamp
        | PipelineStatistics _ -> VkQueryType.PipelineStatistics

    /// The pipeline statistics flags (if any).
    member x.Flags =
        match x with
        | PipelineStatistics (flags, _) -> flags
        | _ -> VkQueryPipelineStatisticFlags.None


/// Type to store cached pool results.
type PoolHandle(pool : QueryPool, valuesPerQuery : int) =
    let mutable cached = None

    new(device : Device, typ : QueryType, queryCount : int) =
        let pool = device |> QueryPool.create typ.Native typ.Flags queryCount
        new PoolHandle(pool, typ.ValuesPerQuery)

    member x.Native = pool

    member x.Reset() =
        cached <- None

    member x.GetResult() =
        match cached with
        | Some result -> result
        | _ ->
            let result = pool |> QueryPool.getValues valuesPerQuery
            cached <- Some result
            result

    member x.TryGetResult() =
        if cached = None then
            cached <- pool |> QueryPool.tryGetValues valuesPerQuery

        cached

    member x.Dispose() =
        pool |> QueryPool.delete

    interface IDisposable with
        member x.Dispose() = x.Dispose()


/// Inner query that can consist of multiple query pools.
type InternalQuery(device : Device, typ : QueryType, queryCount : int) =
    let mutable refCount = 0

    // All allocated query pools
    let pools = List<PoolHandle>()

    // Pools that are currently active
    let activePools = List<PoolHandle>()

    // Pools that are available to be used
    let availablePools = Queue<PoolHandle>()

    // Currently recording pool
    let mutable recordingPool : Option<PoolHandle> = None

    // Creates a new pool
    let newPool() =
        let h = new PoolHandle(device, typ, queryCount)
        pools.Add h
        h

    // Gets the next pool. If there is none available, creates one.
    let nextPool() =
        if availablePools.IsEmpty() then
            newPool()
        else
            availablePools.Dequeue()

    // Allocate single pool in advance
    do availablePools.Enqueue <| newPool()

    /// Indicates whether the query is currently in use (i.e. reference counter is greater than zero).
    member x.IsUsed =
        refCount > 0

    /// Increments the reference count.
    member x.Acquire() =
        inc &refCount

    /// Decrements the reference count.
    member x.Release() =
        dec &refCount

    /// Indicates whether the query is active.
    member x.IsActive =
        activePools.Count > 0

    /// Begins the query and returns the pool.
    member x.Begin() =
        match recordingPool with
        | None ->
            let p = nextPool()
            recordingPool <- Some p
            p
        | _ ->
            failf "Recording pool detected. End() has to be called first!"

    /// Ends the query and returns the used pool.
    member x.End() =
        match recordingPool with
        | Some p ->
            recordingPool <- None
            activePools.Add p
            p
        | _ ->
            failf "No recording pool. Begin() has to be called first!"

    /// Resets the query.
    member x.Reset() =
        activePools.Clear()
        availablePools.Clear()

        for p in pools do
            p.Reset()
            availablePools.Enqueue p

    /// Blocks to retrieve the query results.
    member x.GetResults() =
        activePools |> Seq.map (fun h -> h.GetResult()) |> Seq.toArray

    /// Retrieves the query results if available.
    member x.TryGetResults() =
        let r = activePools |> Seq.choose (fun h -> h.TryGetResult()) |> Seq.toArray

        if r.Length < activePools.Count then
            None
        else
            Some r

    /// Deletes all pools.
    member x.Dispose() =
        for p in pools do p.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()


[<AbstractClass>]
type Query(device : Device, typ : QueryType, queryCount : int) =

    // List of all queries that have been allocated.
    let queries = List<InternalQuery>()

    // The currently active query
    let mutable activeQuery : InternalQuery option = None

    // The current level of the queue, i.e. of the nested Begin-End pairs
    let mutable currentLevel = 0

    // Returns whether the query is active.
    let isActive() =
        activeQuery
        |> Option.map (fun q -> q.IsActive)
        |> Option.defaultValue false

    /// Resets the given query pool.
    abstract member Reset : CommandBuffer * QueryPool -> unit
    default x.Reset (cmd : CommandBuffer, pool : QueryPool) =
        cmd.Enqueue <| Command.Reset pool

    /// Begins the given query pool.
    abstract member Begin : CommandBuffer * QueryPool -> unit
    default x.Begin (cmd : CommandBuffer, pool : QueryPool) =
        cmd.Enqueue <| Command.BeginQuery(pool, 0, VkQueryControlFlags.None)

    /// Ends the given query pool
    abstract member End : CommandBuffer * QueryPool -> unit
    default x.End (cmd : CommandBuffer, pool : QueryPool) =
        cmd.Enqueue <| Command.EndQuery(pool, 0)

    // Creates or gets a query that is currently unused.
    member private x.GetQuery() =
        let reused = queries |> Seq.tryFind (fun q -> not q.IsUsed)

        match reused with
        | Some q ->
            q.Reset()
            q

        | _ ->
            let q = new InternalQuery (device, typ, queryCount)
            queries.Add q
            q

    /// If there is an active query, increments the ref counter, releases the global lock,
    /// calls the function on the query, and finally decrements the ref counter.
    /// Otherwise returns None.
    member private x.TryUseActiveQuery(reset : bool, f : InternalQuery -> 'T) =
        let query =
            lock x (fun _ ->
                if isActive() then
                    let q = activeQuery.Value
                    q.Acquire()

                    if reset then
                        activeQuery <- None

                    Some q

                else
                    None
            )

        try
            query |> Option.map f
        finally
            lock x (fun _ -> query |> Option.iter (fun q -> q.Release()))

    /// Waits until there is an active query, increments the ref counter, releases the global lock,
    /// calls the function on the query, and finally decrements the ref counter.
    member private x.UseActiveQuery(reset : bool, f : InternalQuery -> 'T) =
        let query =
            lock x (fun _ ->
                while not (isActive()) do
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
        lock x (fun _ -> activeQuery <- None)

    /// Begins the query.
    member x.Begin (cmd : CommandBuffer) =
        let pool = activeQuery.Value.Begin()
        x.Reset(cmd, pool.Native)
        x.Begin(cmd, pool.Native)

    /// Ends the query.
    member x.End (cmd : CommandBuffer) =
        let pool = activeQuery.Value.End()
        x.End(cmd, pool.Native)

    /// Locks the query and resets it.
    member x.Begin() =
        Monitor.Enter x
        inc &currentLevel

        // Just entered the top-level begin-end pair -> reset
        if currentLevel = 1 then
            activeQuery <- Some <| x.GetQuery()

    /// Unlocks the query.
    member x.End() =
        dec &currentLevel

        // We just left the top-level begin-end pair
        // Notify other threads to try query results again.
        if currentLevel = 0 && isActive() then
            Monitor.PulseAll x

        Monitor.Exit x

    /// Trys to get the result from the queries.
    member x.TryGetResults(reset : bool) =
        x.TryUseActiveQuery(reset, fun query ->
            query.TryGetResults()
        ) |> Option.bind id

    /// Blocks until the result of every query is available.
    member x.GetResults(reset : bool) =
        x.UseActiveQuery(reset, fun query ->
            query.GetResults()
        )

    /// Deletes the queries.
    member x.Dispose() =
        lock x (fun _ ->
            for q in queries do q.Dispose()
        )

    interface IVulkanQuery with

        member x.Reset() = x.Reset()

        member x.Begin() = x.Begin()

        member x.End() = x.End()

        member x.Begin (cmd : CommandBuffer) = x.Begin cmd

        member x.End (cmd : CommandBuffer) = x.End cmd

    interface IDisposable with
        member x.Dispose() = x.Dispose()

[<AutoOpen>]
module internal ``IQuery Extensions`` =

    let private tryUnwrap (onlyTimeQueries : bool) (query : IQuery) =
        match query with
        | :? IVulkanQuery as q when (not onlyTimeQueries || q :? ITimeQuery) -> Some q
        | _ -> None

    type RenderToken with
        member x.GetVulkanQueries([<Optional; DefaultParameterValue(false)>] onlyTimeQueries : bool) =
            x.Queries |> List.choose (tryUnwrap onlyTimeQueries)

[<AutoOpen>]
module ``Query Command Extensions`` =

    type Command with

        static member Begin(query : IVulkanQuery) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue cmd =
                    query.Begin cmd
                    []
            }

        static member End(query : IVulkanQuery) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue cmd =
                    query.End cmd
                    []
            }