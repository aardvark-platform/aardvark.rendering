
#I @"..\..\..\bin\Debug\"
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
    open Aardvark.Base
    open Aardvark.Base.Incremental
    open Aardvark.Base.Rendering
    open Aardvark.Rendering.NanoVg
    open Aardvark.SceneGraph
    open Aardvark.SceneGraph.Semantics

    open Aardvark.Application
    open Aardvark.Application.WinForms

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

