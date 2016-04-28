namespace Aardvark.SceneGraph

open System
open System.Collections.Concurrent
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Rendering.Text


module Sg =
    open Aardvark.SceneGraph.Semantics

    type Text(content : IMod<RenderText>) =
        interface ISg

        member x.Content = content


    [<Ag.Semantic>]
    type TextSem() =

        member x.RenderObjects(t : Text) : aset<IRenderObject> =
            let content = t.Content
            let cache = Font.GetOrCreateCache(t.Runtime)
            let ro = RenderObject.create()
                
            let indirectAndOffsets =
                content |> Mod.map (fun renderText ->
                    let indirectBuffer = 
                        renderText.glyphs 
                            |> Array.map cache.GetBufferRange
                            |> Array.mapi (fun i r ->
                                DrawCallInfo(
                                    FirstIndex = r.Min,
                                    FaceVertexCount = r.Size + 1,
                                    FirstInstance = i,
                                    InstanceCount = 1,
                                    BaseVertex = 0
                                )
                                )
                            |> ArrayBuffer
                            :> IBuffer

                    let offsets = 
                        Array.zip renderText.offsets renderText.scales
                            |> Array.map (fun (o,s) -> V4f.op_Explicit (V4d(o.X, o.Y, s.X, s.Y)))
                            |> ArrayBuffer
                            :> IBuffer

                    let colors = 
                        renderText.colors
                            |> ArrayBuffer
                            :> IBuffer

                    indirectBuffer, offsets, colors
                )

            let offsets = BufferView(Mod.map (fun (_,o,_) -> o) indirectAndOffsets, typeof<V4f>)
            let colors = BufferView(Mod.map (fun (_,_,c) -> c) indirectAndOffsets, typeof<C4b>)

            let instanceAttributes =
                let old = ro.InstanceAttributes
                { new IAttributeProvider with
                    member x.TryGetAttribute sem =
                        if sem = Path.Attributes.PathOffsetAndScale then offsets |> Some
                        elif sem = Path.Attributes.PathColor then colors |> Some
                        else old.TryGetAttribute sem
                    member x.All = old.All
                    member x.Dispose() = old.Dispose()
                }

            ro.RenderPass <- 100UL
            ro.BlendMode <- Mod.constant BlendMode.Blend
            ro.VertexAttributes <- cache.VertexBuffers
            ro.IndirectBuffer <- indirectAndOffsets |> Mod.map (fun (i,_,_) -> i)
            ro.InstanceAttributes <- instanceAttributes
            ro.Mode <- Mod.constant IndexedGeometryMode.TriangleList
            ro.Surface <- Mod.constant cache.Surface

            ASet.single (ro :> IRenderObject)

        member x.FillGlyphs(s : ISg) =
            let mode = s.FillMode
            mode |> Mod.map (fun m -> m = FillMode.Fill)

    let text (content : IMod<RenderText>) =
        Text(content)
            |> Sg.uniform "Antialias" (Mod.constant true)

    let label (f : Font) (color : C4b) (content : IMod<string>) =
        content 
            |> Mod.map (fun c -> Text.Layout(f, color, c)) 
            |> text




