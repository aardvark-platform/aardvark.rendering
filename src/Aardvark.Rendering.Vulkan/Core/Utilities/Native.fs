namespace Aardvark.Rendering.Vulkan

open System
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base

#nowarn "9"

[<AutoOpen>]
module internal NativeUtilities =

    type Converter<'a when 'a : unmanaged> private() =
        static let convert =
            let t = typeof<'a>
            if t.IsEnum then
                let bt = t.GetEnumUnderlyingType()
                if bt = typeof<uint8> then fun (a : 'a[]) -> a |> Array.map unbox<uint8> |> unbox<Array>
                elif bt = typeof<int8> then fun (a : 'a[]) -> a |> Array.map unbox<int8> |> unbox<Array>
                elif bt = typeof<uint16> then fun (a : 'a[]) -> a |> Array.map unbox<uint16> |> unbox<Array>
                elif bt = typeof<int16> then fun (a : 'a[]) -> a |> Array.map unbox<int16> |> unbox<Array>
                elif bt = typeof<uint32> then fun (a : 'a[]) -> a |> Array.map unbox<uint32> |> unbox<Array>
                elif bt = typeof<int32> then fun (a : 'a[]) -> a |> Array.map unbox<int32> |> unbox<Array>
                elif bt = typeof<uint64> then fun (a : 'a[]) -> a |> Array.map unbox<uint64> |> unbox<Array>
                elif bt = typeof<int64> then fun (a : 'a[]) -> a |> Array.map unbox<int64> |> unbox<Array>
                else fun (a : 'a[]) -> a :> System.Array
            else
                fun (a : 'a[]) -> a :> System.Array

        static member Convert(v : 'a[]) = convert v

    type NativeBuilder() =

        member inline x.Bind<'a,'r when 'a : unmanaged>(value : 'a, f : nativeptr<'a> -> 'r) =
            let ptr = NativePtr.alloc 1
            NativePtr.write ptr value
            try f ptr
            finally NativePtr.free ptr

        member inline x.Bind<'a,'r when 'a : unmanaged>(m : 'a[], f : nativeptr<'a> -> 'r) =
            if m.Length = 0 then
                f NativePtr.zero
            else
                let gc = GCHandle.Alloc(Converter<'a>.Convert m, GCHandleType.Pinned)
                try f (NativePtr.ofNativeInt (gc.AddrOfPinnedObject()))
                finally gc.Free()

        member inline x.Bind<'r>(value : string, f : nativeptr<byte> -> 'r) =
            let ptr = CStr.malloc value
            try f ptr
            finally CStr.free ptr

        member inline x.Bind<'r>(values : string[], f : nativeptr<nativeptr<byte>> -> 'r) =
            let arr = values |> Array.map CStr.malloc
            let gc = GCHandle.Alloc(arr, GCHandleType.Pinned)
            try
                f (NativePtr.ofNativeInt (gc.AddrOfPinnedObject()))
            finally
                gc.Free()
                for ptr in arr do CStr.free ptr


        member inline x.Bind<'a,'r when 'a : unmanaged>(m : Option<'a>, f : nativeptr<'a> -> 'r) =
            match m with
            | None ->
                f NativePtr.zero
            | Some v ->
                let ptr = NativePtr.alloc 1
                NativePtr.write ptr v
                try f ptr
                finally NativePtr.free ptr

        member inline x.Bind<'a,'r when 'a : unmanaged>(m : list<'a>, f : nativeptr<'a> -> 'r) =
            match m with
            | [] ->
                f NativePtr.zero
            | _ ->
                let sa = sizeof<'a>
                let mutable ptr = Marshal.AllocHGlobal (4 * sa)
                try
                    let mutable cap = 4
                    let mutable cnt = 0
                    for e in m do
                        if cnt >= cap then
                            ptr <- Marshal.ReAllocHGlobal(ptr, nativeint (2 * cap * sa))
                            cap <- cap * 2
                        NativePtr.set (NativePtr.ofNativeInt ptr) cnt e
                        cnt <- cnt + 1
                    f (NativePtr.ofNativeInt ptr)
                finally
                    Marshal.FreeHGlobal ptr

        member inline x.Return(v : 'a) = v
        member inline x.Zero() = ()
        member inline x.Combine(l : unit, r : unit -> 'a) = r()
        member inline x.Delay(f : unit -> 'a) = f
        member inline x.Run(f : unit -> 'a) = f()

        member inline x.For(s : seq<'a>, f : 'a -> unit) =
            for e in s do f e

        member inline x.While(guard : unit -> bool, body : unit -> unit) =
            while guard() do
                body()

        member inline x.Using<'a, 'r when 'a :> IDisposable>(v : 'a, f : 'a -> 'r) =
            try
                f v
            finally
                v.Dispose()

    let native = NativeBuilder()

    let inline (!!) (v : nativeptr<'a>) = NativePtr.read v

    module NativePtr =

        let inline readOrEmpty (ptr : nativeptr< ^a>) =
            if NativePtr.isNull ptr then
                ((^a) : (static member Empty : ^a) ())
            else
                !!ptr
