namespace Aardvark.Rendering.GL

open Aardvark.Base
open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4

type internal TimeQuery(ctx : Context) =
    inherit Query<unit, MicroTime>(ctx, [| QueryTarget.Timestamp; QueryTarget.Timestamp |])

    override x.BeginQueries(queries : QueryObject[]) =
        queries.[0].Timestamp()

    override x.EndQueries(queries : QueryObject[]) =
        queries.[1].Timestamp()

    override x.Compute(_, data : int64[]) =
        (data.[1] - data.[0]) |> MicroTime.ofNanoseconds

    interface ITimeQuery