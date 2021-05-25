namespace Aardvark.Rendering.Raytracing

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open System
open FShade

type GeometryData =
    | AABBs     of buffer : BufferView
    | Triangles of vertexBuffer: BufferView * indexBuffer : option<BufferView>

type GeometryFlags =
    | None                = 0
    | Opaque              = 1
    | IgnoreDuplicateHits = 2

type Geometry(data, hitGroup, flags) =
    member x.Data     : GeometryData  = data
    member x.HitGroup : Symbol        = hitGroup
    member x.Flags    : GeometryFlags = flags

[<Struct>]
type InstanceMask =
    val mutable Value : uint8

    new(value : uint8)  = { Value = value }
    new(value : int8)   = InstanceMask(uint8 value)
    new(value : uint32) = InstanceMask(uint8 value)
    new(value : int32)  = InstanceMask(uint8 value)
    new(enabled : bool) = InstanceMask(if enabled then Byte.MaxValue else 0uy)

    static member op_Explicit (m : InstanceMask) : uint8 = m.Value

    static member All  = InstanceMask(true)
    static member None = InstanceMask(false)

[<RequireQualifiedAccess>]
type CullingMode =
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

type Instance(geometry, transform, culling, geometryMode, mask) =
    member x.Geometry     : aset<Geometry>        = geometry
    member x.Transform    : aval<Trafo3d>         = transform
    member x.Culling      : aval<CullingMode>     = culling
    member x.GeometryMode : aval<GeometryMode>    = geometryMode
    member x.Mask         : aval<InstanceMask>    = mask

type PipelineState =
    {
        Effect      : RaytracingEffect
        Scenes      : amap<Symbol, CompactSet<Instance>>
        Uniforms    : IUniformProvider
    }