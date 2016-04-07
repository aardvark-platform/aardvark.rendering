namespace Aardvark.Rendering.Vulkan

open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Aardvark.Base
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"

[<AbstractClass; Sealed; Extension>]
type BufferCommandExtensions private() =
    
    [<Extension>]
    static member BeginPass(this : CommandBuffer, pass : RenderPass, fbo : Framebuffer) =

        let mutable info =
            VkRenderPassBeginInfo(
                VkStructureType.RenderPassBeginInfo, 0n,
                pass.Handle, fbo.Handle,
                VkRect2D(VkOffset2D(0,0), VkExtent2D(fbo.Size.X, fbo.Size.Y)),
                0u,
                NativePtr.zero
            )

        VkRaw.vkCmdBeginRenderPass(
            this.Handle,
            &&info,
            VkSubpassContents.Inline
        )

        ()
    
    [<Extension>]
    static member EndPass(this : CommandBuffer) =
        VkRaw.vkCmdEndRenderPass(this.Handle)


    [<Extension>]
    static member SetViewports(this : CommandBuffer, viewports : Box2i[]) =
        
        let pViewports = NativePtr.stackalloc viewports.Length

        for i in 0..viewports.Length-1 do
            let b = viewports.[i]
            let viewport = 
                VkViewport(
                    float32 b.Min.X, float32 b.Min.Y, 
                    float32 (1 + b.SizeX), float32 (1 + b.SizeY),
                    0.0f, 1.0f
                )
            NativePtr.set pViewports i viewport
        
        VkRaw.vkCmdSetViewport(
            this.Handle,
            0u,
            uint32 viewports.Length,
            pViewports
        )

    [<Extension>]
    static member SetViewport(this : CommandBuffer, viewport : Box2i) =
        BufferCommandExtensions.SetViewports(this, [|viewport|])

    [<Extension>]
    static member SetViewport(this : CommandBuffer, viewportSize : V2i) =
        BufferCommandExtensions.SetViewports(this, [|Box2i(V2i.Zero, viewportSize - V2i.II)|])



    [<Extension>]
    static member SetScissors(this : CommandBuffer, scissors : Box2i[]) =
        
        let pScissors = NativePtr.stackalloc scissors.Length

        for i in 0..scissors.Length-1 do
            let b = scissors.[i]
            let scissor = 
                VkRect2D(
                    VkOffset2D(b.Min.X, b.Min.Y),
                    VkExtent2D(b.SizeX + 1, b.SizeY + 1)
                )
            NativePtr.set pScissors i scissor
        
        VkRaw.vkCmdSetScissor(
            this.Handle,
            0u,
            uint32 scissors.Length,
            pScissors
        )

    [<Extension>]
    static member SetScissor(this : CommandBuffer, scissor : Box2i) =
        BufferCommandExtensions.SetScissors(this, [| scissor |])

    [<Extension>]
    static member SetScissor(this : CommandBuffer, scissorSize : V2i) =
        BufferCommandExtensions.SetScissors(this, [| Box2i(V2i.Zero, scissorSize - V2i.II) |])

    [<Extension>]
    static member SetBlendColor(this : CommandBuffer, color : C4f) =
        VkRaw.vkCmdSetBlendConstants(this.Handle, C4f.op_Explicit color)

    [<Extension>]        
    static member SetLineWidth(this : CommandBuffer, width : float) =
        VkRaw.vkCmdSetLineWidth(this.Handle, float32 width)

    [<Extension>]        
    static member SetDepthBias(this : CommandBuffer, constant : float, clamp : float, slope : float) =
        VkRaw.vkCmdSetDepthBias(this.Handle, float32 constant, float32 clamp, float32 slope)        

    [<Extension>]        
    static member SetDepthBounds(this : CommandBuffer, min : float, max : float) =
        VkRaw.vkCmdSetDepthBounds(this.Handle, float32 min, float32 max)

    [<Extension>]        
    static member SetStencil(this : CommandBuffer, compareMask : uint32, writeMask : uint32, ref : uint32) =
        VkRaw.vkCmdSetStencilCompareMask(this.Handle, VkStencilFaceFlags.FrontBit ||| VkStencilFaceFlags.BackBit, compareMask)
        VkRaw.vkCmdSetStencilWriteMask(this.Handle, VkStencilFaceFlags.FrontBit ||| VkStencilFaceFlags.BackBit, writeMask)
        VkRaw.vkCmdSetStencilReference(this.Handle, VkStencilFaceFlags.FrontBit ||| VkStencilFaceFlags.BackBit, ref)
