namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Collections.Concurrent
open System.Runtime.InteropServices
open Aardvark.Base
open OpenTK
open OpenTK.Platform
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4

/// <summary>
/// defines usage hints for buffers. Note that these 
/// hints can (but must not) be respected by the backend implementation.
/// </summary>
type BufferUsage = Static | Dynamic

/// <summary>
/// Buffer simply wraps an OpenGL buffer object and
/// maintains its creation-context, etc.
/// </summary>
type Buffer =
    class
        val mutable public Handle : int
        val mutable public Context : Context
        val mutable public SizeInBytes : nativeint
            
        member x.Validate() =
            using x.Context.ResourceLock (fun _ ->
                validate {
                    do! requires (GL.IsBuffer x.Handle) "not a buffer object"

                    GL.BindBuffer(BufferTarget.ArrayBuffer, x.Handle)
                    let mutable r = 0L
                    GL.GetBufferParameter(BufferTarget.ArrayBuffer, BufferParameterName.BufferSize, &r)
                    do! eq r (int64 x.SizeInBytes) "invalid buffer size"
                    GL.BindBuffer(BufferTarget.ArrayBuffer, 0)

                }
            )

        interface IResource with
            member x.Context = x.Context
            member x.Handle = x.Handle

        interface IBackendBuffer with
            member x.Handle = x.Handle :> obj


        new(ctx : Context, size : nativeint, handle : int) = { Context = ctx; SizeInBytes = size; Handle = handle}
    end



