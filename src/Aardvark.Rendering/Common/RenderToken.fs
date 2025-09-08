namespace Aardvark.Rendering

open System
open Aardvark.Base

/// Token for gathering and querying statistics about the rendering process.
/// It is mutable for construction/mutation in C# code
[<CLIMutable>]
type RenderToken =
    {
        /// User provided GPU queries.
        Queries    : IQuery list

        /// Optional runtime statistics, can be null.
        Statistics : FrameStatistics
    }

    /// Empty token without frame statistics.
    static member Empty = RenderTokenEmpty.Empty

    /// Empty token with frame statistics.
    static member inline Zero =
        {
            Queries    = []
            Statistics = FrameStatistics()
        }

    /// Returns whether the token has statistics, i.e. Statistics is not null.
    member inline this.HasStatistics = not <| obj.ReferenceEquals(this.Statistics, null)

    /// Begins the queries of the token and returns an IDisposable that
    /// ends the queries when disposed.
    member inline this.Use() : QueryUseDisposable =
        for q in this.Queries do q.Begin()
        new QueryUseDisposable(this.Queries)

    member inline private this.GetStatistic(fallback: 'T, [<InlineIfLambda>] mapping: FrameStatistics -> 'T) =
        if this.HasStatistics then mapping this.Statistics
        else fallback

    member inline private this.GetStatistic([<InlineIfLambda>] mapping: FrameStatistics -> 'T) =
        this.GetStatistic(zero, mapping)

    member inline this.InPlaceUpdates         = this.GetStatistic(Dict.empty, _.InPlaceUpdates)
    member inline this.TotalInPlaceUpdates    = this.GetStatistic(_.TotalInplaceUpdates)
    member inline this.ReplacedResources      = this.GetStatistic(Dict.empty, _.ReplacedResources)
    member inline this.TotalReplacedResources = this.GetStatistic(_.TotalReplacedResources)
    member inline this.CreatedResources       = this.GetStatistic(Dict.empty, _.CreatedResources)
    member inline this.TotalCreatedResources  = this.GetStatistic(_.TotalCreatedResources)
    member inline this.UpdateCounts           = this.GetStatistic(Dict.empty, _.UpdateCounts)

    member inline this.RenderPasses           = this.GetStatistic(_.RenderPasses)
    member inline this.TotalInstructions      = this.GetStatistic(_.TotalInstructions)
    member inline this.ActiveInstructions     = this.GetStatistic(_.ActiveInstructions)
    member inline this.DrawCallCount          = this.GetStatistic(_.DrawCallCount)
    member inline this.EffectiveDrawCallCount = this.GetStatistic(_.EffectiveDrawCallCount)
    member inline this.SortingTime            = this.GetStatistic(_.SortingTime)
    member inline this.DrawUpdateTime         = this.GetStatistic(_.DrawUpdateTime)

    member inline this.AddedRenderObjects     = this.GetStatistic(_.AddedRenderObjects)
    member inline this.RemovedRenderObjects   = this.GetStatistic(_.RemovedRenderObjects)

    member inline this.InPlaceResourceUpdate(kind: ResourceKind) =
        if this.HasStatistics then this.Statistics.InPlaceResourceUpdate kind

    member inline this.ReplacedResource(kind: ResourceKind) =
        if this.HasStatistics then this.Statistics.ReplacedResource(kind)

    member inline this.CreatedResource(kind: ResourceKind) =
        if this.HasStatistics then this.Statistics.CreatedResource(kind)

    member inline this.AddInstructions(total: int, active: int) =
        if this.HasStatistics then this.Statistics.AddInstructions(total, active)

    member inline this.AddDrawCalls(count: int, effective: int) =
        if this.HasStatistics then this.Statistics.AddDrawCalls(count, effective)

    member inline this.AddSubTask(sorting: MicroTime, update: MicroTime) =
        if this.HasStatistics then this.Statistics.AddSubTask(sorting, update)

    member inline this.RenderObjectDeltas(added: int, removed: int) =
        if this.HasStatistics then this.Statistics.RenderObjectDeltas(added, removed)

and [<Sealed; AbstractClass>] private RenderTokenEmpty =
    static let empty = { Queries = []; Statistics = null }
    static member Empty = empty

and [<Struct>] QueryUseDisposable (queries: IQuery list) =
    member _.Queries = queries
    member _.Dispose() = for q in queries do q.End()

    interface IDisposable with
        member this.Dispose() = this.Dispose()