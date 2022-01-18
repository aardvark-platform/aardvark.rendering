namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open FSharp.Data.Adaptive

type ClearTask(device : Device, renderPass : RenderPass, values : aval<ClearValues>) =
    inherit AdaptiveObject()

    let id = newId()
    let pool = device.GraphicsFamily.CreateCommandPool()
    let cmd = pool.CreateCommandBuffer(CommandBufferLevel.Primary)

    let renderPassDepthAspect =
        match renderPass.DepthStencilAttachment with
        | Some format -> ImageAspect.ofTextureAspects format.Aspects
        | _ -> ImageAspect.None

    member x.Run(caller : AdaptiveToken, renderToken : RenderToken, outputs : OutputDescription) =
        x.EvaluateAlways caller (fun caller ->
            let fbo = unbox<Framebuffer> outputs.framebuffer
            renderPass |> RenderPass.validateCompability fbo

            use token = device.Token

            let values = values.GetValue caller
            let depth = values.Depth
            let stencil = values.Stencil

            let vulkanQueries = renderToken.Query.ToVulkanQuery()

            renderToken.Query.Begin()

            token.enqueue {
                for q in vulkanQueries do
                    do! Command.Begin q

                for KeyValue(_, att) in renderPass.ColorAttachments do
                    match values.Colors.[att.Name] with
                    | Some color -> do! Command.ClearColor(fbo.Attachments.[att.Name], ImageAspect.Color, color)
                    | _ -> ()

                if renderPassDepthAspect <> ImageAspect.None then
                    let view = fbo.Attachments.[DefaultSemantic.DepthStencil]
                    match depth, stencil with
                    | Some d, Some s -> do! Command.ClearDepthStencil(view, renderPassDepthAspect, d, s)
                    | Some d, None   -> do! Command.ClearDepthStencil(view, ImageAspect.Depth, d, 0)
                    | None, Some s   -> do! Command.ClearDepthStencil(view, ImageAspect.Stencil, 0.0, s)
                    | None, None     -> ()

                for q in vulkanQueries do
                    do! Command.End q
            }

            renderToken.Query.End()

            token.Sync()
        )

    interface IRenderTask with
        member x.Id = id
        member x.Update(c, t) = ()
        member x.Run(c,t,o) = x.Run(c,t,o)
        member x.Dispose() =
            cmd.Dispose()
            pool.Dispose()

        member x.FrameId = 0UL
        member x.FramebufferSignature = Some (renderPass :> _)
        member x.Runtime = Some device.Runtime
        member x.Use f = lock x f