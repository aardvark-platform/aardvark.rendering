namespace Aardvark.Rendering

open System.Runtime.CompilerServices
open System.Runtime.InteropServices

/// Interface for managing GPU queries.
type IQueryRuntime =

    /// Creates a time query.
    abstract member CreateTimeQuery : unit -> ITimeQuery

    /// Creates an occlusion query.
    /// If precise is set to false, an implementation is allowed to return approximate
    /// query results, which may reduce performance overhead.
    abstract member CreateOcclusionQuery : [<Optional; DefaultParameterValue(true)>] precise : bool -> IOcclusionQuery

    /// Creates a pipeline statistics query.
    /// The parameter statistics determines what kind of statistics can be queried.
    abstract member CreatePipelineQuery : statistics : seq<PipelineStatistics> -> IPipelineQuery

    /// The types of pipeline statistics that may be queried.
    abstract member SupportedPipelineStatistics : Set<PipelineStatistics>


[<AbstractClass; Sealed; Extension>]
type IQueryRuntimeExtensions private() =

    /// Creates a pipeline statistics query, enabling all supported statistics.
    [<Extension>]
    static member CreatePipelineQuery(this : IQueryRuntime) =
        this.CreatePipelineQuery(this.SupportedPipelineStatistics)

    /// Creates a pipeline statistics query, enabling a single statistic.
    [<Extension>]
    static member CreatePipelineQuery(this : IQueryRuntime, statistic : PipelineStatistics) =
        this.CreatePipelineQuery(Set.singleton statistic)