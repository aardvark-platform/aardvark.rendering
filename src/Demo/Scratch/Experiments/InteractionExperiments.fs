#if INTERACTIVE
#I @"../../../../bin/Debug"
#I @"../../../../bin/Release"
#load "LoadReferences.fsx"
#else
namespace Examples
#endif

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.Interactive

open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application

module Controllers = 

    FsiSetup.initFsi (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; ".."; "bin";"Debug";"Examples.exe"])

    let win = Interactive.Window

    type Kind = Move | Down

    type PickOperation<'msg> = Kind -> Option<'msg>
    
    module Pick =
        let ignore = []

    type Primitive = 
        | Quad        of Quad3d
        | Sphere      of Sphere3d

    type Axis = X | Y | Z
    type Msg = Hover of Axis | Cleared | Translate of Axis

    type Scene<'msg> = 
        | Transform of Trafo3d * seq<Scene<'msg>>
        | Colored   of C4b     * seq<Scene<'msg>>
        | Render    of list<PickOperation<'msg>> * Primitive 
        | Group     of seq<Scene<'msg>>

    let colored     = curry Colored 
    let transformed = curry Transform 
    let translate x y z  = transformed ( Trafo3d.Translation(x,y,z) )
    let render      = curry Render

    type Model = {
        hovered   : Option<Axis>
        lastClick : Option<PixelPosition>
        trafo     : Trafo3d
    }

    let on (ref : Kind) (action : Msg) (k : Kind) =
        if ref = k then Some action else None

    let dir (a : Axis) =
        match a with 
            | X -> V3d.XAxis | Y -> V3d.YAxis | Z -> V3d.ZAxis

    let update (m : Model) (a : Msg) =
        match a with
            | Cleared ->  { m with hovered = None }
            | Hover v ->  { m with hovered = Some v}
            | Translate d -> { m with trafo = m.trafo * Trafo3d.Translation(dir d * 0.1) }


    let view (m : Model) =
        let sphereHandle = Sphere3d(V3d.Zero, 0.1)

        let ifHit (a : Axis) (selection : C4b) (defaultColor : C4b) =
            match m.hovered with
                | Some v when v = a -> selection
                | _ -> defaultColor

        transformed m.trafo [
                translate 1.0 0.0 0.0 [
                    [sphereHandle |> Sphere |> render [on Move (Hover X); on Down (Translate X)]] 
                        |> colored (ifHit X C4b.White C4b.DarkRed)
                ]
                translate 0.0 1.0 0.0 [
                    [sphereHandle |> Sphere |> render [on Move (Hover Y); on Down (Translate Y)]] 
                        |> colored (ifHit Y C4b.White C4b.DarkBlue)
                ]
                translate 0.0 0.0 1.0 [
                    [sphereHandle |> Sphere |> render [on Move (Hover Z); on Down (Translate Z)]] 
                        |> colored (ifHit Z C4b.White C4b.DarkGreen)
                ]
                translate 0.0 0.0 0.0 [
                    [sphereHandle |> Sphere |> render Pick.ignore] |> colored C4b.Gray
                ]
        ]


    type State = { trafo : Trafo3d; color : C4b }

    let toSg (scene : Scene<'msg>) : ISg = 
        let rec toSg (s : State) (scene : Scene<'msg>) =
            match scene with
                | Transform(t,children) -> 
                    children |> Seq.map ( toSg { s with trafo = s.trafo * t } ) |> Sg.group'
                | Colored(c,children) ->
                    children |> Seq.map ( toSg { s with color = c } ) |> Sg.group'
                | Render (_, Quad q) -> 
                    let geom = 
                        IndexedGeometry(
                            Mode = IndexedGeometryMode.TriangleList,
                            IndexedAttributes = SymDict.ofList [ 
                                DefaultSemantic.Positions,  q.Points |> Seq.toArray   :> System.Array 
                                DefaultSemantic.Colors,     Array.replicate 4 s.color :> System.Array
                            ]
                        )
                    Sg.ofIndexedGeometry geom
                | Render (_, Sphere g) -> 
                    let ig = IndexedGeometryPrimitives.solidSubdivisionSphere g 5 s.color
                    //ig.IndexedAttributes.[DefaultSemantic.Colors] <- (Array.replicate 4 s.color :> System.Array)
                    //Sg.sphere' 3 s.color g.Radius 
                    // TODO: fix both non working ways
                    ig
                     |> Sg.ofIndexedGeometry
                     |> Sg.transform (Trafo3d.Translation(g.Center)) 
                     |> Sg.transform s.trafo
                | Group xs -> xs |> Seq.map ( toSg s) |> Sg.group'
        toSg { trafo = Trafo3d.Identity; color = C4b.White } scene

    let pick (r : Ray3d) (s : Scene<'msg>)  =
        let rec go (state : State) s = 
            match s with    
                | Group xs -> xs |> Seq.toList |>  List.collect (go state)
                | Transform(t,xs) -> xs |> Seq.toList |> List.collect (go { state with trafo = state.trafo * t })
                | Colored(_,xs) -> xs |> Seq.toList |> List.collect (go state)
                | Render(action,Sphere s) ->    
                    let s2 = state.trafo.Forward.TransformPos(s.Center)
                    let mutable ha = RayHit3d.MaxRange
                    if r.HitsSphere(s2,s.Radius,0.0,Double.PositiveInfinity, &ha) then
                        [ha.T, action]
                    else []
                | _ -> failwith ""
        match s |> go { trafo = Trafo3d.Identity; color = C4b.White } with
            | [] -> None
            | xs -> xs |>  List.sortBy fst |> Some

    let cameraView = 
        CameraView.lookAt (V3d(6.0, 6.0, 6.0)) V3d.Zero V3d.OOI
            |> DefaultCameraController.control win.Mouse win.Keyboard win.Time

    let frustum = 
        win.Sizes 
            |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y))

    let camera = Mod.map2 Camera.create cameraView frustum

    let mutable model = { hovered = None; lastClick = None; trafo = Trafo3d.Identity }
    let scene = Mod.init (view model)
    let sg = scene |> Mod.map toSg |> Sg.dynamic

    win.Mouse.Move.Values.Subscribe(fun (oldP,newP) -> 
        let ray = newP |> Camera.pickRay (camera |> Mod.force)
        match pick ray (Mod.force scene) with
            | Some ((d,f)::_) -> 
                for msg in f do
                    match msg Move with
                        | Some r -> model <- update model r
                        | _ -> ()
            | _ -> 
                model <- update model Cleared
        transact (fun _ -> scene.Value <- view model) 
    ) |> ignore

    win.Mouse.Down.Values.Subscribe(fun p -> 
        let ray = win.Mouse.Position |> Mod.force |> Camera.pickRay (camera |> Mod.force)
        match pick ray (Mod.force scene) with
            | Some ((d,f)::_) -> 
                for msg in f do
                    match msg Down with
                        | Some r -> 
                            printfn "%A" r
                            model <- update model r
                        | _ -> ()
            | _ -> 
                model <- update model Cleared
        transact (fun _ -> scene.Value <- view model) 
    ) |> ignore

    let s = 
        Interactive.Window.Runtime.PrepareEffect [
                DefaultSurfaces.trafo |> toEffect       
                DefaultSurfaces.vertexColor |> toEffect 
        ] 

    let fullScene =
//        sg |> Sg.effect [
//                DefaultSurfaces.trafo |> toEffect       
//                DefaultSurfaces.vertexColor |> toEffect 
//                ]
          sg 
            |> Sg.andAlso (Sg.box' C4b.White Box3d.Unit |> Sg.translate 10000.0 10000.0 1000.0)
            |> Sg.surface( s :> ISurface |> Mod.constant)
            |> Sg.viewTrafo (Mod.map CameraView.viewTrafo cameraView)
            |> Sg.projTrafo (Mod.map Frustum.projTrafo frustum)

    let run () =
        FsiSetup.initFsi (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; ".."; "bin";"Debug";"Examples.exe"])
        Interactive.SceneGraph <- fullScene
        Interactive.RunMainLoop()

open Controllers

#if INTERACTIVE
Interactive.SceneGraph <- fullScene
printfn "Done. Modify sg and set Interactive.SceneGraph again in order to see the modified rendering results."
#else
#endif