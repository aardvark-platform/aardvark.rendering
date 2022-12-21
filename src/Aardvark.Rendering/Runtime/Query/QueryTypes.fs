namespace Aardvark.Rendering

open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base

/// Typed interface for GPU queries.
type IQuery<'Parameter, 'Result> =
    inherit IQuery
    inherit IDisposable

    /// Returns if the query has a result.
    abstract member HasResult : unit -> bool

    /// Tries to retrieve the result of the query without blocking.
    /// If reset is set to true, the query is reset if the result was ready.
    abstract member TryGetResult : parameter : 'Parameter *
                                   [<Optional; DefaultParameterValue(false)>] reset : bool -> 'Result option

    /// Retrieves the result of the query.
    /// If the result is not ready, the call blocks until it is.
    /// If reset is set to true, the query is reset.
    abstract member GetResult : parameter : 'Parameter *
                                [<Optional; DefaultParameterValue(false)>] reset : bool -> 'Result


/// Query to measure GPU time.
type ITimeQuery =
    inherit IQuery<unit, MicroTime>


/// Query to measure the number of samples that passed fragment tests.
type IOcclusionQuery =
    inherit IQuery<unit, uint64>

    /// Indicates whether the results are guaranteed to be precise.
    abstract member IsPrecise : bool

[<Struct>]
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

    static member None : Set<PipelineStatistics> = Set.empty

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
    inherit IQuery<seq<PipelineStatistics>, Map<PipelineStatistics, uint64>>

    /// The types of statistics supported by this query.
    abstract member Statistics : Set<PipelineStatistics>


[<AbstractClass; Sealed; Extension>]
type IQueryResultsExtensions private() =

    /// Tries to retrieve the result of the query without blocking.
    /// If reset is set to true, the query is reset if the result was ready.
    [<Extension>]
    static member TryGetResult(this : IQuery<unit, 'a>, [<Optional; DefaultParameterValue(false)>] reset : bool) =
        this.TryGetResult((), reset)

    /// Retrieves the result of the query.
    /// If the result is not ready, the call blocks until it is.
    /// If reset is set to true, the query is reset.
    [<Extension>]
    static member GetResult(this : IQuery<unit, 'a>, [<Optional; DefaultParameterValue(false)>] reset : bool) =
        this.GetResult((), reset)

    /// Tries to retrieve the result of the query without blocking.
    /// If reset is set to true, the query is reset if the result was ready.
    [<Extension>]
    static member TryGetResult(this : IPipelineQuery, statistic : PipelineStatistics, [<Optional; DefaultParameterValue(false)>] reset : bool) =
        this.TryGetResult(Set.singleton statistic, reset) |> Option.map (Map.find statistic)

    /// Retrieves the result of the query.
    /// If the result is not ready, the call blocks until it is.
    /// If reset is set to true, the query is reset.
    [<Extension>]
    static member GetResult(this : IPipelineQuery, statistic : PipelineStatistics, [<Optional; DefaultParameterValue(false)>] reset : bool) =
        this.GetResult(Set.singleton statistic, reset) |> Map.find statistic

    /// Tries to retrieve the result of the query without blocking.
    /// If reset is set to true, the query is reset if the result was ready.
    [<Extension>]
    static member TryGetResult(this : IPipelineQuery, [<Optional; DefaultParameterValue(false)>] reset : bool) =
        this.TryGetResult(this.Statistics, reset)

    /// Retrieves the result of the query.
    /// If the result is not ready, the call blocks until it is.
    /// If reset is set to true, the query is reset.
    [<Extension>]
    static member GetResult(this : IPipelineQuery, [<Optional; DefaultParameterValue(false)>] reset : bool) =
        this.GetResult(this.Statistics, reset)