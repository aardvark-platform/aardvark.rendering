namespace Aardvark.Rendering.Tests.Rendering

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Aardvark.Application
open FSharp.Data.Adaptive
open Expecto

module RenderTasks =

    module Cases =

        let update (runtime : IRuntime) =
            let color0 = C3b.Aquamarine
            let color1 = C3b.Chocolate

            let color = AVal.init color0
            let size = AVal.constant <| V2i(256)

            let makeSg (color : C3b) =
                Sg.fullScreenQuad
                |> Sg.uniform' "Color" color
                |> Sg.shader {
                    do! DefaultSurfaces.sgColor
                }

            use signature =
                runtime.CreateFramebufferSignature([
                    DefaultSemantic.Colors, TextureFormat.Rgba8
                ])

            use task =
                color
                |> AVal.map makeSg
                |> Sg.dynamic
                |> Sg.compile runtime signature

            let output = task |> RenderTask.renderToColor size
            output.Acquire()

            try
                let res0 = output.GetValue().Download().AsPixImage<uint8>()
                res0 |> PixImage.isColor (color0.ToArray())

                transact (fun _ ->
                    color.Value <- color1
                )

                Expect.isTrue task.OutOfDate "Task was not marked out of date"

                task.Update()

                Expect.isFalse task.OutOfDate "Task is still out of date"

                let res1 = output.GetValue().Download().AsPixImage<uint8>()
                res1 |> PixImage.isColor (color1.ToArray())

            finally
                output.Release()

        let occlusionQuery (contextSwitch : bool) (runtime : IRuntime) =
            let size = V2i(256)

            use query = runtime.CreateOcclusionQuery()

            use __ : IDisposable =
                match runtime with
                | :? GL.Runtime as gl when contextSwitch ->
                    let context = gl.Context.CreateContext()

                    try
                        context.MakeCurrent()
                        query.Begin()
                        query.End()
                        context
                    finally
                        context.ReleaseCurrent()

                | _ ->
                    Disposable.empty

            use signature =
                runtime.CreateFramebufferSignature([
                    DefaultSemantic.Colors, TextureFormat.Rgba8
                ])

            use task =
                Sg.fullScreenQuad
                |> Sg.shader {
                    do! DefaultSurfaces.constantColor C4f.White
                }
                |> Sg.compile runtime signature

            let output = task |> RenderTask.renderToColor (AVal.constant <| size)
            output.Acquire()

            try
                let _ = output.GetValue(AdaptiveToken.Top, { RenderToken.Empty with Queries = [query] })
                let samples = query.GetResult()

                Expect.isGreaterThan samples 0UL "Non-positive sample count"
                Expect.equal samples (uint64 (size.X * size.Y)) "Unexpected sample count"
            finally
                output.Release()

    let tests (backend : Backend) =
        [
            "Update", Cases.update

            "Occlusion query", Cases.occlusionQuery false

            if backend = Backend.GL then
                "Occlusion query with context switch", Cases.occlusionQuery true
        ]
        |> prepareCases backend "Render tasks"