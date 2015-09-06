namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Collections.Concurrent
open System.Runtime.InteropServices
open Aardvark.Base
open OpenTK
open OpenTK.Platform
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL

type ChannelKind =
    | Signed 
    | Unsigned
    | Float
    | Norm
    | SignedNorm 

type ChannelType = ChannelType of ChannelKind * int * int * int * int

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ChannelType =

    // https://www.opengl.org/registry/api/GL/glcorearb.h

    let R8              = ChannelType(Norm, 8, 0, 0, 0)
    let R8_SNORM        = ChannelType(SignedNorm, 8, 0, 0, 0)
    let R16             = ChannelType(Norm, 16, 0, 0, 0)
    let R16_SNORM       = ChannelType(SignedNorm, 16, 0, 0, 0)
    let RG8             = ChannelType(Norm, 8, 8, 0, 0)
    let RG8_SNORM       = ChannelType(SignedNorm, 8, 8, 0, 0)
    let RG16            = ChannelType(Norm, 16, 16, 0, 0)
    let RG16_SNORM      = ChannelType(SignedNorm, 16, 16, 0, 0)
    let R3_G3_B2        = ChannelType(Norm, 3, 3, 2, 0)
    let RGB4            = ChannelType(Norm, 4, 4, 4, 0)
    let RGB5            = ChannelType(Norm, 5, 5, 5, 0)
    let RGB8            = ChannelType(Norm, 8, 8, 8, 0)
    let RGB8_SNORM      = ChannelType(SignedNorm, 8, 8, 8, 0)
    let RGB10           = ChannelType(Norm, 10, 10, 10, 0)
    let RGB12           = ChannelType(Norm, 12, 12, 12, 0)
    let RGB16_SNORM     = ChannelType(Norm, 16, 16, 16, 0)
    let RGBA2           = ChannelType(Norm, 2, 2, 2, 2)
    let RGBA4           = ChannelType(Norm, 4, 4, 4, 4)
    let RGB5_A1         = ChannelType(Norm, 5, 5, 5, 1)
    let RGBA8           = ChannelType(Norm, 8, 8, 8, 8)
    let RGBA8_SNORM     = ChannelType(SignedNorm, 8, 8, 8, 8)
    let RGB10_A2        = ChannelType(Norm, 10, 10, 10, 2)
    let RGB10_A2UI      = ChannelType(Unsigned, 10, 10, 10, 2)
    let RGBA12          = ChannelType(Norm, 12, 12, 12, 12)
    let RGBA16          = ChannelType(Norm, 16, 16, 16, 16)
    let SRGB8           = ChannelType(Norm, 8, 8, 8, 0)
    let SRGB8_ALPHA8    = ChannelType(Norm, 8, 8, 8, 8)
    let R16F            = ChannelType(Float, 16, 0, 0, 0)
    let RG16F           = ChannelType(Float, 16, 16, 0, 0)
    let RGB16F          = ChannelType(Float, 16, 16, 16, 0)
    let RGBA16F         = ChannelType(Float, 16, 16, 16, 16)
    let R32F            = ChannelType(Float, 32, 0, 0, 0)
    let RG32F           = ChannelType(Float, 32, 32, 0, 0)
    let RGB32F          = ChannelType(Float, 32, 32, 32, 0)
    let RGBA32F         = ChannelType(Float, 32, 32, 32, 32)
    let R11F_G11F_B10F  = ChannelType(Float, 11, 11, 10, 0)
    let RGB9_E5         = ChannelType(Norm, 9, 9, 9, 0)
    let R8I             = ChannelType(Signed, 8, 0, 0, 0)
    let R8UI            = ChannelType(Unsigned, 8, 0, 0, 0)
    let R16I            = ChannelType(Signed, 16, 0, 0, 0)
    let R16UI           = ChannelType(Unsigned, 16, 0, 0, 0)
    let R32I            = ChannelType(Signed, 32, 0, 0, 0)
    let R32UI           = ChannelType(Unsigned, 32, 0, 0, 0)
    let RG8I            = ChannelType(Signed, 8, 8, 0, 0)
    let RG8UI           = ChannelType(Unsigned, 8, 8, 0, 0)
    let RG16I           = ChannelType(Signed, 16, 16, 0, 0)
    let RG16UI          = ChannelType(Unsigned, 16, 16, 0, 0)
    let RG32I           = ChannelType(Signed, 32, 32, 0, 0)
    let RG32UI          = ChannelType(Unsigned, 32, 32, 0, 0)
    let RGB8I           = ChannelType(Signed, 8, 8, 8, 0)
    let RGB8UI          = ChannelType(Unsigned, 8, 8, 8, 0)
    let RGB16I          = ChannelType(Signed, 16, 16, 16, 0)
    let RGB16UI         = ChannelType(Unsigned, 16, 16, 16, 0)
    let RGB32I          = ChannelType(Signed, 32, 32, 32, 0)
    let RGB32UI         = ChannelType(Unsigned, 32, 32, 32, 0)
    let RGBA8I          = ChannelType(Signed, 8, 8, 8, 8)
    let RGBA8UI         = ChannelType(Unsigned, 8, 8, 8, 8)
    let RGBA16I         = ChannelType(Signed, 16, 16, 16, 16)
    let RGBA16UI        = ChannelType(Unsigned, 16, 16, 16, 16)
    let RGBA32I         = ChannelType(Signed, 32, 32, 32, 32)
    let RGBA32UI        = ChannelType(Unsigned, 32, 32, 32, 32)



    let (|R8|_|) (t : ChannelType) = match t with | ChannelType(Norm, 8, 0, 0, 0) -> Some R8 | _ -> None
    let (|R8_SNORM|_|) (t : ChannelType) = match t with | ChannelType(Signed, 8, 0, 0, 0) -> Some R8_SNORM | _ -> None
    let (|R16|_|) (t : ChannelType) = match t with | ChannelType(Norm, 16, 0, 0, 0) -> Some R16 | _ -> None
    let (|R16_SNORM|_|) (t : ChannelType) = match t with | ChannelType(Signed, 16, 0, 0, 0) -> Some R16_SNORM | _ -> None
    let (|RG8|_|) (t : ChannelType) = match t with | ChannelType(Norm, 8, 8, 0, 0) -> Some RG8 | _ -> None
    let (|RG8_SNORM|_|) (t : ChannelType) = match t with | ChannelType(Signed, 8, 8, 0, 0) -> Some RG8_SNORM | _ -> None
    let (|RG16|_|) (t : ChannelType) = match t with | ChannelType(Norm, 16, 16, 0, 0) -> Some RG16 | _ -> None
    let (|RG16_SNORM|_|) (t : ChannelType) = match t with | ChannelType(Signed, 16, 16, 0, 0) -> Some RG16_SNORM | _ -> None
    let (|R3_G3_B2|_|) (t : ChannelType) = match t with | ChannelType(Norm, 3, 3, 2, 0) -> Some R3_G3_B2 | _ -> None
    let (|RGB4|_|) (t : ChannelType) = match t with | ChannelType(Norm, 4, 4, 4, 0) -> Some RGB4 | _ -> None
    let (|RGB5|_|) (t : ChannelType) = match t with | ChannelType(Norm, 5, 5, 5, 0) -> Some RGB5 | _ -> None
    let (|RGB8|_|) (t : ChannelType) = match t with | ChannelType(Norm, 8, 8, 8, 0) -> Some RGB8 | _ -> None
    let (|RGB8_SNORM|_|) (t : ChannelType) = match t with | ChannelType(Signed, 8, 8, 8, 0) -> Some RGB8_SNORM | _ -> None
    let (|RGB10|_|) (t : ChannelType) = match t with | ChannelType(Norm, 10, 10, 10, 0) -> Some RGB10 | _ -> None
    let (|RGB12|_|) (t : ChannelType) = match t with | ChannelType(Norm, 12, 12, 12, 0) -> Some RGB12 | _ -> None
    let (|RGB16_SNORM|_|) (t : ChannelType) = match t with | ChannelType(Norm, 16, 16, 16, 0) -> Some RGB16_SNORM | _ -> None
    let (|RGBA2|_|) (t : ChannelType) = match t with | ChannelType(Norm, 2, 2, 2, 2) -> Some RGBA2 | _ -> None
    let (|RGBA4|_|) (t : ChannelType) = match t with | ChannelType(Norm, 4, 4, 4, 4) -> Some RGBA4 | _ -> None
    let (|RGB5_A1|_|) (t : ChannelType) = match t with | ChannelType(Norm, 5, 5, 5, 1) -> Some RGB5_A1 | _ -> None
    let (|RGBA8|_|) (t : ChannelType) = match t with | ChannelType(Norm, 8, 8, 8, 8) -> Some RGBA8 | _ -> None
    let (|RGBA8_SNORM|_|) (t : ChannelType) = match t with | ChannelType(Signed, 8, 8, 8, 8) -> Some RGBA8_SNORM | _ -> None
    let (|RGB10_A2|_|) (t : ChannelType) = match t with | ChannelType(Norm, 10, 10, 10, 2) -> Some RGB10_A2 | _ -> None
    let (|RGB10_A2UI|_|) (t : ChannelType) = match t with | ChannelType(Unsigned, 10, 10, 10, 2) -> Some RGB10_A2UI | _ -> None
    let (|RGBA12|_|) (t : ChannelType) = match t with | ChannelType(Norm, 12, 12, 12, 12) -> Some RGBA12 | _ -> None
    let (|RGBA16|_|) (t : ChannelType) = match t with | ChannelType(Norm, 16, 16, 16, 16) -> Some RGBA16 | _ -> None
    let (|SRGB8|_|) (t : ChannelType) = match t with | ChannelType(Norm, 8, 8, 8, 0) -> Some SRGB8 | _ -> None
    let (|SRGB8_ALPHA8|_|) (t : ChannelType) = match t with | ChannelType(Norm, 8, 8, 8, 8) -> Some SRGB8_ALPHA8 | _ -> None
    let (|R16F|_|) (t : ChannelType) = match t with | ChannelType(Float, 16, 0, 0, 0) -> Some R16F | _ -> None
    let (|RG16F|_|) (t : ChannelType) = match t with | ChannelType(Float, 16, 16, 0, 0) -> Some RG16F | _ -> None
    let (|RGB16F|_|) (t : ChannelType) = match t with | ChannelType(Float, 16, 16, 16, 0) -> Some RGB16F | _ -> None
    let (|RGBA16F|_|) (t : ChannelType) = match t with | ChannelType(Float, 16, 16, 16, 16) -> Some RGBA16F | _ -> None
    let (|R32F|_|) (t : ChannelType) = match t with | ChannelType(Float, 32, 0, 0, 0) -> Some R32F | _ -> None
    let (|RG32F|_|) (t : ChannelType) = match t with | ChannelType(Float, 32, 32, 0, 0) -> Some RG32F | _ -> None
    let (|RGB32F|_|) (t : ChannelType) = match t with | ChannelType(Float, 32, 32, 32, 0) -> Some RGB32F | _ -> None
    let (|RGBA32F|_|) (t : ChannelType) = match t with | ChannelType(Float, 32, 32, 32, 32) -> Some RGBA32F | _ -> None
    let (|R11F_G11F_B10F|_|) (t : ChannelType) = match t with | ChannelType(Float, 11, 11, 10, 0) -> Some R11F_G11F_B10F | _ -> None
    let (|RGB9_E5|_|) (t : ChannelType) = match t with | ChannelType(Norm, 9, 9, 9, 0) -> Some RGB9_E5 | _ -> None
    let (|R8I|_|) (t : ChannelType) = match t with | ChannelType(Signed, 8, 0, 0, 0) -> Some R8I | _ -> None
    let (|R8UI|_|) (t : ChannelType) = match t with | ChannelType(Unsigned, 8, 0, 0, 0) -> Some R8UI | _ -> None
    let (|R16I|_|) (t : ChannelType) = match t with | ChannelType(Signed, 16, 0, 0, 0) -> Some R16I | _ -> None
    let (|R16UI|_|) (t : ChannelType) = match t with | ChannelType(Unsigned, 16, 0, 0, 0) -> Some R16UI | _ -> None
    let (|R32I|_|) (t : ChannelType) = match t with | ChannelType(Signed, 32, 0, 0, 0) -> Some R32I | _ -> None
    let (|R32UI|_|) (t : ChannelType) = match t with | ChannelType(Unsigned, 32, 0, 0, 0) -> Some R32UI | _ -> None
    let (|RG8I|_|) (t : ChannelType) = match t with | ChannelType(Signed, 8, 8, 0, 0) -> Some RG8I | _ -> None
    let (|RG8UI|_|) (t : ChannelType) = match t with | ChannelType(Unsigned, 8, 8, 0, 0) -> Some RG8UI | _ -> None
    let (|RG16I|_|) (t : ChannelType) = match t with | ChannelType(Signed, 16, 16, 0, 0) -> Some RG16I | _ -> None
    let (|RG16UI|_|) (t : ChannelType) = match t with | ChannelType(Unsigned, 16, 16, 0, 0) -> Some RG16UI | _ -> None
    let (|RG32I|_|) (t : ChannelType) = match t with | ChannelType(Signed, 32, 32, 0, 0) -> Some RG32I | _ -> None
    let (|RG32UI|_|) (t : ChannelType) = match t with | ChannelType(Unsigned, 32, 32, 0, 0) -> Some RG32UI | _ -> None
    let (|RGB8I|_|) (t : ChannelType) = match t with | ChannelType(Signed, 8, 8, 8, 0) -> Some RGB8I | _ -> None
    let (|RGB8UI|_|) (t : ChannelType) = match t with | ChannelType(Unsigned, 8, 8, 8, 0) -> Some RGB8UI | _ -> None
    let (|RGB16I|_|) (t : ChannelType) = match t with | ChannelType(Signed, 16, 16, 16, 0) -> Some RGB16I | _ -> None
    let (|RGB16UI|_|) (t : ChannelType) = match t with | ChannelType(Unsigned, 16, 16, 16, 0) -> Some RGB16UI | _ -> None
    let (|RGB32I|_|) (t : ChannelType) = match t with | ChannelType(Signed, 32, 32, 32, 0) -> Some RGB32I | _ -> None
    let (|RGB32UI|_|) (t : ChannelType) = match t with | ChannelType(Unsigned, 32, 32, 32, 0) -> Some RGB32UI | _ -> None
    let (|RGBA8I|_|) (t : ChannelType) = match t with | ChannelType(Signed, 8, 8, 8, 8) -> Some RGBA8I | _ -> None
    let (|RGBA8UI|_|) (t : ChannelType) = match t with | ChannelType(Unsigned, 8, 8, 8, 8) -> Some RGBA8UI | _ -> None
    let (|RGBA16I|_|) (t : ChannelType) = match t with | ChannelType(Signed, 16, 16, 16, 16) -> Some RGBA16I | _ -> None
    let (|RGBA16UI|_|) (t : ChannelType) = match t with | ChannelType(Unsigned, 16, 16, 16, 16) -> Some RGBA16UI | _ -> None
    let (|RGBA32I|_|) (t : ChannelType) = match t with | ChannelType(Signed, 32, 32, 32, 32) -> Some RGBA32I | _ -> None
    let (|RGBA32UI|_|) (t : ChannelType) = match t with | ChannelType(Unsigned, 32, 32, 32, 32) -> Some RGBA32UI | _ -> None

    let fromGlFormat (t : PixelInternalFormat) : ChannelType =
        match t with
            | PixelInternalFormat.R8 -> R8
            | PixelInternalFormat.R8Snorm -> R8_SNORM
            | PixelInternalFormat.R16 -> R16
    //        | 0x8F98 |> unbox<PixelInternalFormat> -> R16_SNORM
    //        | 0x8F9A |> unbox<PixelInternalFormat> -> RGB16_SNORM
    //        | 0x8F99 |> unbox<PixelInternalFormat> -> RG16_SNORM
            | PixelInternalFormat.Rg8 -> RG8
            | PixelInternalFormat.Rg8Snorm -> RG8_SNORM
            | PixelInternalFormat.Rg16 -> RG16
            | PixelInternalFormat.R3G3B2 -> R3_G3_B2
            | PixelInternalFormat.Rgb4 -> RGB4
            | PixelInternalFormat.Rgb5 -> RGB5
            | PixelInternalFormat.Rgb8 -> RGB8
            | PixelInternalFormat.Rgb8Snorm -> RGB8_SNORM
            | PixelInternalFormat.Rgb10 -> RGB10
            | PixelInternalFormat.Rgb12 -> RGB12
            | PixelInternalFormat.Rgba2 -> RGBA2
            | PixelInternalFormat.Rgba4 -> RGBA4
            | PixelInternalFormat.Rgb5A1 -> RGB5_A1
            | PixelInternalFormat.Rgba8 -> RGBA8
            | PixelInternalFormat.Rgba8Snorm -> RGBA8_SNORM
            | PixelInternalFormat.Rgb10A2 -> RGB10_A2
            | PixelInternalFormat.Rgb10A2ui -> RGB10_A2UI
            | PixelInternalFormat.Rgba12 -> RGBA12
            | PixelInternalFormat.Rgba16 -> RGBA16
            | PixelInternalFormat.Srgb8 -> SRGB8
            | PixelInternalFormat.Srgb8Alpha8 -> SRGB8_ALPHA8
            | PixelInternalFormat.R16f -> R16F
            | PixelInternalFormat.Rg16f -> RG16F
            | PixelInternalFormat.Rgb16f -> RGB16F
            | PixelInternalFormat.Rgba16f -> RGBA16F
            | PixelInternalFormat.R32f -> R32F
            | PixelInternalFormat.Rg32f -> RG32F
            | PixelInternalFormat.Rgb32f -> RGB32F
            | PixelInternalFormat.Rgba32f -> RGBA32F
            | PixelInternalFormat.R11fG11fB10f -> R11F_G11F_B10F
            | PixelInternalFormat.Rgb9E5 -> RGB9_E5
            | PixelInternalFormat.R8i -> R8I
            | PixelInternalFormat.R8ui -> R8UI
            | PixelInternalFormat.R16i -> R16I
            | PixelInternalFormat.R16ui -> R16UI
            | PixelInternalFormat.R32i -> R32I
            | PixelInternalFormat.R32ui -> R32UI
            | PixelInternalFormat.Rg8i -> RG8I
            | PixelInternalFormat.Rg8ui -> RG8UI
            | PixelInternalFormat.Rg16i -> RG16I
            | PixelInternalFormat.Rg16ui -> RG16UI
            | PixelInternalFormat.Rg32i -> RG32I
            | PixelInternalFormat.Rg32ui -> RG32UI
            | PixelInternalFormat.Rgb8i -> RGB8I
            | PixelInternalFormat.Rgb8ui -> RGB8UI
            | PixelInternalFormat.Rgb16i -> RGB16I
            | PixelInternalFormat.Rgb16ui -> RGB16UI
            | PixelInternalFormat.Rgb32i -> RGB32I
            | PixelInternalFormat.Rgb32ui -> RGB32UI
            | PixelInternalFormat.Rgba8i -> RGBA8I
            | PixelInternalFormat.Rgba8ui -> RGBA8UI
            | PixelInternalFormat.Rgba16i -> RGBA16I
            | PixelInternalFormat.Rgba16ui -> RGBA16UI
            | PixelInternalFormat.Rgba32i -> RGBA32I
            | PixelInternalFormat.Rgba32ui -> RGBA32UI
            | PixelInternalFormat.CompressedRgb -> RGB8UI
            | PixelInternalFormat.CompressedRgba -> RGBA8UI
            | _ -> failwith "unknown internal format" 


    let toGlInternalFormat (t : ChannelType) : PixelInternalFormat =
        match t with
        | R8 -> PixelInternalFormat.R8
        | R8_SNORM -> PixelInternalFormat.R8Snorm
        | R16 -> PixelInternalFormat.R16
        | R16_SNORM -> 0x8F98 |> unbox<PixelInternalFormat> //PixelInternalFormat.R16SNorm
        | RG8 -> PixelInternalFormat.Rg8
        | RG8_SNORM -> PixelInternalFormat.Rg8Snorm
        | RG16 -> PixelInternalFormat.Rg16
        | RG16_SNORM -> 0x8F99 |> unbox<PixelInternalFormat> //PixelInternalFormat.Rg16Snorm
        | R3_G3_B2 -> PixelInternalFormat.R3G3B2
        | RGB4 -> PixelInternalFormat.Rgb4
        | RGB5 -> PixelInternalFormat.Rgb5
        | RGB8 -> PixelInternalFormat.Rgb8
        | RGB8_SNORM -> PixelInternalFormat.Rgb8Snorm
        | RGB10 -> PixelInternalFormat.Rgb10
        | RGB12 -> PixelInternalFormat.Rgb12
        | RGB16_SNORM -> 0x8F9A |> unbox<PixelInternalFormat> //PixelInternalFormat.Rgb16Snorm
        | RGBA2 -> PixelInternalFormat.Rgba2
        | RGBA4 -> PixelInternalFormat.Rgba4
        | RGB5_A1 -> PixelInternalFormat.Rgb5A1
        | RGBA8 -> PixelInternalFormat.Rgba8
        | RGBA8_SNORM -> PixelInternalFormat.Rgba8Snorm
        | RGB10_A2 -> PixelInternalFormat.Rgb10A2
        | RGB10_A2UI -> PixelInternalFormat.Rgb10A2ui
        | RGBA12 -> PixelInternalFormat.Rgba12
        | RGBA16 -> PixelInternalFormat.Rgba16
        | SRGB8 -> PixelInternalFormat.Srgb8
        | SRGB8_ALPHA8 -> PixelInternalFormat.Srgb8Alpha8
        | R16F -> PixelInternalFormat.R16f
        | RG16F -> PixelInternalFormat.Rg16f
        | RGB16F -> PixelInternalFormat.Rgb16f
        | RGBA16F -> PixelInternalFormat.Rgba16f
        | R32F -> PixelInternalFormat.R32f
        | RG32F -> PixelInternalFormat.Rg32f
        | RGB32F -> PixelInternalFormat.Rgb32f
        | RGBA32F -> PixelInternalFormat.Rgba32f
        | R11F_G11F_B10F -> PixelInternalFormat.R11fG11fB10f
        | RGB9_E5 -> PixelInternalFormat.Rgb9E5
        | R8I -> PixelInternalFormat.R8i
        | R8UI -> PixelInternalFormat.R8ui
        | R16I -> PixelInternalFormat.R16i
        | R16UI -> PixelInternalFormat.R16ui
        | R32I -> PixelInternalFormat.R32i
        | R32UI -> PixelInternalFormat.R32ui
        | RG8I -> PixelInternalFormat.Rg8i
        | RG8UI -> PixelInternalFormat.Rg8ui
        | RG16I -> PixelInternalFormat.Rg16i
        | RG16UI -> PixelInternalFormat.Rg16ui
        | RG32I -> PixelInternalFormat.Rg32i
        | RG32UI -> PixelInternalFormat.Rg32ui
        | RGB8I -> PixelInternalFormat.Rgb8i
        | RGB8UI -> PixelInternalFormat.Rgb8
        | RGB16I -> PixelInternalFormat.Rgb16i
        | RGB16UI -> PixelInternalFormat.Rgb16ui
        | RGB32I -> PixelInternalFormat.Rgb32i
        | RGB32UI -> PixelInternalFormat.Rgb32ui
        | RGBA8I -> PixelInternalFormat.Rgba8
        | RGBA8UI -> PixelInternalFormat.Rgba8
        | RGBA16I -> PixelInternalFormat.Rgba16
        | RGBA16UI -> PixelInternalFormat.Rgba16
        | RGBA32I -> PixelInternalFormat.Rgba32i
        | RGBA32UI -> PixelInternalFormat.Rgba32ui
        | _ -> failwith "unknown internal format"

    let tryGetPixelFormat (fmt : Col.Format) : Option<PixelFormat>  =
        match fmt with
            | Col.Format.BGR -> Some PixelFormat.Bgr
            | Col.Format.BGRA -> Some PixelFormat.Bgra
            | Col.Format.BGRP -> Some PixelFormat.Bgra
            | Col.Format.BW -> Some PixelFormat.Luminance
            | Col.Format.Gray -> Some PixelFormat.Luminance
            | Col.Format.RGB -> Some PixelFormat.Rgb
            | Col.Format.RGBA -> Some PixelFormat.Rgba
            | Col.Format.RGBP -> Some PixelFormat.Rgba
            | _ -> None

    let tryGetPixelType (t : Type) : Option<PixelType> =
        if t = typeof<byte> then
            Some PixelType.UnsignedByte
        elif t = typeof<sbyte> then
            Some PixelType.Byte
        elif t = typeof<uint16> then
            Some PixelType.UnsignedShort
        elif t = typeof<int16> then
            Some PixelType.Short
        elif t = typeof<uint32> then
            Some PixelType.UnsignedInt
        elif t = typeof<int> then
            Some PixelType.Int

        elif t = typeof<float32> then
            Some PixelType.Float

        else
            None

    let tryGetFormat (fmt : PixFormat) : Option<PixelType * PixelFormat> =
        match tryGetPixelType fmt.Type, tryGetPixelFormat fmt.Format with
            | Some t, Some fmt -> Some(t,fmt)
            | _ -> None


    let private ofColFormatAndBits (kind : ChannelKind) (fmt : Col.Format) (bits : int) =
        match fmt with
            | Col.Format.BGR -> ChannelType(kind, bits, bits, bits, 0)
            | Col.Format.BGRA -> ChannelType(kind, bits, bits, bits, bits)
            | Col.Format.BGRP -> ChannelType(kind, bits, bits, bits, bits)
            | Col.Format.Gray -> ChannelType(kind, bits, 0, 0, 0)
            | Col.Format.RGB -> ChannelType(kind, bits, bits, bits, 0)
            | Col.Format.RGBA -> ChannelType(kind, bits, bits, bits, bits)
            | Col.Format.RGBP -> ChannelType(kind, bits, bits, bits, bits)
            | _ -> failwithf "unsupported col-format: %A" fmt

    let private getBitsAndKind (t : Type) =
        if t = typeof<byte> then ChannelKind.Unsigned, 8
        elif t = typeof<uint16> then ChannelKind.Unsigned, 16
        elif t = typeof<float32> then ChannelKind.Float, 32
        elif t = typeof<uint32> then ChannelKind.Unsigned, 32
        elif t = typeof<sbyte> then ChannelKind.Signed, 8
        elif t = typeof<int16> then ChannelKind.Signed, 16
        elif t = typeof<int> then ChannelKind.Signed, 32
        else failwithf "unsuppoorted pixel-type: %A" t

    let getChannelType (bits : int) (kind : ChannelKind) =
        match kind, bits with
            | ChannelKind.Unsigned, 8 -> typeof<byte>
            | ChannelKind.Unsigned, 16 -> typeof<uint16>
            | ChannelKind.Unsigned, 32 -> typeof<uint32>
            | ChannelKind.Unsigned, 64 -> typeof<uint64>

            | ChannelKind.Float,    16 -> failwith "half is not supported by the .NET environment"
            | ChannelKind.Float,    32 -> typeof<float32>
            | ChannelKind.Float,    64 -> typeof<float>

            | ChannelKind.Signed,   8 -> typeof<sbyte>
            | ChannelKind.Signed,   16 -> typeof<int16>
            | ChannelKind.Signed,   32 -> typeof<int>
            | ChannelKind.Signed,   64 -> typeof<int64>

            | _ -> failwithf "unsuppoorted bits/kind: %A/%A" bits kind

    let getDownloadChannelType (bits : int) (kind : ChannelKind) =
        match kind, bits with
            | ChannelKind.Unsigned,     8   -> typeof<byte>
            | ChannelKind.Unsigned,     10  -> typeof<uint16>
            | ChannelKind.Unsigned,     16  -> typeof<uint16>
            | ChannelKind.Unsigned,     32  -> typeof<uint32>
            | ChannelKind.Unsigned,     64  -> typeof<uint64>

            | ChannelKind.Signed,       8   -> typeof<sbyte>
            | ChannelKind.Signed,       16  -> typeof<int16>
            | ChannelKind.Signed,       32  -> typeof<int>
            | ChannelKind.Signed,       64  -> typeof<int64>

            | ChannelKind.Norm,         2   -> typeof<byte>
            | ChannelKind.Norm,         3   -> typeof<byte>
            | ChannelKind.Norm,         4   -> typeof<byte>
            | ChannelKind.Norm,         5   -> typeof<byte>
            | ChannelKind.Norm,         8   -> typeof<byte>
            | ChannelKind.Norm,         9   -> typeof<uint16>
            | ChannelKind.Norm,         16  -> typeof<uint16>
            | ChannelKind.Norm,         32  -> typeof<uint32>
            | ChannelKind.Norm,         64  -> typeof<uint64>

            | ChannelKind.SignedNorm,   8   -> typeof<sbyte>
            | ChannelKind.SignedNorm,   16  -> typeof<int16>

            | ChannelKind.Float,        11  -> typeof<float32>
            | ChannelKind.Float,        16  -> typeof<float32>
            | ChannelKind.Float,        32  -> typeof<float32>


            | _ -> failwithf "unsuppoorted bits/kind: %A/%A" bits kind


    let ofPixFormat (fmt : PixFormat) =
        let (kind, bits) = getBitsAndKind fmt.Type
        ofColFormatAndBits kind fmt.Format bits

    let toPixFormat (ChannelType(kind, r, g, b, a)) =
        let bits = [r;g;b;a] |> List.tryFind (fun b -> b <> 0)
        match bits with
            | Some bits ->
                let baseType = getChannelType bits kind

                match r,g,b,a with
                    | _,0,0,0 -> PixFormat(baseType, Col.Format.Gray)
                    | _,_,0,0 -> failwith "RG images are unsupported by Col.Format"
                    | _,_,_,0 -> PixFormat(baseType, Col.Format.RGB)
                    | _,_,_,_ -> PixFormat(baseType, Col.Format.RGBA)
            | _ ->
                failwith "ChannelType has to specify at least a one component"
      
    let toDownloadFormat (ChannelType(kind, r, g, b, a)) =
        let bits = [r;g;b;a] |> List.max
        match bits with
            | 0 ->
                failwith "ChannelType has to specify at least a one component"
            | bits ->
                let baseType = getDownloadChannelType bits kind

                match r,g,b,a with
                    | _,0,0,0 -> PixFormat(baseType, Col.Format.Gray)
                    | _,_,0,0 -> failwith "RG images are unsupported by Col.Format"
                    | _,_,_,0 -> PixFormat(baseType, Col.Format.RGB)
                    | _,_,_,_ -> PixFormat(baseType, Col.Format.RGBA)      

    let toSizedInternalFormat (t : ChannelType) =
        match t with
        | R8 -> SizedInternalFormat.R8
        | R16 -> SizedInternalFormat.R16
        | RG8 -> SizedInternalFormat.Rg8
        | RG16 -> SizedInternalFormat.Rg16
        | RGBA8 -> SizedInternalFormat.Rgba8
        | RGBA16 -> SizedInternalFormat.Rgba16
        | R16F -> SizedInternalFormat.R16f
        | RG16F -> SizedInternalFormat.Rg16f
        | RGBA16F -> SizedInternalFormat.Rgba16f
        | R32F -> SizedInternalFormat.R32f
        | RG32F -> SizedInternalFormat.Rg32f
        | RGBA32F -> SizedInternalFormat.Rgba32f
        | R8I -> SizedInternalFormat.R8i
        | R8UI -> SizedInternalFormat.R8ui
        | R16I -> SizedInternalFormat.R16i
        | R16UI -> SizedInternalFormat.R16ui
        | R32I -> SizedInternalFormat.R32i
        | R32UI -> SizedInternalFormat.R32ui
        | RG8I -> SizedInternalFormat.Rg8i
        | RG8UI -> SizedInternalFormat.Rg8ui
        | RG16I -> SizedInternalFormat.Rg16i
        | RG16UI -> SizedInternalFormat.Rg16ui
        | RG32I -> SizedInternalFormat.Rg32i
        | RG32UI -> SizedInternalFormat.Rg32ui
        | RGBA8I -> SizedInternalFormat.Rgba8i
        | RGBA8UI -> SizedInternalFormat.Rgba8
        | RGBA16I -> SizedInternalFormat.Rgba16i
        | RGBA16UI -> SizedInternalFormat.Rgba16
        | RGBA32I -> SizedInternalFormat.Rgba32i
        | RGBA32UI -> SizedInternalFormat.Rgba32ui
        | _ -> failwith "unknown internal format"


