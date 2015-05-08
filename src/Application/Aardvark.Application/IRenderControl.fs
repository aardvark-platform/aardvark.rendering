namespace Aardvark.Application

open System
open Aardvark.Base
open Aardvark.Base.Incremental

type TimeMod() =
    inherit AdaptiveObject()

    interface IMod with
        member x.IsConstant = false
        member x.GetValue() = x.EvaluateAlways (fun () -> DateTime.Now :> obj)

    interface IMod<DateTime> with
        member x.Compute() = DateTime.Now
        member x.GetValue() = x.EvaluateAlways (fun () -> DateTime.Now)

type IRenderTarget =
    abstract member Sizes : IEvent<V2i>
    abstract member RenderTask : IRenderTask with get, set
    abstract member Time : IMod<DateTime>

type IRenderControl =
    inherit IRenderTarget

    abstract member Keyboard : IKeyboard
    abstract member Mouse : IMouse
    