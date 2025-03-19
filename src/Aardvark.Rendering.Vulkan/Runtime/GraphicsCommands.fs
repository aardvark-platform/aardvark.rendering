namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop

#nowarn "9"

[<AutoOpen>]
module ``Graphics Commands`` =

    let private endPass =
        { new Command() with
            member x.Compatible = QueueFlags.Graphics
            member x.Enqueue cmd =
                cmd.AppendCommand()
                VkRaw.vkCmdEndRenderPass(cmd.Handle)
                []
        }

    type Command with
        static member BeginPass(renderPass : RenderPass, framebuffer : Framebuffer, bounds : Box2i, inlineContent : bool) =
            { new Command() with
                member x.Compatible = QueueFlags.Graphics
                member x.Enqueue cmd =
                    native {
                        let! pInfo =
                            VkRenderPassBeginInfo(
                                renderPass.Handle,
                                framebuffer.Handle,
                                VkRect2D(VkOffset2D(bounds.Min.X, bounds.Min.Y), VkExtent2D(1 + bounds.SizeX, 1 + bounds.SizeY)),
                                0u,
                                NativePtr.zero
                            )

                        cmd.AppendCommand()
                        VkRaw.vkCmdBeginRenderPass(cmd.Handle, pInfo, if inlineContent then VkSubpassContents.Inline else VkSubpassContents.SecondaryCommandBuffers)
                        return [renderPass :> IResource; framebuffer :> IResource]
                    }
            }

        static member BeginPass(renderPass : RenderPass, framebuffer : Framebuffer, inlineContent : bool) =
            Command.BeginPass(renderPass, framebuffer, Box2i(V2i.Zero, framebuffer.Size - V2i.II), inlineContent)

        static member EndPass = endPass

        static member SetViewports(viewports : Box2i[]) =
            { new Command() with
                member x.Compatible = QueueFlags.Graphics
                member x.Enqueue cmd =
                    cmd.AppendCommand()
                    native {
                        let! pViewports =
                            viewports |> Array.map (fun b ->
                                VkViewport(float32 b.Min.X, float32 b.Min.X, float32 (1 + b.SizeX), float32 (1 + b.SizeY), 0.0f, 1.0f)
                            )

                        VkRaw.vkCmdSetViewport(cmd.Handle, 0u, uint32 viewports.Length, pViewports)

                        return []
                    }
            }

        static member SetScissors(scissors : Box2i[]) =
            { new Command() with
                member x.Compatible = QueueFlags.Graphics
                member x.Enqueue cmd =
                    cmd.AppendCommand()
                    native {
                        let! pScissors =
                            scissors |> Array.map (fun b ->
                                VkRect2D(VkOffset2D(b.Min.X, b.Min.Y), VkExtent2D(1 + b.SizeX, 1 + b.SizeY))
                            )

                        VkRaw.vkCmdSetScissor(cmd.Handle, 0u, uint32 scissors.Length, pScissors)
                        return []
                    }
            }