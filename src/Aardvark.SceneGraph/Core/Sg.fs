namespace Aardvark.SceneGraph

open System
open Aardvark.Base
open Aardvark.Base.Ag

open Aardvark.Rendering
open FSharp.Data.Adaptive

module Sg =

    [<AbstractClass>]
    type AbstractApplicator(child : aval<ISg>) =
        interface IApplicator with
            member x.Child = child

        member x.Child = child

        new(child : ISg) = AbstractApplicator(AVal.constant child)

    type AdapterNode(node : obj) =
        interface ISg

        member x.Node = node

        // The Ag rule (Semantics/Adapter.fs) is `a.Node?RenderObjects(scope)`, which
        // forwards to whatever `Node` is. On the direct render path, an ISg node goes
        // through RenderSg.get (GetRenderObjects for an IRenderSg, RenderSg.bridge for a
        // legacy ISg); only a plain object is bridged through this adapter itself.
        interface IRenderSg with
            member self.GetRenderObjects ts =
                match node with
                | :? ISg as sg -> RenderSg.get sg ts
                | _            -> RenderSg.bridge self ts

    type DynamicNode(child : aval<ISg>) =
        inherit AbstractApplicator(child)

        // Pass-through: no TraversalState modification.
        interface IRenderSg with
            member _.GetRenderObjects ts =
                child |> ASet.bind (fun c -> RenderSg.get c ts)

    type RenderNode(call : aval<DrawCallInfo>, mode : IndexedGeometryMode) =
        interface ISg

        member x.Mode = mode
        member x.DrawCallInfo = call

        // Direct render path: mirrors RenderObjectSem.RenderObjects(r : Sg.RenderNode, scope)
        // in Semantics/RenderObject.fs line for line, but builds the render object with
        // RenderObject.ofTraversalState instead of RenderObject.ofScope.
        interface IRenderSg with
            member _.GetRenderObjects ts =
                let rj = RenderObject.ofTraversalState ts
                let drawCalls =
                    let mutable current = [| DrawCallInfo() |]
                    let cmp = System.Collections.Generic.EqualityComparer<DrawCallInfo>.Default
                    AVal.custom (fun t ->
                        let mutable info = call.GetValue t
                        if info.FaceVertexCount < 0 then
                            info.FaceVertexCount <- ts.FaceVertexCount.GetValue t
                            info.BaseVertex <- 0
                        if not <| cmp.Equals(current.[0], info) then
                            current <- [| info |]
                        current
                    )
                rj.DrawCalls <- DrawCalls.Direct drawCalls
                rj.Mode <- mode
                ASet.single (rj :> IRenderObject)

        new(call : DrawCallInfo, mode : IndexedGeometryMode) = RenderNode(AVal.constant call, mode)
        new(count : int, mode : IndexedGeometryMode) =
            let call =
                DrawCallInfo(
                    FaceVertexCount = count,
                    InstanceCount = 1,
                    FirstIndex = 0,
                    FirstInstance = 0,
                    BaseVertex = 0
                )
            RenderNode(AVal.constant call, mode)

        new(count : aval<int>, mode : IndexedGeometryMode) =
            let call =
                count |> AVal.map (fun x ->
                    DrawCallInfo(
                        FaceVertexCount = x,
                        InstanceCount = 1,
                        FirstIndex = 0,
                        FirstInstance = 0,
                        BaseVertex = 0
                    )
                )
            RenderNode(call, mode)

    type RenderObjectNode(objects : aset<IRenderObject>) =
        interface ISg
        member x.Objects = objects
        interface IRenderSg with
            member _.GetRenderObjects _ = objects

    type IndirectRenderNode(buffer : aval<IndirectBuffer>, mode : IndexedGeometryMode) =
        interface ISg

        member x.Mode = mode
        member x.Indirect = buffer

        // Direct render path: mirrors RenderObjectSem.RenderObjects(r : Sg.IndirectRenderNode,
        // scope) in Semantics/RenderObject.fs.
        interface IRenderSg with
            member _.GetRenderObjects ts =
                let rj = RenderObject.ofTraversalState ts
                rj.DrawCalls <- DrawCalls.Indirect buffer
                rj.Mode <- mode
                ASet.single (rj :> IRenderObject)

    type VertexAttributeApplicator(values : Map<Symbol, BufferView>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Values = values

        interface IRenderSg with
            member _.GetRenderObjects ts =
                child |> ASet.bind (fun c -> RenderSg.get c (TraversalState.applyVertexAttributes values ts))

        new(values : Map<Symbol, BufferView>, child : ISg)            = VertexAttributeApplicator(values, AVal.constant child)
        new(semantic : Symbol, value : BufferView, child : aval<ISg>) = VertexAttributeApplicator(Map.ofList [semantic, value], child)
        new(semantic : Symbol, value : BufferView, child : ISg)       = VertexAttributeApplicator(Map.ofList [semantic, value], AVal.constant child)
        new(values : SymbolDict<BufferView>, child : ISg)             = VertexAttributeApplicator(values |> Seq.map (fun (KeyValue(k,v)) -> k,v) |> Map.ofSeq, AVal.constant child)

    type VertexIndexApplicator(value : BufferView, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Value = value

        interface IRenderSg with
            member _.GetRenderObjects ts =
                child |> ASet.bind (fun c -> RenderSg.get c (TraversalState.applyVertexIndexBuffer value ts))

        new(value : BufferView, child : ISg)         = VertexIndexApplicator(value, AVal.constant child)

    type InstanceAttributeApplicator(values : Map<Symbol, BufferView>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Values = values

        interface IRenderSg with
            member _.GetRenderObjects ts =
                child |> ASet.bind (fun c -> RenderSg.get c (TraversalState.mergeInstanceAttributes values ts))

        new(values : Map<Symbol, BufferView>, child : ISg)            = InstanceAttributeApplicator(values, AVal.constant child)
        new(semantic : Symbol, value : BufferView, child : aval<ISg>) = InstanceAttributeApplicator(Map.ofList [semantic, value], child)
        new(semantic : Symbol, value : BufferView, child : ISg)       = InstanceAttributeApplicator(Map.ofList [semantic, value], AVal.constant child)
        new(values : SymbolDict<BufferView>, child : ISg)             = InstanceAttributeApplicator(values |> Seq.map (fun (KeyValue(k,v)) -> k,v) |> Map.ofSeq, AVal.constant child)


    type OnOffNode(on : aval<bool>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.IsActive = on

        // Logical and with the current IsActive, like the OnOffNode rule in Semantics/Flags.fs.
        interface IRenderSg with
            member _.GetRenderObjects ts =
                child |> ASet.bind (fun c -> RenderSg.get c (TraversalState.andIsActive on ts))

        new(on : aval<bool>, child : ISg) = OnOffNode(on, AVal.constant child)

    type ActivationApplicator(activate : unit -> IDisposable, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        // Use reference counting to prevent callback being invoked for
        // each individual render object. Prepare might be called concurrently, so
        // we have to ensure mutual exclusion.
        let lockObj = obj()
        let mutable count = 0
        let mutable handle = null :> IDisposable

        let disposable =
            { new IDisposable with
                member x.Dispose() =
                    lock lockObj (fun _ ->
                        dec &count

                        if count = 0 then
                            handle.Dispose()
                            handle <- null

                        elif count < 0 then
                            Log.warn "[Sg] ActivationApplicator has negative reference count"
                    )
            }

        let activateWrapped() =
            lock lockObj (fun _ ->
                if count = 0 then
                    handle <- activate()

                inc &count
            )

            disposable

        member x.Activate = activateWrapped

        interface IRenderSg with
            member self.GetRenderObjects ts =
                child |> ASet.bind (fun c -> RenderSg.get c (TraversalState.pushActivate self.Activate ts))

        new(activate : unit -> IDisposable, child : ISg) = ActivationApplicator(activate, AVal.constant child)

    // TODO: Caching?
    type DelayNode(generator : Ag.Scope -> ISg) =
        interface ISg
        member x.Generator = generator

        // Leaf: the generator needs a scope (Semantics/Delay.fs), so this goes through the bridge.
        interface IRenderSg with
            member self.GetRenderObjects ts = RenderSg.bridge self ts

    type PassApplicator(pass : RenderPass, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Pass = pass

        interface IRenderSg with
            member _.GetRenderObjects ts =
                child |> ASet.bind (fun c -> RenderSg.get c (TraversalState.withRenderPass pass ts))

        new(pass : RenderPass, child : ISg) = PassApplicator(pass, AVal.constant child)

    /// Marks the subtree's render objects as transparent.
    type TransparentApplicator(isTransparent : bool, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.IsTransparent = isTransparent

        interface IRenderSg with
            member _.GetRenderObjects ts =
                child |> ASet.bind (fun c -> RenderSg.get c (TraversalState.withTransparent isTransparent ts))

        new(isTransparent : bool, child : ISg) = TransparentApplicator(isTransparent, AVal.constant child)

    type UniformApplicator(uniformHolder : IUniformProvider, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member internal x.Uniforms = uniformHolder

        member x.TryFindUniform (scope : Scope) (name : Symbol) =
            uniformHolder.TryGetUniform (scope,name)

        interface IRenderSg with
            member _.GetRenderObjects ts =
                child |> ASet.bind (fun c -> RenderSg.get c (TraversalState.pushUniforms uniformHolder ts))

        new(value : IUniformProvider, child : ISg) = UniformApplicator( value, AVal.constant child)
        new(name : string, value : IAdaptiveValue, child : ISg) = UniformApplicator( (new Providers.SingleUniformHolder(Symbol.Create name, value) :> IUniformProvider), AVal.constant child)
        new(name : Symbol, value : IAdaptiveValue, child : ISg) = UniformApplicator( (new Providers.SingleUniformHolder(name, value) :> IUniformProvider), AVal.constant child)
        new(name : Symbol, value : IAdaptiveValue, child : aval<ISg>) = UniformApplicator( (new Providers.SingleUniformHolder(name, value) :> IUniformProvider), child)
        new(map : Map<Symbol,IAdaptiveValue>, child : ISg) = UniformApplicator( UniformProvider.ofMap map, AVal.constant child)


    type SurfaceApplicator(surface : Surface, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Surface = surface

        interface IRenderSg with
            member _.GetRenderObjects ts =
                child |> ASet.bind (fun c -> RenderSg.get c (TraversalState.withSurface surface ts))

        new(value : Surface, child : ISg) = SurfaceApplicator(value, AVal.constant child)

    type TextureApplicator(semantic : Symbol, texture : aval<ITexture>, child : aval<ISg>) =
        inherit UniformApplicator(semantic, texture :> IAdaptiveValue, child)

        // IRenderSg is inherited from UniformApplicator: the texture is wired through the
        // uniform holder, the same way the Ag's UniformSem rule handles it.

        member x.Texture = texture

        new(semantic : Symbol, texture : aval<ITexture>, child : ISg) = TextureApplicator(semantic, texture, AVal.constant child)
        new(texture : aval<ITexture>, child : ISg) = TextureApplicator(DefaultSemantic.DiffuseColorTexture, texture, child)
        new(texture : aval<ITexture>, child : aval<ISg>) = TextureApplicator(DefaultSemantic.DiffuseColorTexture,texture,child)


    type TrafoApplicator(trafo : aval<Trafo3d>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Trafo = trafo

        interface IRenderSg with
            member _.GetRenderObjects ts =
                child |> ASet.bind (fun c -> RenderSg.get c (TraversalState.pushModelTrafo trafo ts))

        new(value : aval<Trafo3d>, child : ISg) = TrafoApplicator(value, AVal.constant child)
        new(value : Trafo3d, child : ISg) = TrafoApplicator(AVal.constant value, AVal.constant child)

    type ViewTrafoApplicator(trafo : aval<Trafo3d>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.ViewTrafo = trafo

        interface IRenderSg with
            member _.GetRenderObjects ts =
                child |> ASet.bind (fun c -> RenderSg.get c (TraversalState.withViewTrafo trafo ts))

        new(value : aval<Trafo3d>, child : ISg) = ViewTrafoApplicator(value, AVal.constant child)

    type ProjectionTrafoApplicator(trafo : aval<Trafo3d>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.ProjectionTrafo = trafo

        interface IRenderSg with
            member _.GetRenderObjects ts =
                child |> ASet.bind (fun c -> RenderSg.get c (TraversalState.withProjTrafo trafo ts))

        new(value : aval<Trafo3d>, child : ISg) = ProjectionTrafoApplicator(value, AVal.constant child)

    // Blending
    type BlendModeApplicator(mode : aval<BlendMode>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        interface IRenderSg with
            member _.GetRenderObjects ts =
                child |> ASet.bind (fun c -> RenderSg.get c (TraversalState.withBlendMode mode ts))

        new(value : aval<BlendMode>, child : ISg) = BlendModeApplicator(value, AVal.constant child)

    type BlendConstantApplicator(color : aval<C4f>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Color = color

        interface IRenderSg with
            member _.GetRenderObjects ts =
                child |> ASet.bind (fun c -> RenderSg.get c (TraversalState.withBlendConstant color ts))

        new(color : aval<C4f>, child : ISg) = BlendConstantApplicator(color, AVal.constant child)

    type ColorWriteMaskApplicator(mask : aval<ColorMask>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mask = mask

        interface IRenderSg with
            member _.GetRenderObjects ts =
                child |> ASet.bind (fun c -> RenderSg.get c (TraversalState.withColorWriteMask mask ts))

        new(enabled : aval<bool>, child : aval<ISg>) = ColorWriteMaskApplicator(enabled |> AVal.map ColorMask.enable, child)
        new(enabled : aval<bool>, child : ISg) = ColorWriteMaskApplicator(enabled, AVal.constant child)
        new(mask : aval<ColorMask>, child : ISg) = ColorWriteMaskApplicator(mask, AVal.constant child)

    type AttachmentBlendModeApplicator(value : aval<Map<Symbol, BlendMode>>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Modes = value

        interface IRenderSg with
            member _.GetRenderObjects ts =
                child |> ASet.bind (fun c -> RenderSg.get c (TraversalState.withAttachmentBlendMode value ts))

        new(value : aval<Map<Symbol, BlendMode>>, child : ISg) = AttachmentBlendModeApplicator(value, AVal.constant child)

    type AttachmentColorWriteMaskApplicator(value : aval<Map<Symbol, ColorMask>>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Masks = value

        interface IRenderSg with
            member _.GetRenderObjects ts =
                child |> ASet.bind (fun c -> RenderSg.get c (TraversalState.withAttachmentColorWriteMask value ts))

        new(value : aval<Map<Symbol, bool>>, child : aval<ISg>) = AttachmentColorWriteMaskApplicator(value |> AVal.map (Map.map (fun _ x -> ColorMask.enable x)), child)
        new(value : aval<Map<Symbol, ColorMask>>, child : ISg) = AttachmentColorWriteMaskApplicator(value, AVal.constant child)
        new(value : aval<Map<Symbol, bool>>, child : ISg) = AttachmentColorWriteMaskApplicator(value, AVal.constant child)

    // Depth
    type DepthTestApplicator(test : aval<DepthTest>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Test = test

        interface IRenderSg with
            member _.GetRenderObjects ts =
                child |> ASet.bind (fun c -> RenderSg.get c (TraversalState.withDepthTest test ts))

        new(test : aval<DepthTest>, child : ISg) = DepthTestApplicator(test, AVal.constant child)

    type DepthBiasApplicator(bias : aval<DepthBias>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.State = bias

        interface IRenderSg with
            member _.GetRenderObjects ts =
                child |> ASet.bind (fun c -> RenderSg.get c (TraversalState.withDepthBias bias ts))

        new(state : aval<DepthBias>, child : ISg) = DepthBiasApplicator(state, AVal.constant child)

    type DepthWriteMaskApplicator(writeEnabled : aval<bool>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.WriteEnabled = writeEnabled

        interface IRenderSg with
            member _.GetRenderObjects ts =
                child |> ASet.bind (fun c -> RenderSg.get c (TraversalState.withDepthWriteMask writeEnabled ts))

        new(writeEnabled : aval<bool>, child : ISg) = DepthWriteMaskApplicator(writeEnabled, AVal.constant child)

    type DepthClampApplicator(clamp : aval<bool>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Clamp = clamp

        interface IRenderSg with
            member _.GetRenderObjects ts =
                child |> ASet.bind (fun c -> RenderSg.get c (TraversalState.withDepthClamp clamp ts))

        new(clamp : aval<bool>, child : ISg) = DepthClampApplicator(clamp, AVal.constant child)

    // Stencil
    type StencilModeFrontApplicator(mode : aval<StencilMode>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        interface IRenderSg with
            member _.GetRenderObjects ts =
                child |> ASet.bind (fun c -> RenderSg.get c (TraversalState.withStencilModeFront mode ts))

        new(mode : aval<StencilMode>, child : ISg) = StencilModeFrontApplicator(mode, AVal.constant child)

    type StencilWriteMaskFrontApplicator(mask : aval<StencilMask>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mask = mask

        interface IRenderSg with
            member _.GetRenderObjects ts =
                child |> ASet.bind (fun c -> RenderSg.get c (TraversalState.withStencilWriteMaskFront mask ts))

        new(enabled : aval<bool>, child : aval<ISg>) = StencilWriteMaskFrontApplicator(enabled |> AVal.map StencilMask, child)
        new(enabled : aval<bool>, child : ISg) = StencilWriteMaskFrontApplicator(enabled, AVal.constant child)
        new(mask : aval<uint32>, child : aval<ISg>) = StencilWriteMaskFrontApplicator(mask |> AVal.map StencilMask, child)
        new(mask : aval<uint32>, child : ISg) = StencilWriteMaskFrontApplicator(mask, AVal.constant child)
        new(mask : aval<StencilMask>, child : ISg) = StencilWriteMaskFrontApplicator(mask, AVal.constant child)

    type StencilModeBackApplicator(mode : aval<StencilMode>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        interface IRenderSg with
            member _.GetRenderObjects ts =
                child |> ASet.bind (fun c -> RenderSg.get c (TraversalState.withStencilModeBack mode ts))

        new(mode : aval<StencilMode>, child : ISg) = StencilModeBackApplicator(mode, AVal.constant child)

    type StencilWriteMaskBackApplicator(mask : aval<StencilMask>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mask = mask

        interface IRenderSg with
            member _.GetRenderObjects ts =
                child |> ASet.bind (fun c -> RenderSg.get c (TraversalState.withStencilWriteMaskBack mask ts))

        new(enabled : aval<bool>, child : aval<ISg>) = StencilWriteMaskBackApplicator(enabled |> AVal.map StencilMask, child)
        new(enabled : aval<bool>, child : ISg) = StencilWriteMaskBackApplicator(enabled, AVal.constant child)
        new(mask : aval<uint32>, child : aval<ISg>) = StencilWriteMaskBackApplicator(mask |> AVal.map StencilMask, child)
        new(mask : aval<uint32>, child : ISg) = StencilWriteMaskBackApplicator(mask, AVal.constant child)
        new(mask : aval<StencilMask>, child : ISg) = StencilWriteMaskBackApplicator(mask, AVal.constant child)

    // Rasterizer
    type CullModeApplicator(mode : aval<CullMode>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        interface IRenderSg with
            member _.GetRenderObjects ts =
                child |> ASet.bind (fun c -> RenderSg.get c (TraversalState.withCullMode mode ts))

        new(value : aval<CullMode>, child : ISg) = CullModeApplicator(value, AVal.constant child)

    type FrontFacingApplicator(winding : aval<WindingOrder>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.WindingOrder = winding

        interface IRenderSg with
            member _.GetRenderObjects ts =
                child |> ASet.bind (fun c -> RenderSg.get c (TraversalState.withFrontFacing winding ts))

        new(winding : aval<WindingOrder>, child : ISg) = FrontFacingApplicator(winding, AVal.constant child)

    type FillModeApplicator(mode : aval<FillMode>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        interface IRenderSg with
            member _.GetRenderObjects ts =
                child |> ASet.bind (fun c -> RenderSg.get c (TraversalState.withFillMode mode ts))

        new(value : aval<FillMode>, child : ISg) = FillModeApplicator(value, AVal.constant child)
        new(value : FillMode, child : ISg) = FillModeApplicator(AVal.constant value, AVal.constant child)

    type MultisampleApplicator(state : aval<bool>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Multisample = state

        interface IRenderSg with
            member _.GetRenderObjects ts =
                child |> ASet.bind (fun c -> RenderSg.get c (TraversalState.withMultisample state ts))

        new(state : aval<bool>, child : ISg) = MultisampleApplicator(state, AVal.constant child)

    type ConservativeRasterApplicator(state : aval<bool>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.ConservativeRaster = state

        interface IRenderSg with
            member _.GetRenderObjects ts =
                child |> ASet.bind (fun c -> RenderSg.get c (TraversalState.withConservativeRaster state ts))

        new(state : aval<bool>, child : ISg) = ConservativeRasterApplicator(state, AVal.constant child)

    type ViewportApplicator private (uniforms : IUniformProvider, state : aval<Box2i> option, child : aval<ISg>) =
        inherit UniformApplicator(uniforms, child)

        member x.Viewport = state

        // Overrides the IRenderSg inherited from UniformApplicator so that the viewport
        // is set as well (the inherited implementation only pushes the uniform holder).
        interface IRenderSg with
            member _.GetRenderObjects ts =
                let ts' = ts |> TraversalState.pushUniforms uniforms |> TraversalState.withViewport state
                child |> ASet.bind (fun c -> RenderSg.get c ts')

        new(state : aval<Box2i> option, child : aval<ISg>) =
            let uniforms : IUniformProvider =
                match state with
                | Some vp -> new Providers.SingleUniformHolder(Symbol.Create "ViewportSize", vp |> AVal.map _.Size)
                | _ -> UniformProvider.Empty

            ViewportApplicator(uniforms, state, child)

        new(state : aval<Box2i> option, child : ISg) = ViewportApplicator(state, AVal.constant child)
        new(state : aval<Box2i>, child : aval<ISg>) = ViewportApplicator(Some state, child)
        new(state : aval<Box2i>, child : ISg) = ViewportApplicator(Some state, child)

    type ScissorApplicator(state : aval<Box2i> option, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Scissor = state

        interface IRenderSg with
            member _.GetRenderObjects ts =
                child |> ASet.bind (fun c -> RenderSg.get c (TraversalState.withScissor state ts))

        new(state : aval<Box2i> option, child : ISg) = ScissorApplicator(state, AVal.constant child)
        new(state : aval<Box2i>, child : aval<ISg>) = ScissorApplicator(Some state, child)
        new(state : aval<Box2i>, child : ISg) = ScissorApplicator(Some state, child)

    type Set(content : aset<ISg>) =

        interface IGroup with
            member x.Children = content

        interface IRenderSg with
            member _.GetRenderObjects ts =
                content |> ASet.collect (fun c -> RenderSg.get c ts)

        member x.ASet = content

        new([<ParamArray>] items: ISg[]) = Set(items |> ASet.ofArray)

        new(items : seq<ISg>) = Set(items |> ASet.ofSeq)
