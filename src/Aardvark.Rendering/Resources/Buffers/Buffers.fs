namespace Aardvark.Rendering

open System
open System.Runtime.InteropServices

/// Represents the type of storage used by a buffer.
type BufferStorage =

    /// Device local storage, which is most efficient for device access but cannot be accessed by the host directly.
    /// Suitable if the data is accessed only occasionally by the host.
    | Device = 0

    /// Host-visible storage, which can be accessed directly by the host.
    /// Suitable if the data is accessed regularly by the host.
    | Host = 1

/// Flags to specify how a buffer may be used.
[<Flags>]
type BufferUsage =
    | None                  = 0x00

    /// Buffer may be used as index buffer.
    | Index                 = 0x01

    /// Buffer may be used for indirect rendering.
    | Indirect              = 0x02

    /// Buffer may be used as vertex buffer.
    | Vertex                = 0x04

    /// Buffer may be used as uniform buffer.
    | Uniform               = 0x08

    /// Buffer may be used as storage buffer.
    | Storage               = 0x10

    /// Buffer may be used as source for copy operations.
    | Read                  = 0x20

    /// Buffer may be used as destination for copy operations.
    | Write                 = 0x40

    /// Buffer may be used as source and destination for copy operations (combination of Read and Write flags).
    | ReadWrite             = 0x60

    /// Buffer may be used as storage for acceleration structures.
    | AccelerationStructure = 0x80

    /// Equivalent to combination of all other usage flags.
    | All                   = 0xFF

type IBuffer =
    interface end

type INativeBuffer =
    inherit IBuffer
    abstract member SizeInBytes : nativeint
    abstract member Use : (nativeint -> 'a) -> 'a
    abstract member Pin : unit -> nativeint
    abstract member Unpin : unit -> unit

type IBackendBuffer =
    inherit IBuffer
    inherit IBufferRange
    inherit IDisposable
    abstract member Runtime : IBufferRuntime
    abstract member Handle : uint64
    abstract member Name : string with get, set

and IExportedBackendBuffer =
    inherit IBackendBuffer
    inherit IExportedResource

and IBufferRange =
    abstract member Buffer : IBackendBuffer
    abstract member Offset : nativeint
    abstract member SizeInBytes : nativeint

and IBufferVector<'T when 'T : unmanaged> =
    abstract member Buffer : IBackendBuffer
    abstract member Origin : int
    abstract member Delta : int
    abstract member Count : int

and IBufferRange<'T when 'T : unmanaged> =
    inherit IBufferRange
    inherit IBufferVector<'T>

and IBuffer<'T when 'T : unmanaged> =
    inherit IBuffer
    inherit IBufferRange<'T>
    inherit IDisposable

and IBufferRuntime =
    ///<summary>
    /// Prepares a buffer, allocating and uploading the data to GPU memory.
    /// If the given data is an IBackendBuffer the operation performs NOP.
    ///</summary>
    ///<param name="data">The data to upload to the buffer.</param>
    ///<param name="usage">The usage flags of the buffer. Default is BufferUsage.All.</param>
    ///<param name="storage">The type of storage that is preferred. Default is BufferStorage.Device.</param>
    abstract member PrepareBuffer : data : IBuffer *
                                    [<Optional; DefaultParameterValue(BufferUsage.All)>] usage : BufferUsage *
                                    [<Optional; DefaultParameterValue(BufferStorage.Device)>] storage : BufferStorage -> IBackendBuffer

    ///<summary>Creates a buffer.</summary>
    ///<param name="sizeInBytes">The size (in bytes) of the buffer.</param>
    ///<param name="usage">The usage flags of the buffer. Default is BufferUsage.All.</param>
    ///<param name="storage">The type of storage that is preferred. Default is BufferStorage.Device.</param>
    abstract member CreateBuffer : sizeInBytes : nativeint *
                                   [<Optional; DefaultParameterValue(BufferUsage.All)>] usage : BufferUsage *
                                   [<Optional; DefaultParameterValue(BufferStorage.Device)>] storage : BufferStorage -> IBackendBuffer

    ///<summary>Copies data from host memory to a buffer.</summary>
    ///<param name="src">Location of the data to copy.</param>
    ///<param name="dst">The buffer to copy data to.</param>
    ///<param name="dstOffset">Offset (in bytes) into the buffer.</param>
    ///<param name="sizeInBytes">Number of bytes to copy.</param>
    abstract member Upload : src : nativeint * dst : IBackendBuffer * dstOffset : nativeint * sizeInBytes : nativeint -> unit

    ///<summary>Copies data from a buffer to host memory.</summary>
    ///<param name="src">The buffer to copy data from.</param>
    ///<param name="srcOffset">Offset (in bytes) into the buffer.</param>
    ///<param name="dst">Location to copy the data to.</param>
    ///<param name="sizeInBytes">Number of bytes to copy.</param>
    abstract member Download : src : IBackendBuffer * srcOffset : nativeint * dst : nativeint * sizeInBytes : nativeint -> unit

    ///<summary>Asynchronously copies data from a buffer to host memory.</summary>
    ///<param name="src">The buffer to copy data from.</param>
    ///<param name="srcOffset">Offset (in bytes) into the buffer.</param>
    ///<param name="dst">Location to copy the data to.</param>
    ///<param name="sizeInBytes">Number of bytes to copy.</param>
    ///<returns>A function that blocks until the download is complete.</returns>
    abstract member DownloadAsync : src : IBackendBuffer * srcOffset : nativeint * dst : nativeint * sizeInBytes : nativeint -> (unit -> unit)

    ///<summary>Copies data from a buffer to another.</summary>
    ///<param name="src">The buffer to copy data from.</param>
    ///<param name="srcOffset">Offset (in bytes) into the source buffer.</param>
    ///<param name="dst">The buffer to copy data to.</param>
    ///<param name="dstOffset">Offset (in bytes) into the destination buffer.</param>
    ///<param name="sizeInBytes">Number of bytes to copy.</param>
    abstract member Copy : src : IBackendBuffer * srcOffset : nativeint * dst : IBackendBuffer * dstOffset : nativeint * sizeInBytes : nativeint -> unit