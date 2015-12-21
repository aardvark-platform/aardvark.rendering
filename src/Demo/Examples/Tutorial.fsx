(*
Tutorial.fsx

This examples demonstrates how to load the aardvark rendering engine into the F# interactive shell. After
building Aardvark.Rendering.sln either by script or by visual studio, select the contents of this file and press
ALT+ENTER in order to the run the code immediately.
On Linux, simply `cd` to this source directory and run mono fsi.exe Tutorial.fsx.
If all runs fine, you will see a window containing a single quad. After this you can freely modify the construction code
(beginning from quadSg), modify parts and rerun the code by pressing ALT+ENTER (e.g. modify elements in the positions coordinate).
The function setSg (imported from RenderingSetup.fsx) has type ISg -> unit and activates a new scene graph.
*)


#load "RenderingSetup.fsx"
open RenderingSetup

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application

open Default // makes viewTrafo and other tutorial specicific default creators visible

let quadSg =
    let quad =
        let index = [|0;1;2; 0;2;3|]
        let positions = [|V3f(-1,-1,0); V3f(1,-1,0); V3f(1,1,0); V3f(-1,1,0) |]

        IndexedGeometry(IndexedGeometryMode.TriangleList, index, SymDict.ofList [DefaultSemantic.Positions, positions :> Array], SymDict.empty)

    // create a scenegraph given the indexedGeometry containing a quad
    quad |> Sg.ofIndexedGeometry

let sg =
    quadSg |> Sg.effect [
            DefaultSurfaces.trafo |> toEffect                   // compose shaders by using FShade composition.
            DefaultSurfaces.constantColor C4f.Red |> toEffect   // use standard trafo + map a constant color to each fragment
            ]
        // viewTrafo () creates camera controls and returns IMod<ICameraView> which we project to its view trafo component by using CameraView.viewTrafo
        |> Sg.viewTrafo (viewTrafo   () |> Mod.map CameraView.viewTrafo ) 
        // perspective () connects a proj trafo to the current main window (in order to take account for aspect ratio when creating the matrices.
        // Again, perspective() returns IMod<Frustum> which we project to its matrix by mapping ofer Frustum.projTrafo.
        |> Sg.projTrafo (perspective () |> Mod.map Frustum.projTrafo    )

setSg sg

printfn "Done. Modify sg and call setSg again in order to see the modified rendering result."
