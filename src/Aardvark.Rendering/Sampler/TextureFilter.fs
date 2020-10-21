namespace Aardvark.Rendering

open Aardvark.Base

type TextureFilter =
    | Anisotropic = 0
    | MinLinearMagMipPoint = 1
    | MinLinearMagPointMipLinear = 2
    | MinMagLinearMipPoint = 3
    | MinMagMipLinear = 4
    | MinMagMipPoint = 5
    | MinMagPointMipLinear = 6
    | MinPointMagLinearMipPoint = 7
    | MinPointMagMipLinear = 8
    | MinMagPoint = 9
    | MinMagLinear = 10
    | MinPointMagLinear = 11
    | MinLinearMagPoint = 12

type FilterMode =
    | Point = 0
    | Linear = 1

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module TextureFilter =

    /// Returns whether the texture filter is anisotropic.
    let isAnisotropic = function
        | TextureFilter.Anisotropic -> true
        | _ -> false

    /// Returns the minification filter mode.
    let minification =
        LookupTable.lookupTable [
            TextureFilter.MinMagPoint,                  FilterMode.Point
            TextureFilter.MinPointMagLinear,            FilterMode.Point
            TextureFilter.MinLinearMagPoint,            FilterMode.Linear
            TextureFilter.MinMagLinear,                 FilterMode.Linear
            TextureFilter.MinMagMipPoint,               FilterMode.Point
            TextureFilter.MinMagPointMipLinear,         FilterMode.Point
            TextureFilter.MinPointMagLinearMipPoint,    FilterMode.Point
            TextureFilter.MinPointMagMipLinear,         FilterMode.Point
            TextureFilter.MinLinearMagMipPoint,         FilterMode.Linear
            TextureFilter.MinLinearMagPointMipLinear,   FilterMode.Linear
            TextureFilter.MinMagLinearMipPoint,         FilterMode.Linear
            TextureFilter.MinMagMipLinear,              FilterMode.Linear
            TextureFilter.Anisotropic,                  FilterMode.Linear
        ]

    /// Returns the magnification filter mode.
    let magnification =
        LookupTable.lookupTable [
            TextureFilter.MinMagPoint,                  FilterMode.Point
            TextureFilter.MinPointMagLinear,            FilterMode.Linear
            TextureFilter.MinLinearMagPoint,            FilterMode.Point
            TextureFilter.MinMagLinear,                 FilterMode.Linear
            TextureFilter.MinMagMipPoint,               FilterMode.Point
            TextureFilter.MinMagPointMipLinear,         FilterMode.Point
            TextureFilter.MinPointMagLinearMipPoint,    FilterMode.Linear
            TextureFilter.MinPointMagMipLinear,         FilterMode.Linear
            TextureFilter.MinLinearMagMipPoint,         FilterMode.Point
            TextureFilter.MinLinearMagPointMipLinear,   FilterMode.Point
            TextureFilter.MinMagLinearMipPoint,         FilterMode.Linear
            TextureFilter.MinMagMipLinear,              FilterMode.Linear
            TextureFilter.Anisotropic,                  FilterMode.Linear
        ]

    /// Returns the mipmap mode.
    let mipmapMode =
        LookupTable.lookupTable' [
            TextureFilter.Anisotropic,                FilterMode.Linear
            TextureFilter.MinLinearMagMipPoint,       FilterMode.Point
            TextureFilter.MinLinearMagPointMipLinear, FilterMode.Linear
            TextureFilter.MinMagLinearMipPoint,       FilterMode.Point
            TextureFilter.MinMagMipLinear,            FilterMode.Linear
            TextureFilter.MinMagMipPoint,             FilterMode.Point
            TextureFilter.MinMagPointMipLinear,       FilterMode.Linear
            TextureFilter.MinPointMagLinearMipPoint,  FilterMode.Point
            TextureFilter.MinPointMagMipLinear,       FilterMode.Linear
        ]