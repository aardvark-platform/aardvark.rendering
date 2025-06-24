namespace Aardvark.Rendering.Vulkan.Raytracing

#nowarn "9"

open Aardvark.Base
open Aardvark.Rendering.Raytracing
open Aardvark.Rendering.Vulkan
open KHRAccelerationStructure

type AccelerationStructure =
    class
        inherit Resource<VkAccelerationStructureKHR>
        val Data : AccelerationStructureData
        val Usage : AccelerationStructureUsage
        val AllowUpdate : bool
        val ResultBuffer : Buffer
        val ScratchBuffer : Buffer
        val DeviceAddress : VkDeviceAddress
        val mutable private name : string

        member x.Name
            with get() = x.name
            and set name =
                x.name <- name
                if name <> null then
                    x.ResultBuffer.Name <- $"{name} (Result Buffer)"
                    x.ScratchBuffer.Name <- $"{name} (Scratch Buffer)"
                x.Device.SetObjectName(VkObjectType.AccelerationStructureKhr, x.Handle.Handle, name)

        member x.GeometryCount =
            match x.Data with
            | AccelerationStructureData.Geometry g -> g.Count
            | _ -> 0

        override x.Destroy() =
            VkRaw.vkDestroyAccelerationStructureKHR(x.Device.Handle, x.Handle, NativePtr.zero)
            x.ResultBuffer.Dispose()
            x.ScratchBuffer.Dispose()

        new(device : Device, handle : VkAccelerationStructureKHR,
            data : AccelerationStructureData, usage : AccelerationStructureUsage, allowUpdate : bool,
            resultBuffer : Buffer, scratchBuffer : Buffer) =
            let address =
                native {
                    let! pInfo = VkAccelerationStructureDeviceAddressInfoKHR(handle)
                    return VkRaw.vkGetAccelerationStructureDeviceAddressKHR(device.Handle, pInfo)
                }

            { inherit Resource<_>(device, handle)
              Data = data
              Usage = usage
              AllowUpdate = allowUpdate
              ResultBuffer = resultBuffer
              ScratchBuffer = scratchBuffer
              DeviceAddress = address
              name = null }

        interface IAccelerationStructure with
            member x.Usage = x.Usage
            member x.GeometryCount = x.GeometryCount
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

            static member Build(accelerationStructure : AccelerationStructure, data : NativeAccelerationStructureData, updateOnly : bool) =
                { new Command() with
                    member x.Compatible = QueueFlags.Compute
                    member x.Enqueue cmd =
                        cmd.AppendCommand()

                        let mutable info = data.GeometryInfo
                        info.mode <- if updateOnly then VkBuildAccelerationStructureModeKHR.Update else VkBuildAccelerationStructureModeKHR.Build
                        info.scratchData.deviceAddress <- accelerationStructure.ScratchBuffer.DeviceAddress
                        info.srcAccelerationStructure <- if updateOnly then accelerationStructure.Handle else VkAccelerationStructureKHR.Null
                        info.dstAccelerationStructure <- accelerationStructure.Handle

                        native {
                            let! pGeometryInfo = info
                            let! pBuildRangeInfos = data.BuildRangeInfos
                            let! ppBuildRangeInfos = pBuildRangeInfos

                            VkRaw.vkCmdBuildAccelerationStructuresKHR(
                                cmd.Handle, 1u, pGeometryInfo, ppBuildRangeInfos
                            )
                        }

                        cmd.AddResource accelerationStructure
                }

    let private createHandle (device : Device) (data : AccelerationStructureData) (usage : AccelerationStructureUsage)
                             (allowUpdate : bool) (resultBuffer : Buffer) (scratchBuffer : Buffer) =
        let typ =
            match data with
            | AccelerationStructureData.Instances _ -> VkAccelerationStructureTypeKHR.TopLevel
            | AccelerationStructureData.Geometry _  -> VkAccelerationStructureTypeKHR.BottomLevel

        let handle =
            native {
                let! pHandle = VkAccelerationStructureKHR.Null
                let! pInfo =
                    VkAccelerationStructureCreateInfoKHR(
                        VkAccelerationStructureCreateFlagsKHR.None,
                        resultBuffer.Handle, 0UL, resultBuffer.Size,
                        typ, 0UL
                    )

                let result = VkRaw.vkCreateAccelerationStructureKHR(device.Handle, pInfo, NativePtr.zero, pHandle)
                if result <> VkResult.Success then
                    failwithf "[Raytracing] Could not create acceleration structure: %A" result

                return !!pHandle
            }

        new AccelerationStructure(device, handle, data, usage, allowUpdate, resultBuffer, scratchBuffer)

    /// Creates and builds an acceleration structure with the given data.
    let create (device : Device) (allowUpdate : bool) (usage : AccelerationStructureUsage) (data : AccelerationStructureData) =
        use nativeData = NativeAccelerationStructureData.alloc device allowUpdate usage data
        let buffers = nativeData |> NativeAccelerationStructureData.createBuffers device
        let accelerationStructure = createHandle device data usage allowUpdate buffers.ResultBuffer buffers.ScratchBuffer

        device.perform {
            do! Command.Build(accelerationStructure, nativeData, false)
        }

        accelerationStructure

    /// Attempts to update the given acceleration structure with the given data.
    /// Returns true on success, false otherwise.
    let tryUpdate (data : AccelerationStructureData) (accelerationStructure : AccelerationStructure) =

        // Determine if we can update with new data
        // https://khronos.org/registry/vulkan/specs/1.2-khr-extensions/html/chap32.html
        // https://www.khronos.org/registry/vulkan/specs/1.2-extensions/man/html/vkGetAccelerationStructureBuildSizesKHR.html
        let isGeometryCompatible (n : TraceGeometry) (o : TraceGeometry) =
            match n, o with
            | TraceGeometry.Triangles nm, TraceGeometry.Triangles om ->
                nm.Length <= om.Length &&
                (nm, om) ||> Array.safeForall2 (fun n o ->
                    n.Flags = o.Flags &&
                    n.Primitives = o.Primitives &&
                    isNull n.Indices = isNull o.Indices
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
                | AccelerationStructureData.Instances n, AccelerationStructureData.Instances o ->
                    n.Count = o.Count

                | AccelerationStructureData.Geometry n, AccelerationStructureData.Geometry o ->
                    isGeometryCompatible n o

                | _ ->
                    failwithf "[Raytracing] Trying to update acceleration structure with data of different type"

            else
                false

        if isCompatible then
            use nativeData = NativeAccelerationStructureData.alloc accelerationStructure.Device true accelerationStructure.Usage data
            accelerationStructure.Device.perform {
                do! Command.Build(accelerationStructure, nativeData, true)
            }

            true
        else
            false