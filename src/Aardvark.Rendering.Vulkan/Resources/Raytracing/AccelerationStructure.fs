namespace Aardvark.Rendering.Vulkan.Raytracing

#nowarn "9"

open Microsoft.FSharp.NativeInterop

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open Aardvark.Rendering.Vulkan
open Aardvark.Rendering.Vulkan.KHRAccelerationStructure
open Aardvark.Rendering.Vulkan.KHRBufferDeviceAddress

type AccelerationStructure =
    class
        inherit Resource<VkAccelerationStructureKHR>
        val Data : AccelerationStructureData
        val ResultBuffer : Buffer
        val ScratchBuffer : Buffer
        val DeviceAddress : VkDeviceAddress

        override x.Destroy() =
            VkRaw.vkDestroyAccelerationStructureKHR(x.Device.Handle, x.Handle, NativePtr.zero)
            x.ResultBuffer.Dispose()
            x.ScratchBuffer.Dispose()

        new(device : Device, handle : VkAccelerationStructureKHR, data : AccelerationStructureData, resultBuffer : Buffer, scratchBuffer : Buffer) =
            let address =
                native {
                    let! pInfo = VkAccelerationStructureDeviceAddressInfoKHR(handle)
                    return VkRaw.vkGetAccelerationStructureDeviceAddressKHR(device.Handle, pInfo)
                }  

            { inherit Resource<_>(device, handle)
              Data = data
              ResultBuffer = resultBuffer
              ScratchBuffer = scratchBuffer
              DeviceAddress = address }   
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AccelerationStructure =

    [<AutoOpen>]
    module private AccelerationStructureCommands =
    
        type Command with
    
            static member Build(accelerationStructure : AccelerationStructure, data : NativeAccelerationStructureData, updateOnly : bool) =
                { new Command() with
                    member x.Compatible = QueueFlags.Graphics
                    member x.Enqueue cmd =
                        cmd.AppendCommand()

                        let mutable info = data.GeometryInfo
                        info.mode <- if updateOnly then VkBuildAccelerationStructureModeKHR.Update else VkBuildAccelerationStructureModeKHR.Build
                        info.scratchData.deviceAddress <- accelerationStructure.ScratchBuffer.DeviceAddress
                        info.srcAccelerationStructure <- if updateOnly then accelerationStructure.Handle else VkAccelerationStructureKHR.Null
                        info.dstAccelerationStructure <- accelerationStructure.Handle

                        let buildRangeInfos =
                            data.Primitives |> Array.map (fun count ->
                                let info = VkAccelerationStructureBuildRangeInfoKHR(count, 0u, 0u, 0u)
                                let ptr = NativePtr.alloc 1
                                info |> NativePtr.write ptr
                                ptr
                            )
    
                        native {
                            let! pGeometryInfo = info
                            let! ppBuildRangeInfos = buildRangeInfos

                            VkRaw.vkCmdBuildAccelerationStructuresKHR(
                                cmd.Handle, 1u, pGeometryInfo, ppBuildRangeInfos
                            )
                        }

                        [ CommandResource.compensation (fun _ -> buildRangeInfos |> Array.iter NativePtr.free) ]
                }

    let private createHandle (device : Device) (data : AccelerationStructureData) (resultBuffer : Buffer) (scratchBuffer : Buffer) =
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
                        resultBuffer.Handle, 0UL, uint64 resultBuffer.Size,
                        typ, 0UL
                    )

                let result = VkRaw.vkCreateAccelerationStructureKHR(device.Handle, pInfo, NativePtr.zero, pHandle)
                if result <> VkResult.Success then
                    failwithf "[Raytracing] Could not create acceleration structure: %A" result

                return !!pHandle
            }    

        new AccelerationStructure(device, handle, data, resultBuffer, scratchBuffer)

    /// Creates and builds an acceleration structure with the given data.
    let create (device : Device) (data : AccelerationStructureData) =

        use nativeData = NativeAccelerationStructureData.alloc data
        let buffers = nativeData |> NativeAccelerationStructureData.createBuffers device
        let accelerationStructure = createHandle device data buffers.ResultBuffer buffers.ScratchBuffer
    
        device.perform {
            do! Command.Build(accelerationStructure, nativeData, false)
        }
    
        accelerationStructure

    /// Attempts to update the given acceleration structure with the given data.
    /// Returns true on success, false otherwise.
    let tryUpdate (data : AccelerationStructureData) (accelerationStructure : AccelerationStructure) =

        // Determine if we can update with new data
        // See: https://www.khronos.org/registry/vulkan/specs/1.2-extensions/man/html/vkGetAccelerationStructureBuildSizesKHR.html
        let isGeometryCompatible (n : GeometryData) (o : GeometryData) =
            match n, o with
            | GeometryData.Triangles (nv, ni, _), GeometryData.Triangles (ov, oi, _) ->
                nv.Count <= ov.Count

            | GeometryData.AABBs _, GeometryData.AABBs _ -> true
            | _ -> false

        let isCompatible =
            match data, accelerationStructure.Data with
            | AccelerationStructureData.Instances n, AccelerationStructureData.Instances o ->
                n.Count <= o.Count

            | AccelerationStructureData.Geometry n, AccelerationStructureData.Geometry o ->
                n.Length <= o.Length &&
                (n, o) ||> Array.forall2 (fun n o ->
                    n.Primitives <= o.Primitives && isGeometryCompatible n.Data o.Data
                )

            | _ ->
                failwithf "[Raytracing] Trying to update acceleration structure with data of different type"

        if isCompatible then
            use nativeData = NativeAccelerationStructureData.alloc data
            accelerationStructure.Device.perform {
                do! Command.Build(accelerationStructure, nativeData, true)
            }

            true
        else
            false