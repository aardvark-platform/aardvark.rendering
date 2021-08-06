namespace Aardvark.Rendering.Raytracing

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open FSharp.Data.Adaptive

[<CLIMutable>]
type AdaptiveTriangleMesh =
    {
        /// Vertices of the mesh.
        Vertices   : VertexData<aval<IBuffer>>

        /// Indices of the mesh.
        Indices    : IndexData<aval<IBuffer>> option

        /// Transformation to apply on the mesh.
        Transform  : aval<Trafo3d>

        /// Number of triangles in the mesh.
        Primitives : uint32

        /// Geometry flags of the mesh.
        Flags      : aval<GeometryFlags>
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AdaptiveTriangleMesh =

    let constant (m : TriangleMesh) =
        { Vertices   = m.Vertices |> VertexData.map AVal.constant
          Indices    = m.Indices |> Option.map (IndexData.map AVal.constant)
          Transform  = AVal.constant m.Transform
          Primitives = m.Primitives
          Flags      = AVal.constant m.Flags }

    let transform (trafo : aval<Trafo3d>) (mesh : AdaptiveTriangleMesh) =
        { mesh with Transform = trafo }

    let transform' (trafo : Trafo3d) (mesh : AdaptiveTriangleMesh) =
        mesh |> transform (AVal.constant trafo)

    let flags (f : aval<GeometryFlags>) (mesh : AdaptiveTriangleMesh) =
        { mesh with Flags = f }

    let flags' (f : GeometryFlags) (mesh : AdaptiveTriangleMesh) =
        mesh |> flags (AVal.constant f)

    let toAVal (m : AdaptiveTriangleMesh) : aval<TriangleMesh> =
        AVal.custom (fun t ->
            { Vertices   = m.Vertices |> VertexData.map (fun b -> b.GetValue(t))
              Indices    = m.Indices |> Option.map (IndexData.map (fun b -> b.GetValue(t)))
              Transform  = m.Transform.GetValue(t)
              Primitives = m.Primitives
              Flags      = m.Flags.GetValue(t) }
        )

[<CLIMutable>]
type AdaptiveBoundingBoxes =
    {
        /// Bounding box data.
        Data  : AABBsData<aval<IBuffer>>

        /// Number of bounding boxes.
        Count : uint32

        /// Geometry flags of the bounding boxes.
        Flags : aval<GeometryFlags>
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AdaptiveBoundingBoxes =

    let constant (boxes : BoundingBoxes) =
        { Data = boxes.Data |> AABBsData.map AVal.constant
          Count = boxes.Count
          Flags = AVal.constant boxes.Flags }

    let flags (f : aval<GeometryFlags>) (bbs : AdaptiveBoundingBoxes) =
        { bbs with Flags = f }

    let flags' (f : GeometryFlags) (bbs : AdaptiveBoundingBoxes) =
        bbs |> flags (AVal.constant f)

    let toAVal (bb : AdaptiveBoundingBoxes) : aval<BoundingBoxes> =
        AVal.custom (fun t ->
            { Data  = bb.Data |> AABBsData.map (fun b -> b.GetValue(t))
              Count = bb.Count
              Flags = bb.Flags.GetValue(t) }
        )

[<RequireQualifiedAccess>]
type AdaptiveTraceGeometry =
    | Triangles of AdaptiveTriangleMesh[]
    | AABBs     of AdaptiveBoundingBoxes[]

    /// Number of individual geometries.
    member x.Count =
        match x with
        | Triangles arr -> arr.Length
        | AABBs arr -> arr.Length

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AdaptiveTraceGeometry =

    let constant (geometry : TraceGeometry) =
        match geometry with
        | TraceGeometry.AABBs aabbs ->
            aabbs |> Array.map AdaptiveBoundingBoxes.constant |> AdaptiveTraceGeometry.AABBs

        | TraceGeometry.Triangles meshes ->
            meshes |> Array.map AdaptiveTriangleMesh.constant |> AdaptiveTraceGeometry.Triangles

    let append (a : AdaptiveTraceGeometry) (b : AdaptiveTraceGeometry) =
        match a, b with
        | AdaptiveTraceGeometry.Triangles a, AdaptiveTraceGeometry.Triangles b ->
            AdaptiveTraceGeometry.Triangles (Array.append a b)

        | AdaptiveTraceGeometry.AABBs a, AdaptiveTraceGeometry.AABBs b ->
            AdaptiveTraceGeometry.AABBs (Array.append a b)

        | _ ->
            failwithf "Cannot combine different types of trace geometry"

    let ofSeq (g : AdaptiveTraceGeometry seq) =
        g |> Seq.reduce append

    let ofList (g : AdaptiveTraceGeometry list) =
        g |> List.reduce append

    let ofArray (g : AdaptiveTraceGeometry[]) =
        g |> Array.reduce append

    let ofBoundingBoxes (bb : AdaptiveBoundingBoxes) =
        AdaptiveTraceGeometry.AABBs [| bb |]

    let ofTriangleMesh (mesh : AdaptiveTriangleMesh) =
        AdaptiveTraceGeometry.Triangles [| mesh |]

    let toAVal (g : AdaptiveTraceGeometry) : aval<TraceGeometry> =
        let inline eval (t : AdaptiveToken) (v : IAdaptiveValue<'T>) = v.GetValue(t)

        match g with
        | AdaptiveTraceGeometry.Triangles meshes ->
            AVal.custom (fun t ->
                meshes
                |> Array.map (AdaptiveTriangleMesh.toAVal >> eval t)
                |> TraceGeometry.Triangles
            )

        | AdaptiveTraceGeometry.AABBs aabbs ->
            AVal.custom (fun t ->
                aabbs
                |> Array.map (AdaptiveBoundingBoxes.toAVal >> eval t)
                |> TraceGeometry.AABBs
            )
