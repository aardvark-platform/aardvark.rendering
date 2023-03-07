namespace Aardvark.Rendering

type TextureLayout =
    | Undefined         = 0
    | TransferRead      = 1
    | TransferWrite     = 2
    | ShaderRead        = 3
    | ShaderWrite       = 4
    | ShaderReadWrite   = 5
    | ColorAttachment   = 6
    | DepthStencil      = 7
    | DepthStencilRead  = 8
    | General           = 9
    | Present           = 10