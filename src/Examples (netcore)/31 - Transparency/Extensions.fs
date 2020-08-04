namespace Aardvark.Base

open Aardvark.Base.Rendering
open FSharp.Data.Adaptive

// TODO: Already implemented in cleanup branch...
type AdaptiveFramebuffer'(runtime : IRuntime, signature : IFramebufferSignature, attachments : Map<Symbol, IOutputMod<IFramebufferOutput>>) =
    inherit AbstractOutputMod<IFramebuffer>()

    let mutable handle = None

    let compare x y =
        let x = x |> Map.toList |> List.map snd
        let y = y |> Map.toList |> List.map snd
        List.forall2 (=) x y

    let create signature att =
        let fbo = runtime.CreateFramebuffer(signature, att)
        handle <- Some (fbo, att)
        fbo

    override x.Create() =
        for (KeyValue (_, att)) in attachments do att.Acquire()

    override x.Destroy() =
        for (KeyValue (_, att)) in attachments do att.Release()
        handle |> Option.iter (fun (fbo, _) -> runtime.DeleteFramebuffer fbo)
        handle <- None

    override x.Compute(token : AdaptiveToken, t : RenderToken) =
        let att =
            attachments |> Map.map (fun _ att -> att.GetValue(token, t))

        match handle with
        | Some (h, att') ->
            if compare att att' then
                h
            else
                t.ReplacedResource(ResourceKind.Framebuffer)
                runtime.DeleteFramebuffer(h)
                create signature att

        | None ->
            t.CreatedResource(ResourceKind.Framebuffer)
            create signature att