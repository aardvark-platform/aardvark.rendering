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
            DefaultSurfaces.stableTrafo |> toEffect
            DefaultSurfaces.constantColor C4f.Red |> toEffect
            DefaultSurfaces.stableHeadlight |> toEffect
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
            CubeSide.NegativeX, @"C:\Users\Schorsch\Development\WorkDirectory\lazarus_negative_x.jpg"
            CubeSide.PositiveX, @"C:\Users\Schorsch\Development\WorkDirectory\lazarus_positive_x.jpg"
            CubeSide.NegativeY, @"C:\Users\Schorsch\Development\WorkDirectory\lazarus_negative_y.jpg"
            CubeSide.PositiveY, @"C:\Users\Schorsch\Development\WorkDirectory\lazarus_positive_y.jpg"
            CubeSide.NegativeZ, @"C:\Users\Schorsch\Development\WorkDirectory\lazarus_negative_z.jpg"
            CubeSide.PositiveZ, @"C:\Users\Schorsch\Development\WorkDirectory\lazarus_positive_z.jpg"
        ]

    let environment =
        environment 
//            |> PixImageCube.toOpenGlConvention
            |> PixImageCube.rotX90
            |> PixImageCube.toTexture true

    let env =
        Sg.farPlaneQuad
            |> Sg.shader {
                do! Shader.environment
               }
            |> Sg.texture (Symbol.Create "EnvironmentMap") (Mod.constant environment)

    let coord =
        Sg.coordinateCross' 3.0
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.vertexColor
            }

    Sg.fullScreenQuad
        |> Sg.effect [
            DefaultSurfaces.trafo |> toEffect
            DefaultSurfaces.constantColor C4f.Red |> toEffect
            DefaultSurfaces.diffuseTexture |> toEffect
           ]
        |> Sg.diffuseFileTexture' @"C:\Users\Schorsch\Development\WorkDirectory\pattern.jpg" false
        |> Sg.andAlso env
        |> Sg.andAlso coord

[<ReflectedDefinition>]
module ShaderStuff =
    open FShade

    let sampler1 =
        sampler2d {
            texture uniform?DiffuseColorTexture
            filter Filter.MinMagMipLinear
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
        }

    let sampler2 =
        sampler2d {
            texture uniform?DiffuseColorTexture
            filter Filter.MinMagPoint
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
        }

    let sepp (s : Sampler2d) (tc : V2d) =
        s.Sample(tc)

    let sample(tc : V2d) =
        if tc.X > 0.5 then
            let s = sampler1.Size
            sepp sampler1 (tc * V2d s / 100.0)
        else
            sepp sampler2 tc
        

    let fragment (v : Effects.Vertex) =
        fragment {
            let a : V3d = uniform?A
            let b : V3d = uniform?B
            let c : float = uniform?C
            let f = (a + b) * c
            return sample(v.tc * f.XY) 
        }


    [<Demo("Duplicate Texture Name Demo")>]
    let duplTexture() =
        let tex = FileTexture(@"C:\Users\Schorsch\Development\WorkDirectory\pattern.jpg", { TextureParams.mipmapped with wantSrgb = true })
        Sg.fullScreenQuad
            |> Sg.effect [
                DefaultSurfaces.trafo |> toEffect
                fragment |> toEffect
               ]
            |> Sg.uniform "A" (Mod.constant V3d.IOI)
            |> Sg.uniform "B" (Mod.constant V3d.OII)
            |> Sg.uniform "C" (Mod.constant 1.0)
            |> Sg.diffuseTexture (tex :> ITexture |> Mod.constant)



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

