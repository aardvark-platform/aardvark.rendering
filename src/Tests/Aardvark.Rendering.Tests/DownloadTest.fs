namespace Aardvark.Rendering.Tests

open System.Reflection
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Application
open Aardvark.SceneGraph
open NUnit.Framework

module ``Download Tests`` =

    do IntrospectionProperties.CustomEntryAssembly <- Assembly.GetAssembly(typeof<ISg>)
    do Aardvark.Init()

    let createWindow (testBackend : string) =
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

    let comparePixImages (offset : V2i) (input : PixImage<'T>) (output : PixImage<'T>) =
        for x in 0 .. output.Size.X - 1 do
            for y in 0 .. output.Size.Y - 1 do
                for c in 0 .. output.ChannelCount - 1 do
                    let coord = V2i(x, y)
                    let coordInput = coord - offset
                    let ref =
                        if Vec.allGreaterOrEqual coordInput V2i.Zero && Vec.allSmaller coordInput input.Size then
                            input.Volume.[V3i(coordInput, c)]
                        else
                            Unchecked.defaultof<'T>

                    Assert.AreEqual(output.Volume.[x, y, c], ref)

    let simpleUploadAndDownloadTest (runtime : IRuntime) (size : V2i) (samples : int) (data : PixImage<'T>) =
        let fmt = TextureFormat.ofPixFormat data.PixFormat TextureParams.empty

        let t = runtime.CreateTexture(size, fmt, 1, samples)
        runtime.Upload(t, data)
        let result = runtime.Download(t).ToPixImage<'T>()
        runtime.DeleteTexture(t)

        Assert.AreEqual(size, result.Size)
        comparePixImages V2i.Zero data result


    [<Test>]
    let ``Simple Texture`` ([<Values("OpenGL", "Vulkan")>] backend : string) =
        use win = createWindow backend
        let runtime = win.Runtime
        simpleUploadAndDownloadTest runtime (V2i(333, 666)) 1 DefaultTextures.checkerboardPix

    [<Test>]
    let ``Multisampled Texture`` ([<Values("OpenGL", "Vulkan")>] backend : string) =
        use win = createWindow backend
        let runtime = win.Runtime
        simpleUploadAndDownloadTest runtime (V2i(333, 666)) 8 DefaultTextures.checkerboardPix