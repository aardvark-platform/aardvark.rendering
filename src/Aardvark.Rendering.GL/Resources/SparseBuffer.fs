namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Collections.Generic
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Rendering.GL

[<AbstractClass>]
type SparseBuffer(ctx : Context, size : nativeint, handle : int, beforeRender : unit -> unit, afterRender : unit -> unit) =
    inherit Buffer(ctx, size, handle)
    
    let writeFence = FenceSet()
    let resourceLock = ResourceLock()

    abstract member UsedMemory : Mem
    abstract member Commitment : offset : nativeint * size : nativeint * commit : bool -> unit
    abstract member PerformWrite : offset : nativeint * size : nativeint * data : nativeint -> unit

    abstract member Release : unit -> unit
    default x.Release() = ()

    member x.Dispose() =
        use __ = ctx.ResourceLock
        x.Release()
        GL.DeleteBuffer(handle)
        GL.Check "could not delete buffer"

    member x.Write(offset : nativeint, size : nativeint, data : nativeint) =
        LockedResource.access x (fun () ->
            x.PerformWrite(offset, size, data)
            writeFence.Enqueue()
        )

    member x.OnLock(usage : Option<ResourceUsage>) =
        match usage with
            | Some ResourceUsage.Render -> 
                beforeRender()
                writeFence.WaitGPU()
            | _ -> ()

    member x.OnUnlock(usage : Option<ResourceUsage>) =
        match usage with
            | Some ResourceUsage.Render ->
                afterRender()
            | _ -> ()

    member x.Lock = resourceLock

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface ILockedResource with
        member x.Lock = resourceLock
        member x.OnLock(usage) = x.OnLock usage
        member x.OnUnlock(usage) = x.OnUnlock usage

[<AutoOpen>]
module private SparseBufferImplementation = 
    type Page(buffer : int, offset : nativeint, size : nativeint, totalSize : ref<int64>) =
        
        let mutable refCount = 0
        let mutable fence : Fence = null

        let wait() =
            match fence with
                | null -> ()
                | f -> 
                    if f.IsSignaled then
                        f.Dispose()
                        fence <- null
                    else
                        f.WaitGPU()

        member x.IsCommitted = refCount > 0 
        
        member x.Commit() =
            lock x (fun () ->
                refCount <- refCount + 1
                if refCount = 1 then
                    Interlocked.Add(totalSize, int64 size) |> ignore
                    wait()
                    GL.NamedBufferPageCommitment(buffer, offset, size, true)
                    fence <-Fence.Create()
                else
                    wait()
            )
            
        member x.Decommit() =
            lock x (fun () ->
                refCount <- refCount - 1
                if refCount = 0 then
                    Interlocked.Add(totalSize, int64 -size) |> ignore
                    wait()
                    GL.NamedBufferPageCommitment(buffer, offset, size, false)
                    fence <- Fence.Create()
                else
                    wait()
            )

        member x.Commitment(c : bool) =
            if c then x.Commit()
            else x.Decommit()

    type RealSparseBuffer(ctx : Context, handle : int, totalSize : nativeint, pageSize : nativeint, beforeRender : unit -> unit, afterRender : unit -> unit) =
        inherit SparseBuffer(ctx, 0n, handle, beforeRender, afterRender)
        let committedSize = ref 0L

        let pages = 
            let pageCount = totalSize / pageSize |> int
            Array.init pageCount (fun pi -> 
                Page(handle, nativeint pi * pageSize, pageSize, committedSize)
            )

        override x.UsedMemory = Mem !committedSize

        override x.PerformWrite(offset : nativeint, size : nativeint, data : nativeint) =
            if offset < 0n || size < 0n || offset + size > totalSize then
                failwith "[GL] commitment region out of bounds"
            
            if size > 0n then
                GL.NamedBufferSubData(handle, offset, size, data)

        override x.Commitment(offset : nativeint, size : nativeint, c : bool) =
            if offset < 0n || size < 0n || offset + size > totalSize then
                failwith "[GL] commitment region out of bounds"

            if size > 0n then
                let lastByte = offset + size - 1n
                let firstPage = offset / pageSize |> int
                let lastPage = lastByte / pageSize |> int

                for pi in firstPage .. lastPage do
                    pages.[pi].Commitment(c)

    type FakeSparseBuffer(ctx : Context, handle : int, beforeRender : unit -> unit, afterRender : unit -> unit) =
        inherit SparseBuffer(ctx, 0n, handle, beforeRender, afterRender)

        let usedBytes = SortedSet<nativeint>()

        let changeCommitment(lastByte : nativeint, c : bool) =
            lock usedBytes (fun () ->
                if c then usedBytes.Add lastByte |> ignore
                else usedBytes.Remove lastByte |> ignore
                if usedBytes.Count = 0 then
                    0n
                else
                    usedBytes.Max + 1n |> int64 |> Fun.NextPowerOfTwo |> nativeint
            )

        member x.AdjustCapacity(c : nativeint) =
            if c <> x.SizeInBytes then
                LockedResource.update x (fun () ->
                    if c <> x.SizeInBytes then
                        let copySize = min c x.SizeInBytes

                        let temp = GL.GenBuffer()
                        GL.Check "could not create temp buffer"

                        GL.NamedBufferData(temp, copySize, 0n, BufferUsageHint.StaticCopy)
                        GL.Check "could not allocate temp buffer"

                        GL.NamedCopyBufferSubData(handle, temp, 0n, 0n, copySize)
                        GL.Check "could not copy to temp buffer"

                        GL.NamedBufferData(handle, c, 0n, BufferUsageHint.StaticDraw)
                        GL.Check "could not resize buffer"

                        GL.NamedCopyBufferSubData(temp, handle, 0n, 0n, copySize)
                        GL.Check "could not copy from temp buffer"

                        GL.DeleteBuffer(temp)
                        GL.Check "could not delete temp buffer"

                        x.SizeInBytes <- c
                )

        override x.Release() =
            usedBytes.Clear()

        override x.PerformWrite(offset : nativeint, size : nativeint, data : nativeint) =
            if offset < 0n || size < 0n || offset + size > x.SizeInBytes then
                failwith "[GL] commitment region out of bounds"
            
            if size > 0n then
                GL.NamedBufferSubData(handle, offset, size, data)

        override x.UsedMemory = Mem x.SizeInBytes

        override x.Commitment(offset : nativeint, size : nativeint, c : bool) =
            let lastByte = offset + size - 1n
            let c = changeCommitment(lastByte, c)
            x.AdjustCapacity(c)

