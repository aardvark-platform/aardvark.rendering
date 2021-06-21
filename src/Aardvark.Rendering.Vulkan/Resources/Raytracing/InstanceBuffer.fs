namespace Aardvark.Rendering.Vulkan.Raytracing

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open Aardvark.Rendering.Vulkan
open Aardvark.Rendering.Vulkan.KHRAccelerationStructure

open FSharp.Data.Adaptive

[<AutoOpen>]
module private AdaptiveInstanceBufferInternals =

    let stride = sizeof<VkAccelerationStructureInstanceKHR>

    let acquireObject (o : TraceObject) =
        o.Geometry.Acquire()

    let releaseObject (o : TraceObject) =
        o.Geometry.Release()

    let getFlags (cullMode : CullMode) (geometryMode : GeometryMode) =
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

    let prepareInstance (sbt : aval<ShaderBindingTable>) (token : AdaptiveToken) (index : int) (o : TraceObject) =
        let sbt = sbt.GetValue(token)
        let accel = o.Geometry.GetValue(token) |> unbox<AccelerationStructure>
        let hitg = sbt.HitGroupTable.Indices.[o.HitGroups.GetValue(token)]
        let trafo = o.Transform.GetValue(token)
        let cull = o.Culling.GetValue(token)
        let geom = o.GeometryMode.GetValue(token)
        let mask = o.Mask.GetValue(token)

        let flags = getFlags cull geom

        VkAccelerationStructureInstanceKHR(
            VkTransformMatrixKHR(M34f trafo.Forward),
            uint24 index, uint8 mask, uint24 hitg, uint8 flags,
            accel.DeviceAddress
        )


type AdaptiveInstanceBuffer(objects : amap<TraceObject, int>, sbt : aval<ShaderBindingTable>) =
    inherit AdaptiveCompactBuffer<TraceObject, VkAccelerationStructureInstanceKHR>(
        objects, stride, acquireObject, releaseObject, prepareInstance sbt, NativeInt.write
    )

    let count = AMap.count objects

    member x.Count = count

    override x.Create() =
        sbt.Acquire()
        base.Acquire()

    override x.Destroy() =
        base.Release()
        sbt.Release()