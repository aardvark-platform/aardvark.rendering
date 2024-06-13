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
            member x.Handle = x.Handle :> obj
            member x.Buffer = x
            member x.Offset = 0n
            member x.SizeInBytes = x.SizeInBytes
            member x.Dispose() = x.Dispose()

        new(ctx : Context, size : nativeint, handle : int) =
            { Context = ctx; SizeInBytes = size; Handle = handle }
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
            | _ -> failwithf "[GL] Unknown buffer storage type %A" storage

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

            new Buffer(x, nativeint sizeInBytes, handle)

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
            let size = nativeint (data.GetType().GetElementType().GLSize) * nativeint data.Length

            pinned data (fun src ->
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

                GL.Dispatch.BufferStorageMem(BufferTarget.CopyWriteBuffer, nativeint memory.Size, sharedMemory.Handle, memory.Offset)
                GL.Check "failed to import buffer"

                ResourceCounts.addBuffer x (int64 buffer.SizeInBytes)
                new SharedBuffer(x, buffer.SizeInBytes, handle, buffer, sharedMemory)
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
                nb.Use (fun ptr -> x.CreateBuffer(ptr, nb.SizeInBytes, storage))

            | :? IExportedBackendBuffer as b ->
                x.ImportBuffer b

            | :? IBufferRange as bv ->
                let handle = bv.Buffer
                x.CreateBuffer(handle, storage)

            | _ ->
                failwith "unsupported buffer-type"



        member x.Upload(b : Buffer, data : IBuffer, useNamed : bool) =
            if b.Handle = 0 then failwith "cannot update null buffer"
            match data with
            | :? ArrayBuffer as ab -> x.Upload(b, ab.Data, useNamed)

            | :? Buffer as bb ->
                if bb.Handle <> b.Handle then failwith "cannot change backend-buffer handle"

            | :? INativeBuffer as n ->
                n.Use (fun ptr -> x.Upload(b, ptr, n.SizeInBytes, useNamed))

            | :? IBufferRange as bv ->
                let handle = bv.Buffer
                x.Upload(b, handle, useNamed)

            | _ ->
                failwithf "unsupported buffer-data-type: %A" data

        member x.Upload(b : Buffer, data : IBuffer) =
            x.Upload(b, data, true)

        // =======================================================================================================================
        //      UploadRange Overloads
        // =======================================================================================================================
        /// <summary>
        /// uploads data from the given pointer to the target-range specified
        /// while leaving the rest of the buffer untouched.
        /// NOTE: Fails when the target-range exceeds the buffer's current size.
        /// </summary>
        member x.UploadRange(buffer : Buffer, src : nativeint, targetOffset : int, size : int) =
            use __ = x.ResourceLock
            assert (targetOffset >= 0)
            assert (size >= 0)
            assert (src <> 0n)

            let nativeSize = size |> nativeint
            let totalSize = targetOffset + size |> nativeint

            if targetOffset < 0 || totalSize > buffer.SizeInBytes then
                raise <| IndexOutOfRangeException("range uploads may not exceed the buffer's size")
            else
                let target = GL.Dispatch.MapNamedBufferRange(buffer.Handle, nativeint targetOffset, nativeSize, BufferAccessMask.MapWriteBit)
                GL.Check "failed to map buffer for writing"
                if target <> 0n then
                    // TODO: Marshal.Copy should possibly take int64 sizes
                    Marshal.Copy(src, target, size)
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

            let elementType = source.GetType().GetElementType()
            let elementSize = Marshal.SizeOf(elementType)
            let targetOffset = targetStartIndex * elementSize
            let size = count * elementSize

            let ptr = handle.AddrOfPinnedObject() + nativeint (sourceStartIndex * elementSize)

            x.UploadRange(buffer, ptr, targetOffset, size)


            handle.Free()

        /// <summary>
        /// uploads a subrange of the given array to a (possibly different) range of the buffer.
        /// </summary>
        member x.UploadRange(buffer : Buffer, source : 'a[], sourceStartIndex : int, targetStartIndex : int, count : int) =
            // TODO: maybe inline this (maybe faster since no reflection stuff needed)
            x.UploadRange(buffer, source :> Array, sourceStartIndex, targetStartIndex, count)



        // =======================================================================================================================
        //      DownloadRange Overloads
        // =======================================================================================================================

        /// <summary>
        /// downloads a range of the given buffer to the target specified.
        /// NOTE: Fails when the source-range exceeds the buffer's current size.
        /// </summary>
        member x.DownloadRange(buffer : Buffer, target : nativeint, sourceOffset : int, size : int) =
            use __ = x.ResourceLock
            assert (sourceOffset >= 0)
            assert (size >= 0)
            assert (target <> 0n)

            let nativeSize = size |> nativeint
            let totalSize = sourceOffset + size |> nativeint

            if sourceOffset < 0 || totalSize > buffer.SizeInBytes then
                raise <| IndexOutOfRangeException("downloads may not exceed the buffer's size")
            else
                let src = GL.Dispatch.MapNamedBufferRange(buffer.Handle, nativeint sourceOffset, nativeSize, BufferAccessMask.MapReadBit)
                GL.Check "failed to map buffer for reading"
                if src <> 0n then
                    // TODO: Marshal.Copy should possibly take int64 sizes
                    Marshal.Copy(src, target, size)
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

            let elementType = target.GetType().GetElementType()
            let elementSize = Marshal.SizeOf(elementType)
            let sourceOffset = sourceStartIndex * elementSize
            let size = count * elementSize

            let ptr = handle.AddrOfPinnedObject() + nativeint (targetStartIndex * elementSize)

            x.DownloadRange(buffer, ptr, sourceOffset, size)


            handle.Free()

        /// <summary>
        /// downloads a subrange of the given buffer to a (possibly different) range of the array.
        /// </summary>
        member x.DownloadRange(buffer : Buffer, target : 'a[], sourceStartIndex : int, targetStartIndex : int, count : int) =
            // TODO: maybe inline this (maybe faster since no reflection stuff needed)
            x.DownloadRange(buffer, target :> Array, sourceStartIndex, targetStartIndex, count)

        // =======================================================================================================================
        //      Upload Overloads
        // =======================================================================================================================

        /// <summary>
        /// uploads data from the pointer to a buffer while possibly resizing the buffer.
        /// note that performing this operation will cause the buffer's
        /// usage-hint to become "DynamicDraw"
        /// </summary>
        member x.Upload(buffer : Buffer, src : nativeint, size : nativeint, useNamed : bool) =
            use __ = x.ResourceLock
            assert(size >= 0n)
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

        member x.Upload(buffer : Buffer, src : nativeint, size : nativeint) =
            x.Upload(buffer, src, size, true)



        /// <summary>
        /// uploads a range from the data array to a buffer while possibly resizing the buffer.
        /// note that performing this operation will cause the buffer's
        /// usage-hint to become "DynamicDraw"
        /// </summary>
        member x.Upload(buffer : Buffer, source : Array, sourceStartIndex : int, count : int, useNamed : bool) =
            assert(sourceStartIndex >= 0)
            assert(count >= 0)
            assert(sourceStartIndex + count <= source.Length)

            let handle = GCHandle.Alloc(source, GCHandleType.Pinned)

            let elementType = source.GetType().GetElementType()
            let elementSize = Marshal.SizeOf(elementType) |> nativeint
            let size = nativeint count * elementSize

            let ptr = handle.AddrOfPinnedObject() + nativeint sourceStartIndex * elementSize

            x.Upload(buffer, ptr, size, useNamed)


            handle.Free()

        member x.Upload(buffer : Buffer, source : Array, sourceStartIndex : int, count : int) =
            x.Upload(buffer, source, sourceStartIndex, count, true)

        member x.Upload(buffer : Buffer, source : Array, sourceStartIndex : int, useNamed : bool) =
            x.Upload(buffer, source, sourceStartIndex, source.Length - sourceStartIndex, useNamed)

        member x.Upload(buffer : Buffer, source : Array, useNamed : bool) =
            x.Upload(buffer, source, 0, source.Length, useNamed)


        member x.Upload(buffer : Buffer, source : Array, sourceStartIndex : int) =
            x.Upload(buffer, source, sourceStartIndex, source.Length - sourceStartIndex, true)

        member x.Upload(buffer : Buffer, source : Array) =
            x.Upload(buffer, source, 0, source.Length, true)


        // =======================================================================================================================
        //      Download Overloads
        // =======================================================================================================================

        member x.Download(buffer : Buffer, target : Array, targetStartIndex : int, count : int) =
            x.DownloadRange(buffer, target, 0, targetStartIndex, count)

        member x.Download(buffer : Buffer, target : Array, count : int) =
            x.DownloadRange(buffer, target, 0, 0, count)

        member x.Download(buffer : Buffer, target : Array) =
            x.DownloadRange(buffer, target, 0, 0, target.Length)

        member x.Download(buffer : Buffer, target : 'a[], targetStartIndex : int, count : int) =
            x.DownloadRange(buffer, target :> Array, 0, targetStartIndex, count)

        member x.Download(buffer : Buffer, target : 'a[], count : int) =
            x.DownloadRange(buffer, target :> Array, 0, 0, count)

        member x.Download(buffer : Buffer, target : 'a[]) =
            x.DownloadRange(buffer, target :> Array, 0, 0, target.Length)


        // =======================================================================================================================
        //      Debug Download Overloads
        // =======================================================================================================================
        member x.Download(buffer : Buffer, t : Type) =
            let elementSize = Marshal.SizeOf t
            let arr = Array.CreateInstance(t, int buffer.SizeInBytes / elementSize)
            x.DownloadRange(buffer, arr, 0, 0, arr.Length)
            arr

        member x.Download<'a>(buffer : Buffer) =
            let elementSize = sizeof<'a>
            let arr : 'a[] = Array.zeroCreate (int buffer.SizeInBytes / elementSize)
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
                failwith "[GL] invalid arguments for buffer copy"

            if targetOffset + size > target.SizeInBytes || sourceOffset + size > source.SizeInBytes then
                failwith "[GL] insufficient buffer size"

            GL.Dispatch.CopyNamedBufferSubData(source.Handle, target.Handle, sourceOffset, targetOffset, size)
            GL.Check "could not copy buffer"

        member x.Clone(b : Buffer, offset : nativeint, size : nativeint, [<Optional; DefaultParameterValue(BufferStorage.Device)>] storage : BufferStorage) =
            let mine = x.CreateBuffer(0n, size, storage)
            x.Copy(b, offset, mine, 0n, size)
            mine

        member x.Clone(b : Buffer) = x.Clone(b, 0n, b.SizeInBytes)

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

