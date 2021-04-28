﻿namespace Aardvark.Rendering

open Aardvark.Base

module DefaultSemantic =

    let Positions = Sym.ofString "Positions"
    let Normals = Sym.ofString "Normals"
    let Colors = Sym.ofString "Colors"
    let DiffuseColorCoordinates = Sym.ofString "DiffuseColorCoordinates"
    let LightMapCoordinates = Sym.ofString "LightMapCoordinates"
    let NormalMapCoordinates = Sym.ofString "NormalMapCoordinates"
    let DiffuseColorUTangents = Sym.ofString "DiffuseColorUTangents"
    let DiffuseColorVTangents = Sym.ofString "DiffuseColorVTangents"

    // Single Attributes
    let DiffuseColorTexture = Sym.ofString "DiffuseColorTexture"
    let AmbientColorTexture = Sym.ofString "AmbientColorTexture"
    let EmissiveColorTexture = Sym.ofString "EmissiveColorTexture"
    let SpecularColorTexture = Sym.ofString "SpecularColorTexture"
    let ShininessTexture = Sym.ofString "ShininessTexture"

    let LightMapTexture = Sym.ofString "LightMapTexture"
    let NormalMapTexture = Sym.ofString "NormalMapTexture"

    let Trafo3d = Sym.ofString "Trafo3d"
    let DiffuseColorTrafo2d = Sym.ofString "DiffuseColorTrafo2d"
    let Name = Sym.ofString "Name"
    let Material = Sym.ofString "Material"
    let CreaseAngle = Sym.ofString "CreaseAngle"
    let WindingOrder = Sym.ofString "WindingOrder"
    let AreaSum = Sym.ofString "AreaSum"

    // Various
    let Depth = Sym.ofString "Depth"
    let Stencil = Sym.ofString "Stencil"
    let ImageOutput = Sym.ofString "ImageOutput"
    let InstanceTrafo = Sym.ofString "InstanceTrafo"
    let InstanceTrafoInv = Sym.ofString "InstanceTrafoInv"
    let SamplerStateModifier = Sym.ofString "SamplerStateModifier"