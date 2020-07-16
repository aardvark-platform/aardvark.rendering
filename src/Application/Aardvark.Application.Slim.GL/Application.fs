namespace Aardvark.Application.Slim

open System

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering.GL
open Aardvark.Application
open System.Runtime.InteropServices


type OpenGlApplication(forceNvidia : bool, enableDebug : bool, shaderCachePath : Option<string>) =
    do if forceNvidia then Aardvark.Base.DynamicLinker.tryLoadLibrary "nvapi64.dll" |> ignore
       OpenTK.Toolkit.Init(new OpenTK.ToolkitOptions(Backend=OpenTK.PlatformBackend.PreferNative)) |> ignore
       
    let runtime = new Runtime()
    let glfw = Glfw.Application(runtime)
    
    let windowConfig =
        {
            Glfw.WindowConfig.title = "Aardvark rocks \\o/"
            Glfw.WindowConfig.width = 1024
            Glfw.WindowConfig.height = 768
            Glfw.WindowConfig.resizable = true
            Glfw.WindowConfig.focus = true
            Glfw.WindowConfig.vsync = true
            Glfw.WindowConfig.opengl = true
            Glfw.WindowConfig.physicalSize = false
            Glfw.WindowConfig.transparent = false
            Glfw.WindowConfig.samples = 1
        }
        
    let createContext() =
        let w = glfw.CreateWindow windowConfig
        let h = ContextHandle(w.Context, w.WindowInfo)
        if enableDebug then h.AttachDebugOutputIfNeeded()
        let o = ContextHandle.Current
        h.MakeCurrent()

        OpenTK.Graphics.OpenGL4.GL.Hint(OpenTK.Graphics.OpenGL4.HintTarget.PointSmoothHint, OpenTK.Graphics.OpenGL4.HintMode.Fastest)
        OpenTK.Graphics.OpenGL4.GL.Enable(OpenTK.Graphics.OpenGL4.EnableCap.TextureCubeMapSeamless)
        OpenTK.Graphics.OpenGL4.GL.Disable(OpenTK.Graphics.OpenGL4.EnableCap.PolygonSmooth)
        OpenTK.Graphics.OpenGL4.GL.Hint(OpenTK.Graphics.OpenGL4.HintTarget.FragmentShaderDerivativeHint, OpenTK.Graphics.OpenGL4.HintMode.Nicest)

        h.ReleaseCurrent()
        match o with
        | Some o -> o.MakeCurrent()
        | None -> ()
        glfw.RemoveExistingWindow w
        h

    let resourceContexts =
        Array.init 2 (fun _ ->
            createContext()
        )

    let ctx = new Context(runtime, enableDebug, resourceContexts, fun () -> glfw.Invoke createContext)

    do ctx.ShaderCachePath <- shaderCachePath
       glfw.Context <- ctx
       runtime.Initialize(ctx, true, true)
 
    new(enableDebug) = new OpenGlApplication(true, enableDebug)
    new() = new OpenGlApplication(true, false)
    new(forceNvidia, enableDebug) = new OpenGlApplication(forceNvidia, enableDebug, Context.DefaultShaderCachePath)

    member x.Context = ctx
    member x.Runtime = runtime

    member x.Dispose() =
        // first dispose runtime in order to properly dispose resources..
        runtime.Dispose()
        ctx.Dispose()

    member x.Initialize(ctrl : IRenderControl, samples : int) = 
        failwithf "unknown control type: %A" ctrl
        

    member x.CreateGameWindow(?samples : int) =
        let samples = defaultArg samples 1
        let w = glfw.CreateWindow { windowConfig with samples = samples }

        w.KeyDown.Add (fun e ->
            match e.Key with
            | Keys.R when e.Ctrl && e.Shift ->
                w.RenderAsFastAsPossible <- not w.RenderAsFastAsPossible
                Log.line "[Window] RenderAsFastAsPossible: %A" w.RenderAsFastAsPossible
            | Keys.V when e.Ctrl && e.Shift ->
                w.VSync <- not w.VSync
                Log.line "[Window] VSync: %A" w.VSync
            | Keys.G when e.Ctrl && e.Shift ->
                w.MeasureGpuTime <- not w.MeasureGpuTime
                Log.line "[Window] MeasureGpuTime: %A" w.MeasureGpuTime
            | Keys.Enter when e.Ctrl && e.Shift ->
                w.Fullcreen <- not w.Fullcreen
                Log.line "[Window] Fullcreen: %A" w.Fullcreen
            | _ ->
                ()
        )

        w

    interface IApplication with
        member x.Initialize(ctrl : IRenderControl, samples : int) = x.Initialize(ctrl, samples)
        member x.Runtime = x.Runtime :> IRuntime
        member x.Dispose() = x.Dispose()


