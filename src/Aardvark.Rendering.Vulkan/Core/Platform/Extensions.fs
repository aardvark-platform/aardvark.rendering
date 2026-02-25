namespace Aardvark.Rendering.Vulkan

open System.Runtime.InteropServices

/// Contains Vulkan extensions (both instance and device)
module Extensions =
    let [<Literal>] Surface                         = KHRSurface.Name
    let [<Literal>] SwapChain                       = KHRSwapchain.Name
    let [<Literal>] Display                         = KHRDisplay.Name
    let [<Literal>] DisplaySwapChain                = KHRDisplaySwapchain.Name

    let [<Literal>] AndroidSurface                  = KHRAndroidSurface.Name
    let [<Literal>] WaylandSurface                  = KHRWaylandSurface.Name
    let [<Literal>] Win32Surface                    = KHRWin32Surface.Name
    let [<Literal>] XcbSurface                      = KHRXcbSurface.Name
    let [<Literal>] XlibSurface                     = KHRXlibSurface.Name
    let [<Literal>] GetPhysicalDeviceProperties2    = KHRGetPhysicalDeviceProperties2.Name

    let [<Literal>] ShaderSubgroupVote              = EXTShaderSubgroupVote.Name
    let [<Literal>] ShaderSubgroupBallot            = EXTShaderSubgroupBallot.Name

    let [<Literal>] ConservativeRasterization       = EXTConservativeRasterization.Name

    let [<Literal>] CustomBorderColor               = EXTCustomBorderColor.Name

    let [<Literal>] Debug                           = EXTDebugUtils.Name

    let [<Literal>] DeviceFault                     = EXTDeviceFault.Name

    let [<Literal>] MemoryBudget                    = EXTMemoryBudget.Name

    let [<Literal>] MemoryPriority                  = EXTMemoryPriority.Name

    let [<Literal>] PortabilityEnumeration          = KHRPortabilityEnumeration.Name

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