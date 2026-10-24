namespace Aardvark.Rendering

open System
open System.Runtime.ExceptionServices
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

    /// <summary>
    /// Captures and begins the queries in list order, returning a scope that ends them in the same order.
    /// If Begin fails, ends only successfully begun entries. A lone failure is rethrown unchanged;
    /// simultaneous failures are aggregated without flattening, Begin first and cleanup failures in list order.
    /// Partial effects inside a failing Begin remain the query's responsibility.
    /// </summary>
    member inline this.Use() : QueryUseDisposable =
        let queries = this.Queries
        let mutable begun = 0
        try
            for q in queries do
                q.Begin()
                begun <- begun + 1
            new QueryUseDisposable(queries)
        with beginError ->
            let mutable remaining = queries
            let mutable errors = Unchecked.defaultof<ResizeArray<exn>>
            for _ in 1 .. begun do
                try remaining.Head.End()
                with endError ->
                    if isNull errors then
                        errors <- ResizeArray<exn>()
                        errors.Add beginError
                    errors.Add endError
                remaining <- remaining.Tail
            if isNull errors then reraise()
            else raise <| AggregateException(errors)

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
    /// <summary>
    /// Attempts End once per list entry in list order, including duplicate entries and repeated disposals.
    /// A lone failure retains its identity and stack; multiple failures are aggregated in order without flattening.
    /// </summary>
    member _.Dispose() =
        let mutable firstError = Unchecked.defaultof<ExceptionDispatchInfo>
        let mutable errors = Unchecked.defaultof<ResizeArray<exn>>
        for q in queries do
            try q.End()
            with error ->
                if isNull firstError then
                    firstError <- ExceptionDispatchInfo.Capture error
                else
                    if isNull errors then
                        errors <- ResizeArray<exn>()
                        errors.Add firstError.SourceException
                    errors.Add error
        if not (isNull errors) then raise <| AggregateException(errors)
        elif not (isNull firstError) then firstError.Throw()

    interface IDisposable with
        member this.Dispose() = this.Dispose()