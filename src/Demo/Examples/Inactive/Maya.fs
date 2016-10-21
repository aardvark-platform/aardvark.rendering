#if INTERACTIVE
#I @"../../../bin/Debug"
#I @"../../../bin/Release"
#load "LoadReferences.fsx"
#else
namespace Examples
#endif

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.Interactive
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Rendering.NanoVg
open Aardvark.Base.Monads.State

#nowarn "9"
#nowarn "51"



module Controller =
    open Aardvark.Base.Incremental.Operators
    type StartStop<'a> = { start : Event<'a>; stop : Event<'a> }
    type Config =
        {
            look        : StartStop<unit>
            pan         : StartStop<unit>
            zoom        : StartStop<unit>
            forward     : IMod<bool>
            backward    : IMod<bool>
            right       : IMod<bool>
            left        : IMod<bool>
            move        : Event<PixelPosition * PixelPosition>
            scroll      : Event<float>
        }

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Config =
        let wsad (rc : IRenderControl) =
            let lDown       = rc.Mouse.Down.Values |> Event.ofObservable |> Event.filter (fun m -> m = MouseButtons.Left) |> Event.ignore
            let lUp         = rc.Mouse.Up.Values |> Event.ofObservable |> Event.filter (fun m -> m = MouseButtons.Left) |> Event.ignore
            let mDown       = rc.Mouse.Down.Values |> Event.ofObservable |> Event.filter (fun m -> m = MouseButtons.Middle) |> Event.ignore
            let mUp         = rc.Mouse.Up.Values |> Event.ofObservable |> Event.filter (fun m -> m = MouseButtons.Middle) |> Event.ignore
            let rDown       = rc.Mouse.Down.Values |> Event.ofObservable |> Event.filter (fun m -> m = MouseButtons.Right) |> Event.ignore
            let rUp         = rc.Mouse.Up.Values |> Event.ofObservable |> Event.filter (fun m -> m = MouseButtons.Right) |> Event.ignore
            
            let scroll      = rc.Mouse.Scroll.Values |> Event.ofObservable
            let move        = rc.Mouse.Move.Values |> Event.ofObservable

            {
                look        = { start = lDown; stop = lUp }
                pan         = { start = mDown; stop = mUp }
                zoom        = { start = rDown; stop = rUp }
                forward     = rc.Keyboard.IsDown(Keys.W)
                backward    = rc.Keyboard.IsDown(Keys.S)
                right       = rc.Keyboard.IsDown(Keys.D)
                left        = rc.Keyboard.IsDown(Keys.A)
                move        = move
                scroll      = scroll
            }

    let move (c : Config) = 
        proc {  
            let! speed = 
                (c.forward   %?  V2d.OI %. V2d.OO) %+
                (c.backward  %? -V2d.OI %. V2d.OO) %+
                (c.left      %? -V2d.IO %. V2d.OO) %+ 
                (c.right     %?  V2d.IO %. V2d.OO)

            if speed <> V2d.Zero then
                for dt in Proc.dt do
                    do! fun (cam : CameraView) -> 
                        let direction = 
                            speed.X * cam.Right + 
                            speed.Y * cam.Forward

                        let delta = 0.5 * direction * dt.TotalSeconds

                        cam.WithLocation(cam.Location + delta)
        }

    let look (c : Config) =
        Proc.startStop (Proc.ofEvent c.look.start) (Proc.ofEvent c.look.stop) {
            for (o, n) in c.move do
                let delta = n.Position - o.Position
                do! fun (s : CameraView) ->
                    let trafo =
                        M44d.Rotation(s.Right, float delta.Y * -0.01) *
                        M44d.Rotation(s.Sky, float delta.X * -0.01)

                    let newForward = trafo.TransformDir s.Forward |> Vec.normalize
                    s.WithForward(newForward)
        }


    let scroll (c : Config) =
        proc {
            let mutable speed = 0.0
            while true do
                try
                    do! until [ Proc.ofEvent c.scroll ]

                    let interpolate =
                        proc {
                            let! dt = Proc.dt
                            do! fun (s : CameraView) -> 
                                let v = speed * s.Forward
                                let res = CameraView.withLocation (s.Location + dt.TotalSeconds *0.1 * v) s
                                speed <- speed * Fun.Pow(0.004, dt.TotalSeconds)
                                res

                            if abs speed > 0.5 then 
                                do! self
                            else 
                                do! Proc.never
                        }

                    do! interpolate

                with delta ->
                    speed <- speed + delta
        }
            
    let pan (c : Config) =
        proc {
            while true do
                let! d = c.pan.start
                try
                    do! until [ Proc.ofEvent c.pan.stop ]
                    for (o, n) in c.move do
                        let delta = n.Position - o.Position
                        do! State.modify (fun (s : CameraView) ->
                            let step = 0.05 * (s.Down * float delta.Y + s.Right * float delta.X)
                            s.WithLocation(s.Location + step)
                        )


                with _ ->
                    ()
        }

    let zoom (c : Config) =
        proc {
            while true do
                let! d = c.zoom.start
                try
                    do! until [ Proc.ofEvent c.zoom.stop ]
                    for (o, n) in c.move do
                        let delta = n.Position - o.Position
                        do! State.modify (fun (s : CameraView) ->
                            let step = -0.05 * (s.Forward * float delta.Y)
                            s.WithLocation(s.Location + step)
                        )


                with _ ->
                    ()
        }
    
    let control (c : Config) =
        Proc.par [ look c; scroll c; move c; pan c; zoom c ]


