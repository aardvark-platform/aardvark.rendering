namespace Aardvark.Rendering.GL

open Aardvark.Base
open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4
open Microsoft.FSharp.NativeInterop
open Aardvark.Rendering.GL

#nowarn "9"

[<RequireQualifiedAccess>]
type internal PixelBuffer =
    | Host   of nativeptr<byte>
    | Native of BufferTarget

    member x.Pixels =
        match x with
        | Host ptr -> NativePtr.toNativeInt ptr
        | Native _ -> 0n

module internal PixelBuffer =

    let private usingNative (target : BufferTarget) (flags : BufferStorageFlags) (sizeInBytes : nativeint) (f : PixelBuffer -> 'T) =
        let pbo = GL.GenBuffer()
        GL.BindBuffer(target, pbo)
        GL.Check "could not bind PBO"

        GL.BufferStorage(target, sizeInBytes, 0n, flags)
        GL.Check "could not initialize PBO"

        try
            f (PixelBuffer.Native target)
        finally
            GL.BindBuffer(target, 0)
            GL.Check "could not unbind PBO"

            GL.DeleteBuffer(pbo)
            GL.Check "could not delete PBO"

    let private usingHost (sizeInBytes : nativeint) (f : PixelBuffer -> 'T) =
        let ptr = NativePtr.alloc<byte> (int sizeInBytes)

        try
            f (PixelBuffer.Host ptr)
        finally
            NativePtr.free ptr

    let using (target : BufferTarget) (flags : BufferStorageFlags) (sizeInBytes : nativeint) (f : PixelBuffer -> 'T) =
        if Config.UsePixelBufferObjects then usingNative target flags sizeInBytes f
        else usingHost sizeInBytes f

    let pack (sizeInBytes : nativeint) (f : PixelBuffer -> 'T) =
        using BufferTarget.PixelPackBuffer BufferStorageFlags.MapReadBit sizeInBytes f

    let unpack (sizeInBytes : nativeint) (f : PixelBuffer -> 'T) =
        using BufferTarget.PixelUnpackBuffer BufferStorageFlags.MapWriteBit sizeInBytes f

    let mapped (access : BufferAccess) (f : nativeint -> 'T) = function
        | PixelBuffer.Native target ->
            let dst = GL.MapBuffer(target, access)
            GL.Check "could not map PBO"

            try
                f dst
            finally
                let worked = GL.UnmapBuffer(target)
                if not worked then Log.warn "[GL] could not unmap buffer"
                GL.Check "could not unmap PBO"

        | PixelBuffer.Host ptr ->
            ptr |> NativePtr.toNativeInt |> f