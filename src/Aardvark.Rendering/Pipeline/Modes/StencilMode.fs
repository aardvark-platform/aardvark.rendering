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
type StencilState =
    {
        mutable Pass        : StencilOperation
        mutable Fail        : StencilOperation
        mutable DepthFail   : StencilOperation
        mutable Comparison  : ComparisonFunction
        mutable CompareMask : uint32
        mutable WriteMask   : uint32
        mutable Reference   : int32
    }

    /// Default state that does not modify stencil values.
    static member Default =
        { Pass          = StencilOperation.Keep
          Fail          = StencilOperation.Keep
          DepthFail     = StencilOperation.Keep
          Comparison    = ComparisonFunction.Always
          CompareMask   = UInt32.MaxValue
          WriteMask     = UInt32.MaxValue
          Reference     = 0 }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module StencilState =

    /// Creates a stencil state without masks.
    let simple (pass : StencilOperation) (fail : StencilOperation) (depthFail : StencilOperation)
               (compare : ComparisonFunction) (reference : int) =
        { StencilState.Default with
            Pass        = pass
            Fail        = fail
            DepthFail   = depthFail
            Comparison  = compare
            Reference   = reference }

    /// Sets the masks for the given stencil state.
    let withMask (compareMask : uint32) (writeMask : uint32) (state : StencilState) =
        { state with
            CompareMask = compareMask
            WriteMask   = writeMask }


[<Struct; CustomEquality; NoComparison>]
type StencilMode =
    {
        Enabled : bool
        Front   : StencilState
        Back    : StencilState
    }

    member private x.Config =
        x.Front, x.Back

    override x.Equals(other : obj) =
        let cmp (x : StencilMode) (y : StencilMode) =
            match x.Enabled, y.Enabled with
            | false, false -> true
            | true, true -> x.Config = y.Config
            | _ -> false

        match other with
        | :? StencilMode as y -> cmp x y
        | _ -> false

    override x.GetHashCode() =
        if x.Enabled then
            hash x.Config
        else
            0

    /// Disabled stencil testing.
    static member Disabled =
        { Enabled   = false
          Front     = Unchecked.defaultof<_>
          Back      = Unchecked.defaultof<_> }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module StencilMode =

    /// Creates a stencil mode with two separate front and back states.
    let separate (front : StencilState) (back : StencilState) =
        { Enabled   = true
          Front     = front
          Back      = back }

    /// Creates a stencil mode from a single state.
    let single (state : StencilState) =
        separate state state

    /// Create a stencil mode without masks.
    let simple (pass : StencilOperation) (fail : StencilOperation) (depthFail : StencilOperation)
               (compare : ComparisonFunction) (reference : int) =
        single <| StencilState.simple pass fail depthFail compare reference

    /// Sets the masks for the given stencil mode.
    let withMask (compareMask : uint32) (writeMask : uint32) (mode : StencilMode) =
        { mode with
            Front = mode.Front |> StencilState.withMask compareMask writeMask
            Back = mode.Back |> StencilState.withMask compareMask writeMask }