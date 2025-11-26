namespace Aardvark.Rendering.Vulkan
open System
open Aardvark.Base
open FSharp.Data.Adaptive

open Aardvark.Rendering.Vulkan
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Reflection
open Microsoft.FSharp.NativeInterop

open KHRSurface
open KHRWin32Surface
open KHRXlibSurface
open KHRAndroidSurface
open KHRXcbSurface
open KHRWaylandSurface

#nowarn "9"
// #nowarn "51"

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


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VkSurfaceTransformFlagsKHR =
    let ofImageTrafo =
        LookupTable.lookup [
            ImageTrafo.Identity,            VkSurfaceTransformFlagsKHR.IdentityBit
            ImageTrafo.Rot90,           VkSurfaceTransformFlagsKHR.Rotate90Bit
            ImageTrafo.Rot180,          VkSurfaceTransformFlagsKHR.Rotate180Bit
            ImageTrafo.Rot270,          VkSurfaceTransformFlagsKHR.Rotate270Bit
            ImageTrafo.MirrorX,         VkSurfaceTransformFlagsKHR.HorizontalMirrorBit
            ImageTrafo.MirrorY,         VkSurfaceTransformFlagsKHR.HorizontalMirrorRotate180Bit
            ImageTrafo.Transpose,       VkSurfaceTransformFlagsKHR.HorizontalMirrorRotate90Bit
            ImageTrafo.Transverse,      VkSurfaceTransformFlagsKHR.HorizontalMirrorRotate270Bit
        ]

    let toImageTrafo' =
        LookupTable.tryLookup [
           VkSurfaceTransformFlagsKHR.IdentityBit,                      ImageTrafo.Identity
           VkSurfaceTransformFlagsKHR.Rotate90Bit,                      ImageTrafo.Rot90
           VkSurfaceTransformFlagsKHR.Rotate180Bit,                     ImageTrafo.Rot180
           VkSurfaceTransformFlagsKHR.Rotate270Bit,                     ImageTrafo.Rot270
           VkSurfaceTransformFlagsKHR.HorizontalMirrorBit,              ImageTrafo.MirrorX
           VkSurfaceTransformFlagsKHR.HorizontalMirrorRotate180Bit,     ImageTrafo.MirrorY
           VkSurfaceTransformFlagsKHR.HorizontalMirrorRotate90Bit,      ImageTrafo.Transpose
           VkSurfaceTransformFlagsKHR.HorizontalMirrorRotate270Bit,     ImageTrafo.Transverse
        ]

    let toImageTrafo =
        toImageTrafo' >> Option.get

    let toImageTrafos (flags : VkSurfaceTransformFlagsKHR) =
        let values = Enum.GetValues(typeof<VkSurfaceTransformFlagsKHR>) |> unbox<VkSurfaceTransformFlagsKHR[]>
        values 
            |> Seq.filter (fun v -> (flags &&& v) <> VkSurfaceTransformFlagsKHR.None)
            |> Seq.choose toImageTrafo'
            |> Set.ofSeq

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

    let supported =
        native {
            let! ptr = 0u
            VkRaw.vkGetPhysicalDeviceSurfaceSupportKHR(physical.Handle, uint32 family.Index, handle, ptr)
                |> check "could not get Surface support info"
            return !!ptr
        }

    let getSurfaceCaps() =
        let mutable result = VkSurfaceCapabilitiesKHR.Empty

        if supported = VkTrue then
            use ptr = fixed &result
            VkRaw.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(physical.Handle, handle, ptr)
                |> check "could not get Surface capabilities"
        else
            result.minImageCount <- 1u
            result.maxImageCount <- UInt32.MaxValue
            result.maxImageArrayLayers <- UInt32.MaxValue
            result.maxImageExtent <- VkExtent2D(UInt32.MaxValue, UInt32.MaxValue)

        result

    let toInt =
        min <| (uint32 Int32.MaxValue) >> int

    let surfaceCaps = getSurfaceCaps()

    let supportedTransforms = unbox<VkSurfaceTransformFlagsKHR> (int surfaceCaps.supportedTransforms) |> VkSurfaceTransformFlagsKHR.toImageTrafos
    let supportedCompositeAlpha = unbox<VkCompositeAlphaFlagsKHR> (int surfaceCaps.supportedCompositeAlpha)
    let supportedUsage = surfaceCaps.supportedUsageFlags
    let minSize = V2i(toInt surfaceCaps.minImageExtent.width, toInt surfaceCaps.minImageExtent.height)
    let maxSize = V2i(toInt surfaceCaps.maxImageExtent.width, toInt surfaceCaps.maxImageExtent.height)
    let maxSlices = toInt surfaceCaps.maxImageArrayLayers
    let minImageCount = toInt surfaceCaps.minImageCount
    let maxImageCount = toInt surfaceCaps.maxImageCount


    let presentModes =
        if supported = 0u then
            Set.empty
        else
            native {
                let! pCount = 0u
                VkRaw.vkGetPhysicalDeviceSurfacePresentModesKHR(physical.Handle, handle, pCount, NativePtr.zero) 
                    |> check "could not get Surface present modes"
                    
                let modes : int[] = Array.zeroCreate (int !!pCount)
                let! pModes = modes
                VkRaw.vkGetPhysicalDeviceSurfacePresentModesKHR(physical.Handle, handle, pCount, NativePtr.cast pModes) 
                    |> check "could not get Surface present modes"

                return modes |> Seq.map unbox<VkPresentModeKHR> |> Set.ofSeq
            }

    let supportedFormats =
        if supported = 0u then
            Map.empty
        else
            native {
                let! pFormatCount = 0u
                VkRaw.vkGetPhysicalDeviceSurfaceFormatsKHR(physical.Handle, handle, pFormatCount, NativePtr.zero) 
                    |> check "could not get supported Surface formats"

                let formats = Array.zeroCreate (int !!pFormatCount)
                let! pFormats = formats
                VkRaw.vkGetPhysicalDeviceSurfaceFormatsKHR(physical.Handle, handle, pFormatCount, pFormats) 
                    |> check "could not get supported Surface formats"

                return formats
                    |> Seq.map (fun fmt -> fmt.format, fmt.colorSpace)
                    |> Map.ofSeqWithDuplicates
            }
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
            let surfaceCaps = getSurfaceCaps()
            let size = V2ui(surfaceCaps.currentExtent.width, surfaceCaps.currentExtent.height)
            if size <> V2ui(0xFFFFFFFFu) then
                V2i(toInt size.X, toInt size.Y)
            else
                V2i.Zero // See doc for VkSurfaceCapabilitiesKHR.currentExtent
        else
            V2i.Zero

    member x.HasCompositeAlpha (t : VkCompositeAlphaFlagsKHR) = (t &&& supportedCompositeAlpha) = t
    member x.HasUsage (t : VkImageUsageFlags) = (t &&& supportedUsage) = t

    override x.Destroy() =
        if x.Handle.IsValid then
            VkRaw.vkDestroySurfaceKHR(x.Device.Instance.Handle, x.Handle, NativePtr.zero)
            x.Handle <- VkSurfaceKHR.Null


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Surface =
    let create (info : SurfaceInfo) (device : Device) =
        native {
            let instance = device.Instance
            let! pHandle = VkSurfaceKHR.Null

            match info with
                | XLib info ->
                    let! pInfo = 
                        VkXlibSurfaceCreateInfoKHR(
                            VkXlibSurfaceCreateFlagsKHR.None,
                            info.dpy,
                            info.window
                        )

                    VkRaw.vkCreateXlibSurfaceKHR(instance.Handle, pInfo, NativePtr.zero, pHandle)
                        |> check "could not create xlib surface"

                | Xcb info ->
                    let! pInfo =
                        VkXcbSurfaceCreateInfoKHR(
                            VkXcbSurfaceCreateFlagsKHR.None,
                            info.connection,
                            info.window
                        )
                    VkRaw.vkCreateXcbSurfaceKHR(instance.Handle, pInfo, NativePtr.zero, pHandle)
                        |> check "could not create xcb surface"

                | Wayland info ->
                    let! pInfo =
                        VkWaylandSurfaceCreateInfoKHR(
                            VkWaylandSurfaceCreateFlagsKHR.None,
                            info.display,
                            info.surface
                        )
                    VkRaw.vkCreateWaylandSurfaceKHR(instance.Handle, pInfo, NativePtr.zero, pHandle)
                        |> check "could not create wayland surface"

                | Mir info ->
                    failwith "TODO: MIR"
                    (*let! pInfo =
                        VkMirSurfaceCreateInfoKHR(
                            VkStructureType.MirSurfaceCreateInfoKhr, 0n,
                            VkMirSurfaceCreateFlagsKHR.None,
                            info.connection,
                            info.mirSurface
                        )
                    VkRaw.vkCreateMirSurfaceKHR(instance.Handle, pInfo, NativePtr.zero, pHandle)
                        |> check "could not create mir surface"*)

                | Android info ->
                    let! pInfo =
                        VkAndroidSurfaceCreateInfoKHR(
                            VkAndroidSurfaceCreateFlagsKHR.None,
                            info.window
                        )
                    VkRaw.vkCreateAndroidSurfaceKHR(instance.Handle, pInfo, NativePtr.zero, pHandle)
                        |> check "could not create android surface"

                | Win32 info ->
                    let! pInfo =
                        VkWin32SurfaceCreateInfoKHR( 
                            VkWin32SurfaceCreateFlagsKHR.None,
                            info.hinstance,
                            info.hwnd
                        )
                    VkRaw.vkCreateWin32SurfaceKHR(instance.Handle, pInfo, NativePtr.zero, pHandle)
                        |> check "could not create win32 surface"

            return new Surface(device, !!pHandle)
        }

[<AbstractClass; Sealed; Extension>]
type DeviceSurfaceExtensions private() =
    [<Extension>]
    static member inline CreateSurface(this : Device, info : SurfaceInfo) =
        this |> Surface.create info