[<Demo("Frustum Merge")>]
let frustumMerge() =

    let lProj = { left = -0.2; right = 0.3; top = 0.25; bottom = -0.25; near = 1.0; far = 10.0 }
    let rProj = { left = -0.3; right = 0.2; top = 0.25; bottom = -0.25; near = 1.0; far = 10.0 }

    let lViewProj = Trafo3d.Translation(0.7, 0.0, 0.0) * Frustum.projTrafo lProj
    let rViewProj = Trafo3d.Translation(-0.7, 0.0, 0.0) * Frustum.projTrafo rProj

    let merged = ViewProjection.mergeStereo lViewProj rViewProj

    let frustum (color : C4b) (viewProj : Trafo3d) =
        Sg.wireBox' color (Box3d(-V3d.III, V3d.III))
            |> Sg.transform (viewProj.Inverse)

    Sg.ofList [
        frustum C4b.Green lViewProj
        frustum C4b.Red rViewProj
        frustum C4b.Yellow merged
    ]
    |> Sg.transform (Trafo3d.FromBasis(V3d.IOO, -V3d.OOI, V3d.OIO, V3d.Zero))
    |> Sg.shader {
        do! DefaultSurfaces.trafo
        do! DefaultSurfaces.vertexColor
    }



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




module Ranges =
    open System.Collections.Generic

    type HalfRangeKind =
        | Left = 0
        | Right = 1

    [<StructuredFormatDisplay("{AsString}")>]
    type RangeSet(store : MapExt<int64, HalfRangeKind>) =
        static let empty = RangeSet(MapExt.empty)

        static member Empty = empty

        member private x.store = store

        static member OfSeq(s : seq<Range1l>) =
            let arr = s |> Seq.toArray
            if arr.Length = 0 then
                empty
            elif arr.Length = 1 then
                let r = arr.[0]
                RangeSet(MapExt.ofList [r.Min, HalfRangeKind.Left; r.Max + 1L, HalfRangeKind.Right ])
            else
                // TODO: better impl possible (sort array and traverse)
                arr |> Array.fold (fun s r -> s.Add r) empty

        member x.Add(r : Range1l) =
            let min = r.Min
            let max = r.Max + 1L

            let lm, _, inner = MapExt.split min store
            let inner, _, rm = MapExt.split max inner

            let before = MapExt.tryMax lm |> Option.map (fun mk -> mk, lm.[mk])
            let after = MapExt.tryMin rm |> Option.map (fun mk -> mk, rm.[mk])

            let newStore = 
                match before, after with
                    | None, None ->
                        MapExt.ofList [ min, HalfRangeKind.Left; max, HalfRangeKind.Right]

                    | Some(bk, HalfRangeKind.Right), None ->
                        lm 
                        |> MapExt.add min HalfRangeKind.Left
                        |> MapExt.add max HalfRangeKind.Right

                    | Some(bk, HalfRangeKind.Left), None ->
                        lm 
                        |> MapExt.add max HalfRangeKind.Right

                    | None, Some(ak, HalfRangeKind.Left) ->
                        rm
                        |> MapExt.add min HalfRangeKind.Left
                        |> MapExt.add max HalfRangeKind.Right

                    | None, Some(ak, HalfRangeKind.Right) ->
                        rm
                        |> MapExt.add min HalfRangeKind.Left

                    | Some(bk, HalfRangeKind.Right), Some(ak, HalfRangeKind.Left) ->
                        let self = MapExt.ofList [ min, HalfRangeKind.Left; max, HalfRangeKind.Right]
                        MapExt.union (MapExt.union lm self) rm
                        
                    | Some(bk, HalfRangeKind.Left), Some(ak, HalfRangeKind.Left) ->
                        let self = MapExt.ofList [ max, HalfRangeKind.Right]
                        MapExt.union (MapExt.union lm self) rm

                    | Some(bk, HalfRangeKind.Right), Some(ak, HalfRangeKind.Right) ->
                        let self = MapExt.ofList [ min, HalfRangeKind.Left ]
                        MapExt.union (MapExt.union lm self) rm

                    | Some(bk, HalfRangeKind.Left), Some(ak, HalfRangeKind.Right) ->
                        MapExt.union lm rm

                    | _ ->
                        failwithf "impossible"

            RangeSet(newStore)

        member x.Remove(r : Range1l) =
            let min = r.Min
            let max = r.Max + 1L

            let lm, _, inner = MapExt.split min store
            let inner, _, rm = MapExt.split max inner

            let before = MapExt.tryMax lm |> Option.map (fun mk -> mk, lm.[mk])
            let after = MapExt.tryMin rm |> Option.map (fun mk -> mk, rm.[mk])

            let newStore = 
                match before, after with
                    | None, None ->
                        MapExt.empty

                    | Some(bk, HalfRangeKind.Right), None ->
                        lm

                    | Some(bk, HalfRangeKind.Left), None ->
                        lm 
                        |> MapExt.add min HalfRangeKind.Right

                    | None, Some(ak, HalfRangeKind.Left) ->
                        rm

                    | None, Some(ak, HalfRangeKind.Right) ->
                        rm
                        |> MapExt.add max HalfRangeKind.Left

                    | Some(bk, HalfRangeKind.Right), Some(ak, HalfRangeKind.Left) ->
                        MapExt.union lm rm
                        
                    | Some(bk, HalfRangeKind.Left), Some(ak, HalfRangeKind.Left) ->
                        let self = MapExt.ofList [ min, HalfRangeKind.Right]
                        MapExt.union (MapExt.union lm self) rm

                    | Some(bk, HalfRangeKind.Right), Some(ak, HalfRangeKind.Right) ->
                        let self = MapExt.ofList [ max, HalfRangeKind.Left ]
                        MapExt.union (MapExt.union lm self) rm

                    | Some(bk, HalfRangeKind.Left), Some(ak, HalfRangeKind.Right) ->
                        let self = MapExt.ofList [ min, HalfRangeKind.Right; max, HalfRangeKind.Left]
                        MapExt.union (MapExt.union lm self) rm

                    | _ ->
                        failwithf "impossible"

            RangeSet(newStore)

        member x.Contains(v : int64) =
            let l, s, _ = MapExt.neighbours v store
            match s with
                | Some(_,k) -> 
                    k = HalfRangeKind.Left
                | _ ->
                    match l with
                        | Some(_,HalfRangeKind.Left) -> true
                        | _ -> false

        member x.Count = 
            assert (store.Count &&& 1 = 0)
            store.Count / 2

        member private x.AsString = x.ToString()

        member x.ToArray() =
            let arr = Array.zeroCreate (store.Count / 2)
            let rec write (i : int) (l : list<int64 * HalfRangeKind>) =
                match l with
                    | (lKey,lValue) :: (rKey, rValue) :: rest ->
                        arr.[i] <- Range1l(lKey, rKey - 1L)
                        write (i + 1) rest

                    | [_] -> failwith "bad RangeSet"

                    | [] -> ()
                    
            store |> MapExt.toList |> write 0
            arr

        member x.ToList() =
            let rec build (l : list<int64 * HalfRangeKind>) =
                match l with
                    | (lKey,lValue) :: (rKey, rValue) :: rest ->
                        Range1l(lKey, rKey - 1L) :: 
                        build rest

                    | [_] -> failwith "bad RangeSet"

                    | [] -> []

            store |> MapExt.toList |> build
             
        member x.ToSeq() =
            x :> seq<_>       

        override x.ToString() =
            let rec ranges (l : list<int64 * HalfRangeKind>) =
                match l with
                    | (kMin, vMin) :: (kMax, vMax) :: rest ->
                        sprintf "[%d,%d)" kMin kMax ::
                        ranges rest

                    | [(k,v)] ->
                        [ sprintf "ERROR: %d %A" k v ]

                    | [] ->
                        []
                
            store |> MapExt.toList |> ranges |> String.concat ", " |> sprintf "ranges [ %s ]"

        interface System.Collections.IEnumerable with
            member x.GetEnumerator() = new RangeSetEnumerator((store :> seq<_>).GetEnumerator()) :> _
            
        interface System.Collections.Generic.IEnumerable<Range1l> with
            member x.GetEnumerator() = new RangeSetEnumerator((store :> seq<_>).GetEnumerator()) :> _

    and private RangeSetEnumerator(e : IEnumerator<KeyValuePair<int64, HalfRangeKind>>) =
        
        let mutable a = Unchecked.defaultof<_>
        let mutable b = Unchecked.defaultof<_>

        member x.MoveNext() =
            if e.MoveNext() then
                a <- e.Current
                if e.MoveNext() then
                    b <- e.Current
                    true
                else
                    failwithf "impossible"
            else
                false
            
        member x.Reset() =
            e.Reset()
            a <- Unchecked.defaultof<_>
            b <- Unchecked.defaultof<_>

        member x.Current =
            assert (a.Value = HalfRangeKind.Left && b.Value = HalfRangeKind.Right)
            Range1l(a.Key, b.Key - 1L)

        member x.Dispose() =
            e.Dispose()
            a <- Unchecked.defaultof<_>
            b <- Unchecked.defaultof<_>

        interface System.Collections.IEnumerator with
            member x.MoveNext() = x.MoveNext()
            member x.Current = x.Current :> obj
            member x.Reset() = x.Reset()

        interface System.Collections.Generic.IEnumerator<Range1l> with
            member x.Dispose() = x.Dispose()
            member x.Current = x.Current

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module RangeSet =
        let empty = RangeSet.Empty

        let inline ofSeq (s : seq<Range1l>) = RangeSet.OfSeq s
        let inline ofList (s : list<Range1l>) = RangeSet.OfSeq s
        let inline ofArray (s : Range1l[]) = RangeSet.OfSeq s

        let inline add (r : Range1l) (s : RangeSet) = s.Add r
        let inline remove (r : Range1l) (s : RangeSet) = s.Remove r
        let inline contains (v : int64) (s : RangeSet) = s.Contains v
        let inline count (s : RangeSet) = s.Count

        let inline toSeq (s : RangeSet) = s :> seq<_>
        let inline toList (s : RangeSet) = s.ToList()
        let inline toArray (s : RangeSet) = s.ToArray()


        
