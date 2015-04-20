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

    let inline private integrate (time : IMod<DateTime>) (input : IMod<Option<'b>>) (m : IMod<'a>) (f : TimeSpan -> 'b -> 'a -> 'a) =
        let dt = Cache(Ag.getContext(), fun i -> differentiate time)
        let integral = ref Unchecked.defaultof<_>

        adaptive {
            let! v = m
            integral := v
            let! i = input

            match i with
                | Some i ->
                    let! (t, dt) = dt.Invoke 0
                    match dt with
                        | Some dt ->
                            let newValue = f dt i !integral
                            integral := newValue
                            return newValue
                        | None ->
                            return !integral

                | None ->
                    dt.Clear(ignore)
                    return !integral 
        }

    let controlWSAD (k : IKeyboard) (t : IMod<DateTime>) (view : IMod<CameraView>) =   
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

        let move = 
            Mod.map2 (fun x y -> 
                let r = x + y
                if r <> V2i.Zero then Some r
                else None
            ) moveX moveY

        let last = ref <| Unchecked.defaultof<_>

        integrate t move view (fun dt m v ->
            let direction = float m.X * v.Right + float m.Y * v.Forward
            let delta = 1.2 * dt.TotalSeconds * direction

            v.WithLocation(v.Location + delta)
        )

    let controlLookAround (m : IMouse) (view : IMod<CameraView>) =
        
        let down = Mod.initMod false
        let lastMousePos = ref Unchecked.defaultof<_>
        m.Down.Values.Subscribe(fun e -> if e.buttons = MouseButtons.Left then transact (fun () -> Mod.change down true); lastMousePos := e.location) |> ignore
        m.Up.Values.Subscribe(fun e -> if e.buttons = MouseButtons.Left then transact (fun () -> Mod.change down false)) |> ignore

        let drag = Mod.map2 (fun d m -> if d then Some m else None) down m.Move.Mod

        
        let integral = ref <| Unchecked.defaultof<_>
        adaptive {
            let! v = view
            integral := v

            let! d = drag
            match d with
                | Some d ->
                    let delta = d.Position - lastMousePos.Value.Position
                    lastMousePos := d

                    let trafo =
                        M44d.Rotation(integral.Value.Right, float delta.Y * -0.01) *
                        M44d.Rotation(integral.Value.Sky, float delta.X * -0.01)

                    let newForward = trafo.TransformDir integral.Value.Forward |> Vec.normalize
                    integral := integral.Value.WithForward(newForward)

                    return !integral
                | None ->
                    return !integral


        }


//        adaptive {
//            let! v = view
//            last := v
//
//            let! m = move
//            if m <> V2i.Zero then
//                let! (t,dt) = differentiate t
//                
//                match dt with
//                    | Some dt ->
//                        let delta = 0.1 * dt.TotalSeconds * (v.Right * (float m.X) + v.Forward * (float m.Y)) 
//                        let newV = last.Value.WithLocation(v.Location + delta)
//                        last := newV
//
//                        return newV
//                    | None ->
//                        return v
//            else
//                return v
//        }

