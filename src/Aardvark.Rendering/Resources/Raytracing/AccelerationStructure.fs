namespace Aardvark.Rendering.Raytracing

open Aardvark.Base
open Aardvark.Rendering
open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

/// Describes a buffer containing Box3f values.
[<CLIMutable>]
type AABBsData<'T> =
    {
        /// Buffer containing the data.
        Buffer : 'T

        /// Offset in bytes into the buffer.
        Offset : uint64

        /// Stride in bytes between each AABB.
        Stride : uint64
    }

/// Describes a buffer containing V3f vertices.
[<CLIMutable>]
type VertexData<'T> =
    {
        /// Buffer containing the data.
        Buffer : 'T

        /// Number of vertices in the buffer.
        Count  : uint32

        /// Offset in bytes into the buffer.
        Offset : uint64

        /// Stride in bytes between each vertex.
        Stride : uint64
    }

type IndexType =
    | UInt16 = 0
    | UInt32 = 1

/// Describes a buffer containing index data.
[<CLIMutable>]
type IndexData<'T> =
    {
        /// The type of the index data.
        Type   : IndexType

        /// Buffer containing the data.
        Buffer : 'T

        /// Offset in bytes into the buffer.
        Offset : uint64
    }

/// Flags controlling trace properties of geometry.
[<Flags>]
type GeometryFlags =
    | None                = 0

    /// Geometry does not invoke any hit shaders.
    | Opaque              = 1

    /// Any hit shader may only be invoked once per primitive in the geometry.
    | IgnoreDuplicateHits = 2

/// Trace geometry described by a triangle mesh.
[<CLIMutable>]
type TriangleMesh =
    {
        /// Vertices of the mesh.
        Vertices   : VertexData<IBuffer>

        /// Indices of the mesh.
        Indices    : IndexData<IBuffer> option

        /// Transformation to apply on the mesh.
        Transform  : Trafo3d

        /// Number of triangles in the mesh.
        Primitives : uint32

        /// Geometry flags of the mesh.
        Flags      : GeometryFlags
    }

/// Trace geometry described by axis-aligned bounding boxes.
[<CLIMutable>]
type BoundingBoxes =
    {
        /// Bounding box data.
        Data  : AABBsData<IBuffer>

        /// Number of bounding boxes.
        Count : uint32

        /// Geometry flags of the bounding boxes.
        Flags : GeometryFlags
    }

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

type AccelerationStructureUsage =
    /// Favor fast tracing over fast building.
    | Static = 0

    /// Favor fast building over fast tracing.
    | Dynamic = 1

type IAccelerationStructure =
    inherit IDisposable

    abstract member Usage : AccelerationStructureUsage
    abstract member GeometryCount : int

type IAccelerationStructureRuntime =
    abstract member CreateAccelerationStructure : geometry: TraceGeometry *
                                                  usage: AccelerationStructureUsage *
                                                  allowUpdate: bool -> IAccelerationStructure
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