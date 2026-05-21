namespace Aardvark.Rendering.Tests.Rendering

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Aardvark.Application
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open Expecto

module Transparency =

    module private Semantic =
        let PickData = Sym.ofString "PickData"

    module private Shader =
        open FShade

        type Fragment = {
            [<FragCoord>] coord : V4f
        }

        // Writes PickData = V4f(pickId, ndcDepth, 0, 0). The Colors output is
        // produced by an upstream shader; this stage only adds PickData.
        // f.coord.Z is the window-space depth (already in [0, 1]) so it's
        // directly comparable to the depth attachment. PickId is baked into
        // the effect as a closure (avoids per-RO uniform plumbing).
        let writePick (pickId : float32) (f : Fragment) =
            fragment {
                return {| PickData = V4f(pickId, f.coord.Z, 0.0f, 0.0f) |}
            }

        // --- Smoke test: can a fragment shader write to a storage image? ---
        type UniformScope with
            member x.SmokeTarget : UIntImage2d<Formats.r32ui> = x?SmokeTarget

        // Writes (x + y*width + 1) into the storage image at each pixel, so a
        // readback of 0 means "the write never happened". Returns black.
        let smokeWriteImage (width : int) (f : Fragment) =
            fragment {
                let px = V2i f.coord.XY
                uniform.SmokeTarget.[px] <- V4ui(uint32 (px.X + px.Y * width + 1), 0u, 0u, 0u)
                return V4f.Zero
            }

    module Cases =

        /// Smoke test: prove a fragment shader can write to a storage image
        /// through the graphics pipeline (the foundation the A-buffer relies
        /// on). A fullscreen quad writes (x + y*w + 1) into an R32UI image;
        /// we read it back and check every pixel.
        let fragmentImageWrite (runtime : IRuntime) =
            let size = V2i(8)

            let target = runtime.CreateTexture2D(size, TextureFormat.R32ui)

            use signature =
                runtime.CreateFramebufferSignature([
                    DefaultSemantic.Colors, TextureFormat.Rgba8
                ])

            let colorTex = runtime.CreateTexture2D(size, TextureFormat.Rgba8)
            let fbo =
                runtime.CreateFramebuffer(signature, Map.ofList [
                    DefaultSemantic.Colors, colorTex.GetOutputView()
                ])

            use task =
                Sg.fullScreenQuad
                |> Sg.texture "SmokeTarget" (AVal.constant (target :> ITexture))
                |> Sg.shader { do! Shader.smokeWriteImage size.X }
                |> Sg.compile runtime signature

            try
                task.Run(AdaptiveToken.Top, RenderToken.Empty, OutputDescription.ofFramebuffer fbo)

                // Each pixel writes a unique id in [1 .. w*h]; verify the
                // multiset of written values is exactly {1 .. w*h}, which
                // proves every pixel was written exactly once (independent of
                // the gl_FragCoord-vs-download Y orientation).
                let data = target.Download().AsPixImage<uint32>().Data
                let got = data |> Array.sort
                let expected = Array.init (size.X * size.Y) (fun i -> uint32 (i + 1))
                Expect.sequenceEqual got expected
                    "fragment storage-image writes must cover every pixel exactly once (0 means the write never landed)"
            finally
                fbo.Dispose()
                runtime.DeleteTexture target
                runtime.DeleteTexture colorTex

        /// Five screen-filling quads with identity camera and varying NDC z:
        ///
        ///   z = -0.8  transparent  A  (blue,  in front of solid)
        ///   z = -0.3  transparent  B  (green, in front of solid)
        ///   z =  0.0  SOLID OPAQUE     (red)
        ///   z =  0.3  transparent  C  (yellow, behind solid — should be occluded)
        ///   z =  0.7  transparent  D  (cyan,   behind solid — should be occluded)
        ///
        /// Verifies that
        ///   - PickData[R] equals A's pick id (the closest transparent)
        ///   - depth equals A's window-space depth
        ///   - color is influenced by A and B but not by C or D (those are
        ///     occluded by the solid's depth so their writes are discarded)
        let zStackWithOccluder (runtime : IRuntime) =
            let size = V2i(8)

            let makeQuad (color : C4f) (pickId : float32) (z : float) (transparent : bool) =
                let sg =
                    Sg.fullScreenQuad
                    |> Sg.translate 0.0 0.0 z
                    |> Sg.shader {
                        do! DefaultSurfaces.trafo
                        do! DefaultSurfaces.constantColor color
                        do! Shader.writePick pickId
                    }
                if transparent then sg |> Sg.transparent else sg

            let scene =
                Sg.ofList [
                    makeQuad (C4f(0.0f, 0.0f, 1.0f, 0.5f)) 2.0f -0.8 true   // A
                    makeQuad (C4f(0.0f, 1.0f, 0.0f, 0.5f)) 3.0f -0.3 true   // B
                    makeQuad (C4f(1.0f, 0.0f, 0.0f, 1.0f)) 1.0f  0.0 false  // SOLID
                    makeQuad (C4f(1.0f, 1.0f, 0.0f, 0.5f)) 4.0f  0.3 true   // C
                    makeQuad (C4f(0.0f, 1.0f, 1.0f, 0.5f)) 5.0f  0.7 true   // D
                ]

            use signature =
                runtime.CreateFramebufferSignature([
                    DefaultSemantic.Colors,       TextureFormat.Rgba32f
                    Semantic.PickData,            TextureFormat.Rgba32f
                    DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
                ])

            let colorTex = runtime.CreateTexture2D(size, TextureFormat.Rgba32f)
            let pickTex  = runtime.CreateTexture2D(size, TextureFormat.Rgba32f)
            let depthTex = runtime.CreateTexture2D(size, TextureFormat.Depth24Stencil8)

            let fbo =
                runtime.CreateFramebuffer(signature, Map.ofList [
                    DefaultSemantic.Colors,       colorTex.GetOutputView()
                    Semantic.PickData,            pickTex.GetOutputView()
                    DefaultSemantic.DepthStencil, depthTex.GetOutputView()
                ])

            use task      = scene |> Sg.compile runtime signature
            use clearTask =
                runtime.CompileClear(
                    signature,
                    ~~(clear { color C4f.Black; depth 1.0; stencil 0 }))

            try
                clearTask.Run(AdaptiveToken.Top, RenderToken.Empty, OutputDescription.ofFramebuffer fbo)
                task.Run(     AdaptiveToken.Top, RenderToken.Empty, OutputDescription.ofFramebuffer fbo)

                // -------- PickData (R channel) --------------------------------
                // Pass 3 must elect the closest transparent fragment (A,
                // pickId = 2) at every pixel. PickData is RGBA32F so the
                // interleaved Data buffer has R at indices 0, 4, 8, ...
                let pickData = pickTex.Download().AsPixImage<float32>().Data
                let nPixels = pickData.Length / 4
                for i in 0 .. nPixels - 1 do
                    let r = pickData.[i * 4]
                    Expect.floatClose
                        Accuracy.low
                        (float r) 2.0
                        $"PickData[R] @ pixel {i} must equal the closest transparent's id (A=2)"

                // -------- Depth ------------------------------------------------
                // Pass 3 must have written A's window-space depth. With the
                // default GL depth mapping ((-1, 1) -> (0, 1)), A at NDC z=-0.8
                // becomes window-space depth 0.1.
                let depth = depthTex.DownloadDepth().Data
                depth |> Array.iter (fun d ->
                    Expect.isLessThan d 0.2f
                        "Depth must be near A's window-space depth (~0.1); larger values mean transparent objects didn't win the depth test")

                // -------- Color ------------------------------------------------
                // The composited color should reflect A (blue) and B (green)
                // blended over the solid red, NOT C (yellow) or D (cyan).
                let cArr = colorTex.Download().AsPixImage<float32>().Data
                let r0, g0, b0 = cArr.[0], cArr.[1], cArr.[2]

                Expect.isLessThan r0 1.0f
                    "Red must drop below 1 — transparents in front modify it"

                Expect.isGreaterThan g0 0.0f
                    "Green must be > 0 — quad B contributed"

                Expect.isGreaterThan b0 0.0f
                    "Blue must be > 0 — quad A contributed"

                Expect.isLessThan r0 0.95f
                    "Red must not include occluded yellow's contribution"

            finally
                fbo.Dispose()
                runtime.DeleteTexture colorTex
                runtime.DeleteTexture pickTex
                runtime.DeleteTexture depthTex

    let tests (backend : Backend) =
        [
            "fragment storage-image write", Cases.fragmentImageWrite
            "z-stack with opaque occluder",  Cases.zStackWithOccluder
        ]
        |> prepareCases backend "Transparency"
