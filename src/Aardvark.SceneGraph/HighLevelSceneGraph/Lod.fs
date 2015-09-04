namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.Base.AgHelpers
open Aardvark.SceneGraph

open Aardvark.SceneGraph.Internal
open System.Collections.Generic


[<AutoOpen>]
module Ext =

    type LodScope = { trafo : Trafo3d; cameraPosition : V3d; scope : Scope}

    type ViewFrustumCullNode(sg : IMod<ISg>) =
        interface IApplicator with
            member x.Child = sg
        member x.Child = sg

        new(s : ISg) = ViewFrustumCullNode(Mod.constant s)
        new(s : IEvent<ISg>) = ViewFrustumCullNode(Mod.fromEvent  s)

    type LodNode(viewDecider : (LodScope -> bool), 
                 low : IMod<ISg>, high : IMod<ISg>) =
        interface ISg

        member x.Low = low
        member x.High = high
        member x.ViewDecider = viewDecider
        member val Name = "" with get, set

        new(viewDecider : System.Func<LodScope, bool>, low : ISg, high : ISg) = 
            LodNode((fun t -> viewDecider.Invoke t), Mod.constant low, Mod.constant high)

    module LodSemantics =

        [<Semantic>]
        type LodSem() =
            member x.RenderJobs(node : LodNode) : aset<RenderObject> =

                let mvTrafo = node?ModelViewTrafo()

                aset {
                    let scope = Ag.getContext()

                    let! highSg,lowSg = node.High,node.Low

                    let lowJobs = lowSg?RenderJobs() : aset<RenderObject> 
                    let highJobs = highSg?RenderJobs() : aset<RenderObject>

                    //this parallel read is absolutely crucial for performance, since otherwise the 
                    //resulting set will no longer be referentially equal (cannot really be solved any other way)
                    //once more we see that adaptive code is extremely sensible.
                    let! camLocation,trafo = node?CameraLocation,mvTrafo

                    if node.ViewDecider { trafo = trafo; cameraPosition = camLocation; scope = scope } then 
                        yield! highJobs
                    else    
                        yield! lowJobs
                }

                            //            member x.GlobalBoundingBox(n : LodNode) : IMod<Box3d> = 
                //                adaptive {
                //                    let! low = n.Low
                //                    return! low?GlobalBoundingBox()
                //                }