namespace Aardvark.Application.WinForms

open System
open System.Windows.Forms
open Aardvark.Application
open Aardvark.Base

type private WKeys = System.Windows.Forms.Keys

type Keyboard() as this =
    inherit EventKeyboard()

    let mutable ctrl : Option<Control> = None

    let (~%) (k : System.Windows.Forms.Keys) : Keys =
        KeyConverter.keyFromVirtualKey(int k)


    let onKeyDown (s : obj) (e : KeyEventArgs) =
        this.KeyDown (%e.KeyCode)
        e.Handled <- true

    let onPreviewKeyDown (s : obj) (e : PreviewKeyDownEventArgs) =
        match e.KeyCode with
            | WKeys.Left | WKeys.Right | WKeys.Up | WKeys.Down ->
                this.KeyDown (%e.KeyCode)
            | _ -> ()

        if (this :> IKeyboard).ClaimsKeyEvents then
            e.IsInputKey <- true

    let onKeyUp (s : obj) (e : KeyEventArgs) =
        this.KeyUp (%e.KeyCode)
        e.Handled <- true

    let onKeyPress (s : obj) (e : KeyPressEventArgs) =
        this.KeyPress e.KeyChar
        e.Handled <- true


    let onKeyDownHandler = KeyEventHandler(onKeyDown)
    let onPreviewKeyDownHandler = PreviewKeyDownEventHandler(onPreviewKeyDown)
    let onKeyUpHandler = KeyEventHandler(onKeyUp)
    let onKeyPressHandler = KeyPressEventHandler(onKeyPress)

    let addHandlers() =
        match ctrl with
            | Some ctrl -> 
               ctrl.PreviewKeyDown.AddHandler onPreviewKeyDownHandler
               ctrl.KeyDown.AddHandler onKeyDownHandler
               ctrl.KeyUp.AddHandler onKeyUpHandler
               ctrl.KeyPress.AddHandler onKeyPressHandler
            | _ -> ()

    let removeHandlers() =
        match ctrl with
            | Some ctrl ->
                ctrl.PreviewKeyDown.RemoveHandler onPreviewKeyDownHandler
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
