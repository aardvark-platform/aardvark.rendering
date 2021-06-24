namespace Aardvark.Rendering

open System

[<Flags>]
type ResourceAccess =
    | None                       = 0x0000
    | ShaderRead                 = 0x0001
    | ShaderWrite                = 0x0002
    | TransferRead               = 0x0004
    | TransferWrite              = 0x0008
    | IndirectCommandRead        = 0x0010
    | IndexRead                  = 0x0020
    | VertexAttributeRead        = 0x0040
    | UniformRead                = 0x0080
    | InputRead                  = 0x0100
    | ColorRead                  = 0x0200
    | ColorWrite                 = 0x0400
    | DepthStencilRead           = 0x0800
    | DepthStencilWrite          = 0x1000
    | AccelerationStructureRead  = 0x2000
    | AccelerationStructureWrite = 0x4000
    | All                        = 0x7FFF