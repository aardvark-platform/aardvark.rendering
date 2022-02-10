namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.NativeInterop
open System.Runtime.InteropServices

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

    [<AutoOpen>]
    module private NativeUtilities =

        type nativeptr<'a when 'a : unmanaged> with
            member x.Item
                with inline get(i : int) = NativePtr.get x i
                and inline set(i : int) (v : 'a) = NativePtr.set x i v

        let inline (++) (ptr : nativeptr<'a>) (i : int) =
            NativePtr.add ptr i

        module NativeVolume =

            let setC4b (x : int) (y : int) (color : C4b) (volume : NativeVolume<uint8>) =
                for c = 0 to min 3 (int volume.SZ - 1) do
                    volume.[x, y, c] <- color.[c]

    [<AutoOpen>]
    module private ColorUtilities =

        module Bits =

            let expand4 =
                Array.init 16 (fun i -> uint8 (i * 17))

            let expand5 =
                Array.init 32 (fun i -> uint8 ((i <<< 3) ||| (i >>> 2)))

            let expand6 =
                Array.init 64 (fun i -> uint8 ((i <<< 2) ||| (i >>> 4)))

        module C4b =

            let two = C4b(2, 2, 2, 2)
            let three = C4b(3, 3, 3, 3)

            let fromR5G6B5 (color : uint16) =
                let r = int (color &&& 0xF800us) >>> 11
                let g = int (color &&& 0x07E0us) >>>  5
                let b = int (color &&& 0x001Fus)

                C4b(Bits.expand5.[r], Bits.expand6.[g], Bits.expand5.[b])

    // See: https://docs.microsoft.com/en-us/windows/win32/direct3d10/d3d10-graphics-programming-guide-resources-block-compression
    module private BC1 =

        let mirrorCopy (src : nativeptr<byte>) (dst : nativeptr<byte>) =
            dst.[0] <- src.[0]
            dst.[1] <- src.[1]
            dst.[2] <- src.[2]
            dst.[3] <- src.[3]
            dst.[7] <- src.[4]
            dst.[6] <- src.[5]
            dst.[5] <- src.[6]
            dst.[4] <- src.[7]

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

            colors.[0] <- C4b.fromR5G6B5 pColors.[0]
            colors.[1] <- C4b.fromR5G6B5 pColors.[1]

            if pColors.[0] > pColors.[1] then
                colors.[2] <- ((colors.[0] / C4b.three) * C4b.two) + (colors.[1] / C4b.three)
                colors.[3] <- (colors.[0] / C4b.three) + ((colors.[1] / C4b.three) * C4b.two)
            else
                colors.[2] <- (colors.[0] / C4b.two) + (colors.[1] / C4b.two)
                colors.[3] <- C4b.Zero

            for y = offset.Y to size.Y - 1 do
                let row = int pIndices.[y]

                for x = offset.X to size.X - 1 do
                    let index = (row >>> (2 * x)) &&& 0x03
                    dst |> NativeVolume.setC4b x y colors.[index]

    module private BC2 =

        let mirrorCopy (src : nativeptr<byte>) (dst : nativeptr<byte>) =
            dst.[6] <- src.[0]
            dst.[7] <- src.[1]
            dst.[4] <- src.[2]
            dst.[5] <- src.[3]
            dst.[2] <- src.[4]
            dst.[3] <- src.[5]
            dst.[0] <- src.[6]
            dst.[1] <- src.[7]
            BC1.mirrorCopy (src ++ 8) (dst ++ 8)

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
                let row = int pAlpha.[y]

                for x = offset.X to size.X - 1 do
                    let alpha = (row >>> (4 * x)) &&& 0xF
                    dst.[x, y, 3] <- Bits.expand4.[alpha]

    module private BC4 =

        [<Struct; StructLayout(LayoutKind.Explicit, Size = 6)>]
        type private Indices =
            {
                [<FieldOffset(0)>] L0 : int
                [<FieldOffset(3)>] L1 : int
            }

            member idx.Item(x : int, y : int) =
                let d, row =
                    if y < 2 then
                        idx.L0, y
                    else
                        idx.L1, y - 2

                let offset = x * 3 + row * 3 * 4
                (d >>> offset) &&& 0x07

        let mirrorCopy (src : nativeptr<byte>) (dst : nativeptr<byte>) =
            let line_0_1 = uint32 src.[2] + 256u * (uint32 src.[3] + 256u * uint32 src.[4]);
            let line_2_3 = uint32 src.[5] + 256u * (uint32 src.[6] + 256u * uint32 src.[7]);
            let line_1_0 = ((line_0_1 &&& 0x000fffu) <<< 12) ||| ((line_0_1 &&& 0xfff000u) >>> 12);
            let line_3_2 = ((line_2_3 &&& 0x000fffu) <<< 12) |||  ((line_2_3 &&& 0xfff000u) >>> 12);
            dst.[0] <- src.[0]
            dst.[1] <- src.[1]
            dst.[2] <- byte (line_3_2 &&& 0xffu)
            dst.[3] <- byte ((line_3_2 &&& 0xff00u) >>> 8)
            dst.[4] <- byte ((line_3_2 &&& 0xff0000u) >>> 16)
            dst.[5] <- byte (line_1_0 &&& 0xffu)
            dst.[6] <- byte ((line_1_0 &&& 0xff00u) >>> 8)
            dst.[7] <- byte ((line_1_0 &&& 0xff0000u) >>> 16)

        let decodeS (offset : V2i) (src : nativeint) (dst : nativeint) (dstInfo : VolumeInfo) =
            assert (Vec.allSmaller offset 4)
            assert (Vec.allGreaterOrEqual offset 0)
            assert (Vec.allSmallerOrEqual dstInfo.Size.XY 4L)

            let dst = dst |> NativeVolume.ofNativeInt<int8> dstInfo
            let size = V2i dst.Size.XY

            let pRed = src |> NativePtr.ofNativeInt<int8>

            let red = Array.zeroCreate 8
            red.[0] <- int16 pRed.[0]
            red.[1] <- int16 pRed.[1]

            if red.[0] > red.[1] then
                red.[2] <- (6s * red.[0] + 1s * red.[1]) / 7s
                red.[3] <- (5s * red.[0] + 2s * red.[1]) / 7s
                red.[4] <- (4s * red.[0] + 3s * red.[1]) / 7s
                red.[5] <- (3s * red.[0] + 4s * red.[1]) / 7s
                red.[6] <- (2s * red.[0] + 5s * red.[1]) / 7s
                red.[7] <- (1s * red.[0] + 6s * red.[1]) / 7s

            else
                red.[2] <- (4s * red.[0] + 1s * red.[1]) / 5s;
                red.[3] <- (3s * red.[0] + 2s * red.[1]) / 5s;
                red.[4] <- (2s * red.[0] + 3s * red.[1]) / 5s;
                red.[5] <- (1s * red.[0] + 4s * red.[1]) / 5s;
                red.[6] <- -127s
                red.[7] <- 127s

            let indices = (src + 2n) |> NativePtr.ofNativeInt<Indices> |> NativePtr.read

            for y = offset.Y to size.Y - 1 do
                for x = offset.X to size.X - 1 do
                    dst.[x, y, 0] <- int8 red.[indices.[x, y]]

        let decodeU (offset : V2i) (src : nativeint) (dst : nativeint) (dstInfo : VolumeInfo) =
            assert (Vec.allSmaller offset 4)
            assert (Vec.allGreaterOrEqual offset 0)
            assert (Vec.allSmallerOrEqual dstInfo.Size.XY 4L)

            let dst = dst |> NativeVolume.ofNativeInt<uint8> dstInfo
            let size = V2i dst.Size.XY

            let pRed = src |> NativePtr.ofNativeInt<uint8>

            let red = Array.zeroCreate 8
            red.[0] <- uint16 pRed.[0]
            red.[1] <- uint16 pRed.[1]

            if red.[0] > red.[1] then
                red.[2] <- (6us * red.[0] + 1us * red.[1]) / 7us
                red.[3] <- (5us * red.[0] + 2us * red.[1]) / 7us
                red.[4] <- (4us * red.[0] + 3us * red.[1]) / 7us
                red.[5] <- (3us * red.[0] + 4us * red.[1]) / 7us
                red.[6] <- (2us * red.[0] + 5us * red.[1]) / 7us
                red.[7] <- (1us * red.[0] + 6us * red.[1]) / 7us

            else
                red.[2] <- (4us * red.[0] + 1us * red.[1]) / 5us;
                red.[3] <- (3us * red.[0] + 2us * red.[1]) / 5us;
                red.[4] <- (2us * red.[0] + 3us * red.[1]) / 5us;
                red.[5] <- (1us * red.[0] + 4us * red.[1]) / 5us;
                red.[6] <- 0us
                red.[7] <- 255us

            let indices = (src + 2n) |> NativePtr.ofNativeInt<Indices> |> NativePtr.read

            for y = offset.Y to size.Y - 1 do
                for x = offset.X to size.X - 1 do
                    dst.[x, y, 0] <- uint8 red.[indices.[x, y]]

    module private BC3 =

        let mirrorCopy (src : nativeptr<byte>) (dst : nativeptr<byte>) =
            BC4.mirrorCopy src dst
            BC1.mirrorCopy (src ++ 8) (dst ++ 8)

        let decode (offset : V2i) (src : nativeint) (dst : nativeint) (dstInfo : VolumeInfo) =
            assert (Vec.allSmaller offset 4)
            assert (Vec.allGreaterOrEqual offset 0)
            assert (Vec.allSmallerOrEqual dstInfo.Size.XY 4L)
            assert (dstInfo.Size.Z >= 4L)

            let alphaVolume =
                let offset = V3l(0, 0, 3)
                let size = V3l(dstInfo.Size.XY, 1L)
                dstInfo.SubVolume(offset, size)

            BC1.decode offset (src + 8n) dst dstInfo
            BC4.decodeU offset src dst alphaVolume

    module private BC5 =

        let mirrorCopy (src : nativeptr<byte>) (dst : nativeptr<byte>) =
            BC4.mirrorCopy src dst
            BC4.mirrorCopy (src ++ 8) (dst ++ 8)

        let private decode (decodeBC4 : V2i -> nativeint -> nativeint-> VolumeInfo -> unit)
                           (offset : V2i) (src : nativeint) (dst : nativeint) (dstInfo : VolumeInfo) =
            assert (Vec.allSmaller offset 4)
            assert (Vec.allGreaterOrEqual offset 0)
            assert (Vec.allSmallerOrEqual dstInfo.Size.XY 4L)
            assert (dstInfo.Size.Z >= 2L)

            let greenVolume =
                let offset = V3l(0, 0, 1)
                let size = V3l(dstInfo.Size.XY, 1L)
                dstInfo.SubVolume(offset, size)

            decodeBC4 offset src dst dstInfo
            decodeBC4 offset (src + 8n) dst greenVolume

        let decodeS = decode BC4.decodeS
        let decodeU = decode BC4.decodeU


    let mirrorCopy (mode : CompressionMode) (size : V2i) (src : nativeint) (dst : nativeint) =
        let blocks = CompressionMode.numberOfBlocks size.XYI mode
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
            if size.Y % (CompressionMode.blockSize mode) <> 0 then
                Log.warn "Mirroring block compressed texture layers with an unaligned height will result in artifacts"

            let mutable src = src
            let mutable dst = dst + nativeint rowPitch * nativeint (blocks.Y - 1)
            let blockJmp = nativeint blockBytes
            let dstJmp = nativeint -rowPitch - nativeint rowPitch

            for _ in 0 .. blocks.Y - 1 do
                 for _ in 0 .. blocks.X - 1 do
                     mirrorCopy (NativePtr.ofNativeInt src) (NativePtr.ofNativeInt dst)
                     src <- src + blockJmp
                     dst <- dst + blockJmp
                 dst <- dst + dstJmp

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

        let mutable src = src

        for y in 0 .. blocks.Y - 1 do
            for x in 0 .. blocks.X - 1 do
                let blockOffset = blockSize * V2i(x, y)
                let offsetInBlock = max 0 (offset - blockOffset)

                let dstInfo =
                    let size = min blockSize (offset + size - blockOffset)

                    dstInfo.SubVolume(
                        V3i(blockOffset - offset, 0),
                        V3i(size, channels)
                    )

                decode offsetInBlock src dst dstInfo
                src <- src + bytesPerBlock