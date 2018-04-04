﻿namespace Aardvark.Application.WinForms

open System
open System.Windows.Forms

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Rendering.GL
open Aardvark.Application


type OpenGlApplication(forceNvidia : bool, enableDebug : bool) =
    do if forceNvidia then Aardvark.Base.DynamicLinker.tryLoadLibrary "nvapi64.dll" |> ignore
       OpenTK.Toolkit.Init(new OpenTK.ToolkitOptions(Backend=OpenTK.PlatformBackend.PreferNative)) |> ignore
       try 
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException)
       with e -> Report.Warn("Could not set UnhandledExceptionMode.")

    let runtime = new Runtime()
    let ctx = new Context(runtime, enableDebug)
    do runtime.Context <- ctx
 
    let defaultCachePath =
        let dir =
            Path.combine [
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                "Aardvark"
                "OpenGlShaderCache"
            ]
        ctx.ShaderCachePath <- Some dir
        dir
              
    let init =
        let initialized = ref false
        fun (ctx : Context)  ->
            if not !initialized then
                initialized := true
//                glctx.MakeCurrent(w)
//                glctx.LoadAll()
//                match glctx with | :? OpenTK.Graphics.IGraphicsContextInternal as c -> c.GetAddress("glBindFramebuffer") |> ignore | _ -> ()
//
//                let handle = ContextHandle(glctx,w)
//                ctx.CurrentContextHandle <- Some handle
//                ContextHandle.Current <- Some handle

                using ctx.ResourceLock (fun _ ->

                    Log.startTimed "initializing OpenGL runtime"

//                    Aardvark.Rendering.GL.OpenGl.Unsafe.BindFramebuffer (int OpenTK.Graphics.OpenGL4.FramebufferTarget.Framebuffer) 0
//                    OpenTK.Graphics.OpenGL4.GL.GetError() |> ignore
//                    OpenTK.Graphics.OpenGL4.GL.BindFramebuffer(OpenTK.Graphics.OpenGL4.FramebufferTarget.Framebuffer, 0)
//                    OpenTK.Graphics.OpenGL4.GL.Check "first GL call failed"
                    OpenGl.Unsafe.ActiveTexture (int OpenTK.Graphics.OpenGL4.TextureUnit.Texture0)
                    OpenTK.Graphics.OpenGL4.GL.Check "first GL call failed"
                
                    try GLVM.vmInit()
                    with _ -> Log.line "No glvm found, running without glvm"
               
                    Log.line "vendor:   %A" ctx.Driver.vendor
                    Log.line "renderer: %A" ctx.Driver.renderer 
                    Log.line "version:  OpenGL %A / GLSL %A" ctx.Driver.version ctx.Driver.glsl

                    Log.stop()
                )

    do init ctx

//                glctx.MakeCurrent(null)
//                ctx.CurrentContextHandle <- None
//                ContextHandle.Current <- None

    new(enableDebug) = new OpenGlApplication(true, enableDebug)
    new() = new OpenGlApplication(true, false)

    member x.Context = ctx
    member x.Runtime = runtime

    member x.ShaderCachePath
        with get() = ctx.ShaderCachePath
        and set p = ctx.ShaderCachePath <- p

    member x.Dispose() =
        // first dispose runtime in order to properly dispose resources..
        runtime.Dispose()
        ctx.Dispose()

    member x.Initialize(ctrl : IRenderControl, samples : int) = 
        match ctrl with
            | :? RenderControl as ctrl ->
                
                ctrl.Implementation <- new OpenGlRenderControl(runtime, enableDebug, samples)
                init ctx 
            | _ ->
                failwithf "unknown control type: %A" ctrl
        

    member x.CreateGameWindow(?samples : int) =
        let samples = defaultArg samples 1
        let w = new GameWindow(runtime, enableDebug, samples)
        init ctx 
        w

    interface IApplication with
        member x.Initialize(ctrl : IRenderControl, samples : int) = x.Initialize(ctrl, samples)
        member x.Runtime = x.Runtime :> IRuntime
        member x.Dispose() = x.Dispose()


