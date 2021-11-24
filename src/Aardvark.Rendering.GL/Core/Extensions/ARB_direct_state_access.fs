namespace Aardvark.Rendering.GL

#nowarn "51"

open System
open Aardvark.Base
open OpenTK.Graphics.OpenGL4

open ExtensionHelpers

[<AutoOpen>]
module ARB_direct_state_access =

    type GL private() =

        static let supported = ExtensionHelpers.isSupported (Version(4,5)) "GL_ARB_direct_state_access"

        static member ARB_direct_state_access = supported

    type GL.Dispatch with

        static member CreateBuffer() =
            if GL.ARB_direct_state_access then
                let mutable b = 0
                GL.CreateBuffers(1, &b)
                b
            else
                let b = GL.GenBuffer()
                bindBuffer b ignore
                b

        static member CreateFramebuffer() =
            if GL.ARB_direct_state_access then
                let mutable b = 0
                GL.CreateFramebuffers(1, &b)
                b
            else
                let b = GL.GenFramebuffer()
                bindFramebuffer b ignore
                b

        static member CreateTexture(target : TextureTarget) =
            if GL.ARB_direct_state_access then
                let mutable t = 0
                GL.CreateTextures(target, 1, &t)
                t
            else
                let t = GL.GenTexture()
                bindTexture target t ignore
                t

        static member TextureSubImage2D(texture : int, target : TextureTarget, level : int,
                                        offset : V2i, size : V2i, fmt : PixelFormat, typ : PixelType, pixels : nativeint) =
            if GL.ARB_direct_state_access then
                GL.TextureSubImage2D(texture, level, offset.X, offset.Y, size.X, size.Y, fmt, typ, pixels)
            else
                bindTexture target texture (fun _ ->
                    GL.TexSubImage2D(target, level, offset.X, offset.Y, size.X, size.Y, fmt, typ, pixels)
                )

        static member NamedBufferData(buffer : int, size : nativeint, data : nativeint, usage : BufferUsageHint) =
            if GL.ARB_direct_state_access then
                GL.NamedBufferData(buffer, size, data, usage)
            else
                bindBuffer buffer (fun t ->
                    GL.BufferData(t, size, data, usage)
                )

        static member NamedBufferSubData(buffer : int, offset : nativeint, size : nativeint, data : nativeint) =
            if GL.ARB_direct_state_access then
                GL.NamedBufferSubData(buffer, offset, size, data)
            else
                bindBuffer buffer (fun t ->
                    GL.BufferSubData(t, offset, size, data)
                )

        static member ClearNamedBufferSubData(buffer : int, ifmt : PixelInternalFormat, offset : nativeint, size : nativeint, fmt : PixelFormat, pixelType : PixelType, data : nativeint) =
            if GL.ARB_direct_state_access then
                GL.ClearNamedBufferSubData(buffer, ifmt, offset, size, fmt, pixelType, data)
            else
                bindBuffer buffer (fun t ->
                    GL.ClearBufferSubData(t, ifmt, offset, int size, fmt, pixelType, data)
                )

        static member GetNamedBufferSubData(buffer : int, offset : nativeint, size : nativeint, data : nativeint) =
            if GL.ARB_direct_state_access then
                GL.GetNamedBufferSubData(buffer, offset, size, data)
            else
                bindBuffer buffer (fun t ->
                    GL.GetBufferSubData(t, offset, size, data)
                )

        static member CopyNamedBufferSubData(src : int, dst : int, srcOffset : nativeint, dstOffset : nativeint, size : nativeint) =
            if GL.ARB_direct_state_access then
                GL.CopyNamedBufferSubData(src, dst, srcOffset, dstOffset, size)
            else
                bindBuffers src dst (fun tSrc tDst ->
                    GL.CopyBufferSubData(tSrc, tDst, srcOffset, dstOffset, size)
                )

        static member NamedBufferStorage(buffer: int, size : nativeint, data : nativeint, flags: BufferStorageFlags) =
            if GL.ARB_direct_state_access then
                if GL.ARB_buffer_storage then
                    GL.NamedBufferStorage(buffer, size, data, flags)
                else
                    GL.NamedBufferData(buffer, size, data, BufferUsageHint.DynamicDraw)
            else
                bindBuffer buffer (fun t ->
                    GL.Dispatch.BufferStorage(t, size, data, flags)
                )

        static member MapNamedBuffer(buffer: int, access : BufferAccess) =
            if GL.ARB_direct_state_access then
                GL.MapNamedBuffer(buffer, access)
            else
                bindBuffer buffer (fun t ->
                    GL.MapBuffer(t, access)
                )

        static member UnmapNamedBuffer(buffer: int) =
            if GL.ARB_direct_state_access then
                GL.UnmapNamedBuffer(buffer)
            else
                bindBuffer buffer (fun t ->
                    GL.UnmapBuffer(t)
                )

        static member MapNamedBufferRange(buffer: int, offset : nativeint, size : nativeint, access : BufferAccessMask) =
            if GL.ARB_direct_state_access then
                GL.MapNamedBufferRange(buffer, offset, size, access)
            else
                bindBuffer buffer (fun t ->
                    GL.MapBufferRange(t, offset, size, access)
                )

        static member FlushMappedNamedBufferRange(buffer: int, offset : nativeint, size : nativeint) =
            if GL.ARB_direct_state_access then
                GL.FlushMappedNamedBufferRange(buffer, offset, size)
            else
                bindBuffer buffer (fun t ->
                    GL.FlushMappedBufferRange(t, offset, size)
                )

        static member GetNamedBufferParameter(buffer : int, pname : BufferParameterName, arr : int[]) =
            if GL.ARB_direct_state_access then
                GL.GetNamedBufferParameter(buffer, pname, arr)
            else
                bindBuffer buffer (fun t ->
                    GL.GetBufferParameter(t, pname, arr)
                )

        static member GetNamedBufferParameter(buffer : int, pname : BufferParameterName, res : byref<int>) =
            if GL.ARB_direct_state_access then
                GL.GetNamedBufferParameter(buffer, pname, &res)
            else
                let mutable r = res
                bindBuffer buffer (fun t ->
                    GL.GetBufferParameter(t, pname, &r)
                )
                res <- r

        static member GetNamedBufferParameter(buffer : int, pname : BufferParameterName, res : byref<int64>) =
            if GL.ARB_direct_state_access then
                GL.GetNamedBufferParameter(buffer, pname, &&res)
            else
                let mutable r = res
                bindBuffer buffer (fun t ->
                    GL.GetBufferParameter(t, pname, &r)
                )
                res <- r