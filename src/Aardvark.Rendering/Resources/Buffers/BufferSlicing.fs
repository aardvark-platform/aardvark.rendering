namespace Aardvark.Rendering

open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base

type IBufferRange =
    abstract member Buffer : IBackendBuffer
    abstract member Offset : nativeint
    abstract member Size : nativeint

type IBufferVector<'a when 'a : unmanaged> =
    abstract member Buffer : IBackendBuffer
    abstract member Origin : int
    abstract member Delta : int
    abstract member Count : int

type IBufferRange<'a when 'a : unmanaged> =
    inherit IBufferRange
    inherit IBufferVector<'a>
    abstract member Count : int

type IBuffer<'a when 'a : unmanaged> =
    inherit IBuffer
    inherit IBufferRange<'a>
    inherit IDisposable


[<AutoOpen>]
module private RuntimeBufferImplementation =

    type RuntimeBufferVector<'a when 'a : unmanaged>(buffer : IBackendBuffer, origin : int, delta : int, count : int) =
        interface IBufferVector<'a> with
            member x.Buffer = buffer
            member x.Origin = origin
            member x.Delta = delta
            member x.Count = count

    type RuntimeBufferRange<'a when 'a : unmanaged>(buffer : IBackendBuffer, offset : nativeint, count : int) =

        member x.Buffer = buffer
        member x.Offset = offset
        member x.Count = count

        interface IBufferVector<'a> with
            member x.Buffer = buffer
            member x.Origin = 0
            member x.Delta = 1
            member x.Count = count

        interface IBufferRange<'a> with
            member x.Buffer = buffer
            member x.Offset = offset
            member x.Count = count
            member x.Size = nativeint count * nativeint sizeof<'a>

    type RuntimeBuffer<'a when 'a : unmanaged>(buffer : IBackendBuffer, count : int) =
        inherit RuntimeBufferRange<'a>(buffer, 0n, count)
        interface IBuffer<'a> with
            member x.Dispose() = buffer.Dispose()

    let nsa<'a when 'a : unmanaged> = nativeint sizeof<'a>


