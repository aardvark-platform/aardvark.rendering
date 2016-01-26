
#I @"..\..\..\bin\Debug\"
#I @"..\..\..\bin\Release\"

#r @"..\..\..\Packages\Aardvark.Base.FSharp\lib\net45\Aardvark.Base.TypeProviders.dll"
#r "Aardvark.Base.dll"
#r "Aardvark.Base.Essentials.dll"
#r "Aardvark.Base.FSharp.dll"
#r "Aardvark.Base.Incremental.dll"
#r "Aardvark.Base.Runtime.dll"
#r "Aardvark.Base.Rendering.dll"
#r "FShade.dll"
#r "FShade.Compiler.dll"
#r "Aardvark.SceneGraph.dll"
#r "Aardvark.Rendering.NanoVg.dll"
#r "Aardvark.Rendering.GL.dll"
#r "Aardvark.Application.dll"
#r "Aardvark.Application.WinForms.dll"
#r "Aardvark.Application.WinForms.GL.dll"

namespace Examples

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

    let mutable private initialized = 0

    let private winFormsApp = lazy ( new OpenGlApplication() )




    let init() =
        if Interlocked.Exchange(&initialized, 1) = 0 then
            System.Environment.CurrentDirectory <- System.IO.Path.Combine(__SOURCE_DIRECTORY__, @"..\..\..\bin\Debug\")
            IntrospectionProperties.CustomEntryAssembly <- System.Reflection.Assembly.LoadFile <| System.IO.Path.Combine(__SOURCE_DIRECTORY__, @"..\..\..\bin\Debug\Examples.exe")

            Ag.initialize()
            Aardvark.Base.Ag.unpack <- fun o ->
                    match o with
                        | :? IMod as o -> o.GetValue(null)
                        | _ -> o
 
    let openWindow() =
        init()

        let app = winFormsApp.Value
        let win = app.CreateSimpleRenderWindow(1)
        let task = app.Runtime.CompileRender(win.FramebufferSignature, Sg.group [])

        win.Text <- @"Aardvark rocks \o/"
        win.TopMost <- true
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
        init()

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
        init()
        showSg w (Sg.group [])

    let viewTrafo (win : IRenderControl) =
        let view =  CameraView.LookAt(V3d(3.0, 3.0, 3.0), V3d.Zero, V3d.OOI)
        DefaultCameraController.control win.Mouse win.Keyboard win.Time view

    let perspective (win : IRenderControl) = 
        win.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.01 10.0 (float s.X / float s.Y))


    let runInteractive () =
           
        
        System.Environment.CurrentDirectory <- System.IO.Path.Combine(__SOURCE_DIRECTORY__, @"..\..\..\bin\Debug\")
        IntrospectionProperties.CustomEntryAssembly <- System.Reflection.Assembly.LoadFile <| System.IO.Path.Combine(__SOURCE_DIRECTORY__, @"..\..\..\bin\Debug\Examples.exe")

        Ag.initialize()
        Aardvark.Base.Ag.unpack <- fun o ->
                match o with
                    | :? IMod as o -> o.GetValue(null)
                    | _ -> o


        let app = new OpenGlApplication()
        let win = app.CreateSimpleRenderWindow(1)
        let root = Mod.init <| (Sg.group [] :> ISg)


        let task = app.Runtime.CompileRender(win.FramebufferSignature, Sg.DynamicNode root)

        win.Text <- @"Aardvark rocks \o/"
        win.TopMost <- true
        win.Visible <- true

        let fixupRenderTask () =
            if win.RenderTask = Unchecked.defaultof<_> then win.RenderTask <- task

        (fun s -> fixupRenderTask () ; transact (fun () -> Mod.change root s)), win, task
      
    