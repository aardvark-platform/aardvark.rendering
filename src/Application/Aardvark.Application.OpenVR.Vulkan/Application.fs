namespace Aardvark.Application.OpenVR

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan
open Valve.VR
open Aardvark.Application
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open Valve.VR

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


    type HiddenVertex =
        {
            [<Position>]
            pos : V4d

            [<Semantic("EyeIndex"); Interpolation(InterpolationMode.Flat)>]
            eyeIndex : int

            [<Layer>]
            layer : int
        }

    let hiddenAreaFragment (t : HiddenVertex) =
        fragment {
            if t.layer <> t.eyeIndex then
                discard()

            return V4d.IIII
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
    let mutable hiddenTask = RuntimeCommand.Empty

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

    let beforeRender = Event<unit>()
    let afterRender = Event<unit>()
    let mutable userCmd = RuntimeCommand.Empty
    let mutable loaded = false

    let compileHidden (m : IndexedGeometry) =
        let writeStencil =
            StencilMode(
                StencilOperation(
                    StencilOperationFunction.Replace,
                    StencilOperationFunction.Replace,
                    StencilOperationFunction.Replace
                ),
                StencilFunction(
                    StencilCompareFunction.Always,
                    1,
                    0xFFFFFFFFu
                )
            )

        let sg =
            Sg.ofIndexedGeometry m
                |> Sg.shader {
                    do! StereoShader.hiddenAreaFragment
                }
                |> Sg.stencilMode (Mod.constant writeStencil)
                |> Sg.writeBuffers' (Set.ofList [DefaultSemantic.Stencil])

        hiddenTask <- RuntimeCommand.Render(sg.RenderObjects())

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
        with set (t : RuntimeCommand) = 
            userCmd <- t
            if loaded then
                let list = AList.ofList [ hiddenTask; t ]
                task <- new Aardvark.Rendering.Vulkan.Temp.CommandTask(app.Device, renderPass, RuntimeCommand.Ordered list)
                
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
                VkImageUsageFlags.ColorAttachmentBit ||| VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.SampledBit
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

        let queue = device.GraphicsFamily.Queues |> List.head

        let fTex = 
            VRVulkanTextureData_t(
                m_nImage = uint64 fImg.Handle.Handle,
                m_pDevice = device.Handle,
                m_pPhysicalDevice = device.PhysicalDevice.Handle,
                m_pInstance = device.Instance.Handle,
                m_pQueue = queue.Handle,
                m_nQueueFamilyIndex = uint32 device.GraphicsFamily.Index,
                m_nWidth = uint32 fImg.Size.X, 
                m_nHeight = uint32 fImg.Size.Y,
                m_nFormat = uint32 fImg.Format,
                m_nSampleCount = uint32 1
            )

            
        device.perform {
            do! Command.TransformLayout(dImg, VkImageLayout.DepthStencilAttachmentOptimal)
        }

        
        compileHidden x.HiddenAreaMesh
        
        let list = AList.ofList [ hiddenTask; userCmd ]
        task <- new Aardvark.Rendering.Vulkan.Temp.CommandTask(app.Device, renderPass, RuntimeCommand.Ordered list)
        loaded <- true

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

            do! Command.ClearColor(cImg.[ImageAspect.Color, 0, *], x.BackgroundColor)
            do! Command.ClearDepthStencil(dImg.[ImageAspect.DepthStencil, 0, *], 1.0, 0u)
            
            do! Command.TransformLayout(dImg, VkImageLayout.DepthStencilAttachmentOptimal)
            do! Command.TransformLayout(cImg, VkImageLayout.ColorAttachmentOptimal)

        }

        caller.EvaluateAlways AdaptiveToken.Top (fun t ->
            task.Run(t, RenderToken.Empty, output)
        )
        let a = cImg.[ImageAspect.Color, *, 0]

        if device.AllCount > 1u then

            device.perform {
                for di in 0 .. int device.AllCount - 1 do
                    do! Command.SetDeviceMask (1u <<< di)

                    do! Command.TransformLayout(cImg, VkImageLayout.TransferSrcOptimal)

                    if samples > 1 then
                        if di = 0 then
                            do! Command.ResolveMultisamples(cImg.[ImageAspect.Color, 0, 0], V3i.Zero, fImg.[ImageAspect.Color, 0, 0], V3i.Zero, V3i(info.framebufferSize, 1))
                        else
                            do! Command.ResolveMultisamples(cImg.[ImageAspect.Color, 0, 1], V3i.Zero, fImg.[ImageAspect.Color, 0, 0], V3i(info.framebufferSize.X, 0, 0), V3i(info.framebufferSize, 1))
                    else
                        if di = 0 then
                            do! Command.Copy(cImg.[ImageAspect.Color, 0, 0], V3i.Zero, fImg.[ImageAspect.Color, 0, 0], V3i.Zero, V3i(info.framebufferSize, 1))
                        else
                            do! Command.Copy(cImg.[ImageAspect.Color, 0, 1], V3i.Zero, fImg.[ImageAspect.Color, 0, 0], V3i(info.framebufferSize.X, 0, 0), V3i(info.framebufferSize, 1))
             
                    do! Command.TransformLayout(fImg, VkImageLayout.TransferSrcOptimal)

                do! Command.SetDeviceMask 3u
                do! Command.SyncPeersDefault(fImg)
            }
        else
            device.perform {
                do! Command.TransformLayout(cImg, VkImageLayout.TransferSrcOptimal)
                if samples > 1 then
                    do! Command.ResolveMultisamples(cImg.[ImageAspect.Color, 0, 0], V3i.Zero, fImg.[ImageAspect.Color, 0, 0], V3i.Zero, V3i(info.framebufferSize, 1))
                    do! Command.ResolveMultisamples(cImg.[ImageAspect.Color, 0, 1], V3i.Zero, fImg.[ImageAspect.Color, 0, 0], V3i(info.framebufferSize.X, 0, 0), V3i(info.framebufferSize, 1))
                else
                    do! Command.Copy(cImg.[ImageAspect.Color, 0, 0], V3i.Zero, fImg.[ImageAspect.Color, 0, 0], V3i.Zero, V3i(info.framebufferSize, 1))
                    do! Command.Copy(cImg.[ImageAspect.Color, 0, 1], V3i.Zero, fImg.[ImageAspect.Color, 0, 0], V3i(info.framebufferSize.X, 0, 0), V3i(info.framebufferSize, 1))
             
                do! Command.TransformLayout(fImg, VkImageLayout.TransferSrcOptimal)
            }
            

            //Command.SyncPeersDefault(cImg)

