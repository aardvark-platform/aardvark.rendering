namespace Aardvark.SceneGraph.Raytracing

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open System
open System.Runtime.InteropServices

/// <summary>
/// Describes an adaptive buffer containing <see cref="V3f"/> vertices.
/// </summary>
type AdaptiveVertexData =

    /// Adaptive buffer containing the data.
    val Buffer : aval<IBuffer>

    /// Number of vertices in the buffer.
    val Count : uint32

    /// Offset in bytes into the buffer.
    val Offset : uint64

    /// Stride in bytes between each vertex.
    val Stride : uint64

    /// <summary>
    /// Creates a new <see cref="AdaptiveVertexData"/> instance.
    /// </summary>
    /// <param name="buffer">Adaptive buffer containing the data.</param>
    /// <param name="count">Number of vertices in the buffer.</param>
    /// <param name="offset">Offset in bytes into the buffer.</param>
    /// <param name="stride">Number of bytes between two consecutive vertices.</param>
    new (buffer: aval<IBuffer>, count: uint32, offset: uint64, stride: uint64) =
        { Buffer = buffer; Count = count; Offset = offset; Stride = stride }

    /// <summary>
    /// Creates new <see cref="AdaptiveVertexData"/> from constant vertex data.
    /// </summary>
    /// <param name="data">Constant vertex data.</param>
    new (data: VertexData) =
        AdaptiveVertexData(~~data.Buffer, data.Count, data.Offset, data.Stride)

    member inline internal this.GetValue(token: AdaptiveToken) =
        VertexData(this.Buffer.GetValue token, this.Count, this.Offset, this.Stride)

    member inline private this.Equals(other: AdaptiveVertexData) =
        this.Buffer = other.Buffer && this.Count = other.Count && this.Offset = other.Offset && this.Stride = other.Stride

    override this.Equals(obj: obj) =
        match obj with
        | :? AdaptiveVertexData as other -> this.Equals other
        | _ -> false

    override this.GetHashCode() =
        HashCode.Combine(this.Buffer.GetHashCode(), this.Count.GetHashCode(), this.Offset.GetHashCode(), this.Stride.GetHashCode())

    interface IEquatable<AdaptiveVertexData> with
        member this.Equals other = this.Equals other

/// Describes an adaptive buffer containing index data.
[<AllowNullLiteral>]
type AdaptiveIndexData =

    /// Type of the index data.
    val Type : IndexType

    /// Adaptive buffer containing the data.
    val Buffer : aval<IBuffer>

    /// Number of indices in the buffer.
    val Count : uint32

    /// Offset in bytes into the buffer.
    val Offset : uint64

    /// <summary>
    /// Creates a new <see cref="AdaptiveIndexData"/> instance.
    /// </summary>
    /// <param name="indexType">Type of the index data.</param>
    /// <param name="buffer">Adaptive buffer containing the data.</param>
    /// <param name="count">Number of indices in the buffer.</param>
    /// <param name="offset">Offset in bytes into the buffer.</param>
    new (indexType: IndexType, buffer: aval<IBuffer>, count: uint32, offset: uint64) =
        { Type = indexType; Buffer = buffer; Count = count; Offset = offset }

    /// <summary>
    /// Creates new <see cref="AdaptiveIndexData"/> from constant index data.
    /// </summary>
    /// <param name="data">Constant index data.</param>
    new (data: IndexData) =
        AdaptiveIndexData(data.Type, ~~data.Buffer, data.Count, data.Offset)

    /// <summary>
    /// Creates new <see cref="AdaptiveIndexData"/> from constant index data.
    /// Returns <c>null</c> if <paramref name="data"/> is null.
    /// </summary>
    /// <param name="data">Constant index data or <c>null</c>.</param>
    static member inline FromIndexData(data: IndexData) =
        if isNull data then null
        else AdaptiveIndexData data

    member inline internal this.GetValue(token: AdaptiveToken) =
        IndexData(this.Type, this.Buffer.GetValue token, this.Count, this.Offset)

    member inline private this.Equals(other: AdaptiveIndexData) =
        this.Type = other.Type && this.Buffer = other.Buffer && this.Offset = other.Offset

    override this.Equals(obj: obj) =
        match obj with
        | :? AdaptiveIndexData as other -> this.Equals other
        | _ -> false

    override this.GetHashCode() =
        HashCode.Combine(this.Type.GetHashCode(), this.Buffer.GetHashCode(), this.Offset.GetHashCode())

    interface IEquatable<AdaptiveIndexData> with
        member this.Equals other = this.Equals other

