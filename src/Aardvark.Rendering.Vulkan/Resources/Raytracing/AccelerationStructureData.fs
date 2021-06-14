namespace Aardvark.Rendering.Vulkan.Raytracing

open System.Runtime.CompilerServices

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

//type AccelerationStructureInstance =
//    { Transform           : Trafo3d
//      CustomIndex         : uint32
//      SbtOffset           : uint32
//      Culling             : CullMode
//      GeometryMode        : GeometryMode
//      Mask                : InstanceMask 
//      BottomLevelAddress  : uint64 }

type InstanceData =
    { Buffer : Buffer
      Count : uint32 }

[<RequireQualifiedAccess>]
type AccelerationStructureData =
    | Geometry of Geometry[]
    | Instances of InstanceData

type private NativeAccelerationStructureData(typ : VkAccelerationStructureTypeKHR, data : VkAccelerationStructureGeometryKHR[], primitives : uint32[]) =
    let gc = GCHandle.Alloc(data, GCHandleType.Pinned)
    let pData = NativePtr.ofNativeInt<VkAccelerationStructureGeometryKHR> <| gc.AddrOfPinnedObject()

    let flags =
        VkBuildAccelerationStructureFlagsKHR.PreferFastTraceBit ||| VkBuildAccelerationStructureFlagsKHR.AllowUpdateBit

    let geometryInfo =
        let mutable info = VkAccelerationStructureBuildGeometryInfoKHR.Empty
        info._type <- typ
        info.flags <- flags
        info.geometryCount <- uint32 data.Length
        info.pGeometries <- pData
        info

    member x.Primitives = primitives
    member x.GeometryInfo = geometryInfo

    member x.Dispose() =
        for d in data do
            match d.geometryType with
            //| VkGeometryTypeKHR.Instances ->
            //    let address = d.geometry.instances.data.hostAddress
            //    address |> NativePtr.ofNativeInt<VkAccelerationStructureInstanceKHR> |> NativePtr.free

            | VkGeometryTypeKHR.Triangles ->
                let address = d.geometry.triangles.transformData.hostAddress
                address |> NativePtr.ofNativeInt<VkTransformMatrixKHR> |> NativePtr.free

            | _ ->
                ()

        gc.Free()

    interface IDisposable with
        member x.Dispose() = x.Dispose()


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module private NativeAccelerationStructureData =

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module private NativePtr =

        let toAddress (ptr : nativeptr<'T>) =
            ptr |> NativePtr.toNativeInt |> VkDeviceOrHostAddressConstKHR.HostAddress

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module private Buffer =

        let toAddress (offset : uint64) (buffer : Buffer) =
            VkDeviceOrHostAddressConstKHR.DeviceAddress(
                 buffer.DeviceAddress + offset
             )

        let unbox (buffer : IBuffer) =
            match buffer with
            | :? Buffer as b -> b
            | _ -> failwithf "[AccelerationStructure] Expected buffer of type %A but got %A" typeof<Buffer> buffer

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module private CullMode =
    
        let toFlags = function
            | CullMode.Enabled order ->
                if order = WindingOrder.CounterClockwise then
                    VkGeometryInstanceFlagsKHR.TriangleFrontCounterclockwiseBit
                else
                    VkGeometryInstanceFlagsKHR.None
    
            | CullMode.Disabled ->
                VkGeometryInstanceFlagsKHR.TriangleFacingCullDisableBit
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module private GeometryMode =
    
        let toFlags = function
            | GeometryMode.Default -> VkGeometryInstanceFlagsKHR.None
            | GeometryMode.Opaque -> VkGeometryInstanceFlagsKHR.ForceOpaqueBit
            | GeometryMode.Transparent -> VkGeometryInstanceFlagsKHR.ForceNoOpaqueBit


    let private ofGeometry (data : Geometry[]) =

        let ofAabbData (data : AABBsData) =
            let address =
                data.Buffer |> Buffer.unbox |> Buffer.toAddress data.Offset

            VkAccelerationStructureGeometryDataKHR.Aabbs(
                VkAccelerationStructureGeometryAabbsDataKHR(address, data.Stride)
            )

        let ofTriangleData (vertexData : VertexData) (indexData : Option<IndexData>) (transform : Trafo3d) =
            let vertexDataAddress =
                vertexData.Buffer |> Buffer.unbox |> Buffer.toAddress vertexData.Offset

            let indexDataAddress =
                match indexData with
                | Some data -> data.Buffer |> Buffer.unbox |> Buffer.toAddress data.Offset
                | _ -> VkDeviceOrHostAddressConstKHR()

            let indexType =
                match indexData with
                | Some _ -> VkIndexType.Uint32
                | _ -> VkIndexType.NoneKhr

            let transformData =
                let ptr = NativePtr.alloc 1
                VkTransformMatrixKHR(M34f transform.Forward) |> NativePtr.write ptr
                NativePtr.toAddress ptr

            VkAccelerationStructureGeometryDataKHR.Triangles(
                VkAccelerationStructureGeometryTrianglesDataKHR(
                    VkFormat.R32g32b32Sfloat, vertexDataAddress, vertexData.Stride, vertexData.Count,
                    indexType, indexDataAddress, transformData
                )
            )

        let geometries =
            data |> Array.map (fun g ->
                let typ, data =
                    match g.Data with
                    | GeometryData.AABBs d ->
                        VkGeometryTypeKHR.Aabbs, ofAabbData d

                    | GeometryData.Triangles (vertexData, indexData, transform) ->
                        VkGeometryTypeKHR.Triangles, ofTriangleData vertexData indexData transform

                VkAccelerationStructureGeometryKHR(typ, data, unbox g.Flags)
            )
        
        let primitives =
            data |> Array.map (fun g -> g.Primitives)

        new NativeAccelerationStructureData(
            VkAccelerationStructureTypeKHR.BottomLevel, geometries, primitives
        )


    //let private ofInstances (data : AccelerationStructureInstance[]) =
        //let pInstances = NativePtr.alloc data.Length

        //data |> Array.iteri (fun i inst ->
        //    let flags =
        //        CullMode.toFlags inst.Culling ||| GeometryMode.toFlags inst.GeometryMode

        //    let inst =
        //        VkAccelerationStructureInstanceKHR(
        //            VkTransformMatrixKHR(M34f inst.Transform.Forward),
        //            uint24 inst.CustomIndex, uint8 inst.Mask, uint24 inst.SbtOffset, uint8 flags,
        //            inst.BottomLevelAddress
        //        )

        //    inst |> NativePtr.set pInstances i
        //)

    let private ofInstances (buffer : InstanceData) =
        let geometry =
            VkAccelerationStructureGeometryKHR(
                VkGeometryTypeKHR.Instances,
                VkAccelerationStructureGeometryDataKHR.Instances(
                    VkAccelerationStructureGeometryInstancesDataKHR(0u, buffer.Buffer |> Buffer.toAddress 0UL)
                ),
                VkGeometryFlagsKHR.None
            )

        new NativeAccelerationStructureData(
            VkAccelerationStructureTypeKHR.TopLevel, [| geometry |], [| buffer.Count |]
        )


    let alloc = function
        | AccelerationStructureData.Geometry data -> ofGeometry data
        | AccelerationStructureData.Instances data -> ofInstances data

    let createBuffers (device : Device) (data : NativeAccelerationStructureData) =
        let sizes =
            native {
                let! pSizeInfo = Unchecked.defaultof<_>
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