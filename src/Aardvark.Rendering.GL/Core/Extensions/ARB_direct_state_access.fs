namespace Aardvark.Rendering.GL

#nowarn "51"

open System
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL

open ExtensionHelpers

[<AutoOpen>]
module ARB_direct_state_access =

    // Arb functions are in GL type, need alias to prevent recursive calls
    [<AutoOpen>]
    module private ArbFunctions =
        module GL =
            type Arb = OpenGL4.GL

    type GL private() =

        static let supported = ExtensionHelpers.isSupported (Version(4,5)) "GL_ARB_direct_state_access"

        static member ARB_direct_state_access = supported

        static member CreateBuffer() =
            if supported then
                let mutable b = 0
                GL.CreateBuffers(1, &b)
                b
            else
                let b = GL.GenBuffer()
                bindBuffer b ignore
                b

        static member CreateFramebuffer() =
            if supported then
                let mutable b = 0
                GL.CreateFramebuffers(1, &b)
                b
            else
                let b = GL.GenFramebuffer()
                bindFramebuffer b ignore
                b

        static member CreateTexture(target : TextureTarget) =
            if supported then
                let mutable t = 0
                GL.CreateTextures(target, 1, &t)
                t
            else
                let t = GL.GenTexture()
                bindTexture target t ignore
                t

        static member NamedBufferData(buffer : int, size : nativeint, data : nativeint, usage : BufferUsageHint) =
            if supported then
                GL.Arb.NamedBufferData(buffer, size, data, usage)
            else
                bindBuffer buffer (fun t ->
                    GL.BufferData(t, size, data, usage)
                )

        static member NamedBufferSubData(buffer : int, offset : nativeint, size : nativeint, data : nativeint) =
            if supported then
                GL.Arb.NamedBufferSubData(buffer, offset, size, data)
            else
                bindBuffer buffer (fun t ->
                    GL.BufferSubData(t, offset, size, data)
                )

        static member NamedClearBufferSubData(buffer : int, ifmt : PixelInternalFormat, offset : nativeint, size : nativeint, fmt : PixelFormat, pixelType : PixelType, data : nativeint) =
            if supported then
                GL.Arb.ClearNamedBufferSubData(buffer, ifmt, offset, size, fmt, pixelType, data)
            else
                bindBuffer buffer (fun t ->
                    GL.ClearBufferSubData(t, ifmt, offset, int size, fmt, pixelType, data)
                )

        static member GetNamedBufferSubData(buffer : int, offset : nativeint, size : nativeint, data : nativeint) =
            if supported then
                GL.Arb.GetNamedBufferSubData(buffer, offset, size, data)
            else
                bindBuffer buffer (fun t ->
                    GL.GetBufferSubData(t, offset, size, data)
                )

        static member CopyNamedBufferSubData(src : int, srcOffset : nativeint, dst : int, dstOffset : nativeint, size : nativeint) =
            if supported then
                GL.Arb.CopyNamedBufferSubData(src, dst, srcOffset, dstOffset, size)
            else
                bindBuffers src dst (fun tSrc tDst ->
                    GL.CopyBufferSubData(tSrc, tDst, srcOffset, dstOffset, size)
                )


        static member NamedBufferStorage(buffer: int, size : nativeint, data : nativeint, flags: BufferStorageFlags) =
            if supported then
                if GL.ARB_buffer_storage then
                    GL.Arb.NamedBufferStorage(buffer, size, data, flags)
                else
                    GL.Arb.NamedBufferData(buffer, size, data, BufferUsageHint.DynamicDraw)
            else
                bindBuffer buffer (fun t ->
                    GL.BufferStorage(t, size, data, flags)
                )

        static member MapNamedBuffer(buffer: int, access : BufferAccess) =
            if supported then
                GL.Arb.MapNamedBuffer(buffer, access)
            else
                bindBuffer buffer (fun t ->
                    GL.MapBuffer(t, access)
                )

        static member UnmapNamedBuffer(buffer: int) =
            if supported then
                GL.Arb.UnmapNamedBuffer(buffer)
            else
                bindBuffer buffer (fun t ->
                    GL.UnmapBuffer(t)
                )

        static member MapNamedBufferRange(buffer: int, offset : nativeint, size : nativeint, access : BufferAccessMask) =
            if supported then
                GL.Arb.MapNamedBufferRange(buffer, offset, size, access)
            else
                bindBuffer buffer (fun t ->
                    GL.MapBufferRange(t, offset, size, access)
                )

        static member FlushMappedNamedBufferRange(buffer: int, offset : nativeint, size : nativeint) =
            if supported then
                GL.Arb.FlushMappedNamedBufferRange(buffer, offset, size)
            else
                bindBuffer buffer (fun t ->
                    GL.FlushMappedBufferRange(t, offset, size)
                )

        static member GetNamedBufferParameter(buffer : int, pname : BufferParameterName, arr : int[]) =
            if supported then
                GL.Arb.GetNamedBufferParameter(buffer, pname, arr)
            else
                bindBuffer buffer (fun t ->
                    GL.GetBufferParameter(t, pname, arr)
                )
        static member GetNamedBufferParameter(buffer : int, pname : BufferParameterName, res : byref<int>) =
            if supported then
                GL.Arb.GetNamedBufferParameter(buffer, pname, &res)
            else
                let mutable r = res
                bindBuffer buffer (fun t ->
                    GL.GetBufferParameter(t, pname, &r)
                )
                res <- r
        static member GetNamedBufferParameter(buffer : int, pname : BufferParameterName, res : byref<int64>) =
            if supported then
                GL.Arb.GetNamedBufferParameter(buffer, pname, &&res)
            else
                let mutable r = res
                bindBuffer buffer (fun t ->
                    GL.GetBufferParameter(t, pname, &r)
                )
                res <- r

        static member NamedCopyBufferSubData(readBuffer : int, writeBuffer : int, readOffset : nativeint, writeOffset : nativeint, size : nativeint) =
            if supported then
                GL.Arb.CopyNamedBufferSubData(readBuffer, writeBuffer, readOffset, writeOffset, size)
            else
                bindBuffers readBuffer writeBuffer (fun t0 t1 ->
                    GL.CopyBufferSubData(t0, t1, readOffset, writeOffset, size)
                )