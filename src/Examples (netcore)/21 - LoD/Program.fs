open System
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application

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
    let randomColor2 ()  =
        C4b(rand.Next(255) |> byte, rand.Next(255) |> byte, rand.Next(255) |> byte, 255uy)

    let frustum (f : aval<CameraView>) (proj : aval<Frustum>) =
        let invViewProj = AVal.map2 (fun v p -> (CameraView.viewTrafo v * Frustum.projTrafo p).Inverse) f proj

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

type Mode =
    | Main
    | Test
            
module LoD = 

    // ===================================================================================
    // example usage
    // ===================================================================================
    type DummyDataProvider(root : Box3d) =
    

        let wert = 
            lazy (
                
                let rec traverse (level : int) (b : Box3d) =
                    let box = b
                    let n = 600

                    let center = b.Center

                    let children =
                        let l = b.Min
                        let u = b.Max
                        let c = center
                        [|
                            Box3d(V3d(l.X, l.Y, l.Z), V3d(c.X, c.Y, c.Z))
                            Box3d(V3d(c.X, l.Y, l.Z), V3d(u.X, c.Y, c.Z))
                            Box3d(V3d(l.X, c.Y, l.Z), V3d(c.X, u.Y, c.Z))
                            Box3d(V3d(c.X, c.Y, l.Z), V3d(u.X, u.Y, c.Z))
                            Box3d(V3d(l.X, l.Y, c.Z), V3d(c.X, c.Y, u.Z))
                            Box3d(V3d(c.X, l.Y, c.Z), V3d(u.X, c.Y, u.Z))
                            Box3d(V3d(l.X, c.Y, c.Z), V3d(c.X, u.Y, u.Z))
                            Box3d(V3d(c.X, c.Y, c.Z), V3d(u.X, u.Y, u.Z))
                        |]

                    let kids = 
                        if level < 6 then
                            children |> Array.map (traverse (level + 1)) |> Some
                        else
                            None

                    { id = b; level = level; bounds = box;  pointCountNode = 100L; pointCountTree = 100L; children = kids }

                traverse 0 root :> ILodDataNode
            )

        interface ILodData with
            member x.BoundingBox = root

            member x.RootNode() = wert.Value

            member x.Dependencies = []

            member x.GetData (cell : ILodDataNode) =
                async {
                    //do! Async.SwitchToThreadPool()
                    let box = cell.Bounds

                    let points =
                        [|
                            for x in 0 .. 9 do
                                for y in 0 .. 9 do
                                    for z in 0 .. 9 do
                                        //if x = 0 || x = 9 || y = 0 || y = 9 || z = 0 || z = 9 then
                                            yield V3d(x,y,z)*0.1*box.Size + box.Min |> V3f.op_Explicit
                                            
                        |]

                    //let points = 
                    //    [| for x in 0 .. 9 do
                    //         for y in 0 .. 9 do
                    //            for z in 0 .. 9 do
                    //                yield V3d(x,y,z)*0.1*box.Size + box.Min |> V3f.op_Explicit
                    //     |]
                    let colors = Array.create points.Length (Helpers.randomColor())
                    //let points = Helpers.randomPoints cell.bounds 1000
                    //let b = Helpers.box (Helpers.randomColor()) cell.bounds
//                  
                    //do! Async.Sleep(100)
                    let mutable a = 0

//                    for i in 0..(1 <<< 20) do a <- a + 1
//
//                    let a = 
//                        let mutable a = 0
//                        for i in 0..(1 <<< 20) do a <- a + 1
//                        a

                    return Some <| IndexedGeometry(Mode = unbox a, IndexedAttributes = SymDict.ofList [ DefaultSemantic.Positions, points :> Array; DefaultSemantic.Colors, colors :> System.Array])
                }

    let data = DummyDataProvider(Box3d(V3d.OOO, 20.0 * V3d.III)) :> ILodData


    module Instanced =
        open FShade
        open Aardvark.SceneGraph.Semantics
        type Vertex = { 
                [<Position>]      pos   : V4d 
                [<Color>]         col   : V4d
                [<PointSize>] blubb : float
            }

        let trafo (v : Vertex) =
            vertex {
                return { 
                    v with blubb = 1.0
                           col = V4d(v.col.XYZ,0.5)
                }
            }
            
    let eff =
        let effects = [
            Instanced.trafo |> toEffect           
            DefaultSurfaces.vertexColor  |> toEffect         
        ]
        let e = FShade.Effect.compose effects
        FShadeSurface.Get(e) :> ISurface 


    let progress = 
        {   
            LodProgress.activeNodeCount     = ref 0
            LodProgress.expectedNodeCount   = ref 0
            LodProgress.dataAccessTime      = ref 0L
            LodProgress.rasterizeTime       = ref 0L
        }

    let pointCloud data config =
        Sg.pointCloud data config

      
 open LoD

