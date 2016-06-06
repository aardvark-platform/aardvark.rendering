(*
PostProcessing.fsx

This example illustrates how to do a very simple PostProcessing on a scene.
For simplicity the scene is just a random set of points but the example easily 
extends to more complicated scenes since it's just using renderTasks for composition.

Here we simply apply a gaussian blur to the rendered image but other effects can be achieved in a very
similar way. (e.g. fluid-rendering things, etc.)

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


open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering
open Aardvark.Rendering.NanoVg

module Shadows = 

    Aardvark.Rendering.Interactive.FsiSetup.defaultCamera <- false
    FsiSetup.initFsi (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug";"Examples.exe"])

    let win = openWindow()

    let shadowMapSize = Mod.init (V2i(1024, 1024))

    let shadowCam = CameraView.lookAt (V3d.III * 3.0) V3d.Zero V3d.OOI
    let shadowProj = Frustum.perspective 60.0 0.1 10.0 1.0

    let quadSg (color : C4b) = 
            let index = [|0;1;2; 0;2;3|]
            let positions = [|V3f(-1,-1,0); V3f(1,-1,0); V3f(1,1,0); V3f(-1,1,0) |]

            IndexedGeometry(IndexedGeometryMode.TriangleList, index, 
                SymDict.ofList [
                    DefaultSemantic.Positions, positions :> Array
                    DefaultSemantic.Colors,  Array.init positions.Length (constF color  ) :> Array
                    DefaultSemantic.Normals, Array.init positions.Length (constF V3f.OOI) :> Array
                ], SymDict.empty) |> Sg.ofIndexedGeometry

    let boxSg (color : C4b) (box : Box3f) = 
        let indices =
            [|
                1;2;6; 1;6;5
                2;3;7; 2;7;6
                4;5;6; 4;6;7
                3;0;4; 3;4;7
                0;1;5; 0;5;4
                0;3;2; 0;2;1
            |]

        let positions = 
            [|
                V3f(box.Min.X, box.Min.Y, box.Min.Z)
                V3f(box.Max.X, box.Min.Y, box.Min.Z)
                V3f(box.Max.X, box.Max.Y, box.Min.Z)
                V3f(box.Min.X, box.Max.Y, box.Min.Z)
                V3f(box.Min.X, box.Min.Y, box.Max.Z)
                V3f(box.Max.X, box.Min.Y, box.Max.Z)
                V3f(box.Max.X, box.Max.Y, box.Max.Z)
                V3f(box.Min.X, box.Max.Y, box.Max.Z)
            |]
            |> Array.map (fun x -> x - box.Center)

        let normals = 
            [| 
                 V3f.IOO;
                 V3f.OIO;
                 V3f.OOI;
                -V3f.IOO;
                -V3f.OIO;
                -V3f.OOI;
            |]

        IndexedGeometry(
            Mode = IndexedGeometryMode.TriangleList,
            IndexedAttributes =
                SymDict.ofList [
                    DefaultSemantic.Positions, indices |> Array.map (fun i -> positions.[i]) :> Array
                    DefaultSemantic.Normals, indices |> Array.mapi (fun ti _ -> normals.[ti / 6]) :> Array
                    DefaultSemantic.Colors, indices |> Array.map (fun _ -> color) :> Array
                ]
        ) |> Sg.ofIndexedGeometry

    let sceneSg =
        quadSg C4b.Green 
        |> Sg.andAlso ((boxSg C4b.Yellow (Box3f.Unit.Scaled (V3f.One * 0.25f))) 
        |> Sg.trafo (Trafo3d.Translation(V3d(0.0,0.0,0.3)) |> Mod.constant))

    let signature = 
        win.Runtime.CreateFramebufferSignature [
            DefaultSemantic.Depth, { format = RenderbufferFormat.DepthComponent32; samples = 1 }
        ]
 
    let shadowDepth =
        sceneSg
            |> Sg.viewTrafo (shadowCam |> CameraView.viewTrafo |> Mod.constant)
            |> Sg.projTrafo (shadowProj |> Frustum.projTrafo |> Mod.constant)
            |> Sg.effect [
                DefaultSurfaces.trafo |> toEffect
                DefaultSurfaces.vertexColor |> toEffect
            ]
            |> Sg.compile win.Runtime signature   
            |> RenderTask.renderToDepth shadowMapSize

    module Shader =
        open FShade

        type Vertex = {
            [<Position>]        pos     : V4d
            [<WorldPosition>]   wp      : V4d
            [<Normal>]          n       : V3d
            [<BiNormal>]        b       : V3d
            [<Tangent>]         t       : V3d
            [<Color>]           c       : V4d
            [<TexCoord>]        tc      : V2d
        }

        type UniformScope with
            member x.LightViewMatrix : M44d = uniform?LightViewMatrix
        
        let private diffuseSampler =
            sampler2dShadow {
                texture uniform?DiffuseColorTexture
                filter Filter.MinMagLinear
                addressU WrapMode.Border
                addressV WrapMode.Border
                borderColor C4f.White
                comparison ComparisonFunction.LessOrEqual
            }

        let shadowShader (v : Vertex) =
            fragment {
                let lightSpace = uniform.LightViewMatrix * v.wp
                let div = lightSpace.XYZ / lightSpace.W
                let v = V3d(0.5, 0.5,0.5) + V3d(0.5, 0.5, 0.5) * div.XYZ
                let d = diffuseSampler.Sample(v.XY, v.Z - 0.000017)
                return V4d(d,d,d,1.0)
            }

    let angle = Mod.init 0.0
    let lightSpaceViewProjTrafo =
        angle |> Mod.map (fun angle -> 
            Trafo3d.RotationZ(angle) * (shadowCam |> CameraView.viewTrafo) * (shadowProj |> Frustum.projTrafo)
        )

    let sg =
        sceneSg
            |> Sg.uniform "ViewportSize" win.Sizes
            |> Sg.uniform "LightViewMatrix" (lightSpaceViewProjTrafo)
            |> Sg.texture DefaultSemantic.DiffuseColorTexture shadowDepth
            |> Sg.effect [DefaultSurfaces.trafo |> toEffect
                          DefaultSurfaces.vertexColor |> toEffect
                          Shader.shadowShader |> toEffect 
                          DefaultSurfaces.simpleLighting |> toEffect ]
            |> Sg.viewTrafo (viewTrafo win   |> Mod.map CameraView.viewTrafo )
            |> Sg.projTrafo (perspective win |> Mod.map Frustum.projTrafo)


    win.Keyboard.KeyDown(Keys.T).Values.Subscribe(fun _ -> 
        transact (fun _ -> Mod.change angle (angle.Value + 0.1))
    ) |> ignore

    let run () =
        Aardvark.Rendering.Interactive.FsiSetup.init (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug"])
        showSg win sg
        System.Windows.Forms.Application.Run ()


    let setShadowSize (w : int) (h : int) =
        transact (fun () ->
            Mod.change shadowMapSize (V2i(w,h))
        )

open Shadows

#if INTERACTIVE
showSg win g
#endif