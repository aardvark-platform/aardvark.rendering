namespace Aardvark.SceneGraph

open System
open System.Collections.Concurrent
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Rendering.Text


module Sg =
    open Aardvark.SceneGraph.Semantics

    type Text(font : Font, content : IMod<string>) =
        interface ISg

        member x.Font = font
        member x.Content = content


    [<Ag.Semantic>]
    type TextSem() =
        member x.RenderObjects(t : Text) : aset<IRenderObject> =
            let font = t.Font
            let content = t.Content
            let cache = font.GetOrCreateCache(t.Runtime)
            let ro = RenderObject.create()
                
            let indirectAndOffsets =
                content |> Mod.map (fun str ->
                    let arr = Text.layout font TextAlignment.Left (Box2d(0.0, 0.0, 100000.0, 100000.0)) str

                    let indirectBuffer = 
                        arr |> Array.map (snd >> cache.GetBufferRange)
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
                        arr |> Array.map (fst >> V2f.op_Explicit)
                            |> ArrayBuffer
                            :> IBuffer

                    indirectBuffer, offsets
                )

            let offsets = BufferView(Mod.map snd indirectAndOffsets, typeof<V2f>)

            let instanceAttributes =
                let old = ro.InstanceAttributes
                { new IAttributeProvider with
                    member x.TryGetAttribute sem =
                        if sem = Path.Attributes.PathOffset then offsets |> Some
                        else old.TryGetAttribute sem
                    member x.All = old.All
                    member x.Dispose() = old.Dispose()
                }

            ro.VertexAttributes <- cache.VertexBuffers
            ro.IndirectBuffer <- indirectAndOffsets |> Mod.map fst
            ro.InstanceAttributes <- instanceAttributes
            ro.Mode <- Mod.constant IndexedGeometryMode.TriangleList

            ASet.single (ro :> IRenderObject)

        member x.FillGlyphs(s : ISg) =
            let mode = s.FillMode
            mode |> Mod.map (fun m -> m = FillMode.Fill)

    let text (f : Font) (content : IMod<string>) =
        Text(f, content)
            |> Sg.effect [
                    Path.Shader.pathVertex |> toEffect
                    DefaultSurfaces.trafo |> toEffect
                    Path.Shader.pathFragment |> toEffect
               ]
            |> Sg.uniform "Antialias" (Mod.constant true)





