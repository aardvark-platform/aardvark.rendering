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

[<RequireQualifiedAccess>]
type GeometryData =
    | AABBs     of data : AABBsData
    | Triangles of vertexData : VertexData * indexData : option<IndexData> * transform : Trafo3d

[<Flags>]
type GeometryFlags =
    | None                = 0
    | Opaque              = 1
    | IgnoreDuplicateHits = 2

type Geometry(data, primitives, flags) =
    member x.Data       : GeometryData  = data
    member x.Primitives : uint32        = primitives
    member x.Flags      : GeometryFlags = flags

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
    abstract member CreateAccelerationStructure : geometry: Geometry[] * usage: AccelerationStructureUsage * allowUpdate: bool -> IAccelerationStructure
    abstract member TryUpdateAccelerationStructure : handle: IAccelerationStructure * geometry: Geometry[] -> bool


[<Extension>]
type AccelerationStructureRuntimeExtensions() =

    [<Extension>]
    static member CreateAccelerationStructure(this : IAccelerationStructureRuntime, geometry : Geometry[], usage : AccelerationStructureUsage) =
        this.CreateAccelerationStructure(geometry, usage, true)

    [<Extension>]
    static member CreateAccelerationStructure(this : IAccelerationStructureRuntime, geometry : Geometry[],
                                              [<Optional; DefaultParameterValue(true)>] allowUpdate : bool) =
        this.CreateAccelerationStructure(geometry, AccelerationStructureUsage.Static, allowUpdate)

    [<Extension>]
    static member CreateAccelerationStructure(this : IAccelerationStructureRuntime, geometry : List<Geometry>,
                                              [<Optional; DefaultParameterValue(true)>] allowUpdate : bool) =
        this.CreateAccelerationStructure(Array.ofList geometry, AccelerationStructureUsage.Static, allowUpdate)

    [<Extension>]
    static member CreateAccelerationStructure(this : IAccelerationStructureRuntime, geometry : List<Geometry>, usage : AccelerationStructureUsage,
                                              [<Optional; DefaultParameterValue(true)>] allowUpdate : bool) =
        this.CreateAccelerationStructure(Array.ofList geometry, usage, allowUpdate)
