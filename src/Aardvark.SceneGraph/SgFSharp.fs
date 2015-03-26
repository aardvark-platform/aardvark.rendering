namespace Aardvark.SceneGraph


open System
open Aardvark.Base
open Aardvark.Base.Ag
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Runtime.CompilerServices

open Aardvark.Base.Incremental


[<AutoOpen>]
module SgFSharp =

    let private constantSurfaceCache = MemoCache(false)
    let private constantlistSurfaceCache = MemoCache(false)
    let private surfaceCache = MemoCache(false)
    let private effectCache = MemoCache(false)

    module Sg =

        let uniform (name : string) (value : IMod<'a>) (sg : ISg) =
            Sg.UniformApplicator(name, value :> IMod, sg) :> ISg

        let trafo (m : IMod<Trafo3d>) (sg : ISg) =
            Sg.TrafoApplicator(m, sg) :> ISg

        let viewTrafo (m : IMod<Trafo3d>) (sg : ISg) =
            Sg.ViewTrafoApplicator(m, sg) :> ISg

        let projTrafo (m : IMod<Trafo3d>) (sg : ISg) =
            Sg.ProjectionTrafoApplicator(m, sg) :> ISg


        let surface (m : IMod<ISurface>) (sg : ISg) =
            Sg.SurfaceApplicator(m, sg) :> ISg

        let group (s : #seq<ISg>) =
            Sg.Group s

        let group' (s : #seq<ISg>) =
            Sg.Group s :> ISg

        let set (set : aset<ISg>) =
            Sg.Set(set) :> ISg

        let visibleBB (c : C4b) (sg : ISg) renderBoth = Sg.VisibleBB(c,sg, renderBoth) :> ISg

        let texture (sem : Symbol) (tex : IMod<ITexture>) (sg : ISg) =
            Sg.TextureApplicator(sem, tex, sg) :> ISg

        let diffuseTexture (tex : IMod<ITexture>) (sg : ISg) = 
            texture DefaultSemantic.DiffuseColorTexture tex sg

        let scopeDependentTexture (sem : Symbol) (tex : Scope -> IMod<ITexture>) (sg : ISg) =
            Sg.UniformApplicator(Uniforms.ScopeDependentUniformHolder([sem, fun s -> tex s :> IMod]), sg) :> ISg

        let scopeDependentDiffuseTexture (tex : Scope -> IMod<ITexture>) (sg : ISg) =
            scopeDependentTexture DefaultSemantic.DiffuseColorTexture tex sg

        let runtimeDependentTexture (sem : Symbol) (tex : IRuntime -> IMod<ITexture>) (sg : ISg) =
            let cache = Dictionary<IRuntime, IMod<ITexture>>()
            let tex runtime =
                match cache.TryGetValue runtime with
                    | (true, v) -> v
                    | _ -> 
                        let v = tex runtime
                        cache.[runtime] <- v
                        v

            scopeDependentTexture sem (fun s -> s?Runtime |> tex) sg

        let runtimeDependentDiffuseTexture(tex : IRuntime -> IMod<ITexture>) (sg : ISg) =
            runtimeDependentTexture DefaultSemantic.DiffuseColorTexture tex sg

        let fillMode (m : IMod<FillMode>) (sg : ISg) =
            Sg.FillModeApplicator(m, sg) :> ISg
        
        let blendMode (m : IMod<BlendMode>) (sg : ISg) =
            Sg.BlendModeApplicator(m, sg) :> ISg

        let cullMode (m : IMod<CullMode>) (sg : ISg) =
            Sg.CullModeApplicator(m, sg) :> ISg

        let depthTest (m : IMod<DepthTestMode>) (sg : ISg) =
            Sg.DepthTestModeApplicator(m, sg) :> ISg

        let pass (pass : uint64) (sg : ISg) = Sg.PassApplicator(Mod.initConstant pass, sg)




    type ISg with

        member x.Trafo(t : Trafo3d) =
            Sg.TrafoApplicator(Mod.initConstant t, x) :> ISg

        member x.Trafo(t : IMod<Trafo3d>) =
            Sg.TrafoApplicator(t, x) :> ISg

        member x.OnOff(on : IMod<bool>) =
            Sg.OnOffNode(on, x) :> ISg

        member x.DiffuseTexture(t : IMod<ITexture>) =
            Sg.TextureApplicator(DefaultSemantic.DiffuseColorTexture, t, x) :> ISg

        member x.WithUniforms(m : Map<Symbol, IMod>) =
            Sg.UniformApplicator(Uniforms.SimpleUniformHolder(m), x) :> ISg

        member x.WithUniform(name : Symbol, value : IMod<'a>) =
            Sg.UniformApplicator(name, value :> IMod, x) :> ISg

        member x.WithUniforms(values : list<Symbol * IMod>) =
            Sg.UniformApplicator(Uniforms.SimpleUniformHolder(Map.ofList values), x) :> ISg



    type IndexedGeometry with
        member x.Sg =
            Sg.ofIndexedGeometry x
