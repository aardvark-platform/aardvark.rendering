namespace Aardvark.Base

open System
open FSharp.Data.Adaptive

[<Flags>]
type BufferUsage = // Buffer usage
    | None = 0
    | Index = 1
    | Indirect = 2
    | Vertex = 4
    | Uniform = 8
    | Storage = 16
    | Read = 256
    | Write = 512
    | ReadWrite = 0x300
    | Default = 0x031f

type IBuffer = interface end

type INativeBuffer =
    inherit IBuffer
    abstract member SizeInBytes : int
    abstract member Use : (nativeint -> 'a) -> 'a
    abstract member Pin : unit -> nativeint
    abstract member Unpin : unit -> unit

type IAdaptiveBuffer =
    inherit IAdaptiveValue<IBuffer>
    abstract member GetReader : unit -> IAdaptiveBufferReader

and IAdaptiveBufferReader =
    inherit IAdaptiveObject
    inherit IDisposable
    abstract member GetDirtyRanges : AdaptiveToken -> INativeBuffer * RangeSet

type IBackendBuffer =
    inherit IBuffer // ISSUE: this allows a backend buffer to be used everywhere even if it is restricted to a specific type -> HOWEVER buffers can have multiple mixed usage flags -> interface restriction not possible
    inherit IDisposable
    abstract member Runtime : IBufferRuntime
    abstract member Handle : obj
    abstract member SizeInBytes : nativeint

and IBufferRuntime =
    /// Deletes a buffer and releases all GPU resources and API handles
    abstract member DeleteBuffer : IBackendBuffer -> unit

    /// Prepares a buffer, allocating and uploading the data to GPU memory
    /// If the buffer is an IBackendBuffer the operation performs NOP
    abstract member PrepareBuffer : data : IBuffer * usage : BufferUsage * sync : TaskSync -> IBackendBuffer

    /// Creates a GPU buffer with the specified size in bytes and usage
    abstract member CreateBuffer : size : nativeint * usage : BufferUsage -> IBackendBuffer

    abstract member Copy : srcData : nativeint * dst : IBackendBuffer * dstOffset : nativeint * size : nativeint * sync : TaskSync -> unit
    abstract member Copy : srcBuffer : IBackendBuffer * srcOffset : nativeint * dstData : nativeint * size : nativeint * sync : TaskSync -> unit
    abstract member Copy : srcBuffer : IBackendBuffer * srcOffset : nativeint * dstBuffer : IBackendBuffer * dstOffset : nativeint * size : nativeint * sync : TaskSync -> unit

    abstract member CopyAsync : srcBuffer : IBackendBuffer * srcOffset : nativeint * dstData : nativeint * size : nativeint -> (unit -> unit)

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
            member x.Dispose() = buffer.Runtime.DeleteBuffer buffer

    let nsa<'a when 'a : unmanaged> = nativeint sizeof<'a>