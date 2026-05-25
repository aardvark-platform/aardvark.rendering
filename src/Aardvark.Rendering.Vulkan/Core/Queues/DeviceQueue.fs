namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open FSharp.NativeInterop
open System
open Vulkan11

#nowarn "9"
#nowarn "51"

/// Opt-in (AARDVARK_QTRACE=1) flushing tracer for VkQueue submit/present ordering — used to
/// diagnose the MoltenVK glyph-upload fence wedge. Writes to stderr (auto-flush) so the tail
/// survives a hang.
module SubmitTrace =
    let enabled = System.Environment.GetEnvironmentVariable "AARDVARK_QTRACE" = "1"
    let private counter = ref 0
    let next () = System.Threading.Interlocked.Increment counter
    let log (s: string) =
        if enabled then
            System.Console.Error.WriteLine("[QTRACE] " + s)
            System.Console.Error.Flush()
    let inline tid () = System.Threading.Thread.CurrentThread.ManagedThreadId

type DeviceQueue internal (family: IDeviceQueueFamily, index: int) =
    let device = family.DeviceInterface
    let mutable handle = VkQueue.Zero
    do VkRaw.vkGetDeviceQueue(device.Handle, uint32 family.Info.index, uint32 index, &&handle)

    let fence = device.CreateFence()

    // A VkQueue is NOT thread-safe: vkQueueSubmit / vkQueuePresentKHR / vkQueueBindSparse
    // on the same queue must be externally synchronized, and RunSynchronously shares the
    // per-queue `fence` above. The render thread (submit + present) and background uploads
    // (RunSynchronously — e.g. text-glyph GeometryPool streaming) otherwise race here; on
    // MoltenVK that deadlocks the fence wait. Serialize all queue access through this lock.
    // (ContextLock is a GL make-current artifact and does NOT guard the Vulkan queue.)
    let queueLock = obj()

    member x.HasTransfer = family.Info.flags.HasFlag QueueFlags.Transfer
    member x.HasCompute = family.Info.flags.HasFlag QueueFlags.Compute
    member x.HasGraphics = family.Info.flags.HasFlag QueueFlags.Graphics

    member internal x.DeviceInterface = device
    member internal x.FamilyInterface = family
    member x.Flags = family.Info.flags
    member x.FamilyIndex = family.Info.index
    member x.Index = index
    member x.Handle = handle
    /// Guards all VkQueue access on this queue. vkQueuePresentKHR (Swapchain) must take it too.
    member x.QueueLock = queueLock

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
                |> checkForFault device "could not bind sparse memory"

        | _ ->
            use pBinds = fixed binds
            VkRaw.vkQueueBindSparse(handle, uint32 binds.Length, pBinds, fence)
                |> checkForFault device "could not bind sparse memory"

    member x.BindSparseSynchronously(binds: VkBindSparseInfo[]) =
        lock queueLock (fun () ->
            fence.Reset()
            x.BindSparse(binds, fence)
            fence.Wait())

    member x.Submit(buffers: CommandBuffer[], waitFor: Semaphore[], signal: Semaphore[], fence: Fence) =
        let pWaitFor = waitFor |> NativePtr.stackUseArr _.Handle
        let pWaitDstFlags = waitFor |> NativePtr.stackUseArr (fun _ -> VkPipelineStageFlags.TopOfPipeBit)
        let pSignal = signal |> NativePtr.stackUseArr _.Handle
        let pCommandBuffers = buffers |> NativePtr.stackUseArr _.Handle

        let fence =
            if isNull fence then VkFence.Null
            else fence.Handle

        if SubmitTrace.enabled then
            let hs (a: Semaphore[]) = a |> Array.map (fun s -> sprintf "%x" s.Handle.Handle) |> String.concat ","
            SubmitTrace.log (sprintf "submit  seq=%d tid=%d q=fam%d/%d qh=%x bufs=%d fence=%x wait=[%s] signal=[%s]"
                (SubmitTrace.next()) (SubmitTrace.tid()) family.Info.index index (int64 handle.Handle) buffers.Length (int64 fence.Handle) (hs waitFor) (hs signal))

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
                |> checkForFault device "could not submit command buffer"

        | _ ->
            let mutable submitInfo =
                VkSubmitInfo(
                    uint32 waitFor.Length, pWaitFor, pWaitDstFlags,
                    uint32 buffers.Length, pCommandBuffers,
                    uint32 signal.Length, pSignal
                )

            VkRaw.vkQueueSubmit(handle, 1u, &&submitInfo, fence)
                |> checkForFault device "could not submit command buffer"

    member x.RunSynchronously(buffers: CommandBuffer[], waitFor: Semaphore[], signal: Semaphore[]) =
        lock queueLock (fun () ->
            fence.Reset()
            x.Submit(buffers, waitFor, signal, fence)
            if SubmitTrace.enabled then
                SubmitTrace.log (sprintf "runsync-wait seq=%d tid=%d q=fam%d/%d qh=%x fence=%x"
                    (SubmitTrace.next()) (SubmitTrace.tid()) family.Info.index index (int64 handle.Handle) (int64 fence.Handle))
            fence.Wait()
            if SubmitTrace.enabled then
                SubmitTrace.log (sprintf "runsync-DONE tid=%d qh=%x" (SubmitTrace.tid()) (int64 handle.Handle)))

    member x.RunSynchronously(buffer: CommandBuffer) =
        if not buffer.IsEmpty then
            x.RunSynchronously([|buffer|], Array.empty, Array.empty)

    member x.StartTask(buffers: CommandBuffer[], waitFor: Semaphore[], signal: Semaphore[]) =
        let f = device.CreateFence()
        lock queueLock (fun () -> x.Submit(buffers, waitFor, signal, f))
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