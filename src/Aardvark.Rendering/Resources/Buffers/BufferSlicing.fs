namespace Aardvark.Rendering

open System
open System.Runtime.CompilerServices
open Aardvark.Base

module internal BufferInternals =
    let inline byteSize<'T> (count : int) = uint64 count * uint64 sizeof<'T>

    let inline invalidArg (message : string) =
        raise <| ArgumentException $"[Buffer] {message}."

    let inline elementCount<'T> (buffer : IBackendBuffer) =
        let count = buffer.SizeInBytes / uint64 sizeof<'T>
        if count > uint64 Int32.MaxValue then invalidArg $"element count {count} exceeds the supported maximum of {Int32.MaxValue}"
        int count

    let inline checkRange (totalSize : uint64) (start : uint64) (size : uint64) =
        if start > totalSize || size > totalSize - start then
            invalidArg $"subrange out of bounds (start = {start}, size = {size}, total size = {totalSize})"

    let inline checkElements (totalCount : int) (start : int) (count : int) =
        if start < 0 then invalidArg $"invalid negative start index: {start}"
        if count < 0 then invalidArg $"invalid negative element count: {count}"
        if start > totalCount || count > totalCount - start then
            invalidArg $"element range out of bounds (start = {start}, count = {count}, total count = {totalCount})"

    let inline checkInt (name : string) (value : int64) =
        if value < int64 Int32.MinValue || value > int64 Int32.MaxValue then
            invalidArg $"{name} is not representable as a 32-bit integer: {value}"
        int value

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
        inherit BufferRange<'T>(buffer, 0, elementCount<'T> buffer)

        interface IBuffer<'T> with
            member x.Dispose() = buffer.Dispose()

open BufferInternals

