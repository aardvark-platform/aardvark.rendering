namespace Aardvark.Base

/// Struct for specifying synchronization objects when submitting work to a device.
[<Struct>]
type TaskSync(waitFor : ISync seq, signal : ISync option) =

    new(waitFor : ISync seq, signal : ISync) =
        TaskSync(waitFor, Some signal)

    new(waitFor : ISync, signal : ISync) =
        TaskSync(Seq.singleton waitFor, Some signal)

    new(waitFor : ISync seq) =
        TaskSync(waitFor, None)

    new(signal : ISync option) =
        TaskSync(Seq.empty, signal)

    new(signal : ISync) =
        TaskSync(Seq.empty, Some signal)

    /// The device will wait for these sync object to be signaled before starting execution.
    member x.WaitFor = waitFor

    /// Sync objects to signal upon execution completion.
    member x.Signal = signal

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module TaskSync =

    let none =
        TaskSync([], None)

    let create (waitFor : ISync seq) (signal : ISync) =
        TaskSync(waitFor, Some signal)

    let wait (waitFor : ISync seq) =
        TaskSync(waitFor)

    let signal (signal : ISync) =
        TaskSync(signal)

    /// Splits the given TaskSync into an array of length count.
    /// The values of WaitFor and Signal of info are assigned to
    /// the first and last element respectively. The remaining elements are empty
    /// TaskSync structs.
    let sequential (count : int) (info : TaskSync) =

        let get = function
            | 0 when count = 1 ->
                info
            | x when x = count - 1 ->
                TaskSync(info.Signal)
            | 1 ->
                wait info.WaitFor
            | _ ->
                none

        Array.init count get