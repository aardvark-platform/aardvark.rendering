namespace Aardvark.Rendering.Vulkan.Raytracing

#nowarn "9"

open System
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop

open Aardvark.Base
open Aardvark.Rendering.Vulkan
open Aardvark.Rendering.Vulkan.KHRAccelerationStructure
open Aardvark.Rendering.Vulkan.KHRBufferDeviceAddress

type AABBsData =
    { Buffer : Buffer
      Offset : uint64
      Stride : uint64 }

type VertexData =
    { Format : VkFormat
      Buffer : Buffer
      Count  : uint32
      Offset : uint64
      Stride : uint64 }

type IndexData =
    { Type   : VkIndexType
      Buffer : Buffer
      Offset : uint64 }

[<RequireQualifiedAccess>]
type GeometryData =
    | AABBs     of data: AABBsData
    | Triangles of vertexData: VertexData * indexData: Option<IndexData> * transform: Buffer

type Geometry =
    { Data : GeometryData
      Primitives : uint32
      Flags : VkGeometryFlagsKHR }

type Instances =
    { Buffer : Buffer
      Offset : uint64 
      Count : uint32 }

[<RequireQualifiedAccess>]
type AccelerationStructureType =
    | TopLevel
    | BottomLevel

    member x.VulkanType =
        match x with
        | TopLevel -> VkAccelerationStructureTypeKHR.TopLevel
        | BottomLevel -> VkAccelerationStructureTypeKHR.BottomLevel

[<RequireQualifiedAccess>]
type AccelerationStructureData =
    | Geometry of Geometry[]
    | Instances of Instances

    member x.Type =
        match x with
        | Geometry _ -> AccelerationStructureType.BottomLevel
        | Instances _ -> AccelerationStructureType.TopLevel

