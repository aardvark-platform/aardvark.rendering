namespace Aardvark.SceneGraph

open System
open System.Runtime.InteropServices
open System.Collections.Generic

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.Base.Rendering

module Sg =

    [<AbstractClass>]
    type AbstractApplicator(child : IMod<ISg>) =
        interface IApplicator with
            member x.Child = child

        member x.Child = child

        new(child : ISg) = AbstractApplicator(Mod.initConstant child)

    type AdapterNode(node : obj) =
        interface ISg

        member x.Node = node

    type DynamicNode(child : IMod<ISg>) = inherit AbstractApplicator(child)

    type RenderNode(call : IMod<DrawCallInfo>) =
        interface ISg

        member x.DrawCallInfo = call

        new(call : IEvent<DrawCallInfo>) = RenderNode(Mod.fromEvent call)
        new(call : DrawCallInfo) = RenderNode(Mod.initConstant call)
    
    type VertexAttributeApplicator(values : Map<Symbol, BufferView>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Values = values

        new(values : Map<Symbol, BufferView>, child : ISg)            = VertexAttributeApplicator(values, Mod.initConstant child)
        new(semantic : Symbol, value : BufferView, child : IMod<ISg>) = VertexAttributeApplicator(Map.ofList [semantic, value], child)
        new(semantic : Symbol, value : BufferView, child : ISg)       = VertexAttributeApplicator(Map.ofList [semantic, value], Mod.initConstant child)
        new(values : SymbolDict<BufferView>, child : ISg)             = VertexAttributeApplicator(values |> Seq.map (fun (KeyValue(k,v)) -> k,v) |> Map.ofSeq, Mod.initConstant child)

    type VertexIndexApplicator(value : IMod<Array>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Value = value

        new(value : IMod<Array>, child : ISg)         = VertexIndexApplicator(value, Mod.initConstant child)
        new(value : IEvent<Array>, child : IMod<ISg>) = VertexIndexApplicator(Mod.fromEvent value, child)
        new(value : IEvent<Array>, child : ISg)       = VertexIndexApplicator(Mod.fromEvent value, Mod.initConstant child)

    type InstanceAttributeApplicator(values : Map<Symbol, BufferView>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Values = values

        new(values : Map<Symbol, BufferView>, child : ISg)            = InstanceAttributeApplicator(values, Mod.initConstant child)
        new(semantic : Symbol, value : BufferView, child : IMod<ISg>) = InstanceAttributeApplicator(Map.ofList [semantic, value], child)
        new(semantic : Symbol, value : BufferView, child : ISg)       = InstanceAttributeApplicator(Map.ofList [semantic, value], Mod.initConstant child)
        new(values : SymbolDict<BufferView>, child : ISg)             = InstanceAttributeApplicator(values |> Seq.map (fun (KeyValue(k,v)) -> k,v) |> Map.ofSeq, Mod.initConstant child)
 

    type OnOffNode(on : IMod<bool>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.IsActive = on

        new(on : IMod<bool>, child : ISg) = OnOffNode(on, Mod.initConstant child)
        new(on : IEvent<bool>, child : IMod<ISg>) = OnOffNode(Mod.fromEvent on, child)
        new(on : IEvent<bool>, child : ISg) = OnOffNode(Mod.fromEvent on, Mod.initConstant child)

    type PassApplicator(pass : IMod<uint64>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Pass = pass

        new(pass : IMod<uint64>, child : ISg) = PassApplicator(pass, Mod.initConstant child)
        new(pass : IEvent<uint64>, child : IMod<ISg>) = PassApplicator(Mod.fromEvent pass, child)
        new(pass : IEvent<uint64>, child : ISg) = PassApplicator(Mod.fromEvent pass, Mod.initConstant child)

    type UniformApplicator(uniformHolder : IUniformProvider, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member internal x.Uniforms = uniformHolder
        
        member x.TryFindUniform (scope : Scope) (name : Symbol) =
            uniformHolder.TryGetUniform (scope,name)

        new(value : IUniformProvider, child : ISg) = UniformApplicator( value, Mod.initConstant child)
        new(name : string, value : IMod, child : ISg) = UniformApplicator( (new Providers.SimpleUniformHolder ([Symbol.Create name,value]) :> IUniformProvider), Mod.initConstant child)
        new(name : Symbol, value : IMod, child : ISg) = UniformApplicator( (new Providers.SimpleUniformHolder( [name,value]) :> IUniformProvider), Mod.initConstant child)
        new(name : Symbol, value : IMod, child : IMod<ISg>) = UniformApplicator( (new Providers.SimpleUniformHolder( [name,value]) :> IUniformProvider), child)
        new(map : Map<Symbol,IMod>, child : ISg) = UniformApplicator( (new Providers.SimpleUniformHolder( map) :> IUniformProvider), Mod.initConstant child)


    type SurfaceApplicator(surface : IMod<ISurface>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Surface = surface

        new(value : IMod<ISurface>, child : ISg) = SurfaceApplicator(value, Mod.initConstant child)

        new(value : IEvent<ISurface>, child : IMod<ISg>) = SurfaceApplicator(Mod.fromEvent value, child)
        new(value : IEvent<ISurface>, child : ISg) = SurfaceApplicator(Mod.fromEvent value, Mod.initConstant child)
        new(value : ISurface, child : ISg) = SurfaceApplicator(Mod.initConstant value, Mod.initConstant child)

    type TextureApplicator(semantic : Symbol, texture : IMod<ITexture>, child : IMod<ISg>) =
        inherit UniformApplicator(semantic, texture :> IMod, child)

        member x.Texture = texture

        new(semantic : Symbol, texture : IMod<ITexture>, child : ISg) = TextureApplicator(semantic, texture, Mod.initConstant child)
        new(semantic : Symbol, texture : IEvent<ITexture>, child : IMod<ISg>) = TextureApplicator(semantic, Mod.fromEvent texture, child)
        new(semantic : Symbol, texture : IEvent<ITexture>, child : ISg) = TextureApplicator(semantic, Mod.fromEvent texture, Mod.initConstant child)
        new(texture : IMod<ITexture>, child : ISg) = TextureApplicator(DefaultSemantic.DiffuseColorTexture, texture, child)
        new(texture : IEvent<ITexture>, child : IMod<ISg>) = TextureApplicator(DefaultSemantic.DiffuseColorTexture,  texture, child)
        new(texture : IEvent<ITexture>, child : ISg) = TextureApplicator(DefaultSemantic.DiffuseColorTexture, texture, child)
        new(texture : IMod<ITexture>, child : IMod<ISg>) = TextureApplicator(DefaultSemantic.DiffuseColorTexture,texture,child)


    type TrafoApplicator(trafo : IMod<Trafo3d>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Trafo = trafo

        new(value : IMod<Trafo3d>, child : ISg) = TrafoApplicator(value, Mod.initConstant child)
        new(value : IEvent<Trafo3d>, child : IMod<ISg>) = TrafoApplicator(Mod.fromEvent value, child)
        new(value : IEvent<Trafo3d>, child : ISg) = TrafoApplicator(Mod.fromEvent value, Mod.initConstant child)
        new(value : Trafo3d, child : ISg) = TrafoApplicator(Mod.initConstant value, Mod.initConstant child)
    
    type ViewTrafoApplicator(trafo : IMod<Trafo3d>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.ViewTrafo = trafo

        new(value : IMod<Trafo3d>, child : ISg) = ViewTrafoApplicator(value, Mod.initConstant child)
        new(value : IEvent<Trafo3d>, child : IMod<ISg>) = ViewTrafoApplicator(Mod.fromEvent value, child)
        new(value : IEvent<Trafo3d>, child : ISg) = ViewTrafoApplicator(Mod.fromEvent value, Mod.initConstant child)

    type ProjectionTrafoApplicator(trafo : IMod<Trafo3d>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.ProjectionTrafo = trafo

        new(value : IMod<Trafo3d>, child : ISg) = ProjectionTrafoApplicator(value, Mod.initConstant child)
        new(value : IEvent<Trafo3d>, child : IMod<ISg>) = ProjectionTrafoApplicator(Mod.fromEvent value, child)
        new(value : IEvent<Trafo3d>, child : ISg) = ProjectionTrafoApplicator(Mod.fromEvent value, Mod.initConstant child)


    type DepthTestModeApplicator(mode : IMod<DepthTestMode>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        new(value : IMod<DepthTestMode>, child : ISg) = DepthTestModeApplicator(value, Mod.initConstant child)
        new(value : IEvent<DepthTestMode>, child : IMod<ISg>) = DepthTestModeApplicator(Mod.fromEvent value, child)
        new(value : IEvent<DepthTestMode>, child : ISg) = DepthTestModeApplicator(Mod.fromEvent value, Mod.initConstant child)

    type CullModeApplicator(mode : IMod<CullMode>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        new(value : IMod<CullMode>, child : ISg) = CullModeApplicator(value, Mod.initConstant child)
        new(value : IEvent<CullMode>, child : IMod<ISg>) = CullModeApplicator(Mod.fromEvent value, child)
        new(value : IEvent<CullMode>, child : ISg) = CullModeApplicator(Mod.fromEvent value, Mod.initConstant child)

    type FillModeApplicator(mode : IMod<FillMode>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        new(value : IMod<FillMode>, child : ISg) = FillModeApplicator(value, Mod.initConstant child)
        new(value : IEvent<FillMode>, child : IMod<ISg>) = FillModeApplicator(Mod.fromEvent value, child)
        new(value : IEvent<FillMode>, child : ISg) = FillModeApplicator(Mod.fromEvent value, Mod.initConstant child)
        new(value : FillMode, child : ISg) = FillModeApplicator(Mod.initConstant value, Mod.initConstant child)

    type StencilModeApplicator(mode : IMod<StencilMode>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        new(value : IMod<StencilMode>, child : ISg) = StencilModeApplicator(value, Mod.initConstant child)
        new(value : IEvent<StencilMode>, child : IMod<ISg>) = StencilModeApplicator(Mod.fromEvent value, child)
        new(value : IEvent<StencilMode>, child : ISg) = StencilModeApplicator(Mod.fromEvent value, Mod.initConstant child)

    type BlendModeApplicator(mode : IMod<BlendMode>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        new(value : IMod<BlendMode>, child : ISg) = BlendModeApplicator(value, Mod.initConstant child)
        new(value : IEvent<BlendMode>, child : IMod<ISg>) = BlendModeApplicator(Mod.fromEvent value, child)
        new(value : IEvent<BlendMode>, child : ISg) = BlendModeApplicator(Mod.fromEvent value, Mod.initConstant child)

    type RasterizerStateApplicator(state : IMod<RasterizerState>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        let depth = state |> Mod.map (fun s -> s.DepthTest)
        let cull = state |> Mod.map (fun s -> s.CullMode)
        let fill = state |> Mod.map (fun s -> s.FillMode)
        let stencil = state |> Mod.map (fun s -> s.StencilMode)
        let blend = state |> Mod.map (fun s -> s.BlendMode)

        member x.RasterizerState = state
        member x.DepthTestMode = depth
        member x.CullMode = cull
        member x.FillMode = fill
        member x.StencilMode = stencil
        member x.BlendMode = blend

        new(value : IMod<RasterizerState>, child : ISg) = RasterizerStateApplicator(value, Mod.initConstant child)
        new(value : IEvent<RasterizerState>, child : IMod<ISg>) = RasterizerStateApplicator(Mod.fromEvent value, child)
        new(value : IEvent<RasterizerState>, child : ISg) = RasterizerStateApplicator(Mod.fromEvent value, Mod.initConstant child)


    type Group(elements : seq<ISg>) =
        let aset = cset(elements)

        interface ISg

        member x.ASet : aset<ISg> = aset :> aset<_>


        member x.Add v =
            transact (fun () ->
               aset.Add v
            )

        member x.Remove v =
            transact (fun () ->
                aset.Remove v
            )

        member x.Clear() =
            transact (fun () ->
                aset.Clear()
            )

        member x.UnionWith v =
            transact (fun () ->
                aset.UnionWith v
            )

        member x.ExceptWith v =
            transact (fun () ->
                aset.ExceptWith v
            )

        member x.SymmetricExceptWith v =
            transact (fun () ->
                aset.SymmetricExceptWith v
            )

        member x.Count = aset.Count

        new() = Group(Seq.empty)
        
    type Set(content : aset<ISg>) =

        interface ISg
        member x.ASet = content


    type Environment (runtime : IRuntime, viewTrafo : IMod<Trafo3d>, projTrafo : IMod<Trafo3d>, viewSize : IMod<V2i>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Runtime = runtime
        member x.ViewTrafo = viewTrafo
        member x.ProjTrafo = projTrafo
        member x.ViewSize = viewSize

        member x.Scene = child

        new(runtime : IRuntime, viewTrafo : IEvent<Trafo3d>, projTrafo : IEvent<Trafo3d>, viewSize : IEvent<V2i>, child : ISg) =
            Environment(runtime, Mod.fromEvent viewTrafo, Mod.fromEvent projTrafo, Mod.fromEvent viewSize, Mod.initConstant child)
        new(runtime : IRuntime, viewTrafo : IEvent<Trafo3d>, projTrafo : IEvent<Trafo3d>, viewSize : IEvent<V2i>, child : IMod<ISg>) =
            Environment(runtime, Mod.fromEvent viewTrafo, Mod.fromEvent projTrafo, Mod.fromEvent viewSize, child)
        new(runtime : IRuntime, viewTrafo : IMod<Trafo3d>, projTrafo : IMod<Trafo3d>, viewSize : IMod<V2i>, child : ISg) =
            Environment(runtime, viewTrafo, projTrafo, viewSize, Mod.initConstant child)
