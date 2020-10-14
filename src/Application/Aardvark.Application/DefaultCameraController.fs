namespace Aardvark.Application

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open FSharp.Data.Adaptive

module DefaultCameraController =
    /// <summary>Move the camera forward, back, left, or right.
    /// WASD controls the functionality.</summary>
    /// <param name="keyboard"> the keyboard inputs (this is often pulled from the parent window)</param>
    /// <param name="time"> incremental input representing the current time. (this is often pulled from the parent window)  </param>
    /// <returns>An incremental function that takes a CameraView</returns>
    let controlWSAD (keyboard : IKeyboard) (time : aval<DateTime>) =   

        //Each keypress is represented by an incremental variable, or IAdaptiveValue.
        let w = keyboard.IsDown Keys.W 
        let s = keyboard.IsDown Keys.S
        let a = keyboard.IsDown Keys.A 
        let d = keyboard.IsDown Keys.D 

        // left and rignt are mapped to a single axis 
        let moveX = 
            AVal.map2 (fun l r ->
                if l && not r then -V2i.IO
                elif r && not l then V2i.IO
                else V2i.Zero
            ) a d
        // same for up and down
        let moveY = 
            AVal.map2 (fun f b ->
                if f && not b then V2i.OI
                elif b && not f then -V2i.OI
                else V2i.Zero
            ) w s

        //we combine the separate axes into one variable, a (normalized) movement vector
        let move = AVal.map2 (+) moveX moveY

        //an adaptive computation expression is created to make working in the incremental 'realm' easier
        adaptive {
            
            //let! unwraps an IAdaptiveValue so we can work with the underlying value.
            let! m = move

            //If a key has been pressed
            if m <> V2i.Zero then
                // map the current time to a delta time, then apply it to a function
                return time 
                |> AVal.stepTime (fun t dt (cam : CameraView)  ->
                    
                    //apply our movement vector relative to the direction the camera is facing
                    let direction = float m.X * cam.Right + float m.Y * cam.Forward
                    
                    // 1.2 is the default speed
                    let delta = 1.2 * dt.TotalSeconds * direction

                    //update camera location
                    cam.WithLocation(cam.Location + delta)
                )
            // else do nothing
            else
                return AdaptiveFunc.Identity
        }

    /// <summary> Gives a camera look controls. 
    /// Left mouse button controls the functionality </summary>
    /// <param name="mouse"> the mouse inputs (this is often pulled from the parent window)</param>
    /// <returns>An incremental function that takes a CameraView</returns>
    let controlLookAround (mouse : IMouse) =
        let down = mouse.IsDown(MouseButtons.Left)
        let location = mouse.Position |> AVal.map (fun pp -> pp.Position)

        adaptive {
            let! d = down
 
            if d then
                return location |> AVal.step (fun op delta (cam : CameraView) ->
                    let trafo =
                        M44d.Rotation(cam.Right, float delta.Y * -0.005 ) *
                        M44d.Rotation(cam.Sky, float delta.X * -0.005   )

                    let newForward = trafo.TransformDir cam.Forward |> Vec.normalize
                    cam.WithForward(newForward)

                ) 
            else
                return AdaptiveFunc.Identity
        }

    /// <summary> Gives a camera look controls, orbiting around a point. 
    /// Left mouse button controls the functionality.</summary>
    /// <param name="mouse"> the mouse inputs (this is often pulled from the parent window)</param>
    /// <returns>An incremental function that takes a CameraView</returns>
    let controlOrbitAround (mouse : IMouse) (center : aval<V3d>) =
        let down = mouse.IsDown(MouseButtons.Left)
        let location = mouse.Position |> AVal.map (fun pp -> pp.Position)

        adaptive {
            let! d = down
            let! center' = center
            if d then
                return location |> AVal.step (fun op delta (cam : CameraView) ->
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

    /// <summary> Gives a camera an option to move around laterally from
    /// the direction of the camera.
    /// A middle mouse button controls the functionality.</summary>
    /// <param name="mouse"> the mouse inputs (this is often pulled from the parent window)</param>
    /// <returns>An incremental function that takes a CameraView</returns>    
    let controlPan (mouse : IMouse) =
        let down = mouse.IsDown(MouseButtons.Middle)
        let location = mouse.Position |> AVal.map (fun pp -> pp.Position)

        adaptive {
            let! d = down

            if d then
                return location |> AVal.step (fun p delta (cam : CameraView) ->

                    let step = 0.05 * (cam.Down * float delta.Y + cam.Right * float delta.X)

                    cam.WithLocation(cam.Location + step)

                )
            else
                return AdaptiveFunc.Identity
        }  

    /// <summary> Gives a camera an option to move towards or away from the facing direction.
    /// right mouse button with mouse y axis movement controls the functionality.</summary>
    /// <param name="mouse"> the mouse inputs (this is often pulled from the parent window)</param>
    /// <returns>An incremental function that takes a CameraView</returns> 
    let controlZoom (mouse : IMouse) =
        let down = mouse.IsDown(MouseButtons.Right)
        let location = mouse.Position |> AVal.map (fun pp -> pp.Position)

        adaptive {
            let! d = down

            if d then
                return location |> AVal.step (fun p delta (cam : CameraView) ->

                    let step = -0.05 * (cam.Forward * float delta.Y)
                    cam.WithLocation(cam.Location + step)

                )
            else
                return AdaptiveFunc.Identity
        }  

    /// <summary> Gives a camera an option to move towards or away from the facing direction.
    /// middle mouse wheel controls the functionality.</summary>
    /// <param name="mouse"> the mouse inputs (this is often pulled from the parent window)</param>
    /// <param name="time"> the time to use as reference (this is often pulled from the parent window)</param>
    /// <returns>An incremental function that takes a CameraView</returns> 
    let controllScroll (mouse : IMouse) (time : aval<DateTime>) =
        let active = AVal.init false

        let speed = ref 0.0
        let s = mouse.Scroll.Values.Subscribe(fun d ->
            speed := !speed + d
            if not <| active.GetValue() then
                transact (fun () -> active.Value <- true)
        )

        adaptive {
            let! a = active 
            if a then
                return time |> AVal.step (fun t dt (cam : CameraView) ->
                    let v = !speed * pow 0.004 dt.TotalSeconds
                    speed := v

                    let df = v * dt.TotalSeconds
                    let direction = 0.10 * (cam.Forward * df)

                    if abs v < 0.5 then
                        AdaptiveObject.RunAfterEvaluate(fun () ->
                            active.Value <- false
                        )

                    cam.WithLocation(cam.Location + direction)

                )
            else
                return AdaptiveFunc.Identity
        } 
        
    /// <summary> Implement common control functions for movement, looking, panning, and zooming 
    /// for a given camera.</summary>
    /// <param name="mouse"> the mouse inputs (this is often pulled from the parent window)</param>
    /// <param name="keyboard"> the keyboard inputs (this is often pulled from the parent window)</param>
    /// <param name="time"> the relative time (this is often pulled from the parent window)</param>
    /// <param name="camera"> the initial state of the camera</param>
    /// <returns>An incremental function that takes a CameraView</returns> 
    let control (mouse : IMouse) (keyboard : IKeyboard) (time : aval<DateTime>) (camera : CameraView) : aval<CameraView> =
        AVal.integrate camera time [
            controlWSAD keyboard time
            controlLookAround mouse
            controlPan mouse
            controlZoom mouse
            controllScroll mouse time
        ]

    /// <summary>Move the camera forward, back, left, or right.
    /// WASD controls the functionality.</summary>
    /// <param name="speed"> the camera movement speed</param>
    /// <param name="keyboard"> the keyboard inputs (this is often pulled from the parent window)</param>
    /// <param name="time"> incremental input representing the current time. (this is often pulled from the parent window)  </param>
    /// <returns>An incremental function that takes a CameraView</returns>
    let controlWSADwithSpeed (speed : cval<float>) (keyboard : IKeyboard) (time : aval<DateTime>) =   
        let w = keyboard.IsDown Keys.W 
        let s = keyboard.IsDown Keys.S
        let a = keyboard.IsDown Keys.A 
        let d = keyboard.IsDown Keys.D 

        let moveX = 
            AVal.map2 (fun l r ->
                if l && not r then -V2i.IO
                elif r && not l then V2i.IO
                else V2i.Zero
            ) a d

        let moveY = 
            AVal.map2 (fun f b ->
                if f && not b then V2i.OI
                elif b && not f then -V2i.OI
                else V2i.Zero
            ) w s

        let move = AVal.map2 (+) moveX moveY

        adaptive {
            let! m = move
            if m <> V2i.Zero then
                return time |> AVal.stepTime (fun t dt (cam : CameraView)  ->
                    //printfn "%Ams" dt.TotalMilliseconds
                    let direction = float m.X * cam.Right + float m.Y * cam.Forward
                    let delta = speed.Value * dt.TotalSeconds * direction

                    cam.WithLocation(cam.Location + delta)
                )
            else
                return AdaptiveFunc.Identity
        } // |> AVal.onPush

    /// <summary> Gives a camera an option to move around laterally from
    /// the direction of the camera.
    /// A middle mouse button controls the functionality.</summary>
    /// <param name="speed"> the rate at which the panning movement occurs</param>
    /// <param name="mouse"> the mouse inputs (this is often pulled from the parent window)</param>
    /// <returns>An incremental function that takes a CameraView</returns>    
    let controlPanWithSpeed (speed : cval<float>) (mouse : IMouse) =
        let down = mouse.IsDown(MouseButtons.Middle)
        let location = mouse.Position |> AVal.map (fun pp -> pp.Position)

        adaptive {
            let! d = down

            if d then
                return location |> AVal.step (fun p delta (cam : CameraView) ->

                    let step = 0.006 * speed.Value * (cam.Down * float delta.Y + cam.Right * float delta.X)

                    cam.WithLocation(cam.Location + step)

                )
            else
                return AdaptiveFunc.Identity
        }

    /// <summary> Gives a camera the ability to reset to it's initial state.
    /// F9 is the default button for resetting.</summary>
    /// <param name="keyboard"> the keyboard inputs (this is often pulled from the parent window)</param>
    /// <returns>An incremental function that takes a CameraView</returns>    
    let controlReset (initial : CameraView) (keyboard : IKeyboard) =
        adaptive {
            let! t = keyboard.IsDown Keys.F9
            if t then
                return AVal.constant 0 |> AVal.step (fun _ _ _ -> initial )
            else
                return AdaptiveFunc.Identity
        }
    
    /// <summary> Gives a camera an option to move towards or away from the facing direction.
    /// middle mouse wheel controls the functionality.</summary>
    /// <param name="moveSpeed"> the rate at which the zooming occurs</param>
    /// <param name="mouse"> the mouse inputs (this is often pulled from the parent window)</param>
    /// <param name="time"> the time to use as reference (this is often pulled from the parent window)</param>
    /// <returns>An incremental function that takes a CameraView</returns> 
    let controllScrollWithSpeed (moveSpeed : cval<float>) (mouse : IMouse) (time : aval<DateTime>) =
        let active = AVal.init false

        let speed = ref 0.0
        let s = mouse.Scroll.Values.Subscribe(fun d ->
            speed := !speed + d
            if not <| active.GetValue() then
                transact (fun () -> active.Value <- true)
        )

        adaptive {
            let! a = active 
            if a then
                return time |> AVal.step (fun t dt (cam : CameraView) ->
                    let v = !speed * pow 0.004 dt.TotalSeconds
                    speed := v

                    let df = v * dt.TotalSeconds
                    let direction = (0.012 * moveSpeed.Value) * (cam.Forward * df)

                    if abs v < 0.5 then
                        AdaptiveObject.RunAfterEvaluate (fun () ->
                            active.Value <- false
                        )

                    cam.WithLocation(cam.Location + direction)

                )
            else
                return AdaptiveFunc.Identity
        }

    /// <summary> Gives a camera an option to move towards or away from the facing direction.
    /// right mouse button with mouse y axis movement controls the functionality.</summary>
    /// <param name="moveSpeed"> the rate at which the zooming occurs</param>
    /// <param name="mouse"> the mouse inputs (this is often pulled from the parent window)</param>
    /// <returns>An incremental function that takes a CameraView</returns> 
    let controlZoomWithSpeed (moveSpeed : cval<float>) (mouse : IMouse) =
        let down = mouse.IsDown(MouseButtons.Right)
        let location = mouse.Position |> AVal.map (fun pp -> pp.Position)

        adaptive {
            let! d = down

            if d then
                return location |> AVal.step (fun p delta (cam : CameraView) ->

                    let step = -0.006 * moveSpeed.Value * (cam.Forward * float delta.Y)
                    cam.WithLocation(cam.Location + step)

                )
            else
                return AdaptiveFunc.Identity
        } 
        
    /// <summary> Implement common control functions for movement, looking, panning, and zooming 
    /// for a given camera.</summary>
    /// <param name="moveSpeed"> the rate at which all movement occurs</param>
    /// <param name="mouse"> the mouse inputs (this is often pulled from the parent window)</param>
    /// <param name="keyboard"> the keyboard inputs (this is often pulled from the parent window)</param>
    /// <param name="time"> the relative time (this is often pulled from the parent window)</param>
    /// <param name="camera"> the initial state of the camera</param>
    /// <returns>An incremental function that takes a CameraView</returns>
    let controlWithSpeed (speed : cval<float>) (mouse : IMouse) (keyboard : IKeyboard) (time : aval<DateTime>) (cam : CameraView) : aval<CameraView> =
         AVal.integrate cam time [
            controlWSADwithSpeed speed keyboard time
            controlLookAround mouse
            controlPanWithSpeed speed mouse
            controlZoomWithSpeed speed mouse
            controllScrollWithSpeed speed mouse time
        ]

    /// <summary> Implement common control functions for movement, looking, panning, and zooming 
    /// for a given camera. also adds the ability to adjust movement speed.
    /// Default controls use PageUp and PageDown.</summary>
    /// <param name="initialSpeed"> the initial rate at which all movement occurs</param>
    /// <param name="mouse"> the mouse inputs (this is often pulled from the parent window)</param>
    /// <param name="keyboard"> the keyboard inputs (this is often pulled from the parent window)</param>
    /// <param name="time"> the relative time (this is often pulled from the parent window)</param>
    /// <param name="camera"> the initial state of the camera</param>
    /// <returns>An incremental function that takes a CameraView</returns>
    let controlExt (initialSpeed : float ) (mouse : IMouse) (keyboard : IKeyboard) (time : aval<DateTime>) (cam : CameraView) : aval<CameraView> =
        let speed = AVal.init initialSpeed

        let ctrl = keyboard.IsDown Keys.LeftCtrl

        keyboard.DownWithRepeats.Values.Add ( fun k -> 
            match k with
            | Keys.PageUp -> transact ( fun _ -> speed.Value <- speed.Value * 1.3 )
            | Keys.PageDown -> transact ( fun _ -> speed.Value <- speed.Value / 1.3 )

            | Keys.Up when AVal.force ctrl -> transact ( fun _ -> speed.Value <- speed.Value * 1.3 )
            | Keys.Down when AVal.force ctrl -> transact ( fun _ -> speed.Value <- speed.Value / 1.3 )

            | _ -> ()
        )

        AVal.integrate cam time [
           controlWSADwithSpeed speed keyboard time
           controlLookAround mouse
           controlReset cam keyboard
           controlPanWithSpeed speed mouse
           controlZoomWithSpeed speed mouse
           controllScrollWithSpeed speed mouse time
       ]