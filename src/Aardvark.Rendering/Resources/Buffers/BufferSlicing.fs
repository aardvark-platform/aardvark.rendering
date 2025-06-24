namespace Aardvark.Rendering

open System
open System.Runtime.CompilerServices
open Aardvark.Base

module private BufferInternals =

    let inline byteSize<'T> (count : int) = uint64 count * uint64 sizeof<'T>

    type BufferRange(buffer : IBackendBuffer, offset : uint64, sizeInBytes : uint64) =
        interface IBufferRange with
            member x.Buffer = buffer
            member x.Offset = offset
            member x.SizeInBytes = sizeInBytes

    type BufferRange<'T when 'T : unmanaged>(buffer : IBackendBuffer, origin : int, count : int) =
        inherit BufferRange(buffer, byteSize<'T> origin, byteSize<'T> count)

        interface IBufferVector<'T> with
            member x.Buffer = buffer
            member x.Origin = origin
            member x.Delta = 1
            member x.Count = count

        interface IBufferRange<'T> with
            member x.Buffer = buffer

    type BufferVector<'T when 'T : unmanaged>(buffer : IBackendBuffer, origin : int, delta : int, count : int) =
        interface IBufferVector<'T> with
            member x.Buffer = buffer
            member x.Origin = origin
            member x.Delta = delta
            member x.Count = count

    type Buffer<'T when 'T : unmanaged>(buffer : IBackendBuffer) =
        inherit BufferRange<'T>(buffer, 0, int (buffer.SizeInBytes / uint64 sizeof<'T>))

        interface IBuffer<'T> with
            member x.Dispose() = buffer.Dispose()


