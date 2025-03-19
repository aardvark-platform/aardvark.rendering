namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open System
open System.Collections.Generic
open System.Runtime.InteropServices

#nowarn "51"

type CommandBufferLevel =
    | Primary = 0
    | Secondary = 1

[<Flags>]
type CommandBufferUsage =
    | None = 0
    | OneTimeSubmit = 0x00000001
    | RenderPassContinue = 0x00000002
    | SimultaneousUse = 0x00000004

type CommandBuffer internal (pool: ICommandPool, level: CommandBufferLevel, removeFromPool: CommandBuffer -> unit) =
    let device = pool.DeviceInterface
    let mutable handle = VkCommandBuffer.Zero

    do
        let mutable allocateInfo =
            VkCommandBufferAllocateInfo(
                pool.Handle,
                enum (int level),
                1u
            )

        VkRaw.vkAllocateCommandBuffers(device.Handle, &&allocateInfo, &&handle)
            |> check "could not allocated command buffer"

        device.Instance.RegisterDebugTrace(handle)

    let mutable commands = 0
    let mutable recording = false

    // Set of resources used by recorded commands. Need to be disposed whenever
    // the command buffer is reset to allow them to be freed.
    let resources = HashSet<IResource>()

    let releaseResources() =
        for r in resources do r.Dispose()
        resources.Clear()

    let beginPrimary (usage: CommandBufferUsage) =
        let mutable beginInfo =
            VkCommandBufferBeginInfo(
                unbox (int usage),
                NativePtr.zero
            )

        VkRaw.vkBeginCommandBuffer(handle, &&beginInfo)
            |> check "could not begin command buffer"

    let beginSecondary (pass: VkRenderPass) (framebuffer: VkFramebuffer) (inheritQueries: bool) (usage: CommandBufferUsage) =
        let occlusion, control, statistics =
            let features = device.PhysicalDevice.Features.Queries

            if inheritQueries && features.InheritedQueries then
                let control =
                    if features.OcclusionQueryPrecise then
                        VkQueryControlFlags.All
                    else
                        VkQueryControlFlags.All ^^^ VkQueryControlFlags.PreciseBit

                let statistics =
                    if features.PipelineStatistics then
                        VkQueryPipelineStatisticFlags.All
                    else
                        VkQueryPipelineStatisticFlags.None

                1u, control, statistics
            else
                0u, VkQueryControlFlags.None, VkQueryPipelineStatisticFlags.None

        let mutable inheritanceInfo =
            VkCommandBufferInheritanceInfo(
                pass, 0u, framebuffer,
                occlusion, control, statistics
            )

        let mutable beginInfo =
            VkCommandBufferBeginInfo(
                unbox (int usage),
                &&inheritanceInfo
            )

        VkRaw.vkBeginCommandBuffer(handle, &&beginInfo)
            |> check "could not begin command buffer"

    let beginBuffer (pass: VkRenderPass) (framebuffer: VkFramebuffer) (inheritQueries: bool) (usage: CommandBufferUsage) =
        releaseResources()

        match level with
        | CommandBufferLevel.Primary -> beginPrimary usage
        | CommandBufferLevel.Secondary -> beginSecondary pass framebuffer inheritQueries usage
        | _ -> failwith "unknown command buffer level"

        commands <- 0
        recording <- true

    member internal x.Reset(resetByPool: bool) =
        releaseResources()

        if not resetByPool then
            VkRaw.vkResetCommandBuffer(handle, VkCommandBufferResetFlags.ReleaseResourcesBit)
                |> check "could not reset command buffer"

        commands <- 0
        recording <- false

    member x.Reset() =
        x.Reset false

    member x.Begin(renderPass: IResource<VkRenderPass>, framebuffer: IResource<VkFramebuffer>,
                   [<Optional; DefaultParameterValue(CommandBufferUsage.None)>] usage: CommandBufferUsage,
                   [<Optional; DefaultParameterValue(false)>] inheritQueries: bool) =
        beginBuffer renderPass.Handle framebuffer.Handle inheritQueries usage

    member x.Begin(renderPass: IResource<VkRenderPass>,
                   [<Optional; DefaultParameterValue(CommandBufferUsage.None)>] usage: CommandBufferUsage,
                   [<Optional; DefaultParameterValue(false)>] inheritQueries : bool) =
        beginBuffer renderPass.Handle VkFramebuffer.Null inheritQueries usage

    member x.Begin([<Optional; DefaultParameterValue(CommandBufferUsage.None)>] usage: CommandBufferUsage,
                   [<Optional; DefaultParameterValue(false)>] inheritQueries : bool) =
        beginBuffer VkRenderPass.Null VkFramebuffer.Null inheritQueries usage

    member x.End() =
        VkRaw.vkEndCommandBuffer(handle)
            |> check "could not end command buffer"
        recording <- false

    member x.AppendCommand() =
        if not recording then failf "cannot enqueue commands to non-recording CommandBuffer"
        commands <- commands + 1

    member x.Set(event: Event, stageMask: VkPipelineStageFlags) =
        x.AppendCommand()
        VkRaw.vkCmdSetEvent(handle, event.Handle, stageMask)

    member x.Reset(event: Event, stageMask: VkPipelineStageFlags) =
        x.AppendCommand()
        VkRaw.vkCmdResetEvent(handle, event.Handle, stageMask)

    member x.WaitAll(events: Event[], [<Optional; DefaultParameterValue(VkPipelineStageFlags.AllCommandsBit)>] dstStageFlags: VkPipelineStageFlags) =
        x.AppendCommand()
        let pEvents = events |> NativePtr.stackUseArr _.Handle
        VkRaw.vkCmdWaitEvents(
            handle, uint32 events.Length, pEvents,
            VkPipelineStageFlags.None, dstStageFlags,
            0u, NativePtr.zero,
            0u, NativePtr.zero,
            0u, NativePtr.zero
        )

    member x.IsEmpty = commands = 0
    member x.CommandCount = commands
    member x.IsRecording = recording
    member x.Level = level
    member x.Handle = handle
    member internal x.DeviceInterface = device
    member internal x.PoolInterface = pool

    member x.AddResource(resource: IResource) =
        if resources.Add(resource) then resource.AddReference()

    member x.AddResources(resources: seq<IResource>) =
        resources |> Seq.iter x.AddResource

    member x.AddCompensation(action: unit -> unit) =
        x.AddResource(
            { new IResource with
                member x.ReferenceCount = 1
                member x.AddReference() = ()
                member x.Dispose() = action() }
        )

    member x.AddCompensation(disposable: IDisposable) =
        x.AddResource(
            { new IResource with
                member x.ReferenceCount = 1
                member x.AddReference() = ()
                member x.Dispose() = disposable.Dispose() }
        )

    member x.Dispose() =
        if handle <> 0n && device.Handle <> 0n then
            releaseResources()

            removeFromPool x
            VkRaw.vkFreeCommandBuffers(device.Handle, pool.Handle, 1u, &&handle)
            handle <- 0n

    interface IDeviceObject with
        member x.DeviceInterface = x.DeviceInterface

    interface IDisposable with
        member x.Dispose() = x.Dispose()