namespace Aardvark.Rendering.Vulkan.Raytracing

open Aardvark.Base
open Aardvark.Rendering.Raytracing
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop
open KHRAccelerationStructure
open KHRBufferDeviceAddress

#nowarn "9"
#nowarn "51"

type AccelerationStructure =
    class
        inherit Resource<VkAccelerationStructureKHR>
        val Data : AccelerationStructureData
        val Usage : AccelerationStructureUsage
        val ResultBuffer : Buffer
        val DeviceAddress : VkDeviceAddress
        val mutable private scratchBuffer : Buffer voption
        val mutable private name : string

        member this.Name
            with get() = this.name
            and set name =
                this.name <- name
                if name <> null then
                    this.ResultBuffer.Name <- $"{name} (Result Buffer)"
                    this.scratchBuffer |> ValueOption.iter (fun b -> b.Name <- $"{name} (Scratch Buffer)")
                this.Device.SetObjectName(VkObjectType.AccelerationStructureKhr, this.Handle.Handle, name)

        member this.GeometryCount =
            match this.Data with
            | AccelerationStructureData.Geometry g -> g.Count
            | _ -> 0

        member this.Size =
            this.ResultBuffer.Size

        member this.TotalSize =
            let scratchSize = this.scratchBuffer |> ValueOption.map _.Size |> ValueOption.defaultValue 0UL
            this.ResultBuffer.Size + scratchSize

        member this.AllowUpdate =
            this.Usage.HasFlag AccelerationStructureUsage.Update

        member this.ScratchBuffer =
            match this.scratchBuffer with
            | ValueSome b -> b
            | _ -> failf "scratch buffer is not allocated"

        member internal this.CreateScratchBuffer(size : VkDeviceSize) =
            match this.scratchBuffer with
            | ValueSome b when b.Size = size -> ()
            | _ ->
                this.scratchBuffer |> ValueOption.iter _.Dispose()
                let alignment = uint64 this.Device.PhysicalDevice.Limits.Raytracing.Value.MinAccelerationStructureScratchOffsetAlignment
                let flags = VkBufferUsageFlags.StorageBufferBit ||| VkBufferUsageFlags.ShaderDeviceAddressBitKhr
                let buffer = this.Device.DeviceMemory |> Buffer.create' false false flags alignment size
                this.scratchBuffer <- ValueSome buffer

        member internal this.DestroyScratchBuffer() =
            this.scratchBuffer |> ValueOption.iter _.Dispose()
            this.scratchBuffer <- ValueNone

        override this.Destroy() =
            VkRaw.vkDestroyAccelerationStructureKHR(this.Device.Handle, this.Handle, NativePtr.zero)
            this.ResultBuffer.Dispose()
            this.DestroyScratchBuffer()

        new(device : Device, handle : VkAccelerationStructureKHR, buffer : Buffer,
            data : AccelerationStructureData, usage : AccelerationStructureUsage) =

            let address =
                let mutable info = VkAccelerationStructureDeviceAddressInfoKHR(handle)
                VkRaw.vkGetAccelerationStructureDeviceAddressKHR(device.Handle, &&info)

            {
                inherit Resource<_>(device, handle)
                Data = data
                Usage = usage
                ResultBuffer = buffer
                DeviceAddress = address
                scratchBuffer = ValueNone
                name = null
            }

        interface IAccelerationStructure with
            member x.Usage = x.Usage
            member x.GeometryCount = x.GeometryCount
            member x.SizeInBytes = x.TotalSize
            member x.Name with get() = x.Name and set name = x.Name <- name
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AccelerationStructure =

    module private Array =

        let inline safeForall2 ([<InlineIfLambda>] predicate : 'T1 -> 'T2 -> bool) (a : 'T1[]) (b : 'T2[]) =
            let mutable result = true
            let mutable i = 0
            let n = min a.Length b.Length
            while result && i < n do
                result <- predicate a.[i] b.[i]
                &i += 1
            result

    [<AutoOpen>]
    module private AccelerationStructureCommands =

        type Command with

            static member Build(accelerationStructure : AccelerationStructure, data : PreparedAccelerationStructureData, update : bool) =
                { new Command() with
                    member x.Compatible = QueueFlags.Compute
                    member x.Enqueue cmd =
                        cmd.AppendCommand()

                        let mutable buildGeometryInfo = data.BuildGeometryInfo
                        buildGeometryInfo.mode <- if update then VkBuildAccelerationStructureModeKHR.Update else VkBuildAccelerationStructureModeKHR.Build
                        buildGeometryInfo.scratchData.deviceAddress <- accelerationStructure.ScratchBuffer.DeviceAddress
                        buildGeometryInfo.srcAccelerationStructure <- if update then accelerationStructure.Handle else VkAccelerationStructureKHR.Null
                        buildGeometryInfo.dstAccelerationStructure <- accelerationStructure.Handle

                        use pBuildRangeInfos = fixed data.BuildRangeInfos
                        let ppBuildRangeInfos = NativePtr.stackalloc 1
                        ppBuildRangeInfos.[0] <- pBuildRangeInfos

                        VkRaw.vkCmdBuildAccelerationStructuresKHR(
                            cmd.Handle, 1u, &&buildGeometryInfo, ppBuildRangeInfos
                        )

                        cmd.AddResource accelerationStructure
                }

            static member WriteProperties(accelerationStructure : AccelerationStructure, queryPool : QueryPool) =
                { new Command() with
                    member _.Compatible = QueueFlags.Compute
                    member _.Enqueue cmd =
                        cmd.AppendCommand()

                        let mutable handle = accelerationStructure.Handle
                        VkRaw.vkCmdWriteAccelerationStructuresPropertiesKHR(
                            cmd.Handle, 1u, &&handle, queryPool.Type, queryPool.Handle, 0u
                        )

                        cmd.AddResource accelerationStructure
                        cmd.AddResource queryPool
                }

            static member Copy(src : AccelerationStructure, dst : AccelerationStructure, mode : VkCopyAccelerationStructureModeKHR) =
                { new Command() with
                    member _.Compatible = QueueFlags.Compute
                    member _.Enqueue cmd =
                        cmd.AppendCommand()

                        let mutable info = VkCopyAccelerationStructureInfoKHR(src.Handle, dst.Handle, mode)
                        VkRaw.vkCmdCopyAccelerationStructureKHR(cmd.Handle, &&info)

                        cmd.AddResource src
                        cmd.AddResource dst
                }

    let private createHandle (device : Device) (data : AccelerationStructureData) (usage : AccelerationStructureUsage) (size : uint64) =
        let buffer =
            let flags = VkBufferUsageFlags.AccelerationStructureStorageBitKhr ||| VkBufferUsageFlags.ShaderDeviceAddressBitKhr
            device.DeviceMemory |> Buffer.create flags size

        let mutable createInfo =
            VkAccelerationStructureCreateInfoKHR(
                VkAccelerationStructureCreateFlagsKHR.None,
                buffer.Handle, 0UL, buffer.Size,
                data.Type, 0UL
            )

        let mutable handle = VkAccelerationStructureKHR.Null
        VkRaw.vkCreateAccelerationStructureKHR(device.Handle, &&createInfo, NativePtr.zero, &&handle)
            |> checkf "could not create acceleration structure"

        new AccelerationStructure(device, handle, buffer, data, usage)

    /// Creates and builds an acceleration structure with the given data.
    let create (device : Device) (usage : AccelerationStructureUsage) (data : AccelerationStructureData) =
        use prepared = AccelerationStructureData.prepare device usage data

        let mutable result = createHandle device data usage prepared.Size
        result.CreateScratchBuffer prepared.ScratchBuildSize

        if usage.HasFlag AccelerationStructureUsage.Compact then
            use queryPool = device.CreateQueryPool(VkQueryType.AccelerationStructureCompactedSizeKhr)

            device.perform {
                do! Command.Build(result, prepared, false)

                do! Command.MemoryBarrier(
                    VkPipelineStageFlags.AccelerationStructureBuildBitKhr, VkAccessFlags.AccelerationStructureWriteBitKhr,
                    VkPipelineStageFlags.AccelerationStructureBuildBitKhr, VkAccessFlags.AccelerationStructureReadBitKhr
                )

                do! Command.Reset queryPool
                do! Command.WriteProperties(result, queryPool)
            }

            let compactSize = queryPool.GetResults().[0]
            if compactSize < result.Size then
                let compact = createHandle device data usage compactSize

                if device.DebugConfig.PrintAccelerationStructureCompactionInfo then
                    let ratio = 100.0 * (float compactSize / float result.Size)
                    let typ = if data.Type = VkAccelerationStructureTypeKHR.TopLevel then "TLAS" else "BLAS"
                    Log.startTimed $"[Vulkan] compacting {typ} {Mem result.Size} -> {Mem compactSize} (%.2f{ratio}%%)"

                device.perform {
                    do! Command.Copy(result, compact, VkCopyAccelerationStructureModeKHR.Compact)
                }

                if device.DebugConfig.PrintAccelerationStructureCompactionInfo then
                    Log.stop()

                result.Dispose()
                result <- compact
        else
            device.perform {
                do! Command.Build(result, prepared, false)
            }

        // We no longer need the scratch buffer we used for building.
        // But we may need a (potentially smaller) scratch buffer for updating.
        if result.AllowUpdate then
            result.CreateScratchBuffer prepared.ScratchUpdateSize
        else
            result.DestroyScratchBuffer()

        result

    /// Attempts to update the given acceleration structure with the given data.
    /// Returns true on success, false otherwise.
    let tryUpdate (data : AccelerationStructureData) (accelerationStructure : AccelerationStructure) =

        // Determine if we can update with new data
        // https://registry.khronos.org/vulkan/specs/latest/man/html/vkCmdBuildAccelerationStructuresKHR.html
        let inline indexType (m : TriangleMesh) =
            if isNull m.Indices then ValueNone
            else ValueSome m.Indices.Type

        let isGeometryCompatible (n : TraceGeometry) (o : TraceGeometry) =
            match n, o with
            | TraceGeometry.Triangles nm, TraceGeometry.Triangles om ->
                nm.Length = om.Length &&
                (nm, om) ||> Array.safeForall2 (fun n o ->
                    n.Flags = o.Flags &&
                    n.Primitives = o.Primitives &&
                    n.Vertices.Count = o.Vertices.Count &&
                    indexType n = indexType o
                )

            | TraceGeometry.AABBs nbb, TraceGeometry.AABBs obb ->
                nbb.Length = obb.Length &&
                (nbb, obb) ||> Array.safeForall2 (fun n o ->
                    n.Count = o.Count &&
                    n.Flags = o.Flags
                )

            | _ -> false

        let isCompatible =
            if accelerationStructure.AllowUpdate then
                match data, accelerationStructure.Data with
                | AccelerationStructureData.Instances (nc, _), AccelerationStructureData.Instances (oc, _) ->
                    nc = oc

                | AccelerationStructureData.Geometry n, AccelerationStructureData.Geometry o ->
                    isGeometryCompatible n o

                | _ ->
                    failf "Trying to update acceleration structure with data of different type"

            else
                false

        if isCompatible then
            use prepared = data |> AccelerationStructureData.prepare accelerationStructure.Device accelerationStructure.Usage

            accelerationStructure.Device.perform {
                do! Command.Build(accelerationStructure, prepared, true)
            }

            true
        else
            false