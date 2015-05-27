namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.Base.AgHelpers
open Aardvark.SceneGraph
open Aardvark.Base.Rendering

open Aardvark.SceneGraph.Internal

[<AutoOpen>]
module UniformSemantics =

    type ISg with   
        member x.Uniforms : list<IUniformProvider> = x?Uniforms
 
    [<Semantic>]
    type UniformSem() =
        member x.Uniforms(e : Root) =
            e.Child?Uniforms <- ([] : list<IUniformProvider>)

        member x.Uniforms(u : Sg.UniformApplicator) =
            u.Child?Uniforms <- u.Uniforms :: u?Uniforms
