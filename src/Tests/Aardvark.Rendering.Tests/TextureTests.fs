namespace Aardvark.Rendering.Tests

open System
open System.Reflection
open Aardvark.Base
open Aardvark.Rendering
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

    let private desktopPath =
        Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop)

    let saveToDesktop (fileName : string) (img : #PixImage) =
        img.SaveAsImage(Path.combine [desktopPath; fileName])


module ``Texture Tests`` =

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


    // TODO: GL backend considers layers > 0 as arrayed? Totally sane...
    [<Test>]
    let ``[Create] 3D textures`` ([<Values("OpenGL", "Vulkan")>] backend : string) =
        use win = Window.create backend
        let runtime = win.Runtime

        let create() =
            let t = runtime.CreateTexture(V3i(128), TextureDimension.Texture3D, TextureFormat.Rgba32f, 1, 1)
            runtime.DeleteTexture(t)

        let createMultisampled() =
            let t = runtime.CreateTexture(V3i(128), TextureDimension.Texture3D, TextureFormat.Rgba32f, 1, 8)
            runtime.DeleteTexture(t)

        let createMipmapped() =
            let t = runtime.CreateTexture(V3i(128), TextureDimension.Texture3D, TextureFormat.Rgba32f, 4, 1)
            runtime.DeleteTexture(t)

        let createArray() =
            let t = runtime.CreateTextureArray(V3i(128), TextureDimension.Texture3D, TextureFormat.Rgba32f, 1, 1, 12)
            runtime.DeleteTexture(t)

        create()
        createMultisampled()
        createMipmapped |> should throw typeof<ArgumentException>
        createArray     |> should throw typeof<ArgumentException>


    // TODO: Implement all the checks and raise the appropriate expceptions
    [<Test>]
    let ``[Create] Non-positive arguments`` ([<Values("OpenGL", "Vulkan")>] backend : string) =
        use win = Window.create backend
        let runtime = win.Runtime

        let create (levels : int) (samples : int) () =
            let t = runtime.CreateTexture2D(V2i(512, 512), TextureFormat.Rgba32f, levels, samples)
            runtime.DeleteTexture(t)

        let create3d (levels : int) () =
            let t = runtime.CreateTexture3D(V3i(128), TextureFormat.Rgba32f, levels)
            runtime.DeleteTexture(t)

        let createArray (slices : int) (levels : int) (samples : int) () =
            let t = runtime.CreateTexture2DArray(V2i(512, 512), TextureFormat.Rgba32f, slices, levels, samples)
            runtime.DeleteTexture(t)

        let createCube (levels : int) (samples : int) () =
            let t = runtime.CreateTextureCube(512, TextureFormat.Rgba32f, levels, samples)
            runtime.DeleteTexture(t)

        let createCubeArray (slices : int) (levels : int) (samples : int) () =
            let t = runtime.CreateTextureCubeArray(512, TextureFormat.Rgba32f, slices, levels, samples)
            runtime.DeleteTexture(t)

        create  0  1 |> should throw typeof<ArgumentException>
        create -3  1 |> should throw typeof<ArgumentException>
        create  1  0 |> should throw typeof<ArgumentException>
        create  1 -1 |> should throw typeof<ArgumentException>

        createArray  8  0  1 |> should throw typeof<ArgumentException>
        createArray  8 -3  1 |> should throw typeof<ArgumentException>
        createArray  8  1  0 |> should throw typeof<ArgumentException>
        createArray  8  1 -1 |> should throw typeof<ArgumentException>
        createArray  0  1  1 |> should throw typeof<ArgumentException>
        createArray -4  1  1 |> should throw typeof<ArgumentException>

        create3d  0 |> should throw typeof<ArgumentException>
        create3d -1 |> should throw typeof<ArgumentException>

        createCube  0  1 |> should throw typeof<ArgumentException>
        createCube -3  1 |> should throw typeof<ArgumentException>
        createCube  1  0 |> should throw typeof<ArgumentException>
        createCube  1 -1 |> should throw typeof<ArgumentException>

        createCubeArray  8  0  1 |> should throw typeof<ArgumentException>
        createCubeArray  8 -3  1 |> should throw typeof<ArgumentException>
        createCubeArray  8  1  0 |> should throw typeof<ArgumentException>
        createCubeArray  8  1 -1 |> should throw typeof<ArgumentException>
        createCubeArray  0  1  1 |> should throw typeof<ArgumentException>
        createCubeArray -4  1  1 |> should throw typeof<ArgumentException>


    // TODO: Implement all the checks and raise the appropriate expceptions
    [<Test>]
    let ``[Create] Multisampled textures cannot have mip maps`` ([<Values("OpenGL", "Vulkan")>] backend : string) =
        use win = Window.create backend
        let runtime = win.Runtime

        let create() =
            let t = runtime.CreateTexture2D(V2i(512, 512), TextureFormat.Rgba32f, 2, 2)
            runtime.DeleteTexture(t)

        let createArray() =
            let t = runtime.CreateTexture2DArray(V2i(512, 512), TextureFormat.Rgba32f, 4, 8, 16)
            runtime.DeleteTexture(t)

        let createCube() =
            let t = runtime.CreateTextureCube(512, TextureFormat.Rgba32f, 2, 2)
            runtime.DeleteTexture(t)

        let createCubeArray() =
            let t = runtime.CreateTextureCubeArray(512, TextureFormat.Rgba32f, 4, 8, 16)
            runtime.DeleteTexture(t)

        create          |> should throw typeof<ArgumentException>
        createArray     |> should throw typeof<ArgumentException>
        createCube      |> should throw typeof<ArgumentException>
        createCubeArray |> should throw typeof<ArgumentException>


    // TODO: Implement all the checks and raise the appropriate expceptions
    //[<Test>]
    //let ``[Download] Arguments out of range`` ([<Values("OpenGL", "Vulkan")>] backend : string) =
    //    use win = Window.create backend
    //    let runtime = win.Runtime

    //    let t = runtime.CreateTextureArray(V2i(512, 512), TextureFormat.Rgba32f, 4, 8, 16)
    //    (fun _() -> runtime.Download(t, -1, 0) |> ignore) |> should throw typeof<ArgumentException>

    //    runtime.DeleteTexture(t)