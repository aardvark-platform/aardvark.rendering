namespace Aardvark.SceneGraph.Sg

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

    type LodNode(viewDecider : (LodScope -> bool), 
                 low : IMod<ISg>, high : IMod<ISg>) =
        interface ISg

        member x.Low = low
        member x.High = high
        member x.ViewDecider = viewDecider
        member val Name = "" with get, set

        new(viewDecider : System.Func<LodScope, bool>, low : ISg, high : ISg) = 
            LodNode((fun t -> viewDecider.Invoke t), Mod.initConstant low, Mod.initConstant high)

    module LodSemantics =
        
        let foo x = x * 2
        let gah x = x * 2

        type Urdar = Urdar of unit

        [<Semantic>]
        type LodSem() =
            member x.RenderJobs(node : LodNode) : aset<RenderJob> =

                let mvTrafo = node?ModelViewTrafo()

                aset {
                    let scope = Ag.getContext()

                    let! highSg,lowSg = node.High,node.Low

                    let lowJobs = lowSg?RenderJobs() : aset<RenderJob> 
                    let highJobs = highSg?RenderJobs() : aset<RenderJob>

                    //this parallel read is absolutely crucial for performance, since otherwise the 
                    //resulting set will no longer be referentially equal (cannot really be solved any other way)
                    //once more we see that adaptive code is extremely sensible.
                    let! camLocation,trafo = node?CameraLocation,mvTrafo

                    if node.ViewDecider { trafo = trafo; cameraPosition = camLocation; scope = scope } then 
                        yield! highJobs
                    else    
                        yield! lowJobs
                }