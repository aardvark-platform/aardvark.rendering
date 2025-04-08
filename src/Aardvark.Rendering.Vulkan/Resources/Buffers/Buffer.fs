namespace Aardvark.Rendering.Vulkan

open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Collections.Concurrent
open Aardvark.Base
open Aardvark.Rendering
open Microsoft.FSharp.NativeInterop

open Vulkan11
open KHRBufferDeviceAddress

#nowarn "9"
#nowarn "51"

// =======================================================================
// Resource Definition
// =======================================================================
type Buffer =
    class
        inherit Resource<VkBuffer>
        val public Memory : DevicePtr
        val public Usage : VkBufferUsageFlags
        val public Size : uint64
        val public DeviceAddress : VkDeviceAddress

        override x.Destroy() =
            if x.Handle.IsValid then
                VkRaw.vkDestroyBuffer(x.Device.Handle, x.Handle, NativePtr.zero)
                x.Handle <- VkBuffer.Null
                x.Memory.Dispose()

        interface IBackendBuffer with
            member x.Runtime = x.Device.Runtime :> IBufferRuntime
            member x.Handle = x.Handle :> obj
            member x.Buffer = x
            member x.Offset = 0n
            member x.SizeInBytes = nativeint x.Size // NOTE: return size as specified by user. memory might have larger size as it is an aligned block

        new(device: Device, handle: VkBuffer, memory: DevicePtr, size, usage: VkBufferUsageFlags) =
            let address =
                if usage.HasFlag VkBufferUsageFlags.ShaderDeviceAddressBitKhr then
                    let mutable info = VkBufferDeviceAddressInfoKHR(handle)
                    VkRaw.vkGetBufferDeviceAddressKHR(device.Handle, &&info)
                else
                    0UL

            { inherit Resource<_>(device, handle); Memory = memory; Size = size; Usage = usage; DeviceAddress = address }
    end

type internal ExportedBuffer =
    class
        inherit Buffer
        val public ExternalMemory : ExternalMemory

        interface IExportedBackendBuffer with
            member x.Memory = x.ExternalMemory

        new(device, handle, memory: DevicePtr, size, usage) =
            let externalMemory =
                { Block  = memory.ExternalBlock
                  Offset = int64 memory.Offset
                  Size   = int64 memory.Size }

            { inherit Buffer(device, handle, memory, size, usage); ExternalMemory = externalMemory }
    end

type BufferView =
    class
        inherit Resource<VkBufferView>
        val public Buffer : Buffer
        val public Format : VkFormat
        val public Offset : uint64
        val public Size : uint64

        override x.Destroy() =
            if x.Handle.IsValid then
                VkRaw.vkDestroyBufferView(x.Device.Handle, x.Handle, NativePtr.zero)
                x.Handle <- VkBufferView.Null

        new(device, handle, buffer, fmt, offset, size) =
            { inherit Resource<_>(device, handle); Buffer = buffer; Format = fmt; Offset = offset; Size = size }
    end

[<AbstractClass>]
type BufferDecorator =
    class
        inherit Buffer
        val private Parent : Buffer

        override x.Destroy() =
            x.Parent.Dispose()

        new (parent : Buffer) =
            { inherit Buffer(parent.Device, parent.Handle, parent.Memory, parent.Size, parent.Usage);
              Parent = parent }
    end

