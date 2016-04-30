namespace Aardvark.Rendering.Vulkan

#nowarn "9"
#nowarn "51"

open System
open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base


type CommandPool(device : Device, queueFamily : PhysicalQueueFamily, handle : VkCommandPool) =
    inherit Resource(device)
    member x.Handle = handle
    member x.Device = device
    member x.QueueFamily = queueFamily

    override x.Release() =
        VkRaw.vkDestroyCommandPool(device.Handle, handle, NativePtr.zero)

    override x.Equals o =
        match o with
            | :? CommandPool as o -> handle = o.Handle
            | _ -> false

    override x.GetHashCode() =
        handle.GetHashCode()


type CommandBuffer(pool : CommandPool, handle : VkCommandBuffer) =
    inherit Resource(pool)

    let mutable handle = handle
    member x.Pool = pool
    member x.Handle = handle

    override x.Release() =
        VkRaw.vkFreeCommandBuffers(pool.Device.Handle, pool.Handle, 1u, &&handle)


[<AbstractClass; Sealed; Extension>]
type CommandPoolExtensions private() =
    
    [<Extension>]
    static member CreateCommandPool(device : Device, queueFamily : PhysicalQueueFamily) =
        let mutable info =
            VkCommandPoolCreateInfo(
                VkStructureType.CommandPoolCreateInfo,
                0n,
                VkCommandPoolCreateFlags.ResetCommandBufferBit,
                uint32 queueFamily.Index
            )

        let mutable res = VkCommandPool.Null
        VkRaw.vkCreateCommandPool(device.Handle, &&info, NativePtr.zero, &&res)
            |> check "vkCreateCommandPool"

        new CommandPool(device, queueFamily, res)

    [<Extension>]
    static member CreateCommandBuffers(this : CommandPool, count : int, primary : bool) =
        
        let mutable info =
            VkCommandBufferAllocateInfo(
                VkStructureType.CommandBufferAllocateInfo,
                0n,
                this.Handle,
                (if primary then VkCommandBufferLevel.Primary else VkCommandBufferLevel.Secondary),
                uint32 count
            )
            
        let ptr = NativePtr.stackalloc count
        VkRaw.vkAllocateCommandBuffers(this.Device.Handle, &&info, ptr)
            |> check "vkAllocateCommandBuffers"

        ptr |> NativePtr.toArray count
            |> Array.map (fun h -> new CommandBuffer(this, h))

    [<Extension>]
    static member CreateCommandBuffers(this : CommandPool, count : int) =
        CommandPoolExtensions.CreateCommandBuffers(this, count, true)

    [<Extension>]
    static member CreateCommandBuffer(this : CommandPool, primary : bool) =
        CommandPoolExtensions.CreateCommandBuffers(this, 1, primary).[0]

    [<Extension>]
    static member CreateCommandBuffer(this : CommandPool) =
        CommandPoolExtensions.CreateCommandBuffers(this, 1, true).[0]

    [<Extension>]
    static member Delete(this : CommandPool, b : CommandBuffer) =
        b.Dispose()


[<AbstractClass; Sealed; Extension>]
type CommandBufferExtensions private() =
    
    [<Extension>]
    static member Begin(buffer : CommandBuffer, persistent : bool) =
        let mutable inh =
            VkCommandBufferInheritanceInfo(
                VkStructureType.CommandBufferInheritanceInfo,
                0n,
                VkRenderPass.Null,
                0u,
                VkFramebuffer.Null,
                0u, VkQueryControlFlags.None,
                VkQueryPipelineStatisticFlags.None
            )

        let mutable info =
            VkCommandBufferBeginInfo(
                VkStructureType.CommandBufferBeginInfo,
                0n,
                (if persistent then VkCommandBufferUsageFlags.None else VkCommandBufferUsageFlags.OneTimeSubmitBit),
                &&inh
            )
        VkRaw.vkBeginCommandBuffer(buffer.Handle, &&info)
            |> check "vkBeginCommandBuffer" 

    [<Extension>]
    static member Begin(buffer : CommandBuffer) =
        CommandBufferExtensions.Begin(buffer, false)

    [<Extension>]
    static member End(buffer : CommandBuffer) =
        VkRaw.vkEndCommandBuffer(buffer.Handle)
            |> check "vkEndCommandBuffer"

