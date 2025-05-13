namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Collections.Concurrent
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Rendering
open KHRSwapchain
open EXTMemoryBudget
open Vulkan11

#nowarn "9"
#nowarn "51"

[<RequireQualifiedAccess>]
type UploadMode =
    | Sync
    | Async

type Device private (physicalDevice: PhysicalDevice, wantedExtensions: string seq) =
    let isGroup, physicalDevices =
        match physicalDevice with
        | :? PhysicalDeviceGroup as g -> true, g.Devices
        | _ -> false, [| physicalDevice |]

    let instance = physicalDevice.Instance

    let caches = ConcurrentDictionary<Symbol, IDeviceCache>()

    // Find a graphics, compute and transfer family
    let graphicsFamilyInfo, computeFamilyInfo, transferFamilyInfo =
        let sortedFamilies =
            physicalDevice.QueueFamilies
            |> Array.sortByDescending (_.flags >> QueueFlags.score)

        let findFamily excludedFlags requiredFlags =
            sortedFamilies
            |> Array.tryFind (fun qf ->
                qf.flags.HasFlag requiredFlags &&
                qf.flags &&& excludedFlags = QueueFlags.None
            )

        QueueFlags.Graphics |> findFamily QueueFlags.None,
        QueueFlags.Compute  |> findFamily QueueFlags.Graphics,
        QueueFlags.Transfer |> findFamily (QueueFlags.Graphics ||| QueueFlags.Compute)

    let queueFamilyInfos =
        [| graphicsFamilyInfo; computeFamilyInfo; transferFamilyInfo |]
        |> Array.collect Option.toArray
        |> Array.distinct

    let pAllQueueFamilyIndices =
        if queueFamilyInfos.Length <= 1 then
            NativePtr.zero
        else
            let ptr = NativePtr.alloc queueFamilyInfos.Length
            for i = 0 to queueFamilyInfos.Length - 1 do
                ptr.[i] <- uint32 queueFamilyInfos.[i].index
            ptr

    let uploadMode =
        if transferFamilyInfo.IsNone then UploadMode.Sync
        else UploadMode.Async

    let sharingMode =
        if queueFamilyInfos.Length <= 1 then VkSharingMode.Exclusive
        else VkSharingMode.Concurrent

    let onDispose = Event<unit>()
    let onDisposeObservable = onDispose.Publish

    let mutable shaderCachePath : Option<string> =
        Some <| Path.combine [
            CachingProperties.CacheDirectory
            "Shaders"
            "Vulkan"
        ]

    let wantedExtensions =
        if instance.DebugConfig.DebugPrintEnabled || instance.DebugConfig.GenerateShaderDebugInfo then
            List.ofSeq wantedExtensions @ [KHRShaderNonSemanticInfo.Name]
        else
            List.ofSeq wantedExtensions

    let enabledExtensions =
        let availableExtensions = physicalDevice.GlobalExtensions |> Seq.map (fun e -> e.name.ToLower(), e.name) |> Dictionary.ofSeq

        let enabledExtensions =
            wantedExtensions
            |> List.filter (fun e -> instance.EnabledExtensions |> List.contains e |> not)
            |> List.choose (fun name ->
                let name = name.ToLower()
                match availableExtensions.TryGetValue name with
                | true, realName ->
                    Log.Vulkan.debug "enabled device extension %A" name
                    Some realName
                | _ ->
                    Log.Vulkan.warn "could not enable extension '%s' since it is not available" name
                    None
            )

        enabledExtensions

    let enabledFeatures =
        physicalDevice.GetFeatures(flip List.contains enabledExtensions)

    let mutable isDisposed = 0

    let mutable device =
        let queuePriorities = Array.replicate 32 1.0f
        use pQueuePriorities = fixed queuePriorities

        let queueCreateInfos =
            queueFamilyInfos
            |> Array.map (fun familyInfo ->
                VkDeviceQueueCreateInfo(
                    VkDeviceQueueCreateFlags.None,
                    uint32 familyInfo.index,
                    uint32 familyInfo.count,
                    pQueuePriorities
                )
            )

        native {
            let! pQueueCreateInfos = queueCreateInfos
            let extensions = List.toArray enabledExtensions
            let! pExtensions = extensions

            let deviceHandles = physicalDevices |> Array.map _.Handle
            let! pDevices = deviceHandles
            let groupInfo =
                VkDeviceGroupDeviceCreateInfo(
                    uint32 physicalDevices.Length,
                    pDevices
                )

            // TODO: Do we really want to enable all available features?
            // Does this have real performance implications?
            use pNext =
                DeviceFeatures.toNativeChain enabledFeatures
                |> if isGroup then VkStructChain.add groupInfo else id

            let! pInfo =
                VkDeviceCreateInfo(
                    pNext.Handle,
                    VkDeviceCreateFlags.None,
                    uint32 queueCreateInfos.Length, pQueueCreateInfos,
                    0u, NativePtr.zero,
                    uint32 extensions.Length, pExtensions,
                    NativePtr.zero
                )
            let! pDevice = VkDevice.Zero

            VkRaw.vkCreateDevice(physicalDevice.Handle, pInfo, NativePtr.zero, pDevice)
                |> check "could not create device"

            return !!pDevice
        }

    let mutable queueFamilies : DeviceQueueFamily[] = null
    let mutable graphicsFamily : DeviceQueueFamily option = None
    let mutable computeFamily : DeviceQueueFamily option = None
    let mutable transferFamily : DeviceQueueFamily option = None

    let mutable memoryAllocator : MemoryAllocator = Unchecked.defaultof<_>
    let mutable hostMemory : IDeviceMemory = Unchecked.defaultof<_>
    let mutable deviceMemory : IDeviceMemory = Unchecked.defaultof<_>
    let mutable stagingMemory : IDeviceMemory = Unchecked.defaultof<_>
    let mutable readbackMemory : IDeviceMemory = Unchecked.defaultof<_>

    let mutable runtime = Unchecked.defaultof<IRuntime>

    let copyEngine = 
        lazy (
            match transferFamily with
            | Some pool -> new CopyEngine(pool)
            | None -> failf "the device does not support transfer-queues"
        )

    member private this.Initialize() =
        queueFamilies <-
            queueFamilyInfos |> Array.map (fun info ->
                DeviceQueueFamily.Create(this, info, onDisposeObservable)
            )

        let getFamily (info: QueueFamilyInfo) =
            queueFamilies |> Array.find (fun qf -> qf.Info = info)

        graphicsFamily <- graphicsFamilyInfo |> Option.map getFamily
        computeFamily <- computeFamilyInfo |> Option.map getFamily
        transferFamily <- transferFamilyInfo |> Option.map getFamily

        memoryAllocator <- new MemoryAllocator(this)
        hostMemory <- memoryAllocator.GetMemory(preferDevice = true, hostAccess = HostAccess.WriteOnly)
        deviceMemory <- memoryAllocator.GetMemory(preferDevice = true, hostAccess = HostAccess.None)
        stagingMemory <- memoryAllocator.GetMemory(preferDevice = false, hostAccess = HostAccess.WriteOnly)
        readbackMemory <- memoryAllocator.GetMemory(preferDevice = false, hostAccess = HostAccess.ReadWrite)

    static member Create(physicalDevice: PhysicalDevice, wantedExtensions: string seq) =
        let device = new Device(physicalDevice, wantedExtensions)
        device.Initialize()
        device

    member x.CopyEngine = copyEngine.Value

    member x.ShaderCachePath
        with get() = shaderCachePath
        and set p = shaderCachePath <- p

    member x.UploadMode = uploadMode

    member x.GetCache(name : Symbol) : DeviceCache<'Value, 'Resource> =
        caches.GetOrAdd(name, fun name ->
            DeviceCache<'Value, 'Resource>(name, onDisposeObservable) :> IDeviceCache
        ) |> unbox

    /// Gets or creates a cached resource for the cache with the given name.
    /// Cached resources are kept alive until they are removed from the cache (see Device.RemoveCached()) and
    /// all references to it have been disposed.
    member x.GetCached(cacheName: Symbol, value: 'Value, create: 'Value -> 'Resource) : 'Resource =
        let cache : DeviceCache<'Value, 'Resource> = x.GetCache(cacheName)
        cache.Invoke(value, create)

    /// Removes the given resource from its cache (if it was cached).
    /// The resource is destroyed once all references have been disposed.
    member x.RemoveCached(value : #ICachedResource) : unit =
        match value.Cache with
        | Some (:? IDeviceCache<_> as cache) -> cache.Revoke value
        | Some cache -> Log.warn $"[Vulkan] Cannot remove {value} from device cache '{cache.Name}' since it is not compatible"
        | _ -> ()

    member x.ComputeToken =
        x.ComputeFamily.CurrentToken

    member x.Token =
        x.GraphicsFamily.CurrentToken

    member x.Runtime
        with get() = runtime
        and internal set r = runtime <- r

    member x.QueueFamilies = queueFamilies

    member x.EnabledFeatures = enabledFeatures

    member x.EnabledExtensions = enabledExtensions

    member x.IsExtensionEnabled(extension) =
        enabledExtensions |> List.contains extension

    /// Returns whether descriptors may be updated after being bound.
    member x.UpdateDescriptorsAfterBind =
        not <| RuntimeConfig.SuppressUpdateAfterBind &&
        x.IsExtensionEnabled EXTDescriptorIndexing.Name

    member x.MinMemoryMapAlignment = physicalDevice.Limits.Memory.MinMemoryMapAlignment
    member x.MinTexelBufferOffsetAlignment = physicalDevice.Limits.Memory.MinTexelBufferOffsetAlignment
    member x.MinUniformBufferOffsetAlignment = physicalDevice.Limits.Memory.MinUniformBufferOffsetAlignment
    member x.MinStorageBufferOffsetAlignment = physicalDevice.Limits.Memory.MinStorageBufferOffsetAlignment
    member x.BufferImageGranularity = physicalDevice.Limits.Memory.BufferImageGranularity

    member x.Instance = instance
    member x.DebugConfig = instance.DebugConfig

    member internal x.QueueFamilyCount = uint32 queueFamilies.Length
    member internal x.QueueFamilyIndices = pAllQueueFamilyIndices
    member internal x.SharingMode = sharingMode

    member x.GraphicsFamily : DeviceQueueFamily  = 
        match graphicsFamily with
        | Some pool -> pool
        | None -> failf "the device does not support graphics-queues"

    member x.ComputeFamily : DeviceQueueFamily =
        match computeFamily with
        | Some pool -> pool
        | None -> failf "the device does not support compute-queues"

    member x.TransferFamily : DeviceQueueFamily =
        match transferFamily with
        | Some pool -> pool
        | None -> failf "the device does not support transfer-queues"

    member x.IsDisposed = instance.IsDisposed || isDisposed <> 0

    member x.NullPtr = memoryAllocator.NullPtr

    /// Memory for resources that are frequently written by the CPU and read by the GPU.
    /// Must be host visible but device local memory is preferred.
    member x.HostMemory = hostMemory

    /// Memory for static resources that are read by the GPU.
    member x.DeviceMemory = deviceMemory

    /// Memory for staging buffers that are only written (sequentially) by the CPU.
    /// If the staging memory is written non-sequentially use ReadbackMemory instead.
    member x.StagingMemory = stagingMemory

    /// Memory for resources that are read by the CPU to readback data from the GPU.
    member x.ReadbackMemory = readbackMemory

    member x.OnDispose = onDisposeObservable :> IObservable<_>

    member x.Dispose() =
        if not instance.IsDisposed then
            let o = Interlocked.Exchange(&isDisposed, 1)
            if o = 0 then 
                if copyEngine.IsValueCreated then
                    copyEngine.Value.Dispose()

                onDispose.Trigger()
                memoryAllocator.Dispose()
                for f in queueFamilies do f.Dispose()
                VkRaw.vkDestroyDevice(device, NativePtr.zero)
                device <- VkDevice.Zero

                if queueFamilies.Length > 0 then
                    NativePtr.free pAllQueueFamilyIndices

    member x.Handle = device

    member x.PhysicalDevice = physicalDevice
    member x.PhysicalDevices = physicalDevices
    member x.PhysicalDeviceGroup = physicalDevice :?> PhysicalDeviceGroup
    member x.IsDeviceGroup = isGroup

    member x.PrintMemoryUsage([<Optional; DefaultParameterValue(2)>] verbosity: int) =
        memoryAllocator.PrintUsage verbosity

    interface IDevice with
        member x.Handle = x.Handle
        member x.Instance = x.Instance
        member x.PhysicalDevice = x.PhysicalDevice
        member x.EnabledFeatures = x.EnabledFeatures
        member x.IsExtensionEnabled(extension) = x.IsExtensionEnabled(extension)

    interface IDisposable with
        member x.Dispose() = x.Dispose()


[<AutoOpen>]
module IDeviceObjectExtensions =

    type IDeviceObject with
        member inline x.Device = x.DeviceInterface :?> Device

[<AbstractClass; Sealed; Extension>]
type DeviceExtensions private() =

    [<Extension>]
    static member CreateDevice(this: PhysicalDevice, wantedExtensions: string seq) =
        Device.Create(this, wantedExtensions)

    [<Extension>]
    static member Set(semaphore: Vulkan.Semaphore) =
        if semaphore.Handle.IsValid then
            let device = unbox<Device> semaphore.Device
            use h = device.GraphicsFamily.CurrentQueue
            h.Queue.RunSynchronously([||], [|semaphore|], [||])
        else
            failf "cannot signal disposed fence"