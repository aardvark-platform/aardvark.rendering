namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.Base.AgHelpers
open Aardvark.SceneGraph
open Aardvark.Base.Rendering

[<AutoOpen>]
module TextureSemantics =

    type ISg with
        member x.HasDiffuseColorTexture : IMod<bool> = x?HasDiffuseColorTexture()

    [<Semantic>]
    type DerivedSem() =

        let trueM = Mod.constant true
        let falseM = Mod.constant false

        member x.HasDiffuseColorTexture(sg : ISg) = 
            let uniforms : IUniformProvider list = sg?Uniforms 
            match uniforms |> List.tryPick (fun uniforms -> uniforms.TryGetUniform (Ag.getContext(), Symbol.Create("DiffuseColorTexture"))) with
                | None -> match tryGetAttributeValue sg (string DefaultSemantic.DiffuseColorTexture) with
                                | Success v -> trueM
                                | _ -> falseM
                | Some _ -> trueM