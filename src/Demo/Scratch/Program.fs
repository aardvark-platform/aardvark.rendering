// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.

open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.SceneGraph.IO
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Rendering.NanoVg
open FShade

module Shader =
    open FShade

    type Vertex = {
        [<Position>]        pos     : V4d
        [<Semantic("Urdar")>] m : M44d
        [<WorldPosition>]   wp      : V4d
        [<Normal>]          n       : V3d
        [<BiNormal>]        b       : V3d
        [<Tangent>]         t       : V3d
        [<Color>]           c       : V4d
        [<TexCoord>]        tc      : V2d
    }

    let trafo (v : Vertex) =
        vertex {
            let wp = v.m * v.pos
            return { v with
                        pos = uniform.ViewProjTrafo * wp
                        wp = wp 
                   }
        }
    let tcColor (v : Vertex) =
        fragment {
            return V4d(v.tc.X, v.tc.Y, 1.0, 1.0)
        }

    let environmentMap =
        samplerCube {
            texture uniform?EnvironmentMap
            filter Filter.MinMagMipLinear
            addressU WrapMode.Clamp
            addressV WrapMode.Clamp
            addressW WrapMode.Clamp
        }

    type Fragment =
        {
            [<Color>]
            color : V4d

            [<Depth>]
            depth : float
        }

    let environment (v : Vertex) =
        fragment {
            let pixel = v.pos //V4d(v.pos.X * 2.0, v.pos.Y * 2.0, v.pos.Z, v.pos.W)
            let world = uniform.ViewProjTrafoInv * pixel
            let world = world.XYZ / world.W
            let dir = world - uniform.CameraLocation |> Vec.normalize
            return {
                color = environmentMap.Sample(dir)
                depth = 1.0
            }
        }

[<Demo("Simple Sphere Demo")>]
[<Description("simply renders a red sphere with very simple lighting")>]
let bla() =
    Sg.sphere' 5 C4b.Red 1.0
        |> Sg.effect [
            DefaultSurfaces.trafo |> toEffect
            DefaultSurfaces.constantColor C4f.Red |> toEffect
            DefaultSurfaces.simpleLighting |> toEffect
        ]


[<Demo("Simple Cube Demo")>]
let blubber() =
    Sg.box' C4b.Red Box3d.Unit
        |> Sg.effect [
            DefaultSurfaces.trafo |> toEffect
            DefaultSurfaces.constantColor C4f.Red |> toEffect
            DefaultSurfaces.simpleLighting |> toEffect
        ]
        

[<Demo("Quad Demo")>]
let quad() =
    Sg.fullScreenQuad
        |> Sg.effect [
            DefaultSurfaces.trafo |> toEffect
            DefaultSurfaces.constantColor C4f.Red |> toEffect
        ]

[<Demo("Textured Quad Demo")>]
let quadTexture() =
    let environment =
        PixImageCube.load [
            CubeSide.NegativeX, @"E:\Development\WorkDirectory\DataSVN\lazarus_negative_x.jpg"
            CubeSide.PositiveX, @"E:\Development\WorkDirectory\DataSVN\lazarus_positive_x.jpg"
            CubeSide.NegativeY, @"E:\Development\WorkDirectory\DataSVN\lazarus_negative_y.jpg"
            CubeSide.PositiveY, @"E:\Development\WorkDirectory\DataSVN\lazarus_positive_y.jpg"
            CubeSide.NegativeZ, @"E:\Development\WorkDirectory\DataSVN\lazarus_negative_z.jpg"
            CubeSide.PositiveZ, @"E:\Development\WorkDirectory\DataSVN\lazarus_positive_z.jpg"
        ]

    let environment =
        environment 
            |> PixImageCube.ofOpenGlConvention
            |> PixImageCube.toTexture true

    let env =
        Sg.farPlaneQuad
            |> Sg.shader {
                do! Shader.environment
               }
            |> Sg.texture (Symbol.Create "EnvironmentMap") (Mod.constant environment)

    Sg.fullScreenQuad
        |> Sg.effect [
            DefaultSurfaces.trafo |> toEffect
            DefaultSurfaces.constantColor C4f.Red |> toEffect
            DefaultSurfaces.diffuseTexture |> toEffect
           ]
        |> Sg.diffuseFileTexture' @"E:\Development\WorkDirectory\DataSVN\pattern.jpg" false
        |> Sg.andAlso env