module Maya = 

    module Shader =
        open FShade

        type HugoVertex = 
            {
                [<Semantic("Hugo")>] m : M44d
                [<Position>] p : V4d
            }

        let hugoShade (v : HugoVertex) =
            vertex {
                return { v 
                    with 
                        p = v.m * v.p 
                }
            }

        type Vertex = 
            {
                [<Semantic("ThingTrafo")>] m : M44d
                [<Semantic("ThingNormalTrafo")>] nm : M33d
                [<Position>] p : V4d
                [<Normal>] n : V3d
            }

        let thingTrafo (v : Vertex) =
            vertex {
                return { v 
                    with 
                        p = v.m * v.p 
                        n = v.nm * v.n
                }
            }

    [<Flags>]
    type ControllerPart =
        | None = 0x00
        | X = 0x01 
        | Y = 0x02 
        | Z = 0x04

    let radius = 0.025

    let intersectController (trafo : Trafo3d) (r : Ray3d) =
        let innerRay = r.Transformed(trafo.Backward)

        let mutable res = ControllerPart.None

        if innerRay.GetMinimalDistanceTo(Line3d(V3d.Zero, V3d.IOO)) < radius then
            res <- res ||| ControllerPart.X

        if innerRay.GetMinimalDistanceTo(Line3d(V3d.Zero, V3d.OIO)) < radius then
            res <- res ||| ControllerPart.Y

        if innerRay.GetMinimalDistanceTo(Line3d(V3d.Zero, V3d.OOI)) < radius then
            res <- res ||| ControllerPart.Z

        res

    open Aardvark.SceneGraph.Semantics
    
    let run () =


        Ag.initialize()
        Aardvark.Init()
        use app = new OpenGlApplication()
        use win = app.CreateGameWindow()
        //use win = app.CreateSimpleRenderWindow(1)
        //win.VSync <- OpenTK.VSyncMode.On
        //win.Text <- "Aardvark rocks \\o/"

        let view = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
        let perspective = 
            win.Sizes 
              |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 50.0 (float s.X / float s.Y))


        let viewTrafo = DefaultCameraController.control win.Mouse win.Keyboard win.Time view

      

        let pool        = GeometryPool.create app.Runtime
        let box         = pool.Add Primitives.unitBox.Flat
        let cone        = pool.Add (Primitives.unitCone 16).Flat
        let cylinder    = pool.Add (Primitives.unitCylinder 16).Flat


        let scaleCylinder = Trafo3d.Scale(radius, radius, 1.0)

        let render = 
            Mod.init [
                scaleCylinder * Trafo3d.FromOrthoNormalBasis(V3d.OOI, V3d.OIO, V3d.IOO), cylinder, C4b.Red
                scaleCylinder * Trafo3d.FromOrthoNormalBasis(V3d.IOO, V3d.OOI, V3d.OIO), cylinder, C4b.Green

                scaleCylinder, cylinder, C4b.Blue
            ]

        let drawCallInfos = 
            let rangeToInfo (i : int) (r : Range1i) =
                DrawCallInfo(
                    FaceVertexCount = r.Size + 1, 
                    FirstIndex = r.Min, 
                    InstanceCount = 1, 
                    FirstInstance = i
                )
            render |> Mod.map (fun l -> l |> List.mapi (fun i (_,g,_) -> rangeToInfo i g) |> IndirectBuffer.ofList)

        let trafos =
            let buffer = render |> Mod.map (fun v -> v |> List.map (fun (t,_,_) -> t.Forward |> M44f.op_Explicit) |> List.toArray |> ArrayBuffer :> IBuffer) 
            BufferView(buffer, typeof<M44f>)

        let normalTrafos =
            let buffer = render |> Mod.map (fun v -> v |> List.map (fun (t,_,_) -> t.Backward.Transposed.UpperLeftM33() |> M33f.op_Explicit) |> List.toArray |> ArrayBuffer :> IBuffer) 
            BufferView(buffer, typeof<M33f>)


        let colors =
            let buffer = render |> Mod.map (fun v -> v |> List.map (fun (_,_,c) -> c) |> List.toArray |> ArrayBuffer :> IBuffer) 
            BufferView(buffer, typeof<C4b>)

        let trafo = Symbol.Create "ThingTrafo"
        let normalTrafo = Symbol.Create "ThingNormalTrafo"
        let color = DefaultSemantic.Colors

        let pos = BufferView(pool.GetBuffer DefaultSemantic.Positions, typeof<V3f>)
        let n = BufferView(pool.GetBuffer DefaultSemantic.Normals, typeof<V3f>)

        let sg =
            Sg.air {
                do! Air.BindEffect [
                        Shader.thingTrafo |> toEffect
                        DefaultSurfaces.trafo |> toEffect
                        DefaultSurfaces.vertexColor |> toEffect
                        DefaultSurfaces.simpleLighting |> toEffect
                    ]

                do! Air.BindVertexBuffers [
                        DefaultSemantic.Positions, pos
                        DefaultSemantic.Normals, n
                    ]

                do! Air.BindInstanceBuffers [
                        normalTrafo, normalTrafos
                        trafo, trafos
                        color, colors
                    ]

                do! Air.Toplogy IndexedGeometryMode.TriangleList
                do! Air.DrawIndirect drawCallInfos
            }

