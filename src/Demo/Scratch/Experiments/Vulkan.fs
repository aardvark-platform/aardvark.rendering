module Vulkan

open System
open System.Threading
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.SceneGraph
open Aardvark.Base.Incremental.Operators


type App private() =
    static let emptySg = Sg.set ASet.empty
    
    static let mutable instance = None




    do Aardvark.Init()
    let mutable initialized = 0
    let app = new VulkanApplication(true)
    let win = app.CreateSimpleRenderWindow()
    let sg = Mod.init emptySg
    let mutable clear = Unchecked.defaultof<IRenderTask>
    let mutable render = Unchecked.defaultof<IRenderTask>
    
    let view =
        let initial = CameraView.lookAt (V3d(3,3,3)) V3d.Zero V3d.OOI
        initial |> DefaultCameraController.control win.Mouse win.Keyboard win.Time

    let proj = 
        win.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y))

    let init() =
        if Interlocked.Exchange(&initialized, 1) = 0 then
            let sg =
                Sg.dynamic sg
                    |> Sg.viewTrafo (view |> Mod.map CameraView.viewTrafo)
                    |> Sg.projTrafo (proj |> Mod.map Frustum.projTrafo)

            clear <- app.Runtime.CompileClear(win.FramebufferSignature, ~~C4f.Gray30, ~~1.0)
            render <- app.Runtime.CompileRender(win.FramebufferSignature, sg)
    
            let task = RenderTask.ofList [clear; render]
            win.RenderTask <- task
            win.Show()

    do init()

    static member Instance =
        match instance with
            | Some i -> i
            | None ->
                let i = new App()
                instance <- Some i
                i

    member x.Control = win :> IRenderControl

    member x.Clear() =
        transact (fun () -> sg.Value <- emptySg)

    member x.SceneGraph 
        with get () = sg.Value
        and set (v : ISg) = 
            transact (fun () -> sg.Value <- v)

    member x.Dispose() = ()
//        if Interlocked.Exchange(&initialized, 0) = 1 then
//            win.Close()
//            win.Dispose()
//            (app :> IDisposable).Dispose()
       


    member x.Run() =
        win.Run()
        
    interface IDisposable with
        member x.Dispose() = x.Dispose()


module Simple = 
    let run() =
        let app = App.Instance
        let quadGeometry =
            IndexedGeometry(
                Mode = IndexedGeometryMode.TriangleList,
                IndexArray = ([|0;1;2; 0;2;3|] :> Array),
                IndexedAttributes =
                    SymDict.ofList [
                        DefaultSemantic.Positions, [| V3f.OOO; V3f.IOO; V3f.IIO; V3f.OIO |] :> Array
                        DefaultSemantic.Colors, [| C3b.Red; C3b.Green; C3b.Blue; C3b.Yellow |] :> Array
                        DefaultSemantic.Normals, [| V3f.OOI; V3f.OOI; V3f.OOI; V3f.OOI |] :> Array
                        DefaultSemantic.DiffuseColorCoordinates, [| V2f.OO; V2f.IO; V2f.II; V2f.OI |] :> Array
                    ]
            )

//        let red = PixImage<byte>(Col.Format.RGBA, V2i(128,128))
//        red.GetMatrix<C4b>().Set(C4b.Red) |> ignore
        //let tex = PixTexture2d(PixImageMipMap [|red :> PixImage|], true) :> ITexture


        let mode = Mod.init FillMode.Fill


//        let surf =
//            BinarySurface [
//                ShaderStage.Vertex, BinaryShader "Vertex.spv"
//                ShaderStage.Pixel, BinaryShader "Pixel.spv"
//            ]

        let sg =
            quadGeometry
                |> Sg.ofIndexedGeometry
                //|> Sg.diffuseTexture ~~tex
                //|> Sg.surface (surf :> ISurface |> Mod.constant)
                |> Sg.effect [
                    DefaultSurfaces.trafo |> toEffect
                    //DefaultSurfaces.diffuseTexture |> toEffect
                    DefaultSurfaces.constantColor C4f.White |> toEffect
                    DefaultSurfaces.simpleLighting |> toEffect
                   ]
                |> Sg.depthTest (Mod.constant DepthTestMode.None)
                |> Sg.fillMode mode

        app.Control.Keyboard.DownWithRepeats.Values.Add(fun k ->  
            if k = Keys.X then 
                let newMode = 
                    match mode.Value with
                        | FillMode.Fill -> FillMode.Line
                        | FillMode.Line -> FillMode.Point
                        | _ -> FillMode.Fill

                Log.line "mode = %A" newMode
                transact (fun () -> mode.Value <- newMode)
        )


        app.SceneGraph <- sg
        app.Run()



