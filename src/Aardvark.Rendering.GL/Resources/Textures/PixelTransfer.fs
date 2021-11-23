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

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module GeneralPixelDataInfo =

        let create (alignment : int) (size : V3i) (format : PixelFormat) (typ : PixelType) =
            let elementSize = PixelType.size typ
            let channels = PixelFormat.channels format

            let alignedLineSize =
                let lineSize = nativeint size.X * nativeint elementSize * nativeint channels
                let align = nativeint alignment
                let mask = align - 1n

                if lineSize % align = 0n then lineSize
                else (lineSize + mask) &&& ~~~mask

            let sizeInBytes = alignedLineSize * nativeint size.Y * nativeint size.Z

            { Channels        = channels
              ElementSize     = elementSize
              AlignedLineSize = alignedLineSize
              SizeInBytes     = sizeInBytes }

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
            Copy        : nativeint -> unit
        }

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module CompressedPixelData =
        let getInfo (data : CompressedPixelData) =
            { SizeInBytes = data.SizeInBytes }

        let copy (ptr : nativeint) (data : CompressedPixelData) =
            data.Copy ptr

    type GeneralPixelData =
        {
            Size   : V3i
            Type   : PixelType
            Format : PixelFormat
            Copy   : int -> int -> nativeint -> nativeint -> nativeint -> unit
        }

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module GeneralPixelData =
        let getInfo (alignment : int) (data : GeneralPixelData) =
            GeneralPixelDataInfo.create alignment data.Size data.Format data.Type

        let copy (ptr : nativeint) (info : GeneralPixelDataInfo) (data : GeneralPixelData) =
            data.Copy info.Channels info.ElementSize info.AlignedLineSize info.SizeInBytes ptr

    [<RequireQualifiedAccess>]
    type PixelData =
        | General    of GeneralPixelData
        | Compressed of CompressedPixelData

        member x.Size =
            match x with
            | General d -> d.Size
            | Compressed d -> d.Size

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module PixelData =
        let getInfo (alignment : int) (data : PixelData) =
            match data with
            | PixelData.General d ->
                d |> GeneralPixelData.getInfo alignment |> PixelDataInfo.General

            | PixelData.Compressed d ->
                d |> CompressedPixelData.getInfo |> PixelDataInfo.Compressed

        let copy (ptr : nativeint) (info : PixelDataInfo) (data : PixelData) =
            match data, info with
            | PixelData.General d, PixelDataInfo.General i ->
                    GeneralPixelData.copy ptr i d

            | PixelData.Compressed d, PixelDataInfo.Compressed _ ->
                    CompressedPixelData.copy ptr d

            | _ ->
                failwithf "PixelData.copy not possible with %A and %A" data info