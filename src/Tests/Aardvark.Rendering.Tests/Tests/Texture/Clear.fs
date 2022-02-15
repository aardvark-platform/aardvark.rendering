namespace Aardvark.Rendering.Tests.Texture

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Aardvark.Application
open Expecto

module TextureClear =

    module Cases =

        let inline createAndClearColor (runtime : IRuntime) (format : TextureFormat) (color : ^T) =
            let t = runtime.CreateTexture2D(V2i(256), format)

            try
                runtime.Clear(t, color)
                t.Download()
            finally
                runtime.DeleteTexture(t)

        let rgba8 (runtime : IRuntime) =
            let data = createAndClearColor runtime TextureFormat.Rgba8 <| C3f(0.5)
            data.AsPixImage<uint8>() |> PixImage.isColor (C4b(127uy).ToArray())

        let rgba32i (runtime : IRuntime) =
            let data = createAndClearColor runtime TextureFormat.Rgba32i <| V4i(-1)
            data.AsPixImage<int>() |> PixImage.isColor (V4i(-1).ToArray())

        let rgba32ui (runtime : IRuntime) =
            let data = createAndClearColor runtime TextureFormat.Rgba32ui <| V4i(-1)
            data.AsPixImage<uint>() |> PixImage.isColor [| UInt32.MaxValue; UInt32.MaxValue; UInt32.MaxValue; UInt32.MaxValue |]

        let rgba16ui (runtime : IRuntime) =
            let data = createAndClearColor runtime TextureFormat.Rgba16ui <| V4i(-1)
            data.AsPixImage<uint16>() |> PixImage.isColor [| UInt16.MaxValue; UInt16.MaxValue; UInt16.MaxValue; UInt16.MaxValue |]

        let rgba32f (runtime : IRuntime) =
            let data = createAndClearColor runtime TextureFormat.Rgba32f <| V4i(-1)
            data.AsPixImage<float32>() |> PixImage.isColor (V4f(-1).ToArray())

        let depthStencil (runtime : IRuntime) =
            let t = runtime.CreateTexture2D(V2i(256), TextureFormat.Depth24Stencil8)

            try
                runtime.ClearDepthStencil(t, 0.5, 3)
                let depth, stencil = t.DownloadDepth(), t.DownloadStencil()

                depth.Data |> Array.iter (fun x -> Expect.floatClose Accuracy.medium (float x) 0.5 "Depth data mismatch")
                stencil.Data |> Array.iter (fun x -> Expect.equal x 3 "Stencil data mismatch")
            finally
                runtime.DeleteTexture(t)

        let createAndClearFramebuffer (runtime : IRuntime) (clearFramebuffer : IFramebuffer -> ClearValues -> unit) =
            let c0 = Sym.ofString "c0"
            let c1 = Sym.ofString "c1"
            let c2 = Sym.ofString "c2"
            let c3 = Sym.ofString "c3"
            let c4 = Sym.ofString "c4"

            let formats =
                Map.ofList [
                    c0, TextureFormat.Rgba32f
                    c1, TextureFormat.Rgba32i
                    c2, TextureFormat.Rgba32ui
                    c3, TextureFormat.Rgba8
                    c4, TextureFormat.Rgba16i
                    DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
                ]

            let signature =
                runtime.CreateFramebufferSignature(formats)

            let textures =
                formats |> Map.map (fun _ fmt -> runtime.CreateTexture2D(V2i(256), fmt))

            let fbo =
                let attachments = textures |> Map.map (fun _ tex -> tex.GetOutputView())
                runtime.CreateFramebuffer(signature, attachments)

            try
                let clear =
                    clear {
                        color C3b.AliceBlue
                        color (c0, C3us.Aquamarine)
                        color (c1, V4i(-1))
                        color (c3, C3f(0.5))
                        depth 0.5
                        stencil 4
                    }

                clearFramebuffer fbo clear

                do
                    let c = C3us.Aquamarine |> C4f
                    let pi = textures.[c0].Download().AsPixImage<float32>()
                    pi |> PixImage.isColor (c.ToArray())

                do
                    let pi = textures.[c1].Download().AsPixImage<int>()
                    pi |> PixImage.isColor (V4i(-1).ToArray())

                do
                    let c = C3b.AliceBlue |> V4i
                    let pi = textures.[c2].Download().AsPixImage<uint>()
                    pi |> PixImage.isColor (c.ToArray() |> Array.map uint32)

                do
                    let c = C4b(127uy)
                    let pi = textures.[c3].Download().AsPixImage<uint8>()
                    pi |> PixImage.isColor (c.ToArray())

                do
                    let c = C3b.AliceBlue |> V4i
                    let pi = textures.[c4].Download().AsPixImage<int16>()
                    pi |> PixImage.isColor (c.ToArray() |> Array.map int16)

                do
                    let depth = textures.[DefaultSemantic.DepthStencil].DownloadDepth()
                    depth.Data |> Array.iter (fun x -> Expect.floatClose Accuracy.medium (float x) 0.5 "Depth data mismatch")

                do
                    let stencil = textures.[DefaultSemantic.DepthStencil].DownloadStencil()
                    stencil.Data |> Array.iter (fun x -> Expect.equal x 4 "Stencil data mismatch")

            finally
                fbo.Dispose()
                signature.Dispose()
                textures |> Map.iter (fun _ t -> runtime.DeleteTexture t)


        let framebuffer (runtime : IRuntime) =
            createAndClearFramebuffer runtime (fun fbo clear ->
                runtime.Clear(fbo, clear)
            )

        let framebufferCompileClear (runtime : IRuntime) =
            createAndClearFramebuffer runtime (fun fbo clear ->
                use task = runtime.CompileClear(fbo.Signature, clear)
                task.Run(RenderToken.Empty, fbo)
            )

        let framebufferRenderCommand (runtime : IRuntime) =
            createAndClearFramebuffer runtime (fun fbo clear ->
                use task =
                    Sg.execute (RenderCommand.Clear clear)
                    |> Sg.compile runtime fbo.Signature

                task.Run(RenderToken.Empty, fbo)
            )

    let tests (backend : Backend) =
        [
            if backend <> Backend.Vulkan then
                "Color rgba8",                  Cases.rgba8
                "Color rgba32i",                Cases.rgba32i
                "Color rgba16ui",               Cases.rgba16ui
                "Color rgba32ui",               Cases.rgba32ui
                "Color rgba32f",                Cases.rgba32f
                "Depth-stencil",                Cases.depthStencil
                "Framebuffer",                  Cases.framebuffer

            "Framebuffer (compiled)",       Cases.framebufferCompileClear
            "Framebuffer (render command)", Cases.framebufferRenderCommand
        ]
        |> prepareCases backend "Clear"