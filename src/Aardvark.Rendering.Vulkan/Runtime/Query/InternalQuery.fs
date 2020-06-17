namespace Aardvark.Rendering.Vulkan

open System.Collections.Generic
open Aardvark.Base

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
    inherit RefCountedQuery<uint64[][]>()

    // Query pools
    let pools = List<PoolHandle>()

    // Pools that are available to be used.
    let availablePools = Queue<PoolHandle>()

    // Currently recording pool
    let mutable recordingPool : Option<PoolHandle> = None

    // Cached results from the pools
    let results = List<PoolHandle>()

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

    // Deletes the pools
    let deletePools() =
        pools |> Seq.iter (fun x -> x.Dispose())

    // Begins the next pool.
    let beginPool() =
        match recordingPool with
        | None ->
            let p = nextPool()
            results.Add p
            recordingPool <- Some p
            p
        | _ ->
            failwith "Active pool detected. End() has to be called first!"

    // Ends the current pool.
    let endPool() =
        match recordingPool with
        | Some p ->
            recordingPool <- None
            p
        | _ -> failwith "No active pool. Begin() has to be called first!"

    // Resets the results.
    let reset() =
        // Make all pools available again
        availablePools.Clear()
        pools |> Seq.iter (fun p -> p.Reset(); availablePools.Enqueue p)

        // Reset results
        results.Clear()

    // Gets the results.
    let getResults() =
        results |> Seq.map (fun r -> r.GetResult()) |> Seq.toArray

    // Gets the results if available.
    let tryGetResults() =
        let r = results |> Seq.choose (fun r -> r.TryGetResult()) |> Seq.toArray

        if r.Length < results.Count then
            None
        else
            Some r

    // Allocate single pool in advance
    do availablePools.Enqueue <| newPool()

    /// Begins the query and returns the pool.
    member x.Begin() =
        beginPool()

    /// Ends the query and returns the used pool.
    member x.End() =
        endPool()

    override x.IsActive =
        results.Count > 0

    override x.Reset() =
        reset()

    override x.GetResults() =
        getResults()

    override x.TryGetResults() =
        tryGetResults()

    override x.Dispose() =
        deletePools()