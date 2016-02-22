(*
Tutorial.fsx

This examples demonstrates how to load the aardvark rendering engine into the F# interactive shell. After
building Aardvark.Rendering.sln either by script or by visual studio, select the contents of this file and press
ALT+ENTER in order to the run the code immediately.
On Linux, simply `cd` to this source directory and run mono fsi.exe Tutorial.fsx.
If all runs fine, you will see a window containing a single quad. After this you can freely modify the construction code
(beginning from quadSg), modify parts and rerun the code by pressing ALT+ENTER (e.g. modify elements in the positions coordinate).
The function setSg (imported from RenderingSetup.fsx) has type ISg -> unit and activates a new scene graph.


How to build:
(1) build the project via build.cmd/build.sh
(2) build the solution in Debug via msbuild,xbuild or visual studio.
(3) On linux: make sure to use a 64bit fsi.exe
    On Windows in VisualStudio: verify that you use a 64bit fsi (Tools/Options/Fsharp tools/64bit interactive active).
    You can verify this step by typing: 
    > System.IntPtr.Size;;
    which should print: val it : int = 8
(4) In the console, invoke fsi which Tutorial.fsx argument, in VisualStudio send the code to interactive shell 
    (right click, send to interactive or ALT+ENTER)
(5) A new window should now spawn. At the end of each example there are some functions for modifying the content.
    Run those lines in order to see the effects. In Tutorial.fsx you can for example modify values in the positions array
    and rerun lines beginning from quadSg till setSg in order to activate the new content.
*)


#if INTERACTIVE
#I @"../../../bin/Debug"
#I @"../../../bin/Release"
#load "LoadReferences.fsx"
#else
namespace Examples
#endif

open System
open Aardvark.Base
open Aardvark.Rendering.Interactive

open Default // makes viewTrafo and other tutorial specicific default creators visible

open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application

module Pong = 

    Aardvark.Rendering.Interactive.FsiSetup.init (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug"])

    type BlockState = Dead | Alive
    type Paddle = { pos : IMod<V3d> }
    type Block = { boundingSphere : Sphere3d; state : IModRef<BlockState> }
    type GameState =
        {
            blocks : Block[,]
            paddle : Paddle
        }

    type Input = MoveTo of V3d | Shoot

    let step (g : GameState) (i : Input) =
        let paddlePos = g.paddle.pos |> Mod.force
        match i with 
         | MoveTo target ->
            failwith "wip"
         | _ -> failwith ""

    
    let sphere = Sphere.solidSphere C4b.DarkBlue 5 |> Sg.trafo (Mod.constant <| Trafo3d.Scale 0.5 * Trafo3d.Translation(0.5,0.5,0.5))
    let aliens = 
        Array2D.init 10 4 (fun x y -> 
            {
                boundingSphere = Sphere3d.FromCenterAndRadius(V3d(float x,0.0,float y),0.5)
                state = Mod.init Alive 
            })
    
    let startState = { blocks = aliens; paddle = { pos = V3d(5,0,-4) } }

    let blocks =
       aliens |> Seq.cast |> Seq.map (fun { boundingSphere = bb } ->
            let t = Trafo3d.Translation(bb.Center) |> Mod.constant
            Sg.trafo t sphere
        ) |> Sg.group

    let view = viewTrafo' ( V3d(5.0,12.0,7.4) ) ( V3d(5,0,0) )

    let sg =
        blocks
            |> Sg.effect [
                DefaultSurfaces.trafo |> toEffect                 
                DefaultSurfaces.simpleLighting |> toEffect
                ]
            |> Sg.viewTrafo (view           |> Mod.map CameraView.viewTrafo ) 
            |> Sg.projTrafo (perspective () |> Mod.map Frustum.projTrafo    )

    let run () =
        setSg sg
        win.Run()

open Pong

#if INTERACTIVE
setSg sg
printfn "Done. Modify sg and call setSg again in order to see the modified rendering result."
#else
#endif