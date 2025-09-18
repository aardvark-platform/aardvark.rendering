namespace Aardvark.Rendering.ImGui

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open System
open Hexa.NET.ImGui

type internal RenderState (runtime: IRuntime) =
    let textures = new Textures(runtime)
    let drawLists = clist<DrawList>()
    let display = AVal.init Box2d.Unit

    let projTrafo =
        display |> AVal.map (fun display ->
            Trafo3d.OrthoProjectionGL(display.Left, display.Right, display.Bottom, display.Top, 0.0, 1.0)
        )

    let sg =
        RenderCommand.Ordered(drawLists |> AList.map _.Scene)
        |> Sg.execute
        |> Sg.blendMode' BlendMode.Blend
        |> Sg.depthTest' DepthTest.None
        |> Sg.cullMode' CullMode.None
        |> Sg.projTrafo projTrafo
        |> Sg.surface Shader.Effect

    let updateDrawLists (display: Box2i) (data: ImVector<ImDrawListPtr> inref) =
        let currentCount = drawLists.Count

        // Add and remove draw lists if the count has changed
        for _ = 1 to data.Size - currentCount do
            new DrawList(runtime, textures) |> drawLists.Append |> ignore

        for _ = 1 to currentCount - data.Size do
            drawLists.[drawLists.MaxIndex].Dispose()
            drawLists.Remove(drawLists.MaxIndex) |> ignore

        // Update the draw lists
        let mutable i = 0
        for list in drawLists do
            list.Update(data.[i], &display)
            inc &i

    member _.Scene = sg

    member _.Update(data: ImDrawDataPtr) =
        display.Value <- data.Display
        textures.Update &data.Textures
        updateDrawLists (Box2i data.Display) &data.CmdLists

    member _.Dispose() =
        for list in drawLists do list.Dispose()
        (drawLists :> alist<_>).Content.Outputs.Clear()
        drawLists.Clear()
        textures.Dispose()

    interface IDisposable with
        member this.Dispose() = this.Dispose()