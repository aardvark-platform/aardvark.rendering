namespace Aardvark.Rendering.Tests.Texture

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.Application
open Expecto

module TextureCreate =

    module Cases =

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

            Expect.throwsT<ArgumentException> (createArray size.XYI TextureDimension.Texture2D  3  0  1) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (createArray size.XYI TextureDimension.Texture2D  3 -3  1) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (createArray size.XYI TextureDimension.Texture2D  3  1  0) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (createArray size.XYI TextureDimension.Texture2D  3  1 -1) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (createArray size.XYI TextureDimension.Texture2D  0  1  1) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (createArray size.XYI TextureDimension.Texture2D -4  1  1) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (createArray (size.XYI * -1) TextureDimension.Texture2D 1  1  2) "Expected ArgumentException"


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
            let t8 = runtime.PrepareTexture <| EmbeddedResource.getTexture TextureParams.mipmappedCompressed "data/bc1.dds"

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

    let tests (backend : Backend) =
        [
            "Non-positive arguments",        Cases.nonPositiveArguments
            "Invalid usage",                 Cases.invalidUsage
            "Valid usage",                   Cases.validUsage
            "Unsupported multisample count", Cases.unsupportedMultisamples

            if backend = Backend.GL then
                "Memory usage",              Cases.memoryUsage
        ]
        |> prepareCases backend "Create"