namespace Aardvark.Rendering.Tests.Rendering

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Aardvark.Application

module Viewport =

    module Cases =

        module private Shader =
            open FShade

            let viewportSize () =
                fragment {
                    return uniform.ViewportSize
                }

        type private Mode =
            | Clear  of command: bool
            | Render of dynamic: bool

        let private viewport (withScissor: bool) (mode: Mode) (runtime: IRuntime) =
            use signature =
                runtime.CreateFramebufferSignature([
                    DefaultSemantic.Colors, TextureFormat.Rg32i
                ])

            use colors = runtime.CreateTexture2D(V2i(256), TextureFormat.Rg32i)
            use framebuffer = runtime.CreateFramebuffer(signature, [ DefaultSemantic.Colors, colors.GetOutputView() ])

            let viewport = Box2i.FromMinAndSize(V2i(10, 30), V2i(159, 138))
            let scissor = if withScissor then Box2i.FromMinAndSize(viewport.Min + V2i(46, 19), V2i(33, 87)) else viewport

            let resultValue = viewport.Size
            let clearValue = V2i -42
            framebuffer.Clear(clearValue)

            match mode with
            | Clear command ->
                use task =
                    if command then
                        RenderCommand.Clear resultValue
                        |> Sg.execute
                        |> Sg.compile runtime signature
                    else
                        runtime.CompileClear(signature, resultValue)

                let output = { Framebuffer = framebuffer; Viewport = viewport; Scissor = scissor }
                task.Run(output)

            | Render dynamic ->
                use task =
                    Sg.fullScreenQuad
                    |> Sg.shader { do! Shader.viewportSize }
                    |> if dynamic then Sg.viewport' viewport else id
                    |> if dynamic then Sg.scissor' scissor else id
                    |> Sg.compile runtime signature

                let output =
                    if dynamic then
                        OutputDescription.ofFramebuffer framebuffer
                    else
                        { Framebuffer = framebuffer; Viewport = viewport; Scissor = scissor }

                task.Run(output)

            let result = colors.Download().AsPixImage<int>()

            let expected =
                let region = Box2i.FromMinAndSize(scissor.Min, scissor.Size - 1)
                let pi = PixImage<int>(Col.Format.RG, colors.Size.XY)

                for i = 0 to pi.ChannelCount - 1 do
                    pi.GetChannel(int64 i).SetByCoord(fun (coord: V2l) ->
                        if region.Contains (V2i coord) then
                            resultValue.[i]
                        else
                            clearValue.[i]
                    ) |> ignore

                pi.Transformed(ImageTrafo.MirrorY)

            PixImage.compare V2i.Zero expected result

        let clearTaskStatic             = viewport false (Clear false)
        let clearCmdStatic              = viewport false (Clear true)
        let renderStatic                = viewport false (Render false)
        let renderDynamic               = viewport false (Render true)

        let clearTaskStaticWithScissor  = viewport true (Clear false)
        let clearCmdStaticWithScissor   = viewport true (Clear true)
        let renderStaticWithScissor     = viewport true (Render false)
        let renderDynamicWithScissor    = viewport true (Render true)

    let tests (backend: Backend) =
        [
            "Clear task with static viewport",                  Cases.clearTaskStatic
            "Clear command with static viewport",               Cases.clearCmdStatic
            "Render with static viewport",                      Cases.renderStatic
            "Render with dynamic viewport",                     Cases.renderDynamic

            "Clear task with static viewport and scissor",      Cases.clearTaskStaticWithScissor
            "Clear command with static viewport and scissor",   Cases.clearCmdStaticWithScissor
            "Render with static viewport and scissor",          Cases.renderStaticWithScissor
            "Render with dynamic viewport and scissor",         Cases.renderDynamicWithScissor
        ]
        |> prepareCases backend "Viewport"