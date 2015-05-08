namespace Aardvark.Application

open Aardvark.Base
open System.Runtime.CompilerServices
open Aardvark.Base.Incremental

type KeyboardEvent =
    | KeyDown of Keys
    | KeyUp of Keys
    | KeyPress of char

type IKeyboard =
    abstract member Events : IEvent<KeyboardEvent>


type IKeyboard2 =
    abstract member IsDown : Keys -> IMod<bool>
    abstract member Down : Keys -> IEvent<unit>
    abstract member Up : Keys -> IEvent<unit>

    abstract member KeyDown : IEvent<Keys>
    abstract member KeyUp : IEvent<Keys>
    abstract member Input : IEvent<char>

[<AutoOpen>]
module ``F# Keyboard Extensions`` =
    open System.Runtime.CompilerServices
    open System.Collections.Generic
    open System.Collections.Concurrent
    
    let private table = ConditionalWeakTable<IKeyboard, ConcurrentDictionary<Symbol, IEvent>>()

    let private get (id : Symbol) (f : IEvent<KeyboardEvent> -> IEvent<'a>) (m : IKeyboard)=
        let tab = table.GetOrCreateValue(m)
        tab.GetOrAdd(id, fun s -> f m.Events :> IEvent) |> unbox<IEvent<'a>>

    let private down = Sym.ofString "down"
    let private up = Sym.ofString "up"
    let private press = Sym.ofString "press"

    let private isDown = Sym.ofString "isDown"


    type IKeyboard with
        member x.Down = x |> get down (fun e -> e |> Event.choose (function | KeyDown e -> Some e | _ -> None))
        member x.Up = x |> get down (fun e -> e |> Event.choose (function | KeyUp e -> Some e | _ -> None))
        member x.Press = x |> get down (fun e -> e |> Event.choose (function | KeyPress e -> Some e | _ -> None))

        member x.IsDown(key : Keys) =
            let isDown = ref false
            x |> get (Sym.ofString (down.ToString() + key.ToString())) (fun e -> e |> Event.chooseLatest (fun () -> !isDown) (fun e ->
                match e with
                    | KeyDown k when k = key ->
                        if not !isDown then
                            isDown := true
                            Some true
                        else
                            None
                    | KeyUp k when k = key ->
                        if !isDown then
                            isDown := false
                            Some false
                        else
                            None
                    | _ -> None
            ))

[<AbstractClass; Sealed; Extension>]
type CSharpKeyboardExtensions private() =
    
    [<Extension>]
    static member Down(x : IKeyboard) = x.Down

    [<Extension>]
    static member Up(x : IKeyboard) = x.Up

    [<Extension>]
    static member Press(x : IKeyboard) = x.Press


    [<Extension>]
    static member IsDown(x : IKeyboard, key : Keys) = x.IsDown(key)
