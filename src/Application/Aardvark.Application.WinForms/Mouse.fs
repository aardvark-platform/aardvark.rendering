namespace Aardvark.Application.WinForms

open System
open System.Windows.Forms
open Aardvark.Base
open Aardvark.Application

type private WinFormsButtons = System.Windows.Forms.MouseButtons

//type MouseOld(ctrl : Control) =
//    let mutable lastPos = PixelPosition(0,0,ctrl.ClientSize.Width, ctrl.ClientSize.Height)
//
//    let events = EventSource<MouseEvent>()
//
//    let (~%) (m : WinFormsButtons) =
//        let mutable buttons = MouseButtons.None
//
//        if m &&& WinFormsButtons.Left <> WinFormsButtons.None then 
//            buttons <- buttons ||| MouseButtons.Left
//
//        if m &&& WinFormsButtons.Right <> WinFormsButtons.None then 
//            buttons <- buttons ||| MouseButtons.Right
//
//        if m &&& WinFormsButtons.Middle <> WinFormsButtons.None then 
//            buttons <- buttons ||| MouseButtons.Middle
//
//        buttons
//
//    let pos (e : MouseEventArgs) =
//        let pp = PixelPosition(e.X, e.Y, ctrl.ClientSize.Width, ctrl.ClientSize.Height)
//        if pp <> lastPos then
//            lastPos <- pp
//            events.Emit (MouseMove pp)
//        pp
//
//    let (~%%) (m : MouseEventArgs) =
//        let buttons = %m.Button
//        let location = pos m
//        { location = location; buttons = buttons }
//
//    let mousePos() =
//        let p = ctrl.PointToClient(Control.MousePosition)
//        let s = ctrl.ClientSize
//        
//        let x = clamp 0 (s.Width-1) p.X
//        let y = clamp 0 (s.Height-1) p.Y
//
//        let pp = PixelPosition(x, y, ctrl.ClientSize.Width, ctrl.ClientSize.Height)
//        if pp <> lastPos then
//            lastPos <- pp
//            events.Emit (MouseMove pp)
//        pp
//
//
//    let onMouseDownHandler = MouseEventHandler(fun s e -> events.Emit (MouseDown %%e))
//    let onMouseUpHandler = MouseEventHandler(fun s e -> events.Emit (MouseUp %%e))
//    let onMouseMoveHandler = MouseEventHandler(fun s e -> events.Emit (MouseMove (pos e)))
//    let onMouseClickHandler = MouseEventHandler(fun s e -> events.Emit (MouseClick %%e))
//    let onMouseDoubleClickHandler = MouseEventHandler(fun s e -> events.Emit (MouseDoubleClick %%e))
//    let onMouseWheelHandler = MouseEventHandler(fun s e -> events.Emit (MouseScroll(float e.Delta, pos e)))
//    let onMouseEnter = EventHandler(fun s e -> events.Emit (MouseEnter <| mousePos()))
//    let onMouseLeave = EventHandler(fun s e -> events.Emit (MouseLeave <| mousePos()))
//
//    do ctrl.MouseDown.AddHandler onMouseDownHandler
//       ctrl.MouseUp.AddHandler onMouseUpHandler
//       ctrl.MouseMove.AddHandler onMouseMoveHandler
//       ctrl.MouseClick.AddHandler onMouseClickHandler
//       ctrl.MouseDoubleClick.AddHandler onMouseDoubleClickHandler
//       ctrl.MouseWheel.AddHandler onMouseWheelHandler
//       ctrl.MouseEnter.AddHandler onMouseEnter
//       ctrl.MouseLeave.AddHandler onMouseLeave
//
//    member x.Dispose() =
//        ctrl.MouseDown.RemoveHandler onMouseDownHandler
//        ctrl.MouseUp.RemoveHandler onMouseUpHandler
//        ctrl.MouseMove.RemoveHandler onMouseMoveHandler
//        ctrl.MouseClick.RemoveHandler onMouseClickHandler
//        ctrl.MouseDoubleClick.RemoveHandler onMouseDoubleClickHandler
//        ctrl.MouseWheel.RemoveHandler onMouseWheelHandler
//        ctrl.MouseEnter.RemoveHandler onMouseEnter
//        ctrl.MouseLeave.RemoveHandler onMouseLeave
//
//    member x.Down = events |> Event.choose (function MouseDown p -> Some p | _ -> None)
//    member x.Up = events |> Event.choose (function MouseUp p -> Some p | _ -> None)
//    member x.Click = events |> Event.choose (function MouseClick p -> Some p | _ -> None)
//    member x.DoubleClick = events |> Event.choose (function MouseDoubleClick p -> Some p | _ -> None)
//    member x.Move = events |> Event.choose (function MouseMove p -> Some p | _ -> None)
//    member x.Scroll = events |> Event.choose (function MouseScroll(delta, p) -> Some(delta,p) | _ -> None)
//    member x.Enter = events |> Event.choose (function MouseEnter p -> Some(p) | _ -> None)
//    member x.Leave = events |> Event.choose (function MouseLeave p -> Some(p) | _ -> None)
//
//    interface IDisposable with
//        member x.Dispose() = x.Dispose()
//
//    interface IMouse with
//        member x.Events = events :> IEvent<_>
//

