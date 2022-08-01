namespace Aardvark.Rendering.Vulkan.Raytracing

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open Aardvark.Rendering.Vulkan
open Aardvark.Rendering.Vulkan.KHRAccelerationStructure

open FSharp.Data.Adaptive

module internal InstanceBuffer =

    [<AutoOpen>]
    module private Helpers =

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
                    | GeometryMode.Default -> VkGeometryInstanceFlagsKHR.None
                    | GeometryMode.Opaque  -> VkGeometryInstanceFlagsKHR.ForceOpaqueBit
                    | _                    -> VkGeometryInstanceFlagsKHR.ForceNoOpaqueBit
                )

            uint8 (c ||| g)

        let private getHitGroup (sbt : aval<ShaderBindingTable>) (token : AdaptiveToken) (inst : ITraceInstance) =
            let sbt = sbt.GetValue(token)
            let cfg = inst.HitGroups.GetValue(token)
            let accel = inst.Geometry.GetValue(token)

            if accel.GeometryCount > cfg.Length then
                failwithf "[Raytracing] Object has %d geometries but only %d hit groups" accel.GeometryCount cfg.Length

            uint24 sbt.HitGroupTable.Indices.[cfg]

        let evaluate (sbt : aval<ShaderBindingTable>) (token : AdaptiveToken) (inst : ITraceInstance) =
            let trafo = inst.Transform.GetValue(token)
            let index = inst.CustomIndex.GetValue(token)
            let mask = inst.Mask.GetValue(token)
            let cull = inst.Culling.GetValue(token)
            let geom = inst.GeometryMode.GetValue(token)
            let accel = inst.Geometry.GetValue(token) |> unbox<AccelerationStructure>

            VkAccelerationStructureInstanceKHR(
                VkTransformMatrixKHR(M34f trafo.Forward),
                uint24 index,
                uint8 mask,
                getHitGroup sbt token inst,
                getFlags cull geom,
                accel.DeviceAddress
            )

        let acquire (inst : ITraceInstance) =
            inst.Geometry.Acquire()

        let release (inst : ITraceInstance) =
            inst.Geometry.Release()

    let create (runtime : IRuntime) (sbt : aval<ShaderBindingTable>) (instances : aset<ITraceInstance>) =
        runtime.CreateCompactBuffer(
            evaluate sbt, acquire, release, instances,
            BufferUsage.Write ||| BufferUsage.AccelerationStructure
        )