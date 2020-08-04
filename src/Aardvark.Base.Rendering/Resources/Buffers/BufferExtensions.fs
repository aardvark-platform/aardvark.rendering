namespace Aardvark.Base

open System
open FSharp.Data.Adaptive
open System.Runtime.InteropServices
open System.Runtime.CompilerServices

[<AbstractClass; Sealed; Extension>]
type BufferExtensions private() =

    static let check (b : IBackendBuffer) (off : nativeint) (size : nativeint) =
        if off < 0n then failwithf "[Buffer] invalid offset %A" off
        if size < 0n then failwithf "[Buffer] invalid size %A" size
        let e = off + size
        if e > b.SizeInBytes then failwithf "[Buffer] range out of bounds { offset = %A; size = %A } (size: %A)" off size b.SizeInBytes

    static let checkRange (b : IBufferRange<'a>) (min : int) (max : int) =
        if min < 0 then failwithf "[BufferRange] invalid offset %A" min
        if max < min then failwithf "[BufferRange] invalid range [%A, %A]" min max
        if max >= b.Count then failwithf "[BufferRange] range out of bounds { min = %A; max = %A } (count: %A)" min max b.Count

    // Basic runtime methods without sync
    [<Extension>]
    static member Copy(this : IBufferRuntime, srcData : nativeint, dst : IBackendBuffer, dstOffset : nativeint, size : nativeint) =
        this.Copy(srcData, dst, dstOffset, size, TaskSync.none)

    [<Extension>]
    static member Copy(this : IBufferRuntime, srcBuffer : IBackendBuffer, srcOffset : nativeint, dstData : nativeint, size : nativeint) =
        this.Copy(srcBuffer, srcOffset, dstData, size)

    [<Extension>]
    static member Copy(this : IBufferRuntime, srcBuffer : IBackendBuffer, srcOffset : nativeint, dstBuffer : IBackendBuffer, dstOffset : nativeint, size : nativeint) =
        this.Copy(srcBuffer, srcOffset, dstBuffer, dstOffset, size, TaskSync.none)

    /// Prepares a buffer, allocating and uploading the data to GPU memory
    /// If the buffer is an IBackendBuffer the operation performs NOP
    [<Extension>]
    static member PrepareBuffer(this : IBufferRuntime, data : IBuffer, usage : BufferUsage) =
        this.PrepareBuffer(data, usage, TaskSync.none)

    /// <summary>
    /// Prepares a buffer for GPU usage with all BufferUsage flags, but only write permissions.
    /// If the buffer is an IBackendBuffer the operation performs NOP
    /// </summary>
    [<Extension>]
    static member PrepareBuffer(this : IBufferRuntime, buffer : IBuffer) =
        this.PrepareBuffer(buffer, BufferUsage.Default &&& ~~~BufferUsage.Read, TaskSync.none)

    // Buffer download, upload, copy (with sync)
    [<Extension>]
    static member Upload(this : IBackendBuffer, offset : nativeint, data : nativeint, size : nativeint, sync : TaskSync) =
        check this offset size
        this.Runtime.Copy(data, this, offset, size, sync)

    [<Extension>]
    static member Download(this : IBackendBuffer, offset : nativeint, data : nativeint, size : nativeint, sync : TaskSync) =
        check this offset size
        this.Runtime.Copy(this, offset, data, size, sync)

    [<Extension>]
    static member CopyTo(src : IBackendBuffer, srcOffset : nativeint, dst : IBackendBuffer, dstOffset : nativeint, size : nativeint, sync : TaskSync) =
        check src srcOffset size
        check dst dstOffset size
        let runtime = src.Runtime
        runtime.Copy(src, srcOffset, dst, dstOffset, size, sync)

    [<Extension>]
    static member Upload(this : IBufferRange, data : nativeint, size : nativeint, sync : TaskSync) =
        this.Buffer.Upload(this.Offset, data, min this.Size size, sync)

    [<Extension>]
    static member Download(this : IBufferRange, data : nativeint, size : nativeint, sync : TaskSync) =
        this.Buffer.Download(this.Offset, data, min this.Size size, sync)

    [<Extension>]
    static member Upload(this : IBufferRange<'a>, src : 'a[], srcIndex : int, dstIndex : int, count : int, sync : TaskSync) =
        let gc = GCHandle.Alloc(src, GCHandleType.Pinned)
        try
            let ptr = gc.AddrOfPinnedObject()
            this.Buffer.Upload(nativeint dstIndex * nsa<'a>, ptr + nsa<'a> * nativeint srcIndex, nsa<'a> * nativeint count, sync)
        finally
            gc.Free()

    [<Extension>]
    static member Upload(this : IBufferRange<'a>, src : 'a[], dstIndex : int, count : int, sync : TaskSync) =
        this.Upload(src, 0, dstIndex, count, sync)

    [<Extension>]
    static member Upload(this : IBufferRange<'a>, src : 'a[], count : int, sync : TaskSync) =
        this.Upload(src, 0, 0, count, sync)

    [<Extension>]
    static member Upload(this : IBufferRange<'a>, src : 'a[], sync : TaskSync) =
        this.Upload(src, 0, 0, src.Length, sync)

    [<Extension>]
    static member Download(this : IBufferRange<'a>, srcIndex : int, dst : 'a[], dstIndex : int, count : int, sync : TaskSync) =
        let gc = GCHandle.Alloc(dst, GCHandleType.Pinned)
        try
            let ptr = gc.AddrOfPinnedObject()
            this.Buffer.Download(this.Offset + nativeint srcIndex * nsa<'a>, ptr + nsa<'a> * nativeint dstIndex, nsa<'a> * nativeint count, sync)
        finally
            gc.Free()

    [<Extension>]
    static member Download(this : IBufferRange<'a>, srcIndex : int, dst : 'a[], count : int, sync : TaskSync) =
        this.Download(srcIndex, dst, 0, count, sync)

    [<Extension>]
    static member Download(this : IBufferRange<'a>, dst : 'a[], count : int, sync : TaskSync) =
        this.Download(0, dst, 0, count, sync)

    [<Extension>]
    static member Download(this : IBufferRange<'a>, dst : 'a[], sync : TaskSync) =
        this.Download(0, dst, 0, dst.Length, sync)

    [<Extension>]
    static member Download(this : IBufferRange<'a>, sync : TaskSync) =
        let dst = Array.zeroCreate this.Count
        this.Download(0, dst, 0, dst.Length, sync)
        dst

    [<Extension>]
    static member CopyTo(src : IBufferRange, dst : IBufferRange, sync : TaskSync) =
        if src.Size <> dst.Size then failwithf "[Buffer] mismatching size in copy: { src = %A; dst = %A }" src.Size dst.Size
        src.Buffer.CopyTo(src.Offset, dst.Buffer, dst.Offset, src.Size, sync)

    // Buffer download, upload, copy (without sync)
    [<Extension>]
    static member Upload(this : IBackendBuffer, offset : nativeint, data : nativeint, size : nativeint) =
        this.Upload(offset, data, size, TaskSync.none)

    [<Extension>]
    static member Download(this : IBackendBuffer, offset : nativeint, data : nativeint, size : nativeint) =
        this.Download(offset, data, size, TaskSync.none)

    [<Extension>]
    static member CopyTo(src : IBackendBuffer, srcOffset : nativeint, dst : IBackendBuffer, dstOffset : nativeint, size : nativeint) =
        src.CopyTo(srcOffset, dst, dstOffset, size, TaskSync.none)

    [<Extension>]
    static member Upload(this : IBufferRange, data : nativeint, size : nativeint) =
        this.Upload(data, size, TaskSync.none)

    [<Extension>]
    static member Download(this : IBufferRange, data : nativeint, size : nativeint) =
        this.Download(data, size, TaskSync.none)

    [<Extension>]
    static member Upload(this : IBufferRange<'a>, src : 'a[], srcIndex : int, dstIndex : int, count : int) =
        this.Upload(src, srcIndex, dstIndex, count, TaskSync.none)

    [<Extension>]
    static member Upload(this : IBufferRange<'a>, src : 'a[], dstIndex : int, count : int) =
        this.Upload(src, dstIndex, count, TaskSync.none)

    [<Extension>]
    static member Upload(this : IBufferRange<'a>, src : 'a[], count : int) =
        this.Upload(src, count, TaskSync.none)

    [<Extension>]
    static member Upload(this : IBufferRange<'a>, src : 'a[]) =
        this.Upload(src, TaskSync.none)

    [<Extension>]
    static member Download(this : IBufferRange<'a>, srcIndex : int, dst : 'a[], dstIndex : int, count : int) =
        this.Download(srcIndex, dst, dstIndex, count, TaskSync.none)

    [<Extension>]
    static member Download(this : IBufferRange<'a>, srcIndex : int, dst : 'a[], count : int) =
        this.Download(srcIndex, dst, count, TaskSync.none)

    [<Extension>]
    static member Download(this : IBufferRange<'a>, dst : 'a[], count : int) =
        this.Download(dst, count, TaskSync.none)

    [<Extension>]
    static member Download(this : IBufferRange<'a>, dst : 'a[]) =
        this.Download(dst, TaskSync.none)

    [<Extension>]
    static member Download(this : IBufferRange<'a>) =
        this.Download(TaskSync.none)

    [<Extension>]
    static member CopyTo(src : IBufferRange, dst : IBufferRange) =
        src.CopyTo(dst, TaskSync.none)

    // Buffer vector extensions
    [<Extension>]
    static member SubVector(this : IBufferVector<'a>, offset : int, delta : int, count : int) =
        if offset < 0 then failwithf "[Buffer] invalid negative offset: %A" offset
        if offset > this.Count then failwithf "[Buffer] offset out of bounds: %A (count: %A)" offset this.Count

        if count < 0 then failwithf "[Buffer] invalid negative count: %A" offset
        if offset + count > this.Count then failwithf "[Buffer] range out of bounds: (%A, %A) (count: %A)" offset count this.Count

        let origin = this.Origin + offset * this.Delta
        let delta = this.Delta * delta

        let sa = nativeint sizeof<'a>
        let firstByte = sa * nativeint origin
        let lastByte = sa * (nativeint origin + nativeint delta * nativeint (count - 1))
        if firstByte < 0n || firstByte >= this.Buffer.SizeInBytes then failwithf "[Buffer] range out of bounds"
        if lastByte < 0n || lastByte >= this.Buffer.SizeInBytes then failwithf "[Buffer] range out of bounds"

        RuntimeBufferVector<'a>(
            this.Buffer,
            origin,
            delta,
            count
        ) :> IBufferVector<_>

    [<Extension>]
    static member SubVector(this : IBufferVector<'a>, offset : int, count : int) =
        this.SubVector(offset, 1, count)

    [<Extension>]
    static member Skip(this : IBufferVector<'a>, n : int) =
        let n = min (this.Count - 1) n
        this.SubVector(n, 1, max 0 (this.Count - n))

    [<Extension>]
    static member Take(this : IBufferVector<'a>, n : int) =
        this.SubVector(0, 1, n)

    [<Extension>]
    static member Strided(this : IBufferVector<'a>, d : int) =
        let n = 1 + (this.Count - 1) / d
        this.SubVector(0, d, n)

    // Buffer range extensions
    [<Extension>]
    static member GetSlice(this : IBufferRange<'a>, min : Option<int>, max : Option<int>) =
        let min = defaultArg min 0
        let max = defaultArg max (this.Count - 1)
        checkRange this min max
        RuntimeBufferRange<'a>(this.Buffer, this.Offset + nativeint min * nsa<'a>, 1 + max - min) :> IBufferRange<_>

    [<Extension>]
    static member SetSlice(this : IBufferRange<'a>, min : Option<int>, max : Option<int>, data : 'a[], sync : TaskSync) =
        let slice = this.GetSlice(min, max)
        slice.Upload(data, Fun.Min(data.Length, slice.Count), sync)

    [<Extension>]
    static member SetSlice(this : IBufferRange<'a>, min : Option<int>, max : Option<int>, other : IBufferRange<'a>, sync : TaskSync) =
        let slice = this.GetSlice(min, max)
        other.CopyTo(slice, sync)

    [<Extension>]
    static member SetSlice(this : IBufferRange<'a>, min : Option<int>, max : Option<int>, value : 'a, sync : TaskSync) =
        let slice = this.GetSlice(min, max)
        slice.Upload(Array.create slice.Count value, sync)

    [<Extension>]
    static member SetSlice(this : IBufferRange<'a>, min : Option<int>, max : Option<int>, data : 'a[]) =
        this.SetSlice(min, max, data, TaskSync.none)

    [<Extension>]
    static member SetSlice(this : IBufferRange<'a>, min : Option<int>, max : Option<int>, other : IBufferRange<'a>) =
        this.SetSlice(min, max, other, TaskSync.none)

    [<Extension>]
    static member SetSlice(this : IBufferRange<'a>, min : Option<int>, max : Option<int>, value : 'a) =
        this.SetSlice(min, max, value, TaskSync.none)

    // Create buffer
    [<Extension>]
    static member CreateBuffer<'a when 'a : unmanaged>(this : IBufferRuntime, count : int) =
        let buffer = this.CreateBuffer(nsa<'a> * nativeint count, BufferUsage.Default)
        new RuntimeBuffer<'a>(buffer, count) :> IBuffer<'a>

    [<Extension>]
    static member CreateBuffer<'a when 'a : unmanaged>(this : IBufferRuntime, data : 'a[], sync : TaskSync) =
        let buffer = this.CreateBuffer(nsa<'a> * nativeint data.Length, BufferUsage.Default)
        let res = new RuntimeBuffer<'a>(buffer, data.Length) :> IBuffer<'a>
        res.Upload(data, sync)
        res

    [<Extension>]
    static member CreateBuffer<'a when 'a : unmanaged>(this : IBufferRuntime, data : 'a[]) =
        this.CreateBuffer(data, TaskSync.none)

    [<Extension>]
    static member Coerce<'a when 'a : unmanaged>(this : IBackendBuffer) =
        new RuntimeBuffer<'a>(this, int (this.SizeInBytes / nativeint sizeof<'a>)) :> IBuffer<'a>

    /// <summary>
    /// Creates a buffer in GPU memory with default BufferUsage flags, enabling all usages as well as read and write.
    /// </summary>
    [<Extension>]
    static member CreateBuffer(this : IBufferRuntime, size : nativeint) =
        this.CreateBuffer(size, BufferUsage.Default)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module BufferRange =
    let inline upload (data : 'a[]) (range : IBufferRange<'a>) = range.Upload(data)
    let inline download (range : IBufferRange<'a>) = range.Download()
    let inline copy (src : IBufferRange) (dst : IBufferRange) = src.CopyTo dst

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Buffer =
    let inline create<'a when 'a : unmanaged> (runtime : IBufferRuntime) (count : int) = runtime.CreateBuffer<'a>(count)
    let inline ofArray<'a when 'a : unmanaged> (runtime : IBufferRuntime) (data : 'a[]) = runtime.CreateBuffer<'a>(data)
    let inline delete (b : IBuffer<'a>) =  b.Dispose()