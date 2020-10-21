namespace Aardvark.Rendering

/// Depth test comparison function
type DepthTest =
    | None = 0
    | Always = 0
    | Never = 1
    | Less = 2
    | Equal = 3
    | LessOrEqual = 4
    | Greater = 5
    | GreaterOrEqual = 6
    | NotEqual = 7

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DepthTest =

    /// Converts the depth test function to the equivalent comparison function.
    let toComparisonFunction (test : DepthTest) =
        unbox<ComparisonFunction> test

    /// Converts the depth test function to the equivalent comparison function.
    let ofComparisonFunction (cmp : ComparisonFunction) =
        unbox<DepthTest> cmp