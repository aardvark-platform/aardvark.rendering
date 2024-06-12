namespace Aardvark.Rendering.Tests.Rendering

open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.Application
open FSharp.Data.Adaptive
open Expecto
open FShade

module Surfaces =

    module Cases =

        let private glDynamicShaderCaching (signature: IFramebufferSignature) (surface: Surface) (runtime: GL.Runtime) =
            use __ = runtime.Context.ResourceLock
            let _, p1 = runtime.ResourceManager.CreateSurface(signature, surface, IndexedGeometryMode.TriangleList)
            let _, p2 = runtime.ResourceManager.CreateSurface(signature, surface, IndexedGeometryMode.TriangleList)
            Expect.isTrue (obj.ReferenceEquals(p1, p2)) "Not reference equal"

        let private vkDynamicShaderCaching (pass: Vulkan.RenderPass) (surface: Surface) (runtime: Vulkan.Runtime) =
            let _, p1 = runtime.ResourceManager.CreateShaderProgram(pass, surface, IndexedGeometryMode.TriangleList)
            let _, p2 = runtime.ResourceManager.CreateShaderProgram(pass, surface, IndexedGeometryMode.TriangleList)

            p1.Acquire()
            p2.Acquire()

            try
                Expect.isTrue (obj.ReferenceEquals(p1, p2)) "Not reference equal"
            finally
                p1.Release()
                p2.Release()

        let dynamicShaderCaching (runtime: IRuntime) =
            use signature =
                runtime.CreateFramebufferSignature([
                    DefaultSemantic.Colors, TextureFormat.Rgba8
                ])

            let surface =
                let effect =
                    Effect.compose [
                        Effects.Trafo.Effect
                        Effects.VertexColor.Effect
                    ]

                Surface.effectPool [| effect |] (AVal.init 0)

            match runtime, signature with
            | :? GL.Runtime as r, _ -> glDynamicShaderCaching signature surface r
            | :? Vulkan.Runtime as r, (:? Vulkan.RenderPass as p) -> vkDynamicShaderCaching p surface r
            | _ -> failwith "Unknown backend"

        let equality (_: IRuntime) =
            let getEffect() =
                Effect.compose [
                    toEffect DefaultSurfaces.trafo
                    toEffect DefaultSurfaces.vertexColor
                ]

            let s1 = Surface.Effect <| getEffect()
            let s2 = Surface.Effect <| getEffect()
            Expect.equal s1 s2 "Surface.Effect not equal"

            let effect = getEffect()

            let compile =
                System.Func<_, _, _>(
                    fun (signature: IFramebufferSignature) (topology: IndexedGeometryMode) ->
                        let module_ = Effect.link signature topology false effect
                        EffectInputLayout.ofModule module_, AVal.constant module_
                )

            let s1 = Surface.Dynamic compile
            let s2 = Surface.Dynamic compile
            Expect.equal s1 s2 "Surface.Dynamic not equal"

    let tests (backend : Backend) =
        [
            "Dynamic shader caching",   Cases.dynamicShaderCaching
            "Equality",                 Cases.equality
        ]
        |> prepareCases backend "Surfaces"