namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4

#nowarn "9"

[<AutoOpen>]
module internal BufferResourceCounts =

    module ResourceCounts =

        let addBuffer (ctx:Context) size =
            Interlocked.Increment(&ctx.MemoryUsage.BufferCount) |> ignore
            Interlocked.Add(&ctx.MemoryUsage.BufferMemory,size) |> ignore

        let removeBuffer (ctx:Context) size =
            Interlocked.Decrement(&ctx.MemoryUsage.BufferCount)  |> ignore
            Interlocked.Add(&ctx.MemoryUsage.BufferMemory,-size) |> ignore

        let updateBuffer (ctx:Context) oldSize newSize =
            Interlocked.Add(&ctx.MemoryUsage.BufferMemory, newSize-oldSize) |> ignore

/// <summary>
/// Buffer simply wraps an OpenGL buffer object and
/// maintains its creation-context, etc.
/// </summary>
type Buffer =
    class
        val mutable public Handle : int
        val mutable public Context : Context
        val mutable public SizeInBytes : nativeint
        val mutable private name : string

        abstract member Name : string with get, set
        default x.Name
            with get() = x.name
            and set name =
                x.name <- name
                x.Context.SetObjectLabel(ObjectLabelIdentifier.Buffer, x.Handle, name)

        abstract member Destroy : unit -> unit
        default x.Destroy() =
            GL.DeleteBuffer x.Handle
            GL.Check "failed to delete buffer"

        member x.Dispose() =
            using x.Context.ResourceLock (fun _ ->
                x.Destroy()

                ResourceCounts.removeBuffer x.Context (int64 x.SizeInBytes)
                x.SizeInBytes <- 0n
                x.Handle <- 0
            )

        interface IBackendBuffer with
            member x.Runtime = x.Context.Runtime :> IBufferRuntime
            member x.Handle = uint64 x.Handle
            member x.Buffer = x
            member x.Offset = 0UL
            member x.SizeInBytes = uint64 x.SizeInBytes
            member x.Name with get() = x.Name and set name = x.Name <- name
            member x.Dispose() = x.Dispose()

        new(ctx : Context, size : nativeint, handle : int) =
            { Context = ctx; SizeInBytes = size; Handle = handle; name = null }
    end

type internal SharedBuffer(ctx, size, handle, external : IExportedBackendBuffer, memory : SharedMemoryBlock) =
    inherit Buffer(ctx, size, handle)

    member x.External = external
    member x.Memory = memory

    override x.Destroy() =
        x.Memory.Dispose()
        base.Destroy()

