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


type CommandBuffer(pool : CommandPool, handle : VkCommandBuffer) =
    let mutable handle = handle
    member x.Pool = pool
    member x.Handle = handle

    member private x.Dispose(disposing : bool) =
        let mutable old = Interlocked.Exchange(&handle, 0n)
        if old <> 0n then
            VkRaw.vkFreeCommandBuffers(pool.Device.Handle, pool.Handle, 1u, &&old)
            if disposing then GC.SuppressFinalize x

    member x.Dispose() = x.Dispose true
    override x.Finalize() =
        try x.Dispose false
        with _ -> ()

    interface IDisposable with
        member x.Dispose() = x.Dispose true

[<AbstractClass; Sealed; Extension>]
type CommandPoolExtensions private() =
    

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
    static member End(buffer : CommandBuffer) =
        VkRaw.vkEndCommandBuffer(buffer.Handle)
            |> check "vkEndCommandBuffer"

[<AbstractClass; Sealed; Extension>]
type QueueCommandExtensions private() =
    [<Extension>]
    static member Submit(this : Queue, cmd : CommandBuffer[]) =

        async {
            let start() =
                let ptrs = NativePtr.stackalloc cmd.Length
                for i in 0..cmd.Length-1 do
                    NativePtr.set ptrs i cmd.[i].Handle

                let mutable submit =
                    VkSubmitInfo(
                        VkStructureType.SubmitInfo, 
                        0n,
                        0u, NativePtr.zero,
                        NativePtr.zero,
                        uint32 cmd.Length, ptrs,
                        0u, NativePtr.zero
                    )

                let mutable fence = VkFence.Null
                let mutable fenceInfo =
                    VkFenceCreateInfo(
                        VkStructureType.FenceCreateInfo, 
                        0n,
                        VkFenceCreateFlags.None
                    )

                VkRaw.vkCreateFence(this.Device.Handle, &&fenceInfo, NativePtr.zero, &&fence)
                    |> check "vkCreateFence"

                VkRaw.vkQueueSubmit(this.Handle, 1u, &&submit, fence)
                    |> check "vkQueueSubmit"

                let wait() =
                    let mutable fence = fence
                    VkRaw.vkWaitForFences(this.Device.Handle, 1u, &&fence, 1u, ~~~0UL)
                        |> check "vkWaitForFences"

                    VkRaw.vkDestroyFence(this.Device.Handle, fence, NativePtr.zero)

                wait
                
            let wait = start()
            wait()
        }

    [<Extension>]
    static member SubmitAndWait(this : Queue, cmd : CommandBuffer[]) =
        QueueCommandExtensions.Submit(this, cmd) |> Async.RunSynchronously

    [<Extension>]
    static member SubmitTask(this : Queue, cmd : CommandBuffer[]) =
        QueueCommandExtensions.Submit(this, cmd) |> Async.StartAsTask