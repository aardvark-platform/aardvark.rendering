namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open System
open System.Collections.Concurrent
open System.Runtime.InteropServices
open Vulkan11
open KHRRayTracingPipeline
open KHRRayTracingPositionFetch
open KHRRayQuery
open KHRAccelerationStructure
open KHRBufferDeviceAddress
open KHRShaderFloat16Int8
open KHR8bitStorage
open KHRExternalMemoryCapabilities
open KHRPortabilitySubset
open EXTOpacityMicromap
open EXTCustomBorderColor
open EXTDescriptorIndexing
open EXTMemoryPriority
open EXTDeviceFault
open NVRayTracingInvocationReorder
open NVRayTracingValidation

type IVulkanInstance = interface end

type DeviceProperties =
    {
        Name          : string
        Vendor        : string
        Type          : VkPhysicalDeviceType
        APIVersion    : Version
        DriverVersion : Version
        UniqueId      : string
        NodeMask      : uint32
        Limits        : DeviceLimits
    }

    member inline this.FullName =
        if this.Name.StartsWith(this.Vendor, StringComparison.InvariantCultureIgnoreCase) then
            this.Name
        else
            $"{this.Vendor} {this.Name}"

type PhysicalDevice internal(instance: IVulkanInstance, handle: VkPhysicalDevice) =
    let availableExtensions =
        native {
            let! pCount = 0u
            VkRaw.vkEnumerateDeviceExtensionProperties(handle, null, pCount, NativePtr.zero)
                |> check "could not get device-extensions"

            let props = Array.zeroCreate (int !!pCount)
            let! ptr = props
            VkRaw.vkEnumerateDeviceExtensionProperties(handle, null, pCount, ptr)
                |> check "could not get device-layers"

            return props |> Array.map (fun p ->
                let info = ExtensionInfo.ofVulkan p
                info.name, info
            )
            |> Map.ofArray
        }

    let hasExtension = flip Map.containsKey availableExtensions

    let queryFeatures (hasExtension: string -> bool) =
        let f, pm, memp, ycbcr, cbc, s8, s16, f16i8, vp, sdp, idx, psub, rtp, rtpos, rtir, rtv, acc, omm, rq, bda, dflt =
            use chain = new VkStructChain()
            let pMem        = chain.Add<VkPhysicalDeviceProtectedMemoryFeatures>()
            let pMemPrior   = chain.Add<VkPhysicalDeviceMemoryPriorityFeaturesEXT>             (hasExtension EXTMemoryPriority.Name)
            let pYcbcr      = chain.Add<VkPhysicalDeviceSamplerYcbcrConversionFeatures>()
            let pCbc        = chain.Add<VkPhysicalDeviceCustomBorderColorFeaturesEXT>          (hasExtension EXTCustomBorderColor.Name)
            let p8bit       = chain.Add<VkPhysicalDevice8BitStorageFeaturesKHR>                (hasExtension KHR8bitStorage.Name)
            let p16bit      = chain.Add<VkPhysicalDevice16BitStorageFeatures>()
            let pf16i8      = chain.Add<VkPhysicalDeviceFloat16Int8FeaturesKHR>                (hasExtension KHRShaderFloat16Int8.Name)
            let pVarPtrs    = chain.Add<VkPhysicalDeviceVariablePointersFeatures>()
            let pDrawParams = chain.Add<VkPhysicalDeviceShaderDrawParametersFeatures>()
            let pIdx        = chain.Add<VkPhysicalDeviceDescriptorIndexingFeaturesEXT>         (hasExtension EXTDescriptorIndexing.Name)
            let pPSub       = chain.Add<VkPhysicalDevicePortabilitySubsetFeaturesKHR>          (hasExtension KHRPortabilitySubset.Name)
            let pRTP        = chain.Add<VkPhysicalDeviceRayTracingPipelineFeaturesKHR>         (hasExtension KHRRayTracingPipeline.Name)
            let pRTPos      = chain.Add<VkPhysicalDeviceRayTracingPositionFetchFeaturesKHR>    (hasExtension KHRRayTracingPositionFetch.Name)
            let pRTIR       = chain.Add<VkPhysicalDeviceRayTracingInvocationReorderFeaturesNV> (hasExtension NVRayTracingInvocationReorder.Name)
            let pRTV        = chain.Add<VkPhysicalDeviceRayTracingValidationFeaturesNV>        (hasExtension NVRayTracingValidation.Name)
            let pAcc        = chain.Add<VkPhysicalDeviceAccelerationStructureFeaturesKHR>      (hasExtension KHRAccelerationStructure.Name)
            let pOmm        = chain.Add<VkPhysicalDeviceOpacityMicromapFeaturesEXT>            (hasExtension EXTOpacityMicromap.Name)
            let pRQ         = chain.Add<VkPhysicalDeviceRayQueryFeaturesKHR>                   (hasExtension KHRRayQuery.Name)
            let pDevAddr    = chain.Add<VkPhysicalDeviceBufferDeviceAddressFeaturesKHR>        (hasExtension KHRBufferDeviceAddress.Name)
            let pDevFault   = chain.Add<VkPhysicalDeviceFaultFeaturesEXT>                      (hasExtension EXTDeviceFault.Name)
            let pFeatures   = chain.Add<VkPhysicalDeviceFeatures2>()

            VkRaw.vkGetPhysicalDeviceFeatures2(handle, VkStructChain.toNativePtr chain)

            let psub =
                // If the VK_KHR_portability_subset extension is not supported we have a
                // fully conformant implementation (e.g., all the features are supported)
                if NativePtr.isNull pPSub then
                    VkPhysicalDevicePortabilitySubsetFeaturesKHR(
                        VkTrue, VkTrue, VkTrue, VkTrue, VkTrue,
                        VkTrue, VkTrue, VkTrue, VkTrue, VkTrue,
                        VkTrue, VkTrue, VkTrue, VkTrue, VkTrue
                    )
                else
                    !!pPSub

            (!!pFeatures).features, !!pMem, NativePtr.readOrEmpty pMemPrior, !!pYcbcr, NativePtr.readOrEmpty pCbc,
            NativePtr.readOrEmpty p8bit, !!p16bit, NativePtr.readOrEmpty pf16i8,
            !!pVarPtrs, !!pDrawParams, NativePtr.readOrEmpty pIdx, psub, NativePtr.readOrEmpty pRTP, NativePtr.readOrEmpty pRTPos,
            NativePtr.readOrEmpty pRTIR, NativePtr.readOrEmpty pRTV, NativePtr.readOrEmpty pAcc, NativePtr.readOrEmpty pOmm, NativePtr.readOrEmpty pRQ,
            NativePtr.readOrEmpty pDevAddr, NativePtr.readOrEmpty pDevFault

        f |> DeviceFeatures.create pm memp ycbcr cbc s8 s16 f16i8 vp sdp idx psub rtp rtpos rtir rtv acc omm rq bda dflt

    let availableFeatures =
        queryFeatures hasExtension

    let properties =
        let properties, main, devId, psub, cbc, rtp, rtir, acc, omm =
            use chain = new VkStructChain()
            let pMain       = chain.Add<VkPhysicalDeviceMaintenance3Properties>()
            let pDevId      = chain.Add<VkPhysicalDeviceIDPropertiesKHR>()
            let pPSub       = chain.Add<VkPhysicalDevicePortabilitySubsetPropertiesKHR>          (hasExtension KHRPortabilitySubset.Name)
            let pCbc        = chain.Add<VkPhysicalDeviceCustomBorderColorPropertiesEXT>          (hasExtension EXTCustomBorderColor.Name)
            let pRTP        = chain.Add<VkPhysicalDeviceRayTracingPipelinePropertiesKHR>         (hasExtension KHRRayTracingPipeline.Name)
            let pRTIR       = chain.Add<VkPhysicalDeviceRayTracingInvocationReorderPropertiesNV> (hasExtension NVRayTracingInvocationReorder.Name)
            let pAcc        = chain.Add<VkPhysicalDeviceAccelerationStructurePropertiesKHR>      (hasExtension KHRAccelerationStructure.Name)
            let pOmm        = chain.Add<VkPhysicalDeviceOpacityMicromapPropertiesEXT>            (hasExtension EXTOpacityMicromap.Name)
            let pProperties = chain.Add<VkPhysicalDeviceProperties2>()

            VkRaw.vkGetPhysicalDeviceProperties2(handle, VkStructChain.toNativePtr chain)

            (!!pProperties).properties, !!pMain, !!pDevId,
            NativePtr.readOrEmpty pPSub,
            NativePtr.readOrEmpty pCbc, NativePtr.readOrEmpty pRTP, NativePtr.readOrEmpty pRTIR,
            NativePtr.readOrEmpty pAcc, NativePtr.readOrEmpty pOmm

        {
            Name          = properties.deviceName.Value
            Vendor        = PCI.vendorName <| int properties.vendorID
            Type          = properties.deviceType
            APIVersion    = Version.FromVulkan properties.apiVersion
            DriverVersion = Version.FromVulkan properties.driverVersion
            UniqueId      = sprintf "{ GUID = %A; Mask = %d }" devId.deviceUUID devId.deviceNodeMask
            NodeMask      = if devId.deviceLUIDValid = VkTrue then devId.deviceNodeMask else 1u
            Limits        = properties.limits |> DeviceLimits.create main psub cbc rtp rtir acc omm
        }

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

    let memoryHeaps =
        Array.init (int memoryProperties.memoryHeapCount) (fun i ->
            let info = memoryProperties.memoryHeaps.[i]
            MemoryHeapInfo(i, int64 info.size, unbox (int info.flags))
        )

    let memoryTypes =
        Array.init (int memoryProperties.memoryTypeCount) (fun i ->
            let info = memoryProperties.memoryTypes.[i]
            { MemoryInfo.index = i; MemoryInfo.heap = memoryHeaps.[int info.heapIndex]; MemoryInfo.flags = unbox (int info.propertyFlags) }
        )

    let formatProperties = ConcurrentDictionary<_, VkFormatProperties>()
    let imageFormatProperties = ConcurrentDictionary<_, VkImageFormatProperties * VkExternalMemoryProperties>()
    let externalBufferProperties = ConcurrentDictionary<_, VkExternalBufferProperties>()

    member x.GetFormatProperties(format: VkFormat) =
        formatProperties.GetOrAdd(format, fun format ->
            NativePtr.temp (fun pProperties ->
                VkRaw.vkGetPhysicalDeviceFormatProperties(handle, format, pProperties)
                !!pProperties
            )
        )

    member x.GetImageFormatFeatures(format: VkFormat, [<Optional; DefaultParameterValue(VkImageTiling.Optimal)>] tiling: VkImageTiling) =
        let properties = x.GetFormatProperties(format)
        if tiling = VkImageTiling.Optimal then properties.optimalTilingFeatures else properties.linearTilingFeatures

    [<Obsolete("Use GetImageFormatFeatures instead.")>]
    member x.GetFormatFeatures(tiling : VkImageTiling, fmt : VkFormat) =
        x.GetImageFormatFeatures(fmt, tiling)

    member internal x.GetImageProperties(format : VkFormat, typ : VkImageType, tiling : VkImageTiling, usage : VkImageUsageFlags,
                                         flags : VkImageCreateFlags, external : VkExternalMemoryHandleTypeFlags) =
        let key = (format, typ, tiling, usage, flags, external)

        imageFormatProperties.GetOrAdd(key, fun _ ->
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

    member x.GetBufferFormatFeatures(format: VkFormat) =
        let properties = x.GetFormatProperties(format)
        properties.bufferFeatures

    member x.GetExternalBufferProperties(flags : VkBufferCreateFlags, usage : VkBufferUsageFlags) =
        let key = (flags, usage)

        externalBufferProperties.GetOrAdd(key, fun _ ->
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

    member x.AvailableExtensions = availableExtensions
    member x.HasExtension name = hasExtension name
    member x.QueueFamilies = queueFamilyInfos
    member x.MemoryTypes = memoryTypes
    member x.MemoryHeaps = memoryHeaps

    member x.Handle = handle
    member x.Properties : DeviceProperties = properties
    member x.Vendor : string = properties.Vendor
    member x.Name : string = properties.Name
    member x.FullName : string = properties.FullName
    member x.Type = properties.Type
    member x.APIVersion : Version = properties.APIVersion
    member x.DriverVersion : Version = properties.DriverVersion

    member internal x.InstanceInterface = instance
    member x.Features : DeviceFeatures = availableFeatures
    member x.Limits : DeviceLimits = properties.Limits

    member internal x.GetFeatures(hasExtension: string -> bool) = queryFeatures hasExtension

    abstract member DeviceMask : uint32
    default x.DeviceMask = properties.NodeMask

    abstract member Id : string
    default x.Id = properties.UniqueId

    override x.ToString() =
        sprintf "{ name = %s; type = %A; api = %A }" x.FullName x.Type x.APIVersion