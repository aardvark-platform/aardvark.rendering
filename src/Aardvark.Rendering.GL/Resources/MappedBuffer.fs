﻿namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Management
open FSharp.Data.Adaptive
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL

#nowarn "9"
#nowarn "51"


[<AutoOpen>]
module ResizeBufferImplementation =
    
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

    [<Literal>]
    let private GL_TIMEOUT_IGNORED = 0xFFFFFFFFFFFFFFFFUL

    [<AllowNullLiteral>]
    type Fence private(ctx : ContextHandle, handle : nativeint) =
        let mutable handle = handle

        member x.Context = ctx
        
        member x.IsSignaled =
            if handle = 0n then
                true
            else
                let status = GL.ClientWaitSync(handle, ClientWaitSyncFlags.None, 0L)
                status = WaitSyncStatus.AlreadySignaled

        static member Create() =
            let ctx = ValueOption.get ContextHandle.Current
            let f = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, WaitSyncFlags.None)
            GL.Check "could not enqueue fence"
            GL.Flush()
            GL.Check "could not flush"
            new Fence(ctx, f)

        member x.WaitGPU(current : ContextHandle) =
            let handle = handle
            if handle <> 0n then
                if ctx <> current then 
                    GL.WaitSync(handle, WaitSyncFlags.None, GL_TIMEOUT_IGNORED) |> ignore
                    GL.Check "could not enqueue wait"

            else
                Log.warn "waiting on disposed fence"

        member x.WaitGPU() =
            let ctx = ValueOption.get ContextHandle.Current
            x.WaitGPU ctx

        member x.WaitCPU() =
            if handle <> 0n then
                match GL.ClientWaitSync(handle, ClientWaitSyncFlags.None, GL_TIMEOUT_IGNORED) with
                    | WaitSyncStatus.WaitFailed -> failwith "[GL] failed to wait for fence"
                    | WaitSyncStatus.TimeoutExpired -> failwith "[GL] fence timeout"
                    | _ -> ()
                GL.Check "could not wait for fence"
            else
                Log.warn "waiting on disposed fence"

        member x.Dispose() = 
            let o = Interlocked.Exchange(&handle, 0n)
            if o <> 0n then 
                GL.DeleteSync(o)
                GL.Check "could not delete fence"

        interface IDisposable with
            member x.Dispose() = x.Dispose()


