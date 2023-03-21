namespace Aardvark.Rendering.Tests.Rendering

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Aardvark.Application
open FSharp.Data.Adaptive
open Expecto

module Culling =

    module private Shader =
        open FShade

        type Vertex =
            { [<FrontFacing>] isFrontFace : bool }

        let frontFacing (front : V3d) (back : V3d) (v : Vertex) =
            fragment {
                return if v.isFrontFace then front else back
            }

    module private Effect =
        let constantRed = toEffect <| DefaultSurfaces.constantColor C4f.Red
        let frontFacingRed = toEffect <| Shader.frontFacing V3d.IOO V3d.OOO

    module FullscreenQuad =

        let private makeSg (mode : IndexedGeometryMode) (positions : V3f[]) =
            DrawCallInfo(faceVertexCount = positions.Length)
            |> Sg.render mode
            |> Sg.vertexAttribute' DefaultSemantic.Positions positions

        let alternating =
            [| V3f(-1,-1,0); V3f(1,-1,0); V3f(-1,1,0); V3f(1,-1,0); V3f(-1,1,0); V3f(1,1,0) |]
            |> makeSg IndexedGeometryMode.TriangleList

        let ccw =
            [| V3f(-1,-1,0); V3f(1,-1,0); V3f(-1,1,0); V3f(1,1,0) |]
            |> makeSg IndexedGeometryMode.TriangleStrip

        let cw =
            [| V3f(-1,-1,0); V3f(-1,1,0); V3f(1,-1,0); V3f(1,1,0) |]
            |> makeSg IndexedGeometryMode.TriangleStrip

    module Cases =

        let private renderToPix (f : PixImage<uint8> -> unit) (sg : ISg) (runtime : IRuntime) =
            let size = AVal.constant <| V2i(256)

            use signature =
                runtime.CreateFramebufferSignature([
                    DefaultSemantic.Colors, TextureFormat.Rgba8
                ])

            use task =
                sg |> Sg.compile runtime signature

            let output = task |> RenderTask.renderToColor size
            output.Acquire()

            try
                f <| output.GetValue().Download().AsPixImage<uint8>()
            finally
                output.Release()

        let private renderToRed : ISg -> IRuntime -> unit =
            Sg.effect [Effect.constantRed]
            >> renderToPix (
                PixImage.isColor [| 255uy; 0uy; 0uy |]
            )

        let defaultNoCulling : IRuntime -> unit =
            FullscreenQuad.alternating
            |> renderToRed

        let defaultFrontFaceCCW : IRuntime -> unit =
            FullscreenQuad.ccw
            |> Sg.cullMode' CullMode.Back
            |> renderToRed

        let private renderToRedWithCulling (cull : CullMode) (frontFace : WindingOrder) =
            Sg.cullMode' cull
            >> Sg.frontFacing' frontFace
            >> renderToRed

        let backFaceCullingFrontFaceCCW : IRuntime -> unit =
            FullscreenQuad.ccw
            |> renderToRedWithCulling CullMode.Back WindingOrder.CounterClockwise

        let backFaceCullingFrontFaceCW : IRuntime -> unit =
            FullscreenQuad.cw
            |> renderToRedWithCulling CullMode.Back WindingOrder.Clockwise

        let frontFaceCullingFrontFaceCCW : IRuntime -> unit =
            FullscreenQuad.cw
            |> renderToRedWithCulling CullMode.Front WindingOrder.CounterClockwise

        let frontFaceCullingFrontFaceCW : IRuntime -> unit =
            FullscreenQuad.ccw
            |> renderToRedWithCulling CullMode.Front WindingOrder.Clockwise

        let shaderFrontFacing : IRuntime -> unit =
            FullscreenQuad.ccw
            |> Sg.cullMode' CullMode.Back
            |> Sg.frontFacing' WindingOrder.CounterClockwise
            |> Sg.effect [Effect.frontFacingRed]
            |> renderToPix (
                PixImage.isColor [| 255uy; 0uy; 0uy |]
            )

    let tests (backend : Backend) =
        [
            "Default no culling",                     Cases.defaultNoCulling
            "Default front face CCW",                 Cases.defaultFrontFaceCCW

            "Back face culling with front face CCW",  Cases.backFaceCullingFrontFaceCCW
            "Back face culling with front face CW",   Cases.backFaceCullingFrontFaceCW

            "Front face culling with front face CCW", Cases.frontFaceCullingFrontFaceCCW
            "Front face culling with front face CW",  Cases.frontFaceCullingFrontFaceCW

            "Shader front facing",                    Cases.shaderFrontFacing
        ]
        |> prepareCases backend "Culling"