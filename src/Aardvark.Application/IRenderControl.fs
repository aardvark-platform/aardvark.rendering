namespace Aardvark.Application

open Aardvark.Base

type IRenderControlImplementation =
    abstract member RenderTask : IRenderTask with get, set

type IRenderControl =
    inherit IRenderControlImplementation

    abstract member Sizes : IEvent<V2i>
    abstract member CameraView : ICameraView with get, set
    abstract member CameraProjection : ICameraProjection with get, set
    abstract member Keyboard : IKeyboard
    abstract member Mouse : IMouse
    