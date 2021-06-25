namespace Aardvark.Rendering.Vulkan.Raytracing

#nowarn "9"

open System
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open Aardvark.Rendering.Vulkan
open Aardvark.Rendering.Vulkan.KHRAccelerationStructure
open Aardvark.Rendering.Vulkan.KHRBufferDeviceAddress

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

        let prepare (device : Device) (buffer : IBuffer) =
            device |> Buffer.ofBuffer usage buffer

    let private getFlags (allowUpdate : bool) (usage : AccelerationStructureUsage) =
        let hint =
            match usage with
            | AccelerationStructureUsage.Static -> VkBuildAccelerationStructureFlagsKHR.PreferFastTraceBit
            | AccelerationStructureUsage.Dynamic -> VkBuildAccelerationStructureFlagsKHR.PreferFastBuildBit

        let update =
            if allowUpdate then
                VkBuildAccelerationStructureFlagsKHR.AllowUpdateBit
            else
                VkBuildAccelerationStructureFlagsKHR.None

        update ||| hint

    let private ofGeometry (device : Device) (allowUpdate : bool) (usage : AccelerationStructureUsage) (geometry : TraceGeometry) =

        let mutable buffers = List<Buffer>()

        let getBufferAddress (offset : uint64) (buffer : IBuffer) =
            let prepared = buffer |> Buffer.prepare device
            buffers.Add(prepared)
            prepared |> Buffer.toAddress offset

        let ofAabbData (data : AABBsData) =
            let address =
                getBufferAddress data.Offset data.Buffer

            VkAccelerationStructureGeometryDataKHR.Aabbs(
                VkAccelerationStructureGeometryAabbsDataKHR(address, data.Stride)
            )

        let ofTriangleData (vertexData : VertexData) (indexData : Option<IndexData>) (transform : Trafo3d) =
            let vertexDataAddress =
                getBufferAddress vertexData.Offset vertexData.Buffer

            let indexDataAddress =
                match indexData with
                | Some data -> getBufferAddress data.Offset data.Buffer
                | _ -> VkDeviceOrHostAddressConstKHR()

            let indexType =
                match indexData with
                | Some _ -> VkIndexType.Uint32
                | _ -> VkIndexType.NoneKhr

            let transformDataAddress =
                let buffer = ArrayBuffer([| VkTransformMatrixKHR(M34f transform.Forward) |])
                getBufferAddress 0UL buffer

            VkAccelerationStructureGeometryDataKHR.Triangles(
                VkAccelerationStructureGeometryTrianglesDataKHR(
                    VkFormat.R32g32b32Sfloat, vertexDataAddress, vertexData.Stride, vertexData.Count,
                    indexType, indexDataAddress, transformDataAddress
                )
            )

        let geometries =
            match geometry with
            | TraceGeometry.Triangles arr ->
                arr |> Array.map (fun mesh ->
                    let data = ofTriangleData mesh.Vertices mesh.Indices mesh.Transform
                    VkAccelerationStructureGeometryKHR(VkGeometryTypeKHR.Triangles, data, unbox mesh.Flags)
                )

            | TraceGeometry.AABBs arr ->
                arr |> Array.map (fun bb ->
                    let data = ofAabbData bb.Data
                    VkAccelerationStructureGeometryKHR(VkGeometryTypeKHR.Aabbs, data, unbox bb.Flags)
                )

        let flags = getFlags allowUpdate usage

        new NativeAccelerationStructureData(
            VkAccelerationStructureTypeKHR.BottomLevel, geometries, geometry.Primitives, flags, buffers.ToArray()
        )

    let private ofInstances (allowUpdate : bool) (usage : AccelerationStructureUsage) (instances : Instances) =
        let geometry =
            VkAccelerationStructureGeometryKHR(
                VkGeometryTypeKHR.Instances,
                VkAccelerationStructureGeometryDataKHR.Instances(
                    VkAccelerationStructureGeometryInstancesDataKHR(0u, instances.Buffer |> Buffer.toAddress 0UL)
                ),
                VkGeometryFlagsKHR.None
            )

        let flags = getFlags allowUpdate usage

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
            device |> Buffer.alloc flags (int64 sizes.accelerationStructureSize)

        let scratchBuffer =
            let flags = VkBufferUsageFlags.AccelerationStructureStorageBitKhr ||| VkBufferUsageFlags.ShaderDeviceAddressBitKhr
            let size = max sizes.buildScratchSize sizes.updateScratchSize
            device |> Buffer.alloc flags (int64 size)

        {| ResultBuffer = resultBuffer; ScratchBuffer = scratchBuffer |}