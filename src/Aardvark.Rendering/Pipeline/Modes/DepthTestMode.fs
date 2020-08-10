namespace Aardvark.Rendering

/// Struct describing the depth test mode.
[<Struct; CustomEquality; NoComparison>]
type DepthTestMode =
    {
        /// Indicates whether depth testing is enabled
        mutable Enabled : bool

        /// The comparison function to use for depth testing.
        mutable Comparison : ComparisonFunction

        /// Indicates whether depth values are clamped.
        mutable Clamp   : bool
    }

    member private x.Config =
        x.Comparison, x.Clamp

    override x.Equals(other : obj) =
        let cmp (x : DepthTestMode) (y : DepthTestMode) =
            match x.Enabled, y.Enabled with
            | false, false -> true
            | true, true -> x.Config = y.Config
            | _ -> false

        match other with
        | :? DepthTestMode as y -> cmp x y
        | _ -> false

    override x.GetHashCode() =
        if x.Enabled then
            hash x.Config
        else
            0

    static member None            = { Enabled = false; Comparison = Unchecked.defaultof<_>; Clamp = Unchecked.defaultof<_> }
    static member Always          = { Enabled = true; Comparison = ComparisonFunction.Always; Clamp = false }
    static member Never           = { Enabled = true; Comparison = ComparisonFunction.Never; Clamp = false }
    static member Less            = { Enabled = true; Comparison = ComparisonFunction.Less; Clamp = false }
    static member LessOrEqual     = { Enabled = true; Comparison = ComparisonFunction.LessOrEqual; Clamp = false }
    static member Greater         = { Enabled = true; Comparison = ComparisonFunction.Greater; Clamp = false }
    static member GreaterOrEqual  = { Enabled = true; Comparison = ComparisonFunction.GreaterOrEqual; Clamp = false }
    static member Equal           = { Enabled = true; Comparison = ComparisonFunction.Equal; Clamp = false }
    static member NotEqual        = { Enabled = true; Comparison = ComparisonFunction.NotEqual; Clamp = false }