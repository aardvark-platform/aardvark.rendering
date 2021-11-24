namespace Aardvark.Rendering.GL

open System
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL

[<AutoOpen>]
module ARB_texture_view =

    type GL private() =

        static let supported = ExtensionHelpers.isSupported (Version(4,3)) "GL_ARB_texture_view"

        static member ARB_texture_view = supported

    type GL.Dispatch with

        static member TextureView(texture : int, target : TextureTarget, origTexture : int,
                                  internalFormat : PixelInternalFormat, minLevel : int, numLevels : int,
                                  minLayer : int, numLayers : int) =
            if GL.ARB_texture_view then
                GL.TextureView(texture, target, origTexture, internalFormat, minLevel, numLevels, minLayer, numLayers)
            else
                failwith "glTextureView is not available!"