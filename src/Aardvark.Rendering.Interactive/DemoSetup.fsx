
#I @"../../bin/Debug"
#I @"../../bin/Release"

#load "LoadReferences.fsx"

open Aardvark.Rendering.Interactive

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application

open Default // makes viewTrafo and other tutorial specicific default creators visible
Aardvark.Rendering.Interactive.FsiSetup.init (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; "bin";"Debug"])

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
