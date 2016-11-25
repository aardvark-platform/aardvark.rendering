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
open Aardvark.Application.WinForms.Vulkan

type RendererConfiguration =
    Vulkan | GL

type Interactive private() =

    static let mutable renderer = RendererConfiguration.GL

    static let emptySg = Sg.ofList []
    static let mutable samples = 8
    static let mutable config = BackendConfiguration.Default

    static let sg = Mod.init emptySg
    static let window = new SimpleRenderWindow()
    static let mutable initialized = false

    static do
        window.Text <- sprintf "Aardvark Interactive Session Setup running: %A" renderer
        window.Keyboard.KeyDown(Keys.End).Values.Add(fun _ -> 
            match renderer with
                | Vulkan -> 
                    Log.warn "switching to GL renderer."
                    Interactive.Renderer <- GL
                | GL -> 
                    Log.warn "switching to Vulkan renderer."
                    Interactive.Renderer <- Vulkan
        )

    static let mutable app : Option<IApplication> = None

    static let getApp () = 
        match app with
            | None -> 
                let a = 
                    match renderer with
                        | GL -> new OpenGlApplication() :> IApplication
                        | Vulkan -> new VulkanApplication() :> IApplication
                app <- Some a
                a
            | Some v -> v

    static let initWindow (force : bool) =
        if force || not initialized then
            initialized <- true
            let app = getApp()
            window.Text <- sprintf "Aardvark Interactive Session Setup running: %A" renderer
            app.Initialize(window.Control, samples)
            let task = 
                match renderer with
                    | Vulkan -> 
                        app.Runtime.CompileRender(window.FramebufferSignature, config, Sg.dynamic sg) 
                    | GL -> 
                        app.Runtime.CompileRender(window.FramebufferSignature, config, Sg.dynamic sg) |> DefaultOverlays.withStatistics
        
            window.RenderTask <- task
            window.Show()

//            let realControl = window.Control.Implementation
//            realControl.Disposed.Add (fun _ -> 
//                task.Dispose()
//            )


    static member Window = window :> IRenderWindow

    static member Samples 
        with get () = samples
        and set v = samples <- v

    static member BackendConfiguration 
        with get () = config
        and set v = config <- v

    static member Runtime = getApp().Runtime
    static member Keyboard = window.Keyboard
    static member Mouse = window.Mouse

    static member Renderer 
        with get () = renderer
        and set v =
            renderer <- v
            match app with
                | Some oldApp -> 
                    let oldImpl = window.Control.Implementation
                    let renderTask = window.RenderTask
                    window.SuspendLayout()

                    //window.Control.Implementation <- new System.Windows.Forms.Panel()
                    oldImpl.Dispose()

                    app <- None
                    let app = getApp()
                    initWindow true
                    window.ResumeLayout()

                    renderTask.Dispose()
                    oldApp.Dispose()

                | None -> 
                    ()

    static member SceneGraph
        with get() = 
            sg.Value
        and set s =
            initWindow false
            transact (fun () -> sg.Value <- s)
            if not window.Visible then window.Show()

    static member ControlledCameraView (eye : V3d) (lookAt : V3d) =
        let view =  CameraView.LookAt(eye,lookAt, V3d.OOI)
        DefaultCameraController.control window.Mouse window.Keyboard window.Time view

    static member DefaultCameraView = 
        Interactive.ControlledCameraView (V3d.III * 3.0) V3d.Zero

    static member DefaultViewTrafo =
        Interactive.ControlledViewTrafo (V3d.III * 3.0) V3d.Zero

    static member ControlledViewTrafo  (eye : V3d) (lookAt : V3d) =
        Interactive.ControlledCameraView eye lookAt |> Mod.map CameraView.viewTrafo

    static member DefaultFrustum =
        window.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.01 100.0 (float s.X / float s.Y))

    static member DefaultProjTrafo =
        window.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.01 100.0 (float s.X / float s.Y) |> Frustum.projTrafo)

    static member DefaultCamera =
        Mod.map2 (fun c f -> { cameraView = c; frustum = f }) Interactive.DefaultCameraView Interactive.DefaultFrustum


    static member RunMainLoop() =
        initWindow false
        window.Show()
        System.Windows.Forms.Application.Run ()
        window.RenderTask.Dispose()
        window.Dispose()
        match app with
            | Some app -> app.Dispose()
            | _ -> ()

