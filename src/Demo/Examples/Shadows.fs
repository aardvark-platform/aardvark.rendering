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

module Shadows = 

    open System
    open Aardvark.Base
    open Aardvark.Rendering.Interactive

    Aardvark.Rendering.Interactive.FsiSetup.init (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug"])

    open Aardvark.Base.Incremental
    open Aardvark.SceneGraph
    open Aardvark.Application
    open Aardvark.Base.Incremental.Operators
    open Aardvark.Base.Rendering
    open Aardvark.Rendering.NanoVg
    open Examples


    let win = openWindow()

    // let's start by creating our example-scene containing random points.
    let pointSize = Mod.init 50.0
    let pointCount = 2048

    let shadowMapSize = Mod.init (V2i(1024, 1024))

    let shadowCam = CameraView.lookAt (V3d.III * 3.0) V3d.Zero V3d.OOI
    let shadowProj = Frustum.perspective 60.0 0.1 100.0 1.0

    let pointSg = 
        let rand = Random()
        let randomV3f() = V3f(rand.NextDouble(), rand.NextDouble(), rand.NextDouble())
        let randomColor() = C4b(rand.NextDouble(), rand.NextDouble(), rand.NextDouble(), 1.0)

        Sg.draw IndexedGeometryMode.PointList
            |> Sg.vertexAttribute DefaultSemantic.Positions (Array.init pointCount (fun _ -> randomV3f()) |> Mod.constant)
            |> Sg.vertexAttribute DefaultSemantic.Colors (Array.init pointCount (fun _ -> randomColor()) |> Mod.constant)
            |> Sg.uniform "PointSize" pointSize

    let stencilMode =
        StencilMode(
            IsEnabled = true,
            Compare =
                StencilFunction(
                    Function = StencilCompareFunction.Always, 
                    Reference = 0, 
                    Mask = 0xFFu
                ),
            Operation =
                StencilOperation(
                    StencilFail = StencilOperationFunction.Keep,
                    DepthFail = StencilOperationFunction.Keep,
                    DepthPass = StencilOperationFunction.Increment
                )
        )

    let signature = 
        win.Runtime.CreateFramebufferSignature [
            DefaultSemantic.Depth, { format = RenderbufferFormat.DepthComponent32; samples = 1 }
            //DefaultSemantic.Stencil, { format = shadowStencilFormat; samples = 1 }
        ]
 
    let shadowDepth =
        pointSg
            |> Sg.uniform "ViewportSize" win.Sizes
            |> Sg.effect [
                DefaultSurfaces.trafo |> toEffect
                DefaultSurfaces.pointSprite |> toEffect
                DefaultSurfaces.pointSpriteFragment |> toEffect
                DefaultSurfaces.constantColor C4f.Red |> toEffect]

            |> Sg.viewTrafo (viewTrafo win|> Mod.map CameraView.viewTrafo)
            |> Sg.projTrafo (perspective win |> Mod.map Frustum.projTrafo)

            |> Sg.compile win.Runtime signature   
            |> RenderTask.renderToDepth shadowMapSize



    let fullscreenQuad =
        Sg.draw IndexedGeometryMode.TriangleStrip
            |> Sg.vertexAttribute DefaultSemantic.Positions (Mod.constant [|V3f(-1.0,-1.0,0.0); V3f(1.0,-1.0,0.0); V3f(-1.0,1.0,0.0);V3f(1.0,1.0,0.0) |])
            |> Sg.vertexAttribute DefaultSemantic.DiffuseColorCoordinates (Mod.constant [|V2f.OO; V2f.IO; V2f.OI; V2f.II|])
            |> Sg.depthTest ~~DepthTestMode.None
            |> Sg.uniform "ViewportSize" win.Sizes

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

        let private diffuseSampler =
            sampler2d {
                texture uniform?DiffuseColorTexture
                filter Filter.MinMagPoint
                addressU WrapMode.Wrap
                addressV WrapMode.Wrap
            }

        let diffuseTexture (v : Vertex) =
            fragment {
                let d = Fun.Pow(diffuseSampler.Sample(v.tc).X, 100.0)
                return V4d(d,d,d,1.0)
            }

    let sg =
        fullscreenQuad
            |> Sg.effect [Shader.diffuseTexture |> toEffect]
            |> Sg.texture DefaultSemantic.DiffuseColorTexture shadowDepth

    let g = Sg.group [sg]
    showSg win g

    #if INTERACTIVE
    showSg win g
    #else
    let run () =
        Aardvark.Rendering.Interactive.FsiSetup.init (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug"])
        showSg win g
        System.Windows.Forms.Application.Run ()
    #endif

    let reset() = g.Clear()
    let set() = g.Add(sg) |> ignore

    let setShadowSize (w : int) (h : int) =
        transact (fun () ->
            Mod.change shadowMapSize (V2i(w,h))
        )