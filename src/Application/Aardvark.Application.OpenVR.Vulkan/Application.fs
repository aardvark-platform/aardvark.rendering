namespace Aardvark.Application.OpenVR

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan
open Valve.VR
open Aardvark.Application

type VulkanVRApplication(samples : int, debug : bool) =
    inherit VrRenderer()

    let app = new HeadlessVulkanApplication(debug)
    let device = app.Device

    let mutable task = RenderTask.empty
    
    let mutable dImg = Unchecked.defaultof<Image>
    let mutable lImg = Unchecked.defaultof<Image>
    let mutable rImg = Unchecked.defaultof<Image>
    let mutable lFbo = Unchecked.defaultof<Framebuffer>
    let mutable rFbo = Unchecked.defaultof<Framebuffer>
    let mutable info = Unchecked.defaultof<VrRenderInfo>

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

    new(samples) = VulkanVRApplication(samples, false)
    new(debug) = VulkanVRApplication(1, debug)
    new() = VulkanVRApplication(1, false)


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

        let lnImg = 
            device.CreateImage(
                V3i(info.framebufferSize, 1),
                1, 1, samples, 
                TextureDimension.Texture2D,
                TextureFormat.Rgba8,
                VkImageUsageFlags.ColorAttachmentBit ||| VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.SampledBit
            )

        let rnImg = 
            device.CreateImage(
                V3i(info.framebufferSize, 1),
                1, 1, samples, 
                TextureDimension.Texture2D,
                TextureFormat.Rgba8,
                VkImageUsageFlags.ColorAttachmentBit ||| VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.SampledBit
            )

        let depth =
            device.CreateImage(
                V3i(info.framebufferSize, 1),
                1, 1, samples, 
                TextureDimension.Texture2D,
                TextureFormat.Depth24Stencil8,
                VkImageUsageFlags.DepthStencilAttachmentBit ||| VkImageUsageFlags.TransferDstBit
            )
            
        let lView = device.CreateOutputImageView(lnImg)
        let rView = device.CreateOutputImageView(rnImg)
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

        dImg <- depth
        lImg <- lnImg
        rImg <- rnImg
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
                m_nSampleCount = uint32 samples
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
                m_nSampleCount = uint32 samples
            )

            
        device.perform {
            do! Command.TransformLayout(depth, VkImageLayout.DepthStencilAttachmentOptimal)
        }

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

        device.perform {
            do! Command.TransformLayout(lImg, VkImageLayout.ColorAttachmentOptimal)
            do! Command.TransformLayout(rImg, VkImageLayout.ColorAttachmentOptimal)
        }

        task.Run(AdaptiveToken.Top, RenderToken.Empty, lOutput)
        task.Run(AdaptiveToken.Top, RenderToken.Empty, rOutput)

        
        device.perform {
            do! Command.TransformLayout(lImg, VkImageLayout.TransferSrcOptimal)
            do! Command.TransformLayout(rImg, VkImageLayout.TransferSrcOptimal)
        }

        transact (fun () -> time.MarkOutdated())

    override x.Release() = 
        // delete views
        device.Delete lFbo.Attachments.[DefaultSemantic.Colors]
        device.Delete rFbo.Attachments.[DefaultSemantic.Colors]
        device.Delete lFbo.Attachments.[DefaultSemantic.Depth]

        // delete FBOs
        device.Delete lFbo
        device.Delete rFbo

        // delete images
        device.Delete lImg
        device.Delete rImg
        device.Delete dImg

    interface IRenderTarget with
        member x.Runtime = app.Runtime :> IRuntime
        member x.Sizes = Mod.constant x.DesiredSize
        member x.Samples = samples
        member x.FramebufferSignature = x.FramebufferSignature
        member x.RenderTask
            with get() = x.RenderTask
            and set t = x.RenderTask <- t
        member x.Time = time