namespace Aardvark.Application

open Aardvark.Base
open System.Runtime.CompilerServices
open Aardvark.Base.Incremental
open System.Collections.Concurrent


type IKeyboard =
    abstract member IsDown : Keys -> IMod<bool>
    abstract member KeyDown : Keys -> IEvent<unit>
    abstract member KeyUp : Keys -> IEvent<unit>

    abstract member Down : IEvent<Keys>
    abstract member DownWithRepeats : IEvent<Keys>
    abstract member Up : IEvent<Keys>
    abstract member Press : IEvent<char>

    abstract member ClaimsKeyEvents : bool with get, set


type EventKeyboard() =
    let downKeys = CSet.empty
    let isDown = ConcurrentDictionary<Keys, ModRef<bool>>()
    let downEvents = ConcurrentDictionary<Keys, EventSource<unit>>()
    let upEvents = ConcurrentDictionary<Keys, EventSource<unit>>()
    let downEvent = EventSource<Keys>()
    let upEvent = EventSource<Keys>()
    let downWithRepeats = EventSource<Keys>()
    let input = EventSource<char>()
    let mutable claimEvents = true



    abstract member KeyDown : Keys -> unit
    abstract member KeyUp : Keys -> unit
    abstract member KeyPress : char -> unit


    default x.KeyDown(k : Keys) =
        downWithRepeats.Emit k
        if CSet.add k downKeys then
            downEvent.Emit k

            match downEvents.TryGetValue k with
                | (true, e) -> e.Emit ()
                | _ -> ()     
                
            match isDown.TryGetValue k with
                | (true, e) -> transact (fun () -> Mod.change e true)
                | _ -> ()      

    default x.KeyUp(k : Keys) =
        if CSet.remove k downKeys then
            upEvent.Emit k

            match upEvents.TryGetValue k with
                | (true, e) -> e.Emit ()
                | _ -> ()     
                
            match isDown.TryGetValue k with
                | (true, e) -> transact (fun () -> Mod.change e false)
                | _ -> ()   

    default x.KeyPress(c : char) =
        input.Emit c


    
    member x.Use(o : IKeyboard) =
        let subscriptions =
            [
                o.DownWithRepeats.Values.Subscribe(x.KeyDown)
                o.Up.Values.Subscribe(x.KeyUp)
                o.Press.Values.Subscribe(x.KeyPress)
            ]

        { new System.IDisposable with
            member x.Dispose() = subscriptions |> List.iter (fun i -> i.Dispose()) 
        }

        
    member x.ClaimsKeyEvents
        with get() = claimEvents
        and set v = claimEvents <- v

    interface IKeyboard with
        member x.IsDown (k : Keys) =
            let r = isDown.GetOrAdd(k, fun k -> Mod.init (downKeys.Contains k))
            r :> IMod<_>

        member x.KeyDown (k : Keys) =
            let e = downEvents.GetOrAdd(k, fun k -> EventSource())
            e :> IEvent<_>

        member x.KeyUp (k : Keys) =
            let e = upEvents.GetOrAdd(k, fun k -> EventSource())
            e :> IEvent<_>

        member x.Down = downEvent :> IEvent<_>
        member x.DownWithRepeats = downWithRepeats :> IEvent<_>
        member x.Up = upEvent :> IEvent<_>
        member x.Press = input :> IEvent<_>
        
        member x.ClaimsKeyEvents
            with get() = claimEvents
            and set v = claimEvents <- v

