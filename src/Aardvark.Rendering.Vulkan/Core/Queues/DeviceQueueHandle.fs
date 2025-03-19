namespace Aardvark.Rendering.Vulkan

open System

type DeviceQueueHandle =
    struct
        val mutable private queue : DeviceQueue
        val mutable private release : (DeviceQueueHandle -> unit) voption

        internal new (queue: DeviceQueue) =
            { queue = queue; release = ValueNone }

        internal new (queue: DeviceQueue, release: DeviceQueueHandle -> unit) =
            { queue = queue; release = ValueSome release }

        member x.Queue = x.queue

        member x.Dispose() =
            match x.release with
            | ValueSome release ->
                release x
                x.release <- ValueNone
            | _ -> ()

        interface IDisposable with
            member x.Dispose() = x.Dispose()
    end