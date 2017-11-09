namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Runtime.InteropServices
open System.Collections.Generic
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Rendering.GL

open Aardvark.Base.Management


[<AbstractClass>]
type SparseBuffer(ctx : Context, size : nativeint, handle : int, beforeRender : unit -> unit, afterRender : unit -> unit) =
    inherit Buffer(ctx, size, handle)
    
    //let writeFence = FenceSet()
    let resourceLock = ResourceLock()

    abstract member UsedMemory : Mem
    abstract member Commitment : offset : nativeint * size : nativeint * commit : bool -> unit
    abstract member PerformWrite : offset : nativeint * size : nativeint * data : nativeint -> unit

    abstract member Release : unit -> unit
    default x.Release() = ()

    //member x.WriteFences = writeFence

    member x.Dispose() =
        use __ = ctx.ResourceLock
        x.Release()
        GL.DeleteBuffer(handle)
        GL.Check "could not delete buffer"

    member x.WriteUnsafe(offset : nativeint, size : nativeint, data : nativeint) =
        use __ = ctx.ResourceLock
        x.PerformWrite(offset, size, data)

    member x.Write(offset : nativeint, size : nativeint, data : nativeint) =
        use __ = ctx.ResourceLock
        x.PerformWrite(offset, size, data)
        //writeFence.Enqueue()

    member x.OnLock(usage : Option<ResourceUsage>) =
        match usage with
            | Some ResourceUsage.Render -> 
                beforeRender()
                //writeFence.WaitGPU()
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
        let mutable blaSize = 0L

        let pages = 
            let pageCount = totalSize / pageSize |> int
            Array.init pageCount (fun pi -> 
                Page(handle, nativeint pi * pageSize, pageSize, committedSize)
            )

        override x.UsedMemory = Mem !committedSize //blaSize

        override x.PerformWrite(offset : nativeint, size : nativeint, data : nativeint) =
            if offset < 0n || size < 0n || offset + size > totalSize then
                failwith "[GL] commitment region out of bounds"
            

            if size > 0n then
                GL.NamedBufferSubData(handle, offset, size, data)

        override x.Commitment(offset : nativeint, size : nativeint, c : bool) =
            if offset < 0n || size < 0n || offset + size > totalSize then
                failwith "[GL] commitment region out of bounds"
                
            let delta =
                if c then int64 size
                else int64 -size
            Interlocked.Add(&blaSize, delta) |> ignore

            if size > 0n then
                let lastByte = offset + size - 1n
                let firstPage = offset / pageSize |> int
                let lastPage = lastByte / pageSize |> int

                for pi in firstPage .. lastPage do
                    pages.[pi].Commitment(c)

    type FakeSparseBuffer(ctx : Context, handle : int, beforeRender : unit -> unit, afterRender : unit -> unit) =
        inherit SparseBuffer(ctx, 0n, handle, beforeRender, afterRender)
        let lockObj = obj()

        let mutable usedBytes = MapExt.empty
        let writeFences = FenceSet()

        let changeCommitment(lastByte : nativeint, c : bool) =
            let key = int64 lastByte
            usedBytes <-
                usedBytes |> MapExt.alter key (fun s ->
                    let s = Option.defaultValue 0 s
                    let n = if c then s + 1 else s - 1
                    if n > 0 then
                        Some n
                    else
                        None
                )

            match MapExt.tryMax usedBytes with
                | Some mv ->
                    mv + 1L |> Fun.NextPowerOfTwo |> nativeint
                | _ ->
                    0n

        member x.AdjustCapacity(c : nativeint) =
            if c <> x.SizeInBytes then
                lock x (fun () ->
                    if c < x.SizeInBytes then Log.warn "shrink: %A where (x=%A)" (Mem c) (Mem x.SizeInBytes)
                    else  Log.warn "grow: %A where (x=%A)" (Mem c) (Mem x.SizeInBytes)
                    writeFences.WaitCPU()
                    let copySize = min c x.SizeInBytes

                    if copySize > 0n then
                        let temp = GL.GenBuffer()
                        GL.Check "could not create temp buffer"

                        GL.NamedBufferData(temp, copySize, 0n, BufferUsageHint.StaticDraw)
                        GL.Check "could not allocate temp buffer"

                        GL.NamedCopyBufferSubData(handle, temp, 0n, 0n, copySize)
                        GL.Check "could not copy to temp buffer"

                        //GL.InvalidateBufferData(handle) // bad on intel and not necessary.
                        //GL.Check "could not invalidate buffer"

                        GL.NamedBufferData(handle, c, 0n, BufferUsageHint.StaticDraw)
                        GL.Check "could not resize buffer"
                    
                        GL.NamedCopyBufferSubData(temp, handle, 0n, 0n, copySize)
                        GL.Check "could not copy from temp buffer"

                        GL.DeleteBuffer(temp)
                        GL.Check "could not delete temp buffer"

                        
                    else
                        GL.NamedBufferData(handle, c, 0n, BufferUsageHint.StaticDraw)
                        GL.Check "could not resize buffer"

                    x.SizeInBytes <- c
                    GL.Sync()
                )

        override x.Release() =
            usedBytes <- MapExt.empty

        override x.PerformWrite(offset : nativeint, size : nativeint, data : nativeint) =
            lock x (fun () ->
                if offset < 0n || size < 0n || offset + size > x.SizeInBytes then
                    failwith "[GL] write region out of bounds"
            
                if size > 0n then
                    GL.NamedBufferSubData(handle, offset, size, data)

                writeFences.Enqueue()
            )

        override x.UsedMemory = Mem x.SizeInBytes

        override x.Commitment(offset : nativeint, size : nativeint, c : bool) =
            let lastByte = offset + size - 1n
            lock lockObj (fun () ->
                let c = changeCommitment(lastByte, c)
                x.AdjustCapacity(c)
            )

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
                let pageSize = 2n * pageSize

                let memorySize = SparseHelpers.totalMemory x
                let mutable virtualSize = memorySize |> Alignment.next pageSize

                let handle = SparseHelpers.tryAlloc(&virtualSize)
                new RealSparseBuffer(x, handle, virtualSize, pageSize, beforeRender, afterRender) :> SparseBuffer
            else
                let handle = GL.GenBuffer()
                GL.Check "could not create buffer"
        
                new FakeSparseBuffer(x, handle, beforeRender, afterRender) :> SparseBuffer
      
        member x.CreateSparseBuffer() =
            x.CreateSparseBuffer(id, id)

        member x.Delete(b : SparseBuffer) =
            b.Dispose()


