open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application

type Shape =
    | Box of size : V3d
    | Sphere of radius : float


type RigidBody =
    {
        shape       : Shape
        inertia     : M33d
        invInertia  : M33d
        mass        : float
        invMass     : float
    }
   
module RigidBody =
    let ofShape (mass : float) (shape : Shape) =
        match shape with
            | Box size ->
                let I = 
                    M33d(
                        mass * (size.Y * size.Y + size.Z * size.Z) / 12.0, 0.0, 0.0,
                        0.0, mass * (size.X * size.X + size.Z * size.Z) / 12.0, 0.0,
                        0.0, 0.0, mass * (size.X * size.X + size.Y * size.Y) / 12.0
                    )

                let invI =
                    M33d(
                        12.0 / (mass * (size.Y * size.Y + size.Z * size.Z)), 0.0, 0.0,
                        0.0, 12.0 / (mass * (size.X * size.X + size.Z * size.Z)), 0.0,
                        0.0, 0.0, 12.0 / (mass * (size.X * size.X + size.Y * size.Y))
                    )

                { 
                    shape = shape
                    inertia = I
                    invInertia = invI
                    mass = mass
                    invMass = 1.0 / mass
                }
            | _ ->
                failwith "not implemented"


type RigidBodyInstance =
    {
        body                : RigidBody
        trafo               : Euclidean3d
        linearMomentum      : V3d
        angularMomentum     : V3d
    }

type Force = { pos : V3d; force : V3d }


