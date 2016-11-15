namespace Aardvark.Application.WinForms

#nowarn "9"
#nowarn "51"

open System
open Aardvark.Base
open Aardvark.Rendering.Vulkan
open System.Runtime.InteropServices
open System.Windows.Forms

[<AllowNullLiteral>]
type VulkanSwapChainDescription(device : Device, colorFormat : RenderbufferFormat, depthFormat : RenderbufferFormat, samples : int,
                                presentMode : VkPresentModeKHR, preTransform : VkSurfaceTransformFlagBitsKHR, colorSpace : VkColorSpaceKHR,
                                bufferCount : int, surface : VkSurfaceKHR) =
    
    let pass =
        device.CreateRenderPass(
            Map.ofList [ 
                DefaultSemantic.Colors, { format = colorFormat; samples = samples }
                DefaultSemantic.Depth, { format = depthFormat; samples = samples }  
            ]
        )
        
    member x.Device = device
    member x.PhysicalDevice = device.PhysicalDevice
    member x.Instance = device.Instance

    member x.Samples = samples
    member x.RenderPass = pass
    member x.ColorFormat = colorFormat
    member x.DepthFormat = depthFormat
    member x.PresentMode = presentMode
    member x.PreTransform = preTransform
    member x.ColorSpace = colorSpace
    member x.BufferCount = bufferCount
    member x.Surface = surface

    member x.Dispose() =
        device.Delete pass
        VkRaw.vkDestroySurfaceKHR(device.Instance.Handle, surface, NativePtr.zero)

    interface IDisposable with
        member x.Dispose() = x.Dispose()

[<AllowNullLiteral>]
type IVulkanSwapChain =
    inherit IDisposable
    
    abstract member BeginFrame : DeviceQueue -> unit
    abstract member EndFrame : DeviceQueue -> unit
    abstract member Framebuffer : Framebuffer

[<AutoOpen>]
module private EnumExtensions =

    let inline check str err =
        ()

    let private enumValue (extensionNumber : int) (id : int) =
        0xc0000000 - extensionNumber * -1024 + id

    let private VK_EXT_KHR_SWAPCHAIN_EXTENSION_NUMBER = 1
    let private VK_EXT_KHR_DEVICE_SWAPCHAIN_EXTENSION_NUMBER = 2


    type VkStructureType with
        //static member SurfaceDescriptionWindowKHR  = enumValue VK_EXT_KHR_SWAPCHAIN_EXTENSION_NUMBER 0 |> unbox<VkStructureType>
        static member SwapChainCreateInfoKHR = 1000001000 |> unbox<VkStructureType>
        static member PresentInfoKHR = 1000001001 |> unbox<VkStructureType>

    type VkImageLayout with
        static member PresentSrcKhr = unbox<VkImageLayout> 1000001002

