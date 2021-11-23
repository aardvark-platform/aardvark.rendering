namespace Aardvark.Rendering.GL

open System
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4

[<AutoOpen>]
module TextureStorageExtensions =

    module private InternalFormat =
        open Aardvark.Base

        let getCompatibleFormatAndType =
            LookupTable.lookupTable [
                // Depth-only formats
                PixelInternalFormat.DepthComponent32f,  (PixelFormat.DepthComponent, PixelType.Float)
                PixelInternalFormat.DepthComponent32,   (PixelFormat.DepthComponent, PixelType.UnsignedInt)
                PixelInternalFormat.DepthComponent24,   (PixelFormat.DepthComponent, PixelType.UnsignedInt)
                PixelInternalFormat.DepthComponent16,   (PixelFormat.DepthComponent, PixelType.UnsignedShort)

                // Combined depth-stencil formats
                PixelInternalFormat.Depth32fStencil8,   (PixelFormat.DepthStencil, PixelType.Float32UnsignedInt248Rev)
                PixelInternalFormat.Depth24Stencil8,    (PixelFormat.DepthStencil, PixelType.UnsignedInt248)

                // Stencil-only format
                unbox 36168 (* GL_STENCIL_INDEX8 *),    (PixelFormat.StencilIndex, PixelType.UnsignedByte)

                // Sized internal formats
                PixelInternalFormat.R8,           (PixelFormat.Red,         PixelType.UnsignedByte)
                PixelInternalFormat.R8Snorm,      (PixelFormat.Red,         PixelType.Byte)
                PixelInternalFormat.R16,          (PixelFormat.Red,         PixelType.UnsignedShort)
                PixelInternalFormat.R16Snorm,     (PixelFormat.Red,         PixelType.Short)
                PixelInternalFormat.Rg8,          (PixelFormat.Rg,          PixelType.UnsignedByte)
                PixelInternalFormat.Rg8Snorm,     (PixelFormat.Rg,          PixelType.Byte)
                PixelInternalFormat.Rg16,         (PixelFormat.Rg,          PixelType.UnsignedShort)
                PixelInternalFormat.Rg16Snorm,    (PixelFormat.Rg,          PixelType.Short)
                PixelInternalFormat.R3G3B2,       (PixelFormat.Rgb,         PixelType.UnsignedByte332)
                PixelInternalFormat.Rgb4,         (PixelFormat.Rgb,         PixelType.UnsignedByte)
                PixelInternalFormat.Rgb5,         (PixelFormat.Rgb,         PixelType.UnsignedByte)
                PixelInternalFormat.Rgb8,         (PixelFormat.Rgb,         PixelType.UnsignedByte)
                PixelInternalFormat.Rgb8Snorm,    (PixelFormat.Rgb,         PixelType.Byte)
                PixelInternalFormat.Rgb10,        (PixelFormat.Rgb,         PixelType.UnsignedShort)
                PixelInternalFormat.Rgb12,        (PixelFormat.Rgb,         PixelType.UnsignedShort)
                PixelInternalFormat.Rgb16Snorm,   (PixelFormat.Rgb,         PixelType.Short)
                PixelInternalFormat.Rgba2,        (PixelFormat.Rgba,        PixelType.UnsignedShort4444)
                PixelInternalFormat.Rgba4,        (PixelFormat.Rgba,        PixelType.UnsignedShort4444)
                PixelInternalFormat.Rgb5A1,       (PixelFormat.Rgba,        PixelType.UnsignedShort5551)
                PixelInternalFormat.Rgba8,        (PixelFormat.Rgba,        PixelType.UnsignedByte)
                PixelInternalFormat.Rgba8Snorm,   (PixelFormat.Rgba,        PixelType.Byte)
                PixelInternalFormat.Rgb10A2,      (PixelFormat.Rgba,        PixelType.UnsignedInt1010102)
                PixelInternalFormat.Rgb10A2ui,    (PixelFormat.RgbaInteger, PixelType.UnsignedInt1010102)
                PixelInternalFormat.Rgba12,       (PixelFormat.Rgba,        PixelType.UnsignedShort)
                PixelInternalFormat.Rgba16,       (PixelFormat.Rgba,        PixelType.UnsignedShort)
                PixelInternalFormat.Srgb8,        (PixelFormat.Rgb,         PixelType.UnsignedByte)
                PixelInternalFormat.Srgb8Alpha8,  (PixelFormat.Rgba,        PixelType.UnsignedByte)
                PixelInternalFormat.R16f,         (PixelFormat.Red,         PixelType.HalfFloat)
                PixelInternalFormat.Rg16f,        (PixelFormat.Rg,          PixelType.HalfFloat)
                PixelInternalFormat.Rgb16f,       (PixelFormat.Rgb,         PixelType.HalfFloat)
                PixelInternalFormat.Rgba16f,      (PixelFormat.Rgba,        PixelType.HalfFloat)
                PixelInternalFormat.R32f,         (PixelFormat.Red,         PixelType.Float)
                PixelInternalFormat.Rg32f,        (PixelFormat.Rg,          PixelType.Float)
                PixelInternalFormat.Rgb32f,       (PixelFormat.Rgb,         PixelType.Float)
                PixelInternalFormat.Rgba32f,      (PixelFormat.Rgba,        PixelType.Float)
                PixelInternalFormat.R11fG11fB10f, (PixelFormat.Rgb,         PixelType.UnsignedInt10F11F11FRev)
                PixelInternalFormat.Rgb9E5,       (PixelFormat.Rgb,         PixelType.UnsignedInt5999Rev)
                PixelInternalFormat.R8i,          (PixelFormat.RedInteger,  PixelType.Byte)
                PixelInternalFormat.R8ui,         (PixelFormat.RedInteger,  PixelType.UnsignedByte)
                PixelInternalFormat.R16i,         (PixelFormat.RedInteger,  PixelType.Short)
                PixelInternalFormat.R16ui,        (PixelFormat.RedInteger,  PixelType.UnsignedShort)
                PixelInternalFormat.R32i,         (PixelFormat.RedInteger,  PixelType.Int)
                PixelInternalFormat.R32ui,        (PixelFormat.RedInteger,  PixelType.UnsignedInt)
                PixelInternalFormat.Rg8i,         (PixelFormat.RgInteger,   PixelType.Byte)
                PixelInternalFormat.Rg8ui,        (PixelFormat.RgInteger,   PixelType.UnsignedByte)
                PixelInternalFormat.Rg16i,        (PixelFormat.RgInteger,   PixelType.Short)
                PixelInternalFormat.Rg16ui,       (PixelFormat.RgInteger,   PixelType.UnsignedShort)
                PixelInternalFormat.Rg32i,        (PixelFormat.RgInteger,   PixelType.Int)
                PixelInternalFormat.Rg32ui,       (PixelFormat.RgInteger,   PixelType.UnsignedInt)
                PixelInternalFormat.Rgb8i,        (PixelFormat.RgbInteger,  PixelType.Byte)
                PixelInternalFormat.Rgb8ui,       (PixelFormat.RgbInteger,  PixelType.UnsignedByte)
                PixelInternalFormat.Rgb16i,       (PixelFormat.RgbInteger,  PixelType.Short)
                PixelInternalFormat.Rgb16ui,      (PixelFormat.RgbInteger,  PixelType.UnsignedShort)
                PixelInternalFormat.Rgb32i,       (PixelFormat.RgbInteger,  PixelType.Int)
                PixelInternalFormat.Rgb32ui,      (PixelFormat.RgbInteger,  PixelType.UnsignedInt)
                PixelInternalFormat.Rgba8i,       (PixelFormat.RgbaInteger, PixelType.Byte)
                PixelInternalFormat.Rgba8ui,      (PixelFormat.RgbaInteger, PixelType.UnsignedByte)
                PixelInternalFormat.Rgba16i,      (PixelFormat.RgbaInteger, PixelType.Short)
                PixelInternalFormat.Rgba16ui,     (PixelFormat.RgbaInteger, PixelType.UnsignedShort)
                PixelInternalFormat.Rgba32i,      (PixelFormat.RgbaInteger, PixelType.Int)
                PixelInternalFormat.Rgba32ui,     (PixelFormat.RgbaInteger, PixelType.UnsignedInt)
            ]

    [<AutoOpen>]
    module ARB_texture_storage =

        // Arb functions are in GL type, need alias to prevent recursive calls
        [<AutoOpen>]
        module private ArbFunctions =
            module GL =
                type Arb = OpenGL4.GL

        type GL private() =

            static let supported = ExtensionHelpers.isSupported (Version(4,2)) "GL_ARB_texture_storage"

            static member ARB_texture_storage = supported

            static member TexStorage1D(target : TextureTarget1d, levels : int, internalFormat : SizedInternalFormat,
                                       width : int) =
                if supported then
                    GL.Arb.TexStorage1D(target, levels, internalFormat, width);
                else
                    let internalFormat = unbox internalFormat
                    let format, typ = InternalFormat.getCompatibleFormatAndType internalFormat

                    for i = 0 to levels - 1 do
                        let width = max 1 (width >>> i)
                        GL.TexImage1D(unbox target, i, internalFormat, width, 0, format, typ, 0n)


            static member TexStorage2D(target : TextureTarget2d, levels : int, internalFormat : SizedInternalFormat,
                                       width : int, height : int) =
                if supported then
                    GL.Arb.TexStorage2D(target, levels, internalFormat, width, height)
                else
                    let internalFormat = unbox internalFormat
                    let format, typ = InternalFormat.getCompatibleFormatAndType internalFormat

                    match target with
                    | TextureTarget2d.TextureCubeMap ->
                        for i = 0 to levels - 1 do
                            let width = max 1 (width >>> i)
                            let height = max 1 (height >>> i)

                            for face = 0 to 5 do
                                let target = int TextureTarget.TextureCubeMapPositiveX + face |> unbox<TextureTarget>
                                GL.Arb.TexImage2D(target, i, internalFormat, width, height, 0, format, typ, 0n)

                    | TextureTarget2d.Texture1DArray | TextureTarget2d.ProxyTexture1DArray ->
                        for i = 0 to levels - 1 do
                            let width = max 1 (width >>> i)
                            GL.Arb.TexImage2D(unbox target, i, internalFormat, width, height, 0, format, typ, 0n)

                    | _ ->
                        for i = 0 to levels - 1 do
                            let width = max 1 (width >>> i)
                            let height = max 1 (height >>> i)
                            GL.Arb.TexImage2D(unbox target, i, internalFormat, width, height, 0, format, typ, 0n)


            static member TexStorage3D(target : TextureTarget3d, levels : int, internalFormat : SizedInternalFormat,
                                       width : int, height : int, depth : int) =
                if supported then
                    GL.Arb.TexStorage3D(target, levels, internalFormat, width, height, depth)
                else
                    let internalFormat = unbox internalFormat
                    let format, typ = InternalFormat.getCompatibleFormatAndType internalFormat

                    match target with
                    | TextureTarget3d.Texture3D | TextureTarget3d.ProxyTexture3D ->
                        for i = 0 to levels - 1 do
                            let width = max 1 (width >>> i)
                            let height = max 1 (height >>> i)
                            let depth = max 1 (depth >>> i)
                            GL.Arb.TexImage3D(unbox target, i, internalFormat, width, height, depth, 0, format, typ, 0n)

                    | _ ->
                        for i = 0 to levels - 1 do
                            let width = max 1 (width >>> i)
                            let height = max 1 (height >>> i)
                            GL.Arb.TexImage3D(unbox target, i, internalFormat, width, height, depth, 0, format, typ, 0n)

    [<AutoOpen>]
    module ARB_texture_storage_multisample =

        // Arb functions are in GL type, need alias to prevent recursive calls
        [<AutoOpen>]
        module private ArbFunctions =
            module GL =
                type Arb = OpenGL4.GL

        type GL private() =

            static let supported = ExtensionHelpers.isSupported (Version(4,3)) "GL_ARB_texture_storage_multisample"

            static member ARB_texture_storage_multisample = supported

            static member TexStorage2DMultisample(target : TextureTargetMultisample2d, samples : int, internalFormat : SizedInternalFormat,
                                                  width : int, height : int, fixedSampleLocations : bool) =
                if supported then
                    GL.Arb.TexStorage2DMultisample(target, samples, internalFormat, width, height, fixedSampleLocations)
                else
                    GL.Arb.TexImage2DMultisample(unbox target, samples, unbox internalFormat, width, height, fixedSampleLocations)


            static member TexStorage3DMultisample(target : TextureTargetMultisample3d, samples : int, internalFormat : SizedInternalFormat,
                                                  width : int, height : int, depth : int, fixedSampleLocations : bool) =
                if supported then
                    GL.Arb.TexStorage3DMultisample(target, samples, internalFormat, width, height, depth, fixedSampleLocations)
                else
                    GL.Arb.TexImage3DMultisample(unbox target, samples, unbox internalFormat, width, height, depth, fixedSampleLocations)