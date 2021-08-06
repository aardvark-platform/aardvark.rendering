namespace Aardvark.Rendering.Raytracing

open Aardvark.Base
open Aardvark.Rendering

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AABBsData =

    let map (mapping : 'T -> 'U) (data : AABBsData<'T>) =
        { Buffer = mapping data.Buffer
          Offset = data.Offset
          Stride = data.Stride }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VertexData =

    let map (mapping : 'T -> 'U) (data : VertexData<'T>) =
        { Buffer = mapping data.Buffer
          Count  = data.Count
          Offset = data.Offset
          Stride = data.Stride }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module IndexData =

    let map (mapping : 'T -> 'U) (data : IndexData<'T>) =
        { Type = data.Type
          Buffer = mapping data.Buffer
          Offset = data.Offset }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module BoundingBoxes =

    let ofBox3fArray (boxes : Box3f[]) =
        let data = {
                Buffer = ArrayBuffer boxes :> IBuffer
                Offset = 0UL
                Stride = uint64 sizeof<Box3f>
            }

        { Data  = data;
          Count = uint32 boxes.Length;
          Flags = GeometryFlags.None }

    let ofBox3f (box : Box3f) =
        [| box |] |> ofBox3fArray

    let ofBox3dArray (boxes : Box3d[]) =
        boxes |> Array.map Box3f |> ofBox3fArray

    let ofBox3d (box : Box3d) =
        [| Box3f box |] |> ofBox3fArray

    let ofCenterAndRadius (position : V3d) (radius : float) =
        Box3d.FromCenterAndSize(position, V3d(radius * 2.0)) |> ofBox3d

    let flags (f : GeometryFlags) (bb : BoundingBoxes) =
        { bb with Flags = f }


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module TriangleMesh =

    let ofIndexedGeometry (g : IndexedGeometry) =
        let geometry =
            g |> IndexedGeometry.toNonStripped

        if geometry.Mode <> IndexedGeometryMode.TriangleList then
            failwithf "Unsupported geometry mode: %A" geometry.Mode

        let vertices = geometry.IndexedAttributes.[DefaultSemantic.Positions]

        let indices =
            if geometry.IsIndexed then
                let typ =
                    let t = geometry.IndexArray.GetType().GetElementType()

                    if t = typeof<uint16> || t = typeof<int16> then IndexType.UInt16
                    elif t = typeof<uint32> || t = typeof<int32> then IndexType.UInt32
                    else failwithf "[TraceGeometry] Unsupported index type %A" t

                Some (geometry.IndexArray, typ)
            else
                None

        let primitives =
            match indices with
            | Some (arr, _) -> arr.Length / 3
            | _ -> vertices.Length / 3

        let vertexData =
            { Buffer = ArrayBuffer(vertices) :> IBuffer
              Count = uint32 vertices.Length
              Offset = 0UL
              Stride = uint64 sizeof<V3f> }

        let indexData =
            indices |> Option.map (fun (arr, t) ->
                { Type = t
                  Buffer = ArrayBuffer(arr) :> IBuffer
                  Offset = 0UL }
            )

        { Vertices = vertexData
          Indices = indexData
          Transform = Trafo3d.Identity
          Primitives = uint32 primitives
          Flags = GeometryFlags.None }

    let transform (trafo : Trafo3d) (m : TriangleMesh) =
        { m with Transform = trafo }

    let flags (f : GeometryFlags) (m : TriangleMesh) =
        { m with Flags = f }


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module TraceGeometry =

    let append (a : TraceGeometry) (b : TraceGeometry) =
        match a, b with
        | TraceGeometry.Triangles a, TraceGeometry.Triangles b ->
            TraceGeometry.Triangles (Array.append a b)

        | TraceGeometry.AABBs a, TraceGeometry.AABBs b ->
            TraceGeometry.AABBs (Array.append a b)

        | _ ->
            failwithf "Cannot combine different types of trace geometry"

    let ofSeq (g : TraceGeometry seq) =
        g |> Seq.reduce append

    let ofList (g : TraceGeometry list) =
        g |> List.reduce append

    let ofArray (g : TraceGeometry[]) =
        g |> Array.reduce append

    let ofBoundingBoxes (bb : BoundingBoxes) =
        TraceGeometry.AABBs [| bb |]

    let ofTriangleMesh (mesh : TriangleMesh) =
        TraceGeometry.Triangles [| mesh |]