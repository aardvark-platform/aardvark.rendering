namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Ag
open Aardvark.SceneGraph
open Aardvark.Base.Rendering

[<AutoOpen>]
module TextureSemantics =

    type ISg with
        member x.HasDiffuseColorTexture : aval<bool> = x?HasDiffuseColorTexture()

    [<Semantic>]
    type DerivedSem() =

        let trueM = AVal.constant true
        let falseM = AVal.constant false

        member x.HasDiffuseColorTexture(sg : ISg) = 
            let uniforms : IUniformProvider list = sg?Uniforms 
            match uniforms |> List.tryPick (fun uniforms -> uniforms.TryGetUniform (Ag.getContext(), Symbol.Create("DiffuseColorTexture"))) with
                | None -> match tryGetAttributeValue sg (string DefaultSemantic.DiffuseColorTexture) with
                                | Success v -> trueM
                                | _ -> falseM
                | Some _ -> trueM