namespace Aardvark.Rendering

open Aardvark.Base

type ShaderStage =
    | Vertex        = 1
    | TessControl   = 2
    | TessEval      = 3
    | Geometry      = 4
    | Fragment      = 5
    | Compute       = 6
    | RayGeneration = 7
    | Intersection  = 8
    | AnyHit        = 9
    | ClosestHit    = 10
    | Miss          = 11
    | Callable      = 12

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ShaderStage =
    let ofFShade =
        LookupTable.lookup [
            FShade.ShaderStage.Vertex,          ShaderStage.Vertex
            FShade.ShaderStage.TessControl,     ShaderStage.TessControl
            FShade.ShaderStage.TessEval,        ShaderStage.TessEval
            FShade.ShaderStage.Geometry,        ShaderStage.Geometry
            FShade.ShaderStage.Fragment,        ShaderStage.Fragment
            FShade.ShaderStage.Compute,         ShaderStage.Compute
            FShade.ShaderStage.RayGeneration,   ShaderStage.RayGeneration
            FShade.ShaderStage.Intersection,    ShaderStage.Intersection 
            FShade.ShaderStage.AnyHit,          ShaderStage.AnyHit       
            FShade.ShaderStage.ClosestHit,      ShaderStage.ClosestHit   
            FShade.ShaderStage.Miss,            ShaderStage.Miss         
            FShade.ShaderStage.Callable,        ShaderStage.Callable     
        ]

    let toFShade =
        LookupTable.lookup [
            ShaderStage.Vertex,         FShade.ShaderStage.Vertex
            ShaderStage.TessControl,    FShade.ShaderStage.TessControl
            ShaderStage.TessEval,       FShade.ShaderStage.TessEval
            ShaderStage.Geometry,       FShade.ShaderStage.Geometry
            ShaderStage.Fragment,       FShade.ShaderStage.Fragment
            ShaderStage.Compute,        FShade.ShaderStage.Compute
            ShaderStage.RayGeneration,  FShade.ShaderStage.RayGeneration
            ShaderStage.Intersection,   FShade.ShaderStage.Intersection 
            ShaderStage.AnyHit,         FShade.ShaderStage.AnyHit       
            ShaderStage.ClosestHit,     FShade.ShaderStage.ClosestHit   
            ShaderStage.Miss,           FShade.ShaderStage.Miss         
            ShaderStage.Callable,       FShade.ShaderStage.Callable     
        ]