namespace Aardvark.Rendering.GL

open Aardvark.Base
open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4

type GeometryQuery(ctx : Context) =
    inherit Query(ctx, QueryTarget.PrimitivesGenerated, 1)

    let compute (statistics : seq<PipelineStatistics>) (data : uint64[]) =
        statistics |> Seq.map (fun s ->
            let value = if s = ClippingInputPrimitives then data.[0] else 0UL
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
            Set.singleton ClippingInputPrimitives