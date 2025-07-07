namespace Aardvark.Application.OpenVR

open System.Diagnostics
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.Rendering.Vulkan
open Valve.VR
open Aardvark.Application
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open System.Runtime.InteropServices

module StereoShader =
    open FShade
    open FShade.Imperative

    type Vertex = 
        {
            [<Layer>]           layer   : int
            [<Position>]        pos     : V4f
            [<WorldPosition>]   wp      : V4f
            [<Normal>]          n       : V3f
            [<BiNormal>]        b       : V3f
            [<Tangent>]         t       : V3f
            [<Color>]           c       : V4f
            [<TexCoord>]        tc      : V2f
        }

    let flip (v : Vertex) =
        vertex {
            let version : int = uniform?Version
            let zero = 1.0E-10f * float32 (version % 2)
            return { v with pos = V4f(1.0f, -1.0f, 1.0f + zero, 1.0f) * v.pos }
        }


    type HiddenVertex =
        {
            [<Position>]
            pos : V4f

            [<Semantic("EyeIndex"); Interpolation(InterpolationMode.Flat)>]
            eyeIndex : int

            [<Layer>]
            layer : int
        }

    let hiddenAreaFragment (t : HiddenVertex) =
        fragment {
            if t.layer <> t.eyeIndex then
                discard()

            return V4f.IIII
        }

type private DummyObject() =
    inherit AdaptiveObject()

