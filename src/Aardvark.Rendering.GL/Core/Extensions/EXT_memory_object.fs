namespace Aardvark.Rendering.GL

open System
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL

[<AutoOpen>]
module EXT_memory_object =

    type GL private() =
        static let supported = ExtensionHelpers.isSupported (Version(999,999)) "GL_EXT_memory_object"
        static member EXT_memory_object = supported

    type GL.Dispatch with

        static member CreateMemoryObject() =
            if GL.EXT_memory_object then
                let mutable h = 0
                GL.Ext.CreateMemoryObjects(1, &h)
                h
            else
                failwith "glCreateMemoryObjects is not available!"

        static member DeleteMemoryObject(memoryObject : int) =
            if GL.EXT_memory_object then
                GL.Ext.DeleteMemoryObject memoryObject
            else
                failwith "glDeleteMemoryObject is not available!"

        // 1D
        static member TexStorageMem1D(target : OpenGL4.TextureTarget, levels : int, internalFormat : OpenGL4.InternalFormat,
                                      width : int, memory : int, offset : int64) =
            if GL.EXT_memory_object then
                GL.Ext.TexStorageMem1D(unbox target, levels, unbox internalFormat, width, memory, offset)
            else
                failwith "glTexStorageMem1D is not available!"

        // 2D
        static member TexStorageMem2D(target : OpenGL4.TextureTarget, levels : int, internalFormat : OpenGL4.InternalFormat,
                                      width : int, height : int, memory : int, offset : int64) =
            if GL.EXT_memory_object then
                GL.Ext.TexStorageMem2D(unbox target, levels, unbox internalFormat, width, height, memory, offset)
            else
                failwith "glTexStorageMem2D is not available!"


        // 2D MS
        static member TexStorageMem2DMultisample(target : OpenGL4.TextureTarget, samples : int, internalFormat : OpenGL4.InternalFormat,
                                                 width : int, height : int, fixedSampleLocations : bool, memory : int, offset : int64) =
            if GL.EXT_memory_object then
                GL.Ext.TexStorageMem2DMultisample(unbox target, samples, unbox internalFormat, width, height, fixedSampleLocations, memory, offset)
            else
                failwith "glTexStorageMem2DMultisample is not available!"

        // 3D
        static member TexStorageMem3D(target : OpenGL4.TextureTarget, levels : int, internalFormat : OpenGL4.InternalFormat,
                                      width : int, height : int, depth : int, memory : int, offset : int64) =
            if GL.EXT_memory_object then
                GL.Ext.TexStorageMem3D(unbox target, levels, unbox internalFormat, width, height, depth, memory, offset)
            else
                failwith "glTexStorageMem3D is not available!"

        // 3D MS
        static member TexStorageMem3DMultisample(target : OpenGL4.TextureTarget, samples : int, internalFormat : OpenGL4.InternalFormat,
                                                 width : int, height : int, depth : int, fixedSampleLocations : bool, memory : int, offset : int64) =
            if GL.EXT_memory_object then
                GL.Ext.TexStorageMem3DMultisample(unbox target, samples, unbox internalFormat, width, height, depth, fixedSampleLocations, memory, offset)
            else
                failwith "glTexStorageMem3DMultisample is not available!"

[<AutoOpen>]
module EXT_memory_object_win32 =

    type GL private() =
        static let supported = ExtensionHelpers.isSupported (Version(999,999)) "GL_EXT_memory_object_win32"
        static member EXT_memory_object_win32 = supported

    type GL.Dispatch with

        static member ImportMemoryWin32Handle(memory : int, size : int64, handleType : ExternalHandleType, handle : nativeint) =
            if GL.EXT_memory_object_win32 then
                GL.Ext.ImportMemoryWin32Handle(memory, size, handleType, handle)
            else
                failwith "glImportMemoryWin32Handle is not available!"

[<AutoOpen>]
module EXT_memory_object_fd =

    type GL private() =
        static let supported = ExtensionHelpers.isSupported (Version(999,999)) "GL_EXT_memory_object_fd"
        static member EXT_memory_object_fd = supported

    type GL.Dispatch with

        static member ImportMemoryFd(memory : int, size : int64, handleType : ExternalHandleType, fd : int) =
            if GL.EXT_memory_object_fd then
                GL.Ext.ImportMemoryF(memory, size, handleType, fd)
            else
                failwith "glImportMemoryFd is not available!"