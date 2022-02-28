namespace Aardvark.Rendering.Tests.Rendering

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Aardvark.Application
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open Expecto

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

    module Cases =

        let flipY (runtime : IRuntime) =

            let size = V2i(256)
            let pix = PixImage.random8ui size
            let format = TextureFormat.ofPixFormat pix.PixFormat TextureParams.empty

            let src = runtime.PrepareTexture(PixTexture2d(pix, false))
            let dst = runtime.CreateTexture2D(size, format)
            let shader = runtime.CreateComputeShader(Shader.flipY<FShade.Formats.rgba8>)

            use input = runtime.NewInputBinding(shader)
            input.["src"] <- src.GetOutputView()
            input.["dst"] <- dst.GetOutputView()
            input.Flush()

            try
                runtime.Run([
                    ComputeCommand.Bind(shader)
                    ComputeCommand.SetInput(input)
                    ComputeCommand.TransformLayout(src, TextureLayout.General)
                    ComputeCommand.TransformLayout(dst, TextureLayout.General)
                    ComputeCommand.Dispatch(size / shader.LocalSize.XY)
                ])

                let result = dst.Download().Transformed(ImageTrafo.MirrorY).AsPixImage<uint8>()
                PixImage.compare V2i.Zero pix result

            finally
                runtime.DeleteTexture(src)
                runtime.DeleteTexture(dst)
                runtime.DeleteComputeShader(shader)


    let tests (backend : Backend) =
        [
            "Flip Y",  Cases.flipY
        ]
        |> prepareCases backend "Images"