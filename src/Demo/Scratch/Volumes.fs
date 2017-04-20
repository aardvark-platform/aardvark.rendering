namespace Scratch

open System
open System.IO
open System.IO.MemoryMappedFiles
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Microsoft.FSharp.NativeInterop
open System.Threading
open System.Threading.Tasks

#nowarn "9"


module NewestImpl =
    
    type RawVolume<'a when 'a : unmanaged> private(file : MemoryMappedFile, size : V3i, channels : int, capacity : int64, access : MemoryMappedFileAccess) =
        static let sa = sizeof<'a> |> nativeint
        
        let view = file.CreateViewAccessor(0L, capacity, access)
        let ptr = view.SafeMemoryMappedViewHandle.DangerousGetHandle()
        let sliceSize = nativeint size.X * nativeint size.Y * nativeint channels * sa

        let sliceInfo =
            VolumeInfo(
                0L,
                V3l(size.X, size.Y, channels),
                V3l(int64 channels, int64 size.X * int64 channels, 1L)
            )

        member x.Size = size

        member x.Count = size.Z

        member x.Item
            with get (z : int) =
                if z < 0 || z >= size.Z then failwithf "[RawVolume] z-index out of bounds %d vs [0,%d]" z (size.Z - 1)
                let pData = ptr + nativeint z * sliceSize

                let data : 'a[] = Array.zeroCreate (int sliceSize)
                let gc = GCHandle.Alloc(data, GCHandleType.Pinned)
                try
                    Marshal.Copy(pData, gc.AddrOfPinnedObject(), sliceSize)
                    Volume<'a>(data, sliceInfo)
                finally
                    gc.Free()

            and set (z : int) (src : Volume<'a>) =
                if z < 0 || z >= size.Z then failwithf "[RawVolume] z-index out of bounds %d vs [0,%d]" z (size.Z - 1)
                let pData = ptr + nativeint z * sliceSize

                let dst = NativeVolume<'a>(NativePtr.ofNativeInt pData, sliceInfo)
                if dst.Size <> src.Size then failwithf "[RawVolume] cannot set slice { expected-size: %A; input-size: %A }" dst.Size src.Size

                NativeVolume.using src (fun pSrc ->
                    pSrc.CopyTo(dst)
                )
        
        member x.Dispose() =
            view.Dispose()
            file.Dispose()

        interface IDisposable with
            member x.Dispose() = x.Dispose()

        static member OpenRead(file : string, size : V3i) =
            if File.Exists file then
                let info = FileInfo(file)

                let singleChannelSize = int64 size.X * int64 size.Y * int64 size.Z * int64 sa
                if info.Length % singleChannelSize <> 0L then
                    failwithf "[RawVolume] unexpected file size %A" info.Length

                let channels = int (info.Length / singleChannelSize)
                let handle = MemoryMappedFile.CreateFromFile(file, FileMode.Open, null, info.Length, MemoryMappedFileAccess.Read)
                new RawVolume<'a>(handle, size, channels, info.Length, MemoryMappedFileAccess.Read)
            else
                failwithf "[RawVolume] cannot find file %A" file

    type RawVolume =
        static member inline OpenRead<'a when 'a : unmanaged>(file : string, size : V3i) =
            RawVolume<'a>.OpenRead(file, size)


    [<StructLayout(LayoutKind.Sequential, Size = 128)>]
    type StoreHeader =
        struct
            val mutable public Magic        : Guid
            val mutable public Version      : int
            val mutable public Size         : V3i
            val mutable public InputSize    : V3i
            val mutable public Channels     : int
            val mutable public MipMapLevels : int
            val mutable public BrickSize    : V3i
            val mutable public MinValue     : uint64
            val mutable public MaxValue     : uint64
        end

    type private pstoreheader<'a when 'a : unmanaged>(ptr : nativeptr<StoreHeader>) =

        static member SizeInBytes =
            nativeint sizeof<StoreHeader>

        member x.Magic
            with get() : Guid = NativePtr.read (NativePtr.ofNativeInt (NativePtr.toNativeInt ptr + 0n))
            and set (v : Guid) = NativePtr.write (NativePtr.ofNativeInt (NativePtr.toNativeInt ptr + 0n)) v

        member x.Version
            with get() : int = NativePtr.read (NativePtr.ofNativeInt (NativePtr.toNativeInt ptr + 16n))
            and set (v : int) = NativePtr.write (NativePtr.ofNativeInt (NativePtr.toNativeInt ptr + 16n)) v

        member x.Size
            with get() : V3i = NativePtr.read (NativePtr.ofNativeInt (NativePtr.toNativeInt ptr + 20n))
            and set (v : V3i) = NativePtr.write (NativePtr.ofNativeInt (NativePtr.toNativeInt ptr + 20n)) v

        member x.InputSize
            with get() : V3i = NativePtr.read (NativePtr.ofNativeInt (NativePtr.toNativeInt ptr + 32n))
            and set (v : V3i) = NativePtr.write (NativePtr.ofNativeInt (NativePtr.toNativeInt ptr + 32n)) v
            
        member x.Channels
            with get() : int = NativePtr.read (NativePtr.ofNativeInt (NativePtr.toNativeInt ptr + 44n))
            and set (v : int) = NativePtr.write (NativePtr.ofNativeInt (NativePtr.toNativeInt ptr + 44n)) v

        member x.MipMapLevels
            with get() : int= NativePtr.read (NativePtr.ofNativeInt (NativePtr.toNativeInt ptr + 48n))
            and set (v : int) = NativePtr.write (NativePtr.ofNativeInt (NativePtr.toNativeInt ptr + 48n)) v

        member x.BrickSize
            with get() : V3i= NativePtr.read (NativePtr.ofNativeInt (NativePtr.toNativeInt ptr + 52n))
            and set (v : V3i) = NativePtr.write (NativePtr.ofNativeInt (NativePtr.toNativeInt ptr + 52n)) v

        member x.MinValue
            with get() : 'a = NativePtr.read (NativePtr.ofNativeInt (NativePtr.toNativeInt ptr + 64n))
            and set (v : 'a) = NativePtr.write (NativePtr.ofNativeInt (NativePtr.toNativeInt ptr + 64n)) v

        member x.MaxValue
            with get() : 'a = NativePtr.read (NativePtr.ofNativeInt (NativePtr.toNativeInt ptr + 72n))
            and set (v : 'a) = NativePtr.write (NativePtr.ofNativeInt (NativePtr.toNativeInt ptr + 72n)) v
            
    type private phistogram<'a when 'a : unmanaged>(ptr : nativeptr<uint32>) =
        static let cnt = 1n <<< (8 * sizeof<'a>)

        static member SizeInBytes = cnt * nativeint sizeof<uint32>

        member x.Array = 
            let dst : int[] = Array.zeroCreate (int cnt)
            let gc = GCHandle.Alloc(dst, GCHandleType.Pinned)
            try
                Marshal.Copy(NativePtr.toNativeInt ptr, gc.AddrOfPinnedObject(), cnt * 4n)
                dst
            finally
                gc.Free()

        member x.Count = int cnt

        member x.Item
            with get(i : int) = NativePtr.get ptr i
            and set (i : int) (v : uint32) = NativePtr.set ptr i v


    let private withBorder (size : V2i) (v : Volume<'a>) =
        let vSize = V3i v.Size
        if vSize.XY = size then 
            v
        else
            let data = Array.zeroCreate (size.X * size.Y * vSize.Z)
            let info = VolumeInfo(0L, V3l(size.X, size.Y, vSize.Z), V3l(int64 vSize.Z, int64 vSize.Z * int64 size.X, 1L))
            let res = Volume<'a>(data, info)

            let l = (size - vSize.XY) / 2
            let h = size - vSize.XY - l
            res.SubVolume(V3l(l.X, l.Y, 0), v.Size).Set(v) |> ignore
            res.ReplicateBorder(Border2l(int64 l.X, int64 l.Y, int64 h.X, int64 h.Y))
            res

    type Tensor4<'a> with
        static member Create(size : V4l) =
            let arr = Array.zeroCreate (int (size.X * size.Y * size.Z * size.W))
            Tensor4<'a>(arr, Tensor4Info(0L, V4l(size.X, size.Y, size.Z, size.W), V4l(size.W, size.W * size.X, size.W * size.X * size.Y, 1L)))

        static member Create(size : V3i, channels : int) =
            let arr = Array.zeroCreate (size.X * size.Y * size.Z * channels)
            Tensor4<'a>(arr, Tensor4Info(0L, V4l(size.X, size.Y, size.Z, channels), V4l(int64 channels, int64 channels * int64 size.X, int64 channels * int64 size.X * int64 size.Y, 1L)))

    module NativeTensor4 =
        let toManaged (x : NativeTensor4<'a>) =
            let arr = Array.zeroCreate (int (x.SX * x.SY * x.SZ * x.SW))
            let t = Tensor4<'a>(arr, Tensor4Info(0L, x.Size, V4l(x.SW, x.SW * x.SX, x.SW * x.SX * x.SY, 1L)))

            NativeTensor4.using t (fun dst -> x.CopyTo dst)
            t

    type VolumeStore<'a when 'a : unmanaged> private(file : MemoryMappedFile, view : MemoryMappedViewAccessor, header : pstoreheader<'a>, histogram : phistogram<'a>, pData : nativeint) =
        static let currentVersion = 1
        static let brickSize = V3i(128,128,128)
        static let magic = Guid "fdc55c47-ef98-41bb-995b-450067d0a292"
        static let sa = nativeint sizeof<'a>



        static let half (v : V3i) =
            V3i( max 1 (v.X / 2), max 1 (v.Y / 2), max 1 (v.Z / 2) )

        static let fileSize (size : V3i) (channels : int) (levels : int) =
            let rec acc (l : int) (s : V3i) (total : nativeint) =
                if l < 0 then
                    total
                else
                    let t = total + nativeint s.X * nativeint s.Y * nativeint channels * nativeint s.Z * sa
                    acc (l - 1) (half s) t

            let fix = pstoreheader<'a>.SizeInBytes + phistogram<'a>.SizeInBytes
            acc (levels - 1) size fix

        static let allCoords (size : V3i) =
            [|
                for z in 0 .. size.Z - 1 do
                    for y in 0 .. size.Y - 1 do
                        for x in 0 .. size.X - 1 do
                            yield V3i(x,y,z)
            |]


        static let halfSize (aggregate : 'a[] -> 'a) (src : Tensor4<'a>)  =
            let half = V4l(src.SX / 2L, src.SY / 2L, src.SZ / 2L, src.SW)
            let srcPart = src.SubTensor4(V4l.Zero, half, 2L * src.Delta)
            let p = Tensor4<'a>.Create(half)
            let d = src.Delta

            p.SetByIndex(srcPart, fun (si : int64) ->
                aggregate [|
                    src.[si]
                    src.[si + d.X]
                    src.[si + d.Y]
                    src.[si + d.X + d.Y]
                    src.[si + d.Z]
                    src.[si + d.X + d.Z]
                    src.[si + d.Y + d.Z]
                    src.[si + d.X + d.Y + d.Z]
                |]
            )

        let size = header.Size
        let inputSize = header.InputSize
        let channels = header.Channels
        let mipMapLevels = header.MipMapLevels
        let brickSize = header.BrickSize


        let brickInfo =
            Tensor4Info(
                0L,
                V4l(brickSize.X, brickSize.Y, brickSize.Z, channels),
                V4l(int64 channels, int64 channels * int64 brickSize.X, int64 channels * int64 brickSize.X * int64 brickSize.Y, 1L)
            )
        
        let brickDelta (cnt : V3i) = 
            let sb = nativeint brickSize.X * nativeint brickSize.Y * nativeint brickSize.Z * nativeint channels * sa |> int64
            V3l(sb, int64 cnt.X * sb, int64 cnt.X * int64 cnt.Y * sb)

        let ptrAndSize (level : int) =
            let rec add (s : V3i) (o : nativeint) (l : int) =
                if l <= 0 then 
                    o, s
                else
                    let d = nativeint s.X * nativeint s.Y * nativeint s.Z * sa
                    add (half s) (o + d) (l - 1)

            add size pData level

        let isBricked (s : V3i) =
            s.X % brickSize.X = 0 && s.Y % brickSize.Y = 0 && s.Z % brickSize.Z = 0

        member x.Size = size
        member x.InputSize = inputSize
        member x.Channels = channels
        member x.MipMapLevels = mipMapLevels
        member x.BrickSize = brickSize

        member x.Histogram =
            histogram.Array


        member x.Brick(level : int, index : V3i) =
            if level < 0 || level >= mipMapLevels then
                failwithf "[Volume] level %d out of range [0,%d]" level (mipMapLevels - 1)

            let ptr, size = ptrAndSize level

            if isBricked size then
                let bricks = size / brickSize
                let brickDelta = brickDelta bricks

                if index.AnySmaller 0 || index.AnyGreaterOrEqual bricks then
                    failwithf "[Volume] brick %A is not available on level %d" index level

                let offset = Vec.dot brickDelta (V3l index) |> nativeint
                let pBrick = ptr + offset

                NativeTensor4<'a>(NativePtr.ofNativeInt pBrick, brickInfo)
            else
                if index.AnyDifferent 0 then
                    failwithf "[Volume] brick %A is not available on level %d" index level
                    
                let info =
                    Tensor4Info(
                        0L,
                        V4l(size.X, size.Y, size.Z, channels),
                        V4l(int64 channels, int64 channels * int64 size.X, int64 channels * int64 size.X * int64 size.Y, 1L)
                    )

                NativeTensor4<'a>(NativePtr.ofNativeInt ptr, info)

        member x.BrickCount(level : int) =
            if level < 0 || level >= mipMapLevels then
                failwithf "[Volume] level %d out of range [0,%d]" level (mipMapLevels - 1)

            let ptr, size = ptrAndSize level
            if isBricked size then size / brickSize
            else V3i.III

        member x.GetBricks(level : int, offset : V3i, size : V3i) =
            let bricks = x.BrickCount level
            if bricks = V3i.III then
                let b = x.Brick(level, V3i.Zero)

                let region =
                    if offset = V3i.Zero && size = V3i b.Size.XYZ then b
                    else b.SubTensor4(V4l(offset.X, offset.Y, offset.Z, 0), V4l(size.X, size.Y, size.Z, channels))

                [V3i.Zero, region]
            else
                let lastPixelE = offset + size
                let firstBrick = offset / brickSize
                let lastBrick =
                    if lastPixelE.X % brickSize.X = 0 && lastPixelE.Y % brickSize.Y = 0 && lastPixelE.Z % brickSize.Z = 0 then lastPixelE / brickSize
                    else V3i.III + lastPixelE / brickSize

                [
                    for bz in firstBrick.Z .. lastBrick.Z do
                        for by in firstBrick.Y .. lastBrick.Y do
                            for bx in firstBrick.X .. lastBrick.X do
                                let bi = V3i(bx,by,bz)
                                let bo = brickSize * bi
                            
                                let globalOffset = V3i(max bo.X offset.X, max bo.Y offset.Y, max bo.Z offset.Z)
                                let inBrickOffset = globalOffset - bo
                                let targetOffset = globalOffset - offset

                                let inBrickSize = 
                                    let ble = bo + brickSize
                                    let ble = V3i(min lastPixelE.X ble.X, min lastPixelE.Y ble.Y, min lastPixelE.Z ble.Z)
                                    ble - bo

                                if inBrickSize.AllGreater 0 then
                                    let b = x.Brick(level, bi)
                                    yield targetOffset, b.SubTensor4(V4l(inBrickOffset.X, inBrickOffset.Y, inBrickOffset.Z, 0), V4l(inBrickSize.X, inBrickSize.Y, inBrickSize.Z, channels))


                ]



        member x.SetSlice(level : int, z : int, data : Volume<'a>) =
            if level < 0 || level >= mipMapLevels then
                failwithf "[Volume] level %d out of range [0,%d]" level (mipMapLevels - 1)
            
            if z < 0 || z >= size.Z then
                failwithf "[Volume] z-index %d out of range [0,%d]" z (size.Z - 1)
                
            let ptr, size = ptrAndSize level
            if data.Size.XY <> V2l size.XY || data.SZ <> int64 channels then
                failwithf "[Volume] invalid slice-size %A (expected %A)" data.Size (V3i(size.XY, channels))
                
            if z < 0 || z >= size.Z then
                failwithf "[Volume] invalid slice-index %A [0,%d]" z (size.Z - 1)
                
            if isBricked size then
                let bricks = size / brickSize
                let bz = z / brickSize.Z
                let z = z % brickSize.Z
                let brickDelta = brickDelta bricks
                let brickSliceSize = nativeint brickSize.X * nativeint brickSize.Y * nativeint channels * sa
                
                let info = 
                    VolumeInfo(
                        0L,
                        V3l(brickSize.X, brickSize.Y, channels),
                        V3l(int64 channels, int64 brickSize.X * int64 channels, 1L)
                    )

                for by in 0 .. bricks.Y - 1 do
                    for bx in 0 .. bricks.X - 1 do
                        let i = V3l(bx,by,bz)
                        let offset = Vec.dot brickDelta i |> nativeint
                        let pBrickSlice = ptr + offset + nativeint z * brickSliceSize

                        let dst = NativeVolume<'a>(NativePtr.ofNativeInt pBrickSlice, info)
                        let src = 
                            data.SubVolume(
                                V3l(i.X * int64 brickSize.X, i.Y * int64 brickSize.Y, 0L),
                                V3l(brickSize.X, brickSize.Y, channels)
                            )

                        NativeVolume.using src (fun src -> src.CopyTo dst)

            else
                let sliceSize = nativeint size.X * nativeint size.Y * nativeint channels * sa
                let pSlice = ptr + nativeint z * sliceSize

                let info = 
                    VolumeInfo(
                        0L,
                        V3l(size.X, size.Y, channels),
                        V3l(int64 channels, int64 size.X * int64 channels, 1L)
                    )

                let dst = NativeVolume<'a>(NativePtr.ofNativeInt pSlice, info)
                NativeVolume.using data (fun src -> src.CopyTo dst)

        member x.Set(input : RawVolume<'a>) =
            let l = (size - input.Size) / 2
            let h = size - input.Size - l

            Log.startTimed "copy level 0"

            let hist : uint32[] = Array.zeroCreate (1 <<< (8 * sizeof<'a>))
            let toInt32 = PrimitiveValueConverter.converter<'a, int>

            let mutable sz = 0
            let zero = input.[0] |> withBorder size.XY
            for _ in 1 .. l.Z do
                x.SetSlice(0, sz, zero)
                sz <- sz + 1
                x.Flush()
                Report.Progress(float sz / float size.Z)


            let withBorder' (size : V2i) (v : Volume<'a>) =
                let vSize = V3i v.Size
                if vSize.XY = size then 
                    v
                else
                    let data = Array.zeroCreate (size.X * size.Y * vSize.Z)
                    let info = VolumeInfo(0L, V3l(size.X, size.Y, vSize.Z), V3l(int64 vSize.Z, int64 vSize.Z * int64 size.X, 1L))
                    let res = Volume<'a>(data, info)

                    let l = (size - vSize.XY) / 2
                    let h = size - vSize.XY - l
                    res.SubVolume(V3l(l.X, l.Y, 0), v.Size).SetByIndex(v, fun (vi : int64) ->
                        let v = v.Data.[int vi]
                        let vi = toInt32 v
                        let r = &hist.[vi]
                        r <- r + 1u


                        v
                    ) |> ignore
                    res.ReplicateBorder(Border2l(int64 l.X, int64 l.Y, int64 h.X, int64 h.Y))
                    res

            for z in 0 .. input.Size.Z - 1 do
                let slice = input.[z] |> withBorder' size.XY

                x.SetSlice(0, sz, slice)
                sz <- sz + 1
                x.Flush()
                Report.Progress(float sz / float size.Z)
            
            let zero = input.[input.Count - 1] |> withBorder size.XY
            for _ in 1 .. h.Z do
                x.SetSlice(0, sz, zero)
                sz <- sz + 1
                x.Flush()
                Report.Progress(float sz / float size.Z)

            for i in 0 .. hist.Length - 1 do
                histogram.[i] <- hist.[i]

            x.Flush()

            Log.stop()

        member x.GenerateMipMaps(aggregate : 'a[] -> 'a) =
            let mutable srcSize = size
            let mutable dstSize = half size

            Log.startTimed "generate mipmaps"

            for dstLevel in 1 .. mipMapLevels - 1 do
                Log.startTimed "generate level %d" dstLevel
                let srcLevel = dstLevel - 1
                
                if isBricked srcSize && isBricked dstSize then
                    // both bricked
                    let dstBricks = dstSize / brickSize

                    let mutable index = 0
                    let totalBricks = dstBricks.X * dstBricks.Y * dstBricks.Z
                    for dz in 0 .. dstBricks.Z - 1 do
                        for dy in 0 .. dstBricks.Y - 1 do
                            for dx in 0 .. dstBricks.X - 1 do
                                let dstBrick = V3i(dx,dy,dz)
                                let dst = x.Brick(dstLevel, dstBrick)
                                
                                for oz in 0 .. 1 do
                                    for oy in 0 .. 1 do
                                        for ox in 0 .. 1 do
                                            let o = V3i(ox,oy,oz)
                                            let srcPart = 
                                                x.Brick(srcLevel, 2 * dstBrick + o) 
                                                    |> NativeTensor4.toManaged 
                                                    |> halfSize aggregate
                                            
                                            let half = brickSize / 2
                                            let dstPart = 
                                                dst.SubTensor4(
                                                    V4l(ox * half.X, oy * half.Y, oz * half.Z, 0),
                                                    V4l(half.X, half.Y, half.Z, channels)
                                                )

                                            NativeTensor4.using srcPart (fun srcPart -> srcPart.CopyTo dstPart)
                                
                                index <- index + 1
                                Report.Progress(float index / float totalBricks)

                                x.Flush()

                elif isBricked srcSize then
                    // src bricked
                    let srcBricks = srcSize / brickSize
                    let overall = Tensor4<'a>.Create(srcSize, channels)
                    for bz in 0 .. srcBricks.Z - 1 do
                        for by in 0 .. srcBricks.Y - 1 do
                            for bx in 0 .. srcBricks.X - 1 do
                                let srcBrick = V3i(bx,by,bz)
                                
                                let src = x.Brick(srcLevel, srcBrick)
                                let dst = 
                                    overall.SubTensor4(
                                        V4l(bx * brickSize.X, by * brickSize.Y, bz * brickSize.Z, 0), 
                                        V4l(brickSize.X, brickSize.Y, brickSize.Z, channels)
                                    )

                                NativeTensor4.using dst (fun dst -> src.CopyTo dst)

                    let src = overall |> halfSize aggregate
                    let dst = x.Brick(dstLevel, V3i.Zero)
                    NativeTensor4.using src (fun src -> src.CopyTo dst)
                    x.Flush()

                else
                    // none bricked
                    let src = x.Brick(srcLevel, V3i.Zero) |> NativeTensor4.toManaged |> halfSize aggregate
                    let dst = x.Brick(dstLevel, V3i.Zero)
                    NativeTensor4.using src (fun src -> src.CopyTo dst)
                    x.Flush()



                srcSize <- dstSize
                dstSize <- half srcSize
                Log.stop()

            
            Log.stop()


        member x.SetMapBrick(other : VolumeStore<'b>, mapping : int -> Tensor4<'b> -> Tensor4<'a>) =
            let mutable size = size

            let totalSize (size : V3i) (channels : int) (levels : int) =
                let rec acc (l : int) (s : V3i) (total : nativeint) =
                    if l < 0 then
                        total
                    else
                        let t = total + nativeint s.X * nativeint s.Y * nativeint channels * nativeint s.Z
                        acc (l - 1) (half s) t
                acc (levels - 1) size 0n
                
            let totalPixels = int64 (totalSize size channels mipMapLevels)
            let mutable processed = 0L

            for l in 0 .. mipMapLevels - 1 do
                if isBricked size then
                    let bricks = size / brickSize

                    let caller = Thread.CurrentThread.ManagedThreadId
                    Parallel.ForEach(allCoords bricks, fun b ->
                        let src = 
                            other.Brick(l, b)
                                |> NativeTensor4.toManaged
                                |> mapping l

                        let dst = x.Brick(l, b)

                        NativeTensor4.using src (fun src -> src.CopyTo dst)

                        Interlocked.Add(&processed, src.SX * src.SY * src.SZ * src.SW) |> ignore
                        if Thread.CurrentThread.ManagedThreadId = caller then
                            
                            Report.Progress(float processed / float totalPixels)
                    ) |> ignore

                else
                    let src = 
                        other.Brick(l, V3i.Zero)
                            |> NativeTensor4.toManaged
                            |> mapping l

                    let dst = x.Brick(l, V3i.Zero)
                    NativeTensor4.using src (fun src -> src.CopyTo dst)
                    x.Flush()

                    processed <- processed + src.SX * src.SY * src.SZ * src.SW
                    Report.Progress(float processed / float totalPixels)

                size <- half size

        member x.SetMap(other : VolumeStore<'b>, mapping : int -> V3i -> 'b -> 'a) =
            x.SetMapBrick(other, fun (level : int) (src : Tensor4<'b>) ->
                let dst = Tensor4<'a>.Create(src.Size)
                dst.ForeachXYZIndex(fun (x : int64) (y : int64) (z : int64) (i : int64) ->
                    let value = mapping level (V3i(int x, int y, int z)) src.[i]
                    dst.Data.[int i] <- value
                )   
                dst
            )

        member x.Flush() = 
            view.Flush()

        member x.Dispose() =
            view.Dispose()
            file.Dispose()

        interface IDisposable with
            member x.Dispose() = x.Dispose()


        static member CreateNew(file : string, size : V3i, mipMapLevels : int) =
            if File.Exists file then File.Delete file

            let realSize = V3i(Fun.NextPowerOfTwo size.X, Fun.NextPowerOfTwo size.Y, Fun.NextPowerOfTwo size.Z)
            let capacity = fileSize realSize 1 mipMapLevels


            let handle = MemoryMappedFile.CreateFromFile(file, FileMode.CreateNew, null, int64 capacity, MemoryMappedFileAccess.ReadWrite)
            let view = handle.CreateViewAccessor()
            let filePtr = view.SafeMemoryMappedViewHandle.DangerousGetHandle()

            let header = pstoreheader<'a>(NativePtr.ofNativeInt filePtr)
            let histogram = phistogram<'a>(NativePtr.ofNativeInt<uint32> (filePtr + pstoreheader<'a>.SizeInBytes))
            let pData = filePtr + pstoreheader<'a>.SizeInBytes + phistogram<'a>.SizeInBytes

            header.Magic <- magic
            header.Version <- currentVersion
            header.Size <- realSize
            header.InputSize <- size
            header.Channels <- 1
            header.MipMapLevels <- mipMapLevels
            header.BrickSize <- brickSize
            header.MinValue <- Unchecked.defaultof<'a>
            header.MaxValue <- Unchecked.defaultof<'a>

            for i in 0 .. histogram.Count - 1 do
                histogram.[i] <- 0u

            view.Flush()

            new VolumeStore<'a>(handle, view, header, histogram, pData)

        static member CreateNew(file : string, size : V3i, mipMaps : bool) =
            let levels =
                if mipMaps then 1 + int(floor(Fun.Log2 (min size.X (min size.Y size.Z))))
                else 1

            VolumeStore<'a>.CreateNew(file, size, levels)

        static member Open(file : string) =
            if File.Exists file then
                let info = FileInfo(file)

                let handle = MemoryMappedFile.CreateFromFile(file, FileMode.Open, null, info.Length, MemoryMappedFileAccess.ReadWrite)
                let view = handle.CreateViewAccessor()
                let filePtr = view.SafeMemoryMappedViewHandle.DangerousGetHandle()

                let header = pstoreheader<'a>(NativePtr.ofNativeInt filePtr)
                let histogram = phistogram<'a>(NativePtr.ofNativeInt (filePtr + pstoreheader<'a>.SizeInBytes))
                let pData = filePtr + pstoreheader<'a>.SizeInBytes + phistogram<'a>.SizeInBytes

                if header.Magic <> magic || header.Version <> currentVersion then
                    view.Dispose()
                    handle.Dispose()
                    failwithf "[Volume] invalid volume file %A" file


                new VolumeStore<'a>(handle, view, header, histogram, pData)
            else
                failwithf "[Volume] cannot open file %A" file

    type VolumeStore =
        static member CreateNew<'a when 'a : unmanaged> (file : string, size : V3i, mipMapLevels : int) =
            VolumeStore<'a>.CreateNew(file, size, mipMapLevels)

        static member CreateNew<'a when 'a : unmanaged> (file : string, size : V3i, mipMaps : bool) =
            VolumeStore<'a>.CreateNew(file, size, mipMaps)

        static member Open<'a when 'a : unmanaged> (file : string) = 
            VolumeStore<'a>.Open file


    [<AutoOpen>]
    module ``GL Extensions`` =
        open Aardvark.Rendering.GL
        open OpenTK.Graphics.OpenGL4

        type SparseVolumeTexture<'a when 'a : unmanaged>(t : SparseTexture, data : VolumeStore<'a>) =
            inherit SparseTexture(t.Context, t.Handle, t.Dimension, t.MipMapLevels, t.Multisamples, t.Size, t.Count, t.Format, t.PageSize, t.SparseLevels)

            member private x.EnableDebug() = ()
//                match ContextHandle.Current with
//                    | Some v -> v.AttachDebugOutputIfNeeded()
//                    | None -> Report.Warn("No active context handle in RenderTask.Run")
//                GL.Enable EnableCap.DebugOutput
                
            member x.Commit(level : int, offset : V3i, size : V3i) =
                use __ = x.Context.ResourceLock
                x.EnableDebug()
                x.Commit(level, Box3i(offset, offset + size - V3i.III))

            member x.Decommit(level : int, offset : V3i, size : V3i) =
                use __ = x.Context.ResourceLock
                x.EnableDebug()
                x.Decommit(level, Box3i(offset, offset + size - V3i.III))

            member x.Commit(level : int) =
                x.Commit(level, V3i.Zero, x.GetSize level)

            member x.Decommit(level : int) =
                x.Decommit(level, V3i.Zero, x.GetSize level)

            member x.Upload(level : int, offset : V3i, size : V3i) =
                use __ = x.Context.ResourceLock
                x.EnableDebug()

                for (offset, brick) in data.GetBricks(level, offset, size) do
                    x.Upload(level, offset, brick)

                
            member x.Download(level : int, offset : V3i, size : V3i) =
                use __ = x.Context.ResourceLock
                x.EnableDebug()

                for (offset, brick) in data.GetBricks(level, offset, size) do
                    x.Download(level, offset, brick)

            member x.Upload(level : int) =
                x.Upload(level, V3i.Zero, x.GetSize level)

            member x.Download(level : int) =
                x.Download(level, V3i.Zero, x.GetSize level)

            member x.MakeResident(level : int, offset : V3i, size : V3i) =
                use __ = x.Context.ResourceLock
                x.Commit(level, offset, size)
                x.Upload(level, offset, size)

            member x.MakeResident(level : int) =
                x.MakeResident(level, V3i.Zero, x.GetSize level)
                

        type Context with
            member x.CreateSparseVolume(data : VolumeStore<uint16>) =
                use __ = x.ResourceLock
                let t = x.CreateSparseTexture(data.Size, TextureFormat.R16, data.MipMapLevels)

                for l in t.SparseLevels .. data.MipMapLevels-1 do
                    t.Upload(l, V3i.Zero, data.Brick(l, V3i.Zero))


                SparseVolumeTexture<uint16>(t, data)



    let preprocess (file : string) (input : RawVolume<uint16>) =
        let store = VolumeStore.CreateNew<uint16>(file, input.Size, true)
        try
            Log.startTimed "preprocessing"

            store.Set(input)

            store.GenerateMipMaps(fun arr ->
                let mutable sum = 0.0
                for a in arr do
                    sum <- sum + float a
                sum / 8.0 |> round |> uint16
            )

            Log.stop()
        finally
            store.Dispose()

        VolumeStore.Open<uint16>(file)

    let test() =
        // create
        use input = RawVolume.OpenRead<uint16>(@"C:\Users\Schorsch\Desktop\Testdatensatz_600x600x1000px.raw", V3i(600, 600, 1000))
        use store =  input |> preprocess @"E:\blubber2.store"

        // open
        //use store = VolumeStore.Open<uint16> @"E:\blubber.store"

        // store the histogram
        let csv = store.Histogram |> Seq.mapi (fun v c -> sprintf "%d;%d" v c) |> String.concat "\r\n"
        File.WriteAllText(@"C:\Users\Schorsch\Desktop\hist.csv", csv)

        // store one brick as slices
        let bi = V3i(0,0,0) 
        let t = store.Brick(4, bi) |> NativeTensor4.toManaged
        
        for z in 0 .. int t.SZ - 1 do
            let v = t.SubXYWVolume(int64 z)
            let img = PixImage<uint16>(Col.Format.Gray, v).ToImageLayout()
            img.SaveAsImage (sprintf @"C:\Users\Schorsch\Desktop\brick\%d.jpg" z)



module VolumeTest =
    open Aardvark.Base.Incremental
    open Aardvark.SceneGraph
    open Aardvark.Application
    open Aardvark.Application.WinForms
    open Aardvark.Rendering.GL

    open NewestImpl

    [<ReflectedDefinition>]
    module Shader =
        open FShade

        let volumeTexture =
            sampler3d {
                texture uniform?VolumeTexture
                filter Filter.MinMagLinearMipPoint
                addressU WrapMode.Clamp
                addressV WrapMode.Clamp
                addressW WrapMode.Clamp
            }

        let pickRay (p : V2d) =
            let pn = uniform.ViewProjTrafoInv * V4d(p.X, p.Y, 0.0, 1.0)
            let nearPlanePoint = pn.XYZ / pn.W
            Vec.normalize nearPlanePoint

        type Vertex =
            {
                [<Position>]
                pos : V4d

                [<Semantic("ViewPosition")>]
                viewPos : V4d

                [<Semantic("RayDirection")>]
                dir : V3d

                [<Semantic("CubeCoord")>]
                cubeCoord : V3d

            }

        let hsv2rgb (h : float) (s : float) (v : float) =
            let s = clamp 0.0 1.0 s
            let v = clamp 0.0 1.0 v

            let h = h % 1.0
            let h = if h < 0.0 then h + 1.0 else h
            let hi = floor ( h * 6.0 ) |> int
            let f = h / 6.0 - float hi
            let p = v * (1.0 - s)
            let q = v * (1.0 - s * f)
            let t = v * (1.0 - s * ( 1.0 - f ))
            match hi with
                | 1 -> V3d(q,v,p)
                | 2 -> V3d(p,v,t)
                | 3 -> V3d(p,q,v)
                | 4 -> V3d(t,p,v)
                | 5 -> V3d(v,p,q)
                | _ -> V3d(v,t,p)

        let vertex (v : Vertex) =
            vertex {
                let cameraInModel = uniform.ModelTrafoInv.TransformPos uniform.CameraLocation
                let wp = uniform.ModelTrafo * v.pos
                let p = uniform.ViewProjTrafo * wp
                return {
                    pos = p
                    viewPos = p
                    dir = cameraInModel - v.pos.XYZ
                    cubeCoord = v.pos.XYZ
                }
            }

        let isAir (v : float) =
            v < 0.25

        let isVisible (p : V3d) =
            p.X >= -1.0 && p.X < 1.0 && p.Y >= -1.0 && p.Y <= 1.0 && p.Z >= -1.0 && p.Z <= 1.0

        let project (m : M44d) (v : V3d) =
            let r = m * V4d(v, 1.0)
            r.XYZ / r.W

        let fragment (v : Vertex) =
            fragment {
                let size = volumeTexture.Size / 2
                
                let dir = Vec.normalize v.dir
                let absDir = V3d(abs dir.X , abs dir.Y, abs dir.Z)

                let dirMax1 =
                    if absDir.X > absDir.Y then
                        if absDir.X > absDir.Z then dir / absDir.X
                        else dir / absDir.Z
                    else
                        if absDir.Y > absDir.Z then dir / absDir.Y
                        else dir / absDir.Z

                let lengthInPixels = Vec.length (dirMax1 * V3d size)
                let steps = 2 * (lengthInPixels |> ceil |> int)
                let step = dirMax1 / float steps




                let mutable near = steps
                let mutable far = 0
                
                let mutable c = v.cubeCoord + dirMax1
                let mutable pp = project uniform.ModelViewProjTrafo c
                while not (isVisible pp) && near >= 0 do
                    c <- c - step
                    pp <- project uniform.ModelViewProjTrafo c
                    near <- near - 1
                
                
                let mutable c = v.cubeCoord
                let mutable pp = project uniform.ModelViewProjTrafo c
                while not (isVisible pp) && far <= steps do
                    c <- c + step
                    pp <- project uniform.ModelViewProjTrafo c
                    far <- far + 1

                
                
                // if there is material in between
                let mutable value = V3d.Zero
                if near > far then
                    let mutable c = v.cubeCoord + step * float far
                    let f : float = uniform?ScaleFactor

                    let mutable alpha = 0.0
                    let mutable res = V3d.Zero
                    for i in far .. near do
                        let v = volumeTexture.SampleLevel(c, 1.0).X
                        res <- V3d.III * v + res

//                        let v2 = volumeTexture.SampleLevel(c, 2.0).X
//                        let v = volumeTexture.SampleLevel(c, 1.0).X
//                        if not (isAir v2) then
//                            let v = if isAir v then 0.0 else 1.0
//                            res <- V3d(v, 1.0, 1.0) + res



//                        if isAir v && not (isAir (volumeTexture.SampleLevel(c, 2.0).X)) then
////
////                            let mutable noAirLevel = 2.0
////                            while isAir (volumeTexture.SampleLevel(c, noAirLevel).X) && noAirLevel < 5.0 do
////                                noAirLevel <- noAirLevel + 1.0
////
////                            if noAirLevel < 5.0 then
//                            //let vi = (noAirLevel - 1.0) / 5.0
//                            let color = V3d.III //hsv2rgb (360.0 * vi) 1.0 1.0
//                            let a = f //(0.25 - v) / 0.25
//
//                            res <- a * color + (1.0 - a) * res
//                            alpha <- a  + (1.0 - a) * alpha
//                            //res <- res + 1.0

                        c <- c + step


                    value <- f * res / 1000.0
                return V4d(value, 1.0)
               
//                    
//                    
//
//
//                let dirInPixels = (Vec.normalize v.dir) * V3d size
//                let absDirInPixels = V3d(abs dirInPixels.X , abs dirInPixels.Y, abs dirInPixels.Z)
//
//
//
//
//
//
//                let dir =
//                    if absDirInPixels.X > absDirInPixels.Y then
//                        if absDirInPixels.X > absDirInPixels.Z then dirInPixels / absDirInPixels.X
//                        else dirInPixels / absDirInPixels.Z
//                    else
//                        if absDirInPixels.Y > absDirInPixels.Z then dirInPixels / absDirInPixels.Y
//                        else dirInPixels / absDirInPixels.Z
//              
//
//                let step = 0.5 * dir / V3d size
//
//                let mutable currentDepth = 0.0
//                let mutable sampleLocation = v.cubeCoord
//                let mutable alpha = 0.0
//                let mutable value = 0.0
//                
//                let mutable steps = 0
//                do
//                    // find far-entry
//                    while sampleLocation.X >= 0.0 && sampleLocation.X <= 1.0 && 
//                          sampleLocation.Y >= 0.0 && sampleLocation.Y <= 1.0 && 
//                          sampleLocation.Z >= 0.0 && sampleLocation.Z <= 1.0 &&
//                          currentDepth >= -1.0 && currentDepth <= 1.0 do
//
//                        let v = volumeTexture.SampleLevel(sampleLocation, 1.0).X
//
//                        if not (isAir v) then
//                            currentDepth <- -100.0
//                        else
//                            sampleLocation <- sampleLocation + step
//                            
//                    let maxLoc = sampleLocation
//
//                    // find near-entry
//                    sampleLocation <- v.cubeCoord + dir
//                    let step = -step
//                    while sampleLocation.X >= 0.0 && sampleLocation.X <= 1.0 && 
//                          sampleLocation.Y >= 0.0 && sampleLocation.Y <= 1.0 && 
//                          sampleLocation.Z >= 0.0 && sampleLocation.Z <= 1.0 &&
//                          currentDepth >= -1.0 && currentDepth <= 1.0 do
//
//                        let v = volumeTexture.SampleLevel(sampleLocation, 1.0).X
//
//                        if not (isAir v) then
//                            currentDepth <- -100.0
//                        else
//                            sampleLocation <- sampleLocation + step
//
//                    let minLoc = sampleLocation
//
//                    sampleLocation <- minLoc
//                    while sampleLocation.X >= 0.0 && sampleLocation.X <= 1.0 && 
//                          sampleLocation.Y >= 0.0 && sampleLocation.Y <= 1.0 && 
//                          sampleLocation.Z >= 0.0 && sampleLocation.Z <= 1.0 &&
//                          currentDepth >= -1.0 && currentDepth <= 1.0 do
//
//
//
////                        if last >= 0.0 && penultimate >= 0.0 then
////                            let d = (v - last) - (last - penultimate) |> abs
////                            if d > 0.015 then
////                                let v = 10.0 * d
////                                let a = 1.0 - exp -(d / 0.02)
////
////                                value <- a * v + (1.0 - a) * value
////                                alpha <- a  + (1.0 - a) * alpha
////
////                        penultimate <- last
////                        last <- v
////
////                        if v < 0.3 then
////                            if entered then
////                                value <- value + 1.0
////                            entered <- false
////                        else
////                            entered <- true
//                        sampleLocation <- sampleLocation + step
//
//                        let view = uniform.ModelViewProjTrafo * V4d(sampleLocation, 1.0)
//                        currentDepth <- view.Z / view.W
//                        steps <- steps + 1
//                let value = value //(sampleLocation - v.cubeCoord).Length /// float steps
//                //let l : float = uniform?MinValue
//                //let h : float = uniform?MaxValue
//
//                //let value = (value - 0.1) / (0.9) |> clamp 0.0 1.0
//
//                return V4d(value, value, value, alpha)
            }


    let openOrCreate (file : string) (input : RawVolume<uint16>) =
        if not (File.Exists file) then
            let store = VolumeStore.CreateNew<uint16>(file, input.Size, true)
            try
                Log.startTimed "preprocessing"

                store.Set(input)

                store.GenerateMipMaps(fun arr ->
                    let mutable sum = 0.0
                    for a in arr do
                        sum <- sum + float a
                    sum / 8.0 |> round |> uint16
                )

                Log.stop()
            finally
                store.Dispose()

        VolumeStore.Open<uint16>(file)

    let run() =
        Ag.initialize()
        Aardvark.Init()

        use app = new OpenGlApplication()
        use win = app.CreateSimpleRenderWindow()
        let sliceFolder = @"C:\Users\Schorsch\Desktop\slices\"

        
        // create
        use input = RawVolume.OpenRead<uint16>(@"C:\Users\Schorsch\Desktop\Testdatensatz_600x600x1000px.raw", V3i(600, 600, 1000))
        use store =  input |> openOrCreate @"E:\blubber2.store"

        let texture = app.Runtime.Context.CreateSparseVolume(store)

        let size = V3d store.Size / float store.Size.NormMax
        let view = CameraView.lookAt (V3d(6,6,6)) V3d.Zero V3d.OOI
        let proj = win.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y))
        let view = view |> DefaultCameraController.control win.Mouse win.Keyboard win.Time



        //texture.MakeResident(0)
        for l in 1 .. 5 do
            texture.MakeResident(l)
            
 


//        let s = texture.Size / 2
//        texture.MakeResident(0) //, V3i.Zero, V3i(s.X / 2, s.Y / 2, s.Z / 2))
//        let data = store.GetLevel(0)
//        let v = PixVolume<uint16>(Col.Format.Gray, data.GetTensor())

        //app.Runtime.Context.CreateTexture(NativeTe
        

        let factor = Mod.init 0.5


        win.Keyboard.KeyDown(Keys.OemPlus).Values.Add(fun _ ->
            transact (fun () -> factor.Value <- factor.Value * 2.0)
        )
        win.Keyboard.KeyDown(Keys.OemMinus).Values.Add(fun _ ->
            transact (fun () -> factor.Value <- factor.Value / 2.0)
        )


        let sg = 
            Sg.box' C4b.Red (Box3d(-size, size))
                |> Sg.scale 5.0
                |> Sg.uniform "ScaleFactor" factor
                |> Sg.uniform "VolumeTexture" (Mod.constant (texture :> ITexture))
                |> Sg.shader {
                    do! Shader.vertex
                    do! Shader.fragment
                   }
                |> Sg.depthTest (Mod.constant DepthTestMode.None)
                |> Sg.cullMode (Mod.constant CullMode.CounterClockwise)
                |> Sg.viewTrafo (view |> Mod.map CameraView.viewTrafo)
                |> Sg.projTrafo (proj |> Mod.map Frustum.projTrafo)

        let task = app.Runtime.CompileRender(win.FramebufferSignature, sg)
        win.RenderTask <- task

        win.Run()
        ()

