namespace Aardvark.Rendering.Tests.Other

open Aardvark.Base
open Aardvark.Rendering.Tests
open Aardvark.Rendering.Tests.Other.VulkanWrapper
open Aardvark.Rendering.Vulkan
open KHRTimelineSemaphore
open KHRGetPhysicalDeviceProperties2
open Expecto
open System
open FSharp.NativeInterop

#nowarn "9"
#nowarn "51"

// Tests for the loading of Vulkan entry points. They assert on the loader's state and
// must therefore run without a test application.
module VulkanEntryPoints =

    module private Cases =

        let private version = Version(1, 0, 0)

        let private init() =
            VkRaw.EntryPoints.ReportEntryPointAddresses <- true
            { new IDisposable with
                member _.Dispose() =
                    Expect.equal VkRaw.EntryPoints.LoadedInstance 0n "unexpected LoadedInstance"
                    Expect.equal VkRaw.EntryPoints.LoadedInstanceVersion 0u "unexpected LoadedInstanceVersion"
                    Expect.isEmpty VkRaw.EntryPoints.LoadedDevices "unexpected LoadedDevices"
            }

        let loadGlobal () =
            use _ = init()
            let result = VkRaw.vkGetInstanceProcAddr(VkInstance.Zero, "blub")
            Expect.isTrue VkRaw.EntryPoints.LoadedGlobal "unexpected LoadedGlobal"
            Expect.equal VkRaw.EntryPoints.LoadedInstance 0n "unexpected LoadedInstance"
            Expect.equal VkRaw.EntryPoints.LoadedInstanceVersion 0u "unexpected LoadedInstanceVersion"
            Expect.isEmpty VkRaw.EntryPoints.LoadedDevices "unexpected LoadedDevices"
            Expect.equal result 0n "unexpected entry point"

        let loadInstance () =
            use _ = init()

            for _ = 1 to 2 do
                let instance = VkInstance.create version []

                Expect.isTrue VkRaw.EntryPoints.LoadedGlobal "unexpected LoadedGlobal"
                Expect.equal VkRaw.EntryPoints.LoadedInstance instance "unexpected LoadedInstance"
                Expect.equal VkRaw.EntryPoints.LoadedInstanceVersion (version.ToVulkan()) "unexpected LoadedInstanceVersion"
                Expect.isEmpty VkRaw.EntryPoints.LoadedDevices "unexpected LoadedDevices"

                VkInstance.getPhysicalDevice instance |> ignore
                VkRaw.vkDestroyInstance(instance, NativePtr.zero)

                Expect.isTrue VkRaw.EntryPoints.LoadedGlobal "unexpected LoadedGlobal"
                Expect.equal VkRaw.EntryPoints.LoadedInstance 0n "unexpected LoadedInstance"
                Expect.equal VkRaw.EntryPoints.LoadedInstanceVersion 0u "unexpected LoadedInstanceVersion"
                Expect.isEmpty VkRaw.EntryPoints.LoadedDevices "unexpected LoadedDevices"

        let loadInstanceExtension () =
            use _ = init()

            for _ = 1 to 2 do
                let instance = VkInstance.create version [ KHRGetPhysicalDeviceProperties2.Name ]
                let physicalDevice = VkInstance.getPhysicalDevice instance

                Expect.isTrue VkRaw.EntryPoints.LoadedGlobal "unexpected LoadedGlobal"
                Expect.equal VkRaw.EntryPoints.LoadedInstance instance "unexpected LoadedInstance"
                Expect.equal VkRaw.EntryPoints.LoadedInstanceVersion (version.ToVulkan()) "unexpected LoadedInstanceVersion"
                Expect.isEmpty VkRaw.EntryPoints.LoadedDevices "unexpected LoadedDevices"

                let mutable features = VkPhysicalDeviceFeatures2KHR.Empty
                VkRaw.vkGetPhysicalDeviceFeatures2KHR(physicalDevice, &&features)

                VkRaw.vkDestroyInstance(instance, NativePtr.zero)

                Expect.isTrue VkRaw.EntryPoints.LoadedGlobal "unexpected LoadedGlobal"
                Expect.equal VkRaw.EntryPoints.LoadedInstance 0n "unexpected LoadedInstance"
                Expect.equal VkRaw.EntryPoints.LoadedInstanceVersion 0u "unexpected LoadedInstanceVersion"
                Expect.isEmpty VkRaw.EntryPoints.LoadedDevices "unexpected LoadedDevices"

            Expect.isTrue VkRaw.EntryPoints.LoadedGlobal "unexpected LoadedGlobal"
            Expect.equal VkRaw.EntryPoints.LoadedInstance 0n "unexpected LoadedInstance"
            Expect.equal VkRaw.EntryPoints.LoadedInstanceVersion 0u "unexpected LoadedInstanceVersion"
            Expect.isEmpty VkRaw.EntryPoints.LoadedDevices "unexpected LoadedDevices"

        let loadDevice () =
            use _ = init()

            let instance = VkInstance.create version []
            let physicalDevice = VkInstance.getPhysicalDevice instance

            for _ = 1 to 2 do
                let device = VkDevice.create physicalDevice []

                Expect.isTrue VkRaw.EntryPoints.LoadedGlobal "unexpected LoadedGlobal"
                Expect.equal VkRaw.EntryPoints.LoadedInstance instance "unexpected LoadedInstance"
                Expect.equal VkRaw.EntryPoints.LoadedInstanceVersion (version.ToVulkan()) "unexpected LoadedInstanceVersion"
                Expect.sequenceEqual VkRaw.EntryPoints.LoadedDevices [ device ] "unexpected LoadedDevices"
                Expect.equal (VkRaw.EntryPoints.GetDeviceVersion device) (version.ToVulkan()) "unexpected GetDeviceVersion"

                let mutable pool = VkCommandPool.Null
                let mutable info = VkCommandPoolCreateInfo.Empty
                VkRaw.vkCreateCommandPool(device, &&info, NativePtr.zero, &&pool) |> VkResult.check
                VkRaw.vkDestroyCommandPool(device, pool, NativePtr.zero)

                VkRaw.vkDestroyDevice(device, NativePtr.zero)

                Expect.isTrue VkRaw.EntryPoints.LoadedGlobal "unexpected LoadedGlobal"
                Expect.equal VkRaw.EntryPoints.LoadedInstance instance "unexpected LoadedInstance"
                Expect.equal VkRaw.EntryPoints.LoadedInstanceVersion (version.ToVulkan()) "unexpected LoadedInstanceVersion"
                Expect.isEmpty VkRaw.EntryPoints.LoadedDevices "unexpected LoadedDevices"
                Expect.equal (VkRaw.EntryPoints.GetDeviceVersion device) 0u "unexpected GetDeviceVersion"

            VkRaw.vkDestroyInstance(instance, NativePtr.zero)

            Expect.isTrue VkRaw.EntryPoints.LoadedGlobal "unexpected LoadedGlobal"
            Expect.equal VkRaw.EntryPoints.LoadedInstance 0n "unexpected LoadedInstance"
            Expect.equal VkRaw.EntryPoints.LoadedInstanceVersion 0u "unexpected LoadedInstanceVersion"
            Expect.isEmpty VkRaw.EntryPoints.LoadedDevices "unexpected LoadedDevices"

        let loadDeviceExtension () =
            use _ = init()

            let instance = VkInstance.create version []
            let physicalDevice = VkInstance.getPhysicalDevice instance

            let mutable timelineFeatures = VkPhysicalDeviceTimelineSemaphoreFeaturesKHR.Empty
            timelineFeatures.timelineSemaphore <- 1u

            for _ = 1 to 2 do
                let device = VkDevice.createWithFeatures physicalDevice [ KHRTimelineSemaphore.Name ] (NativePtr.toNativeInt &&timelineFeatures)

                Expect.isTrue VkRaw.EntryPoints.LoadedGlobal "unexpected LoadedGlobal"
                Expect.equal VkRaw.EntryPoints.LoadedInstance instance "unexpected LoadedInstance"
                Expect.equal VkRaw.EntryPoints.LoadedInstanceVersion (version.ToVulkan()) "unexpected LoadedInstanceVersion"
                Expect.sequenceEqual VkRaw.EntryPoints.LoadedDevices [ device ] "unexpected LoadedDevices"
                Expect.equal (VkRaw.EntryPoints.GetDeviceVersion device) (version.ToVulkan()) "unexpected GetDeviceVersion"

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

                VkRaw.vkDestroyDevice(device, NativePtr.zero)

                Expect.isTrue VkRaw.EntryPoints.LoadedGlobal "unexpected LoadedGlobal"
                Expect.equal VkRaw.EntryPoints.LoadedInstance instance "unexpected LoadedInstance"
                Expect.equal VkRaw.EntryPoints.LoadedInstanceVersion (version.ToVulkan()) "unexpected LoadedInstanceVersion"
                Expect.isEmpty VkRaw.EntryPoints.LoadedDevices "unexpected LoadedDevices"
                Expect.equal (VkRaw.EntryPoints.GetDeviceVersion device) 0u "unexpected GetDeviceVersion"

            VkRaw.vkDestroyInstance(instance, NativePtr.zero)

            Expect.isTrue VkRaw.EntryPoints.LoadedGlobal "unexpected LoadedGlobal"
            Expect.equal VkRaw.EntryPoints.LoadedInstance 0n "unexpected LoadedInstance"
            Expect.equal VkRaw.EntryPoints.LoadedInstanceVersion 0u "unexpected LoadedInstanceVersion"
            Expect.isEmpty VkRaw.EntryPoints.LoadedDevices "unexpected LoadedDevices"

        let validation () =
            use _ = init()
            let ver = Version(9, 9, 9).ToVulkan()
            let noext _ = false

            // Device before instance -> fail
            Expect.throwsT<InvalidOperationException> (fun _ ->
                VkRaw.EntryPoints.LoadDevice(1n, ver, noext)
            ) "Invalid LoadDevice did not throw expected exception"

            let instance = VkInstance.create version []

            Expect.isTrue VkRaw.EntryPoints.LoadedGlobal "unexpected LoadedGlobal"
            Expect.equal VkRaw.EntryPoints.LoadedInstance instance "unexpected LoadedInstance"
            Expect.equal VkRaw.EntryPoints.LoadedInstanceVersion (version.ToVulkan()) "unexpected LoadedInstanceVersion"
            Expect.isEmpty VkRaw.EntryPoints.LoadedDevices "unexpected LoadedDevices"

            VkRaw.EntryPoints.LoadInstance(instance, ver, noext) // multiple calls with same instance are allowed

            Expect.isTrue VkRaw.EntryPoints.LoadedGlobal "unexpected LoadedGlobal"
            Expect.equal VkRaw.EntryPoints.LoadedInstance instance "unexpected LoadedInstance"
            Expect.equal VkRaw.EntryPoints.LoadedInstanceVersion ver "unexpected LoadedInstanceVersion"
            Expect.isEmpty VkRaw.EntryPoints.LoadedDevices "unexpected LoadedDevices"

            VkRaw.EntryPoints.UnloadInstance instance

            Expect.isTrue VkRaw.EntryPoints.LoadedGlobal "unexpected LoadedGlobal"
            Expect.equal VkRaw.EntryPoints.LoadedInstance 0n "unexpected LoadedInstance"
            Expect.equal VkRaw.EntryPoints.LoadedInstanceVersion 0u "unexpected LoadedInstanceVersion"
            Expect.isEmpty VkRaw.EntryPoints.LoadedDevices "unexpected LoadedDevices"

            VkRaw.EntryPoints.UnloadInstance instance // no effect but success

            Expect.isTrue VkRaw.EntryPoints.LoadedGlobal "unexpected LoadedGlobal"
            Expect.equal VkRaw.EntryPoints.LoadedInstance 0n "unexpected LoadedInstance"
            Expect.equal VkRaw.EntryPoints.LoadedInstanceVersion 0u "unexpected LoadedInstanceVersion"
            Expect.isEmpty VkRaw.EntryPoints.LoadedDevices "unexpected LoadedDevices"

            VkRaw.EntryPoints.LoadInstance(instance, ver, noext)

            Expect.isTrue VkRaw.EntryPoints.LoadedGlobal "unexpected LoadedGlobal"
            Expect.equal VkRaw.EntryPoints.LoadedInstance instance "unexpected LoadedInstance"
            Expect.equal VkRaw.EntryPoints.LoadedInstanceVersion ver "unexpected LoadedInstanceVersion"
            Expect.isEmpty VkRaw.EntryPoints.LoadedDevices "unexpected LoadedDevices"

            let device = VkDevice.create (VkInstance.getPhysicalDevice instance) []

            Expect.isTrue VkRaw.EntryPoints.LoadedGlobal "unexpected LoadedGlobal"
            Expect.equal VkRaw.EntryPoints.LoadedInstance instance "unexpected LoadedInstance"
            Expect.equal VkRaw.EntryPoints.LoadedInstanceVersion ver "unexpected LoadedInstanceVersion"
            Expect.sequenceEqual VkRaw.EntryPoints.LoadedDevices [ device ] "unexpected LoadedDevices"
            Expect.equal (VkRaw.EntryPoints.GetDeviceVersion device) ver "unexpected GetDeviceVersion"

            VkRaw.EntryPoints.LoadDevice(device, ver, noext) // multiple calls with same device are okay

            Expect.isTrue VkRaw.EntryPoints.LoadedGlobal "unexpected LoadedGlobal"
            Expect.equal VkRaw.EntryPoints.LoadedInstance instance "unexpected LoadedInstance"
            Expect.equal VkRaw.EntryPoints.LoadedInstanceVersion ver "unexpected LoadedInstanceVersion"
            Expect.sequenceEqual VkRaw.EntryPoints.LoadedDevices [ device ] "unexpected LoadedDevices"
            Expect.equal (VkRaw.EntryPoints.GetDeviceVersion device) ver "unexpected GetDeviceVersion"

            VkRaw.EntryPoints.LoadDevice(device + 1n, 1u, noext)

            Expect.isTrue VkRaw.EntryPoints.LoadedGlobal "unexpected LoadedGlobal"
            Expect.equal VkRaw.EntryPoints.LoadedInstance instance "unexpected LoadedInstance"
            Expect.equal VkRaw.EntryPoints.LoadedInstanceVersion ver "unexpected LoadedInstanceVersion"
            Expect.sequenceEqual VkRaw.EntryPoints.LoadedDevices [ device; device + 1n ] "unexpected LoadedDevices"
            Expect.equal (VkRaw.EntryPoints.GetDeviceVersion device) ver "unexpected GetDeviceVersion"
            Expect.equal (VkRaw.EntryPoints.GetDeviceVersion (device + 1n)) 1u "unexpected GetDeviceVersion"

            VkRaw.EntryPoints.LoadDevice(device, ver, noext)

            Expect.isTrue VkRaw.EntryPoints.LoadedGlobal "unexpected LoadedGlobal"
            Expect.equal VkRaw.EntryPoints.LoadedInstance instance "unexpected LoadedInstance"
            Expect.equal VkRaw.EntryPoints.LoadedInstanceVersion ver "unexpected LoadedInstanceVersion"
            Expect.sequenceEqual VkRaw.EntryPoints.LoadedDevices [ device; device + 1n ] "unexpected LoadedDevices"
            Expect.equal (VkRaw.EntryPoints.GetDeviceVersion device) ver "unexpected GetDeviceVersion"
            Expect.equal (VkRaw.EntryPoints.GetDeviceVersion (device + 1n)) 1u "unexpected GetDeviceVersion"

            VkRaw.EntryPoints.UnloadDevice(device + 1n)

            Expect.isTrue VkRaw.EntryPoints.LoadedGlobal "unexpected LoadedGlobal"
            Expect.equal VkRaw.EntryPoints.LoadedInstance instance "unexpected LoadedInstance"
            Expect.equal VkRaw.EntryPoints.LoadedInstanceVersion ver "unexpected LoadedInstanceVersion"
            Expect.sequenceEqual VkRaw.EntryPoints.LoadedDevices [ device ] "unexpected LoadedDevices"
            Expect.equal (VkRaw.EntryPoints.GetDeviceVersion device) ver "unexpected GetDeviceVersion"
            Expect.equal (VkRaw.EntryPoints.GetDeviceVersion (device + 1n)) 0u "unexpected GetDeviceVersion"

            VkRaw.vkDestroyDevice(device, NativePtr.zero)

            Expect.isTrue VkRaw.EntryPoints.LoadedGlobal "unexpected LoadedGlobal"
            Expect.equal VkRaw.EntryPoints.LoadedInstance instance "unexpected LoadedInstance"
            Expect.equal VkRaw.EntryPoints.LoadedInstanceVersion ver "unexpected LoadedInstanceVersion"
            Expect.isEmpty VkRaw.EntryPoints.LoadedDevices "unexpected LoadedDevices"
            Expect.equal (VkRaw.EntryPoints.GetDeviceVersion device) 0u "unexpected GetDeviceVersion"
            Expect.equal (VkRaw.EntryPoints.GetDeviceVersion (device + 1n)) 0u "unexpected GetDeviceVersion"

            VkRaw.vkDestroyInstance(instance, NativePtr.zero)

            Expect.isTrue VkRaw.EntryPoints.LoadedGlobal "unexpected LoadedGlobal"
            Expect.equal VkRaw.EntryPoints.LoadedInstance 0n "unexpected LoadedInstance"
            Expect.equal VkRaw.EntryPoints.LoadedInstanceVersion 0u "unexpected LoadedInstanceVersion"
            Expect.isEmpty VkRaw.EntryPoints.LoadedDevices "unexpected LoadedDevices"
            Expect.equal (VkRaw.EntryPoints.GetDeviceVersion device) 0u "unexpected GetDeviceVersion"
            Expect.equal (VkRaw.EntryPoints.GetDeviceVersion (device + 1n)) 0u "unexpected GetDeviceVersion"

            VkRaw.EntryPoints.UnloadDevice device // no effect but success

    let tests (target: TestTarget) =
        [
            "Load global entry points",             Cases.loadGlobal
            "Load instance entry points",           Cases.loadInstance
            "Load instance extension entry points", Cases.loadInstanceExtension
            "Load device entry points",             Cases.loadDevice
            "Load device extension entry points",   Cases.loadDeviceExtension
            "Validation",                           Cases.validation
        ]
        |> prepareCasesWithoutRuntimeFor "VulkanEntryPoints" target [TestTarget.Vulkan]
