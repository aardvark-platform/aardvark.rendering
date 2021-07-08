namespace Aardvark.Rendering.Vulkan.Raytracing

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open Aardvark.Rendering.Vulkan
open Aardvark.Rendering.Vulkan.KHRAccelerationStructure

open FSharp.Data.Adaptive
open OptimizedClosures

[<AutoOpen>]
module private AdaptiveInstanceBufferInternals =

    module Offsets =
        let Index = nativeint sizeof<VkTransformMatrixKHR>
        let Mask = Index + nativeint sizeof<uint24>
        let HitGroup = Mask + nativeint sizeof<uint8>
        let Flags = HitGroup + nativeint sizeof<uint24>
        let Geometry = Flags + nativeint sizeof<uint8>

        let Stride = sizeof<VkAccelerationStructureInstanceKHR>

    module Writers =
        let private getFlags (cullMode : CullMode) (geometryMode : GeometryMode) =
            let c =
                cullMode |> (function
                    | CullMode.Enabled order ->
                        if order = WindingOrder.CounterClockwise then
                            VkGeometryInstanceFlagsKHR.TriangleFrontCounterclockwiseBit
                        else
                            VkGeometryInstanceFlagsKHR.None

                    | CullMode.Disabled ->
                        VkGeometryInstanceFlagsKHR.TriangleFacingCullDisableBit
                )

            let g =
                geometryMode |> (function
                    | GeometryMode.Default     -> VkGeometryInstanceFlagsKHR.None
                    | GeometryMode.Opaque      -> VkGeometryInstanceFlagsKHR.ForceOpaqueBit
                    | GeometryMode.Transparent -> VkGeometryInstanceFlagsKHR.ForceNoOpaqueBit
                )

            c ||| g

        let writeTransform (token : AdaptiveToken) (dst : nativeint) (o : TraceObject) =
            let trafo = o.Transform.GetValue(token)
            M34f trafo.Forward |> NativeInt.write dst

        let writeIndex (token : AdaptiveToken) (dst : nativeint) (o : TraceObject) =
            let index = o.CustomIndex.GetValue(token)
            uint24 index |> NativeInt.write (dst + Offsets.Index)

        let writeMask (token : AdaptiveToken) (dst : nativeint) (o : TraceObject) =
            let mask = o.Mask.GetValue(token)
            uint8 mask |> NativeInt.write (dst + Offsets.Mask)

        let writeHitGroup (sbt : aval<ShaderBindingTable>) (geometryCount : ref<int>) (token : AdaptiveToken) (dst : nativeint) (o : TraceObject) =
            let sbt = sbt.GetValue(token)
            let cfg = o.HitGroups.GetValue(token)

            if !geometryCount > cfg.Length then
                failwithf "[Raytracing] Object has %d geometries but only %d hit groups" !geometryCount cfg.Length

            let hitg = sbt.HitGroupTable.Indices.[cfg]
            uint24 hitg |> NativeInt.write (dst + Offsets.HitGroup)

        let writeFlags (token : AdaptiveToken) (dst : nativeint) (o : TraceObject) =
            let cull = o.Culling.GetValue(token)
            let geom = o.GeometryMode.GetValue(token)
            let flags = getFlags cull geom
            uint8 flags |> NativeInt.write (dst + Offsets.Flags)

        let writeGeometry (count : ref<int>) (token : AdaptiveToken) (dst : nativeint) (o : TraceObject) =
            let accel = o.Geometry.GetValue(token) |> unbox<AccelerationStructure>
            accel.DeviceAddress |> NativeInt.write (dst + Offsets.Geometry)
            count := accel.GeometryCount


type AdaptiveInstanceBuffer(objects : amap<TraceObject, int>, sbt : aval<ShaderBindingTable>) =
    inherit AdaptiveCompactBuffer<TraceObject>(objects, Offsets.Stride)

    let geometryCount = ref 0

    let writers =
        [| Writers.writeGeometry geometryCount
           Writers.writeTransform
           Writers.writeIndex
           Writers.writeMask
           Writers.writeHitGroup sbt geometryCount
           Writers.writeFlags |]
        |> Array.map FSharpFunc<_,_,_,_>.Adapt

    let count = AMap.count objects

    member x.Count = count

    override x.AcquireValue(o : TraceObject) =
        o.Geometry.Acquire()

    override x.ReleaseValue(o : TraceObject) =
        o.Geometry.Release()

    override x.WriteValue =
        writers

    override x.Create() =
        sbt.Acquire()
        base.Create()

    override x.Destroy() =
        base.Destroy()
        sbt.Release()