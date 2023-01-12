namespace Aardvark.Rendering.GL

open Aardvark.Base
open Aardvark.Rendering
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
        flags |> Seq.sortBy base2GLTarget |> Seq.tryFindIndex ((=) f)

type PipelineQuery(ctx : Context, enabledStatistics : Set<PipelineStatistics>) =
    inherit Query(ctx, enabledStatistics |> Set.map base2GLTarget |> QueryType.Multiple)

    let compute (statistics : seq<PipelineStatistics>) (data : uint64[]) =
        statistics |> Seq.map (fun s ->
            let value =
                match enabledStatistics |> getFlagIndex s with
                | Some i -> data.[i]
                | None -> 0UL

            s, value
        )
        |> Map.ofSeq

    interface IPipelineQuery with
        member x.HasResult() =
            x.TryGetResults(false) |> Option.isSome

        member x.GetResult(statistics : seq<PipelineStatistics>, reset : bool) =
            x.GetResults(reset) |> compute statistics

        member x.TryGetResult(statistics : seq<PipelineStatistics>, reset : bool) =
            x.TryGetResults(reset) |> Option.map (compute statistics)

        member x.Statistics =
            enabledStatistics