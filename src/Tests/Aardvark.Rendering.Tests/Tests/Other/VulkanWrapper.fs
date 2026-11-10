namespace Aardvark.Rendering.Tests.Other

open Aardvark.Base
open Aardvark.Rendering.Tests
open Aardvark.Rendering.Vulkan
open Aardvark.Rendering.Vulkan.Memory
open Aardvark.Rendering.Vulkan.Vulkan14
open KHRAccelerationStructure
open KHRFragmentShadingRate
open NVClusterAccelerationStructure
open KHRTimelineSemaphore
open KHRGetPhysicalDeviceProperties2
open Expecto
open System
open FSharp.NativeInterop

#nowarn "9"

module VulkanWrapper =

    [<AutoOpen>]
    module Utilities =

        module VkResult =
            let check result =
                if result <> VkResult.Success then
                    failwithf "Vulkan returned %A" result

        module VkInstance =
            let create (version: Version) (extensions: string seq) =
                let mutable appInfo = VkApplicationInfo.Empty
                appInfo.apiVersion <- version.ToVulkan()
                appInfo.pApplicationName <- CStr.malloc "Test"
                appInfo.pEngineName <- CStr.malloc "No Engine"

                let pExtensions = extensions |> Seq.map CStr.malloc |> Seq.toArray
                use ppExtensions = fixed pExtensions

                let mutable createInfo = VkInstanceCreateInfo.Empty
                use pAppInfo = fixed &appInfo
                createInfo.pApplicationInfo <- pAppInfo
                createInfo.enabledExtensionCount <- uint32 pExtensions.Length
                createInfo.ppEnabledExtensionNames <- ppExtensions

                let mutable instance = VkInstance.Zero
                use pCreateInfo = fixed &createInfo
                use pInstance = fixed &instance
                VkRaw.vkCreateInstance(pCreateInfo, NativePtr.zero, pInstance) |> VkResult.check

                pExtensions |> Array.iter CStr.free
                CStr.free appInfo.pEngineName
                CStr.free appInfo.pApplicationName

                instance

            let getPhysicalDevice (instance: VkInstance) =
                let mutable count = 0u
                use pCount = fixed &count
                VkRaw.vkEnumeratePhysicalDevices(instance, pCount, NativePtr.zero) |> VkResult.check

                let physicalDevices = Array.zeroCreate (int count)
                use pPhysicalDevices = fixed physicalDevices
                VkRaw.vkEnumeratePhysicalDevices(instance, pCount, pPhysicalDevices) |> VkResult.check

                pPhysicalDevices.[0]

            let getPhysicalDeviceFeatures2 (physicalDevice: VkPhysicalDevice) =
                let mutable features = VkPhysicalDeviceFeatures2KHR.Empty
                use pFeatures = fixed &features
                VkRaw.vkGetPhysicalDeviceFeatures2KHR(physicalDevice, pFeatures)
                features

        module VkDevice =

            /// Creates a device with one queue of the first queue family, the given extensions
            /// and the feature structs chained in pNext.
            let createWithFeatures (physicalDevice: VkPhysicalDevice) (extensions: string seq) (pNext: nativeint) =
                let pExtensions = extensions |> Seq.map CStr.malloc |> Seq.toArray
                use ppExtensions = fixed pExtensions

                let mutable priority = 1.0f
                use pPriority = fixed &priority
                let mutable queueInfo = VkDeviceQueueCreateInfo.Empty
                queueInfo.queueCount <- 1u
                queueInfo.pQueuePriorities <- pPriority
                use pQueueInfo = fixed &queueInfo

                let mutable features = VkPhysicalDeviceFeatures.Empty
                use pFeatures = fixed &features
                let mutable createInfo = VkDeviceCreateInfo.Empty
                createInfo.pNext <- pNext
                createInfo.queueCreateInfoCount <- 1u
                createInfo.pQueueCreateInfos <- pQueueInfo
                createInfo.enabledExtensionCount <- uint32 <| pExtensions.Length
                createInfo.pEnabledFeatures <- pFeatures
                createInfo.ppEnabledExtensionNames <- ppExtensions

                let mutable device = VkDevice.Zero
                use pCreateInfo = fixed &createInfo
                use pDevice = fixed &device
                VkRaw.vkCreateDevice(physicalDevice, pCreateInfo, NativePtr.zero, pDevice) |> VkResult.check

                pExtensions |> Array.iter CStr.free

                device

            let create (physicalDevice: VkPhysicalDevice) (extensions: string seq) =
                createWithFeatures physicalDevice extensions 0n

            let createCommandPool (device: VkDevice) =
                let mutable pool = VkCommandPool.Null
                let mutable info = VkCommandPoolCreateInfo.Empty
                use pInfo = fixed &info
                use pPool = fixed &pool
                VkRaw.vkCreateCommandPool(device, pInfo, NativePtr.zero, pPool) |> VkResult.check
                pool

            let createTimelineSemaphore (device: VkDevice) (initialValue: uint64) =
                let mutable typeCreateInfo = VkSemaphoreTypeCreateInfoKHR.Empty
                typeCreateInfo.initialValue <- initialValue
                typeCreateInfo.semaphoreType <- VkSemaphoreTypeKHR.Timeline
                use pTypeCreateInfo = fixed &typeCreateInfo

                let mutable createInfo = VkSemaphoreCreateInfo.Empty
                createInfo.pNext <- NativePtr.toNativeInt pTypeCreateInfo

                let mutable semaphore = VkSemaphore.Null
                use pCreateInfo = fixed &createInfo
                use pSemaphore = fixed &semaphore
                VkRaw.vkCreateSemaphore(device, pCreateInfo, NativePtr.zero, pSemaphore) |> VkResult.check
                semaphore

            let getSemaphoreCounterValue (device: VkDevice) (semaphore: VkSemaphore) =
                let mutable value = 0UL
                use pValue = fixed &value
                VkRaw.vkGetSemaphoreCounterValueKHR(device, semaphore, pValue) |> VkResult.check
                value
    module Arrays =

        let uint32_32 () =
            let mutable array = uint32_32()
            for i = 0 to array.Length - 1 do array.[i] <- 42u + uint32 i
            for i = 0 to array.Length - 1 do Expect.equal array.[i] (42u + uint32 i) $"Index {i} mismatch"

        let int32_7 () =
            let mutable array = int32_7()
            for i = 0 to array.Length - 1 do array.[i] <- 42 + i
            for i = 0 to array.Length - 1 do Expect.equal array.[i] (42 + i) $"Index {i} mismatch"

        let byte_32 () =
            let mutable array = byte_32()
            for i = 0 to array.Length - 1 do array.[i] <- 42uy + uint8 i
            for i = 0 to array.Length - 1 do Expect.equal array.[i] (42uy + uint8 i) $"Index {i} mismatch"

        let byte_8 () =
            let mutable array = byte_8()
            for i = 0 to array.Length - 1 do array.[i] <- 42uy + uint8 i
            for i = 0 to array.Length - 1 do Expect.equal array.[i] (42uy + uint8 i) $"Index {i} mismatch"

        let float32_6 () =
            let mutable array = float32_6()
            for i = 0 to array.Length - 1 do array.[i] <- 42.0f + float32 i
            for i = 0 to array.Length - 1 do Expect.equal array.[i] (42.0f + float32 i) $"Index {i} mismatch"

        let VkPhysicalDevice_32 () =
            let mutable array = VkPhysicalDevice_32()
            for i = 0 to array.Length - 1 do array.[i] <- 42n + nativeint i
            for i = 0 to array.Length - 1 do Expect.equal array.[i] (42n + nativeint i) $"Index {i} mismatch"

        let VkDeviceSize_16 () =
            let mutable array = VkDeviceSize_16()
            for i = 0 to array.Length - 1 do array.[i] <- 42UL + uint64 i
            for i = 0 to array.Length - 1 do Expect.equal array.[i] (42UL + uint64 i) $"Index {i} mismatch"

        let VkOffset3D_2 () =
            let mutable array = VkOffset3D_2()
            for i = 0 to array.Length - 1 do array.[i] <- VkOffset3D(1 + i, 2 + i, 3 + i)
            for i = 0 to array.Length - 1 do Expect.equal array.[i] (VkOffset3D(1 + i, 2 + i, 3 + i)) $"Index {i} mismatch"

        let VkMemoryHeap_16 () =
            let mutable array = VkMemoryHeap_16()
            for i = 0 to array.Length - 1 do array.[i] <- VkMemoryHeap(42UL + uint64 i, VkMemoryHeapFlags.DeviceLocalBit + enum i)
            for i = 0 to array.Length - 1 do Expect.equal array.[i] (VkMemoryHeap(42UL + uint64 i, VkMemoryHeapFlags.DeviceLocalBit + enum i)) $"Index {i} mismatch"

        let VkMemoryType_32 () =
            let mutable array = VkMemoryType_32()
            for i = 0 to array.Length - 1 do array.[i] <- VkMemoryType(VkMemoryPropertyFlags.HostVisibleBit + enum i, 42u + uint32 i)
            for i = 0 to array.Length - 1 do Expect.equal array.[i] (VkMemoryType(VkMemoryPropertyFlags.HostVisibleBit + enum i, 42u + uint32 i)) $"Index {i} mismatch"

        let VkQueueGlobalPriority_16 () =
            let mutable array = VkQueueGlobalPriority_16()
            for i = 0 to array.Length - 1 do array.[i] <- VkQueueGlobalPriority.Medium + enum i
            for i = 0 to array.Length - 1 do Expect.equal array.[i] (VkQueueGlobalPriority.Medium + enum i) $"Index {i} mismatch"

        let VkFragmentShadingRateCombinerOpKHR_2 () =
            let mutable array = VkFragmentShadingRateCombinerOpKHR_2()
            for i = 0 to array.Length - 1 do array.[i] <- VkFragmentShadingRateCombinerOpKHR.Mul + enum i
            for i = 0 to array.Length - 1 do Expect.equal array.[i] (VkFragmentShadingRateCombinerOpKHR.Mul + enum i) $"Index {i} mismatch"

        let VmaDetailedStatistics_16 () =
            let mutable array = VmaDetailedStatistics_16()
            let value = { VmaDetailedStatistics.Empty with statistics = { VmaStatistics.Empty with blockBytes = 42UL } }
            for i = 0 to array.Length - 1 do array.[i] <- value
            for i = 0 to array.Length - 1 do Expect.equal array.[i] value $"Index {i} mismatch"

        let VmaDetailedStatistics_32 () =
            let mutable array = VmaDetailedStatistics_32()
            let value = { VmaDetailedStatistics.Empty with allocationSizeMax = 42UL }
            for i = 0 to array.Length - 1 do array.[i] <- value
            for i = 0 to array.Length - 1 do Expect.equal array.[i] value $"Index {i} mismatch"

    module Bitfields =

        let VkAccelerationStructureInstanceKHR () =
            let index = 1234u
            let mask = 3u
            let sbtOffset = 4310u
            let flags = VkGeometryInstanceFlagsKHR.TriangleFacingCullDisableBit ||| VkGeometryInstanceFlagsKHR.ForceOpaqueBit
            let mutable inst = VkAccelerationStructureInstanceKHR(VkTransformMatrixKHR.Empty, index, mask, sbtOffset, flags, 0UL)

            Expect.equal inst.instanceCustomIndex index "bad index after constructor"
            Expect.equal inst.mask mask "bad mask after constructor"
            Expect.equal inst.instanceShaderBindingTableRecordOffset sbtOffset "bad offset after constructor"
            Expect.equal inst.flags flags "bad offset after constructor"

            let index = 683u
            let mask = 7u
            let sbtOffset = 43110u
            let flags = VkGeometryInstanceFlagsKHR.TriangleFacingCullDisableBit ||| VkGeometryInstanceFlagsKHR.ForceNoOpaqueBit
            inst.instanceCustomIndex <- index
            inst.mask <- mask
            inst.instanceShaderBindingTableRecordOffset <- sbtOffset
            inst.flags <- flags

            Expect.equal inst.instanceCustomIndex index "bad index after setter"
            Expect.equal inst.mask mask "bad mask after setter"
            Expect.equal inst.instanceShaderBindingTableRecordOffset sbtOffset "bad offset after setter"
            Expect.equal inst.flags flags "bad offset after setter"

        let VkClusterAccelerationStructureBuildTriangleClusterInfoNV () =
            let triangleCount = 324u
            let vertexCount = 413u
            let positionTruncateBitCount = 13u
            let indexType = 14u
            let opacityMicromapIndexType = 9u

            let mutable inst =
                VkClusterAccelerationStructureBuildTriangleClusterInfoNV(
                    0u, VkClusterAccelerationStructureClusterFlagsNV.None,
                    triangleCount, vertexCount, positionTruncateBitCount, indexType, opacityMicromapIndexType,
                    VkClusterAccelerationStructureGeometryIndexAndGeometryFlagsNV.Empty,
                    0us, 0us, 0us, 0us, 0UL, 0UL, 0UL, 0UL, 0UL
                )

            Expect.equal inst.triangleCount triangleCount "bad triangle count after constructor"
            Expect.equal inst.vertexCount vertexCount "bad vertex count after constructor"
            Expect.equal inst.positionTruncateBitCount positionTruncateBitCount "bad position truncate bit count after constructor"
            Expect.equal inst.indexType indexType "bad index type after constructor"
            Expect.equal inst.opacityMicromapIndexType opacityMicromapIndexType "bad index type after constructor"

            let triangleCount = 421u
            let vertexCount = 103u
            let positionTruncateBitCount = 03u
            let indexType = 04u
            let opacityMicromapIndexType = 7u
            inst.triangleCount <- triangleCount
            inst.vertexCount <- vertexCount
            inst.positionTruncateBitCount <- positionTruncateBitCount
            inst.indexType <- indexType
            inst.opacityMicromapIndexType <- opacityMicromapIndexType

            Expect.equal inst.triangleCount triangleCount "bad triangle count after setter"
            Expect.equal inst.vertexCount vertexCount "bad vertex count after setter"
            Expect.equal inst.positionTruncateBitCount positionTruncateBitCount "bad position truncate bit count after setter"
            Expect.equal inst.indexType indexType "bad index type after setter"
            Expect.equal inst.opacityMicromapIndexType opacityMicromapIndexType "bad index type after setter"

    let tests (target: TestTarget) =
        [
            "Arrays.uint32_32",                            Arrays.uint32_32
            "Arrays.int32_7",                              Arrays.int32_7
            "Arrays.byte_32",                              Arrays.byte_32
            "Arrays.byte_8",                               Arrays.byte_8
            "Arrays.float32_6",                            Arrays.float32_6
            "Arrays.VkPhysicalDevice_32",                  Arrays.VkPhysicalDevice_32
            "Arrays.VkDeviceSize_16",                      Arrays.VkDeviceSize_16
            "Arrays.VkOffset3D_2",                         Arrays.VkOffset3D_2
            "Arrays.VkMemoryHeap_16",                      Arrays.VkMemoryHeap_16
            "Arrays.VkMemoryType_32",                      Arrays.VkMemoryType_32
            "Arrays.VkQueueGlobalPriority_16",             Arrays.VkQueueGlobalPriority_16
            "Arrays.VkFragmentShadingRateCombinerOpKHR_2", Arrays.VkFragmentShadingRateCombinerOpKHR_2
            "Arrays.VmaDetailedStatistics_16",             Arrays.VmaDetailedStatistics_16
            "Arrays.VmaDetailedStatistics_32",             Arrays.VmaDetailedStatistics_32

            "Bitfields.VkAccelerationStructureInstanceKHR",                       Bitfields.VkAccelerationStructureInstanceKHR
            "Bitfields.VkClusterAccelerationStructureBuildTriangleClusterInfoNV", Bitfields.VkClusterAccelerationStructureBuildTriangleClusterInfoNV
        ]
        |> prepareCasesCpu "VulkanWrapper" target
