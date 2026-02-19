namespace Aardvark.Rendering.Tests

open Aardvark.Base
open Aardvark.Rendering.Vulkan
open BenchmarkDotNet.Attributes
open FSharp.NativeInterop
open KHRTimelineSemaphore
open EXTSampleLocations
open Vulkan11
open ``Vulkan Wrapper Tests``

#nowarn "9"
#nowarn "51"

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
        device <- VkDevice.create physicalDevice [ KHRTimelineSemaphore.Name; EXTSampleLocations.Name ]

    [<GlobalCleanup>]
    member _.Cleanup() =
        VkRaw.vkDestroyDevice(device, NativePtr.zero)
        device <- VkDevice.Zero
        VkRaw.vkDestroyInstance(instance, NativePtr.zero)
        instance <- VkInstance.Zero

    [<Benchmark>]
    member _.GlobalFunction() =
        let mutable version = 0u
        VkRaw.vkEnumerateInstanceVersion(&&version) |> VkResult.check
        version

    [<Benchmark>]
    member _.DeviceFunction() =
        let mutable pool = VkCommandPool.Null
        let mutable info = VkCommandPoolCreateInfo.Empty
        VkRaw.vkCreateCommandPool(device, &&info, NativePtr.zero, &&pool) |> VkResult.check
        VkRaw.vkDestroyCommandPool(device, pool, NativePtr.zero)
        pool.Handle

    [<Benchmark>]
    member _.ExtensionInstanceFunction() =
        let mutable properties = VkMultisamplePropertiesEXT.Empty
        VkRaw.vkGetPhysicalDeviceMultisamplePropertiesEXT(physicalDevice, VkSampleCountFlags.D4Bit, &&properties)
        properties.maxSampleLocationGridSize.width

    [<Benchmark>]
    member _.ExtensionDeviceFunction() =
        let mutable typeCreateInfo = VkSemaphoreTypeCreateInfoKHR.Empty
        typeCreateInfo.initialValue <- 42UL
        typeCreateInfo.semaphoreType <- VkSemaphoreTypeKHR.Timeline

        let mutable createInfo = VkSemaphoreCreateInfo.Empty
        createInfo.pNext <- NativePtr.toNativeInt &&typeCreateInfo

        let mutable semaphore = VkSemaphore.Null
        VkRaw.vkCreateSemaphore(device, &&createInfo, NativePtr.zero, &&semaphore) |> VkResult.check

        let mutable value = 0UL
        VkRaw.vkGetSemaphoreCounterValueKHR(device, semaphore, &&value) |> VkResult.check

        VkRaw.vkDestroySemaphore(device, semaphore, NativePtr.zero)

        value