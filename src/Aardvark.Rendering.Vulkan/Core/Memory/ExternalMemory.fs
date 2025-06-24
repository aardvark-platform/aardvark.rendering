namespace Aardvark.Rendering.Vulkan.Memory

open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open System
open Vulkan11

#nowarn "51"

[<AllowNullLiteral>]
type internal ExternalMemoryBlock(handle: IExternalMemoryHandle, memory: VkDeviceMemory, sizeInBytes: uint64) =
    member _.Handle = handle
    member _.Memory = memory
    member _.SizeInBytes = sizeInBytes

    member _.Dispose() =
        handle.Dispose()

    member inline private _.Equals(other: ExternalMemoryBlock) =
        memory = other.Memory

    override this.Equals(obj) =
        match obj with
        | :? ExternalMemoryBlock as other -> this.Equals(other)
        | _ -> false

    override this.GetHashCode() =
        hash memory.Handle

    interface IEquatable<ExternalMemoryBlock> with
        member this.Equals(other) = this.Equals(other)

    interface IExternalMemoryBlock with
        member this.Handle = this.Handle
        member this.SizeInBytes = this.SizeInBytes
        member this.Dispose() = this.Dispose()

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