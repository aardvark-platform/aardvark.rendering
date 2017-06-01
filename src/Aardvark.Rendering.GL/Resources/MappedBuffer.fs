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

open Aardvark.Base.Native.NewImpl

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
            let ctx = Option.get ContextHandle.Current
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
            let ctx = Option.get ContextHandle.Current
            x.WaitGPU ctx

        member x.WaitCPU() =
            if handle <> 0n then
                match GL.ClientWaitSync(handle, ClientWaitSyncFlags.None, GL_TIMEOUT_IGNORED) with
                    | WaitSyncStatus.WaitFailed -> failwith "[GL] failed to wait for fence"
                    | WaitSyncStatus.TimeoutExpired -> failwith "[GL] fance timeout"
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


    [<AbstractClass>]
    type AbstractResizeBuffer(ctx : Context, handle : int, pageSize : int64) =
        inherit Buffer(ctx, 0n, handle)

        let resourceLock = new ResourceLock()

        let mutable pendingWrites : hmap<ContextHandle, Fence> = HMap.empty

        let afterWrite() =
            lock resourceLock (fun () ->
                let f = Fence.Create()

                pendingWrites <- 
                    pendingWrites |> HMap.update f.Context (fun old ->
                        match old with 
                            | Some o -> o.Dispose()
                            | None -> ()
                        f
                    )
            )

        let beforeResize() =
            if not (HMap.isEmpty pendingWrites) then
                for (_,f) in pendingWrites |> HMap.toSeq do 
                    f.WaitCPU()
                    f.Dispose()

                pendingWrites <- HMap.empty

        let afterResize() =
            use f = Fence.Create()
            f.WaitCPU()

        let beforeRead() =
            lock resourceLock (fun () ->
                if not (HMap.isEmpty pendingWrites) then
                    let handle = ctx.CurrentContextHandle |> Option.get
                    pendingWrites |> Seq.iter (fun (_, f) -> f.WaitGPU(handle))
            )
            

        member x.Lock = resourceLock

        interface ILockedResource with
            member x.Lock = resourceLock
            member x.OnLock u = x.OnLock u
            member x.OnUnlock u = x.OnUnlock u
        
        member x.OnLock (usage : Option<ResourceUsage>) =
            match usage with
                | Some ResourceUsage.Render -> beforeRead()
                | _ -> ()

        member x.OnUnlock (usage : Option<ResourceUsage>) = ()

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
                        beforeResize()
                        x.Realloc(oldCapacity, newCapacity)
                        afterResize()
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
                        beforeRead()
                        let res = x.MapRead(offset, size, reader)
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
                        afterWrite()
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

                GL.BufferData(BufferTarget.CopyWriteBuffer, copyBytes, 0n, BufferUsageHint.StaticDraw)
                GL.Check "[ResizeableBuffer] could not allocate buffer"

                GL.CopyBufferSubData(BufferTarget.CopyReadBuffer, BufferTarget.CopyWriteBuffer, 0n, 0n, copyBytes)
                GL.Check "[ResizeableBuffer] could not copy buffer"

                GL.BufferData(BufferTarget.CopyReadBuffer, newCapacity, 0n, BufferUsageHint.StaticDraw)
                GL.Check "[ResizeableBuffer] could not allocate buffer"

                GL.CopyBufferSubData(BufferTarget.CopyWriteBuffer, BufferTarget.CopyReadBuffer, 0n, 0n, copyBytes)
                GL.Check "[ResizeableBuffer] could not copy buffer"

                GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
                GL.Check "[ResizeableBuffer] could not unbind buffer"

                GL.DeleteBuffer(tmpBuffer)
                GL.Check "[ResizeableBuffer] could not delete buffer"

            else
                GL.BufferData(BufferTarget.CopyReadBuffer, newCapacity, 0n, BufferUsageHint.StaticDraw)
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
                        BufferStorageFlags.SparseStorageBit ||| BufferStorageFlags.DynamicStorageBit
                    )
                    GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)

                    SparseMemoryResizeBuffer(x, pageSize, buffer) :> AbstractResizeBuffer
                else
                    let buffer = GL.GenBuffer()
                    CopyResizeBuffer(x, buffer) :> AbstractResizeBuffer
            )

