namespace Aardvark.Rendering.GL

open Aardvark.Base
open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4
open Microsoft.FSharp.NativeInterop
open Aardvark.Rendering.GL

#nowarn "9"

[<RequireQualifiedAccess>]
type internal PixelUnpackBuffer =
    | Native of int
    | Host of nativeptr<byte>

module internal PixelUnpackBuffer =

    let private createNative (usage : OpenTK.Graphics.OpenGL4.BufferUsageHint) (sizeInBytes : nativeint) =
        let pbo = GL.GenBuffer()
        GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pbo)
        GL.Check "could not bind PBO"

        GL.BufferData(BufferTarget.PixelUnpackBuffer, sizeInBytes, 0n, usage)
        GL.Check "could not initialize PBO"

        PixelUnpackBuffer.Native pbo

    let private createHost (sizeInBytes : nativeint) =
        PixelUnpackBuffer.Host <| NativePtr.alloc<byte> (int sizeInBytes)

    let create (usage : OpenTK.Graphics.OpenGL4.BufferUsageHint) (sizeInBytes : nativeint) =
        if Config.UsePixelUnpackBuffers then createNative usage sizeInBytes
        else createHost sizeInBytes

    let map (access : BufferAccess) = function
        | PixelUnpackBuffer.Native _ ->
            let dst = GL.MapBuffer(BufferTarget.PixelUnpackBuffer, access)
            GL.Check "could not map PBO"
            dst

        | PixelUnpackBuffer.Host ptr ->
            NativePtr.toNativeInt ptr

    let unmap = function
        | PixelUnpackBuffer.Native _ ->
            let worked = GL.UnmapBuffer(BufferTarget.PixelUnpackBuffer)
            if not worked then Log.warn "[GL] could not unmap buffer"
            GL.Check "could not unmap PBO"

            0n

        | PixelUnpackBuffer.Host ptr ->
            NativePtr.toNativeInt ptr

    let free = function
        | PixelUnpackBuffer.Native pbo ->
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0)
            GL.Check "could not unbind PBO"

            GL.DeleteBuffer(pbo)
            GL.Check "could not delete PBO"

        | PixelUnpackBuffer.Host ptr ->
            NativePtr.free ptr