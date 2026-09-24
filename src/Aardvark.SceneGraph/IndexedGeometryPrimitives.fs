namespace Aardvark.SceneGraph

open System
open System.Collections.Generic
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive

module IndexedGeometryPrimitives =

    module private Trips =
        
        let fst' (x,_,_) = x
        let snd' (_,x,_) = x
        let trd' (_,_,x) = x

    open Trips
        
    
    module private IndexedGeometry =

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
                    

        let fromAttribs (pos:V3f[]) 
                        (col:IndexedAttributeSpecification<C4b>) 
                        (norm:IndexedAttributeSpecification<V3f>) (idx:Option<int[]>) mode =
            let stuff =
                [
                    yield DefaultSemantic.Positions,  pos     :> Array
                    yield DefaultSemantic.Colors, (col |> IndexedAttributeSpecification.toArray pos C4b.Red) :> Array
                    yield DefaultSemantic.Normals,(norm |> IndexedAttributeSpecification.toArray pos V3f.OOI):> Array
                ] |> SymDict.ofList
            fromIndexedAttribs stuff idx mode

        let fromAttribsSimple pos col norm idx mode =
            fromAttribs pos (ManyValues col) (ManyValues norm) idx mode

        let fromPos (pos:V3f[]) (idx:Option<int[]>) mode =
            fromAttribs pos Nothing Nothing idx mode 

        let fromPosCol (pos:V3f[]) (col:C4b[]) (idx:Option<int[]>) mode =
            fromAttribs pos (ManyValues col) Nothing idx mode 

        let fromPosCol' (pos:V3f[]) (col:C4b) (idx:Option<int[]>) mode =
            fromAttribs pos (OneValue col) Nothing idx mode 

        let fromPosColNorm (pos:V3f[]) (col:C4b[]) (norm:V3f[]) (idx:Option<int[]>) mode =
            fromAttribs pos (ManyValues col) (ManyValues norm) idx mode 

        let fromPosColNorm' (pos:V3f[]) (col:C4b[]) (norm:V3f) (idx:Option<int[]>) mode =
            fromAttribs pos (ManyValues col) (OneValue norm) idx mode 
        
        let fromPosCol'Norm (pos:V3f[]) (col:C4b) (norm:V3f[]) (idx:Option<int[]>) mode =
            fromAttribs pos (OneValue col) (ManyValues norm) idx mode 

        let fromPosCol'Norm' (pos:V3f[]) (col:C4b) (norm:V3f) (idx:Option<int[]>) mode =
            fromAttribs pos (OneValue col) (OneValue norm) idx mode 

    module Point = 

        let points (pos : V3f[]) (col : C4b[]) =
            IndexedGeometry.fromPosCol pos col None IndexedGeometryMode.PointList

        let pointsWithNormals (pos : V3f[]) (col : C4b[]) (norm : V3f[]) =
            IndexedGeometry.fromPosColNorm pos col norm None IndexedGeometryMode.PointList

        let point (pos : V3f) (col : C4b) =
            IndexedGeometry.fromPosCol' [|pos|] col None IndexedGeometryMode.PointList

        let pointWithNormal (pos : V3f) (col : C4b) (norm : V3f) =
            IndexedGeometry.fromPosCol'Norm' [|pos|] col norm None IndexedGeometryMode.PointList

    open Point
    let points = points
    let pointsWithNormals = pointsWithNormals
    let point = point
    let pointWithNormal = pointWithNormal

    // Arrays can be read directly; other sources are consumed once before building attributes.
    let private materialize (source : seq<'T>) =
        match source with
        | :? ('T[]) as values -> values
        | _ -> Seq.toArray source

    let private vertexCount (primitiveCount : int) (verticesPerPrimitive : int) =
        Checked.(*) primitiveCount verticesPerPrimitive

    module Line =

        /// Eagerly builds non-indexed lines, consuming the source at most once.
        let lines (lines:seq<Line3d*C4b>) =
            let lines = materialize lines
            let count = vertexCount lines.Length 2
            let pos = Array.zeroCreate<V3f> count
            let col = Array.zeroCreate<C4b> count
            for i in 0 .. lines.Length - 1 do
                let l, c = lines.[i]
                let j = 2 * i
                pos.[j] <- l.P0.ToV3f()
                pos.[j + 1] <- l.P1.ToV3f()
                col.[j] <- c
                col.[j + 1] <- c
            IndexedGeometry.fromPosCol pos col None IndexedGeometryMode.LineList

        /// Eagerly builds uniformly colored lines, consuming the source at most once.
        let lines' (ls:seq<Line3d>) (color : C4b) =
            lines  (ls |> Seq.map ( fun l -> l,color))

        let line (l:Line3d) (color:C4b) =
            lines [l,color]
    open Line

    /// Eagerly builds non-indexed lines, consuming the source at most once.
    let lines = lines
    /// Eagerly builds uniformly colored lines, consuming the source at most once.
    let lines' = lines'
    let line = line

    module Quad =

        module internal Impl = 

            type Quadranglemode = 
                | Solid
                | Wire

            let quadrangles mode hasNormals (p0p1p2p3 : seq<(V3d * C4b * V3d)*(V3d * C4b * V3d)*(V3d * C4b * V3d)*(V3d * C4b * V3d)>) =
                let p0p1p2p3 = p0p1p2p3 |> Seq.toArray
                let attribs =
                    [|
                        for i in 0..(p0p1p2p3.Length-1) do

                            let ((p0,c0,n0),(p1,c1,n1),(p2,c2,n2),(p3,c3,n3)) = p0p1p2p3.[i]
                            let pos = [| V3f p0; V3f p1; V3f p2; V3f p3 |]
                            let col = [| c0; c1; c2; c3 |]
                            let norm =[| V3f n0; V3f n1; V3f n2; V3f n3 |]
                            let idx = 
                                match mode with
                                | Quadranglemode.Solid ->
                                    [|0;1;2; 0;2;3|] |> Array.map (fun idx -> idx + i*4)
                                | Quadranglemode.Wire ->
                                    [|0;1; 1;2; 2;3; 3;0|] |> Array.map (fun idx -> idx + i*4)
                            yield pos,col,norm,idx
                    |]

                let pos = attribs |> Array.map ( fun (v,_,_,_) -> v) |> Array.concat
                let col = attribs |> Array.map ( fun (_,v,_,_) -> v) |> Array.concat
                let nrm = attribs |> Array.map ( fun (_,_,v,_) -> v) |> Array.concat
                let idx = attribs |> Array.map ( fun (_,_,_,v) -> v) |> Array.concat
                let mode = 
                    match mode with
                    | Quadranglemode.Solid -> IndexedGeometryMode.TriangleList
                    | Quadranglemode.Wire -> IndexedGeometryMode.LineList
                     
                if hasNormals then
                    IndexedGeometry.fromPosColNorm pos col nrm (Some idx) mode
                else
                    IndexedGeometry.fromPosCol pos col (Some idx) mode

            let quadrangle mode hasNormals (posColNorm0 : V3d * C4b * V3d) (posColNorm1 : V3d * C4b * V3d) (posColNorm2 : V3d * C4b * V3d) (posColNorm3 : V3d * C4b * V3d) =
                quadrangles mode hasNormals [ posColNorm0, posColNorm1, posColNorm2, posColNorm3 ]

            let quadrangle' mode hasNormals (v0 : V3d) (v1 : V3d) (v2 : V3d) (v3 : V3d) (col : C4b) (norm : V3d) =
                quadrangle mode hasNormals (v0, col,norm) (v1, col,norm) (v2,col,norm) (v3,col,norm)
                
            let quadrangles' mode hasNormals ( p0p1p2p3nc : seq<(V3d*V3d*V3d*V3d)*V3d*C4b> ) =
                quadrangles mode hasNormals (p0p1p2p3nc |> Seq.map ( fun ((p0,p1,p2,p3),n,c) -> (p0,c,n), (p1,c,n), (p2,c,n), (p3,c,n) ))

        open Impl

        let solidQuadrangleWithColorsAndNormals (posColNorm0 : V3d * C4b * V3d) (posColNorm1 : V3d * C4b * V3d) (posColNorm2 : V3d * C4b * V3d) (posColNorm3 : V3d * C4b * V3d) = quadrangle Solid true (posColNorm0 : V3d * C4b * V3d) (posColNorm1 : V3d * C4b * V3d) (posColNorm2 : V3d * C4b * V3d) (posColNorm3 : V3d * C4b * V3d)
        let solidQuadrangle (v0 : V3d) (v1 : V3d) (v2 : V3d) (v3 : V3d) (col : C4b) (norm : V3d) = quadrangle' Solid true (v0 : V3d) (v1 : V3d) (v2 : V3d) (v3 : V3d) (col : C4b) (norm : V3d)
        let solidQuadrangle' (v0 : V3d) (v1 : V3d) (v2 : V3d) (v3 : V3d) (col : C4b) = quadrangle' Solid false v0 v1 v2  v3  col V3d.OOO

        let wireframeQuadrangleWithColorsAndNormals (posColNorm0 : V3d * C4b * V3d) (posColNorm1 : V3d * C4b * V3d) (posColNorm2 : V3d * C4b * V3d) (posColNorm3 : V3d * C4b * V3d) = quadrangle Wire true (posColNorm0 : V3d * C4b * V3d) (posColNorm1 : V3d * C4b * V3d) (posColNorm2 : V3d * C4b * V3d) (posColNorm3 : V3d * C4b * V3d)
        let wireframeQuadrangle (v0 : V3d) (v1 : V3d) (v2 : V3d) (v3 : V3d) (col : C4b) (norm : V3d) = quadrangle' Wire true (v0 : V3d) (v1 : V3d) (v2 : V3d) (v3 : V3d) (col : C4b) (norm : V3d)
        let wireframeQuadrangle' (v0 : V3d) (v1 : V3d) (v2 : V3d) (v3 : V3d) (col : C4b) = quadrangle' Wire false v0 v1 v2  v3  col V3d.OOO

        let solidQuadranglesWithColorsAndNormals corners = quadrangles Solid true corners
        let solidQuadrangles corners  = quadrangles' Solid true corners
        let solidQuadrangles' ( corners : seq<(V3d*V3d*V3d*V3d)*C4b> ) = quadrangles' Solid false (corners |> Seq.map (fun ((p0,p1,p2,p3),c) -> ((p0,p1,p2,p3),V3d.OOO,c)))

        let wireframeQuadranglesWithColorsAndNormals corners = quadrangles Wire true corners
        let wireframeQuadrangles corners = quadrangles' Wire true corners
        let wireframeQuadrangles' ( corners : seq<(V3d*V3d*V3d*V3d)*C4b> ) = quadrangles' Wire false (corners |> Seq.map (fun ((p0,p1,p2,p3),c) -> ((p0,p1,p2,p3),V3d.OOO,c)))

    open Quad

    let quad (posCol0 : V3d * C4b) (posCol1 : V3d * C4b) (posCol2 : V3d * C4b) (posCol3 : V3d * C4b)= 
        Quad.Impl.quadrangle Quad.Impl.Quadranglemode.Solid false
            (posCol0 |> fst, posCol0 |> snd, V3d.OOO)
            (posCol1 |> fst, posCol1 |> snd, V3d.OOO)
            (posCol2 |> fst, posCol2 |> snd, V3d.OOO)
            (posCol3 |> fst, posCol3 |> snd, V3d.OOO)
            
    let quad' = wireframeQuadrangle'

    module Triangle =
        module private Impl = 
            let trianglesWithColors mode (tris:seq<Triangle3d * C4b>) =
                let tris = materialize tris
                let wire = mode = Quad.Impl.Quadranglemode.Wire
                let verticesPerTriangle = if wire then 6 else 3
                let count = vertexCount tris.Length verticesPerTriangle
                let pos = Array.zeroCreate<V3f> count
                let col = Array.zeroCreate<C4b> count
                let nrm = Array.zeroCreate<V3f> count
                for i in 0 .. tris.Length - 1 do
                    let t, c = tris.[i]
                    let j = verticesPerTriangle * i
                    let p0 = t.P0.ToV3f()
                    let p1 = t.P1.ToV3f()
                    let p2 = t.P2.ToV3f()
                    let n = V3f t.Normal
                    pos.[j] <- p0
                    pos.[j + 1] <- p1
                    if wire then
                        pos.[j + 2] <- p1
                        pos.[j + 3] <- p2
                        pos.[j + 4] <- p2
                        pos.[j + 5] <- p0
                    else
                        pos.[j + 2] <- p2
                    for k in j .. j + verticesPerTriangle - 1 do
                        col.[k] <- c
                        nrm.[k] <- n
                let mode = if wire then IndexedGeometryMode.LineList else IndexedGeometryMode.TriangleList
                IndexedGeometry.fromPosColNorm pos col nrm None mode

            let trianglesWithColor mode (tris:seq<Triangle3d>) (col : C4b) =
                trianglesWithColors mode (tris |> Seq.map ( fun t -> t,col ))
        
        open Impl 

        /// Eagerly builds non-indexed solid triangles, consuming the source at most once.
        let solidTrianglesWithColors tris = trianglesWithColors Solid tris
        /// Eagerly builds uniformly colored solid triangles, consuming the source at most once.
        let solidTrianglesWithColor tris col = trianglesWithColor Solid tris col
        
        /// Eagerly builds non-indexed triangle edges, consuming the source at most once.
        let wireframeTrianglesWithColors tris = trianglesWithColors Wire tris
        /// Eagerly builds uniformly colored triangle edges, consuming the source at most once.
        let wireframeTrianglesWithColor tris col = trianglesWithColor Wire tris col

    open Triangle

    /// Eagerly builds non-indexed solid triangles, consuming the source at most once.
    let triangles =             solidTrianglesWithColors
    /// Eagerly builds uniformly colored solid triangles, consuming the source at most once.
    let triangles' =            solidTrianglesWithColor

    module Stuff = 
        let coordinateCross size =  
            [
                Line3d(V3d.Zero, V3d.XAxis * size), C4b.Red
                Line3d(V3d.Zero, V3d.YAxis * size), C4b.Green
                Line3d(V3d.Zero, V3d.ZAxis * size), C4b.Blue
            ] |> lines
            
        let solidCoordinateBox (size : float) =
            let s = size

            let ppp = V3d( s, s, s)
            let ppm = V3d( s, s,-s)
            let pmp = V3d( s,-s, s)
            let pmm = V3d( s,-s,-s)
            let mpp = V3d(-s, s, s)
            let mpm = V3d(-s, s,-s)
            let mmp = V3d(-s,-s, s)
            let mmm = V3d(-s,-s,-s)

            let hi = 70
            let lo = 30
            
            let pos = 
                [|
                    pmp;ppp;ppm;pmm
                    mmp;mmm;mpm;mpp
                    pmp;pmm;mmm;mmp
                    ppp;mpp;mpm;ppm
                    pmp;ppp;mpp;mmp
                    pmm;mmm;mpm;ppm
                |]

            let col =
                [|
                    (C4b(hi,lo,lo,255)); (C4b(hi,lo,lo,255)); (C4b(hi,lo,lo,255)); (C4b(hi,lo,lo,255))
                    (C4b(lo,hi,hi,255)); (C4b(lo,hi,hi,255)); (C4b(lo,hi,hi,255)); (C4b(lo,hi,hi,255))
                    (C4b(lo,hi,lo,255)); (C4b(lo,hi,lo,255)); (C4b(lo,hi,lo,255)); (C4b(lo,hi,lo,255))
                    (C4b(hi,lo,hi,255)); (C4b(hi,lo,hi,255)); (C4b(hi,lo,hi,255)); (C4b(hi,lo,hi,255))
                    (C4b(lo,lo,hi,255)); (C4b(lo,lo,hi,255)); (C4b(lo,lo,hi,255)); (C4b(lo,lo,hi,255))
                    (C4b(hi,hi,lo,255)); (C4b(hi,hi,lo,255)); (C4b(hi,hi,lo,255)); (C4b(hi,hi,lo,255))
                |]

            let idx =
                [| 0; 2; 1; 0; 3; 2; 4; 6; 5; 4; 7; 6; 8; 10; 9; 8; 11; 10; 12; 14; 13; 12; 15; 14; 16; 17; 18; 16; 18; 19; 20; 21; 22; 20; 22; 23 |]
                
            IndexedGeometry ( 
                IndexedGeometryMode.TriangleList,
                idx :> System.Array,
                SymDict.ofList [
                    DefaultSemantic.Positions, (pos |> Array.map V3f :> Array)
                    DefaultSemantic.Colors, (col :> Array)
                ],
                SymDict.empty
            )

        let wireCoordinateBox (size : float) =
            let s = size

            let ppp = V3d( s, s, s)
            let ppm = V3d( s, s,-s)
            let pmp = V3d( s,-s, s)
            let pmm = V3d( s,-s,-s)
            let mpp = V3d(-s, s, s)
            let mpm = V3d(-s, s,-s)
            let mmp = V3d(-s,-s, s)
            let mmm = V3d(-s,-s,-s)

            let hi = 70
            let lo = 30
            
            let pos = 
                [|
                    pmp;ppp;ppm;pmm
                    mmp;mmm;mpm;mpp
                    pmp;pmm;mmm;mmp
                    ppp;mpp;mpm;ppm
                    pmp;ppp;mpp;mmp
                    pmm;mmm;mpm;ppm
                |]

            let col =
                [|
                    (C4b(hi,lo,lo,255)); (C4b(hi,lo,lo,255)); (C4b(hi,lo,lo,255)); (C4b(hi,lo,lo,255))
                    (C4b(lo,hi,hi,255)); (C4b(lo,hi,hi,255)); (C4b(lo,hi,hi,255)); (C4b(lo,hi,hi,255))
                    (C4b(lo,hi,lo,255)); (C4b(lo,hi,lo,255)); (C4b(lo,hi,lo,255)); (C4b(lo,hi,lo,255))
                    (C4b(hi,lo,hi,255)); (C4b(hi,lo,hi,255)); (C4b(hi,lo,hi,255)); (C4b(hi,lo,hi,255))
                    (C4b(lo,lo,hi,255)); (C4b(lo,lo,hi,255)); (C4b(lo,lo,hi,255)); (C4b(lo,lo,hi,255))
                    (C4b(hi,hi,lo,255)); (C4b(hi,hi,lo,255)); (C4b(hi,hi,lo,255)); (C4b(hi,hi,lo,255))
                |]

            let idx =
                [|0; 1; 1; 2; 2; 3; 3; 0; 4; 5; 5; 6; 6; 7; 7; 4; 8; 9; 9; 10; 10; 11; 11; 8; 12; 13; 13; 14; 14; 15; 15; 12; 16; 17; 17; 18; 18; 19; 19; 16; 20; 21; 21; 22; 22; 23; 23; 20|]
                
            IndexedGeometry ( 
                IndexedGeometryMode.LineList,
                idx :> System.Array,
                SymDict.ofList [
                    DefaultSemantic.Positions, (pos |> Array.map V3f :> Array)
                    DefaultSemantic.Colors, (col :> Array)
                ],
                SymDict.empty
            )

        let cameraFrustum (v : aval<CameraView>) (p : aval<Frustum>) (c : aval<C4b>) =
            adaptive {
                let! v = v
                let! p = p
                let! c = c
                return ViewProjection.toIndexedGeometry v p c
            }

        let cameraFrustum' (v : CameraView) (p : Frustum) (c : C4b) =
            ViewProjection.toIndexedGeometry v p c

    open Stuff

    let coordinateCross = coordinateCross
    let solidCoordinateBox = solidCoordinateBox
    let wireCoordinateBox = wireCoordinateBox
    let coordinateBox f = [wireCoordinateBox f]
    let cameraFrustum = cameraFrustum
    let cameraFrustum' = cameraFrustum'

    module Box =
        
        module private Impl = 
            let boxBase mode (box : Box3d) (col : C4b) =
                let sides = 
                    [
                        (box.OOO, box.OOI, box.OII, box.OIO), -V3d.IOO, col
                        (box.IIO, box.III, box.IOI, box.IOO),  V3d.IOO, col
                        (box.OOO, box.IOO, box.IOI, box.OOI), -V3d.OIO, col
                        (box.OII, box.III, box.IIO, box.OIO),  V3d.OIO, col
                        (box.OOO, box.OIO, box.IIO, box.IOO), -V3d.OOI, col
                        (box.IOI, box.III, box.OII, box.OOI),  V3d.OOI, col
                    ]
                match mode with
                | Quad.Impl.Quadranglemode.Solid ->
                    Quad.solidQuadrangles sides
                | Quad.Impl.Quadranglemode.Wire ->
                    Quad.wireframeQuadrangles sides
        
        open Impl

        let solidBox box col = boxBase Solid box col
        let wireBox box col = boxBase Wire box col

    module Tetrahedron =
        
        module private Impl =
            let tetrahedronBase mode (center : V3d) (radius : float) (color : C4b) =
                let p0 = V3d(  sqrt(8.0/9.0),            0.0, -1.0/3.0 ) * radius + center
                let p1 = V3d( -sqrt(2.0/9.0),  sqrt(2.0/3.0), -1.0/3.0 ) * radius + center
                let p2 = V3d( -sqrt(2.0/9.0), -sqrt(2.0/3.0), -1.0/3.0 ) * radius + center
                let p3 = V3d(            0.0,            0.0,      1.0 ) * radius + center

                let sides = 
                    [
                        Triangle3d(p0,p2,p1)
                        Triangle3d(p0,p3,p2)
                        Triangle3d(p0,p1,p3)
                        Triangle3d(p1,p2,p3)
                    ]

                match mode with
                | Quad.Impl.Quadranglemode.Solid ->
                    Triangle.solidTrianglesWithColor sides color
                | Quad.Impl.Quadranglemode.Wire ->
                    Triangle.wireframeTrianglesWithColor sides color
        open Impl

        let solidTetrahedron center radius color = tetrahedronBase Solid center radius color
        let wireframeTetrahedron center radius color = tetrahedronBase Wire center radius color
                
    module Sphere =

        module private Impl = 
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
                    indices.Add(0)
                    indices.Add(1 + i)
                    indices.Add(1 + (i + 1) % horizontalSegments)

                // indices rings
                for i in 0.. verticalSegments - 2 do
                    for j in 0 .. horizontalSegments - 1 do

                        let bl = 1 + i * horizontalSegments + j
                        let br = 1 + i * horizontalSegments + (j + 1) % horizontalSegments
                        let tl = bl + horizontalSegments
                        let tr = br + horizontalSegments
                    
                        indices.AddRange [tl; br; bl]
                        indices.AddRange [tl; tr; br]

                // indices top
                for i in 0 .. horizontalSegments-1 do
                    indices.Add(vertexCount - 1)
                    indices.Add(vertexCount - 2 - i)
                    indices.Add(vertexCount - 2 - (i + 1) % horizontalSegments)
            
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
                let pos = pos |> Array.map (scale.Forward.TransformPos >> trafo.Forward.TransformPos >> V3f)
                let norm = norm |> Array.map V3f
                let col = color |> Array.replicate (pos |> Array.length) 
            
                IndexedGeometry.fromPosColNorm pos col norm (Some idx) mode
        open Impl       

        let wireframePhiThetaSphere (sphere : Sphere3d) (level:int) (color:C4b)  =
            phiThetaWithMode sphere level color IndexedGeometryMode.LineList
        
        let solidPhiThetaSphere (sphere : Sphere3d) (level:int) (color:C4b)  =
            phiThetaWithMode sphere level color IndexedGeometryMode.TriangleList

        let wireframeSubdivisionSphere (sphere : Sphere3d) level (color : C4b) =
            subdivisionWithMode sphere level color IndexedGeometryMode.LineList

        let solidSubdivisionSphere (sphere : Sphere3d) level (color : C4b) =
            subdivisionWithMode sphere level color IndexedGeometryMode.TriangleList

    open Sphere
    
    let wireframePhiThetaSphere = wireframePhiThetaSphere
        
    let solidPhiThetaSphere = solidPhiThetaSphere

    let wireframeSubdivisionSphere = wireframeSubdivisionSphere

    let solidSubdivisionSphere = solidSubdivisionSphere

    module Cylinder =
        
        module internal Impl =
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
                // Store the final format directly, without a double-precision normal buffer
                // and a separate sequence conversion for every duplicated side/cap vertex.
                let normals = List<V3f>()
                let indices = List<int>()

                let axisNormalized = axis.Normalized
                let trafo = Trafo3d.FromNormalFrame(V3d.OOO, axisNormalized)
                let displacement = axis * height

                // The side tangent is displacement + radial * (radiusTop - radius).
                // Normalize its perpendicular once, using scaling to avoid squared-length overflow
                // or underflow. Equal radii and degenerate inputs keep the original radial normals.
                let mutable tapered = false
                let mutable radialScale = 1.0
                let mutable axialNormal = V3d.Zero
                if radius <> radiusTop && tessellation >= 3 &&
                   radius >= 0.0 && radius < Double.PositiveInfinity &&
                   radiusTop >= 0.0 && radiusTop < Double.PositiveInfinity &&
                   center.IsFinite && axisNormalized.IsFinite && axisNormalized.LengthSquared > 0.0 && displacement.IsFinite then
                    let axialMax = displacement.NormMax
                    if axialMax > 0.0 then
                        let direction = displacement / axialMax
                        let axialLength = direction.Length
                        let difference = radius - radiusTop
                        let scale = max axialMax (abs difference)
                        let radial = (axialMax / scale) * axialLength
                        let axial = difference / scale
                        let inverseLength = 1.0 / sqrt (radial * radial + axial * axial)
                        radialScale <- radial * inverseLength
                        axialNormal <- direction * (axial * inverseLength / axialLength)
                        tapered <- true

                // create a ring of triangles around the outside of the cylinder
                for i in 0 .. tessellation - 1 do
                    let radial = getCirclePos i tessellation trafo
                    let normal = V3f (if tapered then radial * radialScale + axialNormal else radial)

                    vertices.Add(radial * radiusTop + displacement)
                    normals.Add(normal)

                    vertices.Add(radial * radius)
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
                    normals.Add(V3f axisNormalized)

                    // bottom
                    vertices.Add(vertices.[i * 2 + 1])
                    normals.Add(V3f -axisNormalized)
                
                    match mode with
                    | IndexedGeometryMode.TriangleList ->
                        cylinderTriangleIndices2 indices i tessellation
                    | IndexedGeometryMode.LineList ->
                        cylinderLineIndices2 indices i tessellation 
                    | _ -> failwith "implement me"

                // top cap center
                vertices.Add(displacement)
                normals.Add(V3f axisNormalized)

                // bottom cap center
                vertices.Add(V3d.OOO)
                normals.Add(V3f -axisNormalized)

                let pos = vertices |> Seq.toArray |> Array.map (Trafo3d.Translation(center).Forward.TransformPos >> V3f)
                let norm = normals.ToArray()
                let idx = indices  |> Seq.toArray


                pos,norm,idx

            let cylinderWithCol (center : V3d) (axis : V3d) (height : float) (radius : float) (radiusTop : float) (tessellation : int) (mode : IndexedGeometryMode) (col : C4b) =
                let (pos,norm,idx) = cylinder center axis height radius radiusTop tessellation mode
                let col = Array.replicate (pos |> Array.length) col

                IndexedGeometry.fromPosColNorm pos col norm (Some idx) mode
        open Impl 

        /// Creates a capped cylinder with smooth, slope-aware side normals. The top is placed at
        /// center + axis * height: axis need not be unit length and height may be signed.
        /// Equal radii and degenerate inputs retain their existing radial-normal behavior.
        let solidCylinder (center : V3d) (axis : V3d) (height : float) (radiusBottom : float) (radiusTop : float) (tessellation : int) (color : C4b) =
            cylinderWithCol center axis height radiusBottom radiusTop tessellation IndexedGeometryMode.TriangleList color

        /// Creates a wire cylinder with smooth, slope-aware side normals. The top is placed at
        /// center + axis * height: axis need not be unit length and height may be signed.
        /// Equal radii and degenerate inputs retain their existing radial-normal behavior.
        let wireframeCylinder (center : V3d) (axis : V3d) (height : float) (radiusBottom : float) (radiusTop : float) (tessellation : int) (color : C4b) =
            cylinderWithCol center axis height radiusBottom radiusTop tessellation IndexedGeometryMode.LineList color

    open Cylinder
    
    /// Creates a capped cylinder with smooth side normals and top displacement axis * height.
    let solidCylinder = solidCylinder
    /// Creates a wire cylinder with smooth side normals and top displacement axis * height.
    let wireframeCylinder = wireframeCylinder

    module Cone = 
        open Cylinder.Impl

        /// Creates a capped cone with smooth side normals. Its tip is at center + axis * height,
        /// including non-unit axes and signed heights. Degenerate-input behavior is unchanged.
        let solidCone (center : V3d) (axis : V3d) (height : float) (radius : float) (tessellation : int) (color : C4b) =
            cylinderWithCol center axis height radius 0.0 tessellation IndexedGeometryMode.TriangleList color
        
        /// Creates a wire cone with smooth side normals. Its tip is at center + axis * height,
        /// including non-unit axes and signed heights. Degenerate-input behavior is unchanged.
        let wireframeCone (center : V3d) (axis : V3d) (height : float) (radius : float) (tessellation : int) (color : C4b) =
            cylinderWithCol center axis height radius 0.0 tessellation IndexedGeometryMode.LineList color

    open Cone
    /// Creates a capped cone with smooth side normals and tip displacement axis * height.
    let solidCone = solidCone
    /// Creates a wire cone with smooth side normals and tip displacement axis * height.
    let wireframeCone = wireframeCone
    
    module Torus =
        
        module private Impl = 
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
            
                let pos = positions |> Seq.map V3f |> Seq.toArray
                let idx = indices   |> Seq.toArray
                let norm = normals |> Seq.map V3f |> Seq.toArray

                idx,pos,norm
        
            let torus (torus : Torus3d) (color : C4b) (majorTessellation : int) (minorTessellation : int) (mode : IndexedGeometryMode) =
                let (idx,pos,norm) = torusWithMode torus majorTessellation minorTessellation mode
                let col = Array.replicate (pos |> Array.length) color

                IndexedGeometry.fromPosColNorm pos col norm (Some idx) mode
        open Impl 

        let solidTorus torus3D color majorTess minorTess =
            torus torus3D color majorTess minorTess IndexedGeometryMode.TriangleList

        let wireframeTorus torus3D color majorTess minorTess =
            torus torus3D color majorTess minorTess IndexedGeometryMode.LineList

    open Torus

    let solidTorus = solidTorus
    let wireframeTorus = wireframeTorus

    /// Creates an arrow consisting of a cylinder for the shaft and a cone for the head.
    let arrow (position : V3d) (axis : V3d)
              (shaftLength : float) (shaftRadius : float) (shaftColor : C4b)       
              (headLength : float) (headRadius : float) (headColor : C4b)
              (tessellation : int) =
        let shaft = solidCylinder position axis shaftLength shaftRadius shaftRadius tessellation shaftColor
        let head = solidCone (position + axis * shaftLength) axis headLength (shaftRadius + headRadius) tessellation headColor
        IndexedGeometry.union head shaft

    /// Creates a coordinate cross consisting of three arrows for the axes and a sphere for the origin.
    let coordinateCross3d (colorX : C4b) (colorY : C4b) (colorZ : C4b) (colorOrigin : C4b) (tessellation : int) =
        let xAxis = arrow V3d.Zero V3d.XAxis 0.75 0.05 colorX 0.25 0.045 colorX tessellation
        let yAxis = arrow V3d.Zero V3d.YAxis 0.75 0.05 colorY 0.25 0.045 colorY tessellation
        let zAxis = arrow V3d.Zero V3d.ZAxis 0.75 0.05 colorZ 0.25 0.045 colorZ tessellation
        let center = solidPhiThetaSphere (Sphere3d(V3d.Zero, 0.085)) tessellation colorOrigin
        [center; xAxis; yAxis; zAxis] |> List.reduce IndexedGeometry.union
