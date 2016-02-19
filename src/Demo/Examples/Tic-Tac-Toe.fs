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


    let cube = [| 0;1;2; 2;3;0;  
                  0;3;4; 4;5;0;
                  0;5;6; 6;1;0;
                  1;6;7; 7;2;1;
                  7;4;3; 3;2;7;
                  4;7;6; 6;5;4 |]

    let vertices = [| V3f.IIO; V3f.OIO; V3f.IOO; V3f.IOI; V3f.III; V3f.OII; V3f.OOI; |]
    let cubeSg = 
        IndexedGeometry(IndexedGeometryMode.TriangleList, cube,  
                SymDict.ofList [DefaultSemantic.Positions, vertices :> Array], SymDict.empty)
          |> Sg.ofIndexedGeometry

    let boxes = 
        [| for x in 0 .. 3 do
            for y in 0 .. 3 do
              for z in 0 .. 3 do
                let t = Trafo3d.Translation(float x * 2.0,float y * 2.0 ,float z * 2.0) |> Mod.constant
                yield Sg.trafo t cubeSg |] |> Sg.group


    let sg =
        boxes |> Sg.effect [
                DefaultSurfaces.trafo |> toEffect                   // compose shaders by using FShade composition.
                DefaultSurfaces.constantColor C4f.Red |> toEffect   // use standard trafo + map a constant color to each fragment
                //Shaders.simpleLighting |> toEffect
                ]
            |> Sg.cullMode (Mod.constant Rendering.CullMode.CounterClockwise)


    let run () =
        Aardvark.Rendering.Interactive.FsiSetup.init (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug"])
        setSg sg
        win.Run()


open TicTacToe

#if INTERACTIVE
setSg sg
printfn "Done. Modify sg and call setSg again in order to see the modified rendering result."
#endif