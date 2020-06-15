namespace Aardvark.Rendering.GL

open Aardvark.Base
open OpenTK.Graphics.OpenGL4

type GeometryQuery(ctx : Context) =
    inherit Query(ctx, QueryTarget.PrimitivesGenerated, 1)

    let validate (statistics : Set<PipelineStatistics>) =
        statistics
        |> Set.filter ((<>) ClippingInputPrimitives)
        |> Set.iter (failwithf "Query does not support '%A' statistic")

    let compute (data : uint64[]) =
        let value = data |> Array.head
        [ ClippingInputPrimitives, value ]
        |> Map.ofList

    interface IPipelineQuery with
        member x.HasResult() =
            x.TryGetResults(false) |> Option.isSome

        member x.GetResult(statistics : Set<PipelineStatistics>, reset : bool) =
            validate statistics
            x.GetResults(reset) |> compute

        member x.TryGetResult(statistics : Set<PipelineStatistics>, reset : bool) =
            validate statistics
            x.TryGetResults(reset) |> Option.map compute

        member x.Statistics =
            Set.singleton ClippingInputPrimitives