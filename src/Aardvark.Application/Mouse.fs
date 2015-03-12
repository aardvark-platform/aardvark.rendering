namespace Aardvark.Application

open Aardvark.Base

type Buttons =
    | None   = 0x0000
    | Left   = 0x0001
    | Right  = 0x0002
    | Middle = 0x0004

type MouseEvent = { buttons : Buttons; location : PixelPosition }


type IMouse =
    abstract member Down : IEvent<MouseEvent>
    abstract member Up : IEvent<MouseEvent>
    abstract member Click : IEvent<MouseEvent>
    abstract member DoubleClick : IEvent<MouseEvent>
    abstract member Move : IEvent<PixelPosition>