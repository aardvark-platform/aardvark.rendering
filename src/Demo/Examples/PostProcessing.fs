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

open FSharp.Data.Adaptive

open Aardvark.Rendering.Interactive
open Aardvark.SceneGraph
open Aardvark.Application
open FSharp.Data.Adaptive.Operators
open Aardvark.Rendering

module PostProcessing = 

    FsiSetup.initFsi (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug";"Examples.exe"])

    Interactive.Samples <- 1
    let win = Interactive.Window
    // let's start by creating our example-scene containing random points.
    let pointSize = AVal.init 50.0
    let pointCount = 2048

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

        type Bla =
            | Bla of float
            | Blubb of int

        type UniformScope with
            member x.Bla : Option<float> = x?Bla

        let gaussX (v : Vertex) =
            fragment {
                let mutable color = V4d.Zero

                let factor =
                    match uniform.Bla with
                        | Some f -> V3d(f, 1.0, 1.0)
                        | None -> V3d.III

                let off = V2d(1.0 / float uniform.ViewportSize.X, 0.0)
                for x in -halfFilterSize..halfFilterSize do
                    let w = weights.[x+halfFilterSize]
                    color <- color + w * inputTex.Sample(v.tc + (float x) * off)

                

                return V4d(factor * color.XYZ, 1.0)
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

    let pointSg = 
        let rand = Random()
        let randomV3f() = V3f(rand.NextDouble(), rand.NextDouble(), rand.NextDouble())
        let randomColor() = C4b(rand.NextDouble(), rand.NextDouble(), rand.NextDouble(), 1.0)

        Sg.draw IndexedGeometryMode.PointList
            |> Sg.vertexAttribute DefaultSemantic.Positions (Array.init pointCount (fun _ -> randomV3f()) |> AVal.constant)
            |> Sg.vertexAttribute DefaultSemantic.Colors (Array.init pointCount (fun _ -> randomColor()) |> AVal.constant)
            |> Sg.viewTrafo Interactive.DefaultViewTrafo
            |> Sg.projTrafo Interactive.DefaultProjTrafo
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

    let signature = //win.FramebufferSignature
        win.Runtime.CreateFramebufferSignature(8, [DefaultSemantic.Colors, RenderbufferFormat.Rgba8; DefaultSemantic.Depth, RenderbufferFormat.Depth24Stencil8])

    let mainTask =
        pointSg
            |> Sg.compile win.Runtime signature
         
   
    let mainResult =
        mainTask
            |> RenderTask.renderToColor win.Sizes
 

    let bla =
        AVal.init (Some 1.0)

    Interactive.Keyboard.DownWithRepeats.Values.Add (fun k ->
        match k with
            | Keys.X -> 
                transact (fun () -> 
                    bla.Value <- 
                        match bla.Value with
                            | Some v -> None
                            | None -> Some 0.5
                )
            | _ ->
                ()
    )

    // by taking the texture created above and the fullscreen quad we can now apply
    // the first gaussian filter to it and in turn get a new aval<ITexture>     
    let blurredOnlyX =
        fullscreenQuad 
            |> Sg.texture DefaultSemantic.DiffuseColorTexture mainResult
            |> Sg.effect [Shaders.gaussX |> toEffect]
            |> Sg.uniform "Bla" bla
            |> Sg.compile win.Runtime win.FramebufferSignature
            |> RenderTask.renderToColor win.Sizes

    // by taking the texture created above and the fullscreen quad we can now apply
    // the first gaussian filter to it and in turn get a new aval<ITexture>     
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
        let overlayPass = RenderPass.main |> RenderPass.after "overlay" RenderPassOrder.Arbitrary
        let overlayOriginal =
            fullscreenQuad
                |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.diffuseTexture |> toEffect]
                |> Sg.texture DefaultSemantic.DiffuseColorTexture mainResult
                |> Sg.trafo ~~(Trafo3d.Scale(overlayRelativeSize) * Trafo3d.Translation(-1.0 + overlayRelativeSize, 1.0 - overlayRelativeSize, 0.0))
                |> Sg.pass overlayPass
                |> Sg.blendMode ~~BlendMode.Blend
//
//        let overlayBox =
//            let box = win.Sizes |> AVal.map (fun s -> Box2d.FromMinAndSize(0.0, 0.0, overlayRelativeSize * float s.X, overlayRelativeSize * float s.Y))
//
//            box |> AVal.map Rectangle
//                |> Nvg.stroke
//                |> Nvg.strokeColor ~~C4f.Gray50
//                |> Nvg.strokeWidth ~~2.0
//
//        let overlayText =
//            Nvg.text ~~"Original"
//                |> Nvg.fontSize ~~20.0
//                |> Nvg.trafo (win.Sizes |> AVal.map (fun s -> M33d.Translation(float s.X * 0.5 * overlayRelativeSize, float s.Y * overlayRelativeSize - 10.0)))
//                |> Nvg.systemFont "Consolas" FontStyle.Bold
//                |> Nvg.align ~~TextAlign.Center
//                |> Nvg.andAlso overlayBox
//                |> win.Runtime.CompileRender
//                |> Sg.overlay


        let mainResult =
            fullscreenQuad 
//                |> Sg.texture DefaultSemantic.DiffuseColorTexture mainResult
//                |> Sg.effect [DefaultSurfaces.diffuseTexture |> toEffect]
//        
                |> Sg.texture DefaultSemantic.DiffuseColorTexture blurredOnlyX
                |> Sg.effect [Shaders.gaussY |> toEffect]
        
        Sg.ofList [mainResult; overlayOriginal]



    let showTexture t =
        Interactive.SceneGraph <- 
            fullscreenQuad 
                |> Sg.texture DefaultSemantic.DiffuseColorTexture t
                |> Sg.effect [DefaultSurfaces.diffuseTexture |> toEffect]


    let run () =
        Aardvark.Rendering.Interactive.FsiSetup.defaultCamera <- false
        Aardvark.Rendering.Interactive.FsiSetup.init (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug"])
               
//
//        ComputeTest.run()
//        Environment.Exit 0

//        let fbo = win.Runtime.CreateFramebuffer(win.FramebufferSignature, AVal.constant (V2i(1024, 768)))
//        fbo.Acquire()
//        let fboHandle = fbo.GetValue()
//        let color = unbox<BackendTextureOutputView> fboHandle.Attachments.[DefaultSemantic.Colors]
//        let output = OutputDescription.ofFramebuffer fboHandle
//        let clear = win.Runtime.CompileClear(win.FramebufferSignature, AVal.constant C4f.Black, AVal.constant 1.0)
//        let view1 = CameraView.lookAt V3d.III V3d.Zero V3d.OOI |> CameraView.viewTrafo
//        let view2 = CameraView.lookAt (10.0 * V3d.III) V3d.Zero V3d.OOI |> CameraView.viewTrafo
//
//        let desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)

//
//        clear.Run(RenderToken.Empty, fboHandle)
//        mainTask.Run(AdaptiveToken.Top, RenderToken.Empty, { output with overrides = Map.empty })
//        win.Runtime.Download(color.texture).SaveAsImage (Path.combine [desktop; "view0.png"])
//
//        clear.Run(RenderToken.Empty, fboHandle)
//        mainTask.Run(AdaptiveToken.Top, RenderToken.Empty, { output with overrides = Map.ofList ["ViewTrafo", view1 :> obj] })
//        win.Runtime.Download(color.texture).SaveAsImage (Path.combine [desktop; "view1.png"])
//        
//        clear.Run(RenderToken.Empty, fboHandle)
//        mainTask.Run(AdaptiveToken.Top, RenderToken.Empty, { output with overrides = Map.ofList ["ViewTrafo", view2 :> obj] })
//        win.Runtime.Download(color.texture).SaveAsImage (Path.combine [desktop; "view2.png"])
//
//        fbo.Release()



        Interactive.SceneGraph <- final
        Interactive.RunMainLoop()

    // finally we create a simple utility for changing the pointSize
    // you can play with it and see the render-result adjust to the given point-size.
    let setPointSize (s : float) =
        transact (fun () -> pointSize.Value <- s)


    // some other setters showing intermediate result textures
    let showMain() = showTexture mainResult
    let showOnlyX() = showTexture blurredOnlyX
    let showOnlyY() = showTexture blurredOnlyY
    let showFinal() = Interactive.SceneGraph <- final      


open PostProcessing

#if INTERACTIVE
Interactive.SceneGraph <- final
printfn "Done. Modify sg and call set the scene graph again in order to see the modified rendering result."
#endif

