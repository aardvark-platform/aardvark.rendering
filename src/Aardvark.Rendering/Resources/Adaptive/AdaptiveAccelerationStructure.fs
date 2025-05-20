namespace Aardvark.Rendering.Raytracing

open Aardvark.Rendering
open FSharp.Data.Adaptive
open System.Runtime.CompilerServices

type IAdaptiveAccelerationStructure =
    inherit IAdaptiveResource<IAccelerationStructure>
    abstract member Runtime : IAccelerationStructureRuntime
    abstract member Geometry : aval<TraceGeometry>
    abstract member Usage : AccelerationStructureUsage

type internal AdaptiveAccelerationStructure(runtime : IAccelerationStructureRuntime, geometry : aval<TraceGeometry>, usage : AccelerationStructureUsage) =
    inherit AdaptiveResource<IAccelerationStructure>()

    let mutable handle : Option<IAccelerationStructure> = None

    let create (data : TraceGeometry) =
        let accel = runtime.CreateAccelerationStructure(data, usage, true)
        handle <- Some accel
        accel

    override x.Create() = ()
    override x.Destroy() =
        match handle with
        | Some h ->
            h.Dispose()
            handle <- None
        | None ->
            ()

    override x.Compute(t : AdaptiveToken, rt : RenderToken) =
        let data = geometry.GetValue(t)

        match handle with
        | Some h ->
            if runtime.TryUpdateAccelerationStructure(h, data) then
                rt.InPlaceResourceUpdate(ResourceKind.AccelerationStructure)
                h
            else
                rt.ReplacedResource(ResourceKind.AccelerationStructure)
                h.Dispose()
                create data

        | None ->
            rt.CreatedResource(ResourceKind.AccelerationStructure)
            create data

    interface IAdaptiveAccelerationStructure with
        member x.Runtime = runtime
        member x.Geometry = geometry
        member x.Usage = usage


[<AbstractClass; Sealed; Extension>]
type IAccelerationStructureRuntimeAdaptiveExtensions private() =

    [<Extension>]
    static member CreateAccelerationStructure(this : IAccelerationStructureRuntime, geometry : aval<TraceGeometry>, usage : AccelerationStructureUsage) =
        AdaptiveAccelerationStructure(this, geometry, usage) :> IAdaptiveAccelerationStructure

    [<Extension>]
    static member CreateAccelerationStructure(this : IAccelerationStructureRuntime, geometry : aval<TraceGeometry>) =
        this.CreateAccelerationStructure(geometry, AccelerationStructureUsage.Static)