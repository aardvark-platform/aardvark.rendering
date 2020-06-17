namespace Aardvark.Rendering.GL

open Aardvark.Base
open OpenTK.Graphics.OpenGL4

type TimeQuery(ctx : Context) =
    inherit Query(ctx, QueryTarget.Timestamp, 2)

    let compute (data : uint64[]) =
        (data.[1] - data.[0]) |> int64 |> MicroTime.FromNanoseconds

    override x.BeginQuery(query : InternalQuery) =
        GL.QueryCounter(query.Handles.[0], QueryCounterTarget.Timestamp)
        GL.Check "failed to write timestamp"

    override x.EndQuery(query : InternalQuery) =
        GL.QueryCounter(query.Handles.[1], QueryCounterTarget.Timestamp)
        GL.Check "failed to write timestamp"

    interface ITimeQuery with
        member x.HasResult() =
            x.TryGetResults(false) |> Option.isSome

        member x.GetResult(_ : unit, reset : bool) =
            compute <| x.GetResults(reset)

        member x.TryGetResult(_ : unit, reset : bool) =
            x.TryGetResults(reset) |> Option.map compute