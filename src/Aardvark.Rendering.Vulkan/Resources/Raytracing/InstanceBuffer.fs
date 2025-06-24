namespace Aardvark.Rendering.Vulkan.Raytracing

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open Aardvark.Rendering.Vulkan
open KHRAccelerationStructure
open CompactBufferImplementation

open FSharp.Data.Adaptive

module internal InstanceBuffer =

    [<AutoOpen>]
    module private Helpers =

        let private getFlags (frontFace : WindingOrder voption) (geometryMode : GeometryMode) =
            let c =
                frontFace |> (function
                    | ValueSome order ->
                        if order = WindingOrder.CounterClockwise then
                            VkGeometryInstanceFlagsKHR.TriangleFrontCounterclockwiseBit
                        else
                            VkGeometryInstanceFlagsKHR.None

                    | _ ->
                        VkGeometryInstanceFlagsKHR.TriangleFacingCullDisableBit |||
                        VkGeometryInstanceFlagsKHR.TriangleFrontCounterclockwiseBit
                )

            let g =
                geometryMode |> (function
                    | GeometryMode.Default -> VkGeometryInstanceFlagsKHR.None
                    | GeometryMode.Opaque  -> VkGeometryInstanceFlagsKHR.ForceOpaqueBit
                    | _                    -> VkGeometryInstanceFlagsKHR.ForceNoOpaqueBit
                )

            c ||| g

        let private getHitGroup (sbt : aval<ShaderBindingTable>) (token : AdaptiveToken) (inst : ITraceInstance) =
            let sbt = sbt.GetValue(token)
            let cfg = inst.HitGroups.GetValue(token)
            let accel = inst.Geometry.GetValue(token)

            if accel.GeometryCount > cfg.Length then
                failwithf "[Raytracing] Object has %d geometries but only %d hit groups" accel.GeometryCount cfg.Length

            uint32 sbt.HitGroupTable.Indices.[cfg]

        let evaluate (sbt : aval<ShaderBindingTable>) (token : AdaptiveToken) (inst : ITraceInstance) =
            let trafo = inst.Transform.GetValue(token)
            let index = inst.CustomIndex.GetValue(token)
            let mask = inst.Mask.GetValue(token)
            let front = inst.FrontFace.GetValue(token)
            let geom = inst.GeometryMode.GetValue(token)
            let accel = inst.Geometry.GetValue(token) |> unbox<AccelerationStructure>

            VkAccelerationStructureInstanceKHR(
                VkTransformMatrixKHR(M34f trafo.Forward),
                index,
                uint32 mask,
                getHitGroup sbt token inst,
                getFlags front geom,
                accel.DeviceAddress
            )

        let acquire (inst : ITraceInstance) =
            inst.Geometry.Acquire()

        let release (inst : ITraceInstance) =
            inst.Geometry.Release()

    let create (device : Device) (sbt : aval<ShaderBindingTable>) (instances : aset<ITraceInstance>) : ICompactBuffer =
        { new AdaptiveCompactBuffer<_, _>(
                device.Runtime, evaluate sbt, acquire, release, instances,
                BufferUsage.Write ||| BufferUsage.AccelerationStructure,
                BufferStorage.Host
            ) with

            // VUID-vkCmdBuildAccelerationStructuresKHR-pInfos-03715
            override x.CreateHandle(size, usage, storage) =
                let usage = VkBufferUsageFlags.ofBufferUsage usage
                let memory = if storage = BufferStorage.Device then device.DeviceMemory else device.HostMemory
                memory.CreateBuffer(usage, size, 16UL)
        }