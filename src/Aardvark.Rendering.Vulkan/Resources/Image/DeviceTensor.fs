namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open Microsoft.FSharp.NativeInterop

#nowarn "9"

[<Struct>]
type DeviceVector<'a when 'a : unmanaged>(memory : DevicePtr, info : VectorInfo) =
    member x.Device = memory.Device
    member x.Info = info

    member x.Size = info.Size

    member x.Mapped (action : NativeVector<'a> -> 'r) =
        let info = info
        memory.Mapped(fun ptr ->
            action (NativeVector<'a>(NativePtr.ofNativeInt ptr, info))
        )

    member x.SubVector(origin : int64, size : int64, delta : int64) =
        DeviceVector<'a>(memory, info.SubVector(origin, size, delta))

    member x.SubVector(origin : int64, size : int64) = x.SubVector(origin, size, info.Delta)
    member x.SubVector(size : int64) = x.SubVector(info.Origin, size, info.Delta)

    // int64 slices
    member x.GetSlice(min : Option<int64>, max : Option<int64>) =
        let min = match min with | Some v -> v | None -> 0L
        let max = match max with | Some v -> v | None -> info.Size - 1L
        x.SubVector(min, 1L + max - min)

    // int slices
    member x.GetSlice(min : Option<int>, max : Option<int>) =
        let min = match min with | Some v -> int64 v | None -> 0L
        let max = match max with | Some v -> int64 v | None -> info.Size - 1L
        x.SubVector(min, 1L + max - min)

    member x.CopyTo(dst : NativeVector<'a>) =
        if info.Size <> dst.Size then failf "mismatching Vector size"
        x.Mapped (fun src ->
            NativeVector.copy src dst
        )

    member x.CopyTo(dst : Vector<'a>) =
        if info.Size <> dst.Size then failf "mismatching Vector size"
        x.Mapped (fun src ->
            NativeVector.using dst (fun dst ->
                NativeVector.copy src dst
            )
        )

    member x.CopyFrom(src : NativeVector<'a>) =
        if info.Size <> src.Size then failf "mismatching Vector size"
        x.Mapped (fun dst ->
            NativeVector.copy src dst
        )

    member x.CopyFrom(src : Vector<'a>) =
        if info.Size <> src.Size then failf "mismatching Vector size"
        x.Mapped (fun dst ->
            NativeVector.using src (fun src ->
                NativeVector.copy src dst
            )
        )

    member x.Set(v : 'a) =
        if info.Size > 0L then
            x.Mapped (fun dst ->
                NativeVector.set v dst
            )

[<Struct>]
type DeviceMatrix<'a when 'a : unmanaged>(memory : DevicePtr, info : MatrixInfo) =
    member x.Device = memory.Device
    member x.Info = info

    member x.Size = info.Size

    member x.Mapped (action : NativeMatrix<'a> -> 'r) =
        let info = info
        memory.Mapped(fun ptr ->
            action (NativeMatrix<'a>(NativePtr.ofNativeInt ptr, info))
        )

    member x.SubMatrix(origin : V2l, size : V2l, delta : V2l) =
        DeviceMatrix<'a>(memory, info.SubMatrix(origin, size, delta))

    member x.SubMatrix(origin : V2l, size : V2l) = x.SubMatrix(origin, size, info.Delta)
    member x.SubMatrix(size : V2l) = x.SubMatrix(V2l.Zero, size, info.Delta)

    member this.SubXVector(y : int64) = DeviceVector<'a>(memory, info.SubXVector(y))
    member this.SubYVector(x : int64) = DeviceVector<'a>(memory, info.SubYVector(x))

    // int64 slices
    member this.GetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>) =
        let minX = match minX with | Some v -> v | None -> 0L
        let minY = match minY with | Some v -> v | None -> 0L
        let maxX = match maxX with | Some v -> v | None -> (info.Size.X - 1L)
        let maxY = match maxY with | Some v -> v | None -> (info.Size.Y - 1L)
        this.SubMatrix(V2l(minX, minY), V2l(1L + maxX - minX, 1L + maxY - minY), info.Delta)

    member this.GetSlice(x : int64, minY : Option<int64>, maxY : Option<int64>) =
        let minY = match minY with | Some v -> v | None -> 0L
        let maxY = match maxY with | Some v -> v | None -> (info.Size.Y - 1L)
        this.SubYVector(x).SubVector(minY, 1L + maxY - minY)

    member this.GetSlice(minX : Option<int64>, maxX : Option<int64>, y : int64) =
        let minX = match minX with | Some v -> v | None -> 0L
        let maxX = match maxX with | Some v -> v | None -> (info.Size.X - 1L)
        this.SubXVector(y).SubVector(minX, 1L + maxX - minX)

    // int slices
    member this.GetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>) =
        let minX = match minX with | Some v -> int64 v | None -> 0L
        let minY = match minY with | Some v -> int64 v | None -> 0L
        let maxX = match maxX with | Some v -> int64 v | None -> (info.Size.X - 1L)
        let maxY = match maxY with | Some v -> int64 v | None -> (info.Size.Y - 1L)
        this.SubMatrix(V2l(minX, minY), V2l(1L + maxX - minX, 1L + maxY - minY), info.Delta)

    member this.GetSlice(x : int, minY : Option<int>, maxY : Option<int>) =
        let minY = match minY with | Some v -> int64 v | None -> 0L
        let maxY = match maxY with | Some v -> int64 v | None -> (info.Size.Y - 1L)
        this.SubYVector(int64 x).SubVector(minY, 1L + maxY - minY)

    member this.GetSlice(minX : Option<int>, maxX : Option<int>, y : int) =
        let minX = match minX with | Some v -> int64 v | None -> 0L
        let maxX = match maxX with | Some v -> int64 v | None -> (info.Size.X - 1L)
        this.SubXVector(int64 y).SubVector(minX, 1L + maxX - minX)


    member x.CopyTo(dst : NativeMatrix<'a>) =
        if info.Size <> dst.Size then failf "mismatching Matrix size"
        x.Mapped (fun src ->
            NativeMatrix.copy src dst
        )

    member x.CopyTo(dst : Matrix<'a>) =
        if info.Size <> dst.Size then failf "mismatching Matrix size"
        x.Mapped (fun src ->
            NativeMatrix.using dst (fun dst ->
                NativeMatrix.copy src dst
            )
        )

    member x.CopyFrom(src : NativeMatrix<'a>) =
        if info.Size <> src.Size then failf "mismatching Matrix size"
        x.Mapped (fun dst ->
            NativeMatrix.copy src dst
        )

    member x.CopyFrom(src : Matrix<'a>) =
        if info.Size <> src.Size then failf "mismatching Matrix size"
        x.Mapped (fun dst ->
            NativeMatrix.using src (fun src ->
                NativeMatrix.copy src dst
            )
        )

    member x.Set(v : 'a) =
        if info.Size.AllGreater 0L then
            x.Mapped (fun dst ->
                NativeMatrix.set v dst
            )

[<Struct>]
type DeviceVolume<'a when 'a : unmanaged>(memory : DevicePtr, info : VolumeInfo) =
    member x.Device = memory.Device
    member x.Info = info

    member x.Size = info.Size

    member x.Mapped (action : NativeVolume<'a> -> 'r) =
        let info = info
        memory.Mapped(fun ptr ->
            action (NativeVolume<'a>(NativePtr.ofNativeInt ptr, info))
        )

    member x.SubVolume(origin : V3l, size : V3l, delta : V3l) =
        DeviceVolume<'a>(memory, info.SubVolume(origin, size, delta))

    member x.SubVolume(origin : V3l, size : V3l) = x.SubVolume(origin, size, info.Delta)
    member x.SubVolume(size : V3l) = x.SubVolume(V3l.Zero, size, info.Delta)

    member this.SubYZMatrix(x : int64) = DeviceMatrix<'a>(memory, info.SubYZMatrix(x))
    member this.SubXZMatrix(y : int64) = DeviceMatrix<'a>(memory, info.SubXZMatrix(y))
    member this.SubXYMatrix(z : int64) = DeviceMatrix<'a>(memory, info.SubXYMatrix(z))

    member this.SubXVector(y : int64, z : int64) = DeviceVector<'a>(memory, VectorInfo(info.Index(0L, y, z), info.SX, info.DX))
    member this.SubYVector(x : int64, z : int64) = DeviceVector<'a>(memory, VectorInfo(info.Index(x, 0L, z), info.SY, info.DY))
    member this.SubZVector(x : int64, y : int64) = DeviceVector<'a>(memory, VectorInfo(info.Index(x, y, 0L), info.SZ, info.DZ))

    // int64 slices
    member this.GetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>) =
        let minX = match minX with | Some v -> v | None -> 0L
        let maxX = match maxX with | Some v -> v | None -> (info.Size.X - 1L)
        let minY = match minY with | Some v -> v | None -> 0L
        let maxY = match maxY with | Some v -> v | None -> (info.Size.Y - 1L)
        let minZ = match minZ with | Some v -> v | None -> 0L
        let maxZ = match maxZ with | Some v -> v | None -> (info.Size.Z - 1L)
        this.SubVolume(V3l(minX, minY, minZ), V3l(1L + maxX - minX, 1L + maxY - minY, 1L + maxZ - minZ), info.Delta)

    member this.GetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, z : int64) =
        let minX = match minX with | Some v -> v | None -> 0L
        let maxX = match maxX with | Some v -> v | None -> (info.Size.X - 1L)
        let minY = match minY with | Some v -> v | None -> 0L
        let maxY = match maxY with | Some v -> v | None -> (info.Size.Y - 1L)
        this.SubXYMatrix(z).SubMatrix(V2l(minX, minY), V2l(1L + maxX - minX, 1L + maxY - minY))

    member this.GetSlice(minX : Option<int64>, maxX : Option<int64>, y : int64, minZ : Option<int64>, maxZ : Option<int64>) =
        let minX = match minX with | Some v -> v | None -> 0L
        let maxX = match maxX with | Some v -> v | None -> (info.Size.X - 1L)
        let minZ = match minZ with | Some v -> v | None -> 0L
        let maxZ = match maxZ with | Some v -> v | None -> (info.Size.Z - 1L)
        this.SubXZMatrix(y).SubMatrix(V2l(minX, maxZ), V2l(1L + maxX - minX, 1L + maxZ - minZ))

    member this.GetSlice(x : int64, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>) =
        let minY = match minY with | Some v -> v | None -> 0L
        let maxY = match maxY with | Some v -> v | None -> (info.Size.Y - 1L)
        let minZ = match minZ with | Some v -> v | None -> 0L
        let maxZ = match maxZ with | Some v -> v | None -> (info.Size.Z - 1L)
        this.SubYZMatrix(x).SubMatrix(V2l(minY, maxZ), V2l(1L + maxY - minY, 1L + maxZ - minZ))

    member this.GetSlice(minX : Option<int64>, maxX : Option<int64>, y : int64, z : int64) =
        let minX = match minX with | Some v -> v | None -> 0L
        let maxX = match maxX with | Some v -> v | None -> (info.Size.X - 1L)
        this.SubXVector(y,z).SubVector(minX, 1L + maxX - minX)

    member this.GetSlice(x : int64, minY : Option<int64>, maxY : Option<int64>, z : int64) =
        let minY = match minY with | Some v -> v | None -> 0L
        let maxY = match maxY with | Some v -> v | None -> (info.Size.Y - 1L)
        this.SubYVector(x,z).SubVector(minY, 1L + maxY - minY)

    member this.GetSlice(x : int64, y : int64, minZ : Option<int64>, maxZ : Option<int64>) =
        let minZ = match minZ with | Some v -> v | None -> 0L
        let maxZ = match maxZ with | Some v -> v | None -> (info.Size.Z - 1L)
        this.SubXVector(x,y).SubVector(minZ, 1L + maxZ - minZ)

    // int slices
    member this.GetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>) =
        let minX = match minX with | Some v -> int64 v | None -> 0L
        let maxX = match maxX with | Some v -> int64 v | None -> (info.Size.X - 1L)
        let minY = match minY with | Some v -> int64 v | None -> 0L
        let maxY = match maxY with | Some v -> int64 v | None -> (info.Size.Y - 1L)
        let minZ = match minZ with | Some v -> int64 v | None -> 0L
        let maxZ = match maxZ with | Some v -> int64 v | None -> (info.Size.Z - 1L)
        this.SubVolume(V3l(minX, minY, minZ), V3l(1L + maxX - minX, 1L + maxY - minY, 1L + maxZ - minZ), info.Delta)

    member this.GetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, z : int) =
        let minX = match minX with | Some v -> int64 v | None -> 0L
        let maxX = match maxX with | Some v -> int64 v | None -> (info.Size.X - 1L)
        let minY = match minY with | Some v -> int64 v | None -> 0L
        let maxY = match maxY with | Some v -> int64 v | None -> (info.Size.Y - 1L)
        this.SubXYMatrix(int64 z).SubMatrix(V2l(minX, minY), V2l(1L + maxX - minX, 1L + maxY - minY))

    member this.GetSlice(minX : Option<int>, maxX : Option<int>, y : int, minZ : Option<int>, maxZ : Option<int>) =
        let minX = match minX with | Some v -> int64 v | None -> 0L
        let maxX = match maxX with | Some v -> int64 v | None -> (info.Size.X - 1L)
        let minZ = match minZ with | Some v -> int64 v | None -> 0L
        let maxZ = match maxZ with | Some v -> int64 v | None -> (info.Size.Z - 1L)
        this.SubXZMatrix(int64 y).SubMatrix(V2l(minX, maxZ), V2l(1L + maxX - minX, 1L + maxZ - minZ))

    member this.GetSlice(x : int, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>) =
        let minY = match minY with | Some v -> int64 v | None -> 0L
        let maxY = match maxY with | Some v -> int64 v | None -> (info.Size.Y - 1L)
        let minZ = match minZ with | Some v -> int64 v | None -> 0L
        let maxZ = match maxZ with | Some v -> int64 v | None -> (info.Size.Z - 1L)
        this.SubYZMatrix(int64 x).SubMatrix(V2l(minY, maxZ), V2l(1L + maxY - minY, 1L + maxZ - minZ))

    member this.GetSlice(minX : Option<int>, maxX : Option<int>, y : int, z : int) =
        let minX = match minX with | Some v -> int64 v | None -> 0L
        let maxX = match maxX with | Some v -> int64 v | None -> (info.Size.X - 1L)
        this.SubXVector(int64 y, int64 z).SubVector(minX, 1L + maxX - minX)

    member this.GetSlice(x : int, minY : Option<int>, maxY : Option<int>, z : int) =
        let minY = match minY with | Some v -> int64 v | None -> 0L
        let maxY = match maxY with | Some v -> int64 v | None -> (info.Size.Y - 1L)
        this.SubYVector(int64 x, int64 z).SubVector(minY, 1L + maxY - minY)

    member this.GetSlice(x : int, y : int, minZ : Option<int>, maxZ : Option<int>) =
        let minZ = match minZ with | Some v -> int64 v | None -> 0L
        let maxZ = match maxZ with | Some v -> int64 v | None -> (info.Size.Z - 1L)
        this.SubXVector(int64 x, int64 y).SubVector(minZ, 1L + maxZ - minZ)


    member x.CopyTo(dst : NativeVolume<'a>) =
        if info.Size <> dst.Size then failf "mismatching Volume size"
        x.Mapped (fun src ->
            NativeVolume.copy src dst
        )

    member x.CopyTo(dst : Volume<'a>) =
        if info.Size <> dst.Size then failf "mismatching Volume size"
        x.Mapped (fun src ->
            NativeVolume.using dst (fun dst ->
                NativeVolume.copy src dst
            )
        )

    member x.CopyFrom(src : NativeVolume<'a>) =
        if info.Size <> src.Size then failf "mismatching Volume size"
        x.Mapped (fun dst ->
            NativeVolume.copy src dst
        )

    member x.CopyFrom(src : Volume<'a>) =
        if info.Size <> src.Size then failf "mismatching Volume size"
        x.Mapped (fun dst ->
            NativeVolume.using src (fun src ->
                NativeVolume.copy src dst
            )
        )

    member x.Set(v : 'a) =
        if info.Size.AllGreater 0L then
            x.Mapped (fun dst ->
                NativeVolume.set v dst
            )

[<Struct>]
type DeviceTensor4<'a when 'a : unmanaged>(memory : DevicePtr, info : Tensor4Info) =
    member x.Device = memory.Device
    member x.Info = info

    member x.Size = info.Size

    member x.Mapped (action : NativeTensor4<'a> -> 'r) =
        let info = info
        memory.Mapped(fun ptr ->
            action (NativeTensor4<'a>(NativePtr.ofNativeInt ptr, info))
        )

    member x.SubTensor4(origin : V4l, size : V4l, delta : V4l) =
        DeviceTensor4<'a>(memory, info.SubTensor4(origin, size, delta))

    member x.SubTensor4(origin : V4l, size : V4l) = x.SubTensor4(origin, size, info.Delta)
    member x.SubTensor4(size : V4l) = x.SubTensor4(V4l.Zero, size, info.Delta)

    member this.SubXYZVolume(w : int64) = DeviceVolume<'a>(memory, info.SubXYZVolume(w))
    member this.SubXYWVolume(z : int64) = DeviceVolume<'a>(memory, info.SubXYWVolume(z))
    member this.SubXZWVolume(y : int64) = DeviceVolume<'a>(memory, info.SubXZWVolume(y))
    member this.SubYZWVolume(x : int64) = DeviceVolume<'a>(memory, info.SubYZWVolume(x))

    member this.SubXYMatrix(z : int64, w : int64) = DeviceMatrix<'a>(memory, MatrixInfo(info.Index(0L, 0L, z, w), info.Size.XY, info.Delta.XY))
    member this.SubXZMatrix(y : int64, w : int64) = DeviceMatrix<'a>(memory, MatrixInfo(info.Index(0L, y, 0L, w), info.Size.XZ, info.Delta.XZ))
    member this.SubXWMatrix(y : int64, z : int64) = DeviceMatrix<'a>(memory, MatrixInfo(info.Index(0L, y, z, 0L), info.Size.XW, info.Delta.XW))
    member this.SubYZMatrix(x : int64, w : int64) = DeviceMatrix<'a>(memory, MatrixInfo(info.Index(x, 0L, 0L, w), info.Size.YZ, info.Delta.YZ))
    member this.SubYWMatrix(x : int64, z : int64) = DeviceMatrix<'a>(memory, MatrixInfo(info.Index(x, 0L, z, 0L), info.Size.YW, info.Delta.YW))
    member this.SubZWMatrix(x : int64, y : int64) = DeviceMatrix<'a>(memory, MatrixInfo(info.Index(x, y, 0L, 0L), info.Size.ZW, info.Delta.ZW))

    member this.SubXVector(y : int64, z : int64, w : int64) = DeviceVector<'a>(memory, VectorInfo(info.Index(0L, y, z, w), info.SX, info.DX))
    member this.SubYVector(x : int64, z : int64, w : int64) = DeviceVector<'a>(memory, VectorInfo(info.Index(x, 0L, z, w), info.SY, info.DY))
    member this.SubZVector(x : int64, y : int64, w : int64) = DeviceVector<'a>(memory, VectorInfo(info.Index(x, y, 0L, w), info.SZ, info.SZ))
    member this.SubWVector(x : int64, y : int64, z : int64) = DeviceVector<'a>(memory, VectorInfo(info.Index(x, y, z, 0L), info.SW, info.SW))

    // int64 slices
    member this.GetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>, minW : Option<int64>, maxW : Option<int64>) =
        let minX = match minX with | Some v -> v | None -> 0L
        let maxX = match maxX with | Some v -> v | None -> (info.Size.X - 1L)
        let minY = match minY with | Some v -> v | None -> 0L
        let maxY = match maxY with | Some v -> v | None -> (info.Size.Y - 1L)
        let minZ = match minZ with | Some v -> v | None -> 0L
        let maxZ = match maxZ with | Some v -> v | None -> (info.Size.Z - 1L)
        let minW = match minW with | Some v -> v | None -> 0L
        let maxW = match maxW with | Some v -> v | None -> (info.Size.W - 1L)
        this.SubTensor4(
            V4l(minX, minY, minZ, minW),
            V4l(1L + maxX - minX, 1L + maxY - minY, 1L + maxZ - minZ, 1L + maxW - minW),
            info.Delta
        )

    member this.GetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>, w : int64) =
        let minX = match minX with | Some v -> v | None -> 0L
        let maxX = match maxX with | Some v -> v | None -> (info.Size.X - 1L)
        let minY = match minY with | Some v -> v | None -> 0L
        let maxY = match maxY with | Some v -> v | None -> (info.Size.Y - 1L)
        let minZ = match minZ with | Some v -> v | None -> 0L
        let maxZ = match maxZ with | Some v -> v | None -> (info.Size.Z - 1L)

        this.SubXYZVolume(w).SubVolume(V3l(minX, minY, minZ), V3l(1L + maxX - minX, 1L + maxY - minY, 1L + maxZ - minZ))

    member this.GetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, z : int64, minW : Option<int64>, maxW : Option<int64>) =
        let minX = match minX with | Some v -> v | None -> 0L
        let maxX = match maxX with | Some v -> v | None -> (info.Size.X - 1L)
        let minY = match minY with | Some v -> v | None -> 0L
        let maxY = match maxY with | Some v -> v | None -> (info.Size.Y - 1L)
        let minW = match minW with | Some v -> v | None -> 0L
        let maxW = match maxW with | Some v -> v | None -> (info.Size.W - 1L)

        this.SubXYWVolume(z).SubVolume(V3l(minX, minY, minW), V3l(1L + maxX - minX, 1L + maxY - minY, 1L + maxW - minW))

    member this.GetSlice(minX : Option<int64>, maxX : Option<int64>, y : int64, minZ : Option<int64>, maxZ : Option<int64>, minW : Option<int64>, maxW : Option<int64>) =
        let minX = match minX with | Some v -> v | None -> 0L
        let maxX = match maxX with | Some v -> v | None -> (info.Size.X - 1L)
        let minZ = match minZ with | Some v -> v | None -> 0L
        let maxZ = match maxZ with | Some v -> v | None -> (info.Size.Z - 1L)
        let minW = match minW with | Some v -> v | None -> 0L
        let maxW = match maxW with | Some v -> v | None -> (info.Size.W - 1L)

        this.SubXZWVolume(y).SubVolume(V3l(minX, minZ, minW), V3l(1L + maxX - minX, 1L + maxZ - minZ, 1L + maxW - minW))

    member this.GetSlice(x : int64, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>, minW : Option<int64>, maxW : Option<int64>) =
        let minY = match minY with | Some v -> v | None -> 0L
        let maxY = match maxY with | Some v -> v | None -> (info.Size.Y - 1L)
        let minZ = match minZ with | Some v -> v | None -> 0L
        let maxZ = match maxZ with | Some v -> v | None -> (info.Size.Z - 1L)
        let minW = match minW with | Some v -> v | None -> 0L
        let maxW = match maxW with | Some v -> v | None -> (info.Size.W - 1L)

        this.SubYZWVolume(x).SubVolume(V3l(minY, minZ, minW), V3l(1L + maxY - minY, 1L + maxZ - minZ, 1L + maxW - minW))

    // TODO: matrix/vector slices


    // int slices
    // int64 slices
    member this.GetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>, minW : Option<int>, maxW : Option<int>) =
        let minX = match minX with | Some v -> int64 v | None -> 0L
        let maxX = match maxX with | Some v -> int64 v | None -> (info.Size.X - 1L)
        let minY = match minY with | Some v -> int64 v | None -> 0L
        let maxY = match maxY with | Some v -> int64 v | None -> (info.Size.Y - 1L)
        let minZ = match minZ with | Some v -> int64 v | None -> 0L
        let maxZ = match maxZ with | Some v -> int64 v | None -> (info.Size.Z - 1L)
        let minW = match minW with | Some v -> int64 v | None -> 0L
        let maxW = match maxW with | Some v -> int64 v | None -> (info.Size.W - 1L)
        this.SubTensor4(
            V4l(minX, minY, minZ, minW),
            V4l(1L + maxX - minX, 1L + maxY - minY, 1L + maxZ - minZ, 1L + maxW - minW),
            info.Delta
        )

    member this.GetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>, w : int) =
        let minX = match minX with | Some v -> int64 v | None -> 0L
        let maxX = match maxX with | Some v -> int64 v | None -> (info.Size.X - 1L)
        let minY = match minY with | Some v -> int64 v | None -> 0L
        let maxY = match maxY with | Some v -> int64 v | None -> (info.Size.Y - 1L)
        let minZ = match minZ with | Some v -> int64 v | None -> 0L
        let maxZ = match maxZ with | Some v -> int64 v | None -> (info.Size.Z - 1L)

        this.SubXYZVolume(int64 w).SubVolume(V3l(minX, minY, minZ), V3l(1L + maxX - minX, 1L + maxY - minY, 1L + maxZ - minZ))

    member this.GetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, z : int, minW : Option<int>, maxW : Option<int>) =
        let minX = match minX with | Some v -> int64 v | None -> 0L
        let maxX = match maxX with | Some v -> int64 v | None -> (info.Size.X - 1L)
        let minY = match minY with | Some v -> int64 v | None -> 0L
        let maxY = match maxY with | Some v -> int64 v | None -> (info.Size.Y - 1L)
        let minW = match minW with | Some v -> int64 v | None -> 0L
        let maxW = match maxW with | Some v -> int64 v | None -> (info.Size.W - 1L)

        this.SubXYWVolume(int64 z).SubVolume(V3l(minX, minY, minW), V3l(1L + maxX - minX, 1L + maxY - minY, 1L + maxW - minW))

    member this.GetSlice(minX : Option<int>, maxX : Option<int>, y : int, minZ : Option<int>, maxZ : Option<int>, minW : Option<int>, maxW : Option<int>) =
        let minX = match minX with | Some v -> int64 v | None -> 0L
        let maxX = match maxX with | Some v -> int64 v | None -> (info.Size.X - 1L)
        let minZ = match minZ with | Some v -> int64 v | None -> 0L
        let maxZ = match maxZ with | Some v -> int64 v | None -> (info.Size.Z - 1L)
        let minW = match minW with | Some v -> int64 v | None -> 0L
        let maxW = match maxW with | Some v -> int64 v | None -> (info.Size.W - 1L)

        this.SubXZWVolume(int64 y).SubVolume(V3l(minX, minZ, minW), V3l(1L + maxX - minX, 1L + maxZ - minZ, 1L + maxW - minW))

    member this.GetSlice(x : int, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>, minW : Option<int>, maxW : Option<int>) =
        let minY = match minY with | Some v -> int64 v | None -> 0L
        let maxY = match maxY with | Some v -> int64 v | None -> (info.Size.Y - 1L)
        let minZ = match minZ with | Some v -> int64 v | None -> 0L
        let maxZ = match maxZ with | Some v -> int64 v | None -> (info.Size.Z - 1L)
        let minW = match minW with | Some v -> int64 v | None -> 0L
        let maxW = match maxW with | Some v -> int64 v | None -> (info.Size.W - 1L)

        this.SubYZWVolume(int64 x).SubVolume(V3l(minY, minZ, minW), V3l(1L + maxY - minY, 1L + maxZ - minZ, 1L + maxW - minW))

    // TODO: matrix/vector slices


    member x.CopyTo(dst : NativeTensor4<'a>) =
        if info.Size <> dst.Size then failf "mismatching Tensor4 size"
        x.Mapped (fun src ->
            NativeTensor4.copy src dst
        )

    member x.CopyTo(dst : Tensor4<'a>) =
        if info.Size <> dst.Size then failf "mismatching Tensor4 size"
        x.Mapped (fun src ->
            NativeTensor4.using dst (fun dst ->
                NativeTensor4.copy src dst
            )
        )

    member x.CopyFrom(src : NativeTensor4<'a>) =
        if info.Size <> src.Size then failf "mismatching Tensor4 size"
        x.Mapped (fun dst ->
            NativeTensor4.copy src dst
        )

    member x.CopyFrom(src : Tensor4<'a>) =
        if info.Size <> src.Size then failf "mismatching Tensor4 size"
        x.Mapped (fun dst ->
            NativeTensor4.using src (fun src ->
                NativeTensor4.copy src dst
            )
        )

    member x.Set(v : 'a) =
        if info.Size.AllGreater 0L then
            x.Mapped (fun dst ->
                NativeTensor4.set v dst
            )