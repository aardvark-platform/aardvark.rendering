namespace Aardvark.Base

open System
open System.Runtime.CompilerServices

/// Sync objects are used to synchronize tasks on one or multiple devices as well as
/// between the host and devices.
type ISync =
    inherit IDisposable

    /// Waits for the sync object to become signaled or the (optional) timeout to be reached.
    /// Returns whether it was signaled in time, if a timeout was specified.
    /// Returns true, otherwise.
    abstract member Wait : timeout : MicroTime option -> bool

    /// Resets the sync object to an unsignaled state.
    abstract member Reset : unit -> unit

    /// Returns whether the sync object is signaled.
    abstract member GetStatus : unit -> bool

[<AbstractClass; Sealed; Extension>]
type ISyncExtensions private() =

    /// Waits for the sync object to become signaled.
    [<Extension>]
    static member Wait(this : ISync) =
        this.Wait None |> ignore

    /// Waits for the sync object to become signaled or the timeout to be reached.
    /// Returns whether it was signaled in time.
    [<Extension>]
    static member Wait(this : ISync, timeout : MicroTime) =
        this.Wait <| Some timeout

    /// Waits for the sync object to become signaled or the timeout (in nanoseconds) to be reached.
    /// Returns whether it was signaled in time.
    [<Extension>]
    static member Wait(this : ISync, timeoutInNanoseconds : uint64) =
        this.Wait(MicroTime.FromNanoseconds <| int64 timeoutInNanoseconds)