module ManagedBufferImplementation =

    module SparseBuffers =
        let supported() = GLExt.ARB_sparse_buffer

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

    type Fences(ctx : Context) =
        let mutable store : HashMap<ContextHandle, Fence> = HashMap.empty


        member x.WaitCPU() =
            lock x (fun () ->
                let all = store
                store <- HashMap.empty

                for (_,f) in all do 
                    f.WaitCPU()
                    f.Dispose()
            )

        member x.WaitGPU() =
            let mine = ctx.CurrentContextHandle.Value
            lock x (fun () ->
                for (_,f) in store do f.WaitGPU(mine) |> ignore
            )

        member x.Enqueue() =
            let f = Fence.Create()
            lock x (fun () ->
                store <-
                    store |> HashMap.alter f.Context (fun o ->
                        match o with
                            | Some o -> o.Dispose()
                            | None -> ()
                        Some f
                    )
            )



    type SparsePoolOperation =
        | Write of managedptr * IndexedGeometry
        | Delete of managedptr

    
    [<AutoOpen>]
    module private Allocator = 
        let rec alloc (cap : byref<int64>) =
            let b = GLExt.CreateBuffer()
            GL.Check "could not create buffer"

            GLExt.NamedBufferStorage(b, nativeint cap, 0n, BufferStorageFlags.SparseStorageBit ||| BufferStorageFlags.DynamicStorageBit)
            let err = GL.GetError()
            if err = ErrorCode.OutOfMemory then
                GL.DeleteBuffer(b)
                GL.Check "could not delete buffer"

                cap <- cap / 2L
                alloc(&cap)
            else
                if err <> ErrorCode.NoError then
                    Log.warn "%A: could not allocate sparse buffer" err
                b


    module private Memory=
        let total (ctx : Context) =
            use __ = ctx.ResourceLock
            let size = 
                match ctx.Driver.device with
                    | GPUVendor.nVidia ->
                        GL.GetInteger64(unbox 0x9047) * 1024L |> Mem
                    | GPUVendor.AMD ->
                        let pars : int[] = Array.zeroCreate 4
                        GL.GetInteger(unbox 0x87FB, pars)
                        int64 pars.[0] * 1024L |> Mem
                    | _ ->
                        8L <<< 30 |> Mem

            match GL.GetError() with
                | ErrorCode.NoError -> size
                | _ -> Mem (8L <<< 30)

    type SparseGeometryPool(ctx : Context, types : Map<Symbol, Type>) as this =
        let pageSize, handles =
            use __ = ctx.ResourceLock

            let total = Memory.total ctx
            let pageSize = GL.GetInteger(GetPName.BufferPageSize) |> int64
            let pageSize = 16L * pageSize
            

            let handles = 
                types |> Map.map (fun sem t ->
                    let s = t.GLSize
                    let mutable virtualCapacity = total.Bytes |> Alignment.next pageSize
                
                    let d = GL.IsEnabled(EnableCap.DebugOutput)
                    if d then GL.Disable(EnableCap.DebugOutput)
                    let b = alloc(&virtualCapacity)
                    if d then GL.Enable(EnableCap.DebugOutput)
                    new SparseGeometryPoolBuffer(ctx, b, nativeint virtualCapacity, nativeint pageSize, this), t, nativeint s
                )

            nativeint pageSize, handles

        let manager = MemoryManager.createNop()
        let fences = Fences(ctx)

        let mutable rendering = 0

        let mutable count = 0

        let mutable pendingFrees : list<Block<unit>> = []
        
        let free ( ptrs : list<Block<unit>>) =
            use __ = ctx.ResourceLock
            fences.WaitGPU()

            for (_,(b,_,s)) in Map.toSeq handles do
                b.Commitment(ptrs, s, false)
                        
            fences.Enqueue()

            for f in ptrs do
                manager.Free f
        
        let notRendering = new ManualResetEventSlim(true)
        let hasFrees = new AutoResetEvent(false)

        let freeThread =
            new Thread(ThreadStart(fun () ->
                while true do
                    WaitHandle.WaitAll([| hasFrees; notRendering.WaitHandle |]) |> ignore
                    
                    let frees = Interlocked.Exchange(&pendingFrees, [])
                    free frees
            ), IsBackground = true)

        do freeThread.Start()

                
        member internal x.BeforeRender() =
            if Interlocked.Increment(&rendering) = 1 then
                notRendering.Reset()

            fences.WaitGPU()

        member internal x.AfterRender() =
            if Interlocked.Decrement(&rendering) = 0 then
                notRendering.Set()
                
        member x.Alloc(fvc : int, g : IndexedGeometry) =
            let ptr = manager.Alloc(nativeint fvc)
            use __ = ctx.ResourceLock
            Interlocked.Increment(&count) |> ignore

            for (sem, (buffer, t, es)) in Map.toSeq handles do
                let o = es * ptr.Offset
                let s = es * ptr.Size

                buffer.Commitment(o, s, true) 
                match g.IndexedAttributes.TryGetValue sem with
                    | (true, data) ->
                        assert(data.GetType().GetElementType() = t)
                        assert(data.Length >= int ptr.Size)

                        let gc = GCHandle.Alloc(data, GCHandleType.Pinned)
                        GLExt.NamedBufferSubData(buffer.Handle, o, s, gc.AddrOfPinnedObject())
                        GL.Check (sprintf "[Pool] could not write to buffer %A" sem)
                        gc.Free()
                    | _ ->
                        ()

            fences.Enqueue()
            ptr

        member x.Free(ptr : Block<unit>) =
            Interlocked.Decrement(&count) |> ignore
            Interlocked.Change(&pendingFrees, fun l -> ptr :: l) |> ignore
            hasFrees.Set() |> ignore

        member x.TryGetBufferView(sem : Symbol) =
            match Map.tryFind sem handles with
                | Some (b,t,_) -> BufferView(AVal.constant (b :> IBuffer), t) |> Some
                | _ -> None

        member x.Dispose() =
            use __ = ctx.ResourceLock
            for (_,(b,_,_)) in Map.toSeq handles do
                ctx.Delete b

        interface IGeometryPool with
            member x.Count = count
            member x.UsedMemory = handles |> Map.toSeq |> Seq.sumBy (fun (_,(b,_,_)) -> b.UsedMemory)
            member x.Dispose() = x.Dispose()
            member x.Alloc(c,g) = x.Alloc(c,g)
            member x.Free p = x.Free ( p )
            member x.TryGetBufferView sem = x.TryGetBufferView sem

    and private Page(b : int, offset : nativeint, size : nativeint, totalSize : ref<int64>) =
        
        let mutable refCount = 0
        let mutable fence : Fence = null
