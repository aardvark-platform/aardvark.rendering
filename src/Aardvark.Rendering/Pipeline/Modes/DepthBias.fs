namespace Aardvark.Rendering

/// Struct describing the applied depth bias
[<Struct>]
type DepthBias =
    {
        /// Scalar factor controlling the constant depth value added to a fragment.
        mutable Constant    : float

        /// Scalar factor controlling the slope of a fragment's depth bias.
        mutable SlopeScale  : float

        /// Maximum or minimum depth bias (no clamping if zero).
        mutable Clamp       : float
    }

    /// Returns whether depth bias is enabled.
    member x.Enabled =
        x.Constant <> 0.0 || x.SlopeScale <> 0.0

    /// Returns whether clamping is enabled.
    member x.Clamped =
        x.Clamp <> 0.0

    /// No depth bias.
    static member None =
        { Constant   = 0.0
          SlopeScale = 0.0
          Clamp      = 0.0 }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DepthBias =

    /// Creates a constant depth bias.
    let constant (value : float) =
        { DepthBias.None with Constant = value }

    /// Creates a depth bias without clamping.
    let simple (constant : float) (slopeScale : float) =
        { Constant   = constant
          SlopeScale = slopeScale
          Clamp      = 0.0 }

    /// Creates a depth bias with clamping.
    let custom (constant : float) (slopeScale : float) (clamp : float) =
        { simple constant slopeScale with Clamp = clamp }