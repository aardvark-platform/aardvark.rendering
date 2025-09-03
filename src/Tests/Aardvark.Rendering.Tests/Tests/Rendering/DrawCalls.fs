namespace Aardvark.Rendering.Tests.Rendering

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Aardvark.Application
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open Expecto

module DrawCalls =

    module Cases =

        let render<'T> (task: IRenderTask) : PixImage<'T> =
            let output = task |> RenderTask.renderToColor (~~V2i(256))
            output.Acquire()

            try
                output.GetValue().Download().AsPixImage<'T>()
            finally
                output.Release()

        let faceVertexCount (withOffsetAndStride: bool) (indexed: bool) (runtime: IRuntime) =
            let getBufferView (invalid: 'T) (data: 'T list) =
                if withOffsetAndStride then
                    let data = data |> List.intersperse invalid |> List.insertAt 0 invalid
                    BufferView(Array.ofList data, offset = sizeof<'T>, stride = sizeof<'T> * 2)
                else
                    BufferView(Array.ofList data)

            let vertices = getBufferView V3f.NaN [ V3f(-1,-1,0); V3f(1,-1,0); V3f(-1,1,0); V3f(1,1,0) ]
            let indices = getBufferView -1s [ 0s; 1s; 2s; 3s ]

            use signature =
                runtime.CreateFramebufferSignature([
                    DefaultSemantic.Colors, TextureFormat.Rgba32f
                ])

            let expectedColor = C4f.Aqua

            use task =
                Sg.draw IndexedGeometryMode.TriangleStrip
                |> if indexed then Sg.indexBuffer indices else id
                |> Sg.vertexBuffer DefaultSemantic.Positions vertices
                |> Sg.effect [ Effects.ConstantColor.Effect expectedColor ]
                |> Sg.compile runtime signature

            let result = render<float32> task
            result |> PixImage.isColor32f Accuracy.medium (expectedColor.ToArray())

    let tests (backend: Backend) =
        [
            "Automatic FaceVertexCount computation (non-indexed)",                         Cases.faceVertexCount false false
            "Automatic FaceVertexCount computation (non-indexed, with offset and stride)", Cases.faceVertexCount true false
            "Automatic FaceVertexCount computation (indexed)",                             Cases.faceVertexCount false true
            "Automatic FaceVertexCount computation (indexed, with offset and stride)",     Cases.faceVertexCount false true
        ]
        |> prepareCases backend "Draw calls"