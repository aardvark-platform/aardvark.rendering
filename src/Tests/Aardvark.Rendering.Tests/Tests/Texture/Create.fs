namespace Aardvark.Rendering.Tests.Texture

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Expecto

module TextureCreate =

    module Cases =

        // Limits are explicit, independent of the production mip utility.
        let private mipSizes = [
            TextureDimension.Texture1D, V3i(1, 1, 1), 1
            TextureDimension.Texture1D, V3i(2, 1, 1), 2
            TextureDimension.Texture1D, V3i(7, 1, 1), 3
            TextureDimension.Texture1D, V3i(8, 1, 1), 4
            TextureDimension.Texture1D, V3i(9, 1, 1), 4
            TextureDimension.Texture1D, V3i(16, 1, 1), 5
            TextureDimension.Texture2D, V3i(1, 1, 1), 1
            TextureDimension.Texture2D, V3i(1, 8, 1), 4
            TextureDimension.Texture2D, V3i(8, 1, 1), 4
            TextureDimension.Texture2D, V3i(3, 5, 1), 3
            TextureDimension.Texture2D, V3i(5, 3, 1), 3
            TextureDimension.Texture2D, V3i(2, 16, 1), 5
            TextureDimension.Texture2D, V3i(17, 9, 1), 5
            TextureDimension.Texture3D, V3i(1, 1, 1), 1
            TextureDimension.Texture3D, V3i(8, 1, 1), 4
            TextureDimension.Texture3D, V3i(1, 8, 1), 4
            TextureDimension.Texture3D, V3i(1, 1, 8), 4
            TextureDimension.Texture3D, V3i(3, 5, 9), 4
            TextureDimension.Texture3D, V3i(9, 3, 5), 4
            TextureDimension.Texture3D, V3i(5, 9, 3), 4
            TextureDimension.Texture3D, V3i(16, 2, 3), 5
            TextureDimension.TextureCube, V3i(1, 1, 1), 1
            TextureDimension.TextureCube, V3i(2, 2, 1), 2
            TextureDimension.TextureCube, V3i(7, 7, 1), 3
            TextureDimension.TextureCube, V3i(8, 8, 1), 4
            TextureDimension.TextureCube, V3i(9, 9, 1), 4
            TextureDimension.TextureCube, V3i(16, 16, 1), 5
        ]

        let private mipCounts dimension =
            if dimension = TextureDimension.Texture3D then [None]
            else [None; Some 1; Some 3; Some 8]

        let private createMipTexture (runtime : IRuntime) typed dimension (size : V3i) levels count =
            match typed, dimension, count with
            | false, _, None -> runtime.CreateTexture(size, dimension, TextureFormat.Rgba8, levels, 1)
            | false, _, Some count -> runtime.CreateTextureArray(size, dimension, TextureFormat.Rgba8, levels, 1, count)
            | true, TextureDimension.Texture1D, None -> runtime.CreateTexture1D(size.X, TextureFormat.Rgba8, levels)
            | true, TextureDimension.Texture1D, Some count -> runtime.CreateTexture1DArray(size.X, TextureFormat.Rgba8, levels, count)
            | true, TextureDimension.Texture2D, None -> runtime.CreateTexture2D(size.XY, TextureFormat.Rgba8, levels)
            | true, TextureDimension.Texture2D, Some count -> runtime.CreateTexture2DArray(size.XY, TextureFormat.Rgba8, levels, count = count)
            | true, TextureDimension.Texture3D, None -> runtime.CreateTexture3D(size, TextureFormat.Rgba8, levels)
            | true, TextureDimension.TextureCube, None -> runtime.CreateTextureCube(size.X, TextureFormat.Rgba8, levels)
            | true, TextureDimension.TextureCube, Some count -> runtime.CreateTextureCubeArray(size.X, TextureFormat.Rgba8, levels, count)
            | _ -> failtest "Unsupported mip fixture"

        let nonPositiveArguments (runtime : IRuntime) =
            let create (size : V3i) (dimension : TextureDimension) (levels : int) (samples : int) () =
                let t = runtime.CreateTexture(size, dimension, TextureFormat.Rgba32f, levels, samples)
                runtime.DeleteTexture(t)

            let createArray (size : V3i) (dimension : TextureDimension) (levels : int) (samples : int) (count : int) () =
                let t = runtime.CreateTextureArray(size, dimension, TextureFormat.Rgba32f, levels, samples, count)
                runtime.DeleteTexture(t)

            let size = V3i(128)
            Expect.throwsT<ArgumentException> (create size.XYI TextureDimension.Texture2D  0  2) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (create size.XYI TextureDimension.Texture2D  2  0) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (create size.XYI TextureDimension.Texture2D -1  2) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (create size.XYI TextureDimension.Texture2D  2 -4) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (create (size.XYI * -1) TextureDimension.Texture2D  1  1) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (create (V3i(0, 1, 1)) TextureDimension.Texture2D  1  1) "Expected ArgumentException"

            Expect.throwsT<ArgumentException> (createArray size.XYI TextureDimension.Texture2D  3  0  1) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (createArray size.XYI TextureDimension.Texture2D  3 -3  1) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (createArray size.XYI TextureDimension.Texture2D  3  1  0) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (createArray size.XYI TextureDimension.Texture2D  3  1 -1) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (createArray size.XYI TextureDimension.Texture2D  0  1  1) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (createArray size.XYI TextureDimension.Texture2D -4  1  1) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (createArray (size.XYI * -1) TextureDimension.Texture2D 1  1  2) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (createArray (V3i(1, 0, 1)) TextureDimension.Texture2D 1  1  2) "Expected ArgumentException"


        let invalidUsage (runtime : IRuntime) =
            let size = function
                | TextureDimension.Texture1D -> V3i(128, 1, 1)
                | TextureDimension.Texture3D -> V3i(128)
                | _ -> V3i(128, 128, 1)

            let create (dimension : TextureDimension) (levels : int) (samples : int) () =
                let t = runtime.CreateTexture(size dimension, dimension, TextureFormat.Rgba32f, levels, samples)
                runtime.DeleteTexture(t)

            let createArray (dimension : TextureDimension) (levels : int) (samples : int) (count : int) () =
                let t = runtime.CreateTextureArray(size dimension, dimension, TextureFormat.Rgba32f, levels, samples, count)
                runtime.DeleteTexture(t)

            Expect.throwsT<ArgumentException> (create TextureDimension.Texture1D 1 8) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (createArray TextureDimension.Texture1D 1 8 4) "Expected ArgumentException"

            Expect.throwsT<ArgumentException> (create TextureDimension.Texture2D 2 8) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (create TextureDimension.Texture2D 1 3) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (createArray TextureDimension.Texture2D 2 8 4) "Expected ArgumentException"

            Expect.throwsT<ArgumentException> (create TextureDimension.Texture3D 1 4) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (createArray TextureDimension.Texture3D 1 1 4) "Expected ArgumentException"

            Expect.throwsT<ArgumentException> (create TextureDimension.TextureCube 1 8) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (createArray TextureDimension.TextureCube 1 8 4) "Expected ArgumentException"

            for dimension, size, limit in mipSizes do
                for count in mipCounts dimension do
                    for typed in [false; true] do
                        for levels in [limit + 1; 32; Int32.MaxValue; Int32.MinValue] do
                            let context = $"dimension={dimension}, size={size}, levels={levels}, limit={limit}, count={count}, typed={typed}"
                            try
                                use texture = createMipTexture runtime typed dimension size levels count
                                failtestf "%s: invalid mip count was accepted" context
                            with :? ArgumentException as error ->
                                Expect.equal (error.GetType()) typeof<ArgumentException> $"{context}: validation exception type"
                                let message = if levels < 1 then "levels must be greater than 0" else $"levels must not exceed {limit} "
                                Expect.stringContains error.Message message $"{context}: rejected by creation validation before allocation"


        let validUsage (runtime : IRuntime) =
            let size = function
                | TextureDimension.Texture1D -> V3i(128, 1, 1)
                | TextureDimension.Texture2D -> V3i(128, 128, 1)
                | TextureDimension.Texture3D -> V3i(128)
                | _                          -> V3i(128, 128, 1)

            let create (dimension : TextureDimension) (levels : int) (samples : int) () =
                let t = runtime.CreateTexture(size dimension, dimension, TextureFormat.Rgba32f, levels, samples)
                runtime.DeleteTexture(t)

            let createArray (dimension : TextureDimension) (levels : int) (samples : int) (count : int) () =
                let t = runtime.CreateTextureArray(size dimension, dimension, TextureFormat.Rgba32f, levels, samples, count)
                runtime.DeleteTexture(t)

            create TextureDimension.Texture1D 1 1 ()
            create TextureDimension.Texture1D 2 1 ()
            createArray TextureDimension.Texture1D 1 1 1 ()
            createArray TextureDimension.Texture1D 2 1 1 ()
            createArray TextureDimension.Texture1D 1 1 4 ()
            createArray TextureDimension.Texture1D 2 1 4 ()

            create TextureDimension.Texture2D 1 1 ()
            create TextureDimension.Texture2D 3 1 ()
            create TextureDimension.Texture2D 1 8 ()
            createArray TextureDimension.Texture2D 1 1 1 ()
            createArray TextureDimension.Texture2D 3 1 1 ()
            createArray TextureDimension.Texture2D 1 8 1 ()
            createArray TextureDimension.Texture2D 1 1 4 ()
            createArray TextureDimension.Texture2D 3 1 4 ()
            createArray TextureDimension.Texture2D 1 8 4 ()

            create TextureDimension.Texture3D 1 1 ()
            create TextureDimension.Texture3D 2 1 ()

            create TextureDimension.TextureCube 1 1 ()
            create TextureDimension.TextureCube 3 1 ()
            createArray TextureDimension.TextureCube 1 1 1 ()
            createArray TextureDimension.TextureCube 3 1 1 ()
            createArray TextureDimension.TextureCube 1 1 4 ()
            createArray TextureDimension.TextureCube 3 1 4 ()

            for dimension, size, limit in mipSizes do
                for count in mipCounts dimension do
                    for typed in [false; true] do
                        for levels in [1; max 1 (limit - 1); limit] |> List.distinct do
                            let context = $"dimension={dimension}, size={size}, levels={levels}, limit={limit}, count={count}, typed={typed}"
                            use texture = createMipTexture runtime typed dimension size levels count
                            Expect.equal texture.Dimension dimension $"{context}: dimension"
                            Expect.equal texture.Size size $"{context}: base spatial size"
                            Expect.equal texture.MipMapLevels levels $"{context}: requested partial or full chain retained"
                            Expect.equal texture.Count (defaultArg count 1) $"{context}: array count independent of mip levels"
                            Expect.equal texture.Samples 1 $"{context}: sample count"
                            Expect.equal texture.Format TextureFormat.Rgba8 $"{context}: format"
                            for level in 0 .. levels - 1 do
                                let expected = V3i(max 1 (size.X >>> level), max 1 (size.Y >>> level), max 1 (size.Z >>> level))
                                Expect.equal (texture.GetSize level) expected $"{context}, level={level}: mip spatial extent"
                            if levels = limit then
                                Expect.equal (texture.GetSize(levels - 1)) V3i.III $"{context}: full chain ends at a singleton"

        let unsupportedMultisamples (runtime : IRuntime) =
            let t = runtime.CreateTexture2D(V2i(245), TextureFormat.Rgba8, samples = 64)

            try
                Expect.isGreaterThan t.Samples 1 "not multisampled"
                Expect.isLessThan t.Samples 64 "weird sample count"
            finally
                runtime.DeleteTexture(t)

        let memoryUsage (runtime : IRuntime) =
            let runtime = unbox<GL.Runtime> runtime
            let context = runtime.Context

            let mutable count = 0
            let mutable memory = 0L

            let check() =
                Expect.equal context.MemoryUsage.TextureCount count "unexpected texture count"
                Expect.equal context.MemoryUsage.TextureMemory memory "unexpected memory usage"

            // Simple 2D
            let t1 = runtime.CreateTexture2D(V2i(512), TextureFormat.Rgba8, levels = 1)
            count <- count + 1
            memory <- memory + (512L * 512L * 4L)
            check()

            // Multisampled 2D
            let t2 = runtime.CreateTexture2D(V2i(512), TextureFormat.R8, samples = 2)
            count <- count + 1
            memory <- memory + (512L * 512L * 2L)
            check()

            // Mipmapped 2D
            let t3 = runtime.CreateTexture2D(V2i(512), TextureFormat.Rgba32f, levels = Fun.MipmapLevels 512)
            count <- count + 1
            for i = 1 to t3.MipMapLevels do
                let size = v3l <| t3.GetSize(i - 1)
                memory <- memory + (size.X * size.Y * 16L)
            check()

            // Simple 1D
            let t4 = runtime.CreateTexture1D(123, TextureFormat.R32f)
            count <- count + 1
            memory <- memory + (123L * 4L)
            check()

            // Simple 3D
            let t5 = runtime.CreateTexture3D(V3i(3, 12, 64), TextureFormat.Rgba8)
            count <- count + 1
            memory <- memory + (3L * 12L * 64L * 4L)
            check()

            // Cube
            let t6 = runtime.CreateTextureCube(64, TextureFormat.Rgba8)
            count <- count + 1
            memory <- memory + (64L * 64L * 4L * 6L)
            check()

            // Cube array
            let t7 = runtime.CreateTextureCubeArray(123, TextureFormat.Rgba8, levels = Fun.MipmapLevels 123, count = 7)
            count <- count + 1
            for _ = 1 to t7.Count * 6 do
                for i = 1 to t7.MipMapLevels do
                    let size = v3l <| t7.GetSize(i - 1)
                    memory <- memory + (size.X * size.X * 4L)
            check()

            // Compressed
            let t8 = runtime.PrepareTexture <| EmbeddedResource.getTexture TextureParams.WantMipMaps "data/bc1.dds"

            let sizeInBytes =
                let mode = t8.Format.CompressionMode

                (0L, [0 .. t8.MipMapLevels - 1]) ||> List.fold (fun sizeInBytes level ->
                    let size = Fun.MipmapLevelSize(t8.Size, level)
                    sizeInBytes + (int64 <| CompressionMode.sizeInBytes size mode)
                )

            count <- count + 1
            memory <- memory + sizeInBytes
            check()

            t1.Dispose()
            t2.Dispose()
            t3.Dispose()
            t4.Dispose()
            t5.Dispose()
            t6.Dispose()
            t7.Dispose()
            t8.Dispose()
            count <- 0
            memory <- 0L
            check()

    let tests (target: TestTarget) =
        [
            "Non-positive arguments",        Cases.nonPositiveArguments
            "Invalid usage",                 Cases.invalidUsage
            "Valid usage",                   Cases.validUsage
            "Unsupported multisample count", Cases.unsupportedMultisamples

            if target = TestTarget.GL then
                "Memory usage",              Cases.memoryUsage
        ]
        |> prepareCasesGpu "Create" target