namespace Aardvark.Application.WPF

#if BuildingOnWindows

open System
open System.Windows.Forms

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Rendering.GL
open Aardvark.Application


type OpenGlApplication() =
    do OpenTK.Toolkit.Init(new OpenTK.ToolkitOptions(Backend=OpenTK.PlatformBackend.PreferNative)) |> ignore
    let runtime = new Runtime()
    let ctx = new Context(runtime)
    do runtime.Context <- ctx

    let init =
        let initialized = ref false
        fun (ctx : Context) (glctx : OpenTK.Graphics.IGraphicsContext) (w : OpenTK.Platform.IWindowInfo) ->
            if not !initialized then
                initialized := true
                glctx.MakeCurrent(w)

                let handle = ContextHandle(glctx,w)
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

                glctx.MakeCurrent(null)
                ctx.CurrentContextHandle <- None
                ContextHandle.Current <- None

    member x.Context = ctx
    member x.Runtime = runtime


    member x.Dispose() =
        ctx.Dispose()
        runtime.Dispose()

    member x.Initialize(ctrl : IRenderControl, samples : int) = 
        
        match ctrl with
            | :? RenderControl as ctrl ->
                let impl = new OpenGlRenderControl(runtime, samples)
                ctrl.Implementation <- impl
                init ctx impl.Context impl.WindowInfo
            | _ ->
                failwith "unknown control type: %A" ctrl
                    
        ()

    member x.CreateGameWindow(samples : int) =
        let w = new GameWindow(runtime, samples)
        init ctx w.Context w.WindowInfo
        w

    interface IApplication with
        member x.Initialize(ctrl : IRenderControl, samples : int) = x.Initialize(ctrl, samples)
        member x.Runtime = x.Runtime :> IRuntime
        member x.Dispose() = x.Dispose()

#endif