// =======================================================================
// Command Extensions
// =======================================================================
[<AutoOpen>]
module BufferCommands =

    type CommandBuffer with

        member internal cmd.BufferBarrier(buffer : Buffer,
                                          srcStage : VkPipelineStageFlags, srcAccess : VkAccessFlags,
                                          dstStage : VkPipelineStageFlags, dstAccess : VkAccessFlags,
                                          srcQueue : uint32, dstQueue : uint32,
                                          offset : uint64, size : uint64)  =

            let srcStage, srcAccess = (srcStage, srcAccess) ||> filterSrcStageAndAccess cmd.QueueFamily.Stages
            let dstStage, dstAccess = (dstStage, dstAccess) ||> filterDstStageAndAccess cmd.QueueFamily.Stages

            let barrier =
                VkBufferMemoryBarrier(
                    srcAccess, dstAccess,
                    srcQueue, dstQueue,
                    buffer.Handle, offset, size
                )

            barrier |> NativePtr.pin (fun pBarrier ->
                VkRaw.vkCmdPipelineBarrier(
                    cmd.Handle,
                    srcStage, dstStage,
                    VkDependencyFlags.None,
                    0u, NativePtr.zero,
                    1u, pBarrier,
                    0u, NativePtr.zero
                )
            )

        member internal cmd.BufferBarrier(buffer : Buffer,
                                          srcStage : VkPipelineStageFlags, srcAccess : VkAccessFlags,
                                          dstStage : VkPipelineStageFlags, dstAccess : VkAccessFlags,
                                          srcQueue : uint32, dstQueue : uint32)  =
            cmd.BufferBarrier(buffer, srcStage, srcAccess, dstStage, dstAccess, srcQueue, dstQueue, 0UL, uint64 buffer.Size)

    type Command with

        // buffer to buffer
        static member Copy(src : Buffer, srcOffset : uint64, dst : Buffer, dstOffset : uint64, size : uint64) =
            if size < 0UL || srcOffset < 0UL || srcOffset + size > src.Size || dstOffset < 0UL || dstOffset + size > dst.Size then
                failf "bad copy range"

            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue cmd =
                    let copyInfo = VkBufferCopy(uint64 srcOffset, uint64 dstOffset, uint64 size)
                    cmd.AppendCommand()
                    copyInfo |> NativePtr.pin (fun pInfo -> VkRaw.vkCmdCopyBuffer(cmd.Handle, src.Handle, dst.Handle, 1u, pInfo))
                    cmd.AddResource src
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
                        cmd.AddResource src
                        cmd.AddResource dst
                }



        static member inline Copy(src : Buffer, dst : Buffer, size : uint64) =
            Command.Copy(src, 0UL, dst, 0UL, size)


        static member inline Copy(src : Buffer, dst : Buffer) = 
            Command.Copy(src, 0UL, dst, 0UL, min src.Size dst.Size)

        static member Acquire(buffer : Buffer, srcQueue : DeviceQueueFamily, offset : uint64, size : uint64) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue cmd =
                    cmd.AppendCommand()

                    cmd.BufferBarrier(
                        buffer,
                        VkPipelineStageFlags.TopOfPipeBit, VkAccessFlags.None,
                        VkBufferUsageFlags.toDstStageFlags buffer.Usage,
                        VkBufferUsageFlags.toDstAccessFlags buffer.Usage,
                        uint32 srcQueue.Index,
                        uint32 cmd.QueueFamily.Index,
                        offset, size
                    )

                    cmd.AddResource buffer
            }

        static member Acquire(buffer : Buffer, srcQueue : DeviceQueueFamily) =
            Command.Acquire(buffer, srcQueue, 0UL, buffer.Size)


        static member Sync(buffer : Buffer,
                           srcStage : VkPipelineStageFlags, srcAccess : VkAccessFlags,
                           dstStage : VkPipelineStageFlags, dstAccess : VkAccessFlags) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue cmd =
                    cmd.AppendCommand()

                    cmd.BufferBarrier(
                        buffer,
                        srcStage, srcAccess,
                        dstStage, dstAccess,
                        VkQueueFamilyIgnored, VkQueueFamilyIgnored
                    )

                    cmd.AddResource buffer
            }

        static member Sync(buffer : Buffer, srcStage : VkPipelineStageFlags, srcAccess : VkAccessFlags) =
            Command.Sync(
                buffer, srcStage, srcAccess,
                VkBufferUsageFlags.toDstStageFlags buffer.Usage,
                VkBufferUsageFlags.toDstAccessFlags buffer.Usage
            )

        static member Sync(buffer : Buffer, srcAccess : VkAccessFlags, dstAccess : VkAccessFlags) =
            Command.Sync(
                buffer,
                VkBufferUsageFlags.toSrcStageFlags buffer.Usage, srcAccess,
                VkBufferUsageFlags.toDstStageFlags buffer.Usage, dstAccess
            )

        static member ZeroBuffer(b : Buffer) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue cmd =
                    cmd.AppendCommand()
                    VkRaw.vkCmdFillBuffer(cmd.Handle, b.Handle, 0UL, uint64 b.Size, 0u)
                    cmd.AddResource b
            }
        static member SetBuffer(b : Buffer, offset : uint64, size : uint64, value : byte[]) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue cmd =
                    cmd.AppendCommand()
                    if value.Length <> 4 then failf "pattern too long"
                    let v = BitConverter.ToUInt32(value, 0)
                    VkRaw.vkCmdFillBuffer(cmd.Handle, b.Handle, offset, size, v)
                    cmd.AddResource b
            }

    type CopyCommand with
        static member Copy(src : Buffer, srcOffset : uint64, dst : Buffer, dstOffset : uint64, size : uint64) =
            CopyCommand.Copy(src.Handle, srcOffset, dst.Handle, dstOffset, size)

        static member Copy(src : Buffer, dst : Buffer, size : uint64) =
            CopyCommand.Copy(src.Handle, 0UL, dst.Handle, 0UL, size)

        static member Copy(src : Buffer, dst : Buffer) =
            CopyCommand.Copy(src.Handle, 0UL, dst.Handle, 0UL, min src.Size dst.Size)

        static member Release(buffer : Buffer, offset : uint64, size : uint64, dstQueueFamily : DeviceQueueFamily) =
            CopyCommand.Release(buffer.Handle, offset, size, dstQueueFamily.Index)

        static member Release(buffer : Buffer, dstQueueFamily : DeviceQueueFamily) =
            CopyCommand.Release(buffer, 0UL, buffer.Size, dstQueueFamily)

