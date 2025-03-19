namespace Aardvark.Rendering.Vulkan

open System.Runtime.InteropServices
open Aardvark.Rendering
open Vulkan11

module ExternalMemory =

    module private Win32 =
        open KHRExternalMemoryWin32

        let getMemoryHandle (device: VkDevice) (memory: VkDeviceMemory) =
            let handle =
                native {
                    let! pHandle = 0n
                    let! pInfo = VkMemoryGetWin32HandleInfoKHR(memory, VkExternalMemoryHandleTypeFlags.OpaqueWin32Bit)

                    VkRaw.vkGetMemoryWin32HandleKHR(device, pInfo, pHandle)
                        |> check "could not create shared handle"

                    return !!pHandle
                }

            new Win32Handle(handle) :> IExternalMemoryHandle

    module private Posix =
        open KHRExternalMemoryFd

        let getMemoryHandle (device: VkDevice) (memory: VkDeviceMemory) =
            let handle =
                native {
                    let! pHandle = 0
                    let! pInfo = VkMemoryGetFdInfoKHR(memory, VkExternalMemoryHandleTypeFlags.OpaqueFdBit)

                    VkRaw.vkGetMemoryFdKHR(device, pInfo, pHandle)
                        |> check "could not create shared handle"

                    return !!pHandle
                }

            new PosixHandle(handle) :> IExternalMemoryHandle

    let Extension =
        if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
            KHRExternalMemoryWin32.Name
        else
            KHRExternalMemoryFd.Name

    let ofDeviceMemory (device: VkDevice) (memory: VkDeviceMemory) =
        if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
            Win32.getMemoryHandle device memory
        else
            Posix.getMemoryHandle device memory