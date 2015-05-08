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
    abstract member Up : IEvent<Keys>
    abstract member Press : IEvent<char>


type EventKeyboard() =
    let downKeys = CSet.empty
    let isDown = ConcurrentDictionary<Keys, ModRef<bool>>()
    let downEvents = ConcurrentDictionary<Keys, EventSource<unit>>()
    let upEvents = ConcurrentDictionary<Keys, EventSource<unit>>()
    let downEvent = EventSource<Keys>()
    let upEvent = EventSource<Keys>()
    let input = EventSource<char>()


    member x.KeyDown(k : Keys) =
        if CSet.add k downKeys then
            downEvent.Emit k

            match downEvents.TryGetValue k with
                | (true, e) -> e.Emit ()
                | _ -> ()     
                
            match isDown.TryGetValue k with
                | (true, e) -> transact (fun () -> Mod.change e true)
                | _ -> ()      

    member x.KeyUp(k : Keys) =
        if CSet.remove k downKeys then
            upEvent.Emit k

            match upEvents.TryGetValue k with
                | (true, e) -> e.Emit ()
                | _ -> ()     
                
            match isDown.TryGetValue k with
                | (true, e) -> transact (fun () -> Mod.change e false)
                | _ -> ()   

    member x.KeyPress(c : char) =
        input.Emit c


    interface IKeyboard with
        member x.IsDown (k : Keys) =
            let r = isDown.GetOrAdd(k, fun k -> Mod.initMod (downKeys.Contains k))
            r :> IMod<_>

        member x.KeyDown (k : Keys) =
            let e = downEvents.GetOrAdd(k, fun k -> EventSource())
            e :> IEvent<_>

        member x.KeyUp (k : Keys) =
            let e = upEvents.GetOrAdd(k, fun k -> EventSource())
            e :> IEvent<_>

        member x.Down = downEvent :> IEvent<_>
        member x.Up = upEvent :> IEvent<_>
        member x.Press = input :> IEvent<_>

