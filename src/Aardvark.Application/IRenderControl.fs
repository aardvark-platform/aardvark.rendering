namespace Aardvark.Application

open Aardvark.Base

type IRenderControl =
    inherit IFramebuffer

    abstract member Sizes : IEvent<V2i>
    abstract member CameraView : ICameraView with get, set
    abstract member CameraProjection : ICameraProjection with get, set

    abstract member Keyboard : IKeyboard
    abstract member Mouse : IMouse

    abstract member RenderTask : IRenderTask with get, set
    