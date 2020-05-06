namespace Aardvark.Base.Rendering

open Aardvark.Base
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

    let constantColor = ConstantColor.constantColor

    let sgColor = SgColor.sgColor

    let vertexColor = VertexColor.vertexColor

    let simpleLighting = SimpleLighting.simpleLighting

    let lighting = Lighting.lighting

    let diffuseTexture = DiffuseTexture.diffuseTexture

    let normalMap = NormalMap.normalMap

    let transformColor = TransformColor.transformColor

    let instanceTrafo = InstanceTrafo.instanceTrafo

    let stableTrafo = SimpleLighting.stableTrafo
    let stableHeadlight = SimpleLighting.stableLight