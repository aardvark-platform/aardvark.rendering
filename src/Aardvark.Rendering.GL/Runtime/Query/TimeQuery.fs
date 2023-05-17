namespace Aardvark.Rendering.GL

open Aardvark.Base
open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4

type internal TimeQuery(ctx : Context) =
    inherit Query<unit, MicroTime>(ctx, [| QueryTarget.Timestamp; QueryTarget.Timestamp |])

    override x.BeginQuery(handles : QueryHandle[]) =
        handles.[0].Timestamp()

    override x.EndQuery(handles : QueryHandle[]) =
        handles.[1].Timestamp()

    override x.Compute(_, data : int64[]) =
        (data.[1] - data.[0]) |> MicroTime.ofNanoseconds

    interface ITimeQuery