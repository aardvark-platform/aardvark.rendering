namespace Aardvark.Rendering.Vulkan
open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Reflection
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"

//static member XLibSurfaceCreateInfo = unbox<VkStructureType> 1000004000
//static member XcbSurfaceCreateInfo = unbox<VkStructureType> 1000005000
//static member WaylandSurfaceCreateInfo = unbox<VkStructureType> 1000006000
//static member MirSurfaceCreateInfo = unbox<VkStructureType> 1000007000
//static member AndroidSurfaceCreateInfo = unbox<VkStructureType> 1000008000
//static member Win32SurfaceCreateInfo = unbox<VkStructureType> 1000009000



type XLibSurfaceInfo = { dpy : nativeptr<nativeint>; window : nativeint }
type XcbSurfaceInfo = { connection : nativeptr<nativeint>; window : nativeint }
type WaylandSurfaceInfo = { display : nativeptr<nativeint>; surface : nativeptr<nativeint> }
type MirSurfaceInfo = { connection : nativeptr<nativeint>; mirSurface : nativeptr<nativeint> }
type AndroidSurfaceInfo = { window : nativeptr<nativeint> }
type Win32SurfaceInfo = { hinstance : nativeint; hwnd : nativeint }

type SurfaceInfo = 
    | XLib of XLibSurfaceInfo
    | Xcb of XcbSurfaceInfo
    | Wayland of WaylandSurfaceInfo
    | Mir of MirSurfaceInfo
    | Android of AndroidSurfaceInfo
    | Win32 of Win32SurfaceInfo

