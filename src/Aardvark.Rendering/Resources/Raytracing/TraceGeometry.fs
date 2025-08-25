namespace Aardvark.Rendering.Raytracing

open Aardvark.Base
open Aardvark.Base.TypeMeta
open Aardvark.Rendering
open System
open System.Runtime.InteropServices

type IndexType =
    | Int16  = 0
    | UInt16 = 1
    | Int32  = 2
    | UInt32 = 3

/// Flags controlling trace properties of geometry.
[<Flags>]
type GeometryFlags =
    | None                = 0

    /// Geometry does not invoke any hit shaders.
    | Opaque              = 1

    /// Any hit shader may only be invoked once per primitive in the geometry.
    | IgnoreDuplicateHits = 2

/// Describes a buffer containing V3f vertices.
type VertexData(buffer: IBuffer, count: uint32,
                [<Optional; DefaultParameterValue(0UL)>] offset: uint64,
                [<Optional; DefaultParameterValue(12UL)>] stride: uint64) =

    /// Buffer containing the data.
    member val Buffer = buffer

    /// Number of vertices in the buffer.
    member val Count = count

    /// Offset in bytes into the buffer.
    member val Offset = offset

    /// Stride in bytes between each vertex.
    member val Stride = stride

    new (data: Array) =
        let buffer = ArrayBuffer data
        let stride = uint64 buffer.ElementType.CLRSize
        VertexData(buffer, uint32 data.Length, 0UL, stride)

    member inline private this.Equals(other: VertexData) =
        this.Buffer = other.Buffer && this.Count = other.Count && this.Offset = other.Offset && this.Stride = other.Stride

    override this.Equals(obj: obj) =
        match obj with
        | :? VertexData as other -> this.Equals other
        | _ -> false

    override this.GetHashCode() =
        HashCode.Combine(this.Buffer.GetHashCode(), this.Count.GetHashCode(), this.Offset.GetHashCode(), this.Stride.GetHashCode())

    interface IEquatable<VertexData> with
        member this.Equals other = this.Equals other

/// Describes a buffer containing index data.
[<AllowNullLiteral>]
type IndexData(indexType: IndexType, buffer: IBuffer, [<Optional; DefaultParameterValue(0UL)>] offset: uint64) =

    /// The type of the index data.
    member val Type = indexType

    /// Buffer containing the data.
    member val Buffer = buffer

    /// Offset in bytes into the buffer.
    member val Offset = offset

    new (data: Array) =
        let buffer = ArrayBuffer data

        let indexType =
            match buffer.ElementType with
            | Int16  -> IndexType.Int16
            | UInt16 -> IndexType.UInt16
            | Int32  -> IndexType.Int32
            | UInt32 -> IndexType.UInt32
            | t -> raise <| NotSupportedException($"Unsupported index type '{t}'.")

        IndexData(indexType, buffer)

    static member inline FromArray(array: Array) =
        if isNull array then null
        else IndexData array

    member inline private this.Equals(other: IndexData) =
        this.Type = other.Type && this.Buffer = other.Buffer && this.Offset = other.Offset

    override this.Equals(obj: obj) =
        match obj with
        | :? IndexData as other -> this.Equals other
        | _ -> false

    override this.GetHashCode() =
        HashCode.Combine(this.Type.GetHashCode(), this.Buffer.GetHashCode(), this.Offset.GetHashCode())

    interface IEquatable<IndexData> with
        member this.Equals other = this.Equals other

