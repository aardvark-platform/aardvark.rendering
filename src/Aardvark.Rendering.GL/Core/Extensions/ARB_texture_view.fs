namespace Aardvark.Rendering.GL

open System
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL

[<AutoOpen>]
module ARB_texture_view =

    // Arb functions are in GL type, need alias to prevent recursive calls
    [<AutoOpen>]
    module private ArbFunctions =
        module GL =
            type Arb = OpenGL4.GL

    type GL private() =

        static let supported = ExtensionHelpers.isSupported (Version(4,3)) "GL_ARB_texture_view"

        static member ARB_texture_view = supported

        static member TextureView(texture : int, target : TextureTarget, origTexture : int,
                                  internalFormat : PixelInternalFormat, minLevel : int, numLevels : int,
                                  minLayer : int, numLayers : int) =
            if supported then
                GL.Arb.TextureView(texture, target, origTexture, internalFormat, minLevel, numLevels, minLayer, numLayers)
            else
                failwith "glTextureView is not available!"