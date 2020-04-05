namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Ag
open Aardvark.SceneGraph
open Aardvark.Base.Rendering

open Aardvark.SceneGraph.Internal

[<AutoOpen>]
module UniformSemantics =

    type Ag.Scope with   
        member x.Uniforms : list<IUniformProvider> = x?Uniforms
 
    module Semantic =
        let uniforms (s : Ag.Scope) : list<IUniformProvider> = s?Uniforms

    [<Rule>]
    type UniformSem() =
        member x.Uniforms(e : Root<ISg>, scope : Ag.Scope) =
            e.Child?Uniforms <- ([] : list<IUniformProvider>)

        member x.Uniforms(u : Sg.UniformApplicator, scope : Ag.Scope) =
            u.Child?Uniforms <- u.Uniforms :: scope.Uniforms
