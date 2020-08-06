namespace Aardvark.Rendering.GL

open Aardvark.Base
open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4

[<AutoOpen>]
module private ``Occlusion Query Helpers`` =
    let getTarget (precise : bool) =
        if precise then
            QueryTarget.SamplesPassed
        else
            QueryTarget.AnySamplesPassedConservative

type OcclusionQuery(ctx : Context, precise : bool) =
    inherit Query(ctx, getTarget precise, 1)

    interface IOcclusionQuery with
        member x.HasResult() =
            x.TryGetResults(false) |> Option.isSome

        member x.GetResult(_ : unit, reset : bool) =
            x.GetResults(reset) |> Array.head

        member x.TryGetResult(_ : unit, reset : bool) =
            x.TryGetResults(reset) |> Option.map Array.head

        member x.IsPrecise = precise