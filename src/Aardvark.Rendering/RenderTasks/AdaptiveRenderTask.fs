namespace Aardvark.Rendering

open FSharp.Data.Adaptive

type AdaptiveRenderTask(input : aval<IRenderTask>) =
    inherit AbstractRenderTask()
    let mutable inner : Option<IRenderTask> = None

    let updateInner (token : AdaptiveToken) (renderToken : RenderToken) =
        let ni = input.GetValue(token, renderToken)

        match inner with
            | Some oi when oi = ni -> ()
            | _ ->
                match inner with
                    | Some oi -> oi.Dispose()
                    | _ -> ()

                match token.Caller with
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

    override x.PerformUpdate(token, renderToken) =
        let ni = updateInner token renderToken
        ni.Update(token, renderToken)

    override x.Perform(token, renderToken, fbo) =
        let ni = updateInner token renderToken
        ni.Run(token, renderToken, fbo)

    override x.Release() =
        input.Outputs.Remove x |> ignore
        match inner with
        | Some i ->
            i.Dispose()
            inner <- None
        | _ -> ()

    override x.Runtime = input.GetValue(AdaptiveToken.Top.WithCaller x).Runtime