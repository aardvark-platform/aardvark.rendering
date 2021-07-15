namespace Aardvark.Rendering

open System

type BlendOperation =
    | Add               = 0
    | Subtract          = 1
    | ReverseSubtract   = 2
    | Minimum           = 3
    | Maximum           = 4

type BlendFactor =
    | Zero                    = 0
    | One                     = 1
    | SourceColor             = 2
    | InvSourceColor          = 3
    | DestinationColor        = 4
    | InvDestinationColor     = 5
    | SourceAlpha             = 6
    | InvSourceAlpha          = 7
    | DestinationAlpha        = 8
    | InvDestinationAlpha     = 9
    | ConstantColor           = 10
    | InvConstantColor        = 11
    | ConstantAlpha           = 12
    | InvConstantAlpha        = 13
    | SourceAlphaSaturate     = 14
    | SecondarySourceColor    = 15
    | InvSecondarySourceColor = 16
    | SecondarySourceAlpha    = 17
    | InvSecondarySourceAlpha = 18

[<Struct; CustomEquality; NoComparison; CLIMutable>]
type BlendMode =
    {
        /// Specifies whether blending is enabled.
        Enabled                 : bool

        /// The factor multiplied with the source RGB values.
        SourceColorFactor       : BlendFactor

        /// The factor multiplied with the source alpha value.
        SourceAlphaFactor       : BlendFactor

        /// The factor multiplied with the destination RGB values.
        DestinationColorFactor  : BlendFactor

        /// The factor multiplied with the destination alpha value.
        DestinationAlphaFactor  : BlendFactor

        /// The blend operation performed on the source and destination RGB values.
        ColorOperation          : BlendOperation

        /// The blend operation performed on the source and destination alpha values.
        AlphaOperation          : BlendOperation
    }

    member private x.Config = (
        x.SourceColorFactor,
        x.SourceAlphaFactor,
        x.DestinationColorFactor,
        x.DestinationAlphaFactor,
        x.ColorOperation,
        x.AlphaOperation
    )

    member x.Equals(other : BlendMode) =
        match x.Enabled, other.Enabled with
        | false, false -> true
        | true, true -> x.Config = other.Config
        | _ -> false

    override x.Equals(other : obj) =
        match other with
        | :? BlendMode as y -> x.Equals(y)
        | _ -> false

    override x.GetHashCode() =
        if x.Enabled then
            hash x.Config
        else
            0

    interface IEquatable<BlendMode> with
        member x.Equals(other : BlendMode) = x.Equals(other)

    /// Disabled blending.
    static member None =
        { Enabled                   = false
          SourceColorFactor         = Unchecked.defaultof<_>
          SourceAlphaFactor         = Unchecked.defaultof<_>
          DestinationColorFactor    = Unchecked.defaultof<_>
          DestinationAlphaFactor    = Unchecked.defaultof<_>
          ColorOperation            = Unchecked.defaultof<_>
          AlphaOperation            = Unchecked.defaultof<_> }

    /// Standard alpha blending with f_src = alpha_src
    /// and f_dst = 1 - alpha_src.
    static member Blend =
        { Enabled                   = true
          SourceColorFactor         = BlendFactor.SourceAlpha
          SourceAlphaFactor         = BlendFactor.SourceAlpha
          DestinationColorFactor    = BlendFactor.InvSourceAlpha
          DestinationAlphaFactor    = BlendFactor.InvSourceAlpha
          ColorOperation            = BlendOperation.Add
          AlphaOperation            = BlendOperation.Add }

    /// Add colors.
    static member Add =
        { BlendMode.Blend with
            SourceColorFactor = BlendFactor.One
            SourceAlphaFactor = BlendFactor.One
            DestinationColorFactor = BlendFactor.One
            DestinationAlphaFactor = BlendFactor.One
        }

    /// Multiply colors.
    static member Multiply =
        { BlendMode.Blend with
            SourceColorFactor = BlendFactor.Zero
            SourceAlphaFactor = BlendFactor.Zero
            DestinationColorFactor = BlendFactor.SourceColor
            DestinationAlphaFactor = BlendFactor.SourceAlpha
        }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module BlendMode =

    /// Creates an additive blend mode with the given factors.
    let simple (src : BlendFactor) (dst : BlendFactor) =
        { BlendMode.Blend with
            SourceColorFactor = src
            SourceAlphaFactor = src
            DestinationColorFactor = dst
            DestinationAlphaFactor = dst }

    /// Creates an additive blend mode with the given factors.
    let separate (srcColor : BlendFactor) (srcAlpha : BlendFactor) (dstColor : BlendFactor) (dstAlpha : BlendFactor) =
        { BlendMode.Blend with
            SourceColorFactor = srcColor
            SourceAlphaFactor = srcAlpha
            DestinationColorFactor = dstColor
            DestinationAlphaFactor = dstAlpha }

    /// Creates a custom blend mode.
    let custom (srcColor : BlendFactor) (srcAlpha : BlendFactor) (dstColor : BlendFactor) (dstAlpha : BlendFactor)
               (colorOp : BlendOperation) (alphaOp : BlendOperation) =
        { Enabled                   = true
          SourceColorFactor         = srcColor
          SourceAlphaFactor         = srcAlpha
          DestinationColorFactor    = dstColor
          DestinationAlphaFactor    = dstAlpha
          ColorOperation            = colorOp
          AlphaOperation            = alphaOp }