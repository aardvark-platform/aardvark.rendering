open System

open Aardvark.Base
open Aardvark.Base.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open FSharp.Data.Adaptive.Operators


// we now need to define some shaders performing the per-pixel blur on a given input texture.
// since the gaussian filter is separable we create two shaders performing the vertical and horizontal blur.
module Shaders =
    open FShade

    type Vertex = { [<TexCoord>] tc : V2d; [<Position>] p : V4d }

    let inputTex =
        sampler2d {
            texture uniform?DiffuseColorTexture
            filter Filter.MinMagMipPoint
        }

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
                color <- color + w * inputTex.Sample(v.tc + (float x) * off)
             

            return V4d(color.XYZ, 1.0)
        }

    let gaussY (v : Vertex) =
        fragment {
            let mutable color = V4d.Zero
            let off = V2d(0.0, 1.0 / float uniform.ViewportSize.Y)
            for y in -halfFilterSize..halfFilterSize do
                let w = weights.[y+halfFilterSize]
                color <- color + w * inputTex.Sample(v.tc + (float y) * off)

            return V4d(color.XYZ, 1.0)
        }
            
    type PSVertex = { [<TexCoord; Interpolation(InterpolationMode.Sample)>] tc : V2d }

    let pointSpriteFragment (v : PSVertex) =
        fragment {
            let tc = v.tc // + 0.00000001 * v.sp

            let c = 2.0 * tc - V2d.II
            if c.Length > 1.0 then
                discard()

            return v
        }

[<EntryPoint>]
let main argv = 
    
    // first we need to initialize Aardvark's core components
    
    Aardvark.Init()


    let win =
        window {
            backend Backend.GL
            display Display.Mono
            debug false
            samples 8
        }

    let pointSize = AVal.init 50.0

    let pointSg = 
        let pointCount = 2048
        let rand = Random()
        let randomV3f() = V3f(rand.NextDouble(), rand.NextDouble(), rand.NextDouble())
        let randomColor() = C4b(rand.NextDouble(), rand.NextDouble(), rand.NextDouble(), 1.0)

        Sg.draw IndexedGeometryMode.PointList
            |> Sg.vertexAttribute DefaultSemantic.Positions (Array.init pointCount (fun _ -> randomV3f()) |> AVal.constant)
            |> Sg.vertexAttribute DefaultSemantic.Colors (Array.init pointCount (fun _ -> randomColor()) |> AVal.constant)
            |> Sg.viewTrafo (win.View |> AVal.map (Array.item 0))    // for stereo rendering we would get two views
            |> Sg.projTrafo (win.Proj |> AVal.map (Array.item 0))    // but we take the first one here.
            |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.pointSprite |> toEffect; Shaders.pointSpriteFragment |> toEffect; DefaultSurfaces.vertexColor |> toEffect]
            |> Sg.uniform "PointSize" pointSize
   
    // for rendering the filtered image we need a fullscreen quad
    let fullscreenQuad =
        Sg.draw IndexedGeometryMode.TriangleStrip
            |> Sg.vertexAttribute DefaultSemantic.Positions (AVal.constant [|V3f(-1.0,-1.0,0.0); V3f(1.0,-1.0,0.0); V3f(-1.0,1.0,0.0);V3f(1.0,1.0,0.0) |])
            |> Sg.vertexAttribute DefaultSemantic.DiffuseColorCoordinates (AVal.constant [|V2f.OO; V2f.IO; V2f.OI; V2f.II|])
            |> Sg.depthTest ~~DepthTestMode.None



    // so in a first pass we need to render our pointScene to a color texture which
    // is quite simple using the RenderTask utilities provided in Base.Rendering.
    // from the rendering we get an aval<ITexture> which will be outOfDate whenever
    // something changes in pointScene and updated whenever subsequent passes need it.
    let singleSampledSignature = 
        win.Runtime.CreateFramebufferSignature(1, [
            DefaultSemantic.Colors, RenderbufferFormat.Rgba8; 
            DefaultSemantic.Depth, RenderbufferFormat.Depth24Stencil8
           ]
        )

    let mainTask =
        pointSg
            |> Sg.compile win.Runtime singleSampledSignature
         
   
    let mainResult =
        mainTask
            |> RenderTask.renderToColor win.Sizes

    // by taking the texture created above and the fullscreen quad we can now apply
    // the first gaussian filter to it and in turn get a new aval<ITexture>     
    let blurredOnlyX =
        fullscreenQuad 
            |> Sg.texture DefaultSemantic.DiffuseColorTexture mainResult
            |> Sg.effect [Shaders.gaussX |> toEffect]
            |> Sg.compile win.Runtime singleSampledSignature
            |> RenderTask.renderToColor win.Sizes

    // by taking the texture created above and the fullscreen quad we can now apply
    // the first gaussian filter to it and in turn get a new aval<ITexture>     
    let blurredOnlyY =
        fullscreenQuad 
            |> Sg.texture DefaultSemantic.DiffuseColorTexture mainResult
            |> Sg.effect [Shaders.gaussY |> toEffect]
            |> Sg.compile win.Runtime singleSampledSignature
            |> RenderTask.renderToColor win.Sizes

    // we could now render the blurred result to a texutre too but for our example
    // we can also render it directly to the screen.
    let final =
        let overlayRelativeSize = 0.3
        let overlayPass = RenderPass.main |> RenderPass.after "overlay" RenderPassOrder.Arbitrary
        let overlayOriginal =
            fullscreenQuad
                |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.diffuseTexture |> toEffect]
                |> Sg.texture DefaultSemantic.DiffuseColorTexture mainResult
                |> Sg.trafo ~~(Trafo3d.Scale(overlayRelativeSize) * Trafo3d.Translation(-1.0 + overlayRelativeSize, 1.0 - overlayRelativeSize, 0.0))
                |> Sg.pass overlayPass
                |> Sg.blendMode ~~BlendMode.Blend

        let mainResult =
            fullscreenQuad 
                |> Sg.texture DefaultSemantic.DiffuseColorTexture blurredOnlyX
                |> Sg.effect [Shaders.gaussY |> toEffect]


        Sg.ofList [mainResult; overlayOriginal] 
            |> Sg.viewTrafo ~~Trafo3d.Identity 
            |> Sg.projTrafo ~~Trafo3d.Identity


    let showTexture t = 
        fullscreenQuad 
            |> Sg.texture DefaultSemantic.DiffuseColorTexture t
            |> Sg.effect [DefaultSurfaces.diffuseTexture |> toEffect]

    let variant = AVal.init 0
    let variants = [| final; showTexture mainResult; showTexture blurredOnlyX; showTexture blurredOnlyY |]

    win.Keyboard.Down.Values.Add(fun k ->
        match k with
            | Keys.Up -> transact (fun () -> pointSize.Value <- pointSize.Value + 5.0)
            | Keys.Down -> transact (fun () -> pointSize.Value <- max 0.0 (pointSize.Value - 5.0))
            | Keys.V -> 
                transact (fun _ -> 
                    variant.Value <- (variant.Value + 1) % variants.Length
                )
            | _ -> ()
    )

    win.Scene <- 
        AVal.map (fun i -> Array.item i variants) variant |> Sg.dynamic

    win.Run()
    0
