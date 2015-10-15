namespace Aardvark.Application

open System
open Aardvark.Base.Incremental

type AdaptiveFunc<'a>(func : 'a -> 'a) =
    inherit AdaptiveObject()
        
    static let identity = AdaptiveFunc<'a>(id)
    static member Identity = identity

    member x.Run(v : 'a) =
        x.EvaluateIfNeeded v (fun () ->
            func v
        )

module Mod =
    let withLast (f : 's -> 's -> 'a -> 'a) (state : IMod<'s>)  : AdaptiveFunc<'a> =

        let oldState = ref <| state.GetValue()
        let res = 
            AdaptiveFunc<'a>(fun value ->
                let s = state.GetValue()
                let newA = f !oldState s value
                oldState := s 
                newA
            )
        state.AddOutput res
        res

    let inline step (f : 's -> 'sd -> 'a -> 'a) (state : IMod<'s>) : AdaptiveFunc<'a> =
        withLast (fun o n a -> f o (n - o) a) state

    let withTime (f : DateTime -> DateTime -> 'a -> 'a) (state : IMod<DateTime>)  : AdaptiveFunc<'a> =

        let oldState = ref <| DateTime.Now

        let res = 
            AdaptiveFunc<'a>(fun value ->
                let s = state.GetValue()
                let newA = f !oldState s value
                oldState := s 
                newA
            )
        state.AddOutput res
        res       

    let inline stepTime (f : DateTime -> TimeSpan -> 'a -> 'a) (state : IMod<DateTime>) : AdaptiveFunc<'a> =
        withTime (fun o n a -> f o (n - o) a) state

    let rec private int (initial : IMod<'a>) (controllers : list<IMod<AdaptiveFunc<'a>>>): IMod<'a> =
        match controllers with
            | c::cs ->
                
                
                let result = 
                    c |> Mod.bind (fun f ->
                        [initial :> IAdaptiveObject; f :> IAdaptiveObject] |> Mod.mapCustom (fun () -> f.Run (initial.GetValue()))
                    )

                int result cs


            | [] -> initial

    let integrate (initial : 'a) (time : IMod<DateTime>) (controllers : list<IMod<AdaptiveFunc<'a>>>) =
        let currentValue = ref initial
        let current = Mod.custom (fun () -> !currentValue)
        let isSubscribed = ref false
        //time.AddOutput current
        //time |> Mod.registerCallback (fun _ -> current.MarkOutdated()) |> ignore
        
        let result = int current controllers
        //time.AddOutput current

        result |> Mod.map (fun v ->
            if !currentValue <> v then
                currentValue := v
                if not !isSubscribed then
                    isSubscribed := true
                    time.AddVolatileMarkingCallback (fun () ->
                        isSubscribed := false
                        transact (fun () -> current.MarkOutdated())
                    ) |> ignore
                    time.GetValue() |> ignore

            v
        )