[<AutoOpen>]
module SparseBufferExtensions =

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module private Alignment = 
        let inline prev (align : ^a) (v : ^a) =
            let r = v % align
            if r = LanguagePrimitives.GenericZero then v
            else v - r

        let inline next (align : ^a) (v : ^a) =
            let r = v % align
            if r = LanguagePrimitives.GenericZero then v
            else align + v - r       

    module private SparseHelpers =
        let rec tryAlloc (cap : byref<nativeint>) =
            let b = GL.GenBuffer()
            GL.Check "could not create buffer"

            GL.NamedBufferStorage(b, cap, 0n, BufferStorageFlags.SparseStorageBit ||| BufferStorageFlags.DynamicStorageBit)
            let err = GL.GetError()
            if err = ErrorCode.OutOfMemory then
                GL.DeleteBuffer(b)
                GL.Check "could not delete buffer"

                cap <- cap / 2n
                tryAlloc(&cap)
            else
                if err <> ErrorCode.NoError then
                    Log.warn "%A: could not allocate sparse buffer" err
                b

        let totalMemory (ctx : Context) =
            use __ = ctx.ResourceLock
            let size = 
                match ctx.Driver.device with
                    | GPUVendor.nVidia ->
                        nativeint (GL.GetInteger64(unbox 0x9047)) * 1024n
                    | GPUVendor.AMD ->
                        let pars : int[] = Array.zeroCreate 4
                        GL.GetInteger(unbox 0x87FB, pars)
                        nativeint pars.[0] * 1024n 
                    | _ ->
                        8n <<< 30

            match GL.GetError() with
                | ErrorCode.NoError -> size
                | _ -> 8n <<< 30

    type Context with
        member x.CreateSparseBuffer(beforeRender : unit -> unit, afterRender : unit -> unit) =
            use __ = x.ResourceLock

            if not RuntimeConfig.SupressSparseBuffers && GL.ARB_sparse_buffer then
                let pageSize = GL.GetInteger(GetPName.BufferPageSize) |> nativeint
                let pageSize = 16n * pageSize

                let memorySize = SparseHelpers.totalMemory x
                let mutable virtualSize = memorySize |> Alignment.next pageSize

                let handle = SparseHelpers.tryAlloc(&virtualSize)
                RealSparseBuffer(x, handle, virtualSize, pageSize, beforeRender, afterRender) :> SparseBuffer
            else
                let handle = GL.GenBuffer()
                GL.Check "could not create buffer"
        
                FakeSparseBuffer(x, handle, beforeRender, afterRender) :> SparseBuffer
      
        member x.CreateSparseBuffer() =
            x.CreateSparseBuffer(id, id)

        member x.Delete(b : SparseBuffer) =
            b.Dispose()