open Ranges 

open Aardvark.Rendering.Vulkan
open System.Runtime.InteropServices
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open System.Threading.Tasks

open System
let test () =

    let app = new OpenGlApplication()
    
    let enabled = ref false

    let fu = obj()
    let d = System.Collections.Generic.List()
    
    Aardvark.Rendering.GL.RuntimeConfig.SupressSparseBuffers <- true

    let runtime = app.Runtime :> IRuntime
    let win = app.CreateGameWindow()
    let ss = runtime.CreateFramebuffer(win.FramebufferSignature,V2i.II |> Mod.constant)
    
    Log.line "START THING"
    let pool = runtime.CreateGeometryPool( [DefaultSemantic.Positions, typeof<V4f>; DefaultSemantic.Colors, typeof<V4f>] |> Map.ofList )

    
    let ct = ref 10000

    let disp() =
        Log.startTimed "clearing %d ptrs" (d.Count)
        for p in d do
            pool.Free p 
        Log.stop()
        lock fu ( fun _ -> d.Clear() )

    win.Keyboard.KeyDown(Keys.Space).Values.Add( fun _ -> enabled := true )
    win.Keyboard.KeyDown(Keys.C).Values.Add( fun _ -> disp() )
    win.Keyboard.KeyDown(Keys.P).Values.Add( fun _ -> ct := !ct * 10; Log.line "ct=%d" (!ct))
    win.Keyboard.KeyDown(Keys.L).Values.Add( fun _ -> ct := !ct / 10; Log.line "ct=%d" (!ct))
    win.Keyboard.KeyDown(Keys.X).Values.Add( fun _ -> 
        for i in 1..10 do (System.GC.Collect(System.Int32.MaxValue, GCCollectionMode.Forced, true); System.GC.WaitForFullGCComplete() |> ignore) 
        Log.line "Collected GC NOW.")
    win.Keyboard.KeyUp(Keys.Space).Values.Add( fun _ -> enabled := false )
    win.Keyboard.KeyDown(Keys.D).Values.Add( fun _ -> Log.line "- render -"; win.RenderTask.Run(RenderToken.Empty, ss |> Mod.force) )
    

    async {
        do! Async.SwitchToNewThread()
        let mutable i = 0
        while true do
            System.Threading.Thread.Sleep 1
            if !enabled then
                let positions = Array.init !ct (constF V4f.IIII)
                let ptr = pool.Alloc( IndexedGeometry( Mode = IndexedGeometryMode.PointList, IndexedAttributes = SymDict.ofList [ DefaultSemantic.Positions, positions :> System.Array; DefaultSemantic.Colors, positions :> System.Array ] ) )
                lock fu ( fun _ -> d.Add ptr )
                GC.Collect(System.Int32.MaxValue, GCCollectionMode.Forced, true)
                i <- i+1
                Log.line "i#%d : added %d vals" i (!ct * (d.Count))
        } |> Async.Start
        
    match pool.TryGetBufferView(DefaultSemantic.Positions), pool.TryGetBufferView(DefaultSemantic.Colors) with
    | Some pos, Some col -> 
        let sg = Sg.draw IndexedGeometryMode.PointList 
                    |> Sg.vertexBuffer DefaultSemantic.Positions pos
                    |> Sg.vertexBuffer DefaultSemantic.Colors col
                    |> Sg.shader {
                            do! DefaultSurfaces.trafo
                        }
        
        win.RenderTask <- runtime.CompileRender(win.FramebufferSignature,sg)
    | _ -> Log.error "fail"; ()


   

    win.Run()

    ()


