namespace Aardvark.Rendering.Tests.Texture

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.Application
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open Expecto

#nowarn "44"

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
                        PixVolume.random32ui <| V3i(size, 1, 1)
                    )
                )

            let texture =
                if count > 1 then
                    runtime.CreateTexture1DArray(size, TextureFormat.Rgba32ui, levels, count)
                else
                    runtime.CreateTexture1D(size, TextureFormat.Rgba32ui, levels)

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
                    runtime.CreateTexture1DArray(dstSize, src.Format, dstLevels, dstCount)
                else
                    runtime.CreateTexture1D(dstSize, src.Format, dstLevels)

            try
                let dstBaseLevel = srcBaseLevel + levelDelta
                runtime.Copy(src, srcBaseSlice, srcBaseLevel, dst, dstBaseSlice, dstBaseLevel, copySlices, copyLevels)

                for srcSlice = srcBaseSlice to srcBaseSlice + copySlices - 1 do
                    let dstSlice = dstBaseSlice + srcSlice - srcBaseSlice

                    for srcLevel = srcBaseLevel to srcBaseLevel + copyLevels - 1 do
                        let dstLevel = dstBaseLevel + srcLevel - srcBaseLevel

                        let levelSize = Fun.MipmapLevelSize(srcSize, srcLevel)
                        let target = dst.[TextureAspect.Color, dstLevel, dstSlice]
                        let result = PixVolume<uint32>(Col.Format.RGBA, V3i(levelSize, 1, 1))
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
                    runtime.CreateTexture1DArray(dstSize, src.Format, dstLevels, dstCount)
                else
                    runtime.CreateTexture1D(dstSize, src.Format, dstLevels)

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
                    let result = PixVolume<uint32>(Col.Format.RGBA, V3i(windowSize, 1, 1))
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
            let dst = runtime.CreateTexture2DArray(dstSize, src.Format, levels = dstLevels, count = dstSlices)

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
                    let result = PixVolume<uint32>(Col.Format.RGBA, V3i(copySize, 1, 1))
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
            let dst = runtime.CreateTextureCubeArray(dstSize, src.Format, levels = dstLevels, count = dstSlices)

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
                    let result = PixVolume<uint32>(Col.Format.RGBA, V3i(copySize, 1, 1))
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
            let dst = runtime.CreateTexture3D(dstSize, src.Format, levels = dstLevels)

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
                let result = PixVolume<uint32>(Col.Format.RGBA, V3i(copySize, 1, 1))
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
                    let data = PixImage.random16ui <| V2i(256)

                    Array.init levels (fun level ->
                        let size = size / (1 <<< level)
                        data |> PixImage.resized size
                    )
                )

            let format = TextureFormat.ofPixFormat data.[0].[0].PixFormat TextureParams.empty
            use src = runtime.CreateTexture2DArray(size, format, levels = levels, count = count)
            use dst = runtime.CreateTexture2DArray(size, format, levels = levels, count = count)

            data |> Array.iteri (fun index mipmaps ->
                mipmaps |> Array.iteri (fun level img ->
                    src.Upload(img, level, index)
                )
            )

            runtime.Copy(src, 2, 1, dst, 2, 1, 3, 3)

            for i in 2 .. 4 do
                for l in 1 .. 3 do
                    let result = runtime.Download(dst, level = l, slice = i).AsPixImage<uint16>()
                    let levelSize = size / (1 <<< l)

                    Expect.equal result.Size levelSize "Texture size inconsistent"
                    PixImage.compare V2i.Zero data.[i].[l] result

        let private renderToMultisampled (runtime : IRuntime) (format : TextureFormat) (samples : int) (data : PixImage)
                                         (f : IBackendTexture -> 'U) =
            use signature = runtime.CreateFramebufferSignature([DefaultSemantic.Colors, format], samples)

            let sampler =
                { SamplerState.Default with Filter = TextureFilter.MinMagPoint }

            use attachment = runtime.CreateTexture2D(data.Size, format, samples = samples)
            use framebuffer = runtime.CreateFramebuffer(signature, [DefaultSemantic.Colors, attachment.GetOutputView()])

            use task =
                Sg.fullScreenQuad
                |> Sg.diffuseTexture' (data |> PixImage.toTexture false)
                |> Sg.samplerState' DefaultSemantic.DiffuseColorTexture sampler
                |> Sg.shader {
                    do! DefaultSurfaces.diffuseTexture
                }
                |> Sg.compile runtime signature

            task.Run(RenderToken.Empty, framebuffer)
            f attachment

        let texture2DResolveMultisamples (runtime : IRuntime) =
            let data = PixImage.random8ui <| V2i(256)
            let offset = V2i(177, 201)

            renderToMultisampled runtime TextureFormat.Rgba8 8 data (fun src ->
                use dst = runtime.CreateTexture2D(src.Size.XY, TextureFormat.Rgba8, samples = 1)

                runtime.ResolveMultisamples(src.GetOutputView(), dst, srcOffset = offset)

                let expected = data |> PixImage.cropped (Box2i.FromMinAndSize(offset, data.Size - offset))
                let result = runtime.Download(dst, region = Box2i.FromSize(expected.Size)).AsPixImage<uint8>()

                PixImage.compare V2i.Zero expected result
            )

        let texture2DMultisampled (resolve : bool) (runtime : IRuntime) =
            let data = PixImage.random32f <| V2i(256)
            let size = data.Size
            let samples = 8

            renderToMultisampled runtime TextureFormat.Rgba32f samples data (fun src ->
                use dst = runtime.CreateTexture2D(size, TextureFormat.Rgba32f, levels = 1, samples = if resolve then 1 else samples)

                runtime.Copy(src, 0, 0, dst, 0, 0, 1, 1)
                let result = runtime.Download(dst).AsPixImage<float32>()

                Expect.equal result.Size size "Texture size inconsistent"
                PixImage.compare V2i.Zero data result
            )

        let texture2DBlitMultisampled (runtime : IRuntime) =
            let data = PixImage.random32f <| V2i(256)
            let size = data.Size
            let samples = 8

            renderToMultisampled runtime TextureFormat.Rgba32f samples data (fun src ->
                use dst = runtime.CreateTexture2D(size, TextureFormat.Rgba32f, samples = samples)

                src.BlitTo(dst, Box2i.FromSize size, Box2i.FromSize size)
                let result = runtime.Download(dst).AsPixImage<float32>()

                Expect.equal result.Size size "Texture size inconsistent"
                PixImage.compare V2i.Zero data result
            )

        let texture2DBlitWithMirrorAndScale (targetRmse : float) (scale : V2i) (runtime : IRuntime) =
            let data = PixImage.random8ui <| V2i(256)
            data.GetChannel(Col.Channel.Alpha).Set(255uy) |> ignore

            let size = data.Size
            let srcRegion = Box2i.FromMinAndSize(V2i(31, 15), V2i(55, 33))
            let dstRegion = Box2i.FromMinAndSize(V2i(244, 233), srcRegion.Size * -scale)

            use src = runtime.PrepareTexture(PixTexture2d data)
            use dst = runtime.CreateTexture2D(size, TextureFormat.Rgba8)

            src.BlitTo(dst, srcRegion, dstRegion)

            let result =
                runtime.Download(dst).AsPixImage<uint8>()
                |> PixImage.cropped (Box2i(dstRegion.Max, dstRegion.Min))

            let expected =
                data
                |> PixImage.cropped srcRegion
                |> PixImage.transformed ImageTrafo.MirrorX
                |> PixImage.transformed ImageTrafo.MirrorY
                |> PixImage.resized -dstRegion.Size

            Expect.equal result.Size expected.Size "Texture size inconsistent"

            let rmse = PixImage.rootMeanSquaredError expected result
            Expect.isLessThanOrEqual rmse targetRmse "Bad root-mean-square error"

        let texture2DBlitDepthWithScale (scale : V2i) (runtime : IRuntime) =
            let clear = clear { depth 0.67 }
            let format = TextureFormat.DepthComponent16
            let size = V2i(256)
            let srcRegion = Box2i.FromMinAndSize(V2i(31, 22), V2i(55, 66))
            let dstRegion = Box2i.FromMinAndSize(V2i(244, 233), srcRegion.Size * -scale)

            renderQuadToDepthStencil runtime format 1 clear size (fun src ->
                use dst = runtime.CreateTexture2D(size, format)

                let srcResult = src.DownloadDepth()
                Expect.validDepthResult srcResult Accuracy.low size 0.5 0.67

                src.BlitTo(dst, srcRegion, dstRegion)

                let dstResult = dst.DownloadDepth(region = Box2i(dstRegion.Max, dstRegion.Min))
                Expect.validDepthResult dstResult Accuracy.low -dstRegion.Size 0.5 0.67
            )

        let texture2DMultisampledDepth (runtime : IRuntime) =
            let clear = clear { depth 0.67 }
            let format = TextureFormat.DepthComponent16
            let size = V2i(256)
            let samples = 8

            renderQuadToDepthStencil runtime format samples clear size (fun src ->
                use dst = runtime.CreateTexture2D(size, format, samples = samples)

                let srcResult = resolveAndDownloadDepth src
                Expect.validDepthResult srcResult Accuracy.low size 0.5 0.67

                runtime.Copy(src, 0, 0, dst, 0, 0, 1, 1)

                let dstResult = resolveAndDownloadDepth dst
                Expect.validDepthResult dstResult Accuracy.low size 0.5 0.67
            )

        let texture2DMultisampledDepthSubwindow (runtime : IRuntime) =
            let clear =
                clear {
                    depth 0.67
                }

            let size = V2i(256)
            let format = TextureFormat.DepthComponent16
            let samples = 8

            let srcOffset = V2i(54, 23)
            let dstOffset = V2i(3, 5)
            let copySize = V2i(45, 123)

            renderQuadToDepthStencil runtime format samples clear size (fun src ->
                let dst = runtime.CreateTexture2D(size, format, samples = samples)

                try
                    let srcResult = resolveAndDownloadDepth src
                    Expect.validDepthResult srcResult Accuracy.low size 0.5 0.67

                    runtime.Copy(src.GetOutputView(), srcOffset, dst.GetOutputView(), dstOffset, copySize)

                    let dstResult = resolveAndDownloadDepth dst
                    let dstResult = dstResult.SubMatrix(dstOffset, copySize)

                    Expect.validDepthResult dstResult Accuracy.low copySize 0.5 0.67

                finally
                    runtime.DeleteTexture dst
            )

        let textureCubeMipmapped (runtime : IRuntime) =
            let levels = 3
            let size = V2i(128)

            let data =
                CubeMap.init levels (fun side level ->
                    let data = PixImage.random16ui <| V2i(256)
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
                        let result = runtime.Download(dst, level = level, slice = slice).AsPixImage<uint16>()
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
                        let data = PixImage.random16ui <| V2i(256)
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
                        let result = runtime.Download(dst, level = level, slice = slice).AsPixImage<uint16>()
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

            // Vulkan only supports 2D to 3D
            // https://vulkan.lunarg.com/doc/view/1.3.239.0/windows/1.3-extensions/vkspec.html#VUID-vkCmdCopyImage-srcImage-07743
            if backend <> Backend.Vulkan then
                "1D to 2D",                         Cases.texture1DTo2D
                "1D to 3D",                         Cases.texture1DTo3D
                "1D to Cube",                       Cases.texture1DToCube

            "2D array mipmapped",               Cases.texture2DArrayMipmapped

            "2D blit with mirror",              Cases.texture2DBlitWithMirrorAndScale 0.0 (V2i(1))
            "2D blit with mirror and scale",    Cases.texture2DBlitWithMirrorAndScale 13.0 (V2i(2, 1))
            "2D blit multisampled",             Cases.texture2DBlitMultisampled
            "2D blit depth scale",              Cases.texture2DBlitDepthWithScale (V2i(2, 2))

            "2D multisampled",                  Cases.texture2DMultisampled false
            "2D multisampled with resolve",     Cases.texture2DMultisampled true
            "2D multisampled depth",            Cases.texture2DMultisampledDepth
            "2D multisampled depth subwindow",  Cases.texture2DMultisampledDepthSubwindow

            "2D resolve multisamples",          Cases.texture2DResolveMultisamples

            "Cube mipmapped",                   Cases.textureCubeMipmapped
            "Cube array mipmapped",             Cases.textureCubeArrayMipmapped
        ]
        |> prepareCases backend "Copy"