//        let mutable fence : Fence = null
//
        let wait() =
            match fence with
                | null -> ()
                | f -> 
                    if f.IsSignaled then
                        f.Dispose()
                        fence <- null
                    else
                        f.WaitGPU(ContextHandle.Current.Value)

        member x.IsCommitted = refCount > 0 

        member x.Commit() =
            lock x (fun () ->
                refCount <- refCount + 1
                if refCount = 1 then
                    Interlocked.Add(totalSize, int64 size) |> ignore
                    wait()
                    GLExt.NamedBufferPageCommitment(b, offset, size, true)
                    GL.Sync()
                    let f = Fence.Create()
                    fence <- f
                else
                    wait()
            )
            
        member x.Decommit() =
            lock x (fun () ->
                refCount <- refCount - 1
                if refCount = 0 then
                    Interlocked.Add(totalSize, int64 -size) |> ignore
                    wait()
                    GLExt.NamedBufferPageCommitment(b, offset, size, false)
                    let f = Fence.Create()
                    fence <- f
                else
                    wait()
            )

        member x.Commitment(c : bool) =
            if c then x.Commit()
            else x.Decommit()

    and private SparseGeometryPoolBuffer(ctx : Context, handle : int, totalSize : nativeint, pageSize : nativeint, parent : SparseGeometryPool) =
        inherit Buffer(ctx, 0n, handle)
        let resourceLock = new ResourceLock()
        let committedSize = ref 0L
        let pageRef : Page[] = Array.init (totalSize / pageSize |> int) (fun pi -> Page(handle, nativeint pi * pageSize, pageSize, committedSize))

        member internal x.Commitment(offset : nativeint, size : nativeint, c : bool) =
            let ctx = ctx.CurrentContextHandle.Value
            let lastByte = offset + size - 1n
            let firstPage = offset / pageSize |> int
            let lastPage = lastByte / pageSize |> int

            for pi in firstPage .. lastPage do
                pageRef.[pi].Commitment(c)


        member internal x.Commitment(ptrs : list<Management.Block<unit>>, elementSize : nativeint, c : bool) =
            let ctx = ctx.CurrentContextHandle.Value
            
            for ptr in ptrs do
                let offset = ptr.Offset * elementSize
                let size = ptr.Size * elementSize
                let lastByte = offset + size - 1n
                let firstPage = offset / pageSize |> int
                let lastPage = lastByte / pageSize |> int

                for pi in firstPage .. lastPage do
                    pageRef.[pi].Commitment(c)


        member x.UsedMemory = Mem !committedSize

        interface ILockedResource with
            member x.Lock = resourceLock
            member x.OnLock(c) =
                match c with
                    | Some ResourceUsage.Render -> parent.BeforeRender()
                    | _ -> ()
            member x.OnUnlock(c) =
                match c with
                    | Some ResourceUsage.Render -> parent.AfterRender()
                    | _ -> ()


    type ResizeGeometryPool(ctx : Context, types : Map<Symbol, Type>) as this =
        let minCapacity = 1n <<< 20
        let rw = new ReaderWriterLockSlim()
        let handles =
            use __ = ctx.ResourceLock
            let total = Memory.total ctx

            types |> Map.map (fun sem t ->
                let s = nativeint t.GLSize
                let b = GLExt.CreateBuffer()
                GL.Check "could not generate buffer"
                new ResizeGeometryPoolBuffer(this, ctx, b, rw, 0n), s, t
            )

        let manager = MemoryManager.createNop()
        let fences = Fences(ctx)
        let notRendering = new ManualResetEventSlim(true)

        let mutable pendingFrees = []
        let hasFrees = new AutoResetEvent(false)

        let freeThread =
            new Thread(ThreadStart(fun () ->
                while true do
                    WaitHandle.WaitAll([| hasFrees; notRendering.WaitHandle |]) |> ignore
                    let frees = Interlocked.Exchange(&pendingFrees, [])
                    for f in frees do manager.Free f
                    match frees with
                        | [] -> ()
                        | _ -> this.AdjustSizes()

            ), IsBackground = true)

        do freeThread.Start()
        let mutable rendering = 0
        let mutable count = 0

        let cap() =
            if manager.Capactiy < 0n then
                0n
            else
                let res = int64 manager.Capactiy + 1L |> Fun.NextPowerOfTwo |> nativeint
                max res minCapacity

        member internal x.BeforeRender() =
            if Interlocked.Increment(&rendering) = 1 then
                notRendering.Reset()
                rw.EnterReadLock()

            
            fences.WaitGPU()

        member internal x.AfterRender() =
            if Interlocked.Decrement(&rendering) = 0 then
                notRendering.Set()
                rw.ExitReadLock()

        member internal x.AdjustSizes() =
            use __ = ctx.ResourceLock
            fences.WaitCPU()
            ReaderWriterLock.write rw (fun () ->
                let newCapacity = cap()

                for (sem, (buffer, es, t)) in Map.toSeq handles do
                    let c = es * newCapacity
                    buffer.Resize c

                GL.Sync()
            )

        member x.Alloc(fvc : int, g : IndexedGeometry) =
            let ptr = manager.Alloc(nativeint fvc)

            use __ = ctx.ResourceLock

            x.AdjustSizes()

            Interlocked.Increment(&count) |> ignore
            fences.WaitGPU()
            ReaderWriterLock.read rw (fun () ->
                for (sem, (buffer, es, t)) in Map.toSeq handles do
                    let o = es * ptr.Offset
                    let s = es * ptr.Size

                    match g.IndexedAttributes.TryGetValue sem with
                        | (true, data) ->
                            assert(data.GetType().GetElementType() = t)
                            assert(data.Length >= int ptr.Size)

                            let gc = GCHandle.Alloc(data, GCHandleType.Pinned)
                        
                            buffer.Write(o, s, gc.AddrOfPinnedObject())
                            GL.Check (sprintf "[Pool] could not write to buffer %A" sem)
                            gc.Free()
                        | _ ->
                            ()
                            //Log.error "%s undefined" (string sem)


                fences.Enqueue()
            )
            ptr

        member x.Free(ptr : Block<unit>) =
            Interlocked.Decrement(&count) |> ignore
            Interlocked.Change(&pendingFrees, fun l -> ptr :: l) |> ignore
            hasFrees.Set() |> ignore

        member x.TryGetBufferView(sem : Symbol) =
            match Map.tryFind sem handles with
                | Some (b,s,t) -> BufferView(AVal.constant (b :> IBuffer), t) |> Some
                | _ -> None

        member x.Dispose() =
            use __ = ctx.ResourceLock
            for (_,(b,_,_)) in Map.toSeq handles do
                ctx.Delete b

        interface IGeometryPool with
            member x.Count = count
            member x.UsedMemory = handles |> Map.toSeq |> Seq.sumBy (fun (_,(b,_,_)) -> b.SizeInBytes) |> Mem
            member x.Dispose() = x.Dispose()
            member x.Alloc(c,g) = x.Alloc(c,g)
            member x.Free p = x.Free (p)
            member x.TryGetBufferView sem = x.TryGetBufferView sem

    and private ResizeGeometryPoolBuffer(parent : ResizeGeometryPool, ctx : Context, handle : int, rw : ReaderWriterLockSlim, initialCap : nativeint) =
        inherit Buffer(ctx, initialCap, handle)

        let resourceLock = new ResourceLock()

        member internal x.resize(newCapacity : nativeint) =
            if newCapacity <> x.SizeInBytes then
                let copySize = min x.SizeInBytes newCapacity
                x.SizeInBytes <- newCapacity

                if copySize > 0n then
                    // allocate a temp buffer
                    let temp = GLExt.CreateBuffer()
                    GL.Check "could not create temp-buffer"
                    GLExt.NamedBufferData(temp, copySize, 0n, BufferUsageHint.StaticDraw)
                    GL.Check "could not allocate temp-buffer"

                    // copy data to temp
                    GLExt.CopyNamedBufferSubData(handle, temp, 0n, 0n, copySize)
                    GL.Check (sprintf "could not copy buffer (size: %A)" copySize)
                    
                    // resize the original buffer
                    GLExt.NamedBufferData(handle, newCapacity, 0n, BufferUsageHint.StaticDraw)
                    GL.Check "could not reallocate buffer"
                    
                    // copy data back
                    GLExt.CopyNamedBufferSubData(temp, handle, 0n, 0n, copySize)
                    GL.Check "could not copy buffer"

                    // delete the temp buffer
                    GL.DeleteBuffer(temp)
                    GL.Check "could not delete temp-buffer"

                else
                    GLExt.NamedBufferData(handle, newCapacity, 0n, BufferUsageHint.StaticDraw)
                    GL.Check "could not resize buffer"


        member internal x.Resize(newCapacity : nativeint) =
            if x.SizeInBytes <> newCapacity then
                if x.SizeInBytes <> newCapacity then
                    use __ = ctx.ResourceLock
                    x.resize newCapacity

        member internal x.Write(offset : nativeint, size : nativeint, data : nativeint) =
            use __ = ctx.ResourceLock

            GLExt.NamedBufferSubData(handle, offset, size, data) 
            GL.Check "could not upload buffer"



        interface ILockedResource with
            member x.Lock = resourceLock
            member x.OnLock(c) =
                match c with
                    | Some ResourceUsage.Render -> parent.BeforeRender()
                    | _ -> ()
            member x.OnUnlock(c) =
                match c with
                    | Some ResourceUsage.Render -> parent.AfterRender()
                    | _ -> ()

