namespace Aardvark.Rendering.Raytracing

open Aardvark.Base
open Aardvark.Rendering

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

    let ofSeq (g : seq<TraceGeometry>) =
        g |> Seq.reduce append

    let ofList (g : List<TraceGeometry>) =
        g |> List.reduce append

    let ofArray (g : TraceGeometry[]) =
        g |> Array.reduce append

    let ofBox3f (flags : GeometryFlags) (box : Box3f) =
        let aabb = {
                Buffer = ArrayBuffer [| box |]
                Offset = 0UL
                Stride = uint64 sizeof<Box3f>
            }

        TraceGeometry.AABBs [| { Data = aabb; Count = 1u; Flags = flags }|]

    let ofBox3d (flags : GeometryFlags) (box : Box3d) =
        box |> Box3f |> ofBox3f flags

    let ofCenterAndRadius (flags : GeometryFlags) (position : V3d) (radius : float) =
        Box3d.FromCenterAndSize(position, V3d(radius * 2.0)) |> ofBox3d flags

    let ofIndexedGeometry (flags : GeometryFlags) (trafo : Trafo3d) (g : IndexedGeometry) =

        let geometry =
            g |> IndexedGeometry.toNonStripped

        if geometry.Mode <> IndexedGeometryMode.TriangleList then
            failwithf "Unsupported geometry mode: %A" geometry.Mode

        let vertices = geometry.IndexedAttributes.[DefaultSemantic.Positions]

        let indices =
            if geometry.IsIndexed then Some geometry.IndexArray else None

        let primitives =
            match indices with
            | Some arr -> arr.Length / 3
            | _ -> vertices.Length / 3

        let vertexData =
            { Buffer = ArrayBuffer(vertices)
              Count = uint32 vertices.Length
              Offset = 0UL
              Stride = uint64 sizeof<V3f> }

        let indexData =
            indices |> Option.map (fun arr ->
                { Buffer = ArrayBuffer(arr)
                  Offset = 0UL }
            )

        let mesh =
            { Vertices = vertexData
              Indices = indexData
              Transform = trafo
              Primitives = uint32 primitives
              Flags = flags }

        TraceGeometry.Triangles [| mesh |]