namespace Aardvark.Rendering.Raytracing

open Aardvark.Base
open Aardvark.Rendering
open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

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

[<Flags>]
type GeometryFlags =
    | None                = 0
    | Opaque              = 1
    | IgnoreDuplicateHits = 2

type TriangleMesh =
    { Vertices   : VertexData
      Indices    : Option<IndexData>
      Transform  : Trafo3d
      Primitives : uint32
      Flags      : GeometryFlags }

type BoundingBoxes =
    { Data  : AABBsData
      Count : uint32
      Flags : GeometryFlags }

[<RequireQualifiedAccess>]
type TraceGeometry =
    | Triangles of TriangleMesh[]
    | AABBs of BoundingBoxes[]

    /// Returns the number of geometry instances.
    member x.Count =
        match x with
        | Triangles arr -> arr.Length
        | AABBs arr -> arr.Length

    /// Returns an array containing the primitive count for
    /// each geometry instance.
    member x.Primitives =
        match x with
        | Triangles arr -> arr |> Array.map (fun mesh -> mesh.Primitives)
        | AABBs arr -> arr |> Array.map (fun bb -> bb.Count)

[<RequireQualifiedAccess>]
type AccelerationStructureUsage =
    /// Favor fast tracing over fast building.
    | Static

    /// Favor fast building over fast tracing.
    | Dynamic

type IAccelerationStructure =
    inherit IDisposable

    abstract member Usage : AccelerationStructureUsage
    abstract member GeometryCount : int

type IAccelerationStructureRuntime =
    abstract member CreateAccelerationStructure : geometry: TraceGeometry * usage: AccelerationStructureUsage * allowUpdate: bool -> IAccelerationStructure
    abstract member TryUpdateAccelerationStructure : handle: IAccelerationStructure * geometry: TraceGeometry -> bool


[<Extension>]
type AccelerationStructureRuntimeExtensions() =

    [<Extension>]
    static member CreateAccelerationStructure(this : IAccelerationStructureRuntime, geometry : TraceGeometry, usage : AccelerationStructureUsage) =
        this.CreateAccelerationStructure(geometry, usage, true)

    [<Extension>]
    static member CreateAccelerationStructure(this : IAccelerationStructureRuntime, geometry : TraceGeometry,
                                              [<Optional; DefaultParameterValue(true)>] allowUpdate : bool) =
        this.CreateAccelerationStructure(geometry, AccelerationStructureUsage.Static, allowUpdate)