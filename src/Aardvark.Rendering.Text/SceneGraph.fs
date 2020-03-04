namespace Aardvark.Rendering.Text

open System
open System.Collections.Concurrent
open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Rendering
open Aardvark.Rendering.Text
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open FSharp.Data.Traceable

module RenderPass =
    let shapes = RenderPass.main |> RenderPass.after "shapes" RenderPassOrder.BackToFront

type Border2d = { left : float; right: float; top: float; bottom : float } with
    static member None = { left = 0.0; right = 0.0; top = 0.0; bottom = 0.0 }

module Sg =
    open Aardvark.SceneGraph.Semantics
    open Aardvark.Base.Ag

    
    type ShapeSet (content : aset<aval<Trafo3d> * aval<ShapeList>>) =
        interface ISg
        member x.Content = content


    type Shape (renderBoundary : bool, boundaryColor : C4b, boundaryExtent : Border2d, content : aval<ShapeList>) =
        interface ISg

        member x.RenderBoundary = renderBoundary
        member x.BoundaryColor = boundaryColor
        member x.BoundaryExtent = boundaryExtent
        member x.Content = content
        new(content) = Shape(false, C4b.Black, Border2d.None, content)
        new(color, content) = Shape(true, color, Border2d.None, content)

    type BillboardApplicator(child : aval<ISg>) =
        inherit Sg.AbstractApplicator(child)


    [<Rule>]
    type ShapeSem() =
        static let defaultDepthBias = 1.0 / float (1 <<< 21)

        member x.ModelTrafoStack(b : BillboardApplicator, scope : Ag.Scope) =
            let view = scope.ViewTrafo

            let trafo =
                scope.ViewTrafo
                    |> AVal.map (fun view ->
                        let pos = view.Forward.TransformPosProj V3d.Zero
                        Trafo3d.Translation(pos) * view.Inverse
                    )


            b.Child?ModelTrafoStack <- trafo::scope.ModelTrafoStack
            
        member x.LocalBoundingBox(t : Shape, scope : Ag.Scope) : aval<Box3d> =
            t.Content |> AVal.map (fun c ->
                Box3d(V3d(c.bounds.Min, 0.0), V3d(c.bounds.Max, 0.0))
            )

        member x.GlobalBoundingBox(t : Shape, scope : Ag.Scope) : aval<Box3d> =
            AVal.map2 (fun c (t : Trafo3d) ->
                let box = Box3d(V3d(c.bounds.Min, 0.0), V3d(c.bounds.Max, 0.0))
                box.Transformed(t.Forward)

            ) t.Content scope.ModelTrafo
            
        member x.LocalBoundingBox(t : ShapeSet, scope : Ag.Scope) : aval<Box3d> =
            AVal.constant Box3d.Invalid

        member x.GlobalBoundingBox(t : ShapeSet, scope : Ag.Scope) : aval<Box3d> =
            AVal.constant Box3d.Invalid

        member x.RenderObjects(t : ShapeSet, scope : Ag.Scope) : aset<IRenderObject> =
            let content = t.Content
            let cache = ShapeCache.GetOrCreateCache(scope.Runtime)
            let shapes = RenderObject.ofScope scope
                
            let reader = content.GetReader()

            let trafosAndShapes =
                AVal.custom (fun token ->
                    reader.GetChanges token |> ignore
                    reader.State |> CountingHashSet.toArray |> Array.map (fun (trafo,shapes) ->
                        let trafo = trafo.GetValue token
                        let shapes = shapes.GetValue token
                        trafo, shapes
                    )
                )

            let shapesOnly = 
                AVal.custom (fun token ->
                    reader.GetChanges token |> ignore
                    reader.State |> CountingHashSet.toArray |> Array.map (fun (trafo,shapes) ->
                        shapes.GetValue token
                    )
                )    

            let trafoBuffer =
                trafosAndShapes |> AVal.map (fun state ->
                    state |> Array.mapi (fun i (trafo, shapes) ->
                        
                        let trafo = 
                            M34d.op_Explicit (
                                trafo.Forward *
                                shapes.renderTrafo.Forward
                            )
                                
                        let len = List.length shapes.concreteShapes 
                        Array.create len (M34f.op_Explicit trafo)
                    )
                    |> Array.concat
                    |> ArrayBuffer
                    :> IBuffer
                )

            let trafoBuffers =
                shapesOnly |> AVal.map (fun state ->
                    let r0, r1 = 
                        state |> Array.collect (fun shapes ->
                            let w = if shapes.flipViewDependent then -1.0f else 1.0f

                            shapes.concreteShapes
                            |> List.toArray
                            |> Array.map (fun shape ->
                                let r0 = V4f(V3f shape.trafo.R0, w)
                                let r1 = V4f shape.trafo.R1.XYZO
                                r0, r1
                            )
                        )
                        |> Array.unzip


                    ArrayBuffer r0 :> IBuffer,
                    ArrayBuffer r1 :> IBuffer
                )

            let colorBuffer =
                shapesOnly |> AVal.map (fun state ->
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
                shapesOnly |> AVal.map (fun state ->
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
                    |> IndirectBuffer.ofArray false
                )

            let trafos = BufferView(trafoBuffer, typeof<M34f>)
            let trafoR0 = BufferView(AVal.map fst trafoBuffers, typeof<V4f>)
            let trafoR1 = BufferView(AVal.map snd trafoBuffers, typeof<V4f>)
            let colors = BufferView(colorBuffer, typeof<C4b>)

            let instanceAttributes =
                let old = shapes.InstanceAttributes
                { new IAttributeProvider with
                    member x.TryGetAttribute sem =
                        if sem = Path.Attributes.TrafoOffsetAndScale then trafos |> Some
                        elif sem = Path.Attributes.PathColor then colors |> Some
                        elif sem = Path.Attributes.ShapeTrafoR0 then trafoR0 |> Some
                        elif sem = Path.Attributes.ShapeTrafoR1 then trafoR1 |> Some
                        else old.TryGetAttribute sem
                    member x.All = old.All
                    member x.Dispose() = old.Dispose()
                }

            let aa =
                match shapes.Uniforms.TryGetUniform(Ag.Scope.Root, Symbol.Create "Antialias") with
                    | Some (:? aval<bool> as aa) -> aa
                    | _ -> AVal.constant false

            let fill =
                match shapes.Uniforms.TryGetUniform(Ag.Scope.Root, Symbol.Create "FillGlyphs") with
                    | Some (:? aval<bool> as aa) -> aa
                    | _ -> AVal.constant true
                    
            let bias =
                match shapes.Uniforms.TryGetUniform(Ag.Scope.Root, Symbol.Create "DepthBias") with
                    | Some (:? aval<float> as bias) -> bias
                    | _ -> AVal.constant defaultDepthBias
                    
            shapes.Uniforms <-
                let old = shapes.Uniforms
                { new IUniformProvider with
                    member x.TryGetUniform(scope : Ag.Scope, sem : Symbol) =
                        match string sem with
                            | "Antialias" -> aa :> IAdaptiveValue |> Some
                            | "FillGlyphs" -> fill :> IAdaptiveValue |> Some
                            | "DepthBias" -> bias :> IAdaptiveValue |> Some
                            | _ -> old.TryGetUniform(scope, sem)

                    member x.Dispose() =
                        old.Dispose()
                }

            shapes.Multisample <- AVal.map2 (fun a f -> not f || a) aa fill
            shapes.RenderPass <- RenderPass.shapes
            shapes.BlendMode <- AVal.constant BlendMode.Blend
            shapes.VertexAttributes <- cache.VertexBuffers
            shapes.DrawCalls <- Indirect(indirect)
            shapes.InstanceAttributes <- instanceAttributes
            shapes.Mode <- IndexedGeometryMode.TriangleList

            trafosAndShapes 
                |> AVal.map(fun l -> 
                    match l |> Array.tryFind (fun (_,s) -> s.renderStyle = RenderStyle.Billboard) with
                    | Some x -> 
                        shapes.Surface <- Surface.FShadeSimple cache.InstancedBillboardEffect
                    | None -> 
                        shapes.Surface <- Surface.FShadeSimple cache.InstancedEffect
                    shapes :> IRenderObject |> HashSet.single
                )
                |> ASet.ofAVal

        member x.RenderObjects(t : Shape, scope : Ag.Scope) : aset<IRenderObject> =
            let content = t.Content
            let cache = ShapeCache.GetOrCreateCache(scope.Runtime)
            let shapes = RenderObject.ofScope scope
                
            let content =
                content |> AVal.map (fun c ->
                    
                    let shapes =
                        c.concreteShapes 
                        |> List.groupBy (fun c -> c.z)
                        |> List.sortBy fst
                        |> List.collect (fun (z,g) -> g)

                    { c with concreteShapes = shapes }
                )

            let indirectAndOffsets =
                content |> AVal.map (fun renderText ->
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
                            |> IndirectBuffer.ofArray false

                    let trafoR0, trafoR1 =
                        let r0, r1 = 
                            let w = if renderText.flipViewDependent then -1.0f else 1.0f

                            renderText.concreteShapes 
                            |> List.toArray
                            |> Array.map (fun shape ->
                                let r0 = V4f(V3f shape.trafo.R0, w)
                                let r1 = V4f shape.trafo.R1.XYZO
                                r0, r1
                            )
                            |> Array.unzip


                        ArrayBuffer r0 :> IBuffer,
                        ArrayBuffer r1 :> IBuffer

                    let colors = 
                        renderText.concreteShapes 
                            |> List.toArray
                            |> Array.map (fun s -> s.color)
                            |> ArrayBuffer
                            :> IBuffer

                    indirectBuffer, trafoR0, trafoR1, colors
                )

            let trafoR0 = BufferView(AVal.map (fun (_,r0,_,_) -> r0) indirectAndOffsets, typeof<V4f>)
            let trafoR1 = BufferView(AVal.map (fun (_,_,r1,_) -> r1) indirectAndOffsets, typeof<V4f>)
            let colors = BufferView(AVal.map (fun (_,_,_,c) -> c) indirectAndOffsets, typeof<C4b>)

            let instanceAttributes =
                let old = shapes.InstanceAttributes
                { new IAttributeProvider with
                    member x.TryGetAttribute sem =
                        if sem = Path.Attributes.ShapeTrafoR0 then trafoR0 |> Some
                        elif sem = Path.Attributes.ShapeTrafoR1 then trafoR1 |> Some
                        elif sem = Path.Attributes.PathColor then colors |> Some
                        else old.TryGetAttribute sem
                    member x.All = old.All
                    member x.Dispose() = old.Dispose()
                }


            let aa =
                match shapes.Uniforms.TryGetUniform(Ag.Scope.Root, Symbol.Create "Antialias") with
                    | Some (:? aval<bool> as aa) -> aa
                    | _ -> AVal.constant false

            let fill =
                match shapes.Uniforms.TryGetUniform(Ag.Scope.Root, Symbol.Create "FillGlyphs") with
                    | Some (:? aval<bool> as aa) -> aa
                    | _ -> AVal.constant true
                    
            let bias =
                match shapes.Uniforms.TryGetUniform(Ag.Scope.Root, Symbol.Create "DepthBias") with
                    | Some (:? aval<float> as bias) -> bias
                    | _ -> AVal.constant defaultDepthBias
                    
            shapes.Uniforms <-
                let old = shapes.Uniforms
                let ownTrafo = content |> AVal.map (fun c -> c.renderTrafo)
                { new IUniformProvider with
                    member x.TryGetUniform(scope, sem) =
                        match string sem with
                            | "Antialias" -> aa :> IAdaptiveValue |> Some
                            | "FillGlyphs" -> fill :> IAdaptiveValue |> Some
                            | "DepthBias" -> bias :> IAdaptiveValue |> Some
                            | "ModelTrafo" -> 
                                match old.TryGetUniform(scope, sem) with
                                    | Some (:? aval<Trafo3d> as m) ->
                                        AVal.map2 (*) ownTrafo m :> IAdaptiveValue |> Some
                                    | _ ->
                                        ownTrafo :> IAdaptiveValue |> Some

                            | _ -> 
                                old.TryGetUniform(scope, sem)

                    member x.Dispose() =
                        old.Dispose()
                }

            shapes.Multisample <- AVal.map2 (fun a f -> not f || a) aa fill
            shapes.RenderPass <- RenderPass.shapes
            shapes.BlendMode <- AVal.constant BlendMode.Blend
            shapes.VertexAttributes <- cache.VertexBuffers
            shapes.DrawCalls <- indirectAndOffsets |> AVal.map (fun (i,_,_,_) -> i) |> Indirect
            shapes.InstanceAttributes <- instanceAttributes
            shapes.Mode <- IndexedGeometryMode.TriangleList
            shapes.DepthBias <- AVal.constant (DepthBiasState(0.0, 0.0, 0.0))
            
            //shapes.WriteBuffers <- Some (Set.ofList [DefaultSemantic.Colors])

            let boundary = RenderObject.ofScope scope
            //boundary.ConservativeRaster <- AVal.constant false
            boundary.RenderPass <- RenderPass.shapes
            boundary.BlendMode <- AVal.constant BlendMode.Blend
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

            boundary.DrawCalls <- Direct ([drawCall] |> AVal.constant)
            boundary.Mode <- IndexedGeometryMode.TriangleList

            let bounds = 
                let e = t.BoundaryExtent
                content |> AVal.map (fun s -> 
                    let b = s.bounds
                    let bounds = Box2d(b.Min.X - e.left, b.Min.Y - e.bottom, b.Max.X + e.right, b.Max.Y + e.top)
                    bounds
                )

            boundary.Uniforms <-
                let old = boundary.Uniforms
                { new IUniformProvider with
                    member x.TryGetUniform(scope, sem) =
                        match string sem with
                            | "BoundaryColor" -> t.BoundaryColor |> AVal.constant :> IAdaptiveValue |> Some
                            | "ModelTrafo" -> 
                                let scaleTrafo = 
                                    bounds |> AVal.map (fun bounds -> 
                                        if bounds.IsValid then
                                            Trafo3d.Scale(bounds.SizeX, bounds.SizeY, 1.0) *
                                            Trafo3d.Translation(bounds.Min.X, bounds.Min.Y, 0.0)
                                        else
                                            Trafo3d.Scale(0.0)
                                    )

                                match old.TryGetUniform(scope, sem) with
                                    | Some (:? aval<Trafo3d> as m) ->
                                        AVal.map2 (*) scaleTrafo m :> IAdaptiveValue |> Some
                                    | _ ->
                                        scaleTrafo :> IAdaptiveValue |> Some

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
            boundary.StencilMode <- AVal.constant writeStencil
            boundary.FillMode <- AVal.constant FillMode.Fill

            content |> AVal.map(fun x -> 
                match x.renderStyle with
                | RenderStyle.Normal -> 
                    shapes.Surface <- Surface.FShadeSimple cache.Effect
                    shapes.DepthTest <- AVal.constant(DepthTestMode.None)
                    shapes.StencilMode <- AVal.constant(readStencil)
                    MultiRenderObject [boundary; shapes] :> IRenderObject  |> HashSet.single
                | RenderStyle.NoBoundary ->
                    shapes.Surface <- Surface.FShadeSimple cache.Effect
                    shapes.DepthTest <- AVal.constant(DepthTestMode.LessOrEqual)
                    shapes.StencilMode <- AVal.constant(StencilMode.Disabled)
                    shapes :> IRenderObject |> HashSet.single // MultiRenderObject [shapes] :> IRenderObject 
                | RenderStyle.Billboard -> 
                    shapes.Surface <- Surface.FShadeSimple cache.BillboardEffect
                    shapes.DepthTest <- AVal.constant(DepthTestMode.LessOrEqual)
                    shapes.StencilMode <- AVal.constant(StencilMode.Disabled)
                    shapes :> IRenderObject |> HashSet.single

                ) |> ASet.ofAVal

        member x.FillGlyphs(s : ISg, scope : Ag.Scope) =
            let mode = scope.FillMode
            mode |> AVal.map (fun m -> m = FillMode.Fill)

        member x.Antialias(r : Root<ISg>, scope : Ag.Scope) =
            r.Child?Antialias <- AVal.constant true

    let billboard (sg : ISg) =
        sg |> AVal.constant |> BillboardApplicator :> ISg

    let shape (content : aval<ShapeList>) =
        Shape(content) :> ISg

    let shapeWithBackground (color : C4b) (border : Border2d) (content : aval<ShapeList>) =
        Shape(true, color, border, content) :> ISg

    let shapes (content : aset<aval<Trafo3d> * aval<ShapeList>>) =
        ShapeSet(content) :> ISg
        
    let textWithConfig (cfg : TextConfig) (content : aval<string>) =
        content 
        |> AVal.map cfg.Layout
        |> shape

    let text (f : Font) (color : C4b) (content : aval<string>) =
        content 
            |> AVal.map (fun c -> Text.Layout(f, color, c)) 
            |> shape
            
    let textWithBackground (f : Font) (color : C4b) (backgroundColor : C4b) (border : Border2d) (content : aval<string>) =
        content 
            |> AVal.map (fun c -> Text.Layout(f, color, c)) 
            |> shapeWithBackground backgroundColor border

    let textsWithConfig (cfg : TextConfig) (content : aset<aval<Trafo3d> * aval<string>>) =
        content |> ASet.map (fun (trafo, content) ->
            trafo, AVal.map cfg.Layout content
        )
        |> shapes

    let texts (f : Font) (color : C4b) (content : aset<aval<Trafo3d> * aval<string>>) =
        content |> ASet.map (fun (trafo, content) ->
            let shapeList = content |> AVal.map (fun c -> Text.Layout(f, color, c))
            trafo, shapeList
        )
        |> shapes
        
            


