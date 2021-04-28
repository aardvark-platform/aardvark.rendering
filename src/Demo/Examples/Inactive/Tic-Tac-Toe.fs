﻿#if INTERACTIVE
#I @"../../../bin/Debug"
#I @"../../../bin/Release"
#load "LoadReferences.fsx"
#else
namespace Examples
#endif
//
//
//open System
//open Aardvark.Base
//open Aardvark.Rendering
//    
//open FSharp.Data.Adaptive
//open Aardvark.SceneGraph
//open Aardvark.Application
//open Aardvark.Rendering.Interactive
//
//open Default // makes viewTrafo and other tutorial specicific default creators visible
//
//module TicTacToe = 
//
//    FsiSetup.initFsi (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug";"Examples.exe"])
//
//    module V3i =
//        let toTup (v : V3i) = (v.X, v.Y, v.Z)
//
//    open FSharp.Data.Adaptive.Git
//
//    let git = FSharp.Data.Adaptive.Git.init ()
//
//    module PMod =
//        let value (p : pmod<_>) = p.Value
//        let modify f p =
//            PAVal.change p (f (value p))
//
//    module Mod =
//        let value (p : IModRef<_>) = p.Value
//        let modify f p =
//            transact (fun () -> AVal.change p (f (value p)))
//
//    type Player = A | B
//    type Tick = Tick of Player | Free
//    type PlayerState = { cursor : V3i }
//    type GameState = 
//        { 
//            player : IModRef<Player>
//            arr    : pmod<Tick>[,,] 
//            stateA : IModRef<PlayerState>
//            stateB : IModRef<PlayerState>
//        }
//    type Dir = Left | Right | Forward | Backward | Up | Down
//    type Input = Move of Dir | Set
//
//    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
//    module GameState = 
//        let move v f old = 
//            let c : V3i = f old.cursor
//            if v c then { cursor = c }
//            else old
//
//        let empty = { 
//            arr = Array3D.init 4 4 4 (fun x y z -> git.pmod (sprintf "%A" (x,y,z)) Free)
//            player = AVal.init A
//            stateA = AVal.init { cursor = V3i.OOO }
//            stateB = AVal.init { cursor = V3i.OOO }
//        }
//    
//    let directions = Map.ofList [ Left, (fun v -> v - V3i.IOO); Right, (fun v -> v + V3i.IOO); Forward, (fun v -> v + V3i.OIO); Backward, (fun v -> v - V3i.OIO); Up, (fun v -> v + V3i.OOI); Down, (fun v -> v - V3i.OOI) ]
//
//    let flipPlayer (g : GameState) =
//        match g.player |> AVal.force with
//         | A -> AVal.change g.player B
//         | B -> AVal.change g.player A
//
//    let inField (c : V3i) = 
//        Box3i.FromMinAndSize(V3i.OOO,V3i.III * 3).Contains c
//
//    let visualizeHistory c = Git2Dgml.visualizeHistory (Path.combine [ __SOURCE_DIRECTORY__; "tictactoe.dgml" ])  c
//
//    let processInput (g : GameState) (i : Input) =
//        let player = AVal.force g.player
//        let c = [
//            match i with
//             | Move d -> 
//                let dir = Map.find d directions
//
//                let valid ( c : V3i ) = 
//                    inField c && (Array3D.get g.arr c.X c.Y c.Z).Value = Free
//
//                match player with 
//                 | A -> AVal.modify (GameState.move valid dir) g.stateA
//                 | B -> AVal.modify (GameState.move valid dir) g.stateB
//             
//             | Set ->
//                let pos,msg = 
//                    match player with
//                      | A -> g.stateA.Value.cursor, sprintf "A set on %A" g.stateA.Value.cursor
//                      | B -> g.stateB.Value.cursor, sprintf "B set on %A" g.stateA.Value.cursor
//                yield PAVal.change (Array3D.get g.arr pos.X pos.Y pos.Z) (Tick player),msg
//        ]
//        for (a,msg) in c do git.apply a
//        let msg = c |> Seq.toArray |> Array.map snd |> String.Concat
//        git.commit (sprintf "game: %s" msg)
//        
//        match i with 
//            | Set -> visualizeHistory (git.Branches |> Dictionary.toList) |> ignore 
//            | _ -> ()
//        if i = Set then transact (fun _ -> flipPlayer g) else ()
//
//    let s = GameState.empty
//
//    let keyBindings =
//        [ Keys.Up,    Move Up
//          Keys.Down,  Move Down 
//          Keys.Right, Move Left 
//          Keys.Left,  Move Right 
//          Keys.F,     Move Forward 
//          Keys.R,     Move Backward 
//          Keys.Space, Set
//        ] 
//
//    for (k,a) in keyBindings do
//        win.Keyboard.KeyDown(k).Values.Subscribe(fun () -> 
//            processInput s a
//        ) |> ignore
//
//    win.Keyboard.KeyDown(Keys.Z).Values.Subscribe(fun () -> 
//        printfn "undo"
//    ) |> ignore
//
//    let cubeSg = Helpers.wireBox C4b.White Box3d.Unit |> Sg.ofIndexedGeometry
//    let cube c = Helpers.box c Box3d.Unit |> Sg.ofIndexedGeometry
//    let cursorA = Helpers.wireBox C4b.Green Box3d.Unit |> Sg.ofIndexedGeometry
//    let cursorB = Helpers.wireBox C4b.Red Box3d.Unit |> Sg.ofIndexedGeometry
//    let markA = Sphere.solidSphere C4b.Green 5 |> Sg.trafo (AVal.constant <| Trafo3d.Scale 0.5 *  Trafo3d.Translation(0.5,0.5,0.5))
//    let markB = cube C4b.Red
//
//    let boxes = 
//        [|
//            for x in 0 .. 3 do
//             for y in 0 .. 3 do
//              for z in 0 .. 3 do
//                 let t = 
//                    Trafo3d.Translation(float x ,float y ,float z) |> AVal.constant
//                 yield 
//                    Array3D.get s.arr x y z
//                      |> AVal.map (function
//                              | Tick A -> Sg.trafo t markA
//                              | Tick B -> Sg.trafo t markB
//                              | Free -> Sg.trafo t cubeSg
//                          ) 
//                      |> Sg.dynamic
//        |] |> Sg.group
//
//    let cursor = 
//        let toTrafo c = c.cursor |> V3d.op_Explicit |> Trafo3d.Translation
//        let A = s.player |> AVal.map ((=)A)
//        let B = s.player |> AVal.map ((=)B)
//        Sg.group [ Sg.onOff A <| Sg.trafo (s.stateA |> AVal.map toTrafo) (cube C4b.Blue)
//                   Sg.onOff B <| Sg.trafo (s.stateB |> AVal.map toTrafo) (cube C4b.Red) ]
//
//    let viewTrafo = 
//        let view =  CameraView.LookAt(V3d(2, 12, 7), V3d(2,2,2), V3d.OOI)
//        AVal.integrate view win.Time 
//            [DefaultCameraController.controlOrbitAround win.Mouse 
//                (AVal.constant <| V3d(2,2,2))]
//
//    let sg =
//        boxes
//        |> Sg.andAlso cursor 
//        |> Sg.effect [
//                DefaultSurfaces.trafo |> toEffect            
//                DefaultSurfaces.simpleLighting |> toEffect
//                ]
//        |> Sg.andAlso cursor 
//        |> Sg.viewTrafo (viewTrafo      |> AVal.map CameraView.viewTrafo )
//        |> Sg.projTrafo (perspective () |> AVal.map Frustum.projTrafo    )
//
//
//    let run () =
//        FsiSetup.initFsi (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug";"Examples.exe"])
//        setSg sg
//        win.Run()
//
//
//open TicTacToe
//
//module SaveGames =
//    open System.Collections.Generic
//    let saveGames = List<_>()
//    let save s =
//        git.checkout s
//        saveGames.Add s
//
//    let load s = 
//        git.checkout s
//
//#if INTERACTIVE
//setSg sg
//printfn "Done. Modify sg and call setSg again in order to see the modified rendering result."
//#endif