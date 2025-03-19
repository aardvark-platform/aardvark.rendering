namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open System
open Vulkan11
open KHRGetPhysicalDeviceProperties2
open KHRRayTracingPipeline
open KHRRayQuery
open KHRAccelerationStructure
open KHRBufferDeviceAddress
open EXTDescriptorIndexing

type IVulkanInstance = interface end

type PhysicalDevice internal(instance: IVulkanInstance, handle: VkPhysicalDevice, hasInstanceExtension: string -> bool) =
    static let allFormats = Enum.GetValues(typeof<VkFormat>) |> unbox<VkFormat[]>

    let availableLayers =
        native {
            let! pCount = 0u
            VkRaw.vkEnumerateDeviceLayerProperties(handle, pCount, NativePtr.zero)
                |> check "could not get device-layers"

            let props = Array.zeroCreate (int !!pCount)
            let! ptr = props
            VkRaw.vkEnumerateDeviceLayerProperties(handle, pCount, ptr)
                |> check "could not get device-layers"

            return props |> Array.map (LayerInfo.ofVulkanDevice handle)
        }

    let globalExtensions =
        native {
            let! pCount = 0u
            VkRaw.vkEnumerateDeviceExtensionProperties(handle, null, pCount, NativePtr.zero)
                |> check "could not get device-extensions"

            let props = Array.zeroCreate (int !!pCount)
            let! ptr = props
            VkRaw.vkEnumerateDeviceExtensionProperties(handle, null, pCount, ptr)
                |> check "could not get device-layers"
            return props |> Array.map ExtensionInfo.ofVulkan
        }

    let hasExtension (name : string) =
        globalExtensions |> Array.exists (fun e -> e.name = name)

    let queryFeatures =
        let inline readOrEmpty (ptr : nativeptr< ^a>) =
            if NativePtr.isNull ptr then
                ((^a) : (static member Empty : ^a) ())
            else
                !!ptr

        fun (hasExtension: string -> bool) ->
            let f, pm, ycbcr, s16, vp, sdp, idx, rtp, acc, rq, bda =
                use chain = new VkStructChain()
                let pMem        = chain.Add<VkPhysicalDeviceProtectedMemoryFeatures>()
                let pYcbcr      = chain.Add<VkPhysicalDeviceSamplerYcbcrConversionFeatures>()
                let p16bit      = chain.Add<VkPhysicalDevice16BitStorageFeatures>()
                let pVarPtrs    = chain.Add<VkPhysicalDeviceVariablePointersFeatures>()
                let pDrawParams = chain.Add<VkPhysicalDeviceShaderDrawParametersFeatures>()
                let pIdx        = if hasExtension EXTDescriptorIndexing.Name then chain.Add<VkPhysicalDeviceDescriptorIndexingFeaturesEXT>() else NativePtr.zero
                let pRTP        = if hasExtension KHRRayTracingPipeline.Name then chain.Add<VkPhysicalDeviceRayTracingPipelineFeaturesKHR>() else NativePtr.zero
                let pAcc        = if hasExtension KHRAccelerationStructure.Name then chain.Add<VkPhysicalDeviceAccelerationStructureFeaturesKHR>() else NativePtr.zero
                let pRQ         = if hasExtension KHRRayQuery.Name then chain.Add<VkPhysicalDeviceRayQueryFeaturesKHR>() else NativePtr.zero
                let pDevAddr    = if hasExtension KHRBufferDeviceAddress.Name then chain.Add<VkPhysicalDeviceBufferDeviceAddressFeaturesKHR>() else NativePtr.zero
                let pFeatures   = chain.Add<VkPhysicalDeviceFeatures2>()

                VkRaw.vkGetPhysicalDeviceFeatures2(handle, VkStructChain.toNativePtr chain)
                (!!pFeatures).features, !!pMem, !!pYcbcr, !!p16bit, !!pVarPtrs, !!pDrawParams, readOrEmpty pIdx,
                readOrEmpty pRTP, readOrEmpty pAcc, readOrEmpty pRQ, readOrEmpty pDevAddr

            DeviceFeatures.create pm ycbcr s16 vp sdp idx rtp acc rq bda f

    let features =
        queryFeatures hasExtension

    let properties, raytracingProperties =
        use chain = new VkStructChain()

        let pRTP, pAcc =
            if hasExtension KHRRayTracingPipeline.Name then
                chain.Add<VkPhysicalDeviceRayTracingPipelinePropertiesKHR>(),
                chain.Add<VkPhysicalDeviceAccelerationStructurePropertiesKHR>()
            else
                NativePtr.zero, NativePtr.zero

        let pProperties = chain.Add<VkPhysicalDeviceProperties2>()

        VkRaw.vkGetPhysicalDeviceProperties2(handle, VkStructChain.toNativePtr chain)
        let props = (!!pProperties).properties

        if hasExtension KHRRayTracingPipeline.Name then
            props, Some(!!pRTP, !!pAcc)
        else
            props, None

    let name = properties.deviceName.Value
    let driverVersion = Version.FromVulkan properties.driverVersion
    let apiVersion = Version.FromVulkan properties.apiVersion


    let maxAllocationSize, maxPerSetDescriptors =
        if apiVersion >= Version(1,1,0) || hasExtension KHRMaintenance3.Name then
            let main3 =
                VkPhysicalDeviceMaintenance3Properties(10u, 10UL)
            main3 |> NativePtr.pin (fun pMain3 ->
                let props =
                    VkPhysicalDeviceProperties2(
                        pMain3.Address,
                        VkPhysicalDeviceProperties()
                    )
                props |> NativePtr.pin (fun pProps ->
                    VkRaw.vkGetPhysicalDeviceProperties2(handle, pProps)
                    let main3 = pMain3.[0]

                    let maxMemoryAllocationSize = min (uint64 Int64.MaxValue) main3.maxMemoryAllocationSize |> int64
                    let maxPerSetDescriptors = min (uint32 Int32.MaxValue) main3.maxPerSetDescriptors |> int

                    maxMemoryAllocationSize, maxPerSetDescriptors
                )
            )
        else
            Int64.MaxValue, Int32.MaxValue

    let uniqueId, deviceMask =
        if apiVersion >= Version(1,1,0) || hasInstanceExtension KHRGetPhysicalDeviceProperties2.Name then
            let id =
                KHRExternalMemoryCapabilities.VkPhysicalDeviceIDPropertiesKHR(
                    Guid.Empty,
                    Guid.Empty,
                    byte_8 (),
                    0u,
                    0u
                )
            id |> NativePtr.pin (fun pId ->
                let khrProps =
                    VkPhysicalDeviceProperties2KHR(
                        pId.Address,
                        VkPhysicalDeviceProperties()
                    )
                khrProps |> NativePtr.pin (fun pProps ->
                    VkRaw.vkGetPhysicalDeviceProperties2(handle, pProps)
                    let id = pId.[0]
                    let uid = sprintf "{ GUID = %A; Mask = %d }" id.deviceUUID id.deviceNodeMask
                    uid, id.deviceNodeMask
                )
            )
        else
            let uid = Guid.NewGuid() |> string
            let mask = 1u
            uid, mask


    let limits = DeviceLimits.create (Mem maxAllocationSize) raytracingProperties properties.limits
    let vendor = PCI.vendorName (int properties.vendorID)




    let queueFamilyInfos =
        native {
            let! pCount = 0u
            VkRaw.vkGetPhysicalDeviceQueueFamilyProperties(handle, pCount, NativePtr.zero)

            let props = Array.zeroCreate (int !!pCount)
            let! ptr = props
            VkRaw.vkGetPhysicalDeviceQueueFamilyProperties(handle, pCount, ptr)

            return props |> Array.mapi (fun i p ->
                {
                    index                       = i
                    count                       = int p.queueCount
                    flags                       = unbox (int p.queueFlags)
                    minImgTransferGranularity   = V3i.OfExtent p.minImageTransferGranularity
                    timestampBits               = int p.timestampValidBits
                }
            )
        }

    let mutable memoryProperties =
        native {
            let! pProps = VkPhysicalDeviceMemoryProperties()
            VkRaw.vkGetPhysicalDeviceMemoryProperties(handle, pProps)
            return !!pProps
        }

    let heaps =
        Array.init (int memoryProperties.memoryHeapCount) (fun i ->
            let info = memoryProperties.memoryHeaps.[i]
            MemoryHeapInfo(i, int64 info.size, unbox (int info.flags))
        )

    let memoryTypes =
        Array.init (int memoryProperties.memoryTypeCount) (fun i ->
            let info = memoryProperties.memoryTypes.[i]
            { MemoryInfo.index = i; MemoryInfo.heap = heaps.[int info.heapIndex]; MemoryInfo.flags = unbox (int info.propertyFlags) }
        )

    let formatProperties =
        Dictionary.ofList [
            for fmt in allFormats do
                let props =
                    temporary<VkFormatProperties,_> (fun pProps ->
                        VkRaw.vkGetPhysicalDeviceFormatProperties(handle, fmt, pProps)
                        pProps.[0]
                    )
                yield fmt, props
        ]

    let imageFormatProperties = FastConcurrentDict()
    let externalBufferProperties = FastConcurrentDict()

    let hostMemory = memoryTypes |> Array.maxBy MemoryInfo.hostScore
    let deviceMemory = memoryTypes |> Array.maxBy MemoryInfo.deviceScore

    member x.MaxAllocationSize = maxAllocationSize
    member x.MaxPerSetDescriptors = maxPerSetDescriptors

    member x.GetFormatFeatures(tiling : VkImageTiling, fmt : VkFormat) =
        match tiling with
        | VkImageTiling.Linear -> formatProperties.[fmt].linearTilingFeatures
        | _ -> formatProperties.[fmt].optimalTilingFeatures

    member internal x.GetImageProperties(format : VkFormat, typ : VkImageType, tiling : VkImageTiling, usage : VkImageUsageFlags,
                                         flags : VkImageCreateFlags, external : VkExternalMemoryHandleTypeFlags) =
        let key = (format, typ, tiling, usage, flags, external)

        imageFormatProperties.GetOrCreate(key, fun _ ->
            native {
                let! pExternalImageFormatInfo =
                    VkPhysicalDeviceExternalImageFormatInfo external

                let! pImageFormatInfo =
                    let pNext =
                        if external = VkExternalMemoryHandleTypeFlags.None then 0n
                        else pExternalImageFormatInfo.Address

                    VkPhysicalDeviceImageFormatInfo2(pNext, format, typ, tiling, usage, flags)

                let! pExternalImageFormatProperties =
                    VkExternalImageFormatProperties.Empty

                let! pImageFormatProperties =
                    let pNext =
                        if external = VkExternalMemoryHandleTypeFlags.None then 0n
                        else pExternalImageFormatProperties.Address

                    VkImageFormatProperties2(pNext, VkImageFormatProperties.Empty)

                VkRaw.vkGetPhysicalDeviceImageFormatProperties2(x.Handle, pImageFormatInfo, pImageFormatProperties)
                    |> check $"could not query image format properties (format = {format}, type = {typ}, tiling = {tiling}, usage = {usage}, flags = {flags}, external = {external})"

                let imageFormatProperties = (!!pImageFormatProperties).imageFormatProperties
                let externalMemoryProperties = (!!pExternalImageFormatProperties).externalMemoryProperties
                return imageFormatProperties, externalMemoryProperties
            }
        )

    member x.GetImageFormatProperties(format : VkFormat, typ : VkImageType, tiling : VkImageTiling, usage : VkImageUsageFlags, flags : VkImageCreateFlags) =
        fst <| x.GetImageProperties(format, typ, tiling, usage, flags, VkExternalMemoryHandleTypeFlags.None)

    member x.GetImageExportable(format : VkFormat, typ : VkImageType, tiling : VkImageTiling, usage : VkImageUsageFlags, flags : VkImageCreateFlags) =
        let _, externalMemoryProperties = x.GetImageProperties(format, typ, tiling, usage, flags, VkExternalMemoryHandleTypeFlags.OpaqueBit)
        externalMemoryProperties.IsExportable

    member x.GetBufferFormatFeatures(fmt : VkFormat) =
        formatProperties.[fmt].bufferFeatures

    member x.GetExternalBufferProperties(flags : VkBufferCreateFlags, usage : VkBufferUsageFlags) =
        let key = (flags, usage)

        externalBufferProperties.GetOrCreate(key, fun _ ->
            native {
                let! pExternalBufferInfo =
                    VkPhysicalDeviceExternalBufferInfo(
                        flags, usage, VkExternalMemoryHandleTypeFlags.OpaqueBit
                    )

                let! pExternalBufferProperties = VkExternalBufferProperties.Empty
                VkRaw.vkGetPhysicalDeviceExternalBufferProperties(x.Handle, pExternalBufferInfo, pExternalBufferProperties)

                return !!pExternalBufferProperties
            }
        )

    member x.GetBufferExportable(flags : VkBufferCreateFlags, usage : VkBufferUsageFlags) =
        let properties = x.GetExternalBufferProperties(flags, usage)
        properties.externalMemoryProperties.IsExportable

    member x.AvailableLayers = availableLayers
    member x.GlobalExtensions : ExtensionInfo[] = globalExtensions
    member x.QueueFamilies = queueFamilyInfos
    member x.MemoryTypes = memoryTypes
    member x.Heaps = heaps

    member x.Handle = handle
    //member x.Index : int = index
    member x.Vendor = vendor
    member x.Name = name
    member x.FullName = if name.StartsWith(vendor, StringComparison.InvariantCultureIgnoreCase) then name else $"{vendor} {name}"
    member x.Type = properties.deviceType
    member x.APIVersion = apiVersion
    member x.DriverVersion = driverVersion

    member x.HostMemory = hostMemory
    member x.DeviceMemory = deviceMemory

    member internal x.InstanceInterface = instance
    member x.Features : DeviceFeatures = features
    member x.Limits : DeviceLimits = limits

    member internal x.GetFeatures(hasExtension: string -> bool) = queryFeatures hasExtension

    abstract member DeviceMask : uint32
    default x.DeviceMask = deviceMask

    abstract member Id : string
    default x.Id = uniqueId

    override x.ToString() =
        sprintf "{ name = %s; type = %A; api = %A }" name x.Type x.APIVersion