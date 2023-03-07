namespace Aardvark.Rendering.GL

open Aardvark.Base
open OpenTK.Graphics.OpenGL4

module internal PixelFormat =

    let private ofColFormatTable, private ofColFormatTableInteger =
        let build (wantInteger : bool) =
            let choose a b = if wantInteger then b else a

            LookupTable.lookupTable [
                Col.Format.Alpha,     choose PixelFormat.Alpha PixelFormat.AlphaInteger
                Col.Format.BW,        choose PixelFormat.Red PixelFormat.RedInteger
                Col.Format.Gray,      choose PixelFormat.Red PixelFormat.RedInteger
                Col.Format.GrayAlpha, choose PixelFormat.Rg PixelFormat.RgInteger
                Col.Format.RGB,       choose PixelFormat.Rgb PixelFormat.RgbInteger
                Col.Format.BGR,       choose PixelFormat.Bgr PixelFormat.BgrInteger
                Col.Format.RGBA,      choose PixelFormat.Rgba PixelFormat.RgbaInteger
                Col.Format.BGRA,      choose PixelFormat.Bgra PixelFormat.RgbaInteger
                Col.Format.RGBP,      choose PixelFormat.Rgba PixelFormat.RgbaInteger
                Col.Format.RG,        choose PixelFormat.Rg PixelFormat.RgInteger
            ]

        build false, build true

    let ofColFormat (wantInteger : bool) =
        if wantInteger then ofColFormatTableInteger else ofColFormatTable

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

[<AutoOpen>]
module internal PixelFormatExtensions =

    type PixelFormat with
        member x.Channels = PixelFormat.channels x