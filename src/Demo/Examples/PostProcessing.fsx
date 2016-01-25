(*
PostProcessing.fsx

This example illustrates how to do a very simple PostProcessing on a scene.
For simplicity the scene is just a random set of points but the example easily 
extends to more complicated scenes since it's just using renderTasks for composition.

Here we simply apply a gaussian blur to the rendered image but other effects can be achieved in a very
similar way. (e.g. fluid-rendering things, etc.)

*)

#load "RenderingSetup.fsx"
open RenderingSetup

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering
open Aardvark.Rendering.NanoVg
open Default // makes viewTrafo and other tutorial specicific default creators visible

// let's start by creating our example-scene containing random points.
let pointSize = Mod.init 50.0
let pointCount = 2048

let pointSg = 
    let rand = Random()
    let randomV3f() = V3f(rand.NextDouble(), rand.NextDouble(), rand.NextDouble())
    let randomColor() = C4b(rand.NextDouble(), rand.NextDouble(), rand.NextDouble(), 1.0)

    Sg.draw IndexedGeometryMode.PointList
        |> Sg.vertexAttribute DefaultSemantic.Positions (Array.init pointCount (fun _ -> randomV3f()) |> Mod.constant)
        |> Sg.vertexAttribute DefaultSemantic.Colors (Array.init pointCount (fun _ -> randomColor()) |> Mod.constant)
        |> Sg.viewTrafo (viewTrafo() |> Mod.map CameraView.viewTrafo)
        |> Sg.projTrafo (perspective() |> Mod.map Frustum.projTrafo)
        |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.pointSprite |> toEffect; DefaultSurfaces.pointSpriteFragment |> toEffect; DefaultSurfaces.vertexColor |> toEffect]
        |> Sg.uniform "PointSize" pointSize
        |> Sg.uniform "ViewportSize" win.Sizes


// we now need to define some shaders performing the per-pixel blur on a given input texture.
// since the gaussian filter is separable we create two shaders performing the vertical and horizontal blur.
module Shaders =
    open FShade

    type Vertex = { [<TexCoord>] tc : V2d; [<Position>] p : V4d }

    let input =
        sampler2d {
            texture uniform?DiffuseColorTexture
            filter Filter.MinMagMipPoint
        }

//    let cube =
//        SamplerCube(uniform?CubeMap, { SamplerState.Empty with Filter = Some Filter.MinMagLinear })

    // for a given filterSize and sigma calculate the weights CPU-side
    let filterSize = 15
    let sigma = 6.0

    let halfFilterSize = filterSize / 2
    let weights =
        let res = 
            Array.init filterSize (fun i ->
                let x = abs (i - halfFilterSize)
                exp (-float (x*x) / (2.0 * sigma * sigma))
            )

        // normalize the weights
        let sum = Array.sum res
        res |> Array.map (fun v -> v / sum)


    let gaussX (v : Vertex) =
        fragment {
            let mutable color = V4d.Zero
            let off = V2d(1.0 / float uniform.ViewportSize.X, 0.0)
            for x in -halfFilterSize..halfFilterSize do
                let w = weights.[x+halfFilterSize]
                color <- color + w * input.Sample(v.tc + (float x) * off)

            return V4d(color.XYZ, 1.0)
        }

    let gaussY (v : Vertex) =
        fragment {
            let mutable color = V4d.Zero
            let off = V2d(0.0, 1.0 / float uniform.ViewportSize.Y)
            for y in -halfFilterSize..halfFilterSize do
                let w = weights.[y+halfFilterSize]
                color <- color + w * input.Sample(v.tc + (float y) * off)

            return V4d(color.XYZ, 1.0)
        }

//    let bla (v : Vertex) =
//        fragment {
//            return cube.Sample(v.p.XYZ.Normalized)
//        }


// for rendering the filtered image we need a fullscreen quad
let fullscreenQuad =
    Sg.draw IndexedGeometryMode.TriangleStrip
        |> Sg.vertexAttribute DefaultSemantic.Positions (Mod.constant [|V3f(-1.0,-1.0,0.0); V3f(1.0,-1.0,0.0); V3f(-1.0,1.0,0.0);V3f(1.0,1.0,0.0) |])
        |> Sg.vertexAttribute DefaultSemantic.DiffuseColorCoordinates (Mod.constant [|V2f.OO; V2f.IO; V2f.OI; V2f.II|])
        |> Sg.depthTest ~~DepthTestMode.None
        |> Sg.uniform "ViewportSize" win.Sizes

// so in a first pass we need to render our pointScene to a color texture which
// is quite simple using the RenderTask utilities provided in Base.Rendering.
// from the rendering we get an IMod<ITexture> which will be outOfDate whenever
// something changes in pointScene and updated whenever subsequent passes need it.
let mainResult =
    pointSg
        |> Sg.compile win.Runtime win.FramebufferSignature 
        |> RenderTask.renderToColor win.Sizes
 
