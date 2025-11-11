namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive

open System
open System.Runtime.InteropServices
open System.Runtime.CompilerServices

#nowarn "51"

/// Interface for adaptive buffers, which can be resized and written to in an imperative fashion.
type IAdaptiveBuffer =
    inherit IAdaptiveResource<IBackendBuffer>

    /// Runtime of the resource.
    abstract member Runtime : IBufferRuntime

    /// The name of the buffer.
    abstract member Name : string with get, set

    /// The size of the buffer in bytes.
    abstract member Size : uint64

    /// <summary>
    /// Resizes the buffer.
    /// </summary>
    /// <param name="sizeInBytes">The new size in bytes.</param>
    /// <param name="forceImmediate">Indicates if the buffer is resized immediately or lazily.</param>
    abstract member Resize : sizeInBytes: uint64 *
                             [<Optional; DefaultParameterValue(false)>] forceImmediate: bool -> unit

    /// <summary>
    /// Writes data to the buffer.
    /// </summary>
    /// <param name="data">The data to write.</param>
    /// <param name="offset">The offset (in bytes) into the buffer.</param>
    /// <param name="sizeInBytes">The number of bytes to write to the buffer.</param>
    /// <param name="discard">Indicates whether the current content of the buffer may be discarded. Default is <c>false</c>.</param>
    abstract member Write : data: nativeint * offset: uint64 * sizeInBytes: uint64 *
                            [<Optional; DefaultParameterValue(false)>] discard : bool -> unit

