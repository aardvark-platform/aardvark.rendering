namespace Aardvark.Rendering.Vulkan.Raytracing

open System

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open Aardvark.Rendering.Vulkan
open EXTOpacityMicromap
open KHRAccelerationStructure
open KHRBufferDeviceAddress
open KHRSynchronization2

#nowarn "9"
#nowarn "51"

type internal Micromap(device: Device, handle: VkMicromapEXT, buffer: Buffer) =
    inherit Resource<VkMicromapEXT>(device, handle)

    member val IndexType     = VkIndexType.NoneKhr        with get, set
    member val IndexBuffer   = Option<Buffer>.None        with get, set
    member val UsageCounts   = Array.empty<MicromapUsage> with get, set
    member val IsDiscardable = false                      with get, set

    member this.Size = buffer.Size

    member this.IndexBufferAddress =
        match this.IndexBuffer with
        | Some buffer -> buffer.DeviceAddress
        | _ -> 0UL

    member this.IndexStride =
        match this.IndexType with
        | VkIndexType.Uint16 -> 2UL
        | VkIndexType.Uint32 -> 4UL
        | _ -> 0UL

    override this.Destroy() =
        VkRaw.vkDestroyMicromapEXT(this.Device.Handle, this.Handle, NativePtr.zero)
        buffer.Dispose()
        this.IndexBuffer |> Option.iter _.Dispose()

    interface IBackendMicromap with
        member this.SizeInBytes = this.Size