[<AutoOpen>]
module BufferExtensions =

    /// <summary>
    /// helper function translating our self-defined BufferUsage
    /// to OpenTK's BufferUsageHints
    /// </summary>   
    let private usageHint (usage : BufferUsage) =
        match usage with
            | Static -> BufferUsageHint.StaticDraw
            | Dynamic -> BufferUsageHint.StaticDraw

    /// <summary>
    /// extends Context with functions for creating, modifying and deleting buffers.
    /// </summary>
    type Context with

        /// <summary>
        /// creates a new buffer and initializes its size (allocates GPU memory)
        /// the usage given is just a hint for the OpenGL implementation how
        /// to treat the buffer internally
        /// </summary>
        member x.CreateBuffer(size : int, usage : BufferUsage) =
            assert(size >= 0)

            let handle = 
                using x.ResourceLock (fun _ ->
                    let handle = GL.GenBuffer()

                    GL.BindBuffer(BufferTarget.ArrayBuffer, handle)
                    GL.Check "failed to create buffer"

                    GL.BufferData(BufferTarget.ArrayBuffer, (nativeint size), 0n, usageHint usage)
                    GL.Check "failed to upload buffer"

                    GL.BindBuffer(BufferTarget.ArrayBuffer, 0)
                    GL.Check "failed to unbind buffer"

                    handle
                )

            Buffer(x, nativeint size, handle)

        /// <summary>
        /// creates a new buffer and initializes its size (allocates GPU memory)
        /// </summary>
        member x.CreateBuffer(size : int) = 
            x.CreateBuffer(size, Static)

        /// <summary>
        /// creates a new buffer and initializes its content (copy to GPU memory)
        /// the usage given is just a hint for the OpenGL implementation how
        /// to treat the buffer internally
        /// </summary>
        member x.CreateBuffer(data : Array, usage : BufferUsage) =
            let size = data.GetType().GetElementType().GLSize * data.Length
            let gcHandle = GCHandle.Alloc(data, GCHandleType.Pinned)
            let result = x.CreateBuffer(gcHandle.AddrOfPinnedObject(), size, usage)     
            gcHandle.Free()
            result


        member x.CreateBuffer(data : nativeint, sizeInBytes : int, usage : BufferUsage) =
            let handle = 
                using x.ResourceLock (fun _ ->
                    let handle = GL.GenBuffer()

                    GL.BindBuffer(BufferTarget.ArrayBuffer, handle)
                    GL.Check "failed to create buffer"

                    GL.BufferData(BufferTarget.ArrayBuffer, (nativeint sizeInBytes), data, usageHint usage)
                    GL.Check "failed to upload buffer"

                    GL.BindBuffer(BufferTarget.ArrayBuffer, 0)
                    GL.Check "failed to unbind buffer"

                    handle
                )

            Buffer(x, nativeint sizeInBytes, handle)

        /// <summary>
        /// creates a new buffer and initializes its content (copy to GPU memory)
        /// </summary>
        member x.CreateBuffer(data : Array) =
            x.CreateBuffer(data, Static)

        /// <summary>
        /// deletes the given buffer causing its memory to be freed
        /// </summary>
        member x.Delete(buffer : Buffer) =
            using x.ResourceLock (fun _ ->
                let handle = Interlocked.Exchange(&buffer.Handle, -1)
                if handle <> -1 then
                    GL.DeleteBuffer buffer.Handle
                    GL.Check "failed to delete buffer"

            )

        member x.CreateBuffer(data : IBuffer) =
            match data with
                | :? Buffer as bb ->
                    bb
                | :? ArrayBuffer as ab ->
                    x.CreateBuffer(ab.Data)
                | :? NullBuffer -> 
                    Buffer(x,0n,0)
                | :? NativeMemoryBuffer as nativeBuffer ->
                    x.CreateBuffer(nativeBuffer.Ptr,nativeBuffer.SizeInBytes, Static)

                | _ -> 
                    failwith "unsupported buffer-type"

        member x.Upload(b : Buffer, data : IBuffer) =
            if b.Handle = 0 then failwith "cannot update null buffer"
            match data with
                | :? ArrayBuffer as ab -> x.Upload(b, ab.Data)
                | :? Buffer as bb ->
                    if bb.Handle <> b.Handle then failwith "cannot change backend-buffer handle"
                | :? NullBuffer ->
                    failwith "cannot create null buffer out of non-null buffer"
                | :? NativeMemoryBuffer as n ->
                   x.Upload(b,n.Ptr,n.SizeInBytes)
                | _ ->
                    failwithf "unsupported buffer-data-type: %A" data


        // =======================================================================================================================
        //      UploadRange Overloads
        // =======================================================================================================================
        /// <summary>
        /// uploads data from the given pointer to the target-range specified
        /// while leaving the rest of the buffer untouched. 
        /// NOTE: Fails when the target-range exceeds the buffer's current size.
        /// </summary>
        member x.UploadRange(buffer : Buffer, src : nativeint, targetOffset : int, size : int) =
            assert (targetOffset >= 0)
            assert (size >= 0)
            assert (src <> 0n)

            let nativeSize = size |> nativeint
            let totalSize = targetOffset + size |> nativeint

            using x.ResourceLock (fun _ ->
                GL.BindBuffer(BufferTarget.ArrayBuffer, buffer.Handle)
                GL.Check "failed to bind buffer"

                if targetOffset < 0 || totalSize > buffer.SizeInBytes then
                    raise <| IndexOutOfRangeException("range uploads may not exceed the buffer's size")
                else
                    let target = GL.MapBufferRange(BufferTarget.ArrayBuffer, nativeint targetOffset, nativeSize, BufferAccessMask.MapWriteBit)
                    GL.Check "failed to map buffer for writing"

                    // TODO: Marshal.Copy should possibly take int64 sizes
                    Marshal.Copy(src, target, size)

                    GL.UnmapBuffer(BufferTarget.ArrayBuffer) |> ignore
                    GL.Check "failed to unmap buffer"


                GL.BindBuffer(BufferTarget.ArrayBuffer, 0)
                GL.Check "failed to unbind buffer"
            )

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
            assert (sourceOffset >= 0)
            assert (size >= 0)
            assert (target <> 0n)
   
            let nativeSize = size |> nativeint
            let totalSize = sourceOffset + size |> nativeint

            using x.ResourceLock (fun _ ->
                GL.BindBuffer(BufferTarget.ArrayBuffer, buffer.Handle)
                GL.Check "failed to bind buffer"

                if sourceOffset < 0 || totalSize > buffer.SizeInBytes then
                    raise <| IndexOutOfRangeException("downloads may not exceed the buffer's size")
                else
                    let src = GL.MapBufferRange(BufferTarget.ArrayBuffer, nativeint sourceOffset, nativeSize, BufferAccessMask.MapReadBit)
                    GL.Check "failed to map buffer for writing"

                    // TODO: Marshal.Copy should possibly take int64 sizes
                    Marshal.Copy(src, target, size)

                    GL.UnmapBuffer(BufferTarget.ArrayBuffer) |> ignore
                    GL.Check "failed to unmap buffer"


                GL.BindBuffer(BufferTarget.ArrayBuffer, 0)
                GL.Check "failed to unbind buffer"
            )

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
        member x.Upload(buffer : Buffer, src : nativeint, size : int) =
            assert(size >= 0)
            assert(src <> 0n)

            let nativeSize = size |> nativeint

            using x.ResourceLock (fun _ ->
                GL.BindBuffer(BufferTarget.ArrayBuffer, buffer.Handle)
                GL.Check "failed to bind buffer"

                if buffer.SizeInBytes <> nativeSize then
                    GL.BufferData(BufferTarget.ArrayBuffer, nativeSize, src, BufferUsageHint.DynamicDraw)
                    GL.Check "failed to set buffer data"
                else
                    let target = GL.MapBufferRange(BufferTarget.ArrayBuffer, 0n, nativeSize, BufferAccessMask.MapWriteBit)
                    GL.Check "failed to map buffer for writing"

                    // TODO: Marshal.Copy should possibly take int64 sizes
                    Marshal.Copy(src, target, size)

                    GL.UnmapBuffer(BufferTarget.ArrayBuffer) |> ignore
                    GL.Check "failed to unmap buffer"


                GL.BindBuffer(BufferTarget.ArrayBuffer, 0)
                GL.Check "failed to unbind buffer"
            )

        /// <summary>
        /// uploads a range from the data array to a buffer while possibly resizing the buffer.
        /// note that performing this operation will cause the buffer's
        /// usage-hint to become "DynamicDraw"
        /// </summary>
        member x.Upload(buffer : Buffer, source : Array, sourceStartIndex : int, count : int) =
            assert(sourceStartIndex >= 0)
            assert(count >= 0)

            let handle = GCHandle.Alloc(source, GCHandleType.Pinned)

            let elementType = source.GetType().GetElementType()
            let elementSize = Marshal.SizeOf(elementType)
            let size = count * elementSize

            let ptr = handle.AddrOfPinnedObject() + nativeint (sourceStartIndex * elementSize)

            x.Upload(buffer, ptr, size)


            handle.Free()

        member x.Upload(buffer : Buffer, source : Array, sourceStartIndex : int) =
            x.Upload(buffer, source, sourceStartIndex, source.Length - sourceStartIndex)

        member x.Upload(buffer : Buffer, source : Array) =
            x.Upload(buffer, source, 0, source.Length)


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

    /// <summary>
    /// extends ExecutionContext with functions for binding/unbinding
    /// buffers. 
    /// </summary>
    module ExecutionContext =
        let bindBuffer (target : BufferTarget) (b : Buffer) =
            [
                yield Instruction.BindBuffer (int target) b.Handle
            ]

        let unbindBuffer (target : BufferTarget) =
            [
                yield Instruction.BindBuffer (int target) 0
            ]


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Buffer =
    let create (c : Context) (size : int) =
        c.CreateBuffer(size)

    let delete (b : Buffer) =
        b.Context.Delete(b)

    let write (data : Array) (b : Buffer) =
        b.Context.Upload(b, data)

    let read (b : Buffer) : 'a[] =
        b.Context.Download(b)

