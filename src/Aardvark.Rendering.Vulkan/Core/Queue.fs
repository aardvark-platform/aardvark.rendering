namespace Aardvark.Rendering.Vulkan

#nowarn "9"
#nowarn "51"

open System
open System.Threading
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base

type Queue(device : Device, cmdPool : ThreadLocal<CommandPool>, family : PhysicalQueueFamily, handle : VkQueue, index : int) =
    member x.CommandPool = cmdPool.Value
    member x.Device = device
    member x.Family = family
    member x.Handle = handle
    member x.QueueIndex = index


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