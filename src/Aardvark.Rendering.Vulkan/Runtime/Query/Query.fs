namespace Aardvark.Rendering.Vulkan

open System
open Aardvark.Base

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
    | PipelineStatistics of flags : VkQueryPipelineStatisticFlags *  valuesPerQuery : int

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
        | PipelineStatistics -> VkQueryType.PipelineStatistics

    /// The pipeline statistics flags (if any).
    member x.Flags =
        match x with
        | PipelineStatistics (flags, _) -> flags
        | _ -> VkQueryPipelineStatisticFlags.None

[<AbstractClass>]
type Query(device : Device, typ : QueryType, queryCount : int) =
    inherit ConcurrentQuery<InternalQuery, uint64[][]>()

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