/// Trace geometry described by an adaptive list of triangles.
type AdaptiveTriangleMesh =

    /// Vertices of the mesh.
    val Vertices : AdaptiveVertexData

    /// <summary>
    /// Indices of the mesh or <c>null</c> if not indexed.
    /// </summary>
    val Indices : AdaptiveIndexData

    /// <summary>
    /// Micromap of the mesh (value can be <c>null</c>).
    /// </summary>
    val Micromap : aval<IMicromap>

    /// Transformation to apply to the mesh.
    val Transform : aval<Trafo3d>

    /// Geometry flags of the mesh.
    val Flags : aval<GeometryFlags>

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
    member inline this.HasMicromap = this.Micromap |> AVal.mapNonAdaptive notNull

    /// <summary>
    /// Creates an adaptive triangle mesh from vertex and index data.
    /// </summary>
    /// <param name="vertices">Vertices of the mesh.</param>
    /// <param name="indices">Indices of the mesh or <c>null</c> if not indexed.</param>
    /// <param name="transform">Transformation to apply to the mesh.</param>
    /// <param name="flags">Geometry flags of the mesh.</param>
    /// <param name="micromap">Micromap of the mesh (value can be <c>null</c>).</param>
    new (vertices: AdaptiveVertexData, indices: AdaptiveIndexData, transform: aval<Trafo3d>, flags: aval<GeometryFlags>, micromap: aval<IMicromap>) =
        { Vertices = vertices; Indices = indices; Micromap = micromap; Transform = transform; Flags = flags }

    /// <summary>
    /// Creates an adaptive triangle mesh from vertex and index data.
    /// </summary>
    /// <param name="vertices">Vertices of the mesh.</param>
    /// <param name="indices">Indices of the mesh or <c>null</c> if not indexed.</param>
    /// <param name="transform">Transformation to apply to the mesh.</param>
    /// <param name="flags">Geometry flags of the mesh.</param>
    new (vertices: AdaptiveVertexData, indices: AdaptiveIndexData, transform: aval<Trafo3d>, flags: aval<GeometryFlags>) =
        AdaptiveTriangleMesh(vertices, indices, transform, flags, ~~null)

    /// <summary>
    /// Creates an adaptive triangle mesh from vertex and index data.
    /// </summary>
    /// <param name="vertices">Vertices of the mesh.</param>
    /// <param name="indices">Indices of the mesh or <c>null</c> if not indexed.</param>
    /// <param name="transform">Transformation to apply to the mesh.</param>
    new (vertices: AdaptiveVertexData, indices: AdaptiveIndexData, transform: aval<Trafo3d>) =
        AdaptiveTriangleMesh(vertices, indices, transform, ~~GeometryFlags.None)

    /// <summary>
    /// Creates an adaptive triangle mesh from vertex and index data.
    /// </summary>
    /// <param name="vertices">Vertices of the mesh.</param>
    /// <param name="indices">Indices of the mesh or <c>null</c> if not indexed.</param>
    /// <param name="transform">Transformation to apply to the mesh.</param>
    /// <param name="flags">Geometry flags of the mesh.</param>
    /// <param name="micromap">Micromap of the mesh (can be <c>null</c>).</param>
    new (vertices: AdaptiveVertexData, indices: AdaptiveIndexData, transform: Trafo3d,
         [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags,
         [<Optional; DefaultParameterValue(null : IMicromap)>] micromap: IMicromap) =
        AdaptiveTriangleMesh(vertices, indices, ~~transform, ~~flags, ~~micromap)

    /// <summary>
    /// Creates an adaptive triangle mesh from vertex and index data.
    /// </summary>
    /// <param name="vertices">Vertices of the mesh.</param>
    /// <param name="indices">Indices of the mesh or <c>null</c> if not indexed.</param>
    /// <param name="flags">Geometry flags of the mesh.</param>
    /// <param name="micromap">Micromap of the mesh (value can be <c>null</c>).</param>
    new (vertices: AdaptiveVertexData, indices: AdaptiveIndexData, flags: aval<GeometryFlags>, micromap: aval<IMicromap>) =
        AdaptiveTriangleMesh(vertices, indices, ~~Trafo3d.Identity, flags, micromap)

    /// <summary>
    /// Creates an adaptive triangle mesh from vertex and index data.
    /// </summary>
    /// <param name="vertices">Vertices of the mesh.</param>
    /// <param name="indices">Indices of the mesh or <c>null</c> if not indexed.</param>
    /// <param name="flags">Geometry flags of the mesh.</param>
    new (vertices: AdaptiveVertexData, indices: AdaptiveIndexData, flags: aval<GeometryFlags>) =
        AdaptiveTriangleMesh(vertices, indices, flags, ~~null)

    /// <summary>
    /// Creates an adaptive triangle mesh from vertex and index data.
    /// </summary>
    /// <param name="vertices">Vertices of the mesh.</param>
    /// <param name="indices">Indices of the mesh or <c>null</c> if not indexed.</param>
    /// <param name="flags">Geometry flags of the mesh.</param>
    /// <param name="micromap">Micromap of the mesh (can be <c>null</c>).</param>
    new (vertices: AdaptiveVertexData, indices: AdaptiveIndexData,
         [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags,
         [<Optional; DefaultParameterValue(null : IMicromap)>] micromap: IMicromap) =
        AdaptiveTriangleMesh(vertices, indices, ~~flags, ~~micromap)

    /// <summary>
    /// Creates an adaptive triangle mesh from a constant triangle mesh.
    /// </summary>
    /// <param name="mesh">Constant triangle mesh.</param>
    new (mesh: TriangleMesh) =
        AdaptiveTriangleMesh(
            AdaptiveVertexData mesh.Vertices, AdaptiveIndexData.FromIndexData mesh.Indices,
            ~~mesh.Transform, ~~mesh.Flags, ~~mesh.Micromap
        )

    /// <summary>
    /// Creates an adaptive triangle mesh from the given <see cref="IndexedGeometry"/>.
    /// </summary>
    /// <param name="geometry">Geometry data; must be a triangle list or strip.</param>
    /// <param name="transform">Transformation to apply to the mesh.</param>
    /// <param name="flags">Geometry flags of the mesh.</param>
    /// <param name="micromap">Micromap of the mesh (value can be <c>null</c>).</param>
    /// <exception cref="NotSupportedException">if geometry topology is not <see cref="IndexedGeometryMode.TriangleList"/> or <see cref="IndexedGeometryMode.TriangleStrip"/>.</exception>
    static member FromIndexedGeometry(geometry: IndexedGeometry, transform: aval<Trafo3d>, flags: aval<GeometryFlags>, micromap: aval<#IMicromap>) =
        let mesh = TriangleMesh.FromIndexedGeometry(geometry)
        AdaptiveTriangleMesh(
            AdaptiveVertexData mesh.Vertices, AdaptiveIndexData.FromIndexData mesh.Indices,
            transform, flags, micromap |> AdaptiveResource.map (fun m -> m :> IMicromap)
        )

    /// <summary>
    /// Creates an adaptive triangle mesh from the given <see cref="IndexedGeometry"/>.
    /// </summary>
    /// <param name="geometry">Geometry data; must be a triangle list or strip.</param>
    /// <param name="transform">Transformation to apply to the mesh.</param>
    /// <param name="flags">Geometry flags of the mesh.</param>
    /// <exception cref="NotSupportedException">if geometry topology is not <see cref="IndexedGeometryMode.TriangleList"/> or <see cref="IndexedGeometryMode.TriangleStrip"/>.</exception>
    static member inline FromIndexedGeometry(geometry: IndexedGeometry, transform: aval<Trafo3d>, flags: aval<GeometryFlags>) =
        AdaptiveTriangleMesh.FromIndexedGeometry(geometry, transform, flags, ~~null)

    /// <summary>
    /// Creates an adaptive triangle mesh from the given <see cref="IndexedGeometry"/>.
    /// </summary>
    /// <param name="geometry">Geometry data; must be a triangle list or strip.</param>
    /// <param name="transform">Transformation to apply to the mesh.</param>
    /// <exception cref="NotSupportedException">if geometry topology is not <see cref="IndexedGeometryMode.TriangleList"/> or <see cref="IndexedGeometryMode.TriangleStrip"/>.</exception>
    static member inline FromIndexedGeometry(geometry: IndexedGeometry, transform: aval<Trafo3d>) =
        AdaptiveTriangleMesh.FromIndexedGeometry(geometry, transform, ~~GeometryFlags.None, ~~null)

    /// <summary>
    /// Creates an adaptive triangle mesh from the given <see cref="IndexedGeometry"/>.
    /// </summary>
    /// <param name="geometry">Geometry data; must be a triangle list or strip.</param>
    /// <param name="transform">Transformation to apply to the mesh.</param>
    /// <param name="flags">Geometry flags of the mesh.</param>
    /// <param name="micromap">Micromap of the mesh (can be <c>null</c>).</param>
    /// <exception cref="NotSupportedException">if geometry topology is not <see cref="IndexedGeometryMode.TriangleList"/> or <see cref="IndexedGeometryMode.TriangleStrip"/>.</exception>
    static member inline FromIndexedGeometry(geometry: IndexedGeometry, transform: Trafo3d,
                                             [<DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags,
                                             [<Optional; DefaultParameterValue(null : IMicromap)>] micromap: IMicromap) =
        AdaptiveTriangleMesh.FromIndexedGeometry(geometry, ~~transform, ~~flags, ~~micromap)

    /// <summary>
    /// Creates an adaptive triangle mesh from the given <see cref="IndexedGeometry"/>.
    /// </summary>
    /// <param name="geometry">Geometry data; must be a triangle list or strip.</param>
    /// <param name="flags">Geometry flags of the mesh.</param>
    /// <param name="micromap">Micromap of the mesh (value can be <c>null</c>).</param>
    /// <exception cref="NotSupportedException">if geometry topology is not <see cref="IndexedGeometryMode.TriangleList"/> or <see cref="IndexedGeometryMode.TriangleStrip"/>.</exception>
    static member inline FromIndexedGeometry(geometry: IndexedGeometry, flags: aval<GeometryFlags>, micromap: aval<#IMicromap>) =
        AdaptiveTriangleMesh.FromIndexedGeometry(geometry, ~~Trafo3d.Identity, flags, micromap)

    /// <summary>
    /// Creates an adaptive triangle mesh from the given <see cref="IndexedGeometry"/>.
    /// </summary>
    /// <param name="geometry">Geometry data; must be a triangle list or strip.</param>
    /// <param name="flags">Geometry flags of the mesh.</param>
    /// <exception cref="NotSupportedException">if geometry topology is not <see cref="IndexedGeometryMode.TriangleList"/> or <see cref="IndexedGeometryMode.TriangleStrip"/>.</exception>
    static member inline FromIndexedGeometry(geometry: IndexedGeometry, flags: aval<GeometryFlags>) =
        AdaptiveTriangleMesh.FromIndexedGeometry(geometry, flags, ~~null)

    /// <summary>
    /// Creates an adaptive triangle mesh from the given <see cref="IndexedGeometry"/>.
    /// </summary>
    /// <param name="geometry">Geometry data; must be a triangle list or strip.</param>
    /// <param name="flags">Geometry flags of the mesh.</param>
    /// <param name="micromap">Micromap of the mesh (can be <c>null</c>).</param>
    /// <exception cref="NotSupportedException">if geometry topology is not <see cref="IndexedGeometryMode.TriangleList"/> or <see cref="IndexedGeometryMode.TriangleStrip"/>.</exception>
    static member inline FromIndexedGeometry(geometry: IndexedGeometry,
                                             [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags,
                                             [<Optional; DefaultParameterValue(null : IMicromap)>] micromap: IMicromap) =
        AdaptiveTriangleMesh.FromIndexedGeometry(geometry, ~~flags, ~~micromap)

    member inline internal this.GetValue(token: AdaptiveToken) =
        let indices = if this.IsIndexed then this.Indices.GetValue token else null
        TriangleMesh(
            this.Vertices.GetValue token, indices,
            this.Transform.GetValue token,
            this.Flags.GetValue token,
            this.Micromap.GetValue token
        )

    member inline private this.Equals(other: AdaptiveTriangleMesh) =
        this.Vertices = other.Vertices && this.Indices = other.Indices && this.Micromap = other.Micromap &&
        this.Primitives = other.Primitives && this.Transform = other.Transform && this.Flags = other.Flags

    override this.Equals(obj: obj) =
        match obj with
        | :? AdaptiveTriangleMesh as other -> this.Equals other
        | _ -> false

    override this.GetHashCode() =
        let indexHash = if this.IsIndexed then this.Indices.GetHashCode() else 0
        HashCode.Combine(
            this.Vertices.GetHashCode(),
            indexHash,
            this.Primitives.GetHashCode(),
            this.Transform.GetHashCode(),
            this.Flags.GetHashCode(),
            this.Micromap.GetHashCode()
        )

    interface IEquatable<AdaptiveTriangleMesh> with
        member this.Equals other = this.Equals other

/// Trace geometry represented by adaptive axis-aligned bounding boxes.
type AdaptiveBoundingBoxes =

    /// Bounding box data.
    val Data : aval<AABBsData>

    /// Number of bounding boxes.
    val Count : uint32

    /// Geometry flags of the bounding boxes.
    val Flags : aval<GeometryFlags>

    /// <summary>
    /// Creates a new <see cref="AdaptiveBoundingBoxes"/> instance.
    /// </summary>
    /// <param name="data">Adaptive bounding box data.</param>
    /// <param name="count">Number of bounding boxes.</param>
    /// <param name="flags">Geometry flags of the bounding boxes.</param>
    new (data: aval<AABBsData>, count: uint32, flags: aval<GeometryFlags>) =
        { Data = data; Count = count; Flags = flags }

    /// <summary>
    /// Creates a new <see cref="AdaptiveBoundingBoxes"/> instance.
    /// </summary>
    /// <param name="data">Adaptive bounding box data.</param>
    /// <param name="count">Number of bounding boxes.</param>
    /// <param name="flags">Geometry flags of the bounding boxes.</param>
    new (data: aval<AABBsData>, count: uint32, [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags) =
        AdaptiveBoundingBoxes(data, count, ~~flags)

    /// <summary>
    /// Creates a new <see cref="AdaptiveBoundingBoxes"/> instance from a single adaptive <see cref="Box3f"/>.
    /// </summary>
    /// <param name="box">Adaptive axis-aligned bounding box.</param>
    /// <param name="flags">Geometry flags of the bounding box.</param>
    new (box: aval<Box3f>, flags: aval<GeometryFlags>) =
        let buffer = box |> AVal.map (fun box -> AABBsData(ArrayBuffer [| box |]))
        AdaptiveBoundingBoxes(buffer, 1u, flags)

    /// <summary>
    /// Creates a new <see cref="AdaptiveBoundingBoxes"/> instance from a single adaptive <see cref="Box3f"/>.
    /// </summary>
    /// <param name="box">Adaptive axis-aligned bounding box.</param>
    /// <param name="flags">Geometry flags of the bounding box.</param>
    new (box: aval<Box3f>, [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags) =
        AdaptiveBoundingBoxes(box, ~~flags)

    /// <summary>
    /// Creates a new <see cref="AdaptiveBoundingBoxes"/> instance from a single adaptive <see cref="Box3d"/>.
    /// </summary>
    /// <param name="box">Adaptive axis-aligned bounding box.</param>
    /// <param name="flags">Geometry flags of the bounding box.</param>
    new (box: aval<Box3d>, flags: aval<GeometryFlags>) =
        AdaptiveBoundingBoxes(box |> AVal.map Box3f, flags)

    /// <summary>
    /// Creates a new <see cref="AdaptiveBoundingBoxes"/> instance from a single adaptive <see cref="Box3d"/>.
    /// </summary>
    /// <param name="box">Adaptive axis-aligned bounding box.</param>
    /// <param name="flags">Geometry flags of the bounding box.</param>
    new (box: aval<Box3d>, [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags) =
        AdaptiveBoundingBoxes(box, ~~flags)

    /// <summary>
    /// Creates a new <see cref="AdaptiveBoundingBoxes"/> instance from a constant <see cref="BoundingBoxes"/> instance.
    /// </summary>
    /// <param name="boxes">Constant bounding boxes.</param>
    new (boxes: BoundingBoxes) =
        AdaptiveBoundingBoxes(~~boxes.Data, boxes.Count, ~~boxes.Flags)

    /// <summary>
    /// Creates a new <see cref="AdaptiveBoundingBoxes"/> instance for a sphere with the given position and radius.
    /// </summary>
    /// <param name="position">Center of the sphere.</param>
    /// <param name="radius">Radius of the sphere.</param>
    /// <param name="flags">Geometry flags of the bounding box.</param>
    static member inline FromCenterAndRadius(position: aval<V3f>, radius: aval<float32>, flags: aval<GeometryFlags>) =
        let data =
            (position, radius) ||> AVal.map2 (fun position radius ->
                let box = Box3f.FromCenterAndSize(position, V3f(radius * 2.0f))
                AABBsData(ArrayBuffer [| box |], 0UL, uint64 typeof<Box3f>.CLRSize)
            )
        AdaptiveBoundingBoxes(data, 1u, flags)

    /// <summary>
    /// Creates a new <see cref="AdaptiveBoundingBoxes"/> instance for a sphere with the given position and radius.
    /// </summary>
    /// <param name="position">Center of the sphere.</param>
    /// <param name="radius">Radius of the sphere.</param>
    /// <param name="flags">Geometry flags of the bounding box.</param>
    static member inline FromCenterAndRadius(position: aval<V3f>, radius: aval<float32>,
                                             [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags) =
        AdaptiveBoundingBoxes.FromCenterAndRadius(position, radius, ~~flags)

    /// <summary>
    /// Creates a new <see cref="AdaptiveBoundingBoxes"/> instance for a sphere with the given position and radius.
    /// </summary>
    /// <param name="position">Center of the sphere.</param>
    /// <param name="radius">Radius of the sphere.</param>
    /// <param name="flags">Geometry flags of the bounding box.</param>
    static member inline FromCenterAndRadius(position: aval<V3d>, radius: aval<float>, flags: aval<GeometryFlags>) =
        let position = position |> AVal.map v3f
        let radius = radius |> AVal.map float32
        AdaptiveBoundingBoxes.FromCenterAndRadius(position, radius, flags)

    /// <summary>
    /// Creates a new <see cref="AdaptiveBoundingBoxes"/> instance for a sphere with the given position and radius.
    /// </summary>
    /// <param name="position">Center of the sphere.</param>
    /// <param name="radius">Radius of the sphere.</param>
    /// <param name="flags">Geometry flags of the bounding box.</param>
    static member inline FromCenterAndRadius(position: aval<V3d>, radius: aval<float>,
                                             [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags) =
        AdaptiveBoundingBoxes.FromCenterAndRadius(position, radius, ~~flags)

    member inline internal this.GetValue(token: AdaptiveToken) =
        BoundingBoxes(this.Data.GetValue token, this.Count, this.Flags.GetValue token)

    member inline private this.Equals(other: AdaptiveBoundingBoxes) =
        this.Data = other.Data && this.Count = other.Count && this.Flags = other.Flags

    override this.Equals(obj: obj) =
        match obj with
        | :? AdaptiveBoundingBoxes as other -> this.Equals other
        | _ -> false

    override this.GetHashCode() =
        HashCode.Combine(this.Data.GetHashCode(), this.Count.GetHashCode(), this.Flags.GetHashCode())

    interface IEquatable<AdaptiveBoundingBoxes> with
        member this.Equals other = this.Equals other

[<RequireQualifiedAccess>]
type AdaptiveTraceGeometry =
    | Triangles of AdaptiveTriangleMesh[]
    | AABBs     of AdaptiveBoundingBoxes[]

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

    member this.ToAdaptiveValue() =
        match this with
        | AdaptiveTraceGeometry.Triangles meshes ->
            AVal.custom (fun token ->
                meshes
                |> Array.map (fun mesh -> mesh.GetValue token)
                |> TraceGeometry.Triangles
            )

        | AdaptiveTraceGeometry.AABBs aabbs ->
            AVal.custom (fun token ->
                aabbs
                |> Array.map (fun bb -> bb.GetValue token)
                |> TraceGeometry.AABBs
            )

    static member FromTraceGeometry(geometry: TraceGeometry) =
        match geometry with
        | TraceGeometry.Triangles meshes ->
            let meshes = meshes |> Array.map AdaptiveTriangleMesh
            AdaptiveTraceGeometry.Triangles meshes

        | TraceGeometry.AABBs bbs ->
            let bbs = bbs |> Array.map AdaptiveBoundingBoxes
            AdaptiveTraceGeometry.AABBs bbs