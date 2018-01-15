namespace Aardvark.Application

open System
open Aardvark.Base
open Aardvark.Base.Incremental

type TimeMod() =
    inherit AdaptiveObject()

    interface IMod with
        member x.IsConstant = false
        member x.GetValue(caller) = x.EvaluateAlways caller (fun caller -> DateTime.Now :> obj)

    interface IMod<DateTime> with
        member x.GetValue(caller) = x.EvaluateAlways caller (fun caller -> DateTime.Now)

type IRenderTarget =
    abstract member Runtime : IRuntime
    abstract member Sizes : IMod<V2i>
    abstract member Samples : int
    abstract member FramebufferSignature : IFramebufferSignature
    abstract member RenderTask : IRenderTask with get, set
    abstract member Time : IMod<DateTime>

    abstract member BeforeRender : Microsoft.FSharp.Control.IEvent<unit>
    abstract member AfterRender : Microsoft.FSharp.Control.IEvent<unit>

type IRenderControl =
    inherit IRenderTarget

    abstract member Keyboard : IKeyboard
    abstract member Mouse : IMouse
    
type IRenderWindow =
    inherit IRenderControl
    
    abstract member Run : unit -> unit


            