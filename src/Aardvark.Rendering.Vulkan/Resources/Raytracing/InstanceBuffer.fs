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

        let writeTransform (token : AdaptiveToken) (dst : nativeint) (inst : ITraceInstance) =
            let trafo = inst.Transform.GetValue(token)
            M34f trafo.Forward |> NativeInt.write dst

        let writeIndex (token : AdaptiveToken) (dst : nativeint) (inst : ITraceInstance) =
            let index = inst.CustomIndex.GetValue(token)
            uint24 index |> NativeInt.write (dst + Offsets.Index)

        let writeMask (token : AdaptiveToken) (dst : nativeint) (inst : ITraceInstance) =
            let mask = inst.Mask.GetValue(token)
            uint8 mask |> NativeInt.write (dst + Offsets.Mask)

        let writeHitGroup (sbt : aval<ShaderBindingTable>) (token : AdaptiveToken) (dst : nativeint) (inst : ITraceInstance) =
            let sbt = sbt.GetValue(token)
            let cfg = inst.HitGroups.GetValue(token)
            let accel = inst.Geometry.GetValue(token)

            if accel.GeometryCount > cfg.Length then
                failwithf "[Raytracing] Object has %d geometries but only %d hit groups" accel.GeometryCount cfg.Length

            let hitg = sbt.HitGroupTable.Indices.[cfg]
            uint24 hitg |> NativeInt.write (dst + Offsets.HitGroup)

        let writeFlags (token : AdaptiveToken) (dst : nativeint) (inst : ITraceInstance) =
            let cull = inst.Culling.GetValue(token)
            let geom = inst.GeometryMode.GetValue(token)
            let flags = getFlags cull geom
            uint8 flags |> NativeInt.write (dst + Offsets.Flags)

        let writeGeometry (token : AdaptiveToken) (dst : nativeint) (inst : ITraceInstance) =
            let accel = inst.Geometry.GetValue(token) |> unbox<AccelerationStructure>
            accel.DeviceAddress |> NativeInt.write (dst + Offsets.Geometry)


type AdaptiveInstanceBuffer(instances : aset<ITraceInstance>, sbt : aval<ShaderBindingTable>) =
    inherit AdaptiveCompactBuffer<ITraceInstance>(instances, Offsets.Stride)

    let writers =
        [| Writers.writeTransform
           Writers.writeIndex
           Writers.writeMask
           Writers.writeHitGroup sbt
           Writers.writeFlags
           Writers.writeGeometry |]
        |> Array.map FSharpFunc<_,_,_,_>.Adapt

    let count = ASet.count instances

    member x.Count = count

    override x.AcquireValue(inst : ITraceInstance) =
        inst.Geometry.Acquire()

    override x.ReleaseValue(inst : ITraceInstance) =
        inst.Geometry.Release()

    override x.WriteValue =
        writers

    override x.Create() =
        sbt.Acquire()
        base.Create()

    override x.Destroy() =
        base.Destroy()
        sbt.Release()