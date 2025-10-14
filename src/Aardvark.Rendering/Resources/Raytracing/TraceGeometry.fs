namespace Aardvark.Rendering.Raytracing

open Aardvark.Base
open Aardvark.Base.TypeMeta
open Aardvark.Rendering
open System
open System.Runtime.InteropServices

[<AutoOpen>]
module internal TraceGeometryUtilities =
    module Array =
        let inline validateSubrange (startIndex: int) (count: int) (array: Array) =
            if isNull array then
                raise <| ArgumentNullException(nameof array)

            if startIndex < 0 then
                raise <| ArgumentOutOfRangeException(nameof startIndex, $"Argument 'startIndex' cannot be negative.")

            if startIndex > array.Length then
                raise <| ArgumentOutOfRangeException(nameof startIndex, $"Argument 'startIndex' exceeds array (Length = {array.Length}).")

            if startIndex + count > array.Length then
                raise <| ArgumentOutOfRangeException(nameof count, $"Range [{startIndex}, {startIndex + count - 1}] exceeds array (Length = {array.Length}).")

            if count < 0 then array.Length - startIndex else count

type IndexType =
    | Int16  = 0
    | UInt16 = 1
    | Int32  = 2
    | UInt32 = 3

/// Flags controlling trace properties of geometry.
[<Flags>]
type GeometryFlags =
    | None                = 0

    /// Geometry does not invoke any-hit shaders.
    | Opaque              = 1

    /// Any hit shader may only be invoked once per primitive in the geometry.
    | IgnoreDuplicateHits = 2

/// <summary>
/// Describes a buffer containing <see cref="V3f"/> vertices.
/// </summary>
type VertexData =

    /// Buffer containing the data.
    val Buffer : IBuffer

    /// Number of vertices in the buffer.
    val Count : uint32

    /// Offset in bytes into the buffer.
    val Offset : uint64

    /// Number of bytes between two consecutive vertices.
    val Stride : uint64

    /// <summary>
    /// Creates a new <see cref="VertexData"/> instance.
    /// </summary>
    /// <param name="buffer">Buffer containing the data.</param>
    /// <param name="count">Number of vertices in the buffer.</param>
    /// <param name="offset">Offset in bytes into the buffer.</param>
    /// <param name="stride">Number of bytes between two consecutive vertices.</param>
    new (buffer: IBuffer, count: uint32,
         [<Optional; DefaultParameterValue(0UL)>] offset: uint64,
         [<Optional; DefaultParameterValue(12UL)>] stride: uint64) =
        { Buffer = buffer; Count = count; Offset = offset; Stride = stride }

    /// <summary>
    /// Creates a new <see cref="VertexData"/> instance from an array.
    /// </summary>
    /// <param name="array">Array of vertices.</param>
    /// <param name="startIndex">Index of the first element in the array.</param>
    /// <param name="count">Number of vertices, or -1 for all remaining elements in the array.</param>
    /// <exception cref="ArgumentNullException">if <paramref name="array"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">if the subrange specified by <paramref name="startIndex"/> and <paramref name="count"/> exceeds <paramref name="array"/>.</exception>
    new (array: Array, [<Optional; DefaultParameterValue(0)>] startIndex: int, [<Optional; DefaultParameterValue(-1)>] count: int) =
        let count = array |> Array.validateSubrange startIndex count
        let buffer = ArrayBuffer array
        let stride = uint64 buffer.ElementType.CLRSize
        VertexData(buffer, uint32 count, uint64 startIndex * stride, stride)

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
type IndexData =

    /// Type of the index data.
    val Type : IndexType

    /// Buffer containing the data.
    val Buffer : IBuffer

    /// Number of indices in the buffer.
    val Count : uint32

    /// Offset in bytes into the buffer.
    val Offset : uint64

    /// <summary>
    /// Creates a new <see cref="IndexData"/> instance.
    /// </summary>
    /// <param name="indexType">Type of the index data.</param>
    /// <param name="buffer">Buffer containing the data.</param>
    /// <param name="count">Number of indices in the buffer.</param>
    /// <param name="offset">Offset in bytes into the buffer.</param>
    new (indexType: IndexType, buffer: IBuffer, count: uint32, [<Optional; DefaultParameterValue(0UL)>] offset: uint64) =
        { Type = indexType; Buffer = buffer; Count = count; Offset = offset }

    /// <summary>
    /// Creates a new <see cref="IndexData"/> instance from an array.
    /// </summary>
    /// <param name="array">Array of indices.</param>
    /// <param name="startIndex">Index of the first element in the array.</param>
    /// <param name="count">Number of indices, or -1 for all remaining elements in the array.</param>
    /// <exception cref="ArgumentNullException">if <paramref name="array"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">if the subrange specified by <paramref name="startIndex"/> and <paramref name="count"/> exceeds <paramref name="array"/>.</exception>
    /// <exception cref="NotSupportedException">if the element type of <paramref name="array"/> is not int16, uint16, int32, or uint32.</exception>
    new (array: Array, [<Optional; DefaultParameterValue(0)>] startIndex: int, [<Optional; DefaultParameterValue(-1)>] count: int) =
        let count = array |> Array.validateSubrange startIndex count
        let buffer = ArrayBuffer array

        let indexType =
            match buffer.ElementType with
            | Int16  -> IndexType.Int16
            | UInt16 -> IndexType.UInt16
            | Int32  -> IndexType.Int32
            | UInt32 -> IndexType.UInt32
            | t -> raise <| NotSupportedException($"Unsupported index type '{t}'.")

        IndexData(indexType, buffer, uint32 count, uint64 startIndex * uint64 buffer.ElementType.CLRSize)

    /// <summary>
    /// Creates a new <see cref="IndexData"/> instance from an array.
    /// Returns <c>null</c> if the <paramref name="array"/> is null.
    /// </summary>
    /// <param name="array">Array of indices or <c>null</c>.</param>
    /// <param name="startIndex">Index of the first element in the array.</param>
    /// <param name="count">Number of indices, or -1 for all remaining elements in the array.</param>
    /// <exception cref="ArgumentOutOfRangeException">if the subrange specified by <paramref name="startIndex"/> and <paramref name="count"/> exceeds <paramref name="array"/>.</exception>
    /// <exception cref="NotSupportedException">if the element type of <paramref name="array"/> is not int16, uint16, int32, or uint32.</exception>
    static member inline FromArray(array: Array, [<Optional; DefaultParameterValue(0)>] startIndex: int, [<Optional; DefaultParameterValue(-1)>] count: int) =
        if isNull array then null
        else IndexData(array, startIndex, count)

    member inline private this.Equals(other: IndexData) =
        this.Type = other.Type && this.Buffer = other.Buffer && this.Count = other.Count && this.Offset = other.Offset

    override this.Equals(obj: obj) =
        match obj with
        | :? IndexData as other -> this.Equals other
        | _ -> false

    override this.GetHashCode() =
        HashCode.Combine(this.Type.GetHashCode(), this.Buffer.GetHashCode(), this.Offset.GetHashCode())

    interface IEquatable<IndexData> with
        member this.Equals other = this.Equals other

