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

    module NativeVolume =
        let toManaged (x : NativeVolume<'a>) =
            let arr = Array.zeroCreate (int (x.SX * x.SY * x.SZ))
            let t = Volume<'a>(arr, VolumeInfo(0L, x.Size, V3l(x.SZ, x.SZ * x.SX, 1L)))

            NativeVolume.using t (fun dst -> x.CopyTo dst)
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

        member x.GetSlice(level : int, z : int) =
            if level < 0 || level >= mipMapLevels then
                failwithf "[Volume] level %d out of range [0,%d]" level (mipMapLevels - 1)
            
            let ptr, size = ptrAndSize level
                
            if z < 0 || z >= size.Z then
                failwithf "[Volume] invalid slice-index %A [0,%d]" z (size.Z - 1)
                
            if isBricked size then
                let bricks = size / brickSize
                let bz = z / brickSize.Z
                let z = z % brickSize.Z
                let brickDelta = brickDelta bricks
                let brickSliceSize = nativeint brickSize.X * nativeint brickSize.Y * nativeint channels * sa
                
                let arr : 'a[] = Array.zeroCreate (size.X * size.Y * channels)
                let data = Volume<'a>(arr, VolumeInfo(0L, V3l(size.X, size.Y, channels), V3l(int64 channels, int64 channels * int64 size.X, 1L)))

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

                        let src = NativeVolume<'a>(NativePtr.ofNativeInt pBrickSlice, info)
                        let dst = 
                            data.SubVolume(
                                V3l(i.X * int64 brickSize.X, i.Y * int64 brickSize.Y, 0L),
                                V3l(brickSize.X, brickSize.Y, channels)
                            )

                        NativeVolume.using dst (fun dst -> src.CopyTo dst)

                data
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
                dst |> NativeVolume.toManaged

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
                if mipMaps then 1 + int(floor(Fun.Log2 (max size.X (max size.Y size.Z))))
                else 1

            VolumeStore<'a>.CreateNew(file, size, levels)

        static member Open(file : string, write : bool) =
            if File.Exists file then
                let info = FileInfo(file)

                let access = if write then MemoryMappedFileAccess.ReadWrite else MemoryMappedFileAccess.Read

                let handle = MemoryMappedFile.CreateFromFile(file, FileMode.Open, null, info.Length, access)
                let view = handle.CreateViewAccessor(0L, info.Length, access)
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

        static member Open<'a when 'a : unmanaged> (file : string, ?write : bool) =
            let write = defaultArg write true
            VolumeStore<'a>.Open(file, write)


    [<AutoOpen>]
    module ``GL Extensions`` =
        open Aardvark.Rendering.GL
        open OpenTK.Graphics.OpenGL4

        type SparseVolumeTexture<'a when 'a : unmanaged>(t : SparseTexture, data : VolumeStore<'a>) =
            inherit SparseTexture(t.Context, t.Handle, t.Dimension, t.MipMapLevels, t.Multisamples, t.Size, (if t.IsArray then Some t.Count else None), t.Format, t.PageSize, t.SparseLevels)

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
                comparison ComparisonFunction.Greater
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

        let clamp min max v =
            if v < min then min
            elif v > max then max
            else v

        let hsv2rgb (h : float) (s : float) (v : float) =
            let s = clamp 0.0 1.0 s
            let v = clamp 0.0 1.0 v

            let h = h % 1.0
            let h = if h < 0.0 then h + 1.0 else h
            let hi = floor ( h * 6.0 ) |> int
            let f = h * 6.0 - float hi
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

        let tex =
            sampler2d {
                texture uniform?DiffuseColorTexture
                filter Filter.MinMagMipLinear
                addressU WrapMode.Clamp
                addressV WrapMode.Clamp
            }
            
        let texArr =
            sampler2dArray {
                texture uniform?DiffuseColorTexture
                filter Filter.MinMagLinear
                addressU WrapMode.Clamp
                addressV WrapMode.Clamp
            }

        let blubb (v : Effects.Vertex) =
            fragment {
                let mutable maxValue = -1.0
                let mutable maxLevel = 10000
                for i in 0 .. 11 do
                    let vi = texArr.SampleLevel(v.tc, i, 0.0).X
                    if vi > 1.05 * maxValue then
                        maxValue <- vi
                        maxLevel <- i


                let c = (float maxLevel) / 11.0 |> clamp 0.0 1.0//hsv2rgb (float maxLevel / 11.0) 1.0 1.0
                

                
                let v = texArr.SampleLevel(v.tc, 0, 0.0).X


                let c = (if maxLevel >= 2 then 1.0 else 0.0)
                //let value = texArr.SampleLevel(v.tc, 1, 0.0).X * V4d.IIII
                return V4d(c, 0.0, 0.0, 1.0) //V4d(hsv2rgb (-(1.0+ 2.0*c)/3.0) 1.0 (0.5 + 0.5*v), 1.0)
            }
        let bla (v : Effects.Vertex) =
            fragment {
                let l0 = uniform?BaseLevel
                let magick : float  = uniform?Magick
                let levels : float = 12.0 //uniform?MipMapLevels
                let mutable maxValue = -1.0
                let mutable maxLevel = 1000.0

                    
                let l1 = levels - 1.0
                let steps = 100

                let step = (l1 - l0) / float steps

                let mutable l = l0
                for _ in 0 .. steps do
                    let vi = tex.SampleLevel(v.tc, l).X
                    if vi > 1.05 * maxValue then
                        maxValue <- vi
                        maxLevel <- l

                    l <- l + step

//                    let avg = 0.2156 //tex.SampleLevel(v.tc, 12.0).X
//                    let value = tex.SampleLevel(v.tc, l0).X - avg
//

                let c = (float maxLevel - l0) / (l1 - l0) |> clamp 0.0 1.0//hsv2rgb (float maxLevel / 11.0) 1.0 1.0
                return V4d(hsv2rgb -(1.0/3.0 + 2.0*c/3.0) 1.0 1.0, 1.0)
            }



        let inputTex =
            sampler2d {
                texture uniform?InputTexture
                filter Filter.MinMagMipLinear
                addressU WrapMode.Clamp
                addressV WrapMode.Clamp
            }

        let maskTex =
            sampler2d {
                texture uniform?MaskTexture
                filter Filter.MinMagMipLinear
                addressU WrapMode.Clamp
                addressV WrapMode.Clamp
            }


        let simple (v : Effects.Vertex) =
            fragment {
                let vm = maskTex.Sample(v.tc)
                let vo = V4d(V3d.III * inputTex.Sample(v.tc).X, 1.0)

                if vm.W < 0.01 then
                    return V4d.Zero
                else
                    return V4d.OIOI
            }

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
                        let v4 = volumeTexture.SampleLevel(c, 4.0).X
                        //if v < 0.25 && v4 > 0.25 then
                        //    res <- 100.0 * V3d.III + res
                        res <- v + res

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

                    // stufen
                    //value <- f * res / 1000.0
                    value <- f * res / 10.0
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

        VolumeStore.Open<uint16>(file, false)

    module Compute =
        open FShade

        [<LocalSize(X = 33, Y = 33)>]
        let computeGradientKernel (img : Image2d<Formats.r16>) (dx : Image2d<Formats.r32f>) (dy : Image2d<Formats.r32f>) =
            compute {
                let store : float[] = allocateShared 1089
                let lid = getLocalId().XY
                let gid = getWorkGroupId().XY
                let id = gid * 32 + lid

                let localIndex = lid.X + 33 * lid.Y
                if id.X < img.Size.X && id.Y < img.Size.Y then
                    store.[localIndex] <- img.[id].X
                else
                    store.[localIndex] <- 0.0

                barrier()

                if id.X < img.Size.X - 1 && id.Y < img.Size.Y && lid.X < 32 && lid.Y < 32 then
                    dx.[id] <- V4d.IIII * (0.5 + 0.5 * (store.[localIndex + 1] - store.[localIndex]))

                if id.X < img.Size.X && id.Y < img.Size.Y - 1 && lid.X < 32 && lid.Y < 32 then
                    dy.[id] <- V4d.IIII * (0.5 + 0.5 * (store.[localIndex + 33] - store.[localIndex]))

            }
            
        [<LocalSize(X = 32, Y = 32)>]
        let computeLaplaceKernel (img : Image2d<Formats.r16>) (dd : Image2d<Formats.r16>) =
            compute {
                //let store : float[] = allocateShared 1156
//                let lid = getLocalId().XY - V2i.II
//                let gid = getWorkGroupId().XY
//                let id = gid * 32 + lid

                let id = getGlobalId().XY

                let sv = 0.02
                let ss = 9.0

                if id.X < img.Size.X && id.Y < img.Size.Y then
                    let mutable sum = 0.0
                    let mutable weightSum = 0.0
                    
                    let v0 = img.[id].X
                    do
                        for x in -4 .. 4 do
                            for y in -4 .. 4 do
                                let d = V2i(x,y)
                                let l2 = Vec.dot d d |> float
                                let i = id + d
                                if i.X >= 0 && i.Y >= 0 && i.X < img.Size.X && i.Y < img.Size.Y then
                                    let v = img.[i].X
                                    let d = v - v0
                                    let d2 = d * d
                                    let w = exp (-0.5 * (l2 / (ss * ss) + d2 / (sv * sv)))

                                    sum <- sum + w * v
                                    weightSum <- weightSum + w

                    dd.[id] <- V4d.IIII * sum / weightSum
//
//                    if id.X > 0 && id.X < img.Size.X - 1 && id.Y > 0 && id.Y < img.Size.Y - 1 then
//                        let v = img.[id].X
//                        let vp0 = img.[id + V2i.IO].X
//                        let vn0 = img.[id - V2i.IO].X
//                        let v0p = img.[id + V2i.OI].X
//                        let v0n = img.[id - V2i.OI].X
//
//                        let vpp = img.[id + V2i(1,1)].X
//                        let vnp = img.[id + V2i(-1,1)].X
//                        let vpn = img.[id + V2i(1,-1)].X
//                        let vnn = img.[id + V2i(-1,-1)].X
//
//                        let ddv = vp0 + vn0 + v0p + v0n + vpp + vnp + vpn + vnn - 8.0 * v
//                        dd.[id] <- V4d.IIII * (0.5 + 2.0 * ddv)
//
//                    else
//                        dd.[id] <- V4d.Zero

//
//
//                let localIndex = lid.X + 1 + 34 * (lid.Y + 1)
//                if id.X >= 0 && id.Y >= 0 && id.X < img.Size.X && id.Y < img.Size.Y then
//                    store.[localIndex] <- img.[id].X
//                else
//                    store.[localIndex] <- 0.0
//
//                barrier()
//
//                let mutable ddx = 0.0
//                let mutable ddy = 0.0
//
//                if lid.X >= 0 && lid.X < 32 then
//                    ddx <- (store.[localIndex + 1] - store.[localIndex]) - (store.[localIndex] - store.[localIndex - 1])
//
//                if lid.Y >= 0 && lid.Y < 32 then
//                    ddy <- (store.[localIndex + 34] - store.[localIndex]) - (store.[localIndex] - store.[localIndex - 34])
//                    
//                let l = ddx + ddy
//
//                if lid.X >= 0 && lid.Y >= 0 && lid.X < 32 && lid.Y < 32 then
//                    dd.[id] <- V4d.IIII * (0.5 + 10.0 * l)

            }


        [<LocalSize(X = 32, Y = 32)>]
        let propagateKernel (img : Image2d<Formats.r16>) (dx : Image2d<Formats.r32f>) (dy : Image2d<Formats.r32f>) (dt : float) =
            compute {
                let id = getGlobalId().XY

                let mutable ddx = 0.0
                if id.X > 0 && id.X < dx.Size.X then 
                    ddx <- dx.[id].X - dx.[id - V2i.IO].X

                let mutable ddy = 0.0
                if id.Y > 0 && id.Y < dx.Size.Y then 
                    ddy <- dy.[id].X - dy.[id - V2i.OI].X
                
                let l = ddx + ddy

                img.[id] <- img.[id] + V4d.IIII * l * dt


            }

        let mutable private gradientK = None
        let mutable private laplaceK = None
        let mutable private propagateK = None

        let computeGradient (ctx : Context) (t : IBackendTexture) (dx : IBackendTexture) (dy : IBackendTexture) =
            let kernel = 
                match gradientK with
                    | Some k -> k
                    | None ->
                        let k = ctx.CompileKernel computeGradientKernel
                        gradientK <- Some k
                        k

            let localSize = V2i(33,33)
            let groups = t.Size.XY / 32
            kernel.Invoke(
                groups * localSize,
                [
                    "img", { texture = t; slice = 0; level = 0 } :> obj
                    "dx", { texture = dx; slice = 0; level = 0 } :> obj
                    "dy", { texture = dy; slice = 0; level = 0 } :> obj
                ]
            )

        let computeLaplace (ctx : Context) (t : BackendTextureOutputView) (dd : BackendTextureOutputView) =
            let kernel = 
                match laplaceK with
                    | Some k -> k
                    | None ->
                        let k = ctx.CompileKernel computeLaplaceKernel
                        laplaceK <- Some k
                        k

            kernel.Invoke(
                t.texture.Size.XY,
                [
                    "img", t :> obj
                    "dd", dd :> obj
                ]
            )

        let propagate (ctx : Context) (t : IBackendTexture) (dx : IBackendTexture) (dy : IBackendTexture) (dt : float) =
            let kernel = 
                match propagateK with
                    | Some k -> k
                    | None ->
                        let k = ctx.CompileKernel propagateKernel
                        propagateK <- Some k
                        k
            let localSize = V2i(32,32)
            let groups = t.Size.XY / 32
            kernel.Invoke(
                groups * localSize,
                [
                    "img", { texture = t; slice = 0; level = 0 } :> obj
                    "dx", { texture = dx; slice = 0; level = 0 } :> obj
                    "dy", { texture = dy; slice = 0; level = 0 } :> obj
                    "dt", dt :> obj
                ]
            )

    let testSegments() =
        Ag.initialize()
        Aardvark.Init()
        

        //LUNKAZ

        use app = new OpenGlApplication()
        use win = app.CreateSimpleRenderWindow()
        //use input = RawVolume.OpenRead<uint16>(@"C:\Users\Schorsch\Desktop\GussPK_AlSi_0.5Sn_180kV_1850x1850x1000px\GussPK_AlSi_0.5Sn_180kV_1850x1850x1000px.raw", V3i(1850, 1850, 1000))

        use store =  VolumeStore.Open @"C:\Users\Schorsch\Desktop\blubber.store"

        win.Width <- 1024
        win.Height <- 1024
        let slice = Mod.init 500

        let stepSlice (step : int) =
            let newValue = 
                match step with
                    | 1 -> slice.Value + 1
                    | -1 -> slice.Value - 1
                    | _ -> slice.Value

            let newValue = clamp 0 (store.Size.Z - 1) newValue
            Log.line "slice: %d" newValue
            transact (fun () -> slice.Value <- newValue)

        win.Keyboard.DownWithRepeats.Values.Add(fun k -> 
            match k with
                | Keys.OemPlus -> stepSlice 1
                | Keys.OemMinus -> stepSlice -1
                | _ -> ()
        )


        let assignRegions (threshold : float) (minSize : int) (maxSize : int) (img : PixImage<uint16>) : PixImage<byte> =
            let m = img.GetChannel(Col.Channel.Gray)
            let mask = PixImage<int>(Col.Format.Gray, m.Size)

            let inputData = m.Data
            let maskData = mask.Volume.Data
            maskData.SetByIndex(fun _ -> -1) |> ignore


            let dx = 1
            let dy = img.Size.X

            let nonAssignedNeighbours (c : V2i) (index : int) =
                let candidates = 
                    [
                        (c + V2i(-1,-1),    index - dx - dy)
                        (c + V2i(0,-1),     index - dy)
                        (c + V2i(1,-1),     index + dx - dy)
                        (c + V2i(-1,0),     index - dx)
                        (c + V2i(1,0),      index + dx)
                        (c + V2i(1,1),      index + dx + dy)
                        (c + V2i(0,1),      index + dy)
                        (c + V2i(1,1),      index + dx + dy)
                    ]

                candidates |> List.filter (fun (c,i) -> 
                    c.AllGreaterOrEqual 0 && c.AllSmaller(img.Size) && maskData.[i] < 0
                )
                    
            
            let mutable regionId = 0

            let growRegion (t : float) (c : V2i) (index : int) =
                let id = regionId
                inc &regionId    
                
                let mutable count = 1
                let mutable avg = float inputData.[index] / 65536.0

                maskData.[index] <- regionId
                let queue = System.Collections.Generic.Queue<_>(nonAssignedNeighbours c index)

                while queue.Count > 0 do
                    let (c,i) = queue.Dequeue()

                    if maskData.[i] < 0 then

                        let value = float inputData.[i] / 65536.0
                        if abs (value - avg) < t then
                            maskData.[i] <- regionId
                            avg <- (avg * float count + value) / float (count + 1)
                            count <- count + 1

                            for n in nonAssignedNeighbours c i do
                                queue.Enqueue n

                (regionId, avg, count)

            let result = System.Collections.Generic.Dictionary<_,_>()
            let mutable index = 0
            for y in 0 .. img.Size.Y - 1 do
                for x in 0 .. img.Size.X - 1 do
                    if maskData.[index] < 0 then
                        let (id, avg, count) = growRegion threshold (V2i(x,y)) index
                        if count >= minSize && count <= maxSize then
                            result.[id] <- (avg, count)


                    index <- index + 1

            let rand = RandomSystem()
            let colors = result |> Dictionary.map (fun id _ -> rand.UniformC3f().ToC4b())

            let result = PixImage<byte>(Col.Format.RGBA, img.Size)

            let maskChannel = mask.GetChannel(Col.Channel.Gray)
            result.GetMatrix<C4b>().SetMap(maskChannel, fun v ->
                match colors.TryGetValue v with
                    | (true, c) -> C4b.Red
                    | _ -> 
                        if v < 0 then C4b(255uy, 0uy, 0uy, 255uy)
                        else C4b(0uy, 0uy, 0uy, 0uy)
            ) |> ignore


            result

        let localMinima (img : PixImage<uint16>) =
            let c = img.GetChannel(Col.Channel.Gray).SubMatrix(V2i.II, img.Size - V2i.II * 2)

            let res = PixImage<byte>(Col.Format.Gray, img.Size)

            let mutable cRes = res.GetChannel(Col.Channel.Gray).SubMatrix(V2i.II, img.Size - V2i.II * 2)
            let dx = c.DX
            let dy = c.DY


            cRes.ForeachIndex(c.Info, fun (ri : int64) (ci : int64) ->

                let n = [ ci - dx - dy; ci - dy; ci - dx + dy; ci - dy; ci + dy; ci + dx - dy; ci + dx; ci + dy + dy]

                let v = c.[ci]
                let isMin = n |> List.forall (fun ni -> c.[ni] > v)


                cRes.[ri] <- (if isMin then 255uy else 0uy)
            ) |> ignore

            res


        let applyThreshold (img : PixImage<uint16>) =
            let res = PixImage<byte>(Col.Format.RGBA, img.Size)

            res.GetMatrix<C4b>().SetMap(img.GetChannel(Col.Channel.Gray), fun v ->
                if v > 13287us && v < 15583us then C4b.Red
                elif v <= 13287us then C4b.Green
                else C4b.Blue
            ) |> ignore

            res




        let input =
            slice |> Mod.map (fun slice ->
                let img = PixImage<uint16>(Col.Format.Gray, store.GetSlice(0,slice))
                img
            )

        let inputTexture =
            input |> Mod.map (fun pi ->
                PixTexture2d(PixImageMipMap [| pi :> PixImage |], TextureParams.mipmapped) :> ITexture
            )

        let resultTexture =
            input |> Mod.map (fun img ->
                let res = assignRegions 0.025 10 1000 img
                PixTexture2d(PixImageMipMap [| res :> PixImage |], TextureParams.mipmapped) :> ITexture


// 
//                let m = img.Data |> unbox<uint16[]>
//                let r = Array.init m.Length id
//                let w = img.Size.X
//
//                let rec find (i : int) =
//                    let p = r.[i]
//                    if p = i then 
//                        i
//                    else 
//                        let res = find p
//                        r.[p] <- res
//                        res
//
//                let union (i : int) (j : int) =
//                    let ri = find i
//                    let rj = find j
//                    if ri < rj then
//                        r.[rj] <- ri
//                    elif rj < ri then
//                        r.[ri] <- rj
//                    
//
//                let eq (l : uint16) (r : uint16) =
//                    abs (int l - int r) < 600
//
//
//                let testUnion (i : int) (j : int) =
//                    if i >= 0 && i < m.Length && j >= 0 && j < m.Length then
//                        if eq m.[i] m.[j] then
//                            union i j
//
//                for i in 0 .. m.Length-1 do
//                    testUnion i (i - w)
//                    testUnion i (i + w)
//                    testUnion i (i - 1)
//                    testUnion i (i + 1)
//
//
//                let values = Array.create m.Length -1
//                let mutable currentValue = 0
//
//                let data = PixImage<uint16>(Col.Format.RGBA, img.Size)
//
//                let cnt = 1 <<< 20
//                let arr = Array.init cnt (fun i -> Shader.hsv2rgb (float i / float cnt) 1.0 1.0) |> Array.map (fun v -> C4b(v.X, v.Y, v.Z, 1.0))
//                
//                let arr = arr.RandomOrder() |> Seq.toArray
//
//
//                let mutable labels = data.GetMatrix<C4b>()
//                for i in 0 .. m.Length-1 do
//                    let l = find i
//                    let v = arr.[l]
//                    labels.[int64 i] <- v
//
//
//                let tex = PixTexture2d(PixImageMipMap [| img :> PixImage |], { wantMipMaps = true; wantSrgb = false; wantCompressed = false })
//                
//                tex :> ITexture
            )

        let sg = 
            Sg.fullScreenQuad
                |> Sg.uniform "InputTexture" inputTexture
                |> Sg.uniform "MaskTexture" resultTexture
                |> Sg.shader {
                    do! Shader.simple
                }

        let task = app.Runtime.CompileRender(win.FramebufferSignature, sg)


        let color = task |> RenderTask.renderToColor (Mod.constant store.Size.XY) 
        color.Acquire()

        let colorImage = app.Runtime.Download(color.GetValue() |> unbox<IBackendTexture>)

        color.Release()
        colorImage.SaveAsImage @"C:\Users\Schorsch\Desktop\output.jpg"





        win.RenderTask <- app.Runtime.CompileRender(win.FramebufferSignature, sg)

        win.Run()
        Environment.Exit 0

    let test() =
        Ag.initialize()
        Aardvark.Init()
        

        //LUNKAZ

        use app = new OpenGlApplication()
        use win = app.CreateSimpleRenderWindow()
        //use input = RawVolume.OpenRead<uint16>(@"C:\Users\Schorsch\Desktop\GussPK_AlSi_0.5Sn_180kV_1850x1850x1000px\GussPK_AlSi_0.5Sn_180kV_1850x1850x1000px.raw", V3i(1850, 1850, 1000))

        use store =  VolumeStore.Open @"C:\Users\Schorsch\Desktop\blubber.store"

        win.Width <- 1024
        win.Height <- 1024

        let baseLevel = Mod.init 0.0
        let slice = Mod.init 500
        let filter = Mod.init 0
        let showLevels = Mod.init false
        let magick = Mod.init 1.05
        let setLevel (l : float) =
            let l = clamp 0.0 10.0 l
            Log.line "level: %f" l
            transact (fun () -> baseLevel.Value <- l)

        let setSlice (step : int) =
            let newValue = 
                match step with
                    | 1 -> slice.Value + 1
                    | -1 -> slice.Value - 1
                    | _ -> slice.Value

            let newValue = clamp 0 (store.Size.Z - 1) newValue
            Log.line "slice: %d" newValue
            transact (fun () -> slice.Value <- newValue)

        win.Keyboard.DownWithRepeats.Values.Add(fun k -> 
            match k with
                | Keys.OemPlus -> setSlice 1
                | Keys.OemMinus -> setSlice -1
                | Keys.F -> transact (fun () -> filter.Value <- filter.Value + 1)
                | Keys.X -> transact (fun () -> showLevels.Value <- not showLevels.Value)
                | Keys.M -> transact (fun () -> magick.Value <- 1.005 * magick.Value)
                | Keys.L -> transact (fun () -> magick.Value <- magick.Value / 1.005)
                | _ -> ()
        )

        win.Mouse.Scroll.Values.Add(fun e ->
            let d = sign e
            setLevel (baseLevel.Value + 0.125 * float d)
        )

        let interpolate (t : float) (a : uint16) (b : uint16) =
            uint16 ((1.0 - t) * float a + t * float b)

        let texture =
            Mod.map2 (fun slice filter ->
                let img = PixImage<uint16>(Col.Format.Gray, store.GetSlice(0,slice))
                
                let levels = int(floor(Fun.Log2(max img.Size.X img.Size.Y))) + 1
                
                let imgs = Array.zeroCreate levels
                imgs.[0] <- img
                for dstLevel in 1 .. levels - 1 do
                    let srcLevel = dstLevel - 1

                    let src = imgs.[srcLevel]

                    let dstSize = V2i(max 1 (src.Size.X / 2), max 1 (src.Size.Y / 2))
                    let dst = PixImage<uint16>(Col.Format.Gray, dstSize)

                    if src.Size.AllGreater 16 then
                        match filter % 6 with
                            | 0 -> Log.line "Cubic"; dst.GetChannel(Col.Channel.Gray).SetScaledCubic(src.GetChannel(Col.Channel.Gray))
                            | 1 -> Log.line "Lanczos"; dst.GetChannel(Col.Channel.Gray).SetScaledLanczos(src.GetChannel(Col.Channel.Gray))
                            | 2 -> Log.line "BSpline3"; dst.GetChannel(Col.Channel.Gray).SetScaledBSpline3(src.GetChannel(Col.Channel.Gray))
                            | 3 -> Log.line "BSpline5"; dst.GetChannel(Col.Channel.Gray).SetScaledBSpline5(src.GetChannel(Col.Channel.Gray))
                            | 4 -> Log.line "Nearest"; dst.GetChannel(Col.Channel.Gray).SetScaledNearest(src.GetChannel(Col.Channel.Gray))
                            | 5 -> Log.line "Linear"; dst.GetChannel(Col.Channel.Gray).SetScaledLinear(src.GetChannel(Col.Channel.Gray), interpolate, interpolate)
                            | _ -> ()
                    else
                        dst.GetChannel(Col.Channel.Gray).SetScaledCubic(src.GetChannel(Col.Channel.Gray))
                        
                    imgs.[dstLevel] <- dst
                    ()


                let m = imgs.[0].Data |> unbox<uint16[]>
                let r = Array.init m.Length id
                let w = imgs.[0].Size.X

                let rec find (i : int) =
                    let p = r.[i]
                    if p = i then 
                        i
                    else 
                        let res = find p
                        r.[p] <- res
                        res

                let union (i : int) (j : int) =
                    let ri = find i
                    let rj = find j
                    if ri < rj then
                        r.[rj] <- ri
                    elif rj < ri then
                        r.[ri] <- rj
                    

                let eq (l : uint16) (r : uint16) =
                    abs (int l - int r) < 600


                let testUnion (i : int) (j : int) =
                    if i >= 0 && i < m.Length && j >= 0 && j < m.Length then
                        if eq m.[i] m.[j] then
                            union i j

                for i in 0 .. m.Length-1 do
                    testUnion i (i - w)
                    testUnion i (i + w)
                    testUnion i (i - 1)
                    testUnion i (i + 1)


                let values = Array.create m.Length -1
                let mutable currentValue = 0

                let data = PixImage<uint16>(Col.Format.Gray, imgs.[0].Size)
                let labels = data.Volume.Data

                for i in 0 .. m.Length-1 do
                    let l = find i
                    let v = values.[l]

                    let v = 
                        if v < 0 then 
                            let v = currentValue
                            values.[l] <- v
                            currentValue <- v + 1
                            v
                        else
                            v
                    labels.[i] <- uint16 v

                Log.line "found %d groups" currentValue
                for i in 0 .. m.Length-1 do
                    labels.[i] <- 65535.0 * float labels.[i] / float currentValue |> uint16

                imgs.[0] <- data


                let tex = PixTexture2d(PixImageMipMap(imgs |> Array.map (fun i -> i :> PixImage)), { wantMipMaps = true; wantSrgb = false; wantCompressed = false })
                


                
                tex :> ITexture
            ) slice filter

        let sg = 
            Sg.fullScreenQuad
                |> Sg.uniform "Magick" magick
                |> Sg.uniform "ShowLevels" showLevels
                |> Sg.uniform "BaseLevel" baseLevel
                |> Sg.uniform "MipMapLevels" (Mod.constant (float store.MipMapLevels))
                |> Sg.diffuseTexture texture
                |> Sg.shader {
                    do! Shader.bla
                }

        let task = app.Runtime.CompileRender(win.FramebufferSignature, sg)


        let color = task |> RenderTask.renderToColor (Mod.constant store.Size.XY) 
        color.Acquire()

        let colorImage = app.Runtime.Download(color.GetValue() |> unbox<IBackendTexture>)

        color.Release()
        colorImage.SaveAsImage @"C:\Users\Schorsch\Desktop\output.jpg"





        win.RenderTask <- app.Runtime.CompileRender(win.FramebufferSignature, sg)

        win.Run()
        Environment.Exit 0

    
    let runTest (effect : list<FShade.Effect>) (img : PixImage<uint16> -> ITexture) =
        Ag.initialize()
        Aardvark.Init()
        

        //LUNKAZ

        use app = new OpenGlApplication()
        use win = app.CreateSimpleRenderWindow()
        //use input = RawVolume.OpenRead<uint16>(@"C:\Users\Schorsch\Desktop\GussPK_AlSi_0.5Sn_180kV_1850x1850x1000px\GussPK_AlSi_0.5Sn_180kV_1850x1850x1000px.raw", V3i(1850, 1850, 1000))

        use store =  VolumeStore.Open @"C:\volumes\blubber.store"

        win.Width <- 1024
        win.Height <- 1024

        let baseLevel = Mod.init 0.0
        let slice = Mod.init 500
        let filter = Mod.init 0
        let showLevels = Mod.init false
        let magick = Mod.init 1.05
        let setLevel (l : float) =
            let l = clamp 0.0 10.0 l
            Log.line "level: %f" l
            transact (fun () -> baseLevel.Value <- l)

        let setSlice (step : int) =
            let newValue = 
                match step with
                    | 1 -> slice.Value + 1
                    | -1 -> slice.Value - 1
                    | _ -> slice.Value

            let newValue = clamp 0 (store.Size.Z - 1) newValue
            Log.line "slice: %d" newValue
            transact (fun () -> slice.Value <- newValue)


        win.Keyboard.DownWithRepeats.Values.Add(fun k -> 
            match k with
                | Keys.OemPlus -> setSlice 1
                | Keys.OemMinus -> setSlice -1
                | Keys.F -> transact (fun () -> filter.Value <- filter.Value + 1)
                | Keys.X -> transact (fun () -> showLevels.Value <- not showLevels.Value)
                | Keys.M -> transact (fun () -> magick.Value <- 1.005 * magick.Value)
                | Keys.L -> transact (fun () -> magick.Value <- magick.Value / 1.005)
                | _ -> ()
        )

        win.Mouse.Scroll.Values.Add(fun e ->
            let d = sign e
            setLevel (baseLevel.Value + 0.125 * float d)
        )        



        let input =
            slice |> Mod.map (fun slice ->
                let img = PixImage<uint16>(Col.Format.Gray, store.GetSlice(0,slice))
                img
            )

        let dst = app.Context.CreateTexture2DArray(store.Size.XY, 12, 1, TextureFormat.R16, 1)

        let test (img : PixImage<uint16>) =
            app.Context.Upload(dst, 0, 0, V2i.Zero, img)


            let mutable src = { texture = dst; slice = 0; level = 0 }

            for i in 1 .. 11 do
                let dst = { texture = dst; slice = i; level = 0 }
                Compute.computeLaplace app.Context src dst
                src <- dst


            dst :> ITexture

        let texture = input |> Mod.map test

        let sg = 
            Sg.fullScreenQuad
                |> Sg.uniform "Magick" magick
                |> Sg.uniform "ShowLevels" showLevels
                |> Sg.uniform "BaseLevel" baseLevel
                |> Sg.uniform "MipMapLevels" (Mod.constant (float store.MipMapLevels))
                |> Sg.diffuseTexture texture
                |> Sg.effect effect

        let task = app.Runtime.CompileRender(win.FramebufferSignature, sg)


        let color = task |> RenderTask.renderToColor (Mod.constant store.Size.XY) 
        color.Acquire()

        let colorImage = app.Runtime.Download(color.GetValue() |> unbox<IBackendTexture>)

        color.Release()
        colorImage.SaveAsImage @"C:\volumes\output.jpg"





        win.RenderTask <- app.Runtime.CompileRender(win.FramebufferSignature, sg)

        win.Run()
        Environment.Exit 0

    let run() =
        //testSegments()

        let simple (img : PixImage<uint16>) =
            PixTexture2d(PixImageMipMap [| img :> PixImage |], TextureParams.mipmapped) :> ITexture

        //runTest [Shader.blubb |> toEffect] simple



        //test()

        Ag.initialize()
        Aardvark.Init()

        use app = new OpenGlApplication()
        use win = app.CreateSimpleRenderWindow()
        let sliceFolder = @"C:\volumes\slices\"


        let data, store, size =
            // quader
            //@"C:\volumes\Testdatensatz_600x600x1000px.raw", @"C:\volumes\blubber2.store",  V3i(600, 600, 1000)
            
            // motorteil
            @"C:\volumes\MT-M6-845x549x1820-10076.raw", @"C:\volumes\mt.store", V3i(845, 549, 1820)

            // stufenzyliner
            //@"C:\volumes\GussPK_AlSi_0.5Sn_180kV_1850x1850x1000px.raw", @"C:\volumes\blubber.store", V3i(1850,1850,1000)

        
        // create
        use input = RawVolume.OpenRead<uint16>(data, size)
        use store =  input |> openOrCreate store
//
//        for i in 0 .. input.Size.Z - 1 do
//            let slice = input.[i]
//            let img = PixImage<uint16>(Col.Format.Gray, slice).ToImageLayout()
//            img.SaveAsImage (sprintf @"C:\Users\Schorsch\Desktop\slices\%d.jpg" i)
//
//        Environment.Exit 0

//        // store one brick as slices
//        let bi = store.BrickCount 0 / 2
//        let t = store.Brick(0, bi) |> NativeTensor4.toManaged
//        
//        for z in 0 .. int t.SZ - 1 do
//            let v = t.SubXYWVolume(int64 z)
//            let img = PixImage<uint16>(Col.Format.Gray, v).ToImageLayout()
//            img.SaveAsImage (sprintf @"C:\Users\Schorsch\Desktop\bricks\%d.jpg" z)

            
//        let csv = store.Histogram |> Seq.mapi (fun v c -> sprintf "%d;%d" v c) |> String.concat "\r\n"
//        File.WriteAllText(@"C:\Users\Schorsch\Desktop\hist.csv", csv)


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


        let should = Mod.init true

        win.Keyboard.KeyDown(Keys.Space).Values.Add(fun _ ->
            transact (fun () -> should.Value <- not should.Value)
        )

        let sg_ () = 
            Sg.box' C4b.Red (Box3d(-size, size))
                |> Sg.scale 5.0
                |> Sg.uniform "ScaleFactor" factor
                |> Sg.uniform "VolumeTexture" (Mod.constant (texture :> ITexture))
                |> Sg.shader {
                    do! Shader.vertex
                    do! Shader.fragment
                   }
                |> Sg.depthTest (Mod.constant DepthTestMode.None)
                |> Sg.cullMode (Mod.constant CullMode.Back)
                |> Sg.viewTrafo (view |> Mod.map CameraView.viewTrafo)
                |> Sg.projTrafo (proj |> Mod.map Frustum.projTrafo)

        let sg =
            should |> Mod.map (function | true -> sg_() | false -> Sg.ofList []) |> Sg.dynamic

        let sg = sg

        let task = app.Runtime.CompileRender(win.FramebufferSignature, sg)
        win.RenderTask <- task

        win.Run()
        ()

