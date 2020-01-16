namespace Aardvark.SceneGraph

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Ag
open Aardvark.SceneGraph

open Aardvark.SceneGraph.Internal
open System.Collections.Generic


module NaiveLod =

    type LodScope = { trafo : Trafo3d; cameraPosition : V3d; scope : Scope; bb : aval<Box3d> }
    type LodNode(viewDecider : (LodScope -> bool), low : aval<ISg>, high : aval<ISg>) =
        inherit Sg.AbstractApplicator(low)

        member x.Low = low
        member x.High = high
        member x.ViewDecider = viewDecider

        new(viewDecider : System.Func<LodScope, bool>, low : ISg, high : ISg) = 
            LodNode((fun t -> viewDecider.Invoke t), AVal.constant low, AVal.constant high)

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
            let cameraLocation : aval<V3d> = node?CameraLocation

            aset {
                let! highSg, lowSg = AVal.map2 (fun a b -> (a,b)) node.High node.Low

                let bb = lowSg.GlobalBoundingBox()
                let lowJobs  = lowSg .RenderObjects()
                let highJobs = highSg.RenderObjects()

                //this parallel read is absolutely crucial for performance, since otherwise the 
                //resulting set will no longer be referentially equal (cannot really be solved any other way)
                //once more we see that adaptive code is extremely sensible.
                let! camLocation,trafo = AVal.map2 (fun a b -> (a,b)) node?CameraLocation mvTrafo

                if node.ViewDecider { trafo = trafo; cameraPosition = camLocation; scope = scope; bb = bb } then 
                    yield! highJobs
                else    
                    yield! lowJobs
            }