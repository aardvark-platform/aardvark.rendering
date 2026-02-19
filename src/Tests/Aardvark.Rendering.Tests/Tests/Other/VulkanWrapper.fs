namespace Aardvark.Rendering.Tests

open Aardvark.Base
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
#nowarn "51"

module ``Vulkan Wrapper Tests`` =

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
                createInfo.pApplicationInfo <- &&appInfo
                createInfo.enabledExtensionCount <- uint32 pExtensions.Length
                createInfo.ppEnabledExtensionNames <- ppExtensions

                let mutable instance = VkInstance.Zero
                VkRaw.vkCreateInstance(&&createInfo, NativePtr.zero, &&instance) |> VkResult.check

                pExtensions |> Array.iter CStr.free
                CStr.free appInfo.pEngineName
                CStr.free appInfo.pApplicationName

                instance

            let getPhysicalDevice (instance: VkInstance) =
                let mutable count = 0u
                VkRaw.vkEnumeratePhysicalDevices(instance, &&count, NativePtr.zero) |> VkResult.check

                let physicalDevices = Array.zeroCreate (int count)
                use pPhysicalDevices = fixed physicalDevices
                VkRaw.vkEnumeratePhysicalDevices(instance, &&count, pPhysicalDevices) |> VkResult.check

                pPhysicalDevices.[0]

        module VkDevice =

            let create (physicalDevice: VkPhysicalDevice) (extensions: string seq) =
                let pExtensions = extensions |> Seq.map CStr.malloc |> Seq.toArray
                use ppExtensions = fixed pExtensions

                let mutable features = VkPhysicalDeviceFeatures.Empty
                let mutable createInfo = VkDeviceCreateInfo.Empty
                createInfo.enabledExtensionCount <- uint32 <| pExtensions.Length
                createInfo.pEnabledFeatures <- &&features
                createInfo.ppEnabledExtensionNames <- ppExtensions

                let mutable device = VkDevice.Zero
                VkRaw.vkCreateDevice(physicalDevice, &&createInfo, NativePtr.zero, &&device) |> VkResult.check

                pExtensions |> Array.iter CStr.free

                device

    module Arrays =

        let uint32_32 =
            test "uint32_32" {
                let mutable array = uint32_32()
                array.[array.Length - 1] <- 42u
                Expect.equal array.[array.Length - 1] 42u ""
            }

        let int32_7 =
            test "int32_7" {
                let mutable array = int32_7()
                array.[array.Length - 1] <- 42
                Expect.equal array.[array.Length - 1] 42 ""
            }

        let byte_32 =
            test "byte_32" {
                let mutable array = byte_32()
                array.[array.Length - 1] <- 42uy
                Expect.equal array.[array.Length - 1] 42uy ""
            }

        let byte_8 =
            test "byte_8" {
                let mutable array = byte_8()
                array.[array.Length - 1] <- 42uy
                Expect.equal array.[array.Length - 1] 42uy ""
            }

        let float32_6 =
            test "float32_6" {
                let mutable array = float32_6()
                array.[array.Length - 1] <- 42.0f
                Expect.equal array.[array.Length - 1] 42.0f ""
            }

        let VkPhysicalDevice_32 =
            test "VkPhysicalDevice_32" {
                let mutable array = VkPhysicalDevice_32()
                array.[array.Length - 1] <- 42n
                Expect.equal array.[array.Length - 1] 42n ""
            }

        let VkDeviceSize_16 =
            test "VkDeviceSize_16" {
                let mutable array = VkDeviceSize_16()
                array.[array.Length - 1] <- 42UL
                Expect.equal array.[array.Length - 1] 42UL ""
            }
            
        let VkOffset3D_2 =
            test "VkOffset3D_2" {
                let mutable array = VkOffset3D_2()
                let value = VkOffset3D(1, 2, 3)
                array.[array.Length - 1] <- value
                Expect.equal array.[array.Length - 1] value ""
            }

        let VkMemoryHeap_16 =
            test "VkMemoryHeap_16" {
                let mutable array = VkMemoryHeap_16()
                let value = VkMemoryHeap(42UL, VkMemoryHeapFlags.DeviceLocalBit)
                array.[array.Length - 1] <- value
                Expect.equal array.[array.Length - 1] value ""
            }

        let VkMemoryType_32 =
            test "VkMemoryType_32" {
                let mutable array = VkMemoryType_32()
                let value = VkMemoryType(VkMemoryPropertyFlags.HostVisibleBit, 42u)
                array.[array.Length - 1] <- value
                Expect.equal array.[array.Length - 1] value ""
            }

        let VkQueueGlobalPriority_16 =
            test "VkQueueGlobalPriority_16" {
                let mutable array = VkQueueGlobalPriority_16()
                let value = VkQueueGlobalPriority.Medium
                array.[array.Length - 1] <- value
                Expect.equal array.[array.Length - 1] value ""
            }

        let VkFragmentShadingRateCombinerOpKHR_2 =
            test "VkFragmentShadingRateCombinerOpKHR_2" {
                let mutable array = VkFragmentShadingRateCombinerOpKHR_2()
                let value = VkFragmentShadingRateCombinerOpKHR.Mul
                array.[array.Length - 1] <- value
                Expect.equal array.[array.Length - 1] value ""
            }

        let VmaDetailedStatistics_16 =
            test "VmaDetailedStatistics_16" {
                let mutable array = VmaDetailedStatistics_16()
                let value = { VmaDetailedStatistics.Empty with statistics = { VmaStatistics.Empty with blockBytes = 42UL } }
                array.[array.Length - 1] <- value
                Expect.equal array.[array.Length - 1] value ""
            }

        let VmaDetailedStatistics_32 =
            test "VmaDetailedStatistics_32" {
                let mutable array = VmaDetailedStatistics_32()
                let value = { VmaDetailedStatistics.Empty with allocationSizeMax = 42UL }
                array.[array.Length - 1] <- value
                Expect.equal array.[array.Length - 1] value ""
            }

    module Bitfields =

        let VkAccelerationStructureInstanceKHR =
            test "VkAccelerationStructureInstanceKHR" {
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
            }

        let VkClusterAccelerationStructureBuildTriangleClusterInfoNV =
            test "VkClusterAccelerationStructureBuildTriangleClusterInfoNV" {
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
            }

    module EntryPoints =

        let private version = Version(1, 0, 0)

        let private init() =
            VkRaw.EntryPoints.ReportEntryPointAddresses <- true
            { new IDisposable with
                member _.Dispose() =
                    Expect.equal VkRaw.EntryPoints.LoadedInstance 0n "unexpected LoadedInstance"
                    Expect.equal VkRaw.EntryPoints.LoadedInstanceVersion 0u "unexpected LoadedInstanceVersion"
                    Expect.isEmpty VkRaw.EntryPoints.LoadedDevices "unexpected LoadedDevices"
            }

        let loadGlobal =
            test "Load global entry points" {
                use _ = init()
                let result = VkRaw.vkGetInstanceProcAddr(VkInstance.Zero, "blub")
                Expect.isTrue VkRaw.EntryPoints.LoadedGlobal "unexpected LoadedGlobal"
                Expect.equal VkRaw.EntryPoints.LoadedInstance 0n "unexpected LoadedInstance"
                Expect.equal VkRaw.EntryPoints.LoadedInstanceVersion 0u "unexpected LoadedInstanceVersion"
                Expect.isEmpty VkRaw.EntryPoints.LoadedDevices "unexpected LoadedDevices"
                Expect.equal result 0n "unexpected entry point"
            }

        let loadInstance =
            test "Load instance entry points" {
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
            }

        let loadInstanceExtension =
            test "Load instance extension entry points" {
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
            }

        let loadDevice =
            test "Load device entry points" {
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
            }

        let loadDeviceExtension =
            test "Load device extension entry points" {
                use _ = init()

                let instance = VkInstance.create version []
                let physicalDevice = VkInstance.getPhysicalDevice instance

                for _ = 1 to 2 do
                    let device = VkDevice.create physicalDevice [ KHRTimelineSemaphore.Name ]

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
            }

        let validation =
            test "Validation" {
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
            }

    [<Tests>]
    let tests =
        testList "VulkanWrapper" [
            testList "Arrays" [
                Arrays.uint32_32
                Arrays.int32_7
                Arrays.byte_32
                Arrays.byte_8
                Arrays.float32_6
                Arrays.VkPhysicalDevice_32
                Arrays.VkDeviceSize_16
                Arrays.VkOffset3D_2
                Arrays.VkMemoryHeap_16
                Arrays.VkMemoryType_32
                Arrays.VkQueueGlobalPriority_16
                Arrays.VkFragmentShadingRateCombinerOpKHR_2
                Arrays.VmaDetailedStatistics_16
                Arrays.VmaDetailedStatistics_32
            ]

            testList "Bitfields" [
                Bitfields.VkAccelerationStructureInstanceKHR
                Bitfields.VkClusterAccelerationStructureBuildTriangleClusterInfoNV
            ]

            testList "Entry points" [
                EntryPoints.loadGlobal
                EntryPoints.loadInstance
                EntryPoints.loadInstanceExtension
                EntryPoints.loadDevice
                EntryPoints.loadDeviceExtension
                EntryPoints.validation
            ]
        ]