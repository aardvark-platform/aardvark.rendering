namespace Aardvark.Rendering

open System.Threading.Tasks
open Aardvark.Base
open FSharp.NativeInterop
open System.Runtime.InteropServices
open System.Runtime.CompilerServices

#nowarn "9"

[<RequireQualifiedAccess>]
type CompressionMode =
    | None
    | BC1       // DXT1
    | BC2       // DXT3
    | BC3       // DXT5
    | BC4 of signed: bool
    | BC5 of signed: bool
    | BC6h
    | BC7

[<AutoOpen>]
module TextureFormatCompressionExtensions =

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module TextureFormat =

        let private compressionModes =
            Dictionary.ofList [
                TextureFormat.CompressedRgbS3tcDxt1,          CompressionMode.BC1
                TextureFormat.CompressedSrgbS3tcDxt1,         CompressionMode.BC1
                TextureFormat.CompressedRgbaS3tcDxt1,         CompressionMode.BC1
                TextureFormat.CompressedSrgbAlphaS3tcDxt1,    CompressionMode.BC1

                TextureFormat.CompressedRgbaS3tcDxt3,         CompressionMode.BC2
                TextureFormat.CompressedSrgbAlphaS3tcDxt3,    CompressionMode.BC2

                TextureFormat.CompressedRgbaS3tcDxt5,         CompressionMode.BC3
                TextureFormat.CompressedSrgbAlphaS3tcDxt5,    CompressionMode.BC3

                TextureFormat.CompressedRedRgtc1,             CompressionMode.BC4 false
                TextureFormat.CompressedSignedRedRgtc1,       CompressionMode.BC4 true

                TextureFormat.CompressedRgRgtc2,              CompressionMode.BC5 false
                TextureFormat.CompressedSignedRgRgtc2,        CompressionMode.BC5 true

                TextureFormat.CompressedRgbBptcSignedFloat,   CompressionMode.BC6h
                TextureFormat.CompressedRgbBptcUnsignedFloat, CompressionMode.BC6h

                TextureFormat.CompressedRgbaBptcUnorm,        CompressionMode.BC7
                TextureFormat.CompressedSrgbAlphaBptcUnorm,   CompressionMode.BC7
            ]

        let compressionMode (fmt : TextureFormat) =
            match compressionModes.TryGetValue fmt with
            | (true, mode) -> mode
            | _ -> CompressionMode.None

    type TextureFormat with
        member x.CompressionMode = TextureFormat.compressionMode x


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CompressionMode =

    let blockSize = function
        | CompressionMode.None -> 1
        | _ -> 4

    let bytesPerBlock = function
        | CompressionMode.None -> 0n
        | CompressionMode.BC1 | CompressionMode.BC4 _ -> 8n
        | _ -> 16n

    let numberOfBlocks (size : V3i) (mode : CompressionMode) =
        let blockSize = blockSize mode
        max 1 ((size + (blockSize - 1)) / blockSize)

    let sizeInBytes (size : V3i) (mode : CompressionMode) =
        let blocks = mode |> numberOfBlocks size
        let bytesPerBlock = mode |> bytesPerBlock
        nativeint (blocks.X * blocks.Y * blocks.Z) * bytesPerBlock


