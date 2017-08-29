namespace Aardvark.SceneGraph


open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Ag
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Runtime.CompilerServices

open Aardvark.Base.Incremental

#nowarn "9"
#nowarn "51"

[<AutoOpen>]
module SgPrimitives =

    module Sphere =
        
        let private cube = 
            let V3d(x : int,y : int,z : int) = V3d(x,y,z).Normalized
            [|
                // +Z
                Triangle3d(V3d(1, 1, 1), V3d(-1, -1, 1), V3d(1, -1, 1))
                Triangle3d(V3d(-1, -1, 1), V3d(1, 1, 1), V3d(-1, 1, 1))

                // -Z
                Triangle3d(V3d(-1, -1, -1), V3d(1, 1, -1), V3d(1, -1, -1))
                Triangle3d(V3d(1, 1, -1), V3d(-1, -1, -1), V3d(-1, 1, -1))


                // +Y
                Triangle3d(V3d(-1, 1, -1), V3d(1, 1, 1), V3d(1, 1, -1))
                Triangle3d(V3d(1, 1, 1), V3d(-1, 1, -1), V3d(-1, 1, 1))

                // -Y
                Triangle3d(V3d(1, -1, 1), V3d(-1, -1, -1), V3d(1, -1, -1))
                Triangle3d(V3d(-1, -1, -1), V3d(1, -1, 1), V3d(-1, -1, 1))

                // +X
                Triangle3d(V3d(1, 1, 1), V3d(1, -1, -1), V3d(1, 1, -1))
                Triangle3d(V3d(1, -1, -1), V3d(1, 1, 1), V3d(1, -1, 1))

                // -X
                Triangle3d(V3d(-1, -1, -1), V3d(-1, 1, 1), V3d(-1, 1, -1))
                Triangle3d(V3d(-1, 1, 1), V3d(-1, -1, -1), V3d(-1, -1, 1))

            |]

        let private subdivide (tris : Triangle3d[]) =
            [|
                for t in tris do
                    let mid = 0.5 * (t.P0 + t.P1) |> Vec.normalize

                    yield Triangle3d(t.P1, t.P2, mid)
                    yield Triangle3d(t.P2, t.P0, mid)
            |]

        let private sphereGeometry (tris : Triangle3d[]) =
            let positions : V3f[] = Array.zeroCreate (3 * tris.Length)
            let normals : V3f[]  = Array.zeroCreate (3 * tris.Length)
            let coords : V2f[]  = Array.zeroCreate (3 * tris.Length)

            let mutable i = 0
            for (t : Triangle3d) in tris do
                let p0 = t.P0
                let p1 = t.P1
                let p2 = t.P2
                positions.[i + 0] <- V3f p0
                positions.[i + 1] <- V3f p1
                positions.[i + 2] <- V3f p2
                normals.[i + 0] <- V3f p0.Normalized
                normals.[i + 1] <- V3f p1.Normalized
                normals.[i + 2] <- V3f p2.Normalized
                coords.[i + 0] <- p0.SphericalFromCartesian() |> V2f
                coords.[i + 1] <- p1.SphericalFromCartesian() |> V2f
                coords.[i + 2] <- p2.SphericalFromCartesian() |> V2f

                i <- i + 3
            let geometry = 
                IndexedGeometry(
                    Mode = IndexedGeometryMode.TriangleList,
                    IndexedAttributes =
                        SymDict.ofList [
                            DefaultSemantic.Positions, positions :> Array
                            DefaultSemantic.Normals, normals :> Array
                            DefaultSemantic.DiffuseColorCoordinates, coords :> Array
                        ]
                )

            geometry


        let private spheres =
            Seq.initInfinite id
                |> Seq.scan (fun last _ -> subdivide last) cube
                |> Seq.map sphereGeometry
                |> Seq.cache


        let rec private sphere =