type Surface(device : Device, handle : VkSurfaceKHR) =
    inherit Resource<VkSurfaceKHR>(device, handle)

    static let depthFormats =
        Set.ofList [
            VkFormat.D16Unorm
            VkFormat.D16UnormS8Uint
            VkFormat.D24UnormS8Uint
            VkFormat.D32Sfloat
            VkFormat.D32SfloatS8Uint
            VkFormat.X8D24UnormPack32
        ]

    let onDispose = Event<unit>()

    let physical = device.PhysicalDevice
    let family = device.GraphicsFamily
        

    let mutable supported = 0u
    do VkRaw.vkGetPhysicalDeviceSurfaceSupportKHR(physical.Handle, uint32 family.Index, handle, &&supported)
        |> check "could not get Surface support info"

    let mutable surfaceCaps = VkSurfaceCapabilitiesKHR()
    do if supported <> 0u then
            VkRaw.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(physical.Handle, handle, &&surfaceCaps) 
                |> check "could not get Surface capabilities"
        
    let supportedTransforms = unbox<VkSurfaceTransformFlagBitsKHR> (int surfaceCaps.supportedTransforms)
    let supportedCompositeAlpha = unbox<VkCompositeAlphaFlagBitsKHR> (int surfaceCaps.supportedCompositeAlpha)
    let supportedUsage = surfaceCaps.supportedUsageFlags
    let minSize = V2i(int surfaceCaps.minImageExtent.width, int surfaceCaps.minImageExtent.height)
    let maxSize = V2i(int surfaceCaps.maxImageExtent.width, int surfaceCaps.maxImageExtent.height)
    let maxSlices = int surfaceCaps.maxImageArrayLayers
    let minImageCount = int surfaceCaps.minImageCount
    let maxImageCount = int surfaceCaps.maxImageCount


    let mutable presentModes =
        if supported = 0u then
            Set.empty
        else
            let mutable count = 0u
            VkRaw.vkGetPhysicalDeviceSurfacePresentModesKHR(physical.Handle, handle, &&count, NativePtr.zero) 
                |> check "could not get Surface present modes"

            
            let modes : int[] = Array.zeroCreate (int count)
            modes |> NativePtr.withA (fun pModes ->
                VkRaw.vkGetPhysicalDeviceSurfacePresentModesKHR(physical.Handle, handle, &&count, NativePtr.cast pModes) 
                    |> check "could not get Surface present modes"
            )

            modes |> Seq.map unbox<VkPresentModeKHR> |> Set.ofSeq

    let supportedFormats =
        if supported = 0u then
            Map.empty
        else
            let mutable formatCount = 0u
            VkRaw.vkGetPhysicalDeviceSurfaceFormatsKHR(physical.Handle, handle, &&formatCount, NativePtr.zero) 
                |> check "could not get supported Surface formats"

            let formats = Array.zeroCreate (int formatCount)
            formats |> NativePtr.withA (fun pFormats ->
                VkRaw.vkGetPhysicalDeviceSurfaceFormatsKHR(physical.Handle, handle, &&formatCount, pFormats) 
                    |> check "could not get supported Surface formats"
            )

            formats
                |> Seq.map (fun fmt -> fmt.format, fmt.colorSpace)
                |> Map.ofSeqDupl

    let availableFormats =
        supportedFormats |> Map.toSeq |> Seq.map fst |> Set.ofSeq

    let availableDepthFormats =
        depthFormats |> Set.filter (fun fmt -> physical.GetFormatFeatures(VkImageTiling.Optimal, fmt).HasFlag(VkFormatFeatureFlags.DepthStencilAttachmentBit))

    [<CLIEvent>]
    member x.OnDispose = onDispose.Publish
    member x.IsSupported = supported <> 0u
    member x.ColorFormats = availableFormats
    member x.DepthFormats = availableDepthFormats
    member x.ColorSpaces = supportedFormats
    member x.PresentModes = presentModes
    member x.Transforms = supportedTransforms
    member x.CompositeAlpha = supportedCompositeAlpha
    member x.Usage = supportedUsage
    member x.MinSize = minSize
    member x.MaxSize = maxSize
    member x.MaxArraySlices = maxSlices
    member x.MinImageCount = minImageCount
    member x.MaxImageCount = maxImageCount
       
    member x.Size =
        if supported <> 0u && handle.IsValid then
            let mutable surfaceCaps = VkSurfaceCapabilitiesKHR()
            VkRaw.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(physical.Handle, handle, &&surfaceCaps) 
                |> check "could not get Surface capabilities"

            V2i(int surfaceCaps.currentExtent.width, int surfaceCaps.currentExtent.height)
        else
            V2i.Zero
        
    member x.HasTransform (t : VkSurfaceTransformFlagBitsKHR) = (t &&& supportedTransforms) = t
    member x.HasCompositeAlpha (t : VkCompositeAlphaFlagBitsKHR) = (t &&& supportedCompositeAlpha) = t
    member x.HasUsage (t : VkImageUsageFlags) = (t &&& supportedUsage) = t


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Surface =
    let create (info : SurfaceInfo) (device : Device) =
        let instance = device.Instance
        let mutable handle = VkSurfaceKHR.Null

        match info with
            | XLib info ->
                let mutable info = 
                    VkXlibSurfaceCreateInfoKHR(
                        VkStructureType.XLibSurfaceCreateInfo, 0n,
                        VkXlibSurfaceCreateFlagsKHR.MinValue,
                        info.dpy,
                        info.window
                    )

                VkRaw.vkCreateXlibSurfaceKHR(instance.Handle, &&info, NativePtr.zero, &&handle)
                    |> check "could not create xlib surface"

            | Xcb info ->
                let mutable info =
                    VkXcbSurfaceCreateInfoKHR(
                        VkStructureType.XcbSurfaceCreateInfo, 0n,
                        VkXcbSurfaceCreateFlagsKHR.MinValue,
                        info.connection,
                        info.window
                    )
                VkRaw.vkCreateXcbSurfaceKHR(instance.Handle, &&info, NativePtr.zero, &&handle)
                    |> check "could not create xcb surface"

            | Wayland info ->
                let mutable info =
                    VkWaylandSurfaceCreateInfoKHR(
                        VkStructureType.WaylandSurfaceCreateInfo, 0n,
                        VkWaylandSurfaceCreateFlagsKHR.MinValue,
                        info.display,
                        info.surface
                    )
                VkRaw.vkCreateWaylandSurfaceKHR(instance.Handle, &&info, NativePtr.zero, &&handle)
                    |> check "could not create wayland surface"

            | Mir info ->
                let mutable info =
                    VkMirSurfaceCreateInfoKHR(
                        VkStructureType.MirSurfaceCreateInfo, 0n,
                        VkMirSurfaceCreateFlagsKHR.MinValue,
                        info.connection,
                        info.mirSurface
                    )
                VkRaw.vkCreateMirSurfaceKHR(instance.Handle, &&info, NativePtr.zero, &&handle)
                    |> check "could not create mir surface"

            | Android info ->
                let mutable info =
                    VkAndroidSurfaceCreateInfoKHR(
                        VkStructureType.AndroidSurfaceCreateInfo, 0n,
                        VkAndroidSurfaceCreateFlagsKHR.MinValue,
                        info.window
                    )
                VkRaw.vkCreateAndroidSurfaceKHR(instance.Handle, &&info, NativePtr.zero, &&handle)
                    |> check "could not create android surface"

            | Win32 info ->
                let mutable info =
                    VkWin32SurfaceCreateInfoKHR(
                        VkStructureType.Win32SurfaceCreateInfo, 0n, 
                        VkWin32SurfaceCreateFlagsKHR.MinValue,
                        info.hinstance,
                        info.hwnd
                    )
                VkRaw.vkCreateWin32SurfaceKHR(instance.Handle, &&info, NativePtr.zero, &&handle)
                    |> check "could not create win32 surface"

        Surface(device, handle)

    let delete (s : Surface) (device : Device) =
        if s.Handle.IsValid then
            VkRaw.vkDestroySurfaceKHR(device.Instance.Handle, s.Handle, NativePtr.zero)
            s.Handle <- VkSurfaceKHR.Null

[<AbstractClass; Sealed; Extension>]
type DeviceSurfaceExtensions private() =
    [<Extension>]
    static member inline CreateSurface(this : Device, info : SurfaceInfo) =
        this |> Surface.create info

    [<Extension>]
    static member inline Delete(this : Device, s : Surface) =
        this |> Surface.delete s