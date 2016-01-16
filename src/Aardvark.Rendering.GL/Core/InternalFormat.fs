namespace Aardvark.Rendering.GL

module InternalFormat =
    open System.Collections.Generic
    open OpenTK.Graphics.OpenGL4

    let internal lookupTable (def : 'b) (l : list<'a * 'b>) =
        let d = Dictionary()
        for (k,v) in l do

            match d.TryGetValue k with
                | (true, vo) -> failwithf "duplicated lookup-entry: %A (%A vs %A)" k vo v
                | _ -> ()

            d.[k] <- v

        fun (key : 'a) ->
            match d.TryGetValue key with
                | (true, v) -> v
                | _ -> def

    let getSizeInBits = 
        lookupTable 8 [
            //PixelInternalFormat.DepthComponent, 24
            //PixelInternalFormat.Alpha, 0
            PixelInternalFormat.Rgb, 24
            PixelInternalFormat.Rgba, 32
            PixelInternalFormat.Luminance, 8
            PixelInternalFormat.LuminanceAlpha, 16
            PixelInternalFormat.R3G3B2, 8
            PixelInternalFormat.Rgb2Ext, 6
            PixelInternalFormat.Rgb4, 12
            PixelInternalFormat.Rgb5, 15
            PixelInternalFormat.Rgb8, 24
            PixelInternalFormat.Rgb10, 30
            PixelInternalFormat.Rgb12, 36
            PixelInternalFormat.Rgb16, 48
            PixelInternalFormat.Rgba2, 8
            PixelInternalFormat.Rgba4, 16
            PixelInternalFormat.Rgb5A1, 16
            PixelInternalFormat.Rgba8, 32
            PixelInternalFormat.Rgb10A2, 32
            PixelInternalFormat.Rgba12, 48
            PixelInternalFormat.Rgba16, 64
            PixelInternalFormat.DualAlpha4Sgis, 8
            PixelInternalFormat.DualAlpha8Sgis, 16
            PixelInternalFormat.DualAlpha12Sgis, 24
            PixelInternalFormat.DualAlpha16Sgis, 32
            PixelInternalFormat.DualLuminance4Sgis, 8
            PixelInternalFormat.DualLuminance8Sgis, 16
            PixelInternalFormat.DualLuminance12Sgis, 24
            PixelInternalFormat.DualLuminance16Sgis, 32
            PixelInternalFormat.DualIntensity4Sgis, 8
            PixelInternalFormat.DualIntensity8Sgis, 16
            PixelInternalFormat.DualIntensity12Sgis, 24
            PixelInternalFormat.DualIntensity16Sgis, 32
            PixelInternalFormat.DualLuminanceAlpha4Sgis, 20
            PixelInternalFormat.DualLuminanceAlpha8Sgis, 24
            PixelInternalFormat.QuadAlpha4Sgis, 16
            PixelInternalFormat.QuadAlpha8Sgis, 32
            PixelInternalFormat.QuadLuminance4Sgis, 16
            PixelInternalFormat.QuadLuminance8Sgis, 32
            PixelInternalFormat.QuadIntensity4Sgis, 16
            PixelInternalFormat.QuadIntensity8Sgis, 32
            PixelInternalFormat.DepthComponent16, 16
            //PixelInternalFormat.DepthComponent16Sgix, 16
            PixelInternalFormat.DepthComponent24, 24
            //PixelInternalFormat.DepthComponent24Sgix, 24
            PixelInternalFormat.DepthComponent32, 32
            //PixelInternalFormat.DepthComponent32Sgix, 32
            //PixelInternalFormat.CompressedRed, 
            //PixelInternalFormat.CompressedRg, 0
            PixelInternalFormat.R8, 8
            PixelInternalFormat.R16, 16
            PixelInternalFormat.Rg8, 16
            PixelInternalFormat.Rg16, 32
            PixelInternalFormat.R16f, 16
            PixelInternalFormat.R32f, 32
            PixelInternalFormat.Rg16f, 32
            PixelInternalFormat.Rg32f, 64
            PixelInternalFormat.R8i, 8
            PixelInternalFormat.R8ui, 8
            PixelInternalFormat.R16i, 16
            PixelInternalFormat.R16ui, 16
            PixelInternalFormat.R32i, 32
            PixelInternalFormat.R32ui, 32
            PixelInternalFormat.Rg8i, 16
            PixelInternalFormat.Rg8ui, 16
            PixelInternalFormat.Rg16i, 32
            PixelInternalFormat.Rg16ui, 32
            PixelInternalFormat.Rg32i, 64
            PixelInternalFormat.Rg32ui, 64
            //PixelInternalFormat.CompressedRgbS3tcDxt1Ext, 0
            //PixelInternalFormat.CompressedRgbaS3tcDxt1Ext, 0
            //PixelInternalFormat.CompressedRgbaS3tcDxt3Ext, 0
            //PixelInternalFormat.CompressedRgbaS3tcDxt5Ext, 0
            PixelInternalFormat.RgbIccSgix, 24
            PixelInternalFormat.RgbaIccSgix, 32
            PixelInternalFormat.AlphaIccSgix, 8
            PixelInternalFormat.LuminanceIccSgix, 8
            PixelInternalFormat.IntensityIccSgix, 8
            PixelInternalFormat.LuminanceAlphaIccSgix, 16
            PixelInternalFormat.R5G6B5IccSgix, 16
            PixelInternalFormat.R5G6B5A8IccSgix, 24
            PixelInternalFormat.Alpha16IccSgix, 16
            PixelInternalFormat.Luminance16IccSgix, 16
            PixelInternalFormat.Intensity16IccSgix, 16
            PixelInternalFormat.Luminance16Alpha8IccSgix, 24
            //PixelInternalFormat.CompressedAlpha, 0
            //PixelInternalFormat.CompressedLuminance, 0
            //PixelInternalFormat.CompressedLuminanceAlpha, 0
            //PixelInternalFormat.CompressedIntensity, 0
            //PixelInternalFormat.CompressedRgb, 0
            //PixelInternalFormat.CompressedRgba, 0
            PixelInternalFormat.DepthStencil, 32
            PixelInternalFormat.Rgba32f, 128
            PixelInternalFormat.Rgb32f, 96
            PixelInternalFormat.Rgba16f,64
            PixelInternalFormat.Rgb16f, 48
            PixelInternalFormat.Depth24Stencil8, 32
            PixelInternalFormat.R11fG11fB10f, 32
            PixelInternalFormat.Rgb9E5, 32
            PixelInternalFormat.Srgb, 24
            PixelInternalFormat.Srgb8, 32
            PixelInternalFormat.SrgbAlpha, 32
            PixelInternalFormat.Srgb8Alpha8, 32
            PixelInternalFormat.SluminanceAlpha, 16
            PixelInternalFormat.Sluminance8Alpha8, 16
            PixelInternalFormat.Sluminance, 8
            PixelInternalFormat.Sluminance8, 8
            //PixelInternalFormat.CompressedSrgb, 0
            //PixelInternalFormat.CompressedSrgbAlpha, 0
            //PixelInternalFormat.CompressedSluminance, 0
            //PixelInternalFormat.CompressedSluminanceAlpha, 0
            //PixelInternalFormat.CompressedSrgbS3tcDxt1Ext, 0
            //PixelInternalFormat.CompressedSrgbAlphaS3tcDxt1Ext, 0
            //PixelInternalFormat.CompressedSrgbAlphaS3tcDxt3Ext, 0
            //PixelInternalFormat.CompressedSrgbAlphaS3tcDxt5Ext, 0
            PixelInternalFormat.DepthComponent32f, 32
            PixelInternalFormat.Depth32fStencil8, 40
            PixelInternalFormat.Rgba32ui, 128
            PixelInternalFormat.Rgb32ui, 96
            PixelInternalFormat.Rgba16ui, 64
            PixelInternalFormat.Rgb16ui, 48
            PixelInternalFormat.Rgba8ui, 32
            PixelInternalFormat.Rgb8ui, 24
            PixelInternalFormat.Rgba32i, 128
            PixelInternalFormat.Rgb32i, 96
            PixelInternalFormat.Rgba16i, 64
            PixelInternalFormat.Rgb16i, 48
            PixelInternalFormat.Rgba8i, 32
            PixelInternalFormat.Rgb8i, 24
            PixelInternalFormat.Float32UnsignedInt248Rev, 40
            //PixelInternalFormat.CompressedRedRgtc1, 0
            //PixelInternalFormat.CompressedSignedRedRgtc1, 0
            //PixelInternalFormat.CompressedRgRgtc2, 0
            //PixelInternalFormat.CompressedSignedRgRgtc2, 0
            //PixelInternalFormat.CompressedRgbaBptcUnorm, 0
            //PixelInternalFormat.CompressedSrgbAlphaBptcUnorm, 0
            //PixelInternalFormat.CompressedRgbBptcSignedFloat, 0
            //PixelInternalFormat.CompressedRgbBptcUnsignedFloat, 0
            PixelInternalFormat.R8Snorm, 8
            PixelInternalFormat.Rg8Snorm, 16
            PixelInternalFormat.Rgb8Snorm, 24
            PixelInternalFormat.Rgba8Snorm, 32
            PixelInternalFormat.R16Snorm, 16
            PixelInternalFormat.Rg16Snorm, 32
            PixelInternalFormat.Rgb16Snorm, 48
            PixelInternalFormat.Rgba16Snorm, 64
            PixelInternalFormat.Rgb10A2ui, 32
            PixelInternalFormat.One, 1
            PixelInternalFormat.Two, 2
            PixelInternalFormat.Three, 3
            PixelInternalFormat.Four, 4
        ]

module RenderbufferStorage =
    open System.Collections.Generic
    open OpenTK.Graphics.OpenGL4

    let getSizeInBits = 
        InternalFormat.lookupTable 8 [
            RenderbufferStorage.DepthComponent, 32
            RenderbufferStorage.R3G3B2, 8
            RenderbufferStorage.Rgb4, 12
            RenderbufferStorage.Rgb5, 15
            RenderbufferStorage.Rgb8, 24
            RenderbufferStorage.Rgb10, 30
            RenderbufferStorage.Rgb12, 36
            RenderbufferStorage.Rgb16, 48
            RenderbufferStorage.Rgba2, 8
            RenderbufferStorage.Rgba4, 16
            RenderbufferStorage.Rgba8, 32
            RenderbufferStorage.Rgb10A2, 32
            RenderbufferStorage.Rgba12, 48
            RenderbufferStorage.Rgba16, 64
            RenderbufferStorage.DepthComponent16, 16
            RenderbufferStorage.DepthComponent24, 24
            RenderbufferStorage.DepthComponent32, 32
            RenderbufferStorage.R8, 8
            RenderbufferStorage.R16, 16
            RenderbufferStorage.Rg8, 16
            RenderbufferStorage.Rg16, 32
            RenderbufferStorage.R16f, 16
            RenderbufferStorage.R32f, 32
            RenderbufferStorage.Rg16f, 32
            RenderbufferStorage.Rg32f, 64
            RenderbufferStorage.R8i, 8
            RenderbufferStorage.R8ui, 8
            RenderbufferStorage.R16i, 16
            RenderbufferStorage.R16ui, 16
            RenderbufferStorage.R32i, 32
            RenderbufferStorage.R32ui, 32
            RenderbufferStorage.Rg8i, 16
            RenderbufferStorage.Rg8ui, 16
            RenderbufferStorage.Rg16i, 32
            RenderbufferStorage.Rg16ui, 32
            RenderbufferStorage.Rg32i, 64
            RenderbufferStorage.Rg32ui, 64
            RenderbufferStorage.DepthStencil, 32
            RenderbufferStorage.Rgba32f, 128
            RenderbufferStorage.Rgb32f, 96
            RenderbufferStorage.Rgba16f, 64
            RenderbufferStorage.Rgb16f, 48
            RenderbufferStorage.Depth24Stencil8, 32
            RenderbufferStorage.R11fG11fB10f, 32
            RenderbufferStorage.Rgb9E5, 32
            RenderbufferStorage.Srgb8, 24
            RenderbufferStorage.Srgb8Alpha8, 32
            RenderbufferStorage.DepthComponent32f, 32
            RenderbufferStorage.Depth32fStencil8, 40
            RenderbufferStorage.StencilIndex1, 1
            //RenderbufferStorage.StencilIndex1Ext, 1
            RenderbufferStorage.StencilIndex4, 4
            //RenderbufferStorage.StencilIndex4Ext, 4
            RenderbufferStorage.StencilIndex8, 8
            //RenderbufferStorage.StencilIndex8Ext, 8
            RenderbufferStorage.StencilIndex16, 16
            //RenderbufferStorage.StencilIndex16Ext, 16
            RenderbufferStorage.Rgba32ui, 128
            RenderbufferStorage.Rgb32ui, 96
            RenderbufferStorage.Rgba16ui, 64
            RenderbufferStorage.Rgb16ui, 48
            RenderbufferStorage.Rgba8ui, 32
            RenderbufferStorage.Rgb8ui, 24
            RenderbufferStorage.Rgba32i, 128
            RenderbufferStorage.Rgb32i, 96
            RenderbufferStorage.Rgba16i, 64
            RenderbufferStorage.Rgb16i, 48
            RenderbufferStorage.Rgba8i, 32
            RenderbufferStorage.Rgb8i, 24
            RenderbufferStorage.Rgb10A2ui, 32
        ]
