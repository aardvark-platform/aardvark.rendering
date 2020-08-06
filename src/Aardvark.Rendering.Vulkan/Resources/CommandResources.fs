namespace Aardvark.Rendering.Vulkan

open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop

#nowarn "9"
// #nowarn "51"


[<AbstractClass; Sealed; Extension>]
type DeviceDrawCallExtensions private() =
    [<Extension>]
    static member CreateDrawCall(this : Device, indexed : bool, calls : list<DrawCallInfo>) =
        DrawCall.Direct(indexed, List.toArray calls)


    [<Extension>]
    static member CreateDrawCall(this : Device, indexed : bool, buffer : VkIndirectBuffer) =
        DrawCall.Indirect(indexed, buffer.Handle, buffer.Count)


[<AbstractClass; Sealed; Extension>]
type DeviceVertexBufferBindingExtensions private() =
    [<Extension>]
    static member CreateVertexBufferBinding(device : Device, first : int, buffersAndOffsets : array<Buffer * int64>) =
        let value = new VertexBufferBinding(first, buffersAndOffsets |> Array.map (fun (b,o) -> b.Handle, o))
        let ptr = NativePtr.alloc 1
        NativePtr.write ptr value
        ptr

    [<Extension>]
    static member UpdateVertexBufferBinding(device : Device, ptr : nativeptr<VertexBufferBinding>, first : int, buffersAndOffsets : array<Buffer * int64>) =
        let old = NativePtr.read ptr
        old.Dispose()

        let value = new VertexBufferBinding(first, buffersAndOffsets |> Array.map (fun (b,o) -> b.Handle, o))
        NativePtr.write ptr value

    [<Extension>]
    static member Delete(this : Device, ptr : nativeptr<VertexBufferBinding>) =
        let old = NativePtr.read ptr
        old.Dispose()
        NativePtr.free ptr


[<AbstractClass; Sealed; Extension>]
type DeviceDescriptorSetBindingExtensions private() =
    [<Extension>]
    static member CreateDescriptorSetBinding(device : Device, layout : PipelineLayout, first : int, sets : array<DescriptorSet>) =
        let value = new DescriptorSetBinding(layout.Handle, first, sets |> Array.map (fun d -> d.Handle))
        let ptr = NativePtr.alloc 1
        NativePtr.write ptr value
        ptr

    [<Extension>]
    static member UpdateDescriptorSetBinding(device : Device, ptr : nativeptr<DescriptorSetBinding>,  layout : PipelineLayout, first : int, sets : array<DescriptorSet>) =
        let old = NativePtr.read ptr
        old.Dispose()

        let value = new DescriptorSetBinding(layout.Handle, first, sets |> Array.map (fun d -> d.Handle))
        NativePtr.write ptr value

    [<Extension>]
    static member Delete(this : Device, ptr : nativeptr<DescriptorSetBinding>) =
        let old = NativePtr.read ptr
        old.Dispose()
        NativePtr.free ptr

[<AbstractClass; Sealed; Extension>]
type DeviceIndexBufferBindingExtensions private() =
    [<Extension>]
    static member CreateIndexBufferBinding(device : Device, buffer : Buffer, t : VkIndexType) =
        let value = new IndexBufferBinding(buffer.Handle, t)
        let ptr = NativePtr.alloc 1
        NativePtr.write ptr value
        ptr

    [<Extension>]
    static member UpdateIndexBufferBinding(device : Device, ptr : nativeptr<IndexBufferBinding>, buffer : Buffer, t : VkIndexType) =
        let value = new IndexBufferBinding(buffer.Handle, t)
        NativePtr.write ptr value

    [<Extension>]
    static member Delete(this : Device, ptr : nativeptr<IndexBufferBinding>) =
        NativePtr.free ptr
