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
                        0.0, 0.0, mass * (size.Y * size.Y + size.Z * size.Z) / 12.0
                    )
                let invI = I.Inverse

                { 
                    shape = shape
                    inertia = I
                    invInertia = invI
                    mass = mass
                    invMass = 1.0 / mass
                }
            | _ ->
                failwith "not implemented"


[<Struct>]
type Euclidean = { rot : V3d; trans : V3d } with
    member x.Inverse = { rot = -x.rot; trans = -x.trans }

    member x.TransformDir(dir : V3d) =
        Vec.cross x.rot dir

    member x.TransformPos(pos : V3d) =
        Vec.cross x.rot pos + x.trans
    
    member x.ToM33d() =
        let angle = Vec.length
        Rot3d(x.rot)


type RigidBodyInstance =
    {
        body                : RigidBody
        trafo               : Euclidean
        linearMomentum      : V3d
        angularMomentum     : V3d
    }

module RigidBodyInstance =
    open Aardvark.Base

    let ofBody (trafo : Euclidean) (body : RigidBody) =
        {
            body = body
            trafo = trafo
            linearMomentum = V3d.Zero
            angularMomentum = V3d.Zero
        }


    let applyMomentum (worldPoint : V3d) (worldMomentum : V3d) (instance : RigidBodyInstance) =

        // vector from center of mass to point
        let p = worldPoint - instance.trafo.trans

        // momentum in object space
        let m = worldMomentum

        // decompose the momentum into a linear and a normal part
        let dist2 = Vec.lengthSquared p


        let L = Vec.cross p m
        
        let P = m
        let linearPart = V3d.Zero // TODO: correct p * Vec.dot p m / dist2
        //let normalPart = m - linearPart

        { instance with
            linearMomentum = instance.linearMomentum + linearPart
            angularMomentum = instance.angularMomentum + Vec.cross p m
        }

    let private decomposeForces (forces : list<V3d * V3d>) (instance : RigidBodyInstance) =
        let mutable F = V3d.Zero
        let mutable M = V3d.Zero

        for (p, f) in forces do
            let p = p - instance.trafo.trans

            let dist2 = Vec.lengthSquared p
            let linear = p * Vec.dot p f / dist2
            
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
            let halfAngle = 0.5 * angleInRadians
            Rot3d(V = sin halfAngle * v / angleInRadians, W = cos halfAngle)

    let private toV3d (r : Rot3d) =
        let mutable axis = V3d.Zero
        let mutable angle = 0.0
        r.ToAxisAngle(&axis, &angle)
        axis * angle


    let step (forces : RigidBodyInstance -> list<V3d * V3d>) (dt : float) (instance : RigidBodyInstance) =
        let F0, M0 = decomposeForces (forces instance) instance
        
        // dP / dt = F
        // dL / dt = M

        // dPos / dt = P
        // dRot / dt = omega = I^-1 * L

        let a0 = instance.body.invMass * F0
        let v0 = instance.body.invMass * instance.linearMomentum

        let m : M33d = Rot3d.op_Explicit instance.trafo.rot.Inverse
        let invInertia = m * instance.body.invInertia * m.Transposed

        let alpha0 = invInertia * m.Transposed * M0
        let omega0 = invInertia * instance.angularMomentum
        
        let dp = v0 * dt + 0.5 * a0 * dt * dt
        let dr = omega0 * dt + 0.5 * alpha0 * dt * dt

        let p1 = instance.trafo.Trans + dp
        let r1 = toV3d instance.trafo.Rot + dr
       
        let newInstance = { instance with trafo = Euclidean3d(toRot3d r1, p1) }
        let F1, M1 = decomposeForces (forces newInstance) newInstance

        
        let m1 : M33d = Rot3d.op_Explicit newInstance.trafo.Rot
        { newInstance with
            linearMomentum = newInstance.linearMomentum + 0.5 * (F0 + F1) * dt
            angularMomentum = newInstance.angularMomentum + 0.5 * (M0 + M1) * dt
        }
        







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
            |> RigidBodyInstance.ofBody Euclidean3d.Identity

    let mutable thing = initialthing
    let mthing =
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
                thing <- RigidBodyInstance.step (fun _ -> []) dt.TotalSeconds thing

            thing
        )


    win.Keyboard.KeyDown(Keys.Enter).Values.Add(fun _ ->
        thing <- RigidBodyInstance.applyMomentum (-size / 2.0) (0.2 * thing.body.mass * V3d.OOI) thing
    )
    
    win.Keyboard.KeyDown(Keys.Back).Values.Add(fun _ ->
        thing <- RigidBodyInstance.applyMomentum (V3d(0.0, 0.0, -size.Z / 2.0)) (0.2 * thing.body.mass * V3d.OOI) thing
    )

    win.Keyboard.KeyDown(Keys.Space).Values.Add(fun _ ->
        thing <- initialthing
    )


    // lets define the bounds/color for our box
    // NOTE that the color is going to be ignored since we're using a texture
    let box = Box3d(-V3d.III, V3d.III)
    let color = C4b.Red

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
    
    win.Scene <- sg
    win.Run()
    
    0
