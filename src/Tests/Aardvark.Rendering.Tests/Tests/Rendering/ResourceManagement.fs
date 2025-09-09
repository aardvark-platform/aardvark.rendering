namespace Aardvark.Rendering.Tests.Rendering

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Aardvark.Application
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open Expecto

module ResourceManagement =

    module Cases =

        type private Mode =
            | InPlace
            | Replace of coerce: bool

        let private render (task: IRenderTask) =
            let output = task |> RenderTask.renderToColor (~~V2i(256))
            output.Acquire()

            try
                output.GetValue().Download().AsPixImage<float32>()
            finally
                output.Release()

        let private createOrReplaceBuffer<'Handle, 'Data when 'Data : unmanaged and 'Data : equality>
                    (kind: ResourceKind)
                    (initialData: 'Data[])
                    (updatedData: 'Data[])
                    (getHandle: IBuffer -> 'Handle)
                    (getSg: aval<'Handle> -> ISg)
                    (initialResult : C4f)
                    (updatedResult : C4f)
                    (mode: Mode)
                    (runtime: IRuntime) =

            use signature =
                runtime.CreateFramebufferSignature([
                    DefaultSemantic.Colors, TextureFormat.Rgba32f
                ])

            let expectedInPlaceUpdates, expectedReplacedResources =
                match mode with
                | InPlace -> 1, 0
                | _ -> 0, 1

            let initialBuffer, preparedBuffer : (IBuffer * IBackendBuffer option) =
                match mode with
                | InPlace -> ArrayBuffer initialData, None
                | Replace coerce ->
                    let prep = runtime.PrepareBuffer(ArrayBuffer initialData)
                    if coerce then
                        prep.Coerce<'Data>(), Some prep
                    else
                        prep, Some prep

            try
                let buffer =
                    AVal.init <| getHandle initialBuffer

                use task =
                    getSg buffer
                    |> Sg.compile runtime signature

                task.Update()
                render task |> PixImage.isColor (initialResult.ToArray())

                transact (fun _ ->
                    buffer.Value <- getHandle <| ArrayBuffer updatedData
                )

                let rt = RenderToken.Zero
                task.Update(AdaptiveToken.Top, rt)
                render task |> PixImage.isColor (updatedResult.ToArray())

                Expect.equal (rt.InPlaceUpdates.GetOrDefault kind) expectedInPlaceUpdates "Unexpected number of in-place updates"
                Expect.equal (rt.ReplacedResources.GetOrDefault kind) expectedReplacedResources "Unexpected number of replaced buffers"

                preparedBuffer |> Option.iter (fun preparedBuffer ->
                    let downloaded = preparedBuffer.Buffer.Coerce<'Data>().Download()
                    Expect.equal downloaded initialData "Data in prepared buffer has changed"
                )
            finally
                preparedBuffer |> Option.iter _.Dispose()

        let private vertexBuffer =
            let initialColors = Array.replicate 4 C4f.Azure
            let updatedColors = Array.replicate 4 C4f.Crimson

            let getSg (colors: aval<IBuffer>) =
                Sg.fullScreenQuad
                |> Sg.vertexBuffer DefaultSemantic.Colors (BufferView(colors, typeof<C4f>))
                |> Sg.surface Effects.VertexColor.Effect

            createOrReplaceBuffer ResourceKind.Buffer initialColors updatedColors id getSg initialColors.[0] updatedColors.[0]

        let private indirectBuffer =
            let initial = [| DrawCallInfo(4, FirstInstance = 0); DrawCallInfo(0, FirstInstance = 1) |]
            let updated = [| DrawCallInfo(0, FirstInstance = 0); DrawCallInfo(4, FirstInstance = 1) |]
            let colors = [| C4f.Azure; C4f.Crimson |]

            let getSg (indirectBuffer: aval<IndirectBuffer>) =
                Sg.indirectDraw IndexedGeometryMode.TriangleStrip indirectBuffer
                |> Sg.vertexArray DefaultSemantic.Positions [| V3f(-1,-1,1); V3f(1,-1,1); V3f(-1,1,1); V3f(1,1,1) |]
                |> Sg.instanceArray DefaultSemantic.Colors colors
                |> Sg.surface Effects.VertexColor.Effect

            let getIndirectBuffer (drawCalls: IBuffer) =
                IndirectBuffer.ofBuffer false 0UL sizeof<DrawCallInfo> 2 drawCalls

            createOrReplaceBuffer ResourceKind.IndirectBuffer initial updated getIndirectBuffer getSg colors.[0] colors.[1]

        let inplaceUpdateVertexBuffer       = vertexBuffer InPlace
        let replaceVertexBuffer coerce      = vertexBuffer <| Replace coerce

        let inplaceUpdateIndirectBuffer     = indirectBuffer InPlace
        let replaceIndirectBuffer coerce    = indirectBuffer <| Replace coerce

    let tests (backend: Backend) =
        [
            "In-place update vertex buffer",     Cases.inplaceUpdateVertexBuffer
            "Replace vertex buffer",             Cases.replaceVertexBuffer false
            "Replace vertex buffer (coerced)",   Cases.replaceVertexBuffer true

            "In-place update indirect buffer",   Cases.inplaceUpdateIndirectBuffer
            "Replace indirect buffer",           Cases.replaceIndirectBuffer false
            "Replace indirect buffer (coerced)", Cases.replaceIndirectBuffer true
        ]
        |> prepareCases backend "Resource Management"