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
        member x.GetValue() = x.EvaluateAlways (fun () -> DateTime.Now)

type IRenderTarget =
    abstract member Runtime : IRuntime
    abstract member Sizes : IMod<V2i>
    abstract member RenderTask : IRenderTask with get, set
    abstract member Time : IMod<DateTime>

type IRenderControl =
    inherit IRenderTarget

    abstract member Keyboard : IKeyboard
    abstract member Mouse : IMouse
    
type IRenderWindow =
    inherit IRenderControl
    
    abstract member Run : unit -> unit