type Texture =
    class
        val mutable public Context : Context
        val mutable public Handle : int
        val mutable public Dimension : TextureDimension
        val mutable public Multisamples : int
        val mutable public Size : V3i
        val mutable public Count : int
        val mutable public ChannelType : ChannelType
        val mutable public MipMapLevels : int

        member x.IsMultisampled = x.Multisamples > 1
        member x.IsArray = x.Count > 1

        member x.Size1D = x.Size.X
        member x.Size2D = x.Size.XY
        member x.Size3D = x.Size

        interface IResource with
            member x.Context = x.Context
            member x.Handle = x.Handle

        interface IBackendTexture with
            member x.WantMipMaps = x.MipMapLevels > 1
            member x.Handle = x.Handle :> obj

        member x.GetSize (level : int)  =
            if level = 0 then x.Size2D
            else 
                let level = Fun.Clamp(level, 0, x.MipMapLevels-1)
                let factor = 1 <<< level
                x.Size2D / factor

        new(ctx : Context, handle : int, dimension : TextureDimension, mipMapLevels : int, multisamples : int, size : V3i, count : int, channelType : ChannelType) =
            { Context = ctx; Handle = handle; Dimension = dimension; MipMapLevels = mipMapLevels; Multisamples = multisamples; Size = size; Count = count; ChannelType = channelType }

    end


