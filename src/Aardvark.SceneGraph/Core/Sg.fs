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

    type DynamicNode(child : aval<ISg>) = inherit AbstractApplicator(child)

    type RenderNode(call : aval<DrawCallInfo>, mode : IndexedGeometryMode) =
        interface ISg

        member x.Mode = mode
        member x.DrawCallInfo = call

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

    type IndirectRenderNode(buffer : aval<IndirectBuffer>, mode : IndexedGeometryMode) =
        interface ISg

        member x.Mode = mode
        member x.Indirect = buffer

    type VertexAttributeApplicator(values : Map<Symbol, BufferView>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Values = values

        new(values : Map<Symbol, BufferView>, child : ISg)            = VertexAttributeApplicator(values, AVal.constant child)
        new(semantic : Symbol, value : BufferView, child : aval<ISg>) = VertexAttributeApplicator(Map.ofList [semantic, value], child)
        new(semantic : Symbol, value : BufferView, child : ISg)       = VertexAttributeApplicator(Map.ofList [semantic, value], AVal.constant child)
        new(values : SymbolDict<BufferView>, child : ISg)             = VertexAttributeApplicator(values |> Seq.map (fun (KeyValue(k,v)) -> k,v) |> Map.ofSeq, AVal.constant child)

    type VertexIndexApplicator(value : BufferView, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Value = value

        new(value : BufferView, child : ISg)         = VertexIndexApplicator(value, AVal.constant child)

    type InstanceAttributeApplicator(values : Map<Symbol, BufferView>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Values = values

        new(values : Map<Symbol, BufferView>, child : ISg)            = InstanceAttributeApplicator(values, AVal.constant child)
        new(semantic : Symbol, value : BufferView, child : aval<ISg>) = InstanceAttributeApplicator(Map.ofList [semantic, value], child)
        new(semantic : Symbol, value : BufferView, child : ISg)       = InstanceAttributeApplicator(Map.ofList [semantic, value], AVal.constant child)
        new(values : SymbolDict<BufferView>, child : ISg)             = InstanceAttributeApplicator(values |> Seq.map (fun (KeyValue(k,v)) -> k,v) |> Map.ofSeq, AVal.constant child)


    type OnOffNode(on : aval<bool>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.IsActive = on

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

        new(activate : unit -> IDisposable, child : ISg) = ActivationApplicator(activate, AVal.constant child)

    // TODO: Caching?
    type DelayNode(generator : Ag.Scope -> ISg) =
        interface ISg
        member x.Generator = generator

    type PassApplicator(pass : RenderPass, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Pass = pass

        new(pass : RenderPass, child : ISg) = PassApplicator(pass, AVal.constant child)

    type UniformApplicator(uniformHolder : IUniformProvider, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member internal x.Uniforms = uniformHolder

        member x.TryFindUniform (scope : Scope) (name : Symbol) =
            uniformHolder.TryGetUniform (scope,name)

        new(value : IUniformProvider, child : ISg) = UniformApplicator( value, AVal.constant child)
        new(name : string, value : IAdaptiveValue, child : ISg) = UniformApplicator( (new Providers.SingleUniformHolder(Symbol.Create name, value) :> IUniformProvider), AVal.constant child)
        new(name : Symbol, value : IAdaptiveValue, child : ISg) = UniformApplicator( (new Providers.SingleUniformHolder(name, value) :> IUniformProvider), AVal.constant child)
        new(name : Symbol, value : IAdaptiveValue, child : aval<ISg>) = UniformApplicator( (new Providers.SingleUniformHolder(name, value) :> IUniformProvider), child)
        new(map : Map<Symbol,IAdaptiveValue>, child : ISg) = UniformApplicator( UniformProvider.ofMap map, AVal.constant child)


    type SurfaceApplicator(surface : Surface, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Surface = surface

        new(value : Surface, child : ISg) = SurfaceApplicator(value, AVal.constant child)

        new(value : ISurface, child : ISg) = SurfaceApplicator(Surface.Backend value, AVal.constant child)
        new(value : ISurface, child : aval<ISg>) = SurfaceApplicator(Surface.Backend value, child)

    type TextureApplicator(semantic : Symbol, texture : aval<ITexture>, child : aval<ISg>) =
        inherit UniformApplicator(semantic, texture :> IAdaptiveValue, child)

        member x.Texture = texture

        new(semantic : Symbol, texture : aval<ITexture>, child : ISg) = TextureApplicator(semantic, texture, AVal.constant child)
        new(texture : aval<ITexture>, child : ISg) = TextureApplicator(DefaultSemantic.DiffuseColorTexture, texture, child)
        new(texture : aval<ITexture>, child : aval<ISg>) = TextureApplicator(DefaultSemantic.DiffuseColorTexture,texture,child)


    type TrafoApplicator(trafo : aval<Trafo3d>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Trafo = trafo

        new(value : aval<Trafo3d>, child : ISg) = TrafoApplicator(value, AVal.constant child)
        new(value : Trafo3d, child : ISg) = TrafoApplicator(AVal.constant value, AVal.constant child)

    type ViewTrafoApplicator(trafo : aval<Trafo3d>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.ViewTrafo = trafo

        new(value : aval<Trafo3d>, child : ISg) = ViewTrafoApplicator(value, AVal.constant child)

    type ProjectionTrafoApplicator(trafo : aval<Trafo3d>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.ProjectionTrafo = trafo

        new(value : aval<Trafo3d>, child : ISg) = ProjectionTrafoApplicator(value, AVal.constant child)

    // Blending
    type BlendModeApplicator(mode : aval<BlendMode>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        new(value : aval<BlendMode>, child : ISg) = BlendModeApplicator(value, AVal.constant child)

    type BlendConstantApplicator(color : aval<C4f>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Color = color

        new(color : aval<C4f>, child : ISg) = BlendConstantApplicator(color, AVal.constant child)

    type ColorWriteMaskApplicator(mask : aval<ColorMask>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mask = mask

        new(enabled : aval<bool>, child : aval<ISg>) = ColorWriteMaskApplicator(enabled |> AVal.map ColorMask.enable, child)
        new(enabled : aval<bool>, child : ISg) = ColorWriteMaskApplicator(enabled, AVal.constant child)
        new(mask : aval<ColorMask>, child : ISg) = ColorWriteMaskApplicator(mask, AVal.constant child)

    type AttachmentBlendModeApplicator(value : aval<Map<Symbol, BlendMode>>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Modes = value

        new(value : aval<Map<Symbol, BlendMode>>, child : ISg) = AttachmentBlendModeApplicator(value, AVal.constant child)

    type AttachmentColorWriteMaskApplicator(value : aval<Map<Symbol, ColorMask>>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Masks = value

        new(value : aval<Map<Symbol, bool>>, child : aval<ISg>) = AttachmentColorWriteMaskApplicator(value |> AVal.map (Map.map (fun _ x -> ColorMask.enable x)), child)
        new(value : aval<Map<Symbol, ColorMask>>, child : ISg) = AttachmentColorWriteMaskApplicator(value, AVal.constant child)
        new(value : aval<Map<Symbol, bool>>, child : ISg) = AttachmentColorWriteMaskApplicator(value, AVal.constant child)

    // Depth
    type DepthTestApplicator(test : aval<DepthTest>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Test = test

        new(test : aval<DepthTest>, child : ISg) = DepthTestApplicator(test, AVal.constant child)

     type DepthBiasApplicator(bias : aval<DepthBias>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.State = bias

        new(state : aval<DepthBias>, child : ISg) = DepthBiasApplicator(state, AVal.constant child)

    type DepthWriteMaskApplicator(writeEnabled : aval<bool>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.WriteEnabled = writeEnabled

        new(writeEnabled : aval<bool>, child : ISg) = DepthWriteMaskApplicator(writeEnabled, AVal.constant child)

    type DepthClampApplicator(clamp : aval<bool>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Clamp = clamp

        new(clamp : aval<bool>, child : ISg) = DepthClampApplicator(clamp, AVal.constant child)

    // Stencil
    type StencilModeFrontApplicator(mode : aval<StencilMode>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        new(mode : aval<StencilMode>, child : ISg) = StencilModeFrontApplicator(mode, AVal.constant child)

    type StencilWriteMaskFrontApplicator(mask : aval<StencilMask>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mask = mask

        new(enabled : aval<bool>, child : aval<ISg>) = StencilWriteMaskFrontApplicator(enabled |> AVal.map StencilMask, child)
        new(enabled : aval<bool>, child : ISg) = StencilWriteMaskFrontApplicator(enabled, AVal.constant child)
        new(mask : aval<uint32>, child : aval<ISg>) = StencilWriteMaskFrontApplicator(mask |> AVal.map StencilMask, child)
        new(mask : aval<uint32>, child : ISg) = StencilWriteMaskFrontApplicator(mask, AVal.constant child)
        new(mask : aval<StencilMask>, child : ISg) = StencilWriteMaskFrontApplicator(mask, AVal.constant child)

    type StencilModeBackApplicator(mode : aval<StencilMode>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        new(mode : aval<StencilMode>, child : ISg) = StencilModeBackApplicator(mode, AVal.constant child)

    type StencilWriteMaskBackApplicator(mask : aval<StencilMask>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mask = mask

        new(enabled : aval<bool>, child : aval<ISg>) = StencilWriteMaskBackApplicator(enabled |> AVal.map StencilMask, child)
        new(enabled : aval<bool>, child : ISg) = StencilWriteMaskBackApplicator(enabled, AVal.constant child)
        new(mask : aval<uint32>, child : aval<ISg>) = StencilWriteMaskBackApplicator(mask |> AVal.map StencilMask, child)
        new(mask : aval<uint32>, child : ISg) = StencilWriteMaskBackApplicator(mask, AVal.constant child)
        new(mask : aval<StencilMask>, child : ISg) = StencilWriteMaskBackApplicator(mask, AVal.constant child)

    // Rasterizer
    type CullModeApplicator(mode : aval<CullMode>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        new(value : aval<CullMode>, child : ISg) = CullModeApplicator(value, AVal.constant child)

    type FrontFaceApplicator(winding : aval<WindingOrder>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.WindingOrder = winding

        new(winding : aval<WindingOrder>, child : ISg) = FrontFaceApplicator(winding, AVal.constant child)

    type FillModeApplicator(mode : aval<FillMode>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        new(value : aval<FillMode>, child : ISg) = FillModeApplicator(value, AVal.constant child)
        new(value : FillMode, child : ISg) = FillModeApplicator(AVal.constant value, AVal.constant child)

    type MultisampleApplicator(state : aval<bool>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Multisample = state

        new(state : aval<bool>, child : ISg) = MultisampleApplicator(state, AVal.constant child)

    type ConservativeRasterApplicator(state : aval<bool>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.ConservativeRaster = state

        new(state : aval<bool>, child : ISg) = ConservativeRasterApplicator(state, AVal.constant child)


    type Set(content : aset<ISg>) =

        interface IGroup with
            member x.Children = content

        member x.ASet = content

        new([<ParamArray>] items: ISg[]) = Set(items |> ASet.ofArray)

        new(items : seq<ISg>) = Set(items |> ASet.ofSeq)