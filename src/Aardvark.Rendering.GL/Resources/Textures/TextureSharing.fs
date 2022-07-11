namespace Aardvark.Rendering.GL

open System.Runtime.InteropServices
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Aardvark.Base
open Aardvark.Rendering
open Microsoft.FSharp.NativeInterop
open Aardvark.Rendering.GL
open System.Runtime.CompilerServices




[<Extension; AbstractClass; Sealed>]
type ContextTextureSharingExtensions =

    [<Extension>]
    static member ImportTexture(this : Context, texture : IBackendTexture) =
        
        using this.ResourceLock (fun _ ->
            if texture.ShareInfo.IsNone then
                failwith "no shared texture"

            if texture.Dimension <> TextureDimension.Texture2D then
                failwith "not implemented"

            let shareInfo = texture.ShareInfo |> Option.get

            let sharedMemory = this.ImportMemoryBlock(shareInfo.BlockHandle, shareInfo.BlockSize)
        
            let glTexHandle = GL.GenTexture()
            let textureTarget = if shareInfo.IsArray then TextureTarget.Texture2DArray else TextureTarget.Texture2D // TODO: other
            GL.BindTexture(textureTarget, glTexHandle)

            // TODO: 1d, 2d, 2dms, 3d, 3dms, cube?
            let internalFormat = TextureFormat.toInternalFormat texture.Format
            if (shareInfo.IsArray) then
                OpenTK.Graphics.OpenGL.GL.Ext.TextureStorageMem3D(glTexHandle, texture.MipMapLevels, unbox internalFormat, texture.Size.X, texture.Size.Y, texture.Count, sharedMemory.GLHandle, shareInfo.Offset);
            else
                OpenTK.Graphics.OpenGL.GL.Ext.TextureStorageMem2D(glTexHandle, texture.MipMapLevels, unbox internalFormat, texture.Size.X, texture.Size.Y, sharedMemory.GLHandle, shareInfo.Offset);
            GL.Check "TextureStorageMemXX"

            let cnt = if shareInfo.IsArray then Some texture.Count else None
            let tex = Texture(this, glTexHandle, texture.Dimension, texture.MipMapLevels, texture.Samples, texture.Size, cnt, texture.Format, shareInfo.SizeInBytes)

            tex
        )
