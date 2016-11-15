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
#nowarn "51"


[<StructLayout(LayoutKind.Sequential)>]
type DrawCall =
    struct
        val mutable public IsIndirect       : int
        val mutable public IsIndexed        : int
        val mutable public IndirectBuffer   : VkBuffer
        val mutable public IndirectCount    : int
        val mutable public DrawCallCount    : int
        val mutable public DrawCalls        : nativeptr<DrawCallInfo>


        static member Indirect (indexed : bool, ib : IndirectBuffer) =
            new DrawCall(true, indexed, ib.Handle, ib.Count, 0, NativePtr.zero)

        static member Direct (indexed : bool, calls : DrawCallInfo[]) =
            let pCalls = NativePtr.alloc calls.Length
            for i in 0 .. calls.Length-1 do
                NativePtr.set pCalls i calls.[i]
            new DrawCall(false, indexed, VkBuffer.Null, 0, calls.Length, pCalls)
                
        member x.Dispose() =
            if not (NativePtr.isNull x.DrawCalls) then
                NativePtr.free x.DrawCalls

            x.IndirectBuffer <- VkBuffer.Null
            x.IndirectCount <- 0
            x.DrawCalls <- NativePtr.zero
            x.DrawCallCount <- 0

        interface IDisposable with
            member x.Dispose() = x.Dispose()

        private new(isIndirect : bool, isIndexed : bool, ib : VkBuffer, ibc : int, callCount : int, pCalls : nativeptr<DrawCallInfo>) =
            {
                IsIndirect = (if isIndirect then 1 else 0)
                IsIndexed = (if isIndexed then 1 else 0)
                IndirectBuffer = ib
                IndirectCount = ibc
                DrawCallCount = callCount
                DrawCalls = pCalls
            }

    end

[<AbstractClass; Sealed; Extension>]
type DeviceDrawCallExtensions private() =
    [<Extension>]
    static member CreateDrawCall(this : Device, indexed : bool, calls : list<DrawCallInfo>) =
        let res = DrawCall.Direct(indexed, List.toArray calls)
        let ptr = NativePtr.alloc 1
        NativePtr.write ptr res
        ptr

    [<Extension>]
    static member CreateDrawCall(this : Device, indexed : bool, buffer : IndirectBuffer) =
        let res = DrawCall.Indirect(indexed, buffer)
        let ptr = NativePtr.alloc 1
        NativePtr.write ptr res
        ptr

    [<Extension>]
    static member UpdateDrawCall(this : Device, ptr : nativeptr<DrawCall>, indexed : bool, calls : list<DrawCallInfo>) =
        let old = NativePtr.read ptr
        old.Dispose()
        let res = DrawCall.Direct(indexed, List.toArray calls)
        NativePtr.write ptr res

    [<Extension>]
    static member UpdateDrawCall(this : Device, ptr : nativeptr<DrawCall>, indexed : bool, indirect : IndirectBuffer) =
        let old = NativePtr.read ptr
        old.Dispose()
        let res = DrawCall.Indirect(indexed, indirect)
        NativePtr.write ptr res

    [<Extension>]
    static member Delete(this : Device, ptr : nativeptr<DrawCall>) =
        if not (NativePtr.isNull ptr) then
            let old = NativePtr.read ptr
            old.Dispose()
            NativePtr.free ptr



[<StructLayout(LayoutKind.Sequential)>]
type VertexBufferBinding =
    struct
        val mutable public FirstBinding : int
        val mutable public BindingCount : int
        val mutable public Buffers : nativeptr<VkBuffer>
        val mutable public Offsets : nativeptr<uint64>

        member x.Dispose() =
            if not (NativePtr.isNull x.Buffers) then
                NativePtr.free x.Buffers
                x.Buffers <- NativePtr.zero

            if not (NativePtr.isNull x.Offsets) then
                NativePtr.free x.Offsets
                x.Offsets <- NativePtr.zero

            x.FirstBinding <- 0
            x.BindingCount <- 0

        interface IDisposable with
            member x.Dispose() = x.Dispose()

        new(first : int, buffersAndOffsets : array<Buffer * int64>) =
            let count = buffersAndOffsets.Length
            let pBuffers = NativePtr.alloc count
            let pOffsets = NativePtr.alloc count

            for i in 0 .. buffersAndOffsets.Length-1 do
                let (b, o) = buffersAndOffsets.[i]
                NativePtr.set pBuffers i (b.Handle)
                NativePtr.set pOffsets i (uint64 o)

            {
                FirstBinding = first
                BindingCount = count
                Buffers = pBuffers
                Offsets = pOffsets
            }
    end

[<AbstractClass; Sealed; Extension>]
type DeviceVertexBufferBindingExtensions private() =
    [<Extension>]
    static member CreateVertexBufferBinding(device : Device, first : int, buffersAndOffsets : array<Buffer * int64>) =
        let value = new VertexBufferBinding(first, buffersAndOffsets)
        let ptr = NativePtr.alloc 1
        NativePtr.write ptr value
        ptr

    [<Extension>]
    static member UpdateVertexBufferBinding(device : Device, ptr : nativeptr<VertexBufferBinding>, first : int, buffersAndOffsets : array<Buffer * int64>) =
        let old = NativePtr.read ptr
        old.Dispose()

        let value = new VertexBufferBinding(first, buffersAndOffsets)
        NativePtr.write ptr value

    [<Extension>]
    static member Delete(this : Device, ptr : nativeptr<VertexBufferBinding>) =
        let old = NativePtr.read ptr
        old.Dispose()
        NativePtr.free ptr



[<StructLayout(LayoutKind.Sequential)>]
type DescriptorSetBinding =
    struct
        val mutable public FirstIndex : int
        val mutable public Count : int
        val mutable public Layout : VkPipelineLayout
        val mutable public Sets : nativeptr<VkDescriptorSet>

        member x.Dispose() =
            if not (NativePtr.isNull x.Sets) then
                NativePtr.free x.Sets
                x.Sets <- NativePtr.zero

            x.Layout <- VkPipelineLayout.Null
            x.FirstIndex <- 0
            x.Count <- 0

        interface IDisposable with
            member x.Dispose() = x.Dispose()

        new(layout : PipelineLayout, first : int, sets : array<DescriptorSet>) =
            let count = sets.Length
            let pSets = NativePtr.alloc count

            for i in 0 .. count-1 do
                let s = sets.[i]
                NativePtr.set pSets i (s.Handle)

            {
                FirstIndex = first
                Count = count
                Layout = layout.Handle
                Sets = pSets
            }
    end

[<AbstractClass; Sealed; Extension>]
type DeviceDescriptorSetBindingExtensions private() =
    [<Extension>]
    static member CreateDescriptorSetBinding(device : Device, layout : PipelineLayout, first : int, sets : array<DescriptorSet>) =
        let value = new DescriptorSetBinding(layout, first, sets)
        let ptr = NativePtr.alloc 1
        NativePtr.write ptr value
        ptr

    [<Extension>]
    static member UpdateDescriptorSetBinding(device : Device, ptr : nativeptr<DescriptorSetBinding>,  layout : PipelineLayout, first : int, sets : array<DescriptorSet>) =
        let old = NativePtr.read ptr
        old.Dispose()

        let value = new DescriptorSetBinding(layout, first, sets)
        NativePtr.write ptr value

    [<Extension>]
    static member Delete(this : Device, ptr : nativeptr<DescriptorSetBinding>) =
        let old = NativePtr.read ptr
        old.Dispose()
        NativePtr.free ptr
