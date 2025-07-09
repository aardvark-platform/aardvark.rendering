namespace Aardvark.Rendering.Vulkan.Raytracing

#nowarn "9"

open System
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open Aardvark.Rendering.Vulkan
open KHRAccelerationStructure
open KHRBufferDeviceAddress
open KHRRayTracingPositionFetch

[<Struct>]
type Instances =
    { Buffer : Buffer
      Count : uint32 }

[<RequireQualifiedAccess>]
type AccelerationStructureData =
    | Geometry of TraceGeometry
    | Instances of Instances

    member x.Count =
        match x with
        | Geometry arr -> uint32 arr.Count
        | Instances inst -> inst.Count

type private NativeAccelerationStructureData(typ : VkAccelerationStructureTypeKHR, data : VkAccelerationStructureGeometryKHR[],
                                             primitives : uint32[], flags : VkBuildAccelerationStructureFlagsKHR,
                                             buffers : Buffer[]) =
    let gc = GCHandle.Alloc(data, GCHandleType.Pinned)
    let pData = NativePtr.ofNativeInt<VkAccelerationStructureGeometryKHR> <| gc.AddrOfPinnedObject()

    let geometryInfo =
        let mutable info = VkAccelerationStructureBuildGeometryInfoKHR.Empty
        info._type <- typ
        info.flags <- flags
        info.geometryCount <- uint32 data.Length
        info.pGeometries <- pData
        info

    let buildRangeInfos =
        primitives |> Array.map (fun count ->
            VkAccelerationStructureBuildRangeInfoKHR(count, 0u, 0u, 0u)
        )

    member x.Primitives = primitives
    member x.GeometryInfo = geometryInfo
    member x.BuildRangeInfos = buildRangeInfos

    member x.Dispose() =
        buffers |> Array.iter Disposable.dispose
        gc.Free()

    interface IDisposable with
        member x.Dispose() = x.Dispose()


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module private NativeAccelerationStructureData =

    open System.Collections.Generic

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module private Buffer =

        let private usage =
            VkBufferUsageFlags.TransferDstBit |||
            VkBufferUsageFlags.ShaderDeviceAddressBitKhr |||
            VkBufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKhr

        let toAddress (offset : uint64) (buffer : Buffer) =
            VkDeviceOrHostAddressConstKHR.DeviceAddress(
                 buffer.DeviceAddress + offset
             )

        let prepare (device : Device) (alignment : uint64) (buffer : IBuffer) =
            Buffer.ofBuffer' false usage alignment buffer device.DeviceMemory

    let private getFlags (positionFetch : bool) (allowUpdate : bool) (usage : AccelerationStructureUsage) =
        let hint =
            match usage with
            | AccelerationStructureUsage.Static -> VkBuildAccelerationStructureFlagsKHR.PreferFastTraceBit
            | _                                 -> VkBuildAccelerationStructureFlagsKHR.PreferFastBuildBit

        let update =
            if allowUpdate then
                VkBuildAccelerationStructureFlagsKHR.AllowUpdateBit
            else
                VkBuildAccelerationStructureFlagsKHR.None

        let positionFetch =
            if positionFetch then
                VkBuildAccelerationStructureFlagsKHR.AllowDataAccess
            else
                VkBuildAccelerationStructureFlagsKHR.None

        update ||| hint ||| positionFetch

    let private ofGeometry (device : Device) (allowUpdate : bool)
                           (usage : AccelerationStructureUsage) (geometry : TraceGeometry) =

        let mutable buffers = List<Buffer>()

        let getBufferAddress (alignment : uint64) (offset : uint64) (buffer : IBuffer) =
            if alignment <> 0UL && offset % alignment <> 0UL then
                failf $"buffer for acceleration structure must be aligned to {alignment} bytes but the offset is {offset}"

            let prepared = buffer |> Buffer.prepare device alignment
            buffers.Add(prepared)
            prepared |> Buffer.toAddress offset

        let ofAabbData (data : AABBsData) =
            let address =
                // VUID-vkCmdBuildAccelerationStructuresKHR-pInfos-03714
                getBufferAddress 8UL data.Offset data.Buffer

            let stride =
                if data.Stride = 0UL then
                    uint64 sizeof<Box3f>
                else
                    data.Stride

            VkAccelerationStructureGeometryDataKHR.Aabbs(
                VkAccelerationStructureGeometryAabbsDataKHR(address, stride)
            )

        let ofTriangleData (vertexData : VertexData) (indexData : IndexData) (transform : Trafo3d) =
            let vertexDataAddress =
                // VUID-vkCmdBuildAccelerationStructuresKHR-pInfos-03711
                getBufferAddress 4UL vertexData.Offset vertexData.Buffer

            let vertexStride =
                if vertexData.Stride = 0UL then
                    uint64 sizeof<V3f>
                else
                    vertexData.Stride

            let indexType =
                if isNull indexData then VkIndexType.NoneKhr
                else
                    match indexData.Type with
                    | IndexType.Int16 | IndexType.UInt16 -> VkIndexType.Uint16
                    | _ -> VkIndexType.Uint32

            let indexDataAddress =
                if isNull indexData then VkDeviceOrHostAddressConstKHR()
                else
                    // VUID-vkCmdBuildAccelerationStructuresKHR-pInfos-03712
                    let alignment = if indexType = VkIndexType.Uint16 then 2UL else 4UL
                    getBufferAddress alignment indexData.Offset indexData.Buffer

            let transformDataAddress =
                // VUID-vkCmdBuildAccelerationStructuresKHR-pInfos-03810
                let buffer = ArrayBuffer([| VkTransformMatrixKHR(M34f transform.Forward) |])
                getBufferAddress 16UL 0UL buffer

            VkAccelerationStructureGeometryDataKHR.Triangles(
                VkAccelerationStructureGeometryTrianglesDataKHR(
                    VkFormat.R32g32b32Sfloat, vertexDataAddress, vertexStride, vertexData.Count - 1u,
                    indexType, indexDataAddress, transformDataAddress
                )
            )

        let geometries, positionFetch =
            match geometry with
            | TraceGeometry.Triangles arr ->
                arr |> Array.map (fun mesh ->
                    let data = ofTriangleData mesh.Vertices mesh.Indices mesh.Transform
                    VkAccelerationStructureGeometryKHR(VkGeometryTypeKHR.Triangles, data, enum <| int mesh.Flags)
                ), device.EnabledFeatures.Raytracing.PositionFetch

            | TraceGeometry.AABBs arr ->
                arr |> Array.map (fun bb ->
                    let data = ofAabbData bb.Data
                    VkAccelerationStructureGeometryKHR(VkGeometryTypeKHR.Aabbs, data, enum <| int bb.Flags)
                ), false

        let flags = getFlags positionFetch allowUpdate usage

        new NativeAccelerationStructureData(
            VkAccelerationStructureTypeKHR.BottomLevel, geometries, geometry.Primitives, flags, buffers.ToArray()
        )

    let private ofInstances (allowUpdate : bool) (usage : AccelerationStructureUsage) (instances : Instances) =
        // VUID-vkCmdBuildAccelerationStructuresKHR-pInfos-03715
        if instances.Buffer.DeviceAddress % 16UL <> 0UL then
            failf $"instance buffers must be aligned to 16 bytes (address = {instances.Buffer.DeviceAddress})"

        let geometry =
            VkAccelerationStructureGeometryKHR(
                VkGeometryTypeKHR.Instances,
                VkAccelerationStructureGeometryDataKHR.Instances(
                    VkAccelerationStructureGeometryInstancesDataKHR(VkFalse, instances.Buffer |> Buffer.toAddress 0UL)
                ),
                VkGeometryFlagsKHR.None
            )

        let flags = getFlags false allowUpdate usage

        new NativeAccelerationStructureData(
            VkAccelerationStructureTypeKHR.TopLevel, [| geometry |], [| instances.Count |], flags, Array.empty
        )


    let alloc device allowUpdate usage = function
        | AccelerationStructureData.Geometry data -> ofGeometry device allowUpdate usage data
        | AccelerationStructureData.Instances data -> ofInstances allowUpdate usage data

    let createBuffers (device : Device) (data : NativeAccelerationStructureData) =
        let sizes =
            native {
                let! pSizeInfo = VkAccelerationStructureBuildSizesInfoKHR.Empty
                let! pBuildInfo = data.GeometryInfo
                let! pPrimitiveCounts = data.Primitives

                VkRaw.vkGetAccelerationStructureBuildSizesKHR(
                    device.Handle, VkAccelerationStructureBuildTypeKHR.Device,
                    pBuildInfo, pPrimitiveCounts, pSizeInfo
                )

                return !!pSizeInfo
            }

        let resultBuffer =
            let flags = VkBufferUsageFlags.AccelerationStructureStorageBitKhr ||| VkBufferUsageFlags.ShaderDeviceAddressBitKhr
            device.DeviceMemory |> Buffer.create flags sizes.accelerationStructureSize

        let scratchBuffer =
            let alignment = uint64 device.PhysicalDevice.Limits.Raytracing.Value.MinAccelerationStructureScratchOffsetAlignment
            let flags = VkBufferUsageFlags.StorageBufferBit ||| VkBufferUsageFlags.ShaderDeviceAddressBitKhr
            let size = max sizes.buildScratchSize sizes.updateScratchSize
            device.DeviceMemory |> Buffer.create' false false flags alignment size

        {| ResultBuffer = resultBuffer; ScratchBuffer = scratchBuffer |}