namespace Aardvark.Rendering.GL

open Aardvark.Base
open Aardvark.Rendering

type internal FramebufferSignature(
                runtime : IRuntime,
                colorAttachments : Map<int, AttachmentSignature>,
                depthStencilAttachment : Option<TextureFormat>,
                samples : int,
                layers : int,
                perLayerUniforms : Set<string>) =

    member x.Runtime = runtime
    member x.Samples = samples
    member x.ColorAttachments = colorAttachments
    member x.DepthStencilAttachment = depthStencilAttachment
    member x.LayerCount = layers
    member x.PerLayerUniforms = perLayerUniforms
    member x.Dispose() = ()

    override x.ToString() = x.Layout.ToString()

    interface IFramebufferSignature with
        member x.Runtime = x.Runtime
        member x.Samples = x.Samples
        member x.ColorAttachments = x.ColorAttachments
        member x.DepthStencilAttachment = x.DepthStencilAttachment
        member x.LayerCount = x.LayerCount
        member x.PerLayerUniforms = x.PerLayerUniforms
        member x.Dispose() = x.Dispose()


[<AutoOpen>]
module internal FramebufferSignatureContextExtensions =
    open OpenTK.Graphics.OpenGL4

    type GetPName with
        static member MaxFramebufferSamples = unbox<GetPName> 0x9318

    type Context with

        member x.CreateFramebufferSignature(colorAttachments : Map<int, AttachmentSignature>,
                                            depthStencilAttachment : Option<TextureFormat>,
                                            samples : int, layers : int,
                                            perLayerUniforms : Set<string>) =
            use __ = x.ResourceLock

            let maxSamples =
                let counts = [
                    if not colorAttachments.IsEmpty then
                        GL.GetInteger(GetPName.MaxColorTextureSamples)

                    if colorAttachments |> Map.exists (fun _ att -> att.Format.IsIntegerFormat) then
                        GL.GetInteger(GetPName.MaxIntegerSamples)

                    if depthStencilAttachment.IsSome then
                        GL.GetInteger(GetPName.MaxDepthTextureSamples)
                ]

                if counts.IsEmpty then GL.GetInteger(GetPName.MaxFramebufferSamples)
                else List.min counts

            GL.Check "could not query maximum samples for framebuffer signature"

            let samples =
                if samples > maxSamples then
                    Log.warn "[GL] cannot create framebuffer signature with %d samples (using %d instead)" samples maxSamples
                    maxSamples
                else
                    samples

            new FramebufferSignature(x.Runtime, colorAttachments, depthStencilAttachment, samples, layers, perLayerUniforms)