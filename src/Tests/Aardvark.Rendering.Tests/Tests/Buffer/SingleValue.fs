namespace Aardvark.Rendering.Tests.Buffer

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Effects
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Aardvark.Application
open FSharp.Data.Adaptive
open FShade
open Expecto
open System

module SingleValueBuffer =

    module private AttributeShader =
        type private Vertex<'T> = { [<Color>] c : 'T }
        let private shader<'T> (v : Vertex<'T>) = fragment { return v.c }
        let Effect<'T> = toEffect (shader<'T>)

        let private shaderV3iToV4i (v : Vertex<V3i>) = fragment { return v.c.XYZO }
        let V3iToV4i = toEffect shaderV3iToV4i

        let private shaderV3uiToV4ui (v : Vertex<V3ui>) = fragment { return v.c.XYZO }
        let V3uiToV4ui = toEffect shaderV3uiToV4ui

    module Cases =

        let private renderFullscreenQuadWithAttribute
                        (effect : Effect)
                        (format : TextureFormat)
                        (color : 'cT)
                        (expected : 'fT[])
                        (instanceAttr : bool)
                        (runtime : IRuntime) =

            use signature =
                runtime.CreateFramebufferSignature([
                    DefaultSemantic.Colors, format
                ])

            let applyAttribute =
                if instanceAttr then
                    Sg.instanceBufferValue' DefaultSemantic.Colors color
                else
                    Sg.vertexBufferValue' DefaultSemantic.Colors color

            use task =
                DrawCallInfo 4
                |> Sg.render IndexedGeometryMode.TriangleStrip
                |> Sg.vertexArray DefaultSemantic.Positions [| V3f(-1,-1,1); V3f(1,-1,1); V3f(-1,1,1); V3f(1,1,1) |]
                |> applyAttribute
                |> Sg.effect [effect]
                |> Sg.compile runtime signature

            let output = task |> RenderTask.renderToColor (AVal.constant <| V2i(14, 19))
            output.Acquire()

            try
                let result = output.GetValue().Download().AsPixImage<'fT>()
                result |> PixImage.isColor expected
            finally
                output.Release()


        let simpleAttribute<'T when 'T : unmanaged and 'T : equality> (format : TextureFormat) (value : 'T) =
            renderFullscreenQuadWithAttribute AttributeShader.Effect<'T> format value [| value |]

        let attributeFloat32 =
            renderFullscreenQuadWithAttribute AttributeShader.Effect<float> TextureFormat.R32f 43.3f [| 43.3f |]

        let attributeInt8 = simpleAttribute TextureFormat.R8i -101y
        let attributeInt16 = simpleAttribute TextureFormat.R16i -24235s
        let attributeInt32 = simpleAttribute TextureFormat.R32i -1689543

        let attributeUInt8 = simpleAttribute TextureFormat.R8ui 101uy
        let attributeUInt16 = simpleAttribute TextureFormat.R16ui 24235us
        let attributeUInt32 = simpleAttribute TextureFormat.R32ui 1689543u

        let attributeV2f =
            renderFullscreenQuadWithAttribute
                AttributeShader.Effect<V2d> TextureFormat.Rg32f
                (V2f(424.0f, 22381.0f)) [| 424.0f; 22381.0f |]

        let attributeV3f =
            renderFullscreenQuadWithAttribute
                AttributeShader.Effect<V3d> TextureFormat.Rgba32f
                C3f.DeepSkyBlue (C3f.DeepSkyBlue.ToArray())

        let attributeV4f =
            renderFullscreenQuadWithAttribute
                AttributeShader.Effect<V4d> TextureFormat.Rgba32f
                C4f.DeepSkyBlue (C4f.DeepSkyBlue.ToArray())

        let attributeV2i =
            renderFullscreenQuadWithAttribute
                AttributeShader.Effect<V2i> TextureFormat.Rg32i
                (V2i(-42, 31)) [| -42; 31 |]

        let attributeV3i =
            renderFullscreenQuadWithAttribute
                AttributeShader.V3iToV4i TextureFormat.Rgba32i
                (V3i(-42, 31, 8)) [| -42; 31; 8 |]

        let attributeV4i =
            renderFullscreenQuadWithAttribute
                AttributeShader.Effect<V4i> TextureFormat.Rgba32i
                (V4i(-42, 31, 8, 1001)) [| -42; 31; 8; 1001 |]

        let attributeV2ui =
            renderFullscreenQuadWithAttribute
                AttributeShader.Effect<V2ui> TextureFormat.Rg32ui
                (V2ui(43u, 24324u)) [| 43u; 24324u |]

        let attributeV3ui =
            renderFullscreenQuadWithAttribute
                AttributeShader.V3uiToV4ui TextureFormat.Rgba32ui
                (V3ui(43u, 132u, 24324u)) [| 43u; 132u; 24324u |]

        let attributeV4ui =
            renderFullscreenQuadWithAttribute
                AttributeShader.Effect<V4ui> TextureFormat.Rgba32ui
                C4ui.Gray (C4ui.Gray.ToArray())


    let tests (backend : Backend) =
        [
            "Vertex attribute float32",     Cases.attributeFloat32 false
            "Instance attribute float32",   Cases.attributeFloat32 true

            // Not supported by FShade
            if false then
                "Vertex attribute int8",     Cases.attributeInt8 false
                "Instance attribute int8",   Cases.attributeInt8 true

                "Vertex attribute uint8",    Cases.attributeUInt8 false
                "Instance attribute uint8",  Cases.attributeUInt8 true

                "Vertex attribute int16",    Cases.attributeInt16 false
                "Instance attribute int16",  Cases.attributeInt16 true

                "Vertex attribute uint16",   Cases.attributeUInt16 false
                "Instance attribute uint16", Cases.attributeUInt16 true

            "Vertex attribute int32",    Cases.attributeInt32 false
            "Instance attribute int32",  Cases.attributeInt32 true

            "Vertex attribute uint32",   Cases.attributeUInt32 false
            "Instance attribute uint32", Cases.attributeUInt32 true

            "Vertex attribute V2f",      Cases.attributeV2f false
            "Instance attribute V2f",    Cases.attributeV2f true

            "Vertex attribute V3f",      Cases.attributeV3f false
            "Instance attribute V3f",    Cases.attributeV3f true

            "Vertex attribute V4f",      Cases.attributeV4f false
            "Instance attribute V4f",    Cases.attributeV4f true

            "Vertex attribute V2i",      Cases.attributeV2i false
            "Instance attribute V2i",    Cases.attributeV2i true

            "Vertex attribute V3i",      Cases.attributeV3i false
            "Instance attribute V3i",    Cases.attributeV3i true

            "Vertex attribute V4i",      Cases.attributeV4i false
            "Instance attribute V4i",    Cases.attributeV4i true

            "Vertex attribute V2ui",      Cases.attributeV2ui false
            "Instance attribute V2ui",    Cases.attributeV2ui true

            "Vertex attribute V3ui",      Cases.attributeV3ui false
            "Instance attribute V3ui",    Cases.attributeV3ui true

            "Vertex attribute V4ui",      Cases.attributeV4ui false
            "Instance attribute V4ui",    Cases.attributeV4ui true
        ]
        |> prepareCases backend "Single value"