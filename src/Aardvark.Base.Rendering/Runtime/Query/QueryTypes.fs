namespace Aardvark.Base

open System
open System.Runtime.CompilerServices

/// Interface for GPU queries.
type IQuery =

    /// Resets the query manually.
    abstract member Reset : unit -> unit

    /// Prepares the query to be used.
    abstract member Begin : unit -> unit

    /// Finishes the query.
    abstract member End : unit -> unit

/// Typed interface for GPU queries.
type IQuery<'a, 'b> =
    inherit IQuery
    inherit IDisposable

    /// Returns if the query has a result.
    abstract member HasResult : unit -> bool

    /// Tries to retrieve the result of the query without blocking.
    /// If reset is set to true, the query is reset if the result was ready.
    abstract member TryGetResult : args : 'a * reset : bool -> 'b option

    /// Retrieves the result of the query.
    /// If the result is not ready, the call blocks until it is.
    /// If reset is set to true, the query is reset.
    abstract member GetResult : args : 'a * reset : bool -> 'b


/// Query to measure GPU time.
type ITimeQuery =
    inherit IQuery<unit, MicroTime>


/// Query to measure the number of samples that passed fragment tests.
type IOcclusionQuery =
    inherit IQuery<unit, uint64>

    /// Indicates whether the results are guaranteed to be precise.
    abstract member IsPrecise : bool


type PipelineStatistics =
    | InputAssemblyVertices
    | InputAssemblyPrimitives
    | VertexShaderInvocations
    | GeometryShaderInvocations
    | GeometryShaderPrimitives
    | ClippingInputPrimitives
    | ClippingOutputPrimitives
    | FragmentShaderInvocations
    | TesselationControlShaderPatches
    | TesselationEvaluationShaderInvocations
    | ComputeShaderInvocations

    with

    static member All =
        Set.ofList [
            InputAssemblyVertices
            InputAssemblyPrimitives
            VertexShaderInvocations
            GeometryShaderInvocations
            GeometryShaderPrimitives
            ClippingInputPrimitives
            ClippingOutputPrimitives
            FragmentShaderInvocations
            TesselationControlShaderPatches
            TesselationEvaluationShaderInvocations
            ComputeShaderInvocations
        ]


/// Interface for queries about GPU pipeline statistics.
type IPipelineQuery =
    inherit IQuery<Set<PipelineStatistics>, Map<PipelineStatistics, uint64>>

    /// The types of statistics supported by this query.
    abstract member Statistics : Set<PipelineStatistics>


[<AbstractClass; Sealed; Extension>]
type IQueryResultsExtensions private() =

    /// Tries to retrieve the result of the query without blocking.
    [<Extension>]
    static member TryGetResult(this : IQuery<'a, 'b>, args : 'a) =
        this.TryGetResult(args, false)

    /// Retrieves the result of the query.
    /// If the result is not ready, the call blocks until it is.
    [<Extension>]
    static member GetResult(this : IQuery<'a, 'b>, args : 'a) =
        this.GetResult(args, false)

    /// Tries to retrieve the result of the query without blocking.
    [<Extension>]
    static member TryGetResult(this : IQuery<unit, 'a>) =
        this.TryGetResult((), false)

    /// Retrieves the result of the query.
    /// If the result is not ready, the call blocks until it is.
    [<Extension>]
    static member GetResult(this : IQuery<unit, 'a>) =
        this.GetResult((), false)

    /// Tries to retrieve the result of the query without blocking.
    /// If reset is set to true, the query is reset if the result was ready.
    [<Extension>]
    static member TryGetResult(this : IQuery<unit, 'a>, reset : bool) =
        this.TryGetResult((), reset)

    /// Retrieves the result of the query.
    /// If the result is not ready, the call blocks until it is.
    /// If reset is set to true, the query is reset.
    [<Extension>]
    static member GetResult(this : IQuery<unit, 'a>, reset : bool) =
        this.GetResult((), reset)

    /// Tries to retrieve the result of the query without blocking.
    /// If reset is set to true, the query is reset if the result was ready.
    [<Extension>]
    static member TryGetResult(this : IPipelineQuery, statistic : PipelineStatistics, reset : bool) =
        this.TryGetResult(Set.singleton statistic, reset) |> Option.map (Map.find statistic)

    /// Retrieves the result of the query.
    /// If the result is not ready, the call blocks until it is.
    /// If reset is set to true, the query is reset.
    [<Extension>]
    static member GetResult(this : IPipelineQuery, statistic : PipelineStatistics, reset : bool) =
        this.GetResult(Set.singleton statistic, reset) |> Map.find statistic

    /// Tries to retrieve the result of the query without blocking.
    [<Extension>]
    static member TryGetResult(this : IPipelineQuery, statistic : PipelineStatistics) =
        this.TryGetResult(Set.singleton statistic, false) |> Option.map (Map.find statistic)

    /// Retrieves the result of the query.
    /// If the result is not ready, the call blocks until it is.
    [<Extension>]
    static member GetResult(this : IPipelineQuery, statistic : PipelineStatistics) =
        this.GetResult(Set.singleton statistic, false) |> Map.find statistic

    /// Tries to retrieve the result of the query without blocking.
    /// If reset is set to true, the query is reset if the result was ready.
    [<Extension>]
    static member TryGetResult(this : IPipelineQuery, reset : bool) =
        this.TryGetResult(this.Statistics, reset)

    /// Retrieves the result of the query.
    /// If the result is not ready, the call blocks until it is.
    /// If reset is set to true, the query is reset.
    [<Extension>]
    static member GetResult(this : IPipelineQuery, reset : bool) =
        this.GetResult(this.Statistics, reset)

    /// Tries to retrieve the result of the query without blocking.
    [<Extension>]
    static member TryGetResult(this : IPipelineQuery) =
        this.TryGetResult(this.Statistics)

    /// Retrieves the result of the query.
    /// If the result is not ready, the call blocks until it is.
    [<Extension>]
    static member GetResult(this : IPipelineQuery) =
        this.GetResult(this.Statistics)