[<AutoOpen>]
module BufferExtensions =

    module private BufferStorage =

        let toUsageHint (storage : BufferStorage) =
            match storage with
            | BufferStorage.Device -> BufferUsageHint.StaticDraw
            | BufferStorage.Host -> BufferUsageHint.StreamDraw
            | _ -> failf "unknown buffer storage type %A" storage

    /// <summary>
    /// extends Context with functions for creating, modifying and deleting buffers.
    /// </summary>
    type Context with

        /// <summary>
        /// creates a new buffer and initializes its content (copy to GPU memory)
        /// the storage type given is just a hint for the OpenGL implementation how
        /// to treat the buffer internally
        /// </summary>
        member x.CreateBuffer(data : nativeint, sizeInBytes : nativeint, [<Optional; DefaultParameterValue(BufferStorage.Device)>] storage : BufferStorage) =
            ResourceCounts.addBuffer x (int64 sizeInBytes)

            let handle =
                using x.ResourceLock (fun _ ->
                    let handle = GL.Dispatch.CreateBuffer()
                    GL.Check "failed to create buffer"

                    GL.Dispatch.NamedBufferData(handle, (nativeint sizeInBytes), data, BufferStorage.toUsageHint storage)
                    GL.Check "failed to upload buffer"

                    handle
                )

            new Buffer(x, sizeInBytes, handle)

        /// <summary>
        /// creates a new buffer and initializes its size (allocates GPU memory)
        /// the storage type given is just a hint for the OpenGL implementation how
        /// to treat the buffer internally
        /// </summary>
        member inline x.CreateBuffer(sizeInBytes : nativeint, [<Optional; DefaultParameterValue(BufferStorage.Device)>] storage : BufferStorage) =
            x.CreateBuffer(0n, sizeInBytes, storage)

        /// <summary>
        /// creates a new buffer and initializes its content (copy to GPU memory)
        /// the access mode given is just a hint for the OpenGL implementation how
        /// to treat the buffer internally
        /// </summary>
        member x.CreateBuffer(data : Array, [<Optional; DefaultParameterValue(BufferStorage.Device)>] storage : BufferStorage) =
            let size = nativeint (data.GetType().GetElementType().CLRSize) * nativeint data.Length

            data |> NativeInt.pin (fun src ->
                x.CreateBuffer(src, size, storage)
            )

        member internal x.ImportBuffer(buffer : IExportedBackendBuffer) =
            using x.ResourceLock (fun _ ->
                let memory = buffer.Memory
                let sharedMemory = x.ImportMemoryBlock memory.Block

                let handle = GL.GenBuffer()
                GL.Check "failed to create buffer"

                GL.BindBuffer(BufferTarget.CopyWriteBuffer, handle)
                GL.Check "failed to bind buffer"

                GL.Dispatch.BufferStorageMem(BufferTarget.CopyWriteBuffer, nativeint memory.Size, sharedMemory.Handle, int64 memory.Offset)
                GL.Check "failed to import buffer"

                ResourceCounts.addBuffer x (int64 buffer.SizeInBytes)
                new SharedBuffer(x, nativeint buffer.SizeInBytes, handle, buffer, sharedMemory)
            )

        /// <summary>
        /// deletes the given buffer causing its memory to be freed
        /// </summary>
        member x.Delete(buffer : Buffer) =
            buffer.Dispose()

        member x.CreateBuffer(data : IBuffer, [<Optional; DefaultParameterValue(BufferStorage.Device)>] storage : BufferStorage) =
            match data with
            | :? Buffer as bb ->
                bb

            | :? ArrayBuffer as ab ->
                x.CreateBuffer(ab.Data, storage)

            | :? INativeBuffer as nb ->
                nb.Use (fun ptr -> x.CreateBuffer(ptr, nativeint nb.SizeInBytes, storage))

            | :? IExportedBackendBuffer as b ->
                x.ImportBuffer b

            | :? IBufferRange as bv when bv != bv.Buffer ->
                x.CreateBuffer(bv.Buffer, storage)

            | _ when data = Unchecked.defaultof<_> ->
                failf $"buffer data must not be null"

            | _ ->
                failf $"unsupported buffer type: {data.GetType()}"

        // =======================================================================================================================
        //      UploadRange Overloads
        // =======================================================================================================================
        /// <summary>
        /// uploads data from the given pointer to the target-range specified
        /// while leaving the rest of the buffer untouched.
        /// NOTE: Fails when the target-range exceeds the buffer's current size.
        /// </summary>
        member x.UploadRange(buffer : Buffer, src : nativeint, targetOffset : nativeint, size : nativeint) =
            use __ = x.ResourceLock
            assert (targetOffset >= 0n)
            assert (size >= 0n)
            assert (src <> 0n)

            if targetOffset + size > buffer.SizeInBytes then
                raise <| IndexOutOfRangeException("range uploads may not exceed the buffer's size")
            else
                let target = GL.Dispatch.MapNamedBufferRange(buffer.Handle, targetOffset, size, BufferAccessMask.MapWriteBit)
                GL.Check "failed to map buffer for writing"
                if target <> 0n then
                    Buffer.MemoryCopy(src, target, uint64 size, uint64 size)
                else
                    Log.warn "[GL] could not map buffer for writing"

                GL.Dispatch.UnmapNamedBuffer(buffer.Handle) |> ignore
                GL.Check "failed to unmap buffer"


        member x.UploadRanges(buffer : Buffer, src : nativeint, ranges : seq<Range1i>) =
            use __ = x.ResourceLock
            let target = GL.Dispatch.MapNamedBufferRange(buffer.Handle, 0n, buffer.SizeInBytes, BufferAccessMask.MapWriteBit ||| BufferAccessMask.MapFlushExplicitBit)
            GL.Check "failed to map buffer for writing"
            if target <> 0n then
                for r in ranges do
                    let offset = nativeint r.Min
                    let size = nativeint (r.Size + 1)

                    Marshal.Copy(src + offset, target + offset, size)

                    GL.Dispatch.FlushMappedNamedBufferRange(buffer.Handle, offset, size)
                    GL.Check "failed to invalidate buffer-range"
            else
                Log.warn "[GL] could not map buffer"

            GL.Dispatch.UnmapNamedBuffer(buffer.Handle) |> ignore
            GL.Check "failed to unmap buffer"

        /// <summary>
        /// uploads a subrange of the given array to a (possibly different) range of the buffer.
        /// source: [sourceStartIndex, sourceStartIndex + count)
        /// target: [targetStartIndex, targetStartIndex + count)
        /// </summary>
        member x.UploadRange(buffer : Buffer, source : Array, sourceStartIndex : int, targetStartIndex : int, count : int) =
            assert(sourceStartIndex >= 0)
            assert(targetStartIndex >= 0)
            assert(count >= 0)

            let handle = GCHandle.Alloc(source, GCHandleType.Pinned)

            try
                let elementType = source.GetType().GetElementType()
                let elementSize = nativeint elementType.CLRSize
                let targetOffset = nativeint targetStartIndex * elementSize
                let size = nativeint count * elementSize

                let ptr = handle.AddrOfPinnedObject() + nativeint sourceStartIndex * elementSize

                x.UploadRange(buffer, ptr, targetOffset, size)

            finally
                handle.Free()


        // =======================================================================================================================
        //      DownloadRange Overloads
        // =======================================================================================================================

        /// <summary>
        /// downloads a range of the given buffer to the target specified.
        /// NOTE: Fails when the source-range exceeds the buffer's current size.
        /// </summary>
        member x.DownloadRange(buffer : Buffer, target : nativeint, sourceOffset : nativeint, size : nativeint) =
            use __ = x.ResourceLock
            assert (sourceOffset >= 0n)
            assert (size >= 0n)
            assert (target <> 0n)

            if sourceOffset + size > buffer.SizeInBytes then
                raise <| IndexOutOfRangeException("downloads may not exceed the buffer's size")
            else
                let src = GL.Dispatch.MapNamedBufferRange(buffer.Handle, sourceOffset, size, BufferAccessMask.MapReadBit)
                GL.Check "failed to map buffer for reading"
                if src <> 0n then
                    Buffer.MemoryCopy(src, target, uint64 size, uint64 size)
                else
                    Log.warn "[GL] could not map buffer for reading"

                GL.Dispatch.UnmapNamedBuffer(buffer.Handle) |> ignore
                GL.Check "failed to unmap buffer"



        /// <summary>
        /// downloads a subrange of the given buffer to a (possibly different) range of the array.
        /// source: [sourceStartIndex, sourceStartIndex + count)
        /// target: [targetStartIndex, targetStartIndex + count)
        /// </summary>
        member x.DownloadRange(buffer : Buffer, target : Array, sourceStartIndex : int, targetStartIndex : int, count : int) =
            assert(sourceStartIndex >= 0)
            assert(targetStartIndex >= 0)
            assert(count >= 0)

            let handle = GCHandle.Alloc(target, GCHandleType.Pinned)

            try
                let elementType = target.GetType().GetElementType()
                let elementSize = nativeint elementType.CLRSize
                let sourceOffset = nativeint sourceStartIndex * elementSize
                let size = nativeint count * elementSize

                let ptr = handle.AddrOfPinnedObject() + nativeint targetStartIndex * elementSize

                x.DownloadRange(buffer, ptr, sourceOffset, size)

            finally
                handle.Free()

        // =======================================================================================================================
        //      Upload Overloads
        // =======================================================================================================================

        /// <summary>
        /// uploads data from the pointer to a buffer while possibly resizing the buffer.
        /// note that performing this operation will cause the buffer's
        /// usage-hint to become "DynamicDraw"
        /// </summary>
        member x.Upload(buffer : Buffer, src : nativeint, size : nativeint, [<Optional; DefaultParameterValue(true)>] useNamed : bool) =
            use __ = x.ResourceLock
            assert(src <> 0n)

            if buffer.SizeInBytes <> size then
                ResourceCounts.removeBuffer x (int64 buffer.SizeInBytes)
                ResourceCounts.addBuffer x (int64 size)
                buffer.SizeInBytes <- size
                let source = if size = 0n then 0n else src

                if useNamed then
                    GL.Dispatch.NamedBufferData(buffer.Handle, size, source, BufferUsageHint.StaticDraw)
                    GL.Check "could not resize buffer"
                else
                    ExtensionHelpers.bindBuffer buffer.Handle (fun t ->
                        GL.BufferData(t, size, source, BufferUsageHint.StaticDraw)
                        GL.Check "could not resize buffer"
                    )

            elif size <> 0n then
                if useNamed then
                    GL.Dispatch.NamedBufferSubData(buffer.Handle, 0n, size, src)
                    GL.Check "failed to upload buffer"
                else
                    ExtensionHelpers.bindBuffer buffer.Handle (fun t ->
                        GL.BufferSubData(t, 0n, size, src)
                        GL.Check "could not upload buffer"
                    )


        /// <summary>
        /// uploads a range from the data array to a buffer while possibly resizing the buffer.
        /// note that performing this operation will cause the buffer's
        /// usage-hint to become "DynamicDraw"
        /// </summary>
        member x.Upload(buffer : Buffer, source : Array, sourceStartIndex : int, count : int, [<Optional; DefaultParameterValue(true)>] useNamed : bool) =
            assert(sourceStartIndex >= 0)
            assert(count >= 0)
            assert(sourceStartIndex + count <= source.Length)

            let handle = GCHandle.Alloc(source, GCHandleType.Pinned)

            try
                let elementType = source.GetType().GetElementType()
                let elementSize = elementType.CLRSize |> nativeint
                let size = nativeint count * elementSize

                let ptr = handle.AddrOfPinnedObject() + nativeint sourceStartIndex * elementSize

                x.Upload(buffer, ptr, size, useNamed)

            finally
                handle.Free()

        member inline x.Upload(buffer : Buffer, source : Array, sourceStartIndex : int, [<Optional; DefaultParameterValue(true)>] useNamed : bool) =
            x.Upload(buffer, source, sourceStartIndex, source.Length - sourceStartIndex, useNamed)

        member inline x.Upload(buffer : Buffer, source : Array, [<Optional; DefaultParameterValue(true)>] useNamed : bool) =
            x.Upload(buffer, source, 0, source.Length, useNamed)

        member x.Upload(b : Buffer, data : IBuffer, [<Optional; DefaultParameterValue(true)>] useNamed : bool) =
            if b.Handle = 0 then failf "cannot update null buffer"
            match data with
            | :? ArrayBuffer as ab ->
                x.Upload(b, ab.Data, useNamed)

            | :? Buffer as bb ->
                if bb.Handle <> b.Handle then failf $"cannot change backend buffer handle (old: {b.Handle}, new: {bb.Handle})"

            | :? INativeBuffer as n ->
                n.Use (fun ptr -> x.Upload(b, ptr, nativeint n.SizeInBytes, useNamed))

            | :? IBufferRange as bv when bv != bv.Buffer ->
                x.Upload(b, bv.Buffer, useNamed)

            | _ when data = Unchecked.defaultof<_> ->
                failf $"buffer data is null"

            | _ ->
                failf $"unsupported buffer type: {data.GetType()}"

        // =======================================================================================================================
        //      Download Overloads
        // =======================================================================================================================

        member inline x.Download(buffer : Buffer, target : Array, targetStartIndex : int, count : int) =
            x.DownloadRange(buffer, target, 0, targetStartIndex, count)

        member inline x.Download(buffer : Buffer, target : Array, count : int) =
            x.DownloadRange(buffer, target, 0, 0, count)

        member inline x.Download(buffer : Buffer, target : Array) =
            x.DownloadRange(buffer, target, 0, 0, target.Length)

        // =======================================================================================================================
        //      Debug Download Overloads
        // =======================================================================================================================
        member x.Download(buffer : Buffer, t : Type) =
            let elementSize = nativeint t.CLRSize
            let arr = Array.CreateInstance(t, int64 (buffer.SizeInBytes / elementSize))
            x.DownloadRange(buffer, arr, 0, 0, arr.Length)
            arr

        member x.Download<'a>(buffer : Buffer) =
            let elementSize = nativeint sizeof<'a>
            let arr : 'a[] = Array.zeroCreate (int (buffer.SizeInBytes / elementSize))
            x.DownloadRange(buffer, arr, 0, 0, arr.Length)
            arr