[<AutoOpen>]
module TextureExtensions =


    // PositiveX = 0,
    // NegativeX = 1,
    // PositiveY = 2,
    // NegativeY = 3,
    // PositiveZ = 4,
    // NegativeZ = 5,
    // cubeSides are sorted like in their implementation (making some things easier)
    let cubeSides =
        [|
            CubeSide.PositiveX, TextureTarget.TextureCubeMapPositiveX
            CubeSide.NegativeX, TextureTarget.TextureCubeMapNegativeX

            CubeSide.PositiveY, TextureTarget.TextureCubeMapPositiveY
            CubeSide.NegativeY, TextureTarget.TextureCubeMapNegativeY
                
            CubeSide.PositiveZ, TextureTarget.TextureCubeMapPositiveZ
            CubeSide.NegativeZ, TextureTarget.TextureCubeMapNegativeZ
        |]

    [<AutoOpen>]
    module private Patterns =
        let (|FileTexture|_|) (t : ITexture) =
            match t with
                | :? FileTexture as t -> Some(FileTexture(t.WantMipMaps, t.FileName))
                | _ -> None

//        let (|PixTextureCube|_|) (t : ITexture) =
//            match t with
//                | :? TextureCube2d as t -> Some(PixTextureCube(t.TexInfo, t.PixCubeFun.Invoke()))
//                | _ -> None
//
        let (|PixTexture2D|_|) (t : ITexture) =
            match t with
                | :? PixTexture2d as t -> Some(t.WantMipMaps, t.PixImageMipMap)
                | _ -> None
