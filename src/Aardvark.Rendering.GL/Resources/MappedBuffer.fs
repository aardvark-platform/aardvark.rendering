namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Collections.Concurrent
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Incremental
open OpenTK
open OpenTK.Platform
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Microsoft.FSharp.NativeInterop
open Aardvark.Rendering.GL


type MappedBuffer(ctx : Context) =
    inherit Mod.AbstractMod<IBuffer>()

    let mutable buffer = Buffer(ctx, 0n, 0)
    let mutable mappedPtr = 0n

    let unmap () =
        GL.BindBuffer(BufferTarget.CopyWriteBuffer, buffer.Handle)
        GL.UnmapBuffer(BufferTarget.CopyWriteBuffer) |> ignore
        GL.BindBuffer(BufferTarget.CopyReadBuffer,0)

    let resize (self : MappedBuffer) (newCapacity : int) =
        if nativeint newCapacity <> buffer.SizeInBytes then
            let copySize = min buffer.SizeInBytes (nativeint newCapacity)

            let newBuffer = GL.GenBuffer()
            
            GL.BindBuffer(BufferTarget.CopyReadBuffer, buffer.Handle)
            GL.UnmapBuffer(BufferTarget.CopyReadBuffer) |> ignore

            GL.BindBuffer(BufferTarget.CopyWriteBuffer, newBuffer)
            GL.BufferStorage(BufferTarget.CopyWriteBuffer, nativeint newCapacity, 0n, BufferStorageFlags.MapPersistentBit ||| BufferStorageFlags.MapWriteBit)

            GL.CopyBufferSubData(BufferTarget.CopyReadBuffer, BufferTarget.CopyWriteBuffer, 0n, 0n, copySize)

            GL.BindBuffer(BufferTarget.CopyReadBuffer, 0)
            GL.DeleteBuffer(buffer.Handle)

            buffer.Handle <- newBuffer
            buffer.SizeInBytes <- nativeint newCapacity

            mappedPtr <-
                GL.MapBufferRange(
                    BufferTarget.CopyWriteBuffer, 
                    0n, 
                    nativeint newCapacity, 
                    BufferAccessMask.MapPersistentBit ||| BufferAccessMask.MapWriteBit 
                )

            GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)

            transact (fun () -> self.MarkOutdated())

    member x.Write(sourcePtr, offset, size) =   
        using ctx.ResourceLock (fun _ ->
            if size + offset > int buffer.SizeInBytes then failwith "insufficient buffer size"
            Marshal.Copy(sourcePtr, mappedPtr + nativeint offset, size)
            GL.BindBuffer(BufferTarget.CopyWriteBuffer, buffer.Handle)
            GL.FlushMappedBufferRange(BufferTarget.CopyWriteBuffer, nativeint offset, nativeint size)
            GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
        )

    member x.Read(targetPtr, offset, size) =   
        Marshal.Copy(mappedPtr + nativeint offset, targetPtr, size)

    member x.Capacity = int buffer.SizeInBytes
    member x.Resize(newCapacity) =
        using ctx.ResourceLock (fun _ ->
            resize x newCapacity
        )

    override x.Compute() =
        buffer :> IBuffer

    interface IMappedBuffer with
        member x.Write(sourcePtr, offset, size) = x.Write(sourcePtr,offset,size)
        member x.Read(targetPtr, offset, size) = x.Read(targetPtr,offset,size)
        member x.Capacity = x.Capacity
        member x.Resize(newCapacity) = x.Resize(newCapacity)
        member x.Dispose() =
            using ctx.ResourceLock (fun _ ->
                 unmap ()
                 ctx.Delete buffer
            )