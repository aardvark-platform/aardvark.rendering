namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Collections.Generic
open System.Collections.Concurrent
open Aardvark.Base
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"

// =======================================================================
// Resource Definition
// =======================================================================
type Buffer =
    class
        inherit Resource<VkBuffer>
        val mutable public Memory : DevicePtr
        val mutable public Size : int64
        val mutable public RefCount : int


        interface IBackendBuffer with
            member x.Handle = x.Handle :> obj
            member x.SizeInBytes = nativeint x.Memory.Size

        member x.AddReference() = Interlocked.Increment(&x.RefCount) |> ignore

        new(device, handle, memory, size) = { inherit Resource<_>(device, handle); Memory = memory; Size = size; RefCount = 1 }
    end

type BufferView =
    class
        inherit Resource<VkBufferView>
        val mutable public Buffer : Buffer
        val mutable public Format : VkFormat
        val mutable public Offset : uint64
        val mutable public Size : uint64

        new(device, handle, buffer, fmt, offset, size) = { inherit Resource<_>(device, handle); Buffer = buffer; Format = fmt; Offset = offset; Size = size }
    end


// =======================================================================
// Command Extensions
// =======================================================================
[<AutoOpen>]
module BufferCommands =
    type Command with
        
        // ptr to buffer
        static member Copy(src : DevicePtr, srcOffset : int64, dst : Buffer, dstOffset : int64, size : int64) =
            if size < 0L || srcOffset < 0L || srcOffset + size > src.Size || dstOffset < 0L || dstOffset + size > dst.Size then
                failf "bad copy range"

            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue cmd =
                    let device = src.Memory.Heap.Device
                    let align = device.MinUniformBufferOffsetAlignment

                    let srcOffset = src.Offset + srcOffset
                    let srcBufferOffset = Alignment.prev align srcOffset
                    let srcCopyOffset = srcOffset - srcBufferOffset
                    let srcBufferSize = size + srcCopyOffset

                    let mutable srcInfo =
                        VkBufferCreateInfo(
                            VkStructureType.BufferCreateInfo, 0n,
                            VkBufferCreateFlags.None,
                            uint64 srcBufferSize, VkBufferUsageFlags.TransferSrcBit, VkSharingMode.Exclusive, 
                            0u, NativePtr.zero
                        )
                        
                    let mutable srcBuffer =
                        let mutable srcBuffer = VkBuffer.Null
                        VkRaw.vkCreateBuffer(device.Handle, &&srcInfo, NativePtr.zero, &&srcBuffer)
                            |> check "could not create temporary buffer"
                        srcBuffer

                    let mutable reqs = VkMemoryRequirements()
                    VkRaw.vkGetBufferMemoryRequirements(device.Handle, srcBuffer, &&reqs)

                    if srcBufferOffset % (int64 reqs.alignment) <> 0L then
                        VkRaw.warn "bad buffer alignment"

                    VkRaw.vkBindBufferMemory(device.Handle, srcBuffer, src.Memory.Handle, uint64 srcBufferOffset)
                        |> check "could not bind temporary buffer memory"

                    let mutable copyInfo = VkBufferCopy(uint64 srcCopyOffset, uint64 dstOffset, uint64 size)
                    cmd.AppendCommand()
                    VkRaw.vkCmdCopyBuffer(cmd.Handle, srcBuffer, dst.Handle, 1u, &&copyInfo)

                    { new Disposable() with
                        member x.Dispose() =
                            if srcBuffer.IsValid then VkRaw.vkDestroyBuffer(device.Handle, srcBuffer, NativePtr.zero)
                    }
            }

        // buffer to ptr
        static member Copy(src : Buffer, srcOffset : int64, dst : DevicePtr, dstOffset : int64, size : int64) =
            if size < 0L || srcOffset < 0L || srcOffset + size > src.Size || dstOffset < 0L || dstOffset + size > dst.Size then
                failf "bad copy range"

            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue cmd =
                    let mutable dstBuffer = VkBuffer.Null
                    let device = src.Device
                    let align = device.MinUniformBufferOffsetAlignment

                    let dstOffset = dst.Offset + dstOffset
                    let dstBufferOffset = Alignment.prev align dstOffset
                    let dstCopyOffset = dstOffset - dstBufferOffset
                    let dstBufferSize = size + dstCopyOffset


                    let mutable dstInfo =
                        VkBufferCreateInfo(
                            VkStructureType.BufferCreateInfo, 0n,
                            VkBufferCreateFlags.None,
                            uint64 dstBufferSize, VkBufferUsageFlags.TransferDstBit, VkSharingMode.Exclusive, 
                            0u, NativePtr.zero
                    )

                    VkRaw.vkCreateBuffer(device.Handle, &&dstInfo, NativePtr.zero, &&dstBuffer)
                        |> check "could not create temporary buffer"

                    VkRaw.vkBindBufferMemory(device.Handle, dstBuffer, dst.Memory.Handle, uint64 dstBufferOffset)
                        |> check "could not bind temporary buffer memory"


                    let mutable copyInfo = VkBufferCopy(uint64 srcOffset, uint64 dstCopyOffset, uint64 size)
                    cmd.AppendCommand()
                    VkRaw.vkCmdCopyBuffer(cmd.Handle, src.Handle, dstBuffer, 1u, &&copyInfo)

                    { new Disposable() with
                        member x.Dispose() = 
                            if dstBuffer.IsValid then VkRaw.vkDestroyBuffer(device.Handle, dstBuffer, NativePtr.zero)
                    }
            }

        // buffer to buffer
        static member Copy(src : Buffer, srcOffset : int64, dst : Buffer, dstOffset : int64, size : int64) =
            if size < 0L || srcOffset < 0L || srcOffset + size > src.Size || dstOffset < 0L || dstOffset + size > dst.Size then
                failf "bad copy range"

            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue cmd =
                    let mutable copyInfo = VkBufferCopy(uint64 srcOffset, uint64 dstOffset, uint64 size)
                    cmd.AppendCommand()
                    VkRaw.vkCmdCopyBuffer(cmd.Handle, src.Handle, dst.Handle, 1u, &&copyInfo)
                    Disposable.Empty
            }

        static member Copy(src : Buffer, dst : Buffer, ranges : Range1l[]) =
            if ranges.Length = 0 then
                Command.Nop
            else
                { new Command() with
                    member x.Compatible = QueueFlags.All
                    member x.Enqueue cmd =
                        let pCopyInfos = NativePtr.stackalloc ranges.Length
                        let mutable current = NativePtr.toNativeInt pCopyInfos
                        for r in ranges do
                            let ci = VkBufferCopy(uint64 r.Min, uint64 r.Min, uint64 (1L + r.Max - r.Min))
                            NativeInt.write current ci
                            current <- current + nativeint sizeof<VkBufferCopy>

                        cmd.AppendCommand()
                        VkRaw.vkCmdCopyBuffer(cmd.Handle, src.Handle, dst.Handle, uint32 ranges.Length, pCopyInfos)
                        Disposable.Empty
                }

        static member inline Copy(src : DevicePtr, dst : Buffer, size : int64) = 
            Command.Copy(src, 0L, dst, 0L, size)

        static member inline Copy(src : Buffer, dst : DevicePtr, size : int64) = 
            Command.Copy(src, 0L, dst, 0L, size)

        static member inline Copy(src : Buffer, dst : Buffer, size : int64) = 
            Command.Copy(src, 0L, dst, 0L, size)

        static member inline Copy(src : DevicePtr, dst : Buffer) = 
            Command.Copy(src, 0L, dst, 0L, min src.Size dst.Size)

        static member inline Copy(src : Buffer, dst : DevicePtr) = 
            Command.Copy(src, 0L, dst, 0L, min src.Size dst.Size)

        static member inline Copy(src : Buffer, dst : Buffer) = 
            Command.Copy(src, 0L, dst, 0L, min src.Size dst.Size)

        static member Sync(b : Buffer, src : VkAccessFlags, dst : VkAccessFlags) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue cmd =
                    let mutable barrier =
                        VkBufferMemoryBarrier(
                            VkStructureType.BufferMemoryBarrier, 0n,
                            src,
                            dst,
                            VK_QUEUE_FAMILY_IGNORED, VK_QUEUE_FAMILY_IGNORED,
                            b.Handle,
                            0UL,
                            uint64 b.Size
                        )

                    let srcStage =
                        match src with
                            | VkAccessFlags.HostReadBit -> VkPipelineStageFlags.HostBit
                            | VkAccessFlags.HostWriteBit -> VkPipelineStageFlags.HostBit
                            | VkAccessFlags.IndexReadBit -> VkPipelineStageFlags.VertexInputBit
                            | VkAccessFlags.IndirectCommandReadBit -> VkPipelineStageFlags.DrawIndirectBit
                            | VkAccessFlags.ShaderReadBit -> VkPipelineStageFlags.ComputeShaderBit
                            | VkAccessFlags.ShaderWriteBit -> VkPipelineStageFlags.ComputeShaderBit
                            | VkAccessFlags.TransferReadBit -> VkPipelineStageFlags.TransferBit
                            | VkAccessFlags.TransferWriteBit -> VkPipelineStageFlags.TransferBit
                            | VkAccessFlags.UniformReadBit -> VkPipelineStageFlags.ComputeShaderBit
                            | VkAccessFlags.VertexAttributeReadBit -> VkPipelineStageFlags.VertexInputBit
                            | _ -> VkPipelineStageFlags.None
                            
                    let dstStage =
                        match dst with
                            | VkAccessFlags.HostReadBit -> VkPipelineStageFlags.HostBit
                            | VkAccessFlags.HostWriteBit -> VkPipelineStageFlags.HostBit
                            | VkAccessFlags.IndexReadBit -> VkPipelineStageFlags.VertexInputBit
                            | VkAccessFlags.IndirectCommandReadBit -> VkPipelineStageFlags.DrawIndirectBit
                            | VkAccessFlags.ShaderReadBit -> VkPipelineStageFlags.VertexShaderBit
                            | VkAccessFlags.ShaderWriteBit -> VkPipelineStageFlags.VertexShaderBit
                            | VkAccessFlags.TransferReadBit -> VkPipelineStageFlags.TransferBit
                            | VkAccessFlags.TransferWriteBit -> VkPipelineStageFlags.TransferBit
                            | VkAccessFlags.UniformReadBit -> VkPipelineStageFlags.VertexShaderBit
                            | VkAccessFlags.VertexAttributeReadBit -> VkPipelineStageFlags.VertexInputBit
                            | _ -> VkPipelineStageFlags.None

                    VkRaw.vkCmdPipelineBarrier(
                        cmd.Handle, 
                        srcStage, 
                        dstStage,
                        VkDependencyFlags.None,
                        0u, NativePtr.zero,
                        1u, &&barrier,
                        0u, NativePtr.zero
                    )

                    Disposable.Empty
            }

        static member SyncWrite(b : Buffer) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue cmd =
                    let mutable barrier =
                        VkBufferMemoryBarrier(
                            VkStructureType.BufferMemoryBarrier, 0n,
                            VkAccessFlags.TransferWriteBit ||| VkAccessFlags.HostWriteBit ||| VkAccessFlags.MemoryWriteBit ||| VkAccessFlags.ShaderWriteBit,
                            VkAccessFlags.TransferReadBit ||| VkAccessFlags.ShaderReadBit ||| VkAccessFlags.IndexReadBit ||| VkAccessFlags.VertexAttributeReadBit ||| VkAccessFlags.UniformReadBit,
                            VK_QUEUE_FAMILY_IGNORED, VK_QUEUE_FAMILY_IGNORED,
                            b.Handle,
                            0UL,
                            uint64 b.Size
                        )

                    VkRaw.vkCmdPipelineBarrier(
                        cmd.Handle, 
                        VkPipelineStageFlags.TopOfPipeBit, 
                        VkPipelineStageFlags.BottomOfPipeBit,
                        VkDependencyFlags.None,
                        0u, NativePtr.zero,
                        1u, &&barrier,
                        0u, NativePtr.zero
                    )

                    Disposable.Empty
            }

        static member ZeroBuffer(b : Buffer) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue cmd =
                    VkRaw.vkCmdFillBuffer(cmd.Handle, b.Handle, 0UL, uint64 b.Size, 0u)
                    Disposable.Empty
            }
