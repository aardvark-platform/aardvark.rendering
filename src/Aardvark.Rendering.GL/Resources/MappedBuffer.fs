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

[<AutoOpen>]
module ResizeBufferImplementation =
    
    [<AutoOpen>]
    module private SparseBuffers = 
        type private BufferPageCommitmentDel = delegate of BufferTarget * nativeint * nativeint * bool -> unit

        let private lockObj = obj()
        let mutable private initialized = false
        let mutable private del : BufferPageCommitmentDel = null
        let mutable supported = false

        let init() =
            lock lockObj (fun () ->
                if not initialized then
                    initialized <- true
                    let handle = ContextHandle.Current |> Option.get
                    let ctx = handle.Handle |> unbox<IGraphicsContextInternal>
                    let ptr = ctx.GetAddress("glBufferPageCommitmentARB")
                    if ptr <> 0n then
                        supported <- true
                        del <- Marshal.GetDelegateForFunctionPointer(ptr, typeof<BufferPageCommitmentDel>) |> unbox
                    else
                        supported <- false
            )

        type GL with
            static member BufferPageCommitment(target : BufferTarget, offset : nativeint, size : nativeint, commit : bool) =
                del.Invoke(target, offset, size, commit)

        type BufferStorageFlags with
            static member inline SparseStorageBit = unbox<BufferStorageFlags> 0x0400

        type GetPName with
            static member inline BufferPageSize = unbox<GetPName> 0x82F8

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module private Alignment = 
        let prev (align : int64) (v : int64) =
            let r = v % align
            if r = 0L then v
            else v - r

        let next (align : int64) (v : int64) =
            let r = v % align
            if r = 0L then v
            else align + v - r

    [<AbstractClass>]
    type AbstractResizeBuffer(ctx : Context, handle : int, pageSize : int64) =
        inherit Buffer(ctx, 0n, handle)

        let lock = new ResourceLock()
        member x.Lock = lock

        interface ILockedResource with
            member x.Lock = lock
        
        abstract member Realloc : oldCapacity : nativeint * newCapacity : nativeint -> unit
        abstract member MapRead<'x> : offset : nativeint * size : nativeint * reader : (nativeint -> 'x) -> 'x
        abstract member MapWrite<'x> : offset : nativeint * size : nativeint * writer : (nativeint -> 'x) -> 'x

        member x.ResizeUnsafe(newCapacity : nativeint) =
            let newCapacity = Fun.NextPowerOfTwo(int64 newCapacity) |> Alignment.next pageSize |> nativeint
            let oldCapacity = x.SizeInBytes
            if oldCapacity <> newCapacity then
                using ctx.ResourceLock (fun _ ->
                    x.Realloc(oldCapacity, newCapacity)
                )
                x.SizeInBytes <- newCapacity

        member x.UseReadUnsafe(offset : nativeint, size : nativeint, reader : nativeint -> 'x) =
            using ctx.ResourceLock (fun _ ->
                x.MapRead(offset, size, reader)
            )

        member x.UseWriteUnsafe(offset : nativeint, size : nativeint, writer : nativeint -> 'x) =
            using ctx.ResourceLock (fun _ ->
                x.MapWrite(offset, size, writer)
            )


        member x.Resize(newCapacity : nativeint) =
            LockedResource.update x (fun () ->
                let newCapacity = Fun.NextPowerOfTwo(int64 newCapacity) |> Alignment.next pageSize |> nativeint
                let oldCapacity = x.SizeInBytes
                if oldCapacity <> newCapacity then
                    using ctx.ResourceLock (fun _ ->
                        let res = x.Realloc(oldCapacity, newCapacity)
                        //GL.Sync()
                        res
                    )
                    x.SizeInBytes <- newCapacity
            )

        member x.UseRead(offset : nativeint, size : nativeint, reader : nativeint -> 'x) =
            if size = 0n then
                reader 0n
            else
                LockedResource.access x (fun () ->
                    if offset < 0n then failwith "offset < 0n"
                    if size < 0n then failwith "negative size"
                    if size + offset > x.SizeInBytes then failwith "insufficient buffer size"

                    using ctx.ResourceLock (fun _ ->
                        let res = x.MapRead(offset, size, reader)
                        //GL.Sync()
                        res
                    )
                )

        member x.UseWrite(offset : nativeint, size : nativeint, writer : nativeint -> 'x) =
            if size = 0n then
                writer 0n
            else
                LockedResource.access x (fun () ->
                    if offset < 0n then failwith "offset < 0n"
                    if size < 0n then failwith "negative size"
                    if size + offset > x.SizeInBytes then failwith "insufficient buffer size"

                    using ctx.ResourceLock (fun _ ->
                        let res = x.MapWrite(offset, size, writer)
                        //GL.Sync()
                        res
                    )
                )

        interface IResizeBuffer with
            member x.Resize cap = x.Resize cap
            member x.UseRead(off, size, reader) = x.UseRead(off, size, reader)
            member x.UseWrite(off, size, reader) = x.UseRead(off, size, reader)

    type internal SparseMemoryResizeBuffer(ctx : Context, pageSize : int64, handle : int) =
        inherit AbstractResizeBuffer(ctx, handle, pageSize)

        override x.Realloc(oldCapacity : nativeint, newCapacity : nativeint) =
            GL.BindBuffer(BufferTarget.CopyWriteBuffer, handle)
            GL.Check "[ResizeableBuffer] could not bind buffer"

            if newCapacity > oldCapacity then
                GL.BufferPageCommitment(BufferTarget.CopyWriteBuffer, oldCapacity, newCapacity - oldCapacity, true)
                GL.Check "[ResizeableBuffer] could not commit pages"

            elif newCapacity < oldCapacity then
                GL.BufferPageCommitment(BufferTarget.CopyWriteBuffer, newCapacity, oldCapacity - newCapacity, false)
                GL.Check "[ResizeableBuffer] could not decommit pages"
                

            GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
            GL.Check "[ResizeableBuffer] could not unbind buffer"

        override x.MapWrite(offset : nativeint, size : nativeint, writer : nativeint -> 'a) =
            let temp = GL.GenBuffer()
            GL.BindBuffer(BufferTarget.CopyReadBuffer, temp)
            GL.Check "[ResizeableBuffer] could not bind buffer"

            GL.BufferStorage(BufferTarget.CopyReadBuffer, size, 0n, BufferStorageFlags.MapWriteBit)
            GL.Check "[ResizeableBuffer] could not allocate temp buffer"

            let ptr = GL.MapBufferRange(BufferTarget.CopyReadBuffer, 0n, size, BufferAccessMask.MapWriteBit ||| BufferAccessMask.MapInvalidateBufferBit)
            let res = writer ptr
            GL.UnmapBuffer(BufferTarget.CopyReadBuffer) |> ignore

            GL.BindBuffer(BufferTarget.CopyWriteBuffer, x.Handle)
            GL.Check "[ResizeableBuffer] could not bind buffer"

            GL.CopyBufferSubData(BufferTarget.CopyReadBuffer, BufferTarget.CopyWriteBuffer, 0n, offset, size)
            GL.Check "[ResizeableBuffer] could not copy buffer"

            GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
            GL.Check "[ResizeableBuffer] could not unbind buffer"
                    
            GL.BindBuffer(BufferTarget.CopyReadBuffer, 0)
            GL.Check "[ResizeableBuffer] could not unbind buffer"

            GL.DeleteBuffer(temp)
            GL.Check "[ResizeableBuffer] could not delete temp buffer"

            res

        override x.MapRead(offset : nativeint, size : nativeint, reader : nativeint -> 'a) =
            let temp = GL.GenBuffer()
            GL.BindBuffer(BufferTarget.CopyWriteBuffer, temp)
            GL.Check "[ResizeableBuffer] could not bind buffer"

            GL.BufferStorage(BufferTarget.CopyWriteBuffer, size, 0n, BufferStorageFlags.MapReadBit)
            GL.Check "[ResizeableBuffer] could not allocate temp buffer"

            GL.BindBuffer(BufferTarget.CopyReadBuffer, x.Handle)
            GL.Check "[ResizeableBuffer] could not bind buffer"

            GL.CopyBufferSubData(BufferTarget.CopyReadBuffer, BufferTarget.CopyWriteBuffer, offset, 0n, size)
            GL.Check "[ResizeableBuffer] could not copy buffer"

            GL.BindBuffer(BufferTarget.CopyReadBuffer, 0)
            GL.Check "[ResizeableBuffer] could not unbind buffer"

            let ptr = GL.MapBufferRange(BufferTarget.CopyWriteBuffer, 0n, size, BufferAccessMask.MapReadBit)
            let res = reader ptr
            GL.UnmapBuffer(BufferTarget.CopyWriteBuffer) |> ignore

            GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
            GL.Check "[ResizeableBuffer] could not unbind temp buffer"

            GL.DeleteBuffer(temp)
            GL.Check "[ResizeableBuffer] could not delete temp buffer"

            res

    type internal CopyResizeBuffer(ctx : Context, handle : int) =
        inherit AbstractResizeBuffer(ctx, handle, 1L)

        override x.Realloc(oldCapacity : nativeint, newCapacity : nativeint) =
            let copyBytes = min newCapacity oldCapacity

            GL.BindBuffer(BufferTarget.CopyReadBuffer, x.Handle)
            GL.Check "[ResizeableBuffer] could not bind buffer"

            if copyBytes <> 0n then
                let tmpBuffer = GL.GenBuffer()
                GL.BindBuffer(BufferTarget.CopyWriteBuffer, tmpBuffer)
                GL.Check "[ResizeableBuffer] could not bind buffer"

                GL.BufferData(BufferTarget.CopyWriteBuffer, copyBytes, 0n, BufferUsageHint.StaticCopy)
                GL.Check "[ResizeableBuffer] could not allocate buffer"

                GL.CopyBufferSubData(BufferTarget.CopyReadBuffer, BufferTarget.CopyWriteBuffer, 0n, 0n, copyBytes)
                GL.Check "[ResizeableBuffer] could not copy buffer"

                GL.BufferData(BufferTarget.CopyReadBuffer, newCapacity, 0n, BufferUsageHint.StreamDraw)
                GL.Check "[ResizeableBuffer] could not allocate buffer"

                GL.CopyBufferSubData(BufferTarget.CopyWriteBuffer, BufferTarget.CopyReadBuffer, 0n, 0n, copyBytes)
                GL.Check "[ResizeableBuffer] could not copy buffer"

                GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
                GL.Check "[ResizeableBuffer] could not unbind buffer"

                GL.DeleteBuffer(tmpBuffer)
                GL.Check "[ResizeableBuffer] could not delete buffer"

            else
                GL.BufferData(BufferTarget.CopyReadBuffer, newCapacity, 0n, BufferUsageHint.StreamDraw)
                GL.Check "[ResizeableBuffer] could not allocate buffer"

            GL.BindBuffer(BufferTarget.CopyReadBuffer,  0)
            GL.Check "[ResizeableBuffer] could not ubbind buffer"

        override x.MapWrite(offset : nativeint, size : nativeint, writer : nativeint -> 'a) =
            let data = Marshal.AllocHGlobal size
            let res = writer data

            GL.BindBuffer(BufferTarget.CopyWriteBuffer, x.Handle)
            GL.Check "[ResizeableBuffer] could not bind buffer"

            GL.BufferSubData(BufferTarget.CopyWriteBuffer, offset, size, data)
            GL.Check "[ResizeableBuffer] could not upload buffer"

            GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
            GL.Check "[ResizeableBuffer] could not unbind buffer"
                    
            Marshal.FreeHGlobal data
            res

        override x.MapRead(offset : nativeint, size : nativeint, reader : nativeint -> 'a) =
            let data = Marshal.AllocHGlobal size

            GL.BindBuffer(BufferTarget.CopyWriteBuffer, x.Handle)
            GL.Check "[ResizeableBuffer] could not bind buffer"

            GL.GetBufferSubData(BufferTarget.CopyWriteBuffer, offset, size, data)
            GL.Check "[ResizeableBuffer] could not download buffer"

            GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
            GL.Check "[ResizeableBuffer] could not unbind buffer"

            let res = reader data
            Marshal.FreeHGlobal data
            res


    type Context with
        member x.CreateResizeBuffer() =
            using x.ResourceLock (fun _ ->
                SparseBuffers.init()
                if not RuntimeConfig.SupressSparseBuffers && SparseBuffers.supported then
                    let pageSize = GL.GetInteger64(GetPName.BufferPageSize)

                    let buffer = GL.GenBuffer()
                    GL.BindBuffer(BufferTarget.CopyWriteBuffer, buffer)
                    GL.BufferStorage(
                        BufferTarget.CopyWriteBuffer, 
                        2n <<< 30, 0n, 
                        BufferStorageFlags.SparseStorageBit
                    )
                    GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)

                    SparseMemoryResizeBuffer(x, pageSize, buffer) :> AbstractResizeBuffer
                else
                    let buffer = GL.GenBuffer()
                    CopyResizeBuffer(x, buffer) :> AbstractResizeBuffer
            )


// =========================================================================
// Historical monsters


module MappedBufferImplementations = 
//
//    type MappedBuffer(ctx : Context) =
//        inherit Mod.AbstractMod<IBuffer>()
//
//        let locks = ReferenceCountingSet<RenderTaskLock>()
//
//        let mutable buffer = Buffer(ctx, 0n, 0)
//        let mutable mappedPtr = 0n
//        let onDispose = new System.Reactive.Subjects.Subject<unit>()
//
//        let mutable oldBuffers : int list = []
//
//        let resourceLocked f =
//            let rec run (locks  : list<RenderTaskLock>) () =
//                match locks with
//                    | x::xs -> x.Update (run xs)
//                    | [] -> f ()
//            let locks = lock locks (fun () -> locks |> Seq.toList)
//            run locks ()
//
//        let unmap () =
//            GL.BindBuffer(BufferTarget.CopyWriteBuffer, buffer.Handle)
//            GL.Check "[MappedBuffer] could bind buffer"
//            GL.UnmapBuffer(BufferTarget.CopyWriteBuffer) |> ignore
//            GL.Check "[MappedBuffer] could unmap buffer"
//            GL.BindBuffer(BufferTarget.CopyReadBuffer,0)
//            GL.Check "[MappedBuffer] could unbind buffer"
//            mappedPtr <- 0n
//
//        let deleteOldBuffers () =
//            let delete = Interlocked.Exchange(&oldBuffers, [])
//            if not (List.isEmpty delete) then
//                using ctx.ResourceLock (fun _ ->
//                    for d in delete do 
//                        GL.DeleteBuffer(d) 
//                        GL.Check "[MappedBuffer] could delete old buffer"
//                )
//
//        let resize (self : MappedBuffer) (newCapacity : nativeint) =
//            if newCapacity <> buffer.SizeInBytes then
//                let copySize = min buffer.SizeInBytes newCapacity
//
//                let oldBuffer = buffer.Handle
//                let newBuffer = GL.GenBuffer()
//                GL.Check "[MappedBuffer] could not create buffer"
//            
//                if buffer.Handle <> 0 then
//                    GL.BindBuffer(BufferTarget.CopyReadBuffer, buffer.Handle)
//                    GL.Check "[MappedBuffer] could not bind old buffer"
//
//                    if mappedPtr <> 0n then // if buffer was empty, we did not map the buffer
//                        GL.UnmapBuffer(BufferTarget.CopyReadBuffer) |> ignore
//                        GL.Check "[MappedBuffer] could not unmap buffer"
//
//                    mappedPtr <- 0n
//
//                GL.BindBuffer(BufferTarget.CopyWriteBuffer, newBuffer)
//                GL.Check "[MappedBuffer] could not bind new buffer"
//
//
//                if newCapacity > 0n then
//                    GL.BufferStorage(BufferTarget.CopyWriteBuffer, newCapacity, 0n, BufferStorageFlags.MapPersistentBit ||| BufferStorageFlags.MapWriteBit ||| BufferStorageFlags.DynamicStorageBit ||| BufferStorageFlags.MapReadBit)
//                    GL.Check "[MappedBuffer] could not set buffer storage"
//
//                    if oldBuffer <> 0 then
//                        if copySize > 0n then
//                            GL.CopyBufferSubData(BufferTarget.CopyReadBuffer, BufferTarget.CopyWriteBuffer, 0n, 0n, copySize)
//                            GL.Check "[MappedBuffer] could not copy buffer"
//
//                        GL.BindBuffer(BufferTarget.CopyReadBuffer, 0)
//                        GL.Check "[MappedBuffer] could unbind old buffer"
//
//                    mappedPtr <-
//                        GL.MapBufferRange(
//                            BufferTarget.CopyWriteBuffer, 
//                            0n, 
//                            newCapacity, 
//                            BufferAccessMask.MapPersistentBit ||| BufferAccessMask.MapWriteBit ||| BufferAccessMask.MapFlushExplicitBit ||| BufferAccessMask.MapReadBit
//                        )
//                    GL.Check "[MappedBuffer] could map buffer"
//                else 
//                    mappedPtr <- 0n
//
//                buffer <- Buffer(ctx, newCapacity, newBuffer)
//
//                GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
//                GL.Check "[MappedBuffer] could unbind buffer"
//
//
//                if oldBuffer <> 0 then
//                    Interlocked.Change(&oldBuffers, fun o -> oldBuffer::o) |> ignore
//
//                true
//            else false
//
//        member x.Write(sourcePtr : IntPtr, offset : nativeint, size : nativeint) =   
//            resourceLocked (fun () -> 
//                if size + offset > buffer.SizeInBytes then failwith "insufficient buffer size"
//                Marshal.Copy(sourcePtr, mappedPtr + nativeint offset, size)
//
//                using ctx.ResourceLock (fun _ ->
//                    GL.BindBuffer(BufferTarget.CopyWriteBuffer, buffer.Handle)
//                    GL.Check "[MappedBuffer] could bind buffer"
//
//                    GL.FlushMappedBufferRange(BufferTarget.CopyWriteBuffer, offset, size)
//                    GL.Check "[MappedBuffer] could flush buffer"
//
//                    GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
//                    GL.Check "[MappedBuffer] could unbind buffer"
//                )
//            )
//
//        member x.Read(targetPtr : IntPtr, offset : nativeint, size : nativeint) =   
//            Marshal.Copy(mappedPtr + offset, targetPtr, size)
//
//        member x.Capacity = buffer.SizeInBytes
//        member x.Resize(newCapacity) =
//            let shouldMark = 
//                resourceLocked (fun () -> 
//                    using ctx.ResourceLock (fun _ ->
//                        resize x newCapacity
//                    )
//                )
//            if shouldMark then transact (fun () -> x.MarkOutdated() )
//
//        member x.UseWrite(offset : nativeint, size : nativeint, f : nativeint -> 'a) =
//            resourceLocked (fun () -> 
//                if size + offset > buffer.SizeInBytes then failwith "insufficient buffer size"
//                let res = f (mappedPtr + offset)
//
//                using ctx.ResourceLock (fun _ ->
//                    GL.BindBuffer(BufferTarget.CopyWriteBuffer, buffer.Handle)
//                    GL.Check "[MappedBuffer] could bind buffer"
//
//                    GL.FlushMappedBufferRange(BufferTarget.CopyWriteBuffer, offset, size)
//                    GL.Check "[MappedBuffer] could flush buffer"
//
//                    GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
//                    GL.Check "[MappedBuffer] could unbind buffer"
//                )
//                res
//            )
//
//        member x.UseRead(offset : nativeint, size : nativeint, f : nativeint -> 'a) =
//            if size + offset > buffer.SizeInBytes then failwith "insufficient buffer size"
//            let res = f (mappedPtr + offset)
//            res
//
//        override x.Compute() =
//            deleteOldBuffers()
//
//            buffer :> IBuffer
//
//        member x.Dispose() =
//            if buffer.Handle <> 0 then
//                using ctx.ResourceLock (fun _ ->
//                    unmap ()
//                    ctx.Delete buffer
//                )
//                onDispose.OnNext()
//                onDispose.Dispose()
//
//        member x.Use(f : unit -> 'a) =
//            resourceLocked f
//
//        member x.AddLock (r : RenderTaskLock) =
//            lock locks (fun () ->
//                locks.Add r |> ignore
//            )
//
//        member x.RemoveLock (r : RenderTaskLock) =
//            lock locks (fun () ->
//                locks.Remove r |> ignore
//            )
//
//        interface IMappedBuffer with
//            member x.Write(sourcePtr, offset, size) = x.Write(sourcePtr,offset,size)
//            member x.Read(targetPtr, offset, size) = x.Read(targetPtr,offset,size)
//            member x.Capacity = x.Capacity
//            member x.Resize(newCapacity) = x.Resize(newCapacity) 
//            member x.Dispose() = x.Dispose()
//            member x.OnDispose = onDispose :> IObservable<_>
//            member x.UseRead(offset, size, f) = x.UseRead(offset, size, f)
//            member x.UseWrite(offset, size, f) = x.UseWrite(offset, size, f)
//
//        interface ILockedResource with
//            member x.Use (f : unit -> 'a) = x.Use f
//            member x.AddLock (r : RenderTaskLock) = x.AddLock r
//            member x.RemoveLock (r : RenderTaskLock) = x.RemoveLock r

    type FakeMappedBuffer(ctx : Context) =
        inherit Mod.AbstractMod<IBuffer>()
        let buffer = ctx.CreateResizeBuffer()
        let onDispose = new System.Reactive.Subjects.Subject<unit>()

        member x.Write(sourcePtr : IntPtr, offset : nativeint, size : nativeint) = 
            buffer.UseWrite(offset, size, fun ptr ->
                Marshal.Copy(sourcePtr, ptr, size)
            )

        member x.Read(targetPtr : IntPtr, offset : nativeint, size : nativeint) =
            buffer.UseWrite(offset, size, fun ptr ->
                Marshal.Copy(ptr, targetPtr, size)
            )

        member x.Capacity = buffer.SizeInBytes

        member x.Resize(newCapacity) =
            buffer.Resize(newCapacity)

        member x.UseWrite(offset : nativeint, size : nativeint, f : nativeint -> 'a) =
            buffer.UseWrite(offset, size, f)

        member x.UseRead(offset : nativeint, size : nativeint, f : nativeint -> 'a) =
            buffer.UseRead(offset, size, f)


        override x.Compute() =
            buffer :> IBuffer

        member x.Dispose() =
            if buffer.Handle <> 0 then
                using ctx.ResourceLock (fun _ ->
                    ctx.Delete buffer
                )
                onDispose.OnNext()
                onDispose.Dispose()

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
            member x.Lock = buffer.Lock

[<AutoOpen>]
module ``MappedBuffer Context Extensions`` =
    type Context with
        member x.CreateMappedBuffer() =
            using x.ResourceLock (fun _ ->
                new MappedBufferImplementations.FakeMappedBuffer(x) :> IMappedBuffer
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
        member x.Lock = buffer.Lock

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


