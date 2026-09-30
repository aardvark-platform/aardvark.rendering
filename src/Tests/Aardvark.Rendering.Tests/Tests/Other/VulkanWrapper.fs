namespace Aardvark.Rendering.Tests.Other

open Aardvark.Rendering.Tests
open Aardvark.Rendering.Vulkan
open Aardvark.Rendering.Vulkan.Memory
open Aardvark.Rendering.Vulkan.Vulkan14
open KHRAccelerationStructure
open KHRFragmentShadingRate
open NVClusterAccelerationStructure
open Expecto

module VulkanWrapper =

    module Arrays =

        let uint32_32 () =
            let mutable array = uint32_32()
            array.[array.Length - 1] <- 42u
            Expect.equal array.[array.Length - 1] 42u ""

        let int32_7 () =
            let mutable array = int32_7()
            array.[array.Length - 1] <- 42
            Expect.equal array.[array.Length - 1] 42 ""

        let byte_32 () =
            let mutable array = byte_32()
            array.[array.Length - 1] <- 42uy
            Expect.equal array.[array.Length - 1] 42uy ""

        let byte_8 () =
            let mutable array = byte_8()
            array.[array.Length - 1] <- 42uy
            Expect.equal array.[array.Length - 1] 42uy ""

        let float32_6 () =
            let mutable array = float32_6()
            array.[array.Length - 1] <- 42.0f
            Expect.equal array.[array.Length - 1] 42.0f ""

        let VkPhysicalDevice_32 () =
            let mutable array = VkPhysicalDevice_32()
            array.[array.Length - 1] <- 42n
            Expect.equal array.[array.Length - 1] 42n ""

        let VkDeviceSize_16 () =
            let mutable array = VkDeviceSize_16()
            array.[array.Length - 1] <- 42UL
            Expect.equal array.[array.Length - 1] 42UL ""

        let VkOffset3D_2 () =
            let mutable array = VkOffset3D_2()
            let value = VkOffset3D(1, 2, 3)
            array.[array.Length - 1] <- value
            Expect.equal array.[array.Length - 1] value ""

        let VkMemoryHeap_16 () =
            let mutable array = VkMemoryHeap_16()
            let value = VkMemoryHeap(42UL, VkMemoryHeapFlags.DeviceLocalBit)
            array.[array.Length - 1] <- value
            Expect.equal array.[array.Length - 1] value ""

        let VkMemoryType_32 () =
            let mutable array = VkMemoryType_32()
            let value = VkMemoryType(VkMemoryPropertyFlags.HostVisibleBit, 42u)
            array.[array.Length - 1] <- value
            Expect.equal array.[array.Length - 1] value ""

        let VkQueueGlobalPriority_16 () =
            let mutable array = VkQueueGlobalPriority_16()
            let value = VkQueueGlobalPriority.Medium
            array.[array.Length - 1] <- value
            Expect.equal array.[array.Length - 1] value ""

        let VkFragmentShadingRateCombinerOpKHR_2 () =
            let mutable array = VkFragmentShadingRateCombinerOpKHR_2()
            let value = VkFragmentShadingRateCombinerOpKHR.Mul
            array.[array.Length - 1] <- value
            Expect.equal array.[array.Length - 1] value ""

        let VmaDetailedStatistics_16 () =
            let mutable array = VmaDetailedStatistics_16()
            let value = { VmaDetailedStatistics.Empty with statistics = { VmaStatistics.Empty with blockBytes = 42UL } }
            array.[array.Length - 1] <- value
            Expect.equal array.[array.Length - 1] value ""

        let VmaDetailedStatistics_32 () =
            let mutable array = VmaDetailedStatistics_32()
            let value = { VmaDetailedStatistics.Empty with allocationSizeMax = 42UL }
            array.[array.Length - 1] <- value
            Expect.equal array.[array.Length - 1] value ""

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
