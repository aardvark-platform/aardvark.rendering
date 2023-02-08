namespace Aardvark.Rendering.Vulkan

open System
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
    inherit AbstractQueryHandle<QueryPool, uint64[]>(pool)

    new(device : Device, typ : VkQueryType, flags : VkQueryPipelineStatisticFlags, valuesPerQuery : int, queryCount : int) =
        let pool = device |> QueryPool.create typ flags queryCount
        new PoolHandle(pool, valuesPerQuery)

    override x.GetValues(pool : QueryPool) =
        pool |> QueryPool.getValues valuesPerQuery

    override x.TryGetValues(pool : QueryPool) =
        pool |> QueryPool.tryGetValues valuesPerQuery

    override x.DeleteHandle(pool : QueryPool) =
        pool |> QueryPool.delete


/// Inner query that can consist of multiple query pools.
type InternalQuery(device : Device, typ : VkQueryType, flags : VkQueryPipelineStatisticFlags, valuesPerQuery : int, queryCount : int) =
    inherit MultiQuery<PoolHandle, uint64[]>()

    // All allocated query pools
    let pools = List<PoolHandle>()

    // Pools that are available to be used.
    let availablePools = Queue<PoolHandle>()

    // Currently recording pool
    let mutable recordingPool : Option<PoolHandle> = None

    // Creates a new pool
    let newPool() =
        let h = new PoolHandle(device, typ, flags, valuesPerQuery, queryCount)
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

    /// Begins the query and returns the pool.
    member x.Begin() =
        match recordingPool with
        | None ->
            let p = nextPool()
            x.Handles.Add p
            recordingPool <- Some p
            p
        | _ ->
            failwith "Active pool detected. End() has to be called first!"

    /// Ends the query and returns the used pool.
    member x.End() =
        match recordingPool with
        | Some p ->
            recordingPool <- None
            p
        | _ -> failwith "No active pool. Begin() has to be called first!"

    /// Resets the query.
    override x.Reset() =
        // Make all pools available again
        availablePools.Clear()
        pools |> Seq.iter (fun p -> p.Reset(); availablePools.Enqueue p)

        // Reset handles
        base.Reset()

    /// Deletes all pools.
    override x.Dispose() =
        pools |> Seq.iter (fun x -> x.Dispose())


[<AbstractClass>]
type Query(device : Device, typ : QueryType, queryCount : int) =
    inherit ConcurrentQuery<InternalQuery, uint64[]>()

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

    /// Begins the query.
    member x.Begin (cmd : CommandBuffer) =
        let pool = x.ActiveQuery.Value.Begin()
        x.Reset(cmd, pool.Native)
        x.Begin(cmd, pool.Native)

    /// Ends the query.
    member x.End (cmd : CommandBuffer) =
        let pool = x.ActiveQuery.Value.End()
        x.End(cmd, pool.Native)

    override x.CreateQuery() =
        new InternalQuery (device, typ.Native, typ.Flags, typ.ValuesPerQuery, queryCount)

    interface IVulkanQuery with

        member x.Reset() = x.Reset()

        member x.Begin() = x.Begin()

        member x.End() = x.End()

        member x.Begin (cmd : CommandBuffer) = x.Begin cmd

        member x.End (cmd : CommandBuffer) = x.End cmd

    interface IDisposable with
        member x.Dispose() = x.Dispose()

[<AutoOpen>]
module ``IQuery Extensions`` =

    let rec private unwrap (onlyTimeQueries : bool) (query : IQuery) =
        match query with
        | :? IVulkanQuery as q when (not onlyTimeQueries || q :? ITimeQuery) -> [q]
        | :? Queries as q -> List.concat <| q.Map (unwrap onlyTimeQueries)
        | _ -> []

    type IQuery with
        member x.ToVulkanQuery([<Optional; DefaultParameterValue(false)>] onlyTimeQueries : bool) =
            x |> unwrap onlyTimeQueries

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