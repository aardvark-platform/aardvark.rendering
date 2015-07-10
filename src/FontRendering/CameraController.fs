namespace FontRendering

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Incremental.Operators
open Aardvark.Application


module CameraController =

    type IMouse with
        member x.PixelPosition = x.Position |> Mod.map (fun p -> p.Position)

    /// rotates the CameraView around its origin using the left mouse-button.
    let controlLook (m : IMouse) =
        controller {
            // map the mouse position to a V2i
            let position = m.Position |> Mod.map (fun p -> p.NormalizedPosition)

            // the down-flag for the left mouse button
            let! d = m.IsDown MouseButtons.Left

            if d then
                // whenever the left button is down we'd like to react to moves
                let! delta = differentiate position

                // this check here is actually necessary in order to avoid useless
                // re-evaluations since CameraView does not provide a proper equality.
                // The system would therefore assume that the cameraview is constantly
                // changing whenever the mouse is down and therefore the controller would
                // be invoked until the mouse-button is released
                if delta <> V2d.Zero then
                    // moving over the entire size of the window will rotate by 360°
                    let relativeDelta = Constant.PiTimesTwo * delta

                    // return a function for transforming the CameraView
                    // by rotating around right and sky 
                    return fun (cam : CameraView) ->
                        let trafo =
                            M44d.Rotation ( cam.Right, -relativeDelta.Y ) *
                            M44d.Rotation ( cam.Sky,   -relativeDelta.X )

                        // finally create a new CameraView with a replaced forward-direction
                        let newForward = trafo.TransformDir cam.Forward |> Vec.normalize
                        newForward |> cam.WithForward

        }

    /// moves the CameraView in its XY-Plane using the middle mouse-button 
    let controlPan (m : IMouse) (speed : float) =
        controller {
            let position = m.Position |> Mod.map (fun p -> p.Position)
            let! d = m.IsDown MouseButtons.Middle

            if d then
                let! move = differentiate position

                return fun (cam : CameraView) ->
                    let step = 
                        cam.Down    * float move.Y + 
                        cam.Right   * float move.X

                    cam.WithLocation(cam.Location + speed * step)

        }

    /// moves the CameraView in its Z-Axis using the right mouse-button 
    let controlZoom (m : IMouse) (speed : float) =
        controller {
            let position = m.Position |> Mod.map (fun p -> p.Position)
            let! d = m.IsDown MouseButtons.Right

            if d then
                let! move = differentiate position

                return fun (cam : CameraView) ->
                    let step = cam.Forward * float move.Y 

                    cam.WithLocation(cam.Location - speed * step)
        }

    /// moves the CameraView in its XZ-Plane using the typical
    /// WSAD key assignment
    let controlWSAD (m : IKeyboard) (speed : float)  =
        controller {
            let! move = 
                (m.IsDown Keys.W %?  V2i.OI %. V2i.OO) %+
                (m.IsDown Keys.S %? -V2i.OI %. V2i.OO) %+
                (m.IsDown Keys.A %? -V2i.IO %. V2i.OO) %+ 
                (m.IsDown Keys.D %?  V2i.IO %. V2i.OO)

            if move <> V2i.Zero then
                let! dt = differentiate Mod.time

                return fun (cam : CameraView) ->
                    let direction = 
                        float move.X * cam.Right + 
                        float move.Y * cam.Forward

                    let delta = speed * direction * dt.TotalSeconds

                    cam.WithLocation(cam.Location + delta)
        }

    /// moves the CameraView in its Z-Axis using the scroll wheel
    /// where speed determines how fast the entire movement will be
    /// and decay specifies the exponential decay on speed 
    let controlScroll (m : IMouse) (speed : float) (decay : float) =
        controller {
            let currentSpeed = ref 0.0
            let last = ref None

            // currentSpeed is just a copy of totalscroll
            // allowing us to decay it manually in the 
            // controller function below
            let! s = differentiate m.TotalScroll
            currentSpeed := !currentSpeed + s

            // reading "differentiate time" here would
            // cause the function below to be re-created all the time
            // and the system would therefore not know that it actually
            // resembles an identity function (and must therefore not be re-executed)
            return fun (cam : CameraView) ->
                if abs !currentSpeed < 0.5 then
                    // if the speed is small stop moving the camera and
                    // reset the controller's state.
                    currentSpeed := 0.0
                    last := None
                    cam
                else
                    // at first get the current/last time where
                    // the last time is optional since we don't want to
                    // measure the dt between two different "active-phases"
                    // of the controller.
                    let n = DateTime.Now
                    let o = match !last with | Some last -> last | None -> n
                    last := Some n

                    // calculate dt and decay the speed accordingly
                    let dt = n - o
                    let v = !currentSpeed * pow decay dt.TotalSeconds
                    currentSpeed := v

                    // finally calculate a step based on the current speed and dt
                    let step = speed * (cam.Forward * v * dt.TotalSeconds)
                    cam.WithLocation(cam.Location + step)
        }


//
//    let controlOrbit (m : IMouse) (center : V3d) =
//        controller {
//            let position = m.Position |> Mod.map (fun p -> p.NormalizedPosition)
//            let! d = m.IsDown MouseButtons.Left
//
//            if d then
//                let! dp = differentiate position
//                if dp <> V2d.Zero then
//                    return fun (cam : CameraView) ->
//                        let dir = cam.Location - center
//
//                        let r = dir.Length
//                        let theta = acos (dir.Z / r) + dp.Y * Constant.Pi
//                        let phi = atan2 dir.Y dir.X + dp.X * Constant.PiTimesTwo
//
//                        let theta = theta |> clamp 0.001 (Constant.Pi - 0.001)
//
//                        let st = sin theta
//                        let newDir = V3d ( r * st * (cos phi), 
//                                           r * st * (sin phi), 
//                                           r * cos theta 
//                                         )
//
//                        CameraView.LookAt(center + newDir, center, V3d.OOI)
//
//        }
//
//    let controlOrbitScroll (m : IMouse) (center : V3d) (speed : float) (decay : float) =
//        controller {
//            let currentSpeed = ref 0.0
//            let last = ref None
//            let! s = differentiate m.TotalScroll
//            currentSpeed := !currentSpeed + s
//
//            return fun (cam : CameraView) ->
//                if abs !currentSpeed < 0.5 then
//                    currentSpeed := 0.0
//                    last := None
//                    cam
//                else
//                    let n = DateTime.Now
//                    let o = match !last with | Some last -> last | None -> n
//                    last := Some n
//
//                    let dt = n - o
//                    let v = !currentSpeed * pow decay dt.TotalSeconds
//                    currentSpeed := v
//
//                    let dir = cam.Location - center
//                    let r = dir |> Vec.length
//                    let newR = max 1.0 (r - speed * v * dt.TotalSeconds)
//
//
//                    cam.WithLocation(center + dir * (newR / r))
//        }
//
