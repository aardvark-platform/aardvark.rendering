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
    open FSharp.Quotations

    module private AttributeShader =
        type private Vertex<'T> = { [<Color>] c : 'T }

        let private shader<'T> (v : Vertex<'T>) = fragment { return v.c }
        let Effect<'T> = toEffect (shader<'T>)

        let private shaderWithView<'T1, 'T2> (view : Expr<'T1 -> 'T2>) (v : Vertex<'T1>) = fragment { return (%view) v.c }
        let EffectWithView<'T1, 'T2> (view : Expr<'T1 -> 'T2>) = toEffect (shaderWithView view)

    module Cases =

        let inline private renderAttribute< ^Color, ^Prim when ^Color : unmanaged and ^Prim : equality>
            (effect : Effect) (format : TextureFormat)
            (color : ^Color) (expected : ^Prim[])
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
                let result = output.GetValue().Download().AsPixImage< ^Prim>()
                result |> PixImage.isColor expected
            finally
                output.Release()

        let attributeFloat32 (instanceAttr : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<float>
                TextureFormat.R32f 43.3f [| 43.3f |]
                instanceAttr runtime

        let attributeInt8 (instanceAttr : bool) (runtime : IRuntime) =
            renderAttribute
                (AttributeShader.EffectWithView<int8, int32> <@ int32 @>)
                TextureFormat.R8i -101y [| -101y |]
                instanceAttr runtime

        let attributeInt16 (instanceAttr : bool) (runtime : IRuntime) =
            renderAttribute
                (AttributeShader.EffectWithView<int16, int32> <@ int32 @>)
                TextureFormat.R16i -24235s [| -24235s |]
                instanceAttr runtime

        let attributeInt32 (instanceAttr : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<int32>
                TextureFormat.R32i -1689543 [| -1689543 |]
                instanceAttr runtime

        let attributeUInt8 (instanceAttr : bool) (runtime : IRuntime) =
            renderAttribute
                (AttributeShader.EffectWithView<uint8, uint32> <@ uint32 @>)
                TextureFormat.R8ui 101uy [| 101uy |]
                instanceAttr runtime

        let attributeUInt16 (instanceAttr : bool) (runtime : IRuntime) =
            renderAttribute
                (AttributeShader.EffectWithView<uint16, uint32> <@ uint32 @>)
                TextureFormat.R16ui 24235us [| 24235us |]
                instanceAttr runtime

        let attributeUInt32 (instanceAttr : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<uint32>
                TextureFormat.R32ui 1689543u [| 1689543u |]
                instanceAttr runtime

        let attributeV2f (instanceAttr : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<V2d> TextureFormat.Rg32f
                (V2d(424.0f, 22381.0f)) [| 424.0f; 22381.0f |]
                instanceAttr runtime

        let attributeV3f (instanceAttr : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<V3d> TextureFormat.Rgba32f
                C3f.DeepSkyBlue (C3f.DeepSkyBlue.ToArray())
                instanceAttr runtime

        let attributeV4f (instanceAttr : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<V4d> TextureFormat.Rgba32f
                C4f.DeepSkyBlue (C4f.DeepSkyBlue.ToArray())
                instanceAttr runtime

        let attributeV2i (instanceAttr : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<V2i> TextureFormat.Rg32i
                (V2i(-42, 31)) [| -42; 31 |]
                instanceAttr runtime

        let attributeV3i (instanceAttr : bool) (runtime : IRuntime) =
            renderAttribute
                (AttributeShader.EffectWithView<V3i, V4i> <@ V4i @>)
                TextureFormat.Rgba32i
                (V3i(-42, 31, 8)) [| -42; 31; 8 |]
                instanceAttr runtime

        let attributeV4i (instanceAttr : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<V4i> TextureFormat.Rgba32i
                (V4i(-42, 31, 8, 1001)) [| -42; 31; 8; 1001 |]
                instanceAttr runtime

        let attributeV2ui (instanceAttr : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<V2ui> TextureFormat.Rg32ui
                (V2ui(43u, 24324u)) [| 43u; 24324u |]
                instanceAttr runtime

        let attributeV3ui (instanceAttr : bool) (runtime : IRuntime) =
            renderAttribute
                (AttributeShader.EffectWithView<V3ui, V4ui> <@ V4ui @>)
                TextureFormat.Rgba32ui
                (V3ui(43u, 132u, 24324u)) [| 43u; 132u; 24324u |]
                instanceAttr runtime

        let attributeV4ui (instanceAttr : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<V4ui> TextureFormat.Rgba32ui
                C4ui.Gray (C4ui.Gray.ToArray())
                instanceAttr runtime


    let tests (backend : Backend) =
        [
            "Vertex attribute float32",     Cases.attributeFloat32 false
            "Instance attribute float32",   Cases.attributeFloat32 true

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