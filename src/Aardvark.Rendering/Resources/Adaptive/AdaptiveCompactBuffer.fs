namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive
open System
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Collections.Generic
open OptimizedClosures

type ICompactBuffer =
    inherit IAdaptiveBuffer

    /// The number of elements in the input set
    abstract member Count : aval<int>

module internal CompactBufferImplementation =

    [<AbstractClass>]
    type AbstractCompactBuffer<'Key, 'Value>(runtime : IBufferRuntime, input : aset<'Key>, usage : BufferUsage, storage : BufferStorage) =
        inherit AdaptiveBuffer(runtime, 0n, usage, storage)

        static let elementSize = nativeint sizeof<'Value>

        let count = ASet.count input
        let compact = ASet.compact input
        let reader = compact.GetReader()

        member inline private x.Transact =
            if x.ProcessDeltasInTransaction then transact
            else (fun ([<InlineIfLambda>] g) -> g())

        member inline private x.GetAlignedSize(count : int) =
            let count = nativeint <| Fun.NextPowerOfTwo(int64 count)
            count * elementSize

        /// Indicates whether a transaction is used when processing deltas.
        abstract member ProcessDeltasInTransaction : bool
        default x.ProcessDeltasInTransaction = false

        /// Called when a new element is added or an old one is moved
        abstract member Set : input: 'Key * index: int -> unit

        /// Called when an element is removed.
        abstract member Remove : input: 'Key -> unit
        default x.Remove(_) = ()

        /// Called after deltas have been processed.
        abstract member Update : token: AdaptiveToken -> unit
        default x.Update(_) = ()

        override x.Compute(t, rt) =
            let ops = reader.GetChanges(t)

            // Grow or shrink buffer if necessary
            let count = count.GetValue(t)
            let alignedSize = x.GetAlignedSize count
            let requiredSize = nativeint count * elementSize

            if x.Size < requiredSize || x.Size > 2n * alignedSize then
                x.Resize alignedSize

            // Process deltas
            x.Transact (fun _ ->

                let removals = List(ops.Count)

                for o in ops do
                    match o with
                    | value, Set index ->
                        x.Set(value, index)

                    | value, Remove ->
                        removals.Add value

                // Process removals after sets to prevent potential reaquiring of resources
                for r in removals do
                    x.Remove r
            )

            x.Update(t)
            base.Compute(t, rt)

        interface ICompactBuffer with
            member x.Count = count

    type ConstantCompactBuffer<'Key, 'Value when 'Value : unmanaged>(
                                runtime : IBufferRuntime,
                                evaluate : 'Key -> 'Value,
                                input : aset<'Key>,
                                usage : BufferUsage,
                                storage : BufferStorage) =
        inherit AbstractCompactBuffer<'Key, 'Value>(runtime, input, usage, storage)

        let stride = sizeof<'Value>

        override x.Set(input, index) =
            let offset = index * stride
            let value = evaluate input
            x.Write(value, nativeint offset)

    type private Writer<'Key, 'Value when 'Value : unmanaged>(
                        buffer : AdaptiveCompactBuffer<'Key, 'Value>,
                        evaluate : FSharpFunc<AdaptiveToken, 'Key, 'Value>,
                        input : 'Key, index : int) =
        inherit AdaptiveObject()

        static let elementSize = nativeint sizeof<'Value>

        let mutable currentIndex = index

        member x.Write(token : AdaptiveToken) =
            if currentIndex >= 0 then
                x.EvaluateIfNeeded token () (fun token ->
                    let offset = nativeint currentIndex * elementSize
                    let value = evaluate.Invoke(token, input)
                    buffer.Write(value, offset)
                )

        member x.SetIndex(index : int) =
            currentIndex <- index
            x.MarkOutdated()

        member x.Dispose() =
            currentIndex <- -1
            x.Outputs.Clear()

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    and AdaptiveCompactBuffer<'Key, 'Value when ' Value : unmanaged>(
                               runtime : IBufferRuntime,
                               evaluate : AdaptiveToken -> 'Key -> 'Value,
                               acquire : 'Key -> unit,
                               release : 'Key -> unit,
                               input : aset<'Key>,
                               usage : BufferUsage,
                               storage : BufferStorage) =
        inherit AbstractCompactBuffer<'Key, 'Value>(runtime, input, usage, storage)

        let writers = Dict<'Key, Writer<'Key, 'Value>>()
        let pending = LockedSet<Writer<'Key, 'Value>>()

        let evaluate = FSharpFunc<_, _, _>.Adapt evaluate

        override x.Destroy() =
            for KeyValue(input, writer) in writers do
                writer.Dispose()
                release input

            pending.Clear()
            writers.Clear()
            base.Destroy()

        // SetIndex calls MarkOutdated which needs to happen in a transaction
        override x.ProcessDeltasInTransaction = true

        override x.Set(input, index) =
            match writers.TryGetValue input with
            | (true, w) ->
                w.SetIndex(index)
                pending.Add(w) |> ignore
            | _ ->
                acquire input
                let w = new Writer<'Key, 'Value>(x, evaluate, input, index)
                writers.Add(input, w)
                pending.Add(w) |> ignore

        override x.Remove(input) =
            match writers.TryGetValue input with
            | (true, w) ->
                w.Dispose()
                pending.Remove(w) |> ignore
                writers.Remove(input) |> ignore
                release input
            | _ -> ()

        override x.InputChangedObject(_, object) =
            match object with
            | :? Writer<'Key, 'Value> as w -> pending.Add w |> ignore
            | _ -> ()

        override x.Update(token : AdaptiveToken) =
            for w in pending.GetAndClear() do
                w.Write(token)

