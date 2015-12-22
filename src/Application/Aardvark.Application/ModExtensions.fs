namespace Aardvark.Application

open System
open Aardvark.Base.Incremental

type AdaptiveFunc<'a>(func : 'a -> 'a) =
    inherit AdaptiveObject()
        
    static let identity = AdaptiveFunc<'a>(id)
    static member Identity = identity

    member x.Run(caller : IAdaptiveObject, v : 'a) =
        x.EvaluateIfNeeded caller v (fun () ->
            func v
        )

[<CompiledName("ApplicationModModule")>]
module Mod =
    let withLast (f : 's -> 's -> 'a -> 'a) (state : IMod<'s>)  : AdaptiveFunc<'a> =

        let res = ref Unchecked.defaultof<_>
        let oldState = ref Unchecked.defaultof<_> 
        res :=
            AdaptiveFunc<'a>(fun value ->
                let s = state.GetValue !res
                let newA = f !oldState s value
                oldState := s 
                newA
            )
        oldState := state.GetValue(null)
        lock state (fun () ->
            state.Outputs.Add !res |> ignore
            state.AddOutput !res
        )

        !res

    let inline step (f : 's -> 'sd -> 'a -> 'a) (state : IMod<'s>) : AdaptiveFunc<'a> =
        withLast (fun o n a -> f o (n - o) a) state

    let withTime (f : DateTime -> DateTime -> 'a -> 'a) (state : IMod<DateTime>)  : AdaptiveFunc<'a> =

        let oldState = ref <| DateTime.Now

        let res = ref Unchecked.defaultof<_>
        res :=
            AdaptiveFunc<'a>(fun value ->
                let s = state.GetValue !res
                let newA = f !oldState s value
                oldState := s 
                newA
            )
        lock state (fun () ->
            state.Outputs.Add !res |> ignore
            state.AddOutput !res
        )

        !res       

    let inline stepTime (f : DateTime -> TimeSpan -> 'a -> 'a) (state : IMod<DateTime>) : AdaptiveFunc<'a> =
        withTime (fun o n a -> f o (n - o) a) state

    let rec private int (initial : IMod<'a>) (controllers : list<IMod<AdaptiveFunc<'a>>>): IMod<'a> =
        match controllers with
            | c::cs ->
                
                
                let result = 
                    c |> Mod.bind (fun f ->
                        [initial :> IAdaptiveObject; f :> IAdaptiveObject] |> Mod.mapCustom (fun s -> 
                            f.Run(s,(initial.GetValue s))
                        )
                    )

                int result cs


            | [] -> initial

    let integrate (initial : 'a) (time : IMod<DateTime>) (controllers : list<IMod<AdaptiveFunc<'a>>>) =
        let currentValue = ref initial
        let current = Mod.custom (fun _ -> !currentValue)
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
