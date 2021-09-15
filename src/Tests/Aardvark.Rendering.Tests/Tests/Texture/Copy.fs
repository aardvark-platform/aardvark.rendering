namespace Aardvark.Rendering.Tests.Texture

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.Application
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open Expecto

module TextureCopy =

    module Cases =

        let argumentsOutOfRange (runtime : IRuntime) =

            let copy src srcSlice srcLevel dst dstSlice dstLevel slices levels () =
                runtime.Copy(src, srcSlice, srcLevel, dst, dstSlice, dstLevel, slices, levels)

            let size = V2i(333, 666)
            let src = runtime.CreateTexture2DArray(size, TextureFormat.Rgba8, levels = 4, count = 3)
            let dst = runtime.CreateTexture2DArray(size, TextureFormat.Rgba8, levels = 3, count = 5)
            let ms = runtime.CreateTexture2D(size, TextureFormat.Rgba8, levels = 1, samples = 8)

            copy src -1  0 dst  0  0  1  1 |> shouldThrowArgExn "cannot be negative"
            copy src  0 -1 dst  0  0  1  1 |> shouldThrowArgExn "cannot be negative"
            copy src  0  0 dst -1  0  1  1 |> shouldThrowArgExn "cannot be negative"
            copy src  0  0 dst  0 -1  1  1 |> shouldThrowArgExn "cannot be negative"
            copy src  0  0 dst  0  0  0  1 |> shouldThrowArgExn "must be greater than zero"
            copy src  0  0 dst  0  0  1  0 |> shouldThrowArgExn "must be greater than zero"

            copy src 1 0 dst 1 1 1 1 |> shouldThrowArgExn "sizes of texture levels do not match"
            copy src 1 2 dst 1 1 1 1 |> shouldThrowArgExn "sizes of texture levels do not match"

            copy src 2 0 dst 0 0 2 1 |> shouldThrowArgExn "cannot access texture slices with index range"
            copy src 0 0 dst 4 0 2 1 |> shouldThrowArgExn "cannot access texture slices with index range"
            copy src 0 2 dst 0 2 1 2 |> shouldThrowArgExn "cannot access texture levels with index range"
            copy dst 0 2 src 0 2 1 2 |> shouldThrowArgExn "cannot access texture levels with index range"

            copy src 0 0 ms 0 0 1 1 |> shouldThrowArgExn "samples of textures do not match"

            runtime.DeleteTexture(src)
            runtime.DeleteTexture(dst)
            runtime.DeleteTexture(ms)


        let simple (runtime : IRuntime) =
            let size = V2i(333, 666)
            let levels = 4
            let count = 5

            let data =
                Array.init count (fun index ->
                    let data = PixImage.checkerboard testColors.[index]

                    Array.init levels (fun level ->
                        let size = size / (1 <<< level)
                        data |> PixImage.resized size
                    )
                )

            let format = TextureFormat.ofPixFormat data.[0].[0].PixFormat TextureParams.empty
            let src = runtime.CreateTexture2DArray(size, format, levels = levels, count = count)
            let dst = runtime.CreateTexture2DArray(size, format, levels = levels, count = count)

            data |> Array.iteri (fun index mipmaps ->
                mipmaps |> Array.iteri (fun level img ->
                    runtime.Upload(src, level, index, img)
                )
            )

            runtime.Copy(src, 2, 1, dst, 2, 1, 3, 3)

            for i in 2 .. 4 do
                for l in 1 .. 3 do
                    let result = runtime.Download(dst, level = l, slice = i).ToPixImage<byte>()
                    let levelSize = size / (1 <<< l)

                    Expect.equal result.Size levelSize "Texture size inconsistent"
                    PixImage.compare V2i.Zero data.[i].[l] result

            runtime.DeleteTexture(src)
            runtime.DeleteTexture(dst)


        let multisampled (resolve : bool) (runtime : IRuntime) =
            let data = PixImage.checkerboard C4b.BurlyWood
            let size = data.Size
            let samples = 8

            let signature = runtime.CreateFramebufferSignature(samples, [DefaultSemantic.Colors, RenderbufferFormat.Rgba8])
            let src = runtime.CreateTexture2D(size, TextureFormat.Rgba8, levels = 1, samples = samples)
            let dst = runtime.CreateTexture2D(size, TextureFormat.Rgba8, levels = 1, samples = if resolve then 1 else samples)
            let framebuffer = runtime.CreateFramebuffer(signature, [DefaultSemantic.Colors, src.GetOutputView()])

            let sampler =
                Some { SamplerState.Default with Filter = TextureFilter.MinMagPoint }

            use task =
                Sg.fullScreenQuad
                |> Sg.diffuseTexture' (data |> PixImage.toTexture false)
                |> Sg.samplerState' DefaultSemantic.DiffuseColorTexture sampler
                |> Sg.shader {
                    do! DefaultSurfaces.diffuseTexture
                }
                |> Sg.compile runtime signature

            task.Run(RenderToken.Empty, framebuffer)

            runtime.Copy(src, 0, 0, dst, 0, 0, 1, 1)
            let result = runtime.Download(dst).ToPixImage<byte>()

            runtime.DeleteFramebuffer(framebuffer)
            runtime.DeleteTexture(src)
            runtime.DeleteTexture(dst)
            runtime.DeleteFramebufferSignature(signature)

            Expect.equal result.Size size "Texture size inconsistent"
            PixImage.compare V2i.Zero data result


        let mipmappedCube (runtime : IRuntime) =
            let levels = 3
            let size = V2i(128)

            let data =
                CubeMap.init levels (fun side level ->
                    let data = PixImage.checkerboard testColors.[int side]
                    let size = size / (1 <<< level)
                    data |> PixImage.resized size
                )

            let format = TextureFormat.ofPixFormat data.[CubeSide.PositiveX].PixFormat TextureParams.empty
            let src = runtime.CreateTextureCube(size.X, format, levels = levels)
            let dst = runtime.CreateTextureCube(size.X, format, levels = levels)

            data |> CubeMap.iteri (fun side level img ->
                runtime.Upload(src, level, int side, img)
            )

            runtime.Copy(src, 3, 1, dst, 3, 1, 3, 2)

            for slice in 3 .. 5 do
                for level in 1 .. 2 do
                    let result = runtime.Download(dst, level = level, slice = slice).ToPixImage<byte>()
                    let levelSize = size / (1 <<< level)
                    let side = unbox<CubeSide> (slice % 6)

                    Expect.equal result.Size levelSize "Texture size inconsistent"
                    PixImage.compare V2i.Zero data.[side, level] result

            runtime.DeleteTexture(src)
            runtime.DeleteTexture(dst)


        let mipmappedCubeArray (runtime : IRuntime) =
            let count = 2
            let levels = 3
            let size = V2i(128)

            let data =
                Array.init count (fun index ->
                    CubeMap.init levels (fun side level ->
                        let data = PixImage.checkerboard testColors.[index * 6 + int side]
                        let size = size / (1 <<< level)
                        data |> PixImage.resized size
                    )
                )

            let format = TextureFormat.ofPixFormat data.[0].[CubeSide.PositiveX].PixFormat TextureParams.empty
            let src = runtime.CreateTextureCubeArray(size.X, format, levels = levels, count = count)
            let dst = runtime.CreateTextureCubeArray(size.X, format, levels = levels, count = count)

            data |> Array.iteri (fun index mipmaps ->
                mipmaps |> CubeMap.iteri (fun side level img ->
                    runtime.Upload(src, level, index * 6 + int side, img)
                )
            )

            runtime.Copy(src, 2, 1, dst, 2, 1, 7, 2)

            for slice in 2 .. 8 do
                for level in 1 .. 2 do
                    let result = runtime.Download(dst, level = level, slice = slice).ToPixImage<byte>()
                    let levelSize = size / (1 <<< level)
                    let index = slice / 6
                    let side = unbox<CubeSide> (slice % 6)

                    Expect.equal result.Size levelSize "Texture size inconsistent"
                    PixImage.compare V2i.Zero data.[index].[side, level] result

            runtime.DeleteTexture(src)
            runtime.DeleteTexture(dst)

    let tests (backend : Backend) =
        [
            "Arguments out of range",        Cases.argumentsOutOfRange
            "Simple",                        Cases.simple
            "Multisampled",                  Cases.multisampled false
            "Multisampled (with resolve)",   Cases.multisampled true
            "Mipmapped cube",                Cases.mipmappedCube
            "Mipmapped cube array",          Cases.mipmappedCubeArray
        ]
        |> prepareCases backend "Copy"