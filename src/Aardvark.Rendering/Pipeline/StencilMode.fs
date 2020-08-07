namespace Aardvark.Rendering

open System
open System.Runtime.InteropServices

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
    val Pass        : StencilOperation
    val Fail        : StencilOperation
    val DepthFail   : StencilOperation
    val Compare     : ComparisonFunction
    val CompareMask : uint32
    val WriteMask   : uint32
    val Reference   : int32

    new(pass : StencilOperation, fail : StencilOperation, depthFail : StencilOperation,
        [<Optional; DefaultParameterValue(ComparisonFunction.Always)>] compare : ComparisonFunction,
        [<Optional; DefaultParameterValue(0)>] reference : int32,
        [<Optional; DefaultParameterValue(UInt32.MaxValue)>] compareMask : uint32,
        [<Optional; DefaultParameterValue(UInt32.MaxValue)>] writeMask : uint32) =

        { Pass          = pass
          Fail          = fail
          DepthFail     = depthFail
          Compare       = compare
          CompareMask   = compareMask
          WriteMask     = writeMask
          Reference     = reference }

    static member Default =
        StencilState(StencilOperation.Keep, StencilOperation.Keep, StencilOperation.Keep)

[<Struct; CustomEquality; NoComparison>]
type StencilMode =
    val Enabled : bool
    val Front   : StencilState
    val Back    : StencilState

    new(front : StencilState, back : StencilState,
        [<Optional; DefaultParameterValue(true)>] enabled : bool) =
        { Enabled = enabled; Front = front; Back = back }

    new(state : StencilState) =
        StencilMode(state, state)

    new(pass : StencilOperation, fail : StencilOperation, depthFail : StencilOperation,
        [<Optional; DefaultParameterValue(ComparisonFunction.Always)>] compare : ComparisonFunction,
        [<Optional; DefaultParameterValue(0)>] reference : int32,
        [<Optional; DefaultParameterValue(UInt32.MaxValue)>] compareMask : uint32,
        [<Optional; DefaultParameterValue(UInt32.MaxValue)>] writeMask : uint32) =

        StencilMode(
            StencilState(pass, fail, depthFail, compare, reference, compareMask, writeMask)
        )

    new(frontPass : StencilOperation, backPass : StencilOperation,
        frontFail : StencilOperation, backFail : StencilOperation,
        frontDepthFail : StencilOperation, backDepthFail : StencilOperation,
        [<Optional; DefaultParameterValue(ComparisonFunction.Always)>] frontCompare : ComparisonFunction,
        [<Optional; DefaultParameterValue(ComparisonFunction.Always)>] backCompare : ComparisonFunction,
        [<Optional; DefaultParameterValue(0)>] frontReference : int32,
        [<Optional; DefaultParameterValue(0)>] backReference : int32,
        [<Optional; DefaultParameterValue(UInt32.MaxValue)>] frontCompareMask : uint32,
        [<Optional; DefaultParameterValue(UInt32.MaxValue)>] frontWriteMask : uint32,
        [<Optional; DefaultParameterValue(UInt32.MaxValue)>] backCompareMask : uint32,
        [<Optional; DefaultParameterValue(UInt32.MaxValue)>] backWriteMask : uint32) =

        StencilMode(
            StencilState(frontPass, frontFail, frontDepthFail, frontCompare, frontReference, frontCompareMask, frontWriteMask),
            StencilState(backPass, backFail, backDepthFail, backCompare, backReference, backCompareMask, backWriteMask)
        )

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
        StencilMode(Unchecked.defaultof<_>, Unchecked.defaultof<_>, enabled = false)