// =======================================================================
// Resource functions for Device
// =======================================================================
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Buffer =
    [<AutoOpen>]
    module private Helpers = 
        let memoryTypes (d : Device) (bits : uint32) =
            let mutable mask = 1u
            d.Memories 
            |> Seq.filter (fun m ->
                let mask = 1u <<< m.Info.index
                bits &&& mask <> 0u
               )
            |> Seq.toList

    let private emptyBuffers = ConcurrentDictionary<Device * VkBufferUsageFlags, Buffer>()

    let empty (usage : VkBufferUsageFlags) (device : Device) =
        let key = (device, usage)
        emptyBuffers.GetOrAdd(key, fun (device, usage) ->
            let mutable info =
                VkBufferCreateInfo(
                    VkStructureType.BufferCreateInfo, 0n,
                    VkBufferCreateFlags.None,
                    256UL,
                    usage,
                    device.AllSharingMode,
                    device.AllQueueFamiliesCnt,
                    device.AllQueueFamiliesPtr
                )

            let mutable handle = VkBuffer.Null
            VkRaw.vkCreateBuffer(device.Handle, &&info, NativePtr.zero, &&handle)
                |> check "could not create empty buffer"

            let mutable reqs = VkMemoryRequirements()
            VkRaw.vkGetBufferMemoryRequirements(device.Handle, handle, &&reqs)

            let ptr = device.GetMemory(reqs.memoryTypeBits).Null
//            VkRaw.vkBindBufferMemory(device.Handle, handle, ptr.Memory.Handle, 0UL)
//                |> check "could not bind empty buffer's memory"

            device.OnDispose.Add (fun () ->
                VkRaw.vkDestroyBuffer(device.Handle, handle, NativePtr.zero)
                emptyBuffers.TryRemove(key) |> ignore
            )   

            Buffer(device, handle, ptr, 256L)
        )


    let createConcurrent (conc : bool) (flags : VkBufferUsageFlags) (size : int64) (memory : DeviceHeap) =
        let device = memory.Device
        let mutable info =
            VkBufferCreateInfo(
                VkStructureType.BufferCreateInfo, 0n,
                VkBufferCreateFlags.None,
                uint64 size, 
                flags,
                (if conc then device.AllSharingMode else VkSharingMode.Exclusive),
                (if conc then device.AllQueueFamiliesCnt else 0u), 
                (if conc then device.AllQueueFamiliesPtr else NativePtr.zero)
            )

        let mutable handle = VkBuffer.Null
        VkRaw.vkCreateBuffer(device.Handle, &&info, NativePtr.zero, &&handle)
            |> check "could not create buffer"

        let mutable reqs = VkMemoryRequirements()
        VkRaw.vkGetBufferMemoryRequirements(device.Handle, handle, &&reqs)

        if reqs.memoryTypeBits &&& (1u <<< memory.Index) = 0u then
            failf "cannot create buffer using memory %A" memory

        let ptr = memory.Alloc(int64 reqs.alignment, int64 reqs.size)

        VkRaw.vkBindBufferMemory(device.Handle, handle, ptr.Memory.Handle, uint64 ptr.Offset)
            |> check "could not bind buffer-memory"


        Buffer(device, handle, ptr, size)

    let inline create  (flags : VkBufferUsageFlags) (size : int64) (memory : DeviceHeap) =
        createConcurrent false flags size memory

    let inline allocConcurrent (conc : bool) (flags : VkBufferUsageFlags) (size : int64) (device : Device) =
        createConcurrent conc flags size device.DeviceMemory

    let inline alloc (flags : VkBufferUsageFlags) (size : int64) (device : Device) =
        allocConcurrent false flags size device

    let internal ofWriter (flags : VkBufferUsageFlags) (size : nativeint) (writer : nativeint -> unit) (device : Device) =
        if size > 0n then
            let align = int64 device.MinUniformBufferOffsetAlignment

            let deviceAlignedSize = Alignment.next align (int64 size)
            let buffer = device |> alloc flags deviceAlignedSize
            let deviceMem = buffer.Memory
        
            let hostPtr = device.HostMemory.Alloc(align, deviceAlignedSize)
            hostPtr.Mapped (fun dst -> writer dst)

            device.eventually {
                try do! Command.Copy(hostPtr, 0L, buffer, 0L, int64 size)
                finally hostPtr.Dispose()
            }

            buffer
        else
            empty flags device

    let internal updateWriter (writer : nativeint -> unit) (buffer : Buffer) =
        let device = buffer.Device
        let align = int64 device.MinUniformBufferOffsetAlignment

        let deviceAlignedSize = Alignment.next align (int64 buffer.Size)
        let deviceMem = buffer.Memory
        
        let hostPtr = device.HostMemory.Alloc(align, deviceAlignedSize)
        hostPtr.Mapped (fun dst -> writer dst)

        device.eventually {
            try do! Command.Copy(hostPtr, 0L, buffer, 0L, buffer.Size)
            finally hostPtr.Dispose()
        }

    let delete (buffer : Buffer) (device : Device) =
        if Interlocked.Decrement(&buffer.RefCount) = 0 then
            if buffer.Handle.IsValid && buffer.Size > 0L then
                VkRaw.vkDestroyBuffer(device.Handle, buffer.Handle, NativePtr.zero)
                buffer.Handle <- VkBuffer.Null
                buffer.Memory.Dispose()

    let tryUpdate (data : IBuffer) (buffer : Buffer) =
        match data with 
            | :? ArrayBuffer as ab ->
                let size = ab.Data.LongLength * int64 (Marshal.SizeOf ab.ElementType)
                if size = buffer.Size then
                    let gc = GCHandle.Alloc(ab.Data, GCHandleType.Pinned)
                    buffer |> updateWriter (fun ptr -> Marshal.Copy(gc.AddrOfPinnedObject(), ptr, size) )
                    true
                else
                    false
            | :? INativeBuffer as nb ->
                let size = nb.SizeInBytes |> int64
                if size = buffer.Size then
                    nb.Use(fun src ->
                        buffer |> updateWriter (fun dst -> Marshal.Copy(src, dst, size))
                    )
                    true
                else
                    false
            | _ ->
                false

    let ofBuffer (flags : VkBufferUsageFlags) (buffer : IBuffer) (device : Device) =
        match buffer with
            | :? ArrayBuffer as ab ->
                if ab.Data.Length <> 0 then
                    let size = nativeint ab.Data.LongLength * nativeint (Marshal.SizeOf ab.ElementType)
                    let gc = GCHandle.Alloc(ab.Data, GCHandleType.Pinned)
                    try device |> ofWriter flags size (fun dst -> Marshal.Copy(gc.AddrOfPinnedObject(), dst, size))
                    finally gc.Free()
                else
                    device |> empty flags

            | :? INativeBuffer as nb ->
                if nb.SizeInBytes <> 0 then
                    let size = nb.SizeInBytes |> nativeint
                    nb.Use(fun src ->
                        device |> ofWriter flags size (fun dst -> Marshal.Copy(src, dst, size))
                    )
                else
                    device |> empty flags
                    

            | :? Buffer as b ->
                b.AddReference()
                b

            | _ ->
                failf "unsupported buffer type %A" buffer


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module BufferView =
    let create (fmt : VkFormat) (b : Buffer) (offset : uint64) (size : uint64) (device : Device) =
        if b.Size = 0L then
            BufferView(device, VkBufferView.Null, b, fmt, offset, size)
        else
            let mutable info = 
                VkBufferViewCreateInfo(
                    VkStructureType.BufferViewCreateInfo, 0n,
                    VkBufferViewCreateFlags.MinValue,
                    b.Handle, 
                    fmt,
                    offset,
                    size
                )

            let mutable handle = VkBufferView.Null
            VkRaw.vkCreateBufferView(device.Handle, &&info, NativePtr.zero, &&handle)
                |> check "could not create BufferView"

            BufferView(device, handle, b, fmt, offset, size)

    let delete (view : BufferView) (device : Device) =
        if view.Handle.IsValid then
            VkRaw.vkDestroyBufferView(device.Handle, view.Handle, NativePtr.zero)
            view.Handle <- VkBufferView.Null


