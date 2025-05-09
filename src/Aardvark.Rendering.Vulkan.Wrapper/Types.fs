﻿namespace Aardvark.Rendering.Vulkan

open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"

type ExtensionType =
    | Device = 0
    | Instance = 1

[<StructLayout(LayoutKind.Explicit, Size = 3)>]
type uint24 =
    struct
        [<FieldOffset(0)>]
        val mutable public B0 : uint8
        [<FieldOffset(1)>]
        val mutable public B1 : uint8
        [<FieldOffset(2)>]
        val mutable public B2 : uint8

        new(v : uint32) = { B0 = byte (v &&& 0xFFu); B1 = byte ((v >>> 8) &&& 0xFFu); B2 = byte ((v >>> 16) &&& 0xFFu) }
        new(v : int32) = uint24(uint32 v)

        static member op_Explicit(x : uint24) : uint32 = uint32 (x.B0) ||| uint32(x.B1 <<< 8) ||| uint32 (x.B2 <<< 16)
        static member op_Explicit(x : uint24) : int32 = x |> uint32 |> int32
    end

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

    //let writeTo (ptr : cstr) (str : System.String) =
    //    let arr = System.Text.ASCIIEncoding.ASCII.GetBytes str
    //    let mutable ptr = ptr
    //    for b in arr do
    //        NativePtr.write ptr b
    //        ptr <- ptr |> NativePtr.step 1
    //    NativePtr.write ptr 0uy
    //    ptr |> NativePtr.step 1

    let strlen(str : cstr) =
        let mutable l = 0
        while NativePtr.get str l <> 0uy do
            l <- l + 1
        l


    //let inline salloc (str : string) =
    //    let ptr = NativePtr.stackalloc (str.Length - 1)
    //    str |> writeTo ptr |> ignore
    //    ptr

    //[<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    //let suse (f : cstr -> 'r) (str : string) =
    //    let ptr = NativePtr.stackalloc (str.Length - 1)
    //    str |> writeTo ptr |> ignore
    //    f ptr

    //let inline sallocMany (strs : seq<string>) =
    //    let mutable length = 0
    //    let mutable count = 0
    //    for s in strs do
    //        length <- length + s.Length + 1
    //        count <- count + 1

    //    let content = NativePtr.stackalloc length
    //    let ptrs = NativePtr.stackalloc count

    //    let mutable currentPtr = ptrs
    //    let mutable current = content
    //    for s in strs do
    //        NativePtr.write currentPtr current
    //        current <- s |> writeTo current
    //        currentPtr <- currentPtr |> NativePtr.step 1

    //    ptrs

    //let susemany (f : int -> nativeptr<cstr> -> 'r) (strings : seq<string>) =
    //    let mutable length = 0
    //    let mutable count = 0
    //    for s in strings do
    //        length <- length + s.Length + 1
    //        count <- count + 1

    //    let content : byte[] = Array.zeroCreate length
        
    //    let gc = GCHandle.Alloc(content, GCHandleType.Pinned)
    //    try
    //        let pData = gc.AddrOfPinnedObject()
    //        let mutable i = 0
    //        let mutable o = 0
    //        let offsets = Array.zeroCreate count

    //        for s in strings do
    //            let arr = System.Text.Encoding.ASCII.GetBytes(s)
    //            offsets.[i] <- pData + nativeint o
    //            arr.CopyTo(content, o)
    //            content.[o + arr.Length] <- 0uy
    //            o <- o + arr.Length + 1
    //            i <- i + 1

                
    //        let gc = GCHandle.Alloc(offsets, GCHandleType.Pinned)
    //        try
    //            let pStrs = gc.AddrOfPinnedObject()
    //            f count (NativePtr.ofNativeInt pStrs)
    //        finally
    //            gc.Free()

    //    finally 
    //        gc.Free()


    let malloc (str : string) =
        Marshal.StringToHGlobalAnsi str |> NativePtr.ofNativeInt<byte>

    let toString (str : cstr) =
        Marshal.PtrToStringAnsi(str |> NativePtr.toNativeInt)

