namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Collections.Concurrent

type DeviceQueueFamily private (device: IDevice, info: QueueFamilyInfo) =
    let currentQueue = new ThreadLocal<DeviceQueue voption>(fun () -> ValueNone)

    let mutable currentToken : ThreadLocal<DeviceToken> = null
    let mutable availableQueues : ConcurrentBag<DeviceQueue> = null
    let mutable availableQueueCount : SemaphoreSlim = null

    let supportedStages =
        let features = device.PhysicalDevice.Features.Shaders
        let mutable stages = info.flags |> VkPipelineStageFlags.ofQueueFlags

        if not features.GeometryShader then
            stages <- stages &&& (~~~VkPipelineStageFlags.GeometryShaderBit)

        if not features.TessellationShader then
            stages <- stages &&& (~~~VkPipelineStageFlags.TessellationControlShaderBit)
            stages <- stages &&& (~~~VkPipelineStageFlags.TessellationEvaluationShaderBit)

        stages

    let releaseQueueHandle (handle: DeviceQueueHandle) =
        currentQueue.Value <- ValueNone
        availableQueues.Add handle.Queue
        availableQueueCount.Release() |> ignore

    member private x.Initialize(onDispose: IObservable<unit>) =
        let queues = Array.init info.count (fun index -> new DeviceQueue(x, index))
        availableQueues <- ConcurrentBag<DeviceQueue>(queues)
        availableQueueCount <- new SemaphoreSlim(availableQueues.Count)
        let getCurrentQueue() = x.CurrentQueue
        currentToken <- new ThreadLocal<DeviceToken>(fun () -> new DeviceToken(x, getCurrentQueue, onDispose))

    static member internal Create(device: IDevice, info: QueueFamilyInfo, onDispose: IObservable<unit>) =
        let family = new DeviceQueueFamily(device, info)
        family.Initialize(onDispose)
        family

    member internal x.DeviceInterface = device
    member x.Info: QueueFamilyInfo = info
    member x.Index : int = info.index
    member x.Flags : QueueFlags = info.flags
    member x.Stages = supportedStages

    member x.RunSynchronously(buffer: CommandBuffer) =
        use h = x.CurrentQueue
        h.Queue.RunSynchronously(buffer)

    member x.StartTask(buffer: CommandBuffer) =
        use h = x.CurrentQueue
        h.Queue.StartTask(buffer)

    member x.CurrentQueue : DeviceQueueHandle =
        match currentQueue.Value with
        | ValueSome q -> new DeviceQueueHandle(q)
        | _ ->
            availableQueueCount.Wait()

            let queue =
                match availableQueues.TryTake() with
                | true, q -> q
                | _ -> failf "failed to get queue"

            currentQueue.Value <- ValueSome queue
            new DeviceQueueHandle(queue, releaseQueueHandle)

    member x.CurrentToken : DeviceToken =
        let token = currentToken.Value
        token.AddRef()
        token

    member x.Dispose() =
        currentToken.Dispose()
        currentQueue.Dispose()
        availableQueueCount.Dispose()
        for q in availableQueues do q.Dispose()

    interface IDeviceQueueFamily with
        member x.DeviceInterface = x.DeviceInterface
        member x.Info = x.Info

    interface IDisposable with
        member x.Dispose() = x.Dispose()

[<AutoOpen>]
module DeviceQueueFamilyExtensions =

    type DeviceQueue with
        member x.Family = x.FamilyInterface :?> DeviceQueueFamily

    type DeviceToken with
        member x.Family = x.FamilyInterface :?> DeviceQueueFamily

    type CommandPool with
        member x.QueueFamily = x.QueueFamilyInterface :?> DeviceQueueFamily

    type CommandBuffer with
        member inline x.QueueFamily = x.Pool.QueueFamily