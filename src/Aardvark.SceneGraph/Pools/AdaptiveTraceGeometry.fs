namespace Aardvark.SceneGraph.Raytracing

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open System
open System.Runtime.InteropServices

/// Describes a buffer containing V3f vertices.
type AdaptiveVertexData(buffer: aval<IBuffer>, count: uint32, offset: uint64, stride: uint64) =

    /// Buffer containing the data.
    member val Buffer = buffer

    /// Number of vertices in the buffer.
    member val Count = count

    /// Offset in bytes into the buffer.
    member val Offset = offset

    /// Stride in bytes between each vertex.
    member val Stride = stride

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

/// Describes a buffer containing index data.
[<AllowNullLiteral>]
type AdaptiveIndexData(indexType: IndexType, buffer: aval<IBuffer>, offset: uint64) =

    /// The type of the index data.
    member val Type = indexType

    /// Buffer containing the data.
    member val Buffer = buffer

    /// Offset in bytes into the buffer.
    member val Offset = offset

    new (data: IndexData) =
        AdaptiveIndexData(data.Type, ~~data.Buffer, data.Offset)

    static member inline FromIndexData(data: IndexData) =
        if isNull data then null
        else AdaptiveIndexData data

    member inline internal this.GetValue(token: AdaptiveToken) =
        IndexData(this.Type, this.Buffer.GetValue token, this.Offset)

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

/// Trace geometry described by a triangle mesh.
type AdaptiveTriangleMesh(vertices: AdaptiveVertexData, indices: AdaptiveIndexData, primitives: uint32, transform: aval<Trafo3d>,
                          flags: aval<GeometryFlags>, micromap: aval<IMicromap>) =

    /// Vertices of the mesh.
    member val Vertices = vertices

    /// Indices of the mesh (or null if not indexed).
    member val Indices = indices

    /// Micromap of the mesh (value can be null).
    member val Micromap = micromap

    /// Number of triangles in the mesh.
    member val Primitives = primitives

    /// Transformation to apply on the mesh.
    member val Transform = transform

    /// Geometry flags of the mesh.
    member val Flags = flags

    new (mesh: TriangleMesh) =
        AdaptiveTriangleMesh(
            AdaptiveVertexData mesh.Vertices, AdaptiveIndexData.FromIndexData mesh.Indices,
            mesh.Primitives, ~~mesh.Transform, ~~mesh.Flags, ~~null
        )

    static member FromIndexedGeometry(geometry: IndexedGeometry, transform: aval<Trafo3d>, flags: aval<GeometryFlags>, micromap: aval<#IMicromap>) =
        let mesh = TriangleMesh.FromIndexedGeometry(geometry)
        AdaptiveTriangleMesh(
            AdaptiveVertexData mesh.Vertices, AdaptiveIndexData.FromIndexData mesh.Indices,
            mesh.Primitives, transform, flags, micromap |> AdaptiveResource.map (fun m -> m :> IMicromap)
        )

    static member inline FromIndexedGeometry(geometry: IndexedGeometry, transform: Trafo3d,
                                             [<DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags,
                                             [<Optional; DefaultParameterValue(null : IMicromap)>] micromap: IMicromap) =
        AdaptiveTriangleMesh.FromIndexedGeometry(geometry, ~~transform, ~~flags, ~~micromap)

    static member inline FromIndexedGeometry(geometry: IndexedGeometry,
                                             [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags,
                                             [<Optional; DefaultParameterValue(null : IMicromap)>] micromap: IMicromap) =
        AdaptiveTriangleMesh.FromIndexedGeometry(geometry, Trafo3d.Identity, flags, micromap)

    /// Returns whether the mesh is indexed.
    member inline this.IsIndexed = not <| obj.ReferenceEquals(this.Indices, null)

    member inline internal this.GetValue(token: AdaptiveToken) =
        let indices = if this.IsIndexed then this.Indices.GetValue token else null
        TriangleMesh(
            this.Vertices.GetValue token,
            indices, this.Primitives,
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

/// Trace geometry described by axis-aligned bounding boxes.
type AdaptiveBoundingBoxes(data: aval<AABBsData>, count: uint32, flags: aval<GeometryFlags>) =

    /// Bounding box data.
    member val Data = data

    /// Number of bounding boxes.
    member val Count = count

    /// Geometry flags of the bounding boxes.
    member val Flags = flags

    new (boundingBoxes: BoundingBoxes) =
        AdaptiveBoundingBoxes(~~boundingBoxes.Data, boundingBoxes.Count, ~~boundingBoxes.Flags)

    static member inline FromCenterAndRadius(position: aval<V3f>, radius: aval<float32>, flags: aval<GeometryFlags>) =
        let data =
            (position, radius) ||> AVal.map2 (fun position radius ->
                let box = Box3f.FromCenterAndSize(position, V3f(radius * 2.0f))
                AABBsData(ArrayBuffer [| box |], 0UL, uint64 typeof<Box3f>.CLRSize)
            )
        AdaptiveBoundingBoxes(data, 1u, flags)

    static member inline FromCenterAndRadius(position: aval<V3f>, radius: aval<float32>,
                                             [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags) =
        AdaptiveBoundingBoxes.FromCenterAndRadius(position, radius, ~~flags)

    static member inline FromCenterAndRadius(position: V3f, radius: float32,
                                             [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags) =
        AdaptiveBoundingBoxes.FromCenterAndRadius(~~position, ~~radius, ~~flags)

    static member inline FromCenterAndRadius(position: aval<V3d>, radius: aval<float>, flags: aval<GeometryFlags>) =
        let position = position |> AVal.map v3f
        let radius = radius |> AVal.map float32
        AdaptiveBoundingBoxes.FromCenterAndRadius(position, radius, flags)

    static member inline FromCenterAndRadius(position: aval<V3d>, radius: aval<float>,
                                             [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags) =
        AdaptiveBoundingBoxes.FromCenterAndRadius(position, radius, ~~flags)

    static member inline FromCenterAndRadius(position: V3d, radius: float,
                                             [<Optional; DefaultParameterValue(GeometryFlags.None)>] flags: GeometryFlags) =
        AdaptiveBoundingBoxes.FromCenterAndRadius(~~position, ~~radius, ~~flags)

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