[<Demo("Super naive LoD")>]
let naiveLoD() =

    let highest = Sg.sphere' 5 C4b.Red 1.0      
    let middle  = Sg.sphere' 3 C4b.Blue 1.0     
    let low     = Sg.box' C4b.Green Box3d.Unit  

    let dist threshhold (s : NaiveLod.LodScope)= 
        (s.cameraPosition - s.trafo.Forward.C3.XYZ).Length < threshhold

    let scene = 
        NaiveLod.Sg.loD 
            low 
            (NaiveLod.Sg.loD middle highest (dist 5.0)) 
            (dist 8.0)

    let size = 10.0
    let many =
        [
            for x in -size .. 2.0 .. size do 
                for y in -size .. 2.0 .. size do
                    for z in -size .. 2.0 .. size do 
                        yield scene |> Sg.translate x y z 
                        //yield scene |> Sg.uniform "Urdar" (Mod.constant (M44d.Translation(x,y,z)))
        ] |> Sg.ofSeq

    let sg = 
        many
            |> Sg.effect [
               // Shader.trafo |> toEffect
                DefaultSurfaces.trafo  |> toEffect
                DefaultSurfaces.vertexColor    |> toEffect
                DefaultSurfaces.simpleLighting |> toEffect
               ]
            |> App.WithCam

    let objs = 
        sg 
        |> Aardvark.SceneGraph.Semantics.RenderObjectSemantics.Semantic.renderObjects 
        //|> Aardvark.Rendering.Optimizer.optimize App.Runtime App.FramebufferSignature
       

    App.Runtime.CompileRender(App.FramebufferSignature, objs)


[<Demo("Picking")>]
let picking() =
    let bounds = Box3d(-V3d.III*0.5, V3d.III*0.5)

    let cylinder = 
        Sg.cylinder' 16 C4b.Blue 0.5 1.0
            |> Sg.pickable (PickShape.Cylinder(Cylinder3d(V3d.Zero, V3d.OOI, 0.5)))
            |> Sg.requirePicking

    let sphere =
        Sg.unitSphere' 5 C4b.Yellow
            |> Sg.pickable (PickShape.Sphere(Sphere3d(V3d.Zero, 1.0)))
            |> Sg.requirePicking
            |> Sg.scale 0.5

    let box =
        Sg.box' C4b.Green bounds
            |> Sg.pickable (PickShape.Box bounds)
            |> Sg.requirePicking

    let rand = System.Random()
    let size = 10.0
    let many =
        Sg.ofList [
            for x in -size .. 2.0 .. size do 
                for y in -size .. 2.0 .. size do
                    for z in -size .. 2.0 .. size do 
                        match rand.Next(3) with
                            | 0 -> 
                                yield box |> Sg.translate x y z 
                            | 1 ->
                                yield cylinder |> Sg.translate x y z 
                            | _ ->
                                yield sphere |> Sg.translate x y z 
        ]

    let win = App.Window
    let cam = CameraView.lookAt (V3d(6,6,6)) V3d.Zero V3d.OOI
    let view = cam |> DefaultCameraController.control win.Mouse win.Keyboard win.Time
    let proj = win.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 1000.0 (float s.X / float s.Y))

    let tree = PickTree.ofSg many

    let mutable last = None

    let cam = Mod.map2 (fun view proj -> { cameraView = view; frustum = proj }) view proj
    let ray = Mod.map2 Camera.pickRay cam App.Mouse.Position
    let hit = Mod.bind tree.Intersect ray

