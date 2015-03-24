namespace Aardvark.Application

open System
open Aardvark.Base
open Aardvark.Base.Incremental

type IRenderTarget =
    abstract member Sizes : IEvent<V2i>
    abstract member RenderTask : IRenderTask with get, set
    abstract member Time : IMod<DateTime>

type IRenderControl =
    inherit IRenderTarget

    
    abstract member Keyboard : IKeyboard
    abstract member Mouse : IMouse
    