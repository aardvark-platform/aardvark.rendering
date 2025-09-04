namespace Aardvark.Rendering.Tests.Rendering

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Aardvark.Application
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open System.Runtime.InteropServices
open Expecto

module DrawCalls =

    module Cases =

        [<StructLayout(LayoutKind.Sequential)>]
        type private DrawCallInfoPadded<'Padding when 'Padding : unmanaged> =
            struct
                val mutable DrawCall : DrawCallInfo
                val private _padding : 'Padding
                new (call) = { DrawCall = call; _padding = Unchecked.defaultof<_> }
            end

        let render<'T> (size: V2i) (task: IRenderTask) : PixImage<'T> =
            let output = task |> RenderTask.renderToColor ~~size
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

            let size = V2i 256
            let result = render<float32> size task
            result |> PixImage.isColor32f Accuracy.medium (expectedColor.ToArray())

        let indirect (withOffsetAndStride: bool) (correctLayout: bool) (runtime: IRuntime) =
            use signature =
                runtime.CreateFramebufferSignature([
                    DefaultSemantic.Colors, TextureFormat.Rgba32f
                ])

            let colors = [| C4f.Wheat; C4f.BlueViolet; C4f.HoneyDew; C4f.Peru |]

            let positions =
                Array.init colors.Length (fun i ->
                    let s = 2.0f / float32 colors.Length
                    let x0 = -1.0f + float32 i * s
                    let x1 = -1.0f + float32 (i + 1) * s
                    [| V3f(x0, -1.0f, 0.0f); V3f(x1, -1.0f, 0.0f); V3f(x0, 1.0f, 0.0f); V3f(x1, 1.0f, 0.0f) |]
                )
                |> Array.concat

            let indirectBuffer =
                let array =
                    Array.init colors.Length (fun i ->
                        DrawCallInfo(
                            FaceVertexCount = 4,
                            InstanceCount   = 1,
                            FirstIndex      = i * 4,
                            FirstInstance   = i
                        )
                    )

                let indexed =
                    if not correctLayout then
                        for i = 0 to array.Length - 1 do DrawCallInfo.ToggleIndexed &array.[i]
                        true
                    else
                        false

                if withOffsetAndStride then
                    let padded = array |> Array.collect (fun call -> [|  Unchecked.defaultof<_>; DrawCallInfoPadded<uint64> call |])
                    let offset = uint64 sizeof<DrawCallInfoPadded<uint64>>
                    let stride = sizeof<DrawCallInfoPadded<uint64>> * 2
                    IndirectBuffer.ofBuffer indexed offset stride array.Length (ArrayBuffer padded)
                else
                    IndirectBuffer.ofArray' indexed 0 array.Length array

            use task =
                Sg.indirectDraw' IndexedGeometryMode.TriangleStrip indirectBuffer
                |> Sg.vertexArray DefaultSemantic.Positions positions
                |> Sg.instanceArray DefaultSemantic.Colors colors
                |> Sg.shader {
                    do! DefaultSurfaces.vertexColor
                }
                |> Sg.compile runtime signature

            let expected =
                let pi = PixImage<float32>(Col.Format.RGBA, 256L, 256L)
                pi.GetMatrix<C4f>().SetByCoord(fun (coord: V2l) ->
                    let index = float32 coord.X / (float32 pi.Width / float32 colors.Length)
                    colors.[int index]
                ) |> ignore
                pi

            let result = render<float32> expected.Size task
            PixImage.compare V2i.Zero expected result

    let tests (backend: Backend) =
        [
            "Automatic FaceVertexCount computation (non-indexed)",                          Cases.faceVertexCount false false
            "Automatic FaceVertexCount computation (non-indexed, with offset and stride)",  Cases.faceVertexCount true false
            "Automatic FaceVertexCount computation (indexed)",                              Cases.faceVertexCount false true
            "Automatic FaceVertexCount computation (indexed, with offset and stride)",      Cases.faceVertexCount false true

            "Indirect array",                                                               Cases.indirect false true
            "Indirect array (with offset and stride)",                                      Cases.indirect true true
            "Indirect array (automatic layout adjustment)",                                 Cases.indirect false false
            "Indirect array (with offset and stride, automatic layout adjustment)",         Cases.indirect true false
        ]
        |> prepareCases backend "Draw calls"