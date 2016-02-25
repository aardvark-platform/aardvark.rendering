#if INTERACTIVE
#I @"../../../bin/Debug"
#I @"../../../bin/Release"
#load "LoadReferences.fsx"
#else
namespace Examples
#endif

open System
open Aardvark.Base
open Aardvark.Rendering.Interactive

//open Default // makes viewTrafo and other tutorial specicific default creators visible


open System.Collections.Generic
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application

module LoD = 

    Aardvark.Rendering.Interactive.FsiSetup.init (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug"])


    // ===================================================================================
    // kill entirely?
    // ===================================================================================
    [<CustomEquality; NoComparison>]
    type GridCell =
        struct
            val mutable public Id : V3l
            val mutable public Exponent : int


            member x.Box =
                let size = pow 2.0 (float x.Exponent)
                let center = V3d(x.Id) * size
                Box3d.FromMinAndSize(center, V3d(size, size, size))

            member x.Children =
                let baseId = 2L * x.Id
                let exp = x.Exponent - 1
                [
                    GridCell(baseId + V3l.OOO, exp)
                    GridCell(baseId + V3l.OOI, exp)
                    GridCell(baseId + V3l.OIO, exp)
                    GridCell(baseId + V3l.OII, exp)
                    GridCell(baseId + V3l.IOO, exp)
                    GridCell(baseId + V3l.IOI, exp)
                    GridCell(baseId + V3l.IIO, exp)
                    GridCell(baseId + V3l.III, exp)
                ]

            member x.Parent =
                let fp = V3d.op_Explicit x.Id / 2.0

                let id =
                    V3l(
                        (if fp.X < 0.0 then floor fp.X else ceil fp.X),
                        (if fp.Y < 0.0 then floor fp.Y else ceil fp.Y),
                        (if fp.Z < 0.0 then floor fp.Z else ceil fp.Z)
                    )

                GridCell(id, x.Exponent + 1)

            override x.ToString() =
                sprintf "{ id = %A; exponent = %d; box = %A }" x.Id x.Exponent x.Box

            override x.GetHashCode() =
                HashCode.Combine(x.Id.GetHashCode(), x.Exponent.GetHashCode())

            override x.Equals o =
                match o with
                    | :? GridCell as o -> x.Id = o.Id && x.Exponent = o.Exponent
                    | _ -> false

            new(id : V3l, exp : int) = { Id = id; Exponent = exp }
        end

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module GridCell =
    
        let inline parent (c : GridCell) = c.Parent
        let inline children (c : GridCell) = c.Children
        let inline box (c : GridCell) = c.Box

        type V3l with
            static member Floor(v : V3d) = V3l(floor v.X, floor v.Y, floor v.Z)
            static member Round(v : V3d) = V3l(round v.X, round v.Y, round v.Z)
            static member Ceil(v : V3d) = V3l(ceil v.X, ceil v.Y, ceil v.Z)

        let containingCells (b : Box3d) =
            let exp = Fun.Log2 b.Size.NormMax |> ceil
            let mutable size = pow 2.0 exp
            let mutable exp = int exp
            let mutable minId = (b.Min + 10.0 * Constant<float>.PositiveTinyValue) / size |> V3l.Floor
            let mutable maxId = (b.Max - 10.0 * Constant<float>.PositiveTinyValue) / size |> V3l.Floor
                                 
            [ for x in minId.X..maxId.X do
                for y in minId.Y..maxId.Y do
                    for z in minId.Z..maxId.Z do
                        yield GridCell(V3l(x,y,z), exp)
            ]

        let inline px (c : GridCell) = GridCell(c.Id + V3l.IOO, c.Exponent)
        let inline py (c : GridCell) = GridCell(c.Id + V3l.OIO, c.Exponent)
        let inline pz (c : GridCell) = GridCell(c.Id + V3l.OOI, c.Exponent)
        let inline nx (c : GridCell) = GridCell(c.Id - V3l.IOO, c.Exponent)
        let inline ny (c : GridCell) = GridCell(c.Id - V3l.OIO, c.Exponent)
        let inline nz (c : GridCell) = GridCell(c.Id - V3l.OOI, c.Exponent)

        let viewVolumeCells (viewProj : Trafo3d) =
            Box3d(-V3d.III, V3d.III).ComputeCorners() 
                |> Array.map (fun p -> viewProj.Backward.TransformPosProj(p))
                |> Box3d
                |> containingCells



        let raster (split : GridCell -> Polygon2d -> Range1d -> bool) (view : CameraView) (proj : Frustum) =
            let view = CameraView.viewTrafo view
            let projTrafo = Frustum.projTrafo proj
            let viewProj = view * projTrafo

            let split (b : GridCell) =
        
                let viewSpacePoints =
                    b.Box.ComputeCorners()
                        |> Array.map (fun v ->
                            let vp = view.Forward.TransformPos(v)
                            let res = projTrafo.Forward.TransformPosProj(vp)

                            let ld = -vp.Z / (proj.far - proj.near)
                            V3d(res.XY, ld)
                        )

                let poly =
                    viewSpacePoints 
                        |> Array.map Vec.xy
                        |> Polygon2d

                let clipped = 
                    poly.ComputeConvexHullIndexPolygon().ToPolygon2d().ConvexClipped(Box2d(-V2d.II, V2d.II))


                let zrange = viewSpacePoints |> Array.map (fun v -> clamp -1.0 1.0 v.Z) |> Range1d


                //printfn "{ area = %A; distance = %A }" area distance
                //printfn "%A, %A -> %A" distance area f
                split b clipped zrange
        
            let rec splitCells (current : list<GridCell>) =
                match current with
                    | [] -> []
                    | _ ->
                        let split, keep = 
                            current |> List.partition split

                        let nested = 
                            split 
                                |> List.collect children 
                                |> List.filter (fun c -> c.Box.IntersectsFrustum(viewProj.Forward))
                                |> splitCells

                        keep @ nested

            viewProj 
                |> viewVolumeCells 
                |> splitCells
                |> List.filter (fun c -> c.Box.IntersectsFrustum(viewProj.Forward))



        let test() =
            let view = CameraView.lookAt (V3d(3,3,3)) V3d.Zero V3d.OOI
            let proj = Frustum.perspective 60.0 0.1 1000.0 1.0

            let decide _ (poly : Polygon2d) (range : Range1d) =
                let f = poly.ComputeArea() / (range.Min * range.Min)
                f > 0.4

            let mutable cnt = 0
            let mutable bounds = Box3d.Invalid
            let cells = raster decide view proj

            let iter = 100
            let sw = System.Diagnostics.Stopwatch()
            sw.Start()
            for i in 1..iter do
                let cells = raster decide view proj
                ()
            sw.Stop()

            for c in cells do
                printfn "%A" c
                cnt <- cnt + 1
                bounds <- Box3d.Union(bounds, c.Box)

            printfn "took %.3f ms" (sw.Elapsed.TotalMilliseconds / float iter)
            printfn "count = %A" cnt
            printfn "box = %A" bounds

    module Helpers = 
        let rand = Random()
        let randomPoints (bounds : Box3d) (pointCount : int) =
            let size = bounds.Size
            let randomV3f() = V3d(rand.NextDouble(), rand.NextDouble(), rand.NextDouble()) * size + bounds.Min |> V3f.op_Explicit
            let randomColor() = C4b(rand.NextDouble(), rand.NextDouble(), rand.NextDouble(), 1.0)

            IndexedGeometry(
                Mode = IndexedGeometryMode.PointList,
                IndexedAttributes = 
                    SymDict.ofList [
                            DefaultSemantic.Positions, Array.init pointCount (fun _ -> randomV3f()) :> Array
                            DefaultSemantic.Colors, Array.init pointCount (fun _ -> randomColor()) :> Array
                    ]
            )

        let randomColor() =
            C4b(128 + rand.Next(127) |> byte, 128 + rand.Next(127) |> byte, 128 + rand.Next(127) |> byte, 255uy)
        let randomColor2(alpha) =
            C4b(rand.Next(255) |> byte, rand.Next(255) |> byte, rand.Next(255) |> byte, alpha)

        let box (color : C4b) (box : Box3d) =

            let randomColor = color //C4b(rand.Next(255) |> byte, rand.Next(255) |> byte, rand.Next(255) |> byte, 255uy)

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

            IndexedGeometry(
                Mode = IndexedGeometryMode.TriangleList,

                IndexedAttributes =
                    SymDict.ofList [
                        DefaultSemantic.Positions, indices |> Array.map (fun i -> positions.[i]) :> Array
                        DefaultSemantic.Normals, indices |> Array.mapi (fun ti _ -> normals.[ti / 6]) :> Array
                        DefaultSemantic.Colors, indices |> Array.map (fun _ -> randomColor) :> Array
                    ]

            )

        let wireBox (color : C4b) (box : Box3d) =
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
                        DefaultSemantic.Colors, indices |> Array.map (fun _ -> color) :> Array
                    ]

            )

        let frustum (f : IMod<CameraView>) (proj : IMod<Frustum>) =
            let invViewProj = Mod.map2 (fun v p -> (CameraView.viewTrafo v * Frustum.projTrafo p).Inverse) f proj

            let positions = 
                [|
                    V3f(-1.0, -1.0, -1.0)
                    V3f(1.0, -1.0, -1.0)
                    V3f(1.0, 1.0, -1.0)
                    V3f(-1.0, 1.0, -1.0)
                    V3f(-1.0, -1.0, 1.0)
                    V3f(1.0, -1.0, 1.0)
                    V3f(1.0, 1.0, 1.0)
                    V3f(-1.0, 1.0, 1.0)
                |]

            let indices =
                [|
                    1;2; 2;6; 6;5; 5;1;
                    2;3; 3;7; 7;6; 4;5; 
                    7;4; 3;0; 0;4; 0;1;
                |]

            let geometry =
                IndexedGeometry(
                    Mode = IndexedGeometryMode.LineList,
                    IndexedAttributes =
                        SymDict.ofList [
                            DefaultSemantic.Positions, indices |> Array.map (fun i -> positions.[i]) :> Array
                            DefaultSemantic.Colors, Array.create indices.Length C4b.Red :> Array
                        ]
                )

            geometry
                |> Sg.ofIndexedGeometry
                |> Sg.trafo invViewProj



    // ===================================================================================
    // move to Aardvark.Base
    // ===================================================================================
    module ASet =
        type CustomReader<'a>(f : IReader<'a> -> list<Delta<'a>>) =
            inherit ASetReaders.AbstractReader<'a>()

            override x.ComputeDelta() =
                f x

            override x.Release() =
                ()

            override x.Inputs = Seq.empty
    
        let custom (f : IReader<'a> -> list<Delta<'a>>) =
            ASet.AdaptiveSet(fun () -> new CustomReader<'a>(f) :> IReader<_>) :> aset<_>


        type WithUpdateReader<'a>(m : IMod<unit>, r : IReader<'a>) =
            inherit ASetReaders.AbstractReader<'a>()


            override x.ComputeDelta() =
                m.GetValue x
                r.GetDelta x

            override x.Release() =
                r.Dispose()

            override x.Inputs = Seq.ofList [m; r]


        let withUpdater (m : IMod<unit>) (s : aset<'a>) =
            ASet.AdaptiveSet(fun () -> new WithUpdateReader<_>(m, s.GetReader()) :> IReader<_>) :> aset<_>


    // ===================================================================================
    // LoD stuff
    // ===================================================================================
    [<CustomEquality; NoComparison>]
    type Node =
        {
            id : obj
            bounds : Box3d
            inner : bool
            granularity : float
        }

        override x.GetHashCode() = x.id.GetHashCode()
        override x.Equals o =
            match o with
                | :? Node as o -> x.id.Equals(o.id)
                | _ -> false


    type IDataProvider =
        abstract member BoundingBox : Box3d
        abstract member Traverse : (Node -> bool) -> unit
        abstract member GetData : node : Node -> count : int -> Async<IndexedGeometry>


    type DummyDataProvider(root : Box3d) =
    
        interface IDataProvider with
            member x.BoundingBox = root

            member x.Traverse f =
                let rec traverse (level : int) (b : Box3d) =
                    let box = b
                    let n = 100.0
                    let node = { id = b; bounds = box; inner = true; granularity = Fun.Cbrt(box.Volume / (n*n*n)) }

                    if f node then
                        let center = b.Center

                        let children =
                            let l = b.Min
                            let u = b.Max
                            let c = center
                            [
                                Box3d(V3d(l.X, l.Y, l.Z), V3d(c.X, c.Y, c.Z))
                                Box3d(V3d(c.X, l.Y, l.Z), V3d(u.X, c.Y, c.Z))
                                Box3d(V3d(l.X, c.Y, l.Z), V3d(c.X, u.Y, c.Z))
                                Box3d(V3d(c.X, c.Y, l.Z), V3d(u.X, u.Y, c.Z))
                                Box3d(V3d(l.X, l.Y, c.Z), V3d(c.X, c.Y, u.Z))
                                Box3d(V3d(c.X, l.Y, c.Z), V3d(u.X, c.Y, u.Z))
                                Box3d(V3d(l.X, c.Y, c.Z), V3d(c.X, u.Y, u.Z))
                                Box3d(V3d(c.X, c.Y, c.Z), V3d(u.X, u.Y, u.Z))
                            ]

                        children |> List.iter (traverse (level + 1))
                    else
                        ()
                traverse 0 root

            member x.GetData (cell : Node) (count : int) =
                async {
                    do! Async.SwitchToThreadPool()
                    let b = Helpers.box (Helpers.randomColor()) cell.bounds
                    do! Async.Sleep 200
                    return b
                }

    [<AutoOpen>]
    module ``Data Provider Extensions`` =
        open System.Collections.Concurrent

        type IDataProvider with
            member x.Rasterize(view : Trafo3d, frustum : Frustum, wantedNearPlaneDistance : float) =
                let projTrafo = Frustum.projTrafo frustum
                let viewProj = view * projTrafo
                let camLocation = view.GetViewPosition()

                let result = HashSet<Node>()

                x.Traverse(fun node ->
                    if node.bounds.IntersectsFrustum viewProj.Forward then
                        if node.inner then
                            let bounds = node.bounds

                            let depthRange =
                                bounds.ComputeCorners()
                                    |> Array.map view.Forward.TransformPos
                                    |> Array.map (fun v -> -v.Z)
                                    |> Range1d

                            if depthRange.Max < frustum.near || depthRange.Min > frustum.far then
                                false
                            else
                                let projAvgDistance =
                                    abs (node.granularity / depthRange.Min)

                                if projAvgDistance > wantedNearPlaneDistance then
                                    true
                                else
                                    result.Add node |> ignore
                                    false
                        else
                            result.Add node |> ignore
                            false
                    else
                        false
                )

                result


    // ===================================================================================
    // example usage
    // ===================================================================================
    let data = DummyDataProvider(Box3d(V3d.OOO, 5.0 * V3d.III)) :> IDataProvider

    [<AutoOpen>]
    module Camera =
        type Mode =
            | Main
            | Test

        let mode = Mod.init Main

        let currentMain = ref (CameraView.lookAt (V3d(3,3,3)) V3d.Zero V3d.OOI)
        let currentTest = ref (CameraView.lookAt (V3d(3,3,3)) V3d.Zero V3d.OOI)

        let mainCam =
            adaptive {
                let! mode = mode
                match mode with
                    | Main ->
                        let! m = DefaultCameraController.control win.Mouse win.Keyboard win.Time !currentMain
                        currentMain := m
                        return m
                    | _ ->
                        return !currentMain
            }

        let gridCam =
            adaptive {
                let! mode = mode
                match mode with
                    | Test ->
                        let! m = DefaultCameraController.control win.Mouse win.Keyboard win.Time !currentTest
                        currentTest := m
                        return m
                    | _ ->
                        return !currentTest
            }

        let view =
            adaptive {
                let! mode = mode
                match mode with
                    | Main -> return! mainCam
                    | Test -> return! gridCam
            }

        win.Keyboard.KeyDown(Keys.Space).Values.Add(fun _ ->
            transact (fun () ->
                match mode.Value with
                    | Main -> Mod.change mode Test
                    | Test -> Mod.change mode Main

                printfn "mode: %A" mode.Value
            )
        )

        let mainProj = perspective win
        let gridProj = Frustum.perspective 60.0 1.0 50.0 1.0 |> Mod.constant

        let proj =
            adaptive {
                let! mode = mode 
                match mode with
                    | Main -> return! mainProj
                    | Test -> return! gridProj
            }

    let nodes =
        ASet.custom (fun self ->
            let proj = gridProj.GetValue self
            let view = gridCam.GetValue self
            let set = data.Rasterize(CameraView.viewTrafo view, proj, 0.005)

            let viewProj = CameraView.viewTrafo view * Frustum.projTrafo proj

            let add = set |> Seq.filter (self.Content.Contains >> not) |> Seq.map Add
            let rem = self.Content |> Seq.filter (set.Contains >> not) |> Seq.map Rem

            Seq.append add rem |> Seq.toList
        )

    let attributeTypes =
        Map.ofList [
            DefaultSemantic.Positions, typeof<V3f>
            DefaultSemantic.Colors, typeof<C4b>
            DefaultSemantic.Normals, typeof<V3f>
        ]

    module Sg =
        open Aardvark.SceneGraph.Semantics
        open System.Collections.Concurrent

        type PointCloud(data : IDataProvider, avgDistance : float) =
            interface ISg

            member x.AverageDistance = avgDistance
            member x.DataProvider = data


    let boxes = 
        nodes 
            |> ASet.mapAsync (fun n -> data.GetData n 100)
            //|> ASet.map (fun n -> data.GetData n 100 |> Async.RunSynchronously)
            |> Sg.geometrySet IndexedGeometryMode.TriangleList attributeTypes
            //|> ASet.map (fun n -> Helpers.box (Helpers.randomColor()) n.bounds)

                                    
    let sg = 
        Sg.group' [
            boxes
            Helpers.frustum gridCam gridProj

            data.BoundingBox.EnlargedByRelativeEps(0.005)
                |> Helpers.wireBox C4b.VRVisGreen
                |> Sg.ofIndexedGeometry
        ]

    let final =
        sg |> Sg.effect [
                DefaultSurfaces.trafo |> toEffect                  
                DefaultSurfaces.vertexColor  |> toEffect 
                ]
            // viewTrafo () creates camera controls and returns IMod<ICameraView> which we project to its view trafo component by using CameraView.viewTrafo
            |> Sg.viewTrafo (view |> Mod.map CameraView.viewTrafo ) 
            // perspective () connects a proj trafo to the current main window (in order to take account for aspect ratio when creating the matrices.
            // Again, perspective() returns IMod<Frustum> which we project to its matrix by mapping ofer Frustum.projTrafo.
            |> Sg.projTrafo (proj |> Mod.map Frustum.projTrafo    )
            //|> Sg.fillMode (Mod.constant FillMode.Line)
            //|> Sg.trafo (Mod.constant (Trafo3d.Scale 0.1))
    
    let run() =
        Aardvark.Rendering.Interactive.FsiSetup.init (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug"])
        setSg final
        win.Run()

open LoD

#if INTERACTIVE
setSg final
#else
#endif