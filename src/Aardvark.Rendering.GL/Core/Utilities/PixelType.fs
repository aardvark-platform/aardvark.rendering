namespace Aardvark.Rendering.GL

open Aardvark.Base
open OpenTK.Graphics.OpenGL4

module internal PixelType =

    let size =
        LookupTable.lookupTable [
            PixelType.UnsignedByte,             1
            PixelType.Byte,                     1
            PixelType.UnsignedShort,            2
            PixelType.Short,                    2
            PixelType.UnsignedInt,              4
            PixelType.Int,                      4
            PixelType.HalfFloat,                2
            PixelType.Float,                    4
            PixelType.UnsignedInt248,           4
            PixelType.Float32UnsignedInt248Rev, 8
        ]

[<AutoOpen>]
module internal PixelTypeExtensions =

    type PixelType with
        member x.Size = PixelType.size x
