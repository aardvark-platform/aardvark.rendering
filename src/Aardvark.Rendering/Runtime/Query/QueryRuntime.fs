namespace Aardvark.Base

open System.Runtime.CompilerServices

/// Interface for managing GPU queries.
type IQueryRuntime =

    /// Creates a time query.
    abstract member CreateTimeQuery : unit -> ITimeQuery

    /// Creates an occlusion query.
    /// If precise is set to false, an implementation is allowed to return approximate
    /// query results, which may reduce performance overhead.
    abstract member CreateOcclusionQuery : precise : bool -> IOcclusionQuery

    /// Creates a pipeline statistics query.
    /// The parameter statistics determines what kind of statistics can be queried.
    abstract member CreatePipelineQuery : statistics : Set<PipelineStatistics> -> IPipelineQuery

    /// The types of pipeline statistics that may be queried.
    abstract member SupportedPipelineStatistics : Set<PipelineStatistics>


[<AbstractClass; Sealed; Extension>]
type IQueryRuntimeExtensions private() =

    /// Creates a precise occlusion query.
    [<Extension>]
    static member CreateOcclusionQuery(this : IQueryRuntime) =
        this.CreateOcclusionQuery(true)

    /// Creates a pipeline statistics query, enabling all supported statistics.
    [<Extension>]
    static member CreatePipelineQuery(this : IQueryRuntime) =
        this.CreatePipelineQuery(this.SupportedPipelineStatistics)

    /// Creates a pipeline statistics query, enabling a single statistic.
    [<Extension>]
    static member CreatePipelineQuery(this : IQueryRuntime, statistic : PipelineStatistics) =
        this.CreatePipelineQuery(Set.singleton statistic)

    /// Creates a pipeline statistics query.
    /// The parameter statistics determines what kind of statistics can be queried.
    [<Extension>]
    static member CreatePipelineQuery(this : IQueryRuntime, statistics : PipelineStatistics seq) =
        this.CreatePipelineQuery(Set.ofSeq statistics)