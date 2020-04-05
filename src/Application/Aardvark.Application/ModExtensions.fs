namespace Aardvark.Application

open System
open FSharp.Data.Adaptive

type AdaptiveFunc<'a>(func : AdaptiveToken -> 'a -> 'a) =
    inherit AdaptiveObject()
        
    static let identity = AdaptiveFunc<'a>(fun _ v -> v)
    static member Identity = identity

    member x.Run(caller : AdaptiveToken, v : 'a) =
        x.EvaluateAlways caller (fun caller ->
            if x.OutOfDate then
                func caller v
            else 
                v
        )

[<CompiledName("ApplicationAValModule")>]
module AVal =
    let withLast (f : 's -> 's -> 'a -> 'a) (state : aval<'s>)  : AdaptiveFunc<'a> =

        let oldState = ref Unchecked.defaultof<_> 
        let res =
            AdaptiveFunc<'a>(fun res value ->
                let s = state.GetValue res
                let newA = f !oldState s value
                oldState := s 
                newA
            )
        oldState := state.GetValue()
        lock state (fun () ->
            state.Outputs.Add res |> ignore
        )

        res

    let inline step (f : 's -> 'sd -> 'a -> 'a) (state : aval<'s>) : AdaptiveFunc<'a> =
        withLast (fun o n a -> f o (n - o) a) state

    let withTime (f : DateTime -> DateTime -> 'a -> 'a) (state : aval<DateTime>)  : AdaptiveFunc<'a> =

        let oldState = ref <| DateTime.Now

        let res =
            AdaptiveFunc<'a>(fun res value ->
                let s = state.GetValue res
                let newA = f !oldState s value
                oldState := s 
                newA
            )
        lock state (fun () ->
            state.Outputs.Add res |> ignore
        )

        res       

    let inline stepTime (f : DateTime -> TimeSpan -> 'a -> 'a) (state : aval<DateTime>) : AdaptiveFunc<'a> =
        withTime (fun o n a -> f o (n - o) a) state

    let rec private int (initial : aval<'a>) (controllers : list<aval<AdaptiveFunc<'a>>>): aval<'a> =
        match controllers with
            | c::cs ->
                let result = 
                    c |> AVal.bind (fun f ->
                        AVal.custom (fun t ->
                            f.Run(t,(initial.GetValue t))
                        )
                    )

                int result cs


            | [] -> initial

    let integrate (initial : 'a) (time : aval<DateTime>) (controllers : list<aval<AdaptiveFunc<'a>>>) =
        let currentValue = ref initial
        let current = AVal.custom (fun _ -> !currentValue)
        //let isSubscribed = ref false
        //time.AddOutput current
        //time |> AVal.registerCallback (fun _ -> current.MarkOutdated()) |> ignore
        
        let result = int current controllers
        //time.AddOutput current


        AVal.custom (fun token ->
            let v = result.GetValue token
            if !currentValue <> v then

                //time.Outputs.Add current |> ignore
                //time.GetValue token |> ignore
                currentValue := v


                AdaptiveObject.RunAfterEvaluate(fun () ->
                    current.MarkOutdated()
                )
            v
        )

//        result |> AVal.map (fun v ->
//            if !currentValue <> v then
//                currentValue := v
//                if not !isSubscribed then
//                    isSubscribed := true
//                    time.AddVolatileMarkingCallback (fun () ->
//                        isSubscribed := false
//                        transact (fun () -> current.MarkOutdated())
//                    ) |> ignore
//                    time.GetValue(AdaptiveTOken()) |> ignore
//
//            v
//        )
