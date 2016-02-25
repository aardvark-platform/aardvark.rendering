namespace Aardvark.Rendering.Interactive

[<AutoOpen>]
module FsiSetup =


    open System
    open System.Threading

    open Aardvark.Base
    open Aardvark.Base.Incremental
    open Aardvark.Base.Rendering
    open Aardvark.Rendering.NanoVg
    open Aardvark.SceneGraph
    open Aardvark.SceneGraph.Semantics

    open Aardvark.Application
    open Aardvark.Application.WinForms

    let mutable defaultCamera = true

    let mutable private initialized = 0

    let private winFormsApp = lazy ( new OpenGlApplication() )




    let init entryDir =
        let entry = "Aardvark.Rendering.Interactive.dll"
        let entryPath = Path.combine [entryDir; entry] 
        if entryPath |> System.IO.File.Exists then
            printfn "using %s as entry assembly." entryPath
        else failwithf "could not find entry assembly: %s" entryPath
        
        if Interlocked.Exchange(&initialized, 1) = 0 then
            System.Environment.CurrentDirectory <- entryDir
            IntrospectionProperties.CustomEntryAssembly <- 
                System.Reflection.Assembly.LoadFile(entryPath)

            Ag.initialize()
            Aardvark.Base.Ag.unpack <- fun o ->
                    match o with
                        | :? IMod as o -> o.GetValue(null)
                        | _ -> o

            Aardvark.Init()
 
    let openWindow() =
        if initialized = 0 then failwith "first use Aardvark.Rendering.Interactive.init(pathToBuild)"

        let app = winFormsApp.Value
        let win = app.CreateSimpleRenderWindow(1)
        let task = app.Runtime.CompileRender(win.FramebufferSignature, Sg.group [])

        win.Text <- @"Aardvark rocks \o/"
        win.Visible <- true 
        win.RenderTask <- task

        win :> IRenderControl

    let closeWindow (c : IRenderControl) =
        match c with
            | :? SimpleRenderWindow as w ->
                w.Close()
                w.RenderTask.Dispose()
                w.Dispose()
            | _ ->
                ()

    let showSg (w : IRenderControl) (sg : ISg) =
        if initialized = 0 then failwith "first use Aardvark.Rendering.Interactive.init(pathToBuild)"

        try
            let task = w.Runtime.CompileRender(w.FramebufferSignature, sg)

            let old = w.RenderTask
            w.RenderTask <- task
            
            match w with
                | :? SimpleRenderWindow as w ->
                    w.Invalidate()
                | _ -> ()

            old.Dispose()
        with e ->
            Log.warn "failed to set sg: %A" e

    let clearSg (w : IRenderControl) =
        showSg w (Sg.group [])

    let viewTrafo (win : IRenderControl) =
        let view =  CameraView.LookAt(V3d(3.0, 3.0, 3.0), V3d.Zero, V3d.OOI)
        DefaultCameraController.control win.Mouse win.Keyboard win.Time view

    let perspective (win : IRenderControl) = 
        win.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.01 100.0 (float s.X / float s.Y))


    let runInteractive () =
          
        if initialized = 0 then failwith "first use Aardvark.Rendering.Interactive.init(pathToBuild)"

        let app = new OpenGlApplication()
        let win = app.CreateSimpleRenderWindow(1)
        let root = Mod.init <| (Sg.group [] :> ISg)

        // mk use of nanovg
        Aardvark.Rendering.NanoVg.TextAlign.BaseLine |> ignore

        let sg = 
            if defaultCamera |> not then Sg.dynamic root
            else
              Log.line "using default camera. If you need identity transformation for your scene, use Aardvark.Rendering.Interactive.FsiSetup.defaultCamera <- false prior to initialization. "
              Sg.dynamic root
                |> Sg.effect [
                        DefaultSurfaces.trafo |> toEffect               
                        DefaultSurfaces.constantColor C4f.Red |> toEffect  
                        ]
                |> Sg.viewTrafo (viewTrafo   win |> Mod.map CameraView.viewTrafo ) 
                |> Sg.projTrafo (perspective win |> Mod.map Frustum.projTrafo    )  

        let task = 
            app.Runtime.CompileRender(win.FramebufferSignature, sg) //|> DefaultOverlays.withStatistics

        win.Text <- @"Aardvark rocks \o/"
        win.Visible <- true

        let fixupRenderTask () =
            if win.RenderTask = Unchecked.defaultof<_> then win.RenderTask <- task


        (fun (s:ISg) -> fixupRenderTask () ; transact (fun () -> Mod.change root  s)), win, task
      
    
