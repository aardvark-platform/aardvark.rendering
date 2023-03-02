namespace Aardvark.Rendering

open System

[<Flags>]
type ResourceAccess =
    | None                       = 0x00000
    | ShaderRead                 = 0x00001
    | ShaderWrite                = 0x00002
    | TransferRead               = 0x00004
    | TransferWrite              = 0x00008
    | IndirectCommandRead        = 0x00010
    | IndexRead                  = 0x00020
    | VertexAttributeRead        = 0x00040
    | UniformRead                = 0x00080
    | InputRead                  = 0x00100
    | ColorRead                  = 0x00200
    | ColorWrite                 = 0x00400
    | DepthStencilRead           = 0x00800
    | DepthStencilWrite          = 0x01000
    | AccelerationStructureRead  = 0x02000
    | AccelerationStructureWrite = 0x04000
    | HostRead                   = 0x08000
    | HostWrite                  = 0x10000
    | All                        = 0x1FFFF