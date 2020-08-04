namespace Aardvark.Application.WPF

open System
open System.IO
open System.Windows.Forms

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering.GL
open Aardvark.Application

module Config =
    let mutable useSharingControl = false

type OpenGlApplication(forceNvidia : bool, enableDebug : bool, shaderCachePath : Option<string>) =
    do if forceNvidia then Aardvark.Base.DynamicLinker.tryLoadLibrary "nvapi64.dll" |> ignore
       OpenTK.Toolkit.Init(new OpenTK.ToolkitOptions(Backend=OpenTK.PlatformBackend.PreferNative)) |> ignore

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
        ctx.Dispose()
        runtime.Dispose()

    member x.Initialize(ctrl : IRenderControl, samples : int) = 
        
        match ctrl with
            | :? RenderControl as ctrl ->
                if Config.useSharingControl then
                    let impl = new OpenGlSharingRenderControl(runtime, samples)
                    ctrl.Implementation <- impl
                else 
                    let impl = new OpenGlRenderControl(runtime, enableDebug, samples) 
                    ctrl.Implementation <- impl
            | _ ->
                failwithf "unknown control type: %A" ctrl
                    
        ()

    interface IApplication with
        member x.Initialize(ctrl : IRenderControl, samples : int) = x.Initialize(ctrl, samples)
        member x.Runtime = x.Runtime :> IRuntime
        member x.Dispose() = x.Dispose()
