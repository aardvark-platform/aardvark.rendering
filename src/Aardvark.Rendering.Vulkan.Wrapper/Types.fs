namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open System
open System.Text
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"

type ExtensionType =
    | Device = 0
    | Instance = 1

type DeviceVendor =
    | Unknown = 0
    | Nvidia = 1
    | Intel = 2
    | AMD = 3
    | Qualcomm = 4
    | Samsung = 5

    | ThreeDFX = 6
    | ARM = 7
    | Broadcom = 8
    | Matrox = 9
    | SiS = 10
    | VIA = 11


[<StructLayout(LayoutKind.Explicit, Size = 256)>]
type String256 =
    struct
        member x.Value =
            let ptr = &&x |> NativePtr.toNativeInt //NativePtr.stackalloc 1
            Marshal.PtrToStringAnsi(ptr)

        override x.ToString() = x.Value
    end

type cstr = nativeptr<byte>

module CStr =

    [<Struct>]
    type Handle =
        val Value : cstr
        new (ptr: cstr) = { Value = ptr }

        member this.Dispose() = NativePtr.free this.Value

        interface IDisposable with
            member this.Dispose() = this.Dispose()

    /// Length in bytes.
    let sizeInBytes (str: cstr) =
        if NativePtr.isNullPtr str then 0
        else
            let mutable count = 0
            while str.[count] <> 0uy do inc &count
            count

    /// Length in UTF8 characters.
    let length (str: cstr) =
        let size = sizeInBytes str
        Encoding.UTF8.GetCharCount(str, size)

    /// Allocates a native null-terminated UTF-8 string on the heap.
    let malloc (str: string) =
        if isNull str then NativePtr.zero
        else
            let size = Encoding.UTF8.GetByteCount str
            let ptr = NativePtr.alloc (size + 1)
            use pSrc = fixed str
            Encoding.UTF8.GetBytes(pSrc, str.Length, ptr, size) |> ignore
            ptr.[size] <- 0uy
            ptr

    /// Frees a native string.
    let inline free (str: cstr) =
        NativePtr.free str

    /// Allocates a native null-terminated UTF-8 string on the heap, returning a disposable handle.
    let inline pinned (str: string) =
        new Handle(malloc str)

    /// Temporarily allocates a native null-terminated UTF-8 string.
    let inline using (str: string) ([<InlineIfLambda>] action: cstr -> 'T) =
        use cstr = pinned str
        action cstr.Value

    /// Converts a native null-terminated UTF-8 string to a .NET string.
    let inline toString (str: cstr) =
        if NativePtr.isNullPtr str then null
        else
            let size = sizeInBytes str
            Encoding.UTF8.GetString(str, size)