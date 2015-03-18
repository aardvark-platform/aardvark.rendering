namespace Aardvark.Application.WPF

open System
open System.Windows
open System.Windows.Input
open System.Windows.Controls
open Aardvark.Application
open Aardvark.Base

[<AutoOpen>]
module private WPFUtilites =
    let (~%) (k : System.Windows.Input.Key) : Keys =
        KeyInterop.VirtualKeyFromKey(k) |> unbox<Keys>

type Keyboard(ctrl : FrameworkElement) =
    
    let events = EventSource<KeyboardEvent>()


    let onKeyDown (s : obj) (e : KeyEventArgs) =
        events.Emit (KeyDown %e.Key)

    let onKeyUp (s : obj) (e : KeyEventArgs) =
        events.Emit (KeyUp %e.Key)

    let onKeyPress (s : obj) (e : TextCompositionEventArgs) =
        for c in e.Text do
            events.Emit (KeyPress c)


    let onKeyDownHandler = KeyEventHandler(onKeyDown)
    let onKeyUpHandler = KeyEventHandler(onKeyUp)
    let onKeyPressHandler = TextCompositionEventHandler(onKeyPress)

    do ctrl.PreviewKeyDown.AddHandler onKeyDownHandler
       ctrl.KeyUp.AddHandler onKeyUpHandler
       ctrl.TextInput.AddHandler onKeyPressHandler

    member x.Dispose() =
        ctrl.PreviewKeyDown.RemoveHandler onKeyDownHandler
        ctrl.KeyUp.RemoveHandler onKeyUpHandler
        ctrl.TextInput.RemoveHandler onKeyPressHandler

    member x.KeyDown = events |> Event.choose (function KeyDown p -> Some p | _ -> None)
    member x.KeyUp = events |> Event.choose (function KeyUp p -> Some p | _ -> None)
    member x.KeyPress = events |> Event.choose (function KeyPress p -> Some p | _ -> None)

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface IKeyboard with
        member x.Events = events :> IEvent<_>
