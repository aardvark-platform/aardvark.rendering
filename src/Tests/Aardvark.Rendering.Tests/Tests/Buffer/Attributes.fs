namespace Aardvark.Rendering.Tests.Buffer

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Aardvark.Application
open FSharp.Data.Adaptive
open FShade
open Expecto

module AttributeBuffer =
    open FSharp.Quotations
    open TypeInfo

    module private AttributeShader =
        type private Vertex<'T> = { [<Color>] c : 'T }

        let private shader<'T> (v : Vertex<'T>) = fragment { return v.c }
        let Effect<'T> = toEffect (shader<'T>)

        let private shaderWithView<'T1, 'T2> (view : Expr<'T1 -> 'T2>) (v : Vertex<'T1>) = fragment { return (%view) v.c }
        let EffectWithView<'T1, 'T2> (view : Expr<'T1 -> 'T2>) = toEffect (shaderWithView view)

    module Cases =

        let inline private renderAttribute< ^Color, ^Prim when ^Color : unmanaged and ^Color : struct and ^Prim : equality>
            (effect : Effect) (format : TextureFormat)
            (color : ^Color) (expected : ^Prim[])
            (perInstance : bool) (singleValue : bool)
            (runtime : IRuntime) =

            use signature =
                runtime.CreateFramebufferSignature([
                    DefaultSemantic.Colors, format
                ])

            let applyAttribute =
                if perInstance then
                    if singleValue then
                        Sg.instanceBufferValue' DefaultSemantic.Colors color
                    else
                        Sg.instanceAttribute' DefaultSemantic.Colors [| color |]
                else
                    if singleValue then
                        Sg.vertexBufferValue' DefaultSemantic.Colors color
                    else
                        Sg.vertexAttribute' DefaultSemantic.Colors (Array.replicate 4 color)

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

                match typeof< ^Prim> with
                | Float32 ->
                    let expected = unbox<float32[]> expected
                    let result = result.AsPixImage<float32>()
                    result |> PixImage.isColor32f Accuracy.medium expected

                | Float64 ->
                    let expected = unbox<float[]> expected
                    let result = result.AsPixImage<float>()
                    result |> PixImage.isColor64f Accuracy.medium expected

                | _ ->
                    result |> PixImage.isColor expected
            finally
                output.Release()

        let attributeFloat32 (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<float>
                TextureFormat.R32f 43.3f [| 43.3f |]
                perInstance singleValue runtime

        let attributeFloat32FromV4f (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<float>
                TextureFormat.R32f (V4f(43.3f, 0.0f, 0.0f, 0.0f)) [| 43.3f |]
                perInstance singleValue runtime

        let attributeInt8 (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                (AttributeShader.EffectWithView<int8, int32> <@ int32 @>)
                TextureFormat.R8i -101y [| -101y |]
                perInstance singleValue runtime

        let attributeInt16 (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                (AttributeShader.EffectWithView<int16, int32> <@ int32 @>)
                TextureFormat.R16i -24235s [| -24235s |]
                perInstance singleValue runtime

        let attributeInt32 (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<int32>
                TextureFormat.R32i -1689543 [| -1689543 |]
                perInstance singleValue runtime

        let attributeUInt8 (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                (AttributeShader.EffectWithView<uint8, uint32> <@ uint32 @>)
                TextureFormat.R8ui 101uy [| 101uy |]
                perInstance singleValue runtime

        // Treat color as integer and take care of BGRA layout (not possible in GL unless single value)
        let attributeUInt8FromC4b (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                (AttributeShader.EffectWithView<uint8, uint32> <@ uint32 @>)
                TextureFormat.R8ui (C4b(101uy, 1uy, 2uy, 3uy)) [| 101uy |]
                perInstance singleValue runtime

        let attributeUInt16 (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                (AttributeShader.EffectWithView<uint16, uint32> <@ uint32 @>)
                TextureFormat.R16ui 24235us [| 24235us |]
                perInstance singleValue runtime

        // Treat color as integer
        let attributeUInt16FromC3us (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                (AttributeShader.EffectWithView<uint16, uint32> <@ uint32 @>)
                TextureFormat.R16ui C3us.BurlyWood [| C3us.BurlyWood.R |]
                perInstance singleValue runtime

        let attributeUInt32 (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<uint32>
                TextureFormat.R32ui 1689543u [| 1689543u |]
                perInstance singleValue runtime

        // Treat color as integer
        let attributeUInt32FromC3ui (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<uint32>
                TextureFormat.R32ui C3ui.BurlyWood [| C3ui.BurlyWood.R |]
                perInstance singleValue runtime

        let attributeV2f (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<V2d> TextureFormat.Rg32f
                (V2f(424.0f, 22381.0f)) [| 424.0f; 22381.0f |]
                perInstance singleValue runtime

        // Double to float conversion (Only GL)
        let attributeV3fFromV3d (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<V3d> TextureFormat.Rgba32f
                (V3d(424.0f, 22381.0f, -234.4f)) [| 424.0f; 22381.0f; -234.4f |]
                perInstance singleValue runtime

        // Treat color as normalized float
        let attributeV3fFromC4bNorm (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<V3d> TextureFormat.Rgba32f
                C4b.BurlyWood (C4b.BurlyWood.ToC4f().ToArray())
                perInstance singleValue runtime

        // Treat color as normalized float
        let attributeV3fFromC3usNorm (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<V3d> TextureFormat.Rgba32f
                C3us.BurlyWood (C3us.BurlyWood.ToC4f().ToArray())
                perInstance singleValue runtime

        // Treat color as normalized float
        let attributeV3fFromC3uiNorm (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<V3d> TextureFormat.Rgba32f
                C3ui.BurlyWood (C3ui.BurlyWood.ToC4f().ToArray())
                perInstance singleValue runtime

        // Integer as float bits
        let attributeV3fFromV3uiBits (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            let value = V3ui(120232u, 42328u, 232327u)
            let floatBits = value.ToArray() |> Array.map Fun.FloatFromUnsignedBits

            renderAttribute
                AttributeShader.Effect<V3d> TextureFormat.Rgba32f
                value floatBits
                perInstance singleValue runtime

        // Integer as float bits
        let attributeV3fFromV3iBits (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            let value = V3i(120232, 42328, 232327)
            let floatBits = value.ToArray() |> Array.map Fun.FloatFromBits

            renderAttribute
                AttributeShader.Effect<V3d> TextureFormat.Rgba32f
                value floatBits
                perInstance singleValue runtime

        let attributeV4f (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<V4d> TextureFormat.Rgba32f
                C4f.DeepSkyBlue (C4f.DeepSkyBlue.ToArray())
                perInstance singleValue runtime

        let attributeV2i (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<V2i> TextureFormat.Rg32i
                (V2i(-42, 31)) [| -42; 31 |]
                perInstance singleValue runtime

        let attributeV3i (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                (AttributeShader.EffectWithView<V3i, V4i> <@ V4i @>)
                TextureFormat.Rgba32i
                (V3i(-42, 31, 8)) [| -42; 31; 8 |]
                perInstance singleValue runtime

        // V3f interpreted as V3i bits
        let attributeV3iFromV3fBits (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            let value = V3f(2323.0f, -1242.0f, 138913.0f)
            let bits = value.ToArray() |> Array.map Fun.FloatToBits

            renderAttribute
                (AttributeShader.EffectWithView<V3i, V4i> <@ V4i @>)
                TextureFormat.Rgba32i value bits
                perInstance singleValue runtime

        let attributeV4i (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<V4i> TextureFormat.Rgba32i
                (V4i(-42, 31, 8, 1001)) [| -42; 31; 8; 1001 |]
                perInstance singleValue runtime

        let attributeV2ui (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<V2ui> TextureFormat.Rg32ui
                (V2ui(43u, 24324u)) [| 43u; 24324u |]
                perInstance singleValue runtime

        let attributeV3ui (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                (AttributeShader.EffectWithView<V3ui, V4ui> <@ V4ui @>)
                TextureFormat.Rgba32ui
                (V3ui(43u, 132u, 24324u)) [| 43u; 132u; 24324u |]
                perInstance singleValue runtime

        // V3f interpreted as V3ui bits
        let attributeV3uiFromV3fBits (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            let value = V3f(2323.0f, -1242.0f, 138913.0f)
            let bits = value.ToArray() |> Array.map Fun.FloatToUnsignedBits

            renderAttribute
                (AttributeShader.EffectWithView<V3ui, V4ui> <@ V4ui @>)
                TextureFormat.Rgba32ui value bits
                perInstance singleValue runtime

        let attributeV4ui (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<V4ui> TextureFormat.Rgba32ui
                C4ui.Gray (C4ui.Gray.ToArray())
                perInstance singleValue runtime

        let attributeM23f (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            let value = M23f(Array.init 6 (id >> float32))
            let view = <@ fun (m : M23d) -> V3d(m.[0, 1], m.[1, 0], m.[1, 2]) @>

            renderAttribute
                (AttributeShader.EffectWithView<M23d, V3d> view)
                TextureFormat.Rgba32f
                value [| 1.0f; 3.0f; 5.0f |]
                perInstance singleValue runtime

        let attributeM34f (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            let value = M34f(Array.init 12 (id >> float32))
            let view = <@ fun (m : M34d) -> V3d(m.[0, 1], m.[1, 0], m.[2, 3]) @>

            renderAttribute
                (AttributeShader.EffectWithView<M34d, V3d> view)
                TextureFormat.Rgba32f
                value [| 1.0f; 4.0f; 11.0f |]
                perInstance singleValue runtime

        let attributeM44f (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            let value = M44f(Array.init 16 (id >> float32))
            let view = <@ fun (m : M44d) -> V4d(m.[0, 1], m.[1, 0], m.[2, 3], m.[3, 2]) @>

            renderAttribute
                (AttributeShader.EffectWithView<M44d, V4d> view)
                TextureFormat.Rgba32f
                value [| 1.0f; 4.0f; 11.0f; 14.0f |]
                perInstance singleValue runtime

        let all = [
            "float32",                   attributeFloat32
            "float32 from V4f",          attributeFloat32FromV4f
            "int8",                      attributeInt8
            "uint8",                     attributeUInt8
            "uint8 from C4b",            attributeUInt8FromC4b
            "int16",                     attributeInt16
            "uint16",                    attributeUInt16
            "uint16 from C3us",          attributeUInt16FromC3us
            "int32",                     attributeInt32
            "uint32",                    attributeUInt32
            "uint32 from C3ui",          attributeUInt32FromC3ui
            "V2f",                       attributeV2f
            "V3f from V3d",              attributeV3fFromV3d
            "V3f from C4b normalized",   attributeV3fFromC4bNorm
            "V3f from C3us normalized",  attributeV3fFromC3usNorm
            "V3f from C3ui normalized",  attributeV3fFromC3uiNorm
            "V3f from V3ui bits",        attributeV3fFromV3uiBits
            "V3f from V3i bits",         attributeV3fFromV3iBits
            "V4f",                       attributeV4f
            "V2i",                       attributeV2i
            "V3i",                       attributeV3i
            "V3i from V3f bits",         attributeV3iFromV3fBits
            "V4i",                       attributeV4i
            "V2ui",                      attributeV2ui
            "V3ui",                      attributeV3ui
            "V3ui from V3f bits",        attributeV3uiFromV3fBits
            "V4ui",                      attributeV4ui
            "M23f",                      attributeM23f
            "M34f",                      attributeM34f
            "M44f",                      attributeM44f
        ]

    let tests (backend : Backend) =
        [
            for perInstance in [false; true] do
                for singleValue in [false; true] do
                    for name, case in Cases.all do

                        let desc =
                            match perInstance, singleValue with
                            | true, true -> "Single instance attribute"
                            | false, true -> "Single vertex attribute"
                            | true, false -> "Instance attribute"
                            | false, false -> "Vertex attribute"

                        // C4b has BGRA layout, so we need to use the special
                        // GL_BGRA constant for buffers. Unfortunately, this
                        // only works for normalized float attributes.
                        // For single values we just fix the layout ourselves.
                        if backend = Backend.GL && name = "uint8 from C4b" && not singleValue then
                            ()

                        // Vulkan does not support converting double to float on-the-fly.
                        // Not really a good idea anyway :)
                        elif backend = Backend.Vulkan && name = "V3f from V3d" then
                            ()

                        // Vulkan does not have normalized 32bit formats (e.g. there is no VK_FORMAT_R32G32B32_UNORM)
                        // See: https://registry.khronos.org/vulkan/specs/1.3-extensions/man/html/VkFormat.html
                        elif backend = Backend.Vulkan && name = "V3f from C3ui normalized" then
                            ()

                        else
                            yield (desc + " " + name), case perInstance singleValue
        ]
        |> prepareCases backend "Attributes"