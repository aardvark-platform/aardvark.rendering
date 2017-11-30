﻿namespace Aardvark.Rendering.Text

open System
open System.Collections.Concurrent
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Rendering.Text
open Aardvark.SceneGraph

module RenderPass =
    let shapes = RenderPass.main |> RenderPass.after "shapes" RenderPassOrder.BackToFront

type Border2d = { left : float; right: float; top: float; bottom : float } with
    static member None = { left = 0.0; right = 0.0; top = 0.0; bottom = 0.0 }

module Sg =
    open Aardvark.SceneGraph.Semantics
    open Aardvark.Base.Ag

    type Shape (renderBoundary : bool, boundaryColor : C4b, boundaryExtent : Border2d, content : IMod<ShapeList>) =
        interface ISg

        member x.RenderBoundary = renderBoundary
        member x.BoundaryColor = boundaryColor
        member x.BoundaryExtent = boundaryExtent
        member x.Content = content
        new(content) = Shape(false, C4b.Black, Border2d.None, content)
        new(color, content) = Shape(true, color, Border2d.None, content)

    type BillboardApplicator(child : IMod<ISg>) =
        inherit Sg.AbstractApplicator(child)


    [<Ag.Semantic>]
    type ShapeSem() =

        member x.ModelTrafoStack(b : BillboardApplicator) =
            let view = b.ViewTrafo

            let trafo =
                b.ViewTrafo
                    |> Mod.map (fun view ->
                        let pos = view.Forward.TransformPosProj V3d.Zero
                        Trafo3d.Translation(pos) * view.Inverse
                    )


            b.Child?ModelTrafoStack <- trafo::b.ModelTrafoStack


        member x.LocalBoundingBox(t : Shape) : IMod<Box3d> =
            t.Content |> Mod.map (fun c ->
                Box3d(V3d(c.bounds.Min, 0.0), V3d(c.bounds.Max, 0.0))
            )

        member x.GlobalBoundingBox(t : Shape) : IMod<Box3d> =
            Mod.map2 (fun c (t : Trafo3d) ->
                let box = Box3d(V3d(c.bounds.Min, 0.0), V3d(c.bounds.Max, 0.0))
                box.Transformed(t.Forward)

            ) t.Content t.ModelTrafo

        member x.RenderObjects(t : Shape) : aset<IRenderObject> =
            let content = t.Content
            let cache = ShapeCache.GetOrCreateCache(t.Runtime)
            let shapes = RenderObject.create()
                
            let indirectAndOffsets =
                content |> Mod.map (fun renderText ->
                    let indirectBuffer = 
                        renderText.shapes 
                            |> List.toArray
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
                            |> IndirectBuffer.ofArray

                    let offsets = 
                        List.zip renderText.offsets renderText.scales
                            |> List.toArray
                            |> Array.map (fun (o,s) -> V4f.op_Explicit (V4d(o.X, o.Y, s.X, s.Y)))
                            |> ArrayBuffer
                            :> IBuffer

                    let colors = 
                        renderText.colors
                            |> List.toArray
                            |> ArrayBuffer
                            :> IBuffer

                    indirectBuffer, offsets, colors
                )

            let offsets = BufferView(Mod.map (fun (_,o,_) -> o) indirectAndOffsets, typeof<V4f>)
            let colors = BufferView(Mod.map (fun (_,_,c) -> c) indirectAndOffsets, typeof<C4b>)

            let instanceAttributes =
                let old = shapes.InstanceAttributes
                { new IAttributeProvider with
                    member x.TryGetAttribute sem =
                        if sem = Path.Attributes.PathOffsetAndScale then offsets |> Some
                        elif sem = Path.Attributes.PathColor then colors |> Some
                        else old.TryGetAttribute sem
                    member x.All = old.All
                    member x.Dispose() = old.Dispose()
                }


            let aa =
                match shapes.Uniforms.TryGetUniform(Ag.emptyScope, Symbol.Create "Antialias") with
                    | Some (:? IMod<bool> as aa) -> aa
                    | _ -> Mod.constant false

            let fill =
                match shapes.Uniforms.TryGetUniform(Ag.emptyScope, Symbol.Create "FillGlyphs") with
                    | Some (:? IMod<bool> as aa) -> aa
                    | _ -> Mod.constant true


            shapes.Multisample <- Mod.map2 (fun a f -> not f || a) aa fill
            shapes.RenderPass <- RenderPass.shapes
            shapes.BlendMode <- Mod.constant BlendMode.Blend
            shapes.VertexAttributes <- cache.VertexBuffers
            shapes.IndirectBuffer <- indirectAndOffsets |> Mod.map (fun (i,_,_) -> i)
            shapes.InstanceAttributes <- instanceAttributes
            shapes.Mode <- Mod.constant IndexedGeometryMode.TriangleList
            shapes.Surface <- Surface.FShadeSimple cache.Effect


            //shapes.WriteBuffers <- Some (Set.ofList [DefaultSemantic.Colors])

            let boundary = RenderObject.create()
            //boundary.ConservativeRaster <- Mod.constant false
            boundary.RenderPass <- RenderPass.shapes
            boundary.BlendMode <- Mod.constant BlendMode.Blend
            boundary.VertexAttributes <- cache.VertexBuffers
            let drawCall =
                let range = cache.GetBufferRange Shape.Quad
                DrawCallInfo(
                    FirstIndex = range.Min,
                    FaceVertexCount = range.Size + 1,
                    FirstInstance = 0,
                    InstanceCount = 1,
                    BaseVertex = 0
                )

            boundary.DrawCallInfos <- [drawCall] |> Mod.constant
            boundary.Mode <- Mod.constant IndexedGeometryMode.TriangleList
            boundary.Uniforms <-
                let old = boundary.Uniforms
                { new IUniformProvider with
                    member x.TryGetUniform(scope, sem) =
                        match string sem with
                            | "BoundaryColor" -> t.BoundaryColor |> Mod.constant :> IMod |> Some
                            | "ModelTrafo" -> 
                                let scaleTrafo = 
                                    let e = t.BoundaryExtent

                                    content |> Mod.map (fun s -> 
                                        let b = s.bounds
                                        let bounds = Box2d(b.Min.X - e.left, b.Min.Y - e.bottom, b.Max.X + e.right, b.Max.Y + e.top)
                                        if bounds.IsValid then
                                            Trafo3d.Scale(bounds.SizeX, bounds.SizeY, 1.0) *
                                            Trafo3d.Translation(bounds.Min.X, bounds.Min.Y, 0.0)
                                        else
                                            Trafo3d.Scale(0.0)
                                    )

                                match old.TryGetUniform(scope, sem) with
                                    | Some (:? IMod<Trafo3d> as m) ->
                                        Mod.map2 (*) scaleTrafo m :> IMod |> Some
                                    | _ ->
                                        scaleTrafo :> IMod |> Some

                            | _ -> old.TryGetUniform(scope, sem)

                    member x.Dispose() =
                        old.Dispose()
                }
            boundary.Surface <- Surface.FShadeSimple cache.BoundaryEffect


            let writeStencil =
                StencilMode(
                    StencilOperationFunction.Replace,
                    StencilOperationFunction.Zero,
                    StencilOperationFunction.Keep,
                    StencilCompareFunction.Always,
                    1,
                    0xFFFFFFFFu
                )

            let readStencil =
                StencilMode(
                    StencilOperationFunction.Keep,
                    StencilOperationFunction.Keep,
                    StencilOperationFunction.Keep,
                    StencilCompareFunction.Equal,
                    1,
                    0xFFFFFFFFu
                )

            let writeBuffers =
                if t.RenderBoundary then None
                else Some (Set.ofList [DefaultSemantic.Depth; DefaultSemantic.Stencil])

            boundary.WriteBuffers <- writeBuffers
            boundary.StencilMode <- Mod.constant writeStencil
            boundary.FillMode <- Mod.constant FillMode.Fill
            shapes.DepthTest <- Mod.constant DepthTestMode.None
            shapes.StencilMode <- Mod.constant readStencil

            //shapes :> IRenderObject |> ASet.single
            MultiRenderObject [boundary; shapes] :> IRenderObject |> ASet.single
                //ASet.ofList [boundary :> IRenderObject; shapes :> IRenderObject]

        member x.FillGlyphs(s : ISg) =
            let mode = s.FillMode
            mode |> Mod.map (fun m -> m = FillMode.Fill)

        member x.Antialias(r : Root<ISg>) =
            r.Child?Antialias <- Mod.constant true

    let billboard (sg : ISg) =
        sg |> Mod.constant |> BillboardApplicator :> ISg

    let shape (content : IMod<ShapeList>) =
        Shape(content) :> ISg

    let shapeWithBackground (color : C4b) (border : Border2d) (content : IMod<ShapeList>) =
        Shape(true, color, border, content) :> ISg

    let text (f : Font) (color : C4b) (content : IMod<string>) =
        content 
            |> Mod.map (fun c -> Text.Layout(f, color, c)) 
            |> shape
            
    let textWithBackground (f : Font) (color : C4b) (backgroundColor : C4b) (border : Border2d) (content : IMod<string>) =
        content 
            |> Mod.map (fun c -> Text.Layout(f, color, c)) 
            |> shapeWithBackground backgroundColor border




