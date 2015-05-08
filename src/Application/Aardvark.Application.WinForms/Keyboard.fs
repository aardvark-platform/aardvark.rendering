namespace Aardvark.Application.WinForms

open System
open System.Windows.Forms
open Aardvark.Application
open Aardvark.Base

type Keyboard() as this =
    inherit EventKeyboard()

    let mutable ctrl : Option<Control> = None

    let (~%) (k : System.Windows.Forms.Keys) : Keys =
        KeyConverter.keyFromVirtualKey(int k)


    let onKeyDown (s : obj) (e : KeyEventArgs) =
        this.KeyDown (%e.KeyCode)

    let onKeyUp (s : obj) (e : KeyEventArgs) =
        this.KeyUp (%e.KeyCode)

    let onKeyPress (s : obj) (e : KeyPressEventArgs) =
        this.KeyPress e.KeyChar


    let onKeyDownHandler = KeyEventHandler(onKeyDown)
    let onKeyUpHandler = KeyEventHandler(onKeyUp)
    let onKeyPressHandler = KeyPressEventHandler(onKeyPress)

    let addHandlers() =
        match ctrl with
            | Some ctrl ->
               ctrl.KeyDown.AddHandler onKeyDownHandler
               ctrl.KeyUp.AddHandler onKeyUpHandler
               ctrl.KeyPress.AddHandler onKeyPressHandler
            | _ -> ()

    let removeHandlers() =
        match ctrl with
            | Some ctrl ->
                ctrl.KeyDown.RemoveHandler onKeyDownHandler
                ctrl.KeyUp.RemoveHandler onKeyUpHandler
                ctrl.KeyPress.RemoveHandler onKeyPressHandler
            | _ -> ()

    member x.SetControl c =
        removeHandlers()
        ctrl <- Some c
        addHandlers()

    member x.Dispose() = 
        removeHandlers()
        ctrl <- None

    interface IDisposable with
        member x.Dispose() = x.Dispose()
