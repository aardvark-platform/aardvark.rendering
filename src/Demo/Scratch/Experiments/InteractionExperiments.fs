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

module ImmutableSceneGraph = 

    type Kind = Move of V3d | Down of V3d

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Event =
        let move = function Move _ -> true | _ -> false
        let down = function Down _ -> true | _ -> false        
        let position = function Move s -> s | Down s -> s

    type PickOperation<'msg> = Kind -> Option<'msg>
    module Pick =
        let ignore = []

    type Primitive = 
        | Quad        of Quad3d
        | Sphere      of Sphere3d
        | Cone        of center : V3d * dir : V3d * height : float * radius : float
        | Cylinder    of center : V3d * dir : V3d * height : float * radius : float

    type Scene<'msg> = 
            | Transform of Trafo3d * seq<Scene<'msg>>
            | Colored   of C4b     * seq<Scene<'msg>>
            | Render    of list<PickOperation<'msg>> * Primitive 
            | Group     of seq<Scene<'msg>>

    let colored c xs  = Colored(c,xs)
    let transformed t xs = Transform(t,xs)
    let translate x y z xs  = transformed ( Trafo3d.Translation(x,y,z) ) xs
    let cylinder c d h r = Cylinder(c,d,h,r)
    let render pick g = Render(pick,g)


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
                | Render (_, Cone(center,dir,height,radius)) -> 
                    let ig = IndexedGeometryPrimitives.solidCone center dir height radius 10 s.color
                    ig
                     |> Sg.ofIndexedGeometry
                     |> Sg.transform s.trafo
                | Render (_, Cylinder(center,dir,height,radius)) -> 
                    let ig = IndexedGeometryPrimitives.solidCylinder center dir height radius radius 10 s.color
                    ig
                     |> Sg.ofIndexedGeometry
                     |> Sg.transform s.trafo
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
                | Render(action,Cone(center,dir,height,radius)) | Render(action,Cylinder(center,dir,height,radius)) -> 
                    let cylinder = Cylinder3d(state.trafo.Forward.TransformPos center,state.trafo.Forward.TransformPos (center+dir*height),radius)
                    let mutable ha = RayHit3d.MaxRange
                    if r.Hits(cylinder,0.0,Double.MaxValue,&ha) then
                        [ha.T, action]
                    else []
                | _ -> failwith ""
        match s |> go { trafo = Trafo3d.Identity; color = C4b.White } with
            | [] -> None
            | xs -> xs |>  List.sortBy fst |> Some

