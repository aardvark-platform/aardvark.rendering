namespace Aardvark.Rendering.Vulkan

open System

/// Records commands submits them to a device queue when disposed.
type DeviceToken internal (family: IDeviceQueueFamily, getCurrentQueue: unit -> DeviceQueueHandle, onDispose: IObservable<unit>) =
    let mutable currentBuffer : CommandBuffer voption = ValueNone

    let mutable refCount = 0

    do onDispose.Add (fun () ->
        match currentBuffer with
        | ValueSome b ->
            b.Pool.Dispose()
            currentBuffer <- ValueNone

        | _ -> ()
    )

    /// Gets the current command buffer or prepares one for recording.
    member x.CurrentBuffer =
        match currentBuffer with
        | ValueSome buffer ->
            if not buffer.IsRecording then
                buffer.Begin CommandBufferUsage.OneTimeSubmit

            buffer

        | _ ->
            let pool = family.CreateCommandPool CommandPoolFlags.Transient
            let buffer = pool.CreateCommandBuffer CommandBufferLevel.Primary

            currentBuffer <- ValueSome buffer
            buffer.Begin CommandBufferUsage.OneTimeSubmit
            buffer

    member internal x.DeviceInterface = family.DeviceInterface
    member internal x.FamilyInterface = family

    member inline private x.Flush(queue: DeviceQueue) =
        match currentBuffer with
        | ValueSome buffer when buffer.IsRecording ->
            buffer.End()
            queue.RunSynchronously buffer
            buffer.Pool.Reset()

        | _ -> ()

    /// Flushes any enqueued commands and waits for their completion.
    member x.Flush() =
        use h = getCurrentQueue()
        x.Flush h.Queue

    /// Flushes any enqueued commands and performs the given action on the current queue.
    member x.FlushAndPerform (action: DeviceQueue -> 'T) =
        use h = getCurrentQueue()
        x.Flush h.Queue
        action h.Queue

    /// Flushes any enqueued commands.
    member x.FlushAsync() =
        match currentBuffer with
        | ValueSome buffer when buffer.IsRecording ->
            currentBuffer <- ValueNone
            buffer.End()
            use h = getCurrentQueue()
            let task = h.Queue.StartTask buffer
            task.OnCompletion buffer.Pool.Dispose
            task

        | _ ->
            DeviceTask.Completed

    member inline x.AddCompensation(compensation: unit -> unit) =
        x.CurrentBuffer.AddCompensation(compensation)

    member inline x.AddCompensation(disposable: IDisposable) =
        x.CurrentBuffer.AddCompensation(disposable)

    member internal x.AddRef() =
        refCount <- refCount + 1

    member internal x.RemoveRef() =
        if refCount = 1 then x.Flush()
        else refCount <- refCount - 1

    member x.Dispose() =
        x.RemoveRef()

    interface IDeviceObject with
        member x.DeviceInterface = x.DeviceInterface

    interface IDisposable with
        member x.Dispose() = x.Dispose()