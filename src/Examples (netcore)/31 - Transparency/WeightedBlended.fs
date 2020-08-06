namespace Transparency

// TODO: Antialiasing is handled kind of sketchy, isn't it?

module WeightedBlended =

    open Aardvark.Base
    open Aardvark.Rendering
    open Aardvark.Base.Rendering
    open Aardvark.SceneGraph
    open FSharp.Data.Adaptive
    open FSharp.Data.Adaptive.Operators

    module private DefaultSemantic =
        let Color0 = Symbol.Create "Color0"
        let Color1 = Symbol.Create "Color1"
        let ColorBuffer = Symbol.Create "ColorBuffer"
        let AlphaBuffer = Symbol.Create "AlphaBuffer"

    module private Shaders =
        open FShade

        type Fragment = {
            [<Color>] color : V4d
            [<FragCoord>] coord : V4d
            [<SampleId>] sample : int
        }

        // Computes the sum of colors as well as the revealage.
        // Since aardvark currently does not support different blend modes per attachment, we waste a bit of memory here.
        // Color0: (R * A * w, G * A * w, B * A * w, -)
        // Color1: (A * w, -, -, A)
        // RGB channels are summed, Alpha channel is a product for computing the revealage.
        // http://casual-effects.blogspot.com/2015/03/implemented-weighted-blended-order.html
        let weightedBlend (f : Fragment) =
            fragment {
                let a = f.color.W * 8.0 + 0.01
                let b = -f.coord.Z * 0.95 + 1.0
                let w = clamp 1e-2 3e2 (a * a * a * 1e8 * b * b * b)

                let alpha = f.color.W
                let color = V4d(f.color.XYZ * alpha, alpha) * w

                return {| Color0 = V4d(color.XYZ, 0.0)
                          Color1 = V4d(color.W, 0.0, 0.0, alpha) |}
            }

        let private colorSampler =
            sampler2d {
                texture uniform?ColorBuffer
                filter Filter.MinMagPoint
                addressU WrapMode.Wrap
                addressV WrapMode.Wrap
            }

        let private alphaSampler =
            sampler2d {
                texture uniform?AlphaBuffer
                filter Filter.MinMagPoint
                addressU WrapMode.Wrap
                addressV WrapMode.Wrap
            }

        let private colorSamplerMS =
            sampler2dMS {
                texture uniform?ColorBuffer
                filter Filter.MinMagPoint
                addressU WrapMode.Wrap
                addressV WrapMode.Wrap
            }

        let private alphaSamplerMS =
            sampler2dMS {
                texture uniform?AlphaBuffer
                filter Filter.MinMagPoint
                addressU WrapMode.Wrap
                addressV WrapMode.Wrap
            }

        // Composites the results using the two buffers from the earlier pass.
        let composite (samples : int) (f : Fragment) =
            fragment {
                let mutable color = V4d.Zero
                let mutable alpha = V4d.Zero

                if samples > 1 then
                    for i in 0 .. samples - 1 do
                        color <- color + colorSamplerMS.Read(V2i f.coord.XY, i)
                        alpha <- alpha + alphaSamplerMS.Read(V2i f.coord.XY, i)

                    color <- color / float samples
                    alpha <- alpha / float samples
                else
                    color <- colorSampler.Read(V2i f.coord.XY, 0)
                    alpha <- alphaSampler.Read(V2i f.coord.XY, 0)

                let accum = V4d(color.XYZ, alpha.X)
                let revealage = alpha.W

                let accum =
                    if isInfinity accum then
                        V4d(accum.W)
                    else
                        accum

                return V4d(accum.XYZ / max accum.W 1e-5, 1.0 - revealage)
            }

        // Blit one multisampled texture to another.
        let private diffuseSampler =
            sampler2d {
                texture uniform?DiffuseColorTexture
                filter Filter.MinMagMipLinear
                addressU WrapMode.Wrap
                addressV WrapMode.Wrap
            }

        let private diffuseSamplerMS =
            sampler2dMS {
                texture uniform?DiffuseColorTexture
                filter Filter.MinMagMipLinear
                addressU WrapMode.Wrap
                addressV WrapMode.Wrap
            }

        let blit (samples : int) (f : Fragment) =
            fragment {
                if samples > 1 then
                    return diffuseSamplerMS.Read(V2i f.coord.XY, f.sample)
                else
                    return diffuseSampler.Read(V2i f.coord.XY, 0)
            }

    [<AutoOpen>]
    module private Utility =

        let blendModeTransparencyPass =
            let mutable bm = BlendMode()
            bm.Enabled <- true
            bm.SourceFactor <- BlendFactor.One
            bm.DestinationFactor <- BlendFactor.One
            bm.SourceAlphaFactor <- BlendFactor.Zero
            bm.DestinationAlphaFactor <- BlendFactor.InvSourceAlpha
            bm.Operation <- BlendOperation.Add
            bm.AlphaOperation <- BlendOperation.Add
            bm

        let createAttachment (runtime : IRuntime) (format : RenderbufferFormat) (samples : int) (size : aval<V2i>) =
            runtime.CreateTextureAttachment(
                runtime.CreateTexture(TextureFormat.ofRenderbufferFormat format, samples, size), 0
            )

    type Technique(runtime : IRuntime, framebuffer : FramebufferInfo, scene : Scene) =

        let size = framebuffer.size
        let samples = framebuffer.samples

        // We create a separate framebuffer so we have access to the depth buffer.
        // Ideally we'd want to use the regular framebuffer directly...
        let offscreenPass =
            runtime.CreateFramebufferSignature(samples, [
                DefaultSemantic.Colors, RenderbufferFormat.Rgba8
                DefaultSemantic.Depth, RenderbufferFormat.Depth24Stencil8
            ])

        // Framebuffer for the transparency pass, reusing the depth buffer from
        // the opaque geometry pass.
        let transparentPass =
            runtime.CreateFramebufferSignature(samples, [
                DefaultSemantic.Color0, RenderbufferFormat.Rgba16f
                DefaultSemantic.Color1, RenderbufferFormat.Rgba32f
                DefaultSemantic.Depth, RenderbufferFormat.Depth24Stencil8
            ])

        let depthBuffer =
            createAttachment runtime RenderbufferFormat.Depth24Stencil8 samples size

        let offscreenFbo =
            runtime.CreateFramebuffer(offscreenPass, Map.ofList [
                DefaultSemantic.Colors, createAttachment runtime RenderbufferFormat.Rgba8 samples size
                DefaultSemantic.Depth, depthBuffer
            ])

        let transparentFbo =
            runtime.CreateFramebuffer(transparentPass, Map.ofList [
                DefaultSemantic.Color0, createAttachment runtime RenderbufferFormat.Rgba16f samples size
                DefaultSemantic.Color1, createAttachment runtime RenderbufferFormat.Rgba32f samples size
                DefaultSemantic.Depth, depthBuffer
            ])

        // Renders the opaque scene to the regular (offscreen) framebuffer.
        let opaqueTask =
            let sg =
                scene.opaque
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.diffuseTexture
                }
                |> Sg.viewTrafo scene.viewTrafo
                |> Sg.projTrafo scene.projTrafo

            let clear = runtime.CompileClear(offscreenPass, ~~C4f.Black, ~~1.0)
            let render = runtime.CompileRender(offscreenPass, sg)
            RenderTask.ofList [clear; render]

        // Renders the transparent scene to the dedicated framebuffer, reusing
        // the depth buffer (for testing only) from the opaque geometry pass.
        let transparentTask, colorOutput, alphaOutput =
            let sg =
                scene.transparent
                |> Sg.writeBuffers' (Set.ofList [DefaultSemantic.Color0; DefaultSemantic.Color1])
                |> Sg.blendMode ~~blendModeTransparencyPass
                |> Sg.viewTrafo scene.viewTrafo
                |> Sg.projTrafo scene.projTrafo
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.sgColor
                    do! Shaders.weightedBlend
                }

            let clearColors =
                Map.ofList [
                    DefaultSemantic.Color0, C4f.Zero
                    DefaultSemantic.Color1, C4f(0.0, 0.0, 0.0, 1.0)
                ]

            let clear = runtime.CompileClear(transparentPass, ~~clearColors, ~~None)
            let render = runtime.CompileRender(transparentPass, sg)
            let task = RenderTask.ofList [clear; render]

            let output = task |> RenderTask.renderTo transparentFbo
            task, output.GetOutputTexture DefaultSemantic.Color0, output.GetOutputTexture DefaultSemantic.Color1

        // Run both passes blending the result in the offscreen framebuffer.
        let compositeTask, compositeOutput =
            let sg =
                Sg.fullScreenQuad
                |> Sg.depthTest ~~DepthTestMode.None
                |> Sg.blendMode ~~BlendMode.Blend
                |> Sg.texture DefaultSemantic.ColorBuffer colorOutput
                |> Sg.texture DefaultSemantic.AlphaBuffer alphaOutput
                |> Sg.shader {
                    do! Shaders.composite samples
                }

            let composite = runtime.CompileRender(offscreenPass, sg)
            let output =
                RenderTask.ofList [opaqueTask; composite]
                |> RenderTask.renderTo offscreenFbo

            composite, output.GetOutputTexture DefaultSemantic.Colors

        // Display the results in the main framebuffer.
        let finalTask =
            let sg =
                Sg.fullScreenQuad
                |> Sg.diffuseTexture compositeOutput
                |> Sg.shader {
                    do! Shaders.blit samples
                }

            runtime.CompileRender(framebuffer.signature, sg)

        member x.Task = finalTask

        member x.Dispose() =
            finalTask.Dispose()
            transparentTask.Dispose()
            compositeTask.Dispose()
            opaqueTask.Dispose()
            runtime.DeleteFramebufferSignature offscreenPass
            runtime.DeleteFramebufferSignature transparentPass

        interface ITechnique with
            member x.Name = "Weighted Blended OIT"
            member x.Task = x.Task
            member x.Dispose() = x.Dispose()