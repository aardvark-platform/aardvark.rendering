namespace Aardvark.Application.OpenVR

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan
open Valve.VR

type VulkanVRApplication(debug : bool) =
    inherit VrRenderer()

    let app = new HeadlessVulkanApplication(debug)
    let device = app.Device

    let mutable task = RenderTask.empty
    
    let mutable lFbo = Unchecked.defaultof<Framebuffer>
    let mutable rFbo = Unchecked.defaultof<Framebuffer>
    let mutable info = Unchecked.defaultof<VrRenderInfo>

    let renderPass = 
        device.CreateRenderPass 
            <| Map.ofList [
                DefaultSemantic.Colors, { format = RenderbufferFormat.Rgba8; samples = 1 }
                DefaultSemantic.Depth, { format = RenderbufferFormat.Depth24Stencil8; samples = 1 }
              ]

    let caller = AdaptiveObject()

    member x.Runtime = app.Runtime

    member x.RenderTask
        with get() = task
        and set t = task <- t

    override x.OnLoad(i : VrRenderInfo) : VrTexture * VrTexture =
        info <- i

        let lImg = 
            device.CreateImage(
                V3i(info.framebufferSize, 1),
                1, 1, 1, 
                TextureDimension.Texture2D,
                TextureFormat.Rgba8,
                VkImageUsageFlags.ColorAttachmentBit ||| VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.SampledBit
            )

        let rImg = 
            device.CreateImage(
                V3i(info.framebufferSize, 1),
                1, 1, 1, 
                TextureDimension.Texture2D,
                TextureFormat.Rgba8,
                VkImageUsageFlags.ColorAttachmentBit ||| VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.SampledBit
            )

        let depth =
            device.CreateImage(
                V3i(info.framebufferSize, 1),
                1, 1, 1, 
                TextureDimension.Texture2D,
                TextureFormat.Depth24Stencil8,
                VkImageUsageFlags.DepthStencilAttachmentBit ||| VkImageUsageFlags.TransferDstBit
            )
            
        let lView = device.CreateOutputImageView(lImg)
        let rView = device.CreateOutputImageView(rImg)
        let depthView = device.CreateOutputImageView(depth)

        let nlFbo =
            device.CreateFramebuffer(
                renderPass, 
                Map.ofList [
                    DefaultSemantic.Colors, lView
                    DefaultSemantic.Depth, depthView
                ]
            )

        let nrFbo =
            device.CreateFramebuffer(
                renderPass, 
                Map.ofList [
                    DefaultSemantic.Colors, rView
                    DefaultSemantic.Depth, depthView
                ]
            )

        lFbo <- nlFbo
        rFbo <- nrFbo

        let queue = device.GraphicsFamily.GetQueue().Handle

        let lTex = 
            VRVulkanTextureData_t(
                m_nImage = uint64 lImg.Handle.Handle,
                m_pDevice = device.Handle,
                m_pPhysicalDevice = device.PhysicalDevice.Handle,
                m_pInstance = device.Instance.Handle,
                m_pQueue = queue,
                m_nQueueFamilyIndex = uint32 device.GraphicsFamily.Index,
                m_nWidth = uint32 lImg.Size.X, 
                m_nHeight = uint32 lImg.Size.Y,
                m_nFormat = uint32 lImg.Format,
                m_nSampleCount = 1u
            )

        let rTex = 
            VRVulkanTextureData_t(
                m_nImage = uint64 rImg.Handle.Handle,
                m_pDevice = device.Handle,
                m_pPhysicalDevice = device.PhysicalDevice.Handle,
                m_pInstance = device.Instance.Handle,
                m_pQueue = queue,
                m_nQueueFamilyIndex = uint32 device.GraphicsFamily.Index,
                m_nWidth = uint32 rImg.Size.X, 
                m_nHeight = uint32 rImg.Size.Y,
                m_nFormat = uint32 rImg.Format,
                m_nSampleCount = 1u
            )

        VrTexture.Vulkan lTex, VrTexture.Vulkan rTex


    override x.Render() = 
        let view, lProj, rProj =
            caller.EvaluateAlways AdaptiveToken.Top (fun t ->
                let view = info.viewTrafo.GetValue t
                let lProj = info.lProjTrafo.GetValue t
                let rProj = info.rProjTrafo.GetValue t

                view, lProj, rProj
            )

        let lOutput = { OutputDescription.ofFramebuffer lFbo with overrides = Map.ofList [ "ViewTrafo", view :> obj; "ProjTrafo", lProj :> obj ] }
        let rOutput = { OutputDescription.ofFramebuffer rFbo with overrides = Map.ofList [ "ViewTrafo", view :> obj; "ProjTrafo", rProj :> obj ] }

        task.Run(AdaptiveToken.Top, RenderToken.Empty, lOutput)
        task.Run(AdaptiveToken.Top, RenderToken.Empty, rOutput)
        ()

    override x.Release() = 
        Log.warn "please dispose"
        ()

