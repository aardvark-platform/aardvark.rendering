namespace Aardvark.Application


open System
open Aardvark.Base
open Aardvark.Base.Incremental
open System.Reactive
open System.Reactive.Linq
open System.Runtime.CompilerServices
open System.Collections.Concurrent

type MouseButtons =
    | None   = 0x0000
    | Left   = 0x0001
    | Right  = 0x0002
    | Middle = 0x0004



type IMouse =
    abstract member Position : IMod<PixelPosition>
    abstract member IsDown : MouseButtons -> IMod<bool>
    abstract member TotalScroll : IMod<float>
    abstract member Inside : IMod<bool>

    abstract member Down : IEvent<MouseButtons>
    abstract member Up : IEvent<MouseButtons>
    abstract member Move : IEvent<PixelPosition * PixelPosition>
    abstract member Click : IEvent<MouseButtons>
    abstract member DoubleClick : IEvent<MouseButtons>
    abstract member Scroll : IEvent<float>
    abstract member Enter : IEvent<PixelPosition>
    abstract member Leave : IEvent<PixelPosition> 

type EventMouse() =
    let position = Mod.initMod <| PixelPosition()
    let buttons = ConcurrentDictionary<MouseButtons, ModRef<bool>>()
    let scroll = Mod.initMod 0.0
    let inside = Mod.initMod false

    let downEvent = EventSource<MouseButtons>()
    let upEvent = EventSource<MouseButtons>()
    let clickEvent = EventSource<MouseButtons>()
    let doubleClickEvent = EventSource<MouseButtons>()
    let scrollEvent = EventSource<float>()
    let enterEvent = EventSource<PixelPosition>()
    let leaveEvent = EventSource<PixelPosition>()
    let moveEvent = EventSource<PixelPosition * PixelPosition>()

    let setPos (p : PixelPosition) =
        if p <> position.GetValue() then
            Mod.change position p

    let getDown button =
        buttons.GetOrAdd(button, fun b -> Mod.initMod false)

    member x.Down(pos : PixelPosition, b : MouseButtons) =
        let m = getDown b
        transact (fun () -> Mod.change m true; setPos pos)
        downEvent.Emit b

    member x.Up(pos : PixelPosition, b : MouseButtons) =
        let m = getDown b
        transact (fun () -> Mod.change m false; setPos pos)
        upEvent.Emit b

    member x.Click(pos : PixelPosition, b : MouseButtons) =
        transact (fun () -> setPos pos)
        clickEvent.Emit b

    member x.DoubleClick(pos : PixelPosition, b : MouseButtons) =
        transact (fun () -> setPos pos)
        doubleClickEvent.Emit b

    member x.Scroll(pos : PixelPosition, b : float) =
        transact (fun () -> Mod.change scroll (scroll.GetValue() + b); setPos pos)
        scrollEvent.Emit b

    member x.Enter(p : PixelPosition) =
        transact (fun () -> Mod.change inside true; setPos p)
        enterEvent.Emit p

    member x.Leave(p : PixelPosition) =
        transact (fun () -> Mod.change inside false; setPos p)
        leaveEvent.Emit p

    member x.Move(p : PixelPosition) =
        let last = position.GetValue()
        transact (fun () -> setPos p)
        moveEvent.Emit ((last, p))

    interface IMouse with
        member x.Position = position :> IMod<_>
        member x.IsDown button = getDown button :> IMod<_>
        member x.TotalScroll = scroll :> IMod<_>
        member x.Inside = inside :> IMod<_>
        
        member x.Down = downEvent :> IEvent<_>
        member x.Up = upEvent :> IEvent<_>
        member x.Move = moveEvent :> IEvent<_>
        member x.Click = clickEvent :> IEvent<_>
        member x.DoubleClick = doubleClickEvent :> IEvent<_>
        member x.Scroll = scrollEvent :> IEvent<_>
        member x.Enter = enterEvent :> IEvent<_>
        member x.Leave = leaveEvent :> IEvent<_>


