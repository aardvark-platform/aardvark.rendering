namespace Aardvark.Application.WinForms

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

    interface IApplication with
        member x.Initialize(ctrl : IRenderControl) = 
            ()

        member x.Runtime = x.Runtime :> IRuntime
        member x.Dispose() = x.Dispose()


