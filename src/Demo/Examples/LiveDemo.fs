namespace Aardvark.SceneGraph

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics

type LodScope = { cameraPosition : V3d; bb : Box3d }
type LodNode(viewDecider : (LodScope -> bool), low : ISg, high : ISg) =
    inherit Sg.AbstractApplicator(low)

    member x.Low = low
    member x.High = high
    member x.ViewDecider = viewDecider

[<Rule>]
type LodSem() =

    member x.RenderObjects(node : LodNode, scope : Ag.Scope) : aset<IRenderObject> =
        aset {
            let bb      = node.Low.GlobalBoundingBox(scope)
            let lowJobs  = node.Low.RenderObjects(scope)
            let highJobs = node.High.RenderObjects(scope)

            let! camera = scope.CameraLocation

            if node.ViewDecider { cameraPosition = camera; bb = AVal.force bb } then 
                yield! highJobs
            else    
                yield! lowJobs
        }

module Sg =

    let lod (decider : LodScope -> bool) (low : ISg) (high : ISg)= 
        LodNode(decider,low,high) :> ISg