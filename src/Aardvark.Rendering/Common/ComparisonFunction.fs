namespace Aardvark.Rendering

// FShade.ComparisonFunction does not define a value for zero,
// which is problematic when the default constructor of a struct initializes its fields
// to zero. Also we cannot use type aliases when we need C# interopability.
type ComparisonFunction =
    | Always = 0
    | Never = 1
    | Less = 2
    | Equal = 3
    | LessOrEqual = 4
    | Greater = 5
    | GreaterOrEqual = 6
    | NotEqual = 7