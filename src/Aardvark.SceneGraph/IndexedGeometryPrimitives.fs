namespace Aardvark.Base

open System
open System.Collections.Generic
open System.Linq
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.SceneGraph

module IndexedGeometryPrimitives =

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

    let quad (v0 : V3d * C4b) (v1 : V3d * C4b) (v2 : V3d * C4b) (v3 : V3d * C4b) =
        let ((p0,c0),(p1,c1),(p2,c2),(p3,c3)) = v0,v1,v2,v3
        let pos = [| p0; p1; p2; p3 |]
        let col = [| c0; c1; c2; c3 |]
        let idx = Some [|0;1;2; 0;2;3|]
        IndexedGeometry.fromPosCol pos col idx IndexedGeometryMode.TriangleList

    let quad' (v0 : V3d) (v1 : V3d) (v2 : V3d) (v3 : V3d) (col : C4b) =
        quad (v0, col) (v1, col) (v2, col) (v3, col)
        
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

        let subdivisionWithMode (sphere : Sphere3d) level (color : C4b) (mode : IndexedGeometryMode) =
            let center = sphere.Center
            let radius = sphere.Radius
            let unitSphere = SgPrimitives.Primitives.unitSphere level
            let pos = unitSphere.IndexedAttributes.[DefaultSemantic.Positions] :?> V3f[]
            let scl = Trafo3d.Scale radius
            let tr = Trafo3d.Translation center
            let tpos = pos |> Array.map ( fun p -> (p.ToV3d() |> scl.Forward.TransformPos |> tr.Forward.TransformPos).ToV3f() )
            let tattribs = 
                [ 
                    for kvp in unitSphere.IndexedAttributes.KeyValuePairs do
                        if not (kvp.Key = DefaultSemantic.Positions) then
                            yield kvp.Key, kvp.Value
                    yield DefaultSemantic.Positions, tpos :> Array
                    yield DefaultSemantic.Colors, (Array.replicate (tpos |> Array.length) color) :> Array
                ] |> SymDict.ofList

            IndexedGeometry(
                Mode = mode,
                IndexArray = unitSphere.IndexArray,
                IndexedAttributes = tattribs,
                SingleAttributes = unitSphere.SingleAttributes
            )

        let indicesTriangles horizontalSegments verticalSegments =
            let vertexCount = horizontalSegments * verticalSegments + 2
            let indices =  List<int>()
            // indices bottom
            for i in 0.. horizontalSegments-1 do
                indices.Add(1 + (i + 1) % horizontalSegments)
                indices.Add(1 + i)
                indices.Add(0)

            // indices rings
            for i in 0.. verticalSegments - 2 do
                for j in 0 .. horizontalSegments - 1 do

                    let bl = 1 + i * horizontalSegments + j
                    let br = 1 + i * horizontalSegments + (j + 1) % horizontalSegments
                    let tl = bl + horizontalSegments
                    let tr = br + horizontalSegments
                    
                    indices.AddRange [bl; br; tl]
                    indices.AddRange [br; tr; tl]

            // indices top
            for i in 0 .. horizontalSegments-1 do
                indices.Add(vertexCount - 2 - (i + 1) % horizontalSegments)
                indices.Add(vertexCount - 2 - i)
                indices.Add(vertexCount - 1)
            
            indices

        let indicesLines horizontalSegments verticalSegments =
            let vertexCount = horizontalSegments * verticalSegments + 2
            let indices =  List<int>()
            // indices bottom
            for i in 0.. horizontalSegments-1 do
                indices.Add(1 + (i + 1) % horizontalSegments)
                indices.Add(1 + i)
                indices.Add(1 + i)
                indices.Add(0)

            // indices rings
            for i in 0.. verticalSegments - 2 do
                for j in 0 .. horizontalSegments - 1 do

                    let bl = 1 + i * horizontalSegments + j
                    let br = 1 + i * horizontalSegments + (j + 1) % horizontalSegments
                    let tl = bl + horizontalSegments
                    let tr = br + horizontalSegments
                    
                    indices.AddRange [bl; br; br; tr; tr; tl; tl; bl]

            // indices top
            for i in 0 .. horizontalSegments-1 do
                indices.Add(vertexCount - 2 - (i + 1) % horizontalSegments)
                indices.Add(vertexCount - 2 - i)
                indices.Add(vertexCount - 2 - i)
                indices.Add(vertexCount - 1)
            
            indices

        let tessellated tess (mode : IndexedGeometryMode) =
            if tess < 3 then failwithf "Tessellation too small (%A), must be at least 3." tess

            let verticalSegments = tess
            let horizontalSegments = tess * 2

            let vertices = List<V3d>()
            let normals =  List<V3d>()

            // bottom of the sphere
            vertices.Add(-V3d.YAxis)
            normals.Add(-V3d.YAxis)

            // create rings of vertices at progressively higher latitudes
            for i in 0 .. verticalSegments - 1 do
            
                let latitude = ((float i + 1.0) * Constant.Pi / float verticalSegments) - Constant.PiHalf

                let dy = Fun.Sin(latitude)
                let dxz = Fun.Cos(latitude)

                // create a single ring of vertices at this latitude
                for j in 0.. horizontalSegments-1 do
                
                    let longitude = float j * Constant.PiTimesTwo / float horizontalSegments

                    let dx = Fun.Cos(longitude) * dxz
                    let dz = Fun.Sin(longitude) * dxz

                    let normal = V3d(dx, dy, dz)

                    vertices.Add(normal)
                    normals.Add(normal)
            
            let indices = 
                match mode with
                | IndexedGeometryMode.LineList -> 
                    indicesLines horizontalSegments verticalSegments
                | IndexedGeometryMode.TriangleList ->
                    indicesTriangles horizontalSegments verticalSegments
                | _ -> failwith "implement me"

            // top of the sphere
            vertices.Add(V3d.YAxis)
            normals.Add(V3d.YAxis)

            let pos = vertices |> Seq.toArray
            let idx = indices  |> Seq.toArray
            let norm = normals |> Seq.toArray

            idx,pos,norm

        let phiThetaWithMode (sphere : Sphere3d) (level:int) (color:C4b) (mode : IndexedGeometryMode) =
            let tess = if level < 3 then 3 else level

            let (idx,pos,norm) = tessellated tess mode
            let trafo = Trafo3d.Translation(sphere.Center)
            let scale = Trafo3d.Scale(sphere.Radius)
            let pos = pos |> Array.map (scale.Forward.TransformPos >> trafo.Forward.TransformPos)
            let col = color |> Array.replicate (pos |> Array.length) 
            
            IndexedGeometry.fromPosCol pos col (Some idx) mode

    open Sphere
    
    let wireframePhiThetaSphere (sphere : Sphere3d) (level:int) (color:C4b)  =
        phiThetaWithMode sphere level color IndexedGeometryMode.LineList
        
    let solidPhiThetaSphere (sphere : Sphere3d) (level:int) (color:C4b)  =
        phiThetaWithMode sphere level color IndexedGeometryMode.TriangleList

    let wireframeSubdivisionSphere (sphere : Sphere3d) level (color : C4b) =
        Sphere.subdivisionWithMode sphere level color IndexedGeometryMode.LineList

    let solidSubdivisionSphere (sphere : Sphere3d) level (color : C4b) =
        Sphere.subdivisionWithMode sphere level color IndexedGeometryMode.TriangleList

    module Cylinder =
        
        
        let getCirclePos (i : int) (tessellation : int) (trafo : Trafo3d) =
            let angle = float i * Constant.PiTimesTwo / float tessellation
            trafo.Forward.TransformPos(V3d(Fun.Cos(angle), Fun.Sin(angle), 0.0))

        let private cylinderTriangleIndices1 (indices : List<int>) (i : int) (tessellation : int) =
                indices.Add(i * 2)
                indices.Add(i * 2 + 1)
                indices.Add((i * 2 + 2) % (tessellation * 2))

                indices.Add(i * 2 + 1)
                indices.Add((i * 2 + 3) % (tessellation * 2))
                indices.Add((i * 2 + 2) % (tessellation * 2))

        let private cylinderTriangleIndices2 (indices : List<int>) (i : int) (tessellation : int) =
                // top
                indices.Add(i * 2 + tessellation * 2)
                indices.Add(((i * 2 + 2) % (tessellation * 2)) + tessellation * 2)
                indices.Add(tessellation * 4)

                // bottom
                indices.Add(((i * 2 + 3) % (tessellation * 2)) + tessellation * 2)
                indices.Add(i * 2 + 1 + tessellation * 2)
                indices.Add(tessellation * 4 + 1)
           
        let private cylinderLineIndices1 (indices : List<int>) (i : int) (tessellation : int) =
                indices.Add(i * 2)
                indices.Add(i * 2 + 1)

        let private cylinderLineIndices2 (indices : List<int>) (i : int) (tessellation : int) =
                // top
                indices.Add(i * 2 + tessellation * 2)
                indices.Add(((i * 2 + 2) % (tessellation * 2)) + tessellation * 2)
                indices.Add(((i * 2 + 2) % (tessellation * 2)) + tessellation * 2)
                indices.Add(tessellation * 4)

                // bottom
                indices.Add(((i * 2 + 3) % (tessellation * 2)) + tessellation * 2)
                indices.Add(i * 2 + 1 + tessellation * 2)
                indices.Add(i * 2 + 1 + tessellation * 2)
                indices.Add(tessellation * 4 + 1)
            

        let cylinder (center : V3d) (axis : V3d) (height : float) (radius : float) (radiusTop : float) (tessellation : int) (mode : IndexedGeometryMode) =
            
            let vertices = List<V3d>()
            let normals = List<V3d>()
            let indices = List<int>()

            let axisNormalized = axis.Normalized
            let trafo = Trafo3d.FromNormalFrame(V3d.OOO, axisNormalized)

            // create a ring of triangles around the outside of the cylinder
            for i in 0 .. tessellation - 1 do
                let normal = getCirclePos i tessellation trafo

                vertices.Add(normal * radiusTop + axis * height)
                normals.Add(normal)

                vertices.Add(normal * radius)
                normals.Add(normal)

                match mode with
                | IndexedGeometryMode.TriangleList ->
                    cylinderTriangleIndices1 indices i tessellation
                | IndexedGeometryMode.LineList ->
                    cylinderLineIndices1 indices i tessellation 
                | _ -> failwith "implement me"

            // top and bottom caps need replicated vertices because of
            // different normals
            for i in 0 .. tessellation - 1 do
                // top
                vertices.Add(vertices.[i * 2])
                normals.Add(axisNormalized)

                // bottom
                vertices.Add(vertices.[i * 2 + 1])
                normals.Add(-axisNormalized)
                
                match mode with
                | IndexedGeometryMode.TriangleList ->
                    cylinderTriangleIndices2 indices i tessellation
                | IndexedGeometryMode.LineList ->
                    cylinderLineIndices2 indices i tessellation 
                | _ -> failwith "implement me"

            // top cap center
            vertices.Add(axis * height)
            normals.Add(axisNormalized)

            // bottom cap center
            vertices.Add(V3d.OOO)
            normals.Add(-axisNormalized)

            let pos = vertices |> Seq.toArray |> Array.map (Trafo3d.Translation(center).Forward.TransformPos)
            let norm = normals |> Seq.toArray
            let idx = indices  |> Seq.toArray

            pos,norm,idx

        let cylinderWithCol (center : V3d) (axis : V3d) (height : float) (radius : float) (radiusTop : float) (tessellation : int) (mode : IndexedGeometryMode) (col : C4b) =
            let (pos,norm,idx) = cylinder center axis height radius radiusTop tessellation mode
            let col = Array.replicate (pos |> Array.length) col

            IndexedGeometry.fromPosColNorm pos col norm (Some idx) mode

    open Cylinder

    let solidCylinder (center : V3d) (axis : V3d) (height : float) (radiusBottom : float) (radiusTop : float) (tessellation : int) (color : C4b) =
        cylinderWithCol center axis height radiusBottom radiusTop tessellation IndexedGeometryMode.TriangleList color

    let wireframeCylinder (center : V3d) (axis : V3d) (height : float) (radiusBottom : float) (radiusTop : float) (tessellation : int) (color : C4b) =
        cylinderWithCol center axis height radiusBottom radiusTop tessellation IndexedGeometryMode.LineList color

    let solidCone (center : V3d) (axis : V3d) (height : float) (radius : float) (tessellation : int) (color : C4b) =
        cylinderWithCol center axis height radius 0.0 tessellation IndexedGeometryMode.TriangleList color
        
    let wireframeCone (center : V3d) (axis : V3d) (height : float) (radius : float) (tessellation : int) (color : C4b) =
        cylinderWithCol center axis height radius 0.0 tessellation IndexedGeometryMode.LineList color

    module Torus =
        
        let private triIndices (indices : List<int>) c =
            indices.AddRange [|c; c + 1; c + 2; c; c + 2; c + 3|]

        let private lineIndices (indices : List<int>) c =
            indices.AddRange [|c; c + 1; c + 1; c + 2; c + 2; c + 3; c + 3; c|]

        let torusWithMode (torus : Torus3d) (majorTessellation : int) (minorTessellation : int) (mode : IndexedGeometryMode) =
            
            let majorCircle = torus.GetMajorCircle()

            let tPoints = [| 0..majorTessellation |]
                            |> Array.map ( fun i -> 
                                    let angle = (float i) / float majorTessellation * Constant.PiTimesTwo
                                    let majorP = majorCircle.GetPoint(angle)
                                    let uAxis = (majorP - torus.Position).Normalized * torus.MinorRadius
                                    let vAxis = torus.Direction.Normalized * torus.MinorRadius

                                    [| 0..minorTessellation |]
                                        |> Array.map ( fun j ->
                                            let angle2 = (float j) / float minorTessellation * Constant.PiTimesTwo;
                                            majorP + uAxis * angle2.Cos() + vAxis * angle2.Sin();
                                        )
                                )
                                
            let indices = List<int>()
            let positions = List<V3d>()
            let normals = List<V3d>()

            for i in 1..tPoints.Length-1 do
                for j in 1..tPoints.[i].Length-1 do
                    let c = positions.Count
                    match mode with
                    | IndexedGeometryMode.TriangleList -> triIndices indices c
                    | IndexedGeometryMode.LineList -> lineIndices indices c
                    | _ -> failwith "implement me"
                    let quad = [| tPoints.[i].[j]; tPoints.[i].[j - 1]; tPoints.[i - 1].[j - 1]; tPoints.[i - 1].[j] |]
                    positions.AddRange quad
                    normals.AddRange (Triangle3d(quad).Normal |> Array.replicate 4)
            
            let pos = positions |> Seq.toArray
            let idx = indices   |> Seq.toArray
            let norm = normals  |> Seq.toArray

            idx,pos,norm
        
        let torus (torus : Torus3d) (color : C4b) (majorTessellation : int) (minorTessellation : int) (mode : IndexedGeometryMode) =
            let (idx,pos,norm) = torusWithMode torus majorTessellation minorTessellation mode
            let col = Array.replicate (pos |> Array.length) color

            IndexedGeometry.fromPosColNorm pos col norm (Some idx) mode

    open Torus

    let solidTorus torus3D color majorTess minorTess =
        torus torus3D color majorTess minorTess IndexedGeometryMode.TriangleList

    let wireframeTorus torus3D color majorTess minorTess =
        torus torus3D color majorTess minorTess IndexedGeometryMode.LineList

    let cameraFrustum (v : IMod<CameraView>) (p : IMod<Frustum>) (c : IMod<C4b>) =
        adaptive {
            let! v = v
            let! p = p
            let! c = c
            return ViewProjection.toIndexedGeometry v p c
        }

    let cameraFrustum' (v : CameraView) (p : Frustum) (c : C4b) =
        ViewProjection.toIndexedGeometry v p c