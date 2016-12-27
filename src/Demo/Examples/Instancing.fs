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

open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application

module Instancing = 

    FsiSetup.initFsi (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug";"Examples.exe"])

    let win = Interactive.Window

    let cylinder = Primitives.unitSphere 10

    let cameraView = Interactive.ControlledViewTrafo (V3d.III * 3.0) V3d.Zero
    let projection = Interactive.DefaultProjTrafo

    let trafos =
        [|
            for x in 0 .. 9 do
                for y in 0 .. 9 do
                    for z in 0 .. 9 do
                        yield Trafo3d.Translation(float x,float y,float z)
        |]

    let sg =
        cylinder
            //|> Sg.ofIndexedGeometry
            |> Sg.instancedGeometry (Mod.constant trafos)
            |> Sg.effect [
                DefaultSurfaces.instanceTrafo |> toEffect        
                DefaultSurfaces.trafo |> toEffect
                DefaultSurfaces.constantColor C4f.Red |> toEffect  
                ]
            |> Sg.viewTrafo cameraView
            |> Sg.projTrafo projection

    let run () =
        FsiSetup.initFsi (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug";"Examples.exe"])
        Interactive.SceneGraph <- sg
        Interactive.RunMainLoop()

open Instancing

#if INTERACTIVE
Interactive.SceneGraph <- sg
printfn "Done. Modify sg and set Interactive.SceneGraph again in order to see the modified rendering results."
#else
#endif