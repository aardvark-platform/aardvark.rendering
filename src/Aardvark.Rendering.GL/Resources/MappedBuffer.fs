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

#nowarn "9"
#nowarn "51"


module ResizeBuffers =

    type ResizeBuffer(ctx : Context, handle : int) =
        inherit Buffer(ctx, 0n, handle)

        member x.Resize(newCapacity : nativeint) =
            let newCapactiy = Fun.NextPowerOfTwo(int64 newCapacity) |> nativeint
            if newCapacity <> x.SizeInBytes then
                using ctx.ResourceLock (fun _ -> 
                    let copyBytes = min newCapacity x.SizeInBytes

                    GL.BindBuffer(BufferTarget.CopyReadBuffer, x.Handle)
                    if copyBytes <> 0n then
                        let tmpBuffer = GL.GenBuffer()
                        GL.BindBuffer(BufferTarget.CopyWriteBuffer, tmpBuffer)

                        GL.BufferData(BufferTarget.CopyWriteBuffer, copyBytes, 0n, BufferUsageHint.StaticCopy)
                        GL.CopyBufferSubData(BufferTarget.CopyReadBuffer, BufferTarget.CopyWriteBuffer, 0n, 0n, copyBytes)

                        GL.BufferData(BufferTarget.CopyReadBuffer, newCapacity, 0n, BufferUsageHint.StreamDraw)
                        GL.CopyBufferSubData(BufferTarget.CopyWriteBuffer, BufferTarget.CopyReadBuffer, 0n, 0n, copyBytes)

                        GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
                        GL.DeleteBuffer(tmpBuffer)
                    else
                        GL.BufferData(BufferTarget.CopyReadBuffer, newCapacity, 0n, BufferUsageHint.StreamDraw)

                    GL.BindBuffer(BufferTarget.CopyReadBuffer,  0)

                    x.SizeInBytes <- newCapacity

                )

        member x.UseWrite(offset : nativeint, size : nativeint, f : nativeint -> 'a) =
            if offset < 0n then failwith "offset < 0n"
            if size < 0n then failwith "negative size"
            if size + offset > x.SizeInBytes then failwith "insufficient buffer size"

            if size = 0n then
                f 0n
            else
                let data = Marshal.AllocHGlobal size

                using ctx.ResourceLock (fun _ ->
                    GL.BindBuffer(BufferTarget.CopyWriteBuffer, x.Handle)
                    GL.Check "[ResizeableBuffer] could not bind buffer"

                    let res = f data

                    GL.BufferSubData(BufferTarget.CopyWriteBuffer, offset, size, data)
                    GL.Check "[ResizeableBuffer] could not upload buffer"

                    GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
                    GL.Check "[ResizeableBuffer] could not unbind buffer"
                    
                    Marshal.FreeHGlobal data
                    res
                )

        member x.UseRead(offset : nativeint, size : nativeint, f : nativeint -> 'a) =
            if offset < 0n then failwith "offset < 0n"
            if size < 0n then failwith "negative size"
            if size + offset > x.SizeInBytes then failwith "insufficient buffer size"

            if size = 0n then
                f 0n
            else
                let data = Marshal.AllocHGlobal size

                using ctx.ResourceLock (fun _ ->
                    GL.BindBuffer(BufferTarget.CopyWriteBuffer, x.Handle)
                    GL.Check "[ResizeableBuffer] could not bind buffer"

                    GL.GetBufferSubData(BufferTarget.CopyWriteBuffer, offset, size, data)
                    GL.Check "[ResizeableBuffer] could not download buffer"

                    let res = f data

                    GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
                    GL.Check "[ResizeableBuffer] could not unbind buffer"

                    Marshal.FreeHGlobal data
                    res
                )


module MappedBufferImplementations = 

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

        let resize (self : MappedBuffer) (newCapacity : nativeint) =
            if newCapacity <> buffer.SizeInBytes then
                let copySize = min buffer.SizeInBytes newCapacity

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


                if newCapacity > 0n then
                    GL.BufferStorage(BufferTarget.CopyWriteBuffer, newCapacity, 0n, BufferStorageFlags.MapPersistentBit ||| BufferStorageFlags.MapWriteBit ||| BufferStorageFlags.DynamicStorageBit ||| BufferStorageFlags.MapReadBit)
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
                            newCapacity, 
                            BufferAccessMask.MapPersistentBit ||| BufferAccessMask.MapWriteBit ||| BufferAccessMask.MapFlushExplicitBit ||| BufferAccessMask.MapReadBit
                        )
                    GL.Check "[MappedBuffer] could map buffer"
                else 
                    mappedPtr <- 0n

                buffer <- Buffer(ctx, newCapacity, newBuffer)

                GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
                GL.Check "[MappedBuffer] could unbind buffer"


                if oldBuffer <> 0 then
                    Interlocked.Change(&oldBuffers, fun o -> oldBuffer::o) |> ignore

                true
            else false

        member x.Write(sourcePtr : IntPtr, offset : nativeint, size : nativeint) =   
            resourceLocked (fun () -> 
                if size + offset > buffer.SizeInBytes then failwith "insufficient buffer size"
                Marshal.Copy(sourcePtr, mappedPtr + nativeint offset, size)

                using ctx.ResourceLock (fun _ ->
                    GL.BindBuffer(BufferTarget.CopyWriteBuffer, buffer.Handle)
                    GL.Check "[MappedBuffer] could bind buffer"

                    GL.FlushMappedBufferRange(BufferTarget.CopyWriteBuffer, offset, size)
                    GL.Check "[MappedBuffer] could flush buffer"

                    GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
                    GL.Check "[MappedBuffer] could unbind buffer"
                )
            )

        member x.Read(targetPtr : IntPtr, offset : nativeint, size : nativeint) =   
            Marshal.Copy(mappedPtr + offset, targetPtr, size)

        member x.Capacity = buffer.SizeInBytes
        member x.Resize(newCapacity) =
            let shouldMark = 
                resourceLocked (fun () -> 
                    using ctx.ResourceLock (fun _ ->
                        resize x newCapacity
                    )
                )
            if shouldMark then transact (fun () -> x.MarkOutdated() )

        member x.UseWrite(offset : nativeint, size : nativeint, f : nativeint -> 'a) =
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

        member x.UseRead(offset : nativeint, size : nativeint, f : nativeint -> 'a) =
            if size + offset > buffer.SizeInBytes then failwith "insufficient buffer size"
            let res = f (mappedPtr + offset)
            res

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

        member x.Use(f : unit -> 'a) =
            resourceLocked f

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
            member x.UseRead(offset, size, f) = x.UseRead(offset, size, f)
            member x.UseWrite(offset, size, f) = x.UseWrite(offset, size, f)

        interface ILockedResource with
            member x.Use (f : unit -> 'a) = x.Use f
            member x.AddLock (r : RenderTaskLock) = x.AddLock r
            member x.RemoveLock (r : RenderTaskLock) = x.RemoveLock r

    type FakeMappedBuffer(ctx : Context) =
        inherit Mod.AbstractMod<IBuffer>()

        let locks = ReferenceCountingSet<RenderTaskLock>()

        let mutable oldBuffers : list<int> = []
        let mutable buffer = ResizeBuffers.ResizeBuffer(ctx, using ctx.ResourceLock (fun _ -> GL.GenBuffer()))
        let onDispose = new System.Reactive.Subjects.Subject<unit>()

        let resourceLocked f =
            let rec run (locks  : list<RenderTaskLock>) () =
                match locks with
                    | x::xs -> x.Update (run xs)
                    | [] -> f ()
            let locks = lock locks (fun () -> locks |> Seq.toList)
            run locks ()

        let resize (newCapacity : nativeint) =
            buffer.Resize(newCapacity)
            false

        member x.Write(sourcePtr : IntPtr, offset : nativeint, size : nativeint) = 
            resourceLocked (fun () -> 
                if size + offset > buffer.SizeInBytes then failwith "insufficient buffer size"

                using ctx.ResourceLock (fun _ ->
                    GL.BindBuffer(BufferTarget.CopyWriteBuffer, buffer.Handle)
                    GL.Check "[MappedBuffer] could bind buffer"

                    GL.BufferSubData(BufferTarget.CopyWriteBuffer, nativeint offset, nativeint size, sourcePtr)
                    GL.Check "[MappedBuffer] could upload buffer"

                    GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
                    GL.Check "[MappedBuffer] could unbind buffer"
                )
            )

        member x.Read(targetPtr : IntPtr, offset : nativeint, size : nativeint) =
            resourceLocked (fun () -> 
                if size + offset > buffer.SizeInBytes then failwith "insufficient buffer size"
                using ctx.ResourceLock (fun _ ->
                    GL.BindBuffer(BufferTarget.CopyWriteBuffer, buffer.Handle)
                    GL.Check "[MappedBuffer] could bind buffer"

                    GL.GetBufferSubData(BufferTarget.CopyWriteBuffer, nativeint offset, nativeint size, targetPtr)
                    GL.Check "[MappedBuffer] could download buffer"

                    GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
                    GL.Check "[MappedBuffer] could unbind buffer"
                )
            )

        member x.Capacity = buffer.SizeInBytes

        member x.Resize(newCapacity) =
            let shouldMark = 
                resourceLocked (fun () -> 
                    using ctx.ResourceLock (fun _ ->
                        resize newCapacity
                    )
                )
            if shouldMark then transact (fun () -> x.MarkOutdated() )

        member x.UseWrite(offset : nativeint, size : nativeint, f : nativeint -> 'a) =
            resourceLocked (fun () -> 
                if size + offset > buffer.SizeInBytes then failwith "insufficient buffer size"

                let data = Marshal.AllocHGlobal size

                using ctx.ResourceLock (fun _ ->
                    GL.BindBuffer(BufferTarget.CopyWriteBuffer, buffer.Handle)
                    GL.Check "[MappedBuffer] could not bind buffer"

                    let res = f data

                    GL.BufferSubData(BufferTarget.CopyWriteBuffer, offset, size, data)
                    GL.Check "[MappedBuffer] could not upload buffer"

                    GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
                    GL.Check "[MappedBuffer] could not unbind buffer"
                    
                    Marshal.FreeHGlobal data
                    res
                )
            )

        member x.UseRead(offset : nativeint, size : nativeint, f : nativeint -> 'a) =
            resourceLocked (fun () -> 
                if size + offset > buffer.SizeInBytes then failwith "insufficient buffer size"

                let data = Marshal.AllocHGlobal size

                using ctx.ResourceLock (fun _ ->
                    GL.BindBuffer(BufferTarget.CopyWriteBuffer, buffer.Handle)
                    GL.Check "[MappedBuffer] could not bind buffer"

                    GL.GetBufferSubData(BufferTarget.CopyWriteBuffer, offset, size, data)
                    GL.Check "[MappedBuffer] could not download buffer"

                    let res = f data

                    GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
                    GL.Check "[MappedBuffer] could not unbind buffer"

                    Marshal.FreeHGlobal data
                    res
                )
            )


        override x.Compute() =
            buffer :> IBuffer

        member x.Dispose() =
            if buffer.Handle <> 0 then
                using ctx.ResourceLock (fun _ ->
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

        member x.Use(f : unit -> 'a) =
            resourceLocked f

        interface IMappedBuffer with
            member x.Write(sourcePtr, offset, size) = x.Write(sourcePtr,offset,size)
            member x.Read(targetPtr, offset, size) = x.Read(targetPtr,offset,size)
            member x.Capacity = x.Capacity
            member x.Resize(newCapacity) = x.Resize(newCapacity) 
            member x.Dispose() = x.Dispose()
            member x.OnDispose = onDispose :> IObservable<_>
            member x.UseRead(offset, size, f) = x.UseRead(offset, size, f)
            member x.UseWrite(offset, size, f) = x.UseWrite(offset, size, f)

        interface ILockedResource with
            member x.Use (f : unit -> 'a) = x.Use f
            member x.AddLock (r : RenderTaskLock) = x.AddLock r
            member x.RemoveLock (r : RenderTaskLock) = x.RemoveLock r


[<AutoOpen>]
module ``MappedBuffer Context Extensions`` =
    type Context with
        member x.CreateMappedBuffer() =
            using x.ResourceLock (fun _ ->
                if ExecutionContext.bufferStorageSupported then new MappedBufferImplementations.MappedBuffer(x) :> IMappedBuffer
                else new MappedBufferImplementations.FakeMappedBuffer(x) :> IMappedBuffer
            )

type MappedIndirectBuffer(ctx : Context, indexed : bool) =
    inherit Mod.AbstractMod<IIndirectBuffer>()
    
    static let sd = sizeof<DrawCallInfo> |> nativeint
    let buffer = ctx.CreateMappedBuffer()

    let mutable capacity = 0
    let count : nativeptr<int> = NativePtr.alloc 1

    let convert =
        if indexed then
            fun (info : DrawCallInfo) ->
                DrawCallInfo(
                    FaceVertexCount = info.FaceVertexCount,
                    InstanceCount = info.InstanceCount,
                    FirstIndex = info.FirstIndex,
                    BaseVertex = info.FirstInstance,
                    FirstInstance = info.BaseVertex
                )
        else id

    member x.Dispose() =
        buffer.Dispose()
        NativePtr.free count
        capacity <- 0


    member x.Resize(cap : int) =
        buffer.Resize(nativeint cap * sd)

    member x.Capacity = 
        buffer.Capacity / sd |> int

    member x.Count
        with get() = NativePtr.read count
        and set c = NativePtr.write count c

    member x.Item
        with get (i : int) = 
            let mutable info = DrawCallInfo()
            buffer.Read(NativePtr.toNativeInt &&info, nativeint i * sd, sd)
            convert info

        and set (i : int) (info : DrawCallInfo) =
            let info = convert info
            let gc = GCHandle.Alloc(info, GCHandleType.Pinned)
            try
                buffer.Write(gc.AddrOfPinnedObject(), nativeint i * sd, sd)
            finally
                gc.Free()
    override x.Compute() =
        let inner = buffer.GetValue(x) |> unbox<Buffer>
        IndirectBuffer(inner, count, 20, indexed) :> IIndirectBuffer

    interface ILockedResource with
        member x.Use f = buffer.Use f
        member x.AddLock l = buffer.AddLock l
        member x.RemoveLock l = buffer.RemoveLock l

    interface IMappedIndirectBuffer with
        member x.Dispose() = x.Dispose()
        member x.Indexed = indexed
        member x.Capacity = x.Capacity
        member x.Count
            with get() = x.Count
            and set c = x.Count <- c

        member x.Item
            with get i = x.[i]
            and set i v = x.[i] <- v

        member x.Resize c = x.Resize c


[<AutoOpen>]
module ``MappedIndirectBuffer Context Extensions`` =
    type Context with
        member x.CreateMappedIndirectBuffer(indexed : bool) =
            new MappedIndirectBuffer(x, indexed) :> IMappedIndirectBuffer


