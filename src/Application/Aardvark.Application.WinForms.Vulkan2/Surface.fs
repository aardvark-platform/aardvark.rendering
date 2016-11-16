namespace Aardvark.Application.WinForms.Vulkan

open System
open Aardvark.Application
open Aardvark.Application.WinForms
open System.Windows.Forms
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

    module private Xcb = 
        [<DllImport("X11-xcb")>]
        extern nativeint private XGetXCBConnection(nativeint xdisplay)

    let ofControl (ctrl : Control) (device : Device) =
        let instance = device.Instance

        match Environment.OSVersion with
            | Windows ->
                let entryModule     = Assembly.GetEntryAssembly().GetModules().[0]
                let hwnd            = ctrl.Handle
                let hinstance       = Marshal.GetHINSTANCE(entryModule)

                let mutable info =
                    VkWin32SurfaceCreateInfoKHR(
                        VkStructureType.Win32SurfaceCreateInfo, 0n,
                        VkWin32SurfaceCreateFlagsKHR.MinValue,
                        hinstance, hwnd
                    )

                let mutable handle = VkSurfaceKHR.Null
                VkRaw.vkCreateWin32SurfaceKHR(instance.Handle, &&info, NativePtr.zero, &&handle)
                    |> check "could not create Win32 surface"

                Surface(device, handle)

            | Linux ->
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
                let connection = Xcb.XGetXCBConnection(display)
                let window = ctrl.Handle

                let dpy = NativePtr.alloc 1
                NativePtr.write dpy connection

                let mutable info =
                    VkXcbSurfaceCreateInfoKHR(
                        VkStructureType.XcbSurfaceCreateInfo, 0n,
                        VkXcbSurfaceCreateFlagsKHR.MinValue,
                        dpy, window
                    )

                let mutable handle = VkSurfaceKHR.Null
                VkRaw.vkCreateXcbSurfaceKHR(instance.Handle, &&info, NativePtr.zero, &&handle)
                    |> check "could not create Xcb surface"

                Surface(device, handle)

            | platform ->
                failf "unsupported platform: %A" platform

    let delete (s : Surface) (device : Device) =
        if s.Handle.IsValid then
            VkRaw.vkDestroySurfaceKHR(device.Instance.Handle, s.Handle, NativePtr.zero)
            s.Handle <- VkSurfaceKHR.Null

[<AbstractClass; Sealed; Extension>]
type DeviceSurfaceExtensions private() =
    [<Extension>]
    static member inline CreateSurface(this : Device, ctrl : Control) =
        this |> Surface.ofControl ctrl

    [<Extension>]
    static member inline Delete(this : Device, s : Surface) =
        this |> Surface.delete s