module internal Micromap =

    [<AutoOpen>]
    module private MicromapCommands =

        type Command with
            static member Build(buildInfo: VkMicromapBuildInfoEXT) =
                { new Command() with
                    member x.Compatible = QueueFlags.Compute
                    member x.Enqueue cmd =
                        cmd.AppendCommand()

                        buildInfo |> NativePtr.pin (fun pBuildInfo ->
                            VkRaw.vkCmdBuildMicromapsEXT(
                                cmd.Handle, 1u, pBuildInfo
                            )
                        )
                }

            static member WriteProperties(micromap: Micromap, queryPool: QueryPool) =
                { new Command() with
                    member _.Compatible = QueueFlags.Compute
                    member _.Enqueue cmd =
                        cmd.AppendCommand()

                        let mutable handle = micromap.Handle
                        VkRaw.vkCmdWriteMicromapsPropertiesEXT(
                            cmd.Handle, 1u, &&handle, queryPool.Type, queryPool.Handle, 0u
                        )

                        cmd.AddResource micromap
                        cmd.AddResource queryPool
                }

            static member Copy(src: Micromap, dst: Micromap, mode: VkCopyMicromapModeEXT) =
                { new Command() with
                    member _.Compatible = QueueFlags.Compute
                    member _.Enqueue cmd =
                        cmd.AppendCommand()

                        let mutable info = VkCopyMicromapInfoEXT(src.Handle, dst.Handle, mode)
                        VkRaw.vkCmdCopyMicromapEXT(cmd.Handle, &&info)

                        cmd.AddResource src
                        cmd.AddResource dst
                }

    // VUID-vkCmdBuildMicromapsEXT-data-07510
    // VUID-vkCmdBuildMicromapsEXT-pInfos-07515
    let private prepareInputBuffer (device: Device) (data: Array) =
        let usage = VkBufferUsageFlags.MicromapBuildInputReadOnlyBitExt ||| VkBufferUsageFlags.ShaderDeviceAddressBitKhr ||| VkBufferUsageFlags.TransferDstBit
        let buffer = ArrayBuffer data
        device.DeviceMemory |> Buffer.ofBuffer' false usage 256UL buffer

    let private createHandle (device: Device) (size: uint64) =
        let buffer =
            device.CreateBuffer(VkBufferUsageFlags.MicromapBuildInputReadOnlyBitExt, size)

        let mutable createInfo =
            VkMicromapCreateInfoEXT(
                VkMicromapCreateFlagsEXT.None,
                buffer.Handle, 0UL, size,
                VkMicromapTypeEXT.OpacityMicromap, 0UL
            )

        let mutable result = VkMicromapEXT.Null
        VkRaw.vkCreateMicromapEXT(device.Handle, &&createInfo, NativePtr.zero, &&result)
            |> check "failed to create micromap"

        new Micromap(device, result, buffer)

    let create (device: Device) (compact: bool) (data: IMicromapData) =
        use pUsageCounts = fixed data.UsageCounts

        let flags =
            if compact then
                VkBuildMicromapFlagsEXT.PreferFastTraceBit ||| VkBuildMicromapFlagsEXT.AllowCompactionBit
            else
                VkBuildMicromapFlagsEXT.PreferFastTraceBit

        let mutable buildInfo =
            VkMicromapBuildInfoEXT(
                VkMicromapTypeEXT.OpacityMicromap, flags, VkBuildMicromapModeEXT.Build, VkMicromapEXT.Null,
                uint32 data.UsageCounts.Length, NativePtr.cast pUsageCounts, NativePtr.zero,
                VkDeviceOrHostAddressConstKHR(), VkDeviceOrHostAddressKHR(), VkDeviceOrHostAddressConstKHR(),
                uint64 sizeof<VkMicromapTriangleEXT>
            )

        let mutable buildSizesInfo = VkMicromapBuildSizesInfoEXT.Empty
        VkRaw.vkGetMicromapBuildSizesEXT(device.Handle, VkAccelerationStructureBuildTypeKHR.Device, &&buildInfo, &&buildSizesInfo)

        let mutable result = createHandle device buildSizesInfo.micromapSize

        use dataBuffer =
            data.Data |> prepareInputBuffer device

        // VUID-vkCmdBuildMicromapsEXT-pInfos-07514
        // VUID-vkCmdBuildMicromapsEXT-pInfos-07511
        use scratchBuffer =
            let usage = VkBufferUsageFlags.StorageBufferBit ||| VkBufferUsageFlags.ShaderDeviceAddressBitKhr
            let alignment = uint64 device.PhysicalDevice.Limits.Raytracing.Value.MinAccelerationStructureScratchOffsetAlignment
            device.CreateBuffer(usage, buildSizesInfo.buildScratchSize, alignment)

        use trianglesBuffer =
            data.Triangles |> prepareInputBuffer device

        buildInfo.dstMicromap <- result.Handle
        buildInfo.data.deviceAddress <- dataBuffer.DeviceAddress
        buildInfo.scratchData.deviceAddress <- scratchBuffer.DeviceAddress
        buildInfo.triangleArray.deviceAddress <- trianglesBuffer.DeviceAddress

        if compact then
            use queryPool = device.CreateQueryPool(VkQueryType.MicromapCompactedSizeExt)

            device.perform {
                do! Command.Build(buildInfo)

                do! Command.MemoryBarrier(
                    VkPipelineStageFlags2KHR.MicromapBuildBitExt, VkAccessFlags2KHR.MicromapWriteBitExt,
                    VkPipelineStageFlags2KHR.MicromapBuildBitExt, VkAccessFlags2KHR.MicromapReadBitExt
                )

                do! Command.Reset queryPool
                do! Command.WriteProperties(result, queryPool)
            }

            let compactSize = queryPool.GetResults().[0]
            if compactSize < result.Size then
                let compact = createHandle device compactSize

                if device.DebugConfig.PrintAccelerationStructureCompactionInfo then
                    let ratio = 100.0 * (float compactSize / float result.Size)
                    Log.startTimed $"[Vulkan] compacting OMM {Mem result.Size} -> {Mem compactSize} (%.2f{ratio}%%)"

                device.perform {
                    do! Command.Copy(result, compact, VkCopyMicromapModeEXT.Compact)
                }

                if device.DebugConfig.PrintAccelerationStructureCompactionInfo then
                    Log.stop()

                result.Dispose()
                result <- compact
        else
            device.perform {
                do! Command.Build(buildInfo)
            }

        if not <| isNull data.Indices then
            result.IndexType   <- VkIndexType.ofType <| data.Indices.GetType().GetElementType()
            result.IndexBuffer <- Some <| prepareInputBuffer device data.Indices

        result.UsageCounts   <- data.IndexUsageCounts
        result.IsDiscardable <- buildSizesInfo.discardable = VkTrue
        result

    let prepare (device: Device) (micromap: IMicromap) : Micromap =
        match micromap with
        | :? Aardvark.Rendering.Raytracing.Micromap as micromap ->
            create device micromap.Compress micromap.Data

        | :? Micromap as micromap ->
            micromap.AddReference()
            micromap

        | _ ->
            failf $"unsupported micromap type: {micromap.GetType()}"