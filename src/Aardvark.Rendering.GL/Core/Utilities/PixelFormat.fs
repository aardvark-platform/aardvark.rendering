namespace Aardvark.Rendering.GL

open Aardvark.Base
open OpenTK.Graphics.OpenGL4

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

[<AutoOpen>]
module internal PixelFormatExtensions =

    type PixelFormat with
        member x.Channels = PixelFormat.channels x