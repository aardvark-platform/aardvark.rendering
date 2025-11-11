namespace Aardvark.Rendering.Tests.Compute

open Aardvark.Base
open Aardvark.GPGPU
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.Application
open Expecto

module ComputePrimitives =

    module Cases =
        open FShade

        let scan (runtime : IRuntime) =
            use p = new ParallelPrimitives(runtime)

            let start = 1
            let count = 62
            let delta = 2
            let input = Array.randomInts (2 * count)

            use inputBuffer = runtime.CreateBuffer(input)
            let inputRange = inputBuffer.Elements(start, count).Strided(delta)

            use outputBuffer = runtime.CreateBuffer<int>(inputRange.Count)
            p.Scan(<@ (+) @>, inputRange, outputBuffer)

            let result = outputBuffer.Download()

            let expected =
                Array.sub input start count
                |> Array.strided delta
                |> Array.scan (+) 0
                |> Array.tail

            Expect.equal expected.Length inputRange.Count "Length mismatch"
            Expect.equal result expected "Result mismatch"

        let scan2d (runtime : IRuntime) =
            use p = new ParallelPrimitives(runtime)

            let size = V2i(41, 31)
            let input = PixImage.random32f size
            let inputMatrix = input.GetMatrix<C4f>()

            use inputTexture = runtime.PrepareTexture (PixTexture2d input)
            use outputTexture = runtime.CreateTexture2D(size, TextureFormat.Rgba32f)

            p.Scan(<@ (*) @>, inputTexture.[TextureAspect.Color, 0, 0], outputTexture.[TextureAspect.Color, 0, 0])
            let result = outputTexture.Download().AsPixImage<float32>()

            let expected = PixImage<float32>(Col.Format.RGBA, size)
            let expectedMatrix = expected.GetMatrix<C4f>()

            // 2D images are flipped -> scan bottom up
            expectedMatrix.SetByCoord(fun (coord : V2l) ->
                let size = V2l(coord.X, int64 size.Y - coord.Y - 1L) + 1L
                Seq.reduce (*) <| inputMatrix.SubMatrix(V2l(0L, coord.Y), size).Elements
            ) |> ignore

            PixImage.compare32f V2i.Zero Accuracy.medium expected result

        let scan3d (runtime : IRuntime) =
            use p = new ParallelPrimitives(runtime)

            let size = V3i(7, 11, 17)
            let input = PixVolume.random32f size
            let inputVolume = input.GetVolume<C4f>()

            use inputTexture = runtime.PrepareTexture (PixTexture3d input)
            use outputTexture = runtime.CreateTexture3D(size, TextureFormat.Rgba32f)

            p.Scan(<@ (*) @>, inputTexture.[TextureAspect.Color, 0, 0], outputTexture.[TextureAspect.Color, 0, 0])
            let result = PixVolume<float32>(input.Format, input.Size)
            outputTexture.Download(result)

            let expected = PixVolume<float32>(Col.Format.RGBA, size)
            let expectedVolume = expected.GetVolume<C4f>()

            expectedVolume.SetByCoord(fun (coord : V3l) ->
                Seq.reduce (*) <| inputVolume.SubVolume(V3l.Zero, coord + 1L).Elements
            ) |> ignore

            PixVolume.compare32f V3i.Zero Accuracy.medium expected result

        let sum (runtime : IRuntime) =
            use p = new ParallelPrimitives(runtime)

            let start = 1
            let count = 61
            let delta = 5
            let input = Array.randomInts (2 * count)

            use inputBuffer = runtime.CreateBuffer(input)
            let inputRange = inputBuffer.Elements(start, count).Strided(delta)

            let result = p.Sum(inputRange)

            let expected =
                Array.sub input start count
                |> Array.strided delta
                |> Array.sum

            Expect.equal result expected "Result mismatch"

        let fold (runtime : IRuntime) =
            use p = new ParallelPrimitives(runtime)

            let start = 7
            let count = 89
            let delta = 7
            let input = Array.randomInts (2 * count)

            use inputBuffer = runtime.CreateBuffer(input)
            let inputRange = inputBuffer.Elements(start, count).Strided(delta)

            let result = p.Fold(<@ (*) @>, inputRange)

            let expected =
                Array.sub input start count
                |> Array.strided delta
                |> Array.reduce (*)

            Expect.equal result expected "Result mismatch"

        [<ReflectedDefinition>]
        let private addByIndex (i : int) (x : int) =
            x + i * x

        let mapReduce (runtime : IRuntime) =
            use p = new ParallelPrimitives(runtime)

            let start = 0
            let count = 32
            let delta = 9
            let input = Array.randomInts (2 * count)

            use inputBuffer = runtime.CreateBuffer(input)
            let inputRange = inputBuffer.Elements(start, count).Strided(delta)

            let result = p.MapReduce(<@ addByIndex @>, <@ (+) @>, inputRange)

            let expected =
                Array.sub input start count
                |> Array.strided delta
                |> Array.mapi addByIndex
                |> Array.reduce (+)

            Expect.equal result expected "Result mismatch"

        let private reduce2D (reduce : ParallelPrimitives -> ITextureSubResource -> V4f)
                             (reference : seq<V4f> -> V4f)
                             (runtime : IRuntime) =
            use p = new ParallelPrimitives(runtime)

            let size = V2i(49, 41)
            let input = PixImage.random32f size
            input.Volume.SetByCoord(fun (c : V3l) -> input.Volume.[c] + 2.0f) |> ignore

            let inputMatrix = input.GetMatrix<C4f>()
            use inputTexture = runtime.PrepareTexture (PixTexture2d input)

            let result = inputTexture.[TextureAspect.Color, 0, 0] |> reduce p
            let expected = inputMatrix.Elements |> Seq.map v4f |> reference

            Expect.v4dClose Accuracy.medium result expected "Result mismatch"

        let private reduce3D (reduce : ParallelPrimitives -> ITextureSubResource -> V4f)
                             (reference : seq<V4f> -> V4f)
                             (runtime : IRuntime) =
            use p = new ParallelPrimitives(runtime)

            let size = V3i(37, 17, 11)
            let input = PixVolume.random32f size
            input.Tensor4.SetByCoord(fun (c : V4l) -> input.Tensor4.[c] + 2.0f) |> ignore

            let inputVolume = input.GetVolume<C4f>()
            use inputTexture = runtime.PrepareTexture (PixTexture3d input)

            let result = inputTexture.[TextureAspect.Color, 0, 0] |> reduce p
            let expected = inputVolume.Elements |> Seq.map v4f |> reference

            Expect.v4dClose Accuracy.medium result expected "Result mismatch"

        let sum2D = reduce2D (fun p -> p.Sum) Seq.sum
        let sum3D = reduce3D (fun p -> p.Sum) Seq.sum
        let min2D = reduce2D (fun p -> p.Min) (Seq.reduce min)
        let min3D = reduce3D (fun p -> p.Min) (Seq.reduce min)


    let tests (backend : Backend) =
        [
            "Scan",         Cases.scan
            "Scan 2D",      Cases.scan2d
            "Scan 3D",      Cases.scan3d
            "Sum",          Cases.sum
            "Sum 2D",       Cases.sum2D
            "Sum 3D",       Cases.sum3D
            "Min 2D",       Cases.min2D
            "Min 3D",       Cases.min3D
            "Fold",         Cases.fold
            "Map reduce",   Cases.mapReduce
        ]
        |> prepareComputeCases backend "Primitives"