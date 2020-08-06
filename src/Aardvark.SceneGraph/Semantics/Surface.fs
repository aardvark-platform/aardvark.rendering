namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Base.Ag
open Aardvark.SceneGraph

[<AutoOpen>]
module SurfaceSemantics =

    type Ag.Scope with
        member x.Surface : Surface = x?Surface

    module Semantic =
        let surface (s : Ag.Scope) : Surface = s?Surface

    let private emptySurface : Surface = 
        Surface.None

    [<Rule>]
    type SurfaceSem() =

        member x.Surface(e : Root<ISg>, scope : Ag.Scope) =
            e.Child?Surface <- emptySurface

        member x.Surface(s : Sg.SurfaceApplicator, scope : Ag.Scope) =
            s.Child?Surface <- s.Surface 