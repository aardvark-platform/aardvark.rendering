namespace Aardvark.Rendering.GL

open OpenTK.Graphics.OpenGL4
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.GL
open System.Runtime.CompilerServices

[<Extension; AbstractClass; Sealed>]
type ContextTextureSharingExtensions =

    [<Extension>]
    static member ImportTexture(this : Context, texture : IBackendTexture) =
        
        using this.ResourceLock (fun _ ->
            if texture.ShareInfo.IsNone then
                failwith "[GL] ImportTexture: no shared texture"

            let shareInfo = texture.ShareInfo |> Option.get

            if texture.Dimension = TextureDimension.Texture3D && (texture.Samples > 1 || shareInfo.IsArray) then
                failwith "[GL] ImportTexture: 3d textures cannot be multisampled or an array"

            if texture.Samples > 1 && texture.MipMapLevels > 1 then
                failwith "[GL] ImportTexture: multisampled textures cannot have mipmaps"

            let sharedMemory = this.ImportMemoryBlock(shareInfo.BlockHandle, shareInfo.BlockSize)
        
            let glTexHandle = GL.GenTexture()
            let textureTarget = TextureTarget.ofParameters texture.Dimension shareInfo.IsArray (texture.Samples > 1)
            GL.BindTexture(textureTarget, glTexHandle)

            let internalFormat = TextureFormat.toInternalFormat texture.Format
            match texture.Dimension with
            | TextureDimension.Texture1D -> 
                if shareInfo.IsArray then
                    OpenTK.Graphics.OpenGL.GL.Ext.TextureStorageMem2D(glTexHandle, texture.MipMapLevels, unbox internalFormat, texture.Size.X, texture.Count, sharedMemory.GLHandle, shareInfo.Offset);
                else
                    OpenTK.Graphics.OpenGL.GL.Ext.TextureStorageMem1D(glTexHandle, texture.MipMapLevels, unbox internalFormat, texture.Size.X, sharedMemory.GLHandle, shareInfo.Offset);
            | TextureDimension.Texture2D
            | TextureDimension.TextureCube ->
                if (shareInfo.IsArray) then
                    if texture.Samples > 1 then
                        OpenTK.Graphics.OpenGL.GL.Ext.TextureStorageMem3DMultisample(glTexHandle, texture.Samples, unbox internalFormat, texture.Size.X, texture.Size.Y, texture.Count, true, sharedMemory.GLHandle, shareInfo.Offset);
                    else
                        OpenTK.Graphics.OpenGL.GL.Ext.TextureStorageMem3D(glTexHandle, texture.MipMapLevels, unbox internalFormat, texture.Size.X, texture.Size.Y, texture.Count, sharedMemory.GLHandle, shareInfo.Offset);
                else
                    if texture.Samples > 1 then
                        OpenTK.Graphics.OpenGL.GL.Ext.TextureStorageMem2DMultisample(glTexHandle, texture.Samples, unbox internalFormat, texture.Size.X, texture.Size.Y, true, sharedMemory.GLHandle, shareInfo.Offset);
                    else
                        OpenTK.Graphics.OpenGL.GL.Ext.TextureStorageMem2D(glTexHandle, texture.MipMapLevels, unbox internalFormat, texture.Size.X, texture.Size.Y, sharedMemory.GLHandle, shareInfo.Offset);
            | TextureDimension.Texture3D ->
                OpenTK.Graphics.OpenGL.GL.Ext.TextureStorageMem3D(glTexHandle, texture.MipMapLevels, unbox internalFormat, texture.Size.X, texture.Size.Y, texture.Size.Z, sharedMemory.GLHandle, shareInfo.Offset);
            | _ -> 
                GL.DeleteTexture glTexHandle
                this.ReleaseShareHandle sharedMemory
                failwith "invalid TextureDimension"

            GL.Check "TextureStorageMemXX"

            this.SetDefaultTextureParams(textureTarget, texture.MipMapLevels)

            let cnt = if shareInfo.IsArray then Some texture.Count else None
            Texture(this, glTexHandle, texture.Dimension, texture.MipMapLevels, texture.Samples, texture.Size, cnt, texture.Format, shareInfo.SizeInBytes)
        )