type SparseBufferGeometryPool(ctx : Context, types : Map<Symbol, Type>) =
    
    let mutable count = 0
    let mutable rendering = 0
    let notRendering = new ManualResetEventSlim(true)
    let hasFrees = new ManualResetEventSlim(false)
    let mutable pendingFrees : list<Block<unit>> = []
    let fences = FenceSet()

    let beforeRender() =
        if Interlocked.Increment(&rendering) = 1 then
            notRendering.Reset()
            fences.WaitGPU()

    let afterRender() =
        if Interlocked.Decrement(&rendering) = 0 then
            notRendering.Set()

    let buffers = 
        types |> Map.map (fun sem t ->
            let s = nativeint t.GLSize
            ctx.CreateSparseBuffer(beforeRender, afterRender),t,s
        )
        
    let manager = MemoryManager.createNop()
        
    let free ( ptrs : list<Block<unit>>) =
        use __ = ctx.ResourceLock

        for (_,(b,_,s)) in Map.toSeq buffers do
            for p in ptrs do
                b.Commitment(p.Offset * s, p.Size * s, false)

        for f in ptrs do
            manager.Free f

    let freeThread =
        new Thread(ThreadStart(fun () ->
            try
                while true do
                    WaitHandle.WaitAll([| hasFrees.WaitHandle |]) |> ignore
                    hasFrees.Reset()

                    let frees = Interlocked.Exchange(&pendingFrees, [])
                    free frees
            with e -> 
                Log.error "[SparseBuffer] free thread gone down: %A" e.Message
        ), IsBackground = true)

    do freeThread.Start()

    member x.Alloc(fvc : int, g : IndexedGeometry) =
        let ptr = manager.Alloc(nativeint fvc)

        use __ = ctx.ResourceLock
        Interlocked.Increment(&count) |> ignore

        for (sem, (buffer, t, es)) in Map.toSeq buffers do
            let o = es * ptr.Offset
            let s = es * ptr.Size

            buffer.Commitment(o, s, true) 
            match g.IndexedAttributes.TryGetValue sem with
                | (true, data) ->
                    assert(data.GetType().GetElementType() = t)
                    assert(data.Length >= int ptr.Size)

                    let gc = GCHandle.Alloc(data, GCHandleType.Pinned)
                    buffer.Write(o, s, gc.AddrOfPinnedObject())
                    gc.Free()

                | _ ->
                    ()
        GL.Sync()
        ptr

    member x.Free(ptr : Block<unit>) =
        Interlocked.Decrement(&count) |> ignore
        if notRendering.IsSet then
            free [ptr]
        else
            Interlocked.Change(&pendingFrees, fun l -> ptr :: l) |> ignore
            hasFrees.Set() |> ignore

    member x.TryGetBufferView(sem : Symbol) =
        match Map.tryFind sem buffers with
            | Some (b,t,_) -> BufferView(Mod.constant (b :> IBuffer), t) |> Some
            | _ -> None

    member x.Dispose() =
        use __ = ctx.ResourceLock
        for (_,(b,_,_)) in Map.toSeq buffers do
            b.Dispose()

    interface IGeometryPool with
        member x.Count = count
        member x.UsedMemory = buffers |> Map.toSeq |> Seq.sumBy (fun (_,(b,_,_)) -> b.UsedMemory)
        member x.Dispose() = x.Dispose()
        member x.Alloc(c,g) = x.Alloc(c,g)
        member x.Free p = x.Free p
        member x.TryGetBufferView sem = x.TryGetBufferView sem

