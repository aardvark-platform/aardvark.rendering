namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.SceneGraph

open Aardvark.SceneGraph.Internal

[<AutoOpen>]
module ActiveSemantics =

    type ISg with
        member x.IsActive : IMod<bool> = x?IsActive
        member x.RenderPass : RenderPass = x?RenderPass

    module Semantic =
        let isActive (s : ISg) : IMod<bool> = s?IsActive
        let renderPass (s : ISg) : RenderPass = s?RenderPass

    [<Semantic>]
    type ActiveSemantics() =

        let trueConstant = Mod.constant true
        let andCache = Caching.BinaryOpCache (Mod.map2 (&&))

        let (<&>) (a : IMod<bool>) (b : IMod<bool>) =
            if a = trueConstant then b
            elif b = trueConstant then a
            else andCache.Invoke a b

        member x.IsActive(r : Root<ISg>) =
            r.Child?IsActive <- trueConstant

        member x.IsActive(o : Sg.OnOffNode) =
            o.Child?IsActive <- o?IsActive <&> o.IsActive


    [<Semantic>]
    type PassSemantics() =

        let defaultPass = RenderPass.main
        
        member x.RenderPass(e : Root<ISg>) =
            e.Child?RenderPass <- defaultPass

        member x.RenderPass(p : Sg.PassApplicator) =
            p.Child?RenderPass <- p.Pass