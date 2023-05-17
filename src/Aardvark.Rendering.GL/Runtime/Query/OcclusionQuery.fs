namespace Aardvark.Rendering.GL

open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4

[<AutoOpen>]
module private ``Occlusion Query Helpers`` =
    let getTarget (precise : bool) =
        if precise then
            QueryTarget.SamplesPassed
        else
            QueryTarget.AnySamplesPassedConservative

type internal OcclusionQuery(ctx : Context, precise : bool) =
    inherit Query<unit, uint64>(ctx, getTarget precise)

    override x.Compute(_, results) =
        uint64 results.[0]

    interface IOcclusionQuery with
        member x.IsPrecise = precise