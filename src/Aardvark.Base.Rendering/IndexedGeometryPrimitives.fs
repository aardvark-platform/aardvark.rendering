namespace Aardvark.Base

open System
open System.Collections.Generic
open System.Linq
open Aardvark.Base

//da fehlen halt noch gscheitere index berechnung, nicht einfach nur TriangleList
module Primitives =

    module private Trips =
        
        let fst' (x,_,_) = x
        let snd' (_,x,_) = x
        let trd' (_,_,x) = x

    open Trips
        
    
    module IndexedGeometry =

        type IndexedAttributeSpecification<'a> =
            | Nothing
            | OneValue of 'a
            | ManyValues of 'a[]

        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module IndexedAttributeSpecification =  
            let toArray referenceArray defaultValue ias =
                match ias with
                | Nothing -> Array.replicate (referenceArray |> Array.length) defaultValue
                | OneValue v -> Array.replicate (referenceArray |> Array.length) v
                | ManyValues a -> a

        let fromIndexedAttribs attribs (idx:Option<int[]>) (mode:IndexedGeometryMode) = 
            match idx with
            | None -> 
                IndexedGeometry (
                    Mode = mode,
                    IndexedAttributes = attribs
                )
            | Some i -> 
                IndexedGeometry (
                    Mode = mode,
                    IndexArray = (i :> Array),
                    IndexedAttributes = attribs
                )
                    

        let fromAttribs (pos:V3d[]) 
                        (col:IndexedAttributeSpecification<C4b>) 
                        (norm:IndexedAttributeSpecification<V3d>) (idx:Option<int[]>) mode =
            let stuff =
                [
                    yield DefaultSemantic.Positions,  pos     :> Array
                    yield DefaultSemantic.Colors, (col |> IndexedAttributeSpecification.toArray pos C4b.Red) :> Array
                    yield DefaultSemantic.Normals,(norm |> IndexedAttributeSpecification.toArray pos V3d.OOI):> Array
                ] |> SymDict.ofList
            fromIndexedAttribs stuff idx mode

        let fromAttribsSimple pos col norm idx mode =
            fromAttribs pos (ManyValues col) (ManyValues norm) idx mode

        let fromPos (pos:V3d[]) (idx:Option<int[]>) mode =
            fromAttribs pos Nothing Nothing idx mode 

        let fromPosCol (pos:V3d[]) (col:C4b[]) (idx:Option<int[]>) mode =
            fromAttribs pos (ManyValues col) Nothing idx mode 

        let fromPosCol' (pos:V3d[]) (col:C4b) (idx:Option<int[]>) mode =
            fromAttribs pos (OneValue col) Nothing idx mode 

        let fromPosColNorm (pos:V3d[]) (col:C4b[]) (norm:V3d[]) (idx:Option<int[]>) mode =
            fromAttribs pos (ManyValues col) (ManyValues norm) idx mode 

        let fromPosColNorm' (pos:V3d[]) (col:C4b[]) (norm:V3d) (idx:Option<int[]>) mode =
            fromAttribs pos (ManyValues col) (OneValue norm) idx mode 
        
        let fromPosCol'Norm (pos:V3d[]) (col:C4b) (norm:V3d[]) (idx:Option<int[]>) mode =
            fromAttribs pos (OneValue col) (ManyValues norm) idx mode 

        let fromPosCol'Norm' (pos:V3d[]) (col:C4b) (norm:V3d) (idx:Option<int[]>) mode =
            fromAttribs pos (OneValue col) (OneValue norm) idx mode 

    let points (pos : V3d[]) (col : C4b[]) =
        IndexedGeometry.fromPosCol pos col None IndexedGeometryMode.PointList

    let pointsWithNormals (pos : V3d[]) (col : C4b[]) (norm : V3d[]) =
        IndexedGeometry.fromPosColNorm pos col norm None IndexedGeometryMode.PointList

    let point (pos : V3d) (col : C4b) =
        IndexedGeometry.fromPosCol' [|pos|] col None IndexedGeometryMode.PointList

    let pointWithNormal (pos : V3d) (col : C4b) (norm : V3d) =
        IndexedGeometry.fromPosCol'Norm' [|pos|] col norm None IndexedGeometryMode.PointList

    let lines (lines:seq<Line3d*C4b>) =
        let pos = lines |> Seq.map fst |> Seq.collect(fun l -> [|l.P0.ToV3f().ToV3d(); l.P1.ToV3f().ToV3d()|]) |> Seq.toArray
        let col = lines |> Seq.map snd |> Seq.collect(fun c -> [|c;c|]) |> Seq.toArray
        IndexedGeometry.fromPosCol pos col None IndexedGeometryMode.LineList

    let lines' (ls:seq<Line3d>) (color : C4b) =
        lines  (ls |> Seq.map ( fun l -> l,color))

    let line (l:Line3d) (color:C4b) =
        lines [l,color]

    let triangles (tris:seq<Triangle3d * C4b>) =
        let pos = tris |> Seq.map fst |> Seq.collect(fun t -> [|t.P0.ToV3f().ToV3d(); t.P1.ToV3f().ToV3d(); t.P2.ToV3f().ToV3d()|]) |> Seq.toArray
        let col = tris |> Seq.map snd |> Seq.collect(fun c -> [|c;c;c|]) |> Seq.toArray
        IndexedGeometry.fromPosCol pos col None IndexedGeometryMode.TriangleList

    let triangles' (tris:seq<Triangle3d>) (col : C4b) =
        triangles (tris |> Seq.map ( fun t -> t,col ))

    let trianglesWithNormals (tris:seq<Triangle3d * C4b * V3d>) =
        let pos = tris |> Seq.map fst' |> Seq.collect(fun t -> [|t.P0.ToV3f().ToV3d(); t.P1.ToV3f().ToV3d(); t.P2.ToV3f().ToV3d()|]) |> Seq.toArray
        let col = tris |> Seq.map snd' |> Seq.collect(fun c -> [|c;c;c|]) |> Seq.toArray
        let nrm = tris |> Seq.map trd' |> Seq.collect(fun n -> [|n;n;n|]) |> Seq.toArray
        IndexedGeometry.fromPosColNorm pos col nrm None IndexedGeometryMode.TriangleList

    let triangles'WithNormals (tris:seq<Triangle3d * V3d>) (col : C4b) =
        trianglesWithNormals (tris |> Seq.map ( fun (t,n) -> t,col,n ))

    let coordinateCross size =  
        [
            Line3d(V3d.Zero, V3d.XAxis * size), C4b.Red
            Line3d(V3d.Zero, V3d.YAxis * size), C4b.Green
            Line3d(V3d.Zero, V3d.ZAxis * size), C4b.Blue
        ] |> lines

    module Sphere =
        
        let tessellated (sphere : Sphere3d) tess =
            if tess < 3 then failwithf "Tessellation too small (%A), must be at least 3." tess
            
            let center = sphere.Center
            let radius = sphere.Radius

            let verticalSegments = tess
            let horizontalSegments = tess * 2

            let vertices = new List<V3d>()
            let normals = new List<V3d>()
            let indices = new List<int>()

            // bottom of the sphere
            vertices.Add(-V3d.YAxis * radius)
            normals.Add(-V3d.YAxis)

            // create rings of vertices at progressively higher latitudes
            for i in 0 .. verticalSegments - 1 do
            
                let latitude = ((float i + 1.0) * Constant.Pi / float verticalSegments) - Constant.PiHalf

                let dy = Fun.Sin(latitude)
                let dxz = Fun.Cos(latitude)

                // create a single ring of vertices at this latitude
                for j in 0.. horizontalSegments do
                
                    let longitude = float j * Constant.PiTimesTwo / float horizontalSegments

                    let dx = Fun.Cos(longitude) * dxz
                    let dz = Fun.Sin(longitude) * dxz

                    let normal = V3d(dx, dy, dz)

                    vertices.Add(normal * radius)
                    normals.Add(normal)
                
            // top of the sphere
            vertices.Add(V3d.YAxis * radius)
            normals.Add(V3d.YAxis)

            // indices bottom
            for i in 0.. horizontalSegments do
                indices.Add(1 + i)
                indices.Add(1 + (i + 1) % horizontalSegments)
                indices.Add(0)

            // indices rings
            for i in 0.. verticalSegments - 2 do
                for j in 0 .. horizontalSegments do
                    let nextI = i + 1
                    let nextJ = (j + 1) % horizontalSegments
                   
                    indices.Add(1 + nextI * horizontalSegments + j)
                    indices.Add(1 + i * horizontalSegments + nextJ)
                    indices.Add(1 + i * horizontalSegments + j)

                    indices.Add(1 + nextI * horizontalSegments + j)
                    indices.Add(1 + nextI * horizontalSegments + nextJ)
                    indices.Add(1 + i * horizontalSegments + nextJ)

            // indices top
            for i in 0 .. horizontalSegments do
                indices.Add(vertices.Count - 2 - i)
                indices.Add(vertices.Count - 2 - (i + 1) % horizontalSegments)
                indices.Add(vertices.Count - 1)

            let pos = vertices.Select(fun x -> x.ToV3f().ToV3d()).ToArray()
            let idx = indices.ToArray()
            let norm = normals.Select(fun x -> x.ToV3f().ToV3d()).ToArray()

            idx,pos,norm

//            let vg = new VertexGeometry(GeometryMode.TriangleList);
//            vg.Positions = 
//            vg.Indices = ;
//            vg.Colors = new C4b[vertices.Count].Set(color);
//
//            if (createNormals)
//                vg.Normals = ;
//
//            return vg.Transformed(Trafo3d.Translation(center));

    let wireframeSphere (sphere : Sphere3d) (color:C4b) (tessellation:Option<int>) =
        let tess = 
            let res = tessellation |> Option.defaultValue 18
            if res < 3 then 3 else res

        let (idx,pos,norm) = Sphere.tessellated sphere tess
        let trafo = Trafo3d.Translation(sphere.Center)
        let pos = pos |> Array.map trafo.Forward.TransformPos
        let col = color |> Array.replicate (pos |> Array.length) 

        
        //todo getlines

        failwith ""