module private BufferSlicing =
    open BufferInternals

    let private argumentOutOfRange (message : string) =
        raise <| ArgumentException(message)

    let inline private checkRange (totalSize : 'T) (start : 'T) (size : 'T) =
        let zero = LanguagePrimitives.GenericZero<'T>
        if start < zero then argumentOutOfRange "[Buffer] start of subrange must not be negative."
        if size < zero then argumentOutOfRange "[Buffer] size of subrange must not be negative."

        if start + size > totalSize then
            let max = start + size - LanguagePrimitives.GenericOne<'T>
            argumentOutOfRange $"[Buffer] subrange [{start}, {max}] out of bounds (max size = {totalSize})."

    let range (offset : uint64) (sizeInBytes : uint64) (range : IBufferRange) =
        (offset, sizeInBytes) ||> checkRange range.SizeInBytes
        BufferRange(range.Buffer, range.Offset + offset, sizeInBytes) :> IBufferRange

    let elements (start : int) (count : int) (range : IBufferRange<'T>) =
        (start, count) ||> checkRange range.Count
        BufferRange<'T>(range.Buffer, range.Origin + start, count) :> IBufferRange<_>

    let subvector (start : int) (delta : int) (count : int) (vector : IBufferVector<'T>) =
        if start < 0 then argumentOutOfRange $"[Buffer] invalid negative offset: {start}"
        if start > vector.Count then argumentOutOfRange $"[Buffer] offset out of bounds: {start} (count: {vector.Count})"

        if count < 0 then argumentOutOfRange $"[Buffer] invalid negative count: {start}"
        if start + count > vector.Count then argumentOutOfRange $"[Buffer] range out of bounds: ({start}, {count}) (count: {vector.Count})"

        let origin = vector.Origin + start * vector.Delta
        let delta = vector.Delta * delta

        let sa = nativeint sizeof<'T>
        let firstByte = sa * nativeint origin
        let lastByte = sa * (nativeint origin + nativeint delta * nativeint (count - 1))
        if firstByte < 0n || firstByte >= nativeint vector.Buffer.SizeInBytes then argumentOutOfRange "[Buffer] range out of bounds"
        if lastByte < 0n || lastByte >= nativeint vector.Buffer.SizeInBytes then argumentOutOfRange "[Buffer] range out of bounds"

        BufferVector<'T>(vector.Buffer, origin, delta, count) :> IBufferVector<_>


[<AbstractClass; Sealed; Extension>]
type BufferSlicingExtensions private() =

    // ================================================================================================================
    // Subrange of untyped ranges
    // ================================================================================================================

    ///<summary>Gets a subrange of the given size starting at the given offset.</summary>
    ///<param name="range">The buffer range to subdivide.</param>
    ///<param name="offset">Offset (in bytes) at which the subrange starts.</param>
    ///<param name="sizeInBytes">Size (in bytes) of the subrange.</param>
    [<Extension>]
    static member Range(range : IBufferRange, offset : uint64, sizeInBytes : uint64) =
        range |> BufferSlicing.range offset sizeInBytes

    ///<summary>Gets a subrange starting at the given offset.</summary>
    ///<param name="range">The buffer range to subdivide.</param>
    ///<param name="offset">Offset (in bytes) at which the subrange starts.</param>
    [<Extension>]
    static member Range(range : IBufferRange, offset : uint64) =
        if offset > range.SizeInBytes then
            raise <| ArgumentException($"[Buffer] subrange start {offset} out of bounds (size = {range.SizeInBytes}).")

        range.Range(offset, range.SizeInBytes - offset)


    // ================================================================================================================
    // Subrange of typed ranges
    // ================================================================================================================

    ///<summary>Gets a subrange of the given count starting at the given index.</summary>
    ///<param name="range">The buffer range to subdivide.</param>
    ///<param name="start">Index at which the subrange starts.</param>
    ///<param name="count">Number of elements in the subrange.</param>
    [<Extension>]
    static member Elements(range : IBufferRange<'T>, start : int, count : int) =
        range |> BufferSlicing.elements start count

    ///<summary>Gets a subrange starting at the given index.</summary>
    ///<param name="range">The buffer range to subdivide.</param>
    ///<param name="start">Index at which the subrange starts.</param>
    [<Extension>]
    static member Elements(range : IBufferRange<'T>, start : int) =
        if start > range.Count then
            raise <| ArgumentException($"[Buffer] subrange start {start} out of bounds (size = {range.Count}).")

        range.Elements(start, range.Count - start)

    ///<summary>Gets a subrange from the start to end index.</summary>
    ///<param name="range">The buffer range to subdivide.</param>
    ///<param name="startIndex">Index at which the subrange starts. Default is 0.</param>
    ///<param name="endIndex">Index at which the subrange ends (inclusive). Default is <paramref name="range"/>.Count - 1.</param>
    [<Extension>]
    static member GetSlice(range : IBufferRange<'T>, startIndex : Option<int>, endIndex : Option<int>) =
        let min = defaultArg startIndex 0
        let max = defaultArg endIndex (range.Count - 1)

        if min > max then
            raise <| ArgumentException($"[Buffer] invalid subrange [{startIndex}, {endIndex}].")

        range.Elements(min, 1 + max - min)


    // ================================================================================================================
    // Subvectors of vectors
    // ================================================================================================================

    [<Extension>]
    static member SubVector(vector : IBufferVector<'T>, start : int, delta : int, count : int) =
        vector |> BufferSlicing.subvector start delta count

    [<Extension>]
    static member inline SubVector(vector : IBufferVector<'T>, offset : int, count : int) =
        vector.SubVector(offset, 1, count)

    [<Extension>]
    static member inline Skip(vector : IBufferVector<'T>, count : int) =
        let count = min (vector.Count - 1) count
        vector.SubVector(count, 1, max 0 (vector.Count - count))

    [<Extension>]
    static member inline Take(vector : IBufferVector<'T>, count : int) =
        vector.SubVector(0, 1, count)

    [<Extension>]
    static member inline Strided(vector : IBufferVector<'T>, delta : int) =
        let count = 1 + (vector.Count - 1) / delta
        vector.SubVector(0, delta, count)