namespace Aardvark.Application.WinForms

open System.Windows.Forms

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.GL
open Aardvark.Application

type OpenGlApplication(forceNvidia : bool, debug : IDebugConfig, shaderCachePath : Option<string>) =
    do if forceNvidia then Aardvark.Base.DynamicLinker.tryLoadLibrary "nvapi64.dll" |> ignore
       OpenTK.Toolkit.Init(new OpenTK.ToolkitOptions(Backend=OpenTK.PlatformBackend.PreferNative)) |> ignore
       try 
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException)
       with e -> Report.Warn("Could not set UnhandledExceptionMode.")

    let runtime = new Runtime(debug)
    let ctx = new Context(runtime, fun () -> ContextHandleOpenTK.create debug)

    do ctx.ShaderCachePath <- shaderCachePath
       runtime.Initialize(ctx)
 
    new(forceNvidia : bool, debug : bool, shaderCachePath : Option<string>) =
        new OpenGlApplication(forceNvidia, DebugLevel.ofBool debug, shaderCachePath)

    new() = new OpenGlApplication(true, DebugLevel.None)
    new(debug : IDebugConfig) = new OpenGlApplication(true, debug)
    new(debug : bool) = new OpenGlApplication(true, debug)
    new(forceNvidia : bool, debug : IDebugConfig) = new OpenGlApplication(forceNvidia, debug, Context.DefaultShaderCachePath)
    new(forceNvidia : bool, debug : bool) = new OpenGlApplication(forceNvidia, debug, Context.DefaultShaderCachePath)

    member x.Context = ctx
    member x.Runtime = runtime

    member x.Dispose() =
        // first dispose runtime in order to properly dispose resources..
        runtime.Dispose()
        ctx.Dispose()

    member x.Initialize(ctrl : IRenderControl, samples : int) = 
        match ctrl with
            | :? RenderControl as ctrl ->
                ctrl.Implementation <- new OpenGlRenderControl(runtime, debug, samples)
            | _ ->
                failwithf "unknown control type: %A" ctrl


    interface IApplication with
        member x.Initialize(ctrl : IRenderControl, samples : int) = x.Initialize(ctrl, samples)
        member x.Runtime = x.Runtime :> IRuntime
        member x.Dispose() = x.Dispose()


