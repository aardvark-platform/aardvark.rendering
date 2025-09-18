namespace Aardvark.Application


open System
open Aardvark.Base
open FSharp.Data.Adaptive
open System.Runtime.CompilerServices
open System.Collections.Concurrent

type MouseButtons =
    | None   = 0x0000
    | Left   = 0x0001
    | Right  = 0x0002
    | Middle = 0x0004



type IMouse =
    abstract member Position : aval<PixelPosition>
    abstract member IsDown : MouseButtons -> aval<bool>
    abstract member TotalScroll : aval<float>
    abstract member Inside : aval<bool>

    abstract member Down : IEvent<MouseButtons>
    abstract member Up : IEvent<MouseButtons>
    abstract member Move : IEvent<PixelPosition * PixelPosition>
    abstract member Click : IEvent<MouseButtons>
    abstract member DoubleClick : IEvent<MouseButtons>
    abstract member Scroll : IEvent<float>
    abstract member Enter : IEvent<PixelPosition>
    abstract member Leave : IEvent<PixelPosition> 

type EventMouse(autoGenerateClickEvents : bool) =
    let position = AVal.init <| PixelPosition()
    let buttons = ConcurrentDictionary<MouseButtons, cval<bool>>()
    let scroll = cval 0.0
    let inside = cval false

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
            position.Value <- p

    let getDown button =
        buttons.GetOrAdd(button, fun b -> AVal.init false)

    let downPosAndTime = System.Collections.Generic.Dictionary<MouseButtons, int * DateTime * PixelPosition>()

    let DoubleClickTime = 500
    let DoubleClickSizeWidth = 4
    let DoubleClickSizeHeight = 4


    let handleDown (pos : PixelPosition) (b : MouseButtons) =
        match downPosAndTime.TryGetValue b with
            | (true, (oc, ot,op)) ->
                let dt = DateTime.Now - ot
                let dp = pos.Position - op.Position
                    
                if dt.TotalMilliseconds < float DoubleClickTime && abs dp.X <= DoubleClickSizeWidth && abs dp.Y < DoubleClickSizeHeight then
                    downPosAndTime.[b] <- (oc + 1, DateTime.Now, pos)
                else
                    downPosAndTime.[b] <- (1, DateTime.Now, pos)

            | _ ->
                downPosAndTime.[b] <- (1, DateTime.Now, pos)

        downEvent.Emit(b)

    let handleUp (pos : PixelPosition) (b : MouseButtons) =
        match downPosAndTime.TryGetValue b with
            | (true, (c,t,p)) ->
                let dt = DateTime.Now - t
                let dp = pos.Position - p.Position
                    
                if autoGenerateClickEvents then
                    if dt.TotalMilliseconds < float DoubleClickTime && abs dp.X <= DoubleClickSizeWidth && abs dp.Y < DoubleClickSizeHeight then
                        if c < 2 then clickEvent.Emit(b)
                        else doubleClickEvent.Emit(b)
                    else
                        ()
            | _ ->
                ()
        upEvent.Emit(b)

    
    abstract member Down : PixelPosition * MouseButtons -> unit
    abstract member Up : PixelPosition * MouseButtons -> unit
    abstract member Click : PixelPosition * MouseButtons -> unit
    abstract member DoubleClick : PixelPosition * MouseButtons -> unit
    abstract member Scroll : PixelPosition * float -> unit
    abstract member Enter : PixelPosition -> unit
    abstract member Leave : PixelPosition -> unit
    abstract member Move : PixelPosition -> unit



    default x.Down(pos : PixelPosition, b : MouseButtons) =
        let m = getDown b
        transact (fun () -> m.Value <- true; setPos pos)
        handleDown pos b

    default x.Up(pos : PixelPosition, b : MouseButtons) =
        let m = getDown b
        transact (fun () -> m.Value <- false; setPos pos)
        handleUp pos b


    default x.Click(pos : PixelPosition, b : MouseButtons) =
        transact (fun () -> setPos pos)
        clickEvent.Emit b

    default x.DoubleClick(pos : PixelPosition, b : MouseButtons) =
        transact (fun () -> setPos pos)
        doubleClickEvent.Emit b

    default x.Scroll(pos : PixelPosition, b : float) =
        transact (fun () -> scroll.Value <- scroll.Value + b; setPos pos)
        scrollEvent.Emit b

    default x.Enter(p : PixelPosition) =
        transact (fun () -> inside.Value <- true; setPos p)
        enterEvent.Emit p

    default x.Leave(p : PixelPosition) =
        transact (fun () -> inside.Value <- false; setPos p)
        leaveEvent.Emit p

    default x.Move(p : PixelPosition) =
        let last = position.GetValue()
        transact (fun () -> setPos p)
        moveEvent.Emit ((last, p))

    member x.IsDown button = getDown button :> aval<_>

    member x.Use(o : IMouse) =
        let pos = o.Position
        let loc() = AVal.force pos
        let subscriptions =
            [
                o.Down.Values.Subscribe(fun v -> x.Down(loc(), v))
                o.Up.Values.Subscribe(fun v -> x.Up(loc(), v))
                o.Click.Values.Subscribe(fun v -> x.Click(loc(), v))
                o.DoubleClick.Values.Subscribe(fun v -> x.DoubleClick(loc(), v))
                o.Scroll.Values.Subscribe(fun v -> x.Scroll(loc(), v))
                o.Enter.Values.Subscribe(x.Enter)
                o.Leave.Values.Subscribe(x.Leave)
                o.Move.Values.Subscribe(fun (_,n) -> x.Move(n))
            ]

        { new IDisposable with
            member x.Dispose() = subscriptions |> List.iter (fun i -> i.Dispose()) 
        }


    interface IMouse with
        member x.Position = position :> aval<_>
        member x.IsDown button = x.IsDown button
        member x.TotalScroll = scroll :> aval<_>
        member x.Inside = inside :> aval<_>
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
//    //abstract member Position : aval<PixelPosition>
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