//            seq {
//                yield cube
//                yield! sphere |> Seq.map subdivide
//            } |> Seq.cache

            Seq.initInfinite id
                |> Seq.scan (fun last _ -> subdivide last) cube
                |> Seq.map sphereGeometry
                |> Seq.cache

        let private sgs =
            spheres |> Seq.map Sg.ofIndexedGeometry |> Seq.cache

        let get (level : int) =
            spheres |> Seq.item level

        let getSg (level : int) =
            sgs |> Seq.item level

    module private Cylinder =
        
        let private create (tess : int) =
            
            let indices = System.Collections.Generic.List<int>()
            let positions = System.Collections.Generic.List<V3d>()
            let normals = System.Collections.Generic.List<V3d>()

            let icb = 0
            let ict = 1

            let step = Constant.PiTimesTwo / float tess

            // bottom cap
            positions.Add V3d.Zero
            normals.Add -V3d.OOI
            let ic = positions.Count - 1
            let mutable phi = 0.0
            let mutable last = ic + tess
            for i in 0 .. tess - 1 do
                let i = ic + i + 1
                let p = V3d(cos phi, sin phi, 0.0)
                positions.Add p
                normals.Add -V3d.OOI
                indices.Add i; indices.Add last; indices.Add ic
                last <- i
                phi <- phi + step


            // top cap
            positions.Add V3d.OOI
            normals.Add V3d.OOI
            let ic = positions.Count - 1
            let mutable phi = 0.0
            let mutable last = ic + tess
            for i in 0 .. tess - 1 do
                let i = ic + i + 1
                let p = V3d(cos phi, sin phi, 1.0)
                positions.Add p
                normals.Add V3d.OOI
                indices.Add last; indices.Add i; indices.Add ic
                last <- i
                phi <- phi + step

            // side faces
            let ic = positions.Count - 1
            let mutable phi = 0.0
            let mutable lt = ic + 1 + 2 * tess - 1
            let mutable lb = ic + 1 + 2 * tess - 2
            for i in 0 .. tess - 1 do
                let ib = 1 + ic + 2 * i
                let it = ib + 1

                let c = cos phi
                let s = sin phi
                let b = V3d(c, s, 0.0)
                let t = V3d(c, s, 1.0)
                positions.Add b; normals.Add (V3d(c,s,0.0))
                positions.Add t; normals.Add (V3d(c,s,0.0))


                // lb ib lt
                // lt ib it

                indices.Add lb; indices.Add ib; indices.Add lt
                indices.Add lt; indices.Add ib; indices.Add it

                
                lt <- it
                lb <- ib

                phi <- phi + step

            let indices = indices.ToArray()
            let positions = positions.MapToArray(fun v -> V3f v)
            let normals = normals.MapToArray(fun v -> V3f v)


            let geometry = 
                IndexedGeometry(
                    Mode = IndexedGeometryMode.TriangleList,
                    IndexArray = indices,
                    IndexedAttributes =
                        SymDict.ofList [
                            DefaultSemantic.Positions, positions :> Array
                            DefaultSemantic.Normals, normals :> Array
                        ]
                )

            geometry


        let private cacheg = Dict<int, IndexedGeometry>()
        let private cache = Dict<int, ISg>()

        let get (tess : int) =
            lock cacheg (fun () ->
                cacheg.GetOrCreate(tess, fun tess -> create tess)
            )

        let getSg (tess : int) =
            lock cache (fun () ->
                cache.GetOrCreate(tess, fun tess -> get tess |> Sg.ofIndexedGeometry)
            )

    module private Cone =
        let private create (tess : int) =
            
            let indices = System.Collections.Generic.List<int>()
            let positions = System.Collections.Generic.List<V3d>()
            let normals = System.Collections.Generic.List<V3d>()

            let icb = 0
            let ict = 1

            let step = Constant.PiTimesTwo / float tess

            // bottom cap
            positions.Add V3d.Zero
            normals.Add -V3d.OOI
            let mutable phi = 0.0
            let offset = positions.Count
            let center = offset - 1
            let mutable last = positions.Count + tess - 1
            for i in 0 .. tess - 1 do
                let i = offset + i
                let p = V3d(cos phi, sin phi, 0.0)
                positions.Add p
                normals.Add -V3d.OOI
                indices.Add i; indices.Add last; indices.Add center
                last <- i
                phi <- phi + step
                
            // side faces
            for i in 0 .. tess - 1 do
                let a0 = phi
                let a1 = a0 + step
                let p0 = V3d(cos a0, sin a0, 0.0)
                let p1 = V3d(cos a1, sin a1, 0.0)
                let p2 = V3d.OOI

                // p0 - p2 = (cos p0, sin p0, -1)
                // p1 - p2 = (cos p1, sin p1, -1)


                // sin p1 * cos (-p0) + cos p1 * sin (-p0) = sin(p1 - p0)

                // (sin p1 - sin p0, cos p0 - cos p1, cos p0 * sin p1 - cos p1 * sin p0)



                let n0 = V3d(sin a1 - sin a0, cos a0 - cos a1, sin (a1 - a0)) |> Vec.normalize
                indices.Add positions.Count; positions.Add p0; normals.Add n0
                indices.Add positions.Count; positions.Add p1; normals.Add n0
                indices.Add positions.Count; positions.Add p2; normals.Add n0

                phi <- a1

            let indices = indices.ToArray()
            let positions = positions.MapToArray(fun v -> V3f v)
            let normals = normals.MapToArray(fun v -> V3f v)


            let geometry = 
                IndexedGeometry(
                    Mode = IndexedGeometryMode.TriangleList,
                    IndexArray = indices,
                    IndexedAttributes =
                        SymDict.ofList [
                            DefaultSemantic.Positions, positions :> Array
                            DefaultSemantic.Normals, normals :> Array
                        ]
                )

            geometry

        let private cacheg = Dict<int, IndexedGeometry>()
        let private cache = Dict<int, ISg>()

        let get (tess : int) =
            lock cacheg (fun () ->
                cacheg.GetOrCreate(tess, fun tess -> create tess)
            )

        let getSg (tess : int) =
            lock cache (fun () ->
                cache.GetOrCreate(tess, fun tess -> get tess |> Sg.ofIndexedGeometry)
            )

    let private shuffle (index : int[]) (data : Array) : Array =
        let t = data.GetType().GetElementType()
        let res = Array.CreateInstance(t, index.Length)
        for i in 0 .. index.Length - 1 do
            res.SetValue(data.GetValue(index.[i]), i)
        res


    type IndexedGeometry with
        member x.Flat =
            if isNull x.IndexArray then x
            else
                let index = x.IndexArray |> unbox<int[]>
                let attributes =
                    x.IndexedAttributes |> SymDict.map (fun k v ->
                        shuffle index v
                    )

                IndexedGeometry(
                    Mode = x.Mode,
                    IndexedAttributes = attributes
                )

    module Primitives = 

        let unitBox =
            let box = Box3d.Unit
            let indices =
                [|
                    1;2;6; 1;6;5
                    2;3;7; 2;7;6
                    4;5;6; 4;6;7
                    3;0;4; 3;4;7
                    0;1;5; 0;5;4
                    0;3;2; 0;2;1
                |]

            let positions = 
                [|
                    V3f(box.Min.X, box.Min.Y, box.Min.Z)
                    V3f(box.Max.X, box.Min.Y, box.Min.Z)
                    V3f(box.Max.X, box.Max.Y, box.Min.Z)
                    V3f(box.Min.X, box.Max.Y, box.Min.Z)
                    V3f(box.Min.X, box.Min.Y, box.Max.Z)
                    V3f(box.Max.X, box.Min.Y, box.Max.Z)
                    V3f(box.Max.X, box.Max.Y, box.Max.Z)
                    V3f(box.Min.X, box.Max.Y, box.Max.Z)
                |]

            let normals = 
                [| 
                    V3f.IOO;
                    V3f.OIO;
                    V3f.OOI;

                    -V3f.IOO;
                    -V3f.OIO;
                    -V3f.OOI;
                |]

            let texcoords =
                [|
                    V2f.OO; V2f.IO; V2f.II;  V2f.OO; V2f.II; V2f.OI
                |]

            IndexedGeometry(
                Mode = IndexedGeometryMode.TriangleList,

                IndexedAttributes =
                    SymDict.ofList [
                        DefaultSemantic.Positions, indices |> Array.map (fun i -> positions.[i]) :> Array
                        DefaultSemantic.Normals, indices |> Array.mapi (fun ti _ -> normals.[ti / 6]) :> Array
                        DefaultSemantic.DiffuseColorCoordinates, indices |> Array.mapi (fun ti _ -> texcoords.[ti % 6]) :> Array
                    ]

            )
 
        let unitSphere (level : int) = Sphere.get level
        let unitCylinder (tess : int) = Cylinder.get tess
        let unitCone (tess : int) = Cone.get tess

    module Sg =
        open Aardvark.Base.Incremental.Operators

        let private unitWireBoxGeometry =
            let box = Box3d.Unit
            let indices =
                [|
                    1;2; 2;6; 6;5; 5;1;
                    2;3; 3;7; 7;6; 4;5; 
                    7;4; 3;0; 0;4; 0;1;
                |]

            let positions = 
                [|
                    V3f(box.Min.X, box.Min.Y, box.Min.Z)
                    V3f(box.Max.X, box.Min.Y, box.Min.Z)
                    V3f(box.Max.X, box.Max.Y, box.Min.Z)
                    V3f(box.Min.X, box.Max.Y, box.Min.Z)
                    V3f(box.Min.X, box.Min.Y, box.Max.Z)
                    V3f(box.Max.X, box.Min.Y, box.Max.Z)
                    V3f(box.Max.X, box.Max.Y, box.Max.Z)
                    V3f(box.Min.X, box.Max.Y, box.Max.Z)
                |]

            let normals = 
                [| 
                    V3f.IOO;
                    V3f.OIO;
                    V3f.OOI;

                    -V3f.IOO;
                    -V3f.OIO;
                    -V3f.OOI;
                |]

            IndexedGeometry(
                Mode = IndexedGeometryMode.LineList,

                IndexedAttributes =
                    SymDict.ofList [
                        DefaultSemantic.Positions, indices |> Array.map (fun i -> positions.[i]) :> Array
                        DefaultSemantic.Normals, indices |> Array.mapi (fun ti _ -> normals.[ti / 6]) :> Array
                    ]

            ) |> Sg.ofIndexedGeometry

        let private unitBox = Sg.ofIndexedGeometry Primitives.unitBox
        
        let fullScreenQuad =
            let drawCall = 
                DrawCallInfo(
                    FaceVertexCount = 4,
                    InstanceCount = 1
                )

            let positions =     [| V3f(-1,-1,0); V3f(1,-1,0); V3f(-1,1,0); V3f(1,1,0) |]
            let texcoords =     [| V2f(0,0); V2f(1,0); V2f(0,1); V2f(1,1) |]
            let normals =       [| V3f.OOI; V3f.OOI; V3f.OOI; V3f.OOI |]

            drawCall
                |> Sg.render IndexedGeometryMode.TriangleStrip 
                |> Sg.vertexAttribute DefaultSemantic.Positions (Mod.constant positions)
                |> Sg.vertexAttribute DefaultSemantic.Normals (Mod.constant normals)
                |> Sg.vertexAttribute DefaultSemantic.DiffuseColorCoordinates (Mod.constant texcoords)

        let farPlaneQuad =
            let drawCall = 
                DrawCallInfo(
                    FaceVertexCount = 4,
                    InstanceCount = 1
                )

            let positions =     [| V3f(-1,-1,1); V3f(1,-1,1); V3f(-1,1,1); V3f(1,1,1) |]
            let texcoords =     [| V2f(0,0); V2f(1,0); V2f(0,1); V2f(1,1) |]
            let normals =       [| V3f.OOI; V3f.OOI; V3f.OOI; V3f.OOI |]

            drawCall
                |> Sg.render IndexedGeometryMode.TriangleStrip 
                |> Sg.vertexAttribute DefaultSemantic.Positions (Mod.constant positions)
                |> Sg.vertexAttribute DefaultSemantic.Normals (Mod.constant normals)
                |> Sg.vertexAttribute DefaultSemantic.DiffuseColorCoordinates (Mod.constant texcoords)

        let coordinateCross (size : IMod<float>) =  
            let drawCall = DrawCallInfo(FaceVertexCount = 6, InstanceCount = 1)

            drawCall
                |> Sg.render IndexedGeometryMode.LineList
                |> Sg.vertexAttribute' DefaultSemantic.Positions [| V3f.OOO; V3f.IOO; V3f.OOO; V3f.OIO; V3f.OOO; V3f.OOI |]
                |> Sg.vertexAttribute' DefaultSemantic.Colors [| C4b.Red; C4b.Red; C4b.Green; C4b.Green; C4b.Blue; C4b.Blue |]
                |> Sg.trafo (size |> Mod.map Trafo3d.Scale)

        let coordinateCross' (size : float) = 
            coordinateCross (Mod.constant size)

        let box (color : IMod<C4b>) (bounds : IMod<Box3d>) =
            let trafo = bounds |> Mod.map (fun box -> Trafo3d.Scale(box.Size) * Trafo3d.Translation(box.Min))
            let color = color |> Mod.map (fun c -> c.ToC4f().ToV4f())
            unitBox
                |> Sg.vertexBufferValue DefaultSemantic.Colors color
                |> Sg.trafo trafo

        let box' (color : C4b) (bounds : Box3d) =
            box ~~color ~~bounds


        let wireBox (color : IMod<C4b>) (bounds : IMod<Box3d>) =
            let trafo = bounds |> Mod.map (fun box -> Trafo3d.Scale(box.Size) * Trafo3d.Translation(box.Min))
            let color = color |> Mod.map (fun c -> c.ToC4f().ToV4f())
            unitWireBoxGeometry
                |> Sg.vertexBufferValue DefaultSemantic.Colors color
                |> Sg.trafo trafo

        let wireBox' (color : C4b) (bounds : Box3d) =
            wireBox ~~color ~~bounds


        let frustum (color : IMod<C4b>) (view : IMod<CameraView>) (proj : IMod<Frustum>) =
            let invViewProj = Mod.map2 (fun v p -> (CameraView.viewTrafo v * Frustum.projTrafo p).Inverse) view proj

            Box3d(-V3d.III, V3d.III)
                |> Mod.constant
                |> wireBox color
                |> Sg.trafo invViewProj

        let lines (color : IMod<C4b>) (lines : IMod<Line3d[]>) =
            

            let call = 
                lines |> Mod.map (fun lines ->
                    DrawCallInfo(
                        FaceVertexCount = 2 * lines.Length,
                        InstanceCount = 1
                    )
                )

            let positions =
                lines |> Mod.map (fun l ->
                    l |> Array.collect (fun l -> [|V3f l.P0; V3f l.P1|])
                )
            
            Sg.RenderNode(call, Mod.constant IndexedGeometryMode.LineList)
                |> Sg.vertexAttribute DefaultSemantic.Positions positions
                |> Sg.vertexBufferValue DefaultSemantic.Colors (color |> Mod.map (fun c -> c.ToC4f().ToV4f()))

        let triangles (color : IMod<C4b>) (triangles : IMod<Triangle3d[]>) =
            let call = 
                triangles |> Mod.map (fun triangles ->
                    DrawCallInfo(
                        FaceVertexCount = 3 * triangles.Length,
                        InstanceCount = 1
                    )
                )

            let positions =
                triangles |> Mod.map (fun l ->
                    l |> Array.collect (fun l -> [|V3f l.P0; V3f l.P1; V3f l.P2|])
                )
            
            Sg.RenderNode(call, Mod.constant IndexedGeometryMode.TriangleList)
                |> Sg.vertexAttribute DefaultSemantic.Positions positions
                |> Sg.vertexBufferValue DefaultSemantic.Colors (color |> Mod.map (fun c -> c.ToC4f().ToV4f()))

        let unitSphere (level : int) (color : IMod<C4b>) =
            Sphere.getSg level
                |> Sg.vertexBufferValue DefaultSemantic.Colors (color |> Mod.map (fun c -> c.ToC4f() |> V4f))

        let sphere (level : int) (color : IMod<C4b>) (radius : IMod<float>)  =
            Sphere.getSg level
                |> Sg.vertexBufferValue DefaultSemantic.Colors (color |> Mod.map (fun c -> c.ToC4f() |> V4f))
                |> Sg.trafo (radius |> Mod.map Trafo3d.Scale)

        let unitSphere' (level : int) (color : C4b) =
            unitSphere level (Mod.constant color)

        let sphere' (level : int) (color : C4b) (radius : float) =
            sphere level (Mod.constant color) (Mod.constant radius)

        let cylinder (tess : int) (color : IMod<C4b>) (radius : IMod<float>) (height : IMod<float>) =
            let trafo = Mod.map2 (fun r h -> Trafo3d.Scale(r,r,h)) radius height
            Cylinder.getSg tess
                |> Sg.vertexBufferValue DefaultSemantic.Colors (color |> Mod.map (fun c -> c.ToC4f() |> V4f))
                |> Sg.trafo trafo

        let cylinder' (tess : int) (color : C4b) (radius : float) (height : float) =
            let trafo = Trafo3d.Scale(radius,radius,height)
            Cylinder.getSg tess
                |> Sg.vertexBufferValue DefaultSemantic.Colors (color.ToC4f() |> V4f |> Mod.constant)
                |> Sg.transform trafo

        let cone (tess : int) (color : IMod<C4b>) (radius : IMod<float>) (height : IMod<float>) =
            let trafo = Mod.map2 (fun r h -> Trafo3d.Scale(r,r,h)) radius height
            Cone.getSg tess
                |> Sg.vertexBufferValue DefaultSemantic.Colors (color |> Mod.map (fun c -> c.ToC4f() |> V4f))
                |> Sg.trafo trafo

        let cone' (tess : int) (color : C4b) (radius : float) (height : float) =
            let trafo = Trafo3d.Scale(radius,radius,height) |> Mod.constant
            Cone.getSg tess
                |> Sg.vertexBufferValue DefaultSemantic.Colors (color.ToC4f() |> V4f |> Mod.constant)
                |> Sg.trafo trafo