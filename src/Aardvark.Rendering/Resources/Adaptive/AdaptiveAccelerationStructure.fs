﻿namespace Aardvark.Rendering.Raytracing

open Aardvark.Rendering
open FSharp.Data.Adaptive
open System.Runtime.CompilerServices

type AdaptiveAccelerationStructure(runtime : IAccelerationStructureRuntime, geometry : aval<TraceGeometry>, usage : AccelerationStructureUsage) =
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


[<AbstractClass; Sealed; Extension>]
type IAccelerationStructureRuntimeAdaptiveExtensions private() =

    [<Extension>]
    static member CreateAccelerationStructure(this : IAccelerationStructureRuntime, geometry : aval<TraceGeometry>, usage : AccelerationStructureUsage) =
        AdaptiveAccelerationStructure(this, geometry, usage) :> IAdaptiveResource<_>

    [<Extension>]
    static member CreateAccelerationStructure(this : IAccelerationStructureRuntime, geometry : aval<TraceGeometry>) =
        this.CreateAccelerationStructure(geometry, AccelerationStructureUsage.Static)