//
//type MouseEventProperties = { buttons : MouseButtons; location : PixelPosition }
//
//type MouseEvent =
//    | MouseDown of MouseEventProperties
//    | MouseUp of MouseEventProperties
//    | MouseClick of MouseEventProperties
//    | MouseDoubleClick of MouseEventProperties
//    | MouseMove of PixelPosition
//    | MouseScroll of float * PixelPosition
//    | MouseEnter of PixelPosition
//    | MouseLeave of PixelPosition
//
//type IMouse =
//    //abstract member Position : IMod<PixelPosition>
//    abstract member Events : IEvent<MouseEvent>
//
//[<AutoOpen>]
//module ``F# Mouse Extensions`` =
//    open System.Runtime.CompilerServices
//    open System.Collections.Generic
//    
//    let private table = ConditionalWeakTable<IMouse, SymbolDict<IEvent>>()
//
//    let private get (id : Symbol) (f : IEvent<MouseEvent> -> IEvent<'a>) (m : IMouse)=
//        let tab =
//            match table.TryGetValue m with
//                | (true, v) -> v
//                | _ -> 
//                    let res = SymbolDict<IEvent>()
//                    table.Add(m,res)
//                    res
//        tab.GetOrCreate(id, fun s -> (f m.Events) :> IEvent) |> unbox<IEvent<'a>>
//
//    let private down = Sym.ofString "down"
//    let private up = Sym.ofString "up"
//    let private move = Sym.ofString "move"
//    let private scroll = Sym.ofString "scroll"
//    let private click = Sym.ofString "click"
//    let private doubleClick = Sym.ofString "doubleClick"
//    let private enter = Sym.ofString "enter"
//    let private leave = Sym.ofString "leave"
//
//    type IMouse with
//        member x.Down = x |> get down (fun e -> e |> Event.choose (function | MouseDown e -> Some e | _ -> None))
//        member x.Up = x |> get up (fun e -> e |> Event.choose (function | MouseUp e -> Some e | _ -> None))
//        member x.Move = x |> get move (fun e -> e |> Event.choose (function | MouseMove e -> Some e | _ -> None))
//        member x.Scroll = x |> get scroll (fun e -> e |> Event.choose (function | MouseScroll(delta, p) -> Some(delta, p) | _ -> None))
//        member x.Click = x |> get click (fun e -> e |> Event.choose (function | MouseClick e -> Some e | _ -> None))
//        member x.DoubleClick = x |> get doubleClick (fun e -> e |> Event.choose (function | MouseDoubleClick e -> Some e | _ -> None))
//        member x.Enter = x |> get enter (fun e -> e |> Event.choose (function | MouseEnter e -> Some e | _ -> None))
//        member x.Leave = x |> get leave (fun e -> e |> Event.choose (function | MouseLeave e -> Some e | _ -> None))
//
//        member x.IsDown(button : MouseButtons) =
//            x |> get (down.ToString() + button.ToString() |> Sym.ofString) (fun e ->
//                e |> Event.choose (fun e ->
//                    match e with
//                        | MouseDown k when k.buttons = button -> Some true
//                        | MouseUp k when k.buttons = button -> Some false
//                        | _ -> None
//                )
//            )
//
//[<AbstractClass; Sealed; Extension>]
//type CSharpMouseExtensions private() =
//    
//    [<Extension>]
//    static member Down(x : IMouse) = x.Down
//
//    [<Extension>]
//    static member Down(x : IMouse, button : MouseButtons) = 
//        x.Down |> Event.filter(fun p -> p.buttons = button)
//
//
//    [<Extension>]
//    static member Up(x : IMouse) = x.Up
//
//    [<Extension>]
//    static member Up(x : IMouse, button : MouseButtons) = 
//        x.Up |> Event.filter(fun p -> p.buttons = button)
//
//
//    [<Extension>]
//    static member Move(x : IMouse) = x.Move
//
//    [<Extension>]
//    static member Scroll(x : IMouse) = x.Scroll
//
//    [<Extension>]
//    static member Click(x : IMouse) = x.Click
//    static member DoubleClick(x : IMouse) = x.DoubleClick
//    static member Enter(x : IMouse) = x.Enter
//    static member Leave(x : IMouse) = x.Leave