type AccelerationStructure =
    class
        inherit Resource<VkAccelerationStructureKHR>
        val mutable Data : AccelerationStructureData
        val mutable ResultBuffer : Buffer
        val mutable ScratchBuffer : Buffer
        val mutable Address : uint64

        member x.Type =
            x.Data.Type

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
              Address = address }   
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AccelerationStructure =

    type private Disposable<'T>(value : 'T, dispose : 'T -> unit) =
        member x.Value = value
        member x.Dispose() = dispose value

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    type private Sizes =
        { ResultSize : uint64
          ScratchSize : uint64 }

    type private BuildInfo =
        { Device : Device
          GeometryInfo : Disposable<VkAccelerationStructureBuildGeometryInfoKHR>
          PrimitiveCounts : uint32[] }

        interface IDisposable with
            member x.Dispose() = x.GeometryInfo.Dispose()

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module private Array =
        
        let pin<'T when 'T : unmanaged> (data : 'T[]) =
            let gc = GCHandle.Alloc(data, GCHandleType.Pinned)
            new Disposable<_>(NativePtr.ofNativeInt<'T> <| gc.AddrOfPinnedObject(), ignore >> gc.Free)

    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module private Buffer =

        let getDeviceAddress (device : Device) (buffer : Buffer) =
            native {
                let! pInfo = VkBufferDeviceAddressInfoKHR(buffer.Handle)
                return VkRaw.vkGetBufferDeviceAddressKHR(device.Handle, pInfo)
            }

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module private VkAccelerationStructureGeometryDataKHR =

        let ofAabbData (device : Device) (data : AABBsData) =
            let address =
                VkDeviceOrHostAddressConstKHR.DeviceAddress(
                     Buffer.getDeviceAddress device data.Buffer + data.Offset
                 )

            VkAccelerationStructureGeometryDataKHR.Aabbs(
                VkAccelerationStructureGeometryAabbsDataKHR(address, data.Stride)
            )

        let ofTriangleData (device : Device) (vertexData : VertexData) (indexData : Option<IndexData>) (transform : Buffer) =
            let vertexDataAddress = 
                VkDeviceOrHostAddressConstKHR.DeviceAddress(
                    Buffer.getDeviceAddress device vertexData.Buffer + vertexData.Offset
                )

            let indexDataAddress =
                VkDeviceOrHostAddressConstKHR.DeviceAddress(
                    match indexData with
                    | Some data -> Buffer.getDeviceAddress device data.Buffer + data.Offset
                    | _ -> 0UL
                )

            let indexType =
                match indexData with
                | Some data -> data.Type
                | _ -> VkIndexType.NoneKhr

            let transformData =
                VkDeviceOrHostAddressConstKHR.DeviceAddress(
                    Buffer.getDeviceAddress device transform
                )

            VkAccelerationStructureGeometryDataKHR.Triangles(
                VkAccelerationStructureGeometryTrianglesDataKHR(
                    vertexData.Format, vertexDataAddress, vertexData.Stride, vertexData.Count,
                    indexType, indexDataAddress, transformData
                )
            )

        let ofInstances (device : Device) (data : Instances) =
            let dataAddress = 
                VkDeviceOrHostAddressConstKHR.DeviceAddress(
                    Buffer.getDeviceAddress device data.Buffer + data.Offset
                )

            VkAccelerationStructureGeometryDataKHR.Instances(
                VkAccelerationStructureGeometryInstancesDataKHR(0u, dataAddress)
            )

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module private VkAccelerationStructureGeometryKHR =

        let ofGeometry (device : Device) (geometry : Geometry) =
            let typ, data =
                match geometry.Data with
                | GeometryData.AABBs d ->
                    VkGeometryTypeKHR.Aabbs,
                    VkAccelerationStructureGeometryDataKHR.ofAabbData device d

                | GeometryData.Triangles (vertexData, indexData, transform) ->
                    VkGeometryTypeKHR.Triangles,
                    VkAccelerationStructureGeometryDataKHR.ofTriangleData device vertexData indexData transform

            VkAccelerationStructureGeometryKHR(typ, data, geometry.Flags)

        let ofInstances (device : Device) (instances : Instances) =
            VkAccelerationStructureGeometryKHR(
                VkGeometryTypeKHR.Instances, 
                VkAccelerationStructureGeometryDataKHR.ofInstances device instances,
                VkGeometryFlagsKHR.None
            )

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module private BuildInfo =

        let private flags =
            VkBuildAccelerationStructureFlagsKHR.PreferFastTraceBit ||| VkBuildAccelerationStructureFlagsKHR.AllowUpdateBit

        let create (device : Device) (data : AccelerationStructureData) =
            let pGeometries, geometryCount, primitiveCounts =
                match data with
                | AccelerationStructureData.Geometry geometry ->
                    let pGeometries =
                        geometry |> Array.map (VkAccelerationStructureGeometryKHR.ofGeometry device) |> Array.pin

                    let primitiveCounts =
                        geometry |> Array.map (fun g -> g.Primitives)

                    pGeometries, uint32 geometry.Length, primitiveCounts

                | AccelerationStructureData.Instances instances ->
                    let pGeometries = new Disposable<_>(NativePtr.alloc 1, NativePtr.free)
                    instances |> (VkAccelerationStructureGeometryKHR.ofInstances device) |> NativePtr.write pGeometries.Value

                    pGeometries, 1u, [| instances.Count |]

            let mutable geometryInfo = VkAccelerationStructureBuildGeometryInfoKHR.Empty
            geometryInfo._type <- data.Type.VulkanType
            geometryInfo.flags <- flags
            geometryInfo.geometryCount <- geometryCount
            geometryInfo.pGeometries <- pGeometries.Value

            { Device = device
              GeometryInfo = new Disposable<_>(geometryInfo, ignore >> pGeometries.Dispose)
              PrimitiveCounts = primitiveCounts }

        let querySizes (info : BuildInfo) =
            let sizes =
                native {
                    let! pSizeInfo = Unchecked.defaultof<_>
                    let! pBuildInfo = info.GeometryInfo.Value
                    let! pPrimitiveCounts = info.PrimitiveCounts

                    VkRaw.vkGetAccelerationStructureBuildSizesKHR(
                        info.Device.Handle, VkAccelerationStructureBuildTypeKHR.Device,
                        pBuildInfo, pPrimitiveCounts, pSizeInfo
                    )

                    return !!pSizeInfo
                }

            { ResultSize = sizes.accelerationStructureSize
              ScratchSize = max sizes.buildScratchSize sizes.updateScratchSize }

        let setHandles (isUpdate : bool) (accelerationStructure : AccelerationStructure) (info : BuildInfo) =

            let mutable geometryInfo = info.GeometryInfo.Value
            geometryInfo.mode <- if isUpdate then VkBuildAccelerationStructureModeKHR.Update else VkBuildAccelerationStructureModeKHR.Build
            geometryInfo.scratchData.deviceAddress <- Buffer.getDeviceAddress info.Device accelerationStructure.ScratchBuffer
            geometryInfo.srcAccelerationStructure <- if isUpdate then accelerationStructure.Handle else VkAccelerationStructureKHR.Null
            geometryInfo.dstAccelerationStructure <- accelerationStructure.Handle

            { info with GeometryInfo = new Disposable<_>(geometryInfo, ignore >> info.GeometryInfo.Dispose) }


    [<AutoOpen>]
    module private AccelerationStructureCommands =
    
        type Command with
    
            static member Build(accelerationStructure : AccelerationStructure, info : BuildInfo, updateOnly : bool) =
                { new Command() with
                    member x.Compatible = QueueFlags.Graphics
                    member x.Enqueue cmd =
                        cmd.AppendCommand()

                        let info =
                            info |> BuildInfo.setHandles updateOnly accelerationStructure

                        let buildRangeInfos =
                            info.PrimitiveCounts |> Array.map (fun count ->
                                let info = VkAccelerationStructureBuildRangeInfoKHR(count, 0u, 0u, 0u)
                                let ptr = NativePtr.alloc 1
                                info |> NativePtr.write ptr
                                ptr
                            )
    
                        native {
                            let! pGeometryInfo = info.GeometryInfo.Value
                            let! ppBuildRangeInfos = buildRangeInfos

                            VkRaw.vkCmdBuildAccelerationStructuresKHR(
                                cmd.Handle, 1u, pGeometryInfo, ppBuildRangeInfos
                            )
                        }

                        [ CommandResource.compensation (fun _ -> buildRangeInfos |> Array.iter NativePtr.free) ]
                }


    let private createResultBuffer (device : Device) (sizes : Sizes) =
        let size = int64 sizes.ResultSize
        let flags = VkBufferUsageFlags.AccelerationStructureStorageBitKhr ||| VkBufferUsageFlags.ShaderDeviceAddressBitKhr
        device |> Buffer.alloc flags size

    let private createScratchBuffer (device : Device) (sizes : Sizes) =
        let size = int64 sizes.ScratchSize
        let flags = VkBufferUsageFlags.ShaderDeviceAddressBitKhr
        device |> Buffer.alloc flags size

    let private createHandle (device : Device) (data : AccelerationStructureData) (resultBuffer : Buffer) (scratchBuffer : Buffer) =
        let handle =
            native {
                let! pHandle = VkAccelerationStructureKHR.Null
                let! pInfo =
                    VkAccelerationStructureCreateInfoKHR(
                        VkAccelerationStructureCreateFlagsKHR.None,
                        resultBuffer.Handle, 0UL, uint64 resultBuffer.Size,
                        data.Type.VulkanType, 0UL
                    )

                let result = VkRaw.vkCreateAccelerationStructureKHR(device.Handle, pInfo, NativePtr.zero, pHandle)
                if result <> VkResult.Success then
                    failwithf "[Raytracing] Could not create acceleration structure: %A" result

                return !!pHandle
            }    

        new AccelerationStructure(device, handle, data, resultBuffer, scratchBuffer)

    /// Creates and builds an acceleration structure with the given data.
    let create (device : Device) (data : AccelerationStructureData) =
        use info = BuildInfo.create device data
        let sizes = BuildInfo.querySizes info
        let resultBuffer = createResultBuffer device sizes
        let scratchBuffer = createScratchBuffer device sizes
        let accelerationStructure = createHandle device data resultBuffer scratchBuffer
    
        device.perform {
            do! Command.Build(accelerationStructure, info, false)
        }
    
        accelerationStructure

    /// Attempts to update the given acceleration structure with the given data.
    /// Returns true on success, false otherwise.
    let tryUpdate (data : AccelerationStructureData) (accelerationStructure : AccelerationStructure) =

        // Determine if we can update with new data
        // See: https://www.khronos.org/registry/vulkan/specs/1.2-extensions/man/html/vkGetAccelerationStructureBuildSizesKHR.html
        let isIndexDataCompatible (n : Option<IndexData>) (o : Option<IndexData>) =
            Option.map (fun d -> d.Type) n = Option.map (fun d -> d.Type) o

        let isGeometryCompatible (n : GeometryData) (o : GeometryData) =
            match n, o with
            | GeometryData.Triangles (nv, ni, _), GeometryData.Triangles (ov, oi, _) ->
                nv.Count <= ov.Count &&
                nv.Format = ov.Format &&
                isIndexDataCompatible ni oi

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
            use info = BuildInfo.create accelerationStructure.Device data
            accelerationStructure.Device.perform {
                do! Command.Build(accelerationStructure, info, true)
            }

            true
        else
            false