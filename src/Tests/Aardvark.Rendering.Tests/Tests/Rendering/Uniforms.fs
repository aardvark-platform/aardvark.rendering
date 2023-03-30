namespace Aardvark.Rendering.Tests.Rendering

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Aardvark.Application
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open Expecto

module Uniforms =

    module private Shader =
        open FShade

        type UniformScope with
            member x.M22d : M22d             = x?MyUniform
            member x.M23d : M23d             = x?MyUniform
            member x.M33d : M33d             = x?MyUniform
            member x.V2dArr : Arr<N<2>, V2d> = x?MyUniform
            member x.V3dArr : Arr<N<2>, V3d> = x?MyUniform

        let m22d (v : Effects.Vertex) =
            fragment {
                return V4d(uniform.M22d.R0, uniform.M22d.R1)
            }

        let m23d (v : Effects.Vertex) =
            fragment {
                return uniform.M23d.R0 + uniform.M23d.R1
            }

        let m33d (v : Effects.Vertex) =
            fragment {
                return uniform.M33d.R0 + uniform.M33d.R1 + uniform.M33d.R2
            }

        let v2dArr (v : Effects.Vertex) =
            fragment {
                return V4d(uniform.V2dArr.[0], uniform.V2dArr.[1])
            }

        let v3dArr (v : Effects.Vertex) =
            fragment {
                return uniform.V3dArr.[0] + uniform.V3dArr.[1]
            }

    module Cases =

        let private render (effect : FShade.Effect) (uniform : 'T) (expected : float32[]) (runtime : IRuntime) =
            use signature =
                runtime.CreateFramebufferSignature([
                    DefaultSemantic.Colors, TextureFormat.Rgba32f
                ])

            use task =
                Sg.fullScreenQuad
                |> Sg.uniform' "MyUniform" uniform
                |> Sg.effect [effect]
                |> Sg.compile runtime signature

            let output = task |> RenderTask.renderToColor (~~V2i(32))
            output.Acquire()

            try
                let pi = output.GetValue().Download().AsPixImage<float32>()
                pi |> PixImage.isColor32f Accuracy.veryHigh expected
            finally
                output.Release()

        let private m22 (create : float32[] -> 'T) : IRuntime -> unit =
            let values = [| 1.0f; 2.0f; 3.0f; 4.0f |]
            render (toEffect Shader.m22d) (create values) values

        let private m23 (create : float32[] -> 'T) : IRuntime -> unit =
            let values = Array.init 6 float32
            let expected = Array.init 3 (fun i -> values[i] + values.[i + 3])
            render (toEffect Shader.m23d) (create values) expected

        let private m33 (create : float32[] -> 'T) : IRuntime -> unit =
            let values = Array.init 9 float32
            let expected = Array.init 3 (fun i -> values[i] + values.[i + 3] + values.[i + 6])
            render (toEffect Shader.m33d) (create values) expected

        let private v2Array (create : V2f[] -> 'T) : IRuntime -> unit =
            let values = [| V2f(1.0f, 2.0f); V2f(3.0f, 4.0f) |]
            let expected = V4f(values.[0], values.[1])
            render (toEffect Shader.v2dArr) (create values) (expected.ToArray())

        let private v3Array (create : V3f[] -> 'T) : IRuntime -> unit =
            let values = [| V3f(1.0f, 2.0f, 3.0f); V3f(4.0f, 5.0f, 6.0f) |]
            let expected = values.[0] + values.[1]
            render (toEffect Shader.v3dArr) (create values) (expected.ToArray())

        let m22f : IRuntime -> unit = m22 M22f
        let m22d : IRuntime -> unit = m22 (Array.map float >> M22d)
        let m23f : IRuntime -> unit = m23 M23f
        let m23d : IRuntime -> unit = m23 (Array.map float >> M23d)
        let m33f : IRuntime -> unit = m33 M33f
        let m33d : IRuntime -> unit = m33 (Array.map float >> M33d)

        let v2fArray : IRuntime -> unit = v2Array id
        let v2dArray : IRuntime -> unit = v2Array (Array.map V2d)
        let v3fArray : IRuntime -> unit = v3Array id
        let v3dArray : IRuntime -> unit = v3Array (Array.map V3d)

    let tests (backend : Backend) =
        [
            "M22f",      Cases.m22f
            "M22d",      Cases.m22d
            "M23f",      Cases.m23f
            "M23d",      Cases.m23d
            "M33f",      Cases.m33f
            "M33d",      Cases.m33d

            "V2f array", Cases.v2fArray
            "V2d array", Cases.v2dArray
            "V3f array", Cases.v3fArray
            "V3d array", Cases.v3dArray
        ]
        |> prepareCases backend "Uniforms"
