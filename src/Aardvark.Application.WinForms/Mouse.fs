namespace Aardvark.Application.WinForms

open System
open System.Windows.Forms
open Aardvark.Base
open Aardvark.Application

type private WinFormsButtons = System.Windows.Forms.MouseButtons

type Mouse(ctrl : Control) =

    let events = EventSource<MouseEvent>()

    let (~%) (m : WinFormsButtons) =
        let mutable buttons = MouseButtons.None

        if m &&& WinFormsButtons.Left <> WinFormsButtons.None then 
            buttons <- buttons ||| MouseButtons.Left

        if m &&& WinFormsButtons.Right <> WinFormsButtons.None then 
            buttons <- buttons ||| MouseButtons.Right

        if m &&& WinFormsButtons.Middle <> WinFormsButtons.None then 
            buttons <- buttons ||| MouseButtons.Middle

        buttons

    let (~%%) (m : MouseEventArgs) =
        let buttons = %m.Button
        let location = PixelPosition(m.X, m.Y, ctrl.ClientSize.Width, ctrl.ClientSize.Height)
        { location = location; buttons = buttons }

    let onMouseDown (s : obj) (e : MouseEventArgs) =
        events.Emit (MouseDown %%e)

    let onMouseUp (s : obj) (e : MouseEventArgs) =
        events.Emit (MouseUp %%e)

    let onMouseMove (s : obj) (e : MouseEventArgs) =
        events.Emit (MouseMove <| PixelPosition(e.X, e.Y, ctrl.ClientSize.Width, ctrl.ClientSize.Height))

    let onMouseClick (s : obj) (e : MouseEventArgs) =
        events.Emit (MouseClick %%e)

    let onMouseDoubleClick (s : obj) (e : MouseEventArgs) =
        events.Emit (MouseDoubleClick %%e)

    let onMouseDownHandler = MouseEventHandler(onMouseDown)
    let onMouseUpHandler = MouseEventHandler(onMouseUp)
    let onMouseMoveHandler = MouseEventHandler(onMouseMove)
    let onMouseClickHandler = MouseEventHandler(onMouseClick)
    let onMouseDoubleClickHandler = MouseEventHandler(onMouseDoubleClick)

    do ctrl.MouseDown.AddHandler onMouseDownHandler
       ctrl.MouseUp.AddHandler onMouseUpHandler
       ctrl.MouseMove.AddHandler onMouseMoveHandler
       ctrl.MouseClick.AddHandler onMouseClickHandler
       ctrl.MouseDoubleClick.AddHandler onMouseDoubleClickHandler

    member x.Dispose() =
        ctrl.MouseDown.RemoveHandler onMouseDownHandler
        ctrl.MouseUp.RemoveHandler onMouseUpHandler
        ctrl.MouseMove.RemoveHandler onMouseMoveHandler
        ctrl.MouseClick.RemoveHandler onMouseClickHandler
        ctrl.MouseDoubleClick.RemoveHandler onMouseDoubleClickHandler

    member x.Down = events |> Event.choose (function MouseDown p -> Some p | _ -> None)
    member x.Up = events |> Event.choose (function MouseUp p -> Some p | _ -> None)
    member x.Click = events |> Event.choose (function MouseClick p -> Some p | _ -> None)
    member x.DoubleClick = events |> Event.choose (function MouseDoubleClick p -> Some p | _ -> None)
    member x.Move = events |> Event.choose (function MouseMove p -> Some p | _ -> None)

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface IMouse with
        member x.Events = events :> IEvent<_>
