namespace Aardvark.Rendering.Raytracing

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open System
open FShade

type AABBsData =
    { Buffer : IBuffer
      Offset : uint64
      Stride : uint64 }

type VertexData =
    { Buffer : IBuffer
      Count  : uint32
      Offset : uint64
      Stride : uint64 }

type IndexData =
    { Buffer : IBuffer
      Offset : uint64 }

[<RequireQualifiedAccess>]
type GeometryData =
    | AABBs     of data : AABBsData
    | Triangles of vertexData : VertexData * indexData : option<IndexData> * transform : Trafo3d

[<Flags>]
type GeometryFlags =
    | None                = 0
    | Opaque              = 1
    | IgnoreDuplicateHits = 2

type Geometry(data, primitives, hitGroup, flags) =
    member x.Data       : GeometryData  = data
    member x.Primitives : uint32        = primitives
    member x.HitGroup   : Symbol        = hitGroup
    member x.Flags      : GeometryFlags = flags

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

type Instance(geometry, transform, culling, geometryMode, mask) =
    member x.Geometry     : aval<Geometry[]>   = geometry
    member x.Transform    : aval<Trafo3d>      = transform
    member x.Culling      : aval<CullMode>     = culling
    member x.GeometryMode : aval<GeometryMode> = geometryMode
    member x.Mask         : aval<InstanceMask> = mask

type PipelineState =
    {
        Effect            : RaytracingEffect
        Scenes            : amap<Symbol, amap<Instance, int>>
        Uniforms          : IUniformProvider
        MaxRecursionDepth : aval<int>
    }