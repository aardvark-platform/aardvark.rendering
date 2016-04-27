#nowarn "77"
#if INTERACTIVE
#I @"../../../bin/Debug"
#I @"../../../bin/Release"
#load "LoadReferences.fsx"
#else
namespace Examples
#endif


open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.Interactive

open Default 

open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application

module Polygons = 

    FsiSetup.initFsi (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug";"Examples.exe"])

    module Scope =
        open System.Threading

        type Scope = string
        type Context = list<Scope>
        let current = new ThreadLocal<Context>(fun _ -> [])

        let scoped n f =
            let old = current.Value
            current.Value <- n :: current.Value
            let r = f ()
            current.Value <- old
            r

        
        type Unique(seed : int, prefix : string) =
            let mutable current = seed
            member x.Fresh() =
                let freshId = sprintf "%s%d" prefix current
                current <- current + 1
                freshId

        let uniqueScope (n : Unique) f =
            let fresh = n.Fresh()
            scoped fresh (fun () -> f fresh)

        let empty () = 
            let prefix = current.Value |> String.concat ""
            Unique(0, prefix)
        let fesh (s : Unique) = s.Fresh() 
        
        let pmod (git : WorkingCopy) (name : string) v =
            git.pmod (sprintf "%s.%s" (current.Value |> String.concat "") name) v
            
        let pset (git : WorkingCopy) (name : string) =
            git.pset (sprintf "%s.%s" (current.Value |> String.concat "") name)

    module PMod =
        let value (m : pmod<'a>) = m.Value

    module ModInternal =
        let getValue (x : IMod<_>) caller = x.GetValue(caller)

    module ASet =
        let getReader (x : aset<_>) = x.GetReader()

    module ASetReader =
        let getDelta (x : IReader<_>) = x.GetDelta()

    [<AutoOpen>]
    module GenericConvert =
        let inline conv< ^a, ^b when (^a or ^b) : (static member op_Explicit : ^a -> ^b)> (i : ^a) =
            ((^a or ^b) : (static member op_Explicit : ^a -> ^b) (i))


    [<AutoOpen>]
    module Interaction =
        type Operation = AddPoint of V3f 
                       | ClosePolygon 
                       | MovePoint of V3f
                       | ToggleMoving

    module Logics =
        let private git = Git.init () // git should be private, i.e. all modification should go via Logics module
        let unsafeGit = git

        let (<~) (p : pmod<'a>) v = PMod.change p v
        
        type Polygon  = pmod<list<pmod<V3f>>>
        type Polygons = pset<Polygon>

        type Scene = { polygons : Polygons }

        type State = { polygon : pmod<list<V3f>>; unique : Scope.Unique; 
                       hoverPosition : IModRef<Option<Polygon * pmod<V3f>>>; 
                       selectedPoint : ref<Option<pmod<V3f>>>
                       dragging      : ref<bool> }

        type Logics = Scene * State

        let createScene () = { polygons = Scope.pset git "polygons" }

        let createWorkingState () = 
            { polygon        = Scope.pmod git "workingPolygon" []; 
              unique         = Scope.empty ()
              hoverPosition  = Mod.init None
              selectedPoint  = ref None
              dragging       = ref false
            }

            
        let interact ((scene,state) : Logics) (commit : bool)  (op : Operation) =
            let changes =
                [
                    match op with
                        | AddPoint p   -> 
                            yield state.polygon <~ p :: state.polygon.Value
                        | ClosePolygon ->
                            let closedPolygon = state.polygon.Value |> List.toArray
                            if closedPolygon.Length > 0 then
                                let newPolygon = 
                                    Scope.uniqueScope state.unique (fun scope ->
                                        state.polygon.Value 
                                            |> List.mapi (fun i v -> Scope.pmod git (sprintf "%d" i) v) 
                                            |> List.rev
                                            |> Scope.pmod git scope
                                )
                                yield PSet.add scene.polygons newPolygon
                            yield state.polygon <~ []
                        | MovePoint (newPos) when !state.dragging  ->
                            yield (!state.selectedPoint).Value <~ newPos
                        | ToggleMoving when !state.dragging ->
                            state.dragging := false
                            state.selectedPoint := None
                        | ToggleMoving when not !state.dragging && state.hoverPosition.Value.IsSome ->
                            state.dragging := true
                            state.selectedPoint := Some (snd state.hoverPosition.Value.Value)
                        | ToggleMoving | MovePoint _ -> ()
                ]
            for c in changes do git.apply c
            let action = sprintf "%A" op

            if commit then
                git.commit action
                Git2Dgml.visualizeHistory (Path.combine [ __SOURCE_DIRECTORY__; "polygons.dgml" ]) (git.Branches |> Dictionary.toList) |> ignore

    module Picking =
        open Logics

        let pick ( (scene, state) : Logics ) =
            let polygons = scene.polygons |> ASet.getReader

            fun (camera : Camera) (current : PixelPosition) ->
                
                polygons |> ASetReader.getDelta |> ignore
                let changes =
                    [
                        let pick = Camera.pickRay camera current
                        let nearbyRay = 
                            polygons.Content
                                |> Seq.collect (fun (p : Logics.Polygon) -> 
                                    [ for px in p.Value do 
                                        let mutable hit = RayHit3d.MaxRange
                                        let hitD = 
                                            if pick.HitsSphere(V3d.op_Explicit px.Value, 0.1, 0.1, 10.0, &hit) 
                                            then hit.T
                                            else Double.MaxValue
                                        yield p, px, hitD
                                    ] )
                                |> Seq.sortBy (fun (_,_,d) -> d)
                                |> Seq.toList
                        match nearbyRay with
                            | [] -> ()
                            | (p,bestPosition,d) :: _ when d < Double.MaxValue && not !state.dragging -> 
                                yield fun () -> Mod.change state.hoverPosition (Some (p, bestPosition)) 
                            | _ -> 
                                yield fun () -> Mod.change state.hoverPosition None 
                    ]

                transact (fun () -> 
                    for c in changes do c ()
                )

        let computePick (mousePosition : IMod<PixelPosition>) (camera : IMod<Camera>) =
            adaptive {
                let! camera        = camera
                let! pixelPosition = mousePosition
                let p = Camera.tryGetPickPointOnPlane camera (Plane3d(V3d.OOI,V3d.OOO)) pixelPosition
                return p
            }

    [<AutoOpen>]
    module View =

        let viewTrafo = viewTrafo ()
        let frustum = perspective ()
        let camera = Mod.map2 Camera.create viewTrafo frustum

        [<AutoOpen>]
        module Controller =
            let appState,workingState = 
                Scope.scoped "mainScene" (fun () -> 
                    Logics.createScene (), Logics.createWorkingState ()
                )

            let camPick     = Picking.computePick win.Mouse.Position camera
            let interactGit = Logics.interact (appState,workingState) true
            let interact    = Logics.interact (appState,workingState) false
            let pick = Picking.pick (appState,workingState) 
             
            let polygons = (appState.polygons.CSet :> aset<_>).GetReader()
            win.Mouse.Move.Values.Subscribe(fun (last,current) ->
                pick (Mod.force camera) current

                interact <| MovePoint (camPick |> Mod.force |> Option.get |> V3f.op_Explicit) 
            ) |> ignore

            win.Mouse.Click.Values.Subscribe(fun c ->
                match c with 
                 | MouseButtons.Left  -> 
                    interactGit <| (camPick |> Mod.force |> Option.map V3f.op_Explicit |> Option.get |> AddPoint)
                 | MouseButtons.Right -> 
                    interactGit <| ClosePolygon 
                 | MouseButtons.Middle -> 
                    interactGit <| ToggleMoving
                 | _ -> ()
            ) |> ignore


        [<AutoOpen>]
        module Visualization =
            let endPoint = camPick |> Mod.map (Option.map V3f.op_Explicit)

            let lineGeometry (color : C4b) (points : list<V3f>) (endPoint : Option<V3f>) =
                Helpers.lineLoopGeometry color (points |> List.append (endPoint |> Option.toList) |> List.toArray)

            let polygonVis =
                aset {
                    for p in appState.polygons do
                        let lines = 
                            Mod.custom (fun self -> 
                                let positions = ModInternal.getValue p self
                                let lines = 
                                    List.foldBack (fun p s -> ModInternal.getValue p self :: s) positions [] 
                                Helpers.lineLoopGeometry C4b.White (lines |> List.toArray)
                         )
                        yield Sg.dynamic lines
                }

            let scene =
                Mod.map2 (lineGeometry C4b.Red) workingState.polygon endPoint 
                    |> Sg.dynamic
                    |> Sg.andAlso (polygonVis |> Sg.set)


        let conditionally (size:float) color (m : IMod<_>) =
            Sphere.solidSphere color 5  |> Sg.trafo (Mod.constant <| Trafo3d.Scale size)
                |> Sg.trafo (m |> Mod.map (Option.defaultValue Trafo3d.Identity << Option.map Trafo3d.Translation))
                |> Sg.onOff (m |> Mod.map Option.isSome)    

        let pickSphere  = conditionally 0.040 C4b.Green camPick
        let hoverSphere = 
            conditionally 0.041 C4b.Yellow (Mod.map (Option.map (conv << PMod.value << snd)) workingState.hoverPosition)
        let groundPlane = Helpers.quad C4b.Gray

        let sg =
            groundPlane 
                |> Sg.andAlso pickSphere
                |> Sg.andAlso hoverSphere
                |> Sg.effect [
                    DefaultSurfaces.trafo |> toEffect                   
                    DefaultSurfaces.simpleLighting |> toEffect
                   ]
                |> Sg.andAlso ( 
                    scene  
                        |> Sg.uniform "LineWidth" (Mod.constant 5.0)
                        |> Sg.uniform "ViewportSize" (Mod.constant (V2i(800,600)))
                        |> Sg.effect [ 
                            DefaultSurfaces.trafo |> toEffect; 
                            DefaultSurfaces.constantColor C4f.Red |> toEffect
                            DefaultSurfaces.thickLine |> toEffect 
                           ] 
                   )
                |> Sg.camera camera

    let run () =
        FsiSetup.initFsi (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug";"Examples.exe"])

        setSg sg
        win.Run()

open Polygons
open Logics

module TestOperations =
    unsafeGit.merge "urdar"

#if INTERACTIVE
setSg sg
printfn "Done. Modify sg and call setSg again in order to see the modified rendering result."
#else
#endif
