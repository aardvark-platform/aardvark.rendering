namespace Aardvark.Base

open Microsoft.FSharp.NativeInterop
open System.Runtime.InteropServices

#nowarn "9"

[<Sealed>]
type NativeVector<'a when 'a : unmanaged>(ptr : nativeptr<'a>, info : VectorInfo) = 
    member x.Pointer = ptr
    member x.Info = info
    member x.Size = info.Size
    member x.Delta = info.Delta
    member x.Origin = info.Origin
    member x.SX = info.SX
    member x.DX = info.DX
    member inline private x.SetX(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX) * sa
        let eX = ptr + sX
        while ptr <> eX do
            NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
            ptr <- ptr + jX
    member x.Set(value : 'a) = 
        x.SetX(value)
    member inline private x.CopyToX(y : NativeVector<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX) * sa
        let yjX = nativeint (y.DX) * sa
        let eX = xptr + sX
        while xptr <> eX do
            NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
            xptr <- xptr + xjX
            yptr <- yptr + yjX
    member x.CopyTo(y : NativeVector<'a>) = 
        if info.Size <> y.Size then
            failwithf "NativeVector size mismatch: { src = %A; dst = %A }" info.Size y.Size
        x.CopyToX(y)
    member inline private x.CopyToX<'b when 'b : unmanaged>(y : NativeVector<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX) * sa
        let yjX = nativeint (y.DX) * sb
        let eX = xptr + sX
        while xptr <> eX do
            NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
            xptr <- xptr + xjX
            yptr <- yptr + yjX
    member x.CopyTo(y : NativeVector<'b>, f : 'a -> 'b) = 
        if info.Size <> y.Size then
            failwithf "NativeVector size mismatch: { src = %A; dst = %A }" info.Size y.Size
        x.CopyToX(y, f)
    static member Using<'b> (m : Vector<'a>, f : NativeVector<'a> -> 'b) = 
        let gc = GCHandle.Alloc(m.Data, GCHandleType.Pinned)
        try f (NativeVector<'a>(NativePtr.ofNativeInt (gc.AddrOfPinnedObject()), m.Info))
        finally gc.Free()
    member x.SubVector(beginX : int64, sizeX : int64, deltaX : int64) = NativeVector<'a>(ptr, info.SubVector(beginX, sizeX, deltaX))
    member x.SubVector(beginX : int64, sizeX : int64) = NativeVector<'a>(ptr, info.SubVector(beginX, sizeX))
    member x.GetSlice(minX : Option<int>, maxX : Option<int>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let info = VectorInfo(info.Index(beginX), sizeX, info.DX)
        let res = NativeVector<'a>(ptr, info)
        res
    member x.GetSlice(minX : Option<int64>, maxX : Option<int64>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let info = VectorInfo(info.Index(beginX), sizeX, info.DX)
        let res = NativeVector<'a>(ptr, info)
        res
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, value : 'a) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let info = VectorInfo(info.Index(beginX), sizeX, info.DX)
        let res = NativeVector<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, value : 'a) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let info = VectorInfo(info.Index(beginX), sizeX, info.DX)
        let res = NativeVector<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, src : NativeVector<'a>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let info = VectorInfo(info.Index(beginX), sizeX, info.DX)
        let res = NativeVector<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, src : NativeVector<'a>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let info = VectorInfo(info.Index(beginX), sizeX, info.DX)
        let res = NativeVector<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, src : Vector<'a>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let info = VectorInfo(info.Index(beginX), sizeX, info.DX)
        let res = NativeVector<'a>(ptr, info)
        NativeVector<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, src : Vector<'a>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let info = VectorInfo(info.Index(beginX), sizeX, info.DX)
        let res = NativeVector<'a>(ptr, info)
        NativeVector<'a>.Using(src, fun src -> src.CopyTo(res))

/// The NativeVector module providers convenient F#-style functions for accessing NativeVectors
module NativeVector =
    /// sets the entire Vector to the given value
    let inline set (value : 'a) (dst : NativeVector<'a>) = dst.Set(value)
    
    /// copies the content of 'src' to 'dst'
    let inline copy (src : NativeVector<'a>) (dst : NativeVector<'a>) = src.CopyTo(dst)
    
    /// copies the content of 'src' to 'dst' by applying the given function
    let inline copyWith (f : 'a -> 'b) (src : NativeVector<'a>) (dst : NativeVector<'b>) = src.CopyTo(dst, f)
    
    /// temporarily pins a Vector making it available as NativeVector
    let using (m : Vector<'a>) (f : NativeVector<'a> -> 'b) = NativeVector<'a>.Using(m, f)


[<Sealed>]
type NativeMatrix<'a when 'a : unmanaged>(ptr : nativeptr<'a>, info : MatrixInfo) = 
    member x.Pointer = ptr
    member x.Info = info
    member x.Size = info.Size
    member x.Delta = info.Delta
    member x.Origin = info.Origin
    member x.SX = info.SX
    member x.SY = info.SY
    member x.DX = info.DX
    member x.DY = info.DY
    member inline private x.SetXY(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX - info.SY * info.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let jY = nativeint (info.DY) * sa
        let eX = ptr + sX
        while ptr <> eX do
            let eY = ptr + sY
            while ptr <> eY do
                NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
                ptr <- ptr + jY
            ptr <- ptr + jX
    member inline private x.SetYX(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let jY = nativeint (info.DY - info.SX * info.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX) * sa
        let eY = ptr + sY
        while ptr <> eY do
            let eX = ptr + sX
            while ptr <> eX do
                NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
                ptr <- ptr + jX
            ptr <- ptr + jY
    member x.Set(value : 'a) = 
        let cXY = compare (abs info.DX) (abs info.DY)
        if cXY >= 0  then x.SetXY(value)
        else x.SetYX(value)
    member inline private x.CopyToXY(y : NativeMatrix<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SY * info.DY) * sa
        let yjX = nativeint (y.DX - y.SY * y.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY) * sa
        let yjY = nativeint (y.DY) * sa
        let eX = xptr + sX
        while xptr <> eX do
            let eY = xptr + sY
            while xptr <> eY do
                NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
                xptr <- xptr + xjY
                yptr <- yptr + yjY
            xptr <- xptr + xjX
            yptr <- yptr + yjX
    member inline private x.CopyToYX(y : NativeMatrix<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SX * info.DX) * sa
        let yjY = nativeint (y.DY - y.SX * y.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX) * sa
        let yjX = nativeint (y.DX) * sa
        let eY = xptr + sY
        while xptr <> eY do
            let eX = xptr + sX
            while xptr <> eX do
                NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
                xptr <- xptr + xjX
                yptr <- yptr + yjX
            xptr <- xptr + xjY
            yptr <- yptr + yjY
    member x.CopyTo(y : NativeMatrix<'a>) = 
        if info.Size <> y.Size then
            failwithf "NativeMatrix size mismatch: { src = %A; dst = %A }" info.Size y.Size
        let cXY = compare (abs info.DX) (abs info.DY)
        if cXY >= 0  then x.CopyToXY(y)
        else x.CopyToYX(y)
    member inline private x.CopyToXY<'b when 'b : unmanaged>(y : NativeMatrix<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SY * info.DY) * sa
        let yjX = nativeint (y.DX - y.SY * y.DY) * sb
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY) * sa
        let yjY = nativeint (y.DY) * sb
        let eX = xptr + sX
        while xptr <> eX do
            let eY = xptr + sY
            while xptr <> eY do
                NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
                xptr <- xptr + xjY
                yptr <- yptr + yjY
            xptr <- xptr + xjX
            yptr <- yptr + yjX
    member inline private x.CopyToYX<'b when 'b : unmanaged>(y : NativeMatrix<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SX * info.DX) * sa
        let yjY = nativeint (y.DY - y.SX * y.DX) * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX) * sa
        let yjX = nativeint (y.DX) * sb
        let eY = xptr + sY
        while xptr <> eY do
            let eX = xptr + sX
            while xptr <> eX do
                NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
                xptr <- xptr + xjX
                yptr <- yptr + yjX
            xptr <- xptr + xjY
            yptr <- yptr + yjY
    member x.CopyTo(y : NativeMatrix<'b>, f : 'a -> 'b) = 
        if info.Size <> y.Size then
            failwithf "NativeMatrix size mismatch: { src = %A; dst = %A }" info.Size y.Size
        let cXY = compare (abs info.DX) (abs info.DY)
        if cXY >= 0  then x.CopyToXY(y, f)
        else x.CopyToYX(y, f)
    static member Using<'b> (m : Matrix<'a>, f : NativeMatrix<'a> -> 'b) = 
        let gc = GCHandle.Alloc(m.Data, GCHandleType.Pinned)
        try f (NativeMatrix<'a>(NativePtr.ofNativeInt (gc.AddrOfPinnedObject()), m.Info))
        finally gc.Free()
    member x.SubMatrix(beginX : int64, beginY : int64, sizeX : int64, sizeY : int64, deltaX : int64, deltaY : int64) = NativeMatrix<'a>(ptr, info.SubMatrix(beginX, beginY, sizeX, sizeY, deltaX, deltaY))
    member x.SubMatrix(beginX : int64, beginY : int64, sizeX : int64, sizeY : int64) = NativeMatrix<'a>(ptr, info.SubMatrix(beginX, beginY, sizeX, sizeY))
    member x.SubMatrix(offset : V2l, size : V2l) = NativeMatrix<'a>(ptr, info.SubMatrix(offset, size))
    member x.SubMatrix(offset : V2l, size : V2l, delta : V2l) = NativeMatrix<'a>(ptr, info.SubMatrix(offset, size, delta))
    member x.SubMatrix(offset : V2i, size : V2i) = NativeMatrix<'a>(ptr, info.SubMatrix(offset, size))
    member x.SubMatrix(offset : V2i, size : V2i, delta : V2i) = NativeMatrix<'a>(ptr, info.SubMatrix(offset, size, delta))
    member x.GetSlice(minX : int, minY : Option<int>, maxY : Option<int>) = 
        let beginX = minX |> int64
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let info = VectorInfo(info.Index(beginX, beginY), sizeY, info.DY)
        let res = NativeVector<'a>(ptr, info)
        res
    member x.GetSlice(minX : int64, minY : Option<int64>, maxY : Option<int64>) = 
        let beginX = minX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let info = VectorInfo(info.Index(beginX, beginY), sizeY, info.DY)
        let res = NativeVector<'a>(ptr, info)
        res
    member x.SetSlice(minX : int, minY : Option<int>, maxY : Option<int>, value : 'a) = 
        let beginX = minX |> int64
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let info = VectorInfo(info.Index(beginX, beginY), sizeY, info.DY)
        let res = NativeVector<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : int64, minY : Option<int64>, maxY : Option<int64>, value : 'a) = 
        let beginX = minX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let info = VectorInfo(info.Index(beginX, beginY), sizeY, info.DY)
        let res = NativeVector<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : int, minY : Option<int>, maxY : Option<int>, src : NativeVector<'a>) = 
        let beginX = minX |> int64
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let info = VectorInfo(info.Index(beginX, beginY), sizeY, info.DY)
        let res = NativeVector<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : int64, minY : Option<int64>, maxY : Option<int64>, src : NativeVector<'a>) = 
        let beginX = minX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let info = VectorInfo(info.Index(beginX, beginY), sizeY, info.DY)
        let res = NativeVector<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : int, minY : Option<int>, maxY : Option<int>, src : Vector<'a>) = 
        let beginX = minX |> int64
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let info = VectorInfo(info.Index(beginX, beginY), sizeY, info.DY)
        let res = NativeVector<'a>(ptr, info)
        NativeVector<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.SetSlice(minX : int64, minY : Option<int64>, maxY : Option<int64>, src : Vector<'a>) = 
        let beginX = minX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let info = VectorInfo(info.Index(beginX, beginY), sizeY, info.DY)
        let res = NativeVector<'a>(ptr, info)
        NativeVector<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.GetSlice(minX : Option<int>, maxX : Option<int>, minY : int) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = minY |> int64
        let info = VectorInfo(info.Index(beginX, beginY), sizeX, info.DX)
        let res = NativeVector<'a>(ptr, info)
        res
    member x.GetSlice(minX : Option<int64>, maxX : Option<int64>, minY : int64) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = minY
        let info = VectorInfo(info.Index(beginX, beginY), sizeX, info.DX)
        let res = NativeVector<'a>(ptr, info)
        res
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : int, value : 'a) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = minY |> int64
        let info = VectorInfo(info.Index(beginX, beginY), sizeX, info.DX)
        let res = NativeVector<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : int64, value : 'a) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = minY
        let info = VectorInfo(info.Index(beginX, beginY), sizeX, info.DX)
        let res = NativeVector<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : int, src : NativeVector<'a>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = minY |> int64
        let info = VectorInfo(info.Index(beginX, beginY), sizeX, info.DX)
        let res = NativeVector<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : int64, src : NativeVector<'a>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = minY
        let info = VectorInfo(info.Index(beginX, beginY), sizeX, info.DX)
        let res = NativeVector<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : int, src : Vector<'a>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = minY |> int64
        let info = VectorInfo(info.Index(beginX, beginY), sizeX, info.DX)
        let res = NativeVector<'a>(ptr, info)
        NativeVector<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : int64, src : Vector<'a>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = minY
        let info = VectorInfo(info.Index(beginX, beginY), sizeX, info.DX)
        let res = NativeVector<'a>(ptr, info)
        NativeVector<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.GetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let info = MatrixInfo(info.Index(beginX, beginY), V2l(sizeX, sizeY), V2l(info.DX, info.DY))
        let res = NativeMatrix<'a>(ptr, info)
        res
    member x.GetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let info = MatrixInfo(info.Index(beginX, beginY), V2l(sizeX, sizeY), V2l(info.DX, info.DY))
        let res = NativeMatrix<'a>(ptr, info)
        res
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, value : 'a) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let info = MatrixInfo(info.Index(beginX, beginY), V2l(sizeX, sizeY), V2l(info.DX, info.DY))
        let res = NativeMatrix<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, value : 'a) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let info = MatrixInfo(info.Index(beginX, beginY), V2l(sizeX, sizeY), V2l(info.DX, info.DY))
        let res = NativeMatrix<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, src : NativeMatrix<'a>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let info = MatrixInfo(info.Index(beginX, beginY), V2l(sizeX, sizeY), V2l(info.DX, info.DY))
        let res = NativeMatrix<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, src : NativeMatrix<'a>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let info = MatrixInfo(info.Index(beginX, beginY), V2l(sizeX, sizeY), V2l(info.DX, info.DY))
        let res = NativeMatrix<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, src : Matrix<'a>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let info = MatrixInfo(info.Index(beginX, beginY), V2l(sizeX, sizeY), V2l(info.DX, info.DY))
        let res = NativeMatrix<'a>(ptr, info)
        NativeMatrix<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, src : Matrix<'a>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let info = MatrixInfo(info.Index(beginX, beginY), V2l(sizeX, sizeY), V2l(info.DX, info.DY))
        let res = NativeMatrix<'a>(ptr, info)
        NativeMatrix<'a>.Using(src, fun src -> src.CopyTo(res))

/// The NativeMatrix module providers convenient F#-style functions for accessing NativeMatrixs
module NativeMatrix =
    /// sets the entire Matrix to the given value
    let inline set (value : 'a) (dst : NativeMatrix<'a>) = dst.Set(value)
    
    /// copies the content of 'src' to 'dst'
    let inline copy (src : NativeMatrix<'a>) (dst : NativeMatrix<'a>) = src.CopyTo(dst)
    
    /// copies the content of 'src' to 'dst' by applying the given function
    let inline copyWith (f : 'a -> 'b) (src : NativeMatrix<'a>) (dst : NativeMatrix<'b>) = src.CopyTo(dst, f)
    
    /// temporarily pins a Matrix making it available as NativeMatrix
    let using (m : Matrix<'a>) (f : NativeMatrix<'a> -> 'b) = NativeMatrix<'a>.Using(m, f)


[<Sealed>]
type NativeVolume<'a when 'a : unmanaged>(ptr : nativeptr<'a>, info : VolumeInfo) = 
    member x.Pointer = ptr
    member x.Info = info
    member x.Size = info.Size
    member x.Delta = info.Delta
    member x.Origin = info.Origin
    member x.SX = info.SX
    member x.SY = info.SY
    member x.SZ = info.SZ
    member x.DX = info.DX
    member x.DY = info.DY
    member x.DZ = info.DZ
    member inline private x.SetXYZ(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX - info.SY * info.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let jY = nativeint (info.DY - info.SZ * info.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let jZ = nativeint (info.DZ) * sa
        let eX = ptr + sX
        while ptr <> eX do
            let eY = ptr + sY
            while ptr <> eY do
                let eZ = ptr + sZ
                while ptr <> eZ do
                    NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
                    ptr <- ptr + jZ
                ptr <- ptr + jY
            ptr <- ptr + jX
    member inline private x.SetYXZ(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let jY = nativeint (info.DY - info.SX * info.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX - info.SZ * info.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let jZ = nativeint (info.DZ) * sa
        let eY = ptr + sY
        while ptr <> eY do
            let eX = ptr + sX
            while ptr <> eX do
                let eZ = ptr + sZ
                while ptr <> eZ do
                    NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
                    ptr <- ptr + jZ
                ptr <- ptr + jX
            ptr <- ptr + jY
    member inline private x.SetYZX(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let jY = nativeint (info.DY - info.SZ * info.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let jZ = nativeint (info.DZ - info.SX * info.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX) * sa
        let eY = ptr + sY
        while ptr <> eY do
            let eZ = ptr + sZ
            while ptr <> eZ do
                let eX = ptr + sX
                while ptr <> eX do
                    NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
                    ptr <- ptr + jX
                ptr <- ptr + jZ
            ptr <- ptr + jY
    member inline private x.SetXZY(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX - info.SZ * info.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let jZ = nativeint (info.DZ - info.SY * info.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let jY = nativeint (info.DY) * sa
        let eX = ptr + sX
        while ptr <> eX do
            let eZ = ptr + sZ
            while ptr <> eZ do
                let eY = ptr + sY
                while ptr <> eY do
                    NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
                    ptr <- ptr + jY
                ptr <- ptr + jZ
            ptr <- ptr + jX
    member inline private x.SetZXY(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let jZ = nativeint (info.DZ - info.SX * info.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX - info.SY * info.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let jY = nativeint (info.DY) * sa
        let eZ = ptr + sZ
        while ptr <> eZ do
            let eX = ptr + sX
            while ptr <> eX do
                let eY = ptr + sY
                while ptr <> eY do
                    NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
                    ptr <- ptr + jY
                ptr <- ptr + jX
            ptr <- ptr + jZ
    member inline private x.SetZYX(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let jZ = nativeint (info.DZ - info.SY * info.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let jY = nativeint (info.DY - info.SX * info.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX) * sa
        let eZ = ptr + sZ
        while ptr <> eZ do
            let eY = ptr + sY
            while ptr <> eY do
                let eX = ptr + sX
                while ptr <> eX do
                    NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
                    ptr <- ptr + jX
                ptr <- ptr + jY
            ptr <- ptr + jZ
    member x.Set(value : 'a) = 
        let cXY = compare (abs info.DX) (abs info.DY)
        let cXZ = compare (abs info.DX) (abs info.DZ)
        let cYZ = compare (abs info.DY) (abs info.DZ)
        if cXY >= 0  && cXZ >= 0  && cYZ >= 0  then x.SetXYZ(value)
        elif cXY <= 0 && cXZ >= 0  && cYZ >= 0  then x.SetYXZ(value)
        elif cXY <= 0 && cXZ <= 0 && cYZ >= 0  then x.SetYZX(value)
        elif cXY >= 0  && cXZ >= 0  && cYZ <= 0 then x.SetXZY(value)
        elif cXY >= 0  && cXZ <= 0 && cYZ <= 0 then x.SetZXY(value)
        else x.SetZYX(value)
    member inline private x.CopyToXYZ(y : NativeVolume<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SY * info.DY) * sa
        let yjX = nativeint (y.DX - y.SY * y.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SZ * info.DZ) * sa
        let yjY = nativeint (y.DY - y.SZ * y.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ) * sa
        let yjZ = nativeint (y.DZ) * sa
        let eX = xptr + sX
        while xptr <> eX do
            let eY = xptr + sY
            while xptr <> eY do
                let eZ = xptr + sZ
                while xptr <> eZ do
                    NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
                    xptr <- xptr + xjZ
                    yptr <- yptr + yjZ
                xptr <- xptr + xjY
                yptr <- yptr + yjY
            xptr <- xptr + xjX
            yptr <- yptr + yjX
    member inline private x.CopyToYXZ(y : NativeVolume<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SX * info.DX) * sa
        let yjY = nativeint (y.DY - y.SX * y.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SZ * info.DZ) * sa
        let yjX = nativeint (y.DX - y.SZ * y.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ) * sa
        let yjZ = nativeint (y.DZ) * sa
        let eY = xptr + sY
        while xptr <> eY do
            let eX = xptr + sX
            while xptr <> eX do
                let eZ = xptr + sZ
                while xptr <> eZ do
                    NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
                    xptr <- xptr + xjZ
                    yptr <- yptr + yjZ
                xptr <- xptr + xjX
                yptr <- yptr + yjX
            xptr <- xptr + xjY
            yptr <- yptr + yjY
    member inline private x.CopyToYZX(y : NativeVolume<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SZ * info.DZ) * sa
        let yjY = nativeint (y.DY - y.SZ * y.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SX * info.DX) * sa
        let yjZ = nativeint (y.DZ - y.SX * y.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX) * sa
        let yjX = nativeint (y.DX) * sa
        let eY = xptr + sY
        while xptr <> eY do
            let eZ = xptr + sZ
            while xptr <> eZ do
                let eX = xptr + sX
                while xptr <> eX do
                    NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
                    xptr <- xptr + xjX
                    yptr <- yptr + yjX
                xptr <- xptr + xjZ
                yptr <- yptr + yjZ
            xptr <- xptr + xjY
            yptr <- yptr + yjY
    member inline private x.CopyToXZY(y : NativeVolume<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SZ * info.DZ) * sa
        let yjX = nativeint (y.DX - y.SZ * y.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SY * info.DY) * sa
        let yjZ = nativeint (y.DZ - y.SY * y.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY) * sa
        let yjY = nativeint (y.DY) * sa
        let eX = xptr + sX
        while xptr <> eX do
            let eZ = xptr + sZ
            while xptr <> eZ do
                let eY = xptr + sY
                while xptr <> eY do
                    NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
                    xptr <- xptr + xjY
                    yptr <- yptr + yjY
                xptr <- xptr + xjZ
                yptr <- yptr + yjZ
            xptr <- xptr + xjX
            yptr <- yptr + yjX
    member inline private x.CopyToZXY(y : NativeVolume<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SX * info.DX) * sa
        let yjZ = nativeint (y.DZ - y.SX * y.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SY * info.DY) * sa
        let yjX = nativeint (y.DX - y.SY * y.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY) * sa
        let yjY = nativeint (y.DY) * sa
        let eZ = xptr + sZ
        while xptr <> eZ do
            let eX = xptr + sX
            while xptr <> eX do
                let eY = xptr + sY
                while xptr <> eY do
                    NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
                    xptr <- xptr + xjY
                    yptr <- yptr + yjY
                xptr <- xptr + xjX
                yptr <- yptr + yjX
            xptr <- xptr + xjZ
            yptr <- yptr + yjZ
    member inline private x.CopyToZYX(y : NativeVolume<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SY * info.DY) * sa
        let yjZ = nativeint (y.DZ - y.SY * y.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SX * info.DX) * sa
        let yjY = nativeint (y.DY - y.SX * y.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX) * sa
        let yjX = nativeint (y.DX) * sa
        let eZ = xptr + sZ
        while xptr <> eZ do
            let eY = xptr + sY
            while xptr <> eY do
                let eX = xptr + sX
                while xptr <> eX do
                    NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
                    xptr <- xptr + xjX
                    yptr <- yptr + yjX
                xptr <- xptr + xjY
                yptr <- yptr + yjY
            xptr <- xptr + xjZ
            yptr <- yptr + yjZ
    member x.CopyTo(y : NativeVolume<'a>) = 
        if info.Size <> y.Size then
            failwithf "NativeVolume size mismatch: { src = %A; dst = %A }" info.Size y.Size
        let cXY = compare (abs info.DX) (abs info.DY)
        let cXZ = compare (abs info.DX) (abs info.DZ)
        let cYZ = compare (abs info.DY) (abs info.DZ)
        if cXY >= 0  && cXZ >= 0  && cYZ >= 0  then x.CopyToXYZ(y)
        elif cXY <= 0 && cXZ >= 0  && cYZ >= 0  then x.CopyToYXZ(y)
        elif cXY <= 0 && cXZ <= 0 && cYZ >= 0  then x.CopyToYZX(y)
        elif cXY >= 0  && cXZ >= 0  && cYZ <= 0 then x.CopyToXZY(y)
        elif cXY >= 0  && cXZ <= 0 && cYZ <= 0 then x.CopyToZXY(y)
        else x.CopyToZYX(y)
    member inline private x.CopyToXYZ<'b when 'b : unmanaged>(y : NativeVolume<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SY * info.DY) * sa
        let yjX = nativeint (y.DX - y.SY * y.DY) * sb
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SZ * info.DZ) * sa
        let yjY = nativeint (y.DY - y.SZ * y.DZ) * sb
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ) * sa
        let yjZ = nativeint (y.DZ) * sb
        let eX = xptr + sX
        while xptr <> eX do
            let eY = xptr + sY
            while xptr <> eY do
                let eZ = xptr + sZ
                while xptr <> eZ do
                    NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
                    xptr <- xptr + xjZ
                    yptr <- yptr + yjZ
                xptr <- xptr + xjY
                yptr <- yptr + yjY
            xptr <- xptr + xjX
            yptr <- yptr + yjX
    member inline private x.CopyToYXZ<'b when 'b : unmanaged>(y : NativeVolume<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SX * info.DX) * sa
        let yjY = nativeint (y.DY - y.SX * y.DX) * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SZ * info.DZ) * sa
        let yjX = nativeint (y.DX - y.SZ * y.DZ) * sb
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ) * sa
        let yjZ = nativeint (y.DZ) * sb
        let eY = xptr + sY
        while xptr <> eY do
            let eX = xptr + sX
            while xptr <> eX do
                let eZ = xptr + sZ
                while xptr <> eZ do
                    NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
                    xptr <- xptr + xjZ
                    yptr <- yptr + yjZ
                xptr <- xptr + xjX
                yptr <- yptr + yjX
            xptr <- xptr + xjY
            yptr <- yptr + yjY
    member inline private x.CopyToYZX<'b when 'b : unmanaged>(y : NativeVolume<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SZ * info.DZ) * sa
        let yjY = nativeint (y.DY - y.SZ * y.DZ) * sb
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SX * info.DX) * sa
        let yjZ = nativeint (y.DZ - y.SX * y.DX) * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX) * sa
        let yjX = nativeint (y.DX) * sb
        let eY = xptr + sY
        while xptr <> eY do
            let eZ = xptr + sZ
            while xptr <> eZ do
                let eX = xptr + sX
                while xptr <> eX do
                    NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
                    xptr <- xptr + xjX
                    yptr <- yptr + yjX
                xptr <- xptr + xjZ
                yptr <- yptr + yjZ
            xptr <- xptr + xjY
            yptr <- yptr + yjY
    member inline private x.CopyToXZY<'b when 'b : unmanaged>(y : NativeVolume<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SZ * info.DZ) * sa
        let yjX = nativeint (y.DX - y.SZ * y.DZ) * sb
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SY * info.DY) * sa
        let yjZ = nativeint (y.DZ - y.SY * y.DY) * sb
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY) * sa
        let yjY = nativeint (y.DY) * sb
        let eX = xptr + sX
        while xptr <> eX do
            let eZ = xptr + sZ
            while xptr <> eZ do
                let eY = xptr + sY
                while xptr <> eY do
                    NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
                    xptr <- xptr + xjY
                    yptr <- yptr + yjY
                xptr <- xptr + xjZ
                yptr <- yptr + yjZ
            xptr <- xptr + xjX
            yptr <- yptr + yjX
    member inline private x.CopyToZXY<'b when 'b : unmanaged>(y : NativeVolume<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SX * info.DX) * sa
        let yjZ = nativeint (y.DZ - y.SX * y.DX) * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SY * info.DY) * sa
        let yjX = nativeint (y.DX - y.SY * y.DY) * sb
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY) * sa
        let yjY = nativeint (y.DY) * sb
        let eZ = xptr + sZ
        while xptr <> eZ do
            let eX = xptr + sX
            while xptr <> eX do
                let eY = xptr + sY
                while xptr <> eY do
                    NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
                    xptr <- xptr + xjY
                    yptr <- yptr + yjY
                xptr <- xptr + xjX
                yptr <- yptr + yjX
            xptr <- xptr + xjZ
            yptr <- yptr + yjZ
    member inline private x.CopyToZYX<'b when 'b : unmanaged>(y : NativeVolume<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SY * info.DY) * sa
        let yjZ = nativeint (y.DZ - y.SY * y.DY) * sb
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SX * info.DX) * sa
        let yjY = nativeint (y.DY - y.SX * y.DX) * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX) * sa
        let yjX = nativeint (y.DX) * sb
        let eZ = xptr + sZ
        while xptr <> eZ do
            let eY = xptr + sY
            while xptr <> eY do
                let eX = xptr + sX
                while xptr <> eX do
                    NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
                    xptr <- xptr + xjX
                    yptr <- yptr + yjX
                xptr <- xptr + xjY
                yptr <- yptr + yjY
            xptr <- xptr + xjZ
            yptr <- yptr + yjZ
    member x.CopyTo(y : NativeVolume<'b>, f : 'a -> 'b) = 
        if info.Size <> y.Size then
            failwithf "NativeVolume size mismatch: { src = %A; dst = %A }" info.Size y.Size
        let cXY = compare (abs info.DX) (abs info.DY)
        let cXZ = compare (abs info.DX) (abs info.DZ)
        let cYZ = compare (abs info.DY) (abs info.DZ)
        if cXY >= 0  && cXZ >= 0  && cYZ >= 0  then x.CopyToXYZ(y, f)
        elif cXY <= 0 && cXZ >= 0  && cYZ >= 0  then x.CopyToYXZ(y, f)
        elif cXY <= 0 && cXZ <= 0 && cYZ >= 0  then x.CopyToYZX(y, f)
        elif cXY >= 0  && cXZ >= 0  && cYZ <= 0 then x.CopyToXZY(y, f)
        elif cXY >= 0  && cXZ <= 0 && cYZ <= 0 then x.CopyToZXY(y, f)
        else x.CopyToZYX(y, f)
    static member Using<'b> (m : Volume<'a>, f : NativeVolume<'a> -> 'b) = 
        let gc = GCHandle.Alloc(m.Data, GCHandleType.Pinned)
        try f (NativeVolume<'a>(NativePtr.ofNativeInt (gc.AddrOfPinnedObject()), m.Info))
        finally gc.Free()
    member x.SubVolume(beginX : int64, beginY : int64, beginZ : int64, sizeX : int64, sizeY : int64, sizeZ : int64, deltaX : int64, deltaY : int64, deltaZ : int64) = NativeVolume<'a>(ptr, info.SubVolume(beginX, beginY, beginZ, sizeX, sizeY, sizeZ, deltaX, deltaY, deltaZ))
    member x.SubVolume(beginX : int64, beginY : int64, beginZ : int64, sizeX : int64, sizeY : int64, sizeZ : int64) = NativeVolume<'a>(ptr, info.SubVolume(beginX, beginY, beginZ, sizeX, sizeY, sizeZ))
    member x.SubVolume(offset : V3l, size : V3l) = NativeVolume<'a>(ptr, info.SubVolume(offset, size))
    member x.SubVolume(offset : V3l, size : V3l, delta : V3l) = NativeVolume<'a>(ptr, info.SubVolume(offset, size, delta))
    member x.SubVolume(offset : V3i, size : V3i) = NativeVolume<'a>(ptr, info.SubVolume(offset, size))
    member x.SubVolume(offset : V3i, size : V3i, delta : V3i) = NativeVolume<'a>(ptr, info.SubVolume(offset, size, delta))
    member x.GetSlice(minX : int, minY : int, minZ : Option<int>, maxZ : Option<int>) = 
        let beginX = minX |> int64
        let beginY = minY |> int64
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let info = VectorInfo(info.Index(beginX, beginY, beginZ), sizeZ, info.DZ)
        let res = NativeVector<'a>(ptr, info)
        res
    member x.GetSlice(minX : int64, minY : int64, minZ : Option<int64>, maxZ : Option<int64>) = 
        let beginX = minX
        let beginY = minY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let info = VectorInfo(info.Index(beginX, beginY, beginZ), sizeZ, info.DZ)
        let res = NativeVector<'a>(ptr, info)
        res
    member x.SetSlice(minX : int, minY : int, minZ : Option<int>, maxZ : Option<int>, value : 'a) = 
        let beginX = minX |> int64
        let beginY = minY |> int64
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let info = VectorInfo(info.Index(beginX, beginY, beginZ), sizeZ, info.DZ)
        let res = NativeVector<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : int64, minY : int64, minZ : Option<int64>, maxZ : Option<int64>, value : 'a) = 
        let beginX = minX
        let beginY = minY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let info = VectorInfo(info.Index(beginX, beginY, beginZ), sizeZ, info.DZ)
        let res = NativeVector<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : int, minY : int, minZ : Option<int>, maxZ : Option<int>, src : NativeVector<'a>) = 
        let beginX = minX |> int64
        let beginY = minY |> int64
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let info = VectorInfo(info.Index(beginX, beginY, beginZ), sizeZ, info.DZ)
        let res = NativeVector<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : int64, minY : int64, minZ : Option<int64>, maxZ : Option<int64>, src : NativeVector<'a>) = 
        let beginX = minX
        let beginY = minY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let info = VectorInfo(info.Index(beginX, beginY, beginZ), sizeZ, info.DZ)
        let res = NativeVector<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : int, minY : int, minZ : Option<int>, maxZ : Option<int>, src : Vector<'a>) = 
        let beginX = minX |> int64
        let beginY = minY |> int64
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let info = VectorInfo(info.Index(beginX, beginY, beginZ), sizeZ, info.DZ)
        let res = NativeVector<'a>(ptr, info)
        NativeVector<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.SetSlice(minX : int64, minY : int64, minZ : Option<int64>, maxZ : Option<int64>, src : Vector<'a>) = 
        let beginX = minX
        let beginY = minY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let info = VectorInfo(info.Index(beginX, beginY, beginZ), sizeZ, info.DZ)
        let res = NativeVector<'a>(ptr, info)
        NativeVector<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.GetSlice(minX : int, minY : Option<int>, maxY : Option<int>, minZ : int) = 
        let beginX = minX |> int64
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ |> int64
        let info = VectorInfo(info.Index(beginX, beginY, beginZ), sizeY, info.DY)
        let res = NativeVector<'a>(ptr, info)
        res
    member x.GetSlice(minX : int64, minY : Option<int64>, maxY : Option<int64>, minZ : int64) = 
        let beginX = minX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ
        let info = VectorInfo(info.Index(beginX, beginY, beginZ), sizeY, info.DY)
        let res = NativeVector<'a>(ptr, info)
        res
    member x.SetSlice(minX : int, minY : Option<int>, maxY : Option<int>, minZ : int, value : 'a) = 
        let beginX = minX |> int64
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ |> int64
        let info = VectorInfo(info.Index(beginX, beginY, beginZ), sizeY, info.DY)
        let res = NativeVector<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : int64, minY : Option<int64>, maxY : Option<int64>, minZ : int64, value : 'a) = 
        let beginX = minX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ
        let info = VectorInfo(info.Index(beginX, beginY, beginZ), sizeY, info.DY)
        let res = NativeVector<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : int, minY : Option<int>, maxY : Option<int>, minZ : int, src : NativeVector<'a>) = 
        let beginX = minX |> int64
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ |> int64
        let info = VectorInfo(info.Index(beginX, beginY, beginZ), sizeY, info.DY)
        let res = NativeVector<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : int64, minY : Option<int64>, maxY : Option<int64>, minZ : int64, src : NativeVector<'a>) = 
        let beginX = minX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ
        let info = VectorInfo(info.Index(beginX, beginY, beginZ), sizeY, info.DY)
        let res = NativeVector<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : int, minY : Option<int>, maxY : Option<int>, minZ : int, src : Vector<'a>) = 
        let beginX = minX |> int64
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ |> int64
        let info = VectorInfo(info.Index(beginX, beginY, beginZ), sizeY, info.DY)
        let res = NativeVector<'a>(ptr, info)
        NativeVector<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.SetSlice(minX : int64, minY : Option<int64>, maxY : Option<int64>, minZ : int64, src : Vector<'a>) = 
        let beginX = minX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ
        let info = VectorInfo(info.Index(beginX, beginY, beginZ), sizeY, info.DY)
        let res = NativeVector<'a>(ptr, info)
        NativeVector<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.GetSlice(minX : int, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>) = 
        let beginX = minX |> int64
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ), V2l(sizeY, sizeZ), V2l(info.DY, info.DZ))
        let res = NativeMatrix<'a>(ptr, info)
        res
    member x.GetSlice(minX : int64, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>) = 
        let beginX = minX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ), V2l(sizeY, sizeZ), V2l(info.DY, info.DZ))
        let res = NativeMatrix<'a>(ptr, info)
        res
    member x.SetSlice(minX : int, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>, value : 'a) = 
        let beginX = minX |> int64
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ), V2l(sizeY, sizeZ), V2l(info.DY, info.DZ))
        let res = NativeMatrix<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : int64, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>, value : 'a) = 
        let beginX = minX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ), V2l(sizeY, sizeZ), V2l(info.DY, info.DZ))
        let res = NativeMatrix<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : int, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>, src : NativeMatrix<'a>) = 
        let beginX = minX |> int64
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ), V2l(sizeY, sizeZ), V2l(info.DY, info.DZ))
        let res = NativeMatrix<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : int64, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>, src : NativeMatrix<'a>) = 
        let beginX = minX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ), V2l(sizeY, sizeZ), V2l(info.DY, info.DZ))
        let res = NativeMatrix<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : int, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>, src : Matrix<'a>) = 
        let beginX = minX |> int64
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ), V2l(sizeY, sizeZ), V2l(info.DY, info.DZ))
        let res = NativeMatrix<'a>(ptr, info)
        NativeMatrix<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.SetSlice(minX : int64, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>, src : Matrix<'a>) = 
        let beginX = minX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ), V2l(sizeY, sizeZ), V2l(info.DY, info.DZ))
        let res = NativeMatrix<'a>(ptr, info)
        NativeMatrix<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.GetSlice(minX : Option<int>, maxX : Option<int>, minY : int, minZ : int) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = minY |> int64
        let beginZ = minZ |> int64
        let info = VectorInfo(info.Index(beginX, beginY, beginZ), sizeX, info.DX)
        let res = NativeVector<'a>(ptr, info)
        res
    member x.GetSlice(minX : Option<int64>, maxX : Option<int64>, minY : int64, minZ : int64) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = minY
        let beginZ = minZ
        let info = VectorInfo(info.Index(beginX, beginY, beginZ), sizeX, info.DX)
        let res = NativeVector<'a>(ptr, info)
        res
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : int, minZ : int, value : 'a) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = minY |> int64
        let beginZ = minZ |> int64
        let info = VectorInfo(info.Index(beginX, beginY, beginZ), sizeX, info.DX)
        let res = NativeVector<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : int64, minZ : int64, value : 'a) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = minY
        let beginZ = minZ
        let info = VectorInfo(info.Index(beginX, beginY, beginZ), sizeX, info.DX)
        let res = NativeVector<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : int, minZ : int, src : NativeVector<'a>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = minY |> int64
        let beginZ = minZ |> int64
        let info = VectorInfo(info.Index(beginX, beginY, beginZ), sizeX, info.DX)
        let res = NativeVector<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : int64, minZ : int64, src : NativeVector<'a>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = minY
        let beginZ = minZ
        let info = VectorInfo(info.Index(beginX, beginY, beginZ), sizeX, info.DX)
        let res = NativeVector<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : int, minZ : int, src : Vector<'a>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = minY |> int64
        let beginZ = minZ |> int64
        let info = VectorInfo(info.Index(beginX, beginY, beginZ), sizeX, info.DX)
        let res = NativeVector<'a>(ptr, info)
        NativeVector<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : int64, minZ : int64, src : Vector<'a>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = minY
        let beginZ = minZ
        let info = VectorInfo(info.Index(beginX, beginY, beginZ), sizeX, info.DX)
        let res = NativeVector<'a>(ptr, info)
        NativeVector<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.GetSlice(minX : Option<int>, maxX : Option<int>, minY : int, minZ : Option<int>, maxZ : Option<int>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = minY |> int64
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ), V2l(sizeX, sizeZ), V2l(info.DX, info.DZ))
        let res = NativeMatrix<'a>(ptr, info)
        res
    member x.GetSlice(minX : Option<int64>, maxX : Option<int64>, minY : int64, minZ : Option<int64>, maxZ : Option<int64>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = minY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ), V2l(sizeX, sizeZ), V2l(info.DX, info.DZ))
        let res = NativeMatrix<'a>(ptr, info)
        res
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : int, minZ : Option<int>, maxZ : Option<int>, value : 'a) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = minY |> int64
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ), V2l(sizeX, sizeZ), V2l(info.DX, info.DZ))
        let res = NativeMatrix<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : int64, minZ : Option<int64>, maxZ : Option<int64>, value : 'a) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = minY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ), V2l(sizeX, sizeZ), V2l(info.DX, info.DZ))
        let res = NativeMatrix<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : int, minZ : Option<int>, maxZ : Option<int>, src : NativeMatrix<'a>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = minY |> int64
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ), V2l(sizeX, sizeZ), V2l(info.DX, info.DZ))
        let res = NativeMatrix<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : int64, minZ : Option<int64>, maxZ : Option<int64>, src : NativeMatrix<'a>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = minY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ), V2l(sizeX, sizeZ), V2l(info.DX, info.DZ))
        let res = NativeMatrix<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : int, minZ : Option<int>, maxZ : Option<int>, src : Matrix<'a>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = minY |> int64
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ), V2l(sizeX, sizeZ), V2l(info.DX, info.DZ))
        let res = NativeMatrix<'a>(ptr, info)
        NativeMatrix<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : int64, minZ : Option<int64>, maxZ : Option<int64>, src : Matrix<'a>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = minY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ), V2l(sizeX, sizeZ), V2l(info.DX, info.DZ))
        let res = NativeMatrix<'a>(ptr, info)
        NativeMatrix<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.GetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, minZ : int) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ |> int64
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ), V2l(sizeX, sizeY), V2l(info.DX, info.DY))
        let res = NativeMatrix<'a>(ptr, info)
        res
    member x.GetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, minZ : int64) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ), V2l(sizeX, sizeY), V2l(info.DX, info.DY))
        let res = NativeMatrix<'a>(ptr, info)
        res
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, minZ : int, value : 'a) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ |> int64
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ), V2l(sizeX, sizeY), V2l(info.DX, info.DY))
        let res = NativeMatrix<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, minZ : int64, value : 'a) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ), V2l(sizeX, sizeY), V2l(info.DX, info.DY))
        let res = NativeMatrix<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, minZ : int, src : NativeMatrix<'a>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ |> int64
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ), V2l(sizeX, sizeY), V2l(info.DX, info.DY))
        let res = NativeMatrix<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, minZ : int64, src : NativeMatrix<'a>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ), V2l(sizeX, sizeY), V2l(info.DX, info.DY))
        let res = NativeMatrix<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, minZ : int, src : Matrix<'a>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ |> int64
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ), V2l(sizeX, sizeY), V2l(info.DX, info.DY))
        let res = NativeMatrix<'a>(ptr, info)
        NativeMatrix<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, minZ : int64, src : Matrix<'a>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ), V2l(sizeX, sizeY), V2l(info.DX, info.DY))
        let res = NativeMatrix<'a>(ptr, info)
        NativeMatrix<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.GetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ), V3l(sizeX, sizeY, sizeZ), V3l(info.DX, info.DY, info.DZ))
        let res = NativeVolume<'a>(ptr, info)
        res
    member x.GetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ), V3l(sizeX, sizeY, sizeZ), V3l(info.DX, info.DY, info.DZ))
        let res = NativeVolume<'a>(ptr, info)
        res
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>, value : 'a) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ), V3l(sizeX, sizeY, sizeZ), V3l(info.DX, info.DY, info.DZ))
        let res = NativeVolume<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>, value : 'a) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ), V3l(sizeX, sizeY, sizeZ), V3l(info.DX, info.DY, info.DZ))
        let res = NativeVolume<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>, src : NativeVolume<'a>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ), V3l(sizeX, sizeY, sizeZ), V3l(info.DX, info.DY, info.DZ))
        let res = NativeVolume<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>, src : NativeVolume<'a>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ), V3l(sizeX, sizeY, sizeZ), V3l(info.DX, info.DY, info.DZ))
        let res = NativeVolume<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>, src : Volume<'a>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ), V3l(sizeX, sizeY, sizeZ), V3l(info.DX, info.DY, info.DZ))
        let res = NativeVolume<'a>(ptr, info)
        NativeVolume<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>, src : Volume<'a>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ), V3l(sizeX, sizeY, sizeZ), V3l(info.DX, info.DY, info.DZ))
        let res = NativeVolume<'a>(ptr, info)
        NativeVolume<'a>.Using(src, fun src -> src.CopyTo(res))

