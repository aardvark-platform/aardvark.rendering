namespace Aardvark.Base

open Aardvark.Base.Rendering
open FSharp.Data.Adaptive
open System.Runtime.CompilerServices

type private AdaptiveRenderingResult(task : IRenderTask, target : IOutputMod<IFramebuffer>) =
    inherit AbstractOutputMod<IFramebuffer>()

    override x.Compute(token : AdaptiveToken, t : RenderToken) =
        let fbo = target.GetValue(token, t)
        task.Run(token, t, OutputDescription.ofFramebuffer fbo)
        fbo

    override x.Create() =
        Log.line "result created"
        target.Acquire()

    override x.Destroy() =
        Log.line "result deleted"
        target.Release()

type private AdaptiveOutputTexture(semantic : Symbol, res : IOutputMod<IFramebuffer>) =
    inherit AbstractOutputMod<ITexture>()

    override x.Compute(token : AdaptiveToken, t : RenderToken) =
        let res = res.GetValue(token, t)

        match Map.tryFind semantic res.Attachments with
            | Some (:? IBackendTextureOutputView as t) ->
                t.texture :> ITexture
            | _ ->
                failwithf "could not get result for semantic %A as texture" semantic

    override x.Create() =
        Log.line "texture created"
        res.Acquire()

    override x.Destroy() =
        Log.line "texture deleted"
        res.Release()

[<AbstractClass; Sealed; Extension>]
type RenderToExtensions private() =

    [<Extension>]
    static member CreateFramebuffer (this : IFramebufferRuntime, signature : IFramebufferSignature, textures : Set<Symbol>, size : aval<V2i>) : IOutputMod<IFramebuffer> =
        AdaptiveFramebuffer(this, signature, textures, size) :> IOutputMod<IFramebuffer>

    [<Extension>]
    static member CreateFramebufferCube (this : IFramebufferRuntime, signature : IFramebufferSignature, textures : Set<Symbol>, size : aval<int>) : IOutputMod<IFramebuffer[]> =
        AdaptiveFramebufferCube(this, signature, textures, size) :> IOutputMod<IFramebuffer[]>

    [<Extension>]
    static member CreateFramebuffer (this : IFramebufferRuntime, signature : IFramebufferSignature, size : aval<V2i>) : IOutputMod<IFramebuffer> =
        let sems =
            Set.ofList [
                yield! signature.ColorAttachments |> Map.toSeq |> Seq.map snd |> Seq.map fst
                if Option.isSome signature.DepthAttachment then yield DefaultSemantic.Depth
                if Option.isSome signature.StencilAttachment then yield DefaultSemantic.Stencil
            ]

        AdaptiveFramebuffer(this, signature, sems, size) :> IOutputMod<IFramebuffer>

    [<Extension>]
    static member RenderTo(this : IRenderTask, output : IOutputMod<IFramebuffer>) =
        AdaptiveRenderingResult(this, output) :> IOutputMod<_>

    [<Extension>]
    static member GetOutputTexture (this : IOutputMod<IFramebuffer>, semantic : Symbol) =
        AdaptiveOutputTexture(semantic, this) :> IOutputMod<_>