namespace Aardvark.Rendering.Tests.Rendering

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

    let tests (backend : Backend) =
        [
            "Update", Cases.update
        ]
        |> prepareCases backend "Render tasks"