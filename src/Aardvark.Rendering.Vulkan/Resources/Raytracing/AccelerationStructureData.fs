namespace Aardvark.Rendering.Vulkan.Raytracing

open System
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open Aardvark.Rendering.Vulkan
open Aardvark.Rendering.Vulkan.Raytracing
open KHRAccelerationStructure
open KHRBufferDeviceAddress
open KHRRayTracingPositionFetch
open EXTOpacityMicromap

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

// TODO: Replace with PinnedValue from Aardvark.Base >= 5.3.16
[<AutoOpen>]
module internal ArrayPinningUtils =

    /// Utility to pin values with IDisposable semantics.
    type PinnedValue private (value: obj, length: int) =
        let gc = GCHandle.Alloc(value, GCHandleType.Pinned)
        let address = gc.AddrOfPinnedObject()

        new (value: obj) =
            let length = match value with | :? Array as array -> array.Length | _ -> 1
            new PinnedValue(value, length)

        new (array: Array) =
            new PinnedValue(array, array.Length)

        /// The address of the pinned value.
        member _.Address = address

        /// The number of elements if the pinned value is an array, 1 otherwise.
        member _.Length = length

        member _.Dispose() = gc.Free()

        interface IDisposable with
            member this.Dispose() = this.Dispose()

    /// Utility to pin values with IDisposable semantics.
    type PinnedValue<'T when 'T : unmanaged> =
        inherit PinnedValue
        new (value: 'T) = { inherit PinnedValue(value :> obj) }
        new (array: 'T[]) = { inherit PinnedValue(array :> Array) }

        /// The pointer of the pinned value.
        member inline this.Pointer : nativeptr<'T> = NativePtr.ofNativeInt this.Address

type internal PreparedAccelerationStructureData(
                    device      : Device,
                    typ         : VkAccelerationStructureTypeKHR,
                    flags       : VkBuildAccelerationStructureFlagsKHR,
                    geometries  : VkAccelerationStructureGeometryKHR[],
                    primitives  : uint32[],
                    micromaps   : Micromap[],   // Micromaps that are not discardable
                    disposables : IDisposable[]
                ) =
    let pGeometries = new PinnedValue<_>(geometries)

    let mutable buildGeometryInfo =
        VkAccelerationStructureBuildGeometryInfoKHR(
            typ, flags,
            VkBuildAccelerationStructureModeKHR.Build,
            VkAccelerationStructureKHR.Null,
            VkAccelerationStructureKHR.Null,
            uint32 geometries.Length,
            pGeometries.Pointer, NativePtr.zero,
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
    member _.Micromaps = micromaps

    member x.Dispose() =
        disposables |> Array.iter Disposable.dispose
        pGeometries.Dispose()

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
        let disposables = ResizeArray<IDisposable>()
        let micromaps = ResizeArray<Micromap>()

        let getBufferAddress (alignment : uint64) (offset : uint64) (buffer : IBuffer) =
            if alignment <> 0UL && offset % alignment <> 0UL then
                failf $"buffer for acceleration structure must be aligned to {alignment} bytes but the offset is {offset}"

            let prepared = buffer |> Buffer.prepare device alignment
            disposables.Add(prepared)
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

        let ofTriangleData (mesh : TriangleMesh)  =
            let vertexDataAddress =
                // VUID-vkCmdBuildAccelerationStructuresKHR-pInfos-03711
                getBufferAddress 4UL mesh.Vertices.Offset mesh.Vertices.Buffer

            let vertexStride =
                if mesh.Vertices.Stride = 0UL then
                    uint64 sizeof<V3f>
                else
                    mesh.Vertices.Stride

            let indexType =
                if not mesh.IsIndexed then VkIndexType.NoneKhr
                else
                    match mesh.Indices.Type with
                    | IndexType.Int16 | IndexType.UInt16 -> VkIndexType.Uint16
                    | _ -> VkIndexType.Uint32

            let indexDataAddress =
                if not mesh.IsIndexed then VkDeviceOrHostAddressConstKHR()
                else
                    // VUID-vkCmdBuildAccelerationStructuresKHR-pInfos-03712
                    let alignment = if indexType = VkIndexType.Uint16 then 2UL else 4UL
                    getBufferAddress alignment mesh.Indices.Offset mesh.Indices.Buffer

            let pNext =
                if mesh.Micromap = null then 0n
                else
                    let micromap = Micromap.prepare device mesh.Micromap

                    // If the micromap is discardable we can dispose it alongside the PreparedAccelerationStructureData.
                    // Otherwise, we have to keep it alive alongside the actual acceleration structure.
                    if micromap.IsDiscardable then
                        disposables.Add micromap
                    else
                        micromaps.Add micromap

                    let pUsageCounts = new PinnedValue<_>(micromap.UsageCounts)
                    disposables.Add pUsageCounts

                    let trianglesOpacityMicromap =
                        VkAccelerationStructureTrianglesOpacityMicromapEXT(
                            micromap.IndexType, VkDeviceOrHostAddressConstKHR.DeviceAddress micromap.IndexBufferAddress, micromap.IndexStride,
                            0u, uint32 micromap.UsageCounts.Length, NativePtr.cast pUsageCounts.Pointer, NativePtr.zero,
                            micromap.Handle
                        )

                    let pTrianglesOpacityMicromap = new PinnedValue(trianglesOpacityMicromap)
                    disposables.Add pTrianglesOpacityMicromap
                    pTrianglesOpacityMicromap.Address

            let transformDataAddress =
                // VUID-vkCmdBuildAccelerationStructuresKHR-pInfos-03810
                let buffer = ArrayBuffer([| VkTransformMatrixKHR(M34f mesh.Transform.Forward) |])
                getBufferAddress 16UL 0UL buffer

            VkAccelerationStructureGeometryDataKHR.Triangles(
                VkAccelerationStructureGeometryTrianglesDataKHR(
                    pNext, VkFormat.R32g32b32Sfloat, vertexDataAddress, vertexStride, mesh.Vertices.Count - 1u,
                    indexType, indexDataAddress, transformDataAddress
                )
            )

        let geometries, positionFetch =
            match geometry with
            | TraceGeometry.Triangles arr ->
                arr |> Array.map (fun mesh ->
                    let data = ofTriangleData mesh
                    VkAccelerationStructureGeometryKHR(VkGeometryTypeKHR.Triangles, data, enum <| int mesh.Flags)
                ), device.EnabledFeatures.Raytracing.PositionFetch

            | TraceGeometry.AABBs arr ->
                arr |> Array.map (fun bb ->
                    let data = ofAabbData bb.Data
                    VkAccelerationStructureGeometryKHR(VkGeometryTypeKHR.Aabbs, data, enum <| int bb.Flags)
                ), false

        let flags = getFlags positionFetch usage

        new PreparedAccelerationStructureData(
            device, VkAccelerationStructureTypeKHR.BottomLevel, flags, geometries, geometry.Primitives,
            micromaps.ToArray(), disposables.ToArray()
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
            device, VkAccelerationStructureTypeKHR.TopLevel, flags, [| geometry |], [| count |], Array.empty, Array.empty
        )

    let prepare device usage data =
        match data with
        | AccelerationStructureData.Geometry geometry -> ofGeometry device usage geometry
        | AccelerationStructureData.Instances (count, buffer) -> ofInstances device usage count buffer