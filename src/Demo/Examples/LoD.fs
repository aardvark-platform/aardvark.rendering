#if INTERACTIVE
#I @"../../../bin/Debug"
#I @"../../../bin/Release"
#load "LoadReferences.fsx"
#else
namespace Examples
#endif

open System
open System.Collections.Generic

open Aardvark.Base
open Aardvark.Rendering.Interactive
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
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



module LoD = 

    Interactive.Renderer <- RendererConfiguration.GL
    //FsiSetup.initFsi (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug";"Examples.exe"])

    let win = Interactive.Window


    // ===================================================================================
    // example usage
    // ===================================================================================
    type DummyDataProvider(root : Box3d) =
    
        interface ILodData with
            member x.BoundingBox = root

            member x.Traverse f =
                let rec traverse (level : int) (b : Box3d) =
                    let box = b
                    let n = 600
                    let node = { id = b; level = level; bounds = box; inner = true; pointCountNode = 100L; pointCountTree = 100L; render = true}

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

            member x.Dependencies = []

            member x.GetData (cell : LodDataNode) =
                async {
                    //do! Async.SwitchToThreadPool()
                    let box = cell.bounds

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

        win.Keyboard.KeyDown(Keys.P).Values.Add(fun _ ->
            let task = win.RenderTask
            
            printfn "%A (%A)" task task.OutOfDate
            printfn "%A" view
        )

        let mainProj = Interactive.DefaultFrustum  
        let gridProj = Frustum.perspective 60.0 1.0 50.0 1.0 |> Mod.constant

        let proj =
            adaptive {
                let! mode = mode 
                match mode with
                    | Main -> return! mainProj
                    | Test -> return! gridProj
            }

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
                    v with blubb = 10.5
                           col = V4d(v.col.XYZ,0.5)
                }
            }
            
    let eff =
        let effects = [
            Instanced.trafo |> toEffect           
            DefaultSurfaces.vertexColor  |> toEffect         
        ]
        let e = FShade.Effect.compose effects
        FShadeSurface(e) :> ISurface 
//
//    let surf = 
//        win.Runtime.PrepareSurface(
//            win.FramebufferSignature,
//            eff
//        ) :> ISurface |> Mod.constant

    let cloud =
        Sg.pointCloud data {
            lodDecider              = Mod.constant (LodData.defaultLodDecider 8.0)
            maxReuseRatio           = 0.5
            minReuseCount           = 1L <<< 20
            pruneInterval           = 500
            customView              = Some (gridCam |> Mod.map CameraView.viewTrafo)
            customProjection        = Some (gridProj |> Mod.map Frustum.projTrafo)
            attributeTypes =
                Map.ofList [
                    DefaultSemantic.Positions, typeof<V3f>
                    DefaultSemantic.Colors, typeof<C4b>
                ]
            boundingBoxSurface      = None //Some surf
            progressCallback        = Action<_>(ignore)
        } 
                     
    let sg = 
        Sg.group' [
            cloud
                |> Sg.effect [
                    DefaultSurfaces.trafo |> toEffect 
                    Instanced.trafo |> toEffect                  
                    DefaultSurfaces.vertexColor  |> toEffect         
                    //DefaultSurfaces.pointSprite  |> toEffect     
                    //DefaultSurfaces.pointSpriteFragment  |> toEffect 
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
            |> Sg.viewTrafo (view |> Mod.map CameraView.viewTrafo ) 
            |> Sg.projTrafo (proj |> Mod.map Frustum.projTrafo    )
            |> Sg.uniform "PointSize" (Mod.constant 4.0)
            |> Sg.uniform "ViewportSize" win.Sizes
    
    let run() =
        //Aardvark.Rendering.Interactive.FsiSetup.init (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug"])
        Interactive.SceneGraph <- final
        Interactive.RunMainLoop()



open LoD

#if INTERACTIVE
Interactive.SceneGraph <- final
#else
#endif