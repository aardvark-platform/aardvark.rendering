namespace Aardvark.SceneGraph

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Rendering


[<AutoOpen>]
module LodTreeRendering =

    type LodTreeRenderConfig =
        {
            budget : aval<int64>
            splitfactor : aval<float>
            time : aval<System.DateTime>
            maxSplits : aval<int>
            renderBounds : aval<bool>
            stats : cval<LodRendererStats>
            pickTrees : Option<cmap<ILodTreeNode,SimplePickTree>>
            alphaToCoverage : bool
        }

    module LodTreeRenderConfig =
        let private time =
            let sw = System.Diagnostics.Stopwatch.StartNew()
            let start = System.DateTime.Now

            let self = ref Unchecked.defaultof<aval<System.DateTime>>

            self :=
                AVal.custom (fun t -> 
                    let now = start + sw.Elapsed
                    AdaptiveObject.RunAfterEvaluate (fun () -> self.Value.MarkOutdated())
                    now
                )
            !self

        let simple =
            {
                budget = AVal.constant -1L
                splitfactor = AVal.constant 0.4
                time = time
                maxSplits = AVal.constant System.Environment.ProcessorCount
                renderBounds = AVal.constant false
                stats = AVal.init Unchecked.defaultof<_>
                pickTrees = None
                alphaToCoverage = false
            }

    module Sg = 
        type LodTreeNode(stats : cval<LodRendererStats>, pickTrees : Option<cmap<ILodTreeNode,SimplePickTree>>, alphaToCoverage : bool, budget : aval<int64>, splitfactor : aval<float>, renderBounds : aval<bool>, maxSplits : aval<int>, time : aval<System.DateTime>, clouds : aset<LodTreeInstance>) =
            member x.Time = time
            member x.Clouds = clouds
            member x.MaxSplits = maxSplits

            member x.Stats = stats
            member x.PickTrees = pickTrees
            member x.RenderBounds = renderBounds
            member x.Budget = budget
            member x.AlphaToCoverage = alphaToCoverage
            member x.SplitFactor = splitfactor
            interface ISg

            new(stats : cval<LodRendererStats>, pickTrees : cmap<ILodTreeNode,SimplePickTree>, alphaToCoverage : bool, budget : aval<int64>, splitfactor : aval<float>, renderBounds : aval<bool>, maxSplits : aval<int>, time : aval<System.DateTime>, clouds : aset<LodTreeInstance>) =
                LodTreeNode(stats, Some pickTrees, alphaToCoverage, budget, splitfactor, renderBounds, maxSplits, time, clouds)
            new(stats : cval<LodRendererStats>, alphaToCoverage : bool, budget : aval<int64>, splitfactor : aval<float>, renderBounds : aval<bool>, maxSplits : aval<int>, time : aval<System.DateTime>, clouds : aset<LodTreeInstance>) =
                LodTreeNode(stats, None, alphaToCoverage, budget, splitfactor, renderBounds, maxSplits, time, clouds)

        let lodTree (cfg : LodTreeRenderConfig) (data : aset<LodTreeInstance>) =
            LodTreeNode(cfg.stats, cfg.pickTrees, cfg.alphaToCoverage, cfg.budget, cfg.splitfactor, cfg.renderBounds, cfg.maxSplits, cfg.time, data) :> ISg
    

namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph  

[<Rule>]
type LodNodeSem() =
    member x.RenderObjects(sg : Sg.LodTreeNode, scope : Ag.Scope) =
        let state = PipelineState.ofScope scope
        let surface = scope.Surface
        let pass = scope.RenderPass

        let model = scope.ModelTrafo
        let view = scope.ViewTrafo
        let proj = scope.ProjTrafo

        let id = newId()
        let obj =
            { new ILodRenderObject with
                member x.Id = id
                member x.AttributeScope = scope
                member x.RenderPass = pass
                member x.Prepare(r, fbo) = 
                    let config =
                        {
                            fbo = fbo
                            time = sg.Time
                            surface = surface
                            state = state
                            pass = pass
                            model = model
                            view = view
                            proj = proj
                            budget = sg.Budget
                            renderBounds = sg.RenderBounds
                            maxSplits = sg.MaxSplits
                            splitfactor = sg.SplitFactor
                            stats = sg.Stats
                            pickTrees = sg.PickTrees
                            alphaToCoverage = sg.AlphaToCoverage
                        }

                    r.CreateLodRenderer(config, sg.Clouds)
            }

        ASet.single (obj :> IRenderObject)
