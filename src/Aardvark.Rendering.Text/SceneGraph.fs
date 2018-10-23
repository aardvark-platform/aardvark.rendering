namespace Aardvark.Rendering.Text

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

    
    type ShapeSet (content : aset<IMod<Trafo3d> * IMod<ShapeList>>) =
        interface ISg
        member x.Content = content


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
        static let defaultDepthBias = 1.0 / float (1 <<< 21)

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
            
        member x.LocalBoundingBox(t : ShapeSet) : IMod<Box3d> =
            Mod.constant Box3d.Invalid

        member x.GlobalBoundingBox(t : ShapeSet) : IMod<Box3d> =
            Mod.constant Box3d.Invalid

        member x.RenderObjects(t : ShapeSet) : aset<IRenderObject> =
            let content = t.Content
            let cache = ShapeCache.GetOrCreateCache(t.Runtime)
            let shapes = RenderObject.create()
                
            let reader = content.GetReader()

            let trafosAndShapes =
                Mod.custom (fun token ->
                    reader.GetOperations token |> ignore
                    reader.State |> HRefSet.toArray |> Array.map (fun (trafo,shapes) ->
                        let trafo = trafo.GetValue token
                        let shapes = shapes.GetValue token
                        trafo, shapes
                    )
                )

            let shapesOnly = 
                Mod.custom (fun token ->
                    reader.GetOperations token |> ignore
                    reader.State |> HRefSet.toArray |> Array.map (fun (trafo,shapes) ->
                        shapes.GetValue token
                    )
                )
                

            let trafoBuffer =
                trafosAndShapes |> Mod.map (fun state ->
                    state |> Array.mapi (fun i (trafo,shapes) ->
                        let depthOffset =
                            M44d.Translation(0.0, 0.0, float i / 100.0)

                        let trafo = 
                            M34d.op_Explicit (
                                trafo.Forward *
                                shapes.renderTrafo.Forward *
                                depthOffset
                            )
                                
                        let len = List.length shapes.concreteShapes 
                        Array.create len (M34f.op_Explicit trafo)
                    )
                    |> Array.concat
                    |> ArrayBuffer
                    :> IBuffer
                )

            let offsetAndScaleBuffer =
                shapesOnly |> Mod.map (fun state ->
                    state |> Seq.collect (fun shapes ->
                        shapes.concreteShapes
                            |> Seq.map (fun shape -> 
                                let s = shape.scale
                                let o = shape.offset
                                let sx = if shapes.flipViewDependent then -s.X else s.X
                                let sy = s.Y
                                V4f(o.X, o.Y, sx, sy)
                            )
                    )
                    |> Seq.toArray
                    |> ArrayBuffer
                    :> IBuffer
                )

            let colorBuffer =
                shapesOnly |> Mod.map (fun state ->
                    state |> Seq.collect (fun shapes ->
                        shapes.concreteShapes |> Seq.map (fun s ->
                            let c = s.color

                            let a = 
                                let size = shapes.zRange.Size
                                if size > 0 then
                                    let layer = s.z - shapes.zRange.Min |> min 255
                                    byte layer
                                else
                                    0uy
                            C4b(c.R, c.G, c.B, a)
                        )
                    )
                    |> Seq.toArray
                    |> ArrayBuffer
                    :> IBuffer
                )
                
            let indirect =
                shapesOnly |> Mod.map (fun state ->
                    state |> Seq.collect (fun shapes ->
                        shapes.concreteShapes |> Seq.map (ConcreteShape.shape >> cache.GetBufferRange)
                    )
                    |> Seq.mapi (fun i r ->
                        DrawCallInfo(
                            FirstIndex = r.Min,
                            FaceVertexCount = r.Size + 1,
                            FirstInstance = i,
                            InstanceCount = 1,
                            BaseVertex = 0
                        )
                    )
                    |> Seq.toArray
                    |> IndirectBuffer.ofArray
                )

            let trafos = BufferView(trafoBuffer, typeof<M34f>)
            let offsetAndScale = BufferView(offsetAndScaleBuffer, typeof<V4f>)
            let colors = BufferView(colorBuffer, typeof<C4b>)

            let instanceAttributes =
                let old = shapes.InstanceAttributes
                { new IAttributeProvider with
                    member x.TryGetAttribute sem =
                        if sem = Path.Attributes.TrafoOffsetAndScale then trafos |> Some
                        elif sem = Path.Attributes.PathColor then colors |> Some
                        elif sem = Path.Attributes.PathOffsetAndScale then offsetAndScale |> Some
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
                    
            let bias =
                match shapes.Uniforms.TryGetUniform(Ag.emptyScope, Symbol.Create "DepthBias") with
                    | Some (:? IMod<float> as bias) -> bias
                    | _ -> Mod.constant defaultDepthBias
                    
            shapes.Uniforms <-
                let old = shapes.Uniforms
                { new IUniformProvider with
                    member x.TryGetUniform(scope : Ag.Scope, sem : Symbol) =
                        match string sem with
                            | "Antialias" -> aa :> IMod |> Some
                            | "FillGlyphs" -> fill :> IMod |> Some
                            | "DepthBias" -> bias :> IMod |> Some
                            | _ -> old.TryGetUniform(scope, sem)

                    member x.Dispose() =
                        old.Dispose()
                }

            shapes.Multisample <- Mod.map2 (fun a f -> not f || a) aa fill
            shapes.RenderPass <- RenderPass.shapes
            shapes.BlendMode <- Mod.constant BlendMode.Blend
            shapes.VertexAttributes <- cache.VertexBuffers
            shapes.IndirectBuffer <- indirect
            shapes.InstanceAttributes <- instanceAttributes
            shapes.Mode <- IndexedGeometryMode.TriangleList
            shapes.Surface <- Surface.FShadeSimple cache.InstancedEffect

            ASet.single (shapes :> IRenderObject)

        member x.RenderObjects(t : Shape) : aset<IRenderObject> =
            let content = t.Content
            let cache = ShapeCache.GetOrCreateCache(t.Runtime)
            let shapes = RenderObject.create()
                
            let indirectAndOffsets =
                content |> Mod.map (fun renderText ->
                    let indirectBuffer = 
                        renderText.concreteShapes 
                            |> List.toArray
                            |> Array.map (ConcreteShape.shape >> cache.GetBufferRange)
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
                        renderText.concreteShapes 
                            |> List.toArray
                            |> Array.map (fun shape ->
                                let o = shape.offset
                                let s = shape.scale
                                let sx = if renderText.flipViewDependent then -s.X else s.X
                                let sy = s.Y
                                V4f(o.X, o.Y, sx, sy)
                            )
                            |> ArrayBuffer
                            :> IBuffer

                    let colors = 
                        renderText.concreteShapes 
                            |> List.toArray
                            |> Array.map (fun s -> s.color)
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
                    
            let bias =
                match shapes.Uniforms.TryGetUniform(Ag.emptyScope, Symbol.Create "DepthBias") with
                    | Some (:? IMod<float> as bias) -> bias
                    | _ -> Mod.constant defaultDepthBias
                    
            shapes.Uniforms <-
                let old = shapes.Uniforms
                let ownTrafo = content |> Mod.map (fun c -> c.renderTrafo)
                { new IUniformProvider with
                    member x.TryGetUniform(scope, sem) =
                        match string sem with
                            | "Antialias" -> aa :> IMod |> Some
                            | "FillGlyphs" -> fill :> IMod |> Some
                            | "DepthBias" -> bias :> IMod |> Some
                            | "ModelTrafo" -> 
                                match old.TryGetUniform(scope, sem) with
                                    | Some (:? IMod<Trafo3d> as m) ->
                                        Mod.map2 (*) ownTrafo m :> IMod |> Some
                                    | _ ->
                                        ownTrafo :> IMod |> Some

                            | _ -> 
                                old.TryGetUniform(scope, sem)

                    member x.Dispose() =
                        old.Dispose()
                }

            shapes.Multisample <- Mod.map2 (fun a f -> not f || a) aa fill
            shapes.RenderPass <- RenderPass.shapes
            shapes.BlendMode <- Mod.constant BlendMode.Blend
            shapes.VertexAttributes <- cache.VertexBuffers
            shapes.IndirectBuffer <- indirectAndOffsets |> Mod.map (fun (i,_,_) -> i)
            shapes.InstanceAttributes <- instanceAttributes
            shapes.Mode <- IndexedGeometryMode.TriangleList
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
            boundary.Mode <- IndexedGeometryMode.TriangleList

            let bounds = 
                let e = t.BoundaryExtent
                content |> Mod.map (fun s -> 
                    let b = s.bounds
                    let bounds = Box2d(b.Min.X - e.left, b.Min.Y - e.bottom, b.Max.X + e.right, b.Max.Y + e.top)
                    bounds
                )

            boundary.Uniforms <-
                let old = boundary.Uniforms
                { new IUniformProvider with
                    member x.TryGetUniform(scope, sem) =
                        match string sem with
                            | "BoundaryColor" -> t.BoundaryColor |> Mod.constant :> IMod |> Some
                            | "ModelTrafo" -> 
                                let scaleTrafo = 
                                    bounds |> Mod.map (fun bounds -> 
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


    let shapes (content : aset<IMod<Trafo3d> * IMod<ShapeList>>) =
        ShapeSet(content) :> ISg
        

    let textWithConfig (cfg : TextConfig) (content : IMod<string>) =
        content 
        |> Mod.map cfg.Layout
        |> shape

    let text (f : Font) (color : C4b) (content : IMod<string>) =
        content 
            |> Mod.map (fun c -> Text.Layout(f, color, c)) 
            |> shape
            
    let textWithBackground (f : Font) (color : C4b) (backgroundColor : C4b) (border : Border2d) (content : IMod<string>) =
        content 
            |> Mod.map (fun c -> Text.Layout(f, color, c)) 
            |> shapeWithBackground backgroundColor border

    let textsWithConfig (cfg : TextConfig) (content : aset<IMod<Trafo3d> * IMod<string>>) =
        content |> ASet.map (fun (trafo, content) ->
            trafo, Mod.map cfg.Layout content
        )
        |> shapes

    let texts (f : Font) (color : C4b) (content : aset<IMod<Trafo3d> * IMod<string>>) =
        content |> ASet.map (fun (trafo, content) ->
            let shapeList = content |> Mod.map (fun c -> Text.Layout(f, color, c))
            trafo, shapeList
        )
        |> shapes
        
            


