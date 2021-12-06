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

        let createAndClearColor (runtime : IRuntime) (format : TextureFormat) =
            let t = runtime.CreateTexture2D(V2i(256), format)

            try
                runtime.ClearColor(t, V4i(-1))
                t.Download()
            finally
                runtime.DeleteTexture(t)

        let rgba32i (runtime : IRuntime) =
            let data = createAndClearColor runtime TextureFormat.Rgba32i
            data.AsPixImage<int>() |> PixImage.isColor (V4i(-1).ToArray())

        let rgba32ui (runtime : IRuntime) =
            let data = createAndClearColor runtime TextureFormat.Rgba32ui
            data.AsPixImage<uint>() |> PixImage.isColor [| UInt32.MaxValue; UInt32.MaxValue; UInt32.MaxValue; UInt32.MaxValue |]

        let rgba16ui (runtime : IRuntime) =
            let data = createAndClearColor runtime TextureFormat.Rgba16ui
            data.AsPixImage<uint16>() |> PixImage.isColor [| UInt16.MaxValue; UInt16.MaxValue; UInt16.MaxValue; UInt16.MaxValue |]

        let rgba32f (runtime : IRuntime) =
            let data = createAndClearColor runtime TextureFormat.Rgba32f
            data.AsPixImage<float32>() |> PixImage.isColor (V4f(-1).ToArray())

        let depthStencil (runtime : IRuntime) =
            let t = runtime.CreateTexture2D(V2i(256), TextureFormat.Depth24Stencil8)

            try
                runtime.ClearDepthStencil(t, Some 0.5, Some 3)
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

            let formats =
                Map.ofList [
                    c0, RenderbufferFormat.Rgba32f
                    c1, RenderbufferFormat.Rgba32i
                    c2, RenderbufferFormat.Rgba32ui
                    c3, RenderbufferFormat.Rgba16i
                    DefaultSemantic.Depth, RenderbufferFormat.Depth24Stencil8
                ]

            let signature =
                runtime.CreateFramebufferSignature(formats)

            let textures =
                formats |> Map.map (fun _ fmt -> runtime.CreateTexture2D(V2i(256), TextureFormat.ofRenderbufferFormat fmt))

            let fbo =
                let attachments = textures |> Map.map (fun _ tex -> tex.GetOutputView())
                runtime.CreateFramebuffer(signature, attachments)

            try
                let clear =
                    clear {
                        color C3b.AliceBlue
                        color (c1, V4i(-1))
                        depth 0.5
                        stencil 4
                    }

                clearFramebuffer fbo clear

                do
                    let c = C3b.AliceBlue |> C4f
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
                    let c = C3b.AliceBlue |> V4i
                    let pi = textures.[c3].Download().AsPixImage<int16>()
                    pi |> PixImage.isColor (c.ToArray() |> Array.map int16)

                do
                    let depth = textures.[DefaultSemantic.Depth].DownloadDepth()
                    depth.Data |> Array.iter (fun x -> Expect.floatClose Accuracy.medium (float x) 0.5 "Depth data mismatch")

                do
                    let stencil = textures.[DefaultSemantic.Depth].DownloadStencil()
                    stencil.Data |> Array.iter (fun x -> Expect.equal x 4 "Stencil data mismatch")       

            finally
                runtime.DeleteFramebuffer(fbo)
                runtime.DeleteFramebufferSignature(signature)
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