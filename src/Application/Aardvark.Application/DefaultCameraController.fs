namespace Aardvark.Application

open System
open Aardvark.Base
open Aardvark.Base.Incremental

module DefaultCameraController =
    
    let inline private differentiate (m : IMod< ^a>) =
        let last = ref None
        m |> Mod.map (fun v ->
            match !last with
                | Some l ->
                    last := Some v
                    (v, Some(v - l))
                | None ->
                    last := Some v
                    (v, None)
        )
//
//    let inline private integrate (time : IMod<DateTime>) (input : IMod<Option<'b>>) (m : IMod<'a>) (f : TimeSpan -> 'b -> 'a -> 'a) =
//        let dt = Cache(Ag.getContext(), fun i -> differentiate time)
//        let integral = ref Unchecked.defaultof<_>
//
//        adaptive {
//            let! v = m
//            integral := v
//            let! i = input
//
//            match i with
//                | Some i ->
//                    let! (t, dt) = dt.Invoke 0
//                    match dt with
//                        | Some dt ->
//                            let newValue = f dt i !integral
//                            integral := newValue
//                            return newValue
//                        | None ->
//                            return !integral
//
//                | None ->
//                    dt.Clear(ignore)
//                    return !integral 
//        }
   
    type AdaptiveFunc<'a>(func : 'a -> 'a) =
        inherit AdaptiveObject()
        
        static let identity = AdaptiveFunc<'a>(id)
        static member Identity = identity

        member x.Run(v : 'a) =
            x.EvaluateIfNeeded v (fun () ->
                func v
            )

    open Microsoft.FSharp.Reflection
    type ModFunction<'f>(f : 'f) =
        inherit AdaptiveObject()
        member x.Run : 'f = x.EvaluateAlways (fun () -> f)

    module ModFunction =

        type Last = Last

        type Builder() =
            member x.Bind(m : Last, f : 'a -> 'a) =
                Mod.initConstant f
            
            member x.Bind(m : IMod<'a>, f : 'a -> IMod<'b>) =
                adaptive.Bind(m, f)
            

            member x.Return(v) = v
            member x.ReturnFrom(v) = Mod.initConstant v
            
            member x.Zero() = Mod.initConstant id

        let build = Builder()

        let withLast (m : IMod<'a>) =
            let r = m |> Mod.force |> ref
            m |> Mod.map (fun v ->
                let l = !r
                r := v
                (l, v)
            )

        let test (down : IMod<bool>) (pos : IMod<V2i>) =
            build {
                let! d = down

                if d then
                    let! (op, np) = withLast pos
                    let dp = np - op

                    return! (fun (a : CameraView) ->
                        //let! (a : CameraView) = Last
                        let dx = Trafo3d.Rotation(a.Right, 0.1 * float dp.Y) *
                                 Trafo3d.Rotation(a.Up, 0.1 * float dp.X)

                        a.WithForward(dx.Forward.TransformDir a.Forward)
                    )
                
            }

        let id<'a> = ModFunction<'a -> 'a>(id)

        let arr f = ModFunction f

        let first (f : ModFunction<'a -> 'b>) : ModFunction<'a * 'c -> 'b * 'c> =
            let res = ModFunction(fun (v,c) -> (f.Run v, c))
            f.AddOutput res

            res

        let second (f : ModFunction<'a -> 'b>) : ModFunction<'c * 'a -> 'c * 'b> =
            let res = ModFunction(fun (c,v) -> (c, f.Run v))
            f.AddOutput res

            res

        let ( *** ) (f : ModFunction<'a -> 'b>) (g : ModFunction<'a -> 'c>) =
            let res = ModFunction(fun v -> (f.Run v, g.Run v))
            f.AddOutput res
            g.AddOutput res
            res

        let ( &&& ) (f : ModFunction<'a -> 'b>) (g : ModFunction<'c -> 'd>) =
            let res = ModFunction(fun fa ga -> (f.Run fa, g.Run ga))
            f.AddOutput res
            g.AddOutput res
            res

        let ( <<< ) (g : ModFunction<'b -> 'c>) (f : ModFunction<'a -> 'b>) =
            let res = ModFunction(fun v -> v |> f.Run |> g.Run)
            f.AddOutput res
            g.AddOutput res
            res

        let ( >>> ) f g =
            g <<< f

        let rec fix<'a, 'b> (f : ('a -> 'b) -> 'a -> 'b) : 'a -> 'b =
            let l = lazy (fix f)
            f (fun v -> l.Value v)

        let rec fix2 (f : ('x * 'a -> 'y * 'b) -> ('x * 'a -> 'y * 'b)) : ('x * 'a -> 'y * 'b) =
            let l = lazy (fix2 f)
            f (fun v -> l.Value v)

        let fac n = fix (fun self n -> if n < 1 then 1 else n * self (n-1)) n

        // fix f = let x = f x in x
        // loop f b = let (c,d) = f (b,d) in c

        let loop (m : ModFunction<'b * 'd -> 'c * Lazy<'d>>) : ModFunction<'b -> 'c> =
            let sepp () : Lazy<'d> =
                failwith ""

            let rec f (a : 'b, d : 'd) : 'c * Lazy<'d> =
                let (c,d) = m.Run (a, d)
                failwith ""
            failwith ""
//            ModFunction(fun b ->
//                let (c, d) = m.Run (b, sepp())
//                c
//            )

        let map (g : 'b -> 'c) (m : ModFunction<'a -> 'b>) =
            ModFunction(fun v -> v |> m.Run |> g)

        let join (j : ModFunction<'a * 'b -> 'c>) (f : ModFunction<_ -> 'a>) (g : ModFunction<_ -> 'b>) =
            ModFunction(fun v -> j.Run (f.Run v, g.Run v))

        let run (m : ModFunction<'a -> 'b>) (arg : 'a) =
            [m :> IAdaptiveObject] |> Mod.mapCustom (fun () -> m.Run arg)

        let bind (m : ModFunction<'a -> 'b>) (arg : IMod<'a>) =
            [m :> IAdaptiveObject; arg :> _] |> Mod.mapCustom (fun () -> arg.GetValue() |> m.Run)


//    type ModFunction<'a, 'b>(f : 'a -> 'b) =
//        inherit AdaptiveObject()
//        member x.Run(v : 'a) =
//            x.EvaluateAlways (fun () -> f v)
//        
//    module ModFunction =
//        let identity<'a> =  ModFunction<'a, 'a>(id)
//
//        let compose (g : ModFunction<'b, 'c>) (f : ModFunction<'a, 'b>) =
//            let res = ModFunction(fun v -> v |> f.Run |> g.Run)
//            f.AddOutput res
//            g.AddOutput res
//            res
//
//        let sequence (l : list<ModFunction<'a, 'a>>) =
//            l |> List.fold (fun f g -> compose g f) identity 
//
//        let bind (f : ModFunction<'a, 'b>) (input : IMod<'a>) =
//            [f :> IAdaptiveObject; input :> IAdaptiveObject] |> Mod.mapCustom (fun () -> input.GetValue() |> f.Run)
//    
//        let bind' (f : ModFunction<'a, 'b>) (input : 'a) =
//            [f :> IAdaptiveObject] |> Mod.mapCustom (fun () -> input |> f.Run)
//    
//        let map (f : 'b -> 'c) (m : ModFunction<'a, 'b>) : ModFunction<'a, 'c> =
//            let res = ModFunction(fun v -> v |> m.Run |> f)
//            m.AddOutput res
//            res
//
//        let asdsadsa (f : 'b -> ModFunction<'c, 'd>) (m : ModFunction<'a, 'b>) : ModFunction<'a * 'c, 'd> =
//            let res = ref Unchecked.defaultof<_>
//            let oldInner = ref None
//            let fRes = ref None
//
//            let compute (a,c) =
//                let inner = m.Run a
//
//                let changed = 
//                    match !oldInner with
//                        | Some o when System.Object.Equals(o, inner) -> false
//                        | _ ->
//                            oldInner := Some inner
//                            true
//
//                if changed then
//                    match !fRes with
//                        | Some r -> r.RemoveOutput
//                    let t = f inner
//
//                    let d = t.Run c
//                    d
//                else
//                    let t = f inner
//
//                    let d = t.Run c
//                    d
//
//            res := ModFunction(compute)
//            m.AddOutput !res
//            !res

    // = { func : 'a -> 'b; dependencies : IAdaptiveObject }




    module Mod =
        let withLast (f : 's -> 's -> 'a -> 'a) (state : IMod<'s>)  : AdaptiveFunc<'a> =

            let oldState = ref <| state.Compute()
            //transact (fun () -> state.MarkOutdated())
            //printfn "new step with: %A" oldState

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
            //transact (fun () -> state.MarkOutdated())
            //printfn "new step with: %A" oldState

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
                        time.MarkingCallbacks.Add (fun () ->
                            isSubscribed := false
                            transact (fun () -> current.MarkOutdated())
                        ) |> ignore
                        time.GetValue() |> ignore

                v
            )

    let controlWSAD (k : IKeyboard) (time : IMod<DateTime>) =   
        let w = k.IsDown Keys.W |> Mod.fromEvent
        let s = k.IsDown Keys.S |> Mod.fromEvent
        let a = k.IsDown Keys.A |> Mod.fromEvent
        let d = k.IsDown Keys.D |> Mod.fromEvent

        let moveX = 
            Mod.map2 (fun l r ->
                if l && not r then -V2i.IO
                elif r && not l then V2i.IO
                else V2i.Zero
            ) a d

        let moveY = 
            Mod.map2 (fun f b ->
                if f && not b then V2i.OI
                elif b && not f then -V2i.OI
                else V2i.Zero
            ) w s

        let move = Mod.map2 (+) moveX moveY

        adaptive {
            let! m = move
            if m <> V2i.Zero then
                return time |> Mod.stepTime (fun t dt (cam : CameraView)  ->
                    //printfn "%Ams" dt.TotalMilliseconds
                    let direction = float m.X * cam.Right + float m.Y * cam.Forward
                    let delta = 1.2 * dt.TotalSeconds * direction

                    cam.WithLocation(cam.Location + delta)
                )
            else
                return AdaptiveFunc.Identity
        } |> Mod.always

    let controlLookAround (m : IMouse) =
        let down = m.IsDown(MouseButtons.Left)
        let location = m.Position |> Mod.map (fun pp -> pp.Position)

        adaptive {
            let! d = down
 
            if d then
                return location |> Mod.step (fun op delta (cam : CameraView) ->
                    printfn "%A" delta
                    let trafo =
                        M44d.Rotation(cam.Right, float delta.Y * -0.01) *
                        M44d.Rotation(cam.Sky, float delta.X * -0.01)

                    let newForward = trafo.TransformDir cam.Forward |> Vec.normalize
                    cam.WithForward(newForward)

                ) 
            else
                return AdaptiveFunc.Identity
        }

    let controlPan (m : IMouse) =
        let down = m.IsDown(MouseButtons.Middle)
        let location = m.Position |> Mod.map (fun pp -> pp.Position)

        adaptive {
            let! d = down

            if d then
                return location |> Mod.step (fun p delta (cam : CameraView) ->

                    let step = 0.05 * (cam.Down * float delta.Y + cam.Right * float delta.X)

                    cam.WithLocation(cam.Location + step)

                )
            else
                return AdaptiveFunc.Identity
        }  

    let controlZoom (m : IMouse) =
        let down = m.IsDown(MouseButtons.Right)
        let location = m.Position |> Mod.map (fun pp -> pp.Position)

        adaptive {
            let! d = down

            if d then
                return location |> Mod.step (fun p delta (cam : CameraView) ->

                    let step = -0.05 * (cam.Forward * float delta.Y)
                    cam.WithLocation(cam.Location + step)

                )
            else
                return AdaptiveFunc.Identity
        }  

    let controllScroll (m : IMouse) (time : IMod<DateTime>) =
        let active = Mod.initMod false

        let speed = ref 0.0
        let s = m.Scroll.Values.Subscribe(fun d ->
            speed := !speed + d
            if not <| active.GetValue() then
                transact (fun () -> Mod.change active true)
        )

        adaptive {
            let! a = active 
            if a then
                return time |> Mod.step (fun t dt (cam : CameraView) ->
                    let v = !speed * pow 0.004 dt.TotalSeconds
                    speed := v

                    let df = v * dt.TotalSeconds
                    let direction = 0.10 * (cam.Forward * df)

                    if abs v < 0.5 then
                        transact (fun () -> Mod.change active false)

                    cam.WithLocation(cam.Location + direction)

                )
            else
                return AdaptiveFunc.Identity
        }  