[<EntryPoint>]
let main argv = 
    
    Aardvark.Init()

    let img = PixImage.Load(@"C:\volumes\bigger.png") |> unbox<PixImage<byte>>
    let a = DistanceField.computeDistance img
    let a = DistanceField.computeDistance img
    let a = DistanceField.computeDistance img
    let a = DistanceField.computeDistance img
    let r = DistanceField.computeDistanceGPU img
    r.Save(@"C:\volumes\gabbl.tiff")

    System.Environment.Exit 0
    
    let win = window {
        backend Backend.GL
        display Display.Mono
        debug true
        samples 1
    }

    let mode = AVal.init Main

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
                | Main -> mode.Value <- Test
                | Test -> mode.Value <- Main

            printfn "mode: %A" mode.Value
        )
    )


    let mainProj = win.Sizes |> AVal.map (fun s -> Frustum.perspective 60.0 0.01 10.0 (float s.X / float s.Y))
    let gridProj = Frustum.perspective 60.0 1.0 50.0 1.0 |> AVal.constant

    let proj =
        adaptive {
            let! mode = mode 
            match mode with
                | Main -> return! mainProj
                | Test -> return! gridProj
        }

    let cloud =
        pointCloud data {
            lodRasterizer           = AVal.constant (LodData.defaultRasterizeSet 5.0)
            freeze                  = AVal.constant false
            maxReuseRatio           = 0.5
            minReuseCount           = 1L <<< 20
            pruneInterval           = 500
            customView              = Some (gridCam |> AVal.map CameraView.viewTrafo)
            customProjection        = Some (gridProj |> AVal.map Frustum.projTrafo)
            attributeTypes =
                Map.ofList [
                    DefaultSemantic.Positions, typeof<V3f>
                    DefaultSemantic.Colors, typeof<C4b>
                ]
            boundingBoxSurface      = None //Some surf
            progressCallback        = None
        } 

    let sg = 
        Sg.ofList [
            cloud
                |> Sg.effect [
                    DefaultSurfaces.trafo        |> toEffect 
                    Instanced.trafo              |> toEffect                  
                    DefaultSurfaces.vertexColor  |> toEffect         
                ]
            Helpers.frustum gridCam gridProj

            data.BoundingBox.EnlargedByRelativeEps(0.005)
                |> Sg.wireBox' C4b.VRVisGreen 

        ]

    let final =
        sg |> Sg.effect [
                DefaultSurfaces.trafo |> toEffect                
                DefaultSurfaces.vertexColor  |> toEffect 
                ]
            |> Sg.viewTrafo (view |> AVal.map CameraView.viewTrafo ) 
            |> Sg.projTrafo (proj |> AVal.map Frustum.projTrafo    )
            |> Sg.uniform "PointSize" (AVal.constant 4.0)
            |> Sg.uniform "ViewportSize" win.Sizes

    let cnt = 100000000
    let points =
        let rand = System.Random()
        Array.init cnt (fun _ -> V3f(rand.NextDouble(),rand.NextDouble(),rand.NextDouble())*1000.0f)
    
    let buffer = win.Runtime.PrepareBuffer(ArrayBuffer(points), BufferUsage.All) :> IBuffer

    let drawCallsCnt = AVal.init 1

    win.Keyboard.KeyDown(Keys.G).Values.Add ( fun _ ->
        transact ( fun _ -> drawCallsCnt.Value <- drawCallsCnt.Value + 10000 )
        Log.line "draw calls: %A" drawCallsCnt.Value
    )

    let final = 
        drawCallsCnt 
        |> AVal.map (fun drawCallsCnt ->
            let pointsPerCall = cnt / drawCallsCnt
            [| 
                for c in 0 .. drawCallsCnt - 1 do
                    let start = c * pointsPerCall
                    printfn "start: %d, count: %d" start pointsPerCall
                    let dci = 
                        DrawCallInfo(BaseVertex = start, FirstIndex = start,
                                        FaceVertexCount = pointsPerCall, FirstInstance = 0, InstanceCount = 1)
                    yield Sg.render IndexedGeometryMode.PointList dci
            |] |> Sg.ofArray
        ) 
        |> Sg.dynamic 
        |> Sg.vertexBuffer DefaultSemantic.Positions (BufferView(AVal.constant buffer, typeof<V3f>))
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.constantColor C4f.Red
            }
        |> Sg.viewTrafo (view |> AVal.map CameraView.viewTrafo)
        |> Sg.projTrafo (proj |> AVal.map Frustum.projTrafo)
 

    win.Scene <- final
    win.Run()

    0
