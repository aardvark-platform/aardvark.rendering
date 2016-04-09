namespace Aardvark.Rendering.Vulkan

open System
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"

type Instruction(ctx : InstructionContext, ptr : nativeint, [<ParamArray>] args : obj[]) =
        
    member x.FunctionPointer = ptr
    member x.Arguments = args

    member x.NativeCall : NativeCall = ptr, args

    member x.Dispose() =
        for a in args do
            match a with
                | :? nativeint as n when n <> 0n -> Marshal.FreeHGlobal n
                | :? IDisposable as d -> d.Dispose()
                | _ -> ()

    override x.ToString() = ctx.ToString x

    interface IDisposable with
        member x.Dispose() = x.Dispose()




and InstructionContext(d : Device) =

    let getAndCheckAddress (device : VkDevice, name : string) =
        let ptr = VkRaw.vkGetDeviceProcAddr(d.Handle, name)
        if ptr = 0n then
            failwithf "could not get device pointer for %s" name
        ptr

    let vkCmdSetViewport = getAndCheckAddress(d.Handle, "vkCmdSetViewport") // 3 args
    let vkCmdSetScissor = getAndCheckAddress(d.Handle, "vkCmdSetScissor") // 3 args
    let vkCmdSetLineWidth = getAndCheckAddress(d.Handle, "vkCmdSetLineWidth") // 2 args
    let vkCmdSetDepthBias = getAndCheckAddress(d.Handle, "vkCmdSetDepthBias") // 4 args
    let vkCmdSetBlendConstants = getAndCheckAddress(d.Handle, "vkCmdSetBlendConstants") // 2 args
    let vkCmdSetDepthBounds = getAndCheckAddress(d.Handle, "vkCmdSetDepthBounds") // 3 args
    let vkCmdSetStencilCompareMask = getAndCheckAddress(d.Handle, "vkCmdSetStencilCompareMask") // 3 args
    let vkCmdSetStencilWriteMask = getAndCheckAddress(d.Handle, "vkCmdSetStencilWriteMask") // 3 args
    let vkCmdSetStencilReference = getAndCheckAddress(d.Handle, "vkCmdSetStencilReference") // 3 args
    let vkCmdBindPipeline = getAndCheckAddress(d.Handle, "vkCmdBindPipeline") // 3 args
    let vkCmdBindDescriptorSets = getAndCheckAddress(d.Handle, "vkCmdBindDescriptorSets") // 8 args
    let vkCmdBindVertexBuffers = getAndCheckAddress(d.Handle, "vkCmdBindVertexBuffers") // 5 args
    let vkCmdBindIndexBuffer = getAndCheckAddress(d.Handle, "vkCmdBindIndexBuffer") // 4 args
    let vkCmdDrawIndexed = getAndCheckAddress(d.Handle, "vkCmdDrawIndexed") // 6 args
    let vkCmdDraw = getAndCheckAddress(d.Handle, "vkCmdDraw") // 5 args
    let vkCmdDrawIndexedIndirect = getAndCheckAddress(d.Handle, "vkCmdDrawIndexedIndirect") // 5 args
    let vkCmdDrawIndirect = getAndCheckAddress(d.Handle, "vkCmdDrawIndirect") // 5 args

    member x.CmdSetViewport = vkCmdSetViewport
    member x.CmdSetScissor = vkCmdSetScissor
    member x.CmdSetLineWidth = vkCmdSetLineWidth
    member x.CmdSetDepthBias = vkCmdSetDepthBias
    member x.CmdSetBlendConstants = vkCmdSetBlendConstants
    member x.CmdSetDepthBounds = vkCmdSetDepthBounds
    member x.CmdSetStencilCompareMask = vkCmdSetStencilCompareMask
    member x.CmdSetStencilWriteMask = vkCmdSetStencilWriteMask
    member x.CmdSetStencilReference = vkCmdSetStencilReference
    member x.CmdBindPipeline = vkCmdBindPipeline
    member x.CmdBindDescriptorSets = vkCmdBindDescriptorSets
    member x.CmdBindVertexBuffers = vkCmdBindVertexBuffers
    member x.CmdBindIndexBuffer = vkCmdBindIndexBuffer
    member x.CmdDrawIndexed = vkCmdDrawIndexed
    member x.CmdDraw = vkCmdDraw
    member x.CmdDrawIndexedIndirect = vkCmdDrawIndexedIndirect
    member x.CmdDrawIndirect = vkCmdDrawIndirect

    member x.Run(i : Instruction, cmd : VkCommandBuffer) =
        let ptr = i.FunctionPointer
            
        if ptr = 0n then
            failwith "invalid instruction"
        elif ptr = vkCmdSetViewport then
            VkRaw.vkCmdSetViewport(
                cmd, 
                uint32 (unbox<int> i.Arguments.[0]),
                uint32 (unbox<int> i.Arguments.[1]), 
                NativePtr.ofNativeInt (unbox<_> i.Arguments.[2])
            )
        elif ptr = vkCmdSetScissor then
            VkRaw.vkCmdSetScissor(
                cmd, 
                uint32 (unbox<int> i.Arguments.[0]),
                uint32 (unbox<int> i.Arguments.[1]), 
                NativePtr.ofNativeInt (unbox<_> i.Arguments.[2])
            )
        elif ptr = vkCmdSetLineWidth then
            VkRaw.vkCmdSetLineWidth(
                cmd,
                unbox<float32> i.Arguments.[0]
            )
        elif ptr = vkCmdSetDepthBias then
            VkRaw.vkCmdSetDepthBias(
                cmd,
                unbox<float32> i.Arguments.[0],
                unbox<float32> i.Arguments.[1],
                unbox<float32> i.Arguments.[2]
            )
        elif ptr = vkCmdSetBlendConstants then
            VkRaw.vkCmdSetBlendConstants(
                cmd,
                unbox<V4f> i.Arguments.[0]
            )
        elif ptr = vkCmdSetDepthBounds then
            VkRaw.vkCmdSetDepthBounds(
                cmd,
                unbox<float32> i.Arguments.[0],
                unbox<float32> i.Arguments.[1]
            )
        elif ptr = vkCmdSetStencilCompareMask then
            VkRaw.vkCmdSetStencilCompareMask(
                cmd,
                unbox (unbox<int> i.Arguments.[0]),
                uint32 (unbox<int> i.Arguments.[1])
            )
        elif ptr = vkCmdSetStencilReference then
            VkRaw.vkCmdSetStencilReference(
                cmd,
                unbox (unbox<int> i.Arguments.[0]),
                uint32 (unbox<int> i.Arguments.[1])
            )
        elif ptr = vkCmdSetStencilWriteMask then
            VkRaw.vkCmdSetStencilWriteMask(
                cmd,
                unbox (unbox<int> i.Arguments.[0]),
                uint32 (unbox<int> i.Arguments.[1])
            )
        elif ptr = vkCmdBindPipeline then
            VkRaw.vkCmdBindPipeline(
                cmd,
                unbox (unbox<int> i.Arguments.[0]),
                unbox i.Arguments.[1]
            )
        elif ptr = vkCmdBindDescriptorSets then
            VkRaw.vkCmdBindDescriptorSets(
                cmd,
                unbox (unbox<int> i.Arguments.[0]),
                unbox i.Arguments.[1],
                uint32 (unbox<int> i.Arguments.[2]),
                uint32 (unbox<int> i.Arguments.[3]),
                NativePtr.ofNativeInt (unbox i.Arguments.[4]),
                uint32 (unbox<int> i.Arguments.[5]),
                NativePtr.ofNativeInt (unbox i.Arguments.[6])
            )
        elif ptr = vkCmdBindVertexBuffers then
            VkRaw.vkCmdBindVertexBuffers(
                cmd,
                uint32 (unbox<int> i.Arguments.[0]),
                uint32 (unbox<int> i.Arguments.[1]),
                NativePtr.ofNativeInt (unbox i.Arguments.[2]),
                NativePtr.ofNativeInt (unbox i.Arguments.[3])
            )
        elif ptr = vkCmdBindIndexBuffer then
            VkRaw.vkCmdBindIndexBuffer(
                cmd,
                (unbox i.Arguments.[0]),
                (unbox<uint64> i.Arguments.[1]),
                unbox (unbox<int> i.Arguments.[2])
            )
        elif ptr = vkCmdDrawIndexed then
            VkRaw.vkCmdDrawIndexed(
                cmd,
                uint32 (unbox<int> i.Arguments.[0]),
                uint32 (unbox<int> i.Arguments.[1]),
                uint32 (unbox<int> i.Arguments.[2]),
                (unbox<int> i.Arguments.[3]),
                uint32 (unbox<int> i.Arguments.[4])
            )
        elif ptr = vkCmdDraw then
            VkRaw.vkCmdDraw(
                cmd,
                uint32 (unbox<int> i.Arguments.[0]),
                uint32 (unbox<int> i.Arguments.[1]),
                uint32 (unbox<int> i.Arguments.[2]),
                uint32 (unbox<int> i.Arguments.[3])
            )
        else
            failwith "unknown instruction"

    member x.ToString(i : Instruction) =
        let ptr = i.FunctionPointer
            
        if ptr = 0n then
            failwith "invalid instruction"
        elif ptr = vkCmdSetViewport then
            let cnt = unbox<int> i.Arguments.[0]
            let arr = i.Arguments.[1] |> unbox |> NativePtr.ofNativeInt<VkViewport> |> NativePtr.toArray cnt

            sprintf "vkCmdSetViewport(%A, %A)" cnt arr
        elif ptr = vkCmdSetScissor then
            let cnt = unbox<int> i.Arguments.[0]
            let arr = i.Arguments.[1] |> unbox |> NativePtr.ofNativeInt<VkRect2D> |> NativePtr.toArray cnt
            sprintf "vkCmdSetScissor(%A, %A)" cnt arr
        elif ptr = vkCmdSetLineWidth then   
            let w = unbox<float32> i.Arguments.[0]
            sprintf "vkCmdSetLineWidth(%A)" w
        elif ptr = vkCmdSetDepthBias then
            let a = unbox<float32> i.Arguments.[0]
            let b = unbox<float32> i.Arguments.[1]
            let c = unbox<float32> i.Arguments.[2]
            sprintf "vkCmdSetDepthBias(%A, %A, %A)" a b c
        elif ptr = vkCmdSetBlendConstants then
            let col : C4f = i.Arguments.[0] |> unbox<V4f> |> V4f.op_Explicit
            sprintf "vkCmdSetBlendConstants(%A)" col
        elif ptr = vkCmdSetDepthBounds then
            let min = unbox<float32> i.Arguments.[0]
            let max = unbox<float32> i.Arguments.[1]
            sprintf "vkCmdSetDepthBounds(%A, %A)" min max
        elif ptr = vkCmdSetStencilCompareMask then
            let face = unbox<VkStencilFaceFlags> (unbox<int> i.Arguments.[0])
            let mask = uint32 (unbox<int> i.Arguments.[1])
            sprintf "vkCmdSetStencilCompareMask(%A, %A)" face mask
        elif ptr = vkCmdSetStencilReference then
            let face = unbox<VkStencilFaceFlags> (unbox<int> i.Arguments.[0])
            let mask = uint32 (unbox<int> i.Arguments.[1])
            sprintf "vkCmdSetStencilReference(%A, %A)" face mask
        elif ptr = vkCmdSetStencilWriteMask then
            let face = unbox<VkStencilFaceFlags> (unbox<int> i.Arguments.[0])
            let mask = uint32 (unbox<int> i.Arguments.[1])
            sprintf "vkCmdSetStencilWriteMask(%A, %A)" face mask
        elif ptr = vkCmdBindPipeline then
            let stage = unbox<VkPipelineBindPoint> (unbox<int> i.Arguments.[0])
            let pipe = unbox<VkPipeline> i.Arguments.[1]
            sprintf "vkCmdBindPipeline(%A, %A)" stage pipe
        elif ptr = vkCmdBindDescriptorSets then
            let stage = unbox<VkPipelineBindPoint> (unbox<int> i.Arguments.[0])
            let layout = unbox<VkPipelineLayout> i.Arguments.[1]
            let firstSet = (unbox<int> i.Arguments.[2])
            let cnt = (unbox<int> i.Arguments.[3])
            let setPtr = NativePtr.ofNativeInt<VkDescriptorSet> (unbox i.Arguments.[4])
            let setArr = setPtr |> NativePtr.toArray cnt
            sprintf "vkCmdBindDescriptorSets(%A, %A, %A, %A, 0, 0n)" stage layout firstSet setArr
        elif ptr = vkCmdBindVertexBuffers then
            let fst = unbox<int> i.Arguments.[0]
            let cnt = unbox<int> i.Arguments.[1]
            let buffers = NativePtr.ofNativeInt<VkBuffer> (unbox i.Arguments.[2]) |> NativePtr.toArray cnt
            let offsets = NativePtr.ofNativeInt<VkDeviceSize> (unbox i.Arguments.[3]) |> NativePtr.toArray cnt
            sprintf "vkCmdBindVertexBuffers(%A, %A, %A)" fst buffers offsets

        elif ptr = vkCmdBindIndexBuffer then
            let buffer = unbox<VkBuffer> i.Arguments.[0]
            let offset = unbox<uint64> i.Arguments.[1]
            let t = unbox<VkIndexType> (unbox<int> i.Arguments.[2])
            sprintf "vkCmdBindIndexBuffer(%A, %A, %A)" buffer offset t
        elif ptr = vkCmdDrawIndexed then
            let a = (unbox<int> i.Arguments.[0])
            let b = (unbox<int> i.Arguments.[1])
            let c = (unbox<int> i.Arguments.[2])
            let d = (unbox<int> i.Arguments.[3])
            let e = (unbox<int> i.Arguments.[4])
            sprintf "vkCmdDrawIndexed(%A,%A,%A,%A,%A)" a b c d e
        elif ptr = vkCmdDraw then
            let a = (unbox<int> i.Arguments.[0])
            let b = (unbox<int> i.Arguments.[1])
            let c = (unbox<int> i.Arguments.[2])
            let d = (unbox<int> i.Arguments.[3])
            sprintf "vkCmdDraw(%A,%A,%A,%A)" a b c d
        else
            failwith "unknown instruction"
