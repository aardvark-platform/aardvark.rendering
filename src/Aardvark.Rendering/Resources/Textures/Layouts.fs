namespace Aardvark.Rendering

type TextureLayout =
    | Undefined                 = 0
    | Sample                    = 1
    | TransferRead              = 2
    | TransferWrite             = 3
    | ShaderRead                = 4
    | ShaderWrite               = 5
    | ShaderReadWrite           = 6
    | ColorAttachment           = 7
    | DepthStencil              = 8
    | DepthStencilRead          = 9
    | General                   = 10
    | Present                   = 11