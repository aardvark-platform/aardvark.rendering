namespace Aardvark.Rendering

open Aardvark.Base

open System
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open BufferInternals

[<AbstractClass; Sealed; Extension>]
type IBackendBufferExtensions private() =

    ///<summary>Creates a typed view of the given buffer.</summary>
    ///<param name="buffer">The buffer to reinterpret.</param>
    ///<returns>A typed view of the original buffer.</returns>
    ///<exception cref="ArgumentException">Thrown if the number of complete elements cannot be represented by the typed buffer interface.</exception>
    [<Extension>]
    static member Coerce<'T when 'T : unmanaged>(buffer : IBackendBuffer) =
        match buffer with
        | :? IBuffer<'T> as typed ->
            if not (typed :? IValidatedBufferRange) then
                elementCount<'T> buffer |> ignore
            typed
        | _ -> new Buffer<'T>(buffer) :> IBuffer<'T>

    ///<summary>Copies data from host memory to a buffer.</summary>
    ///<param name="dst">The buffer to copy data to.</param>
    ///<param name="dstOffset">Offset (in bytes) into the buffer.</param>
    ///<param name="src">Location of the data to copy.</param>
    ///<param name="sizeInBytes">Number of bytes to copy.</param>
    ///<param name="discard">Indicates whether the current content of the buffer may be discarded. Default is <c>false</c>.</param>
    [<Extension>]
    static member inline Upload(dst : IBackendBuffer, dstOffset : uint64, src : nativeint, sizeInBytes : uint64,
                                [<Optional; DefaultParameterValue(false)>] discard : bool) =
        dst.Runtime.Upload(src, dst, dstOffset, sizeInBytes, discard)

    ///<summary>Copies data from a buffer to host memory.</summary>
    ///<param name="src">The buffer to copy data from.</param>
    ///<param name="srcOffset">Offset (in bytes) into the buffer.</param>
    ///<param name="dst">Location to copy the data to.</param>
    ///<param name="sizeInBytes">Number of bytes to copy.</param>
    [<Extension>]
    static member inline Download(src : IBackendBuffer, srcOffset : uint64, dst : nativeint, sizeInBytes : uint64) =
        src.Runtime.Download(src, srcOffset, dst, sizeInBytes)

    ///<summary>Copies data from a buffer to host memory.</summary>
    ///<param name="src">The buffer to copy data from.</param>
    ///<param name="srcOffset">Offset (in bytes) into the buffer.</param>
    ///<param name="dst">Location to copy the data to.</param>
    ///<param name="sizeInBytes">Number of bytes to copy.</param>
    ///<returns>A function that blocks until the download is complete.</returns>
    [<Extension>]
    static member inline DownloadAsync(src : IBackendBuffer, srcOffset : uint64, dst : nativeint, sizeInBytes : uint64) =
        src.Runtime.DownloadAsync(src, srcOffset, dst, sizeInBytes)

    ///<summary>Copies data from a buffer to another.</summary>
    ///<param name="src">The buffer to copy data from.</param>
    ///<param name="srcOffset">Offset (in bytes) into the source buffer.</param>
    ///<param name="dst">The buffer to copy data to.</param>
    ///<param name="dstOffset">Offset (in bytes) into the destination buffer.</param>
    ///<param name="sizeInBytes">Number of bytes to copy.</param>
    /// ///<param name="discard">Indicates whether the current content of the destination buffer may be discarded. Default is <c>false</c>.</param>
    [<Extension>]
    static member inline CopyTo(src : IBackendBuffer, srcOffset : uint64, dst : IBackendBuffer, dstOffset : uint64, sizeInBytes : uint64,
                                [<Optional; DefaultParameterValue(false)>] discard : bool) =
        src.Runtime.Copy(src, srcOffset, dst, dstOffset, sizeInBytes, discard)


