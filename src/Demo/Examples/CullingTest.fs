namespace Rendering.Examples

open System
open Aardvark.Base
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open Aardvark.Base.Rendering
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics

open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Rendering.GL

open System.Threading


module CullingTest =

    type Mode = Spectator | Viewer

    let run (app : Aardvark.Application.IApplication) (win : Aardvark.Application.IRenderWindow) =

        let view = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
        let frustum = 
            win.Sizes 
              |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 200.0 (float s.X / float s.Y))

        let mode = AVal.init Viewer
        let currentMain = ref (CameraView.lookAt (V3d(3,3,3)) V3d.Zero V3d.OOI)
        let currentTest = ref (CameraView.lookAt (V3d(3,3,3)) V3d.Zero V3d.OOI)
        let mainCam =
            adaptive {
                let! mode = mode
                match mode with
                    | Viewer ->
                        let! m = DefaultCameraController.control win.Mouse win.Keyboard win.Time !currentMain
                        currentMain := m
                        return m
                    | _ ->
                        return !currentMain
            }

        let spectatorCam =
            adaptive {
                let! mode = mode
                match mode with
                    | Spectator ->
                        let! m = DefaultCameraController.control win.Mouse win.Keyboard win.Time !currentTest
                        currentTest := m
                        return m
                    | _ ->
                        return !currentTest
            }


        let cameraView =
            adaptive {
                let! mode = mode
                match mode with
                    | Viewer -> return! mainCam
                    | Spectator -> return! spectatorCam
            }

        let changed = MVar.empty ()
        cameraView.AddCallback (fun _ -> MVar.put changed ()) |> ignore

        let objectIndex = AVal.init 1

        let objectTypes =
            [| Sg.box' C4b.White Box3d.Unit
               Sg.sphere' 9 C4b.White 0.3 |]

        let viewProj = AVal.map2 (fun view proj -> CameraView.viewTrafo view * Frustum.projTrafo proj) mainCam frustum
        let box = Box3d.FromPoints(V3d(-1,-1,-1),V3d(1,1,1))
        let noCull = AVal.init false

//        let cache = Array3D.init 13 13 13
//        let isActives =
//            AVal.custom (fun token ->
//                AVal.
//            )

        let objs =
            [|
                for x in -8 .. 12 do 
                    for y in -8 .. 12 do
                        for z in -8 .. 12 do
                            let pos = V3d(float x,float y, float z)
                            //let isActive = AVal.map (fun (a : bool[,,]) -> a.[x,y,z]) isActives
                            let isActive = AVal.init false
//                            let isActive =
//                                viewProj |> AVal.map (fun viewProj -> 
//                                    let noCull = false
//                                    //let! noCull = noCull
//                                    let ndc = viewProj.Forward.TransformPosProj pos
//                                    let visible = box.Contains(ndc) || noCull
//                                    visible
//                                ) |> AVal.onPush
                            let bar = isActive |> AVal.bind AVal.constant |> AVal.bind AVal.constant

                            let sg = 
//                                adaptive {
//                                    let! index = objectIndex
//                                    let sg = objectTypes.[index % objectTypes.Length]
//                                    return sg
//                                } |> Sg.dynamic 
                                  objectTypes.[objectIndex.Value % objectTypes.Length]
                                  |> Sg.transform (Trafo3d.Translation pos) |> Sg.onOff bar
                            yield isActive, pos, sg
            |]


        let cullTimer = System.Diagnostics.Stopwatch()
        let mutable visibleThings = 0


        let cullComputeTime = System.Diagnostics.Stopwatch()
        
        let testSynth n = 
            let sw = System.Diagnostics.Stopwatch()
            let rnd = System.Random()
            let objs = Array.init n (fun _ -> V3d(rnd.NextDouble(),rnd.NextDouble(),rnd.NextDouble()))
            let view = mainCam.GetValue()
            let frustum = frustum.GetValue()
            sw.Start()
            let viewProj = CameraView.viewTrafo view * Frustum.projTrafo frustum
            let mutable visibleCnt = 0
            let box = Box3d.FromPoints(V3d(-1,-1,-1),V3d(1,1,1))
            for pos in objs do
                let ndc = viewProj.Forward.TransformPosProj pos
                let visible = box.Contains(ndc) 
                visibleCnt <- visibleCnt +  (if visible then -1 else +1)
            sw.Stop()
            Log.line "for n=%d took %A" n sw.MicroTime

        testSynth 20000
        for i in 0 .. 5000 .. 200000 do
            testSynth i
            GC.Collect()


        let culling () =
            while true do
                MVar.take changed |> ignore
                cullTimer.Restart()
                cullComputeTime.Restart()
                let view = mainCam.GetValue()
                let frustum = frustum.GetValue()
                let noCull = noCull.GetValue()
                let viewProj = CameraView.viewTrafo view * Frustum.projTrafo frustum
                
                let mutable changedCount = 0
                transact (fun _ -> 
                    for (isActive,pos,sg) in objs do
                        let ndc = viewProj.Forward.TransformPosProj pos
                        let visible = box.Contains(ndc) || noCull
                        ()
//                        if isActive.Value <> visible then 
//                            isActive.Value <- visible
//                            if visible then visibleThings <- visibleThings + 1
//                            else visibleThings <- visibleThings - 1
//                            changedCount <- changedCount + 1
                    cullComputeTime.Stop()
                )
                cullTimer.Stop()
                printfn "[culling took] %A/%A (changes: %d, visible: %d)" cullComputeTime.MicroTime cullTimer.MicroTime changedCount visibleThings


        let cullThread =
            Thread(ThreadStart culling)
        cullThread.Name <- "culling"
        cullThread.IsBackground <- true
        //cullThread.Start()


        let sg =
            objs 
                |> Array.map (fun (_,_,sg) -> sg) 
                |> Sg.ofSeq 
                |> Sg.effect [
                    DefaultSurfaces.trafo                 |> toEffect
                    DefaultSurfaces.constantColor C4f.Red |> toEffect
                    DefaultSurfaces.simpleLighting        |> toEffect
                  ]
               |> Sg.andAlso (
                    Sg.frustum ~~C4b.White mainCam frustum
                    |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.vertexColor |> toEffect]
                  )
               |> Sg.viewTrafo (cameraView   |> AVal.map CameraView.viewTrafo )
               |> Sg.projTrafo (frustum      |> AVal.map Frustum.projTrafo    )

       
        Log.startTimed "gather objs"
        let objects = sg.RenderObjects()
        objects.Content |> AVal.force |> HashSet.toArray |> ignore
        Log.stop()


        let cullTask =
            let mutable wasCull = noCull.GetValue()
            RenderTask.custom (fun (task,token,output) ->   
                let noCull = noCull.GetValue ()
                if not noCull || wasCull <> noCull then
                    wasCull <- noCull
                    cullTimer.Restart()
                    let view = mainCam.GetValue()
                    let frustum = frustum.GetValue ()
                    let viewProj = CameraView.viewTrafo view * Frustum.projTrafo frustum
                
                    let mutable changedCount = 0
                    transact (fun _ -> 
                        for (isActive,pos,sg) in objs do
                            let box = Box3d.FromCenterAndSize(pos, V3d(0.6, 0.6, 0.6))
                            //let ndc = viewProj.Forward.TransformPosProj pos
                            let visible = box.IntersectsFrustum viewProj.Forward || noCull
                            if isActive.Value <> visible then 
                                isActive.Value <- visible
                                if visible then visibleThings <- visibleThings + 1
                                else visibleThings <- visibleThings - 1
                                changedCount <- changedCount + 1
                        cullComputeTime.Stop()
                    )
                    cullTimer.Stop()
                    //printfn "[culling took] %A/%A (changes: %d, visible: %d)" cullComputeTime.MicroTime cullTimer.MicroTime changedCount visibleThings

           )

        let both = 
            RenderTask.ofList [
                cullTask
                app.Runtime.CompileRender(win.FramebufferSignature, BackendConfiguration.Default, objects)
            ]

        let task = both //app.Runtime.CompileRender(win.FramebufferSignature, BackendConfiguration.NativeOptimized, objects)

        win.Keyboard.KeyDown(Keys.Space).Values.Subscribe(fun _ -> 
            transact (fun _ ->
                mode.Value <-
                    match mode.Value with
                        | Viewer -> Spectator
                        | Spectator -> Viewer
                printfn "[Culling] Mode: %A" mode.Value
            )
        ) |> ignore

        win.Keyboard.KeyDown(Keys.O).Values.Subscribe(fun _ -> 
            transact (fun _ ->
                objectIndex.Value <- objectIndex.Value + 1
                printfn "[Culling] Obj kind: %A" objectIndex.Value
            )
        ) |> ignore

        win.Keyboard.KeyDown(Keys.C).Values.Subscribe(fun _ -> 
            transact (fun _ ->
                noCull.Value <- not noCull.Value
                printfn "[Culling] Culling enabled: %A" (not noCull.Value)
                MVar.put changed ()
            )
        ) |> ignore

        win.RenderTask <- task //|> DefaultOverlays.withStatistics
        win.Run()
        0

    let runInstanced () =

        Ag.initialize()
        Aardvark.Init()

        use app = new OpenGlApplication()
        let win = app.CreateSimpleRenderWindow()

        let view = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
        let frustum = 
            win.Sizes 
              |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 40.0 (float s.X / float s.Y))

        let mode = AVal.init Viewer
        let currentMain = ref (CameraView.lookAt (V3d(3,3,3)) V3d.Zero V3d.OOI)
        let currentTest = ref (CameraView.lookAt (V3d(3,3,3)) V3d.Zero V3d.OOI)
        let mainCam =
            adaptive {
                let! mode = mode
                match mode with
                    | Viewer ->
                        let! m = DefaultCameraController.control win.Mouse win.Keyboard win.Time !currentMain
                        currentMain := m
                        return m
                    | _ ->
                        return !currentMain
            }

        let spectatorCam =
            adaptive {
                let! mode = mode
                match mode with
                    | Spectator ->
                        let! m = DefaultCameraController.control win.Mouse win.Keyboard win.Time !currentTest
                        currentTest := m
                        return m
                    | _ ->
                        return !currentTest
            }


        let cameraView =
            adaptive {
                let! mode = mode
                match mode with
                    | Viewer -> return! mainCam
                    | Spectator -> return! spectatorCam
            }

        let changed = MVar.empty ()
        cameraView.AddCallback (fun _ -> MVar.put changed ()) |> ignore

        let objectIndex = AVal.init 1

        let sphere = IndexedGeometryPrimitives.solidSubdivisionSphere (Sphere3d.FromRadius(0.3)) 9 C4b.Red

        let objectTypes =
            [| Sg.box' C4b.White Box3d.Unit
               Sg.sphere' 9 C4b.White 0.3 |]

        let objs =
            [|
                for x in -8 .. 12 do 
                    for y in -8 .. 12 do
                        for z in -8 .. 12 do
                            let pos = V3d(float x,float y, float z)
                            let isActive = AVal.init false
                            let sg = 
                                adaptive {
                                    let! index = objectIndex
                                    let sg = objectTypes.[index % objectTypes.Length]
                                    return sg
                                } |> Sg.dynamic 
                                  |> Sg.transform (Trafo3d.Translation pos) |> Sg.onOff isActive
                            yield Trafo3d.Translation pos, pos, sg
            |]


        let cullTimer = System.Diagnostics.Stopwatch()
        let mutable visibleThings = 0


        let instanceTrafos = AVal.init [||]

        let culling () =
            while true do
                MVar.take changed |> ignore
                cullTimer.Restart()
                let view = mainCam.GetValue()
                let frustum = frustum.GetValue()
                let viewProj = CameraView.viewTrafo view * Frustum.projTrafo frustum
                let box = Box3d.FromPoints(V3d(-1,-1,-1),V3d(1,1,1))
                let mutable changedCount = 0
                transact (fun _ -> 
                    instanceTrafos.Value <-
                        objs |> Array.choose (fun (trafo,pos,_) -> 
                            let ndc = viewProj.Forward.TransformPosProj pos
                            if box.Contains(ndc) then Some trafo
                            else None
                        )
                    visibleThings <- instanceTrafos.Value.Length
                )
                cullTimer.Stop()
                printfn "[culling took] %A (changes: %d, visible: %d)" cullTimer.MicroTime changedCount visibleThings

        let cullThread =
            Thread(ThreadStart culling)
        cullThread.Name <- "culling"
        cullThread.IsBackground <- true
        cullThread.Start()


        let sg =
             sphere
                |> Sg.instancedGeometry instanceTrafos
                |> Sg.effect [
                    DefaultSurfaces.instanceTrafo         |> toEffect
                    DefaultSurfaces.trafo                 |> toEffect
                    DefaultSurfaces.constantColor C4f.Red |> toEffect
                    DefaultSurfaces.simpleLighting        |> toEffect
                  ]
               |> Sg.andAlso (
                    Sg.frustum ~~C4b.White mainCam frustum
                    |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.vertexColor |> toEffect]
                  )
               |> Sg.viewTrafo (cameraView   |> AVal.map CameraView.viewTrafo )
               |> Sg.projTrafo (frustum      |> AVal.map Frustum.projTrafo    )

       
        let task = app.Runtime.CompileRender(win.FramebufferSignature, BackendConfiguration.Default, sg.RenderObjects())

        win.Keyboard.KeyDown(Keys.Space).Values.Subscribe(fun _ -> 
            transact (fun _ ->
                mode.Value <-
                    match mode.Value with
                        | Viewer -> Spectator
                        | Spectator -> Viewer
                printfn "[Culling] Mode: %A" mode.Value
            )
        ) |> ignore

        win.Keyboard.KeyDown(Keys.O).Values.Subscribe(fun _ -> 
            transact (fun _ ->
                objectIndex.Value <- objectIndex.Value + 1
                printfn "[Culling] Obj kind: %A" objectIndex.Value
            )
        ) |> ignore

        win.RenderTask <- task //|> DefaultOverlays.withStatistics
        win.Run()
        0


    let runStructural (app : Aardvark.Application.IApplication) (win : Aardvark.Application.IRenderWindow)  =

        let view = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
        let frustum = 
            win.Sizes 
              |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 40.0 (float s.X / float s.Y))

        let mode = AVal.init Viewer
        let currentMain = ref (CameraView.lookAt (V3d(3,3,3)) V3d.Zero V3d.OOI)
        let currentTest = ref (CameraView.lookAt (V3d(3,3,3)) V3d.Zero V3d.OOI)
        let mainCam =
            adaptive {
                let! mode = mode
                match mode with
                    | Viewer ->
                        let! m = DefaultCameraController.control win.Mouse win.Keyboard win.Time !currentMain
                        currentMain := m
                        return m
                    | _ ->
                        return !currentMain
            }

        let spectatorCam =
            adaptive {
                let! mode = mode
                match mode with
                    | Spectator ->
                        let! m = DefaultCameraController.control win.Mouse win.Keyboard win.Time !currentTest
                        currentTest := m
                        return m
                    | _ ->
                        return !currentTest
            }


        let cameraView =
            adaptive {
                let! mode = mode
                match mode with
                    | Viewer -> return! mainCam
                    | Spectator -> return! spectatorCam
            }

        let changed = MVar.empty ()
        cameraView.AddCallback (fun _ -> MVar.put changed ()) |> ignore

        let objectIndex = AVal.init 1

        let objectTypes =
            [| Sg.box' C4b.White Box3d.Unit
               Sg.sphere' 9 C4b.White 0.3 |]

        let objs =
            [|
                for x in -8 .. 12 do 
                    for y in -8 .. 12 do
                        for z in -8 .. 12 do
                            let pos = V3d(float x,float y, float z)
                            let isActive = AVal.init false
                            let sg = 
                                adaptive {
                                    let! index = objectIndex
                                    let sg = objectTypes.[index % objectTypes.Length]
                                    return sg
                                } |> Sg.dynamic 
                                  |> Sg.transform (Trafo3d.Translation pos) 
                            yield isActive, pos, sg
            |]


        let cullTimer = System.Diagnostics.Stopwatch()
        let mutable visibleThings = 0

        let renderSet = cset()

        let cullComputeTime = System.Diagnostics.Stopwatch()

        let culling () =
            while true do
                MVar.take changed |> ignore
                cullTimer.Restart()
                cullComputeTime.Restart()
                let view = mainCam.GetValue()
                let frustum = frustum.GetValue()
                let viewProj = CameraView.viewTrafo view * Frustum.projTrafo frustum
                let box = Box3d.FromPoints(V3d(-1,-1,-1),V3d(1,1,1))
                let mutable changedCount = 0
                transact (fun _ -> 
                    for (isActive,pos,sg) in objs do
                        let ndc = viewProj.Forward.TransformPosProj pos
                        let visible = box.Contains(ndc) 
                        if isActive.Value <> visible then 
                            isActive.Value <- visible
                            if visible then 
                                visibleThings <- visibleThings + 1
                                let worked = renderSet.Add sg
                                assert worked
                            else 
                                visibleThings <- visibleThings - 1
                                let worked = renderSet.Add sg
                                assert worked
                            changedCount <- changedCount + 1
                    cullComputeTime.Stop()
                )
                cullTimer.Stop()
                printfn "[culling took] %A/%A (changes: %d, visible: %d)" cullComputeTime.MicroTime cullTimer.MicroTime changedCount visibleThings

        let cullThread =
            Thread(ThreadStart culling)
        cullThread.Name <- "culling"
        cullThread.IsBackground <- true
        cullThread.Start()


        let sg =
            renderSet
                |> Sg.set
                |> Sg.effect [
                    DefaultSurfaces.trafo                 |> toEffect
                    DefaultSurfaces.constantColor C4f.Red |> toEffect
                    DefaultSurfaces.simpleLighting        |> toEffect
                  ]
               |> Sg.andAlso (
                    Sg.frustum ~~C4b.White mainCam frustum
                    |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.vertexColor |> toEffect]
                  )
               |> Sg.viewTrafo (cameraView   |> AVal.map CameraView.viewTrafo )
               |> Sg.projTrafo (frustum      |> AVal.map Frustum.projTrafo    )

       
        let task = app.Runtime.CompileRender(win.FramebufferSignature, BackendConfiguration.Default, sg.RenderObjects())

        win.Keyboard.KeyDown(Keys.Space).Values.Subscribe(fun _ -> 
            transact (fun _ ->
                mode.Value <-
                    match mode.Value with
                        | Viewer -> Spectator
                        | Spectator -> Viewer
                printfn "[Culling] Mode: %A" mode.Value
            )
        ) |> ignore

        win.Keyboard.KeyDown(Keys.O).Values.Subscribe(fun _ -> 
            transact (fun _ ->
                objectIndex.Value <- objectIndex.Value + 1
                printfn "[Culling] Obj kind: %A" objectIndex.Value
            )
        ) |> ignore

        win.RenderTask <- task //|> DefaultOverlays.withStatistics
        win.Run()
        0


