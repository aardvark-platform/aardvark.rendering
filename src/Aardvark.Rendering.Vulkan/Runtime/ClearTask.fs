namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open FSharp.Data.Adaptive

type ClearTask(device : Device, renderPass : RenderPass, values : aval<ClearValues>) =
    inherit AdaptiveObject()

    let id = RenderTaskId.New()

    let renderPassDepthAspect =
        match renderPass.DepthStencilAttachment with
        | Some format -> format |> VkFormat.toTextureFormat |> TextureFormat.toAspect
        | _ -> TextureAspect.None

    member val Name : string = null with get, set

    member x.Run(caller : AdaptiveToken, renderToken : RenderToken, outputs : OutputDescription) =
        x.EvaluateAlways caller (fun caller ->
            let fbo = unbox<Framebuffer> outputs.framebuffer
            renderPass |> RenderPass.validateCompability fbo

            use dt = device.Token
            use __ = renderToken.Use()

            let values = values.GetValue(caller, renderToken)
            let depth = values.Depth
            let stencil = values.Stencil

            let vulkanQueries = renderToken.GetVulkanQueries()

            dt.perform {
                do! Command.BeginLabel(x.Name |?? "Clear Task", DebugColor.ClearTask)

                for q in vulkanQueries do
                    do! Command.Begin q

                for KeyValue(_, att) in renderPass.ColorAttachments do
                    match values.[att.Name] with
                    | Some color -> do! Command.ClearColor(fbo.Attachments.[att.Name], TextureAspect.Color, color)
                    | _ -> ()

                if renderPassDepthAspect <> TextureAspect.None then
                    let view = fbo.Attachments.[DefaultSemantic.DepthStencil]
                    match depth, stencil with
                    | Some d, Some s -> do! Command.ClearDepthStencil(view, renderPassDepthAspect, d, s)
                    | Some d, None   -> do! Command.ClearDepthStencil(view, TextureAspect.Depth, d, 0)
                    | None, Some s   -> do! Command.ClearDepthStencil(view, TextureAspect.Stencil, 0.0, s)
                    | None, None     -> ()

                for q in vulkanQueries do
                    do! Command.End q

                do! Command.EndLabel()
            }
        )

    interface IRenderTask with
        member x.Name with get() = x.Name and set name = x.Name <- name
        member x.Id = id
        member x.Update(c, t) = ()
        member x.Run(c,t,o) = x.Run(c,t,o)
        member x.Dispose() = ()
        member x.FrameId = 0UL
        member x.FramebufferSignature = Some (renderPass :> _)
        member x.Runtime = Some device.Runtime
        member x.Use f = lock x f