module private BufferRangeValidation =

    let private fail (message : string) =
        raise <| ArgumentException($"[Buffer] {message}.")

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    let checkIndexRange (name : string) (totalCount : int) (start : int) (count : int) =
        if totalCount < 0 then fail $"{name} has an invalid negative count ({totalCount})"
        if start < 0 then fail $"{name} index must not be negative ({start})"
        if count < 0 then fail $"count must not be negative ({count})"
        if start > totalCount || count > totalCount - start then
            fail $"cannot access {name} range (start = {start}, count = {count}, total count = {totalCount})"

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    let checkBackingRange (range : IBufferRange) =
        if not (range :? IValidatedBufferRange) &&
           (range.Offset > range.Buffer.SizeInBytes || range.SizeInBytes > range.Buffer.SizeInBytes - range.Offset) then
            fail $"logical range exceeds its backing buffer (offset = {range.Offset}, size = {range.SizeInBytes}, buffer size = {range.Buffer.SizeInBytes})"

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    let getTransferOffset<'T when 'T : unmanaged> (range : IBufferRange<'T>) (index : int) (sizeInBytes : uint64) =
        let relativeOffset = byteSize<'T> index

        if relativeOffset > range.SizeInBytes || sizeInBytes > range.SizeInBytes - relativeOffset then
            fail $"element transfer exceeds logical byte range (index = {index}, size = {sizeInBytes}, range size = {range.SizeInBytes})"

        if relativeOffset > UInt64.MaxValue - range.Offset then
            fail $"absolute element offset is not representable (offset = {range.Offset}, index = {index})"

        range.Offset + relativeOffset


[<AbstractClass; Sealed; Extension>]
type IBufferRangeExtensions private() =

    ///<summary>Creates a typed view of the given buffer range.</summary>
    ///<param name="range">The buffer range to reinterpret.</param>
    ///<returns>A typed view of the original buffer range.</returns>
    ///<exception cref="ArgumentException">Thrown if the input range exceeds its backing buffer, its offset is unaligned, or its origin or element count is not representable.</exception>
    [<Extension>]
    static member CoerceRange<'T when 'T : unmanaged>(range : IBufferRange) =
        BufferRangeValidation.checkBackingRange range

        match range with
        | :? IBufferRange<'T> as range ->
            let origin = range.Origin
            let count = range.Count
            if origin < 0 || count < 0 || (count > 0 && count - 1 > Int32.MaxValue - origin) then
                raise <| ArgumentException($"[Buffer] typed range has invalid metadata (origin = {origin}, count = {count}).")
            range
        | _ ->
            if range.Offset % uint64 sizeof<'T> <> 0UL then
                raise <| ArgumentException($"[Buffer] offset must be a multiple of {sizeof<'T>}.")

            let origin = range.Offset / uint64 sizeof<'T>
            let count = range.SizeInBytes / uint64 sizeof<'T>

            if origin > uint64 Int32.MaxValue || count > uint64 Int32.MaxValue ||
               (count > 0UL && count - 1UL > uint64 Int32.MaxValue - origin) then
                raise <| ArgumentException($"[Buffer] typed range is not representable (origin = {origin}, count = {count}).")

            BufferRange<'T>(range.Buffer, int origin, int count) :> IBufferRange<'T>


    // ================================================================================================================
    // Upload
    // ================================================================================================================

    ///<summary>Copies data from host memory to the start of a logical buffer range.</summary>
    ///<param name="dst">The buffer range to copy data to.</param>
    ///<param name="src">Location of the data to copy.</param>
    ///<param name="sizeInBytes">Number of bytes to copy.</param>
    ///<param name="discard">Indicates whether the current content of the buffer may be discarded. Default is <c>false</c>.</param>
    ///<exception cref="ArgumentException">Thrown if the requested transfer exceeds the logical range or its backing buffer.</exception>
    [<Extension>]
    static member inline Upload(dst : IBufferRange, src : nativeint, sizeInBytes : uint64,
                                [<Optional; DefaultParameterValue(false)>] discard : bool) =
        let rangeSize = dst.SizeInBytes
        if sizeInBytes > rangeSize then
            raise <| ArgumentException($"[Buffer] transfer size {sizeInBytes} exceeds logical range size {rangeSize}.")
        if sizeInBytes > 0UL then
            dst.Buffer.Upload(dst.Offset, src, sizeInBytes, discard)
        elif dst.Offset > dst.Buffer.SizeInBytes then
            raise <| ArgumentException($"[Buffer] transfer offset {dst.Offset} exceeds backing buffer size {dst.Buffer.SizeInBytes}.")

    ///<summary>Copies elements from an array to a buffer range.</summary>
    ///<param name="dst">The buffer range to copy data to.</param>
    ///<param name="src">The data array to copy from.</param>
    ///<param name="srcIndex">Index at which copying from the data array begins.</param>
    ///<param name="dstIndex">Index at which copying to the buffer range begins.</param>
    ///<param name="count">Number of elements to copy.</param>
    ///<param name="discard">Indicates whether the current content of the buffer may be discarded. Default is <c>false</c>.</param>
    ///<exception cref="ArgumentException">Thrown if either the host-array or logical destination range is invalid. A zero-count copy may start at any position through the exact end.</exception>
    [<Extension>]
    static member Upload(dst : IBufferRange<'T>, src : 'T[], srcIndex : int, dstIndex : int, count : int,
                         [<Optional; DefaultParameterValue(false)>] discard : bool) =
        BufferRangeValidation.checkIndexRange "source array" src.Length srcIndex count
        BufferRangeValidation.checkIndexRange "destination range" dst.Count dstIndex count
        BufferRangeValidation.checkBackingRange dst
        let sizeInBytes = byteSize<'T> count
        let dstOffset = BufferRangeValidation.getTransferOffset dst dstIndex sizeInBytes

        if count > 0 then
            (srcIndex, src) ||> NativePtr.pinArri (fun pSrc ->
                dst.Buffer.Upload(dstOffset, pSrc.Address, sizeInBytes, discard)
            )

    ///<summary>Copies elements from an array to a buffer range.</summary>
    ///<param name="dst">The buffer range to copy data to.</param>
    ///<param name="src">The data array to copy from.</param>
    ///<param name="count">Number of elements to copy.</param>
    ///<param name="discard">Indicates whether the current content of the buffer may be discarded. Default is <c>false</c>.</param>
    [<Extension>]
    static member inline Upload(dst : IBufferRange<'T>, src : 'T[], count : int,
                                [<Optional; DefaultParameterValue(false)>] discard : bool) =
        dst.Upload(src, 0, 0, count, discard)

    ///<summary>Copies elements from an array to a buffer range.</summary>
    ///<param name="dst">The buffer range to copy data to.</param>
    ///<param name="src">The data array to copy from.</param>
    ///<param name="discard">Indicates whether the current content of the buffer may be discarded. Default is <c>false</c>.</param>
    [<Extension>]
    static member inline Upload(dst : IBufferRange<'T>, src : 'T[],
                                [<Optional; DefaultParameterValue(false)>] discard : bool) =
        dst.Upload(src, min src.Length dst.Count, discard)

    ///<summary>Copies elements from a span to a buffer range.</summary>
    ///<param name="dst">The buffer range to copy data to.</param>
    ///<param name="src">The data span to copy from.</param>
    ///<param name="discard">Indicates whether the current content of the buffer may be discarded. Default is <c>false</c>.</param>
    [<Extension>]
    static member Upload(dst : IBufferRange<'T>, src : Span<'T>,
                         [<Optional; DefaultParameterValue(false)>] discard : bool) =
        let count = min src.Length dst.Count
        SpanPinning.Pin(src, fun pSrc ->
            dst.Upload(pSrc, byteSize<'T> count, discard)
        )

    ///<summary>Copies elements from an array to a buffer subrange.</summary>
    ///<param name="range">The buffer range to upload to.</param>
    ///<param name="startIndex">Index at which the subrange starts. Default is 0.</param>
    ///<param name="endIndex">Index at which the subrange ends (inclusive). Default is <paramref name="range"/>.Count - 1.</param>
    ///<param name="data">The data array to copy from.</param>
    [<Extension>]
    static member inline SetSlice(range : IBufferRange<'T>, startIndex : Option<int>, endIndex : Option<int>, data : 'T[]) =
        let slice = range.GetSlice(startIndex, endIndex)
        slice.Upload(data, 0, 0, min data.Length slice.Count)

    ///<summary>Sets all elements of a buffer subrange to the given value.</summary>
    ///<param name="range">The buffer range to upload to.</param>
    ///<param name="startIndex">Index at which the subrange starts. Default is 0.</param>
    ///<param name="endIndex">Index at which the subrange ends (inclusive). Default is <paramref name="range"/>.Count - 1.</param>
    ///<param name="value">The value to fill the subrange with.</param>
    [<Extension>]
    static member inline SetSlice(range : IBufferRange<'T>, startIndex : Option<int>, endIndex : Option<int>, value : 'T) =
        let slice = range.GetSlice(startIndex, endIndex)
        slice.Upload(Array.create (int slice.Count) value)

    // ================================================================================================================
    // Download
    // ================================================================================================================

    ///<summary>Copies data from the start of a logical buffer range to host memory.</summary>
    ///<param name="src">The buffer range to copy data from.</param>
    ///<param name="dst">Location to copy the data to.</param>
    ///<param name="sizeInBytes">Number of bytes to copy.</param>
    ///<exception cref="ArgumentException">Thrown if the requested transfer exceeds the logical range or its backing buffer.</exception>
    [<Extension>]
    static member inline Download(src : IBufferRange, dst : nativeint, sizeInBytes : uint64) =
        let rangeSize = src.SizeInBytes
        if sizeInBytes > rangeSize then
            raise <| ArgumentException($"[Buffer] transfer size {sizeInBytes} exceeds logical range size {rangeSize}.")
        if sizeInBytes > 0UL then
            src.Buffer.Download(src.Offset, dst, sizeInBytes)
        elif src.Offset > src.Buffer.SizeInBytes then
            raise <| ArgumentException($"[Buffer] transfer offset {src.Offset} exceeds backing buffer size {src.Buffer.SizeInBytes}.")

    ///<summary>Copies elements from a buffer range to an array.</summary>
    ///<param name="src">The buffer range to copy data from.</param>
    ///<param name="dst">The data array to copy to.</param>
    ///<param name="srcIndex">Index at which copying from the buffer range begins.</param>
    ///<param name="dstIndex">Index at which copying to the data array begins.</param>
    ///<param name="count">Number of elements to copy.</param>
    ///<exception cref="ArgumentException">Thrown if either the logical source or host-array range is invalid. A zero-count copy may start at any position through the exact end.</exception>
    [<Extension>]
    static member Download(src : IBufferRange<'T>, dst : 'T[], srcIndex : int, dstIndex : int, count : int) =
        BufferRangeValidation.checkIndexRange "source range" src.Count srcIndex count
        BufferRangeValidation.checkIndexRange "destination array" dst.Length dstIndex count
        BufferRangeValidation.checkBackingRange src
        let sizeInBytes = byteSize<'T> count
        let srcOffset = BufferRangeValidation.getTransferOffset src srcIndex sizeInBytes

        if count > 0 then
            (dstIndex, dst) ||> NativePtr.pinArri (fun pDst ->
                src.Buffer.Download(srcOffset, pDst.Address, sizeInBytes)
            )

    ///<summary>Copies elements from a buffer range to an array.</summary>
    ///<param name="src">The buffer range to copy data from.</param>
    ///<param name="dst">The data array to copy to.</param>
    ///<param name="count">Number of elements to copy.</param>
    [<Extension>]
    static member inline Download(src : IBufferRange<'T>, dst : 'T[], count : int) =
        src.Download(dst, 0, 0, count)

    ///<summary>Copies elements from a buffer range to an array.</summary>
    ///<param name="src">The buffer range to copy data from.</param>
    ///<param name="dst">The data array to copy to.</param>
    [<Extension>]
    static member inline Download(src : IBufferRange<'T>, dst : 'T[]) =
        src.Download(dst, min src.Count dst.Length)

    ///<summary>Copies elements from a buffer range to an array.</summary>
    ///<param name="src">The buffer range to copy data from.</param>
    [<Extension>]
    static member Download(src : IBufferRange<'T>) =
        let dst = Array.zeroCreate<'T> src.Count
        src.Download(dst)
        dst

    ///<summary>Copies elements from a buffer range to a span.</summary>
    ///<param name="src">The buffer range to copy data from.</param>
    ///<param name="dst">The data span to copy to.</param>
    [<Extension>]
    static member Download(src : IBufferRange<'T>, dst : Span<'T>) =
        let count = min src.Count dst.Length
        SpanPinning.Pin(dst, fun pDst ->
            src.Download(pDst, byteSize<'T> count)
        )

    // ================================================================================================================
    // DownloadAsync
    // ================================================================================================================

    ///<summary>Asynchronously copies data from the start of a logical buffer range to host memory.</summary>
    ///<param name="src">The buffer range to copy data from.</param>
    ///<param name="dst">Location to copy the data to.</param>
    ///<param name="sizeInBytes">Number of bytes to copy.</param>
    ///<returns>A function that blocks until the download is complete.</returns>
    ///<exception cref="ArgumentException">Thrown if the requested transfer exceeds the logical range or its backing buffer.</exception>
    [<Extension>]
    static member inline DownloadAsync(src : IBufferRange, dst : nativeint, sizeInBytes : uint64) =
        let rangeSize = src.SizeInBytes
        if sizeInBytes > rangeSize then
            raise <| ArgumentException($"[Buffer] transfer size {sizeInBytes} exceeds logical range size {rangeSize}.")
        if sizeInBytes > 0UL then
            src.Buffer.DownloadAsync(src.Offset, dst, sizeInBytes)
        else
            if src.Offset > src.Buffer.SizeInBytes then
                raise <| ArgumentException($"[Buffer] transfer offset {src.Offset} exceeds backing buffer size {src.Buffer.SizeInBytes}.")
            id

    ///<summary>Asynchronously copies elements from a buffer range to an array.</summary>
    ///<param name="src">The buffer range to copy data from.</param>
    ///<param name="dst">The data array to copy to.</param>
    ///<param name="srcIndex">Index at which copying from the buffer range begins.</param>
    ///<param name="dstIndex">Index at which copying to the data array begins.</param>
    ///<param name="count">Number of elements to copy.</param>
    ///<returns>A function that blocks until the download is complete.</returns>
    ///<exception cref="ArgumentException">Thrown if either the logical source or host-array range is invalid. A zero-count copy may start at any position through the exact end.</exception>
    [<Extension>]
    static member DownloadAsync(src : IBufferRange<'T>, dst : 'T[], srcIndex : int, dstIndex : int, count : int) =
        BufferRangeValidation.checkIndexRange "source range" src.Count srcIndex count
        BufferRangeValidation.checkIndexRange "destination array" dst.Length dstIndex count
        BufferRangeValidation.checkBackingRange src
        let sizeInBytes = byteSize<'T> count
        let srcOffset = BufferRangeValidation.getTransferOffset src srcIndex sizeInBytes

        if count > 0 then
            (dstIndex, dst) ||> NativePtr.pinArri (fun pDst ->
                src.Buffer.DownloadAsync(srcOffset, pDst.Address, sizeInBytes)
            )
        else
            id

    ///<summary>Asynchronously copies elements from a buffer range to an array.</summary>
    ///<param name="src">The buffer range to copy data from.</param>
    ///<param name="dst">The data array to copy to.</param>
    ///<param name="count">Number of elements to copy.</param>
    ///<returns>A function that blocks until the download is complete.</returns>
    [<Extension>]
    static member inline DownloadAsync(src : IBufferRange<'T>, dst : 'T[], count : int) =
        src.DownloadAsync(dst, 0, 0, count)

    ///<summary>Asynchronously copies elements from a buffer range to an array.</summary>
    ///<param name="src">The buffer range to copy data from.</param>
    ///<param name="dst">The data array to copy to.</param>
    ///<returns>A function that blocks until the download is complete.</returns>
    [<Extension>]
    static member inline DownloadAsync(src : IBufferRange<'T>, dst : 'T[]) =
        src.DownloadAsync(dst, min src.Count dst.Length)

    ///<summary>Asynchronously copies elements from a buffer range to an array.</summary>
    ///<param name="src">The buffer range to copy data from.</param>
    ///<returns>A function that blocks until the download is complete.</returns>
    [<Extension>]
    static member DownloadAsync(src : IBufferRange<'T>) =
        let dst = Array.zeroCreate<'T> (int src.Count)
        let wait = src.DownloadAsync(dst)
        fun () -> wait(); dst

    ///<summary>Asynchronously copies elements from a buffer range to a span.</summary>
    ///<param name="src">The buffer range to copy data from.</param>
    ///<param name="dst">The data span to copy to.</param>
    ///<returns>A function that blocks until the download is complete.</returns>
    [<Extension>]
    static member DownloadAsync(src : IBufferRange<'T>, dst : Span<'T>) =
        let count = min src.Count dst.Length

        let wait : Func<unit, unit> =
            SpanPinning.Pin(dst, fun pDst ->
                src.DownloadAsync(pDst, byteSize<'T> count)
            )

        wait.Invoke

    // ================================================================================================================
    // CopyTo
    // ================================================================================================================

    ///<summary>
    /// Copies data from a buffer range to another.
    /// </summary>
    ///<param name="src">The buffer range to copy data from.</param>
    ///<param name="dst">The buffer range to copy data to.</param>
    ///<param name="discard">Indicates whether the current content of the destination buffer may be discarded. Default is <c>false</c>.</param>
    [<Extension>]
    static member inline CopyTo(src : IBufferRange, dst : IBufferRange,
                                [<Optional; DefaultParameterValue(false)>] discard : bool) =
        src.Buffer.CopyTo(src.Offset, dst.Buffer, dst.Offset, min src.SizeInBytes dst.SizeInBytes, discard)

    ///<summary>Copies elements from a buffer range to a buffer subrange.</summary>
    ///<param name="range">The buffer range to upload to.</param>
    ///<param name="startIndex">Index at which the subrange starts. Default is 0.</param>
    ///<param name="endIndex">Index at which the subrange ends (inclusive). Default is <paramref name="range"/>.Count - 1.</param>
    ///<param name="other">The buffer range to copy from.</param>
    [<Extension>]
    static member inline SetSlice(range : IBufferRange<'T>, startIndex : Option<int>, endIndex : Option<int>, other : IBufferRange<'T>) =
        let slice = range.GetSlice(startIndex, endIndex)
        other.CopyTo(slice)


[<AbstractClass; Sealed; Extension>]
type IBufferExtensions private() =

    static let getAvailableCount (elementSize: uint64) (stride: uint64) (offset: uint64) (totalSize: uint64) =
        let availableSize = if offset <= totalSize then totalSize - offset else 0UL
        if availableSize >= elementSize then 1UL + (availableSize - elementSize) / stride else 0UL

    static let copyToArray (elementType: Type) (elementSize: uint64) (stride: uint64) (offset: uint64) (count: uint64) (src: nativeint) =
        let array = Array.CreateInstance(elementType, int64 count)
        let sizeInBytes = count * elementSize
        let src = src + nativeint offset

        if stride = elementSize then
            array |> NativeInt.pin (fun dst ->
                Buffer.MemoryCopy(src, dst, sizeInBytes, sizeInBytes)
            )
        else
            array |> NativeInt.pin (fun dst ->
                let mutable src = src
                let mutable dst = dst
                let mutable i = 0UL

                while i < count do
                    Buffer.MemoryCopy(src, dst, elementSize, elementSize)
                    &src += nativeint stride
                    &dst += nativeint elementSize
                    &i += 1UL
            )

        array

    ///<summary>Returns elements of a buffer as an array.</summary>
    ///<param name="buffer">The buffer from which to retrieve elements.</param>
    ///<param name="elementType">The element type of the buffer and resulting array.</param>
    ///<param name="count">The maximum number of elements to retrieve. Default is UInt64.MaxValue.</param>
    ///<param name="offset">Offset (in bytes) into the buffer. Default is 0.</param>
    ///<param name="stride">Number of bytes between elements or 0 for tightly packed data. Default is 0.</param>
    ///<returns>An array containing the buffer elements.</returns>
    [<Extension>]
    static member ToArray(buffer: IBuffer, elementType: Type,
                          [<Optional; DefaultParameterValue(UInt64.MaxValue)>] count: uint64,
                          [<Optional; DefaultParameterValue(0UL)>] offset: uint64,
                          [<Optional; DefaultParameterValue(0UL)>] stride: uint64) : Array =
        let elementSize = uint64 elementType.CLRSize
        let stride = if stride = 0UL then elementSize else stride

        match buffer with
        | :? ArrayBuffer as buffer when buffer.ElementType = elementType ->
            let totalSize = uint64 buffer.Data.Length * elementSize
            let available = getAvailableCount elementSize stride offset totalSize
            let count = min available count

            if count = uint64 buffer.Data.Length && offset = 0UL && stride = elementSize then
                buffer.Data
            else
                if stride = elementSize && offset % elementSize = 0UL then
                    let result = Array.CreateInstance(elementType, int64 count)
                    Array.Copy(buffer.Data, int64 (offset / elementSize), result, 0L, int64 count)
                    result
                else
                    buffer.Data |> NativeInt.pin (copyToArray elementType elementSize stride offset count)

        | :? INativeBuffer as buffer ->
            let available = getAvailableCount elementSize stride offset buffer.SizeInBytes
            let count = min available count
            buffer.Use(copyToArray elementType elementSize stride offset count)

        | :? IBackendBuffer as buffer ->
            let available = getAvailableCount elementSize stride offset buffer.SizeInBytes
            let count = min available count

            if stride = elementSize then
                let result = Array.CreateInstance(elementType, int64 count)
                result |> NativeInt.pin (fun dst ->
                    buffer.Download(offset, dst, count * elementSize)
                )
                result
            else
                let copySize = if count = 0UL then 0UL else stride * (count - 1UL) + elementSize
                let tmp = Marshal.AllocHGlobal(nativeint copySize)
                try
                    buffer.Download(offset, tmp, copySize)
                    copyToArray elementType elementSize stride 0UL count tmp
                finally
                    Marshal.FreeHGlobal tmp

        | _ ->
            raise <| NotSupportedException($"Cannot retrieve elements of {buffer.GetType()}.")

    ///<summary>Returns elements of a buffer as an array.</summary>
    ///<param name="buffer">The buffer from which to retrieve elements.</param>
    ///<param name="count">The maximum number of elements to retrieve. Default is UInt64.MaxValue.</param>
    ///<param name="offset">Offset (in bytes) into the buffer. Default is 0.</param>
    ///<param name="stride">Number of bytes between elements or 0 for tightly packed data. Default is 0.</param>
    ///<returns>An array containing the buffer elements.</returns>
    [<Extension>]
    static member inline ToArray<'T when 'T : unmanaged>(buffer: IBuffer,
                                                         [<Optional; DefaultParameterValue(UInt64.MaxValue)>] count: uint64,
                                                         [<Optional; DefaultParameterValue(0UL)>] offset: uint64,
                                                         [<Optional; DefaultParameterValue(0UL)>] stride: uint64) : 'T[] =
        buffer.ToArray(typeof<'T>, count, offset, stride) :?> 'T[]


[<AbstractClass; Sealed; Extension>]
type IBufferRuntimeExtensions private() =

    /// Deletes a buffer and releases all GPU resources and API handles.
    [<Extension>]
    static member inline DeleteBuffer(_this : IBufferRuntime, buffer : IBackendBuffer) =
        buffer.Dispose()

    ///<summary>Creates a typed buffer.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="count">The number of elements in the buffer.</param>
    ///<param name="usage">The usage flags of the buffer. Default is BufferUsage.All.</param>
    ///<param name="storage">The type of storage that is preferred. Default is BufferStorage.Device.</param>
    [<Extension>]
    static member CreateBuffer<'T when 'T : unmanaged>(this : IBufferRuntime, count : int,
                                                       [<Optional; DefaultParameterValue(BufferUsage.All)>] usage : BufferUsage,
                                                       [<Optional; DefaultParameterValue(BufferStorage.Device)>] storage : BufferStorage) =
        let buffer = this.CreateBuffer(byteSize<'T> count, usage, storage)
        new Buffer<'T>(buffer) :> IBuffer<'T>

    ///<summary>Creates a typed buffer from the given array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="data">The data elements to upload to the buffer.</param>
    ///<param name="usage">The usage flags of the buffer. Default is BufferUsage.All.</param>
    ///<param name="storage">The type of storage that is preferred. Default is BufferStorage.Device.</param>
    [<Extension>]
    static member CreateBuffer(this : IBufferRuntime, data : 'T[],
                               [<Optional; DefaultParameterValue(BufferUsage.All)>] usage : BufferUsage,
                               [<Optional; DefaultParameterValue(BufferStorage.Device)>] storage : BufferStorage) =
        let buffer = this.CreateBuffer<'T>(data.Length, usage, storage)
        buffer.Upload(data)
        buffer

    ///<summary>Creates a typed buffer from the given span.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="data">The data elements to upload to the buffer.</param>
    ///<param name="usage">The usage flags of the buffer. Default is BufferUsage.All.</param>
    ///<param name="storage">The type of storage that is preferred. Default is BufferStorage.Device.</param>
    [<Extension>]
    static member CreateBuffer(this : IBufferRuntime, data : Span<'T>,
                               [<Optional; DefaultParameterValue(BufferUsage.All)>] usage : BufferUsage,
                               [<Optional; DefaultParameterValue(BufferStorage.Device)>] storage : BufferStorage) =
        let buffer = this.CreateBuffer<'T>(data.Length, usage, storage)
        buffer.Upload(data)
        buffer

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Buffer =

    /// Creates a typed buffer with the given length.
    let inline create (runtime : IBufferRuntime) (count : int) =
        runtime.CreateBuffer<'T>(count)

    /// Creates a typed buffer of the given array.
    let inline ofArray (runtime : IBufferRuntime) (data : 'T[]) =
        runtime.CreateBuffer<'T>(data)

    /// Creates a typed buffer of the given span.
    let inline ofSpan (runtime : IBufferRuntime) (data : Span<'T>) =
        runtime.CreateBuffer<'T>(data)

    /// Deletes the given buffer.
    let inline delete (b : IBackendBuffer) =
        b.Dispose()

    /// Uploads data from an array to the given buffer range.
    let inline upload (data : 'T[]) (range : IBufferRange<'T>) =
        range.Upload(data)

    /// Downloads data from the given buffer range.
    let inline download (range : IBufferRange<'T>) =
        range.Download()

    /// Copies data from one buffer range to another.
    let inline copy (src : IBufferRange) (dst : IBufferRange) =
        src.CopyTo dst
