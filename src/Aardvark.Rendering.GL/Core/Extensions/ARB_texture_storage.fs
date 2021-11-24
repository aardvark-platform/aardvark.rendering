namespace Aardvark.Rendering.GL

open System
open OpenTK.Graphics.OpenGL4

[<AutoOpen>]
module TextureStorageExtensions =

    [<AutoOpen>]
    module ARB_texture_storage =

        type GL private() =

            static let supported = ExtensionHelpers.isSupported (Version(4,2)) "GL_ARB_texture_storage"

            static member ARB_texture_storage = supported

        type GL.Dispatch with

            static member TexStorage1D(target : TextureTarget1d, levels : int, internalFormat : SizedInternalFormat,
                                        width : int) =
                if GL.ARB_texture_storage then
                    GL.TexStorage1D(target, levels, internalFormat, width);
                else
                    let format, typ = TextureFormat.toFormatAndType (unbox internalFormat)

                    for i = 0 to levels - 1 do
                        let width = max 1 (width >>> i)
                        GL.TexImage1D(unbox target, i, unbox internalFormat, width, 0, format, typ, 0n)


            static member TexStorage2D(target : TextureTarget2d, levels : int, internalFormat : SizedInternalFormat,
                                        width : int, height : int) =
                if GL.ARB_texture_storage then
                    GL.TexStorage2D(target, levels, internalFormat, width, height)
                else
                    let format, typ = TextureFormat.toFormatAndType (unbox internalFormat)

                    match target with
                    | TextureTarget2d.TextureCubeMap ->
                        for i = 0 to levels - 1 do
                            let width = max 1 (width >>> i)
                            let height = max 1 (height >>> i)

                            for face = 0 to 5 do
                                let target = int TextureTarget.TextureCubeMapPositiveX + face |> unbox<TextureTarget>
                                GL.TexImage2D(target, i, unbox internalFormat, width, height, 0, format, typ, 0n)

                    | TextureTarget2d.Texture1DArray | TextureTarget2d.ProxyTexture1DArray ->
                        for i = 0 to levels - 1 do
                            let width = max 1 (width >>> i)
                            GL.TexImage2D(unbox target, i, unbox internalFormat, width, height, 0, format, typ, 0n)

                    | _ ->
                        for i = 0 to levels - 1 do
                            let width = max 1 (width >>> i)
                            let height = max 1 (height >>> i)
                            GL.TexImage2D(unbox target, i, unbox internalFormat, width, height, 0, format, typ, 0n)


            static member TexStorage3D(target : TextureTarget3d, levels : int, internalFormat : SizedInternalFormat,
                                        width : int, height : int, depth : int) =
                if GL.ARB_texture_storage then
                    GL.TexStorage3D(target, levels, internalFormat, width, height, depth)
                else
                    let format, typ = TextureFormat.toFormatAndType (unbox internalFormat)

                    match target with
                    | TextureTarget3d.Texture3D | TextureTarget3d.ProxyTexture3D ->
                        for i = 0 to levels - 1 do
                            let width = max 1 (width >>> i)
                            let height = max 1 (height >>> i)
                            let depth = max 1 (depth >>> i)
                            GL.TexImage3D(unbox target, i, unbox internalFormat, width, height, depth, 0, format, typ, 0n)

                    | _ ->
                        for i = 0 to levels - 1 do
                            let width = max 1 (width >>> i)
                            let height = max 1 (height >>> i)
                            GL.TexImage3D(unbox target, i, unbox internalFormat, width, height, depth, 0, format, typ, 0n)

    [<AutoOpen>]
    module ARB_texture_storage_multisample =

        type GL private() =

            static let supported = ExtensionHelpers.isSupported (Version(4,3)) "GL_ARB_texture_storage_multisample"

            static member ARB_texture_storage_multisample = supported


        type GL.Dispatch with

            static member TexStorage2DMultisample(target : TextureTargetMultisample2d, samples : int, internalFormat : SizedInternalFormat,
                                                    width : int, height : int, fixedSampleLocations : bool) =
                if GL.ARB_texture_storage_multisample then
                    GL.TexStorage2DMultisample(target, samples, internalFormat, width, height, fixedSampleLocations)
                else
                    GL.TexImage2DMultisample(unbox target, samples, unbox internalFormat, width, height, fixedSampleLocations)


            static member TexStorage3DMultisample(target : TextureTargetMultisample3d, samples : int, internalFormat : SizedInternalFormat,
                                                    width : int, height : int, depth : int, fixedSampleLocations : bool) =
                if  GL.ARB_texture_storage_multisample then
                    GL.TexStorage3DMultisample(target, samples, internalFormat, width, height, depth, fixedSampleLocations)
                else
                    GL.TexImage3DMultisample(unbox target, samples, unbox internalFormat, width, height, depth, fixedSampleLocations)