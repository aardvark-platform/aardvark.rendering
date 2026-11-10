namespace Aardvark.Rendering.Tests.Other

open Aardvark.Base
open Aardvark.Rendering.Vulkan
open BenchmarkDotNet.Attributes
open FSharp.NativeInterop
open KHRTimelineSemaphore
open EXTSampleLocations
open Vulkan11
open Aardvark.Rendering.Tests.Other.VulkanWrapper

#nowarn "9"

// Before wrapper rework (Core functions via P/Invoke, extensions via Marshal.GetDelegateForPointer)
// | Method                    | Mean          | Error        | StdDev       | Allocated |
// |-------------------------- |--------------:|-------------:|-------------:|----------:|
// | GlobalFunction            | 358,581.88 ns | 3,450.830 ns | 3,227.909 ns |         - |
// | DeviceFunction            |     166.40 ns |     1.028 ns |     0.859 ns |         - |
// | ExtensionInstanceFunction |      13.57 ns |     0.091 ns |     0.085 ns |         - |
// | ExtensionDeviceFunction   |   2,236.94 ns |    33.535 ns |    29.728 ns |         - |

// After wrapper rework (Marshal.GetDelegateForPointer)
// | Method                    | Mean          | Error        | StdDev       | Allocated |
// |-------------------------- |--------------:|-------------:|-------------:|----------:|
// | GlobalFunction            | 348,839.81 ns | 2,520.348 ns | 2,357.536 ns |         - |
// | DeviceFunction            |     202.57 ns |     0.780 ns |     0.730 ns |         - |
// | ExtensionInstanceFunction |      10.29 ns |     0.030 ns |     0.023 ns |         - |
// | ExtensionDeviceFunction   |   2,369.75 ns |    19.246 ns |    16.071 ns |         - |

[<MemoryDiagnoser>]
type VulkanBench() =
    let mutable instance = VkInstance.Zero
    let mutable device = VkDevice.Zero
    let mutable physicalDevice = VkPhysicalDevice.Zero
    let version = System.Version(1, 1, 0)

    [<GlobalSetup>]
    member _.Setup() =
        instance <- VkInstance.create version []
        physicalDevice <- VkInstance.getPhysicalDevice instance
        let mutable timelineFeatures = VkPhysicalDeviceTimelineSemaphoreFeaturesKHR.Empty
        timelineFeatures.timelineSemaphore <- 1u
        use pTimelineFeatures = fixed &timelineFeatures
        device <- VkDevice.createWithFeatures physicalDevice [ KHRTimelineSemaphore.Name; EXTSampleLocations.Name ] (NativePtr.toNativeInt pTimelineFeatures)

    [<GlobalCleanup>]
    member _.Cleanup() =
        VkRaw.vkDestroyDevice(device, NativePtr.zero)
        device <- VkDevice.Zero
        VkRaw.vkDestroyInstance(instance, NativePtr.zero)
        instance <- VkInstance.Zero

    [<Benchmark>]
    member _.GlobalFunction() =
        let mutable version = 0u
        use pVersion = fixed &version
        VkRaw.vkEnumerateInstanceVersion(pVersion) |> VkResult.check
        version

    [<Benchmark>]
    member _.DeviceFunction() =
        let mutable pool = VkCommandPool.Null
        let mutable info = VkCommandPoolCreateInfo.Empty
        use pInfo = fixed &info
        use pPool = fixed &pool
        VkRaw.vkCreateCommandPool(device, pInfo, NativePtr.zero, pPool) |> VkResult.check
        VkRaw.vkDestroyCommandPool(device, pool, NativePtr.zero)
        pool.Handle

    [<Benchmark>]
    member _.ExtensionInstanceFunction() =
        let mutable properties = VkMultisamplePropertiesEXT.Empty
        use pProperties = fixed &properties
        VkRaw.vkGetPhysicalDeviceMultisamplePropertiesEXT(physicalDevice, VkSampleCountFlags.D4Bit, pProperties)
        properties.maxSampleLocationGridSize.width

    [<Benchmark>]
    member _.ExtensionDeviceFunction() =
        let mutable typeCreateInfo = VkSemaphoreTypeCreateInfoKHR.Empty
        typeCreateInfo.initialValue <- 42UL
        typeCreateInfo.semaphoreType <- VkSemaphoreTypeKHR.Timeline

        let mutable createInfo = VkSemaphoreCreateInfo.Empty
        use pTypeCreateInfo = fixed &typeCreateInfo
        createInfo.pNext <- NativePtr.toNativeInt pTypeCreateInfo

        let mutable semaphore = VkSemaphore.Null
        use pCreateInfo = fixed &createInfo
        use pSemaphore = fixed &semaphore
        VkRaw.vkCreateSemaphore(device, pCreateInfo, NativePtr.zero, pSemaphore) |> VkResult.check

        let mutable value = 0UL
        use pValue = fixed &value
        VkRaw.vkGetSemaphoreCounterValueKHR(device, semaphore, pValue) |> VkResult.check

        VkRaw.vkDestroySemaphore(device, semaphore, NativePtr.zero)

        value