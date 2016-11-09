namespace Aardvark.SceneGraph

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics

type LodScope = { cameraPosition : V3d; bb : Box3d }
type LodNode(viewDecider : (LodScope -> bool), low : ISg, high : ISg) =
    inherit Sg.AbstractApplicator(low)

    member x.Low = low
    member x.High = high
    member x.ViewDecider = viewDecider

[<Semantic>]
type LodSem() =

    member x.RenderObjects(node : LodNode) : aset<IRenderObject> =
        aset {
            let bb      = node.Low.GlobalBoundingBox()
            let lowJobs  = node.Low.RenderObjects()
            let highJobs = node.High.RenderObjects()

            let! camera = node.CameraLocation

            if node.ViewDecider { cameraPosition = camera; bb = Mod.force bb } then 
                yield! highJobs
            else    
                yield! lowJobs
        }

module Sg =

    let lod (decider : LodScope -> bool) (low : ISg) (high : ISg)= 
        LodNode(decider,low,high) :> ISg