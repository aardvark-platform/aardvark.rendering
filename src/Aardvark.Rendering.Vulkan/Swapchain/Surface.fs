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
open KHRMirSurface
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
        LookupTable.lookupTable [
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
        LookupTable.lookupTable' [
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

    let surfaceCaps = 
        if supported <> 0u then
            native {
                let! pSurfaceCaps = VkSurfaceCapabilitiesKHR()
                VkRaw.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(physical.Handle, handle, pSurfaceCaps) 
                    |> check "could not get Surface capabilities"
                return !!pSurfaceCaps
            }
        else 
            VkSurfaceCapabilitiesKHR()
        
    let supportedTransforms = unbox<VkSurfaceTransformFlagsKHR> (int surfaceCaps.supportedTransforms) |> VkSurfaceTransformFlagsKHR.toImageTrafos
    let supportedCompositeAlpha = unbox<VkCompositeAlphaFlagsKHR> (int surfaceCaps.supportedCompositeAlpha)
    let supportedUsage = surfaceCaps.supportedUsageFlags
    let minSize = V2i(int surfaceCaps.minImageExtent.width, int surfaceCaps.minImageExtent.height)
    let maxSize = V2i(int surfaceCaps.maxImageExtent.width, int surfaceCaps.maxImageExtent.height)
    let maxSlices = int surfaceCaps.maxImageArrayLayers
    let minImageCount = int surfaceCaps.minImageCount
    let maxImageCount = int surfaceCaps.maxImageCount


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
                    |> Map.ofSeqDupl
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
            let surfaceCaps =
                native {
                    let! ptr = VkSurfaceCapabilitiesKHR()
                    VkRaw.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(physical.Handle, handle, ptr) 
                        |> check "could not get Surface capabilities"
                    return !!ptr
                }
            V2i(int surfaceCaps.currentExtent.width, int surfaceCaps.currentExtent.height)
        else
            V2i.Zero
        
    member x.HasCompositeAlpha (t : VkCompositeAlphaFlagsKHR) = (t &&& supportedCompositeAlpha) = t
    member x.HasUsage (t : VkImageUsageFlags) = (t &&& supportedUsage) = t


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
                            VkXlibSurfaceCreateFlagsKHR.MinValue,
                            info.dpy,
                            info.window
                        )

                    VkRaw.vkCreateXlibSurfaceKHR(instance.Handle, pInfo, NativePtr.zero, pHandle)
                        |> check "could not create xlib surface"

                | Xcb info ->
                    let! pInfo =
                        VkXcbSurfaceCreateInfoKHR(
                            VkXcbSurfaceCreateFlagsKHR.MinValue,
                            info.connection,
                            info.window
                        )
                    VkRaw.vkCreateXcbSurfaceKHR(instance.Handle, pInfo, NativePtr.zero, pHandle)
                        |> check "could not create xcb surface"

                | Wayland info ->
                    let! pInfo =
                        VkWaylandSurfaceCreateInfoKHR(
                            VkWaylandSurfaceCreateFlagsKHR.MinValue,
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
                            VkMirSurfaceCreateFlagsKHR.MinValue,
                            info.connection,
                            info.mirSurface
                        )
                    VkRaw.vkCreateMirSurfaceKHR(instance.Handle, pInfo, NativePtr.zero, pHandle)
                        |> check "could not create mir surface"*)

                | Android info ->
                    let! pInfo =
                        VkAndroidSurfaceCreateInfoKHR(
                            VkAndroidSurfaceCreateFlagsKHR.MinValue,
                            info.window
                        )
                    VkRaw.vkCreateAndroidSurfaceKHR(instance.Handle, pInfo, NativePtr.zero, pHandle)
                        |> check "could not create android surface"

                | Win32 info ->
                    let! pInfo =
                        VkWin32SurfaceCreateInfoKHR( 
                            VkWin32SurfaceCreateFlagsKHR.MinValue,
                            info.hinstance,
                            info.hwnd
                        )
                    VkRaw.vkCreateWin32SurfaceKHR(instance.Handle, pInfo, NativePtr.zero, pHandle)
                        |> check "could not create win32 surface"

            return Surface(device, !!pHandle)
        }

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