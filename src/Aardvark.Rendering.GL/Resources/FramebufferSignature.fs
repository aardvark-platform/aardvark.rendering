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

            let samples =
                if samples = 1 then samples
                else
                    let framebufferMaxSamples = max 1 x.MaxFramebufferSamples

                    let get fmt =
                        let rb = x.GetFormatSamples(ImageTarget.Renderbuffer, fmt)
                        let tex = x.GetFormatSamples(ImageTarget.Texture2DMultisample, fmt)
                        Set.union rb tex

                    let counts =
                        let all =
                            Set.ofList [1; 2; 4; 8; 16; 32; 64]

                        let color =
                            colorAttachments
                            |> Seq.map (_.Value.Format >> get)
                            |> List.ofSeq

                        let depthStencil =
                            depthStencilAttachment
                            |> Option.map get
                            |> Option.toList

                        all :: (color @ depthStencil)
                        |> Set.intersectMany
                        |> Set.filter ((>=) framebufferMaxSamples)

                    if counts.Contains samples then samples
                    else
                        let fallback =
                            counts
                            |> Set.toList
                            |> List.minBy ((-) samples >> abs)

                        Log.warn "[GL] Cannot create framebuffer signature with %d samples (using %d instead)" samples fallback
                        fallback

            new FramebufferSignature(x.Runtime, colorAttachments, depthStencilAttachment, samples, layers, perLayerUniforms)