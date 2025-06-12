namespace Aardvark.Rendering.Raytracing

open Aardvark.Rendering
open FSharp.Data.Adaptive
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

type IAdaptiveAccelerationStructure =
    inherit IAdaptiveResource<IAccelerationStructure>
    abstract member Runtime : IAccelerationStructureRuntime
    abstract member Geometry : aval<TraceGeometry>
    abstract member Usage : AccelerationStructureUsage
    abstract member Name : string with get, set

type internal AdaptiveAccelerationStructure(runtime : IAccelerationStructureRuntime, geometry : aval<TraceGeometry>, usage : AccelerationStructureUsage) =
    inherit AdaptiveResource<IAccelerationStructure>()

    let mutable handle : Option<IAccelerationStructure> = None
    let mutable name = null

    let create (data : TraceGeometry) =
        let accel = runtime.CreateAccelerationStructure(data, usage, true)
        accel.Name <- name
        handle <- Some accel
        accel

    member x.Name
        with get() = name
        and set value =
            name <- value
            handle |> Option.iter _.set_Name(name)

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
        member x.Name with get() = x.Name and set name = x.Name <- name


[<AbstractClass; Sealed; Extension>]
type IAccelerationStructureRuntimeAdaptiveExtensions private() =

    [<Extension>]
    static member CreateAccelerationStructure(this : IAccelerationStructureRuntime, geometry : aval<TraceGeometry>,
                                              [<Optional; DefaultParameterValue(AccelerationStructureUsage.Static)>] usage : AccelerationStructureUsage) =
        AdaptiveAccelerationStructure(this, geometry, usage) :> IAdaptiveAccelerationStructure