//        let sg =
//            let test = 
//                Pooling.testSg win app.Runtime
//                |> Sg.effect [
//                    Shader.hugoShade |> toEffect
//                    DefaultSurfaces.trafo |> toEffect
//                    DefaultSurfaces.constantColor C4f.Red |> toEffect
//                    DefaultSurfaces.simpleLighting |> toEffect
//                ]
//            test
//            Pooling.LodAgain.test()
//        let sg = Sg.ofSeq ]

        
        let camera = Mod.map2 (fun v p -> { cameraView = v; frustum = p }) viewTrafo perspective
        let pickRay = Mod.map2 Camera.pickRay camera win.Mouse.Position
        let trafo = Mod.init Trafo3d.Identity
        let controlledAxis = Mod.map2 intersectController trafo pickRay

//        controlledAxis |> Mod.unsafeRegisterCallbackKeepDisposable (fun c ->
//            printfn "%A" c
//        ) |> ignore

//        let mutable lastRay = pickRay.GetValue()
//        let  moving = ref ControllerPart.None
//        win.Mouse.Down.Values.Add (fun b ->
//            if b = MouseButtons.Left then
//                let c = controlledAxis.GetValue()
//                lastRay <- pickRay.GetValue()
//                moving := c
//                printfn "down %A" c
//        )
//
//        win.Mouse.Move.Values.Add (fun m ->
//            match !moving with
//                | ControllerPart.None -> ()
//                | p ->
//                    printfn "move"
//                    let t = trafo.GetValue()
//                    let pickRay = pickRay.GetValue()
//                    
//                    let ray = pickRay.Transformed(t.Backward)
//                    let last = lastRay.Transformed(t.Backward)
//
//                    let delta = 
//                        match p with
//                            | ControllerPart.X -> 
//                                V3d(ray.Intersect(Plane3d.ZPlane).X - last.Intersect(Plane3d.ZPlane).X, 0.0, 0.0)
//                            | ControllerPart.Y -> 
//                                V3d(0.0, ray.Intersect(Plane3d.ZPlane).Y - last.Intersect(Plane3d.ZPlane).Y, 0.0)
//                            | _ -> 
//                                V3d(0.0, 0.0, ray.Intersect(Plane3d.XPlane).Z - last.Intersect(Plane3d.XPlane).Z)
//                    printfn "%A" delta
//                    transact (fun () ->
//                        trafo.Value <- t * Trafo3d.Translation(delta)
//                    )
//
//                    lastRay <- pickRay
//        )
//        win.Mouse.Up.Values.Add (fun b ->
//            if b = MouseButtons.Left then
//                moving := ControllerPart.None
//        )


        let wsad = Controller.Config.wsad win
        
        let sepp = win.Keyboard.KeyDown(Keys.Y).Values |> Event.ofObservable |> Proc.ofEvent
        let switchMode = win.Keyboard.KeyDown(Keys.P).Values |> Event.ofObservable |> Proc.ofEvent
        let isActive = 
            switchMode |> Proc.fold (fun a () -> not a) true

        let all =
            let inner = Controller.control wsad 
            proc {
                let! active = isActive
                if active then
                    do! inner
            }
