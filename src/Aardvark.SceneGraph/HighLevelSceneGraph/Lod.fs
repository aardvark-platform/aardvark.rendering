namespace Aardvark.SceneGraph

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.SceneGraph

open Aardvark.SceneGraph.Internal
open System.Collections.Generic


module NaiveLod =

    type LodScope = { trafo : Trafo3d; cameraPosition : V3d; scope : Scope; bb : IMod<Box3d> }
    type LodNode(viewDecider : (LodScope -> bool), low : IMod<ISg>, high : IMod<ISg>) =
        inherit Sg.AbstractApplicator(low)

        member x.Low = low
        member x.High = high
        member x.ViewDecider = viewDecider

        new(viewDecider : System.Func<LodScope, bool>, low : ISg, high : ISg) = 
            LodNode((fun t -> viewDecider.Invoke t), Mod.constant low, Mod.constant high)


//    type ViewFrustumCullNode(sg : IMod<ISg>) =
//        interface IApplicator with
//            member x.Child = sg
//        member x.Child = sg
//
//        new(s : ISg) = ViewFrustumCullNode(Mod.constant s)
//        new(s : IEvent<ISg>) = ViewFrustumCullNode(Mod.fromEvent  s)

    module Sg =

        let loD (low : ISg) (high : ISg) (decider : LodScope -> bool) = 
            LodNode(decider,low,high) :> ISg

module NaiveLoDSemantics =

    open NaiveLod
    open Aardvark.SceneGraph.Semantics

    [<Semantic>]
    type LodSem() =

        member x.RenderObjects(node : LodNode) : aset<IRenderObject> =

            let scope                      = Ag.getContext()
            let mvTrafo                    = node.ModelTrafo
            let cameraLocation : IMod<V3d> = node?CameraLocation

            aset {
                let! highSg, lowSg = node.High, node.Low

                let bb = lowSg.GlobalBoundingBox()
                let lowJobs  = lowSg .RenderObjects()
                let highJobs = highSg.RenderObjects()

                //this parallel read is absolutely crucial for performance, since otherwise the 
                //resulting set will no longer be referentially equal (cannot really be solved any other way)
                //once more we see that adaptive code is extremely sensible.
                let! camLocation,trafo = node?CameraLocation,mvTrafo

                if node.ViewDecider { trafo = trafo; cameraPosition = camLocation; scope = scope; bb = bb } then 
                    yield! highJobs
                else    
                    yield! lowJobs
            }