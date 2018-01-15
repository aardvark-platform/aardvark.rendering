namespace Aardvark.Application.WinForms

open System
open System.Runtime.CompilerServices
open System.Windows.Forms
open Aardvark.Application
open Aardvark.Base
open Aardvark.Base.Incremental

type SimpleRenderWindow() as this =
    inherit Form()

    let ctrl = new RenderControl()

    

    let mutable lastBorderStyle = FormBorderStyle.None
    let mutable lastState = FormWindowState.Normal

    let mutable frameCount = 0
    let mutable totalTime = MicroTime.Zero
    let mutable baseTitle = ""

    

    member x.NewFrame (t : MicroTime) = 
        frameCount <- frameCount + 1
        totalTime <- totalTime + t
        if frameCount > 50 then
            let fps = float frameCount / totalTime.TotalSeconds
            x.Text <- baseTitle + sprintf " (%.3f fps)" fps
            frameCount <- 0
            totalTime <- MicroTime.Zero
        ()

    override x.OnHandleCreated(e) =
        base.OnHandleCreated(e)
        x.ClientSize <- System.Drawing.Size(1024, 768)
        ctrl.Dock <- DockStyle.Fill
        x.Controls.Add ctrl
        // find better way...
        let backend = 
            if ctrl.Runtime.GetType().FullName.ToLower().Contains("vulkan") then "Vulkan"
            else "OpenGL"
        baseTitle <- sprintf "Aardvark rocks \\o/ (%s SimpleRenderWindow)" backend
        x.Text <- baseTitle
        let alt = ctrl.Keyboard.IsDown(Keys.LeftAlt)
        let shift = ctrl.Keyboard.IsDown(Keys.LeftShift)
        ctrl.Keyboard.KeyDown(Keys.Enter).Values.Add (fun () ->
            if Mod.force alt && Mod.force shift then
                this.ToggleFullScreen()
        )

        let sw = System.Diagnostics.Stopwatch()
        ctrl.BeforeRender.Add sw.Restart
        ctrl.AfterRender.Add (fun () -> sw.Stop(); x.NewFrame sw.MicroTime)

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

    interface IRenderWindow with
        member x.FramebufferSignature = ctrl.FramebufferSignature
        member x.Runtime = ctrl.Runtime
        member x.Time = ctrl.Time
        member x.RenderTask
            with get() = x.RenderTask
            and set (t : IRenderTask) = x.RenderTask <- t
            

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