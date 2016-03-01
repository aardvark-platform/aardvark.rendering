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
        GL.Check "[MappedBuffer] could bind buffer"
        GL.UnmapBuffer(BufferTarget.CopyWriteBuffer) |> ignore
        GL.Check "[MappedBuffer] could unmap buffer"
        GL.BindBuffer(BufferTarget.CopyReadBuffer,0)
        GL.Check "[MappedBuffer] could unbind buffer"
        mappedPtr <- 0n

    let resize (self : MappedBuffer) (newCapacity : int) =
        if nativeint newCapacity <> buffer.SizeInBytes then
            let copySize = min buffer.SizeInBytes (nativeint newCapacity)

            let newBuffer = GL.GenBuffer()
            GL.Check "[MappedBuffer] could not create buffer"
            
            if buffer.Handle <> 0 then
                GL.BindBuffer(BufferTarget.CopyReadBuffer, buffer.Handle)
                GL.Check "[MappedBuffer] could not bind old buffer"

                GL.UnmapBuffer(BufferTarget.CopyReadBuffer) |> ignore
                GL.Check "[MappedBuffer] could not unmap buffer"

                mappedPtr <- 0n

            GL.BindBuffer(BufferTarget.CopyWriteBuffer, newBuffer)
            GL.Check "[MappedBuffer] could not bind new buffer"

            GL.BufferStorage(BufferTarget.CopyWriteBuffer, nativeint newCapacity, 0n, BufferStorageFlags.MapPersistentBit ||| BufferStorageFlags.MapWriteBit ||| BufferStorageFlags.DynamicStorageBit)
            GL.Check "[MappedBuffer] could not set buffer storage"

            if buffer.Handle <> 0 then
                GL.CopyBufferSubData(BufferTarget.CopyReadBuffer, BufferTarget.CopyWriteBuffer, 0n, 0n, copySize)
                GL.Check "[MappedBuffer] could not copy buffer"

                GL.BindBuffer(BufferTarget.CopyReadBuffer, 0)
                GL.Check "[MappedBuffer] could unbind old buffer"

                GL.DeleteBuffer(buffer.Handle)
                GL.Check "[MappedBuffer] could delete old buffer"

            buffer <- Buffer(ctx, nativeint newCapacity, newBuffer)

            mappedPtr <-
                GL.MapBufferRange(
                    BufferTarget.CopyWriteBuffer, 
                    0n, 
                    nativeint newCapacity, 
                    BufferAccessMask.MapPersistentBit ||| BufferAccessMask.MapWriteBit ||| BufferAccessMask.MapFlushExplicitBit
                )
            GL.Check "[MappedBuffer] could map buffer"

            GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
            GL.Check "[MappedBuffer] could unbind buffer"

            transact (fun () -> self.MarkOutdated())

    member x.Write(sourcePtr, offset, size) =   
        using ctx.ResourceLock (fun _ ->
            if size + offset > int buffer.SizeInBytes then failwith "insufficient buffer size"
            Marshal.Copy(sourcePtr, mappedPtr + nativeint offset, size)

            GL.BindBuffer(BufferTarget.CopyWriteBuffer, buffer.Handle)
            GL.Check "[MappedBuffer] could bind buffer"

            GL.FlushMappedBufferRange(BufferTarget.CopyWriteBuffer, nativeint offset, nativeint size)
            GL.Check "[MappedBuffer] could flush buffer"

            GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
            GL.Check "[MappedBuffer] could unbind buffer"
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