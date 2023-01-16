namespace Aardvark.Rendering

open System.Runtime.CompilerServices

[<AbstractClass; Sealed; Extension>]
type IBufferRuntimeExtensions private() =

    /// Deletes a buffer and releases all GPU resources and API handles
    [<Extension>]
    static member DeleteBuffer(this : IBufferRuntime, buffer : IBackendBuffer) =
        buffer.Dispose()


[<AbstractClass; Sealed; Extension>]
type IBackendBufferExtensions private() =

    ///<summary>Copies data from host memory to a buffer.</summary>
    ///<param name="dst">The buffer to copy data to.</param>
    ///<param name="dstOffset">Offset (in bytes) into the buffer.</param>
    ///<param name="src">Location of the data to copy.</param>
    ///<param name="sizeInBytes">Number of bytes to copy.</param>
    [<Extension>]
    static member Upload(dst : IBackendBuffer, dstOffset : nativeint, src : nativeint, sizeInBytes : nativeint) =
        dst.Runtime.Upload(src, dst, dstOffset, sizeInBytes)

    ///<summary>Copies data from a buffer to host memory.</summary>
    ///<param name="src">The buffer to copy data from.</param>
    ///<param name="srcOffset">Offset (in bytes) into the buffer.</param>
    ///<param name="dst">Location to copy the data to.</param>
    ///<param name="sizeInBytes">Number of bytes to copy.</param>
    [<Extension>]
    static member Download(src : IBackendBuffer, srcOffset : nativeint, dst : nativeint, sizeInBytes : nativeint) =
        src.Runtime.Download(src, srcOffset, dst, sizeInBytes)

    ///<summary>Copies data from a buffer to another.</summary>
    ///<param name="src">The buffer to copy data from.</param>
    ///<param name="srcOffset">Offset (in bytes) into the source buffer.</param>
    ///<param name="dst">The buffer to copy data to.</param>
    ///<param name="dstOffset">Offset (in bytes) into the destination buffer.</param>
    ///<param name="sizeInBytes">Number of bytes to copy.</param>
    [<Extension>]
    static member CopyTo(src : IBackendBuffer, srcOffset : nativeint, dst : IBackendBuffer, dstOffset : nativeint, sizeInBytes : nativeint) =
        src.Runtime.Copy(src, srcOffset, dst, dstOffset, sizeInBytes)