// =======================================================================
// Device Extensions
// =======================================================================
[<AbstractClass; Sealed; Extension>]
type ContextBufferExtensions private() =

    [<Extension>]
    static member inline CreateBuffer(device : Device, flags : VkBufferUsageFlags, size : int64) =
        device |> Buffer.alloc flags size

    [<Extension>]
    static member inline Delete(device : Device, buffer : Buffer) =
        device |> Buffer.delete buffer

    [<Extension>]
    static member inline CreateBuffer(device : Device, flags : VkBufferUsageFlags, b : IBuffer) =
        device |> Buffer.ofBuffer flags b
        
    [<Extension>]
    static member inline TryUpdate(buffer : Buffer, b : IBuffer) =
        buffer |> Buffer.tryUpdate b

    [<Extension>]
    static member inline CreateBufferView(device : Device, buffer : Buffer, format : VkFormat, offset : int64, size : int64) =
        device |> BufferView.create format buffer (uint64 offset) (uint64 size)

    [<Extension>]
    static member inline Delete(device : Device, view : BufferView) =
        device |> BufferView.delete view

[<AutoOpen>]
module ``Buffer Format Extensions`` = 
    module VkFormat =
        let ofType =
            LookupTable.lookupTable [
                typeof<float32>, VkFormat.R32Sfloat
                typeof<V2f>, VkFormat.R32g32Sfloat
                typeof<V3f>, VkFormat.R32g32b32Sfloat
                typeof<V4f>, VkFormat.R32g32b32a32Sfloat

                typeof<int>, VkFormat.R32Sint
                typeof<V2i>, VkFormat.R32g32Sint
                typeof<V3i>, VkFormat.R32g32b32Sint
                typeof<V4i>, VkFormat.R32g32b32a32Sint

                typeof<uint32>, VkFormat.R32Uint
                typeof<uint16>, VkFormat.R16Uint
                typeof<uint8>, VkFormat.R8Uint
                typeof<C4b>, VkFormat.B8g8r8a8Unorm
                typeof<C4us>, VkFormat.R16g16b16a16Unorm
                typeof<C4ui>, VkFormat.R32g32b32a32Uint
                typeof<C4f>, VkFormat.R32g32b32a32Sfloat
            ]