[<Extension; Sealed>]
type RuntimeAdaptiveCompactBufferExtensions private() =

    /// <summary>
    /// Creates a buffer from a set of elements, maintaining a compact layout.
    /// </summary>
    /// <param name="this">The runtime.</param>
    /// <param name="evaluate">The function that maps an element in the input set to a value.</param>
    /// <param name="acquire">The function called when an element is added to the input set.</param>
    /// <param name="release">The function called when an element is removed from the input set.</param>
    /// <param name="input">The input element set.</param>
    /// <param name="usage">The usage flags of the buffer.</param>
    /// <param name="storage">The type of storage that is preferred. Default is BufferStorage.Host.</param>
    [<Extension>]
    static member CreateCompactBuffer<'Key, 'Value when 'Value : unmanaged>(
                                      this : IBufferRuntime,
                                      evaluate : AdaptiveToken -> 'Key -> 'Value,
                                      acquire : 'Key -> unit,
                                      release : 'Key -> unit,
                                      input : aset<'Key>,
                                      [<Optional; DefaultParameterValue(BufferUsage.All)>] usage : BufferUsage,
                                      [<Optional; DefaultParameterValue(BufferStorage.Host)>] storage : BufferStorage) =
        CompactBufferImplementation.AdaptiveCompactBuffer<'Key, 'Value>(
            this, evaluate, acquire, release, input, usage, storage
        ) :> ICompactBuffer

    /// <summary>
    /// Creates a buffer from a set of elements, maintaining a compact layout.
    /// </summary>
    /// <param name="this">The runtime.</param>
    /// <param name="evaluate">The function that maps an element in the input set to a value.</param>
    /// <param name="input">The input element set.</param>
    /// <param name="usage">The usage flags of the buffer.</param>
    /// <param name="storage">The type of storage that is preferred. Default is BufferStorage.Host.</param>
    [<Extension>]
    static member CreateCompactBuffer<'Key, 'Value when 'Value : unmanaged>(
                                      this : IBufferRuntime,
                                      evaluate : 'Key -> 'Value,
                                      input : aset<'Key>,
                                      [<Optional; DefaultParameterValue(BufferUsage.All)>] usage : BufferUsage,
                                      [<Optional; DefaultParameterValue(BufferStorage.Host)>] storage : BufferStorage) =
        CompactBufferImplementation.ConstantCompactBuffer<'Key, 'Value>(
            this, evaluate, input, usage, storage
        ) :> ICompactBuffer

    /// <summary>
    /// Creates a buffer from a set of elements, maintaining a compact layout.
    /// </summary>
    /// <param name="this">The runtime.</param>
    /// <param name="input">The input element set.</param>
    /// <param name="usage">The usage flags of the buffer.</param>
    /// <param name="storage">The type of storage that is preferred. Default is BufferStorage.Host.</param>
    [<Extension>]
    static member CreateCompactBuffer<'T when 'T : unmanaged>(
                                      this : IBufferRuntime,
                                      input : aset<'T>,
                                      [<Optional; DefaultParameterValue(BufferUsage.All)>] usage : BufferUsage,
                                      [<Optional; DefaultParameterValue(BufferStorage.Host)>] storage : BufferStorage) =
        this.CreateCompactBuffer(id, input, usage, storage)

    /// <summary>
    /// Creates a buffer from a set of adaptive elements, maintaining a compact layout.
    /// </summary>
    /// <param name="this">The runtime.</param>
    /// <param name="input">The input element set.</param>
    /// <param name="usage">The usage flags of the buffer.</param>
    /// <param name="storage">The type of storage that is preferred. Default is BufferStorage.Host.</param>
    [<Extension>]
    static member CreateCompactBuffer<'T, 'U when 'T : unmanaged and 'U :> aval<'T>>(
                                      this : IBufferRuntime,
                                      input : aset<'U>,
                                      [<Optional; DefaultParameterValue(BufferUsage.All)>] usage : BufferUsage,
                                      [<Optional; DefaultParameterValue(BufferStorage.Host)>] storage : BufferStorage) =
        let evaluate t (x : 'U) = x.GetValue(t)
        let acquire (x : 'U) = x.Acquire()
        let release (x : 'U) = x.Release()
        this.CreateCompactBuffer(evaluate, acquire, release, input, usage, storage)