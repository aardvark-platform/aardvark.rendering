namespace Aardvark.Application.WinForms

open System.Windows.Forms

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.GL
open Aardvark.Application

type OpenGlApplication(forceNvidia : bool, enableDebug : bool, shaderCachePath : Option<string>) =
    do if forceNvidia then Aardvark.Base.DynamicLinker.tryLoadLibrary "nvapi64.dll" |> ignore
       OpenTK.Toolkit.Init(new OpenTK.ToolkitOptions(Backend=OpenTK.PlatformBackend.PreferNative)) |> ignore
       try 
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException)
       with e -> Report.Warn("Could not set UnhandledExceptionMode.")

    let runtime = new Runtime()
    let ctx = new Context(runtime, fun () -> ContextHandleOpenTK.create enableDebug)

    do ctx.ShaderCachePath <- shaderCachePath
       runtime.Initialize(ctx, true, true)
 
    new() = new OpenGlApplication(true, false)
    new(enableDebug) = new OpenGlApplication(true, enableDebug)
    new(forceNvidia, enableDebug) = new OpenGlApplication(forceNvidia, enableDebug, Context.DefaultShaderCachePath)

    member x.Context = ctx
    member x.Runtime = runtime

    member x.Dispose() =
        // first dispose runtime in order to properly dispose resources..
        runtime.Dispose()
        ctx.Dispose()

    member x.Initialize(ctrl : IRenderControl, samples : int) = 
        match ctrl with
            | :? RenderControl as ctrl ->
                ctrl.Implementation <- new OpenGlRenderControl(runtime, enableDebug, samples)
            | _ ->
                failwithf "unknown control type: %A" ctrl


    interface IApplication with
        member x.Initialize(ctrl : IRenderControl, samples : int) = x.Initialize(ctrl, samples)
        member x.Runtime = x.Runtime :> IRuntime
        member x.Dispose() = x.Dispose()


