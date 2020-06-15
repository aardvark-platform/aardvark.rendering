namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Collections.Generic
open Aardvark.Base

type IVulkanQuery =
    inherit IQuery

    /// Begins the query.
    abstract member Begin : CommandBuffer -> unit

    /// Ends the query.
    abstract member End : CommandBuffer -> unit

[<AbstractClass>]
type VulkanQuery(device : Device, typ : VkQueryType, flags : VkQueryPipelineStatisticFlags, valuesPerQuery : int, queryCount : int) =

    // Queries are reusable, so for each begin - end pair we need a pool.
    // It would be better to allocate queries in a single pool but we don't know
    // the exact number we need a priori.
    let pools = List()

    // Pools that are available to be used.
    let availablePools = Queue()

    // Pools that are currently being used
    let mutable activePools = []

    // Flag to indicate if query was inactive when Begin() was called.
    let mutable wasInactive = true

    // The current level of the query, i.e. of the nested Begin-End pairs.
    let mutable currentLevel = 0

    // Cached results from the active pools
    let mutable results : Option<uint64[]> list = []

    // Creates a new pool
    let newPool() =
        let p = device |> QueryPool.create typ flags queryCount
        pools.Add p
        p

    // Gest the next pool. If there is none available, creates one.
    let nextPool() =
        if availablePools.IsEmpty() then
            newPool()
        else
            availablePools.Dequeue()

    // The currently active pool.
    let mutable currentPool = Some <| newPool()

    // Begins the next pool.
    let beginPool() =
        match currentPool with
        | None ->
            let p = nextPool()
            results <- None :: results
            activePools <- p :: activePools
            currentPool <- Some p
            p
        | _ ->
            failwith "Active pool detected. VulkanQuery.End() has to be called first!"

    // Ends the current pool.
    let endPool() =
        match currentPool with
        | Some p ->
            currentPool <- None
            p
        | _ -> failwith "No active pool. VulkanQuery.Begin() has to be called first!"

    // Resets the results.
    let reset() =
        // Make all pools available again
        availablePools.Clear()
        pools |> Seq.iter (fun p -> availablePools.Enqueue p)

        // Reset results and pools
        results <- []
        activePools <- []
        currentPool <- None

    let queryActivePools (f : QueryPool -> uint64[] option) =
        List.map2 (fun p r ->
            match r with
            | Some _ -> r
            | None -> f p
        ) activePools results

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
        let pool = beginPool()
        x.Reset(cmd, pool)
        x.Begin(cmd, pool)

    /// Ends the query.
    member x.End (cmd : CommandBuffer) =
        let pool = endPool()
        x.End(cmd, pool)

    /// Reset the results and pools.
    member x.Reset() =
        lock x (fun _ -> reset())

    /// Locks the query and resets results and pools.
    member x.Begin() =
        Monitor.Enter x
        inc &currentLevel

        // Just entered the top-level begin-end pair -> reset
        if currentLevel = 1 then
            wasInactive <- activePools.IsEmpty
            reset()

    /// Unlocks the query
    member x.End() =
        dec &currentLevel

        // If we left the top-level begin-end pair, the query was previously
        // inactive, and now has active pools, we can notify other threads to try
        // query results again.
        if currentLevel = 0 && wasInactive && not activePools.IsEmpty then
            Monitor.PulseAll x

        Monitor.Exit x

    /// Trys to get the result from the active query pools.
    member x.TryGetResults(resetQuery : bool) =
        lock x (fun _ ->
            results <- queryActivePools (QueryPool.tryGetValues valuesPerQuery)

            let notReady =
                results.IsEmpty || results |> List.contains None

            if notReady then
                None
            else
                results |> (fun r ->
                    if resetQuery then reset()
                    r |> List.map Option.get |> Some
                )
        )

    /// Blocks until the result of every active query pool is available.
    member x.GetResults(resetQuery : bool) =
        lock x (fun _ ->

            // Wait until there are active query pools
            while activePools.IsEmpty do
                Monitor.Wait x |> ignore

            results <- queryActivePools(QueryPool.getValues valuesPerQuery >> Some)
            results |> (fun r ->
                if resetQuery then reset()
                r |> List.map Option.get
            )
        )

    /// Deletes the query pool.
    member x.Dispose() =
        lock x (fun _ ->
            pools |> Seq.iter QueryPool.delete
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
module ``IQuery Extensions`` =

    let rec private unwrap (query : IQuery) =
        match query with
        | :? IVulkanQuery as q -> Seq.singleton q
        | :? Queries as q -> Seq.concat <| q.Map unwrap
        | _ -> failwithf "unsupported query: %A" query

    type IQuery with

        member x.ToVulkanQuery() =
            unwrap x

[<AutoOpen>]
module ``Query Command Extensions`` =

    type Command with

        static member Begin(query : IVulkanQuery) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue cmd =
                    query.Begin cmd
                    Disposable.Empty
            }

        static member End(query : IVulkanQuery) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue cmd =
                    query.End cmd
                    Disposable.Empty
            }