type VulkanVRApplicationLayered(debug: IDebugConfig, adjustSize: V2i -> V2i,
                                [<Optional; DefaultParameterValue(1)>] samples: int,
                                [<Optional; DefaultParameterValue(null : string seq)>] extensions: string seq,
                                [<Optional; DefaultParameterValue(null : IDeviceChooser)>] chooser: IDeviceChooser) as this =

    inherit VrRenderer(adjustSize)

    let instanceExtensions =
        let extensions = if extensions = null then Seq.empty else extensions
        this.GetVulkanInstanceExtensions() |> Seq.append extensions

    let deviceExtensions (device: PhysicalDevice) =
        this.GetVulkanDeviceExtensions device.Handle

    let app = new HeadlessVulkanApplication(debug, instanceExtensions, deviceExtensions, chooser)
    
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
    let time = AVal.custom(fun _ -> start + sw.Elapsed)

    let renderPass = 
        device.CreateRenderPass(
            [{ Name = DefaultSemantic.Colors; Format = TextureFormat.Rgba8 }],
            Some TextureFormat.Depth24Stencil8,
            samples,
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

    let caller = DummyObject()

    let version = AVal.init 0
    let tex = AVal.custom (fun _ -> fImg :> ITexture)


    let keyboard = new EventKeyboard()
    let mouse = new EventMouse(false)

    let beforeRender = Event<unit>()
    let afterRender = Event<unit>()
    let mutable userCmd = RuntimeCommand.Empty
    let mutable loaded = false

    let compileHidden (m : IndexedGeometry) =
        let writeStencil =
            { StencilMode.None with
                Pass = StencilOperation.Replace
                Fail = StencilOperation.Replace
                DepthFail = StencilOperation.Replace
                Comparison = ComparisonFunction.Always
                Reference = 1 }

        let sg =
            Sg.ofIndexedGeometry m
                |> Sg.shader {
                    do! StereoShader.hiddenAreaFragment
                }
                |> Sg.stencilMode' writeStencil
                |> Sg.writeBuffers' (Set.ofList [WriteBuffer.Stencil])

        hiddenTask <- RuntimeCommand.Render(sg.RenderObjects(Ag.Scope.Root))

    let swClear     = Stopwatch()
    let swRender    = Stopwatch()
    let swResolve   = Stopwatch()
    let swTotal     = Stopwatch()
    
    let queueHandle = device.GraphicsFamily.CurrentQueue
    let queue = queueHandle.Queue

    new(debug: IDebugConfig,
        [<Optional; DefaultParameterValue(1)>] samples: int,
        [<Optional; DefaultParameterValue(null : string seq)>] extensions: string seq,
        [<Optional; DefaultParameterValue(null : IDeviceChooser)>] chooser: IDeviceChooser) =
        new VulkanVRApplicationLayered(debug, id, samples, extensions, chooser)

    new(debug: bool, adjustSize: V2i -> V2i,
        [<Optional; DefaultParameterValue(1)>] samples: int,
        [<Optional; DefaultParameterValue(null : string seq)>] extensions: string seq,
        [<Optional; DefaultParameterValue(null : IDeviceChooser)>] chooser: IDeviceChooser) =
        new VulkanVRApplicationLayered(DebugLevel.ofBool debug, adjustSize, samples, extensions, chooser)

    new([<Optional; DefaultParameterValue(false)>] debug: bool,
        [<Optional; DefaultParameterValue(1)>] samples: int,
        [<Optional; DefaultParameterValue(null : string seq)>] extensions: string seq,
        [<Optional; DefaultParameterValue(null : IDeviceChooser)>] chooser: IDeviceChooser) =
        new VulkanVRApplicationLayered(debug, id, samples, extensions, chooser)

    member x.Version = version
    member x.Texture = tex


    member x.FramebufferSignature = renderPass :> IFramebufferSignature
    member x.Runtime = app.Runtime
    member x.Sizes = AVal.constant x.DesiredSize
    member x.Samples = samples
    member x.Time = time
    

    member x.RenderTask
        with set (t : RuntimeCommand) = 
            userCmd <- t
            if loaded then
                let list = AList.ofList [ hiddenTask; t ]
                task <- app.Runtime.CompileRender(renderPass, RuntimeCommand.Ordered list)
                
    override x.OnLoad(i : VrRenderInfo) : VrTexture * VrTexture =
        info <- i

        if loaded then
            fbo.Dispose()
            cImg.Dispose()
            dImg.Dispose()
            fImg.Dispose()
        else
            compileHidden x.HiddenAreaMesh


        let nImg = 
            device.CreateImage(
                V3i(info.framebufferSize, 1),
                1, 2, samples, 
                TextureDimension.Texture2D,
                TextureFormat.Rgba8,
                VkImageUsageFlags.ColorAttachmentBit ||| VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.SampledBit
            )
        nImg.Name <- "Color Attachment (Window)"

        let nfImg =
            device.CreateImage(
                V3i(info.framebufferSize * V2i(2,1), 1),
                1, 1, 1, 
                TextureDimension.Texture2D,
                TextureFormat.Rgba8,
                VkImageUsageFlags.ColorAttachmentBit ||| VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.SampledBit
            )
        nfImg.Name <- "Resolved Color Attachment (Window)"

        let nDepth =
            device.CreateImage(
                V3i(info.framebufferSize, 1),
                1, 2, samples, 
                TextureDimension.Texture2D,
                TextureFormat.Depth24Stencil8,
                VkImageUsageFlags.DepthStencilAttachmentBit ||| VkImageUsageFlags.TransferDstBit
            )
        nDepth.Name <- "Depth / Stencil Attachment (Window)"
            
        let cView = device.CreateOutputImageView(nImg, 0, 1, 0, 2)
        let dView = device.CreateOutputImageView(nDepth, 0, 1, 0, 2)

        let nFbo =
            device.CreateFramebuffer(
                renderPass, 
                Map.ofList [
                    DefaultSemantic.Colors, cView
                    DefaultSemantic.DepthStencil, dView
                ]
            )

        dImg <- nDepth
        cImg <- nImg
        fImg <- nfImg
        fbo <- nFbo
        

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

        
        
        let list = AList.ofList [ hiddenTask; userCmd ]
        task <- app.Runtime.CompileRender(renderPass, RuntimeCommand.Ordered list)
        task.Name <- "Window Task"
        loaded <- true

        VrTexture.Vulkan(fTex, Box2d(V2d(0.0, 1.0), V2d(0.5, 0.0))), VrTexture.Vulkan(fTex, Box2d(V2d(0.5, 1.0), V2d(1.0, 0.0)))

    override x.ResetRenderStats() =
        swClear.Reset()
        swRender.Reset()
        swResolve.Reset()
        swTotal.Reset()
        
    override x.GetRenderStats() =
        {
            Clear = swClear.MicroTime
            Render = swRender.MicroTime
            Resolve = swResolve.MicroTime
            Total = swTotal.MicroTime
        }

    override x.Render() = 
        swTotal.Start()
        let output = OutputDescription.ofFramebuffer fbo

        swClear.Start()
        device.perform {
            do! Command.TransformLayout(dImg, VkImageLayout.TransferDstOptimal)
            do! Command.TransformLayout(cImg, VkImageLayout.TransferDstOptimal)

            do! Command.ClearColor(cImg.[TextureAspect.Color, 0, *], x.BackgroundColor)
            do! Command.ClearDepthStencil(dImg.[TextureAspect.DepthStencil, 0, *], 1.0, 0u)
            
            do! Command.TransformLayout(dImg, VkImageLayout.DepthStencilAttachmentOptimal)
            do! Command.TransformLayout(cImg, VkImageLayout.ColorAttachmentOptimal)

        }
        swClear.Stop()

        swRender.Start()
        caller.EvaluateAlways AdaptiveToken.Top (fun t ->
            task.Run(t, RenderToken.Empty, output)
        )
        swRender.Stop()


        swResolve.Start()
        let a = cImg.[TextureAspect.Color, *, 0]

        if device.IsDeviceGroup then

            device.perform {
                for di in 0 .. int device.PhysicalDevices.Length - 1 do
                    do! Command.SetDeviceMask (1u <<< di)
                    do! Command.TransformLayout(fImg, VkImageLayout.TransferDstOptimal)
                    do! Command.TransformLayout(cImg, VkImageLayout.TransferSrcOptimal)

                    if samples > 1 then
                        if di = 0 then
                            do! Command.ResolveMultisamples(cImg.[TextureAspect.Color, 0, 0], V3i.Zero, fImg.[TextureAspect.Color, 0, 0], V3i.Zero, V3i(info.framebufferSize, 1))
                        else
                            do! Command.ResolveMultisamples(cImg.[TextureAspect.Color, 0, 1], V3i.Zero, fImg.[TextureAspect.Color, 0, 0], V3i(info.framebufferSize.X, 0, 0), V3i(info.framebufferSize, 1))
                    else
                        if di = 0 then
                            do! Command.Copy(cImg.[TextureAspect.Color, 0, 0], V3i.Zero, fImg.[TextureAspect.Color, 0, 0], V3i.Zero, V3i(info.framebufferSize, 1))
                        else
                            do! Command.Copy(cImg.[TextureAspect.Color, 0, 1], V3i.Zero, fImg.[TextureAspect.Color, 0, 0], V3i(info.framebufferSize.X, 0, 0), V3i(info.framebufferSize, 1))
             
                    do! Command.TransformLayout(fImg, VkImageLayout.TransferSrcOptimal)

                do! Command.SetDeviceMask 3u
                do! Command.SyncPeersDefault(fImg, VkImageLayout.TransferSrcOptimal)
            }
        else
            device.perform {
                do! Command.TransformLayout(cImg, VkImageLayout.TransferSrcOptimal)
                do! Command.TransformLayout(fImg, VkImageLayout.TransferDstOptimal)

                if samples > 1 then
                    do! Command.ResolveMultisamples(cImg.[TextureAspect.Color, 0, 0], V3i.Zero, fImg.[TextureAspect.Color, 0, 0], V3i.Zero, V3i(info.framebufferSize, 1))
                    do! Command.ResolveMultisamples(cImg.[TextureAspect.Color, 0, 1], V3i.Zero, fImg.[TextureAspect.Color, 0, 0], V3i(info.framebufferSize.X, 0, 0), V3i(info.framebufferSize, 1))
                else
                    do! Command.Copy(cImg.[TextureAspect.Color, 0, 0], V3i.Zero, fImg.[TextureAspect.Color, 0, 0], V3i.Zero, V3i(info.framebufferSize, 1))
                    do! Command.Copy(cImg.[TextureAspect.Color, 0, 1], V3i.Zero, fImg.[TextureAspect.Color, 0, 0], V3i(info.framebufferSize.X, 0, 0), V3i(info.framebufferSize, 1))
             
                do! Command.TransformLayout(fImg, VkImageLayout.TransferSrcOptimal)
            }
        swResolve.Stop()


        transact (fun () -> time.MarkOutdated(); version.Value <- version.Value + 1)
        swTotal.Stop()

    override x.AfterSubmit() =
        device.perform {
            do! Command.TransformLayout(fImg.[TextureAspect.Color,*,*], VkImageLayout.General, VkImageLayout.TransferSrcOptimal)
        }

    override x.Use (action : unit -> 'r) =
        use t = queue.Family.CurrentToken
        t.Sync (ignore >> action)

    override x.Release() = 
        // delete views
        fbo.Attachments.[DefaultSemantic.Colors].Dispose()
        fbo.Attachments.[DefaultSemantic.DepthStencil].Dispose()

        // delete FBOs
        fbo.Dispose()

        // delete images
        cImg.Dispose()
        dImg.Dispose()
        fImg.Dispose()

        // dispose the app
        queueHandle.Dispose()
        app.Dispose()
        
    member x.BeforeRender = beforeRender
    member x.AfterRender = afterRender

    member x.SubSampling
        with get() = 1.0
        and set (v : float) = 
            let adjust (s : V2i) : V2i = max V2i.II (V2i (V2d s * v))
            base.AdjustSize <- adjust


    interface IRenderTarget with
        member x.Runtime = app.Runtime :> IRuntime
        member x.Sizes = AVal.constant x.DesiredSize
        member x.Samples = samples
        member x.FramebufferSignature = x.FramebufferSignature
        member x.RenderTask
            with get() = RenderTask.empty
            and set t = () //x.RenderTask <- t
        member x.SubSampling
            with get() = 1.0
            and set v = if v <> 1.0 then failwith "[OpenVR] SubSampling not implemented (use adjustSize instead)"

        member x.Time = time
        member x.BeforeRender = beforeRender.Publish
        member x.AfterRender = afterRender.Publish

    interface IRenderControl with
        member x.Cursor
            with get() = Cursor.Default
            and set c = ()
        member x.Keyboard = keyboard :> IKeyboard
        member x.Mouse = mouse :> IMouse

    interface IRenderWindow with
        member x.Run() = x.Run()