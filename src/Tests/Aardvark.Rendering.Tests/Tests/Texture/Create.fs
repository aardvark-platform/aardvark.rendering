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
            Expect.throwsT<ArgumentException> (create size TextureDimension.Texture2D  0  2) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (create size TextureDimension.Texture2D  2  0) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (create size TextureDimension.Texture2D -1  2) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (create size TextureDimension.Texture2D  2 -4) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (create (size * -1) TextureDimension.Texture2D  1  1) "Expected ArgumentException"

            Expect.throwsT<ArgumentException> (createArray size TextureDimension.Texture2D  3  0  1) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (createArray size TextureDimension.Texture2D  3 -3  1) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (createArray size TextureDimension.Texture2D  3  1  0) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (createArray size TextureDimension.Texture2D  3  1 -1) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (createArray size TextureDimension.Texture2D  0  1  1) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (createArray size TextureDimension.Texture2D -4  1  1) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (createArray (size * -1) TextureDimension.Texture2D 1  1  2) "Expected ArgumentException"


        let invalidUsage (runtime : IRuntime) =
            let create (dimension : TextureDimension) (levels : int) (samples : int) () =
                let t = runtime.CreateTexture(V3i(128), dimension, TextureFormat.Rgba32f, levels, samples)
                runtime.DeleteTexture(t)

            let createArray (dimension : TextureDimension) (levels : int) (samples : int) (count : int) () =
                let t = runtime.CreateTextureArray(V3i(128), dimension, TextureFormat.Rgba32f, levels, samples, count)
                runtime.DeleteTexture(t)

            Expect.throwsT<ArgumentException> (create TextureDimension.Texture1D 1 8) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (createArray TextureDimension.Texture1D 1 8 4) "Expected ArgumentException"

            Expect.throwsT<ArgumentException> (create TextureDimension.Texture2D 2 8) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (createArray TextureDimension.Texture2D 2 8 4) "Expected ArgumentException"

            Expect.throwsT<ArgumentException> (create TextureDimension.Texture3D 1 4) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (createArray TextureDimension.Texture3D 1 1 4) "Expected ArgumentException"

            Expect.throwsT<ArgumentException> (create TextureDimension.TextureCube 1 8) "Expected ArgumentException"
            Expect.throwsT<ArgumentException> (createArray TextureDimension.TextureCube 1 8 4) "Expected ArgumentException"


        let validUsage (runtime : IRuntime) =
            let create (dimension : TextureDimension) (levels : int) (samples : int) () =
                let t = runtime.CreateTexture(V3i(128), dimension, TextureFormat.Rgba32f, levels, samples)
                runtime.DeleteTexture(t)

            let createArray (dimension : TextureDimension) (levels : int) (samples : int) (count : int) () =
                let t = runtime.CreateTextureArray(V3i(128), dimension, TextureFormat.Rgba32f, levels, samples, count)
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

    let tests (backend : Backend) =
        [
            "Non-Positive Arguments",  Cases.nonPositiveArguments
            "Invalid Usage",           Cases.invalidUsage
            "Valid Usage",             Cases.validUsage
        ]
        |> prepareCases backend "Create"