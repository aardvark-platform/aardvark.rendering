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
    open TypeMeta

    module private AttributeShader =
        type private Vertex<'T> = { [<Color>] c : 'T }

        let private shader<'T> (v : Vertex<'T>) = fragment { return v.c }
        let Effect<'T> = toEffect (shader<'T>)

        let private shaderWithView<'T1, 'T2> (view : Expr<'T1 -> 'T2>) (v : Vertex<'T1>) = fragment { return (%view) v.c }
        let EffectWithView<'T1, 'T2> (view : Expr<'T1 -> 'T2>) = toEffect (shaderWithView view)

    module Cases =

        // Conversions according to Vk / GL >= 4.2 spec
        let inline private unorm (value : 'T) =
            let bits = uint64 sizeof<'T> * 8UL
            float32 value / float32 ((pown 2UL bits) - 1UL)

        let inline private snorm (value : 'T) =
            let bits = uint64 sizeof<'T> * 8UL
            max -1.0f (float32 value / float32 ((pown 2UL (bits - 1UL)) - 1UL))

        type C4b with
            member x.ToC4fExact() = C4f(unorm x.R, unorm x.G, unorm x.B, unorm x.A)

        type C3us with
            member x.ToC3fExact() = C3f(unorm x.R, unorm x.G, unorm x.B)

        type C3ui with
            member x.ToC3fExact() = C3f(unorm x.R, unorm x.G, unorm x.B)

        let inline private renderAttribute< ^Color, ^Prim when ^Color : unmanaged and ^Color : struct and ^Prim : equality>
            (effect : Effect) (format : TextureFormat)
            (color : ^Color) (expected : ^Prim[])
            (perInstance : bool) (singleValue : bool) (normalized : bool)
            (runtime : IRuntime) =

            use signature =
                runtime.CreateFramebufferSignature([
                    DefaultSemantic.Colors, format
                ])

            let applyAttribute =
                let view =
                    if singleValue then
                        BufferView(SingleValueBuffer color, normalized = normalized)
                    else
                        let arr = color |> Array.replicate (if perInstance then 1 else 4)
                        BufferView(arr, normalized = normalized)

                if perInstance then
                    Sg.instanceBuffer DefaultSemantic.Colors view
                else
                    Sg.vertexBuffer DefaultSemantic.Colors view

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

                // GL normalized values are not exact...
                // Even more interesting: the values vary based on whether
                // glVertexAttrib or glVertexAttribPointer is used?
                let fuzzyFloatCheck =
                    normalized && runtime.GetType() = typeof<GL.Runtime>

                match typeof< ^Prim> with
                | Float32 when fuzzyFloatCheck ->
                    let expected = unbox<float32[]> expected
                    let result = result.AsPixImage<float32>()
                    result |> PixImage.isColor32f Accuracy.low expected

                | _ ->
                    result |> PixImage.isColor expected
            finally
                output.Release()

        let attributeFloat32 (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<float32>
                TextureFormat.R32f 43.3f [| 43.3f |]
                perInstance singleValue false runtime

        let attributeFloat32FromV4f (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<float32>
                TextureFormat.R32f (V4f(43.3f, 0.0f, 0.0f, 0.0f)) [| 43.3f |]
                perInstance singleValue false runtime

        let attributeFloat32FromInt8Norm (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<float32>
                TextureFormat.R32f -128y [| snorm -128y |]
                perInstance singleValue true runtime

        let attributeFloat32FromInt8Scaled (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<float32>
                TextureFormat.R32f -32y [| -32.0f |]
                perInstance singleValue false runtime

        let attributeFloat32FromUInt8Norm (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<float32>
                TextureFormat.R32f 123uy [| unorm 123uy |]
                perInstance singleValue true runtime

        let attributeFloat32FromUInt8Scaled (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<float32>
                TextureFormat.R32f 123uy [| 123.0f |]
                perInstance singleValue false runtime

        let attributeFloat32FromInt16Norm (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<float32>
                TextureFormat.R32f -1234s [| snorm -1234s |]
                perInstance singleValue true runtime

        let attributeFloat32FromInt16Scaled (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<float32>
                TextureFormat.R32f -1234s [| -1234.0f |]
                perInstance singleValue false runtime

        let attributeFloat32FromUInt16Norm (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<float32>
                TextureFormat.R32f 12345us [| unorm 12345us |]
                perInstance singleValue true runtime

        let attributeFloat32FromUInt16Scaled (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<float32>
                TextureFormat.R32f 1234us [| 1234.0f |]
                perInstance singleValue false runtime

        let attributeInt8 (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                (AttributeShader.EffectWithView<int8, int32> <@ int32 @>)
                TextureFormat.R8i -101y [| -101y |]
                perInstance singleValue false runtime

        let attributeInt16 (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            runtime |> requireFeatures _.Shaders.StorageInputOutput16 "Device does not support 16bit inputs and outputs"

            renderAttribute
                (AttributeShader.EffectWithView<int16, int32> <@ int32 @>)
                TextureFormat.R16i -24235s [| -24235s |]
                perInstance singleValue false runtime

        let attributeInt32 (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<int32>
                TextureFormat.R32i -1689543 [| -1689543 |]
                perInstance singleValue false runtime

        let attributeUInt8 (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                (AttributeShader.EffectWithView<uint8, uint32> <@ uint32 @>)
                TextureFormat.R8ui 101uy [| 101uy |]
                perInstance singleValue false runtime

        // Treat color as integer and take care of BGRA layout (not possible in GL unless single value)
        let attributeUInt8FromC4b (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                (AttributeShader.EffectWithView<uint8, uint32> <@ uint32 @>)
                TextureFormat.R8ui (C4b(101uy, 1uy, 2uy, 3uy)) [| 101uy |]
                perInstance singleValue false runtime

        let attributeUInt16 (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            runtime |> requireFeatures _.Shaders.StorageInputOutput16 "Device does not support 16-bit inputs and outputs"

            renderAttribute
                (AttributeShader.EffectWithView<uint16, uint32> <@ uint32 @>)
                TextureFormat.R16ui 24235us [| 24235us |]
                perInstance singleValue false runtime

        // Treat color as integer
        let attributeUInt16FromC3us (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            runtime |> requireFeatures _.Shaders.StorageInputOutput16 "Device does not support 16-bit inputs and outputs"

            renderAttribute
                (AttributeShader.EffectWithView<uint16, uint32> <@ uint32 @>)
                TextureFormat.R16ui C3us.BurlyWood [| C3us.BurlyWood.R |]
                perInstance singleValue false runtime

        let attributeUInt32 (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<uint32>
                TextureFormat.R32ui 1689543u [| 1689543u |]
                perInstance singleValue false runtime

        // Treat color as integer
        let attributeUInt32FromC3ui (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<uint32>
                TextureFormat.R32ui C3ui.BurlyWood [| C3ui.BurlyWood.R |]
                perInstance singleValue false runtime

        let attributeV2f (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<V2f> TextureFormat.Rg32f
                (V2f(424.0f, 22381.0f)) [| 424.0f; 22381.0f |]
                perInstance singleValue false runtime

        // Double to float conversion (Only GL)
        let attributeV3fFromV3d (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<V3f> TextureFormat.Rgba32f
                (V3d(424.0f, 22381.0f, -234.4f)) [| 424.0f; 22381.0f; -234.4f |]
                perInstance singleValue false runtime

        // Treat color as normalized float
        let attributeV3fFromC4bNorm (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<V3f> TextureFormat.Rgba32f
                C4b.BurlyWood (C4b.BurlyWood.ToC4fExact().ToArray())
                perInstance singleValue true runtime

        // Treat color as scaled float
        let attributeV3fFromC4bScaled (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<V4f> TextureFormat.Rgba32f
                C4b.BurlyWood (C4b.BurlyWood.ToV4f().ToArray())
                perInstance singleValue false runtime

        // Treat color as normalized float
        let attributeV3fFromC3usNorm (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<V3f> TextureFormat.Rgba32f
                C3us.BurlyWood (C3us.BurlyWood.ToC3fExact().ToArray())
                perInstance singleValue true runtime

        // Treat color as scaled float
        let attributeV3fFromC3usScaled (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<V4f> TextureFormat.Rgba32f
                C4us.BurlyWood (C4us.BurlyWood.ToV4f().ToArray())
                perInstance singleValue false runtime

        // Treat color as normalized float
        let attributeV3fFromC3uiNorm (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<V3f> TextureFormat.Rgba32f
                C3ui.BurlyWood (C3ui.BurlyWood.ToC3fExact().ToArray())
                perInstance singleValue true runtime

        let attributeV4f (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<V4f> TextureFormat.Rgba32f
                C4f.DeepSkyBlue (C4f.DeepSkyBlue.ToArray())
                perInstance singleValue false runtime

        let attributeV2i (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<V2i> TextureFormat.Rg32i
                (V2i(-42, 31)) [| -42; 31 |]
                perInstance singleValue false runtime

        let attributeV3i (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                (AttributeShader.EffectWithView<V3i, V4i> <@ V4i @>)
                TextureFormat.Rgba32i
                (V3i(-42, 31, 8)) [| -42; 31; 8 |]
                perInstance singleValue false runtime

        let attributeV4i (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<V4i> TextureFormat.Rgba32i
                (V4i(-42, 31, 8, 1001)) [| -42; 31; 8; 1001 |]
                perInstance singleValue false runtime

        let attributeV2ui (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<V2ui> TextureFormat.Rg32ui
                (V2ui(43u, 24324u)) [| 43u; 24324u |]
                perInstance singleValue false runtime

        let attributeV3ui (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                (AttributeShader.EffectWithView<V3ui, V4ui> <@ V4ui @>)
                TextureFormat.Rgba32ui
                (V3ui(43u, 132u, 24324u)) [| 43u; 132u; 24324u |]
                perInstance singleValue false runtime

        let attributeV4ui (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            renderAttribute
                AttributeShader.Effect<V4ui> TextureFormat.Rgba32ui
                C4ui.Gray (C4ui.Gray.ToArray())
                perInstance singleValue false runtime

        let attributeM23f (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            let value = M23f(Array.init 6 (id >> float32))
            let view = <@ fun (m : M23f) -> V3f(m.[0, 1], m.[1, 0], m.[1, 2]) @>

            renderAttribute
                (AttributeShader.EffectWithView<M23f, V3f> view)
                TextureFormat.Rgba32f
                value [| 1.0f; 3.0f; 5.0f |]
                perInstance singleValue false runtime

        let attributeM34f (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            let value = M34f(Array.init 12 (id >> float32))
            let view = <@ fun (m : M34f) -> V3f(m.[0, 1], m.[1, 0], m.[2, 3]) @>

            renderAttribute
                (AttributeShader.EffectWithView<M34f, V3f> view)
                TextureFormat.Rgba32f
                value [| 1.0f; 4.0f; 11.0f |]
                perInstance singleValue false runtime

        let attributeM44f (perInstance : bool) (singleValue : bool) (runtime : IRuntime) =
            let value = M44f(Array.init 16 (id >> float32))
            let view = <@ fun (m : M44f) -> V4f(m.[0, 1], m.[1, 0], m.[2, 3], m.[3, 2]) @>

            renderAttribute
                (AttributeShader.EffectWithView<M44f, V4f> view)
                TextureFormat.Rgba32f
                value [| 1.0f; 4.0f; 11.0f; 14.0f |]
                perInstance singleValue false runtime

        let all = [
            "float32",                        attributeFloat32
            "float32 from V4f",               attributeFloat32FromV4f
            "float32 from int8 normalized",   attributeFloat32FromInt8Norm
            "float32 from int8 scaled",       attributeFloat32FromInt8Scaled
            "float32 from uint8 normalized",  attributeFloat32FromUInt8Norm
            "float32 from uint8 scaled",      attributeFloat32FromUInt8Scaled
            "float32 from int16 normalized",  attributeFloat32FromInt16Norm
            "float32 from int16 scaled",      attributeFloat32FromInt16Scaled
            "float32 from uint16 normalized", attributeFloat32FromUInt16Norm
            "float32 from uint16 scaled",     attributeFloat32FromUInt16Scaled
            "int8",                           attributeInt8
            "uint8",                          attributeUInt8
            "uint8 from C4b",                 attributeUInt8FromC4b
            "int16",                          attributeInt16
            "uint16",                         attributeUInt16
            "uint16 from C3us",               attributeUInt16FromC3us
            "int32",                          attributeInt32
            "uint32",                         attributeUInt32
            "uint32 from C3ui",               attributeUInt32FromC3ui
            "V2f",                            attributeV2f
            "V3f from V3d",                   attributeV3fFromV3d
            "V3f from C4b normalized",        attributeV3fFromC4bNorm
            "V3f from C4b scaled",            attributeV3fFromC4bScaled
            "V3f from C3us normalized",       attributeV3fFromC3usNorm
            "V3f from C3us scaled",           attributeV3fFromC3usScaled
            "V3f from C3ui normalized",       attributeV3fFromC3uiNorm
            "V4f",                            attributeV4f
            "V2i",                            attributeV2i
            "V3i",                            attributeV3i
            "V4i",                            attributeV4i
            "V2ui",                           attributeV2ui
            "V3ui",                           attributeV3ui
            "V4ui",                           attributeV4ui
            "M23f",                           attributeM23f
            "M34f",                           attributeM34f
            "M44f",                           attributeM44f
        ]


    type private Mode =
        | Vertex of single: bool
        | Instance

    let tests (backend : Backend) =
        [
            for mode in [Vertex false; Vertex true; Instance] do
                for name, case in Cases.all do

                let perInstance, singleValue =
                    match mode with
                    | Vertex single -> false, single
                    | Instance -> true, false

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
                if backend = Backend.GL && (name = "uint8 from C4b" || name = "V3f from C4b scaled") && not singleValue then
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