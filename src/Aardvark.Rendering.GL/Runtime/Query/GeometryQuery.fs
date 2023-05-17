namespace Aardvark.Rendering.GL

open Aardvark.Base
open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4

type internal GeometryQuery(ctx : Context) =
    inherit Query<seq<PipelineStatistics>, Map<PipelineStatistics, uint64>>(ctx, QueryTarget.PrimitivesGenerated)

    override x.Compute(statistics : seq<PipelineStatistics>, data : int64[]) =
        statistics |> Seq.map (fun s ->
            let value = if s = ClippingInputPrimitives then uint64 data.[0] else 0UL
            s, value
        )
        |> Map.ofSeq

    interface IPipelineQuery with
        member x.Statistics = Set.singleton ClippingInputPrimitives