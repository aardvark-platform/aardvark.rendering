namespace Aardvark.Rendering.Tests.Rendering

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Aardvark.Application
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open Expecto

module Samplers =

    module private Semantic =
        let Output1 = Sym.ofString "Output1"
        let Output2 = Sym.ofString "Output2"
        let Texture1 = Sym.ofString "Texture1"
        let Texture2 = Sym.ofString "Texture2"

    module private Shader =
        open FShade

        type Vertex = {
            [<TexCoord>] tc : V2f
        }

        let private diffuseIntSampler =
            intSampler2d {
                texture uniform?DiffuseColorTexture
                filter Filter.MinMagPoint
            }

        let private diffuseUIntSampler =
            uintSampler2d {
                texture uniform?DiffuseColorTexture
                filter Filter.MinMagPoint
            }

        let outOfBoundsTexCoords (v : Vertex) =
            vertex {
                return { v with tc = V2f(2.0f) }
            }

        let diffuseIntTexture (v : Vertex) =
            fragment {
                return diffuseIntSampler.Sample v.tc
            }

        let diffuseUIntTexture (v : Vertex) =
            fragment {
                return diffuseUIntSampler.Sample v.tc
            }

        let private borderSampler1 =
            sampler2d {
                texture uniform?Texture1
                filter Filter.MinMagMipPoint
                addressU WrapMode.Border
                addressV WrapMode.Border
            }

        let private borderSampler2 =
            sampler2d {
                texture uniform?Texture2
                filter Filter.MinMagMipPoint
                addressU WrapMode.Border
                addressV WrapMode.Border
            }

        let texturesWithBorderSampler (tc: V2f) (_ : Vertex) =
            fragment {
                let o1 = borderSampler1.Sample tc
                let o2 = borderSampler2.Sample tc
                return {| Output1 = o1; Output2 = o2 |}
            }

    module Cases =

        let private sample2DInteger (getPixImage : V2i -> PixImage<'T>) (format : TextureFormat) (samples : int) (runtime : IRuntime) =
            let size = V2i(256)

            use signature =
                runtime.CreateFramebufferSignature([
                    DefaultSemantic.Colors, format
                ], samples = samples)

            let input = getPixImage size

            use inputTexture = runtime.CreateTexture2D(size, format)
            inputTexture.Upload input

            use task =
                Sg.fullScreenQuad
                |> Sg.diffuseTexture' inputTexture
                |> Sg.shader {
                    if format.IsSigned then
                        do! Shader.diffuseIntTexture
                    else
                        do! Shader.diffuseUIntTexture
                }
                |> Sg.compile runtime signature

            let buffer = task |> RenderTask.renderToColor (AVal.constant size)
            buffer.Acquire()

            try
                let output = buffer.GetValue().Download().AsPixImage<'T>()

                Expect.equal output.Size size "Unexpected texture size"
                PixImage.compare V2i.Zero input output

            finally
                buffer.Release()

        let sample2Drgba8i (samples : int) (runtime : IRuntime) =
            runtime |> sample2DInteger PixImage.random8i TextureFormat.Rgba8i samples

        let sample2Drgba16i (samples : int) (runtime : IRuntime) =
            runtime |> sample2DInteger PixImage.random16i TextureFormat.Rgba16i samples

        let sample2Drgba32i (samples : int) (runtime : IRuntime) =
            runtime |> sample2DInteger PixImage.random32i TextureFormat.Rgba32i samples

        let sample2Drgba8ui (samples : int) (runtime : IRuntime) =
            runtime |> sample2DInteger PixImage.random8ui TextureFormat.Rgba8ui samples

        let sample2Drgba16ui (samples : int) (runtime : IRuntime) =
            runtime |> sample2DInteger PixImage.random16ui TextureFormat.Rgba16ui samples

        let sample2Drgba32ui (samples : int) (runtime : IRuntime) =
            runtime |> sample2DInteger PixImage.random32ui TextureFormat.Rgba32ui samples

        let sample2DGrayscale (runtime : IRuntime) =
            let size = V2i(256)

            use signature =
                runtime.CreateFramebufferSignature([
                    DefaultSemantic.Colors, TextureFormat.Rgba8
                ])

            let input = PixImage.random8ui' Col.Format.Gray size

            let reference =
                let pi = PixImage<uint8>(Col.Format.RGBA, size)
                let red = input.GetChannel(0L)

                for i = 0 to 2 do
                    pi.GetChannel(int64 i).Set(red) |> ignore
                pi.GetChannel(3L).Set(255uy) |> ignore

                pi

            use inputTexture = runtime.CreateTexture2D(size, TextureFormat.R8)
            inputTexture.Upload input

            use task =
                Sg.fullScreenQuad
                |> Sg.diffuseTexture' inputTexture
                |> Sg.shader {
                    do! DefaultSurfaces.diffuseTexture
                }
                |> Sg.compile runtime signature

            let buffer = task |> RenderTask.renderToColor (AVal.constant size)
            buffer.Acquire()

            try
                let output = buffer.GetValue().Download().AsPixImage<uint8>()

                Expect.equal output.Size size "Unexpected texture size"
                Expect.equal output.ChannelCount 4 "Unexpected channel count"
                PixImage.compare V2i.Zero reference output

            finally
                buffer.Release()

        let sampleDepthStencil (runtime : IRuntime) =
            let size = V2i(256)

            let clear =
                clear {
                    depth 0.95
                    stencil 1
                }

            renderQuadToDepthStencil runtime TextureFormat.Depth24Stencil8 1 clear size (fun texture ->
                use signature =
                    runtime.CreateFramebufferSignature([
                        DefaultSemantic.Colors, TextureFormat.R32f
                    ])

                // Sample depth / stencil with a regular floating point signature.
                // For GL the texture parameter GL_DEPTH_STENCIL_TEXTURE_MODE decides whether depth or stencil is accessed (depth is default)
                // In Vulkan we create an image view with depth aspect.
                use task =
                    Sg.fullScreenQuad
                    |> Sg.diffuseTexture' texture
                    |> Sg.shader {
                        do! DefaultSurfaces.diffuseTexture
                    }
                    |> Sg.compile runtime signature

                let output = task |> RenderTask.renderToColor (AVal.constant size)
                output.Acquire()

                try
                    let result = output.GetValue().Download().AsPixImage<float32>().Matrix
                    Expect.validDepthResult result Accuracy.medium size 0.5 0.95
                finally
                    output.Release()
            )

        let sample2DDynamicSamplerStates (runtime: IRuntime) =
            let size = V2i(2, 1)
            let tc = V2f(1.5, 0.0)

            use signature =
                runtime.CreateFramebufferSignature([
                    Semantic.Output1, TextureFormat.R8
                    Semantic.Output2, TextureFormat.R8
                ])

            let input =
                let mutable mat = Matrix<byte>(size)
                mat.Data.[0] <- 1uy
                mat.Data.[1] <- 2uy
                PixImage<byte>(Col.Format.Gray, mat)

            use inputTexture = runtime.CreateTexture2D(size, TextureFormat.R8)
            inputTexture.Upload input

            use task =
                Sg.fullScreenQuad
                |> Sg.texture' Semantic.Texture1 inputTexture
                |> Sg.modifySamplerState' Semantic.Texture1 (fun s ->
                    { s with AddressU = WrapMode.Mirror }
                )
                |> Sg.texture' Semantic.Texture2 inputTexture
                |> Sg.modifySamplerState' Semantic.Texture2 (fun s ->
                    { s with AddressU = WrapMode.Wrap }
                )
                |> Sg.shader {
                    do! Shader.texturesWithBorderSampler tc
                }
                |> Sg.compile runtime signature

            let r1, r2 =
                let fbo = runtime.CreateFramebuffer(signature, ~~V2i.II)
                let clear = clear { color C4b.Black }
                let output = task |> RenderTask.renderToWithClear fbo clear
                let o1 = output.GetOutputTexture(Semantic.Output1)
                let o2 = output.GetOutputTexture(Semantic.Output2)
                o1, o2

            r1.Acquire(); r2.Acquire()

            try
                let o1 = r1.GetValue().Download().AsPixImage<uint8>().Data.[0]
                let o2 = r2.GetValue().Download().AsPixImage<uint8>().Data.[0]

                Expect.equal o1 1uy "Unexpected value in output 1"
                Expect.equal o2 2uy "Unexpected value in output 2"
            finally
                r1.Release(); r2.Release()

        let private sample2DBorder (expected: 'T[]) (setBorderColor: SamplerState -> SamplerState) (runtime: IRuntime) =
            let size = V2i(1)

            let format, effect =
                match typeof<'T> with
                | TypeMeta.Patterns.Int32   -> TextureFormat.Rgba32i,  toEffect Shader.diffuseIntTexture
                | TypeMeta.Patterns.UInt32  -> TextureFormat.Rgba32ui, toEffect Shader.diffuseUIntTexture
                | TypeMeta.Patterns.Float32 -> TextureFormat.Rgba32f,  toEffect DefaultSurfaces.diffuseTexture
                | t -> failwithf "Invalid type: %A" t

            use signature =
                runtime.CreateFramebufferSignature([
                    DefaultSemantic.Colors, format
                ])

            let dummyTexture =
                let mutable vol = Volume<'T>(V3i(size, 4))
                let pi = PixImage<'T>(Col.Format.RGBA, vol)
                PixTexture2d pi

            use task =
                Sg.fullScreenQuad
                |> Sg.diffuseTexture' dummyTexture
                |> Sg.shader {
                    do! Shader.outOfBoundsTexCoords
                    do! effect
                }
                |> Sg.modifySamplerState' DefaultSemantic.DiffuseColorTexture setBorderColor
                |> Sg.compile runtime signature

            let buffer = task |> RenderTask.renderToColor (AVal.constant size)
            buffer.Acquire()

            try
                let output = buffer.GetValue().Download().AsPixImage<'T>().Data
                Expect.equal output expected "Unexpected result"
            finally
                buffer.Release()

        let sample2DBorderFloat32 (runtime: IRuntime) =
            runtime |> requireFeatures _.Samplers.CustomBorderColors "Device does not support custom border colors"
            let value = C4f(0.5f, 1.234f, -4.12f, 90.24f)
            sample2DBorder (value.ToArray()) (SamplerState.withBorderColor value) runtime

        let sample2DBorderUInt32 (runtime: IRuntime) =
            runtime |> requireFeatures _.Samplers.CustomBorderColors "Device does not support custom border colors"
            let value = C4ui(0u, 1u, 347824u, Constant<uint32>.ParseableMaxValue)
            sample2DBorder (value.ToArray()) (SamplerState.withBorderColorui value) runtime

        let sample2DBorderInt32 (runtime: IRuntime) =
            runtime |> requireFeatures _.Samplers.CustomBorderColors "Device does not support custom border colors"
            let value = V4i(0, 1, -347824, Constant<int32>.ParseableMinValue)
            sample2DBorder (value.ToArray()) (SamplerState.withBorderColori value) runtime

    let tests (backend : Backend) =
        [
            "2D rgba8i", Cases.sample2Drgba8i 1
            "2D rgba16i", Cases.sample2Drgba16i 1
            "2D rgba32i", Cases.sample2Drgba32i 1
            "2D rgba32i multisampled", Cases.sample2Drgba32i 4

            "2D rgba8ui", Cases.sample2Drgba8ui 1
            "2D rgba16ui", Cases.sample2Drgba16ui 1
            "2D rgba32ui", Cases.sample2Drgba32ui 1
            "2D rgba32ui multisampled", Cases.sample2Drgba32ui 2

            "2D grayscale", Cases.sample2DGrayscale

            "2D depth / stencil", Cases.sampleDepthStencil

            "2D dynamic sampler states", Cases.sample2DDynamicSamplerStates

            "2D border color float32", Cases.sample2DBorderFloat32
            "2D border color uint32", Cases.sample2DBorderUInt32
            "2D border color int32", Cases.sample2DBorderInt32
        ]
        |> prepareCases backend "Samplers"