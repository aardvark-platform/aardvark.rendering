namespace Aardvark.Base

type ShaderStage =
    | Vertex = 1
    | TessControl = 2
    | TessEval = 3
    | Geometry = 4
    | Fragment = 5
    | Compute = 6

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ShaderStage =
    let ofFShade =
        LookupTable.lookupTable [
            FShade.ShaderStage.Vertex,      ShaderStage.Vertex
            FShade.ShaderStage.TessControl, ShaderStage.TessControl
            FShade.ShaderStage.TessEval,    ShaderStage.TessEval
            FShade.ShaderStage.Geometry,    ShaderStage.Geometry
            FShade.ShaderStage.Fragment,    ShaderStage.Fragment
            FShade.ShaderStage.Compute,     ShaderStage.Compute
        ]

    let toFShade =
        LookupTable.lookupTable [
            ShaderStage.Vertex,         FShade.ShaderStage.Vertex
            ShaderStage.TessControl,    FShade.ShaderStage.TessControl
            ShaderStage.TessEval,       FShade.ShaderStage.TessEval
            ShaderStage.Geometry,       FShade.ShaderStage.Geometry
            ShaderStage.Fragment,       FShade.ShaderStage.Fragment
            ShaderStage.Compute,        FShade.ShaderStage.Compute
        ]