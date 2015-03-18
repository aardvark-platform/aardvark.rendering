namespace Aardvark.Application.WPF

open System
open System.Windows.Forms

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Rendering.GL
open Aardvark.Application


type OpenGlApplication() =
    let runtime = new Runtime()
    let ctx = new Context(runtime)
    do runtime.Context <- ctx

    member x.Context = ctx
    member x.Runtime = runtime

    member x.Dispose() =
        ctx.Dispose()
        runtime.Dispose()

    member x.Initialize(ctrl : IRenderControl, samples : int) = 
        match ctrl with
            | :? RenderControl as ctrl ->
                ctrl.Implementation <- new OpenGlRenderControl(ctx, samples)
            | _ ->
                failwith "unknown control type: %A" ctrl
                    
        ()


    interface IApplication with
        member x.Initialize(ctrl : IRenderControl, samples : int) = x.Initialize(ctrl, samples)
        member x.Runtime = x.Runtime :> IRuntime
        member x.Dispose() = x.Dispose()


