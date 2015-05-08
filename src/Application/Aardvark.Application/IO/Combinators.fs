namespace Aardvark.Application

open System
open Aardvark.Base
open Aardvark.Base.Incremental


module Keyboard =

    type private NoKeyboard private() =
        static let keyEvent = EventSource<KeyboardEvent>()
        static let instance = NoKeyboard() :> IKeyboard

        static member Instance = instance

        interface IKeyboard with
            member x.Events = keyEvent :> IEvent<_>

    type private StreamKeyboard(e : IEvent<KeyboardEvent>) =
    
        interface IKeyboard with
            member x.Events = e

    
    let empty = NoKeyboard.Instance

    let ofEvent (e : IEvent<KeyboardEvent>) = StreamKeyboard e :> IKeyboard

    let choose (f : KeyboardEvent -> Option<KeyboardEvent>) (k : IKeyboard) =
        k.Events |> Event.choose f |> ofEvent

    let filter (f : KeyboardEvent -> bool) (k : IKeyboard) =
        k.Events |> Event.filter f |> ofEvent

    let map (f : KeyboardEvent -> KeyboardEvent) (k : IKeyboard) =
        k.Events |> Event.map f |> ofEvent

    let collect (f : KeyboardEvent -> #seq<KeyboardEvent>) (k : IKeyboard) =
        k.Events |> Event.collect f |> ofEvent

    let union (keyboards : seq<IKeyboard>) =
        keyboards |> Seq.map (fun k -> k.Events) |> Event.concat |> ofEvent

//module Mouse =
//    
//    type private NoMouse private() =
//        static let events = EventSource<MouseEvent>()
//        static let instance = NoMouse() :> IMouse
//
//        static member Instance = instance
//
//        interface IMouse with
//            member x.Events = events :> IEvent<_>
//
//    type private StreamMouse(e : IEvent<MouseEvent>) =
//    
//        interface IMouse with
//            member x.Events = e
//
//    let empty = NoMouse.Instance
//
//    let ofEvent (e : IEvent<MouseEvent>) = StreamMouse e :> IMouse
//
//    let choose (f : MouseEvent -> Option<MouseEvent>) (k : IMouse) =
//        k.Events |> Event.choose f |> ofEvent
//
//    let filter (f : MouseEvent -> bool) (k : IMouse) =
//        k.Events |> Event.filter f |> ofEvent
//
//    let map (f : MouseEvent -> MouseEvent) (k : IMouse) =
//        k.Events |> Event.map f |> ofEvent
//
//    let collect (f : MouseEvent -> #seq<MouseEvent>) (k : IMouse) =
//        k.Events |> Event.collect f |> ofEvent
//
//    let union (keyboards : seq<IMouse>) =
//        keyboards |> Seq.map (fun k -> k.Events) |> Event.concat |> ofEvent


type ChangeableKeyboard(initial : IKeyboard) =
    let events = EventSource<KeyboardEvent>()

    let subscribe (k : IKeyboard) =
        k.Events.Values.Subscribe events.Emit
    
    let mutable inner = initial
    let mutable subscription = subscribe initial

    let set k =
        subscription.Dispose()
        inner <- k
        subscription <- subscribe inner

    member x.Inner 
        with get() = inner
        and set k = set k

    member x.Dispose() =
        set Keyboard.empty

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface IKeyboard with
        member x.Events = events :> IEvent<_>

    new() = new ChangeableKeyboard(Keyboard.empty)

//type ChangeableMouse(initial : IMouse) =
//    let events = EventSource<MouseEvent>()
//
//    let subscribe (k : IMouse) =
//        k.Events.Values.Subscribe events.Emit
//    
//    let mutable inner = initial
//    let mutable subscription = subscribe initial
//
//
//    let set k =
//        subscription.Dispose()
//        inner <- k
//        subscription <- subscribe inner
//
//    member x.Inner 
//        with get() = inner
//        and set m = set m
//
//    member x.Dispose() =
//        set Mouse.empty
//
//    interface IDisposable with
//        member x.Dispose() = x.Dispose()
//
//    interface IMouse with
//        member x.Events = events :> IEvent<_>
//
//    new() = new ChangeableMouse(Mouse.empty)
