namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Collections.Generic
open System.Collections.Concurrent
open Aardvark.Base
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop

#nowarn "9"
// #nowarn "51"

[<AutoOpen>]
module ``Graphics Commands`` =

    let private endPass =
        { new Command() with
            member x.Compatible = QueueFlags.Graphics
            member x.Enqueue cmd =
                cmd.AppendCommand()
                VkRaw.vkCmdEndRenderPass(cmd.Handle)
                Disposable.Empty
        }

    type Command with
        static member BeginPass(renderPass : RenderPass, framebuffer : Framebuffer, bounds : Box2i, inlineContent : bool) =
            { new Command() with
                member x.Compatible = QueueFlags.Graphics
                member x.Enqueue cmd =
                    native {
                        let! pInfo =
                            VkRenderPassBeginInfo(
                                VkStructureType.RenderPassBeginInfo, 0n,
                                renderPass.Handle,
                                framebuffer.Handle,
                                VkRect2D(VkOffset2D(bounds.Min.X, bounds.Min.Y), VkExtent2D(1 + bounds.SizeX, 1 + bounds.SizeY)),
                                0u,
                                NativePtr.zero
                            )
            
                        cmd.AppendCommand()
                        VkRaw.vkCmdBeginRenderPass(cmd.Handle, pInfo, if inlineContent then VkSubpassContents.Inline else VkSubpassContents.SecondaryCommandBuffers)
                        return Disposable.Empty
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
                    
                        return Disposable.Empty
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
                        return Disposable.Empty
                    }
            }

//
//[<AbstractClass; Sealed; Extension>]
//type CommandBufferRenderingExtensions private() =
//    [<Extension>]
//    static member BeginPass(this : CommandBuffer, renderPass : RenderPass, framebuffer : Framebuffer, bounds : Box2i) =
//        let mutable info =
//            VkRenderPassBeginInfo(
//                VkStructureType.RenderPassBeginInfo, 0n,
//                renderPass.Handle,
//                framebuffer.Handle,
//                VkRect2D(VkOffset2D(bounds.Min.X, bounds.Min.Y), VkExtent2D(1 + bounds.SizeX, 1 + bounds.SizeY)),
//                0u,
//                NativePtr.zero
//            )
//            
//        this.AppendCommand()
//        VkRaw.vkCmdBeginRenderPass(this.Handle, &&info, VkSubpassContents.Inline)
//
//    [<Extension>]
//    static member BeginPass(this : CommandBuffer, renderPass : RenderPass, framebuffer : Framebuffer) =
//        CommandBufferRenderingExtensions.BeginPass(this, renderPass, framebuffer, Box2i(V2i.Zero, framebuffer.Size - V2i.II))
//
//    [<Extension>]
//    static member EndPass(this : CommandBuffer) =
//        this.AppendCommand()
//        VkRaw.vkCmdEndRenderPass(this.Handle)
//
//    [<Extension>]
//    static member SetViewports(this : CommandBuffer, viewports : Box2i[]) =
//        this.AppendCommand()
//        let viewports =
//            viewports |> Array.map (fun b ->
//                VkViewport(float32 b.Min.X, float32 b.Min.X, float32 (1 + b.SizeX), float32 (1 + b.SizeY), 0.0f, 1.0f)
//            )
//
//        viewports |> NativePtr.withA (fun pViewports ->
//            VkRaw.vkCmdSetViewport(this.Handle, 0u, uint32 viewports.Length, pViewports)
//        )
//
//    [<Extension>]
//    static member SetScissors(this : CommandBuffer, scissors : Box2i[]) =
//        this.AppendCommand()
//        let scissors =
//            scissors |> Array.map (fun b ->
//                VkRect2D(VkOffset2D(b.Min.X, b.Min.X), VkExtent2D(1 + b.SizeX, 1 + b.SizeY))
//            )
//
//        scissors |> NativePtr.withA (fun pScissors ->
//            VkRaw.vkCmdSetScissor(this.Handle, 0u, uint32 scissors.Length, pScissors)
//        )