/// Trace geometry described by a list of triangles.
type TriangleMesh =

    /// Vertices of the mesh.
    val Vertices : VertexData

    /// <summary>
    /// Indices of the mesh or <c>null</c> if not indexed.
    /// </summary>
    val Indices : IndexData

    /// <summary>
    /// Micromap of the mesh (can be <c>null</c>).
    /// </summary>
    val Micromap : IMicromap

    /// Transformation to apply to the mesh.
    val Transform : Trafo3d

    /// Geometry flags of the mesh.
    val Flags : GeometryFlags

    /// Returns the effective number of vertices in the mesh (i.e. the index count if indexed and the vertex count if non-indexed).
    member inline this.FaceVertexCount =
        if isNull this.Indices then this.Vertices.Count
        else this.Indices.Count

    /// <summary>
    /// Returns the number of triangles in the mesh, computed as <c>FaceVertexCount / 3</c>.
    /// </summary>
    member inline this.Primitives = this.FaceVertexCount / 3u

    /// Returns whether the mesh is indexed.
    member inline this.IsIndexed = notNull this.Indices

    /// Returns whether the mesh has a micromap.
    member inline this.HasMicromap = notNull this.Micromap

    /// <summary>
    /// Creates a triangle mesh from vertex and index data.
    /// </summary>
    /// <param name="vertices">Vertices of the mesh.</param>
    /// <param name="indices">Indices of the mesh or <c>null</c> if not indexed.</param>
    /// <param name="transform">Transformation to apply to the mesh.</param>
    /// <param name="flags">Geometry flags of the mesh.</param>
    /// <param name="micromap">Micromap of the mesh (can be <c>null</c>).</param>
    new (vertices: VertexData, indices: IndexData, transform: Trafo3d,
         [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags,
         [<Optional; DefaultParameterValue(null : IMicromap)>] micromap: IMicromap) =
        { Vertices = vertices; Indices = indices; Micromap = micromap; Transform = transform; Flags = flags }

    /// <summary>
    /// Creates a triangle mesh from vertex and index data.
    /// </summary>
    /// <param name="vertices">Vertices of the mesh.</param>
    /// <param name="indices">Indices of the mesh or <c>null</c> if not indexed.</param>
    /// <param name="flags">Geometry flags of the mesh.</param>
    /// <param name="micromap">Micromap of the mesh (can be <c>null</c>).</param>
    new (vertices: VertexData, indices: IndexData,
         [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags,
         [<Optional; DefaultParameterValue(null : IMicromap)>] micromap: IMicromap) =
        TriangleMesh(vertices, indices, Trafo3d.Identity, flags, micromap)

    /// <summary>
    /// Creates a triangle mesh from vertex and index data.
    /// </summary>
    /// <param name="vertices">Vertices of the mesh.</param>
    /// <param name="indices">Indices of the mesh or <c>null</c> if not indexed.</param>
    /// <param name="transform">Transformation to apply to the mesh.</param>
    /// <param name="flags">Geometry flags of the mesh.</param>
    /// <param name="micromap">Micromap of the mesh (can be <c>null</c>).</param>
    /// <exception cref="NotSupportedException">if the element type of <paramref name="indices"/> is not int16, uint16, int32, or uint32.</exception>
    new (vertices: Array, indices: Array, transform: Trafo3d,
        [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags,
        [<Optional; DefaultParameterValue(null : IMicromap)>] micromap: IMicromap) =
        TriangleMesh(VertexData vertices, IndexData.FromArray indices, transform, flags, micromap)

    /// <summary>
    /// Creates a triangle mesh from vertex and index data.
    /// </summary>
    /// <param name="vertices">Vertices of the mesh.</param>
    /// <param name="indices">Indices of the mesh or <c>null</c> if not indexed.</param>
    /// <param name="flags">Geometry flags of the mesh.</param>
    /// <param name="micromap">Micromap of the mesh (can be <c>null</c>).</param>
    /// <exception cref="NotSupportedException">if the element type of <paramref name="indices"/> is not int16, uint16, int32, or uint32.</exception>
    new (vertices: Array, indices: Array,
        [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags,
        [<Optional; DefaultParameterValue(null : IMicromap)>] micromap: IMicromap) =
        TriangleMesh(vertices, indices, Trafo3d.Identity, flags, micromap)

    /// <summary>
    /// Creates a triangle mesh from the given <see cref="IndexedGeometry"/>.
    /// </summary>
    /// <param name="geometry">Geometry data; must be a triangle list or strip.</param>
    /// <param name="transform">Transformation to apply to the mesh.</param>
    /// <param name="flags">Geometry flags of the mesh.</param>
    /// <param name="micromap">Micromap of the mesh (can be <c>null</c>).</param>
    /// <exception cref="NotSupportedException">if geometry topology is not <see cref="IndexedGeometryMode.TriangleList"/> or <see cref="IndexedGeometryMode.TriangleStrip"/>.</exception>
    static member FromIndexedGeometry(geometry: IndexedGeometry, transform: Trafo3d,
                                      [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags,
                                      [<Optional; DefaultParameterValue(null : IMicromap)>] micromap: IMicromap) =
        let geometry =
            geometry |> IndexedGeometry.toNonStripped

        if geometry.Mode <> IndexedGeometryMode.TriangleList then
            raise <| NotSupportedException($"Unsupported geometry mode: {geometry.Mode}.")

        let vertexData =
            let data = geometry.IndexedAttributes.[DefaultSemantic.Positions]
            VertexData data

        let indexData =
            IndexData.FromArray geometry.IndexArray

        TriangleMesh(vertexData, indexData, transform, flags, micromap)

    /// <summary>
    /// Creates a triangle mesh from the given <see cref="IndexedGeometry"/>.
    /// </summary>
    /// <param name="geometry">Geometry data; must be a triangle list or strip.</param>
    /// <param name="flags">Geometry flags of the mesh.</param>
    /// <param name="micromap">Micromap of the mesh (can be <c>null</c>).</param>
    /// <exception cref="NotSupportedException">if geometry topology is not <see cref="IndexedGeometryMode.TriangleList"/> or <see cref="IndexedGeometryMode.TriangleStrip"/>.</exception>
    static member FromIndexedGeometry(geometry: IndexedGeometry,
                                      [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags,
                                      [<Optional; DefaultParameterValue(null : IMicromap)>] micromap: IMicromap) =
        TriangleMesh.FromIndexedGeometry(geometry, Trafo3d.Identity, flags, micromap)

    member inline private this.Equals(other: TriangleMesh) =
        this.Vertices = other.Vertices && this.Indices = other.Indices && this.Micromap = other.Micromap &&
        this.Transform = other.Transform && this.Flags = other.Flags

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

/// <summary>
/// Describes a buffer containing axis-aligned bounding boxes as <see cref="Box3f"/> values.
/// </summary>
type AABBsData =

    /// Buffer containing the data.
    val Buffer : IBuffer

    /// Offset in bytes into the buffer.
    val Offset : uint64

    /// Number of bytes between two consecutive AABBs.
    val Stride : uint64

    /// <summary>
    /// Creates a new <see cref="AABBsData"/> instance.
    /// </summary>
    /// <param name="buffer">Buffer containing the data.</param>
    /// <param name="offset">Offset in bytes into the buffer.</param>
    /// <param name="stride">Number of bytes between two consecutive AABBs.</param>
    new (buffer: IBuffer, [<Optional; DefaultParameterValue(0UL)>] offset: uint64, [<Optional; DefaultParameterValue(24UL)>] stride: uint64) =
        { Buffer = buffer; Offset = offset; Stride = stride }

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

/// Trace geometry represented by axis-aligned bounding boxes.
type BoundingBoxes =

    /// Bounding box data.
    val Data : AABBsData

    /// Number of bounding boxes.
    val Count : uint32

    /// Geometry flags of the bounding boxes.
    val Flags : GeometryFlags

    /// <summary>
    /// Creates a new <see cref="BoundingBoxes"/> instance.
    /// </summary>
    /// <param name="data">Bounding box data.</param>
    /// <param name="count">Number of bounding boxes.</param>
    /// <param name="flags">Geometry flags of the bounding boxes.</param>
    new (data: AABBsData, count: uint32, [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags) =
        { Data = data; Count = count; Flags = flags }

    /// <summary>
    /// Creates a new <see cref="BoundingBoxes"/> instance from the given <see cref="Box3f"/> array.
    /// </summary>
    /// <param name="array">Array of axis-aligned bounding boxes.</param>
    /// <param name="startIndex">Index of the first element in the array.</param>
    /// <param name="count">Number of bounding boxes, or -1 for all remaining elements in the array.</param>
    /// <param name="flags">Geometry flags of the bounding boxes.</param>
    /// <exception cref="ArgumentNullException">if <paramref name="array"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">if the subrange specified by <paramref name="startIndex"/> and <paramref name="count"/> exceeds <paramref name="array"/>.</exception>
    new (array: Box3f[],
         [<Optional; DefaultParameterValue(0)>] startIndex: int,
         [<Optional; DefaultParameterValue(-1)>] count: int,
         [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags) =
        let count = array |> Array.validateSubrange startIndex count
        let data = AABBsData(ArrayBuffer array, uint64 startIndex * 24UL)
        BoundingBoxes(data, uint32 count, flags)

    /// <summary>
    /// Creates a new <see cref="BoundingBoxes"/> instance from a single <see cref="Box3f"/>.
    /// </summary>
    /// <param name="box">Axis-aligned bounding box.</param>
    /// <param name="flags">Geometry flags of the bounding box.</param>
    new (box: Box3f, [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags) =
        BoundingBoxes([| box |], flags = flags)

    /// <summary>
    /// Creates a new <see cref="BoundingBoxes"/> instance from the given <see cref="Box3d"/> array.
    /// </summary>
    /// <param name="array">Array of axis-aligned bounding boxes.</param>
    /// <param name="startIndex">Index of the first element in the array.</param>
    /// <param name="count">Number of bounding boxes, or -1 for all remaining elements in the array.</param>
    /// <param name="flags">Geometry flags of the bounding boxes.</param>
    /// <exception cref="ArgumentNullException">if <paramref name="array"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">if the subrange specified by <paramref name="startIndex"/> and <paramref name="count"/> exceeds <paramref name="array"/>.</exception>
    new (array: Box3d[],
         [<Optional; DefaultParameterValue(0)>] startIndex: int,
         [<Optional; DefaultParameterValue(-1)>] count: int,
         [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags) =
        BoundingBoxes(array |> Array.map Box3f, startIndex, count, flags)

    /// <summary>
    /// Creates a new <see cref="BoundingBoxes"/> instance from a single <see cref="Box3d"/>.
    /// </summary>
    /// <param name="box">Axis-aligned bounding box.</param>
    /// <param name="flags">Geometry flags of the bounding box.</param>
    new (box: Box3d, [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags) =
        BoundingBoxes([| Box3f box |], flags = flags)

    /// <summary>
    /// Creates a new <see cref="BoundingBoxes"/> instance for a sphere with the given position and radius.
    /// </summary>
    /// <param name="position">Center of the sphere.</param>
    /// <param name="radius">Radius of the sphere.</param>
    /// <param name="flags">Geometry flags of the bounding box.</param>
    static member inline FromCenterAndRadius(position: V3f, radius: float32, [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags) =
        let box = Box3f.FromCenterAndSize(position, V3f(radius * 2.0f))
        BoundingBoxes(box, flags)

    /// <summary>
    /// Creates a new <see cref="BoundingBoxes"/> instance for a sphere with the given position and radius.
    /// </summary>
    /// <param name="position">Center of the sphere.</param>
    /// <param name="radius">Radius of the sphere.</param>
    /// <param name="flags">Geometry flags of the bounding box.</param>
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