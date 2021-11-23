namespace Aardvark.Rendering.GL

open System

[<AutoOpen>]
module ARB_texture_stencil8 =

    type GL private() =
        static let supported = ExtensionHelpers.isSupported (Version(4, 4)) "GL_ARB_texture_stencil8"
        static member ARB_texture_stencil8 = supported