[<AbstractClass; Sealed; Extension>]
type BufferSlicingExtensions private() =

    // ================================================================================================================
    // Subrange of untyped ranges
    // ================================================================================================================

    ///<summary>Gets a subrange of the given size starting at the given offset.</summary>
    ///<param name="range">The buffer range to subdivide.</param>
    ///<param name="offset">Offset (in bytes) at which the subrange starts.</param>
    ///<param name="sizeInBytes">Size (in bytes) of the subrange.</param>
    ///<returns>A view confined to the requested half-open byte range. Empty ranges may start at the end.</returns>
    ///<exception cref="ArgumentException">Thrown if the requested range exceeds the input range or its composed byte offset or endpoint is not representable.</exception>
    [<Extension>]
    static member Range(range : IBufferRange, offset : uint64, sizeInBytes : uint64) : IBufferRange =
        checkRange range.SizeInBytes offset sizeInBytes

        let origin = range.Offset
        if offset > UInt64.MaxValue - origin then
            invalidArg "subrange offset is not representable"

        let origin = origin + offset
        if sizeInBytes > UInt64.MaxValue - origin then
            invalidArg "subrange endpoint is not representable"

        BufferRange(range.Buffer, origin, sizeInBytes)

    ///<summary>Gets a subrange starting at the given offset.</summary>
    ///<param name="range">The buffer range to subdivide.</param>
    ///<param name="offset">Offset (in bytes) at which the subrange starts.</param>
    ///<returns>A view from <paramref name="offset"/> to the end of the input range.</returns>
    ///<exception cref="ArgumentException">Thrown if <paramref name="offset"/> exceeds the input range.</exception>
    [<Extension>]
    static member Range(range : IBufferRange, offset : uint64) =
        if offset > range.SizeInBytes then
            invalidArg $"subrange start {offset} out of bounds (size = {range.SizeInBytes})"

        range.Range(offset, range.SizeInBytes - offset)


    // ================================================================================================================
    // Subrange of typed ranges
    // ================================================================================================================

    ///<summary>Gets a subrange of the given count starting at the given index.</summary>
    ///<param name="range">The buffer range to subdivide.</param>
    ///<param name="start">Index at which the subrange starts.</param>
    ///<param name="count">Number of elements in the subrange.</param>
    ///<returns>A contiguous typed view. An empty view may start at <paramref name="range"/>.Count.</returns>
    ///<exception cref="ArgumentException">Thrown if the requested elements exceed the input range or their physical indices are not representable.</exception>
    [<Extension>]
    static member Elements(range : IBufferRange<'T>, start : int, count : int) : IBufferRange<_> =
        checkElements range.Count start count

        let origin = range.Origin
        if origin < 0 || start > Int32.MaxValue - origin then
            invalidArg $"element origin is not representable (origin = {origin}, start = {start})"

        let origin = origin + start
        if count > 0 && count - 1 > Int32.MaxValue - origin then
            invalidArg $"last element index is not representable (origin = {origin}, count = {count})"

        BufferRange<'T>(range.Buffer, origin, count)

    ///<summary>Gets a subrange starting at the given index.</summary>
    ///<param name="range">The buffer range to subdivide.</param>
    ///<param name="start">Index at which the subrange starts.</param>
    ///<returns>A contiguous typed view from <paramref name="start"/> to the end of the input range.</returns>
    ///<exception cref="ArgumentException">Thrown if <paramref name="start"/> is negative or exceeds the input range.</exception>
    [<Extension>]
    static member Elements(range : IBufferRange<'T>, start : int) =
        if start < 0 || start > range.Count then
            invalidArg $"subrange start {start} out of bounds (size = {range.Count})"

        range.Elements(start, range.Count - start)

    ///<summary>Gets a subrange from the start to end index.</summary>
    ///<param name="range">The buffer range to subdivide.</param>
    ///<param name="startIndex">Index at which the subrange starts. Default is 0.</param>
    ///<param name="endIndex">Index at which the subrange ends (inclusive). Default is <paramref name="range"/>.Count - 1.</param>
    ///<returns>The requested slice. The default slice of an empty range is empty.</returns>
    [<Extension>]
    static member GetSlice(range : IBufferRange<'T>, startIndex : Option<int>, endIndex : Option<int>) =
        if range.Count = 0 && Option.isNone startIndex && Option.isNone endIndex then
            range.Elements(0, 0)
        else
            let min = defaultArg startIndex 0
            let max = defaultArg endIndex (range.Count - 1)

            if min > max then
                invalidArg $"invalid subrange [{startIndex}, {endIndex}]"

            let count = int64 max - int64 min + 1L
            if count > int64 Int32.MaxValue then
                invalidArg $"subrange count is not representable: {count}"

            range.Elements(min, int count)


    // ================================================================================================================
    // Subvectors of vectors
    // ================================================================================================================

    ///<summary>Creates a vector view by selecting <paramref name="count"/> source elements.</summary>
    ///<param name="vector">The source vector.</param>
    ///<param name="start">Logical source index of the first selected element.</param>
    ///<param name="delta">Logical source-index increment between selected elements. Negative and zero values are supported.</param>
    ///<param name="count">Number of elements in the resulting vector.</param>
    ///<returns>A vector whose origin and delta are composed with those of the source vector. An empty vector may start one past the source end.</returns>
    ///<exception cref="ArgumentException">Thrown if a selected element is outside the source or its composed physical index or delta is not representable.</exception>
    [<Extension>]
    static member SubVector(vector : IBufferVector<'T>, start : int, delta : int, count : int) : IBufferVector<_> =
        if count < 0 then invalidArg $"invalid negative count: {count}"

        if count = 0 then
            if start < 0 || start > vector.Count then
                invalidArg $"empty vector start out of bounds: {start} (count: {vector.Count})"
        else
            if start < 0 || start >= vector.Count then
                invalidArg $"vector start out of bounds: {start} (count: {vector.Count})"

            let last = int64 start + int64 (count - 1) * int64 delta
            if last < 0L || last >= int64 vector.Count then
                invalidArg $"vector range out of bounds (start = {start}, delta = {delta}, count = {count}, source count = {vector.Count})"

        let origin = checkInt "vector origin" (int64 vector.Origin + int64 start * int64 vector.Delta)
        let delta = checkInt "vector delta" (int64 vector.Delta * int64 delta)

        if count > 0 then
            let last = int64 origin + int64 (count - 1) * int64 delta
            checkInt "last vector element" last |> ignore

        BufferVector<'T>(vector.Buffer, origin, delta, count)

    ///<summary>Creates a contiguous logical subvector.</summary>
    ///<param name="vector">The source vector.</param>
    ///<param name="offset">Logical source index at which the view starts.</param>
    ///<param name="count">Number of elements in the resulting vector.</param>
    [<Extension>]
    static member inline SubVector(vector : IBufferVector<'T>, offset : int, count : int) =
        vector.SubVector(offset, 1, count)

    ///<summary>Skips up to the requested number of logical elements.</summary>
    ///<param name="vector">The source vector.</param>
    ///<param name="count">Number of elements to skip. Values at or above the source count return an empty vector.</param>
    ///<returns>The remaining vector, or an empty vector positioned one past the source end.</returns>
    ///<exception cref="ArgumentException">Thrown if <paramref name="count"/> is negative or the empty result's composed metadata is not representable.</exception>
    [<Extension>]
    static member inline Skip(vector : IBufferVector<'T>, count : int) =
        if count < 0 then
            invalidArg $"skip count must not be negative: {count}"

        let start = min vector.Count count
        vector.SubVector(start, 1, vector.Count - start)

    ///<summary>Takes exactly the requested number of logical elements from the start.</summary>
    ///<param name="vector">The source vector.</param>
    ///<param name="count">Number of elements to take. Zero returns an empty vector.</param>
    ///<exception cref="ArgumentException">Thrown if <paramref name="count"/> is negative, exceeds the source count, or the result's composed metadata is not representable.</exception>
    [<Extension>]
    static member inline Take(vector : IBufferVector<'T>, count : int) =
        vector.SubVector(0, 1, count)

    ///<summary>Selects every <paramref name="delta"/>th element of the source vector.</summary>
    ///<param name="vector">The source vector.</param>
    ///<param name="delta">Positive logical source-index increment.</param>
    ///<returns>A strided vector. Striding an empty vector returns an empty vector.</returns>
    ///<exception cref="ArgumentException">Thrown if <paramref name="delta"/> is not positive or the result's composed metadata is not representable.</exception>
    [<Extension>]
    static member inline Strided(vector : IBufferVector<'T>, delta : int) =
        if delta <= 0 then
            invalidArg $"stride must be positive: {delta}"

        let count = if vector.Count = 0 then 0 else 1 + (vector.Count - 1) / delta
        vector.SubVector(0, delta, count)