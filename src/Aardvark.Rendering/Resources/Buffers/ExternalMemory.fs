namespace Aardvark.Rendering

open System

/// Interface for external memory handles.
[<AllowNullLiteral>]
type IExternalMemoryHandle =
    inherit IDisposable
    abstract member Handle : nativeint

/// Represents a block of external memory.
type ExternalMemoryBlock =
    {
        /// The handle of the memory block.
        Handle : IExternalMemoryHandle

        /// The size of the memory block (in bytes).
        SizeInBytes : int64
    }

    member x.Dispose() =
        x.Handle.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

/// Represents a region of an external memory block.
type ExternalMemory =
    {
        /// The external memory block.
        Block : ExternalMemoryBlock

        /// Start offset of the memory region (in bytes).
        Offset : int64

        /// Size of the memory region (in bytes).
        Size : int64
    }

/// Interface for resources that may be backed by exported memory.
/// Used for sharing resources between different backends.
type IExportedResource =

    /// The external memory backing the resource.
    abstract member Memory : ExternalMemory