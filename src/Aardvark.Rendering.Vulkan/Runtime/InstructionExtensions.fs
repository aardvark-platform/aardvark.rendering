namespace Aardvark.Rendering.Vulkan

open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Aardvark.Base
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"

[<AbstractClass; Sealed; Extension>]
type InstructionContextExtensions private() =
    
    static let ret (i : list<Instruction>) = 
        for i in i do infof "%A" i
        i

    [<Extension>]
    static member SetViewports(this : InstructionContext, viewports : Box2i[]) =
        let cnt = viewports.Length
        let arr = NativePtr.alloc<VkViewport> cnt

        for i in 0..viewports.Length-1 do   
            let box = viewports.[i]
            NativePtr.set arr i (VkViewport(float32 box.Min.X, float32 box.Min.Y, float32 box.SizeX, float32 box.SizeY, 0.0f, 1.0f))

        ret [ new Instruction(this, this.CmdSetViewport, cnt, NativePtr.toNativeInt arr) ]
    
    [<Extension>]
    static member SetScissors(this : InstructionContext, viewports : Box2i[]) =
        let cnt = viewports.Length
        let arr = NativePtr.alloc<VkRect2D> cnt

        for i in 0..viewports.Length-1 do   
            let box = viewports.[i]
            NativePtr.set arr i (VkRect2D(VkOffset2D(box.Min.X, box.Min.Y), VkExtent2D(box.SizeX, box.SizeY)))

        ret [ new Instruction(this, this.CmdSetScissor, cnt, NativePtr.toNativeInt arr) ]

    [<Extension>]
    static member SetLineWidth(this : InstructionContext, width : float) =
        ret [ new Instruction(this, this.CmdSetLineWidth, float32 width) ]

    [<Extension>]
    static member SetDepthBias(this : InstructionContext, depthBias : float, clampDepthBias : float, slopeScaledDepthBias : float) =
        ret [ new Instruction(this, this.CmdSetDepthBias, float32 depthBias, float32 clampDepthBias, float32 slopeScaledDepthBias) ]

    [<Extension>]
    static member SetBlendColor(this : InstructionContext, color : C4f) =
        // TODO: AdaptiveProgram needs to support struct arguments
        ret [ new Instruction(this, this.CmdSetBlendConstants, V4f(color.R, color.G, color.B, color.A)) ]

    [<Extension>]
    static member SetDepthBounds(this : InstructionContext, min : float, max : float) =
        ret [ new Instruction(this, this.CmdSetDepthBounds, float32 min, float32 max) ]

    [<Extension>]
    static member SetStencil(this : InstructionContext, compareMask : uint32, writeMask : uint32, ref : uint32) =
        ret [
            new Instruction(this, this.CmdSetStencilCompareMask, int (VkStencilFaceFlags.FrontBit ||| VkStencilFaceFlags.BackBit), int compareMask)
            new Instruction(this, this.CmdSetStencilWriteMask, int (VkStencilFaceFlags.FrontBit ||| VkStencilFaceFlags.BackBit), int writeMask)
            new Instruction(this, this.CmdSetStencilReference, int (VkStencilFaceFlags.FrontBit ||| VkStencilFaceFlags.BackBit), int ref)
        ]

    [<Extension>]
    static member BindPipeline(this : InstructionContext, pipeline : Pipeline) =
        ret [ new Instruction(this, this.CmdBindPipeline, int VkPipelineBindPoint.Graphics, pipeline.Handle)]

    [<Extension>]
    static member BindDescriptorSets(this : InstructionContext, layout : PipelineLayout, sets : DescriptorSet[], firstSet : int) =
        let cnt = sets.Length
        if cnt > 0 then
            let ptr = NativePtr.alloc<VkDescriptorSet> cnt

            for i in 0..sets.Length-1 do
                NativePtr.set ptr i (sets.[i].Handle)

            ret [ new Instruction(this, this.CmdBindDescriptorSets, int VkPipelineBindPoint.Graphics, layout.Handle, firstSet, cnt, NativePtr.toNativeInt ptr, 0, 0n) ]
        else
            ret []

    [<Extension>]
    static member BindVertexBuffers(this : InstructionContext, buffers : Buffer[], offsets : int[], startBinding : int) =    
        let cnt = buffers.Length
        let ptr = NativePtr.alloc<VkBuffer> cnt
        let pOffsets = NativePtr.alloc<uint64> cnt

        for i in 0..cnt-1 do
            NativePtr.set ptr i (buffers.[i].Handle)
            NativePtr.set pOffsets i (uint64 offsets.[i])
        ret [ new Instruction(this, this.CmdBindVertexBuffers, startBinding, cnt, ptr, pOffsets) ]

    [<Extension>]
    static member BindIndexBuffer(this : InstructionContext, buffer : Buffer, offset : int) =
        let indexType =
            match buffer.Format with
                | VkFormat.R32Sint | VkFormat.R32Uint -> VkIndexType.Uint32
                | VkFormat.R16Sint | VkFormat.R16Uint -> VkIndexType.Uint16
                | _ -> failwithf "could not determine index type for buffer format: %A" buffer.Format

        ret [ new Instruction(this, this.CmdBindIndexBuffer, buffer.Handle, uint64 offset, int indexType) ]

    [<Extension>]
    static member DrawIndexed(this : InstructionContext, firstIndex : int, indexCount : int, firstInstance : int, instanceCount : int, vertexOffset : int) =
        ret [ 
            new Instruction(this, this.CmdDrawIndexed, 
                indexCount, instanceCount, 
                firstIndex, vertexOffset, 
                firstInstance
            ) 
        ]
            
    [<Extension>]
    static member Draw(this : InstructionContext, firstVertex : int, vertexCount : int, firstInstance : int, instanceCount : int) =
        ret [ 
            new Instruction(this, this.CmdDraw, 
                vertexCount, instanceCount, 
                firstVertex, firstInstance
            ) 
        ]

    [<Extension>]
    static member DrawIndexedIndirect(this : InstructionContext, buffer : VkBuffer, offset : uint64, drawCount : int, stride : int) =
        ret [
            new Instruction(this,
                this.CmdDrawIndexedIndirect,
                buffer,
                offset, 
                uint32 drawCount,
                uint32 stride
            )
        ]

    [<Extension>]
    static member DrawIndirect(this : InstructionContext, buffer : VkBuffer, offset : uint64, drawCount : int, stride : int) =
        ret [
            new Instruction(this,
                this.CmdDrawIndirect,
                buffer,
                offset, 
                uint32 drawCount,
                uint32 stride
            )
        ]