// =======================================================================
// Resource functions for Device
// =======================================================================
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Buffer =

    let private emptySize = 256UL

    let private createInternal (concurrent: bool) (export: bool) (usage: VkBufferUsageFlags)
                               (alignment: uint64) (size: uint64) (memory: IDeviceMemory) =
        let device = memory.Device

        let externalMemoryInfo =
            VkExternalMemoryBufferCreateInfo VkExternalMemoryHandleTypeFlags.OpaqueBit

        use pExternalMemoryInfo = new VkStructChain()
        pExternalMemoryInfo.Add externalMemoryInfo |> ignore

        let pNext =
            if export then
                if device.IsExtensionEnabled Instance.Extensions.ExternalMemory then
                    if device.PhysicalDevice.GetBufferExportable(VkBufferCreateFlags.None, usage) then
                        pExternalMemoryInfo.Handle
                    else
                        failf $"Cannot export buffer with usage {usage}"
                else
                    failf $"Cannot export buffer memory because {Instance.Extensions.ExternalMemory} is not supported"
            else
                0n

        let mutable info =
            VkBufferCreateInfo(
                pNext,
                VkBufferCreateFlags.None,
                (if size > 0UL then size else emptySize),
                usage,
                (if concurrent then device.SharingMode else VkSharingMode.Exclusive),
                (if concurrent then device.QueueFamilyCount else 0u),
                (if concurrent then device.QueueFamilyIndices else NativePtr.zero)
            )

        let struct (buffer, memory) = memory.CreateBuffer(&info, alignment = alignment, export = export)

        if export then
            new ExportedBuffer(device, buffer, memory, size, usage) :> Buffer
        else
            new Buffer(device, buffer, memory, size, usage)

    let private emptyBuffers = ConcurrentDictionary<_, Lazy<Buffer>>()

    let empty (export: bool) (usage: VkBufferUsageFlags) (memory: IDeviceMemory) =
        let key = (memory, export, usage)

        let buffer =
            emptyBuffers.GetOrAdd(key, fun (memory, export, usage) ->
                lazy (
                    let device = memory.Device
                    let buffer = memory |> createInternal true export usage 0UL emptySize

                    device.OnDispose.Add (fun () ->
                        emptyBuffers.TryRemove key |> ignore
                        buffer.Dispose()
                    )

                    buffer
                )
            ).Value

        buffer.AddReference()
        buffer

    let create' (concurrent: bool) (export: bool) (usage: VkBufferUsageFlags) (alignment: uint64) (size: uint64) (memory: IDeviceMemory) =
        if size > 0UL then
            memory |> createInternal concurrent export usage alignment size
        else
            memory |> empty export usage

    let inline create (usage: VkBufferUsageFlags) (size: uint64) (memory: IDeviceMemory) =
        create' false false usage 0UL size memory

    let internal write (buffer: Buffer) (writer : nativeint -> unit) =
        let device = buffer.Device

        if buffer.Size > 0UL then
            if buffer.Memory.IsHostVisible then
                buffer.Memory.Mapped writer

            else
                match device.UploadMode with
                | UploadMode.Sync ->
                    let hostBuffer = device.StagingMemory |> create VkBufferUsageFlags.TransferSrcBit buffer.Size
                    hostBuffer.Memory.Mapped writer

                    device.eventually {
                        try do! Command.Copy(hostBuffer, buffer)
                        finally hostBuffer.Dispose()
                    }

                | UploadMode.Async ->
                    let hostBuffer = device.StagingMemory |> create VkBufferUsageFlags.TransferSrcBit buffer.Size
                    hostBuffer.Memory.Mapped writer

                    device.CopyEngine.RunSynchronously [
                        CopyCommand.Copy(hostBuffer, buffer, buffer.Size)
                        CopyCommand.Release(buffer, device.GraphicsFamily)
                        CopyCommand.Callback (fun () -> hostBuffer.Dispose())
                    ]

                    device.eventually {
                        do! Command.Acquire(buffer, device.TransferFamily)
                    }

    let internal copyFromHost (buffer: Buffer) (src: nativeint) =
        write buffer (fun dst -> Buffer.MemoryCopy(src, dst, buffer.Size, buffer.Size))

    let rec tryUpdate (data: IBuffer) (buffer: Buffer)  =
        match data with
        | :? Buffer as b ->
            buffer.Handle = b.Handle

        | :? ArrayBuffer as ab ->
            if buffer.Size = uint64 ab.Data.LongLength * uint64 (ab.ElementType.GetCLRSize()) then
                ab.Data |> NativeInt.pin (copyFromHost buffer)
                true
            else
                false

        | :? INativeBuffer as nb ->
            if buffer.Size = uint64 nb.SizeInBytes then
                nb.Use (copyFromHost buffer)
                true
            else
                false

        | :? IBufferRange as bv when bv != bv.Buffer ->
            tryUpdate bv.Buffer buffer

        | _ ->
            false

    let rec ofBuffer' (export: bool) (usage: VkBufferUsageFlags) (alignment: uint64) (buffer: IBuffer) (memory: IDeviceMemory) =
        match buffer with
        | :? ArrayBuffer as ab ->
            if ab.Data.Length <> 0 then
                let size = uint64 ab.Data.LongLength * uint64 (ab.ElementType.GetCLRSize())
                let buffer = create' false export usage alignment size memory
                ab.Data |> NativeInt.pin (copyFromHost buffer)
                buffer
            else
                memory |> empty export usage

        | :? INativeBuffer as nb ->
            if nb.SizeInBytes <> 0n then
                let size = uint64 nb.SizeInBytes
                let buffer = create' false export usage alignment size memory
                nb.Use (copyFromHost buffer)
                buffer
            else
                memory |> empty export usage

        | :? ExportedBuffer when export ->
            ofBuffer' false usage alignment buffer memory

        | :? Buffer as b ->
            if export then
                failf "cannot export buffer after it has been created"

            if alignment <> 0UL && b.DeviceAddress % alignment <> 0UL then
                failf $"cannot use prepared buffer as it is misaligned (address = {b.DeviceAddress}, required alignment = {alignment})"

            b.AddReference()
            b

        | :? IBufferRange as bv when bv <> bv.Buffer ->
            ofBuffer' export usage alignment bv.Buffer memory

        | _ when buffer = Unchecked.defaultof<_> ->
            failf $"buffer data is null"

        | _ ->
            failf $"unsupported buffer type: {buffer.GetType()}"

    let ofBuffer (usage: VkBufferUsageFlags) (buffer: IBuffer) (memory: IDeviceMemory) =
        ofBuffer' false usage 0UL buffer memory

    let inline upload (src: nativeint) (dst: Buffer) (dstOffset: uint64) (sizeInBytes: uint64)  =
        if sizeInBytes > 0UL then
            let device = dst.Device

            if dst.Memory.IsHostVisible then
                dst.Memory.CopyFrom(dstOffset, sizeInBytes, src)
            else
                use temp = device.StagingMemory |> create VkBufferUsageFlags.TransferSrcBit sizeInBytes

                temp.Memory.CopyFrom(sizeInBytes, src)
                device.perform {
                    do! Command.Copy(temp, 0UL, dst, dstOffset, sizeInBytes)
                }

    let inline download (src: Buffer) (srcOffset: uint64) (dst: nativeint) (sizeInBytes: uint64) =
        if sizeInBytes > 0UL then
            let device = src.Device

            if src.Memory.IsHostVisible then
                src.Memory.CopyTo(srcOffset, sizeInBytes, dst)
            else
                use temp = device.ReadbackMemory |> create VkBufferUsageFlags.TransferDstBit sizeInBytes

                device.perform {
                    do! Command.Copy(src, srcOffset, temp, 0UL, sizeInBytes)
                }
                temp.Memory.CopyTo(sizeInBytes, dst)

    let inline downloadAsync (src: Buffer) (srcOffset: uint64) (dst: nativeint) (sizeInBytes: uint64)  =
        if sizeInBytes > 0UL then
            let device = src.Device

            if src.Memory.IsHostVisible then
                (fun () ->
                    src.Memory.CopyTo(srcOffset, sizeInBytes, dst)
                )
            else
                let temp = device.ReadbackMemory |> create VkBufferUsageFlags.TransferDstBit sizeInBytes
                let task = device.GraphicsFamily.StartTask(Command.Copy(src, srcOffset, temp, 0UL, sizeInBytes))

                (fun () ->
                    task.Wait()
                    temp.Memory.CopyTo(sizeInBytes, dst)
                    temp.Dispose()
                )
        else
            ignore

    let inline copy (src: Buffer) (srcOffset: uint64) (dst: Buffer) (dstOffset: uint64) (sizeInBytes: uint64) =
        if sizeInBytes > 0UL then
            src.Device.perform {
                do! Command.Copy(src, srcOffset, dst, dstOffset, sizeInBytes)
            }


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module BufferView =
    let create (format: VkFormat) (buffer: Buffer) (offset: uint64) (size: uint64) (device : Device) =
        if buffer.Size = 0UL then
            new BufferView(device, VkBufferView.Null, buffer, format, offset, size)
        else
            let mutable info =
                VkBufferViewCreateInfo(
                    VkBufferViewCreateFlags.None,
                    buffer.Handle,
                    format,
                    offset,
                    size
                )

            let mutable handle = VkBufferView.Null
            VkRaw.vkCreateBufferView(device.Handle, &&info, NativePtr.zero, &&handle)
                |> check "could not create BufferView"

            new BufferView(device, handle, buffer, format, offset, size)

