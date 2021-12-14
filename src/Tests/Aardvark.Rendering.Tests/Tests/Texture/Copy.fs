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

            try
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

            finally
                runtime.DeleteTexture(src)
                runtime.DeleteTexture(dst)
                runtime.DeleteTexture(ms)

        let private createTexture1D (runtime : IRuntime) (size : int) (levels : int) (count : int) =
            let data =
                Array.init count (fun _ ->
                    Array.init levels (fun level ->
                        let size = Fun.MipmapLevelSize(size, level)
                        PixVolume.random <| V3i(size, 1, 1)
                    )
                )

            let texture =
                if count > 1 then
                    runtime.CreateTexture1DArray(size, TextureFormat.Rgba8, levels, count)
                else
                    runtime.CreateTexture1D(size, TextureFormat.Rgba8, levels)

            data |> Array.iteri (fun slice ->
                Array.iteri (fun level _ ->
                    let target = texture.[TextureAspect.Color, level, slice]
                    target.Upload(data.[slice].[level])
                )
            )

            texture, data

        let private copyTexture1D (runtime : IRuntime)
                                  (srcSize : int) (srcLevels : int) (srcCount : int)
                                  (dstLevels : int) (dstCount : int)
                                  (srcBaseSlice : int) (srcBaseLevel : int)
                                  (dstBaseSlice : int) (copyLevels : int) (copySlices : int) =

            let levelDelta = dstLevels - srcLevels
            let dstSize = srcSize * (pown 2 levelDelta)

            let src, data = createTexture1D runtime srcSize srcLevels srcCount
            let dst =
                if dstCount > 1 then
                    runtime.CreateTexture1DArray(dstSize, TextureFormat.Rgba8, dstLevels, dstCount)
                else
                    runtime.CreateTexture1D(dstSize, TextureFormat.Rgba8, dstLevels)

            try
                let dstBaseLevel = srcBaseLevel + levelDelta
                runtime.Copy(src, srcBaseSlice, srcBaseLevel, dst, dstBaseSlice, dstBaseLevel, copySlices, copyLevels)

                for srcSlice = srcBaseSlice to srcBaseSlice + copySlices - 1 do
                    let dstSlice = dstBaseSlice + srcSlice - srcBaseSlice

                    for srcLevel = srcBaseLevel to srcBaseLevel + copyLevels - 1 do
                        let dstLevel = dstBaseLevel + srcLevel - srcBaseLevel

                        let levelSize = Fun.MipmapLevelSize(srcSize, srcLevel)
                        let target = dst.[TextureAspect.Color, dstLevel, dstSlice]
                        let result = PixVolume<byte>(Col.Format.RGBA, V3i(levelSize, 1, 1))
                        runtime.Download(target, result)

                        Expect.equal (dst.GetSize(dstLevel)) data.[srcSlice].[srcLevel].Size "image size mismatch"
                        PixVolume.compare V3i.Zero data.[srcSlice].[srcLevel] result

            finally
                runtime.DeleteTexture(src)
                runtime.DeleteTexture(dst)

        let texture1DMipmapped (runtime : IRuntime) =
            let srcSize = 123
            let srcLevels = 4
            let srcSlices = 1
            let dstLevels = srcLevels + 1
            let dstSlices = 1

            let srcBaseSlice = 0
            let srcBaseLevel = 2
            let dstBaseSlice = 0

            let copyLevels = 2
            let copySlices = 1

            copyTexture1D runtime srcSize srcLevels srcSlices dstLevels dstSlices
                                  srcBaseSlice srcBaseLevel dstBaseSlice copyLevels copySlices

        let texture1DArrayMipmapped (runtime : IRuntime) =
            let srcSize = 123
            let srcLevels = 4
            let srcSlices = 3
            let dstLevels = srcLevels + 1
            let dstSlices = 6

            let srcBaseSlice = 1
            let srcBaseLevel = 2
            let dstBaseSlice = 3

            let copyLevels = 2
            let copySlices = 2

            copyTexture1D runtime srcSize srcLevels srcSlices dstLevels dstSlices
                                  srcBaseSlice srcBaseLevel dstBaseSlice copyLevels copySlices

        let private copyTexture1DSubwindow (runtime : IRuntime)
                                           (srcSize : int) (srcLevels : int) (srcCount : int)
                                           (dstLevels : int) (dstCount : int)
                                           (srcBaseSlice : int) (srcLevel : int)
                                           (srcOffset : int) (dstOffset : int) (windowSize : int)
                                           (dstBaseSlice : int) (copySlices : int) =

            let levelDelta = dstLevels - srcLevels
            let dstSize = srcSize * (pown 2 levelDelta)

            let src, data = createTexture1D runtime srcSize srcLevels srcCount
            let dst =
                if dstCount > 1 then
                    runtime.CreateTexture1DArray(dstSize, TextureFormat.Rgba8, dstLevels, dstCount)
                else
                    runtime.CreateTexture1D(dstSize, TextureFormat.Rgba8, dstLevels)

            try
                let dstLevel = srcLevel + levelDelta
                let copySrc = src.[TextureAspect.Color, srcLevel, srcBaseSlice .. srcBaseSlice + copySlices - 1]
                let copyDst = dst.[TextureAspect.Color, dstLevel, dstBaseSlice .. dstBaseSlice + copySlices - 1]

                runtime.Copy(
                    copySrc, V3i(srcOffset, 0, 0),
                    copyDst, V3i(dstOffset, 0, 0),
                    V3i(windowSize, 1, 1)
                )

                for srcSlice = srcBaseSlice to srcBaseSlice + copySlices - 1 do
                    let dstSlice = dstBaseSlice + srcSlice - srcBaseSlice

                    let target = dst.[TextureAspect.Color, dstLevel, dstSlice]
                    let result = PixVolume<byte>(Col.Format.RGBA, V3i(windowSize, 1, 1))
                    target.Download(result, V3i(dstOffset, 0, 0), result.Size)

                    PixVolume.compare (V3i(-srcOffset, 0, 0)) data.[srcSlice].[srcLevel] result

            finally
                runtime.DeleteTexture(src)
                runtime.DeleteTexture(dst)

        let texture1DMipmappedSubwindow (runtime : IRuntime) =
            let srcSize = 123
            let srcLevels = 4
            let srcSlices = 1
            let dstLevels = srcLevels + 1
            let dstSlices = 1

            let srcBaseSlice = 0
            let srcLevel = 2
            let srcOffset = 13
            let dstBaseSlice = 0
            let dstOffset = 16

            let copySlices = 1
            let windowSize = 7

            copyTexture1DSubwindow runtime srcSize srcLevels srcSlices dstLevels dstSlices
                                           srcBaseSlice srcLevel srcOffset dstOffset windowSize dstBaseSlice copySlices

        let texture1DArrayMipmappedSubwindow (runtime : IRuntime) =
            let srcSize = 123
            let srcLevels = 4
            let srcSlices = 3
            let dstLevels = srcLevels + 1
            let dstSlices = 6

            let srcBaseSlice = 1
            let srcLevel = 2
            let srcOffset = 13
            let dstBaseSlice = 3
            let dstOffset = 16

            let copySlices = 2
            let windowSize = 7

            copyTexture1DSubwindow runtime srcSize srcLevels srcSlices dstLevels dstSlices
                                           srcBaseSlice srcLevel srcOffset dstOffset windowSize dstBaseSlice copySlices

        let texture1DTo2D (runtime : IRuntime) =
            let srcSize = 123
            let dstSize = V2i(321, 123)
            let srcLevels = 4
            let srcSlices = 3
            let dstLevels = srcLevels + 1
            let dstSlices = 6

            let src, data = createTexture1D runtime srcSize srcLevels srcSlices
            let dst = runtime.CreateTexture2DArray(dstSize, TextureFormat.Rgba8, levels = dstLevels, count = dstSlices)

            try
                let srcLevel = 0
                let srcBaseSlice = 0
                let srcOffset = 13
                let dstLevel = 1
                let dstBaseSlice = 3
                let dstOffset = 7
                let copySlices = 2
                let copySize = 51

                let copySrc = src.[TextureAspect.Color, srcLevel, srcBaseSlice .. srcBaseSlice + copySlices - 1]
                let copyDst = dst.[TextureAspect.Color, dstLevel, dstBaseSlice .. dstBaseSlice + copySlices - 1]

                runtime.Copy(
                    copySrc, V3i(srcOffset, 0, 0),
                    copyDst, V3i(dstOffset, 0, 0),
                    V3i(copySize, 1, 1)
                )

                for srcSlice = srcBaseSlice to srcBaseSlice + copySlices - 1 do
                    let dstSlice = dstBaseSlice + srcSlice - srcBaseSlice

                    let target = dst.[TextureAspect.Color, dstLevel, dstSlice]
                    let result = PixVolume<byte>(Col.Format.RGBA, V3i(copySize, 1, 1))
                    target.Download(result, V3i(dstOffset, 0, 0), result.Size)

                    PixVolume.compare (V3i(-srcOffset, 0, 0)) data.[srcSlice].[srcLevel] result

            finally
                runtime.DeleteTexture(src)
                runtime.DeleteTexture(dst)

        let texture1DToCube (runtime : IRuntime) =
            let srcSize = 123
            let dstSize = 321
            let srcLevels = 4
            let srcSlices = 3
            let dstLevels = srcLevels + 1
            let dstSlices = 2

            let src, data = createTexture1D runtime srcSize srcLevels srcSlices
            let dst = runtime.CreateTextureCubeArray(dstSize, TextureFormat.Rgba8, levels = dstLevels, count = dstSlices)

            try
                let srcLevel = 0
                let srcBaseSlice = 0
                let srcOffset = 13
                let dstLevel = 1
                let dstBaseSlice = 5
                let dstOffset = 7
                let copySlices = 2
                let copySize = 51

                let copySrc = src.[TextureAspect.Color, srcLevel, srcBaseSlice .. srcBaseSlice + copySlices - 1]
                let copyDst = dst.[TextureAspect.Color, dstLevel, dstBaseSlice .. dstBaseSlice + copySlices - 1]

                runtime.Copy(
                    copySrc, V3i(srcOffset, 0, 0),
                    copyDst, V3i(dstOffset, 0, 0),
                    V3i(copySize, 1, 1)
                )

                for srcSlice = srcBaseSlice to srcBaseSlice + copySlices - 1 do
                    let dstSlice = dstBaseSlice + srcSlice - srcBaseSlice

                    let target = dst.[TextureAspect.Color, dstLevel, dstSlice]
                    let result = PixVolume<byte>(Col.Format.RGBA, V3i(copySize, 1, 1))
                    target.Download(result, V3i(dstOffset, 0, 0), result.Size)

                    PixVolume.compare (V3i(-srcOffset, 0, 0)) data.[srcSlice].[srcLevel] result

            finally
                runtime.DeleteTexture(src)
                runtime.DeleteTexture(dst)

        let texture1DTo3D (runtime : IRuntime) =
            let srcSize = 123
            let dstSize = V3i(321, 123, 43)
            let srcLevels = 4
            let srcSlices = 3
            let dstLevels = srcLevels + 1

            let src, data = createTexture1D runtime srcSize srcLevels srcSlices
            let dst = runtime.CreateTexture3D(dstSize, TextureFormat.Rgba8, levels = dstLevels)

            try
                let srcLevel = 0
                let srcOffset = 13
                let dstLevel = 1
                let dstOffset = 7
                let copySize = 51

                let copySrc = src.[TextureAspect.Color, srcLevel, 0]
                let copyDst = dst.[TextureAspect.Color, dstLevel, 0]

                runtime.Copy(
                    copySrc, V3i(srcOffset, 0, 0),
                    copyDst, V3i(dstOffset, 0, 0),
                    V3i(copySize, 1, 1)
                )

                let target = dst.[TextureAspect.Color, dstLevel, 0]
                let result = PixVolume<byte>(Col.Format.RGBA, V3i(copySize, 1, 1))
                target.Download(result, V3i(dstOffset, 0, 0), result.Size)

                PixVolume.compare (V3i(-srcOffset, 0, 0)) data.[0].[srcLevel] result

            finally
                runtime.DeleteTexture(src)
                runtime.DeleteTexture(dst)

        let texture2DArrayMipmapped (runtime : IRuntime) =
            let size = V2i(333, 666)
            let levels = 4
            let count = 5

            let data =
                Array.init count (fun index ->
                    let data = PixImage.random <| V2i(256)

                    Array.init levels (fun level ->
                        let size = size / (1 <<< level)
                        data |> PixImage.resized size
                    )
                )

            let format = TextureFormat.ofPixFormat data.[0].[0].PixFormat TextureParams.empty
            let src = runtime.CreateTexture2DArray(size, format, levels = levels, count = count)
            let dst = runtime.CreateTexture2DArray(size, format, levels = levels, count = count)

            try
                data |> Array.iteri (fun index mipmaps ->
                    mipmaps |> Array.iteri (fun level img ->
                        src.Upload(img, level, index)
                    )
                )

                runtime.Copy(src, 2, 1, dst, 2, 1, 3, 3)

                for i in 2 .. 4 do
                    for l in 1 .. 3 do
                        let result = runtime.Download(dst, level = l, slice = i).ToPixImage<byte>()
                        let levelSize = size / (1 <<< l)

                        Expect.equal result.Size levelSize "Texture size inconsistent"
                        PixImage.compare V2i.Zero data.[i].[l] result

            finally
                runtime.DeleteTexture(src)
                runtime.DeleteTexture(dst)

        let texture2DMultisampled (resolve : bool) (runtime : IRuntime) =
            let data = PixImage.random <| V2i(256)
            let size = data.Size
            let samples = 8

            let signature = runtime.CreateFramebufferSignature(samples, [DefaultSemantic.Colors, TextureFormat.Rgba8])
            let src = runtime.CreateTexture2D(size, TextureFormat.Rgba8, levels = 1, samples = samples)
            let dst = runtime.CreateTexture2D(size, TextureFormat.Rgba8, levels = 1, samples = if resolve then 1 else samples)
            let framebuffer = runtime.CreateFramebuffer(signature, [DefaultSemantic.Colors, src.GetOutputView()])

            let sampler =
                Some { SamplerState.Default with Filter = TextureFilter.MinMagPoint }

            try
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

                Expect.equal result.Size size "Texture size inconsistent"
                PixImage.compare V2i.Zero data result

            finally
                runtime.DeleteFramebuffer(framebuffer)
                runtime.DeleteTexture(src)
                runtime.DeleteTexture(dst)
                runtime.DeleteFramebufferSignature(signature)


        let textureCubeMipmapped (runtime : IRuntime) =
            let levels = 3
            let size = V2i(128)

            let data =
                CubeMap.init levels (fun side level ->
                    let data = PixImage.random <| V2i(256)
                    let size = size / (1 <<< level)
                    data |> PixImage.resized size
                )

            let format = TextureFormat.ofPixFormat data.[CubeSide.PositiveX].PixFormat TextureParams.empty
            let src = runtime.CreateTextureCube(size.X, format, levels = levels)
            let dst = runtime.CreateTextureCube(size.X, format, levels = levels)

            try
                data |> CubeMap.iteri (fun side level img ->
                    src.Upload(img, level, int side)
                )

                runtime.Copy(src, 3, 1, dst, 3, 1, 3, 2)

                for slice in 3 .. 5 do
                    for level in 1 .. 2 do
                        let result = runtime.Download(dst, level = level, slice = slice).ToPixImage<byte>()
                        let levelSize = size / (1 <<< level)
                        let side = unbox<CubeSide> (slice % 6)

                        Expect.equal result.Size levelSize "Texture size inconsistent"
                        PixImage.compare V2i.Zero data.[side, level] result

            finally
                runtime.DeleteTexture(src)
                runtime.DeleteTexture(dst)


        let textureCubeArrayMipmapped (runtime : IRuntime) =
            let count = 2
            let levels = 3
            let size = V2i(128)

            let data =
                Array.init count (fun index ->
                    CubeMap.init levels (fun side level ->
                        let data = PixImage.random <| V2i(256)
                        let size = size / (1 <<< level)
                        data |> PixImage.resized size
                    )
                )

            let format = TextureFormat.ofPixFormat data.[0].[CubeSide.PositiveX].PixFormat TextureParams.empty
            let src = runtime.CreateTextureCubeArray(size.X, format, levels = levels, count = count)
            let dst = runtime.CreateTextureCubeArray(size.X, format, levels = levels, count = count)

            try
                data |> Array.iteri (fun index mipmaps ->
                    mipmaps |> CubeMap.iteri (fun side level img ->
                        src.Upload(img, level, index * 6 + int side)
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

            finally
                runtime.DeleteTexture(src)
                runtime.DeleteTexture(dst)

    let tests (backend : Backend) =
        [
            "Arguments out of range",           Cases.argumentsOutOfRange

            "1D mipmapped",                     Cases.texture1DMipmapped
            "1D mipmapped subwindow",           Cases.texture1DMipmappedSubwindow
            "1D array mipmapped",               Cases.texture1DArrayMipmapped
            "1D array mipmapped subwindow",     Cases.texture1DArrayMipmappedSubwindow
            "1D to 2D",                         Cases.texture1DTo2D
            "1D to 3D",                         Cases.texture1DTo3D
            "1D to Cube",                       Cases.texture1DToCube

            "2D array mipmapped",               Cases.texture2DArrayMipmapped
            "2D multisampled",                  Cases.texture2DMultisampled false
            "2D multisampled (with resolve)",   Cases.texture2DMultisampled true
            "Cube mipmapped",                   Cases.textureCubeMipmapped
            "Cube array mipmapped",             Cases.textureCubeArrayMipmapped
        ]
        |> prepareCases backend "Copy"