module Seq =
    let chooseMinBy (f : 'b -> 'x) (mapping : 'a -> Option<'b>) (seq : seq<'a>) =
        let a = seq |> Seq.choose mapping |> Seq.cache
        if Seq.isEmpty a then
            None
        else
            Seq.minBy f a |> Some

    let tryMinBy (f : 'a -> 'c) (s : seq<'a>) =
        use e = s.GetEnumerator()
        if e.MoveNext() then
            let mutable minE = e.Current
            let mutable min = f minE

            while e.MoveNext() do
                let v = f e.Current
                if v < min then
                    min <- v
                    minE <- e.Current

            Some minE

        else
            None

    let tryChooseMin (f : 'a -> Option<'c * 'b>) (s : seq<'a>) =
        let filtered = s |> Seq.choose f
        let e = filtered.GetEnumerator()

        if e.MoveNext() then
            let (c,b) = e.Current
            let mutable min = c
            let mutable minE = b
            while e.MoveNext() do
                let (c,b) = e.Current
                if c < min then
                    min <- c
                    minE <- b

            Some minE
        else
            None


module RigidBodyInstance =
    open Aardvark.Base




    let ofBody (trafo : Euclidean3d) (body : RigidBody) =
        {
            body = body
            trafo = trafo
            linearMomentum = V3d.Zero
            angularMomentum = V3d.Zero
        }


    let getVelocity (worldPoint : V3d) (instance : RigidBodyInstance) =
        let vl = instance.body.invMass * instance.linearMomentum 

        let m : M33d = Rot3d.op_Explicit instance.trafo.Rot
        let inv = m * instance.body.invInertia * m.Transposed

        let omega = inv * instance.angularMomentum
        let vr = Vec.cross omega (worldPoint - instance.trafo.Trans)

        vl + vr



    let applyMomentum (worldPoint : V3d) (worldMomentum : V3d) (instance : RigidBodyInstance) =

        // vector from center of mass to point
        let r = worldPoint - instance.trafo.Trans

        // momentum in object space
        let p = worldMomentum

        // decompose the momentum into a linear and a angular part
        let angularPart = 
            Vec.cross r p
        
        let linearPart = //V3d.Zero
            let r2 = Vec.lengthSquared r
            if Fun.IsTiny r2 then p
            else r * Vec.dot r p / r2

        { instance with
            linearMomentum = instance.linearMomentum + linearPart
            angularMomentum = instance.angularMomentum + angularPart
        }

    let addVelocity (worldPoint : V3d) (worldVelocity : V3d) (instance : RigidBodyInstance) =
        let m = instance.trafo
        let mi = instance.trafo.Inverse

        let p = mi.TransformPos worldPoint
        let dv = mi.TransformDir worldVelocity

        let domega = 
            Vec.cross p dv / Vec.lengthSquared p

        let dv =
            let len2 = Vec.lengthSquared p
            if Fun.IsTiny len2 then dv
            else
                (Vec.dot p dv / len2) * p

        let L = instance.body.inertia * domega
        let p = instance.body.mass * dv

        { instance with
            angularMomentum = instance.angularMomentum + m.TransformDir L
            linearMomentum = instance.linearMomentum + m.TransformDir p
        }





    let private decomposeForces (forces : list<Force>) (instance : RigidBodyInstance) =
        let mutable F = V3d.Zero
        let mutable M = V3d.Zero

        for { pos = p; force = f } in forces do
            let p = p - instance.trafo.Trans

            let dist2 = Vec.lengthSquared p
            let linear = 
                if Fun.IsTiny dist2 then
                    f
                else 
                    p * Vec.dot p f / dist2
            
            F <- F + linear
            M <- M + Vec.cross p f

        F, M

    let private toRot3d (v : V3d) =
        //public Rot3d(V3d axis, double angleInRadians)
        //{
        //    var halfAngle = angleInRadians / 2;
        //    W = halfAngle.Cos();
        //    V = axis.Normalized * halfAngle.Sin();
        //}

        let angleInRadians = Vec.length v
        if Fun.IsTiny angleInRadians then
            Rot3d.Identity
        else
            let axis = v / angleInRadians
            Rot3d(axis, angleInRadians)

    let step (forces : RigidBodyInstance -> list<Force>) (dt : float) (instance : RigidBodyInstance) =
        let F0, M0 = decomposeForces (forces instance) instance
        
        if not (V3d.ApproxEqual(F0, V3d.Zero)) || 
           not (V3d.ApproxEqual(M0, V3d.Zero)) || 
           not (V3d.ApproxEqual(instance.linearMomentum, V3d.Zero)) ||
           not (V3d.ApproxEqual(instance.angularMomentum, V3d.Zero)) then

                let m : M33d = Rot3d.op_Explicit instance.trafo.Rot
                let mInv = m.Transposed
                
                // dP / dt = F
                // dL / dt = M

                // dPos / dt = P
                // dRot / dt = omega = I^-1 * L
                let invInertia = instance.body.invInertia
        
                let a0 = instance.body.invMass * (mInv * F0)
                let v0 = instance.body.invMass * (mInv * instance.linearMomentum)

                let alpha0 = invInertia * (mInv * M0)
                let omega0 = invInertia * (mInv * instance.angularMomentum)
        
                let dp = v0 * dt + 0.5 * a0 * dt * dt
                let dr = omega0 * dt + 0.5 * alpha0 * dt * dt


                //let ra = Rot3d(V3d.III.Normalized, 0.4)
                //let rb = Rot3d(V3d.OOI.Normalized, 0.2)
                //let ma = M44d.Rotation(ra)
                //let mb = M44d.Rotation(rb)
                //let rc : M33d = ra * rb |> Rot3d.op_Explicit
                //let mc = (ma * mb).UpperLeftM33()
                //let eq = M33d.ApproximatelyEquals(rc, mc, 0.000001)
                
                //let dr = 
                //    instance.trafo.Rot * (toRot3d dr) * instance.trafo.Rot.Inverse
                
                let p1 = instance.trafo.Trans + m * dp
                let r1 = instance.trafo.Rot * (toRot3d dr)
       
                // intersects(trafo * p, prim)

                let newInstance = { instance with trafo = Euclidean3d(r1, p1) }
                let F1, M1 = decomposeForces (forces newInstance) newInstance
                

                { newInstance with
                    linearMomentum = newInstance.linearMomentum + 0.5 * (F0 + F1) * dt
                    angularMomentum = newInstance.angularMomentum + 0.5 * (M0 + M1) * dt
                }
            else
                instance

    let rec intersects (t : float) (dt : float) (lf : RigidBodyInstance -> list<Force>) (rf : RigidBodyInstance -> list<Force>) (l : RigidBodyInstance) (r : RigidBodyInstance) =
        let l1 = step lf dt l
        let r1 = step rf dt r

        match l.body.shape, r.body.shape with
            | Box ls, Box rs ->
                let lb = Box3d.FromCenterAndSize(V3d.Zero, ls)
                let rb = Box3d.FromCenterAndSize(V3d.Zero, rs)

                let inline m (e : Euclidean3d) : M44d = Euclidean3d.op_Explicit e

                let r2l = m l.trafo.Inverse * m r.trafo
                let r2l1 = m l1.trafo.Inverse * m r1.trafo
                
                let intersections = 
                    rb.ComputeCorners() |> Array.toList |> List.choose (fun ptr ->
                        let pt0 = r2l.TransformPos ptr
                        let pt1 = r2l1.TransformPos ptr

                        let dir = pt1 - pt0
                        let len = Vec.length dir
                        let ray = Ray3d(pt0, dir / len) //Line3d(pt, pt')
                    
                        let mutable t = System.Double.PositiveInfinity
                        lb.Intersects(ray, &t) |> ignore

                        if lb.Intersects(ray, &t) && t >= 0.0 && t <= len && not (lb.Contains ray.Origin) then
                            let pi = ray.GetPointOnRay t
                            let t = t / len

                            let eps = 0.01
                            let x = Fun.ApproximateEquals(abs pi.X, lb.Max.X, eps)
                            let y = Fun.ApproximateEquals(abs pi.Y, lb.Max.Y, eps)
                            let z = Fun.ApproximateEquals(abs pi.Z, lb.Max.Z, eps)

                            let dir = 
                                match x,y,z with 
                                    | false, false, false -> V3d.Zero
                                    | true, false, false -> V3d.IOO
                                    | false, true, false -> V3d.OIO
                                    | false, false, true -> V3d.OOI

                                    | true, true, false -> 
                                        let mutable dir = ray.Direction
                                        dir.Z <- 0.0
                                        Vec.normalize dir
                                
                                    | true, false, true -> 
                                        let mutable dir = ray.Direction
                                        dir.Y <- 0.0
                                        Vec.normalize dir
                                
                                    | false, true, true -> 
                                        let mutable dir = ray.Direction
                                        dir.X <- 0.0
                                        Vec.normalize dir
                                    | true, true, true ->
                                        Vec.normalize  ray.Direction

                            let dt' = t * dt

                            Some (t * dt, l1.trafo.TransformPos pi, l1.trafo.TransformDir dir)
                        else
                            None

                    )
                   
                match intersections with
                    | [] -> None
                    | l -> 
                        let l = List.sortBy (fun (t,_,_) -> t) l
                        Some (l)

                //intersections
                //match intersections with
                //    | [] ->
                //        None
                //    | l ->
                //        l
                //    | Some (ti, a, b) ->
                //        if ti > 0.001 then
                //            //failwith "implement me"
                //            match intersects t ti lf rf l r with
                //                | Some(ti,a,b) -> 
                //                    Some(ti, a, b)
                //                | None ->
                //                    let l1 = step lf ti l
                //                    let r1 = step rf ti r
                //                    intersects ti (dt - ti) lf rf l1 r1
                //        else
                //            Some (t + ti, a, b)
                //    | None ->
                //        None
                        
            | _ ->
                failwith ""



type World =
    {
        objects : hmap<string, RigidBodyInstance>
        forces : string -> RigidBodyInstance -> list<Force>
    }

module World =
    

    let rec step (dt : float) (world : World) =
        if dt <= 0.0 then
            world
        else
            let test = 
                let inline fst3 (a,_,_) = a
                let inline thrd (_,_,a) = a
                world.objects |> Seq.chooseMinBy (thrd >> List.head >> fst3) (fun (ln, lo) ->
                    world.objects |> Seq.chooseMinBy (thrd >> List.head >> fst3) (fun (rn, ro) ->
                        if ln < rn then    
                            match RigidBodyInstance.intersects 0.0 dt (world.forces ln) (world.forces rn) lo ro with
                                | Some l -> Some (ln, rn, l)
                                | None -> None
                                //| Some(dt, pos, dir) ->
                                //    Some (ln, rn, dt, pos, dir)
                                //| None ->
                                //    None
                        else
                            None
                    )
                )

            match test with
                | Some (ln, rn, intersections) ->
                    
                    let ti, intersections =
                        match intersections with
                            | (tmin,a,b) :: rest ->
                                let mutable tmin = tmin
                                let mutable res = [a,b]

                                tmin, [
                                    yield a,b
                                    for (t,a,b) in rest do
                                        if Fun.ApproximateEquals(t, tmin, 0.0001) then
                                            yield (a,b)
                                ]
                            | _ ->
                                failwith ""

        
                    let world = { world with objects = world.objects |> HMap.map (fun id -> RigidBodyInstance.step (world.forces id) ti)  }

                    let newWorld = 
                        intersections |> List.fold (fun world (pos, dir) ->
                            if not (V3d.ApproxEqual(dir, V3d.Zero)) then
                                let lo = world.objects.[ln]
                                let ro = world.objects.[rn]
                                let vl = RigidBodyInstance.getVelocity pos lo
                                let vr = RigidBodyInstance.getVelocity pos ro

                                let dvld = 
                                    (2.0 * ro.body.mass / (lo.body.mass + ro.body.mass)) *
                                    (Vec.dot (vr - vl) dir) * dir
                        
                                let dvrd = 
                                    (2.0 * lo.body.mass / (lo.body.mass + ro.body.mass)) *
                                    (Vec.dot (vl - vr) dir) * dir
                        

                                let lo1 = RigidBodyInstance.addVelocity pos dvld lo
                                let ro1 = RigidBodyInstance.addVelocity pos dvrd ro

                                let a = RigidBodyInstance.getVelocity pos lo1 |> Vec.dot dir
                                let a1 = vl + dvld |> Vec.dot dir
                                if not (Fun.IsTiny(a - a1)) then
                                    printfn "bad"

                                let b = RigidBodyInstance.getVelocity pos ro1 |> Vec.dot dir
                                let b1 = vr + dvrd |> Vec.dot dir
                                if not (Fun.IsTiny(b - b1)) then
                                    printfn "bad"


                                let newWorld = 
                                    { world with
                                        objects = HMap.add ln lo1 (HMap.add rn ro1 world.objects)
                                    }
                                
                                newWorld
                            else
                                world
                        ) world

                    if ti < dt then
                        step (dt - ti) newWorld
                    else
                        newWorld
                | None -> 
                    let newWorld = { world with objects = world.objects |> HMap.map (fun id -> RigidBodyInstance.step (world.forces id) dt)  }
                    newWorld

     



[<EntryPoint>]
let main argv = 
    // first we need to initialize Aardvark's core components
    Ag.initialize()
    Aardvark.Init()



    let win =
        window {
            backend Backend.Vulkan
            display Display.Mono
            debug true
            verbosity DebugVerbosity.Warning
            samples 8
        }

    let size = V3d(1.0, 0.2, 0.4)
    let initialthing =
        Box size
            |> RigidBody.ofShape 10.0
            |> RigidBodyInstance.ofBody (Euclidean3d(Rot3d.Identity, V3d.OOI))

    let floorSize = V3d(10.0, 10.0, 0.1)
    let floor =
        Box floorSize
            |> RigidBody.ofShape 100000.0
            |> RigidBodyInstance.ofBody Euclidean3d.Identity


    let initialWorld =
        {
            objects =
                HMap.ofList [
                    "thing", initialthing
                    "floor", floor
                ]

            forces = fun name b ->
                if name = "thing" then
                    [ { pos = b.trafo.Trans; force = V3d(0.0, 0.0, -2.0) } ]
                else
                    []
        }

    let mutable world = initialWorld
    let mworld =
        let sw = System.Diagnostics.Stopwatch.StartNew()
        let mutable lastTime = None
        win.Time |> Mod.map (fun _ ->
            let now = sw.MicroTime
            let dt = 
                match lastTime with
                    | Some last ->  now - last
                    | None -> MicroTime.Zero

            lastTime <- Some now

            if dt.TotalSeconds <= 0.16666 then
                world <- World.step dt.TotalSeconds world

            world
        )


    win.Keyboard.KeyDown(Keys.Space).Values.Add(fun _ ->
        world <- initialWorld
    )

    let mthing = mworld |> Mod.map (fun w -> w.objects.["thing"])
    let mfloor = mworld |> Mod.map (fun w -> w.objects.["floor"])

    // lets define the bounds/color for our box
    // NOTE that the color is going to be ignored since we're using a texture
    let box = Box3d(-V3d.III, V3d.III)
    let color = C4b.Red


    let ang =
        let trafo (t : RigidBodyInstance) =
            let v = t.angularMomentum
            let l = Vec.length v
            Trafo3d.Scale(1.0, 1.0, l) *
            Trafo3d.RotateInto(V3d.OOI,  v / l) * 
            Trafo3d.Translation t.trafo.Trans 

        Sg.cylinder' 16 C4b.Red 0.01 1.0
            |> Sg.trafo (mthing |> Mod.map trafo)
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.constantColor C4f.Red
                do! DefaultSurfaces.simpleLighting
            }

    let coord =
        Sg.coordinateCross' 1.0
            |> Sg.shader {
                do! DefaultSurfaces.trafo
            }

    let floor = 
        Sg.box (Mod.constant color) (Mod.constant (Box3d.FromCenterAndSize(V3d.Zero, floorSize)))
            |> Sg.trafo (mfloor |> Mod.map (fun t -> Trafo3d(Euclidean3d.op_Explicit t.trafo, Euclidean3d.op_Explicit t.trafo.Inverse)))
            
            // apply a shader ...
            // * transforming all vertices
            // * looking up the DiffuseTexture 
            // * applying a simple lighting to the geometry (headlight)
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.constantColor C4f.Red
                do! DefaultSurfaces.simpleLighting
            }
    let sg = 
        // thankfully aardvark defines a primitive box
        Sg.box (Mod.constant color) (Mod.constant (Box3d.FromCenterAndSize(V3d.Zero, size)))
            |> Sg.trafo (mthing |> Mod.map (fun t -> Trafo3d(Euclidean3d.op_Explicit t.trafo, Euclidean3d.op_Explicit t.trafo.Inverse)))
            
            // apply a shader ...
            // * transforming all vertices
            // * looking up the DiffuseTexture 
            // * applying a simple lighting to the geometry (headlight)
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.constantColor C4f.White
                do! DefaultSurfaces.simpleLighting
            }
        |> Sg.andAlso ang
        |> Sg.andAlso coord
        |> Sg.andAlso floor
    win.Scene <- sg
    win.Run()
    
    0
