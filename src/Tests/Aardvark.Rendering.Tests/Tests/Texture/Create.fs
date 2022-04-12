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

    let tests (backend : Backend) =
        [
            "Non-positive arguments",        Cases.nonPositiveArguments
            "Invalid usage",                 Cases.invalidUsage
            "Valid usage",                   Cases.validUsage
            "Unsupported multisample count", Cases.unsupportedMultisamples
        ]
        |> prepareCases backend "Create"