namespace Aardvark.Rendering.Tests

open System
open System.IO
open System.Reflection
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open Aardvark.Application
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open NUnit.Framework
open FsUnit

module Window =

    let create (testBackend : string) =
        let backend' =
            match testBackend with
            | "OpenGL" -> Backend.GL
            | "Vulkan" -> Backend.Vulkan
            | _ -> failwithf "Unknown backend '%s'" testBackend

        GL.Config.CheckErrors <- true

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

    type UnexpectedPassException() =
        inherit Exception("Expected an exception but none occurred")

    let shouldThrowArgExn (message : string) (f : unit -> unit) =
        try
            f()
            raise <| UnexpectedPassException()
        with
        | :? ArgumentException as exn -> exn.Message |> should haveSubstring message
        | :? UnexpectedPassException -> failwith "Expected ArgumentException but got none"
        | exn -> failwithf "Expected ArgumentException but got %A" <| exn.GetType()

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
    let ``[Download] Subwindow`` ([<Values("OpenGL", "Vulkan")>] backend : string) =
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

        let side = CubeSide.NegativeZ
        let index = 1
        let level = 2
        let region = Box2i.FromMinAndSize(V2i(14, 18), V2i(10, 3))
        let result = runtime.Download(t, level, index * 6 + int side, region).ToPixImage<byte>()

        let reference = data.[index].[side, level].SubImage(region)
        result.Size |> should equal reference.Size
        comparePixImages V2i.Zero reference result

        runtime.DeleteTexture(t)


    [<Test>]
    let ``[Download] Depth and stencil`` ([<Values("OpenGL")>] backend : string) =
        use win = Window.create backend
        let runtime = win.Runtime

        let signature =
            runtime.CreateFramebufferSignature([
                DefaultSemantic.Depth, RenderbufferFormat.Depth24Stencil8
            ])

        use task =
            let drawCall =
                DrawCallInfo(
                    FaceVertexCount = 4,
                    InstanceCount = 1
                )

            let positions = [| V3f(-0.5,-0.5,0.0); V3f(0.5,-0.5,0.0); V3f(-0.5,0.5,0.0); V3f(0.5,0.5,0.0) |]

            let stencilMode =
                { StencilMode.None with
                    Pass = StencilOperation.Replace;
                    Reference = 3 }

            drawCall
            |> Sg.render IndexedGeometryMode.TriangleStrip
            |> Sg.vertexAttribute DefaultSemantic.Positions (AVal.constant positions)
            |> Sg.stencilMode' stencilMode
            |> Sg.shader {
                do! DefaultSurfaces.trafo
            }
            |> Sg.compile runtime signature


        let size = V2i(256)

        let depthStencilBuffer =
            let clear = clear { depth 1.0; stencil 0 }
            task |> RenderTask.renderToDepthWithClear (AVal.constant size) clear

        depthStencilBuffer.Acquire()

        let depthResult = runtime.DownloadDepth(depthStencilBuffer.GetValue())
        let stencilResult = runtime.DownloadStencil(depthStencilBuffer.GetValue())

        depthResult.Size |> V2i |> should equal size
        depthResult.Data |> Array.min |> should greaterThan 0.0
        depthResult.Data |> Array.min |> should lessThan 1.0

        stencilResult.Size |> V2i |> should equal size
        stencilResult.Data |> Array.max |> should equal 3

        depthStencilBuffer.Release()

        runtime.DeleteFramebufferSignature(signature)


    [<Test>]
    let ``[Download] Arguments out of range`` ([<Values("OpenGL", "Vulkan")>] backend : string) =
        use win = Window.create backend
        let runtime = win.Runtime

        let createAndDownload (dimension : TextureDimension) (levels : int) (level : int) (slice : int) (region : Box2i) () =
            let t = runtime.CreateTexture(V3i(128), dimension, TextureFormat.Rgba32f, levels, 1)
            try
                runtime.Download(t, level, slice, region) |> ignore
            finally
                runtime.DeleteTexture(t)

        let full = Box2i.Infinite
        let window m s = Box2i.FromMinAndSize(m, s)
        let neg = window V2i.NN V2i.One

        createAndDownload TextureDimension.Texture2D  4 -1  0  full   |> shouldThrowArgExn "level cannot be negative"
        createAndDownload TextureDimension.Texture2D  4  4  0  full   |> shouldThrowArgExn "cannot access texture level"
        createAndDownload TextureDimension.Texture2D  4  2  0  neg    |> shouldThrowArgExn "offset cannot be negative"
        createAndDownload TextureDimension.Texture2D  4  2  0  (window (V2i(8)) (V2i(25, 1))) |> shouldThrowArgExn "exceeds size"

        createAndDownload TextureDimension.TextureCube  4 -1  2  full |> shouldThrowArgExn "level cannot be negative"
        createAndDownload TextureDimension.TextureCube  4  4  2  full |> shouldThrowArgExn "cannot access texture level"
        createAndDownload TextureDimension.TextureCube  4  2 -1  full |> shouldThrowArgExn "slice cannot be negative"
        createAndDownload TextureDimension.TextureCube  4  2  6  full |> shouldThrowArgExn "cannot access texture slice"
        createAndDownload TextureDimension.TextureCube  4  2  3  neg    |> shouldThrowArgExn "offset cannot be negative"
        createAndDownload TextureDimension.TextureCube  4  2  3  (window (V2i(8)) (V2i(25, 1))) |> shouldThrowArgExn "exceeds size"


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

    [<Test>]
    let ``[Copy] Arguments out of range`` ([<Values("OpenGL", "Vulkan")>] backend : string) =
        use win = Window.create backend
        let runtime = win.Runtime

        let copy src srcSlice srcLevel dst dstSlice dstLevel slices levels () =
            runtime.Copy(src, srcSlice, srcLevel, dst, dstSlice, dstLevel, slices, levels)

        let size = V2i(333, 666)
        let src = runtime.CreateTexture2DArray(size, TextureFormat.Rgba8, levels = 4, count = 3)
        let dst = runtime.CreateTexture2DArray(size, TextureFormat.Rgba8, levels = 3, count = 5)
        let ms = runtime.CreateTexture2D(size, TextureFormat.Rgba8, levels = 1, samples = 8)

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

        runtime.DeleteTexture(src)
        runtime.DeleteTexture(dst)
        runtime.DeleteTexture(ms)


    [<Test>]
    let ``[Copy] Simple`` ([<Values("OpenGL", "Vulkan")>] backend : string) =
        use win = Window.create backend
        let runtime = win.Runtime

        let size = V2i(333, 666)
        let levels = 4
        let count = 5

        let data =
            Array.init count (fun index ->
                let data = PixImage.checkerboard colors.[index]

                Array.init levels (fun level ->
                    let size = size / (1 <<< level)
                    data |> PixImage.resized size
                )
            )

        let format = TextureFormat.ofPixFormat data.[0].[0].PixFormat TextureParams.empty
        let src = runtime.CreateTexture2DArray(size, format, levels = levels, count = count)
        let dst = runtime.CreateTexture2DArray(size, format, levels = levels, count = count)

        data |> Array.iteri (fun index mipmaps ->
            mipmaps |> Array.iteri (fun level img ->
                runtime.Upload(src, level, index, img)
            )
        )

        runtime.Copy(src, 2, 1, dst, 2, 1, 3, 3)

        for i in 2 .. 4 do
            for l in 1 .. 3 do
                let result = runtime.Download(dst, level = l, slice = i).ToPixImage<byte>()
                let levelSize = size / (1 <<< l)

                result.Size |> should equal levelSize
                comparePixImages V2i.Zero data.[i].[l] result

        runtime.DeleteTexture(src)
        runtime.DeleteTexture(dst)


    [<Test>]
    let ``[Copy] Multisampled`` ([<Values("OpenGL", "Vulkan")>] backend : string, [<Values(false, true)>] resolve : bool) =
        use win = Window.create backend
        let runtime = win.Runtime

        let data = PixImage.checkerboard C4b.BurlyWood
        let size = data.Size
        let samples = 8

        let signature = runtime.CreateFramebufferSignature(samples, [DefaultSemantic.Colors, RenderbufferFormat.Rgba8])
        let src = runtime.CreateTexture2D(size, TextureFormat.Rgba8, levels = 1, samples = samples)
        let dst = runtime.CreateTexture2D(size, TextureFormat.Rgba8, levels = 1, samples = if resolve then 1 else samples)
        let framebuffer = runtime.CreateFramebuffer(signature, [DefaultSemantic.Colors, src.GetOutputView()])

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

        runtime.Copy(src, 0, 0, dst, 0, 0, 1, 1)
        let result = runtime.Download(dst).ToPixImage<byte>()

        runtime.DeleteFramebuffer(framebuffer)
        runtime.DeleteTexture(src)
        runtime.DeleteTexture(dst)
        runtime.DeleteFramebufferSignature(signature)

        result.Size |> should equal size
        comparePixImages V2i.Zero data result


    [<Test>]
    let ``[Copy] Mipmapped cube`` ([<Values("OpenGL", "Vulkan")>] backend : string) =
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
        let src = runtime.CreateTextureCube(size.X, format, levels = levels)
        let dst = runtime.CreateTextureCube(size.X, format, levels = levels)

        data |> CubeMap.iteri (fun side level img ->
            runtime.Upload(src, level, int side, img)
        )

        runtime.Copy(src, 3, 1, dst, 3, 1, 3, 2)

        for slice in 3 .. 5 do
            for level in 1 .. 2 do
                let result = runtime.Download(dst, level = level, slice = slice).ToPixImage<byte>()
                let levelSize = size / (1 <<< level)
                let side = unbox<CubeSide> (slice % 6)

                result.Size |> should equal levelSize
                comparePixImages V2i.Zero data.[side, level] result

        runtime.DeleteTexture(src)
        runtime.DeleteTexture(dst)


    [<Test>]
    let ``[Copy] Mipmapped cube array`` ([<Values("OpenGL", "Vulkan")>] backend : string) =
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
        let src = runtime.CreateTextureCubeArray(size.X, format, levels = levels, count = count)
        let dst = runtime.CreateTextureCubeArray(size.X, format, levels = levels, count = count)

        data |> Array.iteri (fun index mipmaps ->
            mipmaps |> CubeMap.iteri (fun side level img ->
                runtime.Upload(src, level, index * 6 + int side, img)
            )
        )

        runtime.Copy(src, 2, 1, dst, 2, 1, 7, 2)

        for slice in 2 .. 8 do
            for level in 1 .. 2 do
                let result = runtime.Download(dst, level = level, slice = slice).ToPixImage<byte>()
                let levelSize = size / (1 <<< level)
                let index = slice / 6
                let side = unbox<CubeSide> (slice % 6)

                result.Size |> should equal levelSize
                comparePixImages V2i.Zero data.[index].[side, level] result

        runtime.DeleteTexture(src)
        runtime.DeleteTexture(dst)