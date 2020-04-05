namespace Aardvark.Application

open System
open Aardvark.Base
open FSharp.Data.Adaptive

type TimeMod() =
    inherit AdaptiveObject()

    let sw = System.Diagnostics.Stopwatch.StartNew()
    let start = DateTime.Now

    member x.GetValue(t : AdaptiveToken) =
        x.EvaluateAlways t (fun _ -> 
            start + sw.Elapsed
        )

    interface IAdaptiveValue with
        member x.ContentType = typeof<System.DateTime>
        member x.IsConstant = false
        member x.GetValueUntyped(caller) = x.GetValue(caller) :> obj

    interface aval<DateTime> with
        member x.GetValue(caller) = x.GetValue(caller)

type IRenderTarget =
    abstract member Runtime : IRuntime
    abstract member Sizes : aval<V2i>
    abstract member Samples : int
    abstract member FramebufferSignature : IFramebufferSignature
    abstract member RenderTask : IRenderTask with get, set
    abstract member Time : aval<DateTime>

    abstract member BeforeRender : Microsoft.FSharp.Control.IEvent<unit>
    abstract member AfterRender : Microsoft.FSharp.Control.IEvent<unit>

type IRenderControl =
    inherit IRenderTarget

    abstract member Keyboard : IKeyboard
    abstract member Mouse : IMouse
    
type IRenderWindow =
    inherit IRenderControl
    
    abstract member Run : unit -> unit


            