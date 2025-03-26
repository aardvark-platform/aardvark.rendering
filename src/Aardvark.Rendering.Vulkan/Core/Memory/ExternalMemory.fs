namespace Aardvark.Rendering.Vulkan.Memory

open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open Vulkan11

#nowarn "51"

module internal ExternalMemory =

    module Win32 =
        let getHandle (allocator: VmaAllocator) (allocation: VmaAllocation) =
            let mutable handle = 0n

            Vma.getMemoryWin32Handle(allocator, allocation, 0n, &&handle)
                |> check "could not retrieve external memory handle"

            new Win32Handle(handle) :> IExternalMemoryHandle

    module Posix =
        open KHRExternalMemoryFd

        let getHandle (device: VkDevice) (memory: VkDeviceMemory) =
            let mutable info = VkMemoryGetFdInfoKHR(memory, VkExternalMemoryHandleTypeFlags.OpaqueFdBit)
            let mutable handle = 0

            VkRaw.vkGetMemoryFdKHR(device, &&info, &&handle)
                |> check "could not retrieve external memory handle"

            new PosixHandle(handle) :> IExternalMemoryHandle