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
    [<Extension>]
    static member Coerce<'T when 'T : unmanaged>(buffer : IBackendBuffer) =
        match buffer with
        | :? IBuffer<'T> as buffer -> buffer
        | _ -> new Buffer<'T>(buffer) :> IBuffer<'T>

    ///<summary>Copies data from host memory to a buffer.</summary>
    ///<param name="dst">The buffer to copy data to.</param>
    ///<param name="dstOffset">Offset (in bytes) into the buffer.</param>
    ///<param name="src">Location of the data to copy.</param>
    ///<param name="sizeInBytes">Number of bytes to copy.</param>
    [<Extension>]
    static member inline Upload(dst : IBackendBuffer, dstOffset : nativeint, src : nativeint, sizeInBytes : nativeint) =
        dst.Runtime.Upload(src, dst, dstOffset, sizeInBytes)

    ///<summary>Copies data from a buffer to host memory.</summary>
    ///<param name="src">The buffer to copy data from.</param>
    ///<param name="srcOffset">Offset (in bytes) into the buffer.</param>
    ///<param name="dst">Location to copy the data to.</param>
    ///<param name="sizeInBytes">Number of bytes to copy.</param>
    [<Extension>]
    static member inline Download(src : IBackendBuffer, srcOffset : nativeint, dst : nativeint, sizeInBytes : nativeint) =
        src.Runtime.Download(src, srcOffset, dst, sizeInBytes)

    ///<summary>Copies data from a buffer to host memory.</summary>
    ///<param name="src">The buffer to copy data from.</param>
    ///<param name="srcOffset">Offset (in bytes) into the buffer.</param>
    ///<param name="dst">Location to copy the data to.</param>
    ///<param name="sizeInBytes">Number of bytes to copy.</param>
    ///<returns>A function that blocks until the download is complete.</returns>
    [<Extension>]
    static member inline DownloadAsync(src : IBackendBuffer, srcOffset : nativeint, dst : nativeint, sizeInBytes : nativeint) =
        src.Runtime.DownloadAsync(src, srcOffset, dst, sizeInBytes)

    ///<summary>Copies data from a buffer to another.</summary>
    ///<param name="src">The buffer to copy data from.</param>
    ///<param name="srcOffset">Offset (in bytes) into the source buffer.</param>
    ///<param name="dst">The buffer to copy data to.</param>
    ///<param name="dstOffset">Offset (in bytes) into the destination buffer.</param>
    ///<param name="sizeInBytes">Number of bytes to copy.</param>
    [<Extension>]
    static member inline CopyTo(src : IBackendBuffer, srcOffset : nativeint, dst : IBackendBuffer, dstOffset : nativeint, sizeInBytes : nativeint) =
        src.Runtime.Copy(src, srcOffset, dst, dstOffset, sizeInBytes)


