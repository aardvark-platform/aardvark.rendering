namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open System
open System.Threading

/// Represents a running device operation that can be waited on.
type DeviceTask internal (fence: Fence) =
    let lockObj = obj()
    let mutable fence = fence
    let mutable onCompleted = if isNull fence then null else ResizeArray<unit -> unit>()

    let finalize() =
        for a in onCompleted do a()
        onCompleted <- null
        fence.Dispose()
        fence <- null

    static let completed = new DeviceTask(null)
    static member Completed = completed

    member x.IsCompleted =
        if Monitor.TryEnter lockObj then
            try
                if notNull fence && fence.Signaled then
                    finalize()
                    true
                else
                    false
            finally
                Monitor.Exit lockObj
        else
            false

    member x.Wait() =
        lock lockObj (fun _ ->
            if notNull fence then
                fence.Wait()
                finalize()
        )

    member x.OnCompletion(action: unit -> unit) =
        let completed =
            lock lockObj (fun _ ->
                if isNull fence then true
                else onCompleted.Add action; false
            )

        if completed then action()

    member x.Dispose() =
        x.Wait()

    interface IDisposable with
        member x.Dispose() = x.Dispose()