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

    let locks = ReferenceCountingSet<RenderTaskLock>()

    let mutable buffer = Buffer(ctx, 0n, 0)
    let mutable mappedPtr = 0n
    let onDispose = new System.Reactive.Subjects.Subject<unit>()

    let mutable oldBuffers : int list = []

    let resourceLocked f =
        let rec run (locks  : list<RenderTaskLock>) () =
            match locks with
                | x::xs -> x.Update (run xs)
                | [] -> f ()
        let locks = lock locks (fun () -> locks |> Seq.toList)
        run locks ()

    let unmap () =
        GL.BindBuffer(BufferTarget.CopyWriteBuffer, buffer.Handle)
        GL.Check "[MappedBuffer] could bind buffer"
        GL.UnmapBuffer(BufferTarget.CopyWriteBuffer) |> ignore
        GL.Check "[MappedBuffer] could unmap buffer"
        GL.BindBuffer(BufferTarget.CopyReadBuffer,0)
        GL.Check "[MappedBuffer] could unbind buffer"
        mappedPtr <- 0n

    let deleteOldBuffers () =
        let delete = Interlocked.Exchange(&oldBuffers, [])
        if not (List.isEmpty delete) then
            using ctx.ResourceLock (fun _ ->
                for d in delete do 
                    GL.DeleteBuffer(d) 
                    GL.Check "[MappedBuffer] could delete old buffer"
            )

    let resize (self : MappedBuffer) (newCapacity : int) =
        if nativeint newCapacity <> buffer.SizeInBytes then
            let copySize = min buffer.SizeInBytes (nativeint newCapacity)

            let oldBuffer = buffer.Handle
            let newBuffer = GL.GenBuffer()
            GL.Check "[MappedBuffer] could not create buffer"
            
            if buffer.Handle <> 0 then
                GL.BindBuffer(BufferTarget.CopyReadBuffer, buffer.Handle)
                GL.Check "[MappedBuffer] could not bind old buffer"

                if mappedPtr <> 0n then // if buffer was empty, we did not map the buffer
                    GL.UnmapBuffer(BufferTarget.CopyReadBuffer) |> ignore
                    GL.Check "[MappedBuffer] could not unmap buffer"

                mappedPtr <- 0n

            GL.BindBuffer(BufferTarget.CopyWriteBuffer, newBuffer)
            GL.Check "[MappedBuffer] could not bind new buffer"

            if newCapacity > 0 then
                GL.BufferStorage(BufferTarget.CopyWriteBuffer, nativeint newCapacity, 0n, BufferStorageFlags.MapPersistentBit ||| BufferStorageFlags.MapWriteBit ||| BufferStorageFlags.DynamicStorageBit)
                GL.Check "[MappedBuffer] could not set buffer storage"

                if oldBuffer <> 0 then
                    if copySize > 0n then
                        GL.CopyBufferSubData(BufferTarget.CopyReadBuffer, BufferTarget.CopyWriteBuffer, 0n, 0n, copySize)
                        GL.Check "[MappedBuffer] could not copy buffer"

                    GL.BindBuffer(BufferTarget.CopyReadBuffer, 0)
                    GL.Check "[MappedBuffer] could unbind old buffer"

                mappedPtr <-
                    GL.MapBufferRange(
                        BufferTarget.CopyWriteBuffer, 
                        0n, 
                        nativeint newCapacity, 
                        BufferAccessMask.MapPersistentBit ||| BufferAccessMask.MapWriteBit ||| BufferAccessMask.MapFlushExplicitBit
                    )
                GL.Check "[MappedBuffer] could map buffer"
            else 
                mappedPtr <- 0n

            buffer <- Buffer(ctx, nativeint newCapacity, newBuffer)

            GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
            GL.Check "[MappedBuffer] could unbind buffer"


            if oldBuffer <> 0 then
                Interlocked.Change(&oldBuffers, fun o -> oldBuffer::o) |> ignore

            true
        else false

    member x.Write(sourcePtr, offset, size) =   
        resourceLocked (fun () -> 
            if size + offset > int buffer.SizeInBytes then failwith "insufficient buffer size"
            Marshal.Copy(sourcePtr, mappedPtr + nativeint offset, size)

            using ctx.ResourceLock (fun _ ->
                GL.BindBuffer(BufferTarget.CopyWriteBuffer, buffer.Handle)
                GL.Check "[MappedBuffer] could bind buffer"

                GL.FlushMappedBufferRange(BufferTarget.CopyWriteBuffer, nativeint offset, nativeint size)
                GL.Check "[MappedBuffer] could flush buffer"

                GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
                GL.Check "[MappedBuffer] could unbind buffer"
            )
        )

    member x.Read(targetPtr, offset, size) =   
        Marshal.Copy(mappedPtr + nativeint offset, targetPtr, size)

    member x.Capacity = int buffer.SizeInBytes
    member x.Resize(newCapacity) =
        let shouldMark = 
            resourceLocked (fun () -> 
                using ctx.ResourceLock (fun _ ->
                    resize x newCapacity
                )
            )
        if shouldMark then transact (fun () -> x.MarkOutdated() )

    member x.Use(offset : nativeint, size : nativeint, f : nativeint -> 'a) =
        resourceLocked (fun () -> 
            if size + offset > buffer.SizeInBytes then failwith "insufficient buffer size"
            let res = f (mappedPtr + offset)

            using ctx.ResourceLock (fun _ ->
                GL.BindBuffer(BufferTarget.CopyWriteBuffer, buffer.Handle)
                GL.Check "[MappedBuffer] could bind buffer"

                GL.FlushMappedBufferRange(BufferTarget.CopyWriteBuffer, offset, size)
                GL.Check "[MappedBuffer] could flush buffer"

                GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
                GL.Check "[MappedBuffer] could unbind buffer"
            )
            res
        )

    override x.Compute() =
        deleteOldBuffers()

        buffer :> IBuffer

    member x.Dispose() =
        if buffer.Handle <> 0 then
            using ctx.ResourceLock (fun _ ->
                unmap ()
                ctx.Delete buffer
            )
            onDispose.OnNext()
            onDispose.Dispose()

    member x.AddLock (r : RenderTaskLock) =
        lock locks (fun () ->
            locks.Add r |> ignore
        )

    member x.RemoveLock (r : RenderTaskLock) =
        lock locks (fun () ->
            locks.Remove r |> ignore
        )

    interface IMappedBuffer with
        member x.Write(sourcePtr, offset, size) = x.Write(sourcePtr,offset,size)
        member x.Read(targetPtr, offset, size) = x.Read(targetPtr,offset,size)
        member x.Capacity = x.Capacity
        member x.Resize(newCapacity) = x.Resize(newCapacity) 
        member x.Dispose() = x.Dispose()
        member x.OnDispose = onDispose :> IObservable<_>

    interface ILockedResource with
        member x.AddLock (r : RenderTaskLock) = x.AddLock r
        member x.RemoveLock (r : RenderTaskLock) = x.RemoveLock r