//        let srcBox = Box3i(V3i(0,info.framebufferSize.Y - 1,0), V3i(info.framebufferSize.X - 1, 0, 0))
//        let lBox = Box3i(V3i.Zero, V3i(info.framebufferSize - V2i.II, 0))
//        let rBox = Box3i(V3i(info.framebufferSize.X, 0, 0), V3i(2*info.framebufferSize.X - 1, info.framebufferSize.Y-1, 0))

//        device.perform {
//            do! Command.TransformLayout(cImg, VkImageLayout.TransferSrcOptimal)
//
//            if samples > 1 then
//                do! Command.ResolveMultisamples(cImg.[ImageAspect.Color, 0, 0], V3i.Zero, fImg.[ImageAspect.Color, 0, 0], V3i.Zero, V3i(info.framebufferSize, 1))
//                do! Command.ResolveMultisamples(cImg.[ImageAspect.Color, 0, 1], V3i.Zero, fImg.[ImageAspect.Color, 0, 0], V3i(info.framebufferSize.X, 0, 0), V3i(info.framebufferSize, 1))
//            else
//                do! Command.Copy(cImg.[ImageAspect.Color, 0, 0], V3i.Zero, fImg.[ImageAspect.Color, 0, 0], V3i.Zero, V3i(info.framebufferSize, 1))
//                do! Command.Copy(cImg.[ImageAspect.Color, 0, 1], V3i.Zero, fImg.[ImageAspect.Color, 0, 0], V3i(info.framebufferSize.X, 0, 0), V3i(info.framebufferSize, 1))
//             
//            do! Command.TransformLayout(fImg, VkImageLayout.TransferSrcOptimal)
//        }

        transact (fun () -> time.MarkOutdated(); version.Value <- version.Value + 1)

    override x.Release() = 
//        hiddenTask.Dispose()
//        hiddenTask <- RenderTask.empty
//        
//        overlayTask.Dispose()
//        overlayTask <- RenderTask.empty


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
        
    member x.BeforeRender = beforeRender
    member x.AfterRender = afterRender

    interface IRenderTarget with
        member x.Runtime = app.Runtime :> IRuntime
        member x.Sizes = Mod.constant x.DesiredSize
        member x.Samples = samples
        member x.FramebufferSignature = x.FramebufferSignature
        member x.RenderTask
            with get() = RenderTask.empty
            and set t = () //x.RenderTask <- t
        member x.Time = time
        member x.BeforeRender = beforeRender.Publish
        member x.AfterRender = afterRender.Publish

    interface IRenderControl with
        member x.Keyboard = keyboard :> IKeyboard
        member x.Mouse = mouse :> IMouse

    interface IRenderWindow with
        member x.Run() = x.Run()