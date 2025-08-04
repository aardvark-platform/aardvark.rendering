namespace Aardvark.Rendering.Vulkan.Raytracing

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

#nowarn "9"
#nowarn "51"

[<RequireQualifiedAccess>]
type AccelerationStructureData =
    | Geometry of TraceGeometry
    | Instances of count: uint32 * buffer: Buffer

    member this.Type =
        match this with
        | AccelerationStructureData.Geometry _ -> VkAccelerationStructureTypeKHR.BottomLevel
        | AccelerationStructureData.Instances _ -> VkAccelerationStructureTypeKHR.TopLevel

    member this.Count =
        match this with
        | Geometry arr -> uint32 arr.Count
        | Instances (count, _) -> count

type internal PreparedAccelerationStructureData(
                    device     : Device,
                    typ        : VkAccelerationStructureTypeKHR,
                    flags      : VkBuildAccelerationStructureFlagsKHR,
                    geometries : VkAccelerationStructureGeometryKHR[],
                    primitives : uint32[],
                    buffers    : Buffer[]
                ) =
    let pGeometriesHandle = GCHandle.Alloc(geometries, GCHandleType.Pinned)
    let pGeometries = NativePtr.ofNativeInt <| pGeometriesHandle.AddrOfPinnedObject()

    let mutable buildGeometryInfo =
        VkAccelerationStructureBuildGeometryInfoKHR(
            typ, flags,
            VkBuildAccelerationStructureModeKHR.Build,
            VkAccelerationStructureKHR.Null,
            VkAccelerationStructureKHR.Null,
            uint32 geometries.Length,
            pGeometries, NativePtr.zero,
            VkDeviceOrHostAddressKHR()
        )

    let buildRangeInfos =
        primitives |> Array.map (fun count ->
            VkAccelerationStructureBuildRangeInfoKHR(count, 0u, 0u, 0u)
        )

    let mutable buildSizesInfo = ValueNone

    member _.Type = typ
    member _.BuildGeometryInfo = buildGeometryInfo
    member _.BuildRangeInfos = buildRangeInfos
    member _.BuildSizesInfo =
        match buildSizesInfo with
        | ValueSome info -> info
        | _ ->
            use pMaxPrimitiveCounts = fixed primitives
            let mutable info = VkAccelerationStructureBuildSizesInfoKHR.Empty

            VkRaw.vkGetAccelerationStructureBuildSizesKHR(
                device.Handle, VkAccelerationStructureBuildTypeKHR.Device,
                &&buildGeometryInfo, pMaxPrimitiveCounts, &&info
            )

            buildSizesInfo <- ValueSome info
            info

    member inline this.Size = this.BuildSizesInfo.accelerationStructureSize
    member inline this.ScratchBuildSize = this.BuildSizesInfo.buildScratchSize
    member inline this.ScratchUpdateSize = this.BuildSizesInfo.updateScratchSize

    member x.Dispose() =
        buffers |> Array.iter Disposable.dispose
        pGeometriesHandle.Free()

    interface IDisposable with
        member x.Dispose() = x.Dispose()


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal AccelerationStructureData =

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

    let private getFlags (positionFetch : bool) (usage : AccelerationStructureUsage) =
        [
            if usage.HasFlag AccelerationStructureUsage.Dynamic then
                VkBuildAccelerationStructureFlagsKHR.PreferFastBuildBit

            elif usage.HasFlag AccelerationStructureUsage.Static then
                VkBuildAccelerationStructureFlagsKHR.PreferFastTraceBit

            if usage.HasFlag AccelerationStructureUsage.Update then
                VkBuildAccelerationStructureFlagsKHR.AllowUpdateBit

            if usage.HasFlag AccelerationStructureUsage.Compact then
                VkBuildAccelerationStructureFlagsKHR.AllowCompactionBit

            if positionFetch then
                VkBuildAccelerationStructureFlagsKHR.AllowDataAccess
        ]
        |> List.fold (|||) VkBuildAccelerationStructureFlagsKHR.None

    let private ofGeometry (device : Device) (usage : AccelerationStructureUsage) (geometry : TraceGeometry) =
        let mutable buffers = ResizeArray<Buffer>()

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

        let flags = getFlags positionFetch usage

        new PreparedAccelerationStructureData(
            device, VkAccelerationStructureTypeKHR.BottomLevel, flags, geometries, geometry.Primitives, buffers.ToArray()
        )

    let private ofInstances (device : Device) (usage : AccelerationStructureUsage) (count : uint32) (buffer : Buffer) =
        // VUID-vkCmdBuildAccelerationStructuresKHR-pInfos-03715
        if buffer.DeviceAddress % 16UL <> 0UL then
            failf $"instance buffers must be aligned to 16 bytes (address = {buffer.DeviceAddress})"

        let geometry =
            VkAccelerationStructureGeometryKHR(
                VkGeometryTypeKHR.Instances,
                VkAccelerationStructureGeometryDataKHR.Instances(
                    VkAccelerationStructureGeometryInstancesDataKHR(VkFalse, buffer |> Buffer.toAddress 0UL)
                ),
                VkGeometryFlagsKHR.None
            )

        let flags = getFlags false usage

        new PreparedAccelerationStructureData(
            device, VkAccelerationStructureTypeKHR.TopLevel, flags, [| geometry |], [| count |], Array.empty
        )

    let prepare device usage data =
        match data with
        | AccelerationStructureData.Geometry geometry -> ofGeometry device usage geometry
        | AccelerationStructureData.Instances (count, buffer) -> ofInstances device usage count buffer