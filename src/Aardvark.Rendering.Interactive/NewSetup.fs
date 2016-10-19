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

        static let createWindow() =
            let win = app.CreateSimpleRenderWindow(8)
            win.Text <- "Aardvark Interactive Session Setup"
            let sg = Mod.init emptySg
            let task = 
                app.Runtime.CompileRender(win.FramebufferSignature, Sg.dynamic sg)
                    |> DefaultOverlays.withStatistics
            win.RenderTask <- task
            win.Show()
            win.Disposed.Add (fun _ -> 
                task.Dispose()
                transact (fun () -> sg.Value <- emptySg)
                window <- None
            )
            win, sg

        static let getWindowAndSg() =
            match window with
                | None ->
                    let t = createWindow()
                    window <- Some t
                    t
                | Some t ->
                    t

        static member Window = getWindowAndSg() |> fst :> IRenderWindow
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