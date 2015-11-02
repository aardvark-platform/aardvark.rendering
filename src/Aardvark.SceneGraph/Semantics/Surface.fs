namespace Aardvark.SceneGraph.Semantics

open System

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.Base.AgHelpers
open Aardvark.SceneGraph

[<AutoOpen>]
module SurfaceSemantics =

    type ISg with
        member x.Surface : IMod<ISurface> = x?Surface

    module Semantic =
        let surface (s : ISg) : IMod<ISurface> = s?Surface

    let private emptySurface : IMod<ISurface> = 
        Mod.custom (fun s -> 
            failwith "empty surface encountered. Solution: add a surface to your scene graph."
        )

    [<Semantic>]
    type SurfaceSem() =

        member x.Surface(e : Root<ISg>) =
            e.Child?Surface <- emptySurface

        member x.Surface(s : Sg.SurfaceApplicator) =
            s.Child?Surface <- s.Surface 