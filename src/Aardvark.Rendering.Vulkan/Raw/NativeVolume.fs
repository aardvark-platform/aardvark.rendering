namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base

#nowarn "9"
#nowarn "51"

[<StructLayout(LayoutKind.Sequential)>]
type V3n =
    struct
        val mutable public X : nativeint
        val mutable public Y : nativeint
        val mutable public Z : nativeint

        member x.Dot(o : V3n) =
            x.X * o.X + x.Y * o.Y + x.Z * o.Z

        static member (+) (l : V3n, r : V3n) =
            V3n(l.X + r.X, l.Y + r.Y, l.Z + r.Z)

        static member (-) (l : V3n, r : V3n) =
            V3n(l.X - r.X, l.Y - r.Y, l.Z - r.Z)

        static member (*) (l : V3n, r : nativeint) =
            V3n(l.X * r, l.Y * r, l.Z * r)

        static member (*) (r : nativeint, l : V3n) =
            V3n(l.X * r, l.Y * r, l.Z * r)

        static member Dot (l : V3n, r : V3n) =
            l.Dot(r)

        static member op_Explicit (v : V3i) =
            V3n(nativeint v.X, nativeint v.Y, nativeint v.Z)

        static member op_Explicit (v : V3l) =
            V3n(nativeint v.X, nativeint v.Y, nativeint v.Z)

        new(x,y,z) = { X = x; Y = y; Z = z }
    end

[<StructLayout(LayoutKind.Sequential)>]
type NativeVolumeInfo =
    struct
        val mutable public Delta : V3n
        val mutable public Size : V3n
        val mutable public ElementSize : nativeint

        member x.DX = x.Delta.X * x.ElementSize
        member x.DY = x.Delta.Y * x.ElementSize
        member x.DZ = x.Delta.Z * x.ElementSize
        member x.SX = x.Size.X * x.ElementSize
        member x.SY = x.Size.Y * x.ElementSize
        member x.SZ = x.Size.Z * x.ElementSize

        new(d,s,es) = { Delta = d; Size = s; ElementSize = es }
    end