//let runtime = win.Runtime  
//let signature =
//    runtime.CreateFramebufferSignature [
//        DefaultSemantic.Colors, { format = RenderbufferFormat.Rgba8; samples = 1 }
//        DefaultSemantic.Depth, { format = RenderbufferFormat.Depth24Stencil8; samples = 1 }
//    ]
//let cube = win.Runtime.CreateTextureCube(V2i(1024,1024), TextureFormat.Rgba8, 1, 1)
//let depth = win.Runtime.CreateRenderbuffer(V2i(1024,1024),RenderbufferFormat.Depth24Stencil8, 1)
//
//let fbo = 
//    runtime.CreateFramebuffer(signature, 
//        Map.ofList [
//             DefaultSemantic.Colors, { texture = cube; slice = int CubeSide.PositiveX; level = 0 } :> IFramebufferOutput 
//             DefaultSemantic.Depth, depth :> IFramebufferOutput
//        ])
//
//let cubeResult = 
//    RenderTask.renderTo (Mod.constant <| OutputDescription.ofFramebuffer fbo) (pointSg |> Sg.compile win.Runtime win.FramebufferSignature)
//
//let a = RenderTask.getResult DefaultSemantic.Colors cubeResult

// by taking the texture created above and the fullscreen quad we can now apply
// the first gaussian filter to it and in turn get a new IMod<ITexture>     
let blurredOnlyX =
    fullscreenQuad 
        |> Sg.texture DefaultSemantic.DiffuseColorTexture mainResult
        |> Sg.effect [Shaders.gaussX |> toEffect]
        |> Sg.compile win.Runtime win.FramebufferSignature
        |> RenderTask.renderToColor win.Sizes

// by taking the texture created above and the fullscreen quad we can now apply
// the first gaussian filter to it and in turn get a new IMod<ITexture>     
let blurredOnlyY =
    fullscreenQuad 
        |> Sg.texture DefaultSemantic.DiffuseColorTexture mainResult
        |> Sg.effect [Shaders.gaussY |> toEffect]
        |> Sg.compile win.Runtime win.FramebufferSignature
        |> RenderTask.renderToColor win.Sizes

// we could now render the blurred result to a texutre too but for our example
// we can also render it directly to the screen.
let final =
    let overlayRelativeSize = 0.3

    let overlayOriginal =
        fullscreenQuad
            |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.diffuseTexture |> toEffect]
            |> Sg.texture DefaultSemantic.DiffuseColorTexture mainResult
            |> Sg.trafo ~~(Trafo3d.Scale(overlayRelativeSize) * Trafo3d.Translation(-1.0 + overlayRelativeSize, 1.0 - overlayRelativeSize, 0.0))
            |> Sg.pass 2UL
            |> Sg.blendMode ~~BlendMode.Blend

    let overlayBox =
        let box = win.Sizes |> Mod.map (fun s -> Box2d.FromMinAndSize(0.0, 0.0, overlayRelativeSize * float s.X, overlayRelativeSize * float s.Y))

        box |> Mod.map Rectangle
            |> Nvg.stroke
            |> Nvg.strokeColor ~~C4f.Gray50
            |> Nvg.strokeWidth ~~2.0

    let overlayText =
        Nvg.text ~~"Original"
            |> Nvg.fontSize ~~20.0
            |> Nvg.trafo (win.Sizes |> Mod.map (fun s -> M33d.Translation(float s.X * 0.5 * overlayRelativeSize, float s.Y * overlayRelativeSize - 10.0)))
            |> Nvg.systemFont "Consolas" FontStyle.Bold
            |> Nvg.align ~~TextAlign.Center
            |> Nvg.andAlso overlayBox
            |> win.Runtime.CompileRender
            |> Sg.overlay


    let mainResult =
        fullscreenQuad 
            |> Sg.texture DefaultSemantic.DiffuseColorTexture blurredOnlyX
            |> Sg.effect [Shaders.gaussY |> toEffect]
        
    Sg.group' [mainResult; overlayOriginal; overlayText]

let showTexture t =
    setSg (
        fullscreenQuad 
            |> Sg.texture DefaultSemantic.DiffuseColorTexture t
            |> Sg.effect [DefaultSurfaces.diffuseTexture |> toEffect]
    )




setSg final

// finally we create a simple utility for changing the pointSize
// you can play with it and see the render-result adjust to the given point-size.
let setPointSize (s : float) =
    transact (fun () -> Mod.change pointSize s)


// some other setters showing intermediate result textures
let showMain() = showTexture mainResult
let showOnlyX() = showTexture blurredOnlyX
let showOnlyY() = showTexture blurredOnlyY
let showFinal() = setSg final      

