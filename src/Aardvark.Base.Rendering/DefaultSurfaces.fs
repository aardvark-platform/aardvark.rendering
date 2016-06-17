namespace Aardvark.Base.Rendering

open Aardvark.Base
open Aardvark.Base.Incremental
open FShade
open Microsoft.FSharp.Quotations
open Aardvark.Base.Rendering.Effects

module DefaultSurfaces =
    
    let trafo = Trafo.trafo

    let pointSurface = PointSurface.pointSurface

    let thickLine = ThickLine.thickLine

    let thickLineRoundCaps= ThickLineRoundCaps.thickLineRoundCaps

    let thickLineSparePointSizeCaps = ThickLineSparePointSizeCaps.thickLineSparePointSizeCaps

    let pointSprite = PointSprite.pointSprite

    let viewSizedPointSprites = ViewSizedPointSprites.viewSizedPointSprites

    let pointSpriteFragment = PointSpriteFragment.pointSpriteFragment

    let uniformColor = UniformColor.uniformColor

    let constantColor = ConstantColor.constantColor

    let sgColor = SgColor.sgColor

    let vertexColor = VertexColor.vertexColor

    let simpleLighting = SimpleLighting.simpleLighting

    let lighting = Lighting.lighting

    let diffuseTexture = DiffuseTexture.diffuseTexture

    let normalMap = NormalMap.normalMap

    let transformColor = TransformColor.transformColor

    let instanceTrafo = InstanceTrafo.instanceTrafo


[<AutoOpen>]
module EffectAPI =
    type private Effect = IMod<list<FShadeEffect>>

    type EffectBuilder() =
        member x.Bind(f : 'a -> Expr<'b>, c : unit -> Effect) : Effect =
            let effect = toEffect f
            c() |> Mod.map (fun c -> effect::c)

        member x.Bind(f : FShadeEffect, c : unit -> Effect) : Effect =
            c() |> Mod.map (fun c -> f::c)

        member x.Bind(m : IMod<'a>, f : 'a -> Effect) =
            m |> Mod.bind f

        member x.Return (u : unit) : Effect = Mod.constant []

        member x.Zero() : Effect = Mod.constant []

        member x.Combine(l : Effect, r : unit -> Effect) : Effect = Mod.map2 (fun l r -> l @ r) l (r())

        member x.Delay(f : unit -> Effect) = f

        member x.For(seq : seq<'a>, f : 'a -> Effect) : Effect =
            seq |> Seq.toList |> List.map f |> Mod.mapN (Seq.concat >> Seq.toList)

        member x.Run(f : unit -> Effect) = f()


    let effect = EffectBuilder()