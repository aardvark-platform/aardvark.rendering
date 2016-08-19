namespace Aardvark.SceneGraph.CSharp

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Ag
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Aardvark.SceneGraph
open Aardvark.Base.Incremental

[<Extension>]
[<AbstractClass>]
[<Sealed>]
type GeometryExtensions =
    [<Extension>]
    static member ToSg(ig : IndexedGeometry) = ig |> Sg.ofIndexedGeometry

type QuickUniformHolder(values : SymbolDict<IMod>) =
    interface IUniformProvider with
        member x.TryGetUniform (scope,name) = let (success, value) = values.TryGetValue(name)
                                              if success then Some value else None
        member x.Dispose() = ()

[<Extension>]
[<AbstractClass>]
[<Sealed>]
type SceneGraphExtensions =


    [<Extension>]
    static member CameraView(sg : ISg, view : ICameraView) = Sg.ViewTrafoApplicator(view.ViewTrafos, sg) :> ISg

    [<Extension>]
    static member ViewTrafo(sg : ISg, view : IMod<Trafo3d>) = Sg.viewTrafo view sg

    [<Extension>]
    static member ProjTrafo(sg : ISg, proj : IMod<Trafo3d>) = Sg.projTrafo proj sg

    [<Extension>]
    static member Trafo(sg : ISg, modelTrafo : IMod<Trafo3d>) = Sg.trafo modelTrafo sg

    [<Extension>]
    static member Trafo(sg : ISg, modelTrafo : Trafo3d) = Sg.trafo (Mod.constant modelTrafo) sg

    [<Extension>]
    static member Surface(sg : ISg, surface : ISurface) = Sg.SurfaceApplicator(Mod.constant surface, sg) :> ISg

    [<Extension>]
    static member Surface(sg : ISg, surface : IEvent<ISurface>) = Sg.SurfaceApplicator(surface, sg) :> ISg

    [<Extension>]
    static member Surface(sg : ISg, surface : IMod<ISurface>) = Sg.SurfaceApplicator(surface, sg) :> ISg

    [<Extension>]
    static member FillMode(sg : ISg, mode : IEvent<FillMode>) = Sg.FillModeApplicator(mode, sg) :> ISg

    [<Extension>]
    static member FillMode(sg : ISg, mode : FillMode) = Sg.FillModeApplicator(Mod.constant mode, sg) :> ISg

    [<Extension>]
    static member FillMode(sg : ISg, mode : IMod<FillMode>) = Sg.FillModeApplicator(mode, sg) :> ISg


    [<Extension>]
    static member CullMode(sg : ISg, mode : IEvent<CullMode>) = Sg.CullModeApplicator(mode, sg) :> ISg

    [<Extension>]
    static member CullMode(sg : ISg, mode : CullMode) = Sg.CullModeApplicator(Mod.constant mode, sg) :> ISg

    [<Extension>]
    static member CullMode(sg : ISg, mode : IMod<CullMode>) = Sg.CullModeApplicator(mode, sg) :> ISg


    [<Extension>]
    static member BlendMode(sg : ISg, mode : IEvent<BlendMode>) = Sg.BlendModeApplicator(mode, sg) :> ISg

    [<Extension>]
    static member BlendMode(sg : ISg, mode : BlendMode) = Sg.BlendModeApplicator(Mod.constant mode, sg) :> ISg

    [<Extension>]
    static member BlendMode(sg : ISg, mode : IMod<BlendMode>) = Sg.BlendModeApplicator(mode, sg) :> ISg


    [<Extension>]
    static member StencilMode(sg : ISg, mode : IEvent<StencilMode>) = Sg.StencilModeApplicator(mode, sg) :> ISg

    [<Extension>]
    static member StencilMode(sg : ISg, mode : StencilMode) = Sg.StencilModeApplicator(Mod.constant mode, sg) :> ISg

    [<Extension>]
    static member StencilMode(sg : ISg, mode : IMod<StencilMode>) = Sg.StencilModeApplicator(mode, sg) :> ISg


    [<Extension>]
    static member DepthTestMode(sg : ISg, mode : IEvent<DepthTestMode>) = Sg.DepthTestModeApplicator(mode, sg) :> ISg

    [<Extension>]
    static member DepthTestMode(sg : ISg, mode : DepthTestMode) = Sg.DepthTestModeApplicator(Mod.constant mode, sg) :> ISg

    [<Extension>]
    static member DepthTestMode(sg : ISg, mode : IMod<DepthTestMode>) = Sg.DepthTestModeApplicator(mode, sg) :> ISg

    [<Extension>]
    static member WithEffects(sg : ISg, effects : seq<FShadeEffect>) : ISg = Sg.effect effects sg

    [<Extension>]
    static member Uniform(sg : ISg, name : Symbol, value : IMod) : ISg = Sg.UniformApplicator(name, value, sg) :> ISg

    [<Extension>]
    static member Uniform(sg : ISg, uniforms : IUniformProvider) : ISg = Sg.UniformApplicator(uniforms, sg) :> ISg

    [<Extension>]
    static member Uniform(sg : ISg, uniforms : SymbolDict<IMod>) : ISg = Sg.UniformApplicator(new QuickUniformHolder(uniforms), sg) :> ISg

    [<Extension>]
    static member VertexIndices(sg : ISg, indices : BufferView) : ISg = Sg.VertexIndexApplicator(indices, sg) :> ISg

    [<Extension>]
    static member VertexAttributes(sg : ISg, attributes : SymbolDict<BufferView>) : ISg = Sg.VertexAttributeApplicator(attributes, sg) :> ISg

    [<Extension>]
    static member Pass(sg : ISg, renderPass : RenderPass) : ISg = Sg.PassApplicator(renderPass, sg) :> ISg

    [<Extension>]
    static member WriteBuffers(sg : ISg, bufferIdentifiers : seq<Symbol>) : ISg = Sg.WriteBuffersApplicator(Some (Set.ofSeq bufferIdentifiers), sg) :> ISg

    [<Extension>]
    static member WriteBuffers(sg : ISg, [<ParamArray>] bufferIdentifiers: Symbol[]) : ISg = Sg.WriteBuffersApplicator(Some (Set.ofArray bufferIdentifiers), sg) :> ISg
    
    [<Extension>]
    static member OnOff(sg : ISg, on : IMod<bool>) : ISg = Sg.OnOffNode(on, sg) :> ISg


[<Extension>]
[<AbstractClass>]
[<Sealed>]
type SceneGraphTools =

    [<Extension>]
    static member NormalizeToAdaptive (this : ISg, box : Box3d) = Sg.normalizeToAdaptive box this

    [<Extension>]
    static member NormalizeAdaptive (this : ISg)  = Sg.normalizeAdaptive this 
     
