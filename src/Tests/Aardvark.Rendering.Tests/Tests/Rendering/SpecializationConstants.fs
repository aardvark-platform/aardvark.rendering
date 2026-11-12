namespace Aardvark.Rendering.Tests.Rendering

open System.Threading
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open Expecto

module SpecializationConstants =

    module private Shader =
        open FShade

        type UniformScope with
            member x.ConstantInt    : int     = uniform?SpecializationConstant?ConstantInt
            member x.ConstantUInt   : uint32  = uniform?SpecializationConstant?ConstantUInt
            member x.ConstantFloat  : float32 = uniform?SpecializationConstant?ConstantFloat
            member x.ConstantBool   : bool    = uniform?SpecializationConstant?ConstantBool
            member x.ConstantDouble : float   = uniform?SpecializationConstant?ConstantDouble

        let scalars (v : Effects.Vertex) =
            fragment {
                let flag = if uniform.ConstantBool then 1.0f else 0.0f
                return V4f(float32 uniform.ConstantInt, float32 uniform.ConstantUInt, uniform.ConstantFloat, flag)
            }

        let double (v : Effects.Vertex) =
            fragment {
                return V4f(float32 uniform.ConstantDouble, 0.0f, 0.0f, 1.0f)
            }

    module private Cases =

        let private close (expected : C4f) (actual : C4f) =
            abs (expected.R - actual.R) < 1e-4f && abs (expected.G - actual.G) < 1e-4f &&
            abs (expected.B - actual.B) < 1e-4f && abs (expected.A - actual.A) < 1e-4f

        /// Renders the scene to a 16x16 target and passes an expectation to the
        /// action. On Vulkan the first frames use the unspecialized pipeline while
        /// the specialized one compiles in the background, so the expectation polls
        /// the output until it matches or a timeout expires.
        let private render (runtime : IRuntime) (sg : ISg) (action : (C4f -> unit) -> unit) =
            use signature =
                runtime.CreateFramebufferSignature([
                    DefaultSemantic.Colors, TextureFormat.Rgba32f
                ])

            use task = sg |> Sg.compile runtime signature

            let output =
                task |> RenderTask.renderToColorWithClear (~~V2i(16)) (clear { color C4f.Black })

            output.Acquire()

            try
                action (fun expected ->
                    let download () = output.GetValue().Download().AsPixImage<float32>()

                    let mutable result = download()
                    let mutable attempts = 0

                    while not (close expected (result.GetMatrix<C4f>().[0, 0])) && attempts < 500 do
                        Thread.Sleep 10
                        attempts <- attempts + 1
                        result <- download()

                    result |> PixImage.isColor32f Accuracy.medium (expected.ToArray())
                )
            finally
                output.Release()

        let valuesFromUniforms (runtime : IRuntime) =
            let sg =
                Sg.fullScreenQuad
                |> Sg.shader { do! Shader.scalars }
                |> Sg.uniform' "ConstantInt" -3
                |> Sg.uniform' "ConstantUInt" 7u
                |> Sg.uniform' "ConstantFloat" 2.5f
                |> Sg.uniform' "ConstantBool" true

            render runtime sg (fun expect ->
                expect (C4f(-3.0f, 7.0f, 2.5f, 1.0f))
            )

        let defaults (runtime : IRuntime) =
            let sg =
                Sg.fullScreenQuad
                |> Sg.shader { do! Shader.scalars }

            render runtime sg (fun expect ->
                expect (C4f(0.0f, 0.0f, 0.0f, 0.0f))
            )

        let partiallySet (runtime : IRuntime) =
            let sg =
                Sg.fullScreenQuad
                |> Sg.shader { do! Shader.scalars }
                |> Sg.uniform' "ConstantFloat" 2.5f

            render runtime sg (fun expect ->
                expect (C4f(0.0f, 0.0f, 2.5f, 0.0f))
            )

        let changedValue (runtime : IRuntime) =
            let value = cval 1

            let sg =
                Sg.fullScreenQuad
                |> Sg.shader { do! Shader.scalars }
                |> Sg.uniform "ConstantInt" value

            render runtime sg (fun expect ->
                expect (C4f(1.0f, 0.0f, 0.0f, 0.0f))
                transact (fun () -> value.Value <- 5)
                expect (C4f(5.0f, 0.0f, 0.0f, 0.0f))
            )

        let double (runtime : IRuntime) =
            if not runtime.SupportsShaderFloat64 then
                Tests.skiptest "shader doubles are not supported"

            let sg =
                Sg.fullScreenQuad
                |> Sg.shader { do! Shader.double }
                |> Sg.uniform' "ConstantDouble" 0.75

            render runtime sg (fun expect ->
                expect (C4f(0.75f, 0.0f, 0.0f, 1.0f))
            )

        let mismatchedType (runtime : IRuntime) =
            let sg =
                Sg.fullScreenQuad
                |> Sg.shader { do! Shader.scalars }
                |> Sg.uniform' "ConstantInt" "text"

            Expect.throws
                (fun () -> render runtime sg (fun expect -> expect (C4f(0.0f, 0.0f, 0.0f, 0.0f))))
                "a value that cannot be written as the constant's type must fail"

    let tests (target : TestTarget) =
        [
            "Values from uniforms",           Cases.valuesFromUniforms
            "Unset constants keep defaults",  Cases.defaults
            "Partially set",                  Cases.partiallySet
            "Changed value",                  Cases.changedValue
            "Double",                         Cases.double
            "Mismatched type",                Cases.mismatchedType
        ]
        |> prepareCasesGpu "SpecializationConstants" target
