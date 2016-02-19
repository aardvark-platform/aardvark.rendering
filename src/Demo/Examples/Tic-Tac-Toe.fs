#if INTERACTIVE
#I @"../../../bin/Debug"
#I @"../../../bin/Release"
#load "LoadReferences.fsx"
#else
namespace Examples
#endif


open System
open Aardvark.Base
    
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Rendering.Interactive

open Default // makes viewTrafo and other tutorial specicific default creators visible

module TicTacToe = 

    module Shaders = 
        open FShade
        open DefaultSurfaces

        let simpleLighting (v : Vertex) =
            fragment {
                let n = v.n |> Vec.normalize
                let c = uniform.CameraLocation - v.wp.XYZ |> Vec.normalize

                let ambient = 0.2
                let diffuse = Vec.dot c n |> abs

                let l = ambient + (1.0 - ambient) * diffuse

                return V4d(v.c.XYZ * diffuse, v.c.W)
            }

    Aardvark.Rendering.Interactive.FsiSetup.init (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug"])


    open Aardvark.Base.Incremental.Git

    let git = Aardvark.Base.Incremental.Git.init ()

    module PMod =
        let value (p : pmod<_>) = p.Value
        let modify f p =
            PMod.change p (f (value p))

    type Tick = X | O
    type Player = A | B
    type PlayerState = { cursor : V3i }
    type GameState = 
        { 
            player : Player
            arr    : pmod<Tick>[,,] 
            stateA : pmod<PlayerState>
            stateB : pmod<PlayerState>
        }

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module GameState = 
        let mapCursor f { cursor = cursor } = { cursor = f cursor }
        let empty = { 
            arr = Array3D.init 4 4 4 (fun x y z -> git.pmod (sprintf "%A" (x,y,z)) O)
            player = A
            stateA = git.pmod "playerA" { cursor = V3i.OOO }
            stateB = git.pmod "playerA" { cursor = V3i.OOO }
        }

    type Dir = Left | Right | Forward | Backward | Up | Down
    type Input = Move of Dir | Set
    
    let directions = Map.ofList [ Left, (fun v -> v - V3i.IOO); Right, (fun v -> v + V3i.IOO); Forward, (fun v -> v + V3i.OIO); Backward, (fun v -> v - V3i.OIO); Up, (fun v -> v + V3i.OOI); Down, (fun v -> v - V3i.OOI) ]
    let processInput (g : GameState) (i : Input) =
        let c = [
            match i with
             | Move d -> 
                let dir = Map.find d directions
                match g.player with 
                 | A -> yield PMod.modify (GameState.mapCursor dir) g.stateA 
                 | B -> yield PMod.modify (GameState.mapCursor dir) g.stateB
             | _ -> ()
        ]
        for c in c do git.apply c
        git.commit "blubber"
        g

    let mutable s = GameState.empty

    win.Keyboard.KeyDown(Keys.Up).Values.Subscribe(fun () -> 
        s <- processInput s (Move Up)
    ) |> ignore
    win.Keyboard.KeyDown(Keys.Down).Values.Subscribe(fun () -> 
        s <- processInput s (Move Down)
    ) |> ignore
    win.Keyboard.KeyDown(Keys.Right).Values.Subscribe(fun () -> 
        s <- processInput s (Move Right)
    ) |> ignore
    win.Keyboard.KeyDown(Keys.Left).Values.Subscribe(fun () -> 
        s <- processInput s (Move Left)
    ) |> ignore


    let cubeSg = Helpers.wireBox C4b.White Box3d.Unit |> Sg.ofIndexedGeometry
    let cube c = Helpers.box c Box3d.Unit |> Sg.ofIndexedGeometry

    let boxes = 
        [| for x in 0 .. 3 do
            for y in 0 .. 3 do
              for z in 0 .. 3 do
                let t = Trafo3d.Translation(float x ,float y ,float z) |> Mod.constant
                yield Sg.trafo t cubeSg |] |> Sg.group

    let cursor = 
        let toTrafo c = c.cursor |> V3d.op_Explicit |> Trafo3d.Translation
        Sg.group [ Sg.trafo (s.stateA |> Mod.map toTrafo) (cube C4b.Red)
                   Sg.trafo (s.stateA |> Mod.map toTrafo) (cube C4b.Red) ]

    let sg =
        boxes |> Sg.effect [
                DefaultSurfaces.trafo |> toEffect                   // compose shaders by using FShade composition.
                DefaultSurfaces.constantColor C4f.Red |> toEffect   // use standard trafo + map a constant color to each fragment
                Shaders.simpleLighting |> toEffect
                ]
            |> Sg.andAlso cursor 
            |> Sg.viewTrafo (viewTrafo   () |> Mod.map CameraView.viewTrafo )
            |> Sg.projTrafo (perspective () |> Mod.map Frustum.projTrafo    )


    let run () =
        Aardvark.Rendering.Interactive.FsiSetup.init (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug"])
        setSg sg
        win.Run()


open TicTacToe

#if INTERACTIVE
setSg sg
printfn "Done. Modify sg and call setSg again in order to see the modified rendering result."
#endif