[<AbstractClass; Sealed; Extension>]
type IBufferRangeExtensions private() =

    static let checkNonNegative (name : string) (value : int) =
        if value < 0 then
            raise <| ArgumentException($"[Buffer] {name} must not be negative.")

    static let checkArrayBounds (array : Array) (start : int) (length : int) =
        let last = start + length - 1

        if last >= array.Length then
            raise <| ArgumentException($"[Buffer] cannot access range [{start}, {last}] of array with length {array.LongLength}.")

    ///<summary>Creates a typed view of the given buffer range.</summary>
    ///<param name="range">The buffer range to reinterpret.</param>
    ///<returns>A typed view of the original buffer range.</returns>
    ///<exception cref="ArgumentException">Thrown if the offset of the input range is not a multiple of the element size of 'T.</exception>
    [<Extension>]
    static member CoerceRange<'T when 'T : unmanaged>(range : IBufferRange) =
        match range with
        | :? IBufferRange<'T> as range -> range
        | _ ->
            if range.Offset % nativeint sizeof<'T> <> 0n then
                raise <| ArgumentException($"[Buffer] offset must be a multiple of {sizeof<'T>}.")

            let origin = int (range.Offset / nativeint sizeof<'T>)
            let count = int (range.SizeInBytes / nativeint sizeof<'T>)
            let buffer = range.Buffer.Coerce<'T>()
            buffer |> BufferSlicing.elements origin count


    // ================================================================================================================
    // Upload
    // ================================================================================================================

    ///<summary>Copies data from host memory to a buffer range.</summary>
    ///<param name="dst">The buffer range to copy data to.</param>
    ///<param name="src">Location of the data to copy.</param>
    ///<param name="sizeInBytes">Number of bytes to copy.</param>
    [<Extension>]
    static member inline Upload(dst : IBufferRange, src : nativeint, sizeInBytes : nativeint) =
        dst.Buffer.Upload(dst.Offset, src, sizeInBytes)

    ///<summary>Copies elements from an array to a buffer range.</summary>
    ///<param name="dst">The buffer range to copy data to.</param>
    ///<param name="src">The data array to copy from.</param>
    ///<param name="srcIndex">Index at which copying from the data array begins.</param>
    ///<param name="dstIndex">Index at which copying to the buffer range begins.</param>
    ///<param name="count">Number of elements to copy.</param>
    [<Extension>]
    static member Upload(dst : IBufferRange<'T>, src : 'T[], srcIndex : int, dstIndex : int, count : int) =
        count |> checkNonNegative "count"
        srcIndex |> checkNonNegative "srcIndex"
        (srcIndex, count) ||> checkArrayBounds src

        pinned src (fun pSrc ->
            dst.Buffer.Upload(dst.Offset + byteSize<'T> dstIndex, pSrc + byteSize<'T> srcIndex, byteSize<'T> count)
        )

    ///<summary>Copies elements from an array to a buffer range.</summary>
    ///<param name="dst">The buffer range to copy data to.</param>
    ///<param name="src">The data array to copy from.</param>
    ///<param name="count">Number of elements to copy.</param>
    [<Extension>]
    static member inline Upload(dst : IBufferRange<'T>, src : 'T[], count : int) =
        dst.Upload(src, 0, 0, count)

    ///<summary>Copies elements from an array to a buffer range.</summary>
    ///<param name="dst">The buffer range to copy data to.</param>
    ///<param name="src">The data array to copy from.</param>
    [<Extension>]
    static member inline Upload(dst : IBufferRange<'T>, src : 'T[]) =
        dst.Upload(src, min src.Length dst.Count)

    ///<summary>Copies elements from a span to a buffer range.</summary>
    ///<param name="dst">The buffer range to copy data to.</param>
    ///<param name="src">The data span to copy from.</param>
    [<Extension>]
    static member Upload(dst : IBufferRange<'T>, src : Span<'T>) =
        let count = min src.Length dst.Count
        SpanPinning.Pin(src, fun pSrc ->
            dst.Upload(pSrc, byteSize<'T> count)
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

    [<Extension; Obsolete("Use range.Upload(src, srcIndex, dstIndex, count) instead.")>]
    static member Upload(range : IBufferRange<'T>, src : 'T[], dstIndex : int, count : int) =
        range.Upload(src, 0, dstIndex, count)

    // ================================================================================================================
    // Download
    // ================================================================================================================

    ///<summary>Copies data from a buffer range to host memory.</summary>
    ///<param name="src">The buffer range to copy data from.</param>
    ///<param name="dst">Location to copy the data to.</param>
    ///<param name="sizeInBytes">Number of bytes to copy.</param>
    [<Extension>]
    static member inline Download(src : IBufferRange, dst : nativeint, sizeInBytes : nativeint) =
        src.Buffer.Download(src.Offset, dst, sizeInBytes)

    ///<summary>Copies elements from a buffer range to an array.</summary>
    ///<param name="src">The buffer range to copy data from.</param>
    ///<param name="dst">The data array to copy to.</param>
    ///<param name="srcIndex">Index at which copying from the buffer range begins.</param>
    ///<param name="dstIndex">Index at which copying to the data array begins.</param>
    ///<param name="count">Number of elements to copy.</param>
    [<Extension>]
    static member Download(src : IBufferRange<'T>, dst : 'T[], srcIndex : int, dstIndex : int, count : int) =
        count |> checkNonNegative "count"
        dstIndex |> checkNonNegative "dstIndex"
        (dstIndex, count) ||> checkArrayBounds dst

        pinned dst (fun pDst ->
            src.Buffer.Download(src.Offset + byteSize<'T> srcIndex, pDst + byteSize<'T> dstIndex, byteSize<'T> count)
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

    [<Extension; Obsolete("Use range.Download(dst, srcIndex, dstIndex, count) instead.")>]
    static member Download(src : IBufferRange<'T>, srcIndex : int, dst : 'T[], dstIndex : int, count : int) =
        src.Download(dst, srcIndex, dstIndex, count)

    [<Extension; Obsolete("Use range.Download(dst, srcIndex, dstIndex, count) instead.")>]
    static member Download(src : IBufferRange<'T>, srcIndex : int, dst : 'T[], count : int) =
        src.Download(dst, srcIndex, 0, count)

    // ================================================================================================================
    // DownloadAsync
    // ================================================================================================================

    ///<summary>Asynchronously copies data from a buffer range to host memory.</summary>
    ///<param name="src">The buffer range to copy data from.</param>
    ///<param name="dst">Location to copy the data to.</param>
    ///<param name="sizeInBytes">Number of bytes to copy.</param>
    ///<returns>A function that blocks until the download is complete.</returns>
    [<Extension>]
    static member inline DownloadAsync(src : IBufferRange, dst : nativeint, sizeInBytes : nativeint) =
        src.Buffer.DownloadAsync(src.Offset, dst, sizeInBytes)

    ///<summary>Asynchronously copies elements from a buffer range to an array.</summary>
    ///<param name="src">The buffer range to copy data from.</param>
    ///<param name="dst">The data array to copy to.</param>
    ///<param name="srcIndex">Index at which copying from the buffer range begins.</param>
    ///<param name="dstIndex">Index at which copying to the data array begins.</param>
    ///<param name="count">Number of elements to copy.</param>
    ///<returns>A function that blocks until the download is complete.</returns>
    [<Extension>]
    static member DownloadAsync(src : IBufferRange<'T>, dst : 'T[], srcIndex : int, dstIndex : int, count : int) =
        count |> checkNonNegative "count"
        dstIndex |> checkNonNegative "dstIndex"
        (dstIndex, count) ||> checkArrayBounds dst

        pinned dst (fun pDst ->
            src.Buffer.DownloadAsync(src.Offset + byteSize<'T> srcIndex, pDst + byteSize<'T> dstIndex, byteSize<'T> count)
        )

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
    [<Extension>]
    static member inline CopyTo(src : IBufferRange, dst : IBufferRange) =
        src.Buffer.CopyTo(src.Offset, dst.Buffer, dst.Offset, min src.SizeInBytes dst.SizeInBytes)

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