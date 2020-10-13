namespace Aardvark.Rendering.GL

open System
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4

[<AutoOpen>]
module ARB_buffer_storage =

    // Arb functions are in GL type, need alias to prevent recursive calls
    [<AutoOpen>]
    module private ArbFunctions =
        module GL =
            type Arb = OpenGL4.GL

    type GL private() =

        static let supported = ExtensionHelpers.isSupported (Version(4,4)) "GL_ARB_buffer_storage"

        static member ARB_buffer_storage = supported

        static member BufferStorage(target : BufferTarget, size : nativeint, data : nativeint, flags: BufferStorageFlags) =
            if supported then
                GL.Arb.BufferStorage(target, size, data, flags)
            else
                GL.BufferData(target, size, data, BufferUsageHint.DynamicDraw)