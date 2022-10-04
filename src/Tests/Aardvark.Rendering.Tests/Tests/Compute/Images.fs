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

        let private genericFlipY<'T when 'T : equality>
                (createShader : IComputeRuntime -> IComputeShader)
                (getPixImage : V2i -> PixImage<'T>)
                (format : TextureFormat) (runtime : IRuntime) =

            let size = V2i(256)
            let pix = getPixImage size

            use shader = createShader runtime

            use src = runtime.CreateTexture2D(size, format)
            use dst = runtime.CreateTexture2D(size, format)
            src.Upload pix

            use input = runtime.NewInputBinding(shader)
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

        let flipYrgba8 (runtime : IRuntime) =
            let createShader (runtime : IComputeRuntime) = runtime.CreateComputeShader Shader.flipY<Formats.rgba8>
            runtime |> genericFlipY createShader PixImage.random8ui TextureFormat.Rgba8

        let flipYrgba16 (runtime : IRuntime) =
            let createShader (runtime : IComputeRuntime) = runtime.CreateComputeShader Shader.flipY<Formats.rgba16>
            runtime |> genericFlipY createShader PixImage.random16ui TextureFormat.Rgba16

        let flipYrgba32f (runtime : IRuntime) =
            let createShader (runtime : IComputeRuntime) = runtime.CreateComputeShader Shader.flipY<Formats.rgba32f>
            runtime |> genericFlipY createShader PixImage.random32f TextureFormat.Rgba32f

        let flipYrgba8i (runtime : IRuntime) =
            let createShader (r : IComputeRuntime) = r.CreateComputeShader Shader.intFlipY<Formats.rgba8i>
            runtime |> genericFlipY createShader PixImage.random8i TextureFormat.Rgba8i

        let flipYrgba16i (runtime : IRuntime) =
            let createShader (r : IComputeRuntime) = r.CreateComputeShader Shader.intFlipY<Formats.rgba16i>
            runtime |> genericFlipY createShader PixImage.random16i TextureFormat.Rgba16i

        let flipYrgba32i (runtime : IRuntime) =
            let createShader (r : IComputeRuntime) = r.CreateComputeShader Shader.intFlipY<Formats.rgba32i>
            runtime |> genericFlipY createShader PixImage.random32i TextureFormat.Rgba32i

        let flipYrgba8ui  (runtime : IRuntime) =
            let createShader (runtime : IComputeRuntime) = runtime.CreateComputeShader Shader.uintFlipY<Formats.rgba8ui>
            runtime |> genericFlipY createShader PixImage.random8ui TextureFormat.Rgba8ui

        let flipYrgba16ui  (runtime : IRuntime) =
            let createShader (runtime : IComputeRuntime) = runtime.CreateComputeShader Shader.uintFlipY<Formats.rgba16ui>
            runtime |> genericFlipY createShader PixImage.random16ui TextureFormat.Rgba16ui

        let flipYrgba32ui  (runtime : IRuntime) =
            let createShader (runtime : IComputeRuntime) = runtime.CreateComputeShader Shader.uintFlipY<Formats.rgba32ui>
            runtime |> genericFlipY createShader PixImage.random32ui TextureFormat.Rgba32ui


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
        ]
        |> prepareCases backend "Images"