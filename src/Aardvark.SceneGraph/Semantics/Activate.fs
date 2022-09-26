namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Base.Ag
open Aardvark.SceneGraph

open System

[<AutoOpen>]
module ActivateSemantics =

    type Ag.Scope with
        member x.Activate : list<unit -> IDisposable> = x?Activate

    module Semantic =
        let activate (s : Ag.Scope) : list<unit -> IDisposable> = s?Activate

    [<Rule>]
    type ActivateSemantics() =

        member x.Activate(r : Root<ISg>, scope : Ag.Scope) =
            r.Child?Activate <- ([] : list<unit -> IDisposable>)

        member x.Activate(o : Sg.ActivationApplicator, scope : Ag.Scope) =
            o.Child?Activate <- o.Activate :: scope.Activate