/// The NativeVolume module providers convenient F#-style functions for accessing NativeVolumes
module NativeVolume =
    /// sets the entire Volume to the given value
    let inline set (value : 'a) (dst : NativeVolume<'a>) = dst.Set(value)
    
    /// copies the content of 'src' to 'dst'
    let inline copy (src : NativeVolume<'a>) (dst : NativeVolume<'a>) = src.CopyTo(dst)
    
    /// copies the content of 'src' to 'dst' by applying the given function
    let inline copyWith (f : 'a -> 'b) (src : NativeVolume<'a>) (dst : NativeVolume<'b>) = src.CopyTo(dst, f)
    
    /// temporarily pins a Volume making it available as NativeVolume
    let using (m : Volume<'a>) (f : NativeVolume<'a> -> 'b) = NativeVolume<'a>.Using(m, f)


[<Sealed>]
type NativeTensor4<'a when 'a : unmanaged>(ptr : nativeptr<'a>, info : Tensor4Info) = 
    member x.Pointer = ptr
    member x.Info = info
    member x.Size = info.Size
    member x.Delta = info.Delta
    member x.Origin = info.Origin
    member x.SX = info.SX
    member x.SY = info.SY
    member x.SZ = info.SZ
    member x.SW = info.SW
    member x.DX = info.DX
    member x.DY = info.DY
    member x.DZ = info.DZ
    member x.DW = info.DW
    member inline private x.SetXYZW(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX - info.SY * info.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let jY = nativeint (info.DY - info.SZ * info.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let jZ = nativeint (info.DZ - info.SW * info.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let jW = nativeint (info.DW) * sa
        let eX = ptr + sX
        while ptr <> eX do
            let eY = ptr + sY
            while ptr <> eY do
                let eZ = ptr + sZ
                while ptr <> eZ do
                    let eW = ptr + sW
                    while ptr <> eW do
                        NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
                        ptr <- ptr + jW
                    ptr <- ptr + jZ
                ptr <- ptr + jY
            ptr <- ptr + jX
    member inline private x.SetYXZW(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let jY = nativeint (info.DY - info.SX * info.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX - info.SZ * info.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let jZ = nativeint (info.DZ - info.SW * info.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let jW = nativeint (info.DW) * sa
        let eY = ptr + sY
        while ptr <> eY do
            let eX = ptr + sX
            while ptr <> eX do
                let eZ = ptr + sZ
                while ptr <> eZ do
                    let eW = ptr + sW
                    while ptr <> eW do
                        NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
                        ptr <- ptr + jW
                    ptr <- ptr + jZ
                ptr <- ptr + jX
            ptr <- ptr + jY
    member inline private x.SetYZXW(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let jY = nativeint (info.DY - info.SZ * info.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let jZ = nativeint (info.DZ - info.SX * info.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX - info.SW * info.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let jW = nativeint (info.DW) * sa
        let eY = ptr + sY
        while ptr <> eY do
            let eZ = ptr + sZ
            while ptr <> eZ do
                let eX = ptr + sX
                while ptr <> eX do
                    let eW = ptr + sW
                    while ptr <> eW do
                        NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
                        ptr <- ptr + jW
                    ptr <- ptr + jX
                ptr <- ptr + jZ
            ptr <- ptr + jY
    member inline private x.SetYZWX(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let jY = nativeint (info.DY - info.SZ * info.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let jZ = nativeint (info.DZ - info.SW * info.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let jW = nativeint (info.DW - info.SX * info.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX) * sa
        let eY = ptr + sY
        while ptr <> eY do
            let eZ = ptr + sZ
            while ptr <> eZ do
                let eW = ptr + sW
                while ptr <> eW do
                    let eX = ptr + sX
                    while ptr <> eX do
                        NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
                        ptr <- ptr + jX
                    ptr <- ptr + jW
                ptr <- ptr + jZ
            ptr <- ptr + jY
    member inline private x.SetXZYW(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX - info.SZ * info.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let jZ = nativeint (info.DZ - info.SY * info.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let jY = nativeint (info.DY - info.SW * info.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let jW = nativeint (info.DW) * sa
        let eX = ptr + sX
        while ptr <> eX do
            let eZ = ptr + sZ
            while ptr <> eZ do
                let eY = ptr + sY
                while ptr <> eY do
                    let eW = ptr + sW
                    while ptr <> eW do
                        NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
                        ptr <- ptr + jW
                    ptr <- ptr + jY
                ptr <- ptr + jZ
            ptr <- ptr + jX
    member inline private x.SetZXYW(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let jZ = nativeint (info.DZ - info.SX * info.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX - info.SY * info.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let jY = nativeint (info.DY - info.SW * info.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let jW = nativeint (info.DW) * sa
        let eZ = ptr + sZ
        while ptr <> eZ do
            let eX = ptr + sX
            while ptr <> eX do
                let eY = ptr + sY
                while ptr <> eY do
                    let eW = ptr + sW
                    while ptr <> eW do
                        NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
                        ptr <- ptr + jW
                    ptr <- ptr + jY
                ptr <- ptr + jX
            ptr <- ptr + jZ
    member inline private x.SetZYXW(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let jZ = nativeint (info.DZ - info.SY * info.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let jY = nativeint (info.DY - info.SX * info.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX - info.SW * info.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let jW = nativeint (info.DW) * sa
        let eZ = ptr + sZ
        while ptr <> eZ do
            let eY = ptr + sY
            while ptr <> eY do
                let eX = ptr + sX
                while ptr <> eX do
                    let eW = ptr + sW
                    while ptr <> eW do
                        NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
                        ptr <- ptr + jW
                    ptr <- ptr + jX
                ptr <- ptr + jY
            ptr <- ptr + jZ
    member inline private x.SetZYWX(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let jZ = nativeint (info.DZ - info.SY * info.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let jY = nativeint (info.DY - info.SW * info.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let jW = nativeint (info.DW - info.SX * info.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX) * sa
        let eZ = ptr + sZ
        while ptr <> eZ do
            let eY = ptr + sY
            while ptr <> eY do
                let eW = ptr + sW
                while ptr <> eW do
                    let eX = ptr + sX
                    while ptr <> eX do
                        NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
                        ptr <- ptr + jX
                    ptr <- ptr + jW
                ptr <- ptr + jY
            ptr <- ptr + jZ
    member inline private x.SetXZWY(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX - info.SZ * info.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let jZ = nativeint (info.DZ - info.SW * info.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let jW = nativeint (info.DW - info.SY * info.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let jY = nativeint (info.DY) * sa
        let eX = ptr + sX
        while ptr <> eX do
            let eZ = ptr + sZ
            while ptr <> eZ do
                let eW = ptr + sW
                while ptr <> eW do
                    let eY = ptr + sY
                    while ptr <> eY do
                        NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
                        ptr <- ptr + jY
                    ptr <- ptr + jW
                ptr <- ptr + jZ
            ptr <- ptr + jX
    member inline private x.SetZXWY(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let jZ = nativeint (info.DZ - info.SX * info.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX - info.SW * info.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let jW = nativeint (info.DW - info.SY * info.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let jY = nativeint (info.DY) * sa
        let eZ = ptr + sZ
        while ptr <> eZ do
            let eX = ptr + sX
            while ptr <> eX do
                let eW = ptr + sW
                while ptr <> eW do
                    let eY = ptr + sY
                    while ptr <> eY do
                        NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
                        ptr <- ptr + jY
                    ptr <- ptr + jW
                ptr <- ptr + jX
            ptr <- ptr + jZ
    member inline private x.SetZWXY(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let jZ = nativeint (info.DZ - info.SW * info.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let jW = nativeint (info.DW - info.SX * info.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX - info.SY * info.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let jY = nativeint (info.DY) * sa
        let eZ = ptr + sZ
        while ptr <> eZ do
            let eW = ptr + sW
            while ptr <> eW do
                let eX = ptr + sX
                while ptr <> eX do
                    let eY = ptr + sY
                    while ptr <> eY do
                        NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
                        ptr <- ptr + jY
                    ptr <- ptr + jX
                ptr <- ptr + jW
            ptr <- ptr + jZ
    member inline private x.SetZWYX(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let jZ = nativeint (info.DZ - info.SW * info.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let jW = nativeint (info.DW - info.SY * info.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let jY = nativeint (info.DY - info.SX * info.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX) * sa
        let eZ = ptr + sZ
        while ptr <> eZ do
            let eW = ptr + sW
            while ptr <> eW do
                let eY = ptr + sY
                while ptr <> eY do
                    let eX = ptr + sX
                    while ptr <> eX do
                        NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
                        ptr <- ptr + jX
                    ptr <- ptr + jY
                ptr <- ptr + jW
            ptr <- ptr + jZ
    member inline private x.SetXYWZ(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX - info.SY * info.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let jY = nativeint (info.DY - info.SW * info.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let jW = nativeint (info.DW - info.SZ * info.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let jZ = nativeint (info.DZ) * sa
        let eX = ptr + sX
        while ptr <> eX do
            let eY = ptr + sY
            while ptr <> eY do
                let eW = ptr + sW
                while ptr <> eW do
                    let eZ = ptr + sZ
                    while ptr <> eZ do
                        NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
                        ptr <- ptr + jZ
                    ptr <- ptr + jW
                ptr <- ptr + jY
            ptr <- ptr + jX
    member inline private x.SetYXWZ(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let jY = nativeint (info.DY - info.SX * info.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX - info.SW * info.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let jW = nativeint (info.DW - info.SZ * info.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let jZ = nativeint (info.DZ) * sa
        let eY = ptr + sY
        while ptr <> eY do
            let eX = ptr + sX
            while ptr <> eX do
                let eW = ptr + sW
                while ptr <> eW do
                    let eZ = ptr + sZ
                    while ptr <> eZ do
                        NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
                        ptr <- ptr + jZ
                    ptr <- ptr + jW
                ptr <- ptr + jX
            ptr <- ptr + jY
    member inline private x.SetYWXZ(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let jY = nativeint (info.DY - info.SW * info.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let jW = nativeint (info.DW - info.SX * info.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX - info.SZ * info.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let jZ = nativeint (info.DZ) * sa
        let eY = ptr + sY
        while ptr <> eY do
            let eW = ptr + sW
            while ptr <> eW do
                let eX = ptr + sX
                while ptr <> eX do
                    let eZ = ptr + sZ
                    while ptr <> eZ do
                        NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
                        ptr <- ptr + jZ
                    ptr <- ptr + jX
                ptr <- ptr + jW
            ptr <- ptr + jY
    member inline private x.SetYWZX(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let jY = nativeint (info.DY - info.SW * info.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let jW = nativeint (info.DW - info.SZ * info.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let jZ = nativeint (info.DZ - info.SX * info.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX) * sa
        let eY = ptr + sY
        while ptr <> eY do
            let eW = ptr + sW
            while ptr <> eW do
                let eZ = ptr + sZ
                while ptr <> eZ do
                    let eX = ptr + sX
                    while ptr <> eX do
                        NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
                        ptr <- ptr + jX
                    ptr <- ptr + jZ
                ptr <- ptr + jW
            ptr <- ptr + jY
    member inline private x.SetXWYZ(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX - info.SW * info.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let jW = nativeint (info.DW - info.SY * info.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let jY = nativeint (info.DY - info.SZ * info.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let jZ = nativeint (info.DZ) * sa
        let eX = ptr + sX
        while ptr <> eX do
            let eW = ptr + sW
            while ptr <> eW do
                let eY = ptr + sY
                while ptr <> eY do
                    let eZ = ptr + sZ
                    while ptr <> eZ do
                        NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
                        ptr <- ptr + jZ
                    ptr <- ptr + jY
                ptr <- ptr + jW
            ptr <- ptr + jX
    member inline private x.SetWXYZ(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let jW = nativeint (info.DW - info.SX * info.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX - info.SY * info.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let jY = nativeint (info.DY - info.SZ * info.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let jZ = nativeint (info.DZ) * sa
        let eW = ptr + sW
        while ptr <> eW do
            let eX = ptr + sX
            while ptr <> eX do
                let eY = ptr + sY
                while ptr <> eY do
                    let eZ = ptr + sZ
                    while ptr <> eZ do
                        NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
                        ptr <- ptr + jZ
                    ptr <- ptr + jY
                ptr <- ptr + jX
            ptr <- ptr + jW
    member inline private x.SetWYXZ(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let jW = nativeint (info.DW - info.SY * info.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let jY = nativeint (info.DY - info.SX * info.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX - info.SZ * info.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let jZ = nativeint (info.DZ) * sa
        let eW = ptr + sW
        while ptr <> eW do
            let eY = ptr + sY
            while ptr <> eY do
                let eX = ptr + sX
                while ptr <> eX do
                    let eZ = ptr + sZ
                    while ptr <> eZ do
                        NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
                        ptr <- ptr + jZ
                    ptr <- ptr + jX
                ptr <- ptr + jY
            ptr <- ptr + jW
    member inline private x.SetWYZX(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let jW = nativeint (info.DW - info.SY * info.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let jY = nativeint (info.DY - info.SZ * info.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let jZ = nativeint (info.DZ - info.SX * info.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX) * sa
        let eW = ptr + sW
        while ptr <> eW do
            let eY = ptr + sY
            while ptr <> eY do
                let eZ = ptr + sZ
                while ptr <> eZ do
                    let eX = ptr + sX
                    while ptr <> eX do
                        NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
                        ptr <- ptr + jX
                    ptr <- ptr + jZ
                ptr <- ptr + jY
            ptr <- ptr + jW
    member inline private x.SetXWZY(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX - info.SW * info.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let jW = nativeint (info.DW - info.SZ * info.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let jZ = nativeint (info.DZ - info.SY * info.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let jY = nativeint (info.DY) * sa
        let eX = ptr + sX
        while ptr <> eX do
            let eW = ptr + sW
            while ptr <> eW do
                let eZ = ptr + sZ
                while ptr <> eZ do
                    let eY = ptr + sY
                    while ptr <> eY do
                        NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
                        ptr <- ptr + jY
                    ptr <- ptr + jZ
                ptr <- ptr + jW
            ptr <- ptr + jX
    member inline private x.SetWXZY(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let jW = nativeint (info.DW - info.SX * info.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX - info.SZ * info.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let jZ = nativeint (info.DZ - info.SY * info.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let jY = nativeint (info.DY) * sa
        let eW = ptr + sW
        while ptr <> eW do
            let eX = ptr + sX
            while ptr <> eX do
                let eZ = ptr + sZ
                while ptr <> eZ do
                    let eY = ptr + sY
                    while ptr <> eY do
                        NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
                        ptr <- ptr + jY
                    ptr <- ptr + jZ
                ptr <- ptr + jX
            ptr <- ptr + jW
    member inline private x.SetWZXY(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let jW = nativeint (info.DW - info.SZ * info.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let jZ = nativeint (info.DZ - info.SX * info.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX - info.SY * info.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let jY = nativeint (info.DY) * sa
        let eW = ptr + sW
        while ptr <> eW do
            let eZ = ptr + sZ
            while ptr <> eZ do
                let eX = ptr + sX
                while ptr <> eX do
                    let eY = ptr + sY
                    while ptr <> eY do
                        NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
                        ptr <- ptr + jY
                    ptr <- ptr + jX
                ptr <- ptr + jZ
            ptr <- ptr + jW
    member inline private x.SetWZYX(value : 'a) = 
        let sa = nativeint (sizeof<'a>)
        let mutable ptr = ptr |> NativePtr.toNativeInt
        ptr <- ptr + nativeint info.Origin * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let jW = nativeint (info.DW - info.SZ * info.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let jZ = nativeint (info.DZ - info.SY * info.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let jY = nativeint (info.DY - info.SX * info.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let jX = nativeint (info.DX) * sa
        let eW = ptr + sW
        while ptr <> eW do
            let eZ = ptr + sZ
            while ptr <> eZ do
                let eY = ptr + sY
                while ptr <> eY do
                    let eX = ptr + sX
                    while ptr <> eX do
                        NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value
                        ptr <- ptr + jX
                    ptr <- ptr + jY
                ptr <- ptr + jZ
            ptr <- ptr + jW
    member x.Set(value : 'a) = 
        let cXW = compare (abs info.DX) (abs info.DW)
        let cXY = compare (abs info.DX) (abs info.DY)
        let cXZ = compare (abs info.DX) (abs info.DZ)
        let cYW = compare (abs info.DY) (abs info.DW)
        let cYZ = compare (abs info.DY) (abs info.DZ)
        let cZW = compare (abs info.DZ) (abs info.DW)
        if cXW >= 0  && cXY >= 0  && cXZ >= 0  && cYW >= 0  && cYZ >= 0  && cZW >= 0  then x.SetXYZW(value)
        elif cXW >= 0  && cXY <= 0 && cXZ >= 0  && cYW >= 0  && cYZ >= 0  && cZW >= 0  then x.SetYXZW(value)
        elif cXW >= 0  && cXY <= 0 && cXZ <= 0 && cYW >= 0  && cYZ >= 0  && cZW >= 0  then x.SetYZXW(value)
        elif cXW <= 0 && cXY <= 0 && cXZ <= 0 && cYW >= 0  && cYZ >= 0  && cZW >= 0  then x.SetYZWX(value)
        elif cXW >= 0  && cXY >= 0  && cXZ >= 0  && cYW >= 0  && cYZ <= 0 && cZW >= 0  then x.SetXZYW(value)
        elif cXW >= 0  && cXY >= 0  && cXZ <= 0 && cYW >= 0  && cYZ <= 0 && cZW >= 0  then x.SetZXYW(value)
        elif cXW >= 0  && cXY <= 0 && cXZ <= 0 && cYW >= 0  && cYZ <= 0 && cZW >= 0  then x.SetZYXW(value)
        elif cXW <= 0 && cXY <= 0 && cXZ <= 0 && cYW >= 0  && cYZ <= 0 && cZW >= 0  then x.SetZYWX(value)
        elif cXW >= 0  && cXY >= 0  && cXZ >= 0  && cYW <= 0 && cYZ <= 0 && cZW >= 0  then x.SetXZWY(value)
        elif cXW >= 0  && cXY >= 0  && cXZ <= 0 && cYW <= 0 && cYZ <= 0 && cZW >= 0  then x.SetZXWY(value)
        elif cXW <= 0 && cXY >= 0  && cXZ <= 0 && cYW <= 0 && cYZ <= 0 && cZW >= 0  then x.SetZWXY(value)
        elif cXW <= 0 && cXY <= 0 && cXZ <= 0 && cYW <= 0 && cYZ <= 0 && cZW >= 0  then x.SetZWYX(value)
        elif cXW >= 0  && cXY >= 0  && cXZ >= 0  && cYW >= 0  && cYZ >= 0  && cZW <= 0 then x.SetXYWZ(value)
        elif cXW >= 0  && cXY <= 0 && cXZ >= 0  && cYW >= 0  && cYZ >= 0  && cZW <= 0 then x.SetYXWZ(value)
        elif cXW <= 0 && cXY <= 0 && cXZ >= 0  && cYW >= 0  && cYZ >= 0  && cZW <= 0 then x.SetYWXZ(value)
        elif cXW <= 0 && cXY <= 0 && cXZ <= 0 && cYW >= 0  && cYZ >= 0  && cZW <= 0 then x.SetYWZX(value)
        elif cXW >= 0  && cXY >= 0  && cXZ >= 0  && cYW <= 0 && cYZ >= 0  && cZW <= 0 then x.SetXWYZ(value)
        elif cXW <= 0 && cXY >= 0  && cXZ >= 0  && cYW <= 0 && cYZ >= 0  && cZW <= 0 then x.SetWXYZ(value)
        elif cXW <= 0 && cXY <= 0 && cXZ >= 0  && cYW <= 0 && cYZ >= 0  && cZW <= 0 then x.SetWYXZ(value)
        elif cXW <= 0 && cXY <= 0 && cXZ <= 0 && cYW <= 0 && cYZ >= 0  && cZW <= 0 then x.SetWYZX(value)
        elif cXW >= 0  && cXY >= 0  && cXZ >= 0  && cYW <= 0 && cYZ <= 0 && cZW <= 0 then x.SetXWZY(value)
        elif cXW <= 0 && cXY >= 0  && cXZ >= 0  && cYW <= 0 && cYZ <= 0 && cZW <= 0 then x.SetWXZY(value)
        elif cXW <= 0 && cXY >= 0  && cXZ <= 0 && cYW <= 0 && cYZ <= 0 && cZW <= 0 then x.SetWZXY(value)
        else x.SetWZYX(value)
    member inline private x.CopyToXYZW(y : NativeTensor4<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SY * info.DY) * sa
        let yjX = nativeint (y.DX - y.SY * y.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SZ * info.DZ) * sa
        let yjY = nativeint (y.DY - y.SZ * y.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SW * info.DW) * sa
        let yjZ = nativeint (y.DZ - y.SW * y.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW) * sa
        let yjW = nativeint (y.DW) * sa
        let eX = xptr + sX
        while xptr <> eX do
            let eY = xptr + sY
            while xptr <> eY do
                let eZ = xptr + sZ
                while xptr <> eZ do
                    let eW = xptr + sW
                    while xptr <> eW do
                        NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
                        xptr <- xptr + xjW
                        yptr <- yptr + yjW
                    xptr <- xptr + xjZ
                    yptr <- yptr + yjZ
                xptr <- xptr + xjY
                yptr <- yptr + yjY
            xptr <- xptr + xjX
            yptr <- yptr + yjX
    member inline private x.CopyToYXZW(y : NativeTensor4<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SX * info.DX) * sa
        let yjY = nativeint (y.DY - y.SX * y.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SZ * info.DZ) * sa
        let yjX = nativeint (y.DX - y.SZ * y.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SW * info.DW) * sa
        let yjZ = nativeint (y.DZ - y.SW * y.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW) * sa
        let yjW = nativeint (y.DW) * sa
        let eY = xptr + sY
        while xptr <> eY do
            let eX = xptr + sX
            while xptr <> eX do
                let eZ = xptr + sZ
                while xptr <> eZ do
                    let eW = xptr + sW
                    while xptr <> eW do
                        NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
                        xptr <- xptr + xjW
                        yptr <- yptr + yjW
                    xptr <- xptr + xjZ
                    yptr <- yptr + yjZ
                xptr <- xptr + xjX
                yptr <- yptr + yjX
            xptr <- xptr + xjY
            yptr <- yptr + yjY
    member inline private x.CopyToYZXW(y : NativeTensor4<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SZ * info.DZ) * sa
        let yjY = nativeint (y.DY - y.SZ * y.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SX * info.DX) * sa
        let yjZ = nativeint (y.DZ - y.SX * y.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SW * info.DW) * sa
        let yjX = nativeint (y.DX - y.SW * y.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW) * sa
        let yjW = nativeint (y.DW) * sa
        let eY = xptr + sY
        while xptr <> eY do
            let eZ = xptr + sZ
            while xptr <> eZ do
                let eX = xptr + sX
                while xptr <> eX do
                    let eW = xptr + sW
                    while xptr <> eW do
                        NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
                        xptr <- xptr + xjW
                        yptr <- yptr + yjW
                    xptr <- xptr + xjX
                    yptr <- yptr + yjX
                xptr <- xptr + xjZ
                yptr <- yptr + yjZ
            xptr <- xptr + xjY
            yptr <- yptr + yjY
    member inline private x.CopyToYZWX(y : NativeTensor4<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SZ * info.DZ) * sa
        let yjY = nativeint (y.DY - y.SZ * y.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SW * info.DW) * sa
        let yjZ = nativeint (y.DZ - y.SW * y.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SX * info.DX) * sa
        let yjW = nativeint (y.DW - y.SX * y.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX) * sa
        let yjX = nativeint (y.DX) * sa
        let eY = xptr + sY
        while xptr <> eY do
            let eZ = xptr + sZ
            while xptr <> eZ do
                let eW = xptr + sW
                while xptr <> eW do
                    let eX = xptr + sX
                    while xptr <> eX do
                        NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
                        xptr <- xptr + xjX
                        yptr <- yptr + yjX
                    xptr <- xptr + xjW
                    yptr <- yptr + yjW
                xptr <- xptr + xjZ
                yptr <- yptr + yjZ
            xptr <- xptr + xjY
            yptr <- yptr + yjY
    member inline private x.CopyToXZYW(y : NativeTensor4<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SZ * info.DZ) * sa
        let yjX = nativeint (y.DX - y.SZ * y.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SY * info.DY) * sa
        let yjZ = nativeint (y.DZ - y.SY * y.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SW * info.DW) * sa
        let yjY = nativeint (y.DY - y.SW * y.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW) * sa
        let yjW = nativeint (y.DW) * sa
        let eX = xptr + sX
        while xptr <> eX do
            let eZ = xptr + sZ
            while xptr <> eZ do
                let eY = xptr + sY
                while xptr <> eY do
                    let eW = xptr + sW
                    while xptr <> eW do
                        NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
                        xptr <- xptr + xjW
                        yptr <- yptr + yjW
                    xptr <- xptr + xjY
                    yptr <- yptr + yjY
                xptr <- xptr + xjZ
                yptr <- yptr + yjZ
            xptr <- xptr + xjX
            yptr <- yptr + yjX
    member inline private x.CopyToZXYW(y : NativeTensor4<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SX * info.DX) * sa
        let yjZ = nativeint (y.DZ - y.SX * y.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SY * info.DY) * sa
        let yjX = nativeint (y.DX - y.SY * y.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SW * info.DW) * sa
        let yjY = nativeint (y.DY - y.SW * y.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW) * sa
        let yjW = nativeint (y.DW) * sa
        let eZ = xptr + sZ
        while xptr <> eZ do
            let eX = xptr + sX
            while xptr <> eX do
                let eY = xptr + sY
                while xptr <> eY do
                    let eW = xptr + sW
                    while xptr <> eW do
                        NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
                        xptr <- xptr + xjW
                        yptr <- yptr + yjW
                    xptr <- xptr + xjY
                    yptr <- yptr + yjY
                xptr <- xptr + xjX
                yptr <- yptr + yjX
            xptr <- xptr + xjZ
            yptr <- yptr + yjZ
    member inline private x.CopyToZYXW(y : NativeTensor4<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SY * info.DY) * sa
        let yjZ = nativeint (y.DZ - y.SY * y.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SX * info.DX) * sa
        let yjY = nativeint (y.DY - y.SX * y.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SW * info.DW) * sa
        let yjX = nativeint (y.DX - y.SW * y.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW) * sa
        let yjW = nativeint (y.DW) * sa
        let eZ = xptr + sZ
        while xptr <> eZ do
            let eY = xptr + sY
            while xptr <> eY do
                let eX = xptr + sX
                while xptr <> eX do
                    let eW = xptr + sW
                    while xptr <> eW do
                        NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
                        xptr <- xptr + xjW
                        yptr <- yptr + yjW
                    xptr <- xptr + xjX
                    yptr <- yptr + yjX
                xptr <- xptr + xjY
                yptr <- yptr + yjY
            xptr <- xptr + xjZ
            yptr <- yptr + yjZ
    member inline private x.CopyToZYWX(y : NativeTensor4<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SY * info.DY) * sa
        let yjZ = nativeint (y.DZ - y.SY * y.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SW * info.DW) * sa
        let yjY = nativeint (y.DY - y.SW * y.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SX * info.DX) * sa
        let yjW = nativeint (y.DW - y.SX * y.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX) * sa
        let yjX = nativeint (y.DX) * sa
        let eZ = xptr + sZ
        while xptr <> eZ do
            let eY = xptr + sY
            while xptr <> eY do
                let eW = xptr + sW
                while xptr <> eW do
                    let eX = xptr + sX
                    while xptr <> eX do
                        NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
                        xptr <- xptr + xjX
                        yptr <- yptr + yjX
                    xptr <- xptr + xjW
                    yptr <- yptr + yjW
                xptr <- xptr + xjY
                yptr <- yptr + yjY
            xptr <- xptr + xjZ
            yptr <- yptr + yjZ
    member inline private x.CopyToXZWY(y : NativeTensor4<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SZ * info.DZ) * sa
        let yjX = nativeint (y.DX - y.SZ * y.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SW * info.DW) * sa
        let yjZ = nativeint (y.DZ - y.SW * y.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SY * info.DY) * sa
        let yjW = nativeint (y.DW - y.SY * y.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY) * sa
        let yjY = nativeint (y.DY) * sa
        let eX = xptr + sX
        while xptr <> eX do
            let eZ = xptr + sZ
            while xptr <> eZ do
                let eW = xptr + sW
                while xptr <> eW do
                    let eY = xptr + sY
                    while xptr <> eY do
                        NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
                        xptr <- xptr + xjY
                        yptr <- yptr + yjY
                    xptr <- xptr + xjW
                    yptr <- yptr + yjW
                xptr <- xptr + xjZ
                yptr <- yptr + yjZ
            xptr <- xptr + xjX
            yptr <- yptr + yjX
    member inline private x.CopyToZXWY(y : NativeTensor4<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SX * info.DX) * sa
        let yjZ = nativeint (y.DZ - y.SX * y.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SW * info.DW) * sa
        let yjX = nativeint (y.DX - y.SW * y.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SY * info.DY) * sa
        let yjW = nativeint (y.DW - y.SY * y.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY) * sa
        let yjY = nativeint (y.DY) * sa
        let eZ = xptr + sZ
        while xptr <> eZ do
            let eX = xptr + sX
            while xptr <> eX do
                let eW = xptr + sW
                while xptr <> eW do
                    let eY = xptr + sY
                    while xptr <> eY do
                        NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
                        xptr <- xptr + xjY
                        yptr <- yptr + yjY
                    xptr <- xptr + xjW
                    yptr <- yptr + yjW
                xptr <- xptr + xjX
                yptr <- yptr + yjX
            xptr <- xptr + xjZ
            yptr <- yptr + yjZ
    member inline private x.CopyToZWXY(y : NativeTensor4<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SW * info.DW) * sa
        let yjZ = nativeint (y.DZ - y.SW * y.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SX * info.DX) * sa
        let yjW = nativeint (y.DW - y.SX * y.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SY * info.DY) * sa
        let yjX = nativeint (y.DX - y.SY * y.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY) * sa
        let yjY = nativeint (y.DY) * sa
        let eZ = xptr + sZ
        while xptr <> eZ do
            let eW = xptr + sW
            while xptr <> eW do
                let eX = xptr + sX
                while xptr <> eX do
                    let eY = xptr + sY
                    while xptr <> eY do
                        NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
                        xptr <- xptr + xjY
                        yptr <- yptr + yjY
                    xptr <- xptr + xjX
                    yptr <- yptr + yjX
                xptr <- xptr + xjW
                yptr <- yptr + yjW
            xptr <- xptr + xjZ
            yptr <- yptr + yjZ
    member inline private x.CopyToZWYX(y : NativeTensor4<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SW * info.DW) * sa
        let yjZ = nativeint (y.DZ - y.SW * y.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SY * info.DY) * sa
        let yjW = nativeint (y.DW - y.SY * y.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SX * info.DX) * sa
        let yjY = nativeint (y.DY - y.SX * y.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX) * sa
        let yjX = nativeint (y.DX) * sa
        let eZ = xptr + sZ
        while xptr <> eZ do
            let eW = xptr + sW
            while xptr <> eW do
                let eY = xptr + sY
                while xptr <> eY do
                    let eX = xptr + sX
                    while xptr <> eX do
                        NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
                        xptr <- xptr + xjX
                        yptr <- yptr + yjX
                    xptr <- xptr + xjY
                    yptr <- yptr + yjY
                xptr <- xptr + xjW
                yptr <- yptr + yjW
            xptr <- xptr + xjZ
            yptr <- yptr + yjZ
    member inline private x.CopyToXYWZ(y : NativeTensor4<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SY * info.DY) * sa
        let yjX = nativeint (y.DX - y.SY * y.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SW * info.DW) * sa
        let yjY = nativeint (y.DY - y.SW * y.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SZ * info.DZ) * sa
        let yjW = nativeint (y.DW - y.SZ * y.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ) * sa
        let yjZ = nativeint (y.DZ) * sa
        let eX = xptr + sX
        while xptr <> eX do
            let eY = xptr + sY
            while xptr <> eY do
                let eW = xptr + sW
                while xptr <> eW do
                    let eZ = xptr + sZ
                    while xptr <> eZ do
                        NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
                        xptr <- xptr + xjZ
                        yptr <- yptr + yjZ
                    xptr <- xptr + xjW
                    yptr <- yptr + yjW
                xptr <- xptr + xjY
                yptr <- yptr + yjY
            xptr <- xptr + xjX
            yptr <- yptr + yjX
    member inline private x.CopyToYXWZ(y : NativeTensor4<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SX * info.DX) * sa
        let yjY = nativeint (y.DY - y.SX * y.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SW * info.DW) * sa
        let yjX = nativeint (y.DX - y.SW * y.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SZ * info.DZ) * sa
        let yjW = nativeint (y.DW - y.SZ * y.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ) * sa
        let yjZ = nativeint (y.DZ) * sa
        let eY = xptr + sY
        while xptr <> eY do
            let eX = xptr + sX
            while xptr <> eX do
                let eW = xptr + sW
                while xptr <> eW do
                    let eZ = xptr + sZ
                    while xptr <> eZ do
                        NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
                        xptr <- xptr + xjZ
                        yptr <- yptr + yjZ
                    xptr <- xptr + xjW
                    yptr <- yptr + yjW
                xptr <- xptr + xjX
                yptr <- yptr + yjX
            xptr <- xptr + xjY
            yptr <- yptr + yjY
    member inline private x.CopyToYWXZ(y : NativeTensor4<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SW * info.DW) * sa
        let yjY = nativeint (y.DY - y.SW * y.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SX * info.DX) * sa
        let yjW = nativeint (y.DW - y.SX * y.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SZ * info.DZ) * sa
        let yjX = nativeint (y.DX - y.SZ * y.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ) * sa
        let yjZ = nativeint (y.DZ) * sa
        let eY = xptr + sY
        while xptr <> eY do
            let eW = xptr + sW
            while xptr <> eW do
                let eX = xptr + sX
                while xptr <> eX do
                    let eZ = xptr + sZ
                    while xptr <> eZ do
                        NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
                        xptr <- xptr + xjZ
                        yptr <- yptr + yjZ
                    xptr <- xptr + xjX
                    yptr <- yptr + yjX
                xptr <- xptr + xjW
                yptr <- yptr + yjW
            xptr <- xptr + xjY
            yptr <- yptr + yjY
    member inline private x.CopyToYWZX(y : NativeTensor4<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SW * info.DW) * sa
        let yjY = nativeint (y.DY - y.SW * y.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SZ * info.DZ) * sa
        let yjW = nativeint (y.DW - y.SZ * y.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SX * info.DX) * sa
        let yjZ = nativeint (y.DZ - y.SX * y.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX) * sa
        let yjX = nativeint (y.DX) * sa
        let eY = xptr + sY
        while xptr <> eY do
            let eW = xptr + sW
            while xptr <> eW do
                let eZ = xptr + sZ
                while xptr <> eZ do
                    let eX = xptr + sX
                    while xptr <> eX do
                        NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
                        xptr <- xptr + xjX
                        yptr <- yptr + yjX
                    xptr <- xptr + xjZ
                    yptr <- yptr + yjZ
                xptr <- xptr + xjW
                yptr <- yptr + yjW
            xptr <- xptr + xjY
            yptr <- yptr + yjY
    member inline private x.CopyToXWYZ(y : NativeTensor4<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SW * info.DW) * sa
        let yjX = nativeint (y.DX - y.SW * y.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SY * info.DY) * sa
        let yjW = nativeint (y.DW - y.SY * y.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SZ * info.DZ) * sa
        let yjY = nativeint (y.DY - y.SZ * y.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ) * sa
        let yjZ = nativeint (y.DZ) * sa
        let eX = xptr + sX
        while xptr <> eX do
            let eW = xptr + sW
            while xptr <> eW do
                let eY = xptr + sY
                while xptr <> eY do
                    let eZ = xptr + sZ
                    while xptr <> eZ do
                        NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
                        xptr <- xptr + xjZ
                        yptr <- yptr + yjZ
                    xptr <- xptr + xjY
                    yptr <- yptr + yjY
                xptr <- xptr + xjW
                yptr <- yptr + yjW
            xptr <- xptr + xjX
            yptr <- yptr + yjX
    member inline private x.CopyToWXYZ(y : NativeTensor4<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SX * info.DX) * sa
        let yjW = nativeint (y.DW - y.SX * y.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SY * info.DY) * sa
        let yjX = nativeint (y.DX - y.SY * y.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SZ * info.DZ) * sa
        let yjY = nativeint (y.DY - y.SZ * y.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ) * sa
        let yjZ = nativeint (y.DZ) * sa
        let eW = xptr + sW
        while xptr <> eW do
            let eX = xptr + sX
            while xptr <> eX do
                let eY = xptr + sY
                while xptr <> eY do
                    let eZ = xptr + sZ
                    while xptr <> eZ do
                        NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
                        xptr <- xptr + xjZ
                        yptr <- yptr + yjZ
                    xptr <- xptr + xjY
                    yptr <- yptr + yjY
                xptr <- xptr + xjX
                yptr <- yptr + yjX
            xptr <- xptr + xjW
            yptr <- yptr + yjW
    member inline private x.CopyToWYXZ(y : NativeTensor4<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SY * info.DY) * sa
        let yjW = nativeint (y.DW - y.SY * y.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SX * info.DX) * sa
        let yjY = nativeint (y.DY - y.SX * y.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SZ * info.DZ) * sa
        let yjX = nativeint (y.DX - y.SZ * y.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ) * sa
        let yjZ = nativeint (y.DZ) * sa
        let eW = xptr + sW
        while xptr <> eW do
            let eY = xptr + sY
            while xptr <> eY do
                let eX = xptr + sX
                while xptr <> eX do
                    let eZ = xptr + sZ
                    while xptr <> eZ do
                        NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
                        xptr <- xptr + xjZ
                        yptr <- yptr + yjZ
                    xptr <- xptr + xjX
                    yptr <- yptr + yjX
                xptr <- xptr + xjY
                yptr <- yptr + yjY
            xptr <- xptr + xjW
            yptr <- yptr + yjW
    member inline private x.CopyToWYZX(y : NativeTensor4<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SY * info.DY) * sa
        let yjW = nativeint (y.DW - y.SY * y.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SZ * info.DZ) * sa
        let yjY = nativeint (y.DY - y.SZ * y.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SX * info.DX) * sa
        let yjZ = nativeint (y.DZ - y.SX * y.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX) * sa
        let yjX = nativeint (y.DX) * sa
        let eW = xptr + sW
        while xptr <> eW do
            let eY = xptr + sY
            while xptr <> eY do
                let eZ = xptr + sZ
                while xptr <> eZ do
                    let eX = xptr + sX
                    while xptr <> eX do
                        NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
                        xptr <- xptr + xjX
                        yptr <- yptr + yjX
                    xptr <- xptr + xjZ
                    yptr <- yptr + yjZ
                xptr <- xptr + xjY
                yptr <- yptr + yjY
            xptr <- xptr + xjW
            yptr <- yptr + yjW
    member inline private x.CopyToXWZY(y : NativeTensor4<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SW * info.DW) * sa
        let yjX = nativeint (y.DX - y.SW * y.DW) * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SZ * info.DZ) * sa
        let yjW = nativeint (y.DW - y.SZ * y.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SY * info.DY) * sa
        let yjZ = nativeint (y.DZ - y.SY * y.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY) * sa
        let yjY = nativeint (y.DY) * sa
        let eX = xptr + sX
        while xptr <> eX do
            let eW = xptr + sW
            while xptr <> eW do
                let eZ = xptr + sZ
                while xptr <> eZ do
                    let eY = xptr + sY
                    while xptr <> eY do
                        NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
                        xptr <- xptr + xjY
                        yptr <- yptr + yjY
                    xptr <- xptr + xjZ
                    yptr <- yptr + yjZ
                xptr <- xptr + xjW
                yptr <- yptr + yjW
            xptr <- xptr + xjX
            yptr <- yptr + yjX
    member inline private x.CopyToWXZY(y : NativeTensor4<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SX * info.DX) * sa
        let yjW = nativeint (y.DW - y.SX * y.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SZ * info.DZ) * sa
        let yjX = nativeint (y.DX - y.SZ * y.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SY * info.DY) * sa
        let yjZ = nativeint (y.DZ - y.SY * y.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY) * sa
        let yjY = nativeint (y.DY) * sa
        let eW = xptr + sW
        while xptr <> eW do
            let eX = xptr + sX
            while xptr <> eX do
                let eZ = xptr + sZ
                while xptr <> eZ do
                    let eY = xptr + sY
                    while xptr <> eY do
                        NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
                        xptr <- xptr + xjY
                        yptr <- yptr + yjY
                    xptr <- xptr + xjZ
                    yptr <- yptr + yjZ
                xptr <- xptr + xjX
                yptr <- yptr + yjX
            xptr <- xptr + xjW
            yptr <- yptr + yjW
    member inline private x.CopyToWZXY(y : NativeTensor4<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SZ * info.DZ) * sa
        let yjW = nativeint (y.DW - y.SZ * y.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SX * info.DX) * sa
        let yjZ = nativeint (y.DZ - y.SX * y.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SY * info.DY) * sa
        let yjX = nativeint (y.DX - y.SY * y.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY) * sa
        let yjY = nativeint (y.DY) * sa
        let eW = xptr + sW
        while xptr <> eW do
            let eZ = xptr + sZ
            while xptr <> eZ do
                let eX = xptr + sX
                while xptr <> eX do
                    let eY = xptr + sY
                    while xptr <> eY do
                        NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
                        xptr <- xptr + xjY
                        yptr <- yptr + yjY
                    xptr <- xptr + xjX
                    yptr <- yptr + yjX
                xptr <- xptr + xjZ
                yptr <- yptr + yjZ
            xptr <- xptr + xjW
            yptr <- yptr + yjW
    member inline private x.CopyToWZYX(y : NativeTensor4<'a>) = 
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sa
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SZ * info.DZ) * sa
        let yjW = nativeint (y.DW - y.SZ * y.DZ) * sa
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SY * info.DY) * sa
        let yjZ = nativeint (y.DZ - y.SY * y.DY) * sa
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SX * info.DX) * sa
        let yjY = nativeint (y.DY - y.SX * y.DX) * sa
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX) * sa
        let yjX = nativeint (y.DX) * sa
        let eW = xptr + sW
        while xptr <> eW do
            let eZ = xptr + sZ
            while xptr <> eZ do
                let eY = xptr + sY
                while xptr <> eY do
                    let eX = xptr + sX
                    while xptr <> eX do
                        NativePtr.write (NativePtr.ofNativeInt<'a> yptr) (NativePtr.read (NativePtr.ofNativeInt<'a> xptr))
                        xptr <- xptr + xjX
                        yptr <- yptr + yjX
                    xptr <- xptr + xjY
                    yptr <- yptr + yjY
                xptr <- xptr + xjZ
                yptr <- yptr + yjZ
            xptr <- xptr + xjW
            yptr <- yptr + yjW
    member x.CopyTo(y : NativeTensor4<'a>) = 
        if info.Size <> y.Size then
            failwithf "NativeTensor4 size mismatch: { src = %A; dst = %A }" info.Size y.Size
        let cXW = compare (abs info.DX) (abs info.DW)
        let cXY = compare (abs info.DX) (abs info.DY)
        let cXZ = compare (abs info.DX) (abs info.DZ)
        let cYW = compare (abs info.DY) (abs info.DW)
        let cYZ = compare (abs info.DY) (abs info.DZ)
        let cZW = compare (abs info.DZ) (abs info.DW)
        if cXW >= 0  && cXY >= 0  && cXZ >= 0  && cYW >= 0  && cYZ >= 0  && cZW >= 0  then x.CopyToXYZW(y)
        elif cXW >= 0  && cXY <= 0 && cXZ >= 0  && cYW >= 0  && cYZ >= 0  && cZW >= 0  then x.CopyToYXZW(y)
        elif cXW >= 0  && cXY <= 0 && cXZ <= 0 && cYW >= 0  && cYZ >= 0  && cZW >= 0  then x.CopyToYZXW(y)
        elif cXW <= 0 && cXY <= 0 && cXZ <= 0 && cYW >= 0  && cYZ >= 0  && cZW >= 0  then x.CopyToYZWX(y)
        elif cXW >= 0  && cXY >= 0  && cXZ >= 0  && cYW >= 0  && cYZ <= 0 && cZW >= 0  then x.CopyToXZYW(y)
        elif cXW >= 0  && cXY >= 0  && cXZ <= 0 && cYW >= 0  && cYZ <= 0 && cZW >= 0  then x.CopyToZXYW(y)
        elif cXW >= 0  && cXY <= 0 && cXZ <= 0 && cYW >= 0  && cYZ <= 0 && cZW >= 0  then x.CopyToZYXW(y)
        elif cXW <= 0 && cXY <= 0 && cXZ <= 0 && cYW >= 0  && cYZ <= 0 && cZW >= 0  then x.CopyToZYWX(y)
        elif cXW >= 0  && cXY >= 0  && cXZ >= 0  && cYW <= 0 && cYZ <= 0 && cZW >= 0  then x.CopyToXZWY(y)
        elif cXW >= 0  && cXY >= 0  && cXZ <= 0 && cYW <= 0 && cYZ <= 0 && cZW >= 0  then x.CopyToZXWY(y)
        elif cXW <= 0 && cXY >= 0  && cXZ <= 0 && cYW <= 0 && cYZ <= 0 && cZW >= 0  then x.CopyToZWXY(y)
        elif cXW <= 0 && cXY <= 0 && cXZ <= 0 && cYW <= 0 && cYZ <= 0 && cZW >= 0  then x.CopyToZWYX(y)
        elif cXW >= 0  && cXY >= 0  && cXZ >= 0  && cYW >= 0  && cYZ >= 0  && cZW <= 0 then x.CopyToXYWZ(y)
        elif cXW >= 0  && cXY <= 0 && cXZ >= 0  && cYW >= 0  && cYZ >= 0  && cZW <= 0 then x.CopyToYXWZ(y)
        elif cXW <= 0 && cXY <= 0 && cXZ >= 0  && cYW >= 0  && cYZ >= 0  && cZW <= 0 then x.CopyToYWXZ(y)
        elif cXW <= 0 && cXY <= 0 && cXZ <= 0 && cYW >= 0  && cYZ >= 0  && cZW <= 0 then x.CopyToYWZX(y)
        elif cXW >= 0  && cXY >= 0  && cXZ >= 0  && cYW <= 0 && cYZ >= 0  && cZW <= 0 then x.CopyToXWYZ(y)
        elif cXW <= 0 && cXY >= 0  && cXZ >= 0  && cYW <= 0 && cYZ >= 0  && cZW <= 0 then x.CopyToWXYZ(y)
        elif cXW <= 0 && cXY <= 0 && cXZ >= 0  && cYW <= 0 && cYZ >= 0  && cZW <= 0 then x.CopyToWYXZ(y)
        elif cXW <= 0 && cXY <= 0 && cXZ <= 0 && cYW <= 0 && cYZ >= 0  && cZW <= 0 then x.CopyToWYZX(y)
        elif cXW >= 0  && cXY >= 0  && cXZ >= 0  && cYW <= 0 && cYZ <= 0 && cZW <= 0 then x.CopyToXWZY(y)
        elif cXW <= 0 && cXY >= 0  && cXZ >= 0  && cYW <= 0 && cYZ <= 0 && cZW <= 0 then x.CopyToWXZY(y)
        elif cXW <= 0 && cXY >= 0  && cXZ <= 0 && cYW <= 0 && cYZ <= 0 && cZW <= 0 then x.CopyToWZXY(y)
        else x.CopyToWZYX(y)
    member inline private x.CopyToXYZW<'b when 'b : unmanaged>(y : NativeTensor4<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SY * info.DY) * sa
        let yjX = nativeint (y.DX - y.SY * y.DY) * sb
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SZ * info.DZ) * sa
        let yjY = nativeint (y.DY - y.SZ * y.DZ) * sb
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SW * info.DW) * sa
        let yjZ = nativeint (y.DZ - y.SW * y.DW) * sb
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW) * sa
        let yjW = nativeint (y.DW) * sb
        let eX = xptr + sX
        while xptr <> eX do
            let eY = xptr + sY
            while xptr <> eY do
                let eZ = xptr + sZ
                while xptr <> eZ do
                    let eW = xptr + sW
                    while xptr <> eW do
                        NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
                        xptr <- xptr + xjW
                        yptr <- yptr + yjW
                    xptr <- xptr + xjZ
                    yptr <- yptr + yjZ
                xptr <- xptr + xjY
                yptr <- yptr + yjY
            xptr <- xptr + xjX
            yptr <- yptr + yjX
    member inline private x.CopyToYXZW<'b when 'b : unmanaged>(y : NativeTensor4<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SX * info.DX) * sa
        let yjY = nativeint (y.DY - y.SX * y.DX) * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SZ * info.DZ) * sa
        let yjX = nativeint (y.DX - y.SZ * y.DZ) * sb
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SW * info.DW) * sa
        let yjZ = nativeint (y.DZ - y.SW * y.DW) * sb
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW) * sa
        let yjW = nativeint (y.DW) * sb
        let eY = xptr + sY
        while xptr <> eY do
            let eX = xptr + sX
            while xptr <> eX do
                let eZ = xptr + sZ
                while xptr <> eZ do
                    let eW = xptr + sW
                    while xptr <> eW do
                        NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
                        xptr <- xptr + xjW
                        yptr <- yptr + yjW
                    xptr <- xptr + xjZ
                    yptr <- yptr + yjZ
                xptr <- xptr + xjX
                yptr <- yptr + yjX
            xptr <- xptr + xjY
            yptr <- yptr + yjY
    member inline private x.CopyToYZXW<'b when 'b : unmanaged>(y : NativeTensor4<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SZ * info.DZ) * sa
        let yjY = nativeint (y.DY - y.SZ * y.DZ) * sb
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SX * info.DX) * sa
        let yjZ = nativeint (y.DZ - y.SX * y.DX) * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SW * info.DW) * sa
        let yjX = nativeint (y.DX - y.SW * y.DW) * sb
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW) * sa
        let yjW = nativeint (y.DW) * sb
        let eY = xptr + sY
        while xptr <> eY do
            let eZ = xptr + sZ
            while xptr <> eZ do
                let eX = xptr + sX
                while xptr <> eX do
                    let eW = xptr + sW
                    while xptr <> eW do
                        NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
                        xptr <- xptr + xjW
                        yptr <- yptr + yjW
                    xptr <- xptr + xjX
                    yptr <- yptr + yjX
                xptr <- xptr + xjZ
                yptr <- yptr + yjZ
            xptr <- xptr + xjY
            yptr <- yptr + yjY
    member inline private x.CopyToYZWX<'b when 'b : unmanaged>(y : NativeTensor4<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SZ * info.DZ) * sa
        let yjY = nativeint (y.DY - y.SZ * y.DZ) * sb
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SW * info.DW) * sa
        let yjZ = nativeint (y.DZ - y.SW * y.DW) * sb
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SX * info.DX) * sa
        let yjW = nativeint (y.DW - y.SX * y.DX) * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX) * sa
        let yjX = nativeint (y.DX) * sb
        let eY = xptr + sY
        while xptr <> eY do
            let eZ = xptr + sZ
            while xptr <> eZ do
                let eW = xptr + sW
                while xptr <> eW do
                    let eX = xptr + sX
                    while xptr <> eX do
                        NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
                        xptr <- xptr + xjX
                        yptr <- yptr + yjX
                    xptr <- xptr + xjW
                    yptr <- yptr + yjW
                xptr <- xptr + xjZ
                yptr <- yptr + yjZ
            xptr <- xptr + xjY
            yptr <- yptr + yjY
    member inline private x.CopyToXZYW<'b when 'b : unmanaged>(y : NativeTensor4<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SZ * info.DZ) * sa
        let yjX = nativeint (y.DX - y.SZ * y.DZ) * sb
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SY * info.DY) * sa
        let yjZ = nativeint (y.DZ - y.SY * y.DY) * sb
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SW * info.DW) * sa
        let yjY = nativeint (y.DY - y.SW * y.DW) * sb
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW) * sa
        let yjW = nativeint (y.DW) * sb
        let eX = xptr + sX
        while xptr <> eX do
            let eZ = xptr + sZ
            while xptr <> eZ do
                let eY = xptr + sY
                while xptr <> eY do
                    let eW = xptr + sW
                    while xptr <> eW do
                        NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
                        xptr <- xptr + xjW
                        yptr <- yptr + yjW
                    xptr <- xptr + xjY
                    yptr <- yptr + yjY
                xptr <- xptr + xjZ
                yptr <- yptr + yjZ
            xptr <- xptr + xjX
            yptr <- yptr + yjX
    member inline private x.CopyToZXYW<'b when 'b : unmanaged>(y : NativeTensor4<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SX * info.DX) * sa
        let yjZ = nativeint (y.DZ - y.SX * y.DX) * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SY * info.DY) * sa
        let yjX = nativeint (y.DX - y.SY * y.DY) * sb
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SW * info.DW) * sa
        let yjY = nativeint (y.DY - y.SW * y.DW) * sb
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW) * sa
        let yjW = nativeint (y.DW) * sb
        let eZ = xptr + sZ
        while xptr <> eZ do
            let eX = xptr + sX
            while xptr <> eX do
                let eY = xptr + sY
                while xptr <> eY do
                    let eW = xptr + sW
                    while xptr <> eW do
                        NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
                        xptr <- xptr + xjW
                        yptr <- yptr + yjW
                    xptr <- xptr + xjY
                    yptr <- yptr + yjY
                xptr <- xptr + xjX
                yptr <- yptr + yjX
            xptr <- xptr + xjZ
            yptr <- yptr + yjZ
    member inline private x.CopyToZYXW<'b when 'b : unmanaged>(y : NativeTensor4<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SY * info.DY) * sa
        let yjZ = nativeint (y.DZ - y.SY * y.DY) * sb
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SX * info.DX) * sa
        let yjY = nativeint (y.DY - y.SX * y.DX) * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SW * info.DW) * sa
        let yjX = nativeint (y.DX - y.SW * y.DW) * sb
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW) * sa
        let yjW = nativeint (y.DW) * sb
        let eZ = xptr + sZ
        while xptr <> eZ do
            let eY = xptr + sY
            while xptr <> eY do
                let eX = xptr + sX
                while xptr <> eX do
                    let eW = xptr + sW
                    while xptr <> eW do
                        NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
                        xptr <- xptr + xjW
                        yptr <- yptr + yjW
                    xptr <- xptr + xjX
                    yptr <- yptr + yjX
                xptr <- xptr + xjY
                yptr <- yptr + yjY
            xptr <- xptr + xjZ
            yptr <- yptr + yjZ
    member inline private x.CopyToZYWX<'b when 'b : unmanaged>(y : NativeTensor4<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SY * info.DY) * sa
        let yjZ = nativeint (y.DZ - y.SY * y.DY) * sb
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SW * info.DW) * sa
        let yjY = nativeint (y.DY - y.SW * y.DW) * sb
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SX * info.DX) * sa
        let yjW = nativeint (y.DW - y.SX * y.DX) * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX) * sa
        let yjX = nativeint (y.DX) * sb
        let eZ = xptr + sZ
        while xptr <> eZ do
            let eY = xptr + sY
            while xptr <> eY do
                let eW = xptr + sW
                while xptr <> eW do
                    let eX = xptr + sX
                    while xptr <> eX do
                        NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
                        xptr <- xptr + xjX
                        yptr <- yptr + yjX
                    xptr <- xptr + xjW
                    yptr <- yptr + yjW
                xptr <- xptr + xjY
                yptr <- yptr + yjY
            xptr <- xptr + xjZ
            yptr <- yptr + yjZ
    member inline private x.CopyToXZWY<'b when 'b : unmanaged>(y : NativeTensor4<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SZ * info.DZ) * sa
        let yjX = nativeint (y.DX - y.SZ * y.DZ) * sb
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SW * info.DW) * sa
        let yjZ = nativeint (y.DZ - y.SW * y.DW) * sb
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SY * info.DY) * sa
        let yjW = nativeint (y.DW - y.SY * y.DY) * sb
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY) * sa
        let yjY = nativeint (y.DY) * sb
        let eX = xptr + sX
        while xptr <> eX do
            let eZ = xptr + sZ
            while xptr <> eZ do
                let eW = xptr + sW
                while xptr <> eW do
                    let eY = xptr + sY
                    while xptr <> eY do
                        NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
                        xptr <- xptr + xjY
                        yptr <- yptr + yjY
                    xptr <- xptr + xjW
                    yptr <- yptr + yjW
                xptr <- xptr + xjZ
                yptr <- yptr + yjZ
            xptr <- xptr + xjX
            yptr <- yptr + yjX
    member inline private x.CopyToZXWY<'b when 'b : unmanaged>(y : NativeTensor4<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SX * info.DX) * sa
        let yjZ = nativeint (y.DZ - y.SX * y.DX) * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SW * info.DW) * sa
        let yjX = nativeint (y.DX - y.SW * y.DW) * sb
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SY * info.DY) * sa
        let yjW = nativeint (y.DW - y.SY * y.DY) * sb
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY) * sa
        let yjY = nativeint (y.DY) * sb
        let eZ = xptr + sZ
        while xptr <> eZ do
            let eX = xptr + sX
            while xptr <> eX do
                let eW = xptr + sW
                while xptr <> eW do
                    let eY = xptr + sY
                    while xptr <> eY do
                        NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
                        xptr <- xptr + xjY
                        yptr <- yptr + yjY
                    xptr <- xptr + xjW
                    yptr <- yptr + yjW
                xptr <- xptr + xjX
                yptr <- yptr + yjX
            xptr <- xptr + xjZ
            yptr <- yptr + yjZ
    member inline private x.CopyToZWXY<'b when 'b : unmanaged>(y : NativeTensor4<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SW * info.DW) * sa
        let yjZ = nativeint (y.DZ - y.SW * y.DW) * sb
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SX * info.DX) * sa
        let yjW = nativeint (y.DW - y.SX * y.DX) * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SY * info.DY) * sa
        let yjX = nativeint (y.DX - y.SY * y.DY) * sb
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY) * sa
        let yjY = nativeint (y.DY) * sb
        let eZ = xptr + sZ
        while xptr <> eZ do
            let eW = xptr + sW
            while xptr <> eW do
                let eX = xptr + sX
                while xptr <> eX do
                    let eY = xptr + sY
                    while xptr <> eY do
                        NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
                        xptr <- xptr + xjY
                        yptr <- yptr + yjY
                    xptr <- xptr + xjX
                    yptr <- yptr + yjX
                xptr <- xptr + xjW
                yptr <- yptr + yjW
            xptr <- xptr + xjZ
            yptr <- yptr + yjZ
    member inline private x.CopyToZWYX<'b when 'b : unmanaged>(y : NativeTensor4<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SW * info.DW) * sa
        let yjZ = nativeint (y.DZ - y.SW * y.DW) * sb
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SY * info.DY) * sa
        let yjW = nativeint (y.DW - y.SY * y.DY) * sb
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SX * info.DX) * sa
        let yjY = nativeint (y.DY - y.SX * y.DX) * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX) * sa
        let yjX = nativeint (y.DX) * sb
        let eZ = xptr + sZ
        while xptr <> eZ do
            let eW = xptr + sW
            while xptr <> eW do
                let eY = xptr + sY
                while xptr <> eY do
                    let eX = xptr + sX
                    while xptr <> eX do
                        NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
                        xptr <- xptr + xjX
                        yptr <- yptr + yjX
                    xptr <- xptr + xjY
                    yptr <- yptr + yjY
                xptr <- xptr + xjW
                yptr <- yptr + yjW
            xptr <- xptr + xjZ
            yptr <- yptr + yjZ
    member inline private x.CopyToXYWZ<'b when 'b : unmanaged>(y : NativeTensor4<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SY * info.DY) * sa
        let yjX = nativeint (y.DX - y.SY * y.DY) * sb
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SW * info.DW) * sa
        let yjY = nativeint (y.DY - y.SW * y.DW) * sb
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SZ * info.DZ) * sa
        let yjW = nativeint (y.DW - y.SZ * y.DZ) * sb
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ) * sa
        let yjZ = nativeint (y.DZ) * sb
        let eX = xptr + sX
        while xptr <> eX do
            let eY = xptr + sY
            while xptr <> eY do
                let eW = xptr + sW
                while xptr <> eW do
                    let eZ = xptr + sZ
                    while xptr <> eZ do
                        NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
                        xptr <- xptr + xjZ
                        yptr <- yptr + yjZ
                    xptr <- xptr + xjW
                    yptr <- yptr + yjW
                xptr <- xptr + xjY
                yptr <- yptr + yjY
            xptr <- xptr + xjX
            yptr <- yptr + yjX
    member inline private x.CopyToYXWZ<'b when 'b : unmanaged>(y : NativeTensor4<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SX * info.DX) * sa
        let yjY = nativeint (y.DY - y.SX * y.DX) * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SW * info.DW) * sa
        let yjX = nativeint (y.DX - y.SW * y.DW) * sb
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SZ * info.DZ) * sa
        let yjW = nativeint (y.DW - y.SZ * y.DZ) * sb
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ) * sa
        let yjZ = nativeint (y.DZ) * sb
        let eY = xptr + sY
        while xptr <> eY do
            let eX = xptr + sX
            while xptr <> eX do
                let eW = xptr + sW
                while xptr <> eW do
                    let eZ = xptr + sZ
                    while xptr <> eZ do
                        NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
                        xptr <- xptr + xjZ
                        yptr <- yptr + yjZ
                    xptr <- xptr + xjW
                    yptr <- yptr + yjW
                xptr <- xptr + xjX
                yptr <- yptr + yjX
            xptr <- xptr + xjY
            yptr <- yptr + yjY
    member inline private x.CopyToYWXZ<'b when 'b : unmanaged>(y : NativeTensor4<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SW * info.DW) * sa
        let yjY = nativeint (y.DY - y.SW * y.DW) * sb
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SX * info.DX) * sa
        let yjW = nativeint (y.DW - y.SX * y.DX) * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SZ * info.DZ) * sa
        let yjX = nativeint (y.DX - y.SZ * y.DZ) * sb
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ) * sa
        let yjZ = nativeint (y.DZ) * sb
        let eY = xptr + sY
        while xptr <> eY do
            let eW = xptr + sW
            while xptr <> eW do
                let eX = xptr + sX
                while xptr <> eX do
                    let eZ = xptr + sZ
                    while xptr <> eZ do
                        NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
                        xptr <- xptr + xjZ
                        yptr <- yptr + yjZ
                    xptr <- xptr + xjX
                    yptr <- yptr + yjX
                xptr <- xptr + xjW
                yptr <- yptr + yjW
            xptr <- xptr + xjY
            yptr <- yptr + yjY
    member inline private x.CopyToYWZX<'b when 'b : unmanaged>(y : NativeTensor4<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SW * info.DW) * sa
        let yjY = nativeint (y.DY - y.SW * y.DW) * sb
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SZ * info.DZ) * sa
        let yjW = nativeint (y.DW - y.SZ * y.DZ) * sb
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SX * info.DX) * sa
        let yjZ = nativeint (y.DZ - y.SX * y.DX) * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX) * sa
        let yjX = nativeint (y.DX) * sb
        let eY = xptr + sY
        while xptr <> eY do
            let eW = xptr + sW
            while xptr <> eW do
                let eZ = xptr + sZ
                while xptr <> eZ do
                    let eX = xptr + sX
                    while xptr <> eX do
                        NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
                        xptr <- xptr + xjX
                        yptr <- yptr + yjX
                    xptr <- xptr + xjZ
                    yptr <- yptr + yjZ
                xptr <- xptr + xjW
                yptr <- yptr + yjW
            xptr <- xptr + xjY
            yptr <- yptr + yjY
    member inline private x.CopyToXWYZ<'b when 'b : unmanaged>(y : NativeTensor4<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SW * info.DW) * sa
        let yjX = nativeint (y.DX - y.SW * y.DW) * sb
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SY * info.DY) * sa
        let yjW = nativeint (y.DW - y.SY * y.DY) * sb
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SZ * info.DZ) * sa
        let yjY = nativeint (y.DY - y.SZ * y.DZ) * sb
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ) * sa
        let yjZ = nativeint (y.DZ) * sb
        let eX = xptr + sX
        while xptr <> eX do
            let eW = xptr + sW
            while xptr <> eW do
                let eY = xptr + sY
                while xptr <> eY do
                    let eZ = xptr + sZ
                    while xptr <> eZ do
                        NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
                        xptr <- xptr + xjZ
                        yptr <- yptr + yjZ
                    xptr <- xptr + xjY
                    yptr <- yptr + yjY
                xptr <- xptr + xjW
                yptr <- yptr + yjW
            xptr <- xptr + xjX
            yptr <- yptr + yjX
    member inline private x.CopyToWXYZ<'b when 'b : unmanaged>(y : NativeTensor4<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SX * info.DX) * sa
        let yjW = nativeint (y.DW - y.SX * y.DX) * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SY * info.DY) * sa
        let yjX = nativeint (y.DX - y.SY * y.DY) * sb
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SZ * info.DZ) * sa
        let yjY = nativeint (y.DY - y.SZ * y.DZ) * sb
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ) * sa
        let yjZ = nativeint (y.DZ) * sb
        let eW = xptr + sW
        while xptr <> eW do
            let eX = xptr + sX
            while xptr <> eX do
                let eY = xptr + sY
                while xptr <> eY do
                    let eZ = xptr + sZ
                    while xptr <> eZ do
                        NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
                        xptr <- xptr + xjZ
                        yptr <- yptr + yjZ
                    xptr <- xptr + xjY
                    yptr <- yptr + yjY
                xptr <- xptr + xjX
                yptr <- yptr + yjX
            xptr <- xptr + xjW
            yptr <- yptr + yjW
    member inline private x.CopyToWYXZ<'b when 'b : unmanaged>(y : NativeTensor4<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SY * info.DY) * sa
        let yjW = nativeint (y.DW - y.SY * y.DY) * sb
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SX * info.DX) * sa
        let yjY = nativeint (y.DY - y.SX * y.DX) * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SZ * info.DZ) * sa
        let yjX = nativeint (y.DX - y.SZ * y.DZ) * sb
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ) * sa
        let yjZ = nativeint (y.DZ) * sb
        let eW = xptr + sW
        while xptr <> eW do
            let eY = xptr + sY
            while xptr <> eY do
                let eX = xptr + sX
                while xptr <> eX do
                    let eZ = xptr + sZ
                    while xptr <> eZ do
                        NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
                        xptr <- xptr + xjZ
                        yptr <- yptr + yjZ
                    xptr <- xptr + xjX
                    yptr <- yptr + yjX
                xptr <- xptr + xjY
                yptr <- yptr + yjY
            xptr <- xptr + xjW
            yptr <- yptr + yjW
    member inline private x.CopyToWYZX<'b when 'b : unmanaged>(y : NativeTensor4<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SY * info.DY) * sa
        let yjW = nativeint (y.DW - y.SY * y.DY) * sb
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SZ * info.DZ) * sa
        let yjY = nativeint (y.DY - y.SZ * y.DZ) * sb
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SX * info.DX) * sa
        let yjZ = nativeint (y.DZ - y.SX * y.DX) * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX) * sa
        let yjX = nativeint (y.DX) * sb
        let eW = xptr + sW
        while xptr <> eW do
            let eY = xptr + sY
            while xptr <> eY do
                let eZ = xptr + sZ
                while xptr <> eZ do
                    let eX = xptr + sX
                    while xptr <> eX do
                        NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
                        xptr <- xptr + xjX
                        yptr <- yptr + yjX
                    xptr <- xptr + xjZ
                    yptr <- yptr + yjZ
                xptr <- xptr + xjY
                yptr <- yptr + yjY
            xptr <- xptr + xjW
            yptr <- yptr + yjW
    member inline private x.CopyToXWZY<'b when 'b : unmanaged>(y : NativeTensor4<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SW * info.DW) * sa
        let yjX = nativeint (y.DX - y.SW * y.DW) * sb
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SZ * info.DZ) * sa
        let yjW = nativeint (y.DW - y.SZ * y.DZ) * sb
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SY * info.DY) * sa
        let yjZ = nativeint (y.DZ - y.SY * y.DY) * sb
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY) * sa
        let yjY = nativeint (y.DY) * sb
        let eX = xptr + sX
        while xptr <> eX do
            let eW = xptr + sW
            while xptr <> eW do
                let eZ = xptr + sZ
                while xptr <> eZ do
                    let eY = xptr + sY
                    while xptr <> eY do
                        NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
                        xptr <- xptr + xjY
                        yptr <- yptr + yjY
                    xptr <- xptr + xjZ
                    yptr <- yptr + yjZ
                xptr <- xptr + xjW
                yptr <- yptr + yjW
            xptr <- xptr + xjX
            yptr <- yptr + yjX
    member inline private x.CopyToWXZY<'b when 'b : unmanaged>(y : NativeTensor4<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SX * info.DX) * sa
        let yjW = nativeint (y.DW - y.SX * y.DX) * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SZ * info.DZ) * sa
        let yjX = nativeint (y.DX - y.SZ * y.DZ) * sb
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SY * info.DY) * sa
        let yjZ = nativeint (y.DZ - y.SY * y.DY) * sb
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY) * sa
        let yjY = nativeint (y.DY) * sb
        let eW = xptr + sW
        while xptr <> eW do
            let eX = xptr + sX
            while xptr <> eX do
                let eZ = xptr + sZ
                while xptr <> eZ do
                    let eY = xptr + sY
                    while xptr <> eY do
                        NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
                        xptr <- xptr + xjY
                        yptr <- yptr + yjY
                    xptr <- xptr + xjZ
                    yptr <- yptr + yjZ
                xptr <- xptr + xjX
                yptr <- yptr + yjX
            xptr <- xptr + xjW
            yptr <- yptr + yjW
    member inline private x.CopyToWZXY<'b when 'b : unmanaged>(y : NativeTensor4<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SZ * info.DZ) * sa
        let yjW = nativeint (y.DW - y.SZ * y.DZ) * sb
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SX * info.DX) * sa
        let yjZ = nativeint (y.DZ - y.SX * y.DX) * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX - info.SY * info.DY) * sa
        let yjX = nativeint (y.DX - y.SY * y.DY) * sb
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY) * sa
        let yjY = nativeint (y.DY) * sb
        let eW = xptr + sW
        while xptr <> eW do
            let eZ = xptr + sZ
            while xptr <> eZ do
                let eX = xptr + sX
                while xptr <> eX do
                    let eY = xptr + sY
                    while xptr <> eY do
                        NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
                        xptr <- xptr + xjY
                        yptr <- yptr + yjY
                    xptr <- xptr + xjX
                    yptr <- yptr + yjX
                xptr <- xptr + xjZ
                yptr <- yptr + yjZ
            xptr <- xptr + xjW
            yptr <- yptr + yjW
    member inline private x.CopyToWZYX<'b when 'b : unmanaged>(y : NativeTensor4<'b>, f : 'a -> 'b) = 
        let sb = nativeint (sizeof<'b>)
        let sa = nativeint (sizeof<'a>)
        let mutable xptr = ptr |> NativePtr.toNativeInt
        xptr <- xptr + nativeint info.Origin * sa
        let mutable yptr = y.Pointer |> NativePtr.toNativeInt
        yptr <- yptr + nativeint y.Info.Origin * sb
        let sW = nativeint (info.SW * info.DW) * sa
        let xjW = nativeint (info.DW - info.SZ * info.DZ) * sa
        let yjW = nativeint (y.DW - y.SZ * y.DZ) * sb
        let sZ = nativeint (info.SZ * info.DZ) * sa
        let xjZ = nativeint (info.DZ - info.SY * info.DY) * sa
        let yjZ = nativeint (y.DZ - y.SY * y.DY) * sb
        let sY = nativeint (info.SY * info.DY) * sa
        let xjY = nativeint (info.DY - info.SX * info.DX) * sa
        let yjY = nativeint (y.DY - y.SX * y.DX) * sb
        let sX = nativeint (info.SX * info.DX) * sa
        let xjX = nativeint (info.DX) * sa
        let yjX = nativeint (y.DX) * sb
        let eW = xptr + sW
        while xptr <> eW do
            let eZ = xptr + sZ
            while xptr <> eZ do
                let eY = xptr + sY
                while xptr <> eY do
                    let eX = xptr + sX
                    while xptr <> eX do
                        NativePtr.write (NativePtr.ofNativeInt<'b> yptr) (f (NativePtr.read (NativePtr.ofNativeInt<'a> xptr)))
                        xptr <- xptr + xjX
                        yptr <- yptr + yjX
                    xptr <- xptr + xjY
                    yptr <- yptr + yjY
                xptr <- xptr + xjZ
                yptr <- yptr + yjZ
            xptr <- xptr + xjW
            yptr <- yptr + yjW
    member x.CopyTo(y : NativeTensor4<'b>, f : 'a -> 'b) = 
        if info.Size <> y.Size then
            failwithf "NativeTensor4 size mismatch: { src = %A; dst = %A }" info.Size y.Size
        let cXW = compare (abs info.DX) (abs info.DW)
        let cXY = compare (abs info.DX) (abs info.DY)
        let cXZ = compare (abs info.DX) (abs info.DZ)
        let cYW = compare (abs info.DY) (abs info.DW)
        let cYZ = compare (abs info.DY) (abs info.DZ)
        let cZW = compare (abs info.DZ) (abs info.DW)
        if cXW >= 0  && cXY >= 0  && cXZ >= 0  && cYW >= 0  && cYZ >= 0  && cZW >= 0  then x.CopyToXYZW(y, f)
        elif cXW >= 0  && cXY <= 0 && cXZ >= 0  && cYW >= 0  && cYZ >= 0  && cZW >= 0  then x.CopyToYXZW(y, f)
        elif cXW >= 0  && cXY <= 0 && cXZ <= 0 && cYW >= 0  && cYZ >= 0  && cZW >= 0  then x.CopyToYZXW(y, f)
        elif cXW <= 0 && cXY <= 0 && cXZ <= 0 && cYW >= 0  && cYZ >= 0  && cZW >= 0  then x.CopyToYZWX(y, f)
        elif cXW >= 0  && cXY >= 0  && cXZ >= 0  && cYW >= 0  && cYZ <= 0 && cZW >= 0  then x.CopyToXZYW(y, f)
        elif cXW >= 0  && cXY >= 0  && cXZ <= 0 && cYW >= 0  && cYZ <= 0 && cZW >= 0  then x.CopyToZXYW(y, f)
        elif cXW >= 0  && cXY <= 0 && cXZ <= 0 && cYW >= 0  && cYZ <= 0 && cZW >= 0  then x.CopyToZYXW(y, f)
        elif cXW <= 0 && cXY <= 0 && cXZ <= 0 && cYW >= 0  && cYZ <= 0 && cZW >= 0  then x.CopyToZYWX(y, f)
        elif cXW >= 0  && cXY >= 0  && cXZ >= 0  && cYW <= 0 && cYZ <= 0 && cZW >= 0  then x.CopyToXZWY(y, f)
        elif cXW >= 0  && cXY >= 0  && cXZ <= 0 && cYW <= 0 && cYZ <= 0 && cZW >= 0  then x.CopyToZXWY(y, f)
        elif cXW <= 0 && cXY >= 0  && cXZ <= 0 && cYW <= 0 && cYZ <= 0 && cZW >= 0  then x.CopyToZWXY(y, f)
        elif cXW <= 0 && cXY <= 0 && cXZ <= 0 && cYW <= 0 && cYZ <= 0 && cZW >= 0  then x.CopyToZWYX(y, f)
        elif cXW >= 0  && cXY >= 0  && cXZ >= 0  && cYW >= 0  && cYZ >= 0  && cZW <= 0 then x.CopyToXYWZ(y, f)
        elif cXW >= 0  && cXY <= 0 && cXZ >= 0  && cYW >= 0  && cYZ >= 0  && cZW <= 0 then x.CopyToYXWZ(y, f)
        elif cXW <= 0 && cXY <= 0 && cXZ >= 0  && cYW >= 0  && cYZ >= 0  && cZW <= 0 then x.CopyToYWXZ(y, f)
        elif cXW <= 0 && cXY <= 0 && cXZ <= 0 && cYW >= 0  && cYZ >= 0  && cZW <= 0 then x.CopyToYWZX(y, f)
        elif cXW >= 0  && cXY >= 0  && cXZ >= 0  && cYW <= 0 && cYZ >= 0  && cZW <= 0 then x.CopyToXWYZ(y, f)
        elif cXW <= 0 && cXY >= 0  && cXZ >= 0  && cYW <= 0 && cYZ >= 0  && cZW <= 0 then x.CopyToWXYZ(y, f)
        elif cXW <= 0 && cXY <= 0 && cXZ >= 0  && cYW <= 0 && cYZ >= 0  && cZW <= 0 then x.CopyToWYXZ(y, f)
        elif cXW <= 0 && cXY <= 0 && cXZ <= 0 && cYW <= 0 && cYZ >= 0  && cZW <= 0 then x.CopyToWYZX(y, f)
        elif cXW >= 0  && cXY >= 0  && cXZ >= 0  && cYW <= 0 && cYZ <= 0 && cZW <= 0 then x.CopyToXWZY(y, f)
        elif cXW <= 0 && cXY >= 0  && cXZ >= 0  && cYW <= 0 && cYZ <= 0 && cZW <= 0 then x.CopyToWXZY(y, f)
        elif cXW <= 0 && cXY >= 0  && cXZ <= 0 && cYW <= 0 && cYZ <= 0 && cZW <= 0 then x.CopyToWZXY(y, f)
        else x.CopyToWZYX(y, f)
    static member Using<'b> (m : Tensor4<'a>, f : NativeTensor4<'a> -> 'b) = 
        let gc = GCHandle.Alloc(m.Data, GCHandleType.Pinned)
        try f (NativeTensor4<'a>(NativePtr.ofNativeInt (gc.AddrOfPinnedObject()), m.Info))
        finally gc.Free()
    member x.SubTensor4(beginX : int64, beginY : int64, beginZ : int64, beginW : int64, sizeX : int64, sizeY : int64, sizeZ : int64, sizeW : int64, deltaX : int64, deltaY : int64, deltaZ : int64, deltaW : int64) = NativeTensor4<'a>(ptr, info.SubTensor4(beginX, beginY, beginZ, beginW, sizeX, sizeY, sizeZ, sizeW, deltaX, deltaY, deltaZ, deltaW))
    member x.SubTensor4(beginX : int64, beginY : int64, beginZ : int64, beginW : int64, sizeX : int64, sizeY : int64, sizeZ : int64, sizeW : int64) = NativeTensor4<'a>(ptr, info.SubTensor4(beginX, beginY, beginZ, beginW, sizeX, sizeY, sizeZ, sizeW))
    member x.SubTensor4(offset : V4l, size : V4l) = NativeTensor4<'a>(ptr, info.SubTensor4(offset, size))
    member x.SubTensor4(offset : V4l, size : V4l, delta : V4l) = NativeTensor4<'a>(ptr, info.SubTensor4(offset, size, delta))
    member x.SubTensor4(offset : V4i, size : V4i) = NativeTensor4<'a>(ptr, info.SubTensor4(offset, size))
    member x.SubTensor4(offset : V4i, size : V4i, delta : V4i) = NativeTensor4<'a>(ptr, info.SubTensor4(offset, size, delta))
    member x.GetSlice(minX : int, minY : int, minZ : int, minW : Option<int>, maxW : Option<int>) = 
        let beginX = minX |> int64
        let beginY = minY |> int64
        let beginZ = minZ |> int64
        let beginW = defaultArg minW 0 |> int64
        let maxW = defaultArg maxW (int info.SW - 1) |> int64
        let sizeW = 1L + maxW - beginW
        let info = VectorInfo(info.Index(beginX, beginY, beginZ, beginW), sizeW, info.DW)
        let res = NativeVector<'a>(ptr, info)
        res
    member x.GetSlice(minX : int64, minY : int64, minZ : int64, minW : Option<int64>, maxW : Option<int64>) = 
        let beginX = minX
        let beginY = minY
        let beginZ = minZ
        let beginW = defaultArg minW 0L
        let maxW = defaultArg maxW (info.SW - 1L)
        let sizeW = 1L + maxW - beginW
        let info = VectorInfo(info.Index(beginX, beginY, beginZ, beginW), sizeW, info.DW)
        let res = NativeVector<'a>(ptr, info)
        res
    member x.SetSlice(minX : int, minY : int, minZ : int, minW : Option<int>, maxW : Option<int>, value : 'a) = 
        let beginX = minX |> int64
        let beginY = minY |> int64
        let beginZ = minZ |> int64
        let beginW = defaultArg minW 0 |> int64
        let maxW = defaultArg maxW (int info.SW - 1) |> int64
        let sizeW = 1L + maxW - beginW
        let info = VectorInfo(info.Index(beginX, beginY, beginZ, beginW), sizeW, info.DW)
        let res = NativeVector<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : int64, minY : int64, minZ : int64, minW : Option<int64>, maxW : Option<int64>, value : 'a) = 
        let beginX = minX
        let beginY = minY
        let beginZ = minZ
        let beginW = defaultArg minW 0L
        let maxW = defaultArg maxW (info.SW - 1L)
        let sizeW = 1L + maxW - beginW
        let info = VectorInfo(info.Index(beginX, beginY, beginZ, beginW), sizeW, info.DW)
        let res = NativeVector<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : int, minY : int, minZ : int, minW : Option<int>, maxW : Option<int>, src : NativeVector<'a>) = 
        let beginX = minX |> int64
        let beginY = minY |> int64
        let beginZ = minZ |> int64
        let beginW = defaultArg minW 0 |> int64
        let maxW = defaultArg maxW (int info.SW - 1) |> int64
        let sizeW = 1L + maxW - beginW
        let info = VectorInfo(info.Index(beginX, beginY, beginZ, beginW), sizeW, info.DW)
        let res = NativeVector<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : int64, minY : int64, minZ : int64, minW : Option<int64>, maxW : Option<int64>, src : NativeVector<'a>) = 
        let beginX = minX
        let beginY = minY
        let beginZ = minZ
        let beginW = defaultArg minW 0L
        let maxW = defaultArg maxW (info.SW - 1L)
        let sizeW = 1L + maxW - beginW
        let info = VectorInfo(info.Index(beginX, beginY, beginZ, beginW), sizeW, info.DW)
        let res = NativeVector<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : int, minY : int, minZ : int, minW : Option<int>, maxW : Option<int>, src : Vector<'a>) = 
        let beginX = minX |> int64
        let beginY = minY |> int64
        let beginZ = minZ |> int64
        let beginW = defaultArg minW 0 |> int64
        let maxW = defaultArg maxW (int info.SW - 1) |> int64
        let sizeW = 1L + maxW - beginW
        let info = VectorInfo(info.Index(beginX, beginY, beginZ, beginW), sizeW, info.DW)
        let res = NativeVector<'a>(ptr, info)
        NativeVector<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.SetSlice(minX : int64, minY : int64, minZ : int64, minW : Option<int64>, maxW : Option<int64>, src : Vector<'a>) = 
        let beginX = minX
        let beginY = minY
        let beginZ = minZ
        let beginW = defaultArg minW 0L
        let maxW = defaultArg maxW (info.SW - 1L)
        let sizeW = 1L + maxW - beginW
        let info = VectorInfo(info.Index(beginX, beginY, beginZ, beginW), sizeW, info.DW)
        let res = NativeVector<'a>(ptr, info)
        NativeVector<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.GetSlice(minX : int, minY : int, minZ : Option<int>, maxZ : Option<int>, minW : int) = 
        let beginX = minX |> int64
        let beginY = minY |> int64
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let beginW = minW |> int64
        let info = VectorInfo(info.Index(beginX, beginY, beginZ, beginW), sizeZ, info.DZ)
        let res = NativeVector<'a>(ptr, info)
        res
    member x.GetSlice(minX : int64, minY : int64, minZ : Option<int64>, maxZ : Option<int64>, minW : int64) = 
        let beginX = minX
        let beginY = minY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let beginW = minW
        let info = VectorInfo(info.Index(beginX, beginY, beginZ, beginW), sizeZ, info.DZ)
        let res = NativeVector<'a>(ptr, info)
        res
    member x.SetSlice(minX : int, minY : int, minZ : Option<int>, maxZ : Option<int>, minW : int, value : 'a) = 
        let beginX = minX |> int64
        let beginY = minY |> int64
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let beginW = minW |> int64
        let info = VectorInfo(info.Index(beginX, beginY, beginZ, beginW), sizeZ, info.DZ)
        let res = NativeVector<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : int64, minY : int64, minZ : Option<int64>, maxZ : Option<int64>, minW : int64, value : 'a) = 
        let beginX = minX
        let beginY = minY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let beginW = minW
        let info = VectorInfo(info.Index(beginX, beginY, beginZ, beginW), sizeZ, info.DZ)
        let res = NativeVector<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : int, minY : int, minZ : Option<int>, maxZ : Option<int>, minW : int, src : NativeVector<'a>) = 
        let beginX = minX |> int64
        let beginY = minY |> int64
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let beginW = minW |> int64
        let info = VectorInfo(info.Index(beginX, beginY, beginZ, beginW), sizeZ, info.DZ)
        let res = NativeVector<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : int64, minY : int64, minZ : Option<int64>, maxZ : Option<int64>, minW : int64, src : NativeVector<'a>) = 
        let beginX = minX
        let beginY = minY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let beginW = minW
        let info = VectorInfo(info.Index(beginX, beginY, beginZ, beginW), sizeZ, info.DZ)
        let res = NativeVector<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : int, minY : int, minZ : Option<int>, maxZ : Option<int>, minW : int, src : Vector<'a>) = 
        let beginX = minX |> int64
        let beginY = minY |> int64
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let beginW = minW |> int64
        let info = VectorInfo(info.Index(beginX, beginY, beginZ, beginW), sizeZ, info.DZ)
        let res = NativeVector<'a>(ptr, info)
        NativeVector<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.SetSlice(minX : int64, minY : int64, minZ : Option<int64>, maxZ : Option<int64>, minW : int64, src : Vector<'a>) = 
        let beginX = minX
        let beginY = minY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let beginW = minW
        let info = VectorInfo(info.Index(beginX, beginY, beginZ, beginW), sizeZ, info.DZ)
        let res = NativeVector<'a>(ptr, info)
        NativeVector<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.GetSlice(minX : int, minY : int, minZ : Option<int>, maxZ : Option<int>, minW : Option<int>, maxW : Option<int>) = 
        let beginX = minX |> int64
        let beginY = minY |> int64
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let beginW = defaultArg minW 0 |> int64
        let maxW = defaultArg maxW (int info.SW - 1) |> int64
        let sizeW = 1L + maxW - beginW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeZ, sizeW), V2l(info.DZ, info.DW))
        let res = NativeMatrix<'a>(ptr, info)
        res
    member x.GetSlice(minX : int64, minY : int64, minZ : Option<int64>, maxZ : Option<int64>, minW : Option<int64>, maxW : Option<int64>) = 
        let beginX = minX
        let beginY = minY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let beginW = defaultArg minW 0L
        let maxW = defaultArg maxW (info.SW - 1L)
        let sizeW = 1L + maxW - beginW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeZ, sizeW), V2l(info.DZ, info.DW))
        let res = NativeMatrix<'a>(ptr, info)
        res
    member x.SetSlice(minX : int, minY : int, minZ : Option<int>, maxZ : Option<int>, minW : Option<int>, maxW : Option<int>, value : 'a) = 
        let beginX = minX |> int64
        let beginY = minY |> int64
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let beginW = defaultArg minW 0 |> int64
        let maxW = defaultArg maxW (int info.SW - 1) |> int64
        let sizeW = 1L + maxW - beginW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeZ, sizeW), V2l(info.DZ, info.DW))
        let res = NativeMatrix<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : int64, minY : int64, minZ : Option<int64>, maxZ : Option<int64>, minW : Option<int64>, maxW : Option<int64>, value : 'a) = 
        let beginX = minX
        let beginY = minY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let beginW = defaultArg minW 0L
        let maxW = defaultArg maxW (info.SW - 1L)
        let sizeW = 1L + maxW - beginW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeZ, sizeW), V2l(info.DZ, info.DW))
        let res = NativeMatrix<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : int, minY : int, minZ : Option<int>, maxZ : Option<int>, minW : Option<int>, maxW : Option<int>, src : NativeMatrix<'a>) = 
        let beginX = minX |> int64
        let beginY = minY |> int64
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let beginW = defaultArg minW 0 |> int64
        let maxW = defaultArg maxW (int info.SW - 1) |> int64
        let sizeW = 1L + maxW - beginW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeZ, sizeW), V2l(info.DZ, info.DW))
        let res = NativeMatrix<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : int64, minY : int64, minZ : Option<int64>, maxZ : Option<int64>, minW : Option<int64>, maxW : Option<int64>, src : NativeMatrix<'a>) = 
        let beginX = minX
        let beginY = minY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let beginW = defaultArg minW 0L
        let maxW = defaultArg maxW (info.SW - 1L)
        let sizeW = 1L + maxW - beginW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeZ, sizeW), V2l(info.DZ, info.DW))
        let res = NativeMatrix<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : int, minY : int, minZ : Option<int>, maxZ : Option<int>, minW : Option<int>, maxW : Option<int>, src : Matrix<'a>) = 
        let beginX = minX |> int64
        let beginY = minY |> int64
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let beginW = defaultArg minW 0 |> int64
        let maxW = defaultArg maxW (int info.SW - 1) |> int64
        let sizeW = 1L + maxW - beginW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeZ, sizeW), V2l(info.DZ, info.DW))
        let res = NativeMatrix<'a>(ptr, info)
        NativeMatrix<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.SetSlice(minX : int64, minY : int64, minZ : Option<int64>, maxZ : Option<int64>, minW : Option<int64>, maxW : Option<int64>, src : Matrix<'a>) = 
        let beginX = minX
        let beginY = minY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let beginW = defaultArg minW 0L
        let maxW = defaultArg maxW (info.SW - 1L)
        let sizeW = 1L + maxW - beginW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeZ, sizeW), V2l(info.DZ, info.DW))
        let res = NativeMatrix<'a>(ptr, info)
        NativeMatrix<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.GetSlice(minX : int, minY : Option<int>, maxY : Option<int>, minZ : int, minW : int) = 
        let beginX = minX |> int64
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ |> int64
        let beginW = minW |> int64
        let info = VectorInfo(info.Index(beginX, beginY, beginZ, beginW), sizeY, info.DY)
        let res = NativeVector<'a>(ptr, info)
        res
    member x.GetSlice(minX : int64, minY : Option<int64>, maxY : Option<int64>, minZ : int64, minW : int64) = 
        let beginX = minX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ
        let beginW = minW
        let info = VectorInfo(info.Index(beginX, beginY, beginZ, beginW), sizeY, info.DY)
        let res = NativeVector<'a>(ptr, info)
        res
    member x.SetSlice(minX : int, minY : Option<int>, maxY : Option<int>, minZ : int, minW : int, value : 'a) = 
        let beginX = minX |> int64
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ |> int64
        let beginW = minW |> int64
        let info = VectorInfo(info.Index(beginX, beginY, beginZ, beginW), sizeY, info.DY)
        let res = NativeVector<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : int64, minY : Option<int64>, maxY : Option<int64>, minZ : int64, minW : int64, value : 'a) = 
        let beginX = minX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ
        let beginW = minW
        let info = VectorInfo(info.Index(beginX, beginY, beginZ, beginW), sizeY, info.DY)
        let res = NativeVector<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : int, minY : Option<int>, maxY : Option<int>, minZ : int, minW : int, src : NativeVector<'a>) = 
        let beginX = minX |> int64
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ |> int64
        let beginW = minW |> int64
        let info = VectorInfo(info.Index(beginX, beginY, beginZ, beginW), sizeY, info.DY)
        let res = NativeVector<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : int64, minY : Option<int64>, maxY : Option<int64>, minZ : int64, minW : int64, src : NativeVector<'a>) = 
        let beginX = minX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ
        let beginW = minW
        let info = VectorInfo(info.Index(beginX, beginY, beginZ, beginW), sizeY, info.DY)
        let res = NativeVector<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : int, minY : Option<int>, maxY : Option<int>, minZ : int, minW : int, src : Vector<'a>) = 
        let beginX = minX |> int64
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ |> int64
        let beginW = minW |> int64
        let info = VectorInfo(info.Index(beginX, beginY, beginZ, beginW), sizeY, info.DY)
        let res = NativeVector<'a>(ptr, info)
        NativeVector<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.SetSlice(minX : int64, minY : Option<int64>, maxY : Option<int64>, minZ : int64, minW : int64, src : Vector<'a>) = 
        let beginX = minX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ
        let beginW = minW
        let info = VectorInfo(info.Index(beginX, beginY, beginZ, beginW), sizeY, info.DY)
        let res = NativeVector<'a>(ptr, info)
        NativeVector<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.GetSlice(minX : int, minY : Option<int>, maxY : Option<int>, minZ : int, minW : Option<int>, maxW : Option<int>) = 
        let beginX = minX |> int64
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ |> int64
        let beginW = defaultArg minW 0 |> int64
        let maxW = defaultArg maxW (int info.SW - 1) |> int64
        let sizeW = 1L + maxW - beginW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeY, sizeW), V2l(info.DY, info.DW))
        let res = NativeMatrix<'a>(ptr, info)
        res
    member x.GetSlice(minX : int64, minY : Option<int64>, maxY : Option<int64>, minZ : int64, minW : Option<int64>, maxW : Option<int64>) = 
        let beginX = minX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ
        let beginW = defaultArg minW 0L
        let maxW = defaultArg maxW (info.SW - 1L)
        let sizeW = 1L + maxW - beginW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeY, sizeW), V2l(info.DY, info.DW))
        let res = NativeMatrix<'a>(ptr, info)
        res
    member x.SetSlice(minX : int, minY : Option<int>, maxY : Option<int>, minZ : int, minW : Option<int>, maxW : Option<int>, value : 'a) = 
        let beginX = minX |> int64
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ |> int64
        let beginW = defaultArg minW 0 |> int64
        let maxW = defaultArg maxW (int info.SW - 1) |> int64
        let sizeW = 1L + maxW - beginW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeY, sizeW), V2l(info.DY, info.DW))
        let res = NativeMatrix<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : int64, minY : Option<int64>, maxY : Option<int64>, minZ : int64, minW : Option<int64>, maxW : Option<int64>, value : 'a) = 
        let beginX = minX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ
        let beginW = defaultArg minW 0L
        let maxW = defaultArg maxW (info.SW - 1L)
        let sizeW = 1L + maxW - beginW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeY, sizeW), V2l(info.DY, info.DW))
        let res = NativeMatrix<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : int, minY : Option<int>, maxY : Option<int>, minZ : int, minW : Option<int>, maxW : Option<int>, src : NativeMatrix<'a>) = 
        let beginX = minX |> int64
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ |> int64
        let beginW = defaultArg minW 0 |> int64
        let maxW = defaultArg maxW (int info.SW - 1) |> int64
        let sizeW = 1L + maxW - beginW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeY, sizeW), V2l(info.DY, info.DW))
        let res = NativeMatrix<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : int64, minY : Option<int64>, maxY : Option<int64>, minZ : int64, minW : Option<int64>, maxW : Option<int64>, src : NativeMatrix<'a>) = 
        let beginX = minX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ
        let beginW = defaultArg minW 0L
        let maxW = defaultArg maxW (info.SW - 1L)
        let sizeW = 1L + maxW - beginW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeY, sizeW), V2l(info.DY, info.DW))
        let res = NativeMatrix<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : int, minY : Option<int>, maxY : Option<int>, minZ : int, minW : Option<int>, maxW : Option<int>, src : Matrix<'a>) = 
        let beginX = minX |> int64
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ |> int64
        let beginW = defaultArg minW 0 |> int64
        let maxW = defaultArg maxW (int info.SW - 1) |> int64
        let sizeW = 1L + maxW - beginW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeY, sizeW), V2l(info.DY, info.DW))
        let res = NativeMatrix<'a>(ptr, info)
        NativeMatrix<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.SetSlice(minX : int64, minY : Option<int64>, maxY : Option<int64>, minZ : int64, minW : Option<int64>, maxW : Option<int64>, src : Matrix<'a>) = 
        let beginX = minX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ
        let beginW = defaultArg minW 0L
        let maxW = defaultArg maxW (info.SW - 1L)
        let sizeW = 1L + maxW - beginW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeY, sizeW), V2l(info.DY, info.DW))
        let res = NativeMatrix<'a>(ptr, info)
        NativeMatrix<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.GetSlice(minX : int, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>, minW : int) = 
        let beginX = minX |> int64
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let beginW = minW |> int64
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeY, sizeZ), V2l(info.DY, info.DZ))
        let res = NativeMatrix<'a>(ptr, info)
        res
    member x.GetSlice(minX : int64, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>, minW : int64) = 
        let beginX = minX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let beginW = minW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeY, sizeZ), V2l(info.DY, info.DZ))
        let res = NativeMatrix<'a>(ptr, info)
        res
    member x.SetSlice(minX : int, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>, minW : int, value : 'a) = 
        let beginX = minX |> int64
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let beginW = minW |> int64
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeY, sizeZ), V2l(info.DY, info.DZ))
        let res = NativeMatrix<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : int64, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>, minW : int64, value : 'a) = 
        let beginX = minX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let beginW = minW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeY, sizeZ), V2l(info.DY, info.DZ))
        let res = NativeMatrix<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : int, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>, minW : int, src : NativeMatrix<'a>) = 
        let beginX = minX |> int64
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let beginW = minW |> int64
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeY, sizeZ), V2l(info.DY, info.DZ))
        let res = NativeMatrix<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : int64, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>, minW : int64, src : NativeMatrix<'a>) = 
        let beginX = minX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let beginW = minW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeY, sizeZ), V2l(info.DY, info.DZ))
        let res = NativeMatrix<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : int, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>, minW : int, src : Matrix<'a>) = 
        let beginX = minX |> int64
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let beginW = minW |> int64
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeY, sizeZ), V2l(info.DY, info.DZ))
        let res = NativeMatrix<'a>(ptr, info)
        NativeMatrix<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.SetSlice(minX : int64, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>, minW : int64, src : Matrix<'a>) = 
        let beginX = minX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let beginW = minW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeY, sizeZ), V2l(info.DY, info.DZ))
        let res = NativeMatrix<'a>(ptr, info)
        NativeMatrix<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.GetSlice(minX : int, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>, minW : Option<int>, maxW : Option<int>) = 
        let beginX = minX |> int64
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let beginW = defaultArg minW 0 |> int64
        let maxW = defaultArg maxW (int info.SW - 1) |> int64
        let sizeW = 1L + maxW - beginW
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ, beginW), V3l(sizeY, sizeZ, sizeW), V3l(info.DY, info.DZ, info.DW))
        let res = NativeVolume<'a>(ptr, info)
        res
    member x.GetSlice(minX : int64, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>, minW : Option<int64>, maxW : Option<int64>) = 
        let beginX = minX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let beginW = defaultArg minW 0L
        let maxW = defaultArg maxW (info.SW - 1L)
        let sizeW = 1L + maxW - beginW
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ, beginW), V3l(sizeY, sizeZ, sizeW), V3l(info.DY, info.DZ, info.DW))
        let res = NativeVolume<'a>(ptr, info)
        res
    member x.SetSlice(minX : int, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>, minW : Option<int>, maxW : Option<int>, value : 'a) = 
        let beginX = minX |> int64
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let beginW = defaultArg minW 0 |> int64
        let maxW = defaultArg maxW (int info.SW - 1) |> int64
        let sizeW = 1L + maxW - beginW
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ, beginW), V3l(sizeY, sizeZ, sizeW), V3l(info.DY, info.DZ, info.DW))
        let res = NativeVolume<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : int64, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>, minW : Option<int64>, maxW : Option<int64>, value : 'a) = 
        let beginX = minX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let beginW = defaultArg minW 0L
        let maxW = defaultArg maxW (info.SW - 1L)
        let sizeW = 1L + maxW - beginW
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ, beginW), V3l(sizeY, sizeZ, sizeW), V3l(info.DY, info.DZ, info.DW))
        let res = NativeVolume<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : int, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>, minW : Option<int>, maxW : Option<int>, src : NativeVolume<'a>) = 
        let beginX = minX |> int64
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let beginW = defaultArg minW 0 |> int64
        let maxW = defaultArg maxW (int info.SW - 1) |> int64
        let sizeW = 1L + maxW - beginW
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ, beginW), V3l(sizeY, sizeZ, sizeW), V3l(info.DY, info.DZ, info.DW))
        let res = NativeVolume<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : int64, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>, minW : Option<int64>, maxW : Option<int64>, src : NativeVolume<'a>) = 
        let beginX = minX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let beginW = defaultArg minW 0L
        let maxW = defaultArg maxW (info.SW - 1L)
        let sizeW = 1L + maxW - beginW
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ, beginW), V3l(sizeY, sizeZ, sizeW), V3l(info.DY, info.DZ, info.DW))
        let res = NativeVolume<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : int, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>, minW : Option<int>, maxW : Option<int>, src : Volume<'a>) = 
        let beginX = minX |> int64
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let beginW = defaultArg minW 0 |> int64
        let maxW = defaultArg maxW (int info.SW - 1) |> int64
        let sizeW = 1L + maxW - beginW
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ, beginW), V3l(sizeY, sizeZ, sizeW), V3l(info.DY, info.DZ, info.DW))
        let res = NativeVolume<'a>(ptr, info)
        NativeVolume<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.SetSlice(minX : int64, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>, minW : Option<int64>, maxW : Option<int64>, src : Volume<'a>) = 
        let beginX = minX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let beginW = defaultArg minW 0L
        let maxW = defaultArg maxW (info.SW - 1L)
        let sizeW = 1L + maxW - beginW
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ, beginW), V3l(sizeY, sizeZ, sizeW), V3l(info.DY, info.DZ, info.DW))
        let res = NativeVolume<'a>(ptr, info)
        NativeVolume<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.GetSlice(minX : Option<int>, maxX : Option<int>, minY : int, minZ : int, minW : int) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = minY |> int64
        let beginZ = minZ |> int64
        let beginW = minW |> int64
        let info = VectorInfo(info.Index(beginX, beginY, beginZ, beginW), sizeX, info.DX)
        let res = NativeVector<'a>(ptr, info)
        res
    member x.GetSlice(minX : Option<int64>, maxX : Option<int64>, minY : int64, minZ : int64, minW : int64) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = minY
        let beginZ = minZ
        let beginW = minW
        let info = VectorInfo(info.Index(beginX, beginY, beginZ, beginW), sizeX, info.DX)
        let res = NativeVector<'a>(ptr, info)
        res
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : int, minZ : int, minW : int, value : 'a) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = minY |> int64
        let beginZ = minZ |> int64
        let beginW = minW |> int64
        let info = VectorInfo(info.Index(beginX, beginY, beginZ, beginW), sizeX, info.DX)
        let res = NativeVector<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : int64, minZ : int64, minW : int64, value : 'a) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = minY
        let beginZ = minZ
        let beginW = minW
        let info = VectorInfo(info.Index(beginX, beginY, beginZ, beginW), sizeX, info.DX)
        let res = NativeVector<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : int, minZ : int, minW : int, src : NativeVector<'a>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = minY |> int64
        let beginZ = minZ |> int64
        let beginW = minW |> int64
        let info = VectorInfo(info.Index(beginX, beginY, beginZ, beginW), sizeX, info.DX)
        let res = NativeVector<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : int64, minZ : int64, minW : int64, src : NativeVector<'a>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = minY
        let beginZ = minZ
        let beginW = minW
        let info = VectorInfo(info.Index(beginX, beginY, beginZ, beginW), sizeX, info.DX)
        let res = NativeVector<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : int, minZ : int, minW : int, src : Vector<'a>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = minY |> int64
        let beginZ = minZ |> int64
        let beginW = minW |> int64
        let info = VectorInfo(info.Index(beginX, beginY, beginZ, beginW), sizeX, info.DX)
        let res = NativeVector<'a>(ptr, info)
        NativeVector<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : int64, minZ : int64, minW : int64, src : Vector<'a>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = minY
        let beginZ = minZ
        let beginW = minW
        let info = VectorInfo(info.Index(beginX, beginY, beginZ, beginW), sizeX, info.DX)
        let res = NativeVector<'a>(ptr, info)
        NativeVector<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.GetSlice(minX : Option<int>, maxX : Option<int>, minY : int, minZ : int, minW : Option<int>, maxW : Option<int>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = minY |> int64
        let beginZ = minZ |> int64
        let beginW = defaultArg minW 0 |> int64
        let maxW = defaultArg maxW (int info.SW - 1) |> int64
        let sizeW = 1L + maxW - beginW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeX, sizeW), V2l(info.DX, info.DW))
        let res = NativeMatrix<'a>(ptr, info)
        res
    member x.GetSlice(minX : Option<int64>, maxX : Option<int64>, minY : int64, minZ : int64, minW : Option<int64>, maxW : Option<int64>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = minY
        let beginZ = minZ
        let beginW = defaultArg minW 0L
        let maxW = defaultArg maxW (info.SW - 1L)
        let sizeW = 1L + maxW - beginW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeX, sizeW), V2l(info.DX, info.DW))
        let res = NativeMatrix<'a>(ptr, info)
        res
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : int, minZ : int, minW : Option<int>, maxW : Option<int>, value : 'a) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = minY |> int64
        let beginZ = minZ |> int64
        let beginW = defaultArg minW 0 |> int64
        let maxW = defaultArg maxW (int info.SW - 1) |> int64
        let sizeW = 1L + maxW - beginW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeX, sizeW), V2l(info.DX, info.DW))
        let res = NativeMatrix<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : int64, minZ : int64, minW : Option<int64>, maxW : Option<int64>, value : 'a) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = minY
        let beginZ = minZ
        let beginW = defaultArg minW 0L
        let maxW = defaultArg maxW (info.SW - 1L)
        let sizeW = 1L + maxW - beginW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeX, sizeW), V2l(info.DX, info.DW))
        let res = NativeMatrix<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : int, minZ : int, minW : Option<int>, maxW : Option<int>, src : NativeMatrix<'a>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = minY |> int64
        let beginZ = minZ |> int64
        let beginW = defaultArg minW 0 |> int64
        let maxW = defaultArg maxW (int info.SW - 1) |> int64
        let sizeW = 1L + maxW - beginW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeX, sizeW), V2l(info.DX, info.DW))
        let res = NativeMatrix<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : int64, minZ : int64, minW : Option<int64>, maxW : Option<int64>, src : NativeMatrix<'a>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = minY
        let beginZ = minZ
        let beginW = defaultArg minW 0L
        let maxW = defaultArg maxW (info.SW - 1L)
        let sizeW = 1L + maxW - beginW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeX, sizeW), V2l(info.DX, info.DW))
        let res = NativeMatrix<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : int, minZ : int, minW : Option<int>, maxW : Option<int>, src : Matrix<'a>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = minY |> int64
        let beginZ = minZ |> int64
        let beginW = defaultArg minW 0 |> int64
        let maxW = defaultArg maxW (int info.SW - 1) |> int64
        let sizeW = 1L + maxW - beginW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeX, sizeW), V2l(info.DX, info.DW))
        let res = NativeMatrix<'a>(ptr, info)
        NativeMatrix<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : int64, minZ : int64, minW : Option<int64>, maxW : Option<int64>, src : Matrix<'a>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = minY
        let beginZ = minZ
        let beginW = defaultArg minW 0L
        let maxW = defaultArg maxW (info.SW - 1L)
        let sizeW = 1L + maxW - beginW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeX, sizeW), V2l(info.DX, info.DW))
        let res = NativeMatrix<'a>(ptr, info)
        NativeMatrix<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.GetSlice(minX : Option<int>, maxX : Option<int>, minY : int, minZ : Option<int>, maxZ : Option<int>, minW : int) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = minY |> int64
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let beginW = minW |> int64
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeX, sizeZ), V2l(info.DX, info.DZ))
        let res = NativeMatrix<'a>(ptr, info)
        res
    member x.GetSlice(minX : Option<int64>, maxX : Option<int64>, minY : int64, minZ : Option<int64>, maxZ : Option<int64>, minW : int64) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = minY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let beginW = minW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeX, sizeZ), V2l(info.DX, info.DZ))
        let res = NativeMatrix<'a>(ptr, info)
        res
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : int, minZ : Option<int>, maxZ : Option<int>, minW : int, value : 'a) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = minY |> int64
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let beginW = minW |> int64
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeX, sizeZ), V2l(info.DX, info.DZ))
        let res = NativeMatrix<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : int64, minZ : Option<int64>, maxZ : Option<int64>, minW : int64, value : 'a) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = minY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let beginW = minW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeX, sizeZ), V2l(info.DX, info.DZ))
        let res = NativeMatrix<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : int, minZ : Option<int>, maxZ : Option<int>, minW : int, src : NativeMatrix<'a>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = minY |> int64
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let beginW = minW |> int64
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeX, sizeZ), V2l(info.DX, info.DZ))
        let res = NativeMatrix<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : int64, minZ : Option<int64>, maxZ : Option<int64>, minW : int64, src : NativeMatrix<'a>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = minY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let beginW = minW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeX, sizeZ), V2l(info.DX, info.DZ))
        let res = NativeMatrix<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : int, minZ : Option<int>, maxZ : Option<int>, minW : int, src : Matrix<'a>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = minY |> int64
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let beginW = minW |> int64
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeX, sizeZ), V2l(info.DX, info.DZ))
        let res = NativeMatrix<'a>(ptr, info)
        NativeMatrix<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : int64, minZ : Option<int64>, maxZ : Option<int64>, minW : int64, src : Matrix<'a>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = minY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let beginW = minW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeX, sizeZ), V2l(info.DX, info.DZ))
        let res = NativeMatrix<'a>(ptr, info)
        NativeMatrix<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.GetSlice(minX : Option<int>, maxX : Option<int>, minY : int, minZ : Option<int>, maxZ : Option<int>, minW : Option<int>, maxW : Option<int>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = minY |> int64
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let beginW = defaultArg minW 0 |> int64
        let maxW = defaultArg maxW (int info.SW - 1) |> int64
        let sizeW = 1L + maxW - beginW
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ, beginW), V3l(sizeX, sizeZ, sizeW), V3l(info.DX, info.DZ, info.DW))
        let res = NativeVolume<'a>(ptr, info)
        res
    member x.GetSlice(minX : Option<int64>, maxX : Option<int64>, minY : int64, minZ : Option<int64>, maxZ : Option<int64>, minW : Option<int64>, maxW : Option<int64>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = minY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let beginW = defaultArg minW 0L
        let maxW = defaultArg maxW (info.SW - 1L)
        let sizeW = 1L + maxW - beginW
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ, beginW), V3l(sizeX, sizeZ, sizeW), V3l(info.DX, info.DZ, info.DW))
        let res = NativeVolume<'a>(ptr, info)
        res
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : int, minZ : Option<int>, maxZ : Option<int>, minW : Option<int>, maxW : Option<int>, value : 'a) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = minY |> int64
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let beginW = defaultArg minW 0 |> int64
        let maxW = defaultArg maxW (int info.SW - 1) |> int64
        let sizeW = 1L + maxW - beginW
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ, beginW), V3l(sizeX, sizeZ, sizeW), V3l(info.DX, info.DZ, info.DW))
        let res = NativeVolume<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : int64, minZ : Option<int64>, maxZ : Option<int64>, minW : Option<int64>, maxW : Option<int64>, value : 'a) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = minY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let beginW = defaultArg minW 0L
        let maxW = defaultArg maxW (info.SW - 1L)
        let sizeW = 1L + maxW - beginW
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ, beginW), V3l(sizeX, sizeZ, sizeW), V3l(info.DX, info.DZ, info.DW))
        let res = NativeVolume<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : int, minZ : Option<int>, maxZ : Option<int>, minW : Option<int>, maxW : Option<int>, src : NativeVolume<'a>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = minY |> int64
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let beginW = defaultArg minW 0 |> int64
        let maxW = defaultArg maxW (int info.SW - 1) |> int64
        let sizeW = 1L + maxW - beginW
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ, beginW), V3l(sizeX, sizeZ, sizeW), V3l(info.DX, info.DZ, info.DW))
        let res = NativeVolume<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : int64, minZ : Option<int64>, maxZ : Option<int64>, minW : Option<int64>, maxW : Option<int64>, src : NativeVolume<'a>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = minY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let beginW = defaultArg minW 0L
        let maxW = defaultArg maxW (info.SW - 1L)
        let sizeW = 1L + maxW - beginW
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ, beginW), V3l(sizeX, sizeZ, sizeW), V3l(info.DX, info.DZ, info.DW))
        let res = NativeVolume<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : int, minZ : Option<int>, maxZ : Option<int>, minW : Option<int>, maxW : Option<int>, src : Volume<'a>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = minY |> int64
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let beginW = defaultArg minW 0 |> int64
        let maxW = defaultArg maxW (int info.SW - 1) |> int64
        let sizeW = 1L + maxW - beginW
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ, beginW), V3l(sizeX, sizeZ, sizeW), V3l(info.DX, info.DZ, info.DW))
        let res = NativeVolume<'a>(ptr, info)
        NativeVolume<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : int64, minZ : Option<int64>, maxZ : Option<int64>, minW : Option<int64>, maxW : Option<int64>, src : Volume<'a>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = minY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let beginW = defaultArg minW 0L
        let maxW = defaultArg maxW (info.SW - 1L)
        let sizeW = 1L + maxW - beginW
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ, beginW), V3l(sizeX, sizeZ, sizeW), V3l(info.DX, info.DZ, info.DW))
        let res = NativeVolume<'a>(ptr, info)
        NativeVolume<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.GetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, minZ : int, minW : int) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ |> int64
        let beginW = minW |> int64
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeX, sizeY), V2l(info.DX, info.DY))
        let res = NativeMatrix<'a>(ptr, info)
        res
    member x.GetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, minZ : int64, minW : int64) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ
        let beginW = minW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeX, sizeY), V2l(info.DX, info.DY))
        let res = NativeMatrix<'a>(ptr, info)
        res
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, minZ : int, minW : int, value : 'a) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ |> int64
        let beginW = minW |> int64
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeX, sizeY), V2l(info.DX, info.DY))
        let res = NativeMatrix<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, minZ : int64, minW : int64, value : 'a) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ
        let beginW = minW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeX, sizeY), V2l(info.DX, info.DY))
        let res = NativeMatrix<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, minZ : int, minW : int, src : NativeMatrix<'a>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ |> int64
        let beginW = minW |> int64
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeX, sizeY), V2l(info.DX, info.DY))
        let res = NativeMatrix<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, minZ : int64, minW : int64, src : NativeMatrix<'a>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ
        let beginW = minW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeX, sizeY), V2l(info.DX, info.DY))
        let res = NativeMatrix<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, minZ : int, minW : int, src : Matrix<'a>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ |> int64
        let beginW = minW |> int64
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeX, sizeY), V2l(info.DX, info.DY))
        let res = NativeMatrix<'a>(ptr, info)
        NativeMatrix<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, minZ : int64, minW : int64, src : Matrix<'a>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ
        let beginW = minW
        let info = MatrixInfo(info.Index(beginX, beginY, beginZ, beginW), V2l(sizeX, sizeY), V2l(info.DX, info.DY))
        let res = NativeMatrix<'a>(ptr, info)
        NativeMatrix<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.GetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, minZ : int, minW : Option<int>, maxW : Option<int>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ |> int64
        let beginW = defaultArg minW 0 |> int64
        let maxW = defaultArg maxW (int info.SW - 1) |> int64
        let sizeW = 1L + maxW - beginW
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ, beginW), V3l(sizeX, sizeY, sizeW), V3l(info.DX, info.DY, info.DW))
        let res = NativeVolume<'a>(ptr, info)
        res
    member x.GetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, minZ : int64, minW : Option<int64>, maxW : Option<int64>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ
        let beginW = defaultArg minW 0L
        let maxW = defaultArg maxW (info.SW - 1L)
        let sizeW = 1L + maxW - beginW
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ, beginW), V3l(sizeX, sizeY, sizeW), V3l(info.DX, info.DY, info.DW))
        let res = NativeVolume<'a>(ptr, info)
        res
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, minZ : int, minW : Option<int>, maxW : Option<int>, value : 'a) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ |> int64
        let beginW = defaultArg minW 0 |> int64
        let maxW = defaultArg maxW (int info.SW - 1) |> int64
        let sizeW = 1L + maxW - beginW
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ, beginW), V3l(sizeX, sizeY, sizeW), V3l(info.DX, info.DY, info.DW))
        let res = NativeVolume<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, minZ : int64, minW : Option<int64>, maxW : Option<int64>, value : 'a) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ
        let beginW = defaultArg minW 0L
        let maxW = defaultArg maxW (info.SW - 1L)
        let sizeW = 1L + maxW - beginW
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ, beginW), V3l(sizeX, sizeY, sizeW), V3l(info.DX, info.DY, info.DW))
        let res = NativeVolume<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, minZ : int, minW : Option<int>, maxW : Option<int>, src : NativeVolume<'a>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ |> int64
        let beginW = defaultArg minW 0 |> int64
        let maxW = defaultArg maxW (int info.SW - 1) |> int64
        let sizeW = 1L + maxW - beginW
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ, beginW), V3l(sizeX, sizeY, sizeW), V3l(info.DX, info.DY, info.DW))
        let res = NativeVolume<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, minZ : int64, minW : Option<int64>, maxW : Option<int64>, src : NativeVolume<'a>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ
        let beginW = defaultArg minW 0L
        let maxW = defaultArg maxW (info.SW - 1L)
        let sizeW = 1L + maxW - beginW
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ, beginW), V3l(sizeX, sizeY, sizeW), V3l(info.DX, info.DY, info.DW))
        let res = NativeVolume<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, minZ : int, minW : Option<int>, maxW : Option<int>, src : Volume<'a>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ |> int64
        let beginW = defaultArg minW 0 |> int64
        let maxW = defaultArg maxW (int info.SW - 1) |> int64
        let sizeW = 1L + maxW - beginW
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ, beginW), V3l(sizeX, sizeY, sizeW), V3l(info.DX, info.DY, info.DW))
        let res = NativeVolume<'a>(ptr, info)
        NativeVolume<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, minZ : int64, minW : Option<int64>, maxW : Option<int64>, src : Volume<'a>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = minZ
        let beginW = defaultArg minW 0L
        let maxW = defaultArg maxW (info.SW - 1L)
        let sizeW = 1L + maxW - beginW
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ, beginW), V3l(sizeX, sizeY, sizeW), V3l(info.DX, info.DY, info.DW))
        let res = NativeVolume<'a>(ptr, info)
        NativeVolume<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.GetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>, minW : int) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let beginW = minW |> int64
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ, beginW), V3l(sizeX, sizeY, sizeZ), V3l(info.DX, info.DY, info.DZ))
        let res = NativeVolume<'a>(ptr, info)
        res
    member x.GetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>, minW : int64) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let beginW = minW
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ, beginW), V3l(sizeX, sizeY, sizeZ), V3l(info.DX, info.DY, info.DZ))
        let res = NativeVolume<'a>(ptr, info)
        res
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>, minW : int, value : 'a) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let beginW = minW |> int64
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ, beginW), V3l(sizeX, sizeY, sizeZ), V3l(info.DX, info.DY, info.DZ))
        let res = NativeVolume<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>, minW : int64, value : 'a) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let beginW = minW
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ, beginW), V3l(sizeX, sizeY, sizeZ), V3l(info.DX, info.DY, info.DZ))
        let res = NativeVolume<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>, minW : int, src : NativeVolume<'a>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let beginW = minW |> int64
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ, beginW), V3l(sizeX, sizeY, sizeZ), V3l(info.DX, info.DY, info.DZ))
        let res = NativeVolume<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>, minW : int64, src : NativeVolume<'a>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let beginW = minW
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ, beginW), V3l(sizeX, sizeY, sizeZ), V3l(info.DX, info.DY, info.DZ))
        let res = NativeVolume<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>, minW : int, src : Volume<'a>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let beginW = minW |> int64
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ, beginW), V3l(sizeX, sizeY, sizeZ), V3l(info.DX, info.DY, info.DZ))
        let res = NativeVolume<'a>(ptr, info)
        NativeVolume<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>, minW : int64, src : Volume<'a>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let beginW = minW
        let info = VolumeInfo(info.Index(beginX, beginY, beginZ, beginW), V3l(sizeX, sizeY, sizeZ), V3l(info.DX, info.DY, info.DZ))
        let res = NativeVolume<'a>(ptr, info)
        NativeVolume<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.GetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>, minW : Option<int>, maxW : Option<int>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let beginW = defaultArg minW 0 |> int64
        let maxW = defaultArg maxW (int info.SW - 1) |> int64
        let sizeW = 1L + maxW - beginW
        let info = Tensor4Info(info.Index(beginX, beginY, beginZ, beginW), V4l(sizeX, sizeY, sizeZ, sizeW), V4l(info.DX, info.DY, info.DZ, info.DW))
        let res = NativeTensor4<'a>(ptr, info)
        res
    member x.GetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>, minW : Option<int64>, maxW : Option<int64>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let beginW = defaultArg minW 0L
        let maxW = defaultArg maxW (info.SW - 1L)
        let sizeW = 1L + maxW - beginW
        let info = Tensor4Info(info.Index(beginX, beginY, beginZ, beginW), V4l(sizeX, sizeY, sizeZ, sizeW), V4l(info.DX, info.DY, info.DZ, info.DW))
        let res = NativeTensor4<'a>(ptr, info)
        res
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>, minW : Option<int>, maxW : Option<int>, value : 'a) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let beginW = defaultArg minW 0 |> int64
        let maxW = defaultArg maxW (int info.SW - 1) |> int64
        let sizeW = 1L + maxW - beginW
        let info = Tensor4Info(info.Index(beginX, beginY, beginZ, beginW), V4l(sizeX, sizeY, sizeZ, sizeW), V4l(info.DX, info.DY, info.DZ, info.DW))
        let res = NativeTensor4<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>, minW : Option<int64>, maxW : Option<int64>, value : 'a) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let beginW = defaultArg minW 0L
        let maxW = defaultArg maxW (info.SW - 1L)
        let sizeW = 1L + maxW - beginW
        let info = Tensor4Info(info.Index(beginX, beginY, beginZ, beginW), V4l(sizeX, sizeY, sizeZ, sizeW), V4l(info.DX, info.DY, info.DZ, info.DW))
        let res = NativeTensor4<'a>(ptr, info)
        res.Set(value)
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>, minW : Option<int>, maxW : Option<int>, src : NativeTensor4<'a>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let beginW = defaultArg minW 0 |> int64
        let maxW = defaultArg maxW (int info.SW - 1) |> int64
        let sizeW = 1L + maxW - beginW
        let info = Tensor4Info(info.Index(beginX, beginY, beginZ, beginW), V4l(sizeX, sizeY, sizeZ, sizeW), V4l(info.DX, info.DY, info.DZ, info.DW))
        let res = NativeTensor4<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>, minW : Option<int64>, maxW : Option<int64>, src : NativeTensor4<'a>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let beginW = defaultArg minW 0L
        let maxW = defaultArg maxW (info.SW - 1L)
        let sizeW = 1L + maxW - beginW
        let info = Tensor4Info(info.Index(beginX, beginY, beginZ, beginW), V4l(sizeX, sizeY, sizeZ, sizeW), V4l(info.DX, info.DY, info.DZ, info.DW))
        let res = NativeTensor4<'a>(ptr, info)
        src.CopyTo(res)
    member x.SetSlice(minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>, minW : Option<int>, maxW : Option<int>, src : Tensor4<'a>) = 
        let beginX = defaultArg minX 0 |> int64
        let maxX = defaultArg maxX (int info.SX - 1) |> int64
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0 |> int64
        let maxY = defaultArg maxY (int info.SY - 1) |> int64
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0 |> int64
        let maxZ = defaultArg maxZ (int info.SZ - 1) |> int64
        let sizeZ = 1L + maxZ - beginZ
        let beginW = defaultArg minW 0 |> int64
        let maxW = defaultArg maxW (int info.SW - 1) |> int64
        let sizeW = 1L + maxW - beginW
        let info = Tensor4Info(info.Index(beginX, beginY, beginZ, beginW), V4l(sizeX, sizeY, sizeZ, sizeW), V4l(info.DX, info.DY, info.DZ, info.DW))
        let res = NativeTensor4<'a>(ptr, info)
        NativeTensor4<'a>.Using(src, fun src -> src.CopyTo(res))
    member x.SetSlice(minX : Option<int64>, maxX : Option<int64>, minY : Option<int64>, maxY : Option<int64>, minZ : Option<int64>, maxZ : Option<int64>, minW : Option<int64>, maxW : Option<int64>, src : Tensor4<'a>) = 
        let beginX = defaultArg minX 0L
        let maxX = defaultArg maxX (info.SX - 1L)
        let sizeX = 1L + maxX - beginX
        let beginY = defaultArg minY 0L
        let maxY = defaultArg maxY (info.SY - 1L)
        let sizeY = 1L + maxY - beginY
        let beginZ = defaultArg minZ 0L
        let maxZ = defaultArg maxZ (info.SZ - 1L)
        let sizeZ = 1L + maxZ - beginZ
        let beginW = defaultArg minW 0L
        let maxW = defaultArg maxW (info.SW - 1L)
        let sizeW = 1L + maxW - beginW
        let info = Tensor4Info(info.Index(beginX, beginY, beginZ, beginW), V4l(sizeX, sizeY, sizeZ, sizeW), V4l(info.DX, info.DY, info.DZ, info.DW))
        let res = NativeTensor4<'a>(ptr, info)
        NativeTensor4<'a>.Using(src, fun src -> src.CopyTo(res))

/// The NativeTensor4 module providers convenient F#-style functions for accessing NativeTensor4s
module NativeTensor4 =
    /// sets the entire Tensor4 to the given value
    let inline set (value : 'a) (dst : NativeTensor4<'a>) = dst.Set(value)
    
    /// copies the content of 'src' to 'dst'
    let inline copy (src : NativeTensor4<'a>) (dst : NativeTensor4<'a>) = src.CopyTo(dst)
    
    /// copies the content of 'src' to 'dst' by applying the given function
    let inline copyWith (f : 'a -> 'b) (src : NativeTensor4<'a>) (dst : NativeTensor4<'b>) = src.CopyTo(dst, f)
    
    /// temporarily pins a Tensor4 making it available as NativeTensor4
    let using (m : Tensor4<'a>) (f : NativeTensor4<'a> -> 'b) = NativeTensor4<'a>.Using(m, f)


