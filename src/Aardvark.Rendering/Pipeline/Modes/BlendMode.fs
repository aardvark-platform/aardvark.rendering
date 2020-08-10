namespace Aardvark.Rendering

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

[<Struct; CustomEquality; NoComparison>]
type BlendMode =
    {
        mutable Enabled                 : bool
        mutable SourceColorFactor       : BlendFactor
        mutable SourceAlphaFactor       : BlendFactor
        mutable DestinationColorFactor  : BlendFactor
        mutable DestinationAlphaFactor  : BlendFactor
        mutable ColorOperation          : BlendOperation
        mutable AlphaOperation          : BlendOperation
    }

    member private x.Config = (
        x.SourceColorFactor,
        x.SourceAlphaFactor,
        x.DestinationColorFactor,
        x.DestinationAlphaFactor,
        x.ColorOperation,
        x.AlphaOperation
    )

    override x.Equals(other : obj) =
        let cmp (x : BlendMode) (y : BlendMode) =
            match x.Enabled, y.Enabled with
            | false, false -> true
            | true, true -> x.Config = y.Config
            | _ -> false

        match other with
        | :? BlendMode as y -> cmp x y
        | _ -> false

    override x.GetHashCode() =
        if x.Enabled then
            hash x.Config
        else
            0

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
            SourceColorFactor = BlendFactor.One
            SourceAlphaFactor = BlendFactor.One
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