/// Trace geometry described by a triangle mesh.
type TriangleMesh(vertices: VertexData, indices: IndexData, primitives: uint32, transform: Trafo3d,
                  [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags,
                  [<Optional; DefaultParameterValue(null : IMicromap)>] micromap: IMicromap) =

    /// Vertices of the mesh.
    member val Vertices = vertices

    /// Indices of the mesh (or null if not indexed).
    member val Indices = indices

    /// Micromap of the mesh (can be null).
    member val Micromap = micromap

    /// Number of triangles in the mesh.
    member val Primitives = primitives

    /// Transformation to apply on the mesh.
    member val Transform = transform

    /// Geometry flags of the mesh.
    member val Flags = flags

    /// Returns whether the mesh is indexed.
    member inline this.IsIndexed = not <| obj.ReferenceEquals(this.Indices, null)

    /// Returns whether the mesh has a micromap.
    member inline this.HasMicromap = not <| obj.ReferenceEquals(this.Micromap, null)

    new (vertices: Array, indices: Array, transform: Trafo3d,
        [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags,
        [<Optional; DefaultParameterValue(null : IMicromap)>] micromap: IMicromap) =
        let indices, primitives =
            if isNull indices then null, vertices.Length / 3
            else IndexData indices, indices.Length / 3
        TriangleMesh(VertexData vertices, indices, uint32 primitives, transform, flags, micromap)

    static member FromIndexedGeometry(geometry: IndexedGeometry, transform: Trafo3d,
                                      [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags,
                                      [<Optional; DefaultParameterValue(null : IMicromap)>] micromap: IMicromap) =
        let geometry =
            geometry |> IndexedGeometry.toNonStripped

        if geometry.Mode <> IndexedGeometryMode.TriangleList then
            failwithf "Unsupported geometry mode: %A" geometry.Mode

        let vertexData =
            let data = geometry.IndexedAttributes.[DefaultSemantic.Positions]
            VertexData data

        let indexData =
            if geometry.IsIndexed then IndexData geometry.IndexArray
            else null

        let primitiveCount =
            uint32 (geometry.FaceVertexCount / 3)

        TriangleMesh(
            vertexData, indexData, primitiveCount,
            transform, flags, micromap
        )

    static member FromIndexedGeometry(geometry: IndexedGeometry,
                                      [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags,
                                      [<Optional; DefaultParameterValue(null : IMicromap)>] micromap: IMicromap) =
        TriangleMesh.FromIndexedGeometry(geometry, Trafo3d.Identity, flags, micromap)

    member inline private this.Equals(other: TriangleMesh) =
        this.Vertices = other.Vertices && this.Indices = other.Indices && this.Micromap = other.Micromap &&
        this.Primitives = other.Primitives && this.Transform = other.Transform && this.Flags = other.Flags

    override this.Equals(obj: obj) =
        match obj with
        | :? TriangleMesh as other -> this.Equals other
        | _ -> false

    override this.GetHashCode() =
        let indexHash = if this.IsIndexed then this.Indices.GetHashCode() else 0
        let micromapHash = if this.HasMicromap then this.Micromap.GetHashCode() else 0
        HashCode.Combine(this.Vertices.GetHashCode(), indexHash, micromapHash, this.Primitives.GetHashCode(), this.Transform.GetHashCode(), this.Flags.GetHashCode())

    interface IEquatable<TriangleMesh> with
        member this.Equals other = this.Equals other

/// Describes a buffer containing Box3f values.
type AABBsData(buffer: IBuffer, [<Optional; DefaultParameterValue(0UL)>] offset: uint64, [<Optional; DefaultParameterValue(24UL)>] stride: uint64) =

    /// Buffer containing the data.
    member val Buffer = buffer

    /// Offset in bytes into the buffer.
    member val Offset = offset

    /// Stride in bytes between each AABB.
    member val Stride = stride

    member inline private this.Equals(other: AABBsData) =
        this.Buffer = other.Buffer && this.Offset = other.Offset && this.Stride = other.Stride

    override this.Equals(obj: obj) =
        match obj with
        | :? AABBsData as other -> this.Equals other
        | _ -> false

    override this.GetHashCode() =
        HashCode.Combine(this.Buffer.GetHashCode(), this.Offset.GetHashCode(), this.Stride.GetHashCode())

    interface IEquatable<AABBsData> with
        member this.Equals other = this.Equals other

/// Trace geometry described by axis-aligned bounding boxes.
type BoundingBoxes(data: AABBsData, count: uint32, [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags) =

    /// Bounding box data.
    member val Data = data

    /// Number of bounding boxes.
    member val Count = count

    /// Geometry flags of the bounding boxes.
    member val Flags = flags

    new (boxes: Box3f[], [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags) =
        let data = AABBsData(ArrayBuffer boxes, 0UL, uint64 typeof<Box3f>.CLRSize)
        BoundingBoxes(data, uint32 boxes.Length, flags)

    new (box: Box3f, [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags) =
        BoundingBoxes([| box |], flags)

    new (boxes: Box3d[], [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags) =
        BoundingBoxes(boxes |> Array.map Box3f, flags)

    new (box: Box3d, [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags) =
        BoundingBoxes([| Box3f box |], flags)

    static member inline FromCenterAndRadius(position: V3f, radius: float32, [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags) =
        let box = Box3f.FromCenterAndSize(position, V3f(radius * 2.0f))
        BoundingBoxes(box, flags)

    static member inline FromCenterAndRadius(position: V3d, radius: float, [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags) =
        let box = Box3d.FromCenterAndSize(position, V3d(radius * 2.0))
        BoundingBoxes(box, flags)

    member inline private this.Equals(other: BoundingBoxes) =
        this.Data = other.Data && this.Count = other.Count && this.Flags = other.Flags

    override this.Equals(obj: obj) =
        match obj with
        | :? BoundingBoxes as other -> this.Equals other
        | _ -> false

    override this.GetHashCode() =
        HashCode.Combine(this.Data.GetHashCode(), this.Count.GetHashCode(), this.Flags.GetHashCode())

    interface IEquatable<BoundingBoxes> with
        member this.Equals other = this.Equals other

[<RequireQualifiedAccess>]
type TraceGeometry =
    | Triangles of TriangleMesh[]
    | AABBs     of BoundingBoxes[]

    /// Returns the number of geometry instances.
    member this.Count =
        match this with
        | Triangles arr -> arr.Length
        | AABBs arr -> arr.Length

    /// Returns an array containing the primitive count for each geometry instance.
    member this.Primitives =
        match this with
        | Triangles arr -> arr |> Array.map _.Primitives
        | AABBs arr -> arr |> Array.map _.Count