module Lod =
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

    
    let app = App.Instance
    let win = app.Control

    type DummyDataProvider(root : Box3d) =
    
        interface ILodData with
            member x.BoundingBox = root

            member x.Traverse f =
                let rec traverse (level : int) (b : Box3d) =
                    let box = b
                    let n = 100.0
                    let node = { id = b; level = level; bounds = box; inner = true; granularity = Fun.Cbrt(box.Volume / n); render = true }

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
                        [| for x in 0 .. 9 do
                             for y in 0 .. 9 do
                                for z in 0 .. 9 do
                                    yield V3d(x,y,z)*0.1*box.Size + box.Min |> V3f.op_Explicit
                         |]
                    let colors = Array.create points.Length (Helpers.randomColor())
                    //let points = Helpers.randomPoints cell.bounds 1000
                    //let b = Helpers.box (Helpers.randomColor()) cell.bounds
//                  
                    //do! Async.Sleep(1000)
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

        let mainProj = win.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y)) 
        let gridProj = Frustum.perspective 60.0 1.0 50.0 1.0 |> Mod.constant

        let proj =
            adaptive {
                let! mode = mode 
                match mode with
                    | Main -> return! mainProj
                    | Test -> return! gridProj
            }

    
    let eff =
        let effects = [
            DefaultSurfaces.trafo |> toEffect                  
            DefaultSurfaces.vertexColor  |> toEffect         
        ]
        let e = FShade.Effect.compose effects
        FShadeSurface(e) :> ISurface 

    let surf = 
        win.Runtime.PrepareSurface(
            win.FramebufferSignature,
            eff
        ) :> ISurface |> Mod.constant


    let cloud =
        Sg.pointCloud data {
            targetPointDistance     = Mod.constant 40.0
            maxReuseRatio           = 0.5
            minReuseCount           = 1L <<< 20
            pruneInterval           = 500
            customView              = Some (gridCam |> Mod.map CameraView.viewTrafo)
            customProjection        = Some (gridProj |> Mod.map Frustum.projTrafo)
            attributeTypes =
                Map.ofList [
                    DefaultSemantic.Positions, typeof<V3f>
                    DefaultSemantic.Colors, typeof<C4b>
                    DefaultSemantic.Normals, typeof<V3f>
                ]
            boundingBoxSurface      = None
        }

                                    
    let sg = 
        Sg.group' [
            cloud
                |> Sg.effect [
                    DefaultSurfaces.trafo |> toEffect                  
                    DefaultSurfaces.vertexColor  |> toEffect         
                    //DefaultSurfaces.pointSprite  |> toEffect     
                    //DefaultSurfaces.pointSpriteFragment  |> toEffect 
                ]
//            Helpers.frustum gridCam gridProj
//
//            data.BoundingBox.EnlargedByRelativeEps(0.005)
//                |> Helpers.wireBox C4b.VRVisGreen
//                |> Sg.ofIndexedGeometry
        ]

    let final =
        sg |> Sg.effect [
                DefaultSurfaces.trafo |> toEffect                  
                DefaultSurfaces.vertexColor  |> toEffect 
                ]
            |> Sg.viewTrafo (view |> Mod.map CameraView.viewTrafo ) 
            |> Sg.projTrafo (proj |> Mod.map Frustum.projTrafo    )
            |> Sg.uniform "PointSize" (Mod.constant 4.0)
    
    let run() =
        app.SceneGraph <- final
        app.Run()

    






