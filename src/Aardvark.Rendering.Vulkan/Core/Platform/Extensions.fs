namespace Aardvark.Rendering.Vulkan

open System.Runtime.InteropServices

/// Contains Vulkan extensions (both instance and device)
module Extensions =
    let Surface                         = KHRSurface.Name
    let SwapChain                       = KHRSwapchain.Name
    let Display                         = KHRDisplay.Name
    let DisplaySwapChain                = KHRDisplaySwapchain.Name

    let AndroidSurface                  = KHRAndroidSurface.Name
    let WaylandSurface                  = KHRWaylandSurface.Name
    let Win32Surface                    = KHRWin32Surface.Name
    let XcbSurface                      = KHRXcbSurface.Name
    let XlibSurface                     = KHRXlibSurface.Name
    let GetPhysicalDeviceProperties2    = KHRGetPhysicalDeviceProperties2.Name

    let ShaderSubgroupVote              = EXTShaderSubgroupVote.Name
    let ShaderSubgroupBallot            = EXTShaderSubgroupBallot.Name

    let ConservativeRasterization       = EXTConservativeRasterization.Name

    let CustomBorderColor               = EXTCustomBorderColor.Name

    let Debug                           = EXTDebugUtils.Name

    let DeviceFault                     = EXTDeviceFault.Name

    let MemoryBudget                    = EXTMemoryBudget.Name

    let MemoryPriority                  = EXTMemoryPriority.Name

    let Shader8Bit16Bit = [
        KHR8bitStorage.Name
        KHRShaderFloat16Int8.Name
    ]

    let Maintenance = [
        KHRMaintenance4.Name
        KHRMaintenance5.Name
        KHRDynamicRendering.Name
        KHRDepthStencilResolve.Name
        KHRCreateRenderpass2.Name
    ]

    let Raytracing (validation: bool) = [
            KHRRayTracingPipeline.Name
            KHRRayTracingPositionFetch.Name
            KHRAccelerationStructure.Name
            KHRBufferDeviceAddress.Name
            KHRDeferredHostOperations.Name
            EXTDescriptorIndexing.Name
            KHRSpirv14.Name
            KHRShaderFloatControls.Name
            NVRayTracingInvocationReorder.Name
            EXTOpacityMicromap.Name
            KHRSynchronization2.Name
            if validation then NVRayTracingValidation.Name
        ]

    let ExternalMemory =
        if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
            KHRExternalMemoryWin32.Name
        else
            KHRExternalMemoryFd.Name

    let Sharing = [
            KHRGetPhysicalDeviceProperties2.Name
            KHRExternalMemoryCapabilities.Name
            KHRExternalMemory.Name
            KHRExternalFenceCapabilities.Name
            KHRExternalFence.Name
            KHRExternalSemaphoreCapabilities.Name
            KHRExternalSemaphore.Name
            EXTExternalMemoryHost.Name

            if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
                KHRExternalMemoryWin32.Name
                KHRExternalFenceWin32.Name
                KHRExternalSemaphoreWin32.Name
            else
                KHRExternalMemoryFd.Name
                KHRExternalFenceFd.Name
                KHRExternalSemaphoreFd.Name

                if RuntimeInformation.IsOSPlatform OSPlatform.Linux then
                    EXTExternalMemoryDmaBuf.Name
        ]