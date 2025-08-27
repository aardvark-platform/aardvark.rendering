namespace Aardvark.Application.WinForms

open System
open System.Runtime.CompilerServices
open System.Windows.Forms
open Aardvark.Application
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive

type SimpleRenderWindow() as this =
    inherit Form()

    let ctrl = new RenderControl()

    let mutable lastBorderStyle = FormBorderStyle.None
    let mutable lastState = FormWindowState.Normal

    let mutable frameCount = 0
    let mutable totalTime = MicroTime.Zero
    let mutable baseTitle = ""

    let mutable customTitle = None
    

    member x.NewFrame (t : MicroTime) = 
        frameCount <- frameCount + 1
        totalTime <- totalTime + t
        if frameCount > 50 then
            let fps = float frameCount / totalTime.TotalSeconds
            // magic only if no custom
            if Option.isNone customTitle then
                x.Text <- baseTitle + sprintf " (%.3f fps)" fps
            frameCount <- 0
            totalTime <- MicroTime.Zero
        ()

    override x.OnHandleCreated(e) =
        base.OnHandleCreated(e)
        x.ClientSize <- System.Drawing.Size(1024, 768)
        ctrl.Dock <- DockStyle.Fill
        x.Controls.Add ctrl
        match customTitle with
            | None -> 
                // find better way...
                baseTitle <- "Aardvark rocks WinForms \\o/"
            | Some t -> // override magic
                baseTitle <- t

        x.Text <- baseTitle

        ctrl.Keyboard.KeyDown(Keys.Enter).Values.Add (fun () ->
            if AVal.force ctrl.Keyboard.Alt && AVal.force ctrl.Keyboard.Shift then
                this.ToggleFullScreen()
        )

        let sw = System.Diagnostics.Stopwatch()
        ctrl.BeforeRender.Add sw.Restart
        ctrl.AfterRender.Add (fun () -> sw.Stop(); x.NewFrame sw.MicroTime)

        try
            let asm = typeof<SimpleRenderWindow>.Assembly
            let resourceName = asm.GetManifestResourceNames() |> Array.tryFind (String.endsWith "aardvark-icon.bin")

            match resourceName with
            | Some name ->
                use s = asm.GetManifestResourceStream(name)
                x.Icon <- new Drawing.Icon(s)
            | _ ->
                Log.error "[WinForms] Could not find icon."
        with exn ->
            Log.error "[WinForms] Failed to load icon: %s" exn.Message


    member private x.ToggleFullScreen() : unit =
        x.Invoke (new System.Action (fun () -> 
            x.SuspendLayout()
            match x.FormBorderStyle with
                | FormBorderStyle.None ->
                    x.FormBorderStyle <- lastBorderStyle
                    x.WindowState <- lastState
                | _ ->
                    lastBorderStyle <- x.FormBorderStyle
                    lastState <- x.WindowState
                    x.FormBorderStyle <- FormBorderStyle.None

                    // hack: does not react when already maximized otherwise
                    if lastState = FormWindowState.Maximized then
                        x.WindowState <- FormWindowState.Normal

                    x.WindowState <- FormWindowState.Maximized
            x.ResumeLayout()
        )) |> ignore

    member x.Fullscreen
        with get() = 
            x.WindowState = FormWindowState.Maximized && x.FormBorderStyle = FormBorderStyle.None

        and set (v : bool) =
            if x.Fullscreen <> v then
                x.ToggleFullScreen()
                   
    member x.Title 
        with set v =
            customTitle <- Some v
            base.Text <- v

    member x.Control = ctrl

    member x.Location = ctrl.Location

    member x.RenderTask
        with get() = ctrl.RenderTask
        and set (t : IRenderTask) = ctrl.RenderTask <- t

    member x.Size
        with get() = V2i(base.ClientSize.Width, base.ClientSize.Height)
        and set (size : V2i) = base.ClientSize <- System.Drawing.Size(size.X, size.Y)

    member x.FramebufferSignature = ctrl.FramebufferSignature
    member x.Runtime = ctrl.Runtime
    member x.Sizes = ctrl.Sizes
    member x.Samples = ctrl.Samples
    member x.Keyboard = ctrl.Keyboard
    member x.Mouse = ctrl.Mouse
    member x.Time = ctrl.Time
    member x.Run() = Application.Run(x)

    member x.SubSampling 
        with get() = ctrl.SubSampling
        and set v = ctrl.SubSampling <- v

    interface IRenderWindow with
        member x.Cursor
            with get() = Cursor.Default
            and set c = ()
        member x.FramebufferSignature = ctrl.FramebufferSignature
        member x.Runtime = ctrl.Runtime
        member x.Time = ctrl.Time
        member x.RenderTask
            with get() = x.RenderTask
            and set (t : IRenderTask) = x.RenderTask <- t
            
        member x.SubSampling 
            with get() = x.SubSampling
            and set v = x.SubSampling <- v

        member x.Sizes = ctrl.Sizes
        member x.Samples = ctrl.Samples
        member x.Keyboard = ctrl.Keyboard
        member x.Mouse = ctrl.Mouse
        member x.Run() = x.Run()
        member x.BeforeRender = ctrl.BeforeRender
        member x.AfterRender = ctrl.AfterRender

[<AbstractClass; Sealed; Extension>]
type WinFormsApplicationExtensions private() =
    
    [<Extension>]
    static member CreateSimpleRenderWindow(this : IApplication, samples : int) =
        let w = new SimpleRenderWindow()
        this.Initialize(w.Control, samples)
        w

    [<Extension>]
    static member CreateSimpleRenderWindow(this : IApplication) =
        WinFormsApplicationExtensions.CreateSimpleRenderWindow(this, 1)