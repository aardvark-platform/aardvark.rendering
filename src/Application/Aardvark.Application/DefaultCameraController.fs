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
   
    type ModTransformer<'a>(func : 'a -> 'a) =
        inherit AdaptiveObject()
        
        static let identity = ModTransformer<'a>(id)
        static member Identity = identity

        member x.Run(v : 'a) =
            x.EvaluateIfNeeded v (fun () ->
                func v
            )


        
    // = { func : 'a -> 'b; dependencies : IAdaptiveObject }

    module Mod =
        let step (f : 's -> 's -> 'a -> 'a) (state : IMod<'s>)  : ModTransformer<'a> =

            let oldState = ref <| state.GetValue()
            printfn "new step with: %A" oldState

            let res = 
                ModTransformer(fun value ->
                    let s = state.GetValue()
                    let newA = f !oldState s value
                    oldState := s 
                    newA
                )
            state.AddOutput res
            res



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
                let f =
                    Mod.step (fun (ot : DateTime) (nt : DateTime) (cam : CameraView)  ->
                        let dt = nt - ot
                        let direction = float m.X * cam.Right + float m.Y * cam.Forward
                        let delta = 1.2 * dt.TotalSeconds * direction

                        cam.WithLocation(cam.Location + delta)
                    ) time

                return f
            else
                return ModTransformer.Identity
        } 

    let controlLookAround (m : IMouse) =
        let down = m.IsDown(MouseButtons.Left).Mod
        //let location = m.Move.ImmediateMod |> Mod.map (fun pp -> pp.Position)
        //let location = Mod.initMod V2i.Zero
        //let s = m.Move.Values.Subscribe(fun pp -> transact (fun () -> Mod.change location pp.Position))

        let location = m.Move.ImmediateMod |> Mod.map (fun pp -> pp.Position)

        adaptive {
            let! down = down

            if down then
                let f = 
                    Mod.step (fun (op : V2i) (np : V2i) (cam : CameraView) ->
                        let delta = np - op
                        printfn "delta: %A" delta
                        let trafo =
                            M44d.Rotation(cam.Right, float delta.Y * -0.01) *
                            M44d.Rotation(cam.Sky, float delta.X * -0.01)

                        let newForward = trafo.TransformDir cam.Forward |> Vec.normalize
                        cam.WithForward(newForward)

                    ) location

                return f
            else
                return ModTransformer.Identity
        }

    let controlPan (m : IMouse) =
        let down = m.IsDown(MouseButtons.Middle).Mod
        let location = m.Move.Mod |> Mod.map (fun pp -> pp.Position)

        adaptive {
            let! down = down

            if down then
                let f = 
                    Mod.step (fun (op : V2i) (np : V2i) (cam : CameraView) ->
                        let delta = np - op

                        let direction = 0.1 * (cam.Up * float delta.Y + cam.Right * float delta.X)

                        cam.WithLocation(cam.Location + direction)

                    ) location

                return f
            else
                return ModTransformer.Identity
        }  

//
//    let controlLookAround (m : IMouse) =
//        
//        let down = Mod.initMod false
//        let location = Mod.initMod V2i.Zero
//        let lastMousePos = ref Unchecked.defaultof<_>
//        let delta = ref V2i.Zero
//
//        m.Down.Values.Subscribe(fun e -> if e.buttons = MouseButtons.Left then transact (fun () -> Mod.change down true); lastMousePos := e.location.Position) |> ignore
//        m.Up.Values.Subscribe(fun e -> if e.buttons = MouseButtons.Left then transact (fun () -> Mod.change down false)) |> ignore
//        m.Move.Values.Subscribe(fun p -> 
//            if down.GetValue() then
//                let a = p.Position - !lastMousePos
//                delta := !delta + a
//
//            lastMousePos := p.Position
//        ) |> ignore
//
//        
//
//
//        adaptive {
//
//            let! isDown = down
//            if isDown then
//                return Some (fun _ (dt : TimeSpan) (cam : CameraView) ->
//                    let dM = !delta
//                    delta := V2i.Zero
//                    if dM = V2i.Zero then
//                        cam
//                    else
//                        let trafo =
//                            M44d.Rotation(cam.Right, float dM.Y * -0.01) *
//                            M44d.Rotation(cam.Sky, float dM.X * -0.01)
//
//                        let newForward = trafo.TransformDir cam.Forward |> Vec.normalize
//                        cam.WithForward(newForward)
//                )
//               
//            else
//                return None
//
//
//        } |> TimedController


    let rec step (initial : IMod<'a>) (controllers : list<IMod<ModTransformer<'a>>>): IMod<'a> =
        match controllers with
            | c::cs ->
                
                
                let result = 
                    c |> Mod.bind (fun f ->
                        [initial :> IAdaptiveObject; f :> IAdaptiveObject] |> Mod.mapCustom (fun () -> f.Run (initial.GetValue()))
                    )

                step result cs


            | [] -> initial


    let integrate (initial : 'a) (time : IMod<DateTime>) (controllers : list<IMod<ModTransformer<'a>>>) =
        let currentValue = ref initial
        let current = Mod.custom (fun () -> !currentValue)
        //time.AddOutput current
        //time |> Mod.registerCallback (fun _ -> current.MarkOutdated()) |> ignore
        
        let result = step current controllers
        let changer = result |> Mod.map ignore
        //time.AddOutput current

        result |> Mod.map (fun v ->
            if !currentValue <> v then
                currentValue := v
                time.MarkingCallbacks.Add (fun () ->
                    transact (fun () -> current.MarkOutdated())
                ) |> ignore
                time.GetValue() |> ignore

            v
        )