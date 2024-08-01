namespace Aardvark.Rendering

type FilterMode =
    | Point = 0
    | Linear = 1

type FilterReduction =
    | WeightedAverage = 0
    | Minimum = 1
    | Maximum = 2

[<Struct>]
type TextureFilter =
    {
        Minification  : FilterMode
        Magnification : FilterMode
        MipmapMode    : FilterMode voption
        Reduction     : FilterReduction
    }

    static member MinLinearMagMipPoint =
        { Minification  = FilterMode.Linear
          Magnification = FilterMode.Point
          MipmapMode    = FilterMode.Point |> ValueSome
          Reduction     = FilterReduction.WeightedAverage }

    static member MinLinearMagPointMipLinear =
        { Minification  = FilterMode.Linear
          Magnification = FilterMode.Point
          MipmapMode    = FilterMode.Linear |> ValueSome
          Reduction     = FilterReduction.WeightedAverage }

    static member MinMagLinearMipPoint =
        { Minification  = FilterMode.Linear
          Magnification = FilterMode.Linear
          MipmapMode    = FilterMode.Point |> ValueSome
          Reduction     = FilterReduction.WeightedAverage }

    static member MinMagMipLinear =
        { Minification  = FilterMode.Linear
          Magnification = FilterMode.Linear
          MipmapMode    = FilterMode.Linear |> ValueSome
          Reduction     = FilterReduction.WeightedAverage }

    static member MinMagMipPoint =
        { Minification  = FilterMode.Point
          Magnification = FilterMode.Point
          MipmapMode    = FilterMode.Point |> ValueSome
          Reduction     = FilterReduction.WeightedAverage }

    static member MinMagPointMipLinear =
        { Minification  = FilterMode.Point
          Magnification = FilterMode.Point
          MipmapMode    = FilterMode.Linear |> ValueSome
          Reduction     = FilterReduction.WeightedAverage }

    static member MinPointMagLinearMipPoint =
        { Minification  = FilterMode.Point
          Magnification = FilterMode.Linear
          MipmapMode    = FilterMode.Point |> ValueSome
          Reduction     = FilterReduction.WeightedAverage }

    static member MinPointMagMipLinear =
        { Minification  = FilterMode.Point
          Magnification = FilterMode.Linear
          MipmapMode    = FilterMode.Linear |> ValueSome
          Reduction     = FilterReduction.WeightedAverage }

    static member MinMagPoint =
        { Minification  = FilterMode.Point
          Magnification = FilterMode.Point
          MipmapMode    = ValueNone
          Reduction     = FilterReduction.WeightedAverage }

    static member MinMagLinear =
        { Minification  = FilterMode.Linear
          Magnification = FilterMode.Linear
          MipmapMode    = ValueNone
          Reduction     = FilterReduction.WeightedAverage }

    static member MinPointMagLinear =
        { Minification  = FilterMode.Point
          Magnification = FilterMode.Linear
          MipmapMode    = ValueNone
          Reduction     = FilterReduction.WeightedAverage }

    static member MinLinearMagPoint =
        { Minification  = FilterMode.Linear
          Magnification = FilterMode.Point
          MipmapMode    = ValueNone
          Reduction     = FilterReduction.WeightedAverage }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module TextureFilter =

    /// Returns the minification filter mode.
    let minification (filter : TextureFilter) =
        filter.Minification

    /// Returns the magnification filter mode.
    let magnification (filter : TextureFilter) =
        filter.Magnification

    /// Returns the mipmap mode.
    let mipmapMode (filter : TextureFilter) =
        filter.MipmapMode

    /// Returns the reduction mode of the filter.
    let reduction (filter : TextureFilter) =
        filter.Reduction