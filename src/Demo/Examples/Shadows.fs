(*
Shadows.fsx

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
#r "OpenTK.dll"
#r "OpenTK.Compatibility.dll"
#else
namespace Examples
#endif

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Interactive


open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open FSharp.Data.Adaptive.Operators
open Aardvark.Rendering

[<AutoOpen>]
module EffectStack = 
    open Aardvark.Base.Ag

    module Sg =
        type ComposeEffects(child : aval<ISg>) =
            inherit Sg.AbstractApplicator(child)
            member x.Child = child

        type AttachEffects(child : aval<ISg>, effects : list<FShadeEffect>) =
            inherit Sg.AbstractApplicator(child)
            member x.Effects : list<FShadeEffect> = effects
            member x.Child  = child

        let composeEffects (s : ISg) = ComposeEffects(AVal.constant s) :> ISg
        let attachEffects (e : list<FShadeEffect>) (s : ISg) = AttachEffects(AVal.constant s, e)

    type Ag.Scope with
        member x.EffectStack : list<FShadeEffect> = x?EffectStack

    module EffectStackSemantics =

        [<Rule>]
        type ComposeEffectsSemantics() =
            member x.Surface(sg : Sg.ComposeEffects, scope : Ag.Scope) =
                let e = FShade.Effect.compose scope.EffectStack
                let s = Surface.Effect e
                sg.Child?Surface <- s

            member x.EffectStack(s : Sg.AttachEffects, scope : Ag.Scope) =
                s.Child?EffectStack <- scope.EffectStack @ s.Effects 

            member x.EffectStack(s : Root<ISg>, scope : Ag.Scope) = 
                s.Child?EffectStack <- List.empty<FShadeEffect>
     

module Shader =
    open FShade

    type Vertex = {
        [<Position>]        pos     : V4f
        [<WorldPosition>]   wp      : V4f
        [<Normal>]          n       : V3f
        [<BiNormal>]        b       : V3f
        [<Tangent>]         t       : V3f
        [<Color>]           c       : V4f
        [<TexCoord>]        tc      : V2f
    }

    type UniformScope with
        member x.LightViewMatrix : M44f = uniform?LightViewMatrix
        
    let private diffuseSampler =
        sampler2dShadow {
            texture uniform?DiffuseColorTexture
            filter Filter.MinMagLinear
            addressU WrapMode.Border
            addressV WrapMode.Border
            borderColor C4f.White
            comparison ComparisonFunction.LessOrEqual
        }

    let clipPlane = V4f(1.0f,1.0f,1.0f,0.0f)

    type ClipVertex = {
        [<Position>]        pos     : V4f
        [<WorldPosition>]   wp      : V4f
        [<Normal>]          n       : V3f
        [<BiNormal>]        b       : V3f
        [<Tangent>]         t       : V3f
        [<Color>]           c       : V4f
        [<TexCoord>]        tc      : V2f
        [<ClipDistance>] clipDistances : float32[]
    }

    let trafo (v : Vertex) =
        vertex {
            let wp = uniform.ModelTrafo * v.pos
            let distance = Vec.dot wp clipPlane
            //let distance = 10.0
            return {
                pos = uniform.ViewProjTrafo * wp
                wp = wp
                n = (uniform.ViewTrafo * (V4f(v.n,0.0f))).XYZ
                b = uniform.NormalMatrix * v.b
                t = uniform.NormalMatrix * v.t
                c = v.c
                tc = v.tc
                clipDistances = [| distance |]
            }
        }

    let shadowShader (v : Vertex) =
        fragment {
            let lightSpace = uniform.LightViewMatrix * v.wp
            let div = lightSpace.XYZ / lightSpace.W
            let tc = V3f(0.5f) + V3f(0.5f) * div.XYZ
            let d = max 0.3f (diffuseSampler.Sample(tc.XY, tc.Z - 0.000017f))
            return V4f(v.c.XYZ * d, v.c.W)
        }


    let lighting (v : Vertex) =
        fragment {
            let n = v.n |> Vec.normalize
            let c = uniform?lightLocation - v.wp.XYZ |> Vec.normalize

            let ambient = 0.2f
            let diffuse = Vec.dot (uniform.ViewTrafo * V4f(c,0.0f)).XYZ n |> max 0.0f

            let l = ambient + (1.0f - ambient) * diffuse

            return V4f(v.c.XYZ * diffuse, v.c.W)
        }
           
    type InstanceVertex = { 
        [<Position>]      pos   : V4f
        [<InstanceTrafo>] trafo : M44f
    }

    let instanceTrafo (v : InstanceVertex) =
        vertex {
            return { v with pos = v.trafo * v.pos }
        }
            
module Shadows = 

    FsiSetup.initFsi (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug";"Examples.exe"])

    let win = Interactive.Window

    let shadowMapSize = AVal.init (V2i(4096, 4096))

    let shadowCam = CameraView.lookAt (V3d.III * 2.0) V3d.Zero V3d.OOI
    let shadowProj = Frustum.perspective 60.0 0.1 10.0 1.0

    let time = TimeMod()
    //let angle = AVal.init 0.0
    let rotation =
        controller {
            let! dt = differentiate time
            return fun f -> f + dt.TotalSeconds * 0.6
        }
  
    let angle = AVal.constant 0.0 //AFun.integrate rotation 0.0
    let lightSpaceView =
        angle |> AVal.map (fun angle -> Trafo3d.RotationZ(angle) * (shadowCam |> CameraView.viewTrafo))
    let lightSpaceViewProjTrafo = lightSpaceView |> AVal.map (fun view -> view * (shadowProj |> Frustum.projTrafo))
    let lightPos = lightSpaceView |> AVal.map (fun t -> t.GetViewPosition())


    let pointSize = AVal.init 14.0
    let pointCount = 1000


    let box (color : C4b) (box : Box3d) = 
            let randomColor = color //C4b(rand.Next(255) |> byte, rand.Next(255) |> byte, rand.Next(255) |> byte, 255uy)

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
                        DefaultSemantic.Colors, indices |> Array.map (fun _ -> randomColor) :> Array
                    ]

            )

    let quadSg (color : C4b) = 
            let index = [|0;1;2; 0;2;3|]
            let positions = [|V3f(-1,-1,0); V3f(1,-1,0); V3f(1,1,0); V3f(-1,1,0) |] |> Array.map ((*)3.0f)

            IndexedGeometry(IndexedGeometryMode.TriangleList, index, 
                SymDict.ofList [
                    DefaultSemantic.Positions, positions :> Array
                    DefaultSemantic.Colors,  Array.init positions.Length (constF color  ) :> Array
                    DefaultSemantic.Normals, Array.init positions.Length (constF V3f.OOI) :> Array
                ], SymDict.empty) |> Sg.ofIndexedGeometry

    let pointSg = 
        let rand = Random()
        let randomV3f() = V3f(rand.NextDouble(), rand.NextDouble(), rand.NextDouble())
        Sg.instancedGeometry (AVal.constant <| Array.init pointCount (fun _ -> randomV3f() |> V3d.op_Explicit |> Trafo3d.Translation)) (box C4b.Red (Box3d.FromCenterAndSize(V3d.OOO, 0.04 * V3d.III)))


    let sceneSg (fragmentShader : list<FShadeEffect>) =
        quadSg C4b.Green 
        |> Sg.effect ( (Shader.trafo |> toEffect) :: fragmentShader )
        |> Sg.andAlso ( pointSg 
                        |> Sg.effect (toEffect Shader.instanceTrafo :: toEffect Shader.trafo :: fragmentShader)
                      )
        |> Sg.uniform "LightViewMatrix" lightSpaceViewProjTrafo
        |> Sg.trafo ( Trafo3d.Translation(V3d(0.0,0.0,0.3)) |> AVal.constant )

    let signature = 
        win.Runtime.CreateFramebufferSignature [
            DefaultSemantic.DepthStencil, TextureFormat.DepthComponent32
        ]
 
    let shadowDepth =
        sceneSg [ DefaultSurfaces.vertexColor |> toEffect ]
            |> Sg.viewTrafo lightSpaceView
            |> Sg.projTrafo (shadowProj |> Frustum.projTrafo |> AVal.constant)
            |> Sg.compile win.Runtime signature   
            |> RenderTask.renderToDepth shadowMapSize

    let sg =
        sceneSg [ Shader.shadowShader |> toEffect; Shader.lighting |> toEffect ]
            |> Sg.uniform "lightLocation" lightPos
            |> Sg.texture DefaultSemantic.DiffuseColorTexture shadowDepth

            |> Sg.andAlso (
                Sg.cone' 32 C4b.Red 0.1 0.5 |> Sg.trafo (lightPos |> AVal.map Trafo3d.Translation)
                 |> Sg.effect [ DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.constantColor C4f.Red |> toEffect; DefaultSurfaces.simpleLighting |> toEffect ]
             )

            |> Sg.camera Interactive.DefaultCamera


    let run () =
        Aardvark.Rendering.Interactive.FsiSetup.init (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug"])
        Interactive.SceneGraph <- sg
        Interactive.RunMainLoop()


    let setShadowSize (w : int) (h : int) =
        transact (fun () ->
            shadowMapSize.Value <- V2i(w,h)
        )

open Shadows

#if INTERACTIVE
Interactive.SceneGraph <- sg
#endif