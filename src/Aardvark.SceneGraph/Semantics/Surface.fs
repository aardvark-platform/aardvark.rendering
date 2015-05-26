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

    let emptySurface : IMod<ISurface> = 
        Mod.custom (fun () -> 
            failwith "empty surface encountered. Solution: add a surface to your scene graph."
        )

    [<Semantic>]
    type SurfaceSem() =

        member x.Surface(e : Root) =
            e.Child?Surface <- emptySurface

        member x.Surface(s : Sg.SurfaceApplicator) =
            s.Child?Surface <- s.Surface 