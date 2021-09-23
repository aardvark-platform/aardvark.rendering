namespace Aardvark.Rendering.Tests.Texture

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.Application
open Expecto

module TextureUpload =

    module Cases =

        let private uploadAndDownloadTexture1D (runtime : IRuntime)
                                               (size : int) (levels : int) (count : int)
                                               (level : int) (slice : int)
                                               (window : Range1i option) =
            let region =
                match window with
                | Some r -> r
                | None ->
                    let s = max 1 (size >>> level)
                    Range1i(0, s)

            let regionOffset = V3i(region.Min, 0, 0)
            let regionSize = V3i(region.Size, 1, 1)
            let data = PixVolume.random regionSize
            let fmt = TextureFormat.ofPixFormat data.PixFormat TextureParams.empty

            let texture =
                if count > 1 then
                    runtime.CreateTexture1DArray(size, fmt, levels, count)
                else
                    runtime.CreateTexture1D(size, fmt, levels)


            try
                let target = texture.[TextureAspect.Color, level, slice]
                runtime.Copy(data, target, regionOffset, regionSize)

                let result = PixVolume<byte>(Col.Format.RGBA, regionSize)
                runtime.Copy(target, regionOffset, result, regionSize)

                PixVolume.compare V3i.Zero data result

            finally
                runtime.DeleteTexture(texture)

        let texture1D (runtime : IRuntime) =
            uploadAndDownloadTexture1D runtime 100 1 1 0 0 None

        let texture1DSubwindow (runtime : IRuntime) =
            let region = Some <| Range1i(11, 87)
            uploadAndDownloadTexture1D runtime 100 1 1 0 0 region

        let texture1DLevel (runtime : IRuntime) =
            uploadAndDownloadTexture1D runtime 100 4 1 2 0 None

        let texture1DLevelSubwindow (runtime : IRuntime) =
            let region = Some <| Range1i(13, 47)
            uploadAndDownloadTexture1D runtime 100 4 1 1 0 region

        let texture1DArray (runtime : IRuntime) =
            uploadAndDownloadTexture1D runtime 100 1 5 0 2 None

        let texture1DArraySubwindow (runtime : IRuntime) =
            let region = Some <| Range1i(11, 87)
            uploadAndDownloadTexture1D runtime 100 1 5 0 2 region

        let texture1DArrayLevel (runtime : IRuntime) =
            uploadAndDownloadTexture1D runtime 100 4 5 2 1 None

        let texture1DArrayLevelSubwindow (runtime : IRuntime) =
            let region = Some <| Range1i(13, 47)
            uploadAndDownloadTexture1D runtime 100 4 5 1 3 region


        let private uploadAndDownloadTexture2D (runtime : IRuntime)
                                               (size : V2i) (levels : int) (count : int) (samples : int)
                                               (level : int) (slice : int)
                                               (window : Box2i option) =

            let region =
                match window with
                | Some r -> r
                | None ->
                    let s = max 1 (size >>> level)
                    Box2i(V2i.Zero, s)

            let data = PixImage.random region.Size
            let fmt = TextureFormat.ofPixFormat data.PixFormat TextureParams.empty

            let texture =
                if count > 1 then
                    runtime.CreateTexture2DArray(size, fmt, levels = levels, samples = samples, count = count)
                else
                    runtime.CreateTexture2D(size, fmt, levels = levels, samples = samples)

            try
                runtime.Upload(texture, level, slice, region.Min, data)
                let result = runtime.Download(texture, level, slice, region).AsPixImage<byte>()

                PixImage.compare V2i.Zero data result

            finally
                runtime.DeleteTexture(texture)

        let texture2D (runtime : IRuntime) =
            let size = V2i(100, 75)
            uploadAndDownloadTexture2D runtime size 1 1 1 0 0 None

        let texture2DSubwindow (runtime : IRuntime) =
            let size = V2i(100, 75)
            let region = Some <| Box2i(11, 34, 87, 66)
            uploadAndDownloadTexture2D runtime size 1 1 1 0 0 region

        let texture2DLevel (runtime : IRuntime) =
            let size = V2i(100, 75)
            uploadAndDownloadTexture2D runtime size 4 1 1 2 0 None

        let texture2DLevelSubwindow (runtime : IRuntime) =
            let size = V2i(100, 75)
            let region = Some <| Box2i(11, 25, 47, 29)
            uploadAndDownloadTexture2D runtime size 4 1 1 1 0 region

        let texture2DArray (runtime : IRuntime) =
            let size = V2i(100, 75)
            uploadAndDownloadTexture2D runtime size 1 5 1 0 1 None

        let texture2DArraySubwindow (runtime : IRuntime) =
            let size = V2i(100, 75)
            let region = Some <| Box2i(11, 34, 87, 66)
            uploadAndDownloadTexture2D runtime size 1 5 1 0 2 region

        let texture2DArrayLevel (runtime : IRuntime) =
            let size = V2i(100, 75)
            uploadAndDownloadTexture2D runtime size 4 5 1 2 3 None

        let texture2DArrayLevelSubwindow (runtime : IRuntime) =
            let size = V2i(100, 75)
            let region = Some <| Box2i(11, 25, 47, 29)
            uploadAndDownloadTexture2D runtime size 4 5 1 1 4 region

        let texture2DMultisampled (runtime : IRuntime) =
            let size = V2i(100, 75)
            uploadAndDownloadTexture2D runtime size 1 1 4 0 0 None

        let texture2DMultisampledSubwindow (runtime : IRuntime) =
            let size = V2i(100, 75)
            let region = Some <| Box2i(11, 34, 87, 66)
            uploadAndDownloadTexture2D runtime size 1 1 4 0 0 region

        let texture2DMultisampledArray (runtime : IRuntime) =
            let size = V2i(100, 75)
            uploadAndDownloadTexture2D runtime size 1 5 4 0 1 None

        let texture2DMultisampledArraySubwindow (runtime : IRuntime) =
            let size = V2i(100, 75)
            let region = Some <| Box2i(11, 34, 87, 66)
            uploadAndDownloadTexture2D runtime size 1 5 4 0 2 region

        let private uploadAndDownloadPixTexture2D (runtime : IRuntime) (size : V2i) (textureParams : TextureParams) =
             let data = PixImage.random size

             let pixTexture = PixTexture2d(PixImageMipMap [| data :> PixImage |], textureParams)
             let texture = runtime.PrepareTexture pixTexture

             try
                 let result = runtime.Download(texture).AsPixImage<byte>()
                 PixImage.compare V2i.Zero data result

             finally
                 runtime.DeleteTexture(texture)

        let pixTexture2D (runtime : IRuntime) =
            let size = V2i(256)
            uploadAndDownloadPixTexture2D runtime size TextureParams.empty

        let pixTexture2DSrgb (runtime : IRuntime) =
            let size = V2i(256)
            uploadAndDownloadPixTexture2D runtime size TextureParams.srgb

        let pixTexture2DMipmapped (runtime : IRuntime) =
            let size = V2i(258, 125)
            let levels = 1 + max size.X size.Y |> Fun.Log2Int

            let data =
                Array.init levels (fun i ->
                    let size = max 1 (size >>> i)
                    PixImage.random size
                )

            let texture =
                let data = data |> Array.map (fun pi -> pi :> PixImage)
                let pt = PixTexture2d(PixImageMipMap data, TextureParams.mipmapped)
                runtime.PrepareTexture(pt)

            try
                for i = 0 to levels - 1 do
                    let result = runtime.Download(texture, level = i).AsPixImage<byte>()

                    Expect.equal result.Size data.[i].Size "image size mismatch"
                    PixImage.compare V2i.Zero data.[i] result

            finally
                runtime.DeleteTexture(texture)


        let private uploadAndDownloadTexture3D (runtime : IRuntime)
                                               (size : V3i) (levels : int) (level : int)
                                               (window : Box3i option) =
            let region =
                match window with
                | Some r -> r
                | None ->
                    let s = max 1 (size >>> level)
                    Box3i(V3i.Zero, s)

            let data = PixVolume.random region.Size
            let fmt = TextureFormat.ofPixFormat data.PixFormat TextureParams.empty

            let texture =
                runtime.CreateTexture3D(size, fmt, levels)

            try
                let target = texture.[TextureAspect.Color, level, 0]
                runtime.Copy(data, target, region.Min, region.Size)

                let result = PixVolume<byte>(Col.Format.RGBA, region.Size)
                runtime.Copy(target, region.Min, result, region.Size)

                PixVolume.compare V3i.Zero data result

            finally
                runtime.DeleteTexture(texture)

        let texture3D (runtime : IRuntime) =
            let size = V3i(100, 75, 66)
            uploadAndDownloadTexture3D runtime size 1 0 None

        let texture3DSubwindow (runtime : IRuntime) =
            let size = V3i(100, 75, 66)
            let region = Some <| Box3i(11, 23, 17, 88, 67, 32)
            uploadAndDownloadTexture3D runtime size 1 0 region

        let texture3DLevel (runtime : IRuntime) =
            let size = V3i(100, 75, 66)
            uploadAndDownloadTexture3D runtime size 4 2 None

        let texture3DLevelSubwindow (runtime : IRuntime) =
            let size = V3i(100, 75, 66)
            let region = Some <| Box3i(13, 5, 7, 47, 30, 31)
            uploadAndDownloadTexture3D runtime size 4 1 region

        let private uploadAndDownloadPixTexture3D (runtime : IRuntime) (size : V3i) (textureParams : TextureParams) =
            let data = PixVolume.random size

            let pixTexture = PixTexture3d(data, textureParams)
            let texture = runtime.PrepareTexture pixTexture

            try
                let target = texture.[TextureAspect.Color, 0, 0]
                let result = PixVolume<byte>(Col.Format.RGBA, size)
                runtime.Copy(target, result)

                PixVolume.compare V3i.Zero data result

            finally
                runtime.DeleteTexture(texture)

        let pixTexture3D (runtime : IRuntime) =
            let size = V3i(64)
            uploadAndDownloadPixTexture3D runtime size TextureParams.empty

        let pixTexture3DSrgb (runtime : IRuntime) =
            let size = V3i(64)
            uploadAndDownloadPixTexture3D runtime size TextureParams.srgb


        let private uploadAndDownloadTextureCube (runtime : IRuntime)
                                                 (size : int) (levels : int) (count : int)
                                                 (level : int) (slice : int)
                                                 (window : Box2i option) =
            let region =
                match window with
                | Some r -> r
                | None ->
                    let s = max 1 (size >>> level)
                    Box2i(0, 0, s, s)

            let regionOffset = V3i(region.Min, 0)
            let regionSize = V3i(region.Size, 1)
            let data = PixVolume.random regionSize
            let fmt = TextureFormat.ofPixFormat data.PixFormat TextureParams.empty

            let texture =
                if count > 1 then
                    runtime.CreateTextureCubeArray(size, fmt, levels, count)
                else
                    runtime.CreateTextureCube(size, fmt, levels)


            try
                let target = texture.[TextureAspect.Color, level, slice]
                runtime.Copy(data, target, regionOffset, regionSize)

                let result = PixVolume<byte>(Col.Format.RGBA, regionSize)
                runtime.Copy(target, regionOffset, result, regionSize)

                PixVolume.compare V3i.Zero data result

            finally
                runtime.DeleteTexture(texture)

        let textureCube (runtime : IRuntime) =
            uploadAndDownloadTextureCube runtime 100 1 1 0 0 None

        let textureCubeSubwindow (runtime : IRuntime) =
            let region = Some <| Box2i(11, 45, 87, 58)
            uploadAndDownloadTextureCube runtime 100 1 1 0 0 region

        let textureCubeLevel (runtime : IRuntime) =
            uploadAndDownloadTextureCube runtime 100 4 1 2 0 None

        let textureCubeLevelSubwindow (runtime : IRuntime) =
            let region = Some <| Box2i(11, 45, 37, 47)
            uploadAndDownloadTextureCube runtime 100 4 1 1 0 region

        let textureCubeArray (runtime : IRuntime) =
            uploadAndDownloadTextureCube runtime 100 1 5 0 2 None

        let textureCubeArraySubwindow (runtime : IRuntime) =
            let region = Some <| Box2i(11, 45, 87, 58)
            uploadAndDownloadTextureCube runtime 100 1 5 0 2 region

        let textureCubeArrayLevel (runtime : IRuntime) =
            uploadAndDownloadTextureCube runtime 100 4 5 2 1 None

        let textureCubeArrayLevelSubwindow (runtime : IRuntime) =
            let region = Some <| Box2i(11, 45, 37, 47)
            uploadAndDownloadTextureCube runtime 100 4 5 1 3 region

        let private uploadAndDownloadPixTextureCube (runtime : IRuntime) (size : int) (textureParams : TextureParams) =
            let levels =
                if textureParams.wantMipMaps then 1 + Fun.Log2Int(size) else 1

            let data =
                Array.init 6 (fun _ ->
                    Array.init levels (fun level ->
                        let size = size >>> level
                        PixImage.random <| V2i size
                    )
                )

            let texture =
                let toMipMap arr =
                    PixImageMipMap(arr |> Array.map (fun pi -> pi :> PixImage))

                let pc = PixImageCube(data |> Array.map toMipMap)
                let pt = PixTextureCube(pc, textureParams)
                runtime.PrepareTexture(pt)

            try
                for slice = 0 to 5 do
                    for level = 0 to levels - 1 do
                        let result = runtime.Download(texture, level = level, slice = slice).AsPixImage<byte>()
                        Expect.equal result.Size data.[slice].[level].Size "image size mismatch"
                        PixImage.compare V2i.Zero data.[slice].[level] result

            finally
                runtime.DeleteTexture(texture)

        let pixTextureCube (runtime : IRuntime) =
            let size = 128
            uploadAndDownloadPixTextureCube runtime size TextureParams.empty

        let pixTextureCubeSrgb (runtime : IRuntime) =
            let size = 128
            uploadAndDownloadPixTextureCube runtime size TextureParams.srgb

        let pixTextureCubeMipmapped (runtime : IRuntime) =
            let size = 128
            uploadAndDownloadPixTextureCube runtime size TextureParams.mipmapped

    let tests (backend : Backend) =
        [
            "1D",                        Cases.texture1D
            "1D subwindow",              Cases.texture1DSubwindow
            "1D level",                  Cases.texture1DLevel
            "1D level subwindow",        Cases.texture1DLevelSubwindow

            "1D array",                  Cases.texture1DArray
            "1D array subwindow",        Cases.texture1DArraySubwindow
            "1D array level",            Cases.texture1DArrayLevel
            "1D array level subwindow",  Cases.texture1DArrayLevelSubwindow

            "2D",                        Cases.texture2D
            "2D subwindow",              Cases.texture2DSubwindow
            "2D level",                  Cases.texture2DLevel
            "2D level subwindow",        Cases.texture2DLevelSubwindow

            "2D array",                  Cases.texture2DArray
            "2D array subwindow",        Cases.texture2DArraySubwindow
            "2D array level",            Cases.texture2DArrayLevel
            "2D array level subwindow",  Cases.texture2DArrayLevelSubwindow

            "2D PixTexture",             Cases.pixTexture2D
            "2D PixTexture sRGB",        Cases.pixTexture2DSrgb
            "2D PixTexture mipmapped",   Cases.pixTexture2DMipmapped

            if backend <> Backend.Vulkan then // not supported
                "2D multisampled",                        Cases.texture2DMultisampled
                "2D multisampled subwindow",              Cases.texture2DMultisampledSubwindow
                "2D multisampled array",                  Cases.texture2DMultisampledArray
                "2D multisampled array subwindow",        Cases.texture2DMultisampledArraySubwindow

            "3D",                           Cases.texture3D
            "3D subwindow",                 Cases.texture3DSubwindow
            "3D level",                     Cases.texture3DLevel
            "3D level subwindow",           Cases.texture3DLevelSubwindow

            "3D PixTexture",                Cases.pixTexture3D
            "3D PixTexture sRGB",           Cases.pixTexture3DSrgb

            "Cube",                         Cases.textureCube
            "Cube subwindow",               Cases.textureCubeSubwindow
            "Cube level",                   Cases.textureCubeLevel
            "Cube level subwindow",         Cases.textureCubeLevelSubwindow

            "Cube array",                   Cases.textureCubeArray
            "Cube array subwindow",         Cases.textureCubeArraySubwindow
            "Cube array level",             Cases.textureCubeArrayLevel
            "Cube array level subwindow",   Cases.textureCubeArrayLevelSubwindow

            "Cube PixTexture",              Cases.pixTextureCube
            "Cube PixTexture sRGB",         Cases.pixTextureCubeSrgb
            "Cube PixTexture mipmapped",    Cases.pixTextureCubeMipmapped
        ]
        |> prepareCases backend "Upload"