[<AbstractClass; Sealed; Extension>]
type IBufferRuntimeSlicingExtensions private() =

    static let checkRange (b : IBufferRange<'a>) (min : int) (max : int) =
        if min < 0 then failwithf "[BufferRange] invalid offset %A" min
        if max < min then failwithf "[BufferRange] invalid range [%A, %A]" min max
        if max >= b.Count then failwithf "[BufferRange] range out of bounds { min = %A; max = %A } (count: %A)" min max b.Count

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
        IBufferRuntimeSlicingExtensions.SubVector(this, offset, 1, count)

    [<Extension>]
    static member Skip(this : IBufferVector<'a>, n : int) =
        let n = min (this.Count - 1) n
        IBufferRuntimeSlicingExtensions.SubVector(this, n, 1, max 0 (this.Count - n))

    [<Extension>]
    static member Take(this : IBufferVector<'a>, n : int) =
        IBufferRuntimeSlicingExtensions.SubVector(this, 0, 1, n)

    [<Extension>]
    static member Strided(this : IBufferVector<'a>, d : int) =
        let n = 1 + (this.Count - 1) / d
        IBufferRuntimeSlicingExtensions.SubVector(this, 0, d, n)


    [<Extension>]
    static member Upload(this : IBufferRange, data : nativeint, size : nativeint) =
        IBackendBufferExtensions.Upload(this.Buffer, this.Offset, data, min this.Size size)

    [<Extension>]
    static member Download(this : IBufferRange, data : nativeint, size : nativeint) =
        IBackendBufferExtensions.Download(this.Buffer, this.Offset, data, min this.Size size)


    [<Extension>]
    static member Upload(x : IBufferRange<'a>, src : 'a[], srcIndex : int, dstIndex : int, count : int) =
        let gc = GCHandle.Alloc(src, GCHandleType.Pinned)
        try
            let ptr = gc.AddrOfPinnedObject()
            IBackendBufferExtensions.Upload(x.Buffer, nativeint dstIndex * nsa<'a>, ptr + nsa<'a> * nativeint srcIndex, nsa<'a> * nativeint count)
        finally
            gc.Free()

    [<Extension>]
    static member Upload(x : IBufferRange<'a>, src : 'a[], dstIndex : int, count : int) = IBufferRuntimeSlicingExtensions.Upload(x, src, 0, dstIndex, count)

    [<Extension>]
    static member Upload(x : IBufferRange<'a>, src : 'a[], count : int) = IBufferRuntimeSlicingExtensions.Upload(x, src, 0, 0, count)

    [<Extension>]
    static member Upload(x : IBufferRange<'a>, src : 'a[]) = IBufferRuntimeSlicingExtensions.Upload(x, src, 0, 0, src.Length)

    [<Extension>]
    static member Download(x : IBufferRange<'a>, srcIndex : int, dst : 'a[], dstIndex : int, count : int) =
        let gc = GCHandle.Alloc(dst, GCHandleType.Pinned)
        try
            let ptr = gc.AddrOfPinnedObject()
            IBackendBufferExtensions.Download(x.Buffer, x.Offset + nativeint srcIndex * nsa<'a>, ptr + nsa<'a> * nativeint dstIndex, nsa<'a> * nativeint count)
        finally
            gc.Free()

    [<Extension>]
    static member Download(x : IBufferRange<'a>, srcIndex : int, dst : 'a[], count : int) = IBufferRuntimeSlicingExtensions.Download(x, srcIndex, dst, 0, count)

    [<Extension>]
    static member Download(x : IBufferRange<'a>, dst : 'a[], count : int) = IBufferRuntimeSlicingExtensions.Download(x, 0, dst, 0, count)

    [<Extension>]
    static member Download(x : IBufferRange<'a>, dst : 'a[]) = IBufferRuntimeSlicingExtensions.Download(x, 0, dst, 0, dst.Length)

    [<Extension>]
    static member Download(x : IBufferRange<'a>) =
        let dst = Array.zeroCreate x.Count
        IBufferRuntimeSlicingExtensions.Download(x, 0, dst, 0, dst.Length)
        dst

    [<Extension>]
    static member CopyTo(src : IBufferRange, dst : IBufferRange) =
        if src.Size <> dst.Size then failwithf "[Buffer] mismatching size in copy: { src = %A; dst = %A }" src.Size dst.Size
        IBackendBufferExtensions.CopyTo(src.Buffer, src.Offset, dst.Buffer, dst.Offset, src.Size)

    [<Extension>]
    static member GetSlice(x : IBufferRange<'a>, min : Option<int>, max : Option<int>) =
        let min = defaultArg min 0
        let max = defaultArg max (x.Count - 1)
        checkRange x min max
        RuntimeBufferRange<'a>(x.Buffer, x.Offset + nativeint min * nsa<'a>, 1 + max - min) :> IBufferRange<_>

    [<Extension>]
    static member SetSlice(x : IBufferRange<'a>, min : Option<int>, max : Option<int>, data : 'a[]) =
        let slice = IBufferRuntimeSlicingExtensions.GetSlice(x, min, max)
        IBufferRuntimeSlicingExtensions.Upload(slice, data, Fun.Min(data.Length, slice.Count))

    [<Extension>]
    static member SetSlice(x : IBufferRange<'a>, min : Option<int>, max : Option<int>, other : IBufferRange<'a>) =
        let slice = IBufferRuntimeSlicingExtensions.GetSlice(x, min, max)
        IBufferRuntimeSlicingExtensions.CopyTo(other, slice)

    [<Extension>]
    static member SetSlice(x : IBufferRange<'a>, min : Option<int>, max : Option<int>, value : 'a) =
        let slice = IBufferRuntimeSlicingExtensions.GetSlice(x, min, max)
        IBufferRuntimeSlicingExtensions.Upload(slice, Array.create slice.Count value)

    [<Extension>]
    static member CreateBuffer<'a when 'a : unmanaged>(this : IBufferRuntime, count : int) =
        let buffer = this.CreateBuffer(nsa<'a> * nativeint count)
        new RuntimeBuffer<'a>(buffer, count) :> IBuffer<'a>

    [<Extension>]
    static member CreateBuffer<'a when 'a : unmanaged>(this : IBufferRuntime, data : 'a[]) =
        let buffer = this.CreateBuffer(nsa<'a> * nativeint data.Length)
        let res = new RuntimeBuffer<'a>(buffer, data.Length) :> IBuffer<'a>
        IBufferRuntimeSlicingExtensions.Upload(res, data)
        res

    [<Extension>]
    static member Coerce<'a when 'a : unmanaged>(this : IBackendBuffer) =
        new RuntimeBuffer<'a>(this, int (this.SizeInBytes / nativeint sizeof<'a>)) :> IBuffer<'a>

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