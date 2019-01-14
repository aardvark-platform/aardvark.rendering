namespace Aardvark.SceneGraph

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.SceneGraph


[<AutoOpen>]
module LodTreeRendering =

    type LodTreeRenderConfig =
        {
            budget : IMod<int64>
            time : IMod<System.DateTime>
            maxSplits : IMod<int>
            renderBounds : IMod<bool>
            quality : IModRef<float>
            maxQuality : IModRef<float>
        }

    module LodTreeRenderConfig =
        let simple =
            {
                budget = Mod.constant (1L <<< 30)
                time = Mod.time
                maxSplits = Mod.constant System.Environment.ProcessorCount
                renderBounds = Mod.constant false
                quality = Mod.init 0.0
                maxQuality = Mod.init 0.0
            }

    module Sg = 
        type LodTreeNode(quality : IModRef<float>, maxQuality : IModRef<float>, budget : IMod<int64>, renderBounds : IMod<bool>, maxSplits : IMod<int>, time : IMod<System.DateTime>, clouds : aset<LodTreeInstance>) =
            member x.Time = time
            member x.Clouds = clouds
            member x.MaxSplits = maxSplits

            member x.Quality = quality
            member x.MaxQuality = maxQuality
            member x.RenderBounds = renderBounds
            member x.Budget = budget
            interface ISg

        let lodTree (cfg : LodTreeRenderConfig) (data : aset<LodTreeInstance>) =
            LodTreeNode(cfg.quality, cfg.maxQuality, cfg.budget, cfg.renderBounds, cfg.maxSplits, cfg.time, data) :> ISg
    

namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Base.Ag
open Aardvark.Base.Incremental
open Aardvark.SceneGraph  

[<Semantic>]
type LodNodeSem() =
    member x.RenderObjects(sg : Sg.LodTreeNode) =
        let scope = Ag.getContext()
        let state = PipelineState.ofScope scope
        let surface = sg.Surface
        let pass = sg.RenderPass

        let model = sg.ModelTrafo
        let view = sg.ViewTrafo
        let proj = sg.ProjTrafo

        let id = newId()
        let obj =
            { new ICustomRenderObject with
                member x.Id = id
                member x.AttributeScope = scope
                member x.RenderPass = pass
                member x.Create(r, fbo) = 
                    r.CreateLodRenderer(fbo, surface, state, pass, model, view, proj, sg.Quality, sg.MaxQuality, sg.Budget, sg.RenderBounds, sg.MaxSplits, sg.Time, sg.Clouds)
            }

        ASet.single (obj :> IRenderObject)
