namespace Aardvark.Application


open System
open Aardvark.Base
open System.Reactive
open System.Reactive.Linq
open System.Runtime.CompilerServices

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
    | MouseMove of PixelPosition
    | MouseScroll of float * PixelPosition
    | MouseEnter of PixelPosition
    | MouseLeave of PixelPosition


type IMouse =
    abstract member Events : IEvent<MouseEvent>

[<AutoOpen>]
module ``F# Mouse Extensions`` =
    open System.Runtime.CompilerServices
    open System.Collections.Generic
    
    let private table = ConditionalWeakTable<IMouse, SymbolDict<IEvent>>()

    let private get (id : Symbol) (f : IEvent<MouseEvent> -> IEvent<'a>) (m : IMouse)=
        let tab = table.GetOrCreateValue(m)
        tab.GetOrCreate(id, fun s -> f m.Events :> IEvent) |> unbox<IEvent<'a>>

    let private down = Sym.ofString "down"
    let private up = Sym.ofString "up"
    let private move = Sym.ofString "move"
    let private scroll = Sym.ofString "scroll"
    let private click = Sym.ofString "click"
    let private doubleClick = Sym.ofString "doubleClick"
    let private enter = Sym.ofString "enter"
    let private leave = Sym.ofString "leave"

    type IMouse with
        member x.Down = x |> get down (fun e -> e |> Event.choose (function | MouseDown e -> Some e | _ -> None))
        member x.Up = x |> get down (fun e -> e |> Event.choose (function | MouseUp e -> Some e | _ -> None))
        member x.Move = x |> get down (fun e -> e |> Event.choose (function | MouseMove e -> Some e | _ -> None))
        member x.Scroll = x |> get down (fun e -> e |> Event.choose (function | MouseScroll(delta, p) -> Some(delta, p) | _ -> None))
        member x.Click = x |> get down (fun e -> e |> Event.choose (function | MouseClick e -> Some e | _ -> None))
        member x.DoubleClick = x |> get down (fun e -> e |> Event.choose (function | MouseDoubleClick e -> Some e | _ -> None))
        member x.Enter = x |> get down (fun e -> e |> Event.choose (function | MouseEnter e -> Some e | _ -> None))
        member x.Leave = x |> get down (fun e -> e |> Event.choose (function | MouseLeave e -> Some e | _ -> None))

[<AbstractClass; Sealed; Extension>]
type CSharpMouseExtensions private() =
    
    [<Extension>]
    static member Down(x : IMouse) = x.Down

    [<Extension>]
    static member Up(x : IMouse) = x.Up

    [<Extension>]
    static member Move(x : IMouse) = x.Move

    [<Extension>]
    static member Scroll(x : IMouse) = x.Scroll

    [<Extension>]
    static member Click(x : IMouse) = x.Click
    static member DoubleClick(x : IMouse) = x.DoubleClick
    static member Enter(x : IMouse) = x.Enter
    static member Leave(x : IMouse) = x.Leave
