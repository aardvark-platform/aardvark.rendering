namespace Examples


open System
open Aardvark.Base
open FSharp.Data.Adaptive

open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open FSharp.Data.Adaptive.Operators
open Aardvark.Rendering
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
            let margin : float = uniform?Margin
            if v.tc.X > 0.5 + (margin / 2.0) then 
                let tc = V2d(2.0 * (v.tc.X - 0.5), v.tc.Y)
                return inputSampler.Sample(tc, 1)
            elif v.tc.X < 0.5 - (margin / 2.0) then
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

        let lines = AVal.init []
        let mutable lineCount = 0
        let maxLines = 30

        let text = lines |> AVal.map (List.rev >> String.concat "\r\n")

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
        
        app.System.Controllers |> Seq.iter (fun c ->
            c.Axis |> Array.iter (fun a ->
                a.Press.Add ( fun _ -> captain.WriteLine("arrrr {0}", string a))
            )
        )

        async {
            let d = new System.Windows.Forms.Form()
            
            
            //d.ClientSize <- Drawing.Size(2 * app.DesiredSize.X, app.DesiredSize.Y)
            d.WindowState <- FormWindowState.Maximized
            d.FormBorderStyle <- FormBorderStyle.None

            let mode = GraphicsMode(Col.Format.RGBA, 8, 24, 8, 2, 8, ImageTrafo.MirrorY, false)
            let impl = new VulkanRenderControl(app.Runtime, mode)


            let consoleTrafo = 
                impl.Sizes |> AVal.map (fun (s : V2i) -> 
                    Trafo3d.Scale(float s.Y / float s.X, 1.0, 1.0) *
                    Trafo3d.Translation(-0.95, 0.9, 0.0)
                )

            let helpTrafo = 
                impl.Sizes |> AVal.map (fun s -> 
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
                Sg.text font C4b.White (AVal.constant "press ESC to exit")
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

            //impl.RenderTask <-task
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
                    //do! StereoShader.trafo
                    //do! DefaultSurfaces.constantColor C4f.Red
                    do! DefaultSurfaces.simpleLighting
                }
                |> Sg.viewTrafo (info.viewTrafos |> AVal.map (Array.item 0))
                |> Sg.uniform "ViewTrafo" info.viewTrafos
                |> Sg.uniform "ProjTrafo" info.projTrafos
                |> Sg.compile app.Runtime app.FramebufferSignature

        //app.RenderTask <- task


        app.Run()
        
        ()

    let runNew() =
        let font = Font "Consolas"

        let active = AVal.init true

        let run =
            async {
                do! Async.SwitchToThreadPool()
                while true do
                    let res = System.Console.ReadLine()
                    transact (fun () -> 
                        match res with
                            | "a" -> active.Value <- true
                            | _ -> active.Value <- not active.Value
                        printfn "%A" active.Value
                    )
            }

        Async.Start run

        show {
            display Display.Stereo
            samples 8
            backend Backend.Vulkan
            debug false

            scene (
                Sg.ofList [
                    Sg.box' C4b.Red Box3d.Unit
                        |> Sg.onOff active

                    Sg.unitSphere' 5 C4b.Blue 
                        |> Sg.scale 0.5
                        |> Sg.translate 0.0 0.0 2.0

                    Sg.text font C4b.White ~~"test"
                        |> Sg.transform (Trafo3d.FromBasis(V3d.IOO, V3d.OOI, V3d.OIO, 3.0 * V3d.OOI))
                ]

                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.simpleLighting
                }
            )
        }

    let run() =
        let app = new VulkanApplication(true)
        let win = app.CreateSimpleRenderWindow(1)
        let runtime = app.Runtime :> IRuntime


        let samples = 8

        let signature =
            runtime.CreateFramebufferSignature(
                SymDict.ofList [
                    DefaultSemantic.Colors, { format = TextureFormat.Rgba8; samples = samples }
                    DefaultSemantic.Depth, { format = TextureFormat.Depth24Stencil8; samples = samples }
                ],
                2, 
                Set.ofList [
                    "ProjTrafo"; 
                    "ViewTrafo"; 
                    "ModelViewTrafo"; 
                    "ViewProjTrafo"; 
                    "ModelViewProjTrafo"
                    
                    "ProjTrafoInv"; 
                    "ViewTrafoInv"; 
                    "ModelViewTrafoInv"; 
                    "ViewProjTrafoInv"; 
                    "ModelViewProjTrafoInv"
                ]
            )    

        //let size = V2i(960, 1080)

        let s = win.Sizes |> AVal.map (fun s -> s / V2i(2,1))

        let colors =
            runtime.CreateTexture2DArray(s, TextureFormat.Rgba8, samples = samples, count = 2)

        let depth =
            runtime.CreateTexture2DArray(s, TextureFormat.Depth24Stencil8, samples = samples, count = 2)

        let resolved =
            runtime.CreateTexture2DArray(s, TextureFormat.Rgba8, count = 2)

        let framebuffer =
            runtime.CreateFramebuffer(signature, [
                DefaultSemantic.Colors, runtime.CreateTextureAttachment(colors)
                DefaultSemantic.Depth, runtime.CreateTextureAttachment(depth)
            ])

        let cameraView = 
            CameraView.lookAt (V3d(6.0, 6.0, 6.0)) V3d.Zero V3d.OOI
                |> DefaultCameraController.control win.Mouse win.Keyboard win.Time
                |> AVal.map CameraView.viewTrafo

        let near = 0.1
        let far = 100.0

        let views =
            cameraView |> AVal.map (fun view ->
                [| 
                    view * Trafo3d.Translation(0.05, 0.0, 0.0)
                    view * Trafo3d.Translation(-0.05, 0.0, 0.0)
                |]
            )

        let projs =
            // taken from oculus rift
            let outer = 1.0537801252809621805875367233154
            let inner = 0.77567951104961310377955052031392

            win.Sizes |> AVal.map (fun size ->
                let aspect = float size.X / float size.Y 
                let y = tan (120.0 * Constant.RadiansPerDegree / 2.0) / aspect //(outer + inner) / (2.0 * aspect)d

                [|
                    { left = -outer * near; right = inner * near; top = y * near; bottom = -y * near; near = near; far = far; isOrtho = false } |> Frustum.projTrafo 
                    { left = -inner * near; right = outer * near; top = y * near; bottom = -y * near; near = near; far = far; isOrtho = false } |> Frustum.projTrafo 
                |]
            )

        let font = Font("Consolas")

        let task =
            Sg.ofList [
                Sg.box' C4b.Red Box3d.Unit
                Sg.unitSphere' 5 C4b.Blue 
                    |> Sg.scale 0.5
                    |> Sg.translate 0.0 0.0 2.0

                Sg.text font C4b.White ~~"test"
                    |> Sg.transform (Trafo3d.FromBasis(V3d.IOO, V3d.OOI, -V3d.OIO, 3.0 * V3d.OOI))
            ]
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.simpleLighting
            }

            |> Sg.viewTrafo cameraView
            |> Sg.uniform "ProjTrafo" projs
            |> Sg.uniform "ViewTrafo" views
            |> Sg.compile runtime signature

        let clear = runtime.CompileClear(signature, AVal.constant C4f.Black, AVal.constant 1.0)
        resolved.Acquire()
        framebuffer.Acquire()

        let margin =
            AVal.custom (fun t ->
                let fbo = framebuffer.GetValue(t)
                let colors = colors.GetValue t |> unbox<IBackendTexture>
                let final = resolved.GetValue t |> unbox<IBackendTexture>

                let o = OutputDescription.ofFramebuffer fbo
                clear.Run(t, RenderToken.Empty, o)
                task.Run(t, RenderToken.Empty, o)

                runtime.Copy(colors, 0, 0, final, 0, 0, 2, 1)

                0.001
            )

        let final =
            Sg.fullScreenQuad
                |> Sg.shader {
                    do! StereoShader.sample
                }
                |> Sg.uniform "Margin" margin
                |> Sg.uniform "InputTexture" resolved //(AVal.constant (colors :> ITexture))
                |> Sg.compile runtime win.FramebufferSignature


        win.RenderTask <- final
        win.Run()
        framebuffer.Release()
        resolved.Release()
        System.Environment.Exit 0
