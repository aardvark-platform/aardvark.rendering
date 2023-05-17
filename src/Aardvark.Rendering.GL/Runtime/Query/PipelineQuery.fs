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
    let getFlagIndex (f : PipelineStatistics) (flags : PipelineStatistics[]) =
        flags |> Array.tryFindIndex ((=) f)

type internal PipelineQuery(ctx : Context, enabledStatistics : PipelineStatistics[]) =
    inherit Query<seq<PipelineStatistics>, Map<PipelineStatistics, uint64>>(ctx, enabledStatistics |> Array.map base2GLTarget)

    new (ctx : Context, enabledStatistics : Set<PipelineStatistics>) =
        new PipelineQuery(ctx, Set.toArray enabledStatistics)

    override x.Compute(statistics : seq<PipelineStatistics>, data : int64[]) =
        statistics |> Seq.map (fun s ->
            let value =
                match enabledStatistics |> getFlagIndex s with
                | Some i -> uint64 data.[i]
                | None -> 0UL

            s, value
        )
        |> Map.ofSeq

    interface IPipelineQuery with
        member x.Statistics = Set.ofArray enabledStatistics