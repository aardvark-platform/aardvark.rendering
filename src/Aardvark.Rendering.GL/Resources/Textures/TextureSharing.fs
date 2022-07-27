namespace Aardvark.Rendering.GL

open OpenTK.Graphics.OpenGL4
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.GL
open System.Runtime.CompilerServices

[<Extension; AbstractClass; Sealed>]
type ContextTextureSharingExtensions =

    [<Extension>]
    static member internal ImportTexture(this : Context, texture : IExportedBackendTexture) =

        using this.ResourceLock (fun _ ->
            let memory = texture.Memory

            if texture.Dimension <> TextureDimension.Texture2D && (texture.Samples > 1 || texture.IsArray) then
                failwith "[GL] ImportTexture: 3d textures cannot be multisampled or an array"

            if texture.Samples > 1 && texture.MipMapLevels > 1 then
                failwith "[GL] ImportTexture: multisampled textures cannot have mipmaps"

            let sharedMemory = this.ImportMemoryBlock memory.Block

            let handle = GL.GenTexture()
            let target = TextureTarget.ofParameters texture.Dimension texture.IsArray (texture.Samples > 1)
            GL.BindTexture(target, handle)

            let internalFormat = TextureFormat.toInternalFormat texture.Format

            match texture.Dimension with
            | TextureDimension.Texture1D ->
                if texture.IsArray then
                    GL.Dispatch.TexStorageMem2D(target, texture.MipMapLevels, internalFormat, texture.Size.X, texture.Count, sharedMemory.Handle, memory.Offset)
                else
                    GL.Dispatch.TexStorageMem1D(target, texture.MipMapLevels, internalFormat, texture.Size.X, sharedMemory.Handle, memory.Offset)

            | TextureDimension.Texture2D
            | TextureDimension.TextureCube ->
                if (texture.IsArray) then
                    if texture.Samples > 1 then
                        GL.Dispatch.TexStorageMem3DMultisample(target, texture.Samples, internalFormat, texture.Size.X, texture.Size.Y, texture.Count,
                                                               true, sharedMemory.Handle, memory.Offset)
                    else
                        GL.Dispatch.TexStorageMem3D(target, texture.MipMapLevels, internalFormat, texture.Size.X, texture.Size.Y, texture.Count,
                                                    sharedMemory.Handle, memory.Offset)
                else
                    if texture.Samples > 1 then
                        GL.Dispatch.TexStorageMem2DMultisample(target, texture.Samples, internalFormat, texture.Size.X, texture.Size.Y,
                                                               true, sharedMemory.Handle, memory.Offset)
                    else
                        GL.Dispatch.TexStorageMem2D(target, texture.MipMapLevels, internalFormat, texture.Size.X, texture.Size.Y,
                                                    sharedMemory.Handle, memory.Offset)

            | TextureDimension.Texture3D ->
                GL.Dispatch.TexStorageMem3D(target, texture.MipMapLevels, internalFormat, texture.Size.X, texture.Size.Y, texture.Size.Z,
                                            sharedMemory.Handle, memory.Offset)

            | _ ->
                GL.DeleteTexture handle
                sharedMemory.Dispose()
                failwith "invalid TextureDimension"

            GL.Check "TextureStorageMemXX"

            this.SetDefaultTextureParams(target, texture.MipMapLevels)

            new SharedTexture(this, handle, texture, sharedMemory)
        )
