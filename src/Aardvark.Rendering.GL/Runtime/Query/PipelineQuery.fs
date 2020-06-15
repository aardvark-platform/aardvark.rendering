namespace Aardvark.Rendering.GL

open Aardvark.Base
open OpenTK.Graphics.OpenGL4

[<AutoOpen>]
module private ``Pipeline Query Helpers`` =

    let base2GLTarget =
        LookupTable.lookupTable [
            InputAssemblyVertices,                  QueryTarget.VerticesSubmitted
            InputAssemblyPrimitives,                QueryTarget.PrimitivesSubmitted
            VertexShaderInvocations,                QueryTarget.VertexShaderInvocations
            GeometryShaderInvocations,              QueryTarget.GeometryShaderInvocations
            GeometryShaderPrimitives,               QueryTarget.GeometryShaderPrimitivesEmitted
            ClippingInputPrimitives,                QueryTarget.ClippingInputPrimitives
            ClippingOutputPrimitives,               QueryTarget.ClippingOutputPrimitives
            FragmentShaderInvocations,              QueryTarget.FragmentShaderInvocations
            TesselationControlShaderPatches,        QueryTarget.TessControlShaderPatches
            TesselationEvaluationShaderInvocations, QueryTarget.TessEvaluationShaderInvocations
            ComputeShaderInvocations,               QueryTarget.ComputeShaderInvocations
        ]

    // Returns the index of the statistic in the query result buffer
    let getFlagIndex (f : PipelineStatistics) (flags : Set<PipelineStatistics>) =
        flags |> Seq.sortBy base2GLTarget |> Seq.findIndex ((=) f)

type PipelineQuery(ctx : Context, enabledStatistics : Set<PipelineStatistics>) =
    inherit Query(ctx, enabledStatistics |> Set.map base2GLTarget |> QueryType.Multiple)

    let supported (stat : PipelineStatistics) =
        enabledStatistics |> Set.contains stat

    let validate (statistics : Set<PipelineStatistics>) =
        statistics
        |> Set.filter (supported >> not)
        |> Set.iter (failwithf "Query does not support '%A' statistic")

    let compute (statistics : Set<PipelineStatistics>) (data : uint64[]) =
        statistics |> Seq.map (fun s ->
            let i = enabledStatistics |> getFlagIndex s
            s, data.[i]
        )
        |> Map.ofSeq

    interface IPipelineQuery with
        member x.HasResult() =
            x.TryGetResults(false) |> Option.isSome

        member x.GetResult(statistics : Set<PipelineStatistics>, reset : bool) =
            validate statistics
            x.GetResults(reset) |> compute statistics

        member x.TryGetResult(statistics : Set<PipelineStatistics>, reset : bool) =
            validate statistics
            x.TryGetResults(reset) |> Option.map (compute statistics)

        member x.Statistics =
            enabledStatistics