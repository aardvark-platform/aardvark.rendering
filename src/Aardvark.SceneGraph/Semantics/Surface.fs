namespace Aardvark.SceneGraph.Semantics

open System

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Ag
open Aardvark.SceneGraph

[<AutoOpen>]
module SurfaceSemantics =

    type ISg with
        member x.Surface : Surface = x?Surface

    module Semantic =
        let surface (s : ISg) : Surface = s?Surface

    let private emptySurface : Surface = 
        Surface.None

    [<Semantic>]
    type SurfaceSem() =

        member x.Surface(e : Root<ISg>) =
            e.Child?Surface <- emptySurface

        member x.Surface(s : Sg.SurfaceApplicator) =
            s.Child?Surface <- s.Surface 