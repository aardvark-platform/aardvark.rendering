namespace Aardvark.Rendering

open Aardvark.Base
open System
open System.Threading
open System.Runtime.InteropServices

/// Interface for external memory handles.
type IExternalMemoryHandle =
    inherit IDisposable
    abstract member IsValid : bool

/// Interface for blocks of external memory.
[<AllowNullLiteral>]
type IExternalMemoryBlock =
    inherit IDisposable
    abstract member Handle : IExternalMemoryHandle
    abstract member SizeInBytes : int64

[<AutoOpen>]
module NativePlatformHandles =

    module private Kernel32 =

        [<DllImport("Kernel32.dll")>]
        extern bool CloseHandle(nativeint handle)

    module private Posix =

        [<DllImport("libc")>]
        extern int close(int fd)

    [<AbstractClass>]
    type NativeHandle<'T>(handle : 'T) =
        let mutable isValid = 1

        member x.IsValid = isValid = 1
        member x.Handle = handle

        /// Performs the given action for the handle and invalidates it.
        member x.UseHandle(action : 'T -> 'U) =
            if Interlocked.Exchange(&isValid, 0) = 1 then
                action handle
            else
                failwithf "External memory handle %A is invalid" handle

        abstract member CloseHandle : unit -> bool

        member x.Dispose() =
            if Interlocked.Exchange(&isValid, 0) = 1 then
                if not <| x.CloseHandle() then
                    Log.warn "Could not close external memory handle."

        interface IExternalMemoryHandle with
            member x.IsValid = x.IsValid
            member x.Dispose() = x.Dispose()

    type Win32Handle(handle : nativeint) =
        inherit NativeHandle<nativeint>(handle)

        override x.CloseHandle() = Kernel32.CloseHandle handle

    type PosixHandle(handle : int) =
        inherit NativeHandle<int>(handle)

        override x.CloseHandle() = Posix.close handle = 0

/// Represents a region of an external memory block.
[<Struct>]
type ExternalMemory =
    {
        /// The external memory block.
        Block : IExternalMemoryBlock

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