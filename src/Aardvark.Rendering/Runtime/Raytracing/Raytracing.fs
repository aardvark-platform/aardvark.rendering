namespace Aardvark.Rendering.Raytracing

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open System
open FShade

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

type TraceObject(geometry, hitGroups, transform, culling, geometryMode, mask) =
    member x.Geometry     : aval<IAccelerationStructure> = geometry
    member x.HitGroups    : aval<HitConfig>              = hitGroups
    member x.Transform    : aval<Trafo3d>                = transform
    member x.Culling      : aval<CullMode>               = culling
    member x.GeometryMode : aval<GeometryMode>           = geometryMode
    member x.Mask         : aval<VisibilityMask>         = mask

type RaytracingPipelineState =
    {
        Effect            : RaytracingEffect
        Scenes            : Map<Symbol, amap<TraceObject, int>>
        Uniforms          : IUniformProvider
        MaxRecursionDepth : aval<int>
    }