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
open Aardvark.Base.Incremental

open Aardvark.Rendering.Interactive
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering
open Aardvark.Rendering.NanoVg
open Aardvark.Base.ShaderReflection

module PostProcessing = 

    FsiSetup.initFsi (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug";"Examples.exe"])

    Interactive.Samples <- 1
    let win = Interactive.Window
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
            |> Sg.viewTrafo Interactive.DefaultViewTrafo
            |> Sg.projTrafo Interactive.DefaultProjTrafo
            |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.pointSprite |> toEffect; DefaultSurfaces.pointSpriteFragment |> toEffect; DefaultSurfaces.vertexColor |> toEffect]
            |> Sg.uniform "PointSize" pointSize


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


    // for rendering the filtered image we need a fullscreen quad
    let fullscreenQuad =
        Sg.draw IndexedGeometryMode.TriangleStrip
            |> Sg.vertexAttribute DefaultSemantic.Positions (Mod.constant [|V3f(-1.0,-1.0,0.0); V3f(1.0,-1.0,0.0); V3f(-1.0,1.0,0.0);V3f(1.0,1.0,0.0) |])
            |> Sg.vertexAttribute DefaultSemantic.DiffuseColorCoordinates (Mod.constant [|V2f.OO; V2f.IO; V2f.OI; V2f.II|])
            |> Sg.depthTest ~~DepthTestMode.None

    // so in a first pass we need to render our pointScene to a color texture which
    // is quite simple using the RenderTask utilities provided in Base.Rendering.
    // from the rendering we get an IMod<ITexture> which will be outOfDate whenever
    // something changes in pointScene and updated whenever subsequent passes need it.
    let mainTask =
        pointSg
            |> Sg.compile win.Runtime win.FramebufferSignature 
         
   
    let mainResult =
        mainTask
            |> RenderTask.renderToColor win.Sizes
 

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
        let overlayPass = RenderPass.main |> RenderPass.after "overlay" RenderPassOrder.Arbitrary
        let overlayOriginal =
            fullscreenQuad
                |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.diffuseTexture |> toEffect]
                |> Sg.texture DefaultSemantic.DiffuseColorTexture mainResult
                |> Sg.trafo ~~(Trafo3d.Scale(overlayRelativeSize) * Trafo3d.Translation(-1.0 + overlayRelativeSize, 1.0 - overlayRelativeSize, 0.0))
                |> Sg.pass overlayPass
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
        Interactive.SceneGraph <- 
            fullscreenQuad 
                |> Sg.texture DefaultSemantic.DiffuseColorTexture t
                |> Sg.effect [DefaultSurfaces.diffuseTexture |> toEffect]

    module ComputeTest =
        open OpenTK.Graphics.OpenGL4
        open Aardvark.Rendering.GL
        open Microsoft.FSharp.NativeInterop

        let run() =
            let runtime = unbox<Runtime> Interactive.Window.Runtime
            let ctx = runtime.Context
            use t = ctx.ResourceLock

            let code = 
                String.concat "\r\n" [
                    "#version 440"
                    "layout( std430 ) buffer inputs"
                    "{"
                    "    float a[];"
                    "};"
                    "layout( std430 ) buffer outputs"
                    "{"
                    "    float b[];"
                    "};"

                    "layout( local_size_x = 32 ) in;" 
                    "void main() {" 
                    "    int gid = int(gl_GlobalInvocationID.x);" 
                    "    b[gid] = 3.0 * float(gid);" 
                    "}"
                ]


            match ctx.TryCompileCompute(true, code) with
                | Success prog ->
                    let iface = prog.Interface
                    let str = ShaderInterface.toString prog.Interface

                    let a : ShaderBlock = iface.StorageBlocks |> List.find (fun b -> b.Name = "inputs")
                    let b : ShaderBlock = iface.StorageBlocks |> List.find (fun b -> b.Name = "outputs")

                    let f a =
                        float32 a + 1.0f

                    let ba = ctx.CreateBuffer(Array.init 1024 f, BufferUsage.Dynamic)
                    let bb = ctx.CreateBuffer(Array.init 1024 f, BufferUsage.Dynamic)

                    GL.UseProgram(prog.Handle)
                    GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, a.Index, ba.Handle, 0n, 4096n)
                    GL.Check "bla"
                    GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, b.Index, bb.Handle, 0n, 4096n)
                    GL.Check "blubb"

                    GL.DispatchCompute(1024, 1, 1)
                    GL.Check "sepp"


                    let ra : float32[] = ctx.Download(ba)
                    let rb : float32[] = ctx.Download(bb)



                    printfn "%A %A" ra rb

                | Error err ->
                    Log.error "%s" err

        
            ()

    let run () =
        Aardvark.Rendering.Interactive.FsiSetup.defaultCamera <- false
        Aardvark.Rendering.Interactive.FsiSetup.init (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug"])
               
//
//        ComputeTest.run()
//        Environment.Exit 0

        let fbo = win.Runtime.CreateFramebuffer(win.FramebufferSignature, Mod.constant (V2i(1024, 768)))
        fbo.Acquire()
        let fboHandle = fbo.GetValue()
        let color = unbox<BackendTextureOutputView> fboHandle.Attachments.[DefaultSemantic.Colors]
        let output = OutputDescription.ofFramebuffer fboHandle
        let clear = win.Runtime.CompileClear(win.FramebufferSignature, Mod.constant C4f.Black, Mod.constant 1.0)
        let view1 = CameraView.lookAt V3d.III V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let view2 = CameraView.lookAt (10.0 * V3d.III) V3d.Zero V3d.OOI |> CameraView.viewTrafo

        let desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)


        clear.Run(RenderToken.Empty, fboHandle)
        mainTask.Run(AdaptiveToken.Top, RenderToken.Empty, { output with overrides = Map.empty })
        win.Runtime.Download(color.texture).SaveAsImage (Path.combine [desktop; "view0.png"])

        clear.Run(RenderToken.Empty, fboHandle)
        mainTask.Run(AdaptiveToken.Top, RenderToken.Empty, { output with overrides = Map.ofList ["ViewTrafo", view1 :> obj] })
        win.Runtime.Download(color.texture).SaveAsImage (Path.combine [desktop; "view1.png"])
        
        clear.Run(RenderToken.Empty, fboHandle)
        mainTask.Run(AdaptiveToken.Top, RenderToken.Empty, { output with overrides = Map.ofList ["ViewTrafo", view2 :> obj] })
        win.Runtime.Download(color.texture).SaveAsImage (Path.combine [desktop; "view2.png"])

        fbo.Release()



        Interactive.SceneGraph <- final
        Interactive.RunMainLoop()

    // finally we create a simple utility for changing the pointSize
    // you can play with it and see the render-result adjust to the given point-size.
    let setPointSize (s : float) =
        transact (fun () -> Mod.change pointSize s)


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

