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
open Microsoft.FSharp.NativeInterop
open Aardvark.Rendering.GL

#nowarn "9"

/// <summary>
/// defines usage hints for buffers. Note that these 
/// hints can (but must not) be respected by the backend implementation.
/// </summary>
type BufferUsageHint = Static | Dynamic

/// <summary>
/// Buffer simply wraps an OpenGL buffer object and
/// maintains its creation-context, etc.
/// </summary>
type Buffer =
    class
        val mutable public Handle : int
        val mutable public Context : Context
        val mutable public SizeInBytes : nativeint
        

        interface IBackendBuffer with
            member x.Runtime = x.Context.Runtime :> IBufferRuntime
            member x.Handle = x.Handle :> obj
            member x.SizeInBytes = x.SizeInBytes
            member x.Dispose() = x.Context.Runtime.DeleteBuffer x

        new(ctx : Context, size : nativeint, handle : int) = { Context = ctx; SizeInBytes = size; Handle = handle}
    end



[<AutoOpen>]
module BufferExtensions =

    let addBuffer (ctx:Context) size =
        Interlocked.Increment(&ctx.MemoryUsage.BufferCount) |> ignore
        Interlocked.Add(&ctx.MemoryUsage.BufferMemory,size) |> ignore

    let removeBuffer (ctx:Context) size =
        Interlocked.Decrement(&ctx.MemoryUsage.BufferCount)  |> ignore
        Interlocked.Add(&ctx.MemoryUsage.BufferMemory,-size) |> ignore

    let updateBuffer (ctx:Context) oldSize newSize =
        Interlocked.Add(&ctx.MemoryUsage.BufferMemory, newSize-oldSize) |> ignore

    /// <summary>
    /// helper function translating our self-defined BufferUsage
    /// to OpenTK's BufferUsageHints
    /// </summary>   
    let private glUsageHint (usage : BufferUsageHint) =
        match usage with
            | Static -> OpenTK.Graphics.OpenGL4.BufferUsageHint.StaticDraw
            | Dynamic -> OpenTK.Graphics.OpenGL4.BufferUsageHint.DynamicDraw

    /// <summary>
    /// extends Context with functions for creating, modifying and deleting buffers.
    /// </summary>
    type Context with

        /// <summary>
        /// creates a new buffer and initializes its size (allocates GPU memory)
        /// the usage given is just a hint for the OpenGL implementation how
        /// to treat the buffer internally
        /// </summary>
        member x.CreateBuffer(size : int, usageHint : BufferUsageHint) =
            assert(size >= 0)

            addBuffer x (int64 size)
            
            let handle = 
                using x.ResourceLock (fun _ ->
                    let handle = GL.GenBuffer()
                    
                    GL.NamedBufferData(handle, (nativeint size), 0n, glUsageHint usageHint)
                    GL.Check "failed to upload buffer"
                    
                    handle
                )

            new Buffer(x, nativeint size, handle)

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
        member x.CreateBuffer(data : Array, usageHint : BufferUsageHint) =
            let size = data.GetType().GetElementType().GLSize * data.Length
            let gcHandle = GCHandle.Alloc(data, GCHandleType.Pinned)
            let result = x.CreateBuffer(gcHandle.AddrOfPinnedObject(), size, usageHint)     
            gcHandle.Free()
            result


        member x.CreateBuffer(data : nativeint, sizeInBytes : int, usage : BufferUsageHint) =
            
            addBuffer x (int64 sizeInBytes)
            
            let handle = 
                using x.ResourceLock (fun _ ->
                    let handle = GL.GenBuffer()

                    EXT_direct_state_access.GL.NamedBufferData(handle, (nativeint sizeInBytes), data, glUsageHint usage)
                    GL.Check "failed to upload buffer"

                    handle
                )

            new Buffer(x, nativeint sizeInBytes, handle)

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
                    removeBuffer x (int64 buffer.SizeInBytes)
                    buffer.SizeInBytes <- 0n
                    buffer.Handle <- 0
                    GL.DeleteBuffer handle
                    GL.Check "failed to delete buffer"
                    #if DEBUG
                    let isBuffer = GL.IsBuffer handle
                    if isBuffer then Log.warn "deleted buffer which is still a buffer"
                    #endif
            )

        member x.CreateBuffer(data : IBuffer) =
            match data with
                | :? Buffer as bb ->
                    bb
                | :? ArrayBuffer as ab ->
                    x.CreateBuffer(ab.Data)

                | :? INativeBuffer as nb ->
                    nb.Use (fun ptr -> x.CreateBuffer(ptr, nb.SizeInBytes, Static))

                | :? IBufferRange as bv ->
                    let handle = bv.Buffer
                    x.CreateBuffer handle

                | _ -> 
                    failwith "unsupported buffer-type"



        member x.Upload(b : Buffer, data : IBuffer, useNamed : bool) =
            if b.Handle = 0 then failwith "cannot update null buffer"
            match data with
                | :? ArrayBuffer as ab -> x.Upload(b, ab.Data, useNamed)

                | :? Buffer as bb ->
                    if bb.Handle <> b.Handle then failwith "cannot change backend-buffer handle"

                | :? INativeBuffer as n ->
                    n.Use (fun ptr -> x.Upload(b, ptr, nativeint n.SizeInBytes, useNamed))

                
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
                let target = GL.MapNamedBufferRange(buffer.Handle, nativeint targetOffset, nativeSize, BufferAccessMask.MapWriteBit)
                GL.Check "failed to map buffer for writing"
                if target <> 0n then
                    // TODO: Marshal.Copy should possibly take int64 sizes
                    Marshal.Copy(src, target, size)
                else
                    Log.warn "[GL] could not map buffer for writing"

                GL.UnmapNamedBuffer(buffer.Handle) |> ignore
                GL.Check "failed to unmap buffer"


        member x.UploadRanges(buffer : Buffer, src : nativeint, ranges : seq<Range1i>) =
            use __ = x.ResourceLock
            let target = GL.MapNamedBufferRange(buffer.Handle, 0n, buffer.SizeInBytes, BufferAccessMask.MapWriteBit ||| BufferAccessMask.MapFlushExplicitBit)
            GL.Check "failed to map buffer for writing"
            if target <> 0n then
                for r in ranges do
                    let offset = nativeint r.Min
                    let size = nativeint (r.Size + 1)

                    Marshal.Copy(src + offset, target + offset, size)

                    GL.FlushMappedNamedBufferRange(buffer.Handle, offset, size)
                    GL.Check "failed to invalidate buffer-range"
            else
                Log.warn "[GL] could not map buffer"

            GL.UnmapNamedBuffer(buffer.Handle) |> ignore
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
                let src = GL.MapNamedBufferRange(buffer.Handle, nativeint sourceOffset, nativeSize, BufferAccessMask.MapReadBit)
                GL.Check "failed to map buffer for reading"
                if src <> 0n then
                    // TODO: Marshal.Copy should possibly take int64 sizes
                    Marshal.Copy(src, target, size)
                else
                    Log.warn "[GL] could not map buffer for reading"

                GL.UnmapNamedBuffer(buffer.Handle) |> ignore
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
                removeBuffer x (int64 buffer.SizeInBytes)
                addBuffer x (int64 size)
                buffer.SizeInBytes <- size
                let source = if size = 0n then 0n else src

                if useNamed then
                    GL.NamedBufferData(buffer.Handle, size, source, BufferUsageHint.StaticDraw)
                    GL.Check "could not resize buffer"
                else
                    ExtensionHelpers.bindBuffer buffer.Handle (fun t ->
                        GL.BufferData(t, size, source, BufferUsageHint.StaticDraw)
                        GL.Check "could not resize buffer"
                    )

            elif size <> 0n then
                if useNamed then
                    GL.NamedBufferSubData(buffer.Handle, 0n, size, src)
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

    [<StructLayout(LayoutKind.Sequential)>]
    type DrawArraysIndirectCommand =
        struct
            val mutable public count : uint32
            val mutable public instanceCount : uint32
            val mutable public first : uint32
            val mutable public baseInstance : uint32

            new(info : DrawCallInfo) =
                {
                    count = uint32 info.FaceVertexCount
                    instanceCount = uint32 info.InstanceCount
                    first = uint32 info.FirstIndex
                    baseInstance = uint32 info.FirstInstance
                }
        end

    [<StructLayout(LayoutKind.Sequential)>]
    type DrawElementsIndirectCommand =
        struct
            val mutable public count : uint32
            val mutable public instanceCount : uint32
            val mutable public first : uint32
            val mutable public baseVertex : uint32
            val mutable public baseInstance : uint32

            new(info : DrawCallInfo) =
                {
                    count = uint32 info.FaceVertexCount
                    instanceCount = uint32 info.InstanceCount
                    first = uint32 info.FirstIndex
                    baseVertex = uint32 info.BaseVertex
                    baseInstance = uint32 info.FirstInstance
                }
        end

    let private getIndirectData (indexed : bool) (data : IBuffer) =
        match data with
            | :? ArrayBuffer as ab ->
                match ab.Data with
                    | :? array<DrawCallInfo> as arr ->
                            
                        if indexed then
                            arr |> Array.map DrawElementsIndirectCommand :> Array
                        else
                            arr |> Array.map DrawArraysIndirectCommand :> Array

                    | _ ->
                        failwith "IndirectBuffer must contain DrawCallInfos"
            | _ -> 
                failwith "IndirectBuffers must be ArrayBuffers atm."

    /// repairs the buffer containing DrawCallInfos s.t. it matches the GL layout.
    /// sadly this needs to be done since DrawArraysIndirectCommand is not a sub-range of
    /// DrawElementsIndirectCommand. Therefore we need to exchange the 3rd and 4th fields
    /// of every DrawCallInfo (FirstInstance and BaseVertex)
    /// This function is executed after every write to the buffer (Create / Upload)
    /// and assumes that the buffer is filled with DrawCallInfos
    let private postProcessDrawCallBuffer (indexed : bool) (b : Buffer) =
        let callCount = b.SizeInBytes / nativeint sizeof<DrawCallInfo> |> int

        if indexed && callCount > 0 then
            using b.Context.ResourceLock (fun _ ->
                let ptr = GL.MapNamedBufferRange(b.Handle, 0n, b.SizeInBytes, BufferAccessMask.MapReadBit ||| BufferAccessMask.MapWriteBit)
                if ptr = 0n then failwithf "[GL] could not map buffer"


                let step = 5 //sizeof<DrawCallInfo> / sizeof<int>
                let ptr : nativeptr<int> = NativePtr.ofNativeInt ptr

                // swap the two last fields FirstInstance/BaseVertex (indices 3,4)
                let mutable current = 3
                let mutable next = current + 1
                for i in 0..callCount-1 do
                    let firstInstance = NativePtr.get ptr current
                    let baseVertex = NativePtr.get ptr next
                    NativePtr.set ptr current baseVertex
                    NativePtr.set ptr next firstInstance

                    current <- current + step
                    next <- current + 1


                GL.UnmapNamedBuffer(b.Handle) |> ignore
                GL.Check "could not unmap buffer"
                

            )

        callCount

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
                GL.NamedBufferData(b.Handle, size, 0n, BufferUsageHint.StaticDraw)
                GL.Check "could not clear buffer"
                b.SizeInBytes <- size
            )

        member x.Copy(source : Buffer, sourceOffset : nativeint, target : Buffer, targetOffset : nativeint, size : nativeint) =
            use __ = x.ResourceLock
            
            if sourceOffset < 0n || targetOffset < 0n || size < 0n then
                failwith "[GL] invalid arguments for buffer copy"

            if targetOffset + size > target.SizeInBytes || sourceOffset + size > source.SizeInBytes then
                failwith "[GL] insufficient buffer size"
                
            GL.NamedCopyBufferSubData(source.Handle, target.Handle, sourceOffset, targetOffset, size)
            GL.Check "could not copy buffer"

        member x.Clone(b : Buffer, offset : nativeint, size : nativeint) =
            let mine = x.CreateBuffer(0n, int size, BufferUsageHint.Dynamic)
            x.Copy(b, offset, mine, 0n, size)
            mine

        member x.Clone(b : Buffer) = x.Clone(b, 0n, b.SizeInBytes)

        //member x.Delete(buffer : BackendIndirectBuffer) =
        //    x.Delete(buffer.Buffer :?> Buffer) // by convention

        //member x.UploadIndirect(indirect : GLIndirectBuffer, indexed : bool, data : IndirectBuffer) =
        //    match data.Buffer with
        //    | :? Buffer as b -> 
        //        if data.Indexed <> indexed then
        //            failwith "[GL] incompatible IndirectBuffer: indexed option does not match"
        //        GLIndirectBuffer(b, data.Count, data.Stride, data.Indexed) // OwnHandle !?
        //    | _ ->
        //        using x.ResourceLock (fun _ ->
                    
        //            let layoutedData = 
        //                match data.Buffer with
        //                | :? ArrayBuffer as ab -> failwith "" 
        //                | :? SingleValueBuffer as sb -> failwith ""
        //                | _ -> data.Buffer // Native or other -> assume data is compatible

        //            // TODO
        //            failwith 
        //            //if indirect.Buffer.SizeInBytes <> layoutedData.SizeInBytes then
        //            //    x.Clear(indirect.Buffer, layoutedData.SizeInBytes)
        //            //x.Copy(indirect.Buffer, 0n, buffer, 0n, b.SizeInBytes)
                    
        //            //let buffer = indirect.Buffer :?> Buffer // by convention
        //            //match data.Buffer with
        //            //    | :? Buffer as b ->
        //            //        if buffer.SizeInBytes <> b.SizeInBytes then
        //            //            x.Clear(buffer, b.SizeInBytes)
        //            //        x.Copy(b, 0n, buffer, 0n, b.SizeInBytes)

        //            //    | b -> 
        //            //        x.Upload(buffer, b, false)

        //            //let callCount = postProcessDrawCallBuffer indexed buffer
        //            //indirect.Indexed <- indexed
        //            //indirect.Count <- data.Count
        //        )

        //member x.CreateIndirect(indexed : bool, data : IndirectBuffer) =
        //    match data.Buffer with
        //    | :? Buffer as b -> 
        //        if data.Indexed <> indexed then
        //            failwith "[GL] incompatible IndirectBuffer: indexed option does not match"
        //        GLIndirectBuffer(b, data.Count, data.Stride, data.Indexed) // OwnHandle !?
        //    | _ ->
        //        using x.ResourceLock (fun _ ->
                    
        //            // transform data array to indexed/non-indexed layout
        //            let layoutedData = 
        //                match data.Buffer with
        //                | :? ArrayBuffer as ab -> failwith "" 
        //                | :? SingleValueBuffer as sb -> failwith ""
        //                | _ -> data.Buffer // Native or other -> assume data is compatible

        //            let buffer = x.CreateBuffer(layoutedData)
        //            GLIndirectBuffer(buffer, data.Count, data.Stride, data.Indexed)

        //            //let buffer = 
        //            //    match data.Buffer with
        //            //        | :? Buffer as b -> x.Clone(b)
        //            //        | _ -> x.CreateBuffer(data.Buffer)

        //            //let callCount = postProcessDrawCallBuffer indexed buffer
        //            //BackendIndirectBuffer(buffer :> IBackendBuffer, data.Count, sizeof<DrawCallInfo>, indexed)
        //        )

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

