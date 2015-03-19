namespace Aardvark.Application.WinForms

open System
open System.Windows.Forms
open Aardvark.Application
open Aardvark.Base

[<AutoOpen>]
module private WinFormsUtilities =
    let (~%) (k : System.Windows.Forms.Keys) : Keys =
        k |> int |> unbox

type Keyboard(ctrl : Control) =
    
    let events = EventSource<KeyboardEvent>()


    let onKeyDown (s : obj) (e : PreviewKeyDownEventArgs) =
        events.Emit (KeyDown %e.KeyCode)

    let onKeyUp (s : obj) (e : KeyEventArgs) =
        events.Emit (KeyUp %e.KeyCode)

    let onKeyPress (s : obj) (e : KeyPressEventArgs) =
        events.Emit (KeyPress e.KeyChar)


    let onKeyDownHandler = PreviewKeyDownEventHandler(onKeyDown)
    let onKeyUpHandler = KeyEventHandler(onKeyUp)
    let onKeyPressHandler = KeyPressEventHandler(onKeyPress)

    do ctrl.PreviewKeyDown.AddHandler onKeyDownHandler
       ctrl.KeyUp.AddHandler onKeyUpHandler
       ctrl.KeyPress.AddHandler onKeyPressHandler

    member x.Dispose() =
        ctrl.PreviewKeyDown.RemoveHandler onKeyDownHandler
        ctrl.KeyUp.RemoveHandler onKeyUpHandler
        ctrl.KeyPress.RemoveHandler onKeyPressHandler

    member x.KeyDown = events |> Event.choose (function KeyDown p -> Some p | _ -> None)
    member x.KeyUp = events |> Event.choose (function KeyUp p -> Some p | _ -> None)
    member x.KeyPress = events |> Event.choose (function KeyPress p -> Some p | _ -> None)

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface IKeyboard with
        member x.Events = events :> IEvent<_>