module Controllers = 

    open ImmutableSceneGraph

    FsiSetup.initFsi (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; ".."; "bin";"Debug";"Examples.exe"])

    let win = Interactive.Window

    type Axis = X | Y | Z

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Axis =
        let dir = function | X -> V3d.XAxis | Y -> V3d.YAxis | Z -> V3d.ZAxis
        let moveAxis = function
            | X -> Plane3d.YPlane
            | Y -> Plane3d.ZPlane
            | Z -> Plane3d.XPlane

    type Msg = 
        // hover overs
        | Hover           of Axis * V3d
        | NoHit       
        | MoveRay         of Ray3d
        // translations    
        | Translate       of Axis * V3d
        | EndTranslation 

    let hover      = curry Hover
    let translate_ = curry Translate
    let on  (p : Kind -> bool) (r : V3d -> 'msg) (k : Kind) = if p k then Some (r (Event.position k)) else None

    type Model = {
        hovered           : Option<Axis>
        activeTranslation : Option<Plane3d * V3d>
        trafo             : Trafo3d
    }


    let mutable model = { hovered = None; activeTranslation = None; trafo = Trafo3d.Identity }

    let update (m : Model) (a : Msg) =
        match a, m.activeTranslation with
            | NoHit, _             ->  { m with hovered = None; }
            | Hover (v,_), _       ->  { m with hovered = Some v}
            | Translate (dir,s), _ -> { m with activeTranslation = Some (Axis.moveAxis dir, s) }
            | EndTranslation, _    -> { m with activeTranslation = None; trafo = Trafo3d.Identity }
            | MoveRay r, Some (t,start) -> 
                let mutable ha = RayHit3d.MaxRange
                if r.HitsPlane(t,0.0,Double.MaxValue,&ha) then
                    { m with trafo = Trafo3d.Translation (ha.Point - start) }
                else m
            | MoveRay r, None -> m


    let view (m : Model) =
        let arrow dir = Cone(V3d.OOO,dir,0.3,0.1)

        let ifHit (a : Axis) (selection : C4b) (defaultColor : C4b) =
            match m.hovered with
                | Some v when v = a -> selection
                | _ -> defaultColor
            
        transformed m.trafo [
                translate 1.0 0.0 0.0 [
                    [ arrow V3d.IOO |> render [on Event.move (hover X); on Event.down (translate_ X)] ] 
                        |> colored (ifHit X C4b.White C4b.DarkRed)
                ]
                translate 0.0 1.0 0.0 [
                    [ arrow V3d.OIO |> render [on Event.move (hover Y); on Event.down (translate_ Y)] ] 
                        |> colored (ifHit Y C4b.White C4b.DarkBlue)
                ]
                translate 0.0 0.0 1.0 [
                    [ arrow V3d.OOI |> render [on Event.move (hover Z); on Event.down (translate_ Z)] ] 
                        |> colored (ifHit Z C4b.White C4b.DarkGreen)
                ]

                [ cylinder V3d.OOO V3d.IOO 1.0 0.05 |> render [ on Event.move (hover X); on Event.down (translate_ X) ] ] |> colored (ifHit X C4b.White C4b.DarkRed)
                [ cylinder V3d.OOO V3d.OIO 1.0 0.05 |> render [ on Event.move (hover Y); on Event.down (translate_ Y) ] ] |> colored (ifHit Y C4b.White C4b.DarkBlue)
                [ cylinder V3d.OOO V3d.OOI 1.0 0.05 |> render [ on Event.move (hover Z); on Event.down (translate_ Z) ] ] |> colored (ifHit Z C4b.White C4b.DarkGreen)
                
                translate 0.0 0.0 0.0 [
                    [Sphere3d(V3d.OOO,0.1) |> Sphere |> render Pick.ignore] |> colored C4b.Gray
                ]
        ]

    let cameraView = 
        CameraView.lookAt (V3d(6.0, 6.0, 6.0)) V3d.Zero V3d.OOI
            |> Mod.constant
            //|> DefaultCameraController.control win.Mouse win.Keyboard win.Time

    let frustum = 
        win.Sizes 
            |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y))

    let camera = Mod.map2 Camera.create cameraView frustum

    let scene = Mod.init (view model)
    let sg = scene |> Mod.map toSg |> Sg.dynamic

    win.Mouse.Move.Values.Subscribe(fun (oldP,newP) -> 
        let ray = newP |> Camera.pickRay (camera |> Mod.force)
        match pick ray (Mod.force scene) with
            | Some ((d,f)::_) -> 
                for msg in f do
                    match msg (Move (ray.GetPointOnRay d)) with
                        | Some r -> model <- update model r
                        | _ -> ()
            | _ -> model <- update model NoHit
        model <- update model (MoveRay ray)
        transact (fun _ -> scene.Value <- view model) 
    ) |> ignore


    win.Mouse.Down.Values.Subscribe(fun p -> 
        let ray = win.Mouse.Position |> Mod.force |> Camera.pickRay (camera |> Mod.force)
        match pick ray (Mod.force scene) with
            | Some ((d,f)::_) -> 
                for msg in f do
                    match msg (Down (ray.GetPointOnRay d)) with
                        | Some r -> 
                            model <- update model r
                        | _ -> ()
            | _ -> 
                model <- update model NoHit
        transact (fun _ -> scene.Value <- view model) 
    ) |> ignore

    win.Mouse.Up.Values.Subscribe(fun p -> 
        model <- update model EndTranslation
        transact (fun _ -> scene.Value <- view model) 
    ) |> ignore

    let s = 
        Interactive.Window.Runtime.PrepareEffect [
                DefaultSurfaces.trafo |> toEffect       
                DefaultSurfaces.vertexColor |> toEffect
                DefaultSurfaces.simpleLighting |> toEffect 
        ] 

    let fullScene =

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