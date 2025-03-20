namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open FSharp.NativeInterop
open System
open Vulkan11

#nowarn "9"
#nowarn "51"

type DeviceQueue internal (family: IDeviceQueueFamily, index: int) =
    let device = family.DeviceInterface
    let mutable handle = VkQueue.Zero
    do VkRaw.vkGetDeviceQueue(device.Handle, uint32 family.Info.index, uint32 index, &&handle)

    let fence = device.CreateFence()

    member x.HasTransfer = family.Info.flags.HasFlag QueueFlags.Transfer
    member x.HasCompute = family.Info.flags.HasFlag QueueFlags.Compute
    member x.HasGraphics = family.Info.flags.HasFlag QueueFlags.Graphics

    member internal x.DeviceInterface = device
    member internal x.FamilyInterface = family
    member x.Flags = family.Info.flags
    member x.FamilyIndex = family.Info.index
    member x.Index = index
    member x.Handle = handle

    member x.BindSparse(binds: VkBindSparseInfo[], fence: Fence) =
        let fence =
            if isNull fence then VkFence.Null
            else fence.Handle

        match device.PhysicalDevice with
        | :? PhysicalDeviceGroup as group ->
            let groupInfos =
                binds |> Array.collect (fun _ ->
                    group.AllIndicesArr |> Array.map (fun i ->
                        VkDeviceGroupBindSparseInfo(
                            uint32 i, uint32 i
                        )
                    )
                )

            use pGroupInfos = fixed groupInfos

            let binds =
                let mutable gi = 0
                binds |> Array.collect (fun b ->
                    group.AllIndicesArr |> Array.map (fun _ ->
                        let mutable res = b
                        res.pNext <- NativePtr.toNativeInt (NativePtr.add pGroupInfos gi)
                        gi <- gi + 1
                        res
                    )
                )

            use pBinds = fixed binds
            VkRaw.vkQueueBindSparse(handle, uint32 binds.Length, pBinds, fence)
                |> check "could not bind sparse memory"

        | _ ->
            use pBinds = fixed binds
            VkRaw.vkQueueBindSparse(handle, uint32 binds.Length, pBinds, fence)
                |> check "could not bind sparse memory"

    member x.BindSparseSynchronously(binds: VkBindSparseInfo[]) =
        fence.Reset()
        x.BindSparse(binds, fence)
        fence.Wait()

    member x.Submit(buffers: CommandBuffer[], waitFor: Semaphore[], signal: Semaphore[], fence: Fence) =
        let pWaitFor = waitFor |> NativePtr.stackUseArr _.Handle
        let pWaitDstFlags = waitFor |> NativePtr.stackUseArr (fun _ -> VkPipelineStageFlags.TopOfPipeBit)
        let pSignal = signal |> NativePtr.stackUseArr _.Handle
        let pCommandBuffers = buffers |> NativePtr.stackUseArr _.Handle

        let fence =
            if isNull fence then VkFence.Null
            else fence.Handle

        match device.PhysicalDevice with
        | :? PhysicalDeviceGroup as group ->
            let pCommandBufferDeviceMasks = buffers |> NativePtr.stackUseArr (fun _ -> group.DeviceMask)

            let waitCount, pWaitIndices =
                if waitFor.Length > 0 then uint32 group.Count, group.AllIndices
                else 0u, NativePtr.zero

            let signalCount, pSignalIndices =
                if waitFor.Length > 0 then uint32 group.Count, group.AllIndices
                else 0u, NativePtr.zero

            let mutable groupSubmitInfo =
                VkDeviceGroupSubmitInfo(
                    waitCount, pWaitIndices,
                    uint32 buffers.Length, pCommandBufferDeviceMasks,
                    signalCount, pSignalIndices
                )

            let mutable submitInfo =
                VkSubmitInfo(
                    NativePtr.toNativeInt &&groupSubmitInfo,
                    uint32 waitFor.Length, pWaitFor, pWaitDstFlags,
                    uint32 buffers.Length, pCommandBuffers,
                    uint32 signal.Length, pSignal
                )

            VkRaw.vkQueueSubmit(handle, 1u, &&submitInfo, fence)
                |> check "could not submit command buffer"

        | _ ->
            let mutable submitInfo =
                VkSubmitInfo(
                    uint32 waitFor.Length, pWaitFor, pWaitDstFlags,
                    uint32 buffers.Length, pCommandBuffers,
                    uint32 signal.Length, pSignal
                )

            VkRaw.vkQueueSubmit(handle, 1u, &&submitInfo, fence)
                |> check "could not submit command buffer"

    member x.RunSynchronously(buffers: CommandBuffer[], waitFor: Semaphore[], signal: Semaphore[]) =
        fence.Reset()
        x.Submit(buffers, waitFor, signal, fence)
        fence.Wait()

    member x.RunSynchronously(buffer: CommandBuffer) =
        if not buffer.IsEmpty then
            x.RunSynchronously([|buffer|], Array.empty, Array.empty)

    member x.StartTask(buffers: CommandBuffer[], waitFor: Semaphore[], signal: Semaphore[]) =
        let f = device.CreateFence()
        x.Submit(buffers, waitFor, signal, f)
        new DeviceTask(f)

    member x.StartTask(buffer: CommandBuffer) =
        if buffer.IsEmpty then
            DeviceTask.Completed
        else
            x.StartTask([|buffer|], Array.empty, Array.empty)

    member x.Dispose() =
        fence.Dispose()

    interface IDeviceObject with
        member x.DeviceInterface = x.DeviceInterface

    interface IDisposable with
        member x.Dispose() = x.Dispose()