namespace Aardvark.Rendering.Raytracing

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open System

[<Struct>]
type VisibilityMask =
    val mutable Value : uint8

    new(value : uint8)  = { Value = value }
    new(value : int8)   = VisibilityMask(uint8 value)
    new(value : uint32) = VisibilityMask(uint8 value)
    new(value : int32)  = VisibilityMask(uint8 value)
    new(enabled : bool) = VisibilityMask(if enabled then Byte.MaxValue else 0uy)

    static member op_Explicit (m : VisibilityMask) : uint8 = m.Value

    static member All  = VisibilityMask(true)
    static member None = VisibilityMask(false)

[<RequireQualifiedAccess>]
type CullMode =
    /// No face culling.
    | Disabled

    /// Cull all back-facing triangles.
    | Enabled of front: WindingOrder

[<RequireQualifiedAccess>]
type GeometryMode =
    /// Do not override individual geometry flags.
    | Default

    /// Treat all geometries as if GeometryFlags.Opaque was specified.
    | Opaque

    /// Treat all geometries as if GeometryFlags.Opaque was not specified.
    | Transparent


type HitConfig = List<Symbol>

type TraceObject =
    {
        Geometry     : aval<IAccelerationStructure>
        HitGroups    : aval<HitConfig>
        Transform    : aval<Trafo3d>
        Culling      : aval<CullMode>
        GeometryMode : aval<GeometryMode>
        Mask         : aval<VisibilityMask>
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module TraceObject =

    let create (geometry : aval<IAccelerationStructure>) =
        { Geometry     = geometry
          HitGroups    = AVal.constant []
          Transform    = AVal.constant Trafo3d.Identity
          Culling      = AVal.constant CullMode.Disabled
          GeometryMode = AVal.constant GeometryMode.Default
          Mask         = AVal.constant VisibilityMask.All }

    let create' (geometry : IAccelerationStructure) =
        geometry |> AVal.constant |> create


    let hitGroups (hitConfig : aval<HitConfig>) (obj : TraceObject) =
        { obj with HitGroups = hitConfig }

    let hitGroups' (hitConfig : HitConfig) (obj : TraceObject) =
        obj |> hitGroups (AVal.constant hitConfig)

    let hitGroup (group : aval<Symbol>) (obj : TraceObject) =
        let groups = group |> AVal.map List.singleton
        obj  |> hitGroups groups

    let hitGroup' (group : Symbol) (obj : TraceObject) =
        obj |> hitGroups' [group]


    let transform (trafo : aval<Trafo3d>) (obj : TraceObject) =
        { obj with Transform = trafo }

    let transform' (trafo : Trafo3d) (obj : TraceObject) =
        obj |> transform (AVal.constant trafo)


    let culling (mode : aval<CullMode>) (obj : TraceObject) =
        { obj with Culling = mode }

    let culling' (mode : CullMode) (obj : TraceObject) =
        obj |> culling (AVal.constant mode)


    let geometryMode (mode : aval<GeometryMode>) (obj : TraceObject) =
        { obj with GeometryMode = mode }

    let geometryMode' (mode : GeometryMode) (obj : TraceObject) =
        obj |> geometryMode (AVal.constant mode)


    let mask (value : aval<VisibilityMask>) (obj : TraceObject) =
        { obj with Mask = value }

    let mask' (value : VisibilityMask) (obj : TraceObject) =
        obj |> mask (AVal.constant value)


[<AutoOpen>]
module TraceObjectBuilder =

    type GeometryMustBeSpecified = GeometryMustBeSpecified

    type TraceObjectBuilder() =
        member x.Yield(_) = GeometryMustBeSpecified

        [<CustomOperation("geometry")>]
        member x.Geometry(_ : GeometryMustBeSpecified, accelerationStructure : aval<IAccelerationStructure>) =
            TraceObject.create accelerationStructure

        member x.Geometry(_ : GeometryMustBeSpecified, accelerationStructure : IAccelerationStructure) =
            TraceObject.create' accelerationStructure

        [<CustomOperation("hitgroups")>]
        member x.HitGroups(o : TraceObject, g : aval<HitConfig>) =
            o |> TraceObject.hitGroups g

        member x.HitGroups(o : TraceObject, g : HitConfig) =
            o |> TraceObject.hitGroups' g

        [<CustomOperation("hitgroup")>]
        member x.HitGroup(o : TraceObject, g : aval<Symbol>) =
            o |> TraceObject.hitGroup g

        member x.HitGroup(o : TraceObject, g : Symbol) =
            o |> TraceObject.hitGroup' g

        [<CustomOperation("transform")>]
        member x.Transform(o : TraceObject, t : aval<Trafo3d>) =
            o |> TraceObject.transform t

        member x.Transform(o : TraceObject, t : Trafo3d) =
            o |> TraceObject.transform' t

        [<CustomOperation("culling")>]
        member x.Culling(o : TraceObject, m : aval<CullMode>) =
            o |> TraceObject.culling m

        member x.Culling(o : TraceObject, m : CullMode) =
            o |> TraceObject.culling' m

        [<CustomOperation("geometryMode")>]
        member x.GeometryMode(o : TraceObject, m : aval<GeometryMode>) =
            o |> TraceObject.geometryMode m

        member x.GeometryMode(o : TraceObject, m : GeometryMode) =
            o |> TraceObject.geometryMode' m

        [<CustomOperation("mask")>]
        member x.Mask(o : TraceObject, m : aval<VisibilityMask>) =
            o |> TraceObject.mask m

        member x.Mask(o : TraceObject, m : VisibilityMask) =
            o |> TraceObject.mask' m

        member x.Run(h : TraceObject) =
            h

    let traceobject = TraceObjectBuilder()


type RaytracingPipelineState =
    {
        Effect            : FShade.RaytracingEffect
        Scenes            : Map<Symbol, amap<TraceObject, int>>
        Uniforms          : IUniformProvider
        MaxRecursionDepth : aval<int>
    }