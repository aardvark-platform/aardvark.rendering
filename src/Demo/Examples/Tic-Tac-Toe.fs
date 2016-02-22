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

    Aardvark.Rendering.Interactive.FsiSetup.init (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug"])

    module V3i =
        let toTup (v : V3i) = (v.X, v.Y, v.Z)

    open Aardvark.Base.Incremental.Git

    let git = Aardvark.Base.Incremental.Git.init ()


    module GitVis =
        open System.Text
        open System.IO
        open System.Collections.Generic
        
        let private properties = """
          <Properties>
            <Property Id="Background" Label="Background" DataType="Brush" />
            <Property Id="Label" Label="Label" DataType="String" />
            <Property Id="Size" DataType="String" />
            <Property Id="Start" DataType="DateTime" />
          </Properties>"""

        let rec genDgml (nodes : StringBuilder) (links : StringBuilder) (v : HashSet<_>) (h : Commit) =
            if Commit.isRoot h then ()
            else
                if v.Contains h then ()
                else
                    v.Add h |> ignore
                    nodes.AppendLine(sprintf "\t\t<Node Id=\"%A\" Label=\"%s\"/>" h.hash h.message) |> ignore
                    links.AppendLine(sprintf "\t\t<Link Source=\"%A\" Target=\"%A\"/>" h.hash h.parent.hash) |> ignore
                    genDgml nodes links v h.parent

        let visualizeHistory (fileName : string) (h : Commit) =
            let nodes = StringBuilder()
            let links = StringBuilder()
            genDgml nodes links (HashSet()) h 
            use file = new StreamWriter( fileName )
            file.WriteLine("<?xml version='1.0' encoding='utf-8'?>")
            file.WriteLine("<DirectedGraph xmlns=\"http://schemas.microsoft.com/vs/2009/dgml\">")
            file.WriteLine("<Nodes>")
            file.Write(nodes.ToString())
            file.WriteLine(@"</Nodes>")
            file.WriteLine("<Links>")
            file.Write(links.ToString())
            file.WriteLine("</Links>")
            file.WriteLine(properties)
            file.WriteLine("</DirectedGraph>")
            fileName


    module PMod =
        let value (p : pmod<_>) = p.Value
        let modify f p =
            PMod.change p (f (value p))

    type Player = A | B
    type Tick = X of Player | O
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
        let mapCursor f { cursor = cursor } = 
            let c : V3i = f cursor
            printfn "map to: %A" c
            { cursor = V3i(clamp 0 3 c.X, clamp 0 3 c.Y, clamp 0 3 c.Z) }
        let empty = { 
            arr = Array3D.init 4 4 4 (fun x y z -> git.pmod (sprintf "%A" (x,y,z)) O)
            player = A
            stateA = git.pmod "playerA" { cursor = V3i.OOO }
            stateB = git.pmod "playerA" { cursor = V3i.OOO }
        }

    type Dir = Left | Right | Forward | Backward | Up | Down
    type Input = Move of Dir | Set
    
    let directions = Map.ofList [ Left, (fun v -> v - V3i.IOO); Right, (fun v -> v + V3i.IOO); Forward, (fun v -> v + V3i.OIO); Backward, (fun v -> v - V3i.OIO); Up, (fun v -> v + V3i.OOI); Down, (fun v -> v - V3i.OOI) ]

    let flipPlayer (g : GameState) =
        match g.player with
         | A -> { g with player = B }
         | B -> { g with player = A }

    let visualizeHistory = GitVis.visualizeHistory "history.dgml"

    let processInput (g : GameState) (i : Input) =
        let c = [
            match i with
             | Move d -> 
                let dir = Map.find d directions
                match g.player with 
                 | A -> yield PMod.modify (GameState.mapCursor dir) g.stateA, "a moved"
                 | B -> yield PMod.modify (GameState.mapCursor dir) g.stateB, "b moved"
             | Set ->
                let (x,y,z),msg = 
                    match g.player with
                      | A -> g.stateA.Value.cursor |> V3i.toTup, sprintf "A set on %A" g.stateA.Value.cursor
                      | B -> g.stateB.Value.cursor |> V3i.toTup, sprintf "B set on %A" g.stateA.Value.cursor
                yield PMod.change (Array3D.get g.arr x y z) (X g.player),msg
        ]
        for (a,msg) in c do git.apply a
        let msg = c |> Seq.toArray |> Array.map snd |> String.Concat
        git.commit (sprintf "game: %s" msg)
        match i with 
            | Set -> visualizeHistory git.History  |> ignore 
            | _ -> ()
        if i = Set then flipPlayer g else g

    let mutable s = GameState.empty

    let keyBindings =
        [ Keys.Up,    Move Up
          Keys.Down,  Move Down 
          Keys.Right, Move Right 
          Keys.Left,  Move Left 
          Keys.F,     Move Backward 
          Keys.R,     Move Forward 
          Keys.Space, Set
        ] 

    for (k,a) in keyBindings do
        win.Keyboard.KeyDown(k).Values.Subscribe(fun () -> 
            s <- processInput s a
        ) |> ignore

    win.Keyboard.KeyDown(Keys.Z).Values.Subscribe(fun () -> 
        printfn "undo"
    ) |> ignore

    let cubeSg = Helpers.wireBox C4b.White Box3d.Unit |> Sg.ofIndexedGeometry
    let cube c = Helpers.box c Box3d.Unit |> Sg.ofIndexedGeometry
    let markA = 
        Sphere.solidSphere C4b.Green 5 |> Sg.trafo (Mod.constant <| Trafo3d.Translation(0.5,0.5,0.5) * Trafo3d.Scale 0.5)
    let markB = cube C4b.Red

    let boxes = 
        [|
            for x in 0 .. 3 do
                for y in 0 .. 3 do
                    for z in 0 .. 3 do
                        let t = Trafo3d.Translation(float x ,float y ,float z) |> Mod.constant
                        yield Array3D.get s.arr x y z
                                |> Mod.map (fun n ->
                                        match n with 
                                         | X p -> match p with
                                                    | A -> Sg.trafo t markA
                                                    | b -> Sg.trafo t markB
                                         | O -> Sg.trafo t cubeSg
                                    ) 
                                |> Sg.dynamic
        |] |> Sg.group

    let cursor = 
        let toTrafo c = c.cursor |> V3d.op_Explicit |> Trafo3d.Translation
        Sg.group [ Sg.trafo (s.stateA |> Mod.map toTrafo) (cube C4b.Red)
                   Sg.trafo (s.stateB |> Mod.map toTrafo) (cube C4b.Red) ]

    let sg =
        boxes |> Sg.effect [
                DefaultSurfaces.trafo |> toEffect                   // compose shaders by using FShade composition.
                DefaultSurfaces.constantColor C4f.Red |> toEffect   // use standard trafo + map a constant color to each fragment
                DefaultSurfaces.simpleLighting |> toEffect
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