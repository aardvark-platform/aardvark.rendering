namespace Aardvark.Application.WPF

#if WINDOWS

open System
open System.Windows.Forms

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Rendering.GL
open Aardvark.Application


type OpenGlApplication(forceNvidia : bool, enableDebug : bool) =
    do if forceNvidia then Aardvark.Base.DynamicLinker.tryLoadLibrary "nvapi64.dll" |> ignore
       OpenTK.Toolkit.Init(new OpenTK.ToolkitOptions(Backend=OpenTK.PlatformBackend.PreferNative)) |> ignore

    let runtime = new Runtime()
    let ctx = new Context(runtime, enableDebug)
    do runtime.Context <- ctx

    let init =
        let initialized = ref false
        fun (ctx : Context) (handle : ContextHandle)->
            if not !initialized then
                initialized := true
                handle.MakeCurrent()

                ctx.CurrentContextHandle <- Some handle
                ContextHandle.Current <- Some handle

                using ctx.ResourceLock (fun _ ->
                    try GLVM.vmInit()
                    with _ -> Log.line "No glvm found, running without glvm"

                    Log.startTimed "initializing OpenGL runtime"

                    OpenTK.Graphics.OpenGL4.GL.GetError() |> ignore
                    OpenGl.Unsafe.ActiveTexture (int OpenTK.Graphics.OpenGL4.TextureUnit.Texture0)
                    OpenTK.Graphics.OpenGL4.GL.Check "first GL call failed"
                
               
                    Log.line "vendor:   %A" ctx.Driver.vendor
                    Log.line "renderer: %A" ctx.Driver.renderer 
                    Log.line "version:  OpenGL %A / GLSL %A" ctx.Driver.version ctx.Driver.glsl

                    Log.stop()
                )

                handle.ReleaseCurrent()
                ctx.CurrentContextHandle <- None
                ContextHandle.Current <- None

    new(enableDebug) = new OpenGlApplication(true, enableDebug)
    new() = new OpenGlApplication(true, false)

    member x.Context = ctx
    member x.Runtime = runtime


    member x.Dispose() =
        ctx.Dispose()
        runtime.Dispose()

    member x.Initialize(ctrl : IRenderControl, samples : int) = 
        
        match ctrl with
            | :? RenderControl as ctrl ->
                //let impl = new OpenGlRenderControl(runtime, enableDebug, samples)
                let impl = new OpenGlSharingRenderControl(runtime, samples)
                ctrl.Implementation <- impl
                init ctx impl.ContextHandle
            | _ ->
                failwithf "unknown control type: %A" ctrl
                    
        ()

    member x.CreateGameWindow(samples : int) =
        let w = new GameWindow(runtime, enableDebug, samples)
        let handle = ContextHandle(w.Context, w.WindowInfo)
        init ctx handle
        w

    interface IApplication with
        member x.Initialize(ctrl : IRenderControl, samples : int) = x.Initialize(ctrl, samples)
        member x.Runtime = x.Runtime :> IRuntime
        member x.Dispose() = x.Dispose()

#endif
