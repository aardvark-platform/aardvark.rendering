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

        let private clearDepthStencilInternal (format : TextureFormat)
                                              (initialDepth : float) (initialStencil : int)
                                              (clearDepth : Option<float>) (clearStencil : Option<int>)
                                              (runtime : IRuntime) =
            let t = runtime.CreateTexture2D(V2i(256), format)

            try
                runtime.ClearDepthStencil(t, initialDepth, initialStencil)
                clearDepth |> Option.iter (fun d -> runtime.ClearDepth(t, d))
                clearStencil |> Option.iter (fun s -> runtime.ClearStencil(t, s))

                if format.HasDepth then
                    let depth = t.DownloadDepth()
                    let test = clearDepth |> Option.defaultValue initialDepth
                    depth.Data |> Array.iter (fun x -> Expect.floatClose Accuracy.medium (float x) test "Depth data mismatch")

                if format.HasStencil then
                    let stencil = t.DownloadStencil()
                    let test = clearStencil |> Option.defaultValue initialStencil
                    stencil.Data |> Array.iter (fun x -> Expect.equal x test "Stencil data mismatch")
            finally
                runtime.DeleteTexture(t)

        let depth                   = clearDepthStencilInternal TextureFormat.DepthComponent32f 0.5 3 None None
        let depthStencil            = clearDepthStencilInternal TextureFormat.Depth24Stencil8 0.5 3 None None
        let depthStencilOnlyDepth   = clearDepthStencilInternal TextureFormat.Depth24Stencil8 1.0 5 (Some 0.5) None
        let depthStencilOnlyStencil = clearDepthStencilInternal TextureFormat.Depth24Stencil8 1.0 5 None (Some 3)

        let private createAndTestFramebuffer (runtime : IRuntime) (formats : Map<Symbol, TextureFormat>)
                                             (test : IFramebuffer -> Map<Symbol, IBackendTexture> -> unit) =
            let signature =
                runtime.CreateFramebufferSignature(formats)

            let textures =
                formats |> Map.map (fun _ fmt -> runtime.CreateTexture2D(V2i(256), fmt))

            let fbo =
                let attachments = textures |> Map.map (fun _ tex -> tex.GetOutputView())
                runtime.CreateFramebuffer(signature, attachments)

            try
                test fbo textures
            finally
                fbo.Dispose()
                signature.Dispose()
                textures |> Map.iter (fun _ t -> runtime.DeleteTexture t)

        let private clearFramebufferMixed (clearFramebuffer : IRuntime -> IFramebuffer -> ClearValues -> unit) (runtime : IRuntime) =
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

            createAndTestFramebuffer runtime formats (fun fbo textures ->
                let clear =
                    clear {
                        color C3b.AliceBlue
                        color c0 C3us.Aquamarine
                        color c1 (V4i(-1))
                        color c3 (C3f(0.5))
                        depth 0.5
                        stencil 4
                    }

                clearFramebuffer runtime fbo clear

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
            )


        let private clearFramebufferDepthStencilInternal (format : TextureFormat)
                                                         (initialDepth : float) (initialStencil : int)
                                                         (clearDepth : Option<float>) (clearStencil : Option<int>)
                                                         (clearFramebuffer : IRuntime -> IFramebuffer -> ClearValues -> unit)
                                                         (runtime : IRuntime) =
            let formats =
                Map.ofList [
                    DefaultSemantic.DepthStencil, format
                ]

            createAndTestFramebuffer runtime formats (fun fbo textures ->
                let clearValues =
                    clear { depth initialDepth; stencil initialStencil }

                clearFramebuffer runtime fbo clearValues

                let clearValues =
                    { ClearValues.empty with
                        Depth   = clearDepth |> Option.map float32
                        Stencil = clearStencil |> Option.map uint32 }

                clearFramebuffer runtime fbo clearValues

                do if format.HasDepth then
                    let depth = textures.[DefaultSemantic.DepthStencil].DownloadDepth()
                    let test = clearDepth |> Option.defaultValue initialDepth
                    depth.Data |> Array.iter (fun x -> Expect.floatClose Accuracy.medium (float x) test "Depth data mismatch")

                do if format.HasStencil then
                    let stencil = textures.[DefaultSemantic.DepthStencil].DownloadStencil()
                    let test = clearStencil |> Option.defaultValue initialStencil
                    stencil.Data |> Array.iter (fun x -> Expect.equal x test "Stencil data mismatch")
            )

        let clearFramebufferDepth                   = clearFramebufferDepthStencilInternal TextureFormat.DepthComponent32f 0.5 3 None None
        let clearFramebufferDepthStencil            = clearFramebufferDepthStencilInternal TextureFormat.Depth24Stencil8 0.5 3 None None
        let clearFramebufferDepthStencilOnlyDepth   = clearFramebufferDepthStencilInternal TextureFormat.Depth24Stencil8 1.0 5 (Some 0.5) None
        let clearFramebufferDepthStencilOnlyStencil = clearFramebufferDepthStencilInternal TextureFormat.Depth24Stencil8 1.0 5 None (Some 3)


        let private framebufferDirectClear (test : (IRuntime -> IFramebuffer -> ClearValues -> unit) -> IRuntime -> unit) =
            test (fun runtime fbo clear -> runtime.Clear(fbo, clear))

        let private framebufferCompileClear (test : (IRuntime -> IFramebuffer -> ClearValues -> unit) -> IRuntime -> unit) =
            test (fun runtime fbo clear ->
                use task = runtime.CompileClear(fbo.Signature, clear)
                task.Run(RenderToken.Empty, fbo)
            )

        let private framebufferRenderCommand (test : (IRuntime -> IFramebuffer -> ClearValues -> unit) -> IRuntime  -> unit) =
            test (fun runtime fbo clear ->
                use task =
                    Sg.execute (RenderCommand.Clear clear)
                    |> Sg.compile runtime fbo.Signature

                task.Run(RenderToken.Empty, fbo)
            )

        let framebufferMixed                                = framebufferDirectClear clearFramebufferMixed
        let framebufferMixedCompileClear                    = framebufferCompileClear clearFramebufferMixed
        let framebufferMixedRenderCommand                   = framebufferRenderCommand clearFramebufferMixed

        let framebufferDepth                                = framebufferDirectClear clearFramebufferDepth
        let framebufferDepthCompileClear                    = framebufferCompileClear clearFramebufferDepth
        let framebufferDepthRenderCommand                   = framebufferRenderCommand clearFramebufferDepth

        let framebufferDepthStencil                         = framebufferDirectClear clearFramebufferDepthStencil
        let framebufferDepthStencilCompileClear             = framebufferCompileClear clearFramebufferDepthStencil
        let framebufferDepthStencilRenderCommand            = framebufferRenderCommand clearFramebufferDepthStencil

        let framebufferDepthStencilOnlyDepth                = framebufferDirectClear clearFramebufferDepthStencilOnlyDepth
        let framebufferDepthStencilOnlyDepthCompileClear    = framebufferCompileClear clearFramebufferDepthStencilOnlyDepth
        let framebufferDepthStencilOnlyDepthRenderCommand   = framebufferRenderCommand clearFramebufferDepthStencilOnlyDepth

        let framebufferDepthStencilOnlyStencil              = framebufferDirectClear clearFramebufferDepthStencilOnlyStencil
        let framebufferDepthStencilOnlyStencilCompileClear  = framebufferCompileClear clearFramebufferDepthStencilOnlyStencil
        let framebufferDepthStencilOnlyStencilRenderCommand = framebufferRenderCommand clearFramebufferDepthStencilOnlyStencil

    let tests (backend : Backend) =
        [
            "Color rgba8",                              Cases.rgba8
            "Color rgba32i",                            Cases.rgba32i
            "Color rgba16ui",                           Cases.rgba16ui
            "Color rgba32ui",                           Cases.rgba32ui
            "Color rgba32f",                            Cases.rgba32f
            "Depth",                                    Cases.depth
            "Depth-stencil",                            Cases.depthStencil
            "Depth-only",                               Cases.depthStencilOnlyDepth
            "Stencil-only",                             Cases.depthStencilOnlyStencil

            "Framebuffer",                              Cases.framebufferMixed
            "Framebuffer compiled",                     Cases.framebufferMixedCompileClear
            "Framebuffer render command",               Cases.framebufferMixedRenderCommand

            "Framebuffer depth",                        Cases.framebufferDepth
            "Framebuffer depth compiled",               Cases.framebufferDepthCompileClear
            "Framebuffer depth render command",         Cases.framebufferDepthRenderCommand

            "Framebuffer depth-stencil",                Cases.framebufferDepthStencil
            "Framebuffer depth-stencil compiled",       Cases.framebufferDepthStencilCompileClear
            "Framebuffer depth-stencil render command", Cases.framebufferDepthStencilRenderCommand

            "Framebuffer depth-only",                   Cases.framebufferDepthStencilOnlyDepth
            "Framebuffer depth-only, compiled",         Cases.framebufferDepthStencilOnlyDepthCompileClear
            "Framebuffer depth-only, render command",   Cases.framebufferDepthStencilOnlyDepthRenderCommand

            "Framebuffer stencil-only",                 Cases.framebufferDepthStencilOnlyStencil
            "Framebuffer stencil-only, compiled",       Cases.framebufferDepthStencilOnlyStencilCompileClear
            "Framebuffer stencil-only, render command", Cases.framebufferDepthStencilOnlyStencilRenderCommand
        ]
        |> prepareCases backend "Clear"