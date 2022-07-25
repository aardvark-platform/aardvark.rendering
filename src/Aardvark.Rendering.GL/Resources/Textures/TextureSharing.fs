namespace Aardvark.Rendering.GL

open OpenTK.Graphics.OpenGL
open OpenTK.Graphics.OpenGL4
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.GL
open System.Runtime.CompilerServices

[<Extension; AbstractClass; Sealed>]
type ContextTextureSharingExtensions =

    [<Extension>]
    static member ImportTexture(this : Context, texture : IExportedBackendTexture) =
        
        using this.ResourceLock (fun _ ->
            let memory = texture.Memory

            if texture.Dimension = TextureDimension.Texture3D && (texture.Samples > 1 || texture.IsArray) then
                failwith "[GL] ImportTexture: 3d textures cannot be multisampled or an array"

            if texture.Samples > 1 && texture.MipMapLevels > 1 then
                failwith "[GL] ImportTexture: multisampled textures cannot have mipmaps"

            let sharedMemory = this.ImportMemoryBlock(memory.Block.Handle, memory.Block.SizeInBytes)
        
            let glTexHandle = GL.GenTexture()
            let textureTarget = TextureTarget.ofParameters texture.Dimension texture.IsArray (texture.Samples > 1)
            GL.BindTexture(textureTarget, glTexHandle)

            let internalFormat = TextureFormat.toInternalFormat texture.Format
            match texture.Dimension with
            | TextureDimension.Texture1D -> 
                if texture.IsArray then
                    GL.Ext.TextureStorageMem2D(glTexHandle, texture.MipMapLevels, unbox internalFormat, texture.Size.X, texture.Count, sharedMemory.GLHandle, memory.Offset);
                else
                    GL.Ext.TextureStorageMem1D(glTexHandle, texture.MipMapLevels, unbox internalFormat, texture.Size.X, sharedMemory.GLHandle, memory.Offset);
            | TextureDimension.Texture2D
            | TextureDimension.TextureCube ->
                if (texture.IsArray) then
                    if texture.Samples > 1 then
                        GL.Ext.TextureStorageMem3DMultisample(glTexHandle, texture.Samples, unbox internalFormat, texture.Size.X, texture.Size.Y, texture.Count, true, sharedMemory.GLHandle, memory.Offset);
                    else
                        GL.Ext.TextureStorageMem3D(glTexHandle, texture.MipMapLevels, unbox internalFormat, texture.Size.X, texture.Size.Y, texture.Count, sharedMemory.GLHandle, memory.Offset);
                else
                    if texture.Samples > 1 then
                        GL.Ext.TextureStorageMem2DMultisample(glTexHandle, texture.Samples, unbox internalFormat, texture.Size.X, texture.Size.Y, true, sharedMemory.GLHandle, memory.Offset);
                    else
                        GL.Ext.TextureStorageMem2D(glTexHandle, texture.MipMapLevels, unbox internalFormat, texture.Size.X, texture.Size.Y, sharedMemory.GLHandle, memory.Offset);
            | TextureDimension.Texture3D ->
                GL.Ext.TextureStorageMem3D(glTexHandle, texture.MipMapLevels, unbox internalFormat, texture.Size.X, texture.Size.Y, texture.Size.Z, sharedMemory.GLHandle, memory.Offset);
            | _ -> 
                GL.DeleteTexture glTexHandle
                this.ReleaseShareHandle sharedMemory
                failwith "invalid TextureDimension"

            GL.Check "TextureStorageMemXX"

            this.SetDefaultTextureParams(textureTarget, texture.MipMapLevels)

            SharedTexture(this, glTexHandle, texture, sharedMemory)
        )
