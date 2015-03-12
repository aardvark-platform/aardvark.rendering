namespace Aardvark.Application


open System
open Aardvark.Base
open System.Reactive
open System.Reactive.Linq

type MouseButtons =
    | None   = 0x0000
    | Left   = 0x0001
    | Right  = 0x0002
    | Middle = 0x0004

type MouseEventProperties = { buttons : MouseButtons; location : PixelPosition }

type MouseEvent =
    | MouseDown of MouseEventProperties
    | MouseUp of MouseEventProperties
    | MouseClick of MouseEventProperties
    | MouseDoubleClick of MouseEventProperties
    | MouseMove of PixelPosition with

        member x.Properties =
            match x with
                | MouseDown p -> p
                | MouseUp p -> p
                | MouseClick p -> p
                | MouseDoubleClick p -> p
                | MouseMove pp -> { buttons = MouseButtons.None;  location = pp }

    

type IMouse =
    abstract member Events : IEvent<MouseEvent>