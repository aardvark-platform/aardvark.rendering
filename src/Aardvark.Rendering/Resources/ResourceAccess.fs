namespace Aardvark.Rendering

type ResourceAccess =
    | ShaderRead            = 1
    | ShaderWrite           = 2
    | TransferRead          = 3
    | TransferWrite         = 4
    | IndirectCommandRead   = 5
    | IndexRead             = 6
    | VertexAttributeRead   = 7
    | UniformRead           = 8
    | InputRead             = 9
    | ColorRead             = 10
    | ColorWrite            = 11
    | DepthStencilRead      = 12
    | DepthStencilWrite     = 13

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ResourceAccess =

    let all = Set.ofList [
        ResourceAccess.ShaderRead
        ResourceAccess.ShaderWrite
        ResourceAccess.TransferRead
        ResourceAccess.TransferWrite
        ResourceAccess.IndirectCommandRead
        ResourceAccess.IndexRead
        ResourceAccess.VertexAttributeRead
        ResourceAccess.UniformRead
        ResourceAccess.InputRead
        ResourceAccess.ColorRead
        ResourceAccess.ColorWrite
        ResourceAccess.DepthStencilRead
        ResourceAccess.DepthStencilWrite
    ]