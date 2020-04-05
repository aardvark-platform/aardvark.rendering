namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Base.Rendering
open FSharp.Data.Adaptive
open Aardvark.Base.Ag
open Aardvark.SceneGraph

open Aardvark.SceneGraph.Internal

[<AutoOpen>]
module ActiveSemantics =

    type Ag.Scope with
        member x.IsActive : aval<bool> = x?IsActive
        member x.RenderPass : RenderPass = x?RenderPass

    module Semantic =
        let isActive (s : Ag.Scope) : aval<bool> = s?IsActive
        let renderPass (s : Ag.Scope) : RenderPass = s?RenderPass

    [<Rule>]
    type ActiveSemantics() =

        let trueConstant = AVal.constant true
        let andCache = Caching.BinaryOpCache (AVal.map2 (&&))

        let (<&>) (a : aval<bool>) (b : aval<bool>) =
            if a = trueConstant then b
            elif b = trueConstant then a
            else andCache.Invoke a b

        member x.IsActive(r : Root<ISg>, scope : Ag.Scope) =
            r.Child?IsActive <- trueConstant

        member x.IsActive(o : Sg.OnOffNode, scope : Ag.Scope) =
            o.Child?IsActive <- scope.IsActive <&> o.IsActive


    [<Rule>]
    type PassSemantics() =

        let defaultPass = RenderPass.main
        
        member x.RenderPass(e : Root<ISg>, scope : Ag.Scope) =
            e.Child?RenderPass <- defaultPass

        member x.RenderPass(p : Sg.PassApplicator, scope : Ag.Scope) =
            p.Child?RenderPass <- p.Pass