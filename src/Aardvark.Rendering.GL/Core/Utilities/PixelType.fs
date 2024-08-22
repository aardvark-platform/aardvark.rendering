namespace Aardvark.Rendering.GL

open Aardvark.Base
open OpenTK.Graphics.OpenGL4

module internal PixelType =

    let ofType =
        LookupTable.lookup [
             typeof<uint8>,   PixelType.UnsignedByte
             typeof<int8>,    PixelType.Byte
             typeof<uint16>,  PixelType.UnsignedShort
             typeof<int16>,   PixelType.Short
             typeof<uint32>,  PixelType.UnsignedInt
             typeof<int32>,   PixelType.Int
             typeof<float32>, PixelType.Float
             typeof<float16>, PixelType.HalfFloat
         ]

    let size =
        LookupTable.lookup [
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
