namespace Aardvark.Rendering.Tests

open Aardvark.Rendering.Vulkan
open KHRAccelerationStructure
open NVClusterAccelerationStructure
open Expecto

module ``Vulkan Wrapper Tests`` =

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

    [<Tests>]
    let tests =
        testList "VulkanWrapper" [
            testList "Bitfields" [
                Bitfields.VkAccelerationStructureInstanceKHR
                Bitfields.VkClusterAccelerationStructureBuildTriangleClusterInfoNV
            ]
        ]