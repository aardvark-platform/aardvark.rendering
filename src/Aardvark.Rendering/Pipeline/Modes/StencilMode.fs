namespace Aardvark.Rendering

open System

type StencilOperation =
    | Keep           = 0
    | Zero           = 1
    | Replace        = 2
    | Increment      = 3
    | IncrementWrap  = 4
    | Decrement      = 5
    | DecrementWrap  = 6
    | Invert         = 7

[<Struct>]
type StencilMask =
    val mutable Value : uint32

    new(value : uint32) =
        { Value = value }

    new(value : int) =
        StencilMask(uint32 value)

    new(enabled : bool) =
        StencilMask(if enabled then UInt32.MaxValue else 0u)

    static member op_Explicit (m : StencilMask) : uint32 =
        m.Value

    static member All =
        StencilMask(true)

    static member None =
        StencilMask(false)

[<Struct>]
type StencilMode =
    {
        mutable Pass        : StencilOperation
        mutable Fail        : StencilOperation
        mutable DepthFail   : StencilOperation
        mutable Comparison  : ComparisonFunction
        mutable CompareMask : StencilMask
        mutable Reference   : int32
    }

    member x.Enabled =
        x.Pass <> StencilOperation.Keep ||
        x.Fail <> StencilOperation.Keep ||
        x.DepthFail <> StencilOperation.Keep

    /// Default state that does not modify stencil values.
    static member Default =
        { Pass          = StencilOperation.Keep
          Fail          = StencilOperation.Keep
          DepthFail     = StencilOperation.Keep
          Comparison    = ComparisonFunction.Always
          CompareMask   = StencilMask.All
          Reference     = 0 }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module StencilMode =

    /// Creates a stencil mode without compare mask.
    let simple (pass : StencilOperation) (fail : StencilOperation) (depthFail : StencilOperation)
               (compare : ComparisonFunction) (reference : int) =
        { StencilMode.Default with
            Pass        = pass
            Fail        = fail
            DepthFail   = depthFail
            Comparison  = compare
            Reference   = reference }

    /// Sets the compare mask for the given stencil state.
    let withMask (compareMask : StencilMask) (mode : StencilMode) =
        { mode with CompareMask = compareMask }