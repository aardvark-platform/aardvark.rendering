namespace Aardvark.Application.OpenVR

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan
open Valve.VR
open Aardvark.Application
open Aardvark.Application.WinForms
open System.Windows.Forms
open Aardvark.SceneGraph

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

    let flip (v : Vertex) =
        vertex {
            let version : int = uniform?Version
            let zero = 1.0E-10 * float (version % 2)
            return { v with pos = V4d(1.0, -1.0, 1.0 + zero, 1.0) * v.pos }
        }

type VulkanVRApplicationLayered(samples : int, debug : bool) as this  =
    inherit VrRenderer()

    

    let app = new HeadlessVulkanApplication(debug, this.GetVulkanInstanceExtensions(), fun d -> this.GetVulkanDeviceExtensions d.Handle)
    let device = app.Device

    let mutable task = RenderTask.empty
    
    let mutable dImg = Unchecked.defaultof<Image>
    let mutable cImg = Unchecked.defaultof<Image>
    let mutable fbo = Unchecked.defaultof<Framebuffer>
    let mutable info = Unchecked.defaultof<VrRenderInfo>
    let mutable fImg = Unchecked.defaultof<Image>

    let start = System.DateTime.Now
    let sw = System.Diagnostics.Stopwatch.StartNew()
    let time = Mod.custom(fun _ -> start + sw.Elapsed)

    let renderPass = 
        device.CreateRenderPass(
            Map.ofList [
                DefaultSemantic.Colors, { format = RenderbufferFormat.Rgba8; samples = samples }
                DefaultSemantic.Depth, { format = RenderbufferFormat.Depth24Stencil8; samples = samples }
            ],
            2,
            Set.ofList [
                "ViewTrafo"; "ProjTrafo"; 
                "ModelViewTrafo"; "ViewProjTrafo"; 
                "ModelViewProjTrafo"; 
                "ViewTrafoInv"; "ProjTrafoInv"; 
                "ModelViewTrafoInv"; "ViewProjTrafoInv"; 
                "ModelViewProjTrafoInv"
            ]
        )

    let caller = AdaptiveObject()

    let version = Mod.init 0
    let tex = Mod.custom (fun _ -> fImg :> ITexture)


    let keyboard = new EventKeyboard()
    let mouse = new EventMouse(false)

    new(samples) = VulkanVRApplicationLayered(samples, false)
    new(debug) = VulkanVRApplicationLayered(1, debug)
    new() = VulkanVRApplicationLayered(1, false)

    member x.Version = version
    member x.Texture = tex


    member x.FramebufferSignature = renderPass :> IFramebufferSignature
    member x.Runtime = app.Runtime
    member x.Sizes = Mod.constant x.DesiredSize
    member x.Samples = samples
    member x.Time = time

    member x.ShowWindow() =
        async {
            let d = new Form()
            
            
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

            let task =
                Sg.fullScreenQuad
                    |> Sg.diffuseTexture x.Texture
                    |> Sg.uniform "Version" x.Version
                    |> Sg.shader {
                        do! StereoShader.flip
                        do! DefaultSurfaces.diffuseTexture
                    }

                    |> Sg.compile app.Runtime impl.FramebufferSignature

            impl.RenderTask <-task
            impl.Dock <- System.Windows.Forms.DockStyle.Fill
            d.Controls.Add impl

            impl.KeyDown.Add (fun k ->
                if k.KeyCode = Keys.Escape then
                    d.Close()
            )

            System.Windows.Forms.Application.Run(d)
            x.Shutdown()

        } |> Async.Start


    member x.RenderTask
        with get() = task
        and set t = task <- t

    override x.OnLoad(i : VrRenderInfo) : VrTexture * VrTexture =
        info <- i

        let nImg = 
            device.CreateImage(
                V3i(info.framebufferSize, 1),
                1, 2, samples, 
                TextureDimension.Texture2D,
                TextureFormat.Rgba8,
                VkImageUsageFlags.ColorAttachmentBit ||| VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.SampledBit
            )

        let nfImg =
            device.CreateImage(
                V3i(info.framebufferSize * V2i(2,1), 1),
                1, 1, 1, 
                TextureDimension.Texture2D,
                TextureFormat.Rgba8,
                VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.SampledBit
            )

            

        let nDepth =
            device.CreateImage(
                V3i(info.framebufferSize, 1),
                1, 2, samples, 
                TextureDimension.Texture2D,
                TextureFormat.Depth24Stencil8,
                VkImageUsageFlags.DepthStencilAttachmentBit ||| VkImageUsageFlags.TransferDstBit
            )
            
        let cView = device.CreateOutputImageView(nImg, 0, 1, 0, 2)
        let dView = device.CreateOutputImageView(nDepth, 0, 1, 0, 2)

        let nFbo =
            device.CreateFramebuffer(
                renderPass, 
                Map.ofList [
                    DefaultSemantic.Colors, cView
                    DefaultSemantic.Depth, dView
                ]
            )


        dImg <- nDepth
        cImg <- nImg
        fImg <- nfImg
        fbo <- nFbo

        let queue = device.GraphicsFamily.GetQueue().Handle

        let fTex = 
            VRVulkanTextureData_t(
                m_nImage = uint64 fImg.Handle.Handle,
                m_pDevice = device.Handle,
                m_pPhysicalDevice = device.PhysicalDevice.Handle,
                m_pInstance = device.Instance.Handle,
                m_pQueue = queue,
                m_nQueueFamilyIndex = uint32 device.GraphicsFamily.Index,
                m_nWidth = uint32 fImg.Size.X, 
                m_nHeight = uint32 fImg.Size.Y,
                m_nFormat = uint32 fImg.Format,
                m_nSampleCount = uint32 1
            )

            
        device.perform {
            do! Command.TransformLayout(dImg, VkImageLayout.DepthStencilAttachmentOptimal)
        }

        VrTexture.Vulkan(fTex, Box2d(V2d(0.0, 1.0), V2d(0.5, 0.0))), VrTexture.Vulkan(fTex, Box2d(V2d(0.5, 1.0), V2d(1.0, 0.0)))

    override x.Render() = 
