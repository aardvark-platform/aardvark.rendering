namespace Aardvark.Application.OpenVR

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan
open Valve.VR
open Aardvark.Application

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
        device.CreateRenderPass 
            <| Map.ofList [
                DefaultSemantic.Colors, { format = RenderbufferFormat.Rgba8; samples = samples }
                DefaultSemantic.Depth, { format = RenderbufferFormat.Depth24Stencil8; samples = samples }
              ]

    let caller = AdaptiveObject()

    let version = Mod.init 0
    let tex = Mod.custom (fun _ -> fImg :> ITexture)

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
                1, 1, samples, 
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
                m_nSampleCount = uint32 samples
            )

            
        device.perform {
            do! Command.TransformLayout(dImg, VkImageLayout.DepthStencilAttachmentOptimal)
        }

        VrTexture.Vulkan(fTex, Box2d(V2d.OO, V2d(0.5, 1.0))), VrTexture.Vulkan(fTex, Box2d(V2d(0.5, 0.0), V2d.II))

    override x.Render() = 
        let view, lProj, rProj =
            caller.EvaluateAlways AdaptiveToken.Top (fun t ->
                let view = info.viewTrafo.GetValue t
                let lProj = info.lProjTrafo.GetValue t
                let rProj = info.rProjTrafo.GetValue t

                view, lProj, rProj
            )

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

            do! Command.Blit(cImg.[ImageAspect.Color, 0, 0], srcBox, fImg.[ImageAspect.Color, 0, 0], lBox, VkFilter.Nearest)
            do! Command.Blit(cImg.[ImageAspect.Color, 0, 1], srcBox, fImg.[ImageAspect.Color, 0, 0], rBox, VkFilter.Nearest)

            //do! Command.Copy(cImg.[ImageAspect.Color, 0, 0], V3i.Zero, fImg.[ImageAspect.Color, 0, 0], V3i.Zero, V3i(info.framebufferSize, 1))
            //do! Command.Copy(cImg.[ImageAspect.Color, 0, 1], V3i.Zero, fImg.[ImageAspect.Color, 0, 0], V3i(info.framebufferSize.X, 0, 0), V3i(info.framebufferSize, 1))
            
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