//    let scopeString (scope : Ag.Scope) =
//        match Ag.tryGetAttributeValue scope "ModelTrafo" with
//            | Success (trafo : IMod<Trafo3d>) ->
//                let t = Mod.force trafo
//                sprintf "%A" t.Forward.C3.XYZ
//            | Error e ->
//                "no trafo!!!"
//            
//    App.Mouse.Click.Values.Add (fun b ->
//        match last with
//            | Some scope ->
//                printfn "click(%A, %s)" b (scopeString scope)
//            | None ->
//                ()
//    )
//
//
//    hit |> Mod.unsafeRegisterCallbackKeepDisposable (fun hit ->
//        match hit with
//            | Some hit ->
//                let (part, point) = hit.Value
//                let scope = part.Scope
//                match last with
//                    | Some l when l = scope ->
//                        printfn "move: %A" point
//                    | _ ->
//                        match last with
//                            | Some last ->
//                                printfn "exit: %s" (scopeString last)
//                            | None ->
//                                ()
//
//                        printfn "enter: %s (%A)" (scopeString scope) point
//
//                        last <- Some scope
//
//            | _ ->
//                match last with
//                    | Some l ->
//                        printfn "exit: %s" (scopeString l)
//                        last <- None
//                    | None ->
//                        ()
//    ) |> ignore


    let trafo = 
        hit |> Mod.map (fun hit ->
            match hit with
                | Some hit -> hit.Value |> snd |> Trafo3d.Translation
                | None -> V3d(nan, nan, nan) |> Trafo3d.Translation
        )

    let sg =
        many 
            |> Sg.andAlso (Sg.sphere' 5 C4b.Red 0.1 |> Sg.trafo trafo)
            |> Sg.effect [
               // Shader.trafo |> toEffect
                DefaultSurfaces.trafo  |> toEffect
                DefaultSurfaces.vertexColor    |> toEffect
                DefaultSurfaces.simpleLighting |> toEffect
               ]
            |> Sg.viewTrafo (view |> Mod.map CameraView.viewTrafo)
            |> Sg.projTrafo (proj |> Mod.map Frustum.projTrafo)


//    App.Mouse.Move.Values.Add (fun (_,pp) ->
//        let cam = { cameraView = Mod.force view; frustum = Mod.force proj }
//        let ray = Camera.pickRay cam pp 
//
//        let scopeString (scope : Ag.Scope) =
//            match Ag.tryGetAttributeValue scope "ModelTrafo" with
//                | Success (trafo : IMod<Trafo3d>) ->
//                    let t = Mod.force trafo
//                    sprintf "%A" t.Forward.C3.XYZ
//                | Error e ->
//                    "no trafo!!!"
//            
//
//        let res = hit |> Mod.force
//        match res with
//            | Some hit -> 
//                let scope = hit.Value.Scope
//                match last with
//                    | Some l when l = scope ->
//                        ()
//                    | _ ->
//                        match last with
//                            | Some last ->
//                                printfn "exit: %s" (scopeString last)
//                            | None ->
//                                ()
//
//                        printfn "enter: %s" (scopeString scope)
//
//                        last <- Some scope
//
//            | _ ->
//                match last with
//                    | Some l ->
//                        printfn "exit: %s" (scopeString l)
//                        last <- None
//                    | None ->
//                        ()
//    )

    let objs = 
        sg |> Aardvark.SceneGraph.Semantics.RenderObjectSemantics.Semantic.renderObjects 
        //|> Aardvark.Rendering.Optimizer.optimize App.Runtime App.FramebufferSignature
       

    App.Runtime.CompileRender(App.FramebufferSignature, objs)



[<Demo>]
let manymany() =

    let sphere = Sg.sphere 0 (Mod.constant C4b.Red) (Mod.constant 0.2)

    let controller = 
        controller {
            let! dt = App.Time |> differentiate
            return fun (t : Trafo3d) -> Trafo3d.RotationZ(dt.TotalSeconds) * t
        }

    let input =
        [
            for x in -10.0 .. 2.0 .. 10.0 do 
                for y in -10.0 .. 2.0 .. 10.0 do
                    for z in -10.0 .. 2.0 .. 10.0 do 
                        let m = Mod.init (Trafo3d.RotationZ(0.0))

                        let t = Trafo3d.Translation(x,y,z)
                        let m = AFun.integrate controller t


                        yield 
                            sphere |> Sg.trafo m//, m
                        //yield scene |> Sg.uniform "Urdar" (Mod.constant (M44d.Translation(x,y,z)))
        ]

        
    let many = input |> List.map id |> Sg.ofSeq
    //let mods = List.map snd input |> List.toArray

    //printfn "changing %d mods per frame" mods.Length

    let sg = 
        many
            |> Sg.effect [
               // Shader.trafo |> toEffect
                DefaultSurfaces.trafo  |> toEffect
                DefaultSurfaces.vertexColor    |> toEffect
                DefaultSurfaces.simpleLighting |> toEffect
               ]
            |> App.WithCam

    let mutable last = System.Diagnostics.Stopwatch()
    let mutable framecount = 0
    App.Time |> Mod.unsafeRegisterCallbackKeepDisposable (fun _ -> 
        framecount <- framecount + 1
        if framecount % 100 = 0 then 
            printfn "fps = %f" (100000.0 / last.Elapsed.TotalMilliseconds) 
            last.Restart()
    ) |> ignore

    let objs = 
        sg 
        |> Aardvark.SceneGraph.Semantics.RenderObjectSemantics.Semantic.renderObjects 
        //|> Aardvark.Rendering.Optimizer.optimize App.Runtime App.FramebufferSignature
       
//    let rnd = System.Random()
//    App.Time |> Mod.unsafeRegisterCallbackKeepDisposable (fun a -> 
//        transact (fun () -> 
//            for m in mods do 
//                m.Value <- Trafo3d.RotationZ(rnd.NextDouble())
//        )
//    )|> ignore

    App.Runtime.CompileRender(App.FramebufferSignature, objs)





module DependentHandleNative =
    open System
    open System.Reflection
    open System.Runtime.InteropServices

    [<AutoOpen>]
    module private Impl = 
        let tHandle = Type.GetType("System.Runtime.CompilerServices.DependentHandle")
        let nInitializeMeth = tHandle.GetMethod("nInitialize", BindingFlags.NonPublic ||| BindingFlags.Static)
        let nGetPrimaryMeth = tHandle.GetMethod("nGetPrimary", BindingFlags.NonPublic ||| BindingFlags.Static)
        let nGetPrimaryAndSecondaryMeth = tHandle.GetMethod("nGetPrimaryAndSecondary", BindingFlags.NonPublic ||| BindingFlags.Static)
        let nFreeMeth = tHandle.GetMethod("nFree", BindingFlags.NonPublic ||| BindingFlags.Static)

        type NInitializeDelegate = delegate of obj * obj * byref<nativeint> -> unit
        type NGetPrimaryDelegate = delegate of nativeint * byref<obj> -> unit
        type NGetPrimaryAndSecondaryDelegate = delegate of nativeint * byref<obj> * byref<obj> -> unit
        type NFreeDelegate = delegate of nativeint -> unit

        let nInitializeDel = 
            Delegate.CreateDelegate(typeof<NInitializeDelegate>, nInitializeMeth) |> unbox<NInitializeDelegate>

        let nGetPrimaryDel = 
            Delegate.CreateDelegate(typeof<NGetPrimaryDelegate>, nGetPrimaryMeth) |> unbox<NGetPrimaryDelegate>

        let nGetPrimaryAndSecondaryDel = 
            Delegate.CreateDelegate(typeof<NGetPrimaryAndSecondaryDelegate>, nGetPrimaryAndSecondaryMeth) |> unbox<NGetPrimaryAndSecondaryDelegate>
            
        let nFreeDel = 
            Delegate.CreateDelegate(typeof<NFreeDelegate>, nFreeMeth) |> unbox<NFreeDelegate>

    let nInitialize(primary : obj, secondary : obj, [<Out>] dependentHandle : byref<nativeint>) =
        nInitializeDel.Invoke(primary, secondary, &dependentHandle)

    let nGetPrimary(dependentHandle : nativeint, [<Out>] primary : byref<obj>) =
        nGetPrimaryDel.Invoke(dependentHandle, &primary)

    let nGetPrimaryAndSecondary(dependentHandle : nativeint, [<Out>] primary : byref<obj>, [<Out>] secondary : byref<obj>) =
        nGetPrimaryAndSecondaryDel.Invoke(dependentHandle, &primary, &secondary)

    let nFree(dependentHandle : nativeint) =
        nFreeDel.Invoke(dependentHandle)

type DependentHandle =
    struct
        val mutable public Handle : nativeint

        member x.IsAllocated = x.Handle <> 0n

        member x.GetPrimary() =
            let mutable res = null
            DependentHandleNative.nGetPrimary(x.Handle, &res)
            res

        member x.GetPrimaryAndSecondary() =
            let mutable primary = null
            let mutable secondary = null
            DependentHandleNative.nGetPrimaryAndSecondary(x.Handle, &primary, &secondary)
            primary, secondary

        member x.Free() =
            let h = System.Threading.Interlocked.Exchange(&x.Handle, 0n)
            if h <> 0n then
                DependentHandleNative.nFree(h)

        new(primary : obj, secondary : obj) =
            let mutable handle = 0n
            DependentHandleNative.nInitialize(primary, secondary, &handle)
            { Handle = handle }

    end

[<EntryPoint>]
let main argv = 

    //Scratch.InteractionExperiments.run() |> ignore
    //Scratch.FablishInterop.run argv |> ignore
    //System.Environment.Exit 0

    Ag.initialize()
    Aardvark.Init()

    App.Config <- { BackendConfiguration.Default with useDebugOutput = true }
    App.run()

    0 // return an integer exit code