//        let view, lProj, rProj =
//            caller.EvaluateAlways AdaptiveToken.Top (fun t ->
//                let view = info.viewTrafo.GetValue t
//                let lProj = info.lProjTrafo.GetValue t
//                let rProj = info.rProjTrafo.GetValue t
//
//                view, lProj, rProj
//            )

        let output = OutputDescription.ofFramebuffer fbo

        device.perform {
            do! Command.TransformLayout(dImg, VkImageLayout.TransferDstOptimal)
            do! Command.TransformLayout(cImg, VkImageLayout.TransferDstOptimal)

            do! Command.ClearColor(cImg.[ImageAspect.Color, 0, *], C4f.Black)
            do! Command.ClearDepthStencil(dImg.[ImageAspect.DepthStencil, 0, *], 1.0, 0u)
            
            do! Command.TransformLayout(dImg, VkImageLayout.DepthStencilAttachmentOptimal)
            do! Command.TransformLayout(cImg, VkImageLayout.ColorAttachmentOptimal)

        }

        caller.EvaluateAlways AdaptiveToken.Top (fun t ->
            task.Run(t, RenderToken.Empty, output)
        )
        let a = cImg.[ImageAspect.Color, *, 0]

        let srcBox = Box3i(V3i(0,info.framebufferSize.Y - 1,0), V3i(info.framebufferSize.X - 1, 0, 0))
        let lBox = Box3i(V3i.Zero, V3i(info.framebufferSize - V2i.II, 0))
        let rBox = Box3i(V3i(info.framebufferSize.X, 0, 0), V3i(2*info.framebufferSize.X - 1, info.framebufferSize.Y-1, 0))

        device.perform {
            do! Command.TransformLayout(cImg, VkImageLayout.TransferSrcOptimal)

            //do! Command.Blit(cImg.[ImageAspect.Color, 0, 0], srcBox, fImg.[ImageAspect.Color, 0, 0], lBox, VkFilter.Nearest)
            //do! Command.Blit(cImg.[ImageAspect.Color, 0, 1], srcBox, fImg.[ImageAspect.Color, 0, 0], rBox, VkFilter.Nearest)

            if samples > 1 then
                do! Command.ResolveMultisamples(cImg.[ImageAspect.Color, 0, 0], V3i.Zero, fImg.[ImageAspect.Color, 0, 0], V3i.Zero, V3i(info.framebufferSize, 1))
                do! Command.ResolveMultisamples(cImg.[ImageAspect.Color, 0, 1], V3i.Zero, fImg.[ImageAspect.Color, 0, 0], V3i(info.framebufferSize.X, 0, 0), V3i(info.framebufferSize, 1))
            else
                do! Command.Copy(cImg.[ImageAspect.Color, 0, 0], V3i.Zero, fImg.[ImageAspect.Color, 0, 0], V3i.Zero, V3i(info.framebufferSize, 1))
                do! Command.Copy(cImg.[ImageAspect.Color, 0, 1], V3i.Zero, fImg.[ImageAspect.Color, 0, 0], V3i(info.framebufferSize.X, 0, 0), V3i(info.framebufferSize, 1))
             
            do! Command.TransformLayout(fImg, VkImageLayout.TransferSrcOptimal)
        }

        transact (fun () -> time.MarkOutdated(); version.Value <- version.Value + 1)

    override x.Release() = 
        // delete views
        device.Delete fbo.Attachments.[DefaultSemantic.Colors]
        device.Delete fbo.Attachments.[DefaultSemantic.Depth]

        // delete FBOs
        device.Delete fbo

        // delete images
        device.Delete cImg
        device.Delete dImg
        device.Delete fImg

        // dispose the app
        app.Dispose()

    interface IRenderTarget with
        member x.Runtime = app.Runtime :> IRuntime
        member x.Sizes = Mod.constant x.DesiredSize
        member x.Samples = samples
        member x.FramebufferSignature = x.FramebufferSignature
        member x.RenderTask
            with get() = x.RenderTask
            and set t = x.RenderTask <- t
        member x.Time = time

    interface IRenderControl with
        member x.Keyboard = keyboard :> IKeyboard
        member x.Mouse = mouse :> IMouse

    interface IRenderWindow with
        member x.Run() = x.Run()