[<AutoOpen>]
module IndirectBufferExtensions =

    type GLIndirectBuffer =
        class
            val mutable public Buffer : Buffer
            val mutable public Count : int
            val mutable public Stride : int
            val mutable public Indexed : bool
            val mutable public OwnResource : bool

            new(b, count, stride, indexed, ownResource) = { Buffer = b; Count = count; Stride = stride; Indexed = indexed; OwnResource = ownResource }
        end

    type Context with

        member x.Clear(b : Buffer, size : nativeint) =
            using x.ResourceLock (fun _ ->
                GL.Dispatch.NamedBufferData(b.Handle, size, 0n, BufferUsageHint.StaticDraw)
                GL.Check "could not clear buffer"
                b.SizeInBytes <- size
            )

        member x.Copy(source : Buffer, sourceOffset : nativeint, target : Buffer, targetOffset : nativeint, size : nativeint) =
            use __ = x.ResourceLock

            if sourceOffset < 0n || targetOffset < 0n || size < 0n then
                failf "invalid arguments for buffer copy"

            if targetOffset + size > target.SizeInBytes || sourceOffset + size > source.SizeInBytes then
                failf "insufficient buffer size"

            GL.Dispatch.CopyNamedBufferSubData(source.Handle, target.Handle, sourceOffset, targetOffset, size)
            GL.Check "could not copy buffer"

        member x.Clone(b : Buffer, offset : nativeint, size : nativeint, [<Optional; DefaultParameterValue(BufferStorage.Device)>] storage : BufferStorage) =
            let mine = x.CreateBuffer(0n, size, storage)
            x.Copy(b, offset, mine, 0n, size)
            mine

        member inline x.Clone(b : Buffer, [<Optional; DefaultParameterValue(BufferStorage.Device)>] storage : BufferStorage) =
            x.Clone(b, 0n, b.SizeInBytes, storage)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Buffer =
    let create (c : Context) (sizeInBytes : nativeint) =
        c.CreateBuffer(sizeInBytes)

    let delete (b : Buffer) =
        b.Context.Delete(b)

    let write (data : Array) (b : Buffer) =
        b.Context.Upload(b, data)

    let read (b : Buffer) : 'a[] =
        b.Context.Download(b)

