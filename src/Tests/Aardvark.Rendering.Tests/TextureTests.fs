namespace Aardvark.Rendering.Tests

open System
open System.IO
open System.Reflection
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open Aardvark.Application
open Aardvark.SceneGraph
open NUnit.Framework
open FsUnit

module Window =

    let create (testBackend : string) =
        let backend' =
            match testBackend with
            | "OpenGL" -> Backend.GL
            | "Vulkan" -> Backend.Vulkan
            | _ -> failwithf "Unknown backend '%s'" testBackend

        window {
            display Display.Mono
            samples 1
            backend backend'
            debug true
        }

module PixImage =

    let toTexture (wantMipMaps : bool) (img : #PixImage) =
        PixTexture2d(PixImageMipMap [| img :> PixImage |], wantMipMaps) :> ITexture

    let checkerboard (color : C4b) =
        let pi = PixImage<byte>(Col.Format.RGBA, V2i.II * 256)
        pi.GetMatrix<C4b>().SetByCoord(fun (c : V2l) ->
            let c = c / 16L
            if (c.X + c.Y) % 2L = 0L then
                C4b.White
            else
                color
        ) |> ignore
        pi

    let resized (size : V2i) (img : PixImage<byte>) =
        let result = PixImage<byte>(img.Format, size)

        for c in 0L .. img.Volume.Size.Z - 1L do
            let src = img.Volume.SubXYMatrixWindow(c)
            let dst = result.Volume.SubXYMatrixWindow(c)
            dst.SetScaledCubic(src)

        result

    let private desktopPath =
        Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop)

    let saveToDesktop (fileName : string) (img : #PixImage) =
        let dir = Path.combine [desktopPath; "UnitTests"]
        Directory.CreateDirectory(dir) |> ignore
        img.SaveAsImage(Path.combine [dir; fileName])


module ``Texture Tests`` =

    let colors = [|
            C4b.BurlyWood
            C4b.Crimson
            C4b.DarkOrange
            C4b.ForestGreen
            C4b.AliceBlue
            C4b.Indigo

            C4b.Cornsilk
            C4b.Cyan
            C4b.FireBrick
            C4b.Coral
            C4b.Chocolate
            C4b.LawnGreen
        |]

    let comparePixImages (offset : V2i) (input : PixImage<'T>) (output : PixImage<'T>) =
        for x in 0 .. output.Size.X - 1 do
            for y in 0 .. output.Size.Y - 1 do
                for c in 0 .. output.ChannelCount - 1 do
                    let inputData = input.GetChannel(int64 c)
                    let outputData = output.GetChannel(int64 c)

                    let coord = V2i(x, y)
                    let coordInput = coord - offset
                    let ref =
                        if Vec.allGreaterOrEqual coordInput V2i.Zero && Vec.allSmaller coordInput input.Size then
                            inputData.[coordInput]
                        else
                            Unchecked.defaultof<'T>

                    outputData.[x, y] |> should equal ref


    [<OneTimeSetUp>]
    let setEntryAssembly() =
        IntrospectionProperties.CustomEntryAssembly <- Assembly.GetAssembly(typeof<ISg>)
        Aardvark.Init()

    [<Test>]
    let ``[Download] Simple`` ([<Values("OpenGL", "Vulkan")>] backend : string) =
        use win = Window.create backend
        let runtime = win.Runtime

        let size = V2i(333, 666)
        let data = PixImage.checkerboard C4b.BurlyWood
        let fmt = TextureFormat.ofPixFormat data.PixFormat TextureParams.empty

        let t = runtime.CreateTexture2D(size, fmt, 1, 1)
        runtime.Upload(t, data)
        let result = runtime.Download(t).ToPixImage<byte>()
        runtime.DeleteTexture(t)

        result.Size |> should equal size
        comparePixImages V2i.Zero data result


    [<Test>]
    let ``[Download] Multisampled`` ([<Values("OpenGL", "Vulkan")>] backend : string) =
        use win = Window.create backend
        let runtime = win.Runtime

        let data = PixImage.checkerboard C4b.BurlyWood
        let size = data.Size
        let samples = 8

        let signature = runtime.CreateFramebufferSignature(samples, [DefaultSemantic.Colors, RenderbufferFormat.Rgba32f])
        let colorTexture = runtime.CreateTexture2D(size, TextureFormat.Rgba32f, 1, samples)
        let framebuffer = runtime.CreateFramebuffer(signature, [DefaultSemantic.Colors, colorTexture.GetOutputView()])

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
        let result = runtime.Download(colorTexture).ToPixImage<byte>()

        runtime.DeleteFramebuffer(framebuffer)
        runtime.DeleteTexture(colorTexture)
        runtime.DeleteFramebufferSignature(signature)

        result.Size |> should equal size
        comparePixImages V2i.Zero data result


    [<Test>]
    let ``[Download] Mipmapped array`` ([<Values("OpenGL", "Vulkan")>] backend : string) =
        use win = Window.create backend
        let runtime = win.Runtime

        let count = 4
        let levels = 3
        let size = V2i(128)

        let data =
            Array.init count (fun index ->
                let data = PixImage.checkerboard colors.[index]

                Array.init levels (fun level ->
                    let size = size / (1 <<< level)
                    data |> PixImage.resized size
                )
            )

        let format = TextureFormat.ofPixFormat data.[0].[0].PixFormat TextureParams.empty
        let t = runtime.CreateTexture2DArray(size, format, levels = levels, count = count)

        data |> Array.iteri (fun index mipmaps ->
            mipmaps |> Array.iteri (fun level img ->
                runtime.Upload(t, level, index, img)
            )
        )

        let result =
            data |> Array.mapi (fun index mipmaps ->
                mipmaps |> Array.mapi (fun level _ ->
                    runtime.Download(t, level, index).ToPixImage<byte>()
                )
            )

        runtime.DeleteTexture(t)

        (data, result) ||> Array.iter2 (Array.iter2 (fun src dst ->
                dst.Size |> should equal src.Size
                comparePixImages V2i.Zero src dst
            )
        )


    [<Test>]
    let ``[Download] Mipmapped cube`` ([<Values("OpenGL", "Vulkan")>] backend : string) =
        use win = Window.create backend
        let runtime = win.Runtime

        let levels = 3
        let size = V2i(128)

        let data =
            CubeMap.init levels (fun side level ->
                let data = PixImage.checkerboard colors.[int side]
                let size = size / (1 <<< level)
                data |> PixImage.resized size
            )

        let format = TextureFormat.ofPixFormat data.[CubeSide.PositiveX].PixFormat TextureParams.empty
        let t = runtime.CreateTextureCube(size.X, format, levels = levels)

        data |> CubeMap.iteri (fun side level img ->
            runtime.Upload(t, level, int side, img)
        )

        let result =
            data |> CubeMap.mapi (fun side level _ ->
                runtime.Download(t, level, int side).ToPixImage<byte>()
            )

        (data, result) ||> CubeMap.iteri2 (fun side level src dst ->
                dst.Size |> should equal src.Size
                comparePixImages V2i.Zero src dst
            )

        runtime.DeleteTexture(t)


    [<Test>]
    let ``[Download] Mipmapped cube array`` ([<Values("OpenGL", "Vulkan")>] backend : string) =
        use win = Window.create backend
        let runtime = win.Runtime

        let count = 2
        let levels = 3
        let size = V2i(128)

        let data =
            Array.init count (fun index ->
                CubeMap.init levels (fun side level ->
                    let data = PixImage.checkerboard colors.[index * 6 + int side]
                    let size = size / (1 <<< level)
                    data |> PixImage.resized size
                )
            )

        let format = TextureFormat.ofPixFormat data.[0].[CubeSide.PositiveX].PixFormat TextureParams.empty
        let t = runtime.CreateTextureCubeArray(size.X, format, levels = levels, count = count)

        data |> Array.iteri (fun index mipmaps ->
            mipmaps |> CubeMap.iteri (fun side level img ->
                runtime.Upload(t, level, index * 6 + int side, img)
            )
        )

        let result =
            data |> Array.mapi (fun index mipmaps ->
                mipmaps |> CubeMap.mapi (fun side level _ ->
                    let slice = index * 6 + int side
                    runtime.Download(t, level, slice).ToPixImage<byte>()
                )
            )

        (data, result) ||> Array.iter2 (CubeMap.iter2 (fun src dst ->
                dst.Size |> should equal src.Size
                comparePixImages V2i.Zero src dst
            )
        )

        runtime.DeleteTexture(t)


    [<Test>]
    let ``[Create] Non-positive arguments`` ([<Values("OpenGL", "Vulkan")>] backend : string) =
        use win = Window.create backend
        let runtime = win.Runtime

        let create (size : V3i) (dimension : TextureDimension) (levels : int) (samples : int) () =
            let t = runtime.CreateTexture(size, dimension, TextureFormat.Rgba32f, levels, samples)
            runtime.DeleteTexture(t)

        let createArray (size : V3i) (dimension : TextureDimension) (levels : int) (samples : int) (count : int) () =
            let t = runtime.CreateTextureArray(size, dimension, TextureFormat.Rgba32f, levels, samples, count)
            runtime.DeleteTexture(t)

        let size = V3i(128)
        create size TextureDimension.Texture2D  0  2 |> should throw typeof<ArgumentException>
        create size TextureDimension.Texture2D  2  0 |> should throw typeof<ArgumentException>
        create size TextureDimension.Texture2D -1  2 |> should throw typeof<ArgumentException>
        create size TextureDimension.Texture2D  2 -4 |> should throw typeof<ArgumentException>
        create (size * -1) TextureDimension.Texture2D  1  1 |> should throw typeof<ArgumentException>

        createArray size TextureDimension.Texture2D  3  0  1 |> should throw typeof<ArgumentException>
        createArray size TextureDimension.Texture2D  3 -3  1 |> should throw typeof<ArgumentException>
        createArray size TextureDimension.Texture2D  3  1  0 |> should throw typeof<ArgumentException>
        createArray size TextureDimension.Texture2D  3  1 -1 |> should throw typeof<ArgumentException>
        createArray size TextureDimension.Texture2D  0  1  1 |> should throw typeof<ArgumentException>
        createArray size TextureDimension.Texture2D -4  1  1 |> should throw typeof<ArgumentException>
        createArray (size * -1) TextureDimension.Texture2D 1  1  2 |> should throw typeof<ArgumentException>


    [<Test>]
    let ``[Create] Invalid usage`` ([<Values("OpenGL", "Vulkan")>] backend : string) =
        use win = Window.create backend
        let runtime = win.Runtime

        let create (dimension : TextureDimension) (levels : int) (samples : int) () =
            let t = runtime.CreateTexture(V3i(128), dimension, TextureFormat.Rgba32f, levels, samples)
            runtime.DeleteTexture(t)

        let createArray (dimension : TextureDimension) (levels : int) (samples : int) (count : int) () =
            let t = runtime.CreateTextureArray(V3i(128), dimension, TextureFormat.Rgba32f, levels, samples, count)
            runtime.DeleteTexture(t)

        create TextureDimension.Texture1D 1 8          |> should throw typeof<ArgumentException>
        createArray TextureDimension.Texture1D 1 8 4   |> should throw typeof<ArgumentException>

        create TextureDimension.Texture2D 2 8          |> should throw typeof<ArgumentException>
        createArray TextureDimension.Texture2D 2 8 4   |> should throw typeof<ArgumentException>

        create TextureDimension.Texture3D 1 4          |> should throw typeof<ArgumentException>
        createArray TextureDimension.Texture3D 1 1 4   |> should throw typeof<ArgumentException>

        create TextureDimension.TextureCube 1 8        |> should throw typeof<ArgumentException>
        createArray TextureDimension.TextureCube 1 8 4 |> should throw typeof<ArgumentException>


    [<Test>]
    let ``[Create] Valid usage`` ([<Values("OpenGL", "Vulkan")>] backend : string) =
        use win = Window.create backend
        let runtime = win.Runtime

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


    // TODO: Implement all the checks and raise the appropriate expceptions
    //[<Test>]
    //let ``[Download] Arguments out of range`` ([<Values("OpenGL", "Vulkan")>] backend : string) =
    //    use win = Window.create backend
    //    let runtime = win.Runtime

    //    let t = runtime.CreateTextureArray(V2i(512, 512), TextureFormat.Rgba32f, 4, 8, 16)
    //    (fun _() -> runtime.Download(t, -1, 0) |> ignore) |> should throw typeof<ArgumentException>

    //    runtime.DeleteTexture(t)