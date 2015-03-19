namespace Aardvark.Application

open Aardvark.Base

type IRenderTarget =
    abstract member Sizes : IEvent<V2i>
    abstract member RenderTask : IRenderTask with get, set

type IRenderControl =
    inherit IRenderTarget

    abstract member Keyboard : IKeyboard
    abstract member Mouse : IMouse
    