//
//        let (|PixTexture3D|_|) (t : ITexture) =
//            match t with
//                | :? Texture3d as t -> Some(PixTexture3D(t.TexInfo, t.PixMipMapFun.Invoke()))
//                | _ -> None

    [<AutoOpen>]
    module private Uploads =

        let private withAlignedPixImageContent (packAlign : int) (img : PixImage) (f : nativeint -> 'a) : 'a =
            let image = img.ToCanonicalDenseLayout()

            let lineSize = image.Size.X * image.PixFormat.ChannelCount * image.PixFormat.Type.GLSize
            let gc = GCHandle.Alloc(image.Data, GCHandleType.Pinned)

            let result = 
                if lineSize % packAlign <> 0 then
                    let adjustedLineSize = lineSize + (packAlign - lineSize % packAlign)

                    let data = Marshal.AllocHGlobal(image.Size.Y * adjustedLineSize)
                    let mutable src = gc.AddrOfPinnedObject()
                    let mutable aligned = data

                    for line in 0..image.Size.Y-1 do
                        Marshal.Copy(src, aligned, adjustedLineSize)
                        src <- src + nativeint lineSize
                        aligned <- aligned + nativeint adjustedLineSize
                    
                    try
                        f(data)
                    finally 
                        Marshal.FreeHGlobal(data)

                else
                    f(gc.AddrOfPinnedObject())

            gc.Free()

            result

        let private uploadTexture2DInternal (target : TextureTarget) (isTopLevel : bool) (t : Texture) (mipMaps : bool) (data : PixImageMipMap) =
            if data.LevelCount <= 0 then
                failwith "cannot upload texture having 0 levels"

            let size = data.[0].Size
            let expectedLevels = Fun.Min(size.X, size.Y) |> Fun.Log2 |> Fun.Ceiling |> int //int(Fun.Ceiling(Fun.Log2(Fun.Min(size.X, size.Y))))
            let uploadLevels = if mipMaps then data.LevelCount else 1
            let generateMipMap = mipMaps && data.LevelCount < expectedLevels
            // TODO: think about texture format here
            let newFormat = ChannelType.ofPixFormat data.[0].PixFormat
            let formatChanged = t.ChannelType <> newFormat
            t.ChannelType <- newFormat

            let internalFormat = ChannelType.toGlInternalFormat t.ChannelType
            let sizeChanged = size <> t.Size2D

            GL.BindTexture(target, t.Handle)
            GL.Check "could not bind texture"

            for l in 0..uploadLevels-1 do
                let level = data.[0]
                //let level = level.ToPixImage(Col.Format.RGBA)

                // determine the input format and covert the image
                // to a supported format if necessary.
                let pixelType, pixelFormat, image =
                    match ChannelType.tryGetFormat level.PixFormat with
                        | Some (t,f) -> (t,f, level)
                        | None ->
                            failwith "conversion not implemented"

                // since OpenGL cannot upload image-regions we
                // need to ensure that the image has a canonical layout. 
                // TODO: Check id this is no "real" copy when already canonical
                let image = image.ToCanonicalDenseLayout()


                let lineSize = image.Size.X * image.PixFormat.ChannelCount * image.PixFormat.Type.GLSize
                let packAlign = t.Context.PackAlignment

                withAlignedPixImageContent packAlign image (fun ptr ->
                    if sizeChanged || formatChanged then
                        GL.TexImage2D(target, l, internalFormat, image.Size.X, image.Size.Y, 0, pixelFormat, pixelType, ptr)
                    else
                        GL.TexSubImage2D(target, l, 0, 0, image.Size.X, image.Size.Y, pixelFormat, pixelType, ptr)
                    GL.Check (sprintf "could not upload texture data for level %d" l)
                )


            // if the image did not contain a sufficient
            // number of MipMaps and the user demanded 
            // MipMaps we generate them using OpenGL
            if generateMipMap && isTopLevel then
                GL.GenerateMipmap(GenerateMipmapTarget.Texture2D)
                GL.Check "failed to generate mipmaps"

            GL.BindTexture(target, 0)
            GL.Check "could not bind texture"

            // since some attributes of the texture
            // may have changed we mutate them here
            if isTopLevel then
                t.Size <- V3i(size.X, size.Y, 0)
                t.Multisamples <- 1
                t.Count <- 1
                t.Dimension <- TextureDimension.Texture2D
                //t.ChannelType <- ChannelType.fromGlFormat internalFormat

            generateMipMap

        let uploadTexture2DBitmap (t : Texture) (mipMaps : bool) (bmp : BitmapTexture) =

            let size = V2i(bmp.Bitmap.Width, bmp.Bitmap.Height)
            let expectedLevels = Fun.Min(size.X, size.Y) |> Fun.Log2 |> Fun.Ceiling |> int //int(Fun.Ceiling(Fun.Log2(Fun.Min(size.X, size.Y))))
            let uploadLevels = 1
            let generateMipMap = mipMaps
            let internalFormat = PixelInternalFormat.CompressedRgba
            let sizeChanged = size <> t.Size2D

            GL.BindTexture(TextureTarget.Texture2D, t.Handle)
            GL.Check "could not bind texture"

            // determine the input format and covert the image
            // to a supported format if necessary.
            let pixelType, pixelFormat =
                PixelType.UnsignedByte, PixelFormat.Bgra

            bmp.Bitmap.RotateFlip(Drawing.RotateFlipType.RotateNoneFlipY)
            let locked = bmp.Bitmap.LockBits(Drawing.Rectangle(0,0,bmp.Bitmap.Width, bmp.Bitmap.Height), Drawing.Imaging.ImageLockMode.ReadOnly, Drawing.Imaging.PixelFormat.Format32bppArgb)
            // if the size did not change it is more efficient
            // to use glTexSubImage
            if sizeChanged then
                GL.TexImage2D(TextureTarget.Texture2D, 0, internalFormat, size.X, size.Y, 0, pixelFormat, pixelType, locked.Scan0)
            else
                GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, size.X, size.Y, pixelFormat, pixelType, locked.Scan0)
            GL.Check (sprintf "could not upload texture data for level %d" 0)

            bmp.Bitmap.UnlockBits(locked)
            bmp.Bitmap.RotateFlip(Drawing.RotateFlipType.RotateNoneFlipY)

            // if the image did not contain a sufficient
            // number of MipMaps and the user demanded 
            // MipMaps we generate them using OpenGL
            if generateMipMap then
                GL.GenerateMipmap(GenerateMipmapTarget.Texture2D)
                GL.Check "failed to generate mipmaps"

            GL.BindTexture(TextureTarget.Texture2D, 0)
            GL.Check "could not bind texture"

            // since some attributes of the texture
            // may have changed we mutate them here

            t.Size <- V3i(size.X, size.Y, 0)
            t.Multisamples <- 1
            t.Count <- 1
            t.Dimension <- TextureDimension.Texture2D
            t.ChannelType <- ChannelType.fromGlFormat internalFormat

        let uploadTexture2D (t : Texture) (mipMaps : bool) (data : PixImageMipMap) =
            uploadTexture2DInternal TextureTarget.Texture2D true t mipMaps data |> ignore

        let uploadTextureCube (t : Texture) (mipMaps : bool) (data : PixImageCube) =
            for (s,_) in cubeSides do
                if data.[s].LevelCount <= 0 then
                    failwith "cannot upload texture having 0 levels"

            let mutable generateMipMaps = false
            let size = data.[CubeSide.NegativeX].[0].Size

            for (side, target) in cubeSides do
                let data = data.[side]
                let generate = uploadTexture2DInternal target false t mipMaps data

                if generate && mipMaps then
                    generateMipMaps <- true

            if generateMipMaps then
                GL.BindTexture(TextureTarget.TextureCubeMap, t.Handle)
                GL.Check "could not bind texture"

                GL.GenerateMipmap(GenerateMipmapTarget.TextureCubeMap)
                GL.Check "failed to generate mipmaps"

                GL.BindTexture(TextureTarget.TextureCubeMap, 0)
                GL.Check "could not unbind texture"

            t.Size <- V3i(size.X, size.Y, 0)
            t.Multisamples <- 1
            t.Count <- 1
            t.Dimension <- TextureDimension.TextureCube

        let uploadTexture3D (t : Texture) (mipMaps : bool) (data : PixVolume) =
            let size = data.Size
            let expectedLevels = Fun.Min(size.X, size.Y, size.Z) |> Fun.Log2 |> Fun.Ceiling |> int //int(Fun.Ceiling(Fun.Log2(Fun.Min(size.X, size.Y))))
            let generateMipMap = mipMaps
            let internalFormat = ChannelType.toGlInternalFormat t.ChannelType
            let sizeChanged = size = t.Size3D

            GL.BindTexture(TextureTarget.Texture3D, t.Handle)
            GL.Check "could not bind texture"

            // determine the input format and covert the image
            // to a supported format if necessary.
            let pixelType, pixelFormat, image =
                match ChannelType.tryGetFormat data.PixFormat with
                    | Some (t,f) -> (t,f, data)
                    | None ->
                        failwith "conversion not implemented"

            // since OpenGL cannot upload image-regions we
            // need to ensure that the image has a canonical layout. 
            // TODO: Check id this is no "real" copy when already canonical
            let image = image.CopyToPixVolumeWithCanonicalDenseLayout()


            let gc = GCHandle.Alloc(image.Array, GCHandleType.Pinned)

            // if the size did not change it is more efficient
            // to use glTexSubImage
            if sizeChanged then
                GL.TexImage3D(TextureTarget.Texture3D, 0, internalFormat, size.X, size.Y, size.Z, 0, pixelFormat, pixelType, gc.AddrOfPinnedObject())
            else
                GL.TexSubImage3D(TextureTarget.Texture3D, 0, 0, 0, 0, size.X, size.Y, size.Z, pixelFormat, pixelType, gc.AddrOfPinnedObject())
            GL.Check "could not upload texture data"

            gc.Free()

            // if the image did not contain a sufficient
            // number of MipMaps and the user demanded 
            // MipMaps we generate them using OpenGL
            if generateMipMap then
                GL.GenerateMipmap(GenerateMipmapTarget.Texture3D)
                GL.Check "failed to generate mipmaps"

            GL.BindTexture(TextureTarget.Texture3D, 0)
            GL.Check "could not bind texture"

            // since some attributes of the texture
            // may have changed we mutate them here
            t.Size <- size
            t.Multisamples <- 1
            t.Count <- 1
            t.Dimension <- TextureDimension.Texture3D
            t.ChannelType <- ChannelType.fromGlFormat internalFormat

        let downloadTexture2DInternal (target : TextureTarget) (isTopLevel : bool) (t : Texture) (level : int) (format : PixFormat) =
            if level <> 0 then
                failwith "downloads of mipmap-levels currently not implemented"

            let levelSize = t.Size2D
            let image = PixImage.Create(format, int64 levelSize.X, int64 levelSize.Y)

            GL.BindTexture(target, t.Handle)
            GL.Check "could not bind texture"

            let pixelType, pixelFormat, image =
                match ChannelType.tryGetFormat format with
                    | Some(t,f) -> (t,f,image)
                    | _ ->
                        failwith "conversion not implemented"

            let gc = GCHandle.Alloc(image.Data, GCHandleType.Pinned)

            OpenTK.Graphics.OpenGL4.GL.GetTexImage(target, level, pixelFormat, pixelType, gc.AddrOfPinnedObject())
            GL.Check "could not download image"

            gc.Free()

            image

        let downloadTexture2D (t : Texture) (level : int) (format : PixFormat) =
            [|downloadTexture2DInternal TextureTarget.Texture2D true t level format|]

        let downloadTextureCube (t : Texture) (level : int) (format : PixFormat) =
            let images =
                cubeSides |> Array.map (fun (side, target) ->
                                let image = downloadTexture2DInternal target false t level format
                                image
                             ) 

            images

    type Context with
        member x.CreateTexture1D(size : int, mipMapLevels : int, t : ChannelType) =
            using x.ResourceLock (fun _ ->
                let h = GL.GenTexture()
                GL.Check "could not create texture"

                let tex = Texture(x, h, TextureDimension.Texture1D, mipMapLevels, 1, V3i(size,0,0), 1, t)
                x.UpdateTexture1D(tex, size, mipMapLevels, t)

                tex
            )

        member x.CreateTexture2D(size : V2i, mipMapLevels : int, t : ChannelType, samples : int) =
            using x.ResourceLock (fun _ ->
                let h = GL.GenTexture()
                GL.Check "could not create texture"
                
                let tex = Texture(x, h, TextureDimension.Texture2D, mipMapLevels, 1, V3i(size.X,size.Y,0), 1, t)

                x.UpdateTexture2D(tex, size, mipMapLevels, t, samples)

                tex
            )

        member x.CreateTexture3D(size : V3i, mipMapLevels : int, t : ChannelType, samples : int) =
            using x.ResourceLock (fun _ ->
                let h = GL.GenTexture()
                GL.Check "could not create texture"

                let tex = Texture(x, h, TextureDimension.Texture3D, mipMapLevels, 1, size, 1, t)
                x.UpdateTexture3D(tex, size, mipMapLevels, t, samples)

                tex
            )

        member x.CreateTextureCube(size : V2i, mipMapLevels : int, t : ChannelType) =
            using x.ResourceLock (fun _ ->
                let h = GL.GenTexture()
                GL.Check "could not create texture"

                let tex = Texture(x, h, TextureDimension.TextureCube, mipMapLevels, 1, V3i(size.X, size.Y, 0), 1, t)
                x.UpdateTextureCube(tex, size, mipMapLevels, t)

                tex
            )

        member x.UpdateTexture1D(tex : Texture, size : int, mipMapLevels : int, t : ChannelType) =
            using x.ResourceLock (fun _ ->
                GL.BindTexture(TextureTarget.Texture1D, tex.Handle)
                GL.Check "could not bind texture"


                let ifmt = ChannelType.toSizedInternalFormat t
                GL.TexStorage1D(TextureTarget1d.Texture1D, mipMapLevels, ifmt, size)


                GL.Check "could allocate texture"

                GL.BindTexture(TextureTarget.Texture1D, 0)
                GL.Check "could not unbind texture"

                tex.MipMapLevels <- mipMapLevels
                tex.Dimension <- TextureDimension.Texture1D
                tex.Size <- V3i(size, 0, 0)
                tex.ChannelType <- t
            )

        member x.UpdateTexture2D(tex : Texture, size : V2i, mipMapLevels : int, t : ChannelType, samples : int) =
            using x.ResourceLock (fun _ ->
                GL.BindTexture(TextureTarget.Texture2D, tex.Handle)
                GL.Check "could not bind texture"

                let ifmt = ChannelType.toSizedInternalFormat t
                if samples = 1 then
                    GL.TexStorage2D(TextureTarget2d.Texture2D, mipMapLevels, ifmt, size.X, size.Y)
                else
                    if mipMapLevels > 1 then failwith "multisampled textures cannot have MipMaps"
                    GL.TexStorage2DMultisample(TextureTargetMultisample2d.Texture2DMultisample, samples, ifmt, size.X, size.Y, false)
                GL.Check "could allocate texture"

                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, mipMapLevels)
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0)


                GL.BindTexture(TextureTarget.Texture2D, 0)
                GL.Check "could not unbind texture"

                tex.MipMapLevels <- mipMapLevels
                tex.Dimension <- TextureDimension.Texture2D
                tex.Size <- V3i(size.X, size.Y, 0)
                tex.ChannelType <- t
            )

        member x.UpdateTexture3D(tex : Texture, size : V3i, mipMapLevels : int, t : ChannelType, samples : int) =
            using x.ResourceLock (fun _ ->
                GL.BindTexture(TextureTarget.Texture3D, tex.Handle)
                GL.Check "could not bind texture"

                let ifmt = ChannelType.toSizedInternalFormat t
                if samples = 1 then
                    GL.TexStorage3D(TextureTarget3d.Texture3D, mipMapLevels, ifmt, size.X, size.Y, size.Z)
                else
                    if mipMapLevels > 1 then failwith "multisampled textures cannot have MipMaps"
                    GL.TexStorage3DMultisample(TextureTargetMultisample3d.Texture2DMultisampleArray, samples, ifmt, size.X, size.Y, size.Z, false)
                GL.Check "could allocate texture"


                GL.BindTexture(TextureTarget.Texture3D, 0)
                GL.Check "could not unbind texture"

                tex.MipMapLevels <- mipMapLevels
                tex.Dimension <- TextureDimension.Texture3D
                tex.Size <- size
                tex.ChannelType <- t
            )

        member x.UpdateTextureCube(tex : Texture, size : V2i, mipMapLevels : int, t : ChannelType) =
            using x.ResourceLock (fun _ ->
                for (_,target) in cubeSides do
                    GL.BindTexture(target, tex.Handle)
                    GL.Check "could not bind texture"

                    let target2d = target |> int |> unbox<TextureTarget2d>
                    let ifmt = ChannelType.toSizedInternalFormat t
                    GL.TexStorage2D(target2d, mipMapLevels, ifmt, size.X, size.Y)
                    GL.Check "could allocate texture"

                    GL.BindTexture(TextureTarget.Texture2D, 0)
                    GL.Check "could not unbind texture"

                tex.MipMapLevels <- mipMapLevels
                tex.Dimension <- TextureDimension.TextureCube
                tex.Size <- V3i(size.X, size.Y, 0)
                tex.ChannelType <- t
            )



        member x.CreateTexture(data : ITexture) =
            using x.ResourceLock (fun _ ->
                let h = GL.GenTexture()
                GL.Check "could not create texture"

                let mutable t = Texture(x, h, TextureDimension.Texture2D, 1, 1, V3i(-1,-1,-1), 1, ChannelType.RGBA8)

                match data with

                    | :? BitmapTexture as bmp ->
                        uploadTexture2DBitmap t true bmp

                    | FileTexture(info, file) ->
                        
                        // TODO: maybe there's a better way for loading file-textures
                        if file = null then 
                            ()
                        else
                            let pi = PixImage.Create(file, PixLoadOptions.UseDevil)
                            let mm = PixImageMipMap [|pi|]
                            uploadTexture2D t info mm |> ignore

                    | PixTexture2D(wantMipMaps, data) -> 

//                        if data.LevelCount > 0 then
//                            t.ChannelType <- data.[0].PixFormat |> ChannelType.ofPixFormat

                        uploadTexture2D t wantMipMaps data |> ignore
//
//                    | PixTextureCube(info, data) -> 
//                        uploadTextureCube t info data
//
//                    | PixTexture3D(info, image) ->
//                        uploadTexture3D t info image

                    | :? Texture as o ->
                        GL.DeleteTexture(h)
                        GL.Check "could not delete texture"

                        // TODO: don't create a texture-handle here
                        t <- o

                    | _ ->
                        failwith "unsupported texture data"
                t
            )

        member x.Upload(t : Texture, data : ITexture) =
            using x.ResourceLock (fun _ ->
                match data with
                    | :? BitmapTexture as bmp ->
                        uploadTexture2DBitmap t true bmp

                    | PixTexture2D(wantMipMaps, data) -> 
                        uploadTexture2D t wantMipMaps data |> ignore
//
//                    | PixTextureCube(info, data) -> 
//                        uploadTextureCube t info data
//
//                    | PixTexture3D(info, image) ->
//                        uploadTexture3D t info image

                    | FileTexture(info, file) ->
                        let pi = PixImage.Create(file, PixLoadOptions.UseDevil)
                        let mm = PixImageMipMap [|pi|]
                        uploadTexture2D t info mm |> ignore


                    | :? Texture as o ->
                        if t.Handle <> o.Handle then
                            failwith "cannot upload to framebuffer-texture"

                    | _ ->
                        failwith "unsupported texture data"
            )

        member x.Download(t : Texture, format : PixFormat, level : int) =
            using x.ResourceLock (fun _ ->
                match t.Dimension with
                    | TextureDimension.Texture2D -> 
                        downloadTexture2D t level format

                    | TextureDimension.TextureCube ->
                        downloadTextureCube t level format

                    | _ ->  
                        failwithf "cannot download textures of kind: %A" t.Dimension
            )
            
        member x.Delete(t : Texture) =
            using x.ResourceLock (fun _ ->
                GL.DeleteTexture(t.Handle)
                GL.Check "could not delete texture"
            )
            
    module ExecutionContext =

        let private getTextureTarget (texture : Texture) =
            match texture.Dimension, texture.IsArray, texture.IsMultisampled with

                | TextureDimension.Texture1D,      _,       true     -> failwith "Texture1D cannot be multisampled"
                | TextureDimension.Texture1D,      true,    _        -> TextureTarget.Texture1DArray
                | TextureDimension.Texture1D,      false,   _        -> TextureTarget.Texture1D
                                                   
                | TextureDimension.Texture2D,      false,   false    -> TextureTarget.Texture2D
                | TextureDimension.Texture2D,      true,    false    -> TextureTarget.Texture2DArray
                | TextureDimension.Texture2D,      false,   true     -> TextureTarget.Texture2DMultisample
                | TextureDimension.Texture2D,      true,    true     -> TextureTarget.Texture2DMultisampleArray
                                                   
                | TextureDimension.Texture3D,      false,   false    -> TextureTarget.Texture3D
                | TextureDimension.Texture3D,      _,       _        -> failwith "Texture3D cannot be multisampled or an array"
                                                  
                | TextureDimension.TextureCube,   false,    false    -> TextureTarget.TextureCubeMap
                | TextureDimension.TextureCube,   true,     false    -> TextureTarget.TextureCubeMapArray
                | TextureDimension.TextureCube,   _,        true     -> failwith "TextureCube cannot be multisampled"

                | _ -> failwithf "unknown texture dimension: %A" texture.Dimension

        let bindTexture (unit : int) (texture : Texture) =
            seq {
                yield Instruction.ActiveTexture(int TextureUnit.Texture0 + unit)
                
                let target = getTextureTarget texture
                yield Instruction.BindTexture (int target) texture.Handle
            }            

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Texture =
    let create1D (c : Context) (size : int) (mipLevels : int) (channelType : ChannelType) =
        c.CreateTexture1D(size, mipLevels, channelType)

    let create2D (c : Context) (size : V2i) (mipLevels : int) (channelType : ChannelType) (samples : int) =
        c.CreateTexture2D(size, mipLevels, channelType, samples)

    let createCube (c : Context) (size : V2i) (mipLevels : int) (channelType : ChannelType) =
        c.CreateTextureCube(size, mipLevels, channelType)

    let create3D (c : Context) (size : V3i) (mipLevels : int) (channelType : ChannelType) (samples : int) =
        c.CreateTexture3D(size, mipLevels, channelType, samples)

    let delete (tex : Texture) =
        tex.Context.Delete(tex)

    let write (data : ITexture) (tex : Texture) =
        tex.Context.Upload(tex, data)

    let read (format : PixFormat) (level : int) (tex : Texture) : PixImage[] =
        tex.Context.Download(tex, format, level)