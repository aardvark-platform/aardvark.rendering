namespace Scratch

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.Interactive

open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application

open Scratch.DomainTypes

[<AutoOpen>]
module PickStuff = 
    type Kind = Move of V3d | Down of MouseButtons * V3d

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Event =
        let move = function Move _ -> true | _ -> false
        let down = function Down _ -> true | _ -> false       
        let down' p = function Down(p',_) when p = p' -> true | _ -> false 
        let position = function Move s -> s | Down(_, s) -> s

    type PickOperation<'msg> = Kind -> Option<'msg>

    module Pick =
        let ignore = []
        let map f (p : PickOperation<'a>) =
            fun k -> 
                match p k with
                    | Some r -> Some (f r)
                    | None -> None

    
    let on (p : Kind -> bool) (r : V3d -> 'msg) (k : Kind) = if p k then Some (r (Event.position k)) else None

    type MouseEvent = Down of MouseButtons | Move | Click of MouseButtons | Up of MouseButtons
    type NoPick = NoPick of MouseEvent * Ray3d

    module Primitives =

        type Primitive = 
            | Sphere      of Sphere3d
            | Cone        of center : V3d * dir : V3d * height : float * radius : float
            | Cylinder    of center : V3d * dir : V3d * height : float * radius : float
            | Quad        of Quad3d 

        let hitPrimitive (p : Primitive) (trafo : Trafo3d) (ray : Ray3d) action =
            let mutable ha = RayHit3d.MaxRange
            match p with
                | Sphere s -> 
                    let transformed = trafo.Forward.TransformPos(s.Center)
                    let mutable ha = RayHit3d.MaxRange
                    if ray.HitsSphere(transformed,s.Radius,0.0,Double.PositiveInfinity, &ha) then
                        [ha.T, action]
                    else []
                | Cone(center,dir,height,radius) | Cylinder(center,dir,height,radius) -> 
                    let cylinder = Cylinder3d(trafo.Forward.TransformPos center,trafo.Forward.TransformPos (center+dir*height),radius)
                    let mutable ha = RayHit3d.MaxRange
                    if ray.Hits(cylinder,0.0,Double.MaxValue,&ha) then
                        [ha.T, action]
                    else []
                | Quad q -> 
                    let transformed = Quad3d(q.Points |> Seq.map trafo.Forward.TransformPos)
                    if ray.HitsPlane(Plane3d.ZPlane,0.0,Double.MaxValue,&ha) then [ha.T, action]
                    else []

    module List =
        let updateAt i f xs =
            let rec work current xs =
                match xs with 
                    | x::xs -> 
                        if i = current then f x :: xs
                        else x :: work (current+1) xs
                    | [] -> []
            work 0 xs

module ImmutableSceneGraph = 

    open Primitives

    type Scene<'msg> = 
            | Transform of Trafo3d * seq<Scene<'msg>>
            | Colored   of C4b     * seq<Scene<'msg>>
            | Render    of list<PickOperation<'msg>> * Primitive 
            | Group     of seq<Scene<'msg>>

    module Scene =
        let rec map (f : 'a -> 'b) (s : Scene<'a>) =
            match s with
                | Transform(t,xs) -> Transform(t, Seq.map (map f) xs )
                | Colored(t,xs) -> Colored(t, Seq.map (map f) xs )
                | Render(picks,p) -> 
                    Render(picks |> List.map (Pick.map f), p)
                | Group xs -> xs |> Seq.map (map f) |> Group

    let colored c xs  = Colored(c,xs)
    let transformed t xs = Transform(t,xs)
    let transformed' t x = Transform(t,[x])
    let colored' c x = Colored(c,[x])
    let translate x y z xs  = transformed ( Trafo3d.Translation(x,y,z) ) xs
    let cylinder c d h r = Cylinder(c,d,h,r)
    let render pick g = Render(pick,g)
    let group xs = Group(xs)

    type State = { trafo : Trafo3d; color : C4b }

    let toSg (scene : Scene<'msg>) : ISg = 
        let rec toSg (s : State) (scene : Scene<'msg>) =
            match scene with
                | Transform(t,children) -> 
                    children |> Seq.map ( toSg { s with trafo = s.trafo * t } ) |> Sg.group'
                | Colored(c,children) ->
                    children |> Seq.map ( toSg { s with color = c } ) |> Sg.group'
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
                | Render(_, Quad(p)) ->
                    let vertices = p.Points |> Seq.map V3f |> Seq.toArray
                    let index = [| 0; 1; 2; 0; 2; 3 |]
                    let colors = Array.replicate vertices.Length s.color 
                    let normals = Array.replicate vertices.Length (p.Edge03.Cross(p.P2-p.P0)).Normalized
                    let ig = IndexedGeometry(IndexedGeometryMode.TriangleList, index, SymDict.ofList [DefaultSemantic.Positions, vertices :> Array; DefaultSemantic.Colors, colors :> System.Array; DefaultSemantic.Normals, normals :> System.Array], SymDict.empty)
                    ig
                     |> Sg.ofIndexedGeometry
                     |> Sg.transform s.trafo
                | Group xs -> xs |> Seq.map ( toSg s) |> Sg.group'

        toSg { trafo = Trafo3d.Identity; color = C4b.White } scene


    let pick (r : Ray3d) (s : Scene<'msg>)  =
        let rec go (state : State) s = 
            match s with    
                | Group xs -> xs |> Seq.toList |>  List.collect (go state)
                | Transform(t,xs) -> xs |> Seq.toList |> List.collect (go { state with trafo = state.trafo * t })
                | Colored(_,xs) -> xs |> Seq.toList |> List.collect (go state)
                | Render(action,p) -> 
                    Primitives.hitPrimitive p state.trafo r action
        match s |> go { trafo = Trafo3d.Identity; color = C4b.White } with
            | [] -> []
            | xs -> 
                xs |> List.filter (not << List.isEmpty << snd) |>  List.sortBy fst 

module AnotherSceneGraph =

    open ImmutableSceneGraph
    open Primitives

    type ISg<'msg> = inherit ISg

    let on (p : Kind -> bool) (r : V3d -> 'msg) (k : Kind) = if p k then Some (r (Event.position k)) else None

    let cylinder c d h r = Cylinder(c,d,h,r)

    type AbstractApplicator<'msg>(child : IMod<ISg<'msg>>) =
        interface IApplicator with
            member x.Child = child |> Mod.map (fun a -> a :> ISg)
        member x.Child = child
        new(s : ISg<'msg>) = AbstractApplicator<'msg>(Mod.constant s)

    type Group<'msg>(xs : aset<ISg<'msg>>) =
        interface ISg<'msg>
        interface IGroup with
            member x.Children = xs |> ASet.map (fun a -> a :> ISg)

        member x.Children = xs

        new(l : list<ISg<'msg>>) = Group<'msg>(ASet.ofList l)

    type Transform<'msg>(trafo : IMod<Trafo3d>, xs : list<ISg<'msg>>) =
        inherit Sg.TrafoApplicator(trafo, Group xs)
        interface ISg<'msg>

    type Colored<'msg>(color : IMod<C4b>, xs : list<ISg<'msg>>) =
        inherit AbstractApplicator<'msg>(Group xs)
        interface ISg<'msg>
        member x.Color = color

    type On<'msg>(picks : list<PickOperation<'msg>>, children : list<ISg<'msg>>) =
        inherit AbstractApplicator<'msg>(Group children)
        member x.PickOperations = picks
        interface ISg<'msg>

    type Leaf<'msg>(xs : Primitive) =
        interface ISg<'msg>
        member x.Primitive = xs


    open Aardvark.Base.Ag
    open Aardvark.SceneGraph.Semantics
    [<Semantic>]
    type LeafSemantics() =
        
        member x.InhColor(c : Colored<'msg>) =
            c.Child?InhColor <- c.Color

        member x.InColor(r : Root<ISg>) =
            r.Child?InhColor <- Mod.constant C4b.White

        member x.RenderObjects(l : Leaf<'msg>) =
            match l.Primitive with
                | Sphere s -> 
                    Sg.sphere 5 (l?InhColor) (Mod.constant s.Radius) |> Sg.transform (Trafo3d.Translation s.Center)
                    |> Semantic.renderObjects
                | Cone(c,d,h,r) -> 
                    l?InhColor |> Mod.map (fun (color : C4b) -> IndexedGeometryPrimitives.solidCone c d h r 10 color |> Sg.ofIndexedGeometry) 
                    |> Sg.dynamic
                    |> Semantic.renderObjects
                | Cylinder(c,d,h,r) ->
                    l?InhColor |> Mod.map (fun (color : C4b) -> IndexedGeometryPrimitives.solidCylinder c d h r r 10 color |> Sg.ofIndexedGeometry) 
                    |> Sg.dynamic
                    |> Semantic.renderObjects
                | Quad p -> 
                    let vertices = p.Points |> Seq.map V3f |> Seq.toArray
                    let index = [| 0; 1; 2; 0; 2; 3 |]
                    let colors = l?InhColor |> Mod.map  (fun c -> Array.replicate vertices.Length c)
                    let normals = Array.replicate vertices.Length (p.Edge03.Cross(p.P2-p.P0)).Normalized
                    let ig = IndexedGeometry(IndexedGeometryMode.TriangleList, index, SymDict.ofList [DefaultSemantic.Positions, vertices :> Array; DefaultSemantic.Normals, normals :> System.Array], SymDict.empty)
                    ig
                     |> Sg.ofIndexedGeometry
                     |> Sg.vertexAttribute DefaultSemantic.Colors colors
                     |> Semantic.renderObjects
      
      

    type PickObject<'msg> =
        {
            trafo : IMod<Trafo3d>
            primitive : Primitive
            actions : list<PickOperation<'msg>>
        }    

    [<AutoOpen>]
    module SgExt = 
        type ISg<'msg> with
            member x.PickObjects() : aset<PickObject<'msg>> = x?PickObjects()
            member x.PickOperations : list<PickOperation<'msg>> = x?PickOperations

    [<Semantic>]
    type PickingSemantics() =
    

        member x.PickOperations(o : Root<ISg<'msg>>) =
            o.Child?PickOperations <- List.empty<PickOperation<'msg>>

        member x.PickOperations(o : On<'msg>) =
            o.Child?PickOperations <- o.PickOperations



        member x.PickObjects(g : Group<'msg>) : aset<PickObject<'msg>> =
            g.Children |> ASet.collect (fun s -> s.PickObjects())

        member x.PickObjects(s : Transform<'msg>) : aset<PickObject<'msg>> =
            s.Child |> ASet.bind (fun c -> c?PickObjects())
               
        member x.PickObjects(c : AbstractApplicator<'msg>) : aset<PickObject<'msg>> =
            c.Child |> ASet.bind (fun c -> c?PickObjects())
                   
        member x.PickObjects(l : Leaf<'msg>)  : aset<PickObject<'msg>> =
            match l.PickOperations with
                | [] -> ASet.empty
                | ops -> 
                    ASet.single {
                        trafo = l.ModelTrafo
                        primitive = l.Primitive
                        actions = ops
                    }


    let transform t xs = Transform<'msg>(t,xs) :> ISg<'msg>
    let translate x y z xs = Transform<'msg>(Trafo3d.Translation(x,y,z) |> Mod.constant, xs) :> ISg<_>
    let translate' x y z c = Transform<'msg>(Trafo3d.Translation(x,y,z) |> Mod.constant, List.singleton c) :> ISg<_>
    let colored c xs = Colored<'msg>(c,xs) :> ISg<'msg>
    let colored' c x = Colored<'msg>(c,x |> List.singleton) :> ISg<'msg>
    let pick picks xs = On<'msg>(picks,xs) :> ISg<'msg>
    let group (xs : list<_>) = Group<'msg>(xs) :> ISg<'msg>
    let leaf x = Leaf<'msg>(x) :> ISg<'msg> 
    let render picks p = pick picks [leaf p]


module Elmish3D = 

    open ImmutableSceneGraph
    
    type App<'model,'msg,'view> =
        {
            initial   : 'model
            ofPickMsg : 'model -> NoPick  -> list<'msg>
            update    : 'model   -> 'msg -> 'model
            view      : 'model   -> 'view
        }

    let createApp (ctrl : IRenderControl) (camera : IMod<Camera>) (app : App<'model,'msg, Scene<'msg>>) = 
        let mutable model = app.initial
        let view = Mod.init (app.view model)
        let sceneGraph = view |> Mod.map ImmutableSceneGraph.toSg |> Sg.dynamic
        let mutable history = []

        let updateScene (m : 'model) shouldTrack =
            if shouldTrack then history <- m :: history
            let newView = app.view m
            transact (fun _ -> 
                view.Value <- newView
            )

        let updatePickMsg (m : NoPick) (model : 'model) =
            app.ofPickMsg model m |> List.fold app.update model

        let mutable down = false

        ctrl.Mouse.Move.Values.Subscribe(fun (oldP,newP) -> 
            let ray = newP |> Camera.pickRay (camera |> Mod.force) 
            model <- updatePickMsg (NoPick(MouseEvent.Move,ray)) model // wrong
            match pick ray view.Value with
                | (d,f)::_ -> 
                    for msg in f do
                        match msg (Kind.Move (ray.GetPointOnRay d)) with
                            | Some r -> model <- app.update model r
                            | _ -> ()
                | [] -> ()
            updateScene model false
        ) |> ignore

        ctrl.Mouse.Down.Values.Subscribe(fun p ->  
            down <- true
            let ray = ctrl.Mouse.Position |> Mod.force |> Camera.pickRay (camera |> Mod.force)
            match pick ray view.Value with
                | ((d,f)::_) -> 
                    for msg in f do
                        match msg (Kind.Down(p, ray.GetPointOnRay d)) with
                            | Some r -> 
                                model <- app.update model r
                            | _ -> ()
                | [] -> 
                    model <-  updatePickMsg (NoPick(MouseEvent.Click p, ray)) model
            updateScene model true
        ) |> ignore

        let mutable ctrlDown = false
        ctrl.Keyboard.KeyDown(Keys.LeftCtrl).Values.Subscribe(fun _ -> ctrlDown <- true) |> ignore
        ctrl.Keyboard.KeyUp(Keys.LeftCtrl).Values.Subscribe(fun _ -> ctrlDown <- false) |> ignore
        ctrl.Keyboard.KeyDown(Keys.Z).Values.Subscribe(fun _ -> 
            if ctrlDown then
                match history with
                    | x::xs -> model <- x; history <- xs; updateScene model false
                    | [] -> ()
        ) |> ignore
 
        ctrl.Mouse.Up.Values.Subscribe(fun p ->     
            down <- false
            let ray = ctrl.Mouse.Position |> Mod.force |> Camera.pickRay (camera |> Mod.force)
            model <- updatePickMsg (NoPick(MouseEvent.Up p, ray)) model
            updateScene model false
        ) |> ignore

        sceneGraph

module Elmish3DADaptive =

    open AnotherSceneGraph

    type App<'model,'mmodel,'msg,'view> =
        {
            initial   : 'model
            ofPickMsg : 'model  -> NoPick  -> list<'msg>
            update    : 'model  -> 'msg -> 'model
            view      : 'mmodel -> 'view
        }

    type Unpersist<'immut,'mut> =
        {
            unpersist : 'immut -> ReuseCache -> 'mut
            apply     : 'immut -> 'mut -> ReuseCache -> unit
        }


    let createAppAdaptive (keyboard : IKeyboard) (mouse : IMouse) (camera : IMod<Camera>) (unpersist : Unpersist<'model,'mmodel>) (app : App<'model,'mmodel,'msg, ISg<'msg>>) =

        let model = Mod.init app.initial

        let reuseCache = ReuseCache()
        let mmodel = unpersist.unpersist model.Value reuseCache

        let updateModel (m : 'model) =
            transact (fun () -> 
                model.Value <- m
                unpersist.apply m mmodel reuseCache
            )
        let view = app.view mmodel
        let pickObjects = view.PickObjects()
        let pickReader = pickObjects.GetReader()

        let pick (r : Ray3d) : list<float * list<PickOperation<'msg>>> =
            pickReader.GetDelta() |> ignore
            let picks =
                pickReader.Content 
                 |> Seq.toList 
                 |> List.collect (fun p -> 
                        Primitives.hitPrimitive p.primitive (Mod.force p.trafo) r p.actions
                    )
            picks

        let updatePickMsg (m : NoPick) (model : 'model) =
            app.ofPickMsg model m |> List.fold app.update model

        let mutable down = false

        mouse.Move.Values.Subscribe(fun (oldP,newP) -> 
            let ray = newP |> Camera.pickRay (camera |> Mod.force) 
            let mutable model = updatePickMsg (NoPick(MouseEvent.Move,ray)) model.Value // wrong
            match pick ray with
                | (d,f)::_ -> 
                    for msg in f do
                        match msg (Kind.Move (ray.GetPointOnRay d)) with
                            | Some r -> model <- app.update model r
                            | _ -> ()
                | [] -> ()
            updateModel model 
        ) |> ignore

        mouse.Down.Values.Subscribe(fun p ->  
            down <- true
            let ray = mouse.Position |> Mod.force |> Camera.pickRay (camera |> Mod.force)
            let mutable model = model.Value
            match pick ray with
                | ((d,f)::_) -> 
                    for msg in f do
                        match msg (Kind.Down(p, ray.GetPointOnRay d)) with
                            | Some r -> 
                                model <- app.update model r
                            | _ -> ()
                | [] -> 
                    model <- updatePickMsg (NoPick(MouseEvent.Click p, ray)) model
            updateModel model 
        ) |> ignore
 
        mouse.Up.Values.Subscribe(fun p ->     
            down <- false
            let ray = mouse.Position |> Mod.force |> Camera.pickRay (camera |> Mod.force)
            let model = updatePickMsg (NoPick(MouseEvent.Up p, ray)) model.Value
            updateModel model 
        ) |> ignore

        Sg.adapter view


module TranslateController =

    open ImmutableSceneGraph
    open Primitives

    open Elmish3D
    open TranslateController

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Axis =
        let dir = function | X -> V3d.XAxis | Y -> V3d.YAxis | Z -> V3d.ZAxis
        let moveAxis = function
            | X -> Plane3d.YPlane
            | Y -> Plane3d.ZPlane
            | Z -> Plane3d.XPlane

    type Action = 

        // hover overs
        | Hover           of Axis * V3d
        | NoHit       
        | MoveRay         of Ray3d

        // translations    
        | Translate       of Axis * V3d
        | EndTranslation 

    let hasEnded a =
        match a with
            | EndTranslation -> true
            | _ -> false

    let hover      = curry Hover
    let translate_ = curry Translate

    let initial = { hovered = None; activeTranslation = None; trafo = Trafo3d.Identity; _id = null }

    let update (m : Model) (a : Action) =
        match a, m.activeTranslation with
            | NoHit, _             ->  { m with hovered = None; }
            | Hover (v,_), _       ->  { m with hovered = Some v}
            | Translate (dir,s), _ -> { m with activeTranslation = Some (Axis.moveAxis dir, s) }
            | EndTranslation, _    -> { m with activeTranslation = None;  }
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
                    [ Sphere3d(V3d.OOO,0.1) |> Sphere |> render Pick.ignore ] |> colored C4b.Gray
                ]
        ]

    let view2 (m : MModel) =
        failwith ""


    let ofPickMsg model (NoPick(kind,ray)) =
        match kind with   
            | MouseEvent.Click _ | MouseEvent.Down _  -> [NoHit]
            | MouseEvent.Move when Option.isNone model.activeTranslation ->
                    [NoHit; MoveRay ray]
            | MouseEvent.Move ->  [MoveRay ray]
            | MouseEvent.Up _   -> [EndTranslation]

    let app  = 
        {
            initial = initial
            update = update
            ofPickMsg = ofPickMsg
            view = view
        }

module TranslateController2 =

    open AnotherSceneGraph
    open Elmish3DADaptive
    open TranslateController
    open Primitives

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Axis =
        let dir = function | X -> V3d.XAxis | Y -> V3d.YAxis | Z -> V3d.ZAxis
        let moveAxis = function
            | X -> Plane3d.YPlane
            | Y -> Plane3d.ZPlane
            | Z -> Plane3d.XPlane

    type Action = 

        // hover overs
        | Hover           of Axis * V3d
        | NoHit       
        | MoveRay         of Ray3d

        // translations    
        | Translate       of Axis * V3d
        | EndTranslation 

    let hasEnded a =
        match a with
            | EndTranslation -> true
            | _ -> false

    let hover      = curry Hover
    let translate_ = curry Translate

    let initial = { hovered = None; activeTranslation = None; trafo = Trafo3d.Identity; _id = null }

    let update (m : Model) (a : Action) =
        match a, m.activeTranslation with
            | NoHit, _             ->  { m with hovered = None; }
            | Hover (v,_), _       ->  { m with hovered = Some v}
            | Translate (dir,s), _ -> { m with activeTranslation = Some (Axis.moveAxis dir, s) }
            | EndTranslation, _    -> { m with activeTranslation = None;  }
            | MoveRay r, Some (t,start) -> 
                let mutable ha = RayHit3d.MaxRange
                if r.HitsPlane(t,0.0,Double.MaxValue,&ha) then
                    { m with trafo = Trafo3d.Translation (ha.Point - start) }
                else m
            | MoveRay r, None -> m

    let view (m : MModel) =
        let arrow dir = Cone(V3d.OOO,dir,0.3,0.1)

        let ifHit (a : Axis) (selection : C4b) (defaultColor : C4b) =
            adaptive {
                let! hovered = m.mhovered
                match hovered with
                    | Some v when v = a -> return selection
                    | _ -> return defaultColor
            }
            
        transform m.mtrafo [
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
                    [ Sphere3d(V3d.OOO,0.1) |> Sphere |> render Pick.ignore ] |> colored (Mod.constant C4b.Gray)
                ]
        ]

    let ofPickMsg model (NoPick(kind,ray)) =
        match kind with   
            | MouseEvent.Click _ | MouseEvent.Down _  -> [NoHit]
            | MouseEvent.Move when Option.isNone model.activeTranslation ->
                    [NoHit; MoveRay ray]
            | MouseEvent.Move ->  [MoveRay ray]
            | MouseEvent.Up _   -> [EndTranslation]

    let app = {
        initial = initial
        update = update
        view = view 
        ofPickMsg = ofPickMsg
    }

module SimpleDrawingApp =

    open ImmutableSceneGraph
    open Elmish3D
    open SimpleDrawingApp
    open Primitives


    type Action =
        | ClosePolygon
        | AddPoint   of V3d
        | MoveCursor of V3d

    let update (m : Model) (cmd : Action) =
        match cmd with
            | ClosePolygon -> 
                match m.working with
                    | None -> m
                    | Some p -> 
                        { m with 
                            working = None 
                            finished = p.finishedPoints :: m.finished
                        }
            | AddPoint p ->
                match m.working with
                    | None -> { m with working = Some { finishedPoints = [ p ]; cursor = None;  }}
                    | Some v -> 
                        { m with working = Some { v with finishedPoints = p :: v.finishedPoints }}
            | MoveCursor p ->
                match m.working with
                    | None -> { m with working = Some { finishedPoints = []; cursor = Some p }}
                    | Some v -> { m with working = Some { v with cursor = Some p }}


    let viewPolygon (p : list<V3d>) =
        [ for edge in Polygon3d(p |> List.toSeq).EdgeLines do
            let v = edge.P1 - edge.P0
            yield cylinder edge.P0 v.Normalized v.Length 0.03 |> render Pick.ignore 
        ] |> group


    let view (m : Model) = 
        group [
            yield [ Quad (Quad3d [| V3d(-1,-1,0); V3d(1,-1,0); V3d(1,1,0); V3d(-1,1,0) |]) 
                        |> render [ 
                             on Event.move MoveCursor
                             on (Event.down' MouseButtons.Left)  AddPoint 
                             on (Event.down' MouseButtons.Right) (constF ClosePolygon)
                           ] 
                  ] |> colored C4b.Gray
            match m.working with
                | Some v when v.cursor.IsSome -> 
                    yield 
                        [ Sphere3d(V3d.OOO,0.1) |> Sphere |> render Pick.ignore ] 
                            |> colored C4b.Red
                            |> transformed' (Trafo3d.Translation(v.cursor.Value))
                    yield viewPolygon (v.cursor.Value :: v.finishedPoints)
                | _ -> ()
            for p in m.finished do yield viewPolygon p
        ]

    let initial = { finished = []; working = None; _id = null }

    let app =
        {
            initial = initial
            update = update
            view = view
            ofPickMsg = fun _ _ -> []
        }

module PlaceTransformObjects =

    open ImmutableSceneGraph
    open Elmish3D
    open Primitives

    open TranslateController
    open PlaceTransformObjects


    let initial =
        {
            objects = [ Trafo3d.Translation V3d.OOO; Trafo3d.Translation V3d.IOO; Trafo3d.Translation V3d.OIO ]
            hoveredObj = None
            selectedObj = None
            _id = null
        }

    type Action =
        | PlaceObject of V3d
        | SelectObject of int
        | HoverObject  of int
        | Unselect
        | TransformObject of int * TranslateController.Action

    let update (m : Model) (msg : Action) =
        match msg with
            | PlaceObject p -> { m with objects = (Trafo3d.Translation p) :: m.objects }
            | SelectObject i -> { m with selectedObj = Some (i, { TranslateController.app.initial with trafo = List.item i m.objects }) }
            | TransformObject(index,translation) ->
                match m.selectedObj with
                    | Some (i,tmodel) ->
                        let t = TranslateController.update tmodel translation
                        { m with 
                            selectedObj = Some (i,t)
                            objects = List.updateAt i (constF t.trafo) m.objects }
                    | _ -> m
            | HoverObject i -> { m with hoveredObj = Some i }
            | Unselect -> { m with selectedObj = None }

    let isSelected m i =
        match m.selectedObj with
            | Some (s,_) when s = i -> true
            | _ -> false

    let isHovered m i =
        match m.hoveredObj with
            | Some s when s = i -> true
            | _ -> false

    let viewObjects (m : Model) =
        m.objects |> List.mapi (fun i o -> 
            Sphere3d(V3d.OOO,0.1) 
               |> Sphere 
               |> render [
                    yield on Event.move (constF (HoverObject i))
                    yield on (Event.down' MouseButtons.Middle) (constF Unselect)
                    if m.selectedObj.IsNone then 
                        yield on Event.down (constF (SelectObject i))
                   ]
               |> transformed' o 
               |> colored' (if isSelected m i then C4b.Red elif isHovered m i then C4b.Blue else C4b.Gray)
        )

    let view (m : Model) =
        [
            yield! viewObjects m
            yield 
                Quad (Quad3d [| V3d(-1,-1,0); V3d(1,-1,0); V3d(1,1,0); V3d(-1,1,0) |]) 
                 |> render [ 
                        on (Event.down' MouseButtons.Right) PlaceObject 
                    ] 
                 |> colored' C4b.Gray
            match m.selectedObj with
                | None -> ()
                | Some (i,inner) -> 
                    yield TranslateController.view inner |> Scene.map (fun a -> TransformObject(i,a))
        ] |> group

    let app =
        {
            initial = initial
            update = update
            view = view
            ofPickMsg = 
                fun m (NoPick(me,r)) -> 
                    match m.selectedObj with
                        | None -> []
                        | Some (i,inner) -> 
                            match me with
                                | MouseEvent.Click MouseButtons.Middle -> [Unselect]
                                | _ -> TranslateController.ofPickMsg inner (NoPick(me,r)) |> List.map (fun a -> TransformObject(i,a))
        }
        

module InteractionExperiments = 

    open AnotherSceneGraph
    open Elmish3DADaptive

    [<Demo>]
    let demo () =

        let cameraView = 
            CameraView.lookAt (V3d(6.0, 6.0, 6.0)) V3d.Zero V3d.OOI
                |> Mod.constant
                //|> DefaultCameraController.control win.Mouse win.Keyboard win.Time

        let frustum = 
            App.Size
                |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y))

        let camera = Mod.map2 Camera.create cameraView frustum

        let unpersist : Elmish3DADaptive.Unpersist<TranslateController.Model,_> = { unpersist = (fun immut scope -> immut.ToMod(scope)); apply = (fun immut mut scope -> mut.Apply(immut,scope))}
        let adaptiveResult = Elmish3DADaptive.createAppAdaptive App.Keyboard App.Mouse camera unpersist TranslateController2.app

        let sg = 
            //Elmish3D.createApp win camera TranslateController.app
            //Elmish3D.createApp win camera SimpleDrawingApp.app
            //Elmish3D.createApp win camera PlaceTransformObjects.app
            adaptiveResult
            //view :> ISg

        let fullScene =
              sg 
                |> Sg.andAlso (Sg.box' C4b.White Box3d.Unit |> Sg.translate 10000.0 10000.0 1000.0)
                |> Sg.effect [
                    DefaultSurfaces.trafo |> toEffect       
                    DefaultSurfaces.vertexColor |> toEffect
                    DefaultSurfaces.simpleLighting |> toEffect 
                   ] 
                |> Sg.viewTrafo (Mod.map CameraView.viewTrafo cameraView)
                |> Sg.projTrafo (Mod.map Frustum.projTrafo frustum)


        App.Runtime.CompileRender(App.FramebufferSignature, fullScene)