[<Struct>]
type NativeVolume<'a when 'a : unmanaged> =
    struct
        val mutable public Pointer : nativeptr<'a>
        val mutable public Info : NativeVolumeInfo
        
        member inline private x.ForEachXYZ(f : nativeptr<'a> -> unit) =
            let mutable i = NativePtr.toNativeInt x.Pointer
            let myInfo = x.Info
            
            let xs = myInfo.SX
            let xj = myInfo.DX - myInfo.SY * myInfo.DY
            
            let ys = myInfo.SY
            let yj = myInfo.DY - myInfo.SZ * myInfo.DZ
            
            let zs = myInfo.SZ
            let zj = myInfo.DZ
            
            
            let xe = i + xs
            while i <> xe do
                let ye = i + ys
                while i <> ye do
                    let ze = i + zs
                    while i <> ze do
                        f (NativePtr.ofNativeInt i)
                        i <- i + zj
                    i <- i + yj
                i <- i + xj
        member inline private x.ForEachYXZ(f : nativeptr<'a> -> unit) =
            let mutable i = NativePtr.toNativeInt x.Pointer
            let myInfo = x.Info
            
            let ys = myInfo.SY
            let yj = myInfo.DY - myInfo.SX * myInfo.DX
            
            let xs = myInfo.SX
            let xj = myInfo.DX - myInfo.SZ * myInfo.DZ
            
            let zs = myInfo.SZ
            let zj = myInfo.DZ
            
            
            let ye = i + ys
            while i <> ye do
                let xe = i + xs
                while i <> xe do
                    let ze = i + zs
                    while i <> ze do
                        f (NativePtr.ofNativeInt i)
                        i <- i + zj
                    i <- i + xj
                i <- i + yj
        member inline private x.ForEachYZX(f : nativeptr<'a> -> unit) =
            let mutable i = NativePtr.toNativeInt x.Pointer
            let myInfo = x.Info
            
            let ys = myInfo.SY
            let yj = myInfo.DY - myInfo.SZ * myInfo.DZ
            
            let zs = myInfo.SZ
            let zj = myInfo.DZ - myInfo.SX * myInfo.DX
            
            let xs = myInfo.SX
            let xj = myInfo.DX
            
            
            let ye = i + ys
            while i <> ye do
                let ze = i + zs
                while i <> ze do
                    let xe = i + xs
                    while i <> xe do
                        f (NativePtr.ofNativeInt i)
                        i <- i + xj
                    i <- i + zj
                i <- i + yj
        member inline private x.ForEachXZY(f : nativeptr<'a> -> unit) =
            let mutable i = NativePtr.toNativeInt x.Pointer
            let myInfo = x.Info
            
            let xs = myInfo.SX
            let xj = myInfo.DX - myInfo.SZ * myInfo.DZ
            
            let zs = myInfo.SZ
            let zj = myInfo.DZ - myInfo.SY * myInfo.DY
            
            let ys = myInfo.SY
            let yj = myInfo.DY
            
            
            let xe = i + xs
            while i <> xe do
                let ze = i + zs
                while i <> ze do
                    let ye = i + ys
                    while i <> ye do
                        f (NativePtr.ofNativeInt i)
                        i <- i + yj
                    i <- i + zj
                i <- i + xj
        member inline private x.ForEachZXY(f : nativeptr<'a> -> unit) =
            let mutable i = NativePtr.toNativeInt x.Pointer
            let myInfo = x.Info
            
            let zs = myInfo.SZ
            let zj = myInfo.DZ - myInfo.SX * myInfo.DX
            
            let xs = myInfo.SX
            let xj = myInfo.DX - myInfo.SY * myInfo.DY
            
            let ys = myInfo.SY
            let yj = myInfo.DY
            
            
            let ze = i + zs
            while i <> ze do
                let xe = i + xs
                while i <> xe do
                    let ye = i + ys
                    while i <> ye do
                        f (NativePtr.ofNativeInt i)
                        i <- i + yj
                    i <- i + xj
                i <- i + zj
        member inline private x.ForEachZYX(f : nativeptr<'a> -> unit) =
            let mutable i = NativePtr.toNativeInt x.Pointer
            let myInfo = x.Info
            
            let zs = myInfo.SZ
            let zj = myInfo.DZ - myInfo.SY * myInfo.DY
            
            let ys = myInfo.SY
            let yj = myInfo.DY - myInfo.SX * myInfo.DX
            
            let xs = myInfo.SX
            let xj = myInfo.DX
            
            
            let ze = i + zs
            while i <> ze do
                let ye = i + ys
                while i <> ye do
                    let xe = i + xs
                    while i <> xe do
                        f (NativePtr.ofNativeInt i)
                        i <- i + xj
                    i <- i + yj
                i <- i + zj
        member inline x.ForEach(f : nativeptr<'a> -> unit) = 
            let myInfo = x.Info
            let cxy = compare (abs myInfo.DX) (abs myInfo.DY)
            let cxz = compare (abs myInfo.DX) (abs myInfo.DZ)
            let cyz = compare (abs myInfo.DY) (abs myInfo.DZ)
        
            if cxz > 0 && cyz > 0 then
                // z is mincomponent
                // x > z && y > z
                if cxy < 0 then 
                    // y > x
                    x.ForEachYXZ(f)  // typical piximage case
                else 
                    // x >= y 
                    x.ForEachXYZ(f)  // transposed image
        
            elif cxy > 0 && cyz < 0 then
                // y is mincomponent (very rare)
                // x > y && z > y
                if cxz < 0 then
                    // z > x
                    x.ForEachZXY(f)
                else
                    // x >= z
                    x.ForEachXZY(f)
            else
                // x is mincomponent
                // y > x && z > x
                if cyz < 0 then
                    // y < z
                    x.ForEachZYX(f)
                else
                    // y >= z
                    x.ForEachYZX(f)
        
        member inline private x.ForEachXYZ(other : NativeVolume<'b>, f : nativeptr<'a> -> nativeptr<'b> -> unit) =
            let mutable i = NativePtr.toNativeInt x.Pointer
            let mutable i1 = NativePtr.toNativeInt other.Pointer
            let myInfo = x.Info
            let otherInfo = other.Info
            
            let xs = myInfo.SX
            let xj = myInfo.DX - myInfo.SY * myInfo.DY
            let xj1 = otherInfo.DX - otherInfo.SY * otherInfo.DY
            
            let ys = myInfo.SY
            let yj = myInfo.DY - myInfo.SZ * myInfo.DZ
            let yj1 = otherInfo.DY - otherInfo.SZ * otherInfo.DZ
            
            let zs = myInfo.SZ
            let zj = myInfo.DZ
            let zj1 = otherInfo.DZ
            
            
            let xe = i + xs
            while i <> xe do
                let ye = i + ys
                while i <> ye do
                    let ze = i + zs
                    while i <> ze do
                        f (NativePtr.ofNativeInt i) (NativePtr.ofNativeInt i1)
                        i <- i + zj
                        i1 <- i1 + zj
                    i <- i + yj
                    i1 <- i1 + yj
                i <- i + xj
                i1 <- i1 + xj
        member inline private x.ForEachYXZ(other : NativeVolume<'b>, f : nativeptr<'a> -> nativeptr<'b> -> unit) =
            let mutable i = NativePtr.toNativeInt x.Pointer
            let mutable i1 = NativePtr.toNativeInt other.Pointer
            let myInfo = x.Info
            let otherInfo = other.Info
            
            let ys = myInfo.SY
            let yj = myInfo.DY - myInfo.SX * myInfo.DX
            let yj1 = otherInfo.DY - otherInfo.SX * otherInfo.DX
            
            let xs = myInfo.SX
            let xj = myInfo.DX - myInfo.SZ * myInfo.DZ
            let xj1 = otherInfo.DX - otherInfo.SZ * otherInfo.DZ
            
            let zs = myInfo.SZ
            let zj = myInfo.DZ
            let zj1 = otherInfo.DZ
            
            
            let ye = i + ys
            while i <> ye do
                let xe = i + xs
                while i <> xe do
                    let ze = i + zs
                    while i <> ze do
                        f (NativePtr.ofNativeInt i) (NativePtr.ofNativeInt i1)
                        i <- i + zj
                        i1 <- i1 + zj
                    i <- i + xj
                    i1 <- i1 + xj
                i <- i + yj
                i1 <- i1 + yj
        member inline private x.ForEachYZX(other : NativeVolume<'b>, f : nativeptr<'a> -> nativeptr<'b> -> unit) =
            let mutable i = NativePtr.toNativeInt x.Pointer
            let mutable i1 = NativePtr.toNativeInt other.Pointer
            let myInfo = x.Info
            let otherInfo = other.Info
            
            let ys = myInfo.SY
            let yj = myInfo.DY - myInfo.SZ * myInfo.DZ
            let yj1 = otherInfo.DY - otherInfo.SZ * otherInfo.DZ
            
            let zs = myInfo.SZ
            let zj = myInfo.DZ - myInfo.SX * myInfo.DX
            let zj1 = otherInfo.DZ - otherInfo.SX * otherInfo.DX
            
            let xs = myInfo.SX
            let xj = myInfo.DX
            let xj1 = otherInfo.DX
            
            
            let ye = i + ys
            while i <> ye do
                let ze = i + zs
                while i <> ze do
                    let xe = i + xs
                    while i <> xe do
                        f (NativePtr.ofNativeInt i) (NativePtr.ofNativeInt i1)
                        i <- i + xj
                        i1 <- i1 + xj
                    i <- i + zj
                    i1 <- i1 + zj
                i <- i + yj
                i1 <- i1 + yj
        member inline private x.ForEachXZY(other : NativeVolume<'b>, f : nativeptr<'a> -> nativeptr<'b> -> unit) =
            let mutable i = NativePtr.toNativeInt x.Pointer
            let mutable i1 = NativePtr.toNativeInt other.Pointer
            let myInfo = x.Info
            let otherInfo = other.Info
            
            let xs = myInfo.SX
            let xj = myInfo.DX - myInfo.SZ * myInfo.DZ
            let xj1 = otherInfo.DX - otherInfo.SZ * otherInfo.DZ
            
            let zs = myInfo.SZ
            let zj = myInfo.DZ - myInfo.SY * myInfo.DY
            let zj1 = otherInfo.DZ - otherInfo.SY * otherInfo.DY
            
            let ys = myInfo.SY
            let yj = myInfo.DY
            let yj1 = otherInfo.DY
            
            
            let xe = i + xs
            while i <> xe do
                let ze = i + zs
                while i <> ze do
                    let ye = i + ys
                    while i <> ye do
                        f (NativePtr.ofNativeInt i) (NativePtr.ofNativeInt i1)
                        i <- i + yj
                        i1 <- i1 + yj
                    i <- i + zj
                    i1 <- i1 + zj
                i <- i + xj
                i1 <- i1 + xj
        member inline private x.ForEachZXY(other : NativeVolume<'b>, f : nativeptr<'a> -> nativeptr<'b> -> unit) =
            let mutable i = NativePtr.toNativeInt x.Pointer
            let mutable i1 = NativePtr.toNativeInt other.Pointer
            let myInfo = x.Info
            let otherInfo = other.Info
            
            let zs = myInfo.SZ
            let zj = myInfo.DZ - myInfo.SX * myInfo.DX
            let zj1 = otherInfo.DZ - otherInfo.SX * otherInfo.DX
            
            let xs = myInfo.SX
            let xj = myInfo.DX - myInfo.SY * myInfo.DY
            let xj1 = otherInfo.DX - otherInfo.SY * otherInfo.DY
            
            let ys = myInfo.SY
            let yj = myInfo.DY
            let yj1 = otherInfo.DY
            
            
            let ze = i + zs
            while i <> ze do
                let xe = i + xs
                while i <> xe do
                    let ye = i + ys
                    while i <> ye do
                        f (NativePtr.ofNativeInt i) (NativePtr.ofNativeInt i1)
                        i <- i + yj
                        i1 <- i1 + yj
                    i <- i + xj
                    i1 <- i1 + xj
                i <- i + zj
                i1 <- i1 + zj
        member inline private x.ForEachZYX(other : NativeVolume<'b>, f : nativeptr<'a> -> nativeptr<'b> -> unit) =
            let mutable i = NativePtr.toNativeInt x.Pointer
            let mutable i1 = NativePtr.toNativeInt other.Pointer
            let myInfo = x.Info
            let otherInfo = other.Info
            
            let zs = myInfo.SZ
            let zj = myInfo.DZ - myInfo.SY * myInfo.DY
            let zj1 = otherInfo.DZ - otherInfo.SY * otherInfo.DY
            
            let ys = myInfo.SY
            let yj = myInfo.DY - myInfo.SX * myInfo.DX
            let yj1 = otherInfo.DY - otherInfo.SX * otherInfo.DX
            
            let xs = myInfo.SX
            let xj = myInfo.DX
            let xj1 = otherInfo.DX
            
            
            let ze = i + zs
            while i <> ze do
                let ye = i + ys
                while i <> ye do
                    let xe = i + xs
                    while i <> xe do
                        f (NativePtr.ofNativeInt i) (NativePtr.ofNativeInt i1)
                        i <- i + xj
                        i1 <- i1 + xj
                    i <- i + yj
                    i1 <- i1 + yj
                i <- i + zj
                i1 <- i1 + zj
        member inline x.ForEach(other : NativeVolume<'b>, f : nativeptr<'a> -> nativeptr<'b> -> unit) = 
            let myInfo = x.Info
            let cxy = compare (abs myInfo.DX) (abs myInfo.DY)
            let cxz = compare (abs myInfo.DX) (abs myInfo.DZ)
            let cyz = compare (abs myInfo.DY) (abs myInfo.DZ)
        
            if cxz > 0 && cyz > 0 then
                // z is mincomponent
                // x > z && y > z
                if cxy < 0 then 
                    // y > x
                    x.ForEachYXZ(other, f)  // typical piximage case
                else 
                    // x >= y 
                    x.ForEachXYZ(other, f)  // transposed image
        
            elif cxy > 0 && cyz < 0 then
                // y is mincomponent (very rare)
                // x > y && z > y
                if cxz < 0 then
                    // z > x
                    x.ForEachZXY(other, f)
                else
                    // x >= z
                    x.ForEachXZY(other, f)
            else
                // x is mincomponent
                // y > x && z > x
                if cyz < 0 then
                    // y < z
                    x.ForEachZYX(other, f)
                else
                    // y >= z
                    x.ForEachYZX(other, f)
        
        new(ptr, info) = { Pointer = ptr; Info = info }
    end

[<Struct>]
type NativeVolumeRaw =
    struct
        val mutable public Pointer : nativeint
        val mutable public Info : NativeVolumeInfo

        member inline private x.ForEachXYZ(f : nativeint -> unit) =
            let mutable i = x.Pointer
            let myInfo = x.Info
            
            let xs = myInfo.SX
            let xj = myInfo.DX - myInfo.SY * myInfo.DY
            
            let ys = myInfo.SY
            let yj = myInfo.DY - myInfo.SZ * myInfo.DZ
            
            let zs = myInfo.SZ
            let zj = myInfo.DZ
            
            
            let xe = i + xs
            while i <> xe do
                let ye = i + ys
                while i <> ye do
                    let ze = i + zs
                    while i <> ze do
                        f i
                        i <- i + zj
                    i <- i + yj
                i <- i + xj
        member inline private x.ForEachYXZ(f : nativeint -> unit) =
            let mutable i = x.Pointer
            let myInfo = x.Info
            
            let ys = myInfo.SY
            let yj = myInfo.DY - myInfo.SX * myInfo.DX
            
            let xs = myInfo.SX
            let xj = myInfo.DX - myInfo.SZ * myInfo.DZ
            
            let zs = myInfo.SZ
            let zj = myInfo.DZ
            
            
            let ye = i + ys
            while i <> ye do
                let xe = i + xs
                while i <> xe do
                    let ze = i + zs
                    while i <> ze do
                        f i
                        i <- i + zj
                    i <- i + xj
                i <- i + yj
        member inline private x.ForEachYZX(f : nativeint -> unit) =
            let mutable i = x.Pointer
            let myInfo = x.Info
            
            let ys = myInfo.SY
            let yj = myInfo.DY - myInfo.SZ * myInfo.DZ
            
            let zs = myInfo.SZ
            let zj = myInfo.DZ - myInfo.SX * myInfo.DX
            
            let xs = myInfo.SX
            let xj = myInfo.DX
            
            
            let ye = i + ys
            while i <> ye do
                let ze = i + zs
                while i <> ze do
                    let xe = i + xs
                    while i <> xe do
                        f i
                        i <- i + xj
                    i <- i + zj
                i <- i + yj
        member inline private x.ForEachXZY(f : nativeint -> unit) =
            let mutable i = x.Pointer
            let myInfo = x.Info
            
            let xs = myInfo.SX
            let xj = myInfo.DX - myInfo.SZ * myInfo.DZ
            
            let zs = myInfo.SZ
            let zj = myInfo.DZ - myInfo.SY * myInfo.DY
            
            let ys = myInfo.SY
            let yj = myInfo.DY
            
            
            let xe = i + xs
            while i <> xe do
                let ze = i + zs
                while i <> ze do
                    let ye = i + ys
                    while i <> ye do
                        f i
                        i <- i + yj
                    i <- i + zj
                i <- i + xj
        member inline private x.ForEachZXY(f : nativeint -> unit) =
            let mutable i = x.Pointer
            let myInfo = x.Info
            
            let zs = myInfo.SZ
            let zj = myInfo.DZ - myInfo.SX * myInfo.DX
            
            let xs = myInfo.SX
            let xj = myInfo.DX - myInfo.SY * myInfo.DY
            
            let ys = myInfo.SY
            let yj = myInfo.DY
            
            
            let ze = i + zs
            while i <> ze do
                let xe = i + xs
                while i <> xe do
                    let ye = i + ys
                    while i <> ye do
                        f i
                        i <- i + yj
                    i <- i + xj
                i <- i + zj
        member inline private x.ForEachZYX(f : nativeint -> unit) =
            let mutable i = x.Pointer
            let myInfo = x.Info
            
            let zs = myInfo.SZ
            let zj = myInfo.DZ - myInfo.SY * myInfo.DY
            
            let ys = myInfo.SY
            let yj = myInfo.DY - myInfo.SX * myInfo.DX
            
            let xs = myInfo.SX
            let xj = myInfo.DX
            
            
            let ze = i + zs
            while i <> ze do
                let ye = i + ys
                while i <> ye do
                    let xe = i + xs
                    while i <> xe do
                        f i
                        i <- i + xj
                    i <- i + yj
                i <- i + zj
        member inline x.ForEach(f : nativeint -> unit) = 
            let myInfo = x.Info
            let cxy = compare (abs myInfo.DX) (abs myInfo.DY)
            let cxz = compare (abs myInfo.DX) (abs myInfo.DZ)
            let cyz = compare (abs myInfo.DY) (abs myInfo.DZ)
        
            if cxz > 0 && cyz > 0 then
                // z is mincomponent
                // x > z && y > z
                if cxy < 0 then 
                    // y > x
                    x.ForEachYXZ(f)  // typical piximage case
                else 
                    // x >= y 
                    x.ForEachXYZ(f)  // transposed image
        
            elif cxy > 0 && cyz < 0 then
                // y is mincomponent (very rare)
                // x > y && z > y
                if cxz < 0 then
                    // z > x
                    x.ForEachZXY(f)
                else
                    // x >= z
                    x.ForEachXZY(f)
            else
                // x is mincomponent
                // y > x && z > x
                if cyz < 0 then
                    // y < z
                    x.ForEachZYX(f)
                else
                    // y >= z
                    x.ForEachYZX(f)
        
        member inline private x.ForEachXYZ(other : NativeVolumeRaw, f : nativeint -> nativeint -> unit) =
            let mutable i = x.Pointer
            let mutable i1 = other.Pointer
            let myInfo = x.Info
            let otherInfo = other.Info
            
            let xs = myInfo.SX
            let xj = myInfo.DX - myInfo.SY * myInfo.DY
            let xj1 = otherInfo.DX - otherInfo.SY * otherInfo.DY
            
            let ys = myInfo.SY
            let yj = myInfo.DY - myInfo.SZ * myInfo.DZ
            let yj1 = otherInfo.DY - otherInfo.SZ * otherInfo.DZ
            
            let zs = myInfo.SZ
            let zj = myInfo.DZ
            let zj1 = otherInfo.DZ
            
            
            let xe = i + xs
            while i <> xe do
                let ye = i + ys
                while i <> ye do
                    let ze = i + zs
                    while i <> ze do
                        f i i1
                        i <- i + zj
                        i1 <- i1 + zj
                    i <- i + yj
                    i1 <- i1 + yj
                i <- i + xj
                i1 <- i1 + xj
        member inline private x.ForEachYXZ(other : NativeVolumeRaw, f : nativeint -> nativeint -> unit) =
            let mutable i = x.Pointer
            let mutable i1 = other.Pointer
            let myInfo = x.Info
            let otherInfo = other.Info
            
            let ys = myInfo.SY
            let yj = myInfo.DY - myInfo.SX * myInfo.DX
            let yj1 = otherInfo.DY - otherInfo.SX * otherInfo.DX
            
            let xs = myInfo.SX
            let xj = myInfo.DX - myInfo.SZ * myInfo.DZ
            let xj1 = otherInfo.DX - otherInfo.SZ * otherInfo.DZ
            
            let zs = myInfo.SZ
            let zj = myInfo.DZ
            let zj1 = otherInfo.DZ
            
            
            let ye = i + ys
            while i <> ye do
                let xe = i + xs
                while i <> xe do
                    let ze = i + zs
                    while i <> ze do
                        f i i1
                        i <- i + zj
                        i1 <- i1 + zj
                    i <- i + xj
                    i1 <- i1 + xj
                i <- i + yj
                i1 <- i1 + yj
        member inline private x.ForEachYZX(other : NativeVolumeRaw, f : nativeint -> nativeint -> unit) =
            let mutable i = x.Pointer
            let mutable i1 = other.Pointer
            let myInfo = x.Info
            let otherInfo = other.Info
            
            let ys = myInfo.SY
            let yj = myInfo.DY - myInfo.SZ * myInfo.DZ
            let yj1 = otherInfo.DY - otherInfo.SZ * otherInfo.DZ
            
            let zs = myInfo.SZ
            let zj = myInfo.DZ - myInfo.SX * myInfo.DX
            let zj1 = otherInfo.DZ - otherInfo.SX * otherInfo.DX
            
            let xs = myInfo.SX
            let xj = myInfo.DX
            let xj1 = otherInfo.DX
            
            
            let ye = i + ys
            while i <> ye do
                let ze = i + zs
                while i <> ze do
                    let xe = i + xs
                    while i <> xe do
                        f i i1
                        i <- i + xj
                        i1 <- i1 + xj
                    i <- i + zj
                    i1 <- i1 + zj
                i <- i + yj
                i1 <- i1 + yj
        member inline private x.ForEachXZY(other : NativeVolumeRaw, f : nativeint -> nativeint -> unit) =
            let mutable i = x.Pointer
            let mutable i1 = other.Pointer
            let myInfo = x.Info
            let otherInfo = other.Info
            
            let xs = myInfo.SX
            let xj = myInfo.DX - myInfo.SZ * myInfo.DZ
            let xj1 = otherInfo.DX - otherInfo.SZ * otherInfo.DZ
            
            let zs = myInfo.SZ
            let zj = myInfo.DZ - myInfo.SY * myInfo.DY
            let zj1 = otherInfo.DZ - otherInfo.SY * otherInfo.DY
            
            let ys = myInfo.SY
            let yj = myInfo.DY
            let yj1 = otherInfo.DY
            
            
            let xe = i + xs
            while i <> xe do
                let ze = i + zs
                while i <> ze do
                    let ye = i + ys
                    while i <> ye do
                        f i i1
                        i <- i + yj
                        i1 <- i1 + yj
                    i <- i + zj
                    i1 <- i1 + zj
                i <- i + xj
                i1 <- i1 + xj
        member inline private x.ForEachZXY(other : NativeVolumeRaw, f : nativeint -> nativeint -> unit) =
            let mutable i = x.Pointer
            let mutable i1 = other.Pointer
            let myInfo = x.Info
            let otherInfo = other.Info
            
            let zs = myInfo.SZ
            let zj = myInfo.DZ - myInfo.SX * myInfo.DX
            let zj1 = otherInfo.DZ - otherInfo.SX * otherInfo.DX
            
            let xs = myInfo.SX
            let xj = myInfo.DX - myInfo.SY * myInfo.DY
            let xj1 = otherInfo.DX - otherInfo.SY * otherInfo.DY
            
            let ys = myInfo.SY
            let yj = myInfo.DY
            let yj1 = otherInfo.DY
            
            
            let ze = i + zs
            while i <> ze do
                let xe = i + xs
                while i <> xe do
                    let ye = i + ys
                    while i <> ye do
                        f i i1
                        i <- i + yj
                        i1 <- i1 + yj
                    i <- i + xj
                    i1 <- i1 + xj
                i <- i + zj
                i1 <- i1 + zj
        member inline private x.ForEachZYX(other : NativeVolumeRaw, f : nativeint -> nativeint -> unit) =
            let mutable i = x.Pointer
            let mutable i1 = other.Pointer
            let myInfo = x.Info
            let otherInfo = other.Info
            
            let zs = myInfo.SZ
            let zj = myInfo.DZ - myInfo.SY * myInfo.DY
            let zj1 = otherInfo.DZ - otherInfo.SY * otherInfo.DY
            
            let ys = myInfo.SY
            let yj = myInfo.DY - myInfo.SX * myInfo.DX
            let yj1 = otherInfo.DY - otherInfo.SX * otherInfo.DX
            
            let xs = myInfo.SX
            let xj = myInfo.DX
            let xj1 = otherInfo.DX
            
            
            let ze = i + zs
            while i <> ze do
                let ye = i + ys
                while i <> ye do
                    let xe = i + xs
                    while i <> xe do
                        f i i1
                        i <- i + xj
                        i1 <- i1 + xj
                    i <- i + yj
                    i1 <- i1 + yj
                i <- i + zj
                i1 <- i1 + zj
        member inline x.ForEach(other : NativeVolumeRaw, f : nativeint -> nativeint -> unit) = 
            let myInfo = x.Info
            let cxy = compare (abs myInfo.DX) (abs myInfo.DY)
            let cxz = compare (abs myInfo.DX) (abs myInfo.DZ)
            let cyz = compare (abs myInfo.DY) (abs myInfo.DZ)
        
            if cxz > 0 && cyz > 0 then
                // z is mincomponent
                // x > z && y > z
                if cxy < 0 then 
                    // y > x
                    x.ForEachYXZ(other, f)  // typical piximage case
                else 
                    // x >= y 
                    x.ForEachXYZ(other, f)  // transposed image
        
            elif cxy > 0 && cyz < 0 then
                // y is mincomponent (very rare)
                // x > y && z > y
                if cxz < 0 then
                    // z > x
                    x.ForEachZXY(other, f)
                else
                    // x >= z
                    x.ForEachXZY(other, f)
            else
                // x is mincomponent
                // y > x && z > x
                if cyz < 0 then
                    // y < z
                    x.ForEachZYX(other, f)
                else
                    // y >= z
                    x.ForEachYZX(other, f)
        
        new(ptr, info) = { Pointer = ptr; Info = info }
    end


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module NativeVolumeInfo =
    let ofVolumeInfo (info : VolumeInfo) =
        NativeVolumeInfo(V3n.op_Explicit info.Delta, V3n.op_Explicit info.Size, 1n)

    let scaled (elementSize : int) (i : NativeVolumeInfo) =
        NativeVolumeInfo(i.Delta, i.Size, nativeint elementSize * i.ElementSize)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module NativeVolume =
    
    let inline ofNativeInt<'a when 'a : unmanaged> (info : NativeVolumeInfo) (ptr : nativeint) : NativeVolume<'a> =
        NativeVolume<'a>(NativePtr.ofNativeInt ptr, info)
        
    let inline iter (f : nativeptr<'a> -> unit) (v : NativeVolume<'a>) =
        v.ForEach(f)

    let inline iter2 (f : nativeptr<'a> -> nativeptr<'b> -> unit) (l : NativeVolume<'a>) (r : NativeVolume<'b>) =
        l.ForEach(r, f)

    let pin (f : NativeVolume<'a> -> 'b) (pi : PixImage<'a>) : 'b =
        let gc = GCHandle.Alloc(pi.Volume.Data, GCHandleType.Pinned)
        let nv = gc.AddrOfPinnedObject() |> ofNativeInt (NativeVolumeInfo.ofVolumeInfo pi.VolumeInfo)
        try f nv
        finally gc.Free()

    let pin2 (l : PixImage<'a>) (r : PixImage<'b>) (f : NativeVolume<'a> -> NativeVolume<'b> -> 'c)  : 'c =
        let lgc = GCHandle.Alloc(l.Array, GCHandleType.Pinned)
        let lv = lgc.AddrOfPinnedObject() |> ofNativeInt (NativeVolumeInfo.ofVolumeInfo l.VolumeInfo)

        let rgc = GCHandle.Alloc(r.Array, GCHandleType.Pinned)
        let rv = rgc.AddrOfPinnedObject() |> ofNativeInt (NativeVolumeInfo.ofVolumeInfo r.VolumeInfo)

        try
            f lv rv
        finally
            lgc.Free()
            rgc.Free()

    let copy (src : NativeVolume<'a>) (dst : NativeVolume<'a>) =
        iter2 (fun s d -> NativePtr.write d (NativePtr.read s)) src dst

    module private RuntimeTypes = 
        open System.Reflection

        let anyStatic = BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Static


        type CopyImpl<'a when 'a : unmanaged>() =
            static let toNative =
                fun (src : PixImage<'a>) (info : NativeVolumeInfo) (ptr : nativeint) ->
                    let v : NativeVolume<'a> = ofNativeInt info ptr
                    src |> pin (fun src -> 
                        copy src v
                    )

            static let ofNative =
                fun (info : NativeVolumeInfo) (ptr : nativeint) (dst : PixImage<'a>) ->
                    let v : NativeVolume<'a> = ofNativeInt info ptr
                    dst |> pin (fun dst -> 
                        copy v dst
                    )


            static member ToNative = toNative
            static member OfNative = ofNative
            

        let toNativeCache = System.Collections.Concurrent.ConcurrentDictionary<Type, PixImage -> NativeVolumeInfo -> nativeint -> unit>()
        let ofNativeCache = System.Collections.Concurrent.ConcurrentDictionary<Type, NativeVolumeInfo -> nativeint -> PixImage -> unit>()

        let toNativeFun (t : Type) =
            toNativeCache.GetOrAdd(t, Func<_,_>(fun t ->
                let t = typedefof<CopyImpl<int>>.MakeGenericType [|t|]
                let p = t.GetProperty("ToNative", anyStatic)
                p.GetValue(null) |> unbox
            ))

        let ofNativeFun (t : Type) =
            ofNativeCache.GetOrAdd(t, Func<_,_>(fun t ->
                let t = typedefof<CopyImpl<int>>.MakeGenericType [|t|]
                let p = t.GetProperty("OfNative", anyStatic)
                p.GetValue(null) |> unbox
            ))

    let copyImageToNative (img : PixImage) (info : NativeVolumeInfo) (ptr : nativeint) =
        let f = RuntimeTypes.toNativeFun img.PixFormat.Type
        f img info ptr

    let copyNativeToImage (ptr : nativeint) (info : NativeVolumeInfo) (img : PixImage) =
        let f = RuntimeTypes.ofNativeFun img.PixFormat.Type
        f info ptr img

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module NativeVolumeRaw =
    let ofNativeInt (info : NativeVolumeInfo) (ptr : nativeint) =
        NativeVolumeRaw(ptr, info)

    let private channelSize (fmt : PixFormat) =
        Marshal.SizeOf fmt.Type

    module private RuntimeTypes =

        [<StructLayout(LayoutKind.Explicit, Size = 5)>]
        type private B5 = struct end
        [<StructLayout(LayoutKind.Explicit, Size = 6)>]
        type private B6 = struct end
        [<StructLayout(LayoutKind.Explicit, Size = 7)>]
        type private B7 = struct end
        [<StructLayout(LayoutKind.Explicit, Size = 9)>]
        type private B9 = struct end
        [<StructLayout(LayoutKind.Explicit, Size = 10)>]
        type private B10 = struct end
        [<StructLayout(LayoutKind.Explicit, Size = 11)>]
        type private B11 = struct end
        [<StructLayout(LayoutKind.Explicit, Size = 13)>]
        type private B13 = struct end
        [<StructLayout(LayoutKind.Explicit, Size = 14)>]
        type private B14 = struct end
        [<StructLayout(LayoutKind.Explicit, Size = 15)>]
        type private B15 = struct end

        let inline copy<'a when 'a : unmanaged> =
            fun src dst ->
                NativePtr.write 
                    (NativePtr.ofNativeInt<'a> dst) 
                    (NativePtr.read (NativePtr.ofNativeInt<'a> src))
        
        let private copyFuns =
            Dictionary.ofList [
                1,  copy<int8>
                2,  copy<int16>
                3,  copy<C3b>
                4,  copy<int32>
                5,  copy<B5>
                6,  copy<B6>
                7,  copy<B7>
                8,  copy<V2i>
                9,  copy<B9>
                10, copy<B10>
                11, copy<B11>
                12, copy<V3i>
                13, copy<B13>
                14, copy<B14>
                15, copy<B15>
                16, copy<V4i>
            ]

        let copyFun (s : nativeint) = copyFuns.[int s]

        
    let inline iter (f : nativeint -> unit) (v : NativeVolumeRaw) =
        v.ForEach(f)

    let inline iter2 (f : nativeint -> nativeint -> unit) (l : NativeVolumeRaw) (r : NativeVolumeRaw) =
        l.ForEach(r, f)

    let ofNativeVolume (v : NativeVolume<'a>) =
        NativeVolumeRaw(NativePtr.toNativeInt v.Pointer, NativeVolumeInfo(v.Info.Delta, v.Info.Size, nativeint sizeof<'a>))

    let pin (f : NativeVolumeRaw -> 'b) (pi : PixImage) : 'b =
        let gc = GCHandle.Alloc(pi.Array, GCHandleType.Pinned)

        let info =
            pi.VolumeInfo 
                |> NativeVolumeInfo.ofVolumeInfo
                |> NativeVolumeInfo.scaled (channelSize pi.PixFormat)

        let nv = gc.AddrOfPinnedObject() |> ofNativeInt info
        try f nv
        finally gc.Free()

    let copy (src : NativeVolumeRaw) (dst : NativeVolumeRaw) =
        if src.Info.ElementSize <> dst.Info.ElementSize then
            failf "cannot copy from one volume to another with a differing element size: %A vs. %A" src.Info.ElementSize dst.Info.ElementSize
        
        src.ForEach(dst, RuntimeTypes.copyFun src.Info.ElementSize)

    let copyImageToNative (img : PixImage) (dst : NativeVolumeRaw) =
        img |> pin (fun src ->
            copy src dst
        )

    let copyNativeToImage (src : NativeVolumeRaw) (img : PixImage) =
        img |> pin (fun dst ->
            copy src dst
        )
