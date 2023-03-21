namespace Aardvark.Rendering

type WindingOrder =
    | Clockwise         = 0
    | CounterClockwise  = 1

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module WindingOrder =

    /// Reverses the given winding order.
    let inline reverse (order : WindingOrder) =
        if order = WindingOrder.Clockwise then
            WindingOrder.CounterClockwise
        else
            WindingOrder.Clockwise