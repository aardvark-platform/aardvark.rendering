namespace Aardvark.SceneGraph.CSharp

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Ag
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Aardvark.SceneGraph
open FSharp.Data.Adaptive

[<Extension>]
[<AbstractClass>]
[<Sealed>]
type GeometryExtensions =
    [<Extension>]
    static member ToSg(ig : IndexedGeometry) = ig |> Sg.ofIndexedGeometry

type QuickUniformHolder(values : SymbolDict<IAdaptiveValue>) =
    interface IUniformProvider with
        member x.TryGetUniform (scope,name) = let (success, value) = values.TryGetValue(name)
                                              if success then Some value else None
        member x.Dispose() = ()

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
    static member Surface(sg : ISg, [<ParamArray>] effects : FShade.Effect[]) = Sg.SurfaceApplicator(Surface.FShadeSimple (FShade.Effect.compose effects), sg) :> ISg

    [<Extension>]
    static member Surface(sg : ISg, creator : Func<FShade.EffectConfig, FShade.EffectInputLayout*aval<FShade.Imperative.Module>>) = 
        Sg.SurfaceApplicator(Surface.FShade (fun cfg -> creator.Invoke(cfg)), sg) :> ISg

    [<Extension>]
    static member FillMode(sg : ISg, mode : FillMode) = Sg.FillModeApplicator(AVal.constant mode, sg) :> ISg

    [<Extension>]
    static member FillMode(sg : ISg, mode : aval<FillMode>) = Sg.FillModeApplicator(mode, sg) :> ISg

    [<Extension>]
    static member CullMode(sg : ISg, mode : CullMode) = Sg.CullModeApplicator(AVal.constant mode, sg) :> ISg

    [<Extension>]
    static member CullMode(sg : ISg, mode : aval<CullMode>) = Sg.CullModeApplicator(mode, sg) :> ISg

    [<Extension>]
    static member FrontFace(sg : ISg, frontFace : WindingOrder) = Sg.FrontFaceApplicator(AVal.constant frontFace, sg) :> ISg

    [<Extension>]
    static member FrontFace(sg : ISg, frontFace : aval<WindingOrder>) = Sg.FrontFaceApplicator(frontFace, sg) :> ISg

   
    [<Extension>]
    static member BlendMode(sg : ISg, mode : BlendMode) = Sg.BlendModeApplicator(AVal.constant mode, sg) :> ISg

    [<Extension>]
    static member BlendMode(sg : ISg, mode : aval<BlendMode>) = Sg.BlendModeApplicator(mode, sg) :> ISg

   
    [<Extension>]
    static member StencilMode(sg : ISg, mode : StencilMode) = Sg.StencilModeApplicator(AVal.constant mode, sg) :> ISg

    [<Extension>]
    static member StencilMode(sg : ISg, mode : aval<StencilMode>) = Sg.StencilModeApplicator(mode, sg) :> ISg

    
    [<Extension>]
    static member DepthTestMode(sg : ISg, mode : DepthTestMode) = Sg.DepthTestModeApplicator(AVal.constant mode, sg) :> ISg

    [<Extension>]
    static member DepthTestMode(sg : ISg, mode : aval<DepthTestMode>) = Sg.DepthTestModeApplicator(mode, sg) :> ISg

    [<Extension>]
    static member DepthBias(sg : ISg, constant : float, slopeScale : float) = Sg.DepthBiasApplicator(AVal.constant (DepthBiasState(constant, slopeScale, 0.0)), sg) :> ISg

    [<Extension>]
    static member DepthBias(sg : ISg, constant : float, slopeScale : float, clamp : float) = Sg.DepthBiasApplicator(AVal.constant (DepthBiasState(constant, slopeScale, clamp)), sg) :> ISg

    [<Extension>]
    static member DepthBias(sg : ISg, state : DepthBiasState) = Sg.DepthBiasApplicator(AVal.constant state, sg) :> ISg

    [<Extension>]
    static member DepthBias(sg : ISg, state : aval<DepthBiasState>) = Sg.DepthBiasApplicator(state, sg) :> ISg

    [<Extension>]
    static member WithEffects(sg : ISg, effects : seq<FShadeEffect>) : ISg = Sg.effect effects sg

    [<Extension>]
    static member Uniform(sg : ISg, name : Symbol, value : IAdaptiveValue) : ISg = Sg.UniformApplicator(name, value, sg) :> ISg

    [<Extension>]
    static member Uniform<'a>(sg : ISg, name : TypedSymbol<'a>, value : aval<'a>) : ISg = Sg.UniformApplicator(name.Symbol, value, sg) :> ISg
    
    [<Extension>]
    static member Uniform(sg : ISg, uniforms : IUniformProvider) : ISg = Sg.UniformApplicator(uniforms, sg) :> ISg

    [<Extension>]
    static member Uniform(sg : ISg, uniforms : SymbolDict<IAdaptiveValue>) : ISg = Sg.UniformApplicator(new QuickUniformHolder(uniforms), sg) :> ISg

    [<Extension>]
    static member VertexIndices(sg : ISg, indices : BufferView) : ISg = Sg.VertexIndexApplicator(indices, sg) :> ISg

    [<Extension>]
    static member VertexAttributes(sg : ISg, attributes : SymbolDict<BufferView>) : ISg = Sg.VertexAttributeApplicator(attributes, sg) :> ISg

    [<Extension>]
    static member VertexAttribute(sg : ISg, attribute : Symbol, data : Array) : ISg = Sg.VertexAttributeApplicator(attribute, BufferView(AVal.constant (ArrayBuffer(data) :> IBuffer), data.GetType().GetElementType()), sg) :> ISg

    [<Extension>]
    static member VertexAttribute(sg : ISg, attribute : Symbol, data : aval<Array>) : ISg = Sg.VertexAttributeApplicator(attribute, BufferView(AVal.map (fun x -> (ArrayBuffer(x) :> IBuffer)) data, data.GetValue().GetType().GetElementType()), sg) :> ISg

    [<Extension>]
    static member Pass(sg : ISg, renderPass : RenderPass) : ISg = Sg.PassApplicator(renderPass, sg) :> ISg

    [<Extension>]
    static member WriteBuffers(sg : ISg, bufferIdentifiers : seq<Symbol>) : ISg = Sg.WriteBuffersApplicator(Some (Set.ofSeq bufferIdentifiers), sg) :> ISg

    [<Extension>]
    static member WriteBuffers(sg : ISg, [<ParamArray>] bufferIdentifiers: Symbol[]) : ISg = Sg.WriteBuffersApplicator(Some (Set.ofArray bufferIdentifiers), sg) :> ISg
    
    [<Extension>]
    static member OnOff(sg : ISg, on : aval<bool>) : ISg = Sg.OnOffNode(on, sg) :> ISg


[<Extension>]
[<AbstractClass>]
[<Sealed>]
type SceneGraphTools =

    [<Extension>]
    static member NormalizeToAdaptive (this : ISg, box : Box3d) = Sg.normalizeToAdaptive box this

    [<Extension>]
    static member NormalizeAdaptive (this : ISg)  = Sg.normalizeAdaptive this 
     
