namespace Aardvark.Rendering.GL

#nowarn "9"
#nowarn "51"

open System
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL
open OpenTK.Graphics.OpenGL4
open Microsoft.FSharp.NativeInterop
open Aardvark.Base

open ExtensionHelpers
open Aardvark.Rendering.GL

[<AutoOpen>]
module EXT_direct_state_access =
    type GL private() =

        static let supported = ExtensionHelpers.isSupported (Version(4,5,0)) "GL_EXT_direct_state_access"

        static member EXT_direct_state_access = supported

        static member NamedBufferData(buffer : int, size : nativeint, data : nativeint, usage : OpenTK.Graphics.OpenGL4.BufferUsageHint) =
            if supported then
                GL.Ext.NamedBufferData(buffer, size, data, usage)
            else
                bindBuffer buffer (fun t ->
                    GL.BufferData(t, size, data, usage)
                )

        static member NamedBufferSubData(buffer : int, offset : nativeint, size : nativeint, data : nativeint) =
            if supported then
                GL.Ext.NamedBufferSubData(buffer, offset, size, data)
            else
                bindBuffer buffer (fun t ->
                    GL.BufferSubData(t, offset, size, data)
                )

        static member NamedClearBufferSubData(buffer : int, ifmt : PixelInternalFormat, offset : nativeint, size : nativeint, fmt : PixelFormat, pixelType : PixelType, data : nativeint) =
//            if supported then
//                let ifmt = unbox<ExtDirectStateAccess> (int ifmt)
//                GL.Ext.ClearNamedBufferSubData(buffer, ifmt, fmt, pixelType, offset, size, data)
//            else
                bindBuffer buffer (fun t ->
                    GL.ClearBufferSubData(t, ifmt, offset, int size, fmt, unbox<PixelType> (int pixelType), data)
                )

        static member GetNamedBufferSubData(buffer : int, offset : nativeint, size : nativeint, data : nativeint) =
            if supported then
                GL.Ext.GetNamedBufferSubData(buffer, offset, size, data)
            else
                bindBuffer buffer (fun t ->
                    GL.GetBufferSubData(t, offset, size, data)
                )

        static member CopyNamedBufferSubData(src : int, srcOffset : nativeint, dst : int, dstOffset : nativeint, size : nativeint) =
            if supported then
                GL.Ext.NamedCopyBufferSubData(src, dst, srcOffset, dstOffset, size)
            else
                bindBuffers src dst (fun tSrc tDst ->
                    GL.CopyBufferSubData(tSrc, tDst, srcOffset, dstOffset, size)
                )


        static member NamedBufferStorage(buffer: int, size : nativeint, data : nativeint, flags: BufferStorageFlags) =
            if supported then
                GL.Ext.NamedBufferStorage(buffer, size, data, flags)
            else
                bindBuffer buffer (fun t ->
                    GL.BufferStorage(t, size, data, flags)
                )

        static member MapNamedBuffer(buffer: int, access : OpenTK.Graphics.OpenGL4.BufferAccess) =
            if supported then
                GL.Ext.MapNamedBuffer(buffer, unbox<BufferAccess> (int access))
            else
                bindBuffer buffer (fun t ->
                    GL.MapBuffer(t, access)
                )

        static member UnmapNamedBuffer(buffer: int) =
            if supported then
                GL.Ext.UnmapNamedBuffer(buffer)
            else
                bindBuffer buffer (fun t ->
                    GL.UnmapBuffer(t)
                )
                
        static member MapNamedBufferRange(buffer: int, offset : nativeint, size : nativeint, access : BufferAccessMask) =
            if supported then
                GL.Ext.MapNamedBufferRange(buffer, offset, size, unbox (int access))
            else
                bindBuffer buffer (fun t ->
                    GL.MapBufferRange(t, offset, size, access)
                )
                
        static member FlushMappedNamedBufferRange(buffer: int, offset : nativeint, size : nativeint) =
            if supported then
                GL.Ext.FlushMappedNamedBufferRange(buffer, offset, size)
            else
                bindBuffer buffer (fun t ->
                    GL.FlushMappedBufferRange(t, offset, size)
                )

        static member GetNamedBufferParameter(buffer : int, pname : BufferParameterName, arr : int[]) =
            if supported then
                GL.Ext.GetNamedBufferParameter(buffer, unbox<BufferParameterName> (int pname), arr)
            else
                bindBuffer buffer (fun t ->
                    GL.GetBufferParameter(t, pname, arr)
                )
        static member GetNamedBufferParameter(buffer : int, pname : BufferParameterName, res : byref<int>) =
            if supported then
                GL.Ext.GetNamedBufferParameter(buffer, unbox<BufferParameterName> (int pname), &res)
            else
                let mutable r = res
                bindBuffer buffer (fun t ->
                    GL.GetBufferParameter(t, pname, &r)
                )
                res <- r
        static member GetNamedBufferParameter(buffer : int, pname : BufferParameterName, res : byref<int64>) =
            if supported then
                GL.Ext.GetNamedBufferParameter(buffer, unbox<BufferParameterName> (int pname), NativePtr.cast &&res)
            else
                let mutable r = res
                bindBuffer buffer (fun t ->
                    GL.GetBufferParameter(t, pname, &r)
                )
                res <- r

        static member NamedCopyBufferSubData(readBuffer : int, writeBuffer : int, readOffset : nativeint, writeOffset : nativeint, size : nativeint) =
            if supported then
                GL.Ext.NamedCopyBufferSubData(readBuffer, writeBuffer, readOffset, writeOffset, size)
            else
                bindBuffers readBuffer writeBuffer (fun t0 t1 ->
                    GL.CopyBufferSubData(t0, t1, readOffset, writeOffset, size)
                )