module ManagedBufferImplementation =
    
    [<AutoOpen>]
    module private SparseBufferImpl = 
        type private BufferPageCommitmentDel = delegate of BufferTarget * nativeint * nativeint * bool -> unit
        type private NamedBufferPageCommitmentDel = delegate of uint32 * nativeint * nativeint * bool -> unit

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

    module SparseBuffers =
        let supported() =
            SparseBufferImpl.init()
            SparseBufferImpl.supported

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
        let mutable store : hmap<ContextHandle, Fence> = HMap.empty


        member x.WaitCPU() =
            lock x (fun () ->
                let all = store
                store <- HMap.empty

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
                    store |> HMap.alter f.Context (fun o ->
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
            let b = GL.GenBuffer()
            GL.BindBuffer(BufferTarget.CopyWriteBuffer, b)
            GL.Check "could not bind buffer"

            GL.BufferStorage(BufferTarget.CopyWriteBuffer, nativeint cap, 0n, BufferStorageFlags.SparseStorageBit ||| BufferStorageFlags.DynamicStorageBit)
            let err = GL.GetError()
            GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
            if err = ErrorCode.OutOfMemory then
                GL.DeleteBuffer(b)
                cap <- cap / 2L
                alloc(&cap)
            else
                if err <> ErrorCode.NoError then
                    Log.warn "%A: could not allocate sparse buffer" err
                Log.line "buffer-size: %A" (Mem cap)
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
            SparseBufferImpl.init()
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
                    SparseGeometryPoolBuffer(ctx, b, nativeint virtualCapacity, nativeint pageSize, this), t, nativeint s
                )

            nativeint pageSize, handles

        let manager = MemoryManager.createNop()
        let fences = Fences(ctx)

        let mutable rendering = 0

        let mutable count = 0

        let mutable pendingFrees : list<managedptr> = []
        
        let free ( ptrs : list<managedptr>) =
            use __ = ctx.ResourceLock
            fences.WaitGPU()

            for (_,(b,_,s)) in Map.toSeq handles do
                GL.BindBuffer(BufferTarget.CopyWriteBuffer, b.Handle)
                GL.Check "[Pool] could not bind buffer"
                b.Commitment(ptrs, s, false)
                GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
                GL.Check "[Pool] could not unbind buffer"
                        
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

                GL.BindBuffer(BufferTarget.CopyWriteBuffer, buffer.Handle)
                GL.Check "[Pool] could not bind buffer"

                buffer.Commitment(o, s, true) 
                match g.IndexedAttributes.TryGetValue sem with
                    | (true, data) ->
                        assert(data.GetType().GetElementType() = t)
                        assert(data.Length >= int ptr.Size)

                        let gc = GCHandle.Alloc(data, GCHandleType.Pinned)
                        GL.BufferSubData(BufferTarget.CopyWriteBuffer, o, s, gc.AddrOfPinnedObject())
                        GL.Check (sprintf "[Pool] could not write to buffer %A" sem)
                        gc.Free()
                    | _ ->
                        ()
                        //Log.error "%s undefined" (string sem)

                GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
                GL.Check "[Pool] could not unbind buffer"

            fences.Enqueue()
            ptr

        member x.Free(ptr : managedptr) =
            Interlocked.Decrement(&count) |> ignore
            Interlocked.Change(&pendingFrees, fun l -> ptr :: l) |> ignore
            hasFrees.Set() |> ignore

        member x.TryGetBufferView(sem : Symbol) =
            match Map.tryFind sem handles with
                | Some (b,t,_) -> BufferView(Mod.constant (b :> IBuffer), t) |> Some
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
            member x.Free p = x.Free p
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
                    GL.BufferPageCommitment(BufferTarget.CopyWriteBuffer, offset, size, true)
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
                    GL.BufferPageCommitment(BufferTarget.CopyWriteBuffer, offset, size, false)
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


        member internal x.Commitment(ptrs : list<managedptr>, elementSize : nativeint, c : bool) =
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
                let b = GL.GenBuffer()
                GL.Check "could not generate buffer"
                ResizeGeometryPoolBuffer(this, ctx, b, rw, 0n), s, t
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
            if manager.LastUsedByte < 0n then
                0n
            else
                let res = int64 manager.LastUsedByte + 1L |> Fun.NextPowerOfTwo |> nativeint
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

        member x.Free(ptr : managedptr) =
            Interlocked.Decrement(&count) |> ignore
            Interlocked.Change(&pendingFrees, fun l -> ptr :: l) |> ignore
            hasFrees.Set() |> ignore

        member x.TryGetBufferView(sem : Symbol) =
            match Map.tryFind sem handles with
                | Some (b,s,t) -> BufferView(Mod.constant (b :> IBuffer), t) |> Some
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
            member x.Free p = x.Free p
            member x.TryGetBufferView sem = x.TryGetBufferView sem

    and private ResizeGeometryPoolBuffer(parent : ResizeGeometryPool, ctx : Context, handle : int, rw : ReaderWriterLockSlim, initialCap : nativeint) =
        inherit Buffer(ctx, initialCap, handle)

        let resourceLock = new ResourceLock()

        member internal x.resize(newCapacity : nativeint) =
            if newCapacity <> x.SizeInBytes then
                let copySize = min x.SizeInBytes newCapacity
                x.SizeInBytes <- newCapacity

                if copySize > 0n then
                    // bind the current buffer to read
                    GL.BindBuffer(BufferTarget.CopyReadBuffer, handle)
                    GL.Check "could not bind buffer"

                    // allocate a temp buffer
                    let temp = GL.GenBuffer()
                    GL.BindBuffer(BufferTarget.CopyWriteBuffer, temp)
                    GL.Check "could not bind temp-buffer"
                    GL.BufferData(BufferTarget.CopyWriteBuffer, newCapacity, 0n, BufferUsageHint.StaticDraw)
                    GL.Check "could not allocate temp-buffer"

                    // copy data to temp
                    GL.CopyBufferSubData(BufferTarget.CopyReadBuffer, BufferTarget.CopyWriteBuffer, 0n, 0n, copySize)
                    GL.Check (sprintf "could not copy buffer (size: %A)" copySize)

                    // resize the original buffer
                    GL.BufferData(BufferTarget.CopyReadBuffer, newCapacity, 0n, BufferUsageHint.StaticDraw)
                    GL.Check "could not reallocate buffer"

                    // copy data back
                    GL.CopyBufferSubData(BufferTarget.CopyWriteBuffer, BufferTarget.CopyReadBuffer, 0n, 0n, copySize)
                    GL.Check "could not copy buffer"

                    // cleanup
                    GL.BindBuffer(BufferTarget.CopyReadBuffer, 0)
                    GL.Check "could not unbind buffer"
                    GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
                    GL.Check "could not unbind temp-buffer"
                    GL.DeleteBuffer(temp)
                    GL.Check "could not delete temp-buffer"

                else
                    GL.BindBuffer(BufferTarget.CopyWriteBuffer, handle)
                    GL.Check "could not bind buffer"
                    GL.BufferData(BufferTarget.CopyWriteBuffer, newCapacity, 0n, BufferUsageHint.StaticDraw)
                    GL.Check "could not resize buffer"
                    GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
                    GL.Check "could not unbind buffer"


        member internal x.Resize(newCapacity : nativeint) =
            if x.SizeInBytes <> newCapacity then
                if x.SizeInBytes <> newCapacity then
                    use __ = ctx.ResourceLock
                    x.resize newCapacity

        member internal x.Write(offset : nativeint, size : nativeint, data : nativeint) =
            use __ = ctx.ResourceLock

            GL.BindBuffer(BufferTarget.CopyWriteBuffer, handle)
            GL.Check "could not bind buffer"

            GL.BufferSubData(BufferTarget.CopyWriteBuffer, offset, size, data)
            GL.Check "could not upload buffer"

            GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
            GL.Check "could not unbind buffer"


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



// =========================================================================
// Historical monsters


module MappedBufferImplementations = 

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


        override x.Compute(token) =
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
            member x.OnLock u = ()
            member x.OnUnlock u = ()

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

    let createCount() =
        let ptr = NativePtr.alloc 1
        NativePtr.write ptr 0
        ptr

    let mutable capacity = 0
    let count : nativeptr<int> = createCount()
    
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
    override x.Compute(token) =
        let inner = buffer.GetValue(token) |> unbox<Buffer>
        IndirectBuffer(inner, count, 20, indexed) :> IIndirectBuffer

    interface ILockedResource with
        member x.Lock = buffer.Lock
        member x.OnLock u = ()
        member x.OnUnlock u = ()

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


