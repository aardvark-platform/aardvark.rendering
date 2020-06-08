namespace Aardvark.Base

open FSharp.Data.Adaptive

type ModRenderTask(input : aval<IRenderTask>) =
    inherit AbstractRenderTask()
    let mutable inner : Option<IRenderTask> = None

    let updateInner t (x : AdaptiveToken) =
        let ni = input.GetValue(x, t)

        match inner with
            | Some oi when oi = ni -> ()
            | _ ->
                match inner with
                    | Some oi -> oi.Dispose()
                    | _ -> ()

                match x.Caller with
                | Some caller -> ni.Outputs.Add caller |> ignore
                | None -> ()

        inner <- Some ni
        ni

    override x.Use(f : unit -> 'a) =
        lock x (fun () ->
            lock input (fun () ->
                input.GetValue().Use f
            )
        )

    override x.FramebufferSignature =
        let v = input.GetValue (AdaptiveToken.Top.WithCaller x)
        v.FramebufferSignature

    override x.PerformUpdate(token, t) =
        let ni = updateInner t token
        ni.Update(token, t)

    override x.Perform(token, t, fbo, queries) =
        let ni = updateInner t token
        ni.Run(token, t, fbo, queries)

    override x.Release() =
        input.Outputs.Remove x |> ignore
        match inner with
            | Some i ->
                i.Dispose()
                inner <- None
            | _ -> ()

    override x.Runtime = input.GetValue(AdaptiveToken.Top.WithCaller x).Runtime