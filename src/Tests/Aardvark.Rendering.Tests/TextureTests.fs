namespace Aardvark.Rendering.Tests

open System.Reflection
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Application
open Aardvark.SceneGraph
open NUnit.Framework

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
    open System

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

                    Assert.AreEqual(outputData.[x, y], ref)


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

        let t = runtime.CreateTexture(size, fmt, 1, 1)
        runtime.Upload(t, data)
        let result = runtime.Download(t).ToPixImage<byte>()
        runtime.DeleteTexture(t)

        Assert.AreEqual(size, result.Size)
        comparePixImages V2i.Zero data result

    [<Test>]
    let ``[Download] Multisampled`` ([<Values("OpenGL", "Vulkan")>] backend : string) =
        use win = Window.create backend
        let runtime = win.Runtime

        let data = PixImage.checkerboard C4b.BurlyWood
        let size = data.Size
        let samples = 8

        let signature = runtime.CreateFramebufferSignature(samples, [DefaultSemantic.Colors, RenderbufferFormat.Rgba32f])
        let colorTexture = runtime.CreateTexture(size, TextureFormat.Rgba32f, 1, samples)
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

        Assert.AreEqual(size, result.Size)
        comparePixImages V2i.Zero data result