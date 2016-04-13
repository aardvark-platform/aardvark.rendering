namespace Aardvark.Application.WinForms

#nowarn "9"
#nowarn "51"

open System
open Aardvark.Base
open Aardvark.Rendering.Vulkan
open System.Runtime.InteropServices
open System.Windows.Forms

type VulkanSwapChainDescription(context : Context, colorFormat : VkFormat, depthFormat : VkFormat, samples : int,
                                presentMode : VkPresentModeKHR, preTransform : VkSurfaceTransformFlagBitsKHR, colorSpace : VkColorSpaceKHR,
                                bufferCount : int, surface : VkSurfaceKHR) =
    
    let device = context.Device
    let pass =
        device.CreateRenderPass(
            [| DefaultSemantic.Colors, { format = colorFormat; samples = samples; clearMask = ClearMask.None } |],
            { format = depthFormat; samples = samples; clearMask = ClearMask.None }  
        )
        
    member x.Context = context
    member x.Device = device
    member x.PhysicalDevice = device.Physical
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

type IVulkanSwapChain =
    inherit IDisposable
    
    abstract member BeginFrame : Queue -> unit
    abstract member EndFrame : Queue -> unit
    abstract member Framebuffer : Framebuffer

[<AutoOpen>]
module private EnumExtensions =


    let private enumValue (extensionNumber : int) (id : int) =
        0xc0000000 - extensionNumber * -1024 + id

    let private VK_EXT_KHR_SWAPCHAIN_EXTENSION_NUMBER = 1
    let private VK_EXT_KHR_DEVICE_SWAPCHAIN_EXTENSION_NUMBER = 2


    type VkStructureType with
        static member SurfaceDescriptionWindowKHR  = enumValue VK_EXT_KHR_SWAPCHAIN_EXTENSION_NUMBER 0 |> unbox<VkStructureType>
        static member SwapChainCreateInfoKHR = enumValue VK_EXT_KHR_DEVICE_SWAPCHAIN_EXTENSION_NUMBER 0 |> unbox<VkStructureType>
        static member PresentInfoKHR = enumValue VK_EXT_KHR_DEVICE_SWAPCHAIN_EXTENSION_NUMBER 1 |> unbox<VkStructureType>

    type VkImageLayout with
        static member PresentSrcKhr = unbox<VkImageLayout> 1000001002