type VulkanSwapChain(desc : VulkanSwapChainDescription,
                     colorImages : Image[], colorViews : VkImageView[], 
                     depthMemory : VkDeviceMemory, depthImage : VkImage, depth : VkImageView,
                     swapChain : VkSwapchainKHR, size : V2i) =
    
    let mutable disposed = false
    let device = desc.Device
    let instance = desc.Instance
    let renderPass = desc.RenderPass
    let graphicsFamily = device.GraphicsFamily

    let imageViews = 
        colorViews |> Array.mapi (fun i v -> 
            ImageView(device, v, colorImages.[i], VkImageViewType.D2d, Range1i(0,0), Range1i(0,0))
        )
    let depthImage = 
        Image(device, depthImage, V3i(size.X, size.Y, 1), 1, 1, 1, TextureDimension.Texture2D, VkFormat.ofTextureFormat (TextureFormat.ofRenderbufferFormat desc.DepthFormat), VkComponentMapping(), Unchecked.defaultof<_>, VkImageLayout.DepthStencilAttachmentOptimal)
    
    let depthView = 
        ImageView(device, depth, depthImage, VkImageViewType.D2d, Range1i(0,0), Range1i(0,0))

    let framebuffers = 
        if desc.Samples > 1 then [||]
        else
            imageViews |> Array.map (fun color ->
                device.CreateFramebuffer(renderPass, Map.ofList [DefaultSemantic.Colors, color; DefaultSemantic.Depth, depthView])
            )

    let mutable swapChain = swapChain
    let mutable currentBuffer = 0u

    let mutable presentSemaphore = Unchecked.defaultof<_>

    let check str err =
        ()

    member x.DepthImage = depthImage
    member x.Size = size
    member x.Description = desc
    member x.Device = device
    member x.PhysicalDevice = desc.PhysicalDevice
    member x.Instance = instance


    member x.BeginFrame(q : DeviceQueue) = 
        
        presentSemaphore <- device.CreateSemaphore()
        VkRaw.vkAcquireNextImageKHR(device.Handle, swapChain, ~~~0UL, presentSemaphore.Handle, VkFence.Null, &&currentBuffer) |> check "vkAcquireNextImageKHR"
        
        let image = colorImages.[int currentBuffer]

        use cmd = graphicsFamily.DefaultCommandPool.CreateCommandBuffer(CommandBufferLevel.Primary)
        cmd.Enqueue(Command.TransformLayout(image, VkImageLayout.ColorAttachmentOptimal))
        q.RunSynchronously(cmd)
        q.Wait(presentSemaphore)
        
    member x.Color =
        colorImages.[int currentBuffer]

    member x.Framebuffer =
        framebuffers.[int currentBuffer]

    member x.EndFrame(q : DeviceQueue) =

        let mutable result = VkResult.VkSuccess
        let mutable info =
            VkPresentInfoKHR(
                VkStructureType.PresentInfoKHR,
                0n, 
                0u, NativePtr.zero,
                1u, &&swapChain,
                &&currentBuffer,
                &&result
            )

        let img = colorImages.[int currentBuffer]


        let command =
            { new Command<unit>() with
                member x.Enqueue cmd =
                    let mutable prePresentBarrier =
                        VkImageMemoryBarrier(
                            VkStructureType.ImageMemoryBarrier, 0n, 
                            VkAccessFlags.ColorAttachmentWriteBit,
                            VkAccessFlags.MemoryReadBit,
                            VkImageLayout.ColorAttachmentOptimal,
                            VkImageLayout.PresentSrcKhr,
                            uint32 q.Family.index,
                            uint32 q.Family.index,
                            img.Handle,
                            VkImageSubresourceRange(VkImageAspectFlags.ColorBit, 0u, 1u, 0u, 1u)
                        )

                    VkRaw.vkCmdPipelineBarrier(
                        cmd.Handle,
                        VkPipelineStageFlags.AllCommandsBit, 
                        VkPipelineStageFlags.BottomOfPipeBit, 
                        VkDependencyFlags.None, 
                        0u, NativePtr.zero, 
                        0u, NativePtr.zero, 
                        1u, &&prePresentBarrier
                    )
                member x.Dispose() = ()
            }

        use cmd = device.GraphicsFamily.DefaultCommandPool.CreateCommandBuffer(CommandBufferLevel.Primary)
        cmd.Enqueue command
        device.GraphicsFamily.RunSynchronously cmd
        img.Layout <- VkImageLayout.PresentSrcKhr

        VkRaw.vkQueuePresentKHR(q.Handle, &&info) |> check "vkQueuePresentKHR"

        q.WaitIdle()

        presentSemaphore.Dispose()

    member x.Dispose() =
        if not disposed then
            disposed <- true
            for f in framebuffers do
                device.Delete(f)

            VkRaw.vkDestroyImageView(device.Handle, depth, NativePtr.zero)
            VkRaw.vkDestroyImage(device.Handle, depthImage.Handle, NativePtr.zero)
            VkRaw.vkFreeMemory(device.Handle, depthMemory, NativePtr.zero)
            for c in colorViews do
                VkRaw.vkDestroyImageView(device.Handle, c, NativePtr.zero)

            VkRaw.vkDestroySwapchainKHR(device.Handle, swapChain, NativePtr.zero)

            ()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface IVulkanSwapChain with
        member x.BeginFrame q = x.BeginFrame q
        member x.EndFrame q = x.EndFrame q
        member x.Framebuffer = x.Framebuffer