//            let switchMode = win.Keyboard.KeyDown(Keys.X).Values |> Event.ofObservable |> Proc.ofEvent
//            Proc.whenever 
//                switchMode 
//                true
//                (fun () active -> not active)
//                (fun active -> 
//                    if active then Controller.control wsad 
//                    else Proc.never
//                )

        let rand = Random()

        let sleepMs = ref 0
        let mutable cnt = 0
        let adjust (t : Time) =
            cnt <- cnt + 1
            sleepMs := rand.Next(1, 40)

            t + MicroTime(TimeSpan.FromMilliseconds (20.0 + float !sleepMs))

        let cam = Proc.toMod adjust view all

//        let camera = Mod.init view
//        let runner =
//            async {
//                do! Async.SwitchToNewThread()
//                while true do
//                    let v = Mod.force cam
//                    transact (fun () -> camera.Value <- v)
//                    do! Async.Sleep 1
//            }
//        Async.Start runner


        //let camera = view |> DefaultCameraController.control win.Mouse win.Keyboard win.Time

        let sg =
            sg
                |> Sg.trafo trafo
                // viewTrafo () creates camera controls and returns IMod<ICameraView> which we project to its view trafo component by using CameraView.viewTrafo
                |> Sg.viewTrafo (cam |> Mod.map CameraView.viewTrafo ) 
                // perspective () connects a proj trafo to the current main window (in order to take account for aspect ratio when creating the matrices.
                // Again, perspective() returns IMod<Frustum> which we project to its matrix by mapping ofer Frustum.projTrafo.
                |> Sg.projTrafo (perspective |> Mod.map Frustum.projTrafo    )
                |> Sg.uniform "LightLocation" (Mod.constant (V3d.III * 10.0))
        
        //let objects = sg.RenderObjects() |> Pooling.Optimizer.optimize app.Runtime win.FramebufferSignature

        
        use task = app.Runtime.CompileRender(win.FramebufferSignature, { BackendConfiguration.NativeOptimized with useDebugOutput = false }, sg)
        

        
        let busywait(wanted : int) = 
            let sw = System.Diagnostics.Stopwatch()
            sw.Start()
            while int sw.Elapsed.TotalMilliseconds < wanted do ()
            sw.Stop()

        let busywait(wanted : int) =
            System.Threading.Thread.Sleep(wanted)

        let task = 
            RenderTask.ofList [
                task
                //RenderTask.custom (fun (self, o) -> busywait !sleepMs; FrameStatistics.Zero)
            ]
        
        win.RenderTask <- task |> DefaultOverlays.withStatistics
        win.Run()

