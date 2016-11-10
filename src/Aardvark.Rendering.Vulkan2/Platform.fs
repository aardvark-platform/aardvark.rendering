namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Collections.Generic
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base

#nowarn "9"
#nowarn "51"

[<AutoOpen>]
module private Utilities =
    let check (str : string) (err : VkResult) =
        if err <> VkResult.VkSuccess then failwithf "[Vulkan] %s" str

    module NativePtr =
        let withA (f : nativeptr<'a> -> 'b) (a : 'a[]) =
            let gc = GCHandle.Alloc(a, GCHandleType.Pinned)
            try f (gc.AddrOfPinnedObject() |> NativePtr.ofNativeInt)
            finally gc.Free()

    type Version with
        member v.ToVulkan() =
            ((uint32 v.Major) <<< 22) ||| ((uint32 v.Minor) <<< 12) ||| (uint32 v.Build)

        static member FromVulkan (v : uint32) =
            Version(int (v >>> 22), int ((v >>> 12) &&& 0x3FFu), int (v &&& 0xFFFu))

    type V2i with
        static member OfExtent (e : VkExtent2D) =
            V2i(int e.width, int e.height)
        
        member x.ToExtent() =
            VkExtent2D(uint32 x.X, uint32 x.Y)

    type V3i with
        static member OfExtent (e : VkExtent3D) =
            V3i(int e.width, int e.height, int e.depth)
        
        member x.ToExtent() =
            VkExtent3D(uint32 x.X, uint32 x.Y, uint32 x.Z)

    module VkRaw =
        let warn fmt = Printf.kprintf (fun str -> Report.Warn("[Vulkan] {0}", str)) fmt

        let debug fmt = Printf.kprintf (fun str -> Report.Line(2, "[Vulkan] {0}", str)) fmt

type ExtensionInfo =
    {
        name            : string
        specification   : Version
    }

type LayerInfo =
    {
        name            : string
        description     : string
        implementation  : Version
        specification   : Version
        extensions      : list<ExtensionInfo>
    }

[<Flags>]
type QueueFlags = 
    | None              = 0x00000000
    | Graphics          = 0x00000001
    | Compute           = 0x00000002
    | Transfer          = 0x00000004
    | SparseBinding     = 0x00000008

type QueueFamilyInfo =
    {
        index                       : int
        count                       : int
        flags                       : QueueFlags
        minImgTransferGranularity   : V3i
        timestampBits               : int
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ExtensionInfo =
    let inline name (e : ExtensionInfo) = e.name
    let inline specification (e :ExtensionInfo) = e.specification

    let internal ofVkExtensionsProperties (e : VkExtensionProperties) =
        { 
            ExtensionInfo.name = e.extensionName.Value
            ExtensionInfo.specification = Version.FromVulkan e.specVersion 
        }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module LayerInfo =
    let inline name (e : LayerInfo) = e.name
    let inline description (e : LayerInfo) = e.name
    let inline implementation (e : LayerInfo) = e.implementation
    let inline specification (e : LayerInfo) = e.specification
    let inline extensions (e : LayerInfo) = e.extensions

    let internal ofVkInstanceLayerProperties (prop : VkLayerProperties) =
        let name = prop.layerName.Value
        let mutable count = 0u
        VkRaw.vkEnumerateInstanceExtensionProperties(name, &&count, NativePtr.zero)
            |> check "could not get available instance layers"

        let properties = Array.zeroCreate (int count)
        properties |> NativePtr.withA (fun ptr ->
            VkRaw.vkEnumerateInstanceExtensionProperties(name, &&count, ptr)
                |> check "could not get available instance layers"
        )

        let layerExtensions = 
            properties 
                |> Array.toList 
                |> List.map ExtensionInfo.ofVkExtensionsProperties

        {
            LayerInfo.name = prop.layerName.Value
            LayerInfo.description = prop.description.Value
            LayerInfo.implementation = Version.FromVulkan prop.implementationVersion
            LayerInfo.specification = Version.FromVulkan prop.specVersion
            LayerInfo.extensions = layerExtensions
        }

    let internal ofVkDeviceLayerProperties (device : VkPhysicalDevice) (prop : VkLayerProperties) =
        let name = prop.layerName.Value
        let mutable count = 0u
        VkRaw.vkEnumerateDeviceExtensionProperties(device, name, &&count, NativePtr.zero)
            |> check "could not get available instance layers"

        let properties = Array.zeroCreate (int count)
        properties |> NativePtr.withA (fun ptr ->
            VkRaw.vkEnumerateDeviceExtensionProperties(device, name, &&count, ptr)
                |> check "could not get available instance layers"
        )

        let layerExtensions = 
            properties 
                |> Array.toList 
                |> List.map ExtensionInfo.ofVkExtensionsProperties

        {
            LayerInfo.name = prop.layerName.Value
            LayerInfo.description = prop.description.Value
            LayerInfo.implementation = Version.FromVulkan prop.implementationVersion
            LayerInfo.specification = Version.FromVulkan prop.specVersion
            LayerInfo.extensions = layerExtensions
        }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module QueueFlags =
    let inline graphics (f : QueueFlags) = (f &&& QueueFlags.Graphics) <> QueueFlags.None
    let inline compute (f : QueueFlags) = (f &&& QueueFlags.Compute) <> QueueFlags.None
    let inline transfer (f : QueueFlags) = (f &&& QueueFlags.Transfer) <> QueueFlags.None
    let inline sparseBinding (f : QueueFlags) = (f &&& QueueFlags.SparseBinding) <> QueueFlags.None


    let score (f : QueueFlags) =
        let mutable res = 0
        if graphics f then res <- res + 16
        if compute f then res <- res + 8
        if transfer f then res <- res + 4
        if sparseBinding f then res <- res + 2
        res

    let transferOnly (f : QueueFlags) =
        if graphics f || compute f then false
        else transfer f

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module QueueFamilyInfo =
    let inline index (qf : QueueFamilyInfo) = qf.index
    let inline count (qf : QueueFamilyInfo) = qf.count
    let inline flags (qf : QueueFamilyInfo) = qf.flags
    let inline minImgTransferGranularity (qf : QueueFamilyInfo) = qf.minImgTransferGranularity
    let inline timestampBits (qf : QueueFamilyInfo) = qf.timestampBits


type Instance(apiVersion : Version, layers : Set<string>, extensions : Set<string>) as this =   
    static let availableLayers =
        let mutable count = 0u
        VkRaw.vkEnumerateInstanceLayerProperties(&&count, NativePtr.zero)
            |> check "could not get available instance layers"

        let properties = Array.zeroCreate (int count)
        properties |> NativePtr.withA (fun ptr ->
            VkRaw.vkEnumerateInstanceLayerProperties(&&count, ptr)
                |> check "could not get available instance layers"
        )
        properties |> Array.map LayerInfo.ofVkInstanceLayerProperties


    static let globalExtensions =
        let mutable count = 0u
        VkRaw.vkEnumerateInstanceExtensionProperties(null, &&count, NativePtr.zero)
            |> check "could not get available instance layers"

        let properties = Array.zeroCreate (int count)
        properties |> NativePtr.withA (fun ptr ->
            VkRaw.vkEnumerateInstanceExtensionProperties(null, &&count, ptr)
                |> check "could not get available instance layers"
        )
        properties |> Array.map ExtensionInfo.ofVkExtensionsProperties


    static let availableLayerNames = availableLayers |> Seq.map (fun l -> l.name.ToLower(), l) |> Map.ofSeq
    static let globalExtensionNames = globalExtensions |> Seq.map (fun p -> p.name.ToLower(), p.name) |> Map.ofSeq


    static let filterLayersAndExtensions (wantedLayers : Set<string>, wantedExtensions : Set<string>) =
        let availableExtensions = Dictionary globalExtensionNames

        let enabledLayers = 
            wantedLayers |> Set.filter (fun name ->
                let name = name.ToLower()
                match Map.tryFind name availableLayerNames with
                    | Some layer -> 
                        VkRaw.debug "enabled layer %A" name
                        for e in layer.extensions do
                            availableExtensions.[e.name.ToLower()] <- e.name
                        true
                    | _ ->
                        VkRaw.warn "could not enable instance-layer '%s' since it is not available" name
                        false
            )

        let enabledExtensions =
            wantedExtensions |> Seq.choose (fun name ->
                let name = name.ToLower()
                match availableExtensions.TryGetValue name with
                    | (true, realName) -> 
                        VkRaw.debug "enabled extension %A" name
                        Some realName
                    | _ -> 
                        VkRaw.warn "could not enable instance-extension '%s' since it is not available" name
                        None
            ) |> Set.ofSeq

        enabledLayers, enabledExtensions


    let mutable isDisposed = 0
    let layers, extensions = filterLayersAndExtensions (layers, extensions)

    let mutable instance =
        try
            let layers = Set.toArray layers
            let extensions = Set.toArray extensions

            let pLayers = CStr.sallocMany layers
            let pExtensions = CStr.sallocMany extensions
            let appName = CStr.salloc "Aardvark"

            let apiVersion = apiVersion.ToVulkan()

            let mutable applicationInfo =
                VkApplicationInfo(
                    VkStructureType.ApplicationInfo, 0n,
                    appName,
                    0u,
                    appName,
                    0u,
                    apiVersion
                )

            let mutable info =
                VkInstanceCreateInfo(
                    VkStructureType.InstanceCreateInfo, 0n,
                    0u,
                    &&applicationInfo,
                    uint32 layers.Length, pLayers,
                    uint32 extensions.Length, pExtensions
                )

            let mutable instance = VkInstance.Zero

            VkRaw.vkCreateInstance(&&info, NativePtr.zero, &&instance)
                |> check "could not create instance"

            instance
        finally
            ()

    let devices =
        let mutable deviceCount = 0u
        VkRaw.vkEnumeratePhysicalDevices(instance, &&deviceCount, NativePtr.zero)
            |> check "could not get physical devices"

        let devices = Array.zeroCreate (int deviceCount)
        devices |> NativePtr.withA (fun ptr ->
            VkRaw.vkEnumeratePhysicalDevices(instance, &&deviceCount, ptr)
                |> check "could not get physical devices"
        )

        devices |> Array.mapi (fun i d -> PhysicalDevice(this, d, i))




    member x.Dispose() =
        let o = Interlocked.Exchange(&isDisposed, 1)
        if o = 0 then
            VkRaw.vkDestroyInstance(instance, NativePtr.zero)
            instance <- VkInstance.Zero

    member x.Handle = instance

    member x.Devices = devices

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    static member AvailableLayers = availableLayers
    static member GlobalExtensions = globalExtensions

and PhysicalDevice(instance : Instance, handle : VkPhysicalDevice, index : int) =

    let availableLayers = 
        let mutable count = 0u
        VkRaw.vkEnumerateDeviceLayerProperties(handle, &&count, NativePtr.zero)
            |> check "could not get device-layers"

        let props = Array.zeroCreate (int count)
        props |> NativePtr.withA (fun ptr ->
            VkRaw.vkEnumerateDeviceLayerProperties(handle, &&count, ptr)
                |> check "could not get device-layers"
        )

        props |> Array.map (LayerInfo.ofVkDeviceLayerProperties handle)

    let globalExtensions =
        let mutable count = 0u
        VkRaw.vkEnumerateDeviceExtensionProperties(handle, null, &&count, NativePtr.zero)
            |> check "could not get device-extensions"

        let props = Array.zeroCreate (int count)
        props |> NativePtr.withA (fun ptr ->
            VkRaw.vkEnumerateDeviceExtensionProperties(handle, null, &&count, ptr)
                |> check "could not get device-layers"
        )
        props |> Array.map ExtensionInfo.ofVkExtensionsProperties

    let mutable properties = VkPhysicalDeviceProperties()
    do VkRaw.vkGetPhysicalDeviceProperties(handle, &&properties)

    let name = properties.deviceName.Value
    let driverVersion = Version.FromVulkan properties.driverVersion
    let apiVersion = Version.FromVulkan properties.apiVersion
    
    let queueFamilyInfos =
        let mutable count = 0u
        VkRaw.vkGetPhysicalDeviceQueueFamilyProperties(handle, &&count, NativePtr.zero)

        let props = Array.zeroCreate (int count)
        props |> NativePtr.withA (fun ptr ->
            VkRaw.vkGetPhysicalDeviceQueueFamilyProperties(handle, &&count, ptr)  
        )

        props |> Array.mapi (fun i p ->
            {
                index                       = i
                count                       = int p.queueCount
                flags                       = unbox (int p.queueFlags)
                minImgTransferGranularity   = V3i.OfExtent p.minImageTransferGranularity
                timestampBits               = int p.timestampValidBits
            }
        )

    
    let mainQueue = queueFamilyInfos |> Array.maxBy (QueueFamilyInfo.flags >> QueueFlags.score)
    let pureTransferQueue = queueFamilyInfos |> Array.tryFind (QueueFamilyInfo.flags >> QueueFlags.transferOnly)

    member x.AvailableLayers = availableLayers
    member x.GlobalExtensions = globalExtensions
    member x.QueueFamilies = queueFamilyInfos
    member x.MainQueue = mainQueue
    member x.TransferQueue = pureTransferQueue


    member x.Handle = handle
    member x.Index = index
    member x.Name = name
    member x.Type = properties.deviceType
    member x.APIVersion = apiVersion
    member x.DriverVersion = driverVersion


    member x.Instance = instance

    override x.ToString() =
        sprintf "{ name = %s; type = %A; api = %A }" name x.Type x.APIVersion

and Device(physical : PhysicalDevice, wantedLayers : Set<string>, wantedExtensions : Set<string>, queues : list<QueueFamilyInfo * int>) =

    let layers, extensions =
        let availableExtensions = physical.GlobalExtensions |> Seq.map (fun e -> e.name.ToLower(), e.name) |> Dictionary.ofSeq
        let availableLayerNames = physical.AvailableLayers |> Seq.map (fun l -> l.name.ToLower(), l) |> Map.ofSeq
        let enabledLayers = 
            wantedLayers |> Set.filter (fun name ->
                let name = name.ToLower()
                match Map.tryFind name availableLayerNames with
                    | Some layer -> 
                        VkRaw.debug "enabled layer %A" name
                        for e in layer.extensions do
                            availableExtensions.[e.name.ToLower()] <- e.name
                        true
                    | _ ->
                        VkRaw.warn "could not enable instance-layer '%s' since it is not available" name
                        false
            )

        let enabledExtensions =
            wantedExtensions |> Seq.choose (fun name ->
                let name = name.ToLower()
                match availableExtensions.TryGetValue name with
                    | (true, realName) -> 
                        VkRaw.debug "enabled extension %A" name
                        Some realName
                    | _ -> 
                        VkRaw.warn "could not enable instance-extension '%s' since it is not available" name
                        None
            ) |> Set.ofSeq

        enabledLayers, enabledExtensions


    let queues = 
        queues |> List.choose (fun (q,c) ->
            if c < 0 then 
                None
            else
                let count = 
                    if c > q.count then
                        VkRaw.warn "could not create %d queues for family %A (only %d available)" c q.index q.count
                        q.count
                    else
                        c 
                Some (q.index, count)
        )

    let device =
        let layers = Set.toArray layers
        let extensions = Set.toArray extensions
        let pLayers = CStr.sallocMany layers
        let pExtensions = CStr.sallocMany extensions

        let queueInfos =
            queues |> List.toArray |> Array.map (fun (index,count) -> 
                VkDeviceQueueCreateInfo(
                    VkStructureType.DeviceQueueCreateInfo, 0n,
                    0u,
                    uint32 index,
                    uint32 count,
                    NativePtr.zero
                )
            )
        queueInfos |> NativePtr.withA (fun ptr ->
            let info =
                VkDeviceCreateInfo(
                    VkStructureType.DeviceCreateInfo, 0n,
                    0u,
                    uint32 queueInfos.Length, ptr,
                    uint32 layers.Length, pLayers,
                    uint32 extensions.Length, pExtensions,
                    NativePtr.zero

                )
            ()
        )

    member x.PhysicalDevice = physical