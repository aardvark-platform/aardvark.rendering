namespace Aardvark.Rendering

/// Depth test comparison function
type DepthTest =
    | None = 0
    | Never = 1
    | Less = 2
    | Equal = 3
    | LessOrEqual = 4
    | Greater = 5
    | GreaterOrEqual = 6
    | NotEqual = 7
    | Always = 8