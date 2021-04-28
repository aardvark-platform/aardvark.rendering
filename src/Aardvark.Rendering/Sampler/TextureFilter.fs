namespace Aardvark.Rendering

type FilterMode =
    | Point = 0
    | Linear = 1

[<Struct>]
type TextureFilter =
    {
        Minification : FilterMode
        Magnification : FilterMode
        MipmapMode : FilterMode voption
    }

    static member MinLinearMagMipPoint =
        { Minification  = FilterMode.Linear
          Magnification = FilterMode.Point
          MipmapMode    = FilterMode.Point |> ValueSome }

    static member MinLinearMagPointMipLinear =
        { Minification  = FilterMode.Linear
          Magnification = FilterMode.Point
          MipmapMode    = FilterMode.Linear |> ValueSome }

    static member MinMagLinearMipPoint =
        { Minification  = FilterMode.Linear
          Magnification = FilterMode.Linear
          MipmapMode    = FilterMode.Point |> ValueSome }

    static member MinMagMipLinear =
        { Minification  = FilterMode.Linear
          Magnification = FilterMode.Linear
          MipmapMode    = FilterMode.Linear |> ValueSome }

    static member MinMagMipPoint =
        { Minification  = FilterMode.Point
          Magnification = FilterMode.Point
          MipmapMode    = FilterMode.Point |> ValueSome }

    static member MinMagPointMipLinear =
        { Minification  = FilterMode.Point
          Magnification = FilterMode.Point
          MipmapMode    = FilterMode.Linear |> ValueSome }

    static member MinPointMagLinearMipPoint =
        { Minification  = FilterMode.Point
          Magnification = FilterMode.Linear
          MipmapMode    = FilterMode.Point |> ValueSome }

    static member MinPointMagMipLinear =
        { Minification  = FilterMode.Point
          Magnification = FilterMode.Linear
          MipmapMode    = FilterMode.Linear |> ValueSome }

    static member MinMagPoint =
        { Minification  = FilterMode.Point
          Magnification = FilterMode.Point
          MipmapMode    = ValueNone }

    static member MinMagLinear =
        { Minification  = FilterMode.Linear
          Magnification = FilterMode.Linear
          MipmapMode    = ValueNone }

    static member MinPointMagLinear =
        { Minification  = FilterMode.Point
          Magnification = FilterMode.Linear
          MipmapMode    = ValueNone }

    static member MinLinearMagPoint =
        { Minification  = FilterMode.Linear
          Magnification = FilterMode.Point
          MipmapMode    = ValueNone }

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