type VulkanSwapChain(desc : VulkanSwapChainDescription,
                     colorImages : Image[], colorViews : VkImageView[], 
                     depthMemory : VkDeviceMemory, depthImage : VkImage, depth : VkImageView,
                     swapChain : VkSwapchainKHR, size : V2i) =
    
    let mutable disposed = false
    let context = desc.Context
    let device = desc.Device
    let instance = desc.Instance
    let renderPass = desc.RenderPass

    let imageViews = colorViews |> Array.mapi (fun i v -> ImageView(v, colorImages.[i], VkImageViewType.D2d, desc.ColorFormat, VkComponentMapping.rgba, Range1i(0,0), Range1i(0,0)))
    let depthImage = Image(context, Unchecked.defaultof<_>, depthImage, VkImageType.D2d, desc.DepthFormat, TextureDimension.Texture2D, V3i(size.X, size.Y, 1), 1, 1, 1, VkImageUsageFlags.DepthStencilAttachmentBit, VkImageLayout.DepthStencilAttachmentOptimal)
    let depthView = ImageView(depth, depthImage, VkImageViewType.D2d, desc.DepthFormat, VkComponentMapping.rgba, Range1i(0,0), Range1i(0,0))

    let framebuffers = 
        if desc.Samples > 1 then [||]
        else
            imageViews |> Array.map (fun color ->
                context.CreateFramebuffer(renderPass, [color; depthView], size)
            )

    let mutable swapChain = swapChain
    let mutable currentBuffer = 0u

    let mutable presentSemaphore = Unchecked.defaultof<_>

    member x.DepthImage = depthImage
    member x.Size = size
    member x.Description = desc
    member x.Context = context
    member x.Device = device
    member x.PhysicalDevice = desc.PhysicalDevice
    member x.Instance = instance


    member x.BeginFrame(q : Queue) = 
        presentSemaphore <- device.CreateSemaphore()
        VkRaw.vkAcquireNextImageKHR(device.Handle, swapChain, ~~~0UL, presentSemaphore.Handle, VkFence.Null, &&currentBuffer) |> check "vkAcquireNextImageKHR"
        
        colorImages.[int currentBuffer].ToLayout(VkImageLayout.ColorAttachmentOptimal)
            |> q.RunSynchronously

        q.Wait(presentSemaphore)
        
    member x.Color =
        colorImages.[int currentBuffer]

    member x.Framebuffer =
        framebuffers.[int currentBuffer]

    member x.EndFrame(q : Queue) =

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

        q.RunSynchronously (
            Command.custom (fun s ->
                let mutable prePresentBarrier =
                    VkImageMemoryBarrier(
                        VkStructureType.ImageMemoryBarrier, 0n, 
                        VkAccessFlags.ColorAttachmentWriteBit,
                        VkAccessFlags.MemoryReadBit,
                        VkImageLayout.ColorAttachmentOptimal,
                        VkImageLayout.PresentSrcKhr,
                        uint32 q.Family.Index,
                        uint32 q.Family.Index,
                        img.Handle,
                        VkImageSubresourceRange(VkImageAspectFlags.ColorBit, 0u, 1u, 0u, 1u)
                    )

                VkRaw.vkCmdPipelineBarrier(
                    s.buffer.Handle,
                    VkPipelineStageFlags.AllCommandsBit, 
                    VkPipelineStageFlags.BottomOfPipeBit, 
                    VkDependencyFlags.None, 
                    0u, NativePtr.zero, 
                    0u, NativePtr.zero, 
                    1u, &&prePresentBarrier
                )

                { s with isEmpty = false }
            )
        )
        img.Layout <- VkImageLayout.PresentSrcKhr

        VkRaw.vkQueuePresentKHR(q.Handle, &&info) |> check "vkQueuePresentKHR"

        q.WaitIdle()

        presentSemaphore.Dispose()

    member x.Dispose() =
        if not disposed then
            disposed <- true
            for f in framebuffers do
                context.Delete(f)

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