[<AbstractClass; Sealed; Extension>]
type AdaptiveBufferExtensions private() =

    /// <summary>
    /// Writes data to the buffer.
    /// </summary>
    /// <param name="this">The buffer to write to.</param>
    /// <param name="data">The data to write.</param>
    /// <param name="offset">The offset (in bytes) into the buffer. Default is 0.</param>
    /// <param name="count">The number of elements to write. Default is 1.</param>
    /// <param name="discard">Indicates whether the current content of the buffer may be discarded. Default is <c>false</c>.</param>
    [<Extension>]
    static member Write<'T when 'T : unmanaged>(this : IAdaptiveBuffer, data : nativeptr<'T>,
                                                [<Optional; DefaultParameterValue(0UL)>] offset : uint64,
                                                [<Optional; DefaultParameterValue(1)>] count : int,
                                                [<Optional; DefaultParameterValue(false)>] discard : bool) =
        this.Write(data.Address, offset, uint64 count * uint64 sizeof<'T>, discard)

    /// <summary>
    /// Writes data to the buffer.
    /// </summary>
    /// <param name="this">The buffer to write to.</param>
    /// <param name="data">The data to write.</param>
    /// <param name="offset">The offset (in bytes) into the buffer. Default is 0.</param>
    /// <param name="count">The number of elements to write. Default is 1.</param>
    /// <param name="discard">Indicates whether the current content of the buffer may be discarded. Default is <c>false</c>.</param>
    [<Extension>]
    static member Write<'T when 'T : unmanaged>(this : IAdaptiveBuffer, data : 'T byref,
                                                [<Optional; DefaultParameterValue(0UL)>] offset : uint64,
                                                [<Optional; DefaultParameterValue(1)>] count : int,
                                                [<Optional; DefaultParameterValue(false)>] discard : bool) =
        this.Write(&&data, offset, count, discard)

    /// <summary>
    /// Writes an array to the buffer.
    /// </summary>
    /// <param name="this">The buffer to write to.</param>
    /// <param name="data">The array to write.</param>
    /// <param name="offset">The offset (in bytes) into the buffer.</param>
    /// <param name="sizeInBytes">The number of bytes to write to the buffer.</param>
    /// <param name="discard">Indicates whether the current content of the buffer may be discarded. Default is <c>false</c>.</param>
    [<Extension>]
    static member Write(this : IAdaptiveBuffer, data : Array, offset : uint64, sizeInBytes : uint64,
                        [<Optional; DefaultParameterValue(false)>] discard : bool) =
        data |> NativeInt.pin (fun src ->
            this.Write(src, offset, sizeInBytes, discard)
        )

    /// <summary>
    /// Writes an array to the buffer.
    /// </summary>
    /// <param name="this">The buffer to write to.</param>
    /// <param name="data">The array to write.</param>
    /// <param name="offset">The offset (in bytes) into the buffer.</param>
    /// <param name="start">The first index of the data array to write.</param>
    /// <param name="count">The number of elements to write.</param>
    /// <param name="discard">Indicates whether the current content of the buffer may be discarded. Default is <c>false</c>.</param>
    [<Extension>]
    static member Write<'T when 'T : unmanaged>(this : IAdaptiveBuffer, data : 'T[], offset : uint64, start : int, count : int,
                                                [<Optional; DefaultParameterValue(false)>] discard : bool) =
        assert (start >= 0 && start < data.Length)
        assert (start + count <= data.Length)

        if count > 0 then
            (start, data) ||> NativePtr.pinArri (fun src ->
                let size = uint64 count * uint64 sizeof<'T>
                this.Write(src.Address, offset, size, discard)
            )

    /// <summary>
    /// Writes an array to the buffer.
    /// </summary>
    /// <param name="this">The buffer to write to.</param>
    /// <param name="data">The array to write.</param>
    /// <param name="offset">The offset (in bytes) into the buffer. Default is 0.</param>
    /// <param name="discard">Indicates whether the current content of the buffer may be discarded. Default is <c>false</c>.</param>
    [<Extension>]
    static member Write<'T when 'T : unmanaged>(this : IAdaptiveBuffer, data : 'T[],
                                                [<Optional; DefaultParameterValue(0UL)>] offset : uint64,
                                                [<Optional; DefaultParameterValue(false)>] discard : bool) =
        this.Write(data, offset, 0, data.Length, discard)

    /// <summary>
    /// Writes a value to the buffer.
    /// </summary>
    /// <param name="this">The buffer to write to.</param>
    /// <param name="data">The value to write.</param>
    /// <param name="offset">The offset (in bytes) into the buffer. Default is 0.</param>
    /// <param name="discard">Indicates whether the current content of the buffer may be discarded. Default is <c>false</c>.</param>
    [<Extension>]
    static member Write<'T when 'T : unmanaged>(this : IAdaptiveBuffer, data : 'T,
                                                [<Optional; DefaultParameterValue(0UL)>] offset : uint64,
                                                [<Optional; DefaultParameterValue(false)>] discard : bool) =
        data |> NativePtr.pin (fun src ->
            this.Write(src.Address, offset, uint64 sizeof<'T>, discard)
        )

    /// <summary>
    /// Clears the buffer, i.e. resizes it to 0.
    /// </summary>
    /// <param name="this">The buffer to clear.</param>
    [<Extension>]
    static member Clear(this : IAdaptiveBuffer) =
        this.Resize(0UL, true)


/// Adaptive buffer that can be resized and written to in an imperative fashion.
type AdaptiveBuffer(runtime : IBufferRuntime, sizeInBytes : uint64,
                    [<Optional; DefaultParameterValue(BufferUsage.All)>] usage : BufferUsage,
                    [<Optional; DefaultParameterValue(BufferStorage.Host)>] storage : BufferStorage,
                    [<Optional; DefaultParameterValue(false)>] discardOnResize : bool) =
    inherit AdaptiveResource<IBackendBuffer>()

    let mutable name = null
    let mutable size = sizeInBytes
    let mutable handle : ValueOption<IBackendBuffer> = ValueNone
    let usage =
        if discardOnResize then
            usage ||| BufferUsage.Write
        else
            usage ||| BufferUsage.ReadWrite

    abstract member CreateHandle : size: uint64 * usage: BufferUsage * storage: BufferStorage -> IBackendBuffer
    default _.CreateHandle(size, usage, storage) = runtime.CreateBuffer(size, usage, storage)

    member private x.ComputeHandle(discard : bool) =
        match handle with
        | ValueNone ->
            let h = x.CreateHandle(size, usage, storage)
            h.Name <- name
            handle <- ValueSome h
            h

        | ValueSome old ->
            if old.SizeInBytes <> size then
                let resized = x.CreateHandle(size, usage, storage)
                resized.Name <- name

                if not discard then
                    runtime.Copy(old, 0UL, resized, 0UL, min old.SizeInBytes size)

                runtime.DeleteBuffer(old)
                handle <- ValueSome resized
                resized

            else
                old

    /// The name of the buffer.
    member x.Name
        with get() = name
        and set value =
            name <- value
            handle |> ValueOption.iter _.set_Name(name)

    /// The size of the buffer in bytes.
    member x.Size = size

    /// <summary>
    /// Resizes the buffer.
    /// </summary>
    /// <param name="sizeInBytes">The new size in bytes.</param>
    /// <param name="forceImmediate">Indicates if the buffer is resized immediately or lazily.</param>
    member x.Resize(sizeInBytes : uint64, [<Optional; DefaultParameterValue(false)>] forceImmediate : bool) =
        lock x (fun _ ->
            if sizeInBytes <> x.Size then
                size <- sizeInBytes

                if forceImmediate then
                    x.ComputeHandle(discardOnResize) |> ignore

                transact x.MarkOutdated
        )

    /// <summary>
    /// Writes data to the buffer.
    /// </summary>
    /// <param name="data">The data to write.</param>
    /// <param name="offset">The offset (in bytes) into the buffer.</param>
    /// <param name="sizeInBytes">The number of bytes to write to the buffer.</param>
    /// <param name="discard">Indicates whether the current content of the buffer may be discarded. Default is <c>false</c>.</param>
    member x.Write(data : nativeint, offset : uint64, sizeInBytes : uint64,
                   [<Optional; DefaultParameterValue(false)>] discard : bool) =
        lock x (fun _ ->
            assert (offset + sizeInBytes <= x.Size)
            let handle = x.ComputeHandle(discardOnResize)
            runtime.Upload(data, handle, offset, sizeInBytes, discard)
        )

    override x.Create() =
        x.ComputeHandle(true) |> ignore

    override x.Destroy() =
        handle |> ValueOption.iter Disposable.dispose
        handle <- ValueNone
        size <- 0UL

    override x.Compute(_, _) =
        x.ComputeHandle(discardOnResize)

    interface IAdaptiveBuffer with
        member x.Runtime = runtime
        member x.Name with get() = x.Name and set name = x.Name <- name
        member x.Size = x.Size
        member x.Resize(sizeInBytes, forceImmediate) = x.Resize(sizeInBytes, forceImmediate)
        member x.Write(data, offset, sizeInBytes, discard) = x.Write(data, offset, sizeInBytes, discard)