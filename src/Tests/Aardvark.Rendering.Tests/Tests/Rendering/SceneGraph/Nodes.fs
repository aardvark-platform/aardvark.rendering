namespace Aardvark.Rendering.Tests.Rendering.SceneGraph

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open FSharp.Data.Adaptive
open Expecto
open FShade

module Nodes =

    module private Cases =
        let private testOnActivation (runtime: IRuntime) (countAfterPrepare : int) (countAfterDispose : int) (sgWithActivator : (ISg -> ISg) -> ISg) =
            use signature =
                runtime.CreateFramebufferSignature [
                    DefaultSemantic.Colors, TextureFormat.Rgba8
                ]

            let mutable count = 0

            let sg =
                sgWithActivator (
                    Sg.onActivation (fun _ ->
                        count <- count + 1
                        { new IDisposable with member x.Dispose() = count <- count - 1 }
                    )
                )

            let prepared =
                sg.RenderObjects(Ag.Scope.Root)
                |> ASet.toAVal
                |> AVal.force
                |> HashSet.toList
                |> List.map (fun ro -> runtime.PrepareRenderObject(signature, ro))

            try
                Expect.equal count countAfterPrepare "unexpected count after preparing"
            with e ->
                prepared |> List.iter Disposable.dispose
                raise e

            prepared |> List.iter Disposable.dispose
            Expect.equal count countAfterDispose "unexpected count after disposing"

        let onActivationMultiRenderObject (runtime: IRuntime) =
            let sg (withCountingActivator : ISg -> ISg) =
                Sg.ofList [
                    Sg.quad |> Sg.shader { do! DefaultSurfaces.constantColor C4f.Blue }
                    Sg.quad |> Sg.shader { do! DefaultSurfaces.constantColor C4f.Red }
                ]
                |> withCountingActivator

            sg |> testOnActivation runtime 1 0

        let delayModifySurface (runtime: IRuntime) =
            use signature =
                runtime.CreateFramebufferSignature [
                    DefaultSemantic.Colors, TextureFormat.Rgba8
                ]

            let effectAddBlue =
                let shader (v : Effects.Vertex) =
                    fragment {
                        return v.c + V4f.OOIO
                    }

                Effect.ofFunction shader

            use task =
                Sg.delay (fun scope ->
                    Sg.fullScreenQuad
                    |> Sg.effect [
                        match scope.Surface with
                        | Surface.Effect effect -> yield effect
                        | _ -> ()

                        yield effectAddBlue
                    ]
                )
                |> Sg.shader {
                    do! DefaultSurfaces.constantColor C4f.Red
                }
                |> Sg.compile runtime signature

            let size = AVal.constant <| V2i(128)
            let buffer = task |> RenderTask.renderToColor size
            buffer.Acquire()

            try
                let color = buffer.GetValue().Download().AsPixImage<uint8>()
                color |> PixImage.isColor [| 255uy; 0uy; 255uy; 255uy |]

            finally
                buffer.Release()

        let modelTrafo _ =
            let translation = V3d(40.0, 12.0, -23.0)
            let rotation = V3d(3.0, -2.0, 1.0)
            let scaling = V3d(0.5, 2.0, 3.0)

            let sg =
                Sg.fullScreenQuad
                |> Sg.translation' translation
                |> Sg.rotation' rotation
                |> Sg.scaling' scaling

            let ro =
                sg.RenderObjects(Ag.Scope.Root).Content.GetValue()
                |> Seq.head

            let result = ro.AttributeScope.ModelTrafo |> AVal.force
            let expected = Trafo3d.Translation translation * Trafo3d.RotationEuler rotation * Trafo3d.Scale scaling

            Expect.approxEquals result.Forward expected.Forward 0.001 "Invalid model trafo"

    let tests (target: TestTarget) =
        [
            if target = TestTarget.Cpu then
                "ModelTrafo", Cases.modelTrafo

            elif target.IsGpu then
                "OnActivation multiple RenderObjects", Cases.onActivationMultiRenderObject
                "Delay modify surface",                Cases.delayModifySurface
        ]
        |> prepareCases "SceneGraph.Nodes" target
