namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive
open System
open OptimizedClosures
open System.Runtime.InteropServices
open System.Runtime.CompilerServices

module internal CompactBufferImplementation =

    type private AdaptiveValueWriter<'Key, 'Value when 'Value : unmanaged>(
                                     evaluate : FSharpFunc<AdaptiveToken, 'Key, 'Value>, stride : int, input : 'Key, index : int) =
        inherit AdaptiveObject()

        let mutable currentIndex = index

        member x.Write(token : AdaptiveToken, buffer : IAdaptiveBuffer) =
            x.EvaluateIfNeeded token () (fun token ->
                let offset = currentIndex * stride
                let value = evaluate.Invoke(token, input)
                buffer.Write(value, nativeint offset)
            )

        member x.SetIndex(index : int) =
            currentIndex <- index
            transact x.MarkOutdated

        member x.Dispose() =
            x.Outputs.Clear()

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    /// Buffer that holds the elements of a set in a sequential block of memory.
    type AdaptiveCompactBuffer<'Key, 'Value when ' Value : unmanaged>(
                               runtime : IBufferRuntime,
                               evaluate : AdaptiveToken -> 'Key -> 'Value,
                               acquire : 'Key -> unit,
                               release : 'Key -> unit,
                               input : aset<'Key>,
                               [<Optional; DefaultParameterValue(BufferUsage.Default)>] usage : BufferUsage,
                               [<Optional; DefaultParameterValue(0)>] stride : int,
                               [<Optional; DefaultParameterValue(1L)>] blockAlignment : int64,
                               [<Optional; DefaultParameterValue(1L)>] blockSize : int64) =
        inherit AdaptiveBufferImplementation.MappedAdaptiveBuffer(runtime, 0n, usage = usage, blockAlignment = blockAlignment, blockSize = blockSize)

        let stride = if stride = 0 then sizeof<'Value> else stride

        let reader = (ASet.compact input).GetReader()
        let writers = Dict<'Key, AdaptiveValueWriter<'Key, 'Value>>()
        let evaluate = FSharpFunc<_, _, _>.Adapt evaluate

        override x.Destroy() =
            reader.Outputs.Remove(x) |> ignore

            for KeyValue(input, writer) in writers do
                writer.Dispose()
                release input

            writers.Clear()
            base.Destroy()

        member x.MarkDirty(input : 'Key) =
            match writers.TryGetValue input with
            | (true, w) -> transact w.MarkOutdated
            | _ -> ()

        member private x.Set(input, index) =
            match writers.TryGetValue input with
            | (true, w) ->
                w.SetIndex(index)
            | _ ->
                acquire input
                let w = new AdaptiveValueWriter<'Key, 'Value>(evaluate, stride, input, index)
                writers.Add(input, w)

        member private x.Remove(input) =
            match writers.TryGetValue input with
            | (true, w) ->
                w.Dispose()
                writers.Remove(input) |> ignore
                release input
            | _ -> ()

        override x.Compute(t, rt) =
            let ops = reader.GetChanges(t)

            // Process deltas
            for o in ops do
                match o with
                | value, Set index ->
                    x.Set(value, index)

                | value, Remove ->
                    x.Remove(value)

            // Grow buffer if necessary
            x.Resize <| nativeint (writers.Count * stride)

            // Write values
            for (KeyValue(_, writer)) in writers do
                writer.Write(t, x)

            base.Compute(t, rt)

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
    /// <param name="stride">The number of bytes between the beginning of two successive elements, or 0 for a tightly packed layout. Default is 0.</param>
    /// <param name="blockAlignment">The block alignment of the buffer. Default is 1.</param>
    /// <param name="blockSize">The block size of the buffer. Default is 1.</param>
    [<Extension>]
    static member CreateCompactBuffer<'Key, 'Value when 'Value : unmanaged>(
                                      this : IBufferRuntime,
                                      evaluate : AdaptiveToken -> 'Key -> 'Value,
                                      acquire : 'Key -> unit,
                                      release : 'Key -> unit,
                                      input : aset<'Key>,
                                      [<Optional; DefaultParameterValue(BufferUsage.Default)>] usage : BufferUsage,
                                      [<Optional; DefaultParameterValue(0)>] stride : int,
                                      [<Optional; DefaultParameterValue(1L)>] blockAlignment : int64,
                                      [<Optional; DefaultParameterValue(1L)>] blockSize : int64) =
        CompactBufferImplementation.AdaptiveCompactBuffer<'Key, 'Value>(
            this, evaluate, acquire, release, input, usage, stride, blockAlignment, blockSize
        ) :> IAdaptiveBuffer

    /// <summary>
    /// Creates a buffer from a set of elements, maintaining a compact layout.
    /// </summary>
    /// <param name="this">The runtime.</param>
    /// <param name="input">The input element set.</param>
    /// <param name="usage">The usage flags of the buffer.</param>
    /// <param name="stride">The number of bytes between the beginning of two successive elements, or 0 for a tightly packed layout. Default is 0.</param>
    /// <param name="blockAlignment">The block alignment of the buffer. Default is 1.</param>
    /// <param name="blockSize">The block size of the buffer. Default is 1.</param>
    [<Extension>]
    static member CreateCompactBuffer<'T when 'T : unmanaged>(
                                      this : IBufferRuntime,
                                      input : aset<'T>,
                                      [<Optional; DefaultParameterValue(BufferUsage.Default)>] usage : BufferUsage,
                                      [<Optional; DefaultParameterValue(0)>] stride : int,
                                      [<Optional; DefaultParameterValue(1L)>] blockAlignment : int64,
                                      [<Optional; DefaultParameterValue(1L)>] blockSize : int64) =
        let evaluate _ = id
        this.CreateCompactBuffer(evaluate, ignore, ignore, input, usage, stride, blockAlignment, blockSize)

    /// <summary>
    /// Creates a buffer from a set of adaptive elements, maintaining a compact layout.
    /// </summary>
    /// <param name="this">The runtime.</param>
    /// <param name="input">The input element set.</param>
    /// <param name="usage">The usage flags of the buffer.</param>
    /// <param name="stride">The number of bytes between the beginning of two successive elements, or 0 for a tightly packed layout. Default is 0.</param>
    /// <param name="blockAlignment">The block alignment of the buffer. Default is 1.</param>
    /// <param name="blockSize">The block size of the buffer. Default is 1.</param>
    [<Extension>]
    static member CreateCompactBuffer<'T, 'U when 'T : unmanaged and 'U :> aval<'T>>(
                                      this : IBufferRuntime,
                                      input : aset<'U>,
                                      [<Optional; DefaultParameterValue(BufferUsage.Default)>] usage : BufferUsage,
                                      [<Optional; DefaultParameterValue(0)>] stride : int,
                                      [<Optional; DefaultParameterValue(1L)>] blockAlignment : int64,
                                      [<Optional; DefaultParameterValue(1L)>] blockSize : int64) =
        let evaluate t (x : 'U) = x.GetValue(t)
        let acquire (x : 'U) = x.Acquire()
        let release (x : 'U) = x.Release()
        this.CreateCompactBuffer(evaluate, acquire, release, input, usage, stride, blockAlignment, blockSize)