module BlockCompression =

    [<Struct>]
    type private SymM33f =
        val mutable M00 : float32
        val mutable M01 : float32
        val mutable M02 : float32
        val mutable M11 : float32
        val mutable M12 : float32
        val mutable M22 : float32

        static member inline Multiply(m : inref<SymM33f>, v : V3f) =
            V3f(
                 m.M00 * v.X + m.M01 * v.Y + m.M02 * v.Z,
                 m.M01 * v.X + m.M11 * v.Y + m.M12 * v.Z,
                 m.M02 * v.X + m.M12 * v.Y + m.M22 * v.Z
             )

    module private Array =

        let inline minIndexBy (f : ^T -> ^U) (array : ^T[]) =
            let mutable dist = f array.[0]
            let mutable index = 0

            for i = 1 to array.Length - 1 do
                let d = f array.[i]
                if d < dist then
                    dist <- d
                    index <- i

            index

    [<Extension; Sealed>]
    type private VolumeExtensions() =

        [<Extension>]
        static member inline SubVolumeAlpha(info : VolumeInfo) =
            assert (info.Size.Z >= 4L)
            info.SubVolume(V3l(0, 0, 3), info.Size.XYI)

        [<Extension>]
        static member inline SubVolumeGreen(info : VolumeInfo) =
            assert (info.Size.Z >= 2L)
            info.SubVolume(V3l(0, 0, 1), info.Size.XYI)

        [<Extension>]
        static member inline SubVolumeRGB(info : VolumeInfo) =
            assert (info.Size.Z >= 3L)
            let size = info.Size
            info.SubVolume(V3l.Zero, V3l(size.XY, 3L))

        [<Extension>]
        static member inline MinElement(matrix : NativeMatrix<'T>) =
            let mutable result = matrix.[0, 0]
            matrix.Iter(fun (_ : V2l) value -> if value < result then result <- value)
            result

        [<Extension>]
        static member inline SubXYMatrix(volume : NativeVolume<'T>, z : int64) =
            NativeMatrix<'T>(volume.Pointer, volume.Info.SubXYMatrix(z))

        [<Extension>]
        static member inline GetC3b(volume : NativeVolume<uint8>, x : int, y : int) =
            assert (volume.SZ >= 3L)
            C3b(volume.[x, y, 0], volume.[x, y, 1], volume.[x, y, 2])

        [<Extension>]
        static member inline GetC4b(volume : NativeVolume<uint8>, x : int, y : int) =
            assert (volume.SZ >= 3L)
            if volume.SZ < 4L then
                C4b(volume.GetC3b(x, y))
            else
                C4b(volume.GetC3b(x, y), volume.[x, y, 3])

        [<Extension>]
        static member inline SetC4b(volume : NativeVolume<uint8>, x : int, y : int, color : C4b) =
            assert (volume.SZ >= 3L)
            for c = 0 to min 3 (int volume.SZ - 1) do
                volume.[x, y, c] <- color.[c]

    [<AutoOpen>]
    module private ColorUtilities =

        module Bits =

            module Tables =

                let compress4 =
                    Array.init 256 (fun i -> uint8 ((i * 15 + 135) >>> 8))

                let compress5 =
                    Array.init 256 (fun i -> uint8 ((i * 249 + 1024) >>> 11))

                let compress6 =
                    Array.init 256 (fun i -> uint8 ((i * 253 + 512)  >>> 10))

                let expand4 =
                    Array.init 16 (fun i -> uint8 (i * 17))

                let expand5 =
                    Array.init 32 (fun i -> uint8 ((i <<< 3) ||| (i >>> 2)))

                let expand6 =
                    Array.init 64 (fun i -> uint8 ((i <<< 2) ||| (i >>> 4)))

            let inline compress4 x =
                Tables.compress4.[int x]

            let inline compress5 x =
                Tables.compress5.[int x]

            let inline compress6 x =
                Tables.compress6.[int x]

            let inline expand4 x =
                Tables.expand4.[int x]

            let inline expand5 x =
                Tables.expand5.[int x]

            let inline expand6 x =
                Tables.expand6.[int x]

        [<Struct>]
        type R5G6B5 =
            val private R : uint8
            val private G : uint8
            val private B : uint8

            new (r : uint8, g : uint8, b : uint8) =
                assert (r < 32uy)
                assert (g < 64uy)
                assert (b < 32uy)
                { R = r; G = g; B = b }

            new (color : C3b) =
                { R = Bits.compress5 color.R;
                  G = Bits.compress6 color.G;
                  B = Bits.compress5 color.B }

            new (color : uint16) =
                { R = uint8 ((color &&& 0xF800us) >>> 11);
                  G = uint8 ((color &&& 0x07E0us) >>>  5);
                  B = uint8 (color &&& 0x001Fus) }

            new (color : V3f) =
                assert (Vec.allSmaller (round color) 256.0f)
                assert (Vec.allGreaterOrEqual (round color) 0.0f)
                R5G6B5(C3b(uint8 (round color.X), uint8 (round color.Y), uint8 (round color.Z)))

            member x.ToC3b() =
                C3b(Bits.expand5 x.R, Bits.expand6 x.G, Bits.expand5 x.B)

            member x.ToC4b() =
                C4b(x.ToC3b())

            member x.ToUint16() =
                ((uint16 x.R) <<< 11) |||
                ((uint16 x.G) <<< 5) |||
                (uint16 x.B)

            static member ToC3b(color : R5G6B5) =
                color.ToC3b()

            static member ToUint16(color : R5G6B5) =
                color.ToUint16()

            static member Lerp(x : R5G6B5, y : R5G6B5, t : float32) =
                R5G6B5(
                    lerp x.R y.R t,
                    lerp x.G y.G t,
                    lerp x.B y.B t
                )

            override x.ToString() =
                x |> R5G6B5.ToC3b |> string

        module C3b =

            let inline distanceSquared (x : C3b) (y : C3b) =
                let dR = float32 x.R - float32 y.R
                let dG = float32 x.G - float32 y.G
                let dB = float32 x.B - float32 y.B
                dR * dR + dG * dG + dB * dB

    // See: https://docs.microsoft.com/en-us/windows/win32/direct3d10/d3d10-graphics-programming-guide-resources-block-compression
    module private BC1 =

        [<AutoOpen>]
        module private Encoding =

            let powerIteration (cov : inref<SymM33f>) =
                let mutable bk = V3f.One
                for _ = 0 to 7 do
                    bk <- SymM33f.Multiply(&cov, bk)
                    bk.Normalize()
                bk

            let computePrincipleAxis (center : V3f) (values : V3f[]) =
                let mutable cov = SymM33f()
                for i = 0 to values.Length - 1 do
                    let v = values.[i] - center
                    cov.M00 <- cov.M00 + v.X * v.X
                    cov.M01 <- cov.M01 + v.X * v.Y
                    cov.M02 <- cov.M02 + v.X * v.Z

                    cov.M11 <- cov.M11 + v.Y * v.Y
                    cov.M12 <- cov.M12 + v.Y * v.Z

                    cov.M22 <- cov.M22 + v.Z * v.Z

                powerIteration &cov

            let computeEndpoints (input : NativeVolume<uint8>) =
                let w = int input.SX
                let h = int input.SY
                let n = w * h

                if n < 3 then
                    R5G6B5(input.GetC3b(0, 0)),
                    R5G6B5(input.GetC3b(w - 1, h - 1))

                else
                    let values =
                        Array.init n (fun i ->
                            let x = i % w
                            let y = i / w
                            V3f(float32 input.[x, y, 0], float32 input.[x, y, 1], float32 input.[x, y, 2])
                        )

                    let center =
                        Array.sum values / float32 values.Length

                    let u = values |> computePrincipleAxis center

                    let mutable ta = infinityf
                    let mutable tb = -infinityf

                    for i = 0 to n - 1 do
                        let d = Vec.dot u values.[i]
                        if d < ta then ta <- d
                        if d > tb then tb <- d

                    let tc = Vec.dot center u
                    let a = center + (ta - tc) * u
                    let b = center + (tb - tc) * u

                    R5G6B5(a |> clamp 0.0f 255.0f),
                    R5G6B5(b |> clamp 0.0f 255.0f)

            let computePalette (c0 : R5G6B5) (c1 : R5G6B5) =
                if c0.ToUint16() > c1.ToUint16() then
                    [| c0; c1
                       R5G6B5.Lerp(c0, c1, 1.0f / 3.0f)
                       R5G6B5.Lerp(c0, c1, 2.0f / 3.0f) |]

                else
                    [| c0; c1
                       R5G6B5.Lerp(c0, c1, 0.5f) |]

            let computeIndices (palette : R5G6B5[]) (values : NativeVolume<uint8>) =
                let mutable indices = Matrix<int>(values.Size.XY)
                let palette = palette |> Array.map R5G6B5.ToC3b

                for y = 0 to int values.SY - 1 do
                    for x = 0 to int values.SX - 1 do
                        let value = values.GetC4b(x, y)

                        if value.A < 127uy then
                            indices.[x, y] <- 3

                        else
                            let index = palette |> Array.minIndexBy (C3b.distanceSquared value.RGB)
                            indices.[x, y] <- index

                indices

        let encode (offset : V2i) (src : nativeint) (srcInfo : VolumeInfo) (dst : nativeint)  =
            assert (Vec.allSmaller offset 4)
            assert (Vec.allGreaterOrEqual offset 0)
            assert (Vec.allSmallerOrEqual srcInfo.Size 4L)
            assert (srcInfo.Size.Z >= 3L)

            let src = src |> NativeVolume.ofNativeInt<uint8> srcInfo
            let size = V2i src.Size.XY

            let c0, c1 =
                let c0, c1 = computeEndpoints src

                let hasAlpha =
                    if src.SZ > 3L then
                        src.SubXYMatrix(3L).MinElement() < 127uy
                    else
                        false

                if c0.ToUint16() > c1.ToUint16() then
                    if hasAlpha then c1, c0 else c0, c1
                else
                    if hasAlpha then c0, c1 else c1, c0

            let palette = computePalette c0 c1
            let indices = computeIndices palette src

            let pColors = dst |> NativePtr.ofNativeInt<uint16>
            let pIndices = (dst + 4n) |> NativePtr.ofNativeInt<uint8>

            pColors.[0] <- c0.ToUint16()
            pColors.[1] <- c1.ToUint16()

            for y = offset.Y to size.Y - 1 do
                let mutable row = 0uy

                for x = offset.X to size.X - 1 do
                    let index = uint8 indices.[x, y]
                    row <- row ||| (index <<< (2 * x))

                pIndices.[y] <- row

        let mirrorCopy (offset : int) (height : int) (src : nativeint) (dst : nativeint) =
            let srcColor = NativePtr.ofNativeInt<uint32> src
            let dstColor = NativePtr.ofNativeInt<uint32> dst
            dstColor.[0] <- srcColor.[0]

            let srcIndex = NativePtr.ofNativeInt<uint8> (src + 4n)
            let dstIndex = NativePtr.ofNativeInt<uint8> (dst + 4n)

            for i = 0 to height - 1 do
                dstIndex.[offset + height - 1 - i] <- srcIndex.[i]

            for i = 0 to offset - 1 do
                dstIndex.[i] <- srcIndex.[height - 1]

        let decode (offset : V2i) (src : nativeint) (dst : nativeint) (dstInfo : VolumeInfo) =
            assert (Vec.allSmaller offset 4)
            assert (Vec.allGreaterOrEqual offset 0)
            assert (Vec.allSmallerOrEqual dstInfo.Size.XY 4L)
            assert (dstInfo.Size.Z >= 3L)

            let dst = dst |> NativeVolume.ofNativeInt<uint8> dstInfo
            let size = V2i dst.Size.XY

            let pColors = src |> NativePtr.ofNativeInt<uint16>
            let pIndices = (src + 4n) |> NativePtr.ofNativeInt<uint8>

            let colors = Array.zeroCreate 4

            colors.[0] <- R5G6B5(pColors.[0]).ToC4b()
            colors.[1] <- R5G6B5(pColors.[1]).ToC4b()

            if pColors.[0] > pColors.[1] then
                colors.[2] <- lerp colors.[0] colors.[1] (1.0f / 3.0f)
                colors.[3] <- lerp colors.[0] colors.[1] (2.0f / 3.0f)
            else
                colors.[2] <- lerp colors.[0] colors.[1] 0.5f
                colors.[3] <- C4b.Zero

            for y = offset.Y to size.Y - 1 do
                let row = int pIndices.[y]

                for x = offset.X to size.X - 1 do
                    let index = (row >>> (2 * x)) &&& 0x03
                    dst.SetC4b(x, y, colors.[index])

    module private BC2 =

        let encode (offset : V2i) (src : nativeint) (srcInfo : VolumeInfo) (dst : nativeint)  =
            assert (Vec.allSmaller offset 4)
            assert (Vec.allGreaterOrEqual offset 0)
            assert (Vec.allSmallerOrEqual srcInfo.Size 4L)
            assert (srcInfo.Size.Z >= 4L)

            BC1.encode offset src (srcInfo.SubVolumeRGB()) (dst + 8n)

            let alpha = src |> NativeMatrix.ofNativeInt<uint8> (srcInfo.SubXYMatrix(3L))
            let size = V2i alpha.Size

            let pAlpha = dst |> NativePtr.ofNativeInt<uint16>

            for y = offset.Y to size.Y - 1 do
                let mutable row = 0us

                for x = offset.X to size.X - 1 do
                    let value = Bits.compress4 alpha.[x, y]
                    row <- row ||| ((uint16 value) <<< (4 * x))

                pAlpha.[y] <- row

        let mirrorCopy (offset : int) (height : int) (src : nativeint) (dst : nativeint) =
            let srcAlpha = NativePtr.ofNativeInt<uint16> src
            let dstAlpha = NativePtr.ofNativeInt<uint16> dst

            for i = 0 to height - 1 do
                dstAlpha.[offset + height - 1 - i] <- srcAlpha.[i]

            for i = 0 to offset - 1 do
                dstAlpha.[i] <- srcAlpha.[height - 1]

            BC1.mirrorCopy offset height (src + 8n) (dst + 8n)

        let decode (offset : V2i) (src : nativeint) (dst : nativeint) (dstInfo : VolumeInfo) =
            assert (Vec.allSmaller offset 4)
            assert (Vec.allGreaterOrEqual offset 0)
            assert (Vec.allSmallerOrEqual dstInfo.Size.XY 4L)
            assert (dstInfo.Size.Z >= 4L)

            BC1.decode offset (src + 8n) dst dstInfo

            let dst = dst |> NativeVolume.ofNativeInt<uint8> dstInfo
            let size = V2i dst.Size.XY

            let pAlpha = src |> NativePtr.ofNativeInt<uint16>

            for y = offset.Y to size.Y - 1 do
                let row = pAlpha.[y]

                for x = offset.X to size.X - 1 do
                    let alpha = (row >>> (4 * x)) &&& 0xFus
                    dst.[x, y, 3] <- Bits.expand4 alpha

    module private BC4 =

        [<Struct; StructLayout(LayoutKind.Explicit, Size = 6)>]
        type Indices =
            [<FieldOffset(0)>] val mutable private D0 : uint32
            [<FieldOffset(4)>] val mutable private D1 : uint16

            member private x.Value
                with inline get() = ((uint64 x.D1) <<< 32) ||| uint64 x.D0
                and inline set(value : uint64) =
                    x.D0 <- uint32 (value &&& 0xFFFFFFFFUL)
                    x.D1 <- uint16 ((value >>> 32) &&& 0xFFFFUL)

            member idx.Item
                with get (x : int, y : int) =
                    let offset = x * 3 + y * 3 * 4
                    (int (idx.Value >>> offset)) &&& 0x07

                and set (x : int, y : int) (value : uint8) =
                    let offset = x * 3 + y * 3 * 4
                    let mask = 0x07UL <<< offset
                    let value = (uint64 value &&& 0x07UL) <<< offset
                    idx.Value <- (idx.Value &&& ~~~mask) ||| value

        [<AutoOpen>]
        module private Encoding =

            let inline computeEndpoints (values : NativeMatrix< ^T>) =
                let mutable l = values.[0, 0]
                let mutable h = values.[0, 0]

                values.Iter(fun (_ : V2l) value ->
                    l <- min l value
                    h <- max h value
                )

                h, l

            let inline private computePalette (min : ^T) (max : ^T) (r0 : ^T) (r1 : ^T) =
                let r0 = int16 r0
                let r1 = int16 r1

                if r0 > r1 then
                    [|
                        r0; r1
                        (6s * r0 + 1s * r1) / 7s
                        (5s * r0 + 2s * r1) / 7s
                        (4s * r0 + 3s * r1) / 7s
                        (3s * r0 + 4s * r1) / 7s
                        (2s * r0 + 5s * r1) / 7s
                        (1s * r0 + 6s * r1) / 7s
                    |]

                else
                    [|
                        r0; r1
                        (4s * r0 + 1s * r1) / 5s;
                        (3s * r0 + 2s * r1) / 5s;
                        (2s * r0 + 3s * r1) / 5s;
                        (1s * r0 + 4s * r1) / 5s;
                        int16 min; int16 max
                    |]

            let computePaletteU = computePalette 0uy 255uy
            let computePaletteS = computePalette -127y 127y

            let inline computeIndices (palette : int16[]) (values : NativeMatrix< ^T>) =
                let mutable indices = Matrix<uint8>(values.Size.XY)

                for y = 0 to int values.SY - 1 do
                    for x = 0 to int values.SX - 1 do
                        let value = int16 values.[x, y]
                        let index = palette |> Array.minIndexBy (fun ci -> if value > ci then value - ci else ci - value)
                        indices.[x, y] <- uint8 index

                indices

        let inline private encode (computePalette : ^T -> ^T -> int16[]) (offset : V2i) (src : nativeint) (srcInfo : VolumeInfo) (dst : nativeint) =
            assert (Vec.allSmaller offset 4)
            assert (Vec.allGreaterOrEqual offset 0)
            assert (Vec.allSmallerOrEqual srcInfo.Size 4L)

            let src = src |> NativeMatrix.ofNativeInt< ^T> (srcInfo.SubXYMatrix(0L))
            let size = V2i src.Size.XY

            let r0, r1 = computeEndpoints src
            let palette = computePalette r0 r1
            let indices = computeIndices palette src

            let pRed = NativePtr.ofNativeInt< ^T> dst
            let pIndices = NativePtr.toByRef (NativePtr.ofNativeInt<Indices> (dst + 2n))

            pRed.[0] <- r0
            pRed.[1] <- r1

            for y = offset.Y to size.Y - 1 do
                for x = offset.X to size.X - 1 do
                    pIndices.[x, y] <- indices.[x, y]

        let encodeU = encode computePaletteU
        let encodeS = encode computePaletteS

        let mirrorCopy (offset : int) (height : int) (src : nativeint) (dst : nativeint) =
            let srcRed = NativePtr.ofNativeInt<uint16> src
            let dstRed = NativePtr.ofNativeInt<uint16> dst
            dstRed.[0] <- srcRed.[0]

            let srcIndex = NativePtr.toByRef (NativePtr.ofNativeInt<Indices> (src + 2n))
            let dstIndex = NativePtr.toByRef (NativePtr.ofNativeInt<Indices> (dst + 2n))

            for x = 0 to 3 do
                for y = 0 to height - 1 do
                    dstIndex.[x, offset + height - 1 - y] <- uint8 srcIndex.[x, y]

                for y = 0 to offset - 1 do
                    dstIndex.[x, y] <- uint8 srcIndex.[x, height - 1]

        let inline private decode (cast : int16 -> ^T) (computePalette : ^T -> ^T -> int16[])
                                  (offset : V2i) (src : nativeint) (dst : nativeint) (dstInfo : VolumeInfo) =
            assert (Vec.allSmaller offset 4)
            assert (Vec.allGreaterOrEqual offset 0)
            assert (Vec.allSmallerOrEqual dstInfo.Size.XY 4L)

            let dst = dst |> NativeVolume.ofNativeInt< ^T> dstInfo
            let size = V2i dst.Size.XY

            let pRed = src |> NativePtr.ofNativeInt< ^T>
            let palette = computePalette pRed.[0] pRed.[1]

            let indices = (src + 2n) |> NativePtr.ofNativeInt<Indices> |> NativePtr.read

            for y = offset.Y to size.Y - 1 do
                for x = offset.X to size.X - 1 do
                    dst.[x, y, 0] <- cast palette.[indices.[x, y]]

        let decodeU = decode uint8 computePaletteU
        let decodeS = decode int8 computePaletteS

    module private BC3 =

        let encode (offset : V2i) (src : nativeint) (srcInfo : VolumeInfo) (dst : nativeint)  =
            assert (Vec.allSmaller offset 4)
            assert (Vec.allGreaterOrEqual offset 0)
            assert (Vec.allSmallerOrEqual srcInfo.Size 4L)
            assert (srcInfo.Size.Z >= 4L)

            BC4.encodeU offset src (srcInfo.SubVolumeAlpha()) dst
            BC1.encode offset src (srcInfo.SubVolumeRGB()) (dst + 8n)

        let mirrorCopy (offset : int) (height : int) (src : nativeint) (dst : nativeint) =
            BC4.mirrorCopy offset height src dst
            BC1.mirrorCopy offset height (src + 8n) (dst + 8n)

        let decode (offset : V2i) (src : nativeint) (dst : nativeint) (dstInfo : VolumeInfo) =
            assert (Vec.allSmaller offset 4)
            assert (Vec.allGreaterOrEqual offset 0)
            assert (Vec.allSmallerOrEqual dstInfo.Size.XY 4L)
            assert (dstInfo.Size.Z >= 4L)

            BC1.decode offset (src + 8n) dst dstInfo
            BC4.decodeU offset src dst (dstInfo.SubVolumeAlpha())

    module private BC5 =

        let private encode (encodeBC4 : V2i -> nativeint -> VolumeInfo -> nativeint -> unit)
                           (offset : V2i) (src : nativeint) (srcInfo : VolumeInfo) (dst : nativeint)  =
            assert (Vec.allSmaller offset 4)
            assert (Vec.allGreaterOrEqual offset 0)
            assert (Vec.allSmallerOrEqual srcInfo.Size 4L)
            assert (srcInfo.Size.Z >= 4L)

            encodeBC4 offset src srcInfo dst
            encodeBC4 offset src (srcInfo.SubVolumeGreen()) (dst + 8n)

        let encodeS = encode BC4.encodeS
        let encodeU = encode BC4.encodeU

        let mirrorCopy (offset : int) (height : int) (src : nativeint) (dst : nativeint) =
            BC4.mirrorCopy offset height src dst
            BC4.mirrorCopy offset height (src + 8n) (dst + 8n)

        let private decode (decodeBC4 : V2i -> nativeint -> nativeint-> VolumeInfo -> unit)
                           (offset : V2i) (src : nativeint) (dst : nativeint) (dstInfo : VolumeInfo) =
            assert (Vec.allSmaller offset 4)
            assert (Vec.allGreaterOrEqual offset 0)
            assert (Vec.allSmallerOrEqual dstInfo.Size.XY 4L)
            assert (dstInfo.Size.Z >= 2L)

            decodeBC4 offset src dst dstInfo
            decodeBC4 offset (src + 8n) dst (dstInfo.SubVolumeGreen())

        let decodeS = decode BC4.decodeS
        let decodeU = decode BC4.decodeU


    let encode (mode : CompressionMode) (src : nativeint) (srcInfo : VolumeInfo) (dst : nativeint) =
        let srcSize = V2i srcInfo.Size.XY
        let srcChannels = int srcInfo.Size.Z

        let blocks = CompressionMode.numberOfBlocks srcSize.XYI mode
        let blockSize = CompressionMode.blockSize mode
        let bytesPerBlock = CompressionMode.bytesPerBlock mode

        let encode =
            match mode with
            | CompressionMode.BC1        -> BC1.encode
            | CompressionMode.BC2        -> BC2.encode
            | CompressionMode.BC3        -> BC3.encode
            | CompressionMode.BC4 signed -> if signed then BC4.encodeS else BC4.encodeU
            | CompressionMode.BC5 signed -> if signed then BC5.encodeS else BC5.encodeU
            | _ ->
                failwithf "Cannot encode using %A compression" mode

        Parallel.For(0, blocks.X * blocks.Y, fun i ->
            let block = V2i(i % blocks.X, i / blocks.X)
            let blockOffset = blockSize * block

            let srcInfo =
                let size = min blockSize (srcSize - blockOffset)
                srcInfo.SubVolume(
                    blockOffset.XYO, V3i(size, srcChannels)
                )

            encode V2i.Zero src srcInfo (dst + nativeint i * bytesPerBlock)
        ) |> ignore

    let mirrorCopy (mode : CompressionMode) (size : V2i) (src : nativeint) (dst : nativeint) =
        let blocks = CompressionMode.numberOfBlocks size.XYI mode
        let blockSize = CompressionMode.blockSize mode
        let blockBytes = CompressionMode.bytesPerBlock mode |> int64
        let rowPitch = blockBytes * int64 blocks.X

        let mirrorCopyImpl =
            match mode with
            | CompressionMode.BC1   -> Some BC1.mirrorCopy
            | CompressionMode.BC2   -> Some BC2.mirrorCopy
            | CompressionMode.BC3   -> Some BC3.mirrorCopy
            | CompressionMode.BC4 _ -> Some BC4.mirrorCopy
            | CompressionMode.BC5 _ -> Some BC5.mirrorCopy
            | _ ->
                None

        match mirrorCopyImpl with
        | Some mirrorCopy ->
            if size.Y > blockSize && size.Y % blockSize <> 0 then
                Log.warn "Mirroring block compressed texture layers with an unaligned height will result in artifacts"

            let dstLastRow = dst + nativeint rowPitch * nativeint (blocks.Y - 1)
            let blockJmp = nativeint blockBytes
            let rowJmp = nativeint rowPitch * 2n

            Parallel.For(0, blocks.X * blocks.Y, fun i ->
                let row = i / blocks.X
                let rowOffset = blockSize * row
                let src = src + nativeint i * blockJmp
                let dst = dstLastRow + nativeint i * blockJmp - nativeint row * rowJmp

                let height = min blockSize (size.Y - rowOffset)
                let offset = if size.Y > blockSize then blockSize - height else 0
                mirrorCopy offset height src dst
            ) |> ignore

        | _ ->
            Log.warn "Flipping %A compressed data not supported" mode
            let sizeInBytes = blockBytes * int64 blocks.X * int64 blocks.Y
            Marshal.Copy(src, dst, sizeInBytes)

    let decode (mode : CompressionMode) (offset : V2i) (size : V2i) (src : nativeint) (dst : nativeint) (dstInfo : VolumeInfo) =
        assert (Vec.allSmaller offset 4)
        assert (Vec.allGreaterOrEqual offset 0)

        let channels = int dstInfo.Size.Z
        let blocks = CompressionMode.numberOfBlocks (offset.XYO + size.XYI) mode
        let blockSize = CompressionMode.blockSize mode
        let bytesPerBlock = CompressionMode.bytesPerBlock mode

        let decode =
            match mode with
            | CompressionMode.BC1        -> BC1.decode
            | CompressionMode.BC2        -> BC2.decode
            | CompressionMode.BC3        -> BC3.decode
            | CompressionMode.BC4 signed -> if signed then BC4.decodeS else BC4.decodeU
            | CompressionMode.BC5 signed -> if signed then BC5.decodeS else BC5.decodeU
            | _ ->
                failwithf "Cannot decode %A compressed data" mode

        Parallel.For(0, blocks.X * blocks.Y, fun i ->
            let block = V2i(i % blocks.X, i / blocks.X)
            let blockOffset = blockSize * block
            let offsetInBlock = max 0 (offset - blockOffset)

            let dstInfo =
                let size = min blockSize (offset + size - blockOffset)

                dstInfo.SubVolume(
                    V3i(blockOffset - offset, 0),
                    V3i(size, channels)
                )

            decode offsetInBlock (src + nativeint i * bytesPerBlock) dst dstInfo
        ) |> ignore