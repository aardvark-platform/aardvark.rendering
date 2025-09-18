namespace Aardvark.Rendering.ImGui

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open System
open Hexa.NET.ImGui

type internal DrawCmd(drawCalls: IAdaptiveResource<IBackendBuffer>, index: int) =
    let texture = AVal.init nullTexture
    let scissor = AVal.init Box2i.Invalid

    let stride = sizeof<DrawCallInfo>
    let offset = uint64 index * uint64 stride
    let indirectBuffer = drawCalls |> AdaptiveResource.map (IndirectBuffer.ofBuffer true offset stride 1)

    let sg =
        Sg.indirectDraw IndexedGeometryMode.TriangleList indirectBuffer
        |> Sg.diffuseTexture texture
        |> Sg.scissor scissor

    member _.Scene = sg

    member _.Offset = offset

    member _.Texture
        with get() = texture.Value
        and set value = texture.Value <- value

    member _.Scissor
        with get() = scissor.Value
        and set value = scissor.Value <- value

type internal DrawList(runtime: IRuntime, textures: Textures) =
    let createBuffer (name: string) (usage: BufferUsage) =
        let buffer = AdaptiveBuffer(runtime, 0UL, usage, discardOnResize = true)
        buffer.Acquire()
        buffer.Name <- name
        buffer

    let indexBuffer    = createBuffer "ImGui Index Buffer" BufferUsage.Index
    let vertexBuffer   = createBuffer "ImGui Vertex Buffer" BufferUsage.Vertex
    let indirectBuffer = createBuffer "ImGui Indirect Buffer" BufferUsage.Indirect

    let positions = BufferView(vertexBuffer, typeof<V2f>, 0,  sizeof<ImDrawVert>)
    let texCoords = BufferView(vertexBuffer, typeof<V2f>, 8,  sizeof<ImDrawVert>)
    let colors    = BufferView(vertexBuffer, typeof<C4b>, 16, sizeof<ImDrawVert>)
    let indices   = BufferView(indexBuffer, typeof<uint16>)

    let commands = clist<DrawCmd>()

    let sg =
        RenderCommand.Ordered(commands |> AList.map _.Scene)
        |> Sg.execute
        |> Sg.vertexBuffer DefaultSemantic.Positions positions
        |> Sg.vertexBuffer DefaultSemantic.DiffuseColorCoordinates texCoords
        |> Sg.vertexBuffer DefaultSemantic.Colors colors
        |> Sg.indexBuffer indices

    let resizeBuffer (requiredSize: uint64) (buffer: AdaptiveBuffer) =
        let alignedSize = Fun.NextPowerOfTwo(requiredSize)
        if buffer.Size < requiredSize || buffer.Size > 2UL * alignedSize then
            buffer.Resize alignedSize

    let updateBuffer (data: ImVector<'T>) (buffer: AdaptiveBuffer) =
        let totalSize = uint64 data.Size * uint64 sizeof<'T>
        buffer |> resizeBuffer totalSize
        buffer.Write(data.Data.Address, 0UL, totalSize)

    member _.Scene = sg

    member _.Update(data: ImDrawListPtr, display: Box2i inref) =
        indexBuffer |> updateBuffer data.IdxBuffer
        vertexBuffer |> updateBuffer data.VtxBuffer

        let currentCount = commands.Count

        // Add and remove commands if the count has changed
        for i = 1 to data.CmdBuffer.Size - currentCount do
            DrawCmd(indirectBuffer, currentCount + i - 1) |> commands.Append |> ignore

        for _ = 1 to currentCount - data.CmdBuffer.Size do
            commands.Remove(commands.MaxIndex) |> ignore

        // Update draw calls and textures
        indirectBuffer |> resizeBuffer (uint64 data.CmdBuffer.Size * uint64 sizeof<DrawCallInfo>)

        let mutable i = 0
        for cmd in commands do
            let data = data.CmdBuffer.[i]

            if IntPtr data.UserCallback <> 0n then
                Log.warn "[ImGui] User callbacks are not supported."
            else
                let mutable drawCall =
                    DrawCallInfo(
                        FirstInstance   = 0,
                        InstanceCount   = 1,
                        FirstIndex      = int data.IdxOffset,
                        FaceVertexCount = int data.ElemCount,
                        BaseVertex      = int data.VtxOffset
                    )

                let scissor =
                    Box2i(
                        int data.ClipRect.X, display.SizeY - int data.ClipRect.W,
                        int data.ClipRect.Z, display.SizeY - int data.ClipRect.Y
                    )

                DrawCallInfo.ToggleIndexed &drawCall
                indirectBuffer.Write(&drawCall, cmd.Offset)
                cmd.Texture <- textures.[data.GetTexID()]
                cmd.Scissor <- scissor

            inc &i

    member _.Dispose() =
        (commands :> alist<_>).Content.Outputs.Clear()
        commands.Clear()
        indirectBuffer.Release()
        indexBuffer.Release()
        vertexBuffer.Release()

    interface IDisposable with
        member this.Dispose() = this.Dispose()