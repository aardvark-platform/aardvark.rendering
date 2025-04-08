namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive

open System
open System.Runtime.InteropServices
open System.Runtime.CompilerServices

/// Interface for adaptive buffers, which can be resized and written to in an imperative fashion.
type IAdaptiveBuffer =
    inherit IAdaptiveResource<IBackendBuffer>

    /// The size of the buffer in bytes.
    abstract member Size : nativeint

    /// <summary>
    /// Resizes the buffer.
    /// </summary>
    /// <param name="sizeInBytes">The new size in bytes.</param>
    /// <param name="forceImmediate">Indicates if the buffer is resized immediately or lazily.</param>
    abstract member Resize : sizeInBytes: nativeint *
                             [<Optional; DefaultParameterValue(false)>] forceImmediate: bool -> unit

    /// <summary>
    /// Writes data to the buffer.
    /// </summary>
    /// <param name="data">The data to write.</param>
    /// <param name="offset">The offset (in bytes) into the buffer.</param>
    /// <param name="sizeInBytes">The number of bytes to write to the buffer.</param>
    abstract member Write : data: nativeint * offset: nativeint * sizeInBytes: nativeint -> unit

[<AbstractClass; Sealed; Extension>]
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
        data |> NativeInt.pin (fun src ->
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
    static member Write<'T when 'T : unmanaged>(this : IAdaptiveBuffer, data : 'T[], offset : nativeint, start : int, length : int) =
        assert (start >= 0 && start < data.Length)
        assert (start + length <= data.Length)

        if length > 0 then
            (start, data) ||> NativePtr.pinArri (fun src ->
                let size = nativeint (length * sizeof<'T>)
                this.Write(src.Address, offset, size)
        )

    /// <summary>
    /// Writes an array to the buffer.
    /// </summary>
    /// <param name="this">The buffer to write to.</param>
    /// <param name="data">The array to write.</param>
    /// <param name="offset">The offset (in bytes) into the buffer.</param>
    [<Extension>]
    static member Write<'T when 'T : unmanaged>(this : IAdaptiveBuffer, data : 'T[], offset : nativeint) =
        this.Write(data, offset, 0, data.Length)

    /// <summary>
    /// Writes a value to the buffer.
    /// </summary>
    /// <param name="this">The buffer to write to.</param>
    /// <param name="data">The value to write.</param>
    /// <param name="offset">The offset (in bytes) into the buffer.</param>
    [<Extension>]
    static member Write<'T when 'T : unmanaged>(this : IAdaptiveBuffer, data : 'T, offset : nativeint) =
        data |> NativePtr.pin (fun src ->
            this.Write(src.Address, offset, nativeint sizeof<'T>)
        )

    /// <summary>
    /// Clears the buffer, i.e. resizes it to 0.
    /// </summary>
    /// <param name="this">The buffer to clear.</param>
    [<Extension>]
    static member Clear(this : IAdaptiveBuffer) =
        this.Resize(0n, true)


/// Adaptive buffer that can be resized and written to in an imperative fashion.
type AdaptiveBuffer(runtime : IBufferRuntime, sizeInBytes : nativeint,
                    [<Optional; DefaultParameterValue(BufferUsage.All)>] usage : BufferUsage,
                    [<Optional; DefaultParameterValue(BufferStorage.Host)>] storage : BufferStorage) =
    inherit AdaptiveResource<IBackendBuffer>()

    let mutable size = sizeInBytes
    let mutable handle : ValueOption<IBackendBuffer> = ValueNone
    let usage = usage ||| BufferUsage.Read

    abstract member CreateHandle : size: nativeint * usage: BufferUsage * storage: BufferStorage -> IBackendBuffer
    default _.CreateHandle(size, usage, storage) = runtime.CreateBuffer(size, usage, storage)

    member private x.ComputeHandle(discard : bool) =
        match handle with
        | ValueNone ->
            let h = x.CreateHandle(size, usage, storage)
            handle <- ValueSome h
            h

        | ValueSome old ->
            if old.SizeInBytes <> size then
                let resized = x.CreateHandle(size, usage, storage)

                if not discard then
                    runtime.Copy(old, 0n, resized, 0n, min old.SizeInBytes size)

                runtime.DeleteBuffer(old)
                handle <- ValueSome resized
                resized

            else
                old

    /// The size of the buffer in bytes.
    member x.Size = size

    /// <summary>
    /// Resizes the buffer.
    /// </summary>
    /// <param name="sizeInBytes">The new size in bytes.</param>
    /// <param name="forceImmediate">Indicates if the buffer is resized immediately or lazily</param>
    member x.Resize(sizeInBytes : nativeint, [<Optional; DefaultParameterValue(false)>] forceImmediate : bool) =
        lock x (fun _ ->
            if sizeInBytes <> x.Size then
                size <- sizeInBytes

                if forceImmediate then
                    x.ComputeHandle(false) |> ignore

                transact x.MarkOutdated
        )

    /// <summary>
    /// Writes data to the buffer.
    /// </summary>
    /// <param name="data">The data to write.</param>
    /// <param name="offset">The offset (in bytes) into the buffer.</param>
    /// <param name="sizeInBytes">The number of bytes to write to the buffer.</param>
    member x.Write(data : nativeint, offset : nativeint, sizeInBytes : nativeint) =
        lock x (fun _ ->
            assert (offset + sizeInBytes <= x.Size)
            let handle = x.ComputeHandle(false)
            runtime.Upload(data, handle, offset, sizeInBytes)
        )

    override x.Create() =
        x.ComputeHandle(true) |> ignore

    override x.Destroy() =
        handle |> ValueOption.iter Disposable.dispose
        handle <- ValueNone
        size <- 0n

    override x.Compute(_, _) =
        x.ComputeHandle(false)

    interface IAdaptiveBuffer with
        member x.Size = x.Size
        member x.Resize(sizeInBytes, forceImmediate) = x.Resize(sizeInBytes, forceImmediate)
        member x.Write(data, offset, sizeInBytes) = x.Write(data, offset, sizeInBytes) 