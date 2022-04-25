namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive

open System
open System.Threading
open System.Runtime.InteropServices
open System.Runtime.CompilerServices

/// Interface for adaptive buffers, which can be resized and written to in an imperative fashion.
/// Depending on the implementation, updates to the underlying buffer are either immediately realized or delayed to when the adaptive value is pulled.
type IAdaptiveBuffer =
    inherit IAdaptiveResource<IBackendBuffer>

    /// The (virtual) size of the buffer.
    abstract member Size : nativeint

    /// <summary>
    /// Resizes the buffer.
    /// </summary>
    /// <param name="sizeInBytes">The new size in bytes.</param>
    abstract member Resize : sizeInBytes: nativeint -> unit

    /// <summary>
    /// Writes data to the buffer.
    /// </summary>
    /// <param name="data">The data to write.</param>
    /// <param name="offset">The offset (in bytes) into the buffer.</param>
    /// <param name="sizeInBytes">The number of bytes to write to the buffer.</param>
    abstract member Write : data: nativeint * offset: nativeint * sizeInBytes: nativeint -> unit

[<Extension; Sealed>]
type AdaptiveBufferExtensions private() =

    /// <summary>
    /// Writes an array to the buffer.
    /// </summary>
    /// <param name="this">The buffer to write to.</param>
    /// <param name="data">The array to write.</param>
    /// <param name="offset">The offset (in bytes) into the buffer.</param>
    /// <param name="sizeInBytes">The number of bytes to write to the buffer.</param>
    [<Extension>]
    static member Write(this : IAdaptiveBuffer, data : Array, offset : nativeint, sizeInBytes : nativeint) =
        pinned data (fun src ->
            this.Write(src, offset, sizeInBytes)
        )

    /// <summary>
    /// Writes an array to the buffer.
    /// </summary>
    /// <param name="this">The buffer to write to.</param>
    /// <param name="data">The array to write.</param>
    /// <param name="offset">The offset (in bytes) into the buffer.</param>
    /// <param name="start">The first index of the data array to write.</param>
    /// <param name="length">The number of elements to write.</param>
    [<Extension>]
    static member Write(this : IAdaptiveBuffer, data : 'T[], offset : nativeint, start : int, length : int) =
        assert (start >= 0 && start < data.Length)
        assert (start + length <= data.Length)

        pinned data (fun src ->
            let src = src + nativeint (start * sizeof<'T>)
            let size = nativeint (length * sizeof<'T>)
            this.Write(src, offset, size)
        )

    /// <summary>
    /// Writes an array to the buffer.
    /// </summary>
    /// <param name="this">The buffer to write to.</param>
    /// <param name="data">The array to write.</param>
    /// <param name="offset">The offset (in bytes) into the buffer.</param>
    [<Extension>]
    static member Write(this : IAdaptiveBuffer, data : 'T[], offset : nativeint) =
        this.Write(data, offset, 0, data.Length)

    /// <summary>
    /// Writes a value to the buffer.
    /// </summary>
    /// <param name="this">The buffer to write to.</param>
    /// <param name="data">The value to write.</param>
    /// <param name="offset">The offset (in bytes) into the buffer.</param>
    [<Extension>]
    static member Write(this : IAdaptiveBuffer, data : 'T, offset : nativeint) =
        pinned data (fun src ->
            this.Write(src, offset, nativeint sizeof<'T>)
        )


module internal AdaptiveBufferImplementation =

    /// Base class for adaptive buffer implementations.
    [<AbstractClass>]
    type AbstractAdaptiveBuffer(runtime : IBufferRuntime, sizeInBytes : nativeint, usage : BufferUsage) =
        inherit AdaptiveResource<IBackendBuffer>()

        let mutable size = sizeInBytes
        let mutable handle : ValueOption<IBackendBuffer> = ValueNone

        member internal x.Size
            with get() = size
            and set(s) = size <- s

        member inline internal x.ComputeHandle(discard : bool) =
            match handle with
            | ValueNone ->
                let h = runtime.CreateBuffer(size, usage)
                handle <- ValueSome h
                h

            | ValueSome old ->
                if old.SizeInBytes <> size then
                    let resized = runtime.CreateBuffer(size, usage)

                    if not discard then
                        runtime.Copy(old, 0n, resized, 0n, min old.SizeInBytes size)

                    runtime.DeleteBuffer(old)
                    handle <- ValueSome resized
                    resized

                else
                    old

        abstract member Write : data: nativeint * offset: nativeint * sizeInBytes: nativeint -> unit

        abstract member Resize : sizeInBytes: nativeint -> unit

        override x.Create() =
            x.ComputeHandle(true) |> ignore

        override x.Destroy() =
            handle |> ValueOption.iter Disposable.dispose
            handle <- ValueNone

        interface IAdaptiveBuffer with
            member x.Size = x.Size
            member x.Resize(sizeInBytes) = x.Resize(sizeInBytes)
            member x.Write(data, offset, sizeInBytes) = x.Write(data, offset, sizeInBytes)


    /// Lightweight adaptive buffer implementation that writes data directly to the underlying buffer.
    type DirectAdaptiveBuffer(runtime : IBufferRuntime, sizeInBytes : nativeint,
                            [<Optional; DefaultParameterValue(BufferUsage.Default)>] usage : BufferUsage) =
        inherit AbstractAdaptiveBuffer(runtime, sizeInBytes, usage ||| BufferUsage.Read)

        override x.Resize(sizeInBytes : nativeint) =
            lock x (fun _ ->
                if sizeInBytes <> x.Size then
                    x.Size <- sizeInBytes
                    x.ComputeHandle(false) |> ignore

                    transact x.MarkOutdated
            )

        override x.Write(data : nativeint, offset : nativeint, sizeInBytes : nativeint) =
            lock x (fun _ ->
                assert (offset + sizeInBytes <= x.Size)
                let handle = x.ComputeHandle(false)
                runtime.Copy(data, handle, offset, sizeInBytes)
            )

        override x.Compute(t : AdaptiveToken, rt : RenderToken) =
            x.ComputeHandle(false)


    type private HostBuffer(sizeInBytes : nativeint) =
        let mutable buffer = Marshal.AllocHGlobal(sizeInBytes)
        let mutable capacity = sizeInBytes

        member x.Handle = buffer

        member x.Resize(sizeInBytes : nativeint) =
            if sizeInBytes <> capacity then
                buffer <- Marshal.ReAllocHGlobal(buffer, sizeInBytes)
                capacity <- sizeInBytes

        member x.Write(data : nativeint, offset : nativeint, sizeInBytes : nativeint) =
            assert (offset + sizeInBytes <= capacity)
            Marshal.Copy(data, buffer + offset, sizeInBytes)

        member x.Dispose() =
            Marshal.FreeHGlobal(buffer)
            buffer <- 0n
            capacity <- 0n

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    /// Adaptive buffer implementation that keeps a host-side representation of the buffer.
    /// Updates are realized only on host-side and written to the GPU buffer in a single operation when the adaptive value gets pulled.
    type MappedAdaptiveBuffer(runtime : IBufferRuntime, sizeInBytes : nativeint,
                              [<Optional; DefaultParameterValue(BufferUsage.Default)>] usage : BufferUsage,
                              [<Optional; DefaultParameterValue(1L)>] blockAlignment : int64,
                              [<Optional; DefaultParameterValue(1L)>] blockSize : int64) =
        inherit AbstractAdaptiveBuffer(runtime, sizeInBytes, usage)

        do if blockSize < blockAlignment then
            invalidArg "blockSize" "[MappedAdaptiveBuffer] Block size must be equal or greater than block alignment"

        let hostLock = new ReaderWriterLockSlim()
        let mutable host = new HostBuffer(0n)

        let dirtyLock = obj()
        let mutable dirty = RangeSet.empty

        let addDirtyRange (range : Range1i) =
            lock dirtyLock (fun _ ->
                let alignedOffset = (range.Min / int blockAlignment) * int blockAlignment
                let alignedSize = (range.Size + int blockSize) &&& ~~~(int blockSize - 1)
                let range = Range1i.FromMinAndSize(alignedOffset, alignedSize - 1)
                dirty <- dirty |> RangeSet.insert range
            )

        let getDirtyRanges() =
            lock dirtyLock (fun _ ->
                let res = dirty
                dirty <- RangeSet.empty
                res
            )

        override x.Resize(sizeInBytes : nativeint) =
            ReaderWriterLock.write hostLock (fun _ ->
                host.Resize(sizeInBytes)
            )

            if x.Size <> sizeInBytes then
                x.Size <- sizeInBytes
                addDirtyRange <| Range1i.FromMinAndSize(0, int sizeInBytes - 1)
                transact x.MarkOutdated

        override x.Write(data : nativeint, offset : nativeint, sizeInBytes : nativeint) =
            ReaderWriterLock.read hostLock (fun _ ->
                host.Write(data, offset, sizeInBytes)
            )

            addDirtyRange <| Range1i.FromMinAndSize(int offset, int sizeInBytes - 1)

            transact x.MarkOutdated

        override x.Compute(t, rt) =
            let handle = x.ComputeHandle(true)

            ReaderWriterLock.read hostLock (fun _ ->
                for r in getDirtyRanges() do
                    let offset = nativeint r.Min
                    let size = min (handle.SizeInBytes - offset) (nativeint r.Size + 1n)
                    runtime.Copy(host.Handle + offset, handle, offset, size)
            )

            handle

        override x.Create() =
            base.Create()
            host.Resize(x.Size)

        override x.Destroy() =
            host.Dispose()
            hostLock.Dispose()
            base.Destroy()