namespace Aardvark.Rendering

open System
open Aardvark.Base

module DefaultSemantic =

    let Positions = Symbol.Create "Positions"
    let Normals = Symbol.Create "Normals"
    let Colors = Symbol.Create "Colors"
    let DiffuseColorCoordinates = Symbol.Create "DiffuseColorCoordinates"
    let LightMapCoordinates = Symbol.Create "LightMapCoordinates"
    let NormalMapCoordinates = Symbol.Create "NormalMapCoordinates"
    let DiffuseColorUTangents = Symbol.Create "DiffuseColorUTangents"
    let DiffuseColorVTangents = Symbol.Create "DiffuseColorVTangents"

    // Single Attributes
    let DiffuseColorTexture = Symbol.Create "DiffuseColorTexture"
    let AmbientColorTexture = Symbol.Create "AmbientColorTexture"
    let EmissiveColorTexture = Symbol.Create "EmissiveColorTexture"
    let SpecularColorTexture = Symbol.Create "SpecularColorTexture"
    let ShininessTexture = Symbol.Create "ShininessTexture"

    let LightMapTexture = Symbol.Create "LightMapTexture"
    let NormalMapTexture = Symbol.Create "NormalMapTexture"

    let Trafo3d = Symbol.Create "Trafo3d"
    let DiffuseColorTrafo2d = Symbol.Create "DiffuseColorTrafo2d"
    let Name = Symbol.Create "Name"
    let Material = Symbol.Create "Material"
    let CreaseAngle = Symbol.Create "CreaseAngle"
    let WindingOrder = Symbol.Create "WindingOrder"
    let AreaSum = Symbol.Create "AreaSum"

    // Various
    let DepthStencil = Symbol.Create "DepthStencil"

    [<Obsolete("Use DefaultSemantic.DepthStencil instead. This is just an alias.")>]
    let Depth = DepthStencil

    [<Obsolete("Use DefaultSemantic.DepthStencil instead. This is just an alias.")>]
    let Stencil = DepthStencil

    let ImageOutput = Symbol.Create "ImageOutput"
    let InstanceTrafo = Symbol.Create "InstanceTrafo"
    let InstanceTrafoInv = Symbol.Create "InstanceTrafoInv"
    let SamplerStateModifier = Symbol.Create "SamplerStateModifier"