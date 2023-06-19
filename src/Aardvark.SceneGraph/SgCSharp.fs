namespace Aardvark.SceneGraph.CSharp

open System
open Aardvark.Base

open Aardvark.Rendering
open System.Runtime.CompilerServices
open Aardvark.SceneGraph
open FSharp.Data.Adaptive

[<Extension>]
[<AbstractClass>]
[<Sealed>]
type GeometryExtensions =
    [<Extension>]
    static member ToSg(ig : IndexedGeometry) = ig |> Sg.ofIndexedGeometry

    [<Extension>]
    static member ToSg(ig : IndexedGeometry, instanceCount : int) = instanceCount |> Sg.ofIndexedGeometryInstanced ig

    [<Extension>]
    static member ToSg(ig : IndexedGeometry, instanceCount : aval<int>) = instanceCount |> Sg.ofIndexedGeometryInstancedA ig


[<Extension>]
[<AbstractClass>]
[<Sealed>]
type SceneGraphExtensions =

    [<Extension>]
    static member ToSg(sg : seq<ISg>) = Sg.ofSeq sg

    [<Extension>]
    static member ViewTrafo(sg : ISg, view : aval<Trafo3d>) = Sg.viewTrafo view sg

    [<Extension>]
    static member ProjTrafo(sg : ISg, proj : aval<Trafo3d>) = Sg.projTrafo proj sg

    [<Extension>]
    static member Trafo(sg : ISg, modelTrafo : aval<Trafo3d>) = Sg.trafo modelTrafo sg

    [<Extension>]
    static member Trafo(sg : ISg, modelTrafo : Trafo3d) = Sg.trafo (AVal.constant modelTrafo) sg

    [<Extension>]
    static member Surface(sg : ISg, surface : ISurface) = Sg.SurfaceApplicator(match surface with
                                                                                   | :? FShadeSurface as fs -> Surface.FShadeSimple fs.Effect
                                                                                   | :? IBackendSurface as bs -> Surface.Backend bs
                                                                                   | _ -> failwith "unsupported surface"
                                                                                , sg) :> ISg

    [<Extension>]
    static member Surface(sg : ISg, surface : Surface) = Sg.SurfaceApplicator(surface, sg) :> ISg

    [<Extension>]
    static member Surface(sg : ISg, surface : IBackendSurface) = Sg.SurfaceApplicator(Surface.Backend surface, sg) :> ISg

    [<Extension>]
    static member Surface(sg : ISg, effect : FShade.Effect) = Sg.SurfaceApplicator(Surface.FShadeSimple effect, sg) :> ISg

    [<Extension>]
    static member Surface(sg : ISg, [<ParamArray>] effects : FShade.Effect[]) = Sg.SurfaceApplicator(Surface.FShadeSimple (FShade.Effect.compose effects), sg) :> ISg

    [<Extension>]
    static member Surface(sg : ISg, creator : Func<FShade.EffectConfig, FShade.EffectInputLayout*aval<FShade.Imperative.Module>>) =
        Sg.SurfaceApplicator(Surface.FShade (fun cfg -> creator.Invoke(cfg)), sg) :> ISg

    // Blending
    [<Extension>]
    static member BlendMode(sg : ISg, mode : aval<BlendMode>) = sg |> Sg.blendMode mode
    [<Extension>]
    static member BlendMode(sg : ISg, mode : BlendMode)       = sg |> Sg.blendMode' mode

    [<Extension>]
    static member BlendModes(sg : ISg, modes : aval<Map<Symbol, BlendMode>>) = sg |> Sg.blendModes modes
    [<Extension>]
    static member BlendModes(sg : ISg, modes : Map<Symbol, BlendMode>)       = sg |> Sg.blendModes' modes

    [<Extension>]
    static member BlendConstant(sg : ISg, color : aval<C3f>) = sg |> Sg.blendConstant color
    [<Extension>]
    static member BlendConstant(sg : ISg, color : aval<C4f>) = sg |> Sg.blendConstant color
    [<Extension>]
    static member BlendConstant(sg : ISg, color : aval<C3d>) = sg |> Sg.blendConstant color
    [<Extension>]
    static member BlendConstant(sg : ISg, color : aval<C4d>) = sg |> Sg.blendConstant color
    [<Extension>]
    static member BlendConstant(sg : ISg, color : aval<C3b>) = sg |> Sg.blendConstant color
    [<Extension>]
    static member BlendConstant(sg : ISg, color : aval<C4b>) = sg |> Sg.blendConstant color
    [<Extension>]
    static member BlendConstant(sg : ISg, color : aval<C3ui>) = sg |> Sg.blendConstant color
    [<Extension>]
    static member BlendConstant(sg : ISg, color : aval<C4ui>) = sg |> Sg.blendConstant color
    [<Extension>]
    static member BlendConstant(sg : ISg, color : C3f) = sg |> Sg.blendConstant' color
    [<Extension>]
    static member BlendConstant(sg : ISg, color : C4f) = sg |> Sg.blendConstant' color
    [<Extension>]
    static member BlendConstant(sg : ISg, color : C3d) = sg |> Sg.blendConstant' color
    [<Extension>]
    static member BlendConstant(sg : ISg, color : C4d) = sg |> Sg.blendConstant' color
    [<Extension>]
    static member BlendConstant(sg : ISg, color : C3b) = sg |> Sg.blendConstant' color
    [<Extension>]
    static member BlendConstant(sg : ISg, color : C4b) = sg |> Sg.blendConstant' color
    [<Extension>]
    static member BlendConstant(sg : ISg, color : C3ui) = sg |> Sg.blendConstant' color
    [<Extension>]
    static member BlendConstant(sg : ISg, color : C4ui) = sg |> Sg.blendConstant' color

    [<Extension>]
    static member ColorMask(sg : ISg, mask : aval<ColorMask>) = sg |> Sg.colorMask mask
    [<Extension>]
    static member ColorMask(sg : ISg, mask : ColorMask)       = sg |> Sg.colorMask' mask

    [<Extension>]
    static member ColorMasks(sg : ISg, masks : aval<Map<Symbol, ColorMask>>) = sg |> Sg.colorMasks masks
    [<Extension>]
    static member ColorMasks(sg : ISg, masks : Map<Symbol, ColorMask>)       = sg |> Sg.colorMasks' masks

    [<Extension>]
    static member ColorWrite(sg : ISg, enabled : aval<bool>) = sg |> Sg.colorWrite enabled
    [<Extension>]
    static member ColorWrite(sg : ISg, enabled : bool)       = sg |> Sg.colorWrite' enabled

    [<Extension>]
    static member ColorWrites(sg : ISg, enabled : aval<Map<Symbol, bool>>) = sg |> Sg.colorWrites enabled
    [<Extension>]
    static member ColorWrites(sg : ISg, enabled : Map<Symbol, bool>)       = sg |> Sg.colorWrites' enabled

    [<Extension>]
    static member ColorOutput(sg : ISg, enabled : aval<Set<Symbol>>) = sg |> Sg.colorOutput enabled
    [<Extension>]
    static member ColorOutput(sg : ISg, enabled : Set<Symbol>)       = sg |> Sg.colorOutput' enabled
    [<Extension>]
    static member ColorOutput(sg : ISg, enabled : seq<Symbol>)       = sg |> Sg.colorOutput' (Set.ofSeq enabled)

    // Depth
    [<Extension>]
    static member DepthTest(sg : ISg, test : aval<DepthTest>) = sg |> Sg.depthTest test
    [<Extension>]
    static member DepthTest(sg : ISg, test : DepthTest) = sg |> Sg.depthTest' test

    [<Extension>]
    static member DepthWrite(sg : ISg, depthWriteEnabled : aval<bool>) = sg |> Sg.depthWrite depthWriteEnabled
    [<Extension>]
    static member DepthWrite(sg : ISg, depthWriteEnabled : bool) = sg |> Sg.depthWrite' depthWriteEnabled

    [<Extension>]
    static member DepthBias(sg : ISg, bias : aval<DepthBias>) = sg |> Sg.depthBias bias
    [<Extension>]
    static member DepthBias(sg : ISg, bias : DepthBias) = sg |> Sg.depthBias' bias

    [<Extension>]
    static member DepthClamp(sg : ISg, clamp : aval<bool>) = sg |> Sg.depthClamp clamp
    [<Extension>]
    static member DepthClamp(sg : ISg, clamp : bool) = sg |> Sg.depthClamp' clamp

    // Stencil
    [<Extension>]
    static member StencilModeFront(sg : ISg, mode : aval<StencilMode>) = sg |> Sg.stencilModeFront mode
    [<Extension>]
    static member StencilModeFront(sg : ISg, mode : StencilMode) = sg |> Sg.stencilModeFront' mode

    [<Extension>]
    static member StencilWriteMaskFront(sg : ISg, mask : aval<StencilMask>) = sg |> Sg.stencilWriteMaskFront mask
    [<Extension>]
    static member StencilWriteMaskFront(sg : ISg, mask : StencilMask) = sg |> Sg.stencilWriteMaskFront' mask

    [<Extension>]
    static member StencilWriteFront(sg : ISg, enabled : aval<bool>) = sg |> Sg.stencilWriteFront enabled
    [<Extension>]
    static member StencilWriteFront(sg : ISg, enabled : bool) = sg |> Sg.stencilWriteFront' enabled

    [<Extension>]
    static member StencilModeBack(sg : ISg, mode : aval<StencilMode>) = sg |> Sg.stencilModeBack mode
    [<Extension>]
    static member StencilModeBack(sg : ISg, mode : StencilMode) = sg |> Sg.stencilModeBack' mode

    [<Extension>]
    static member StencilWriteMaskBack(sg : ISg, mask : aval<StencilMask>) = sg |> Sg.stencilWriteMaskBack mask
    [<Extension>]
    static member StencilWriteMaskBack(sg : ISg, mask : StencilMask) = sg |> Sg.stencilWriteMaskBack' mask

    [<Extension>]
    static member StencilWriteBack(sg : ISg, enabled : aval<bool>) = sg |> Sg.stencilWriteBack enabled
    [<Extension>]
    static member StencilWriteBack(sg : ISg, enabled : bool) = sg |> Sg.stencilWriteBack' enabled

    [<Extension>]
    static member StencilMode(sg : ISg, mode : aval<StencilMode>) = sg |> Sg.stencilMode mode
    [<Extension>]
    static member StencilMode(sg : ISg, mode : StencilMode) = sg |> Sg.stencilMode' mode

    [<Extension>]
    static member StencilMode(sg : ISg, front : aval<StencilMode>, back : aval<StencilMode>) = sg |> Sg.stencilModes front back
    [<Extension>]
    static member StencilMode(sg : ISg, front : StencilMode, back : StencilMode) = sg |> Sg.stencilModes' front back

    [<Extension>]
    static member StencilWriteMask(sg : ISg, mask : aval<StencilMask>) = sg |> Sg.stencilWriteMask mask
    [<Extension>]
    static member StencilWriteMask(sg : ISg, mask : StencilMask) = sg |> Sg.stencilWriteMask' mask

    [<Extension>]
    static member StencilWriteMask(sg : ISg, front : aval<StencilMask>, back : aval<StencilMask>) = sg |> Sg.stencilWriteMasks front back
    [<Extension>]
    static member StencilWriteMask(sg : ISg, front : StencilMask, back : StencilMask) = sg |> Sg.stencilWriteMasks' front back

    [<Extension>]
    static member StencilWrite(sg : ISg, enabled : aval<bool>) = sg |> Sg.stencilWrite enabled
    [<Extension>]
    static member StencilWrite(sg : ISg, enabled : bool) = sg |> Sg.stencilWrite' enabled

    [<Extension>]
    static member StencilWrite(sg : ISg, front : aval<bool>, back : aval<bool>) = sg |> Sg.stencilWrites front back
    [<Extension>]
    static member StencilWrite(sg : ISg, front : bool, back : bool) = sg |> Sg.stencilWrites' front back

    // Rasterizer
    [<Extension>]
    static member CullMode(sg : ISg, mode : aval<CullMode>) = sg |> Sg.cullMode mode
    [<Extension>]
    static member CullMode(sg : ISg, mode : CullMode) = sg |> Sg.cullMode' mode

    [<Extension>]
    static member FrontFacing(sg : ISg, order : aval<WindingOrder>) = sg |> Sg.frontFacing order
    [<Extension>]
    static member FrontFacing(sg : ISg, order : WindingOrder) = sg |> Sg.frontFacing' order

    [<Extension>]
    [<Obsolete("Use frontFacing with reversed winding order instead. See: https://github.com/aardvark-platform/aardvark.rendering/issues/101")>]
    static member FrontFace(sg : ISg, order : aval<WindingOrder>) = sg |> Sg.frontFacing (order |> AVal.mapNonAdaptive WindingOrder.reverse)
    [<Extension>]
    [<Obsolete("Use frontFacing with reversed winding order instead. See: https://github.com/aardvark-platform/aardvark.rendering/issues/101")>]
    static member FrontFace(sg : ISg, order : WindingOrder) = sg |> Sg.frontFacing' (order |> WindingOrder.reverse)

    [<Extension>]
    static member FillMode(sg : ISg, mode : aval<FillMode>) = sg |> Sg.fillMode mode
    [<Extension>]
    static member FillMode(sg : ISg, mode : FillMode) = sg |> Sg.fillMode' mode

    [<Extension>]
    static member Multisample(sg : ISg, mode : aval<bool>) = sg |> Sg.multisample mode
    [<Extension>]
    static member Multisample(sg : ISg, mode : bool) = sg |> Sg.multisample' mode

    [<Extension>]
    static member ConservativeRaster(sg : ISg, mode : aval<bool>) = sg |> Sg.conservativeRaster mode
    [<Extension>]
    static member ConservativeRaster(sg : ISg, mode : bool) = sg |> Sg.conservativeRaster' mode

    // Write buffers
    [<Extension>]
    static member WriteBuffers(sg : ISg, bufferIdentifiers : aval<WriteBuffer seq>) : ISg = sg |> Sg.writeBuffers (bufferIdentifiers |> AVal.map Set.ofSeq)

    [<Extension>]
    static member WriteBuffers(sg : ISg, bufferIdentifiers : seq<WriteBuffer>) : ISg = sg |> Sg.writeBuffers' (Set.ofSeq bufferIdentifiers)

    [<Extension>]
    static member WriteBuffers(sg : ISg, [<ParamArray>] bufferIdentifiers: WriteBuffer[]) : ISg = sg |> Sg.writeBuffers' (Set.ofArray bufferIdentifiers)


    [<Extension>]
    static member WithEffects(sg : ISg, effects : seq<FShadeEffect>) : ISg = Sg.effect effects sg

    [<Extension>]
    static member Uniform(sg : ISg, name : Symbol, value : IAdaptiveValue) : ISg = Sg.UniformApplicator(name, value, sg) :> ISg

    [<Extension>]
    static member Uniform<'a>(sg : ISg, name : TypedSymbol<'a>, value : aval<'a>) : ISg = Sg.UniformApplicator(name.Symbol, value, sg) :> ISg

    [<Extension>]
    static member Uniform(sg : ISg, uniforms : IUniformProvider) : ISg = Sg.UniformApplicator(uniforms, sg) :> ISg

    [<Extension>]
    static member Uniform(sg : ISg, uniforms : SymbolDict<IAdaptiveValue>) : ISg = Sg.UniformApplicator(UniformProvider.ofDict uniforms, sg) :> ISg

    [<Extension>]
    static member VertexIndices(sg : ISg, indices : BufferView) : ISg = Sg.VertexIndexApplicator(indices, sg) :> ISg

    [<Extension>]
    static member VertexIndices(sg : ISg, indices : Array) : ISg = Sg.VertexIndexApplicator(BufferView.ofArray indices, sg) :> ISg

    [<Extension>]
    static member VertexAttributes(sg : ISg, attributes : SymbolDict<BufferView>) : ISg = Sg.VertexAttributeApplicator(attributes, sg) :> ISg

    [<Extension>]
    static member VertexAttribute(sg : ISg, attribute : Symbol, data : BufferView) : ISg = Sg.VertexAttributeApplicator(attribute, data, sg) :> ISg

    [<Extension>]
    static member VertexAttribute(sg : ISg, attribute : Symbol, data : Array) : ISg = Sg.VertexAttributeApplicator(attribute, BufferView.ofArray data, sg) :> ISg

    [<Extension>]
    static member VertexAttribute(sg : ISg, attribute : Symbol, data : aval<Array>) : ISg = Sg.VertexAttributeApplicator(attribute, BufferView(AVal.map (fun x -> (ArrayBuffer(x) :> IBuffer)) data, data.GetValue().GetType().GetElementType()), sg) :> ISg

    [<Extension>]
    static member VertexAttribute<'a>(sg : ISg, attribute : Symbol, data : aval<'a[]>) : ISg = Sg.VertexAttributeApplicator(attribute, BufferView(AVal.map (fun x -> (ArrayBuffer(x) :> IBuffer)) data, data.GetValue().GetType().GetElementType()), sg) :> ISg

    [<Extension>]
    static member Pass(sg : ISg, renderPass : RenderPass) : ISg = Sg.PassApplicator(renderPass, sg) :> ISg

    [<Extension>]
    static member OnOff(sg : ISg, on : aval<bool>) : ISg = Sg.OnOffNode(on, sg) :> ISg

    [<Extension>]
    static member InstanceAttribute(sg : ISg, attribute : Symbol, data : BufferView) : ISg = Sg.InstanceAttributeApplicator(attribute, data, sg) :> ISg

    [<Extension>]
    static member InstanceAttribute(sg : ISg, attribute : Symbol, data : Array) : ISg = Sg.InstanceAttributeApplicator(attribute, BufferView.ofArray data, sg) :> ISg

    [<Extension>]
    static member InstanceAttribute(sg : ISg, attribute : Symbol, data : aval<Array>) : ISg = Sg.InstanceAttributeApplicator(attribute, BufferView(AVal.map (fun x -> (ArrayBuffer(x) :> IBuffer)) data, data.GetValue().GetType().GetElementType()), sg) :> ISg

    [<Extension>]
    static member InstanceAttribute(sg : ISg, attribute : Symbol, data : aval<'a[]>) : ISg = Sg.InstanceAttributeApplicator(attribute, BufferView(AVal.map (fun x -> (ArrayBuffer(x) :> IBuffer)) data, data.GetValue().GetType().GetElementType()), sg) :> ISg


[<Extension>]
[<AbstractClass>]
[<Sealed>]
type SceneGraphTools =

    [<Extension>]
    static member NormalizeToAdaptive (this : ISg, box : Box3d) = Sg.normalizeToAdaptive box this

    [<Extension>]
    static member NormalizeAdaptive (this : ISg)  = Sg.normalizeAdaptive this

