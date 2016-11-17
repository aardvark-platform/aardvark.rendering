namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Collections.Generic
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base

#nowarn "9"
#nowarn "51"

[<AutoOpen>]
module private Utilities =
    let check (str : string) (err : VkResult) =
        if err <> VkResult.VkSuccess then failwithf "[Vulkan] %s" str

    let checkf (fmt : Printf.StringFormat<'a, VkResult -> unit>) =
        Printf.kprintf (fun str ->
            fun (res : VkResult) ->
                if res <> VkResult.VkSuccess then failwith ("[Vulkan] " + str)
        ) fmt

    let inline failf fmt = Printf.kprintf (fun str -> failwith ("[Vulkan] " + str)) fmt

[<AutoOpen>]
module BaseLibExtensions = 
    module NativePtr =
        let withA (f : nativeptr<'a> -> 'b) (a : 'a[]) =
            let gc = GCHandle.Alloc(a, GCHandleType.Pinned)
            try f (gc.AddrOfPinnedObject() |> NativePtr.ofNativeInt)
            finally gc.Free()

    type Version with
        member v.ToVulkan() =
            ((uint32 v.Major) <<< 22) ||| ((uint32 v.Minor) <<< 12) ||| (uint32 v.Build)

        static member FromVulkan (v : uint32) =
            Version(int (v >>> 22), int ((v >>> 12) &&& 0x3FFu), int (v &&& 0xFFFu))

    type V2i with
        static member OfExtent (e : VkExtent2D) =
            V2i(int e.width, int e.height)
        
        member x.ToExtent() =
            VkExtent2D(uint32 x.X, uint32 x.Y)

    type V3i with
        static member OfExtent (e : VkExtent3D) =
            V3i(int e.width, int e.height, int e.depth)
        
        member x.ToExtent() =
            VkExtent3D(uint32 x.X, uint32 x.Y, uint32 x.Z)

    module VkRaw =
        let warn fmt = Printf.kprintf (fun str -> Report.Warn("[Vulkan] {0}", str)) fmt

        let debug fmt = Printf.kprintf (fun str -> Report.Line(2, "[Vulkan] {0}", str)) fmt

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module private Alignment = 
    let prev (align : int64) (v : int64) =
        let r = v % align
        if r = 0L then v
        else v - r

    let next (align : int64) (v : int64) =
        let r = v % align
        if r = 0L then v
        else align + v - r


[<AbstractClass>]
type VulkanObject() =
    let mutable isDisposed = 0
    abstract member Release : unit -> unit


    member x.IsDisposed = isDisposed <> 0

    member inline private x.Dispose(disposing : bool) =
        let o = Interlocked.Exchange(&isDisposed, 1)
        if o = 0 then
            x.Release()
            if disposing then GC.SuppressFinalize x

    member x.Dispose() = x.Dispose true
    override x.Finalize() = x.Dispose false

    interface IDisposable with
        member x.Dispose() = x.Dispose()


module NativeUtilities = 

    type V2n =
        struct
            val mutable public X : nativeint
            val mutable public Y : nativeint

            static member Zero = V2n(0n, 0n)
            static member OO = V2n(0n, 0n)
            static member OI = V2n(0n, 1n)
            static member IO = V2n(1n, 0n)
            static member II = V2n(1n, 1n)

            static member (+) (l : V2n, r : V2n) = V2n(l.X + r.X, l.Y + r.Y)
            static member (-) (l : V2n, r : V2n) = V2n(l.X - r.X, l.Y - r.Y)
            static member (*) (l : V2n, r : V2n) = V2n(l.X * r.X, l.Y * r.Y)
            static member (/) (l : V2n, r : V2n) = V2n(l.X / r.X, l.Y / r.Y)
            static member (*) (l : V2n, r : nativeint) = V2n(l.X * r, l.Y * r)
            static member (/) (l : V2n, r : nativeint) = V2n(l.X / r, l.Y / r)
            static member (*) (l : nativeint, r : V2n) = V2n(l * r.X, l * r.Y)
            static member (/) (l : nativeint, r : V2n) = V2n(l / r.X, l / r.Y)

            static member Dot(l : V2n, r : V2n) = l.X * r.X + l.Y * r.Y

            new(x : nativeint, y : nativeint) = { X = x; Y = y }
            
        end

    type V3n =
        struct
            val mutable public X : nativeint
            val mutable public Y : nativeint
            val mutable public Z : nativeint

            
            static member Zero = V3n(0n, 0n, 0n)
            static member OOO = V3n(0n, 0n, 0n)
            static member OOI = V3n(0n, 0n, 1n)
            static member OIO = V3n(0n, 1n, 0n)
            static member OII = V3n(0n, 1n, 1n)
            static member IOO = V3n(1n, 0n, 0n)
            static member IOI = V3n(1n, 0n, 1n)
            static member IIO = V3n(1n, 1n, 0n)
            static member III = V3n(1n, 1n, 1n)

            member x.XY = V2n(x.X, x.Y)
            member x.XZ = V2n(x.X, x.Z)
            member x.YZ = V2n(x.Y, x.Z)

            static member (+) (l : V3n, r : V3n) = V3n(l.X + r.X, l.Y + r.Y, l.Z + r.Z)
            static member (-) (l : V3n, r : V3n) = V3n(l.X - r.X, l.Y - r.Y, l.Z - r.Z)
            static member (*) (l : V3n, r : V3n) = V3n(l.X * r.X, l.Y * r.Y, l.Z * r.Z)
            static member (/) (l : V3n, r : V3n) = V3n(l.X / r.X, l.Y / r.Y, l.Z / r.Z)
            static member (*) (l : V3n, r : nativeint) = V3n(l.X * r, l.Y * r, l.Z * r)
            static member (/) (l : V3n, r : nativeint) = V3n(l.X / r, l.Y / r, l.Z / r)
            static member (*) (l : nativeint, r : V3n) = V3n(l * r.X, l * r.Y, l * r.Z)
            static member (/) (l : nativeint, r : V3n) = V3n(l / r.X, l / r.Y, l / r.Z)


            static member Dot(l : V3n, r : V3n) = l.X * r.X + l.Y * r.Y + l.Z * r.Z

            new(x,y,z) = { X = x; Y = y; Z = z}
        end

    type V4n =
        struct
            val mutable public X : nativeint
            val mutable public Y : nativeint
            val mutable public Z : nativeint
            val mutable public W : nativeint

            
            static member Zero = V4n(0n, 0n, 0n, 0n)
            static member OOOO = V4n(0n, 0n, 0n, 0n)
            static member OOOI = V4n(0n, 0n, 0n, 1n)
            static member OOIO = V4n(0n, 0n, 1n, 0n)
            static member OOII = V4n(0n, 0n, 1n, 1n)
            static member OIOO = V4n(0n, 1n, 0n, 0n)
            static member OIOI = V4n(0n, 1n, 0n, 1n)
            static member OIIO = V4n(0n, 1n, 1n, 0n)
            static member OIII = V4n(0n, 1n, 1n, 1n)
            static member IOOO = V4n(1n, 0n, 0n, 0n)
            static member IOOI = V4n(1n, 0n, 0n, 1n)
            static member IOIO = V4n(1n, 0n, 1n, 0n)
            static member IOII = V4n(1n, 0n, 1n, 1n)
            static member IIOO = V4n(1n, 1n, 0n, 0n)
            static member IIOI = V4n(1n, 1n, 0n, 1n)
            static member IIIO = V4n(1n, 1n, 1n, 0n)
            static member IIII = V4n(1n, 1n, 1n, 1n)

            member x.XY = V2n(x.X, x.Y)
            member x.XZ = V2n(x.X, x.Z)
            member x.XW = V2n(x.X, x.W)
            member x.YZ = V2n(x.Y, x.Z)
            member x.YW = V2n(x.Y, x.W)
            member x.ZW = V2n(x.Z, x.W)

            member x.XYZ = V3n(x.X, x.Y, x.Z)
            member x.YZW = V3n(x.Y, x.Z, x.W)

            static member (+) (l : V4n, r : V4n) = V4n(l.X + r.X, l.Y + r.Y, l.Z + r.Z, l.W + r.W)
            static member (-) (l : V4n, r : V4n) = V4n(l.X - r.X, l.Y - r.Y, l.Z - r.Z, l.W - r.W)
            static member (*) (l : V4n, r : V4n) = V4n(l.X * r.X, l.Y * r.Y, l.Z * r.Z, l.W * r.W)
            static member (/) (l : V4n, r : V4n) = V4n(l.X / r.X, l.Y / r.Y, l.Z / r.Z, l.W / r.W)
            static member (*) (l : V4n, r : nativeint) = V4n(l.X * r, l.Y * r, l.Z * r, l.W * r)
            static member (/) (l : V4n, r : nativeint) = V4n(l.X / r, l.Y / r, l.Z / r, l.W / r)
            static member (*) (l : nativeint, r : V4n) = V4n(l * r.X, l * r.Y, l * r.Z, l * r.W)
            static member (/) (l : nativeint, r : V4n) = V4n(l / r.X, l / r.Y, l / r.Z, l / r.W)


            static member Dot(l : V4n, r : V4n) = l.X * r.X + l.Y * r.Y + l.Z * r.Z + l.W * r.W


            new(x,y,z,w) = { X = x; Y = y; Z = z; W = w}
        end


    type NativeVolume<'a when 'a : unmanaged>(ptr : nativeptr<'a>, info : VolumeInfo) =
        
        member x.Pointer = ptr
        member x.Info = info

        member x.SX = info.SX
        member x.SY = info.SY
        member x.SZ = info.SZ

        member x.SubVolume(start : V3i, size : V3i) = NativeVolume<'a>(ptr, info.SubVolume(start, size))
        member x.SubVolume(start : V3l, size : V3l) = NativeVolume<'a>(ptr, info.SubVolume(start, size))
        member x.SubVolume(start : V3i, size : V3i, delta : V3i) = NativeVolume<'a>(ptr, info.SubVolume(start, size, delta))
        member x.SubVolume(start : V3l, size : V3l, delta : V3l) = NativeVolume<'a>(ptr, info.SubVolume(start, size, delta))
        member x.SubVolume(beginX : int64, beginY : int64, beginZ : int64, sizeX : int64, sizeY : int64, sizeZ : int64) = 
            NativeVolume<'a>(ptr, info.SubVolume(beginX, beginY, beginZ, sizeX, sizeY, sizeZ))
        
        member x.SubVolume(beginX : int64, beginY : int64, beginZ : int64, sizeX : int64, sizeY : int64, sizeZ : int64, deltaX : int64, deltaY : int64, deltaZ : int64) = 
            NativeVolume<'a>(ptr, info.SubVolume(beginX, beginY, beginZ, sizeX, sizeY, sizeZ, deltaX, deltaY, deltaZ))

    module NativeVolume =
        let inline private copyXYZ (src : NativeVolume<'a>) (dst : NativeVolume<'a>) =
            let srcInfo = src.Info
            let dstInfo = dst.Info
            let sa = sizeof<'a> |> nativeint

            let size = V3l(min srcInfo.SX dstInfo.SX, min srcInfo.SY dstInfo.SY, min srcInfo.SZ dstInfo.SZ)

            let mutable sp = src.Pointer |> NativePtr.toNativeInt
            let mutable dp = dst.Pointer |> NativePtr.toNativeInt
            sp <- sp + nativeint srcInfo.Origin * sa
            dp <- dp + nativeint dstInfo.Origin * sa

            let xs  = nativeint (size.X * srcInfo.DX) * sa
            let ys  = nativeint (size.Y * srcInfo.DY) * sa
            let zs  = nativeint (size.Z * srcInfo.DZ) * sa

            let sxj = nativeint (srcInfo.DX - size.Y * srcInfo.DY) * sa
            let dxj = nativeint (dstInfo.DX - size.Y * dstInfo.DY) * sa
            let syj = nativeint (srcInfo.DY - size.Z * srcInfo.DZ) * sa
            let dyj = nativeint (dstInfo.DY - size.Z * dstInfo.DZ) * sa
            let szj = nativeint (srcInfo.DZ) * sa
            let dzj = nativeint (dstInfo.DZ) * sa

            let xe = sp + xs
            while sp <> xe do
                let ye = sp + ys
                while sp <> ye do
                    let ze = sp + zs
                    while sp <> ze do
                        NativePtr.write (NativePtr.ofNativeInt<'a> dp) (NativePtr.read (NativePtr.ofNativeInt<'a> sp))
                        sp <- sp + szj
                        dp <- dp + dzj

                    sp <- sp + syj
                    dp <- dp + dyj

                sp <- sp + sxj
                dp <- dp + dxj

        let inline private copyYXZ (src : NativeVolume<'a>) (dst : NativeVolume<'a>) =
            let srcInfo = src.Info
            let dstInfo = dst.Info
            let sa = sizeof<'a> |> nativeint

            let size = V3l(min srcInfo.SX dstInfo.SX, min srcInfo.SY dstInfo.SY, min srcInfo.SZ dstInfo.SZ)

            let mutable sp = src.Pointer |> NativePtr.toNativeInt
            let mutable dp = dst.Pointer |> NativePtr.toNativeInt
            sp <- sp + nativeint srcInfo.Origin * sa
            dp <- dp + nativeint dstInfo.Origin * sa

            let xs  = nativeint (size.X * srcInfo.DX) * sa
            let ys  = nativeint (size.Y * srcInfo.DY) * sa
            let zs  = nativeint (size.Z * srcInfo.DZ) * sa

            let syj = nativeint (srcInfo.DY - size.X * srcInfo.DX) * sa
            let dyj = nativeint (dstInfo.DY - size.X * dstInfo.DX) * sa
            let sxj = nativeint (srcInfo.DX - size.Z * srcInfo.DZ) * sa
            let dxj = nativeint (dstInfo.DX - size.Z * dstInfo.DZ) * sa
            let szj = nativeint (srcInfo.DZ) * sa
            let dzj = nativeint (dstInfo.DZ) * sa

            let ye = sp + ys
            while sp <> ye do
                let xe = sp + xs
                while sp <> xe do
                    let ze = sp + zs
                    while sp <> ze do
                        NativePtr.write (NativePtr.ofNativeInt<'a> dp) (NativePtr.read (NativePtr.ofNativeInt<'a> sp))
                        sp <- sp + szj
                        dp <- dp + dzj

                    sp <- sp + sxj
                    dp <- dp + dxj

                sp <- sp + syj
                dp <- dp + dyj
        
        let inline private copyXZY (src : NativeVolume<'a>) (dst : NativeVolume<'a>) =
            let srcInfo = src.Info
            let dstInfo = dst.Info
            let sa = sizeof<'a> |> nativeint

            let size = V3l(min srcInfo.SX dstInfo.SX, min srcInfo.SY dstInfo.SY, min srcInfo.SZ dstInfo.SZ)

            let mutable sp = src.Pointer |> NativePtr.toNativeInt
            let mutable dp = dst.Pointer |> NativePtr.toNativeInt
            sp <- sp + nativeint srcInfo.Origin * sa
            dp <- dp + nativeint dstInfo.Origin * sa

            let xs  = nativeint (size.X * srcInfo.DX) * sa
            let ys  = nativeint (size.Y * srcInfo.DY) * sa
            let zs  = nativeint (size.Z * srcInfo.DZ) * sa

            let sxj = nativeint (srcInfo.DX - size.Z * srcInfo.DZ) * sa
            let dxj = nativeint (dstInfo.DX - size.Z * dstInfo.DZ) * sa
            let szj = nativeint (srcInfo.DZ - size.Y * srcInfo.DY) * sa
            let dzj = nativeint (dstInfo.DZ - size.Y * dstInfo.DY) * sa
            let syj = nativeint (srcInfo.DY) * sa
            let dyj = nativeint (dstInfo.DY) * sa

            let xe = sp + xs
            while sp <> xe do
                let ze = sp + zs
                while sp <> ze do
                    let ye = sp + ys
                    while sp <> ye do
                        NativePtr.write (NativePtr.ofNativeInt<'a> dp) (NativePtr.read (NativePtr.ofNativeInt<'a> sp))
                        sp <- sp + syj
                        dp <- dp + dyj

                    sp <- sp + szj
                    dp <- dp + dzj

                sp <- sp + sxj
                dp <- dp + dxj

        let inline private copyYZX (src : NativeVolume<'a>) (dst : NativeVolume<'a>) =
            let srcInfo = src.Info
            let dstInfo = dst.Info
            let sa = sizeof<'a> |> nativeint

            let size = V3l(min srcInfo.SX dstInfo.SX, min srcInfo.SY dstInfo.SY, min srcInfo.SZ dstInfo.SZ)

            let mutable sp = src.Pointer |> NativePtr.toNativeInt
            let mutable dp = dst.Pointer |> NativePtr.toNativeInt
            sp <- sp + nativeint srcInfo.Origin * sa
            dp <- dp + nativeint dstInfo.Origin * sa

            let xs  = nativeint (size.X * srcInfo.DX) * sa
            let ys  = nativeint (size.Y * srcInfo.DY) * sa
            let zs  = nativeint (size.Z * srcInfo.DZ) * sa

            let syj = nativeint (srcInfo.DY - size.Z * srcInfo.DZ) * sa
            let dyj = nativeint (dstInfo.DY - size.Z * dstInfo.DZ) * sa
            let szj = nativeint (srcInfo.DZ - size.X * srcInfo.DX) * sa
            let dzj = nativeint (dstInfo.DZ - size.X * dstInfo.DX) * sa
            let sxj = nativeint (srcInfo.DX) * sa
            let dxj = nativeint (dstInfo.DX) * sa

            let ye = sp + ys
            while sp <> ye do
                let ze = sp + zs
                while sp <> ze do
                    let xe = sp + xs
                    while sp <> xe do
                        NativePtr.write (NativePtr.ofNativeInt<'a> dp) (NativePtr.read (NativePtr.ofNativeInt<'a> sp))
                        sp <- sp + sxj
                        dp <- dp + dxj

                    sp <- sp + szj
                    dp <- dp + dzj

                sp <- sp + syj
                dp <- dp + dyj

        let inline private copyZXY (src : NativeVolume<'a>) (dst : NativeVolume<'a>) =
            let srcInfo = src.Info
            let dstInfo = dst.Info
            let sa = sizeof<'a> |> nativeint

            let size = V3l(min srcInfo.SX dstInfo.SX, min srcInfo.SY dstInfo.SY, min srcInfo.SZ dstInfo.SZ)

            let mutable sp = src.Pointer |> NativePtr.toNativeInt
            let mutable dp = dst.Pointer |> NativePtr.toNativeInt
            sp <- sp + nativeint srcInfo.Origin * sa
            dp <- dp + nativeint dstInfo.Origin * sa

            let xs  = nativeint (size.X * srcInfo.DX) * sa
            let ys  = nativeint (size.Y * srcInfo.DY) * sa
            let zs  = nativeint (size.Z * srcInfo.DZ) * sa

            let szj = nativeint (srcInfo.DZ - size.X * srcInfo.DX) * sa
            let dzj = nativeint (dstInfo.DZ - size.X * dstInfo.DX) * sa
            let sxj = nativeint (srcInfo.DX - size.Y * srcInfo.DY) * sa
            let dxj = nativeint (dstInfo.DX - size.Y * dstInfo.DY) * sa
            let syj = nativeint (srcInfo.DY) * sa
            let dyj = nativeint (dstInfo.DY) * sa

            let ze = sp + zs
            while sp <> ze do
                let xe = sp + xs
                while sp <> xe do
                    let ye = sp + ys
                    while sp <> ye do
                        NativePtr.write (NativePtr.ofNativeInt<'a> dp) (NativePtr.read (NativePtr.ofNativeInt<'a> sp))
                        sp <- sp + syj
                        dp <- dp + dyj

                    sp <- sp + sxj
                    dp <- dp + dxj

                sp <- sp + szj
                dp <- dp + dzj

        let inline private copyZYX (src : NativeVolume<'a>) (dst : NativeVolume<'a>) =
            let srcInfo = src.Info
            let dstInfo = dst.Info
            let sa = sizeof<'a> |> nativeint

            let size = V3l(min srcInfo.SX dstInfo.SX, min srcInfo.SY dstInfo.SY, min srcInfo.SZ dstInfo.SZ)

            let mutable sp = src.Pointer |> NativePtr.toNativeInt
            let mutable dp = dst.Pointer |> NativePtr.toNativeInt
            sp <- sp + nativeint srcInfo.Origin * sa
            dp <- dp + nativeint dstInfo.Origin * sa

            let xs  = nativeint (size.X * srcInfo.DX) * sa
            let ys  = nativeint (size.Y * srcInfo.DY) * sa
            let zs  = nativeint (size.Z * srcInfo.DZ) * sa

            let szj = nativeint (srcInfo.DZ - size.Y * srcInfo.DY) * sa
            let dzj = nativeint (dstInfo.DZ - size.Y * dstInfo.DY) * sa
            let syj = nativeint (srcInfo.DY - size.X * srcInfo.DX) * sa
            let dyj = nativeint (dstInfo.DY - size.X * dstInfo.DX) * sa
            let sxj = nativeint (srcInfo.DX) * sa
            let dxj = nativeint (dstInfo.DX) * sa

            let ze = sp + zs
            while sp <> ze do
                let ye = sp + ys
                while sp <> ye do
                    let xe = sp + xs
                    while sp <> xe do
                        NativePtr.write (NativePtr.ofNativeInt<'a> dp) (NativePtr.read (NativePtr.ofNativeInt<'a> sp))
                        sp <- sp + sxj
                        dp <- dp + dxj

                    sp <- sp + syj
                    dp <- dp + dyj

                sp <- sp + szj
                dp <- dp + dzj

        let copy (src : NativeVolume<'a>) (dst : NativeVolume<'a>) =
            let cxy = compare (abs src.Info.DX) (abs src.Info.DY)
            let cyz = compare (abs src.Info.DY) (abs src.Info.DZ)
            let cxz = compare (abs src.Info.DX) (abs src.Info.DZ)

            if cxz > 0 && cyz > 0 then
                // z < x && z < y
                if cxy < 0 then 
                    // x < y
                    copyYXZ src dst
                else
                    // x >= y
                    copyXYZ src dst

            elif cxy > 0 && cyz < 0 then
                // y < x && y < z
                if cxz > 0 then
                    // z < x
                    copyXZY src dst
                else
                    // z >= x
                    copyZXY src dst
            else
                // x <= y && x <= z
                if cyz > 0 then
                    // z < y
                    copyYZX src dst
                else
                    // z >= y
                    copyZYX src dst

        


        let private setXYZ (src : 'a) (dst : NativeVolume<'a>)  =
            let dstInfo = dst.Info
            let sa = sizeof<'a> |> nativeint

            let mutable ptr = dst.Pointer |> NativePtr.toNativeInt
            ptr <- ptr + nativeint dstInfo.Origin * sa

            let xs  = nativeint (dstInfo.SX * dstInfo.DX) * sa
            let ys  = nativeint (dstInfo.SY * dstInfo.DY) * sa
            let zs  = nativeint (dstInfo.SZ * dstInfo.DZ) * sa

            let xj = nativeint (dstInfo.DX - dstInfo.SY * dstInfo.DY) * sa
            let yj = nativeint (dstInfo.DY - dstInfo.SZ * dstInfo.DZ) * sa
            let zj = nativeint (dstInfo.DZ) * sa

            let xe = ptr + xs
            while ptr <> xe do
                let ye = ptr + ys
                while ptr <> ye do
                    let ze = ptr + zs
                    while ptr <> ze do
                        NativePtr.write (NativePtr.ofNativeInt<'a> ptr) src
                        ptr <- ptr + zj
                    ptr <- ptr + yj
                ptr <- ptr + xj

        let private setYXZ (src : 'a) (dst : NativeVolume<'a>)  =
            let dstInfo = dst.Info
            let sa = sizeof<'a> |> nativeint

            let mutable ptr = dst.Pointer |> NativePtr.toNativeInt
            ptr <- ptr + nativeint dstInfo.Origin * sa

            let xs  = nativeint (dstInfo.SX * dstInfo.DX) * sa
            let ys  = nativeint (dstInfo.SY * dstInfo.DY) * sa
            let zs  = nativeint (dstInfo.SZ * dstInfo.DZ) * sa

            let yj = nativeint (dstInfo.DY - dstInfo.SX * dstInfo.DX) * sa
            let xj = nativeint (dstInfo.DX - dstInfo.SZ * dstInfo.DZ) * sa
            let zj = nativeint (dstInfo.DZ) * sa

            let ye = ptr + ys
            while ptr <> ye do
                let xe = ptr + xs
                while ptr <> xe do
                    let ze = ptr + zs
                    while ptr <> ze do
                        NativePtr.write (NativePtr.ofNativeInt<'a> ptr) src
                        ptr <- ptr + zj
                    ptr <- ptr + xj
                ptr <- ptr + yj

        let private setXZY (src : 'a) (dst : NativeVolume<'a>)  =
            let dstInfo = dst.Info
            let sa = sizeof<'a> |> nativeint

            let mutable ptr = dst.Pointer |> NativePtr.toNativeInt
            ptr <- ptr + nativeint dstInfo.Origin * sa

            let xs  = nativeint (dstInfo.SX * dstInfo.DX) * sa
            let ys  = nativeint (dstInfo.SY * dstInfo.DY) * sa
            let zs  = nativeint (dstInfo.SZ * dstInfo.DZ) * sa

            let xj = nativeint (dstInfo.DX - dstInfo.SZ * dstInfo.DZ) * sa
            let zj = nativeint (dstInfo.DZ - dstInfo.SY * dstInfo.DY) * sa
            let yj = nativeint (dstInfo.DY) * sa

            let xe = ptr + xs
            while ptr <> xe do
                let ze = ptr + zs
                while ptr <> ze do
                    let ye = ptr + ys
                    while ptr <> ye do
                        NativePtr.write (NativePtr.ofNativeInt<'a> ptr) src
                        ptr <- ptr + yj
                    ptr <- ptr + zj
                ptr <- ptr + xj

        let private setYZX (src : 'a) (dst : NativeVolume<'a>)  =
            let dstInfo = dst.Info
            let sa = sizeof<'a> |> nativeint

            let mutable ptr = dst.Pointer |> NativePtr.toNativeInt
            ptr <- ptr + nativeint dstInfo.Origin * sa

            let xs  = nativeint (dstInfo.SX * dstInfo.DX) * sa
            let ys  = nativeint (dstInfo.SY * dstInfo.DY) * sa
            let zs  = nativeint (dstInfo.SZ * dstInfo.DZ) * sa

            let yj = nativeint (dstInfo.DY - dstInfo.SZ * dstInfo.DZ) * sa
            let zj = nativeint (dstInfo.DZ - dstInfo.SX * dstInfo.DX) * sa
            let xj = nativeint (dstInfo.DX) * sa

            let ye = ptr + ys
            while ptr <> ye do
                let ze = ptr + zs
                while ptr <> ze do
                    let xe = ptr + xs
                    while ptr <> xe do
                        NativePtr.write (NativePtr.ofNativeInt<'a> ptr) src
                        ptr <- ptr + xj
                    ptr <- ptr + zj
                ptr <- ptr + yj

        let private setZXY (src : 'a) (dst : NativeVolume<'a>)  =
            let dstInfo = dst.Info
            let sa = sizeof<'a> |> nativeint

            let mutable ptr = dst.Pointer |> NativePtr.toNativeInt
            ptr <- ptr + nativeint dstInfo.Origin * sa

            let xs  = nativeint (dstInfo.SX * dstInfo.DX) * sa
            let ys  = nativeint (dstInfo.SY * dstInfo.DY) * sa
            let zs  = nativeint (dstInfo.SZ * dstInfo.DZ) * sa

            let zj = nativeint (dstInfo.DZ - dstInfo.SX * dstInfo.DX) * sa
            let xj = nativeint (dstInfo.DX - dstInfo.SY * dstInfo.DY) * sa
            let yj = nativeint (dstInfo.DY) * sa

            let ze = ptr + zs
            while ptr <> ze do
                let xe = ptr + xs
                while ptr <> xe do
                    let ye = ptr + ys
                    while ptr <> ye do
                        NativePtr.write (NativePtr.ofNativeInt<'a> ptr) src
                        ptr <- ptr + yj
                    ptr <- ptr + xj
                ptr <- ptr + zj

        let private setZYX (src : 'a) (dst : NativeVolume<'a>)  =
            let dstInfo = dst.Info
            let sa = sizeof<'a> |> nativeint

            let mutable ptr = dst.Pointer |> NativePtr.toNativeInt
            ptr <- ptr + nativeint dstInfo.Origin * sa

            let xs  = nativeint (dstInfo.SX * dstInfo.DX) * sa
            let ys  = nativeint (dstInfo.SY * dstInfo.DY) * sa
            let zs  = nativeint (dstInfo.SZ * dstInfo.DZ) * sa

            let zj = nativeint (dstInfo.DZ - dstInfo.SY * dstInfo.DY) * sa
            let yj = nativeint (dstInfo.DY - dstInfo.SX * dstInfo.DX) * sa
            let xj = nativeint (dstInfo.DX) * sa

            let ze = ptr + zs
            while ptr <> ze do
                let ye = ptr + ys
                while ptr <> ye do
                    let xe = ptr + xs
                    while ptr <> xe do
                        NativePtr.write (NativePtr.ofNativeInt<'a> ptr) src
                        ptr <- ptr + xj
                    ptr <- ptr + yj
                ptr <- ptr + zj

        let set (src : 'a) (dst : NativeVolume<'a>) =
            let cxy = compare (abs dst.Info.DX) (abs dst.Info.DY)
            let cyz = compare (abs dst.Info.DY) (abs dst.Info.DZ)
            let cxz = compare (abs dst.Info.DX) (abs dst.Info.DZ)

            if cxz > 0 && cyz > 0 then
                // z < x && z < y
                if cxy < 0 then 
                    // x < y
                    setYXZ src dst
                else
                    // x >= y
                    setXYZ src dst

            elif cxy > 0 && cyz < 0 then
                // y < x && y < z
                if cxz > 0 then
                    // z < x
                    setXZY src dst
                else
                    // z >= x
                    setZXY src dst
            else
                // x <= y && x <= z
                if cyz > 0 then
                    // z < y
                    setYZX src dst
                else
                    // z >= y
                    setZYX src dst

        let pinned (v : Volume<'a>) (f : NativeVolume<'a> -> 'b) =
            let gc = GCHandle.Alloc(v.Data, GCHandleType.Pinned)
            try
                let vol = NativeVolume<'a>(NativePtr.ofNativeInt (gc.AddrOfPinnedObject()), v.Info)
                f vol
            finally
                gc.Free()