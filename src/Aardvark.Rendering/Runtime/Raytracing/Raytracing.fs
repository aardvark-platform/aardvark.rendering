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


type RaytracingPipelineState =
    {
        Effect            : FShade.RaytracingEffect
        Scenes            : Map<Symbol, amap<TraceObject, int>>
        Uniforms          : IUniformProvider
        MaxRecursionDepth : aval<int>
    }