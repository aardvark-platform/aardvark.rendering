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
open Aardvark.Rendering.Vulkan
open Aardvark.Rendering.Text

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

    let fancyColor (v : Vertex) =
        vertex {
            return { v with c = V4d(v.pos.XYZ, 1.0) }
        }
    let flip (v : Vertex) =
        vertex {
            let version : int = uniform?Version
            let zero = 1.0E-10 * float (version % 2)
            return { v with pos = V4d(1.0, -1.0, 1.0 + zero, 1.0) * v.pos }
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
    open System.Windows.Forms
    open System.Drawing
    open Aardvark.Rendering.Text
    open System.IO
    open System.Text

    type Hook() =
        inherit TextWriter()

        let mutable currentLine = ""

        let lines = Mod.init []
        let mutable lineCount = 0
        let maxLines = 30

        let text = lines |> Mod.map (List.rev >> String.concat "\r\n")

        member x.Text = text

        override x.Encoding = Encoding.UTF8
        override x.Write(str : string) = currentLine <- currentLine + str
        override x.WriteLine(str : string) = currentLine <- str; x.WriteLine()
        override x.WriteLine() = 
            let str = currentLine
            //inner.WriteLine(str)
            currentLine <- ""

            transact (fun () ->
                let old = 
                    if lineCount >= maxLines then 
                        List.take (maxLines - 1) lines.Value
                    else 
                        lineCount <- lineCount + 1
                        lines.Value
                    
                lines.Value <- str :: old
            )
            



    open Aardvark.Application.OpenVR
    let runVive() =
        let captain = new Hook()

        let app = new VulkanVRApplicationLayered(true)
        
        app.Controllers |> Array.iter (fun c ->
            c.Axis |> Array.iter (fun a ->
                a.Press.Add ( fun _ -> captain.WriteLine("arrrr {0}", string a))
            )
        )

        async {
            let d = new System.Windows.Forms.Form()
            
            
            //d.ClientSize <- Drawing.Size(2 * app.DesiredSize.X, app.DesiredSize.Y)
            d.WindowState <- FormWindowState.Maximized
            d.FormBorderStyle <- FormBorderStyle.None

            let mode = GraphicsMode(Col.Format.RGBA, 8, 24, 8, 2, 8, ImageTrafo.MirrorY)
            let impl = new VulkanRenderControl(app.Runtime, mode)


            let consoleTrafo = 
                impl.Sizes |> Mod.map (fun s -> 
                    Trafo3d.Scale(float s.Y / float s.X, 1.0, 1.0) *
                    Trafo3d.Translation(-0.95, 0.9, 0.0)
                )

            let helpTrafo = 
                impl.Sizes |> Mod.map (fun s -> 
                    Trafo3d.Scale(float s.Y / float s.X, 1.0, 1.0) *
                    Trafo3d.Translation(-0.95, -0.95, 0.0)
                    
                )


            
            captain.WriteLine("HOOK")

            let font = Font("Consolas")
            let overlay1 =
                Sg.text font C4b.White (captain.Text)
                    |> Sg.scale 0.05
                    |> Sg.projTrafo consoleTrafo

            let overlay2 =
                Sg.text font C4b.White (Mod.constant "press ESC to exit")
                    |> Sg.scale 0.05
                    |> Sg.projTrafo helpTrafo
                
            let overlay = Sg.ofList [overlay1; overlay2]

            let task =
                Sg.fullScreenQuad
                    |> Sg.diffuseTexture app.Texture
                    |> Sg.uniform "Version" app.Version
                    |> Sg.shader {
                        do! StereoShader.flip
                        do! DefaultSurfaces.diffuseTexture
                    }

                    |> Sg.andAlso overlay
                    |> Sg.compile app.Runtime impl.FramebufferSignature

            impl.RenderTask <-task
            impl.Dock <- System.Windows.Forms.DockStyle.Fill
            d.Controls.Add impl

            impl.KeyDown.Add (fun k ->
                if k.KeyCode = Keys.Escape then
                    d.Close()
            )

            System.Windows.Forms.Application.Run(d)
            app.Shutdown()

        } |> Async.Start

        let info = app.Info

        let task =
            Sg.box' C4b.Red Box3d.Unit
                |> Sg.scale 2.0
                |> Sg.shader {
                    do! StereoShader.fancyColor
                    do! DefaultSurfaces.trafo
                    do! StereoShader.trafo
                    //do! DefaultSurfaces.constantColor C4f.Red
                    do! DefaultSurfaces.simpleLighting
                }
                |> Sg.viewTrafo info.viewTrafo
                |> Sg.uniform "LeftProj" info.lProjTrafo
                |> Sg.uniform "RightProj" info.rProjTrafo
                |> Sg.compile app.Runtime app.FramebufferSignature

        app.RenderTask <- task


        app.Run()
        
        ()

    let run() =
        let app = new VulkanApplication(false)
        let win = app.CreateSimpleRenderWindow(1)
        let runtime = app.Runtime :> IRuntime

        let signature =
            runtime.CreateFramebufferSignature(
                SymDict.ofList [
                    DefaultSemantic.Colors, { format = RenderbufferFormat.Rgba8; samples = 1 }
                    DefaultSemantic.Depth, { format = RenderbufferFormat.Depth24Stencil8; samples = 1 }
                ],
                Set.empty,
                2, 
                Set.ofList [
                    "ProjTrafo"; 
                    "ViewProjTrafo"; 
                    "ModelViewProjTrafo"

                    "ProjTrafoInv"; 
                    "ViewProjTrafoInv"; 
                    "ModelViewProjTrafoInv"
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
            { left = -0.4; right = 0.1; top = 0.25; bottom = -0.25; near = 1.0; far = 100.0 } |> Frustum.projTrafo 
            
        let rProj =
            { left = -0.1; right = 0.4; top = 0.25; bottom = -0.25; near = 1.0; far = 100.0 } |> Frustum.projTrafo


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
                    do! DefaultSurfaces.constantColor C4f.Red
                }
                |> Sg.viewTrafo cameraView
                |> Sg.uniform "ProjTrafo" (Mod.constant [| rProj; lProj |])
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