type VulkanSwapChainMS( real : VulkanSwapChain, samples : int ) =
    let context = real.Context
    let device = real.Device
    let desc = real.Description

    let color = 
        context.CreateImage(
            VkImageType.D2d,
            desc.ColorFormat,
            TextureDimension.Texture2D,
            V3i(real.Size.X, real.Size.Y, 1),
            1,
            1,
            samples,
            VkImageUsageFlags.ColorAttachmentBit ||| VkImageUsageFlags.ColorAttachmentBit,
            VkImageLayout.ColorAttachmentOptimal,
            VkImageTiling.Optimal
        )

    let colorView = context.CreateImageOutputView(color)
    let depthView = context.CreateImageOutputView(real.DepthImage)

    let fbo = 
        context.CreateFramebuffer(
            desc.RenderPass, 
            [ colorView; depthView ]
        )

    interface IDisposable with
        member x.Dispose() =
            context.Delete fbo
            context.Delete colorView
            context.Delete depthView
            context.Delete color
            real.Dispose()

    interface IVulkanSwapChain with
        member x.BeginFrame q =
            real.BeginFrame q

        member x.EndFrame q =

            let cmd = ImageSubResource.blit VkFilter.Nearest (ImageSubResource(color)) (ImageSubResource(real.Color))
            cmd.RunSynchronously context.DefaultQueue

            // resolve here
            real.EndFrame q

        member x.Framebuffer =
            fbo

        


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
        let context = desc.Context

        let mutable surface = desc.Surface //createSurface desc.Instance ctrl

        let mutable swapChainCreateInfo =
            VkSwapchainCreateInfoKHR(
                VkStructureType.SwapChainCreateInfoKHR,
                0n, VkSwapchainCreateFlagsKHR.MinValue,
                desc.Surface,
                uint32 desc.BufferCount,
                desc.ColorFormat,
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
                0u,
                VkSwapchainKHR.Null
            )

        let mutable swapChain = VkSwapchainKHR.Null
        VkRaw.vkCreateSwapchainKHR(device.Handle, &&swapChainCreateInfo, NativePtr.zero, &&swapChain) |> check "vkCreateSwapChainKHR"
        
            
        let mutable imageCount = 0u
        VkRaw.vkGetSwapchainImagesKHR(device.Handle, swapChain, &&imageCount, NativePtr.zero) |> check "vkGetSwapChainImagesKHR"
        let mutable swapChainImages = NativePtr.stackalloc (int imageCount)
        VkRaw.vkGetSwapchainImagesKHR(device.Handle, swapChain, &&imageCount, swapChainImages) |> check "vkGetSwapChainImagesKHR"



        let colorImages : Image[] =
            Array.init (int imageCount) (fun i ->
                let image = NativePtr.get swapChainImages i

                let img = Image(context, Unchecked.defaultof<_>, image, VkImageType.D2d, desc.ColorFormat, TextureDimension.Texture2D, V3i(size.X, size.Y, 1), 1, 1, 1, usageFlags, VkImageLayout.Undefined)
                img.ToLayout(layout) |> context.DefaultQueue.RunSynchronously
                img
            )


        swapChain, colorImages

    type Control with

        member x.CreateVulkanSwapChainDescription(context : Context, depthFormat : VkFormat, samples : int) =
            let device = context.Device
            let instance = device.Instance
            let physical = device.Physical.Handle

            Log.start "SwapChain"

            let surface = createSurface device.Instance x


            let queueIndex = context.DefaultQueue.Family.Index
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

            new VulkanSwapChainDescription(context, format, depthFormat, samples, swapChainPresentMode, preTransform, surfFormat.colorSpace, int desiredNumberOfSwapChainImages, surface)

        member x.CreateVulkanSwapChain(desc : VulkanSwapChainDescription) =
            let size = V2i(x.ClientSize.Width, x.ClientSize.Height)
            Log.line "creating SwapChain for size: %A" size

            let swapChainExtent = VkExtent2D(uint32 x.ClientSize.Width, uint32 x.ClientSize.Height)
            let device = desc.Device
            let context = desc.Context

            let ms = desc.Samples > 1

            let swapChain, images = 
                if ms then createRawSwapChain x desc VkImageUsageFlags.TransferDstBit VkImageLayout.TransferDstOptimal
                else createRawSwapChain x desc VkImageUsageFlags.ColorAttachmentBit VkImageLayout.ColorAttachmentOptimal


            let colorViews : VkImageView[] =
                images |> Array.map (fun img ->
                    let image = img.Handle

                    let mutable colorAttachmentView =
                        VkImageViewCreateInfo(
                            VkStructureType.ImageViewCreateInfo,
                            0n, VkImageViewCreateFlags.MinValue,
                            image,
                            VkImageViewType.D2d,
                            desc.ColorFormat,
                            VkComponentMapping.rgba,
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
                        desc.DepthFormat,
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
                        desc.DepthFormat,
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


                let img = Image(context, Unchecked.defaultof<_>, depthImage, VkImageType.D2d, desc.DepthFormat, TextureDimension.Texture2D, V3i(size.X, size.Y, 1), 1, 1, 1, VkImageUsageFlags.DepthStencilAttachmentBit, VkImageLayout.Undefined)
                img.ToLayout(VkImageAspectFlags.DepthBit, VkImageLayout.DepthStencilAttachmentOptimal)
                    |> context.DefaultQueue.RunSynchronously

                depthView

            let chain = new VulkanSwapChain(desc, images, colorViews, depthMemory, depthImage, depthView, swapChain, size) 
            
            if ms then new VulkanSwapChainMS(chain, desc.Samples) :> IVulkanSwapChain
            else chain :> IVulkanSwapChain