[<AutoOpen>]
module InstanceSwapExtensions =
    open System.Windows.Forms
    open System.Runtime.CompilerServices
    open System.Collections.Concurrent
    open Microsoft.FSharp.NativeInterop
    open System.Runtime.InteropServices
    open System.Reflection

    [<DllImport("X11-xcb")>]
    extern nativeint private XGetXCBConnection(nativeint xdisplay)

    let private createSurface (instance : Instance) (ctrl : Control) : VkSurfaceKHR =
        Log.start "VkSurface"
        try
            match Environment.OSVersion with
                | Windows -> 
                    Log.line "running on Win32"

                    let hwnd = ctrl.Handle
                    let hinstance = Marshal.GetHINSTANCE(Assembly.GetEntryAssembly().GetModules().[0])

                    Log.line "HWND: 0x%016X" hwnd
                    Log.line "HINSTANCE: 0x%016X" hinstance
                

                    let mutable info =
                        VkWin32SurfaceCreateInfoKHR(
                            VkStructureType.Win32SurfaceCreateInfo,
                            0n,
                            0u,
                            hinstance,
                            hwnd
                        )

                    let mutable surface = VkSurfaceKHR.Null
                    VkRaw.vkCreateWin32SurfaceKHR(
                        instance.Handle,
                        &&info,
                        NativePtr.zero, 
                        &&surface
                    ) |> check "vkCreateWin32SurfaceKHR"

                    surface

                | Linux ->
                
                    Log.line "running on Linux"

                    let xp = Type.GetType("System.Windows.Forms.XplatUIX11, System.Windows.Forms")
                    if isNull xp then failwithf "could not get System.Windows.Forms.XplatUIX11"

                    let (?) (t : Type) (name : string) =
                        let prop = t.GetProperty(name, BindingFlags.Static ||| BindingFlags.NonPublic ||| BindingFlags.Public)
                        if isNull prop then 
                            let field = t.GetField(name, BindingFlags.Static ||| BindingFlags.NonPublic ||| BindingFlags.Public)
                            if isNull field then failwithf "cannot get XplatUIX11.%s" name
                            else field.GetValue(null) |> unbox<'a>
                        else
                            prop.GetValue(null) |> unbox<'a>

                    let display = xp?DisplayHandle
                    let connection = XGetXCBConnection(display)
                    let window = ctrl.Handle

                    Log.line "xcb_connection: 0x%016X" connection
                    Log.line "xcb_window:     0x%016X" window

                    let dpy = NativePtr.alloc 1
                    NativePtr.write dpy connection




                    let mutable info =
                        VkXcbSurfaceCreateInfoKHR(
                            VkStructureType.XcbSurfaceCreateInfo, 0n,
                            VkXcbSurfaceCreateFlagsKHR.MinValue,
                            dpy, window
                        )


                    let mutable surface = VkSurfaceKHR.Null
                    VkRaw.vkCreateXcbSurfaceKHR(
                        instance.Handle,
                        &&info,
                        NativePtr.zero, 
                        &&surface
                    ) |> check "vkCreateXcbSurfaceKHR"
                
                    surface

                | _ ->
                    failwith "not implemented"
        finally
            Log.stop()
    
    let createRawSwapChain (ctrl : Control) (desc : VulkanSwapChainDescription) (usageFlags : VkImageUsageFlags) (layout : VkImageLayout) =
        let size = V2i(ctrl.ClientSize.Width, ctrl.ClientSize.Height)
        let swapChainExtent = VkExtent2D(uint32 ctrl.ClientSize.Width, uint32 ctrl.ClientSize.Height)
        let device = desc.Device

        let mutable surface = desc.Surface //createSurface desc.Instance ctrl

        let mutable swapChainCreateInfo =
            VkSwapchainCreateInfoKHR(
                VkStructureType.SwapChainCreateInfoKHR,
                0n, VkSwapchainCreateFlagsKHR.MinValue,
                desc.Surface,
                uint32 desc.BufferCount,
                VkFormat.ofTextureFormat (TextureFormat.ofRenderbufferFormat desc.ColorFormat),
                desc.ColorSpace,
                swapChainExtent,
                1u,
                usageFlags,
                VkSharingMode.Exclusive,
                0u,
                NativePtr.zero,
                desc.PreTransform,
                VkCompositeAlphaFlagBitsKHR.VkCompositeAlphaOpaqueBitKhr,
                desc.PresentMode,
                1u,
                VkSwapchainKHR.Null
            )

        let mutable swapChain = VkSwapchainKHR.Null
        VkRaw.vkCreateSwapchainKHR(device.Handle, &&swapChainCreateInfo, NativePtr.zero, &&swapChain) |> check "vkCreateSwapChainKHR"
        
            
        let mutable imageCount = 0u
        VkRaw.vkGetSwapchainImagesKHR(device.Handle, swapChain, &&imageCount, NativePtr.zero) |> check "vkGetSwapChainImagesKHR"
        let mutable swapChainImages = NativePtr.stackalloc (int imageCount)
        VkRaw.vkGetSwapchainImagesKHR(device.Handle, swapChain, &&imageCount, swapChainImages) |> check "vkGetSwapChainImagesKHR"


        let rgba =
            VkComponentMapping(VkComponentSwizzle.R, VkComponentSwizzle.G,VkComponentSwizzle.B, VkComponentSwizzle.A)

        use cmd = device.GraphicsFamily.DefaultCommandPool.CreateCommandBuffer(CommandBufferLevel.Primary)
        let colorImages : Image[] =
            Array.init (int imageCount) (fun i ->
                let image = NativePtr.get swapChainImages i
                let fmt = VkFormat.ofTextureFormat (TextureFormat.ofRenderbufferFormat desc.ColorFormat)
                let img = Image(device, image, V3i(size.X, size.Y, 1), 1, 1, 1, TextureDimension.Texture2D, fmt, rgba, Unchecked.defaultof<_>, VkImageLayout.Undefined)

                cmd.Enqueue(Command.TransformLayout(img, layout))

                img
            )

        device.GraphicsFamily.RunSynchronously cmd


        swapChain, colorImages

    type Control with

        member x.CreateVulkanSwapChainDescription(device : Device, depthFormat : VkFormat, samples : int) =
            let instance = device.Instance
            let physical = device.PhysicalDevice.Handle

            Log.start "SwapChain"

            let surface = createSurface device.Instance x


            let queueIndex = device.GraphicsFamily.Info.index
            let mutable supported = 0u
            VkRaw.vkGetPhysicalDeviceSurfaceSupportKHR(physical, uint32 queueIndex, surface, &&supported) |> check "vkGetPhysicalDeviceSurfaceSupportKHR"
            let supported = supported = 1u

            if supported then Log.line "queue family %d supports SwapChains" queueIndex
            else Log.warn "queue family %d supports SwapChains" queueIndex

            //printfn "[KHR] using queue %d: { wsi = %A }" queueIndex supported

            let mutable formatCount = 0u
            VkRaw.vkGetPhysicalDeviceSurfaceFormatsKHR(physical, surface, &&formatCount, NativePtr.zero) |> check "vkGetSurfaceFormatsKHR"

            //printfn "[KHR] format count: %A" formatCount

            let formats = NativePtr.stackalloc (int formatCount)
            VkRaw.vkGetPhysicalDeviceSurfaceFormatsKHR(physical, surface, &&formatCount, formats) |> check "vkGetSurfaceFormatsKHR"

            if formatCount < 1u then
                Log.warn "could not find a valid SwapChain format"
                failwith "could not find a valid SwapChain format"

            let surfFormat = NativePtr.get formats 0
            let format =
                if formatCount = 1u && surfFormat.format = VkFormat.Undefined then
                    Log.warn "using fallback format %A" VkFormat.B8g8r8a8Unorm
                    //printfn "[KHR] format fallback"
                    VkFormat.B8g8r8a8Unorm
                else
                    Log.line "using format: %A" surfFormat.format
                    //printfn "[KHR] formats: %A" (Array.init (int formatCount) (fun i -> (NativePtr.get formats i)))
                    surfFormat.format

            //printfn "[KHR] using format: %A" surfFormat

            let mutable surfaceProperties = VkSurfaceCapabilitiesKHR()
            VkRaw.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(physical, surface, &&surfaceProperties) |> check "vkGetPhysicalDeviceSurfaceCapabilitiesKHR"
            //printfn "[KHR] current extent: [%d,%d]" surfaceProperties.currentExtent.width surfaceProperties.currentExtent.height 


            let mutable presentModeCount = 0u
            VkRaw.vkGetPhysicalDeviceSurfacePresentModesKHR(physical, surface, &&presentModeCount, NativePtr.zero) |> check "vkGetSurfacePresentModesKHR"
            let presentModes = NativePtr.stackalloc (int presentModeCount)
            VkRaw.vkGetPhysicalDeviceSurfacePresentModesKHR(physical, surface, &&presentModeCount, presentModes) |> check "vkGetSurfacePresentModesKHR"


            //printfn "[KHR] extent: [%d,%d]" swapChainExtent.width swapChainExtent.height

            let presentModes = List.init (int presentModeCount) (fun i -> NativePtr.get presentModes i) 

            Log.line "available presentModes: %A" presentModes

            let swapChainPresentMode =
                presentModes
                    |> List.minBy(function | VkPresentModeKHR.VkPresentModeMailboxKhr -> 0 | VkPresentModeKHR.VkPresentModeFifoKhr -> 1 | VkPresentModeKHR.VkPresentModeImmediateKhr -> 2 | _ -> 3)
        
            Log.line "using presentMode: %A" swapChainPresentMode
            //let swapChainPresentMode = VkPresentModeKHR.Fifo

            //printfn "[KHR] present mode: %A" swapChainPresentMode

            let desiredNumberOfSwapChainImages =
                let desired = surfaceProperties.minImageCount + 1u
                if surfaceProperties.maxImageCount > 0u then
                    min surfaceProperties.maxImageCount desired
                else
                    desired

            Log.line "buffers: %A" desiredNumberOfSwapChainImages

            let supported = unbox<VkSurfaceTransformFlagBitsKHR> (int surfaceProperties.supportedTransforms)
            let preTransform =
                if supported &&& VkSurfaceTransformFlagBitsKHR.VkSurfaceTransformIdentityBitKhr <> VkSurfaceTransformFlagBitsKHR.None then
                    VkSurfaceTransformFlagBitsKHR.VkSurfaceTransformIdentityBitKhr
                else
                    supported

            Log.line "using preTransform: %A" preTransform
            Log.stop()

            let colorFormat = TextureFormat.toRenderbufferFormat (VkFormat.toTextureFormat format)
            let depthFormat = TextureFormat.toRenderbufferFormat (VkFormat.toTextureFormat depthFormat)
            new VulkanSwapChainDescription(device, colorFormat, depthFormat, samples, swapChainPresentMode, preTransform, surfFormat.colorSpace, int desiredNumberOfSwapChainImages, surface)

        member x.CreateVulkanSwapChain(desc : VulkanSwapChainDescription) =
            let size = V2i(x.ClientSize.Width, x.ClientSize.Height)
            Log.line "creating SwapChain for size: %A" size

            let swapChainExtent = VkExtent2D(uint32 x.ClientSize.Width, uint32 x.ClientSize.Height)
            let device = desc.Device

            let ms = desc.Samples > 1

            let swapChain, images = 
                if ms then createRawSwapChain x desc VkImageUsageFlags.TransferDstBit VkImageLayout.TransferDstOptimal
                else createRawSwapChain x desc VkImageUsageFlags.ColorAttachmentBit VkImageLayout.ColorAttachmentOptimal


            let rgba =
                VkComponentMapping(VkComponentSwizzle.R, VkComponentSwizzle.G,VkComponentSwizzle.B, VkComponentSwizzle.A)

            let colorViews : VkImageView[] =
                images |> Array.map (fun img ->
                    let image = img.Handle

                    let mutable colorAttachmentView =
                        VkImageViewCreateInfo(
                            VkStructureType.ImageViewCreateInfo,
                            0n, VkImageViewCreateFlags.MinValue,
                            image,
                            VkImageViewType.D2d,
                            VkFormat.ofTextureFormat (TextureFormat.ofRenderbufferFormat desc.ColorFormat),
                            rgba,
                            VkImageSubresourceRange(VkImageAspectFlags.ColorBit, 0u, 1u, 0u, 1u)
                        )


                    let mutable view = VkImageView.Null
                    VkRaw.vkCreateImageView(device.Handle, &&colorAttachmentView, NativePtr.zero, &&view) |> check "vkCreateImageView"

                    view
                )


            let mutable depthImage = VkImage.Null
            let mutable depthMemory = VkDeviceMemory.Null
            let depthView : VkImageView =
            
                let mutable image =
                    VkImageCreateInfo(
                        VkStructureType.ImageCreateInfo,
                        0n, VkImageCreateFlags.None,
                        VkImageType.D2d,
                        VkFormat.ofTextureFormat (TextureFormat.ofRenderbufferFormat desc.DepthFormat),
                        VkExtent3D(swapChainExtent.width, swapChainExtent.height, 1u),
                        1u,
                        1u,
                        unbox<VkSampleCountFlags> desc.Samples,
                        VkImageTiling.Optimal,
                        VkImageUsageFlags.DepthStencilAttachmentBit,
                        VkSharingMode.Exclusive,
                        0u,
                        NativePtr.zero,
                        VkImageLayout.Undefined
                    )

                let mutable mem_alloc =
                    VkMemoryAllocateInfo(
                        VkStructureType.MemoryAllocateInfo,
                        0n,
                        0UL,
                        0u
                    )

                let mutable view =
                    VkImageViewCreateInfo(
                        VkStructureType.ImageViewCreateInfo,
                        0n, VkImageViewCreateFlags.MinValue,
                        VkImage.Null,
                        VkImageViewType.D2d,
                        VkFormat.ofTextureFormat (TextureFormat.ofRenderbufferFormat desc.DepthFormat),
                        VkComponentMapping(VkComponentSwizzle.R, VkComponentSwizzle.R, VkComponentSwizzle.R, VkComponentSwizzle.One),
                        VkImageSubresourceRange(VkImageAspectFlags.DepthBit, 0u, 1u, 0u, 1u)
                    )

                
                VkRaw.vkCreateImage(device.Handle, &&image, NativePtr.zero, &&depthImage) |> check "vkCreateImage"

                let mutable memreqs = VkMemoryRequirements()
                VkRaw.vkGetImageMemoryRequirements(device.Handle, depthImage, &&memreqs) 
                mem_alloc.allocationSize <- memreqs.size

                let mutable memProps = VkPhysicalDeviceMemoryProperties()
                VkRaw.vkGetPhysicalDeviceMemoryProperties(desc.PhysicalDevice.Handle, &&memProps)


                let props = VkMemoryPropertyFlags.DeviceLocalBit
                let mutable typeBits = memreqs.memoryTypeBits
                for mi in 0u..memProps.memoryTypeCount-1u do
                    if typeBits &&& 1u = 1u && memProps.memoryTypes.[int mi].propertyFlags &&& props = props then
                        mem_alloc.memoryTypeIndex <- mi
                    typeBits <- typeBits >>> 1



                let mutable depthMemory = VkDeviceMemory.Null
                VkRaw.vkAllocateMemory(device.Handle, &&mem_alloc, NativePtr.zero, &&depthMemory) |> check "vkAllocMemory"

                VkRaw.vkBindImageMemory(device.Handle, depthImage, depthMemory, 0UL) |> check "vkBindImageMemory"
                view.image <- depthImage

                let mutable depthView = VkImageView.Null
                VkRaw.vkCreateImageView(device.Handle, &&view, NativePtr.zero, &&depthView) |> check "vkCreateImageView"

                let img =
                    Image(device, depthImage, V3i(size.X, size.Y, 1), 1, 1, 1, TextureDimension.Texture2D, VkFormat.ofTextureFormat (TextureFormat.ofRenderbufferFormat desc.DepthFormat), rgba, Unchecked.defaultof<_>, VkImageLayout.Undefined)

                use token = device.ResourceToken
                token.Enqueue (Command.TransformLayout(img, VkImageLayout.DepthStencilAttachmentOptimal))

                depthView

            let chain = new VulkanSwapChain(desc, images, colorViews, depthMemory, depthImage, depthView, swapChain, size) 
            
            if ms then failwithf "[Vulkan] no multisampling atm."
            else chain :> IVulkanSwapChain



