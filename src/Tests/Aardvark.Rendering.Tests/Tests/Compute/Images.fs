namespace Aardvark.Rendering.Tests.Rendering

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.Application

module ComputeImages =

    module private Shader =
        open FShade

        [<LocalSize(X = 8, Y = 8)>]
        let flipY<'T when 'T :> Formats.IFloatingFormat> (src : Image2d<'T>) (dst : Image2d<'T>) =
            compute {
                let id = getGlobalId().XY
                let size = min src.Size dst.Size

                if id.X < size.X && id.Y < size.Y then
                    dst.[id] <- src.[V2i(id.X, size.Y - 1 - id.Y)]
            }

        [<LocalSize(X = 8, Y = 8)>]
        let intFlipY<'T when 'T :> Formats.ISignedFormat> (src : IntImage2d<'T>) (dst : IntImage2d<'T>) =
            compute {
                let id = getGlobalId().XY
                let size = min src.Size dst.Size

                if id.X < size.X && id.Y < size.Y then
                    dst.[id] <- src.[V2i(id.X, size.Y - 1 - id.Y)]
            }

        [<LocalSize(X = 8, Y = 8)>]
        let uintFlipY<'T when 'T :> Formats.IUnsignedFormat> (src : UIntImage2d<'T>) (dst : UIntImage2d<'T>) =
            compute {
                let id = getGlobalId().XY
                let size = min src.Size dst.Size

                if id.X < size.X && id.Y < size.Y then
                    dst.[id] <- src.[V2i(id.X, size.Y - 1 - id.Y)]
            }

    module Cases =
        open FShade

        let private genericFlipY
                (shader : 'a -> 'b)
                (getPixImage : V2i -> PixImage<'T>)
                (format : TextureFormat) (runtime : IRuntime) =

            let size = V2i(256)
            let pix = getPixImage size

            use shader = runtime.CreateComputeShader shader

            use src = runtime.CreateTexture2D(size, format)
            use dst = runtime.CreateTexture2D(size, format)
            src.Upload pix

            use input = runtime.CreateInputBinding(shader)
            input.["src"] <- src.GetOutputView()
            input.["dst"] <- dst.GetOutputView()
            input.Flush()

            runtime.Run([
                ComputeCommand.Bind(shader)
                ComputeCommand.SetInput(input)
                ComputeCommand.TransformLayout(src, TextureLayout.General)
                ComputeCommand.TransformLayout(dst, TextureLayout.General)
                ComputeCommand.Dispatch(size / shader.LocalSize.XY)
            ])

            let result = dst.Download().Transformed(ImageTrafo.MirrorY).AsPixImage<'T>()
            PixImage.compare V2i.Zero pix result

        let flipYrgba8 = genericFlipY Shader.flipY<Formats.rgba8> PixImage.random8ui TextureFormat.Rgba8
        let flipYrgba16 = genericFlipY Shader.flipY<Formats.rgba16> PixImage.random16ui TextureFormat.Rgba16
        let flipYrgba32f = genericFlipY Shader.flipY<Formats.rgba32f> PixImage.random32f TextureFormat.Rgba32f

        let flipYrgba8i = genericFlipY Shader.intFlipY<Formats.rgba8i> PixImage.random8i TextureFormat.Rgba8i
        let flipYrgba16i = genericFlipY Shader.intFlipY<Formats.rgba16i> PixImage.random16i TextureFormat.Rgba16i
        let flipYrgba32i = genericFlipY Shader.intFlipY<Formats.rgba32i> PixImage.random32i TextureFormat.Rgba32i

        let flipYrgba8ui = genericFlipY Shader.uintFlipY<Formats.rgba8ui> PixImage.random8ui TextureFormat.Rgba8ui
        let flipYrgba16ui = genericFlipY Shader.uintFlipY<Formats.rgba16ui> PixImage.random16ui TextureFormat.Rgba16ui
        let flipYrgba32ui = genericFlipY Shader.uintFlipY<Formats.rgba32ui> PixImage.random32ui TextureFormat.Rgba32ui

        let genericCopy (getPixImage : V2i -> PixImage<'T>)
                        (format : TextureFormat) (runtime : IRuntime) =
            let size = V2i(177, 133)
            let pi = getPixImage size

            use t1 = runtime.CreateTexture2D(size, format)
            use t2 = runtime.CreateTexture2D(size, format)
            use t3 = runtime.CreateTexture2D(size, format)

            let srcOffset = V2i(23, 13)
            let dstSize = V2i(101, 93)
            let dstOffset = V2i(2, 4)

            t1.Upload pi

            use copy1to2 =
                runtime.CompileCompute([
                    ComputeCommand.TransformLayout(t1, TextureLayout.TransferRead)
                    ComputeCommand.TransformLayout(t2, TextureLayout.TransferWrite)
                    ComputeCommand.Copy(t1.[TextureAspect.Color, 0], t2.[TextureAspect.Color, 0])
                ])

            use copy2to3 =
                runtime.CompileCompute([
                    ComputeCommand.TransformLayout(t2, TextureLayout.TransferRead)
                    ComputeCommand.TransformLayout(t3, TextureLayout.TransferWrite)
                    ComputeCommand.Copy(t2.[TextureAspect.Color, 0], srcOffset, t3.[TextureAspect.Color, 0], dstOffset, dstSize)
                ])

            runtime.Run([
                ComputeCommand.Execute copy1to2
                ComputeCommand.Execute copy2to3
            ])

            let reference = pi.SubImage(srcOffset, dstSize)
            let result = t3.Download().AsPixImage<'T>().SubImage(dstOffset, dstSize)
            PixImage.compare V2i.Zero reference result

        let copy2Drgba8   = genericCopy PixImage.random8ui TextureFormat.Rgba8
        let copy2Drgba16  = genericCopy PixImage.random16ui TextureFormat.Rgba16
        let copy2Drgba32f = genericCopy PixImage.random32f TextureFormat.Rgba32f


    let tests (backend : Backend) =
        [
            "2D flip rgba8",    Cases.flipYrgba8
            "2D flip rgba16",   Cases.flipYrgba16
            "2D flip rgba32f",  Cases.flipYrgba32f

            "2D flip rgba8i",   Cases.flipYrgba8i
            "2D flip rgba16i",  Cases.flipYrgba16i
            "2D flip rgba32i",  Cases.flipYrgba32i

            "2D flip rgba8ui",  Cases.flipYrgba8ui
            "2D flip rgba16ui", Cases.flipYrgba16ui
            "2D flip rgba32ui", Cases.flipYrgba32ui

            "2D copy rgba8",    Cases.copy2Drgba8
            "2D copy rgba16",   Cases.copy2Drgba16
            "2D copy rgba32f",  Cases.copy2Drgba32f
        ]
        |> prepareCases backend "Images"