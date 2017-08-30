namespace Aardvark.Rendering.Vulkan

open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"

[<StructLayout(LayoutKind.Sequential)>]
type V2ui =
    struct
        val mutable public X : uint32
        val mutable public Y : uint32

        override x.ToString() = sprintf "[%A, %A]" x.X x.Y

        new(x,y) = { X = x; Y = y }
    end

[<StructLayout(LayoutKind.Sequential)>]
type V3ui =
    struct
        val mutable public X : uint32
        val mutable public Y : uint32
        val mutable public Z : uint32

        override x.ToString() = sprintf "[%A, %A, %A]" x.X x.Y x.Z

        new(x,y,z) = { X = x; Y = y; Z = z}
    end

[<StructLayout(LayoutKind.Sequential)>]
type V4ui =
    struct
        val mutable public X : uint32
        val mutable public Y : uint32
        val mutable public Z : uint32
        val mutable public W : uint32

        override x.ToString() = sprintf "[%A, %A, %A, %A]" x.X x.Y x.Z x.W

        new (x,y,z,w) = { X = x; Y = y; Z = z; W = w }
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

module ``Non Public Vulkan Functions`` =
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining )>]
    let myId x = x


module NativePtr =
    open ``Non Public Vulkan Functions``

    let inline step (offset : int) (ptr : nativeptr<'a>) : nativeptr<'a> =
        nativeint (sizeof<'a> * offset) + NativePtr.toNativeInt ptr |> NativePtr.ofNativeInt

    let inline cast (n : nativeptr<'a>) : nativeptr<'b> =
        n |> NativePtr.toNativeInt |> NativePtr.ofNativeInt
    
    let inline isNull (ptr : nativeptr<'a>) =
        ptr |> NativePtr.toNativeInt = 0n

    let malloc<'a when 'a: unmanaged> (size : int) =
        size * sizeof<'a> |> Marshal.AllocHGlobal |> NativePtr.ofNativeInt<'a>

    let free (ptr : nativeptr<'a>) =
        ptr |> NativePtr.toNativeInt |> Marshal.FreeHGlobal

    let inline pushStackArray (elements : seq<'a>) =
        let arr = elements |> Seq.toArray
        let ptr = NativePtr.stackalloc arr.Length
        for i in 0..arr.Length-1 do
            NativePtr.set ptr i arr.[i]
        ptr

    let zero<'a when 'a : unmanaged> : nativeptr<'a> = NativePtr.ofNativeInt 0n

    let inline stackallocWith size (f : nativeptr<'a> -> 'b) =
        let ptr = NativePtr.stackalloc size
        let r = f ptr
        myId r

    module Operators =
    
        let ( &+ ) (ptr : nativeptr<'a>) (count : int) =
            ptr |> step count

        let ( &- ) (ptr : nativeptr<'a>) (count : int) =
            ptr |> step (-count)

        let ( !! ) (ptr : nativeptr<'a>) =
            NativePtr.read ptr

module CStr =

    let writeTo (ptr : cstr) (str : System.String) =
        let arr = System.Text.ASCIIEncoding.ASCII.GetBytes str
        let mutable ptr = ptr
        for b in arr do
            NativePtr.write ptr b
            ptr <- ptr |> NativePtr.step 1
        NativePtr.write ptr 0uy
        ptr |> NativePtr.step 1

    let strlen(str : cstr) =
        let mutable l = 0
        while NativePtr.get str l <> 0uy do
            l <- l + 1
        l


    let inline salloc (str : string) =
        let ptr = NativePtr.stackalloc (str.Length - 1)
        str |> writeTo ptr |> ignore
        ptr

    let inline sallocMany (strs : seq<string>) =
        let mutable length = 0
        let mutable count = 0
        for s in strs do
            length <- length + s.Length + 1
            count <- count + 1

        let content = NativePtr.stackalloc length
        let ptrs = NativePtr.stackalloc count

        let mutable currentPtr = ptrs
        let mutable current = content
        for s in strs do
            NativePtr.write currentPtr current
            current <- s |> writeTo current
            currentPtr <- currentPtr |> NativePtr.step 1

        ptrs

    let malloc (str : string) =
        let ptr = NativePtr.malloc (str.Length + 1)
        str |> writeTo ptr |> ignore
        ptr

    let toString (str : cstr) =
        Marshal.PtrToStringAnsi(str |> NativePtr.toNativeInt)