[<EntryPoint>]
let main argv = 
 
    Ag.initialize()
    Aardvark.Init()
       
    use app = new VulkanApplication(true)

    let win = app.CreateSimpleRenderWindow()

    let projTrafo = 
        win.Sizes 
            |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 50.0 (float s.X / float s.Y))
            |> Mod.map Frustum.projTrafo

    let viewTrafo = 
        CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
            |> DefaultCameraController.control win.Mouse win.Keyboard win.Time
            |> Mod.map CameraView.viewTrafo

    let rotor =
        let sw = System.Diagnostics.Stopwatch.StartNew()
        win.Time |> Mod.map (fun _ ->
            Trafo3d.RotationZ sw.Elapsed.TotalSeconds
        )

    let viewTrafo =
        Mod.map2 (*) rotor viewTrafo

    let realObjects = CSet.empty

    let addThing(pos : V3d) =
        let sphere = IndexedGeometryPrimitives.solidPhiThetaSphere (Sphere3d(V3d.Zero, 0.1)) 32 C4b.Red
        let sg =
            Sg.ofIndexedGeometry sphere
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.simpleLighting
                }
                |> Sg.translate pos.X pos.Y pos.Z
                |> Sg.viewTrafo viewTrafo
                |> Sg.projTrafo projTrafo
                //|> Sg.fillMode (Mod.constant FillMode.Line)


        let objects = sg.RenderObjects() |> ASet.toList
    

        for o in objects do
            let test = app.Runtime.ResourceManager.PrepareRenderObjectAsync(unbox win.FramebufferSignature, o, id)
            test.ContinueWith(fun (t : Task<PreparedMultiRenderObject>) ->
            
                transact (fun () -> realObjects.UnionWith (Seq.cast t.Result.Children))
            ) |> ignore

    addThing V3d.Zero


    let rand = RandomSystem()
    let box = Box3d(-V3d.III * 5.0, V3d.III * 5.0)
    win.Keyboard.DownWithRepeats.Values.Add (fun k ->
        if k = Keys.Space then
            for i in 1 .. 20 do
                Task.Factory.StartNew(fun () ->
                    box |> rand.UniformV3d |> addThing
                ) |> (fun _ -> ()) |> id
    )


    let task = app.Runtime.CompileRender(win.FramebufferSignature, BackendConfiguration.Default, realObjects)

    win.RenderTask <- task
    win.Run()
    Environment.Exit 0




    //Rendering.Examples.NullBufferTest.run() |> ignore

//    
//
//    let a = RangeSet.Empty.Add(Range1l(0L, 10L)).Add(Range1l(100L, 1000L)).Add(Range1l(12L, 98L))
//    let b = a.Remove(Range1l(50L, 100L))
//    printfn "%A" a
//    printfn "%A" b
//
//    printf "contained: "
//    for i in 0 .. 10000 do
//        let c = b.Contains (int64 i)
//        if c then printf "%d " i
//
//    printfn ""
//
//    System.Environment.Exit 0




    


    Scratch.TPL.run()

//    App.Config <- { BackendConfiguration.Default with useDebugOutput = true }
//    App.run(quadTexture())

    0 // return an integer exit code

