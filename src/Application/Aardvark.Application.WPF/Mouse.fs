namespace Aardvark.Application.WPF

//open System
//open System.Windows
//open System.Windows.Controls
//open System.Windows.Input
//open Aardvark.Base
//open Aardvark.Application

//type private WPFButtons = System.Windows.Input.MouseButton
//
//type Mouse(ctrl : FrameworkElement) =
//
//    let events = EventSource<MouseEvent>()
//
//    let (~%) (m : WPFButtons) =
//        match m with
//            | WPFButtons.Left -> MouseButtons.Left
//            | WPFButtons.Right -> MouseButtons.Right
//            | WPFButtons.Middle -> MouseButtons.Middle
//            | _ -> MouseButtons.None
//
//    let pos (e : MouseEventArgs) =
//        let pos = e.GetPosition(ctrl)
//        PixelPosition(int pos.X, int pos.Y, int ctrl.ActualWidth, int ctrl.ActualHeight)
//
//    let (~%%) (m : MouseButtonEventArgs) =
//        let buttons = %m.ChangedButton
//        let location = pos m
//        { location = location; buttons = buttons }
//
//    let onMouseDownHandler = 
//        MouseButtonEventHandler(fun s e -> 
//            events.Emit (MouseDown %%e)
//            if e.ClickCount = 1 then
//                events.Emit (MouseClick %%e)
//            elif e.ClickCount = 2 then
//                events.Emit (MouseDoubleClick %%e)
//        )
//
//    let onMouseUpHandler = MouseButtonEventHandler(fun s e -> events.Emit (MouseUp %%e))
//    let onMouseMoveHandler = MouseEventHandler(fun s e -> events.Emit (MouseMove (pos e)))
//    let onMouseDoubleClickHandler = MouseButtonEventHandler(fun s e -> events.Emit (MouseDoubleClick %%e))
//    let onMouseWheelHandler = MouseWheelEventHandler(fun s e -> events.Emit (MouseScroll(float e.Delta, pos e)))
//    let onMouseEnter = MouseEventHandler(fun s e -> events.Emit (MouseEnter(pos e)))
//    let onMouseLeave = MouseEventHandler(fun s e -> events.Emit (MouseLeave(pos e)))
//
//    do ctrl.MouseDown.AddHandler onMouseDownHandler
//       ctrl.MouseUp.AddHandler onMouseUpHandler
//       ctrl.MouseMove.AddHandler onMouseMoveHandler
//       ctrl.MouseWheel.AddHandler onMouseWheelHandler
//       ctrl.MouseEnter.AddHandler onMouseEnter
//       ctrl.MouseLeave.AddHandler onMouseLeave
//
//    member x.Dispose() =
//        ctrl.MouseDown.RemoveHandler onMouseDownHandler
//        ctrl.MouseUp.RemoveHandler onMouseUpHandler
//        ctrl.MouseMove.RemoveHandler onMouseMoveHandler
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