type Mouse() as this =
    inherit EventMouse(false)
    let mutable ctrl : Option<Control> = None
    let mutable lastPos = PixelPosition(0,0,0,0)

    let size() =
        match ctrl with
            | Some ctrl -> V2i(ctrl.ClientSize.Width, ctrl.ClientSize.Height)
            | _ -> V2i.Zero

    let (~%) (m : WinFormsButtons) =
        let mutable buttons = MouseButtons.None

        if m &&& WinFormsButtons.Left <> WinFormsButtons.None then 
            buttons <- buttons ||| MouseButtons.Left

        if m &&& WinFormsButtons.Right <> WinFormsButtons.None then 
            buttons <- buttons ||| MouseButtons.Right

        if m &&& WinFormsButtons.Middle <> WinFormsButtons.None then 
            buttons <- buttons ||| MouseButtons.Middle

        buttons

    let (~%%) (e : MouseEventArgs) =
        let s = size()
        let pp = PixelPosition(e.X, e.Y, s.X, s.Y)
        pp

    let mousePos() =
         match ctrl with
            | Some ctrl -> 
                let p = ctrl.PointToClient(Control.MousePosition)
                let s = ctrl.ClientSize
        
                let x = clamp 0 (s.Width-1) p.X
                let y = clamp 0 (s.Height-1) p.Y

                PixelPosition(x, y, ctrl.ClientSize.Width, ctrl.ClientSize.Height)
            | _ ->
                PixelPosition(0,0,0,0)


    let onMouseDownHandler = MouseEventHandler(fun s e -> this.Down(%%e, %e.Button))
    let onMouseUpHandler = MouseEventHandler(fun s e -> this.Up (%%e, %e.Button))
    let onMouseMoveHandler = MouseEventHandler(fun s e -> this.Move %%e)
    let onMouseWheelHandler = MouseEventHandler(fun s e -> this.Scroll (%%e, (float e.Delta)))
    let onMouseEnter = EventHandler(fun s e -> this.Enter (mousePos()))
    let onMouseLeave = EventHandler(fun s e -> this.Leave (mousePos()))
    let onMouseClickHandler = MouseEventHandler(fun s e -> this.Click(%%e, %e.Button))
    let onMouseDoubleClickHandler = MouseEventHandler(fun s e -> this.DoubleClick(%%e, %e.Button))

    let addHandlers() =
        match ctrl with
            | Some ctrl ->
                ctrl.MouseDown.AddHandler onMouseDownHandler
                ctrl.MouseUp.AddHandler onMouseUpHandler
                ctrl.MouseMove.AddHandler onMouseMoveHandler
                ctrl.MouseWheel.AddHandler onMouseWheelHandler
                ctrl.MouseEnter.AddHandler onMouseEnter
                ctrl.MouseLeave.AddHandler onMouseLeave
                ctrl.MouseClick.AddHandler onMouseClickHandler
                ctrl.MouseDoubleClick.AddHandler onMouseDoubleClickHandler
            | _ ->()

    let removeHandlers() =
        match ctrl with
            | Some ctrl ->
                ctrl.MouseDown.RemoveHandler onMouseDownHandler
                ctrl.MouseUp.RemoveHandler onMouseUpHandler
                ctrl.MouseMove.RemoveHandler onMouseMoveHandler
                ctrl.MouseWheel.RemoveHandler onMouseWheelHandler
                ctrl.MouseEnter.RemoveHandler onMouseEnter
                ctrl.MouseLeave.RemoveHandler onMouseLeave
                ctrl.MouseClick.RemoveHandler onMouseClickHandler
                ctrl.MouseDoubleClick.RemoveHandler onMouseDoubleClickHandler
            | None -> ()

    member x.SetControl(c : Control) =
        removeHandlers()
        ctrl <- Some c
        addHandlers()
        
    member x.DragMouse pX pY =
        let s = size()
        let pp = PixelPosition(pX, pY, s.X, s.Y)
        this.Move pp

    member x.Dispose() = removeHandlers()

    interface IDisposable with
        member x.Dispose() = x.Dispose()