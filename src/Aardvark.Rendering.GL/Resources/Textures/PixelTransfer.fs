namespace Aardvark.Rendering.GL

open Aardvark.Base
open OpenTK.Graphics.OpenGL4

[<AutoOpen>]
module internal PixelTransfer =

    type GeneralPixelDataInfo =
        {
            Channels        : int
            ElementSize     : int
            AlignedLineSize : nativeint
            SizeInBytes     : nativeint
        }

    type CompressedPixelDataInfo =
        {
            SizeInBytes : nativeint
        }

    [<RequireQualifiedAccess>]
    type PixelDataInfo =
        | General    of GeneralPixelDataInfo
        | Compressed of CompressedPixelDataInfo

        member x.SizeInBytes =
            match x with
            | General i -> i.SizeInBytes
            | Compressed i -> i.SizeInBytes

    type CompressedPixelData =
        {
            Size        : V3i
            SizeInBytes : nativeint
            CopyTo      : nativeint -> unit
        }

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module CompressedPixelData =
        let copyTo (dst : nativeint) (data : CompressedPixelData) =
            data.CopyTo dst

    type GeneralPixelData =
        {
            Size   : V3i
            Type   : PixelType
            Format : PixelFormat
            CopyTo : int -> int -> nativeint -> nativeint -> nativeint -> unit
        }

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module GeneralPixelData =
        let copyTo (dst : nativeint) (info : GeneralPixelDataInfo) (data : GeneralPixelData) =
            data.CopyTo info.Channels info.ElementSize info.AlignedLineSize info.SizeInBytes dst

    [<RequireQualifiedAccess>]
    type PixelData =
        | General    of GeneralPixelData
        | Compressed of CompressedPixelData

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module PixelData =
        let copyTo (dst : nativeint) (info : PixelDataInfo) (data : PixelData) =
            match data, info with
            | PixelData.General d, PixelDataInfo.General i ->
                    GeneralPixelData.copyTo dst i d

            | PixelData.Compressed d, PixelDataInfo.Compressed _ ->
                    CompressedPixelData.copyTo dst d

            | _ ->
                failwithf "PixelData.copyTo not possible with %A and %A" data info