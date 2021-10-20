namespace Aardvark.Rendering.GL

open System
open Aardvark.Base
open OpenTK.Graphics.OpenGL4

[<AutoOpen>]
module internal ColFormatExtensions =
    type Col.Format with
        static member Stencil      = unbox<Col.Format> (Int32.MaxValue)
        static member Depth        = unbox<Col.Format> (Int32.MaxValue - 1)
        static member DepthStencil = unbox<Col.Format> (Int32.MaxValue - 2)

module internal PixelFormat =

    let channels =
        LookupTable.lookupTable [
            PixelFormat.Bgr, 3
            PixelFormat.Bgra, 4
            PixelFormat.Red, 1
            PixelFormat.Rg, 2
            PixelFormat.Rgb, 3
            PixelFormat.Rgba, 4
            PixelFormat.BgrInteger, 3
            PixelFormat.BgraInteger, 4
            PixelFormat.RedInteger, 1
            PixelFormat.RgInteger, 2
            PixelFormat.RgbInteger, 3
            PixelFormat.RgbaInteger, 4
            PixelFormat.StencilIndex, 1
            PixelFormat.DepthComponent, 1
            PixelFormat.DepthStencil, 1
        ]

    let toIntegerFormat (format : PixelFormat) =
        format |> LookupTable.lookupTable' [
            PixelFormat.Red,    PixelFormat.RedInteger
            PixelFormat.Green,  PixelFormat.GreenInteger
            PixelFormat.Blue,   PixelFormat.BlueInteger
            PixelFormat.Alpha,  PixelFormat.AlphaInteger
            PixelFormat.Rg,     PixelFormat.RgInteger
            PixelFormat.Rgb,    PixelFormat.RgbInteger
            PixelFormat.Rgba,   PixelFormat.RgbaInteger
            PixelFormat.Bgr,    PixelFormat.BgrInteger
            PixelFormat.Bgra,   PixelFormat.BgraInteger
        ]
        |> Option.defaultValue format

    let ofColFormat (isInteger : bool) =
        LookupTable.lookupTable' [
            Col.Format.Alpha, PixelFormat.Red
            Col.Format.BGR, PixelFormat.Bgr
            Col.Format.BGRA, PixelFormat.Bgra
            Col.Format.BGRP, PixelFormat.Bgra
            Col.Format.BW, PixelFormat.Red
            Col.Format.Gray, PixelFormat.Red
            Col.Format.GrayAlpha, PixelFormat.Rg
            Col.Format.NormalUV, PixelFormat.Rg
            Col.Format.RGB, PixelFormat.Rgb
            Col.Format.RGBA, PixelFormat.Rgba
            Col.Format.RGBP, PixelFormat.Rgba
            Col.Format.Stencil, PixelFormat.StencilIndex
            Col.Format.Depth, PixelFormat.DepthComponent
            Col.Format.DepthStencil, PixelFormat.DepthComponent
        ]
        >> if isInteger then Option.map toIntegerFormat else id

[<AutoOpen>]
module internal PixelFormatExtensions =

    type PixelFormat with
        member x.Channels = PixelFormat.channels x