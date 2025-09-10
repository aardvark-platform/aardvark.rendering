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
            | NoChange

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

            let checkInPlaceUpdates, checkReplacedResources, expectedUpdatedResult =
                match mode with
                | InPlace   -> (=) 1,  (=) 0, updatedResult
                | Replace _ -> (=) 0,  (=) 1, updatedResult
                | NoChange  -> (>=) 1, (=) 0, initialResult

            let initialBuffer, preparedBuffer : IBuffer * IBackendBuffer option =
                match mode with
                | InPlace | NoChange -> ArrayBuffer initialData, None
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
                    if mode = NoChange then
                        buffer.MarkOutdated()
                    else
                        buffer.Value <- getHandle <| ArrayBuffer updatedData
                )

                let rt = RenderToken.Zero
                task.Update(AdaptiveToken.Top, rt)
                render task |> PixImage.isColor (expectedUpdatedResult.ToArray())

                let inPlaceUpdates = rt.InPlaceUpdates.GetOrDefault kind
                let replacedResources = rt.ReplacedResources.GetOrDefault kind
                Expect.isTrue (checkInPlaceUpdates inPlaceUpdates) "Unexpected number of in-place updates"
                Expect.isTrue (checkReplacedResources replacedResources) "Unexpected number of replaced buffers"

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
        let noChangeVertexBuffer            = vertexBuffer NoChange

        let inplaceUpdateIndirectBuffer     = indirectBuffer InPlace
        let replaceIndirectBuffer coerce    = indirectBuffer <| Replace coerce
        let noChangeIndirectBuffer          = indirectBuffer NoChange

    let tests (backend: Backend) =
        [
            // The GL backend caches buffers at aval<IBuffer> level and also at the IBuffer level itself.
            // The latter means that in-place updates are not possible (at least not without introducing locks).
            if backend <> Backend.GL then
                "In-place update vertex buffer",     Cases.inplaceUpdateVertexBuffer
                "In-place update indirect buffer",   Cases.inplaceUpdateIndirectBuffer

            "No change vertex buffer",           Cases.noChangeVertexBuffer
            "No change indirect buffer",         Cases.noChangeIndirectBuffer

            "Replace vertex buffer",             Cases.replaceVertexBuffer false
            "Replace vertex buffer (coerced)",   Cases.replaceVertexBuffer true

            "Replace indirect buffer",           Cases.replaceIndirectBuffer false
            "Replace indirect buffer (coerced)", Cases.replaceIndirectBuffer true
        ]
        |> prepareCases backend "Resource Management"