namespace Aardvark.Rendering

open System
open System.Runtime.InteropServices

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

[<Flags>]
type ColorComponentFlags =
    | Red   = 0x1
    | Green = 0x2
    | Blue  = 0x4
    | Alpha = 0x8
    | None  = 0x0
    | All   = 0xF

[<Struct; CustomEquality; NoComparison>]
type BlendMode =
    val Enabled                 : bool
    val SourceColorFactor       : BlendFactor
    val SourceAlphaFactor       : BlendFactor
    val DestinationColorFactor  : BlendFactor
    val DestinationAlphaFactor  : BlendFactor
    val ColorOperation          : BlendOperation
    val AlphaOperation          : BlendOperation
    val ColorWriteMask          : ColorComponentFlags

    /// Constructs a blend mode using separate parameters for color and alpha.
    new(sourceColorFactor : BlendFactor, destinationColorFactor : BlendFactor,
        sourceAlphaFactor : BlendFactor, destinationAlphaFactor : BlendFactor,
        [<Optional; DefaultParameterValue(BlendOperation.Add)>] colorOperation : BlendOperation,
        [<Optional; DefaultParameterValue(BlendOperation.Add)>] alphaOperation : BlendOperation,
        [<Optional; DefaultParameterValue(ColorComponentFlags.All)>] colorWriteMask : ColorComponentFlags,
        [<Optional; DefaultParameterValue(true)>] enabled : bool) =

        { Enabled                   = enabled
          SourceColorFactor         = sourceColorFactor
          SourceAlphaFactor         = sourceAlphaFactor
          DestinationColorFactor    = destinationColorFactor
          DestinationAlphaFactor    = destinationAlphaFactor
          ColorOperation            = colorOperation
          AlphaOperation            = alphaOperation
          ColorWriteMask            = colorWriteMask }

    /// Constructs a blend mode using the same parameters for color and alpha.
    new(sourceFactor : BlendFactor, destinationFactor : BlendFactor,
        [<Optional; DefaultParameterValue(BlendOperation.Add)>] operation : BlendOperation,
        [<Optional; DefaultParameterValue(ColorComponentFlags.All)>] colorWriteMask : ColorComponentFlags,
        [<Optional; DefaultParameterValue(true)>] enabled : bool) =

        BlendMode(
            sourceFactor, destinationFactor,
            sourceFactor, destinationFactor,
            operation, operation,
            colorWriteMask, enabled
        )

    member private x.Config = (
        x.SourceColorFactor,
        x.SourceAlphaFactor,
        x.DestinationColorFactor,
        x.DestinationAlphaFactor,
        x.ColorOperation,
        x.AlphaOperation,
        x.ColorWriteMask
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
        BlendMode(Unchecked.defaultof<_>, Unchecked.defaultof<_>, enabled = false)

    /// Standard alpha blending with f_src = alpha_src
    /// and f_dst = 1 - alpha_src.
    static member Blend =
        BlendMode(BlendFactor.SourceAlpha, BlendFactor.InvSourceAlpha)