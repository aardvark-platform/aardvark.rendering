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

    let down = System.Collections.Generic.HashSet<Keys>()

    let onKeyDown (s : obj) (e : KeyEventArgs) =
        let k = %e.KeyCode
        if down.Add k then
            events.Emit (KeyDown %e.KeyCode)

    let onKeyUp (s : obj) (e : KeyEventArgs) =
        let k = %e.KeyCode
        if down.Remove k then
            events.Emit (KeyUp k)

    let onKeyPress (s : obj) (e : KeyPressEventArgs) =
        events.Emit (KeyPress e.KeyChar)


    let onKeyDownHandler = KeyEventHandler(onKeyDown)
    let onKeyUpHandler = KeyEventHandler(onKeyUp)
    let onKeyPressHandler = KeyPressEventHandler(onKeyPress)

    do ctrl.KeyDown.AddHandler onKeyDownHandler
       ctrl.KeyUp.AddHandler onKeyUpHandler
       ctrl.KeyPress.AddHandler onKeyPressHandler

    member x.Dispose() =
        ctrl.KeyDown.RemoveHandler onKeyDownHandler
        ctrl.KeyUp.RemoveHandler onKeyUpHandler
        ctrl.KeyPress.RemoveHandler onKeyPressHandler

    member x.KeyDown = events |> Event.choose (function KeyDown p -> Some p | _ -> None)
    member x.KeyUp = events |> Event.choose (function KeyUp p -> Some p | _ -> None)
    member x.KeyPress = events |> Event.choose (function KeyPress p -> Some p | _ -> None)

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface IKeyboard with
        member x.Events = events :> IEvent<_>
