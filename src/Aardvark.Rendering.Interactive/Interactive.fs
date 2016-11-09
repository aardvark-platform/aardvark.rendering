namespace Aardvark.Rendering.Interactive

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Rendering.NanoVg

[<AutoOpen>]
module NewShit = 
    type Interactive private() =
        static let app = new OpenGlApplication()
        static let mutable window = None
        static let emptySg = Sg.ofList []
        static let mutable samples = 8
        static let mutable config = BackendConfiguration.Default

        static let createWindow () =
            let win = app.CreateSimpleRenderWindow(samples)
            win.Text <- "Aardvark Interactive Session Setup"
            let sg = Mod.init emptySg
            let task = 
                app.Runtime.CompileRender(win.FramebufferSignature, config, Sg.dynamic sg)
                    |> DefaultOverlays.withStatistics
            win.RenderTask <- task
            win.Show()
            win.Disposed.Add (fun _ -> 
                task.Dispose()
                transact (fun () -> sg.Value <- emptySg)
                window <- None
            )
            win, sg

        static let getWindowAndSg () =
            match window with
                | None ->
                    let t = createWindow ()
                    window <- Some t
                    t
                | Some t ->
                    t

        static member Window = getWindowAndSg ()  |> fst :> IRenderWindow

        static member Samples 
            with get () = samples
            and set v = samples <- v

        static member BackendConfiguration 
            with get () = config
            and set v = config <- v

        static member Runtime = app.Runtime :> IRuntime
        static member Keyboard = Interactive.Window.Keyboard
        static member Mouse = Interactive.Window.Mouse

        static member SceneGraph
            with get() =
                 let _, ref = getWindowAndSg()
                 ref.Value
            and set sg =
                let win, ref = getWindowAndSg()
                transact (fun () -> ref.Value <- sg)
                if not win.Visible then win.Show()

        static member ControlledCameraView (eye : V3d) (lookAt : V3d) =
            let win, _ = getWindowAndSg()
            let view =  CameraView.LookAt(eye,lookAt, V3d.OOI)
            DefaultCameraController.control win.Mouse win.Keyboard win.Time view

        static member DefaultCameraView = 
            Interactive.ControlledCameraView (V3d.III * 3.0) V3d.Zero

        static member DefaultViewTrafo =
            Interactive.ControlledViewTrafo (V3d.III * 3.0) V3d.Zero

        static member ControlledViewTrafo  (eye : V3d) (lookAt : V3d) =
            Interactive.ControlledCameraView eye lookAt |> Mod.map CameraView.viewTrafo

        static member DefaultFrustum =
            let win, _ = getWindowAndSg()
            win.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.01 100.0 (float s.X / float s.Y))

        static member DefaultProjTrafo =
            let win, _ = getWindowAndSg()
            win.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.01 100.0 (float s.X / float s.Y) |> Frustum.projTrafo)

        static member DefaultCamera =
            Mod.map2 (fun c f -> { cameraView = c; frustum = f }) Interactive.DefaultCameraView Interactive.DefaultFrustum


        static member RunMainLoop() =
            let w, _ = getWindowAndSg ()
            System.Windows.Forms.Application.Run w
