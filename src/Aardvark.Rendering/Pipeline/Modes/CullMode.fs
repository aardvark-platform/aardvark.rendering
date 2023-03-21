namespace Aardvark.Rendering

/// Determines how primitives are discarded based on their orientation.
type CullMode =

    /// No primitives are discarded.
    | None          = 0

    /// Front facing primitives are discarded, only back facing primitives are rasterized.
    | Front         = 1

    /// Back facing primitives are discarded, only front facing primitives are rasterized.
    | Back          = 2

    /// All primitives are discarded.
    | FrontAndBack  = 3