// =======================================================================
// Device Extensions
// =======================================================================
[<AbstractClass; Sealed; Extension>]
type BufferExtensions private() =

    [<Extension>]
    static member inline CreateBuffer(memory: IDeviceMemory, usage: VkBufferUsageFlags, size: uint64,
                                      [<Optional; DefaultParameterValue(0UL)>] alignment : uint64,
                                      [<Optional; DefaultParameterValue(false)>] export : bool) =
        memory |> Buffer.create' false export usage alignment size

    [<Extension>]
    static member inline CreateBuffer(memory: IDeviceMemory, usage: VkBufferUsageFlags, data: IBuffer,
                                      [<Optional; DefaultParameterValue(0UL)>] alignment : uint64,
                                      [<Optional; DefaultParameterValue(false)>] export : bool) =
        Buffer.ofBuffer' export usage alignment data memory

    [<Extension>]
    static member inline CreateBuffer(device: Device, usage: VkBufferUsageFlags, size: uint64,
                                      [<Optional; DefaultParameterValue(0UL)>] alignment : uint64,
                                      [<Optional; DefaultParameterValue(false)>] export : bool) =
        device.DeviceMemory.CreateBuffer(usage, size, alignment, export)

    [<Extension>]
    static member inline CreateBuffer(device: Device, usage: VkBufferUsageFlags, data: IBuffer,
                                      [<Optional; DefaultParameterValue(0UL)>] alignment : uint64,
                                      [<Optional; DefaultParameterValue(false)>] export : bool) =
        device.DeviceMemory.CreateBuffer(usage, data, alignment, export)

    [<Extension>]
    static member inline TryUpdate(buffer: Buffer, data: IBuffer) =
        Buffer.tryUpdate data buffer

    [<Extension>]
    static member inline CreateBufferView(device : Device, buffer : Buffer, format : VkFormat, offset : uint64, size : uint64) =
        device |> BufferView.create format buffer offset size