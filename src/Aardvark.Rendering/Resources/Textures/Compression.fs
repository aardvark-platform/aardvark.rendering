namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.NativeInterop
open System.Runtime.InteropServices

#nowarn "9"

type CompressionMode =
    | None = 0
    | BC1 = 1       // DXT1
    | BC2 = 2       // DXT3
    | BC3 = 3       // DXT5
    | BC4 = 4
    | BC5 = 5
    | BC6h = 6
    | BC7 = 7

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

                TextureFormat.CompressedRedRgtc1,             CompressionMode.BC4
                TextureFormat.CompressedSignedRedRgtc1,       CompressionMode.BC4

                TextureFormat.CompressedRgRgtc2,              CompressionMode.BC5
                TextureFormat.CompressedSignedRgRgtc2,        CompressionMode.BC5

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
        | CompressionMode.None -> 0
        | CompressionMode.BC1 | CompressionMode.BC4 -> 8
        | _ -> 16

    let numberOfBlocks (size : V3i) (mode : CompressionMode) =
        let blockSize = blockSize mode
        max 1 ((size + (blockSize - 1)) / blockSize)


module BlockCompression =

    [<AutoOpen>]
    module private NativeUtilities =

        type nativeptr<'a when 'a : unmanaged> with
            member x.Item
                with inline get(i : int) = NativePtr.get x i
                and inline set(i : int) (v : 'a) = NativePtr.set x i v

        let inline (++) (ptr : nativeptr<'a>) (i : int) =
            NativePtr.add ptr i

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

    module private BC4 =

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

    module private BC3 =

        let mirrorCopy (src : nativeptr<byte>) (dst : nativeptr<byte>) =
            BC4.mirrorCopy src dst
            BC1.mirrorCopy (src ++ 8) (dst ++ 8)

    module private BC5 =

        let mirrorCopy (src : nativeptr<byte>) (dst : nativeptr<byte>) =
            BC4.mirrorCopy src dst
            BC4.mirrorCopy (src ++ 8) (dst ++ 8)


    let private mirrorCopyImplemenations =
        LookupTable.lookupTable' [
            CompressionMode.BC1, BC1.mirrorCopy
            CompressionMode.BC2, BC2.mirrorCopy
            CompressionMode.BC3, BC3.mirrorCopy
            CompressionMode.BC4, BC4.mirrorCopy
            CompressionMode.BC5, BC5.mirrorCopy
        ]

    let mirrorCopy (mode : CompressionMode) (size : V2i) (src : nativeint) (dst : nativeint) =
        let blocks = CompressionMode.numberOfBlocks size.XYI mode
        let blockBytes = CompressionMode.bytesPerBlock mode |> int64
        let rowPitch = blockBytes * int64 blocks.X

        match mirrorCopyImplemenations mode with
        | Some mirrorCopyImpl ->
            if size.Y % (CompressionMode.blockSize mode) <> 0 then
                Log.warn "Mirroring block compressed texture layers with an unaligned height will result in artifacts"

            let mutable src = src
            let mutable dst = dst + nativeint rowPitch * nativeint (blocks.Y - 1)
            let blockJmp = nativeint blockBytes
            let dstJmp = nativeint -rowPitch - nativeint rowPitch

            for _ in 0 .. blocks.Y - 1 do
                 for _ in 0 .. blocks.X - 1 do
                     mirrorCopyImpl (NativePtr.ofNativeInt src) (NativePtr.ofNativeInt dst)
                     src <- src + blockJmp
                     dst <- dst + blockJmp
                 dst <- dst + dstJmp

        | _ ->
            Log.warn "Flipping %A compressed data not supported" mode
            let sizeInBytes = blockBytes * int64 blocks.X * int64 blocks.Y
            Marshal.Copy(src, dst, sizeInBytes)