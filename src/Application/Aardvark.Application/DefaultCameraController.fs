namespace Aardvark.Application

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental

module DefaultCameraController =

    let controlWSAD (k : IKeyboard) (time : IMod<DateTime>) =   
        let w = k.IsDown Keys.W 
        let s = k.IsDown Keys.S
        let a = k.IsDown Keys.A 
        let d = k.IsDown Keys.D 

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
        }

    let controlLookAround (m : IMouse) =
        let down = m.IsDown(MouseButtons.Left)
        let location = m.Position |> Mod.map (fun pp -> pp.Position)

        adaptive {
            let! d = down
 
            if d then
                return location |> Mod.step (fun op delta (cam : CameraView) ->
                    let trafo =
                        M44d.Rotation(cam.Right, float delta.Y * -0.005 ) *
                        M44d.Rotation(cam.Sky, float delta.X * -0.005   )

                    let newForward = trafo.TransformDir cam.Forward |> Vec.normalize
                    cam.WithForward(newForward)

                ) 
            else
                return AdaptiveFunc.Identity
        }


    let controlOrbitAround (m : IMouse) (center : IMod<V3d>) =
        let down = m.IsDown(MouseButtons.Left)
        let location = m.Position |> Mod.map (fun pp -> pp.Position)

        adaptive {
            let! d = down
            let! center' = center
            if d then
                return location |> Mod.step (fun op delta (cam : CameraView) ->
                    let trafo =
                        M44d.Rotation(cam.Right, float delta.Y * -0.01) *
                        M44d.Rotation(cam.Sky, float delta.X * -0.01)

                    let newLocation = trafo.TransformDir cam.Location
                    let tempcam = cam.WithLocation newLocation
                    let newForward = center' - newLocation |> Vec.normalize

                    tempcam.WithForward newForward
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
        let active = Mod.init false

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
                        Mod.changeAfterEvaluation active false

                    cam.WithLocation(cam.Location + direction)

                )
            else
                return AdaptiveFunc.Identity
        }  

    let control (mouse : IMouse) (keyboard : IKeyboard) (time : IMod<DateTime>) (cam : CameraView) : IMod<CameraView> =
        Mod.integrate cam time [
            controlWSAD keyboard time
            controlLookAround mouse
            controlPan mouse
            controlZoom mouse
            controllScroll mouse time
        ]


    let controlWSADwithSpeed (speed : ModRef<float>) (k : IKeyboard) (time : IMod<DateTime>) =   
        let w = k.IsDown Keys.W 
        let s = k.IsDown Keys.S
        let a = k.IsDown Keys.A 
        let d = k.IsDown Keys.D 

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
                    let delta = speed.Value * dt.TotalSeconds * direction

                    cam.WithLocation(cam.Location + delta)
                )
            else
                return AdaptiveFunc.Identity
        } |> Mod.onPush

    let controlPanWithSpeed (speed : ModRef<float>) (m : IMouse) =
        let down = m.IsDown(MouseButtons.Middle)
        let location = m.Position |> Mod.map (fun pp -> pp.Position)

        adaptive {
            let! d = down

            if d then
                return location |> Mod.step (fun p delta (cam : CameraView) ->

                    let step = 0.006 * speed.Value * (cam.Down * float delta.Y + cam.Right * float delta.X)

                    cam.WithLocation(cam.Location + step)

                )
            else
                return AdaptiveFunc.Identity
        }

    let controlReset (initial : CameraView) (k : IKeyboard) =
        adaptive {
            let! t = k.IsDown Keys.F9
            if t then
                return Mod.constant 0 |> Mod.step (fun _ _ _ -> initial )
            else
                return AdaptiveFunc.Identity
        }
        
    let controllScrollWithSpeed (moveSpeed : ModRef<float>) (m : IMouse) (time : IMod<DateTime>) =
        let active = Mod.init false

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
                    let direction = (0.012 * moveSpeed.Value) * (cam.Forward * df)

                    if abs v < 0.5 then
                        Mod.changeAfterEvaluation active false

                    cam.WithLocation(cam.Location + direction)

                )
            else
                return AdaptiveFunc.Identity
        }
        
    let controlZoomWithSpeed (speed : ModRef<float>) (m : IMouse) =
        let down = m.IsDown(MouseButtons.Right)
        let location = m.Position |> Mod.map (fun pp -> pp.Position)

        adaptive {
            let! d = down

            if d then
                return location |> Mod.step (fun p delta (cam : CameraView) ->

                    let step = -0.006 * speed.Value * (cam.Forward * float delta.Y)
                    cam.WithLocation(cam.Location + step)

                )
            else
                return AdaptiveFunc.Identity
        }      

    let controlWithSpeed (speed : ModRef<float>) (mouse : IMouse) (keyboard : IKeyboard) (time : IMod<DateTime>) (cam : CameraView) : IMod<CameraView> =
         Mod.integrate cam time [
            controlWSADwithSpeed speed keyboard time
            controlLookAround mouse
            controlPanWithSpeed speed mouse
            controlZoomWithSpeed speed mouse
            controllScrollWithSpeed speed mouse time
        ]

    let controlExt (initialSpeed : float ) (mouse : IMouse) (keyboard : IKeyboard) (time : IMod<DateTime>) (cam : CameraView) : IMod<CameraView> =
        let speed = Mod.init initialSpeed

        keyboard.DownWithRepeats.Values.Add ( fun k -> 
            match k with
            | Keys.PageUp -> transact ( fun _ -> speed.Value <- speed.Value * 1.3 )
            | Keys.PageDown -> transact ( fun _ -> speed.Value <- speed.Value / 1.3 )
            | _ -> ()
        )

        Mod.integrate cam time [
           controlWSADwithSpeed speed keyboard time
           controlLookAround mouse
           controlReset cam keyboard
           controlPanWithSpeed speed mouse
           controlZoomWithSpeed speed mouse
           controllScrollWithSpeed speed mouse time
       ]