namespace Examples


open System
open Aardvark.Base
open Aardvark.Base.Incremental

open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering
open Aardvark.Base.ShaderReflection

module StereoShader =
    open FShade
    open FShade.Imperative

    type Vertex = 
        {
            [<Layer>]           layer   : int
            [<Position>]        pos     : V4d
            [<WorldPosition>]   wp      : V4d
            [<Normal>]          n       : V3d
            [<BiNormal>]        b       : V3d
            [<Tangent>]         t       : V3d
            [<Color>]           c       : V4d
            [<TexCoord>]        tc      : V2d
        }

    type UniformScope with
        member x.LeftProj : M44d = uniform?PerView?LeftProj
        member x.RightProj : M44d = uniform?PerView?RightProj
        

    let trafo (t : Triangle<Vertex>) =
        triangle {
            yield { t.P0 with layer = 0; pos = uniform.LeftProj * (uniform.ViewTrafo * t.P0.wp) }
            yield { t.P1 with layer = 0; pos = uniform.LeftProj * (uniform.ViewTrafo * t.P1.wp) }
            yield { t.P2 with layer = 0; pos = uniform.LeftProj * (uniform.ViewTrafo * t.P2.wp) }
            restartStrip()

            yield { t.P0 with layer = 1; pos = uniform.RightProj * (uniform.ViewTrafo * t.P0.wp) }
            yield { t.P1 with layer = 1; pos = uniform.RightProj * (uniform.ViewTrafo * t.P1.wp) }
            yield { t.P2 with layer = 1; pos = uniform.RightProj * (uniform.ViewTrafo * t.P2.wp) }
        }

    let inputSampler =
        sampler2dArray {
            texture uniform?InputTexture
            filter Filter.MinMagLinear
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
        }

    let sample (v : Effects.Vertex) =
        fragment {
            if v.tc.X > 0.505 then 
                let tc = V2d(2.0 * (v.tc.X - 0.5), v.tc.Y)
                return inputSampler.Sample(tc, 1)
            elif v.tc.X < 0.495 then
                let tc = V2d(2.0 * v.tc.X, v.tc.Y)
                return inputSampler.Sample(tc, 0)
            else
                return V4d.Zero
        }

module Stereo =
    let run() =
        let app = new VulkanApplication(false)
        let win = app.CreateSimpleRenderWindow(1)
        let runtime = app.Runtime :> IRuntime

        let signature =
            runtime.CreateFramebufferSignature(
                1,
                [
                    DefaultSemantic.Colors, RenderbufferFormat.Rgba8
                    DefaultSemantic.Depth, RenderbufferFormat.Depth24Stencil8
                ]
            )    

        let colors = runtime.CreateTextureArray(V2i(1024, 1024), TextureFormat.Rgba8, 1, 1, 2)
        let depth = runtime.CreateTextureArray(V2i(1024, 1024), TextureFormat.Depth24Stencil8, 1, 1, 2)


        let cameraView = 
            CameraView.lookAt (V3d(6.0, 6.0, 6.0)) V3d.Zero V3d.OOI
                |> DefaultCameraController.control win.Mouse win.Keyboard win.Time
                |> Mod.map CameraView.viewTrafo

        let projection = 
            win.Sizes 
                |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y))
                |> Mod.map Frustum.projTrafo


        let lProj =
            { left = -0.4; right = 0.1; top = 0.25; bottom = -0.25; near = 1.0; far = 100.0 } |> Frustum.projTrafo |> Mod.constant
            
        let rProj =
            { left = -0.1; right = 0.4; top = 0.25; bottom = -0.25; near = 1.0; far = 100.0 } |> Frustum.projTrafo |> Mod.constant


        let framebuffer =
            runtime.CreateFramebuffer(
                signature,
                [
                    DefaultSemantic.Colors, colors.[0] :> IFramebufferOutput
                    DefaultSemantic.Depth, depth.[0]:> IFramebufferOutput
                ]
            )

        let task =
            Sg.box' C4b.Red Box3d.Unit
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! StereoShader.trafo
                    do! DefaultSurfaces.constantColor C4f.Red
                    //do! DefaultSurfaces.simpleLighting
                }
                |> Sg.viewTrafo cameraView
                |> Sg.uniform "LeftProj" lProj
                |> Sg.uniform "RightProj" rProj
                |> Sg.projTrafo lProj
                |> Sg.compile runtime signature

        let clear = runtime.CompileClear(signature, Mod.constant C4f.Green, Mod.constant 1.0)

        let result =
            Mod.custom (fun t ->
                let o = OutputDescription.ofFramebuffer framebuffer
                clear.Run(t, RenderToken.Empty, o)
                task.Run(t, RenderToken.Empty, o)
                colors :> ITexture
            )

        let final =
            Sg.fullScreenQuad
                |> Sg.shader {
                    do! StereoShader.sample
                }
                |> Sg.uniform "InputTexture" result //(Mod.constant (colors :> ITexture))
                |> Sg.compile runtime win.FramebufferSignature

//        let task0 = 
//            RenderTask.custom (fun _ ->
//                let o = OutputDescription.ofFramebuffer framebuffer
//                clear.Run(AdaptiveToken.Top, RenderToken.Empty, o)
//                task.Run(AdaptiveToken.Top, RenderToken.Empty, o)
//            )

        win.RenderTask <- final //RenderTask.ofList [task0; final]
        win.Run()
