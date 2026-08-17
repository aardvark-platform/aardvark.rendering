namespace Aardvark.Rendering.Tests

open System
open Aardvark.Base
open Aardvark.Rendering
open Expecto

module ``Resource Validation Tests`` =

    type private Texture(dimension : TextureDimension, size : V3i, count : int, levels : int, format : TextureFormat) =
        let mutable name = null

        interface IBackendTexture with
            member _.Runtime = Unchecked.defaultof<ITextureRuntime>
            member _.Dimension = dimension
            member _.Format = format
            member _.Samples = 1
            member _.Count = count
            member _.MipMapLevels = levels
            member _.Size = size
            member _.Handle = 0UL
            member _.WantMipMaps = levels > 1
            member _.Name with get() = name and set value = name <- value
            member _.Dispose() = ()

    let private texture dimension size count levels =
        new Texture(dimension, size, count, levels, TextureFormat.Rgba8) :> IBackendTexture

    let private compressedTexture (size : V2i) =
        new Texture(TextureDimension.Texture2D, V3i(size, 1), 1, 1, TextureFormat.CompressedRgbaS3tcDxt1) :> IBackendTexture

    let private expectArgumentException action =
        try
            action()
            failtest "Expected ArgumentException"
        with
        | :? ArgumentException as error -> error

    let private setComponent index value (vector : V3i) =
        match index with
        | 0 -> V3i(value, vector.Y, vector.Z)
        | 1 -> V3i(vector.X, value, vector.Z)
        | _ -> V3i(vector.X, vector.Y, value)

    [<Tests>]
    let tests =
        testList "Resources.Texture validation" [
            testCase "Index ranges include exact end" <| fun _ ->
                let ordinary = texture TextureDimension.Texture2D (V3i(16, 8, 1)) 8 6
                ResourceValidation.Textures.validateSlices 0 8 ordinary
                ResourceValidation.Textures.validateSlices 7 1 ordinary
                ResourceValidation.Textures.validateLevels 0 6 ordinary
                ResourceValidation.Textures.validateLevels 5 1 ordinary

                let extreme = texture TextureDimension.Texture2D (V3i(16, 8, 1)) Int32.MaxValue Int32.MaxValue
                ResourceValidation.Textures.validateSlices 0 Int32.MaxValue extreme
                ResourceValidation.Textures.validateSlices (Int32.MaxValue - 1) 1 extreme
                ResourceValidation.Textures.validateLevels 0 Int32.MaxValue extreme
                ResourceValidation.Textures.validateLevels (Int32.MaxValue - 1) 1 extreme

            testCase "Index ranges reject negative and zero arguments" <| fun _ ->
                let value = texture TextureDimension.Texture2D (V3i(16, 8, 1)) 8 6
                expectArgumentException (fun () -> ResourceValidation.Textures.validateSlices -1 1 value) |> ignore
                expectArgumentException (fun () -> ResourceValidation.Textures.validateSlices 0 0 value) |> ignore
                expectArgumentException (fun () -> ResourceValidation.Textures.validateSlices 0 -1 value) |> ignore
                expectArgumentException (fun () -> ResourceValidation.Textures.validateLevels -1 1 value) |> ignore
                expectArgumentException (fun () -> ResourceValidation.Textures.validateLevels 0 0 value) |> ignore

            testCase "Index ranges reject overflowing ends" <| fun _ ->
                let value = texture TextureDimension.Texture2D (V3i(16, 8, 1)) 8 6

                for validate in [|
                    ResourceValidation.Textures.validateSlices
                    ResourceValidation.Textures.validateLevels
                |] do
                    let error = expectArgumentException (fun () -> validate Int32.MaxValue 2 value)
                    Expect.stringContains error.Message "2147483648" "The diagnostic end index overflowed"

                    let error = expectArgumentException (fun () -> validate 2 Int32.MaxValue value)
                    Expect.stringContains error.Message "2147483648" "The diagnostic end index overflowed"

            testCase "Windows include exact end" <| fun _ ->
                let value = texture TextureDimension.Texture3D (V3i(8, 9, 10)) 1 1
                ResourceValidation.Textures.validateWindow 0 V3i.Zero (V3i(8, 9, 10)) value
                ResourceValidation.Textures.validateWindow 0 (V3i(3, 4, 5)) (V3i(5, 5, 5)) value

            testCase "Windows reject negative and zero arguments" <| fun _ ->
                let value = texture TextureDimension.Texture3D (V3i(8, 9, 10)) 1 1

                for axis in 0 .. 2 do
                    let offset = setComponent axis -1 V3i.Zero
                    expectArgumentException (fun () -> ResourceValidation.Textures.validateWindow 0 offset V3i.III value) |> ignore

                    let zeroSize = setComponent axis 0 V3i.III
                    expectArgumentException (fun () -> ResourceValidation.Textures.validateWindow 0 V3i.Zero zeroSize value) |> ignore

                    let negativeSize = setComponent axis -1 V3i.III
                    expectArgumentException (fun () -> ResourceValidation.Textures.validateWindow 0 V3i.Zero negativeSize value) |> ignore

            testCase "Windows reject overflowing offsets on every axis" <| fun _ ->
                let value = texture TextureDimension.Texture3D (V3i(8, 9, 10)) 1 1

                for axis in 0 .. 2 do
                    let offset = setComponent axis Int32.MaxValue V3i.Zero
                    let windowSize = setComponent axis 2 V3i.III
                    expectArgumentException (fun () -> ResourceValidation.Textures.validateWindow 0 offset windowSize value) |> ignore

            testCase "Windows reject overflowing extents on every axis" <| fun _ ->
                let value = texture TextureDimension.Texture3D (V3i(8, 9, 10)) 1 1

                for axis in 0 .. 2 do
                    let offset = setComponent axis 2 V3i.Zero
                    let windowSize = setComponent axis Int32.MaxValue V3i.III
                    expectArgumentException (fun () -> ResourceValidation.Textures.validateWindow 0 offset windowSize value) |> ignore

            testCase "Compressed upload alignment is unchanged" <| fun _ ->
                let value = compressedTexture (V2i(16, 16))
                ResourceValidation.Textures.validateUploadWindow 0 V3i.Zero (V3i(16, 16, 1)) value
                ResourceValidation.Textures.validateUploadWindow 0 (V3i(4, 4, 0)) (V3i(4, 4, 1)) value
                expectArgumentException (fun () ->
                    ResourceValidation.Textures.validateUploadWindow 0 (V3i(4, 4, 0)) (V3i(5, 4, 1)) value
                ) |> ignore
        ]
