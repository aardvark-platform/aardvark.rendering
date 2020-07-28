namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Collections.Generic
open Aardvark.Base
open Aardvark.Rendering.Vulkan

type MultiSemaphore internal(device : Device, count : int) =

    // Semaphores that are managed by the instance
    let semaphores = List<Semaphore>(count)

    // Unsignaled semaphores that can be signaled again.
    let unsignaled = Queue<Semaphore>()

    // Semaphores that can be waited on.
    let pending = Queue<Semaphore>()

    // Indicates whether a thread has already aquired a semaphore.
    let mutable acquired = new ThreadLocal<bool>(fun _ -> false)

    // Gets an unsignaled semaphore, creating a new one if necessary.
    let getUnsignaled() =
        if unsignaled.IsEmpty() then
            let sem = device.CreateSemaphore()
            semaphores.Add sem
            sem
        else
            unsignaled.Dequeue()

    /// Gets a semaphore to wait on.
    member x.WaitFor =
        lock x (fun _ ->
            while pending.Count = 0 || acquired.Value do
                Monitor.Wait x |> ignore

            acquired.Value <- true
            pending.Dequeue()
        )

    /// Gets semaphores to signal.
    member x.Signal =
        lock x (fun _ ->
            List.init count (ignore >> getUnsignaled)
        )

    /// Resets the pending semaphores and returns them.
    member x.Reset() =
        lock x (fun _ ->
            let rs = pending.ToArray()
            pending.Clear()
            rs |> List.ofArray
        )

    /// To be called after the semaphores were submitted to a queue.
    /// Semaphores that are waited for, become unsignaled.
    /// Semaphores that are to be signaled, become pending.
    member x.Submit(waitFor : Semaphore list, signal : Semaphore list) =
        lock x (fun _ ->
            waitFor |> List.iter unsignaled.Enqueue
            signal |> List.iter pending.Enqueue

            acquired.Dispose()
            acquired <- new ThreadLocal<bool>(fun _ -> false)

            Monitor.PulseAll x
        )

    /// Releases all semaphores.
    member x.Dispose() =
        lock x (fun _ ->
            semaphores |> Seq.iter (fun s -> s.Dispose())
            pending.Clear()
            acquired.Dispose()
        )

    interface IDisposable with
        member x.Dispose() = x.Dispose()


type Sync internal(device : Device, maxDeviceWaits : int) =

    // Event for CPU sync
    let event = new ManualResetEvent(false)

    // Semaphore for GPU sync
    let semaphore = new MultiSemaphore(device, maxDeviceWaits)

    /// The event to signal.
    member x.Event =
        event

    /// The internal semaphore.
    member x.Semaphore =
        semaphore

    /// Releases the fence and semaphores.
    member x.Dispose() =
        event.Dispose()
        semaphore.Dispose()

    /// Waits for the fence to be signaled.
    member x.Wait(timeout : MicroTime option) =
        match timeout with
        | Some t -> event.WaitOne(int t.TotalMilliseconds)
        | None -> event.WaitOne()

    /// Resets the event.
    member x.Reset() =
        event.Reset() |> ignore

    /// Returns the status of the fence
    member x.GetStatus() =
        event.WaitOne(0)

    interface ISync with

        member x.Dispose() = x.Dispose()

        member x.Wait(timeout) = x.Wait(timeout)

        member x.Reset() = x.Reset()

        member x.GetStatus() = x.GetStatus()

[<AutoOpen>]
module ``TaskSync Extensions`` =

    let private toVulkan (sync : ISync) =
        match sync with
        | :? Sync as s -> s
        | _ -> failwithf "unsupported sync: %A" sync

    let private unwrap (sync : ISync) =
        let s = toVulkan sync
        s.Semaphore, s.Event :> EventWaitHandle

    type TaskSync with

        member x.CommandSync =
            let signal, event =
                match x.Signal |> Option.map unwrap with
                | Some (x, y) -> Some x, Some y
                | _ -> None, None

            let waitFor =
                x.WaitFor
                |> List.ofSeq
                |> List.map (unwrap >> fst)

            let waitFor' =
                waitFor |> List.map (fun x -> x.WaitFor)

            let unsignaled, signal' =
                signal
                |> Option.map (fun x -> x.Reset(), x.Signal)
                |> Option.defaultValue ([], [])

            let device =
                { waitFor = waitFor' @ unsignaled; signal = signal' }

            { new ICommandSync with
                member y.Submit(f : SubmitInfo -> 'a) =
                    let result = f { device = device; event = event }

                    signal |> Option.iter (fun s -> s.Submit(unsignaled, signal'))
                    waitFor |> List.iter2 (fun x s -> s.Submit([x], [])) waitFor'

                    result

                member y.SubmitAsync(f : SubmitInfoAsync -> 'b) =
                    failwith ""
            }