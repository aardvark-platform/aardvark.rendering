open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.Base.Incremental.Operators
open Aardvark.SceneGraph
open Aardvark.Application

#nowarn "9"

open System.Threading.Tasks
open System
open System.Collections.Generic
open System.Threading

type LimitedConcurrencyLevelTaskScheduler (priority : ThreadPriority, maxDegreeOfParallelism : int) as this =
    inherit TaskScheduler()

    let sem = new SemaphoreSlim(0)
    let queue = ConcurrentHashQueue<Task>()
    let shutdown = new CancellationTokenSource()
    //let mutable activeCount = 0


    let run() =
        let mutable item = null
        try
            while not shutdown.IsCancellationRequested do
                sem.Wait(shutdown.Token)
                if queue.TryDequeue(&item) then
                    this.TryExecuteTask(item) |> ignore
                    item <- null
        with :? OperationCanceledException ->
            ()

    //let mutable waitCallback = Unchecked.defaultof<WaitCallback>

    //let runItem (state : obj) =
    //    let task = unbox<Task> state
    //    this.TryExecuteTask task |> ignore
    //    if Interlocked.Decrement(&activeCount) < maxDegreeOfParallelism then
    //        match queue.TryDequeue() with
    //            | (true, item) -> 
    //                ThreadPool.UnsafeQueueUserWorkItem(waitCallback, task) |> ignore
    //            | _ -> 
    //                ()

    //do waitCallback <- WaitCallback(runItem)

    let workers =
        Array.init maxDegreeOfParallelism (fun i ->
            let t = Thread(ThreadStart(run))
            t.IsBackground <- true
            t.Priority <- priority
            t.Name <- sprintf "Worker%d" i
            t.Start()
            t
        )

    member x.TryExecuteTask(item) : bool = base.TryExecuteTask(item)

    override x.QueueTask(task : Task) = 
        //ThreadPool.UnsafeQueueUserWorkItem(WaitCallback(runItem), (x,task)) |> ignore
        if queue.Enqueue(task) then
            sem.Release() |> ignore

    override x.GetScheduledTasks() = 
        Seq.empty

    override x.TryExecuteTaskInline(task : Task, taskWasPreviouslyQueued : bool) =
        if not taskWasPreviouslyQueued then
            Log.warn "inline"
            x.TryExecuteTask task |> ignore
            true
        else
            if queue.Remove task then
                Log.warn "inline"
                x.TryExecuteTask task |> ignore
                true
            else
                false

    override x.TryDequeue(task : Task) =
        if queue.Remove(task) then
            Log.warn "removed yeah"
            true
        else
            false

    override x.MaximumConcurrencyLevel = 
        maxDegreeOfParallelism

module Bla =
    open System
    open FShade
    open FShade.GLSL
    open OpenTK.Graphics
    open OpenTK.Graphics.OpenGL4
    open Aardvark.Rendering.GL
    open System.Runtime.InteropServices
    open Aardvark.Base.Management
    open Microsoft.FSharp.NativeInterop
    open System.Threading
    open Aardvark.Base.Runtime

    [<ReflectedDefinition>]
    module CullingShader =
        open FShade

        //typedef  struct {
        //    uint  count;
        //    uint  primCount;
        //    uint  firstIndex;
        //    uint  baseVertex;
        //    uint  baseInstance;
        //} DrawElementsIndirectCommand;
        type DrawInfo =
            struct
                val mutable public FaceVertexCount : int
                val mutable public InstanceCount : int
                val mutable public FirstIndex : int
                val mutable public BaseVertex : int
                val mutable public FirstInstance : int
            end

        type CullingInfo =
            struct
                val mutable public Min : V4f
                val mutable public Max : V4f
            end
        module CullingInfo =
            let instanceCount (i : CullingInfo) =
                int i.Min.W
                
            let getMinMaxInDirection (v : V3d) (i : CullingInfo) =
                let mutable l = V3d.Zero
                let mutable h = V3d.Zero

                if v.X >= 0.0 then
                    l.X <- float i.Min.X
                    h.X <- float i.Max.X
                else
                    l.X <- float i.Max.X
                    h.X <- float i.Min.X
                    
                if v.Y >= 0.0 then
                    l.Y <- float i.Min.Y
                    h.Y <- float i.Max.Y
                else
                    l.Y <- float i.Max.Y
                    h.Y <- float i.Min.Y
                    
                if v.Z >= 0.0 then
                    l.Z <- float i.Min.Z
                    h.Z <- float i.Max.Z
                else
                    l.Z <- float i.Max.Z
                    h.Z <- float i.Min.Z

                (l,h)

            let onlyBelow (plane : V4d) (i : CullingInfo) =
                let l, h = i |> getMinMaxInDirection plane.XYZ
                Vec.dot l plane.XYZ + plane.W < 0.0 && Vec.dot h plane.XYZ + plane.W < 0.0

            let intersectsViewProj (viewProj : M44d) (i : CullingInfo) =
                let r0 = viewProj.R0
                let r1 = viewProj.R1
                let r2 = viewProj.R2
                let r3 = viewProj.R3

                if  onlyBelow (r3 + r0) i || onlyBelow (r3 - r0) i ||
                    onlyBelow (r3 + r1) i || onlyBelow (r3 - r1) i ||
                    onlyBelow (r3 + r2) i || onlyBelow (r3 - r2) i then
                    false
                else
                    true

        [<LocalSize(X = 64)>]
        let culling (infos : DrawInfo[]) (bounds : CullingInfo[]) (isActive : int[]) (count : int) (viewProjs : M44d[]) =
            compute {
                let id = getGlobalId().X
                if id < count then
                    let b = bounds.[id]
                    let rootId = int (b.Max.W + 0.5f)

                    //let mutable b = b
                    //let s = 0.4f * (b.Max.XYZ - b.Min.XYZ)
                    //b.Min <- V4f(b.Min.XYZ + s, b.Min.W)
                    //b.Max <- V4f(b.Max.XYZ - s, b.Max.W)

                    if isActive.[rootId] <> 0 && CullingInfo.intersectsViewProj viewProjs.[rootId] b then
                        infos.[id].InstanceCount <- CullingInfo.instanceCount b
                    else
                        infos.[id].InstanceCount <- 0
            }

    type InstanceSignature = MapExt<string, GLSLType * Type>
    type VertexSignature = MapExt<string, Type>

    type GeometrySignature =
        {
            mode            : IndexedGeometryMode
            indexType       : Option<Type>
            uniformTypes    : InstanceSignature
            attributeTypes  : VertexSignature
        }

    module GeometrySignature =
        let ofGeometry (iface : GLSLProgramInterface) (uniforms : MapExt<string, Array>) (g : IndexedGeometry) =
            let mutable uniformTypes = MapExt.empty
            let mutable attributeTypes = MapExt.empty

            for i in iface.inputs do
                let sym = Symbol.Create i.paramSemantic
                match MapExt.tryFind i.paramSemantic uniforms with
                    | Some arr when not (isNull arr) ->
                        let t = arr.GetType().GetElementType()
                        uniformTypes <- MapExt.add i.paramSemantic (i.paramType, t) uniformTypes
                    | _ ->
                        let t = if isNull g.SingleAttributes then (false, Unchecked.defaultof<_>) else g.SingleAttributes.TryGetValue sym
                        match t with
                            | (true, uniform) ->
                                assert(not (isNull uniform))
                                let t = uniform.GetType()
                                uniformTypes <- MapExt.add i.paramSemantic (i.paramType, t) uniformTypes
                            | _ ->
                                match g.IndexedAttributes.TryGetValue sym with
                                    | (true, arr) ->
                                        assert(not (isNull arr))
                                        let t = arr.GetType().GetElementType()
                                        attributeTypes <- MapExt.add i.paramSemantic t attributeTypes
                                    | _ -> 
                                        ()
              
            let indexType =
                if isNull g.IndexArray then
                    None
                else
                    let t = g.IndexArray.GetType().GetElementType()
                    Some t

            {
                mode = g.Mode
                indexType = indexType
                uniformTypes = uniformTypes
                attributeTypes = attributeTypes
            }



    [<AutoOpen>]
    module MicroTimeExtensions =
        type MicroTime with
            static member FromNanoseconds (ns : int64) = MicroTime(ns)
            static member FromMicroseconds (us : float) = MicroTime(int64 (1000.0 * us))
            static member FromMilliseconds (ms : float) = MicroTime(int64 (1000000.0 * ms))
            static member FromSeconds (s : float) = MicroTime(int64 (1000000000.0 * s))

        let ns = MicroTime(1L)
        let us = MicroTime(1000L)
        let ms = MicroTime(1000000L)
        let s = MicroTime(1000000000L)


    type Regression(degree : int, maxSamples : int) =
        let samples : array<int * MicroTime> = Array.zeroCreate maxSamples
        let mutable count = 0
        let mutable index = 0
        let mutable model : float[] = null

        let getModel() =
            if count <= 0  then
                [| |]
            elif count = 1 then
                let (x,y) = samples.[0]
                [| 0.0; y.TotalSeconds / float x |]
            else
                let degree = min (count - 1) degree
                let arr = 
                    Array2D.init count (degree + 1) (fun r c ->
                        let (s,_) = samples.[r]
                        float s ** float c
                    )

                let r = samples |> Array.take count |> Array.map (fun (_,t) -> t.TotalSeconds)

                let diag = arr.QrFactorize()
                arr.QrSolve(diag, r)

        member private x.GetModel() = 
            lock x (fun () ->
                if isNull model then model <- getModel()
                model
            )
            
        member x.Add(size : int, value : MicroTime) =
            lock x (fun () ->
                let mutable found = false
                let mutable i = (maxSamples + index - count) % maxSamples
                while not found && i <> index do
                    let (x,y) = samples.[i]
                    if x = size then
                        if y <> value then model <- null
                        samples.[i] <- (size, value)
                        found <- true
                    i <- (i + 1) % maxSamples

                if not found then
                    samples.[index] <- (size,value)
                    index <- (index + 1) % maxSamples
                    if count < maxSamples then count <- count + 1
                    model <- null
            )

        member x.Evaluate(size : int) =
            let model = x.GetModel()
            if model.Length > 0 then
                Polynomial.Evaluate(model, float size) |> MicroTime.FromSeconds
            else 
                MicroTime.Zero

    type Context with
        member x.MapBufferRange(b : Buffer, offset : nativeint, size : nativeint, access : BufferAccessMask) =
            let ptr = GL.MapNamedBufferRange(b.Handle, offset, size, access)
            if ptr = 0n then 
                let err = GL.GetError()
                failwithf "[GL] cannot map buffer %d: %A" b.Handle err
            ptr

        member x.UnmapBuffer(b : Buffer) =
            let worked = GL.UnmapNamedBuffer(b.Handle)
            if not worked then failwithf "[GL] cannot unmap buffer %d" b.Handle

    type InstanceBuffer(ctx : Context, semantics : MapExt<string, GLSLType * Type>, count : int) =
        let buffers, totalSize =
            let mutable totalSize = 0L
            let buffers = 
                semantics |> MapExt.map (fun sem (glsl, input) ->
                    let elemSize = GLSLType.sizeof glsl
                    let write = UniformWriters.getWriter 0 glsl input
                    totalSize <- totalSize + int64 count * int64 elemSize
                    ctx.CreateBuffer(elemSize * count), elemSize, write
                )
            buffers, totalSize
            


        member x.TotalSize = totalSize
        member x.ElementSize = totalSize / int64 count
        member x.Context = ctx
        member x.Data = buffers
        member x.Buffers = buffers |> MapExt.map (fun _ (b,_,_) -> b)
        
        member x.Upload(index : int, count : int, data : MapExt<string, Array>) =
            lock x (fun () ->
                use __ = ctx.ResourceLock
                buffers |> MapExt.iter (fun sem (buffer, elemSize, write) ->
                    let offset = nativeint index * nativeint elemSize
                    let size = nativeint count * nativeint elemSize
                    let ptr = ctx.MapBufferRange(buffer, offset, size, BufferAccessMask.MapWriteBit ||| BufferAccessMask.MapInvalidateRangeBit ||| BufferAccessMask.MapUnsynchronizedBit)
                    match MapExt.tryFind sem data with
                        | Some data ->
                            let mutable ptr = ptr
                            for i in 0 .. count - 1 do
                                write.WriteUnsafeValue(data.GetValue i, ptr)
                                ptr <- ptr + nativeint elemSize
                        | _ -> 
                            Marshal.Set(ptr, 0, elemSize)
                    ctx.UnmapBuffer(buffer)
                )
            )

        static member Copy(src : InstanceBuffer, srcOffset : int, dst : InstanceBuffer, dstOffset : int, count : int) =
            // TODO: locking????
            use __ = src.Context.ResourceLock
            src.Data |> MapExt.iter (fun sem (srcBuffer, elemSize, _) ->
                let (dstBuffer,_,_) = dst.Data.[sem]
                let srcOff = nativeint srcOffset * nativeint elemSize
                let dstOff = nativeint dstOffset * nativeint elemSize
                let s = nativeint elemSize * nativeint count
                GL.NamedCopyBufferSubData(srcBuffer.Handle, dstBuffer.Handle, srcOff, dstOff, s)
            )

        member x.Dispose() =
            use __ = ctx.ResourceLock
            buffers |> MapExt.iter (fun _ (b,_,_) -> ctx.Delete b)

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    type VertexBuffer(ctx : Context, semantics : MapExt<string, Type>, count : int) =

        let totalSize, buffers =
            let mutable totalSize = 0L
            let buffers = 
                semantics |> MapExt.map (fun sem typ ->
                    let elemSize = Marshal.SizeOf typ
                    totalSize <- totalSize + int64 elemSize * int64 count
                    ctx.CreateBuffer(elemSize * count), elemSize, typ
                )
            totalSize, buffers
            
        member x.ElementSize = totalSize / int64 count
        member x.TotalSize = totalSize
        member x.Context = ctx
        member x.Data = buffers
        member x.Buffers = buffers |> MapExt.map (fun _ (b,_,t) -> b,t)
        
        member x.Write(startIndex : int, data : MapExt<string, Array>) =
            lock x (fun () ->
                use __ = ctx.ResourceLock
            
                let count = data |> MapExt.toSeq |> Seq.map (fun (_,a) -> a.Length) |> Seq.min

                buffers |> MapExt.iter (fun sem (buffer, elemSize,_) ->
                    let offset = nativeint startIndex * nativeint elemSize
                    let size = nativeint count * nativeint elemSize
                    let ptr = ctx.MapBufferRange(buffer, offset, size, BufferAccessMask.MapWriteBit ||| BufferAccessMask.MapInvalidateRangeBit ||| BufferAccessMask.MapUnsynchronizedBit)

                    match MapExt.tryFind sem data with
                        | Some data ->  
                            let gc = GCHandle.Alloc(data, GCHandleType.Pinned)
                            try Marshal.Copy(gc.AddrOfPinnedObject(), ptr, size)
                            finally gc.Free()
                        | _ -> 
                            Marshal.Set(ptr, 0, size)
                    ctx.UnmapBuffer(buffer)
                )
            )

        static member Copy(src : VertexBuffer, srcOffset : int, dst : VertexBuffer, dstOffset : int, count : int) =
            // TODO: locking???
            use __ = src.Context.ResourceLock
            src.Data |> MapExt.iter (fun sem (srcBuffer, elemSize,_) ->
                let (dstBuffer,_,_) = dst.Data.[sem]
                let srcOff = nativeint srcOffset * nativeint elemSize
                let dstOff = nativeint dstOffset * nativeint elemSize
                let s = nativeint elemSize * nativeint count
                GL.NamedCopyBufferSubData(srcBuffer.Handle, dstBuffer.Handle, srcOff, dstOff, s)
            )

        member x.Dispose() =
            use __ = ctx.ResourceLock
            buffers |> MapExt.iter (fun _ (b,_,_) -> ctx.Delete b)

        interface IDisposable with
            member x.Dispose() = x.Dispose()


    type VertexManager(ctx : Context, semantics : MapExt<string, Type>, chunkSize : int, usedMemory : ref<int64>, totalMemory : ref<int64>) =
        let elementSize =
            semantics |> MapExt.toSeq |> Seq.sumBy (fun (_,t) -> int64 (Marshal.SizeOf t))

        let mem : Memory<VertexBuffer> =
            let malloc (size : nativeint) =
                Log.warn "alloc VertexBuffer"
                let res = new VertexBuffer(ctx, semantics, int size)
                Interlocked.Add(&totalMemory.contents, res.TotalSize) |> ignore
                res

            let mfree (ptr : VertexBuffer) (size : nativeint) =
                Log.warn "free VertexBuffer"
                Interlocked.Add(&totalMemory.contents, -ptr.TotalSize) |> ignore
                ptr.Dispose()

            {
                malloc = malloc
                mfree = mfree
                mcopy = fun _ _ _ _ _ -> failwith "cannot copy"
                mrealloc = fun _ _ _ -> failwith "cannot realloc"
            }
            
        let mutable used = 0L

        let addMem (v : int64) =
            Interlocked.Add(&usedMemory.contents, v) |> ignore
            Interlocked.Add(&used, v) |> ignore
            

        let manager = new ChunkedMemoryManager<VertexBuffer>(mem, nativeint chunkSize)
        
        member x.Alloc(count : int) = 
            addMem (elementSize * int64 count) 
            manager.Alloc(nativeint count)

        member x.Free(b : Block<VertexBuffer>) = 
            if not b.IsFree then
                addMem (elementSize * int64 -b.Size) 
                manager.Free b

        member x.Dispose() = 
            addMem (-used)
            manager.Dispose()

        interface IDisposable with 
            member x.Dispose() = x.Dispose()
        
    type InstanceManager(ctx : Context, semantics : MapExt<string, GLSLType * Type>, chunkSize : int, usedMemory : ref<int64>, totalMemory : ref<int64>) =
        let elementSize =
            semantics |> MapExt.toSeq |> Seq.sumBy (fun (_,(t,_)) -> int64 (GLSLType.sizeof t))

        let mem : Memory<InstanceBuffer> =
            let malloc (size : nativeint) =
                Log.warn "alloc InstanceBuffer"
                let res = new InstanceBuffer(ctx, semantics, int size)
                Interlocked.Add(&totalMemory.contents, res.TotalSize) |> ignore
                res

            let mfree (ptr : InstanceBuffer) (size : nativeint) =
                Log.warn "free InstanceBuffer"
                Interlocked.Add(&totalMemory.contents, -ptr.TotalSize) |> ignore
                ptr.Dispose()

            {
                malloc = malloc
                mfree = mfree
                mcopy = fun _ _ _ _ _ -> failwith "cannot copy"
                mrealloc = fun _ _ _ -> failwith "cannot realloc"
            }

        let manager = new ChunkedMemoryManager<InstanceBuffer>(mem, nativeint chunkSize)
        let mutable used = 0L

        let addMem (v : int64) =
            Interlocked.Add(&usedMemory.contents, v) |> ignore
            Interlocked.Add(&used, v) |> ignore
            

        member x.Alloc(count : int) = 
            addMem (int64 count * elementSize)
            manager.Alloc(nativeint count)

        member x.Free(b : Block<InstanceBuffer>) = 
            if not b.IsFree then
                addMem (int64 -b.Size * elementSize)
                manager.Free b

        member x.Dispose() = 
            addMem -used
            manager.Dispose()

        interface IDisposable with 
            member x.Dispose() = x.Dispose()

    type IndexManager(ctx : Context, chunkSize : int, usedMemory : ref<int64>, totalMemory : ref<int64>) =

        let mem : Memory<Buffer> =
            let malloc (size : nativeint) =
                let res = ctx.CreateBuffer(int size)
                Interlocked.Add(&totalMemory.contents, int64 res.SizeInBytes) |> ignore
                res

            let mfree (ptr : Buffer) (size : nativeint) =
                Interlocked.Add(&totalMemory.contents, -int64 ptr.SizeInBytes) |> ignore
                ctx.Delete ptr

            {
                malloc = malloc
                mfree = mfree
                mcopy = fun _ _ _ _ _ -> failwith "cannot copy"
                mrealloc = fun _ _ _ -> failwith "cannot realloc"
            }
            
        let manager = new ChunkedMemoryManager<Buffer>(mem, nativeint (sizeof<int> * chunkSize))
        
        let mutable used = 0L

        let addMem (v : int64) =
            Interlocked.Add(&usedMemory.contents, v) |> ignore
            Interlocked.Add(&used, v) |> ignore
            
        member x.Alloc(t : Type, count : int) = 
            let size = nativeint (Marshal.SizeOf t) * nativeint count
            addMem (int64 size)
            manager.Alloc(size)

        member x.Free(b : Block<Buffer>) = 
            if not b.IsFree then
                addMem (int64 -b.Size)
                manager.Free b

        member x.Dispose() = 
            addMem -used
            manager.Dispose()

        interface IDisposable with 
            member x.Dispose() = x.Dispose()

    [<StructLayout(LayoutKind.Sequential)>]
    type private BoundingBox =
        struct
            val mutable public Min : V4f
            val mutable public Max : V4f
        end

    type IndirectBuffer(ctx : Context, bounds : bool, active : nativeptr<int>, modelViewProjs : nativeptr<int>, indexed : bool, initialCapacity : int, usedMemory : ref<int64>, totalMemory : ref<int64>) =
        static let es = sizeof<DrawCallInfo>
        static let bs = sizeof<BoundingBox>

        static let ceilDiv (a : int) (b : int) =
            if a % b = 0 then a / b
            else 1 + a / b

        static let cullingCache = System.Collections.Concurrent.ConcurrentDictionary<Context, ComputeShader>()
        
        let initialCapacity = Fun.NextPowerOfTwo initialCapacity
        let adjust (call : DrawCallInfo) =
            if indexed then
                let mutable c = call
                //c.InstanceCount <- 0
                Fun.Swap(&c.BaseVertex, &c.FirstInstance)
                c
            else
                let mutable c = call
                //c.InstanceCount <- 0
                c

        let drawIndices = Dict<DrawCallInfo, int>()
        let mutable capacity = initialCapacity
        let mutable mem : nativeptr<DrawCallInfo> = NativePtr.alloc capacity
        let mutable bmem : nativeptr<BoundingBox> = if bounds then NativePtr.alloc capacity else NativePtr.zero


        let mutable buffer = ctx.CreateBuffer (es * capacity)
        let mutable bbuffer = if bounds then ctx.CreateBuffer(bs * capacity) else new Buffer(ctx, 0n, 0)

        let ub = ctx.CreateBuffer(128)

        let mutable dirty = RangeSet.empty
        let mutable count = 0

        let bufferHandles = NativePtr.allocArray [| V3i(buffer.Handle, bbuffer.Handle, count) |]
        let indirectHandle = NativePtr.allocArray [| V2i(buffer.Handle, 0) |]
        let computeSize = NativePtr.allocArray [| V3i.Zero |]

        let updatePointers() =
            NativePtr.write bufferHandles (V3i(buffer.Handle, bbuffer.Handle, count))
            NativePtr.write indirectHandle (V2i(buffer.Handle, count))
            NativePtr.write computeSize (V3i(ceilDiv count 64, 1, 1))

        let oldProgram = NativePtr.allocArray [| 0 |]
        let oldUB = NativePtr.allocArray [| 0 |]
        let oldUBOffset = NativePtr.allocArray [| 0n |]
        let oldUBSize = NativePtr.allocArray [| 0n |]

        do let es = if bounds then es + bs else es
           Interlocked.Add(&totalMemory.contents, int64 (es * capacity)) |> ignore

        let culling =
            if bounds then 
                cullingCache.GetOrAdd(ctx, fun ctx ->
                    let cs = ComputeShader.ofFunction (V3i(1024, 1024, 1024)) CullingShader.culling
                    let shader = ctx.CompileKernel cs
                    shader 
                )
            else
                Unchecked.defaultof<ComputeShader>

        let resize (newCount : int) =
            let newCapacity = max initialCapacity (Fun.NextPowerOfTwo newCount)
            if newCapacity <> capacity then
                let ess = if bounds then es + bs else es
                Interlocked.Add(&totalMemory.contents, int64 (ess * (newCapacity - capacity))) |> ignore
                let ob = buffer
                let obb = bbuffer
                let om = mem
                let obm = bmem
                let nb = ctx.CreateBuffer (es * newCapacity)
                let nbb = if bounds then ctx.CreateBuffer (bs * newCapacity) else new Buffer(ctx, 0n, 0)
                let nm = NativePtr.alloc newCapacity
                let nbm = if bounds then NativePtr.alloc newCapacity else NativePtr.zero

                Marshal.Copy(NativePtr.toNativeInt om, NativePtr.toNativeInt nm, nativeint count * nativeint es)
                if bounds then Marshal.Copy(NativePtr.toNativeInt obm, NativePtr.toNativeInt nbm, nativeint count * nativeint bs)

                mem <- nm
                bmem <- nbm
                buffer <- nb
                bbuffer <- nbb
                capacity <- newCapacity
                dirty <- RangeSet.ofList [Range1i(0, count - 1)]
                
                NativePtr.free om
                ctx.Delete ob
                if bounds then 
                    NativePtr.free obm
                    ctx.Delete obb
        
        member x.Count = count

        member x.Add(call : DrawCallInfo, box : Box3d, rootId : int) =
            if drawIndices.ContainsKey call then
                false
            else
                if count < capacity then
                    let id = count
                    drawIndices.[call] <- id
                    NativePtr.set mem id (adjust call)
                    if bounds then
                        let bounds =
                            BoundingBox(
                                Min = V4f(V3f box.Min, float32 call.InstanceCount),
                                Max = V4f(V3f box.Max, float32 rootId)
                            )
                        NativePtr.set bmem id bounds
                    count <- count + 1
                    let ess = if bounds then es + bs else es
                    Interlocked.Add(&usedMemory.contents, int64 ess) |> ignore
                    dirty <- RangeSet.insert (Range1i(id,id)) dirty
                    
                    updatePointers()
                    true
                else
                    resize (count + 1)
                    x.Add(call, box, rootId)
                    
        member x.Remove(call : DrawCallInfo) =
            match drawIndices.TryRemove call with
                | (true, oid) ->
                    let last = count - 1
                    count <- count - 1
                    let ess = if bounds then es + bs else es
                    Interlocked.Add(&usedMemory.contents, int64 -ess) |> ignore

                    if oid <> last then
                        let lc = NativePtr.get mem last
                        drawIndices.[lc] <- oid
                        NativePtr.set mem oid lc
                        NativePtr.set mem last Unchecked.defaultof<DrawCallInfo>
                        if bounds then
                            let lb = NativePtr.get bmem last
                            NativePtr.set bmem oid lb
                        dirty <- RangeSet.insert (Range1i(oid,oid)) dirty
                        
                    resize last
                    updatePointers()

                    true
                | _ ->
                    false
        
        member x.Flush() =
            use __ = ctx.ResourceLock
            let toUpload = dirty
            dirty <- RangeSet.empty

            if not (Seq.isEmpty toUpload) then
                if bounds then
                    let ptr = ctx.MapBufferRange(buffer, 0n, nativeint (count * es), BufferAccessMask.MapWriteBit ||| BufferAccessMask.MapFlushExplicitBit)
                    let bptr = ctx.MapBufferRange(bbuffer, 0n, nativeint (count * bs), BufferAccessMask.MapWriteBit ||| BufferAccessMask.MapFlushExplicitBit)
                    for r in toUpload do
                        let o = r.Min * es |> nativeint
                        let s = (1 + r.Max - r.Min) * es |> nativeint
                        Marshal.Copy(NativePtr.toNativeInt mem + o, ptr + o, s)
                        GL.FlushMappedNamedBufferRange(buffer.Handle, o, s)
                        
                        let o = r.Min * bs |> nativeint
                        let s = (1 + r.Max - r.Min) * bs |> nativeint
                        Marshal.Copy(NativePtr.toNativeInt bmem + o, bptr + o, s)
                        GL.FlushMappedNamedBufferRange(bbuffer.Handle, o, s)

                    ctx.UnmapBuffer(buffer)
                    ctx.UnmapBuffer(bbuffer)
                else 
                    let size = count * es
                    let ptr = ctx.MapBufferRange(buffer, 0n, nativeint size, BufferAccessMask.MapWriteBit ||| BufferAccessMask.MapFlushExplicitBit)
                    for r in toUpload do
                        let o = r.Min * es |> nativeint
                        let s = (1 + r.Max - r.Min) * es |> nativeint
                        Marshal.Copy(NativePtr.toNativeInt mem + o, ptr + o, s)
                        GL.FlushMappedNamedBufferRange(buffer.Handle, o, s)
                    ctx.UnmapBuffer(buffer)


        member x.Buffer =
            Aardvark.Rendering.GL.IndirectBufferExtensions.IndirectBuffer(buffer, count, sizeof<DrawCallInfo>, false)

        member x.BoundsBuffer =
            bbuffer

       

        member x.CompileRender(s : ICommandStream, before : ICommandStream -> unit, mvp : nativeptr<M44f>, indexType : Option<_>, runtimeStats : nativeptr<_>, isActive : nativeptr<_>, mode : nativeptr<_>) =
            if bounds then
                let infoSlot = culling.Buffers |> List.pick (fun (a,b,c) -> if b = "infos" then Some a else None)
                let boundSlot = culling.Buffers |> List.pick (fun (a,b,c) -> if b = "bounds" then Some a else None)
                let activeSlot = culling.Buffers |> List.pick (fun (a,b,c) -> if b = "isActive" then Some a else None)
                let viewProjSlot = culling.Buffers |> List.pick (fun (a,b,c) -> if b = "viewProjs" then Some a else None)
                let uniformBlock = culling.UniformBlocks |> List.head
                let countField = uniformBlock.ubFields |> List.find (fun f -> f.ufName = "cs_count")
                
                
                //s.NamedBufferSubData(ub.Handle, nativeint viewProjField.ufOffset, 64n, NativePtr.toNativeInt mvp)
                s.NamedBufferSubData(ub.Handle, nativeint countField.ufOffset, 4n, NativePtr.toNativeInt bufferHandles + 8n)

                s.Get(GetPName.CurrentProgram, oldProgram)
                s.Get(GetIndexedPName.UniformBufferBinding, uniformBlock.ubBinding, oldUB)
                s.Get(GetIndexedPName.UniformBufferStart, uniformBlock.ubBinding, oldUBOffset)
                s.Get(GetIndexedPName.UniformBufferSize, uniformBlock.ubBinding, oldUBSize)

                s.UseProgram(culling.Handle)
                s.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, infoSlot, NativePtr.ofNativeInt (NativePtr.toNativeInt bufferHandles + 0n))
                s.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, boundSlot, NativePtr.ofNativeInt (NativePtr.toNativeInt bufferHandles + 4n))
                s.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, activeSlot, active)
                s.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, viewProjSlot, modelViewProjs)
                s.BindBufferBase(BufferRangeTarget.UniformBuffer, uniformBlock.ubBinding, ub.Handle)
                s.DispatchCompute computeSize


                s.UseProgram(oldProgram)
                s.BindBufferRange(BufferRangeTarget.UniformBuffer, uniformBlock.ubBinding, oldUB, oldUBOffset, oldUBSize)
                
                
                s.MemoryBarrier(MemoryBarrierFlags.CommandBarrierBit)

            let h = NativePtr.read indirectHandle
            if h.Y > 0 then
                before(s)
                match indexType with
                    | Some indexType ->
                        s.DrawElementsIndirect(runtimeStats, isActive, mode, int indexType, indirectHandle)
                    | _ -> 
                        s.DrawArraysIndirect(runtimeStats, isActive, mode, indirectHandle)
            else
                Log.warn "empty indirect call"

        member x.Dispose() =
            let ess = if bounds then es + bs else es
            Interlocked.Add(&usedMemory.contents, int64 (-ess * count)) |> ignore
            Interlocked.Add(&totalMemory.contents, int64 (-ess * capacity)) |> ignore
            NativePtr.free mem
            ctx.Delete buffer
            if bounds then
                NativePtr.free bmem
                ctx.Delete bbuffer
            capacity <- 0
            mem <- NativePtr.zero
            buffer <- new Buffer(ctx, 0n, 0)
            dirty <- RangeSet.empty
            count <- 0
            NativePtr.free indirectHandle
            NativePtr.free computeSize
            drawIndices.Clear()

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    type PoolSlot(ctx : Context, signature : GeometrySignature, ub : Block<InstanceBuffer>, vb : Block<VertexBuffer>, ib : Option<Block<Buffer>>) = 
        let fvc =
            match signature.indexType, ib with
                | Some it, Some ib -> int ib.Size / Marshal.SizeOf it
                | _ -> int vb.Size
        
        static let getIndexType =
            LookupTable.lookupTable [
                typeof<uint8>, DrawElementsType.UnsignedByte
                typeof<int8>, DrawElementsType.UnsignedByte
                typeof<uint16>, DrawElementsType.UnsignedShort
                typeof<int16>, DrawElementsType.UnsignedShort
                typeof<uint32>, DrawElementsType.UnsignedInt
                typeof<int32>, DrawElementsType.UnsignedInt
            ]

        let indexType = signature.indexType |> Option.map getIndexType 

        member x.Memory = 
            Mem (
                int64 ub.Size * ub.Memory.Value.ElementSize +
                int64 vb.Size * vb.Memory.Value.ElementSize +
                (match ib with | Some ib -> int64 ib.Size | _ -> 0L)
            )

        member x.IndexType = indexType
        member x.Signature = signature
        member x.VertexBuffer = vb
        member x.InstanceBuffer = ub
        member x.IndexBuffer = ib

        member x.IsDisposed = vb.IsFree

        member x.Upload(g : IndexedGeometry, uniforms : MapExt<string, Array>) =
            let instanceValues =
                signature.uniformTypes |> MapExt.choose (fun name (glslType, typ) ->
                    match MapExt.tryFind name uniforms with
                        | Some att -> Some att
                        | None -> 
                            match g.SingleAttributes.TryGetValue(Symbol.Create name) with
                                | (true, v) -> 
                                    let arr = Array.CreateInstance(typ, 1) //Some ([| v |] :> Array)
                                    arr.SetValue(v, 0)
                                    Some arr
                                | _ -> 
                                    None
                )
            let vertexArrays =
                signature.attributeTypes |> MapExt.choose (fun name _ ->
                    match g.IndexedAttributes.TryGetValue(Symbol.Create name) with
                        | (true, v) -> Some v
                        | _ -> None
                )

            match ib with
                | Some ib -> 
                    let gc = GCHandle.Alloc(g.IndexArray, GCHandleType.Pinned)
                    try 
                        let ptr = ctx.MapBufferRange(ib.Memory.Value, ib.Offset, ib.Size, BufferAccessMask.MapWriteBit ||| BufferAccessMask.MapInvalidateRangeBit ||| BufferAccessMask.MapUnsynchronizedBit)
                        Marshal.Copy(gc.AddrOfPinnedObject(), ptr,ib.Size )
                        ctx.UnmapBuffer(ib.Memory.Value)
                    finally
                        gc.Free()
                | None ->
                    ()

            ub.Memory.Value.Upload(int ub.Offset, int ub.Size, instanceValues)
            vb.Memory.Value.Write(int vb.Offset, vertexArrays)

        member x.Upload(g : IndexedGeometry) = x.Upload(g, MapExt.empty)

        member x.Mode = signature.mode

        member x.DrawCallInfo =
            match ib with
                | Some ib ->
                    DrawCallInfo(
                        FaceVertexCount = fvc,
                        FirstIndex = int ib.Offset / Marshal.SizeOf(signature.indexType.Value),
                        InstanceCount = int ub.Size,
                        FirstInstance = int ub.Offset,
                        BaseVertex = int vb.Offset
                    )

                | None -> 
                    DrawCallInfo(
                        FaceVertexCount = fvc,
                        FirstIndex = int vb.Offset,
                        InstanceCount = int ub.Size,
                        FirstInstance = int ub.Offset
                    )

    type GeometryPool private(ctx : Context) =
        static let instanceChunkSize = 1 <<< 20
        static let vertexChunkSize = 1 <<< 20
        static let pools = System.Collections.Concurrent.ConcurrentDictionary<Context, GeometryPool>()

        let usedMemory = ref 0L
        let totalMemory = ref 0L
        let instanceManagers = System.Collections.Concurrent.ConcurrentDictionary<InstanceSignature, InstanceManager>()
        let vertexManagers = System.Collections.Concurrent.ConcurrentDictionary<VertexSignature, VertexManager>()

        let getVertexManager (signature : VertexSignature) = vertexManagers.GetOrAdd(signature, fun signature -> new VertexManager(ctx, signature, vertexChunkSize, usedMemory, totalMemory))
        let getInstanceManager (signature : InstanceSignature) = instanceManagers.GetOrAdd(signature, fun signature -> new InstanceManager(ctx, signature, instanceChunkSize, usedMemory, totalMemory))
        let indexManager = new IndexManager(ctx, vertexChunkSize, usedMemory, totalMemory)

        static member Get(ctx : Context) =
            pools.GetOrAdd(ctx, fun ctx ->
                new GeometryPool(ctx)
            )      
            
        member x.UsedMemory = Mem !usedMemory
        member x.TotalMemory = Mem !totalMemory

        member x.Alloc(signature : GeometrySignature, instanceCount : int, indexCount : int, vertexCount : int) =
            let vm = getVertexManager signature.attributeTypes
            let im = getInstanceManager signature.uniformTypes

            let ub = im.Alloc(instanceCount)
            let vb = vm.Alloc(vertexCount)

            let ib = 
                match signature.indexType with
                    | Some t -> indexManager.Alloc(t, indexCount) |> Some
                    | None -> None

            let slot = PoolSlot(ctx, signature, ub, vb, ib)
            slot

        member x.Alloc(signature : GLSLProgramInterface, geometry : IndexedGeometry, uniforms : MapExt<string, Array>) =
            let signature = GeometrySignature.ofGeometry signature uniforms geometry

            let instanceCount =
                if MapExt.isEmpty uniforms then
                    1
                else
                    uniforms |> MapExt.toSeq |> Seq.map (fun (_,arr) -> arr.Length) |> Seq.min

            let vertexCount, indexCount = 
                if isNull geometry.IndexArray then
                    geometry.FaceVertexCount, 0
                else
                    let vc = geometry.IndexedAttributes.Values |> Seq.map (fun v -> v.Length) |> Seq.min
                    let fvc = geometry.IndexArray.Length
                    vc, fvc

            let slot = x.Alloc(signature, instanceCount, indexCount, vertexCount)
            slot.Upload(geometry, uniforms)
            slot
            
        member x.Alloc(signature : GLSLProgramInterface, geometry : IndexedGeometry) =
            x.Alloc(signature, geometry, MapExt.empty)
           

        member x.Free(slot : PoolSlot) =
            //Log.warn "free %A" slot.Memory
            let signature = slot.Signature
            let vm = getVertexManager signature.attributeTypes
            let im = getInstanceManager signature.uniformTypes
            im.Free slot.InstanceBuffer
            vm.Free slot.VertexBuffer
            match slot.IndexBuffer with
                | Some ib -> indexManager.Free ib
                | None -> ()

    type DrawPool(ctx : Context, bounds : bool, activeBuffer : nativeptr<int>, modelViewProjs : nativeptr<int>, state : PreparedPipelineState, pass : RenderPass) as this =
        inherit PreparedCommand(ctx, pass)

        static let initialIndirectSize = 256

        static let getKey (slot : PoolSlot) =
            slot.Mode,
            slot.InstanceBuffer.Memory.Value, 
            slot.VertexBuffer.Memory.Value,
            slot.IndexBuffer |> Option.map (fun b -> slot.IndexType.Value, b.Memory.Value)

        static let beginMode =
            LookupTable.lookupTable [
                IndexedGeometryMode.PointList, BeginMode.Points
                IndexedGeometryMode.LineList, BeginMode.Lines
                IndexedGeometryMode.LineStrip, BeginMode.LineStrip
                IndexedGeometryMode.LineAdjacencyList, BeginMode.LinesAdjacency
                IndexedGeometryMode.TriangleList, BeginMode.Triangles
                IndexedGeometryMode.TriangleStrip, BeginMode.TriangleStrip
                IndexedGeometryMode.TriangleAdjacencyList, BeginMode.TrianglesAdjacency
            ]

        let isActive = NativePtr.allocArray [| 1 |]
        let runtimeStats : nativeptr<V2i> = NativePtr.alloc 1
        let contextHandle : nativeptr<nativeint> = NativePtr.alloc 1

        let pProgramInterface = state.pProgramInterface

        let mvpResource=
            let s = state

            let viewProj =
                match Uniforms.tryGetDerivedUniform "ModelViewProjTrafo" s.pUniformProvider with
                | Some (:? IMod<Trafo3d> as mvp) -> mvp
                | _ -> 
                    match s.pUniformProvider.TryGetUniform(Ag.emptyScope, Symbol.Create "ModelViewProjTrafo") with
                    | Some (:? IMod<Trafo3d> as mvp) -> mvp
                    | _ -> Mod.constant Trafo3d.Identity

            let res = 
                { new Resource<Trafo3d, M44f>(ResourceKind.UniformLocation) with
                    member x.Create(t, rt, o) = viewProj.GetValue(t)
                    member x.Destroy _ = ()
                    member x.View t = t.Forward |> M44f.op_Explicit
                    member x.GetInfo _ = ResourceInfo.Zero
                }

            res.AddRef()
            res.Update(AdaptiveToken.Top, RenderToken.Empty)

            res :> IResource<_,_>


        let query : nativeptr<int> = NativePtr.allocArray [| 0 |]
        let startTime : nativeptr<uint64> = NativePtr.allocArray [| 0UL |]
        let endTime : nativeptr<uint64> = NativePtr.allocArray [| 0UL |]
        



        let usedMemory = ref 0L
        let totalMemory = ref 0L
        let avgRenderTime = RunningMean(10)

        let compile (indexType : Option<DrawElementsType>, mode : nativeptr<GLBeginMode>, a : VertexInputBindingHandle, ib : IndirectBuffer) (s : ICommandStream) =
            s.BindVertexAttributes(contextHandle, a)
            ib.CompileRender(s, this.BeforeRender, mvpResource.Pointer, indexType, runtimeStats, isActive, mode)

        let indirects = Dict<_, IndirectBuffer>()
        let isOutdated = NativePtr.allocArray [| 1 |]
        let updateFun = Marshal.PinDelegate(new System.Action(this.Update))
        let mutable oldCalls : list<Option<DrawElementsType> * nativeptr<GLBeginMode> * VertexInputBindingHandle * IndirectBuffer> = []
        let program = new ChangeableNativeProgram<_>(fun a s -> compile a (AssemblerCommandStream s))
        let puller = AdaptiveObject()
        let sub = puller.AddMarkingCallback (fun () -> NativePtr.write isOutdated 1)
        let tasks = System.Collections.Generic.HashSet<IRenderTask>()

        let mark() = transact (fun () -> puller.MarkOutdated())
        

        let getIndirectBuffer(slot : PoolSlot) =
            let key = getKey slot
            indirects.GetOrCreate(key, fun _ ->
                new IndirectBuffer(ctx, bounds, activeBuffer, modelViewProjs, Option.isSome slot.IndexType, initialIndirectSize, usedMemory, totalMemory)
            )

        let tryGetIndirectBuffer(slot : PoolSlot) =
            let key = getKey slot
            match indirects.TryGetValue key with
                | (true, ib) -> Some ib
                | _ -> None
                
                
        member x.Add(ref : PoolSlot, bounds : Box3d, rootId : int) =
            let ib = getIndirectBuffer ref
            if ib.Add(ref.DrawCallInfo, bounds, rootId) then
                mark()

        member x.Add(ref : PoolSlot, rootId : int) =
            let ib = getIndirectBuffer ref
            if ib.Add(ref.DrawCallInfo, Unchecked.defaultof<Box3d>, rootId) then
                mark()

        member x.Remove(ref : PoolSlot) =
            match tryGetIndirectBuffer ref with
                | Some ib -> 
                    if ib.Remove(ref.DrawCallInfo) then
                        if ib.Count = 0 then
                            let key = getKey ref
                            indirects.Remove(key) |> ignore
                            ib.Dispose()
                            
                        mark()
                        true
                    else
                        false
                | None ->
                    false
                    
        member x.UsedMemory = Mem !totalMemory
        member x.TotalMemory = Mem !totalMemory

        abstract member Evaluate : AdaptiveToken * GLSLProgramInterface -> unit
        default x.Evaluate(_,_) = ()

        abstract member AfterUpdate : unit -> unit
        default x.AfterUpdate () = ()

        abstract member BeforeRender : ICommandStream -> unit
        default x.BeforeRender(_) = ()

        member x.AverageRenderTime = MicroTime(int64 (1000000.0 * avgRenderTime.Average))

        member x.Update() =
            puller.EvaluateAlways AdaptiveToken.Top (fun token ->   
                //let worked =
                //    try System.GC.TryStartNoGCRegion(12000000L)
                //    with _ -> false
                puller.OutOfDate <- true
                
                x.Evaluate(token, pProgramInterface)

                let sw = System.Diagnostics.Stopwatch.StartNew()

                let rawResult = NativePtr.read endTime - NativePtr.read startTime
                let ms = float rawResult / 1000000.0
                avgRenderTime.Add ms



                let calls = 
                    Dict.toList indirects |> List.map (fun ((mode, ib, vb, typeAndIndex), db) ->
                        let indexType = typeAndIndex |> Option.map fst
                        let index = typeAndIndex |> Option.map snd
                        db.Flush()

                        let attributes = 
                            pProgramInterface.inputs |> List.map (fun param ->
                                match MapExt.tryFind param.paramSemantic ib.Buffers with
                                    | Some ib -> 
                                        param.paramLocation, {
                                            Type = GLSLType.toType param.paramType
                                            Content = Left ib
                                            Frequency = AttributeFrequency.PerInstances 1
                                            Normalized = false
                                            Stride = GLSLType.sizeof param.paramType
                                            Offset = 0
                                        }

                                    | None ->   
                                        match MapExt.tryFind param.paramSemantic vb.Buffers with
                                        | Some (vb, typ) ->
                                            let norm = if typ = typeof<C4b> then true else false
                                            param.paramLocation, {
                                                Type = typ
                                                Content = Left vb
                                                Frequency = AttributeFrequency.PerVertex
                                                Normalized = norm
                                                Stride = Marshal.SizeOf typ
                                                Offset = 0
                                            }

                                        | None ->
                                            param.paramLocation, {
                                                Type = GLSLType.toType param.paramType
                                                Content = Right V4f.Zero
                                                Frequency = AttributeFrequency.PerVertex
                                                Normalized = false
                                                Stride = GLSLType.sizeof param.paramType
                                                Offset = 0
                                            }
                            )

                        let bufferBinding = ctx.CreateVertexInputBinding(index, attributes)
                
                        let beginMode = 
                            let bm = beginMode mode
                            NativePtr.allocArray [| GLBeginMode(int bm, 1) |]
                            

                        indexType, beginMode, bufferBinding, db
                    )

                program.Clear()
                for a in calls do program.Add a |> ignore
            
                oldCalls |> List.iter (fun (_,beginMode,bufferBinding,indirect) -> 
                    NativePtr.free beginMode; ctx.Delete bufferBinding
                )
                oldCalls <- calls

                NativePtr.write isOutdated 0

                for t in tasks do
                    puller.Outputs.Add t |> ignore

                x.AfterUpdate()

                sw.Stop()
                if sw.MicroTime.TotalMilliseconds > 15.0 then
                    Log.warn "hugo: %A" sw.MicroTime

                //if worked then
                //    try System.GC.EndNoGCRegion()
                //    with _ -> ()

            )

        override x.Compile(info : CompilerInfo, stream : ICommandStream, last : Option<PreparedCommand>) =
            lock puller (fun () ->
                if tasks.Add info.task then
                    assert (info.task.OutOfDate)
                    puller.AddOutput(info.task) |> ignore
            )
            
            let mvpRes = mvpResource
            let lastState = last |> Option.bind (fun l -> l.ExitState)

            stream.ConditionalCall(isOutdated, updateFun.Pointer)
            
            stream.Copy(info.runtimeStats, runtimeStats)
            stream.Copy(info.contextHandle, contextHandle)
            
            stream.SetPipelineState(info, state, lastState)
            
            stream.QueryTimestamp(query, startTime)
            stream.CallIndirect(program.EntryPointer)
            stream.QueryTimestamp(query, endTime)

            stream.Copy(runtimeStats, info.runtimeStats)

        override x.Release() =
            state.Dispose()
            for ib in indirects.Values do ib.Dispose()
            indirects.Clear()
            updateFun.Dispose()
            NativePtr.free isActive
            NativePtr.free isOutdated
            NativePtr.free runtimeStats
            NativePtr.free contextHandle

            NativePtr.free startTime
            NativePtr.free endTime
            NativePtr.free query

            program.Dispose()
            oldCalls <- []
            

        override x.GetResources() = 
            Seq.append (Seq.singleton (mvpResource :> IResource)) state.Resources

        override x.EntryState = Some state
        override x.ExitState = Some state


    open System.Threading.Tasks

    

    type ITreeNode =
        abstract member Level : int
        abstract member Name : string
        abstract member Root : ITreeNode
        abstract member Parent : Option<ITreeNode>
        abstract member Children : seq<ITreeNode>

        abstract member DataSource : Symbol
        abstract member DataSize : int
        abstract member GetData : CancellationToken -> IndexedGeometry * MapExt<string, Array>

        abstract member ShouldSplit : float * Trafo3d * Trafo3d -> bool
        abstract member ShouldCollapse : float * Trafo3d * Trafo3d -> bool

        abstract member BoundingBox : Box3d

    type IPrediction<'a> =
        abstract member Predict : dt : MicroTime -> Option<'a>
        abstract member WithOffset : offset : MicroTime -> IPrediction<'a>

    type Prediction<'a>(span : MicroTime, interpolate2 : float -> 'a -> 'a -> 'a) =
        let sw = System.Diagnostics.Stopwatch.StartNew()
        let now () = sw.MicroTime

        let mutable history : MapExt<MicroTime, 'a> = MapExt.empty
        
        let prune (t : MicroTime) =
            let _,_,r = history |> MapExt.split (t - span)
            history <- r
            
        let interpolate (arr : array<MicroTime * 'a>) (t : MicroTime) =
            match arr.Length with
                | 0 ->
                    None

                | 1 -> 
                    arr.[0] |> snd |> Some

                | _ ->
                    let (t0, p0) = arr.[0]
                    let (t1, p1) = arr.[arr.Length - 1]
                    let p = (t - t0) / (t1 - t0)
                    interpolate2 p p0 p1 |> Some

        member x.WithOffset(offset : MicroTime) =
            { new IPrediction<'a> with 
                member __.WithOffset(o) = x.WithOffset(offset + o)
                member __.Predict(dt) = x.Predict(offset + dt)
            }

        member x.Add(cam : 'a) =
            lock x (fun () ->
                let t = now()
                history <- MapExt.add t cam history
                prune t
            )

        member x.Predict(dt : MicroTime) =
            lock x (fun () ->
                let t = now()
                prune t
                let future = t + dt
                let arr = MapExt.toArray history
                interpolate arr future
            )

        interface IPrediction<'a> with
            member x.Predict(dt) = x.Predict(dt)
            member x.WithOffset(o) = x.WithOffset o

    module Prediction =
        let rec map (mapping : 'a -> 'b) (p : IPrediction<'a>) =
            { new IPrediction<'b> with
                member x.Predict(dt) = p.Predict(dt) |> Option.map mapping
                member x.WithOffset(o) = p.WithOffset(o) |> map mapping
            }

        let euclidean (span : MicroTime) =
            Prediction<Euclidean3d>(
                span, 
                fun (t : float) (a : Euclidean3d) (b : Euclidean3d) ->
                    let delta = b * a.Inverse

                    let dRot = Rot3d.FromAngleAxis(delta.Rot.ToAngleAxis() * t)
                    let dTrans = delta.Rot.InvTransformDir(delta.Trans) * t
                    let dScaled = Euclidean3d(dRot, dRot.TransformDir dTrans)

                    dScaled * a
            )
            
    module MapExt =
        let tryRemove (key : 'a) (m : MapExt<'a, 'b>) =
            let mutable rem = None
            let m = m |> MapExt.alter key (fun o -> rem <- o; None)
            match rem with
            | Some r -> Some(r, m)
            | None -> None

    module HMap =
        let keys (m : hmap<'a, 'b>) =
            HSet.ofSeq (Seq.map fst (HMap.toSeq m))

        let applySetDelta (set : hdeltaset<'a>) (value : 'b) (m : hmap<'a, 'b>) =
            let delta = 
                set |> HDeltaSet.toHMap |> HMap.map (fun e r ->
                    if r > 0 then Set value
                    else Remove
                )
            HMap.applyDelta m delta |> fst

    [<StructuredFormatDisplay("{AsString}")>]
    type Operation<'a> =
        {
            alloc   : int
            active  : int
            value   : Option<'a>
        }


        member x.Inverse =
            {
                alloc = -x.alloc
                active = -x.active
                value = x.value
            }
        
        member x.ToString(name : string) =
            if x.alloc > 0 then 
                if x.active > 0 then sprintf "alloc(%s, +1)" name
                elif x.active < 0 then sprintf "alloc(%s, -1)" name
                else sprintf "alloc(%s)" name
            elif x.alloc < 0 then sprintf "free(%s)" name
            elif x.active > 0 then sprintf "activate(%s)" name
            elif x.active < 0 then sprintf "deactivate(%s)" name
            else sprintf "nop(%s)" name

        override x.ToString() =
            if x.alloc > 0 then 
                if x.active > 0 then sprintf "alloc(%A, +1)" x.value.Value
                elif x.active < 0 then sprintf "alloc(%A, -1)" x.value.Value
                else sprintf "alloc(%A)" x.value.Value
            elif x.alloc < 0 then "free"
            elif x.active > 0 then "activate"
            elif x.active < 0 then "deactivate"
            else "nop"

        member private x.AsString = x.ToString()

        static member Zero : Operation<'a> = { alloc = 0; active = 0; value = None }

        static member Nop : Operation<'a> = { alloc = 0; active = 0; value = None }
        static member Alloc(value, active) : Operation<'a> = { alloc = 1; active = (if active then 1 else 0); value = Some value }
        static member Free : Operation<'a> = { alloc = -1; active = -1; value = None }
        static member Activate : Operation<'a> = { alloc = 0; active = 1; value = None }
        static member Deactivate : Operation<'a> = { alloc = 0; active = -1; value = None }

        static member (+) (l : Operation<'a>, r : Operation<'a>) =
            {
                alloc = l.alloc + r.alloc
                active = l.active + r.active
                value = match r.value with | Some v -> Some v | None -> l.value
            }

    let Nop<'a> = Operation<'a>.Nop
    let Alloc(v,a) = Operation.Alloc(v,a)
    let Free<'a> = Operation<'a>.Free
    let Activate<'a> = Operation<'a>.Activate
    let Deactivate<'a> = Operation<'a>.Deactivate

    let (|Nop|Alloc|Free|Activate|Deactivate|) (o : Operation<'a>) =
        if o.alloc > 0 then Alloc(o.value.Value, o.active)
        elif o.alloc < 0 then Free
        elif o.active > 0 then Activate
        elif o.active < 0 then Deactivate
        else Nop
        
    [<StructuredFormatDisplay("{AsString}")>]
    type AtomicOperation<'a, 'b> =
        {
            keys : hset<'a>
            ops : hmap<'a, Operation<'b>>
        }
            
        override x.ToString() =
            x.ops 
            |> Seq.map (fun (a, op) -> op.ToString(sprintf "%A" a)) 
            |> String.concat "; " |> sprintf "atomic [%s]"

        member private x.AsString = x.ToString()

        member x.Inverse =
            {
                keys = x.keys
                ops = x.ops |> HMap.map (fun _ o -> o.Inverse)
            }

        static member Empty : AtomicOperation<'a, 'b> = { keys = HSet.empty; ops = HMap.empty }
        static member Zero : AtomicOperation<'a, 'b> = { keys = HSet.empty; ops = HMap.empty }

        static member (+) (l : AtomicOperation<'a, 'b>, r : AtomicOperation<'a, 'b>) =
            let merge (key : 'a) (l : Option<Operation<'b>>) (r : Option<Operation<'b>>) =
                match l with
                | None -> r
                | Some l ->
                    match r with
                    | None -> Some l
                    | Some r -> 
                        match l + r with
                        | Nop -> None
                        | op -> Some op

            let ops = HMap.choose2 merge l.ops r.ops 
            let keys = HMap.keys ops
            { ops = ops; keys = keys }
            
        member x.IsEmpty = HMap.isEmpty x.ops
            
    module AtomicOperation =

        let empty<'a, 'b> = AtomicOperation<'a, 'b>.Empty
        
        let ofHMap (ops : hmap<'a, Operation<'b>>) =
            let keys = HMap.keys ops
            { ops = ops; keys = keys }

        let ofSeq (s : seq<'a * Operation<'b>>) =
            let ops = HMap.ofSeq s
            let keys = HMap.keys ops
            { ops = ops; keys = keys }
                
        let ofList (l : list<'a * Operation<'b>>) = ofSeq l
        let ofArray (a : array<'a * Operation<'b>>) = ofSeq a

    type AtomicQueue<'a, 'b> private(classId : uint64, classes : hmap<'a, uint64>, values : MapExt<uint64, AtomicOperation<'a, 'b>>) =
        let classId = if HMap.isEmpty classes then 0UL else classId

        static let empty = AtomicQueue<'a, 'b>(0UL, HMap.empty, MapExt.empty)

        static member Empty = empty

        member x.Enqueue(op : AtomicOperation<'a, 'b>) =
            if not op.IsEmpty then
                let clazzes = op.keys |> HSet.choose (fun k -> HMap.tryFind k classes)

                if clazzes.Count = 0 then
                    let id = classId
                    let classId = id + 1UL
                    let classes = op.keys |> Seq.fold (fun c k -> HMap.add k id c) classes
                    let values = MapExt.add id op values
                    AtomicQueue(classId, classes, values)
                        
                else
                    let mutable values = values
                    let mutable classes = classes
                    let mutable result = AtomicOperation.empty
                    for c in clazzes do
                        match MapExt.tryRemove c values with
                        | Some (o, rest) ->
                            values <- rest
                            classes <- op.keys |> HSet.fold (fun cs c -> HMap.remove c cs) classes
                            // may not overlap here
                            result <- { ops = HMap.union result.ops o.ops; keys = HSet.union result.keys o.keys } //result + o

                        | None ->
                            ()

                    let result = result + op
                    if result.IsEmpty then
                        AtomicQueue(classId, classes, values)
                    else
                        let id = classId
                        let classId = id + 1UL

                        let classes = result.keys |> HSet.fold (fun cs c -> HMap.add c id cs) classes
                        let values = MapExt.add id result values
                        AtomicQueue(classId, classes, values)
                            
            else
                x
            
        member x.TryDequeue() =
            match MapExt.tryMin values with
            | None ->
                None
            | Some clazz ->
                let v = values.[clazz]
                let values = MapExt.remove clazz values
                let classes = v.keys |> HSet.fold (fun cs c -> HMap.remove c cs) classes
                let newQueue = AtomicQueue(classId, classes, values)
                Some (v, newQueue)

        member x.Dequeue() =
            match x.TryDequeue() with
            | None -> failwith "empty AtomicQueue"
            | Some t -> t

        member x.IsEmpty = MapExt.isEmpty values

        member x.Count = values.Count

        member x.UnionWith(other : AtomicQueue<'a, 'b>) =
            if x.Count < other.Count then
                other.UnionWith x
            else
                other |> Seq.fold (fun (s : AtomicQueue<_,_>) e -> s.Enqueue e) x

        static member (+) (s : AtomicQueue<'a, 'b>, a : AtomicOperation<'a, 'b>) = s.Enqueue a

        interface System.Collections.IEnumerable with
            member x.GetEnumerator() = new AtomicQueueEnumerator<_,_>((values :> seq<_>).GetEnumerator()) :> _
                
        interface IEnumerable<AtomicOperation<'a, 'b>> with
            member x.GetEnumerator() = new AtomicQueueEnumerator<_,_>((values :> seq<_>).GetEnumerator()) :> _

    and private AtomicQueueEnumerator<'a, 'b>(e : IEnumerator<KeyValuePair<uint64, AtomicOperation<'a, 'b>>>) =
        interface System.Collections.IEnumerator with
            member x.MoveNext() = e.MoveNext()
            member x.Current = e.Current.Value :> obj
            member x.Reset() = e.Reset()

        interface IEnumerator<AtomicOperation<'a, 'b>> with
            member x.Dispose() = e.Dispose()
            member x.Current = e.Current.Value

    module AtomicQueue =

        [<GeneralizableValue>]
        let empty<'a, 'b> = AtomicQueue<'a, 'b>.Empty

        let inline isEmpty (queue : AtomicQueue<'a, 'b>) = queue.IsEmpty
        let inline count (queue : AtomicQueue<'a, 'b>) = queue.Count
        let inline enqueue (v : AtomicOperation<'a, 'b>) (queue : AtomicQueue<'a, 'b>) = queue.Enqueue v
        let inline tryDequeue (queue : AtomicQueue<'a, 'b>) = queue.TryDequeue()
        let inline dequeue (queue : AtomicQueue<'a, 'b>) = queue.Dequeue()
        let inline combine (l : AtomicQueue<'a, 'b>) (r : AtomicQueue<'a, 'b>) = l.UnionWith r
            
        let enqueueMany (v : #seq<AtomicOperation<'a, 'b>>) (queue : AtomicQueue<'a, 'b>) = v |> Seq.fold (fun s e -> enqueue e s) queue
        let ofSeq (s : seq<AtomicOperation<'a, 'b>>) = s |> Seq.fold (fun q e -> enqueue e q) empty
        let ofList (l : list<AtomicOperation<'a, 'b>>) = l |> List.fold (fun q e -> enqueue e q) empty
        let ofArray (a : array<AtomicOperation<'a, 'b>>) = a |> Array.fold (fun q e -> enqueue e q) empty
                
        let toSeq (queue : AtomicQueue<'a, 'b>) = queue :> seq<_>
        let toList (queue : AtomicQueue<'a, 'b>) = queue |> Seq.toList
        let toArray (queue : AtomicQueue<'a, 'b>) = queue |> Seq.toArray
        

    [<StructuredFormatDisplay("AsString")>]
    type GeometryInstance =
        {
            signature       : GeometrySignature
            instanceCount   : int
            indexCount      : int
            vertexCount     : int
            geometry        : IndexedGeometry
            uniforms        : MapExt<string, Array>
        }

        override x.ToString() =
            if x.instanceCount > 1 then
                if x.indexCount > 0 then
                    sprintf "gi(%d, %d, %d)" x.instanceCount x.indexCount x.vertexCount
                else
                    sprintf "gi(%d, %d)" x.instanceCount x.vertexCount
            else
                if x.indexCount > 0 then
                    sprintf "g(%d, %d)" x.indexCount x.vertexCount
                else
                    sprintf "g(%d)" x.vertexCount
              
        member private x.AsString = x.ToString()

    module GeometryInstance =

        let inline signature (g : GeometryInstance) = g.signature
        let inline instanceCount (g : GeometryInstance) = g.instanceCount
        let inline indexCount (g : GeometryInstance) = g.indexCount
        let inline vertexCount (g : GeometryInstance) = g.vertexCount
        let inline geometry (g : GeometryInstance) = g.geometry
        let inline uniforms (g : GeometryInstance) = g.uniforms

        let ofGeometry (iface : GLSLProgramInterface) (g : IndexedGeometry) (u : MapExt<string, Array>) =
            let instanceCount =
                if MapExt.isEmpty u then 1
                else u |> MapExt.toSeq |> Seq.map (fun (_,a) -> a.Length) |> Seq.min

            let indexCount, vertexCount =
                if g.IsIndexed then
                    let i = g.IndexArray.Length
                    let v = 
                        if g.IndexedAttributes.Count = 0 then 0
                        else g.IndexedAttributes.Values |> Seq.map (fun a -> a.Length) |> Seq.min
                    i, v
                else
                    0, g.FaceVertexCount

            {
                signature = GeometrySignature.ofGeometry iface u g
                instanceCount = instanceCount
                indexCount = indexCount
                vertexCount = vertexCount
                geometry = g
                uniforms = u
            }

        let load (iface : GLSLProgramInterface) (load : Set<string> -> IndexedGeometry *  MapExt<string, Array>) =
            let wanted = iface.inputs |> List.map (fun p -> p.paramSemantic) |> Set.ofList
            let (g,u) = load wanted

            ofGeometry iface g u

    type MaterializedTree =
        {
            rootId      : int
            original    : ITreeNode
            children    : list<MaterializedTree>
        }

    [<RequireQualifiedAccess>]
    type NodeOperation =
        | Split
        | Collapse of children : list<ITreeNode>
        | Add
        | Remove of children : list<ITreeNode>
        

    module MaterializedTree =
        
        let inline original (node : MaterializedTree) = node.original
        let inline children (node : MaterializedTree) = node.children

        let ofNode (id : int) (node : ITreeNode) =
            {
                rootId = id
                original = node
                children = []
            }

        
        let rec allNodes (node : MaterializedTree) =
            Seq.append 
                (Seq.singleton node)
                (node.children |> Seq.collect allNodes)

        let allChildren (node : MaterializedTree) =
            node.children |> Seq.collect allNodes

        let rec tryExpand (quality : float) (predictView : ITreeNode -> Trafo3d) (view : Trafo3d) (proj : Trafo3d) (t : MaterializedTree) =
            let node = t.original

            let inline tryExpandMany (ls : list<MaterializedTree>) =
                let mutable changed = false
                let newCs = 
                    ls |> List.map (fun c ->
                        match tryExpand quality predictView view proj c with
                            | Some newC -> 
                                changed <- true
                                newC
                            | None ->
                                c
                    )
                if changed then Some newCs
                else None
                
            if List.isEmpty t.children && node.ShouldSplit(quality, view, proj) then //&& node.ShouldSplit(predictView node, proj) then
                Some { t with children = node.Children |> Seq.toList |> List.map (ofNode t.rootId) }

            elif not (List.isEmpty t.children) && node.ShouldCollapse(quality, view, proj) then
                Some { t with children = [] }

            else
                match t.children with
                    | [] ->
                        None

                    | children ->
                        match tryExpandMany children with
                            | Some newChildren -> Some { t with children = newChildren }
                            | _ -> None

        let expand (quality : float) (predictView : ITreeNode -> Trafo3d) (view : Trafo3d) (proj : Trafo3d) (t : MaterializedTree) =
            match tryExpand quality predictView view proj t with
                | Some n -> n
                | None -> t

        let rec computeDelta (acc : hmap<ITreeNode, int * NodeOperation>) (o : MaterializedTree) (n : MaterializedTree) =
            if System.Object.ReferenceEquals(o,n) then
                acc
            else

                let rec computeChildDeltas (acc : hmap<ITreeNode, int * NodeOperation>) (os : list<MaterializedTree>) (ns : list<MaterializedTree>) =
                    match os, ns with
                        | [], [] -> 
                            acc
                        | o :: os, n :: ns ->
                            let acc = computeDelta acc o n
                            computeChildDeltas acc os ns
                        | _ ->
                            failwith "inconsistent child count"
                            
                if o.original = n.original then
                    match o.children, n.children with
                        | [], []    -> acc
                        | [], _     -> HMap.add n.original (n.rootId, NodeOperation.Split) acc
                        | oc, []    -> 
                            let children = allChildren o |> Seq.map original |> Seq.toList
                            HMap.add n.original (n.rootId, NodeOperation.Collapse(children)) acc
                        | os, ns    -> computeChildDeltas acc os ns
                else
                    failwith "inconsistent child values"



    type RenderState =
        {
            iface : GLSLProgramInterface
            calls : DrawPool
            mutable allocs : int
            mutable uploadSize : Mem
            mutable nodeSize : int
            mutable count : int
        }

    type LodRenderer(ctx : Context, state : PreparedPipelineState, pass : RenderPass, useCulling : bool, roots : aset<ITreeNode * MapExt<string, IMod>>, renderTime : IMod<_>, model : IMod<Trafo3d>, view : IMod<Trafo3d>, proj : IMod<Trafo3d>)  =
        inherit PreparedCommand(ctx, pass)

        let manager = (unbox<Runtime> ctx.Runtime).ResourceManager
        let signature = state.pProgramInterface

        let timeWatch = System.Diagnostics.Stopwatch.StartNew()
        let time() = timeWatch.MicroTime
        
        let pool = GeometryPool.Get ctx
        

        let reader = roots.GetReader()
        let euclideanView = view |> Mod.map Euclidean3d

  
        let loadTimes = System.Collections.Concurrent.ConcurrentDictionary<Symbol, Regression>()
        let expandTime = RunningMean(5)
        let updateTime = RunningMean(4)

        let addLoadTime (kind : Symbol) (size : int) (t : MicroTime) =
            let mean = loadTimes.GetOrAdd(kind, fun _ -> Regression(1, 100))
            lock mean (fun () -> mean.Add(size, t))

        let getLoadTime (kind : Symbol) (size : int) =
            match loadTimes.TryGetValue kind with
                | (true, mean) -> 
                    lock mean (fun () -> mean.Evaluate size)
                | _ -> 
                    200.0 * ms

        
        let cache = Dict<ITreeNode, PoolSlot>()

        let needUpdate = Mod.init ()
        let renderingStateLock = obj()
        let mutable renderingConverged = 1
        let mutable renderDelta : AtomicQueue<ITreeNode, GeometryInstance> = AtomicQueue.empty
        let mutable deltaEmpty = true
        
        let rootIdsLock = obj()
        let rootIds : ModRef<hmap<ITreeNode, int>> = Mod.init HMap.empty

        let mutable rootUniforms : hmap<ITreeNode, MapExt<string, IMod>> = HMap.empty
        
        let rootUniformCache = System.Collections.Concurrent.ConcurrentDictionary<ITreeNode, System.Collections.Concurrent.ConcurrentDictionary<string, Option<IMod>>>()
        let rootTrafoCache = System.Collections.Concurrent.ConcurrentDictionary<ITreeNode, IMod<Trafo3d>>()

        let getRootTrafo (root : ITreeNode) =
            rootTrafoCache.GetOrAdd(root, fun root ->
                match HMap.tryFind root rootUniforms with
                | Some table -> 
                    match MapExt.tryFind "ModelTrafo" table with
                    | Some (:? IMod<Trafo3d> as m) -> model %* m
                    | _ -> model
                | None ->
                    model
            )
                
        let getRootUniform (name : string) (root : ITreeNode) : Option<IMod> =
            let rootCache = rootUniformCache.GetOrAdd(root, fun root -> System.Collections.Concurrent.ConcurrentDictionary())
            rootCache.GetOrAdd(name, fun name ->
                match name with
                | "ModelTrafos"              -> getRootTrafo root :> IMod |> Some
                | "ModelViewTrafos"          -> Mod.map2 (fun a b -> a * b) (getRootTrafo root) view :> IMod |> Some
                | "ModelViewProjTrafos"      -> getRootTrafo root %* view %* proj :> IMod |> Some
                | "ModelTrafoInvs"           -> getRootTrafo root |> Mod.map (fun t -> t.Inverse) :> IMod |> Some
                | "ModelViewTrafoInvs"       -> getRootTrafo root %* view |> Mod.map (fun t -> t.Inverse) :> IMod |> Some
                | "ModelViewTrafoProjInvs"   -> getRootTrafo root %* view %* proj |> Mod.map (fun t -> t.Inverse) :> IMod |> Some
                | _ -> 
                    match HMap.tryFind root rootUniforms with
                    | Some table -> MapExt.tryFind name table
                    | None -> None
            )

        let getRootId (root : ITreeNode) =
            match HMap.tryFind root rootIds.Value with
            | Some id -> 
                id
            | None ->
                transact (fun () -> 
                    lock rootIdsLock (fun () ->
                        let ids = Set.ofSeq (Seq.map snd (HMap.toSeq rootIds.Value))
                        let free = Seq.initInfinite id |> Seq.find (fun i -> not (Set.contains i ids))
                        let n = HMap.add root free rootIds.Value
                        rootIds.Value <- n
                        free
                    )
                )

        let freeRootId (root : ITreeNode) =
            rootUniformCache.TryRemove root |> ignore
            rootTrafoCache.TryRemove root |> ignore
            transact (fun () ->
                lock rootIdsLock (fun () ->
                    rootIds.Value <- HMap.remove root rootIds.Value
                )
            )


        let contents =
            state.pProgramInterface.storageBuffers |> MapExt.toSeq |> Seq.choose (fun (name, buffer) ->
                if Map.containsKey buffer.ssbBinding state.pStorageBuffers then
                    None
                else
                    let typ = GLSLType.toType buffer.ssbType
                    let conv = PrimitiveValueConverter.convert typ
                    
                    let content =
                        Mod.custom (fun t ->
                            let st = time()
                            let ids = rootIds.GetValue t
                            if HMap.isEmpty ids then
                                ArrayBuffer (System.Array.CreateInstance(typ, 0)) :> IBuffer
                            else
                                let maxId = ids |> HMap.toSeq |> Seq.map snd |> Seq.max
                                let data = System.Array.CreateInstance(typ, 1 + maxId)
                                ids |> HMap.iter (fun root id ->
                                    match getRootUniform name root with
                                    | Some v ->
                                        let v = v.GetValue(t) |> conv
                                        data.SetValue(v, id)
                                    | None ->
                                        ()
                                )
                                let dt = time() - st
                                if dt.TotalMilliseconds > 5.0 then Log.warn "long upload"
                                ArrayBuffer data :> IBuffer
                        )
                    Some (buffer.ssbBinding, content)
            )
            |> Map.ofSeq
            
        let storageBuffers =
            contents |> Map.map (fun _ content ->
                let b = manager.CreateBuffer(content)
                b.AddRef()
                b.Update(AdaptiveToken.Top, RenderToken.Empty)
                b
            )

        let activeBuffer =
            let data = 
                Mod.custom (fun t ->
                    let ids = rootIds.GetValue t
                    if HMap.isEmpty ids then
                        ArrayBuffer (Array.empty<int>) :> IBuffer
                    else
                        let maxId = ids |> HMap.toSeq |> Seq.map snd |> Seq.max
                        let data : int[] = Array.zeroCreate (1 + maxId)
                        ids |> HMap.iter (fun root id ->
                            match getRootUniform "TreeActive" root with
                            | Some v ->
                                match v.GetValue(t) with
                                | :? bool as b ->
                                    data.[id] <- (if b then 1 else 0)
                                | _ ->
                                    data.[id] <- 1
                            | None ->
                                data.[id] <- 1
                        )
                        ArrayBuffer data :> IBuffer
                )
            manager.CreateBuffer data

        let modelViewProjBuffer =
            let data = 
                Mod.custom (fun t ->
                    let st = time()
                    let ids = rootIds.GetValue t
                    if HMap.isEmpty ids then
                        ArrayBuffer (Array.empty<M44f>) :> IBuffer
                    else
                        let maxId = ids |> HMap.toSeq |> Seq.map snd |> Seq.max
                        let data : M44f[] = Array.zeroCreate (1 + maxId)
                        ids |> HMap.iter (fun root id ->
                            match getRootUniform "ModelViewProjTrafos" root with
                            | Some v ->
                                match v.GetValue(t) with
                                | :? Trafo3d as b ->
                                    data.[id] <- M44f.op_Explicit b.Forward
                                | _ ->
                                    failwith "bad anarchy"
                            | None ->
                                    failwith "bad anarchy"
                        )
                        let dt = time() - st
                        if dt.TotalMilliseconds > 5.0 then Log.warn "long upload"
                        ArrayBuffer data :> IBuffer
                )
            manager.CreateBuffer data
            
        let allocWatch = System.Diagnostics.Stopwatch()
        let uploadWatch = System.Diagnostics.Stopwatch()
        let activateWatch = System.Diagnostics.Stopwatch()
        let freeWatch = System.Diagnostics.Stopwatch()
        let deactivateWatch = System.Diagnostics.Stopwatch()

        let alloc (state : RenderState) (node : ITreeNode) (g : GeometryInstance) =
            cache.GetOrCreate(node, fun node ->
                let slot = pool.Alloc(g.signature, g.instanceCount, g.indexCount, g.vertexCount)
                slot.Upload(g.geometry, g.uniforms)

                state.uploadSize <- state.uploadSize + slot.Memory
                state.nodeSize <- state.nodeSize + node.DataSize
                state.count <- state.count + 1
                slot
            )
            
        let performOp (state : RenderState) (parentOp : AtomicOperation<ITreeNode, GeometryInstance>) (node : ITreeNode) (op : Operation<GeometryInstance>) =
            let rootId = 
                match HMap.tryFind node.Root rootIds.Value with
                | Some id -> id
                | _ -> -1
            
            match op with
                | Alloc(instance, active) ->
                    inc &state.allocs
                    let slot = alloc state node instance
                    if active > 0 then state.calls.Add(slot, node.BoundingBox, rootId) |> ignore
                    elif active < 0 then state.calls.Remove slot |> ignore
                  
                    //frees.Remove(node) |> ignore

                | Free ->
                    freeWatch.Start()
                    match cache.TryRemove node with
                        | (true, slot) -> 
                            state.calls.Remove slot |> ignore
                            pool.Free slot
                            //let r = frees.GetOrCreate(node, fun _ -> ref Unchecked.defaultof<_>)
                            //r := parentOp
                        | _ ->
                            ()
                            //Log.warn "cannot free %s" node.Name
                    freeWatch.Stop()

                | Activate ->
                    activateWatch.Start()
                    match cache.TryGetValue node with
                        | (true, slot) ->
                            state.calls.Add(slot, node.BoundingBox, rootId) |> ignore
                        | _ ->
                            ()
                            //match frees.TryGetValue(node) with
                            //    | (true, r) ->
                            //        let evilOp = !r
                            //        Log.warn "cannot activate %A %A (%A)" node (Option.isSome op.value) evilOp 
                            //    | _ ->
                            //        Log.warn "cannot activate %A %A (no reason)" node (Option.isSome op.value)
                    activateWatch.Stop()

                | Deactivate ->
                    deactivateWatch.Start()
                    match cache.TryGetValue node with
                        | (true, slot) ->
                            state.calls.Remove slot |> ignore
                        | _ ->
                            ()
                    deactivateWatch.Stop()
                | Nop ->
                    ()
            
        let perform (state : RenderState) (op : AtomicOperation<ITreeNode, GeometryInstance>) =
            op.ops |> HMap.iter (performOp state op)
            

        //let glWait() =
        //    let fence = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, WaitSyncFlags.None)
        //    let mutable status = GL.ClientWaitSync(fence, ClientWaitSyncFlags.SyncFlushCommandsBit, 0L)
        //    while status <> WaitSyncStatus.AlreadySignaled && status <> WaitSyncStatus.ConditionSatisfied do
        //        status <- GL.ClientWaitSync(fence, ClientWaitSyncFlags.SyncFlushCommandsBit, 0L)
        let rec enter (l : obj) =
            let gotLock = Monitor.TryEnter(l, 5)
            if gotLock then ()
            else 
                Log.warn "timeout"
                enter l

        let sync() =
            let t = time()
            GL.Flush()
            GL.Finish()
            let n = time()
            let dt = n - t
            if dt.TotalMilliseconds > 5.0 then Log.warn "long sync %A" dt

        let run (token : AdaptiveToken) (maxMem : Mem) (maxTime : MicroTime) (calls : DrawPool) (iface : GLSLProgramInterface) =
            sync()

            let sw = System.Diagnostics.Stopwatch.StartNew()
      
            let state =
                {
                    iface = iface
                    calls = calls
                    allocs = 0
                    uploadSize = Mem.Zero
                    nodeSize = 0
                    count = 0
                }

            allocWatch.Reset()
            uploadWatch.Reset()
            activateWatch.Reset()
            freeWatch.Reset()
            deactivateWatch.Reset()


            let rec run (cnt : int)  =
                let mem = state.uploadSize > maxMem
                let time = sw.MicroTime > maxTime 

                if mem || time then
                    if state.nodeSize > 0 && state.count > 0 then
                        updateTime.Add(sw.MicroTime.TotalMilliseconds)
                        let took = sw.MicroTime
                        if took > MicroTime.FromMilliseconds 10.0 then
                            Log.warn "long update: %A (%d, %A, %A, %A, %A, %A, %A)" took state.allocs state.uploadSize allocWatch.MicroTime uploadWatch.MicroTime freeWatch.MicroTime activateWatch.MicroTime deactivateWatch.MicroTime
                        else 
                            Log.line "long update: %A (%d, %A, %A, %A, %A, %A, %A)" took state.allocs state.uploadSize allocWatch.MicroTime uploadWatch.MicroTime freeWatch.MicroTime activateWatch.MicroTime deactivateWatch.MicroTime

                    renderTime.GetValue token |> ignore
                else
                    let dequeued = 
                        enter renderingStateLock
                        try
                            match AtomicQueue.tryDequeue renderDelta with
                            | Some (ops, rest) ->
                                renderDelta <- rest
                                deltaEmpty <- AtomicQueue.isEmpty rest
                                Some ops
                            | None ->
                                None
                        finally
                            Monitor.Exit renderingStateLock

                    match dequeued with
                    | None -> 
                        if state.nodeSize > 0 && state.count > 0 then
                            updateTime.Add(sw.MicroTime.TotalMilliseconds)
                            let took = sw.MicroTime
                            if took > MicroTime.FromMilliseconds 10.0 then
                                Log.warn "long update: %A (%d, %A, %A, %A, %A, %A, %A)" took state.allocs state.uploadSize allocWatch.MicroTime uploadWatch.MicroTime freeWatch.MicroTime activateWatch.MicroTime deactivateWatch.MicroTime
                            else 
                                Log.line "long update: %A (%d, %A, %A, %A, %A, %A, %A)" took state.allocs state.uploadSize allocWatch.MicroTime uploadWatch.MicroTime freeWatch.MicroTime activateWatch.MicroTime deactivateWatch.MicroTime

                        renderingConverged <- 1

                    | Some ops ->
                        perform state ops
                        sync()
                        run (cnt + 1)

            //enter renderingStateLock
            run 0
            //finally Monitor.Exit renderingStateLock

        let evaluate (calls : DrawPool) (token : AdaptiveToken) (iface : GLSLProgramInterface) =
            let t = time()
            needUpdate.GetValue(token)
            let dt = time() - t
            if dt.TotalMilliseconds > 5.0 then Log.warn "long pull: %A" dt

            let maxTime = max (1 * ms) calls.AverageRenderTime
            let maxMem = Mem (3L <<< 30)
            run token maxMem maxTime calls iface
            sync()
            let dt = time() - t
            if dt.TotalMilliseconds > 10.0 then
                Log.warn "update: %A" dt
            
        let inner =
            { new DrawPool(ctx, useCulling, activeBuffer.Pointer, modelViewProjBuffer.Pointer, state, pass) with
                override x.Evaluate(token : AdaptiveToken, iface : GLSLProgramInterface) =
                    evaluate x token iface

                override x.BeforeRender(stream : ICommandStream) =
                    for (slot, b) in Map.toSeq storageBuffers do 
                        stream.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, slot, b.Pointer)

            }

        
        let shutdown = new CancellationTokenSource()

        
        let cameraPrediction, thread =
            let prediction = Prediction.euclidean (MicroTime(TimeSpan.FromMilliseconds 55.0))
            let predictedTrafo = prediction |> Prediction.map Trafo3d
    
            let mutable v = 0
            let newVersion() = Interlocked.Increment(&v)
        
            let mutable baseState : hmap<ITreeNode, MaterializedTree> = HMap.empty
            let mutable baseVersion = newVersion()

            let cameraPrediction =
                startThread (fun () ->
                    let mutable lastTime = time()
                    let mutable lastReport = time()
                    let timer = new MultimediaTimer.Trigger(10)
                    
                    while not shutdown.IsCancellationRequested do
                        timer.Wait()
                        let view = euclideanView.GetValue()
                        prediction.Add(view)

                        let flushTime = max (10*ms) inner.AverageRenderTime
                        let now = time()
                        if not deltaEmpty && (now - lastTime > flushTime) then
                            lastTime <- now
                            transact (fun () -> needUpdate.MarkOutdated())
                            

                        if now - lastReport > 2*s then
                            lastReport <- now
                            //let cnt = baseState |> Seq.sumBy (fun (_,s) -> s.Count)

                            let linearRegressionStr (parameter : string) (r : Regression) =
                                let d = r.Evaluate(0)
                                let k = r.Evaluate(1) - d
                                match d = MicroTime.Zero, k = MicroTime.Zero with
                                    | true, true    -> sprintf "0" 
                                    | true, false   -> sprintf "%s*%A" parameter k
                                    | false, true   -> sprintf "%A" d
                                    | false, false  -> sprintf "%s*%A+%A" parameter k d
                                

                            let loads = 
                                loadTimes 
                                |> Seq.map (fun (KeyValue(n,r)) -> 
                                    sprintf "%s: %s" (string n) (linearRegressionStr "s" r)
                                )
                                |> String.concat "; "
                                |> sprintf "[%s]"
                                
                            let e = expandTime.Average |> MicroTime.FromMilliseconds
                            let u = updateTime.Average |> MicroTime.FromMilliseconds

                            Log.line "m: %A (%A) r: %A l : %s e: %A u: %A" (pool.UsedMemory + inner.UsedMemory) (pool.TotalMemory + inner.TotalMemory) inner.AverageRenderTime loads e u

                )

            let submit (op : AtomicOperation<ITreeNode, GeometryInstance>) =  
                renderDelta <- AtomicQueue.enqueue op renderDelta
                deltaEmpty <- AtomicQueue.isEmpty renderDelta

            let computeDeltas (o : hmap<'a, MaterializedTree>) (n : hmap<'a, MaterializedTree>) : hmap<ITreeNode, int * NodeOperation> =
                HMap.choose2 (fun _ l r -> Some (l, r)) o n
                |> Seq.fold (fun delta (_,(l, r)) ->
                    match l, r with
                        | None, None -> 
                            delta

                        | Some l, Some r -> 
                            MaterializedTree.computeDelta delta l r

                        | Some l, None -> 
                            let children = MaterializedTree.allChildren l |> Seq.map MaterializedTree.original |> Seq.toList
                            HMap.add l.original (l.rootId, NodeOperation.Remove children) delta

                        | None, Some r -> 
                            assert (r.original.Level = 0)
                            HMap.add r.original (r.rootId, NodeOperation.Add) delta
                    
                ) HMap.empty
                
            let scheduler = new LimitedConcurrencyLevelTaskScheduler(ThreadPriority.Normal, 1)
            
            let startTask (ct : CancellationToken) (f : unit -> 'a) =
                Task.Factory.StartNew(Func<_>(f), ct, TaskCreationOptions.None, scheduler)
                
            let pull (t : AdaptiveToken) (f : AdaptiveToken -> 'r) =
                let t = t.Isolated
                let res = f t
                t.Release()
                res

            let thread = 
                startThread (fun () ->
                    let mutable runningTasks = 0
                    let mutable loadingState = HMap.empty
                    let mutable loadingVersion = newVersion()
                    let notConverged = new ManualResetEventSlim(true)

                    let cancel = System.Collections.Concurrent.ConcurrentDictionary<ITreeNode, CancellationTokenSource>()
                    let timer = new MultimediaTimer.Trigger(1)
                    
                    let stop (node : ITreeNode) =
                        match cancel.TryRemove node with
                            | (true, c) -> 
                                try c.Cancel()
                                with :? ObjectDisposedException -> ()
                            | _ -> 
                                ()
         
                    let load (ct : CancellationToken) (rootId : int) (node : ITreeNode) (cont : CancellationToken -> ITreeNode -> GeometryInstance -> 'r) =
                        startTask ct (fun () ->
                            let startTime = time()
                            let (g,u) = node.GetData(ct)

                            let cnt = 
                                match Seq.tryHead u with
                                | Some (KeyValue(_, (v : Array) )) -> v.Length
                                | _ -> 1

                            let u = MapExt.add "TreeId" (Array.create cnt rootId :> System.Array) u
                            let loaded = GeometryInstance.ofGeometry signature g u
                                
                            let endTime = time()
                            addLoadTime node.DataSource node.DataSize (endTime - startTime)


                            if not ct.IsCancellationRequested then
                                cont ct node loaded
                            else
                                raise <| OperationCanceledException()
                        )


                    let remove (node : ITreeNode) (children : list<ITreeNode>) =
                        node :: children
                        |> List.map (fun c -> c, Free)
                        |> AtomicOperation.ofList
                                    
                    let split (node : ITreeNode) (children : hmap<ITreeNode, GeometryInstance>) =
                        children 
                        |> HMap.map (fun n i -> Alloc(i, true))
                        |> HMap.add node Deactivate
                        |> AtomicOperation.ofHMap
                                    
                    let collapse (node : ITreeNode) (children : list<ITreeNode>) =
                        children 
                        |> List.map (fun c -> c, Free)
                        |> HMap.ofList
                        |> HMap.add node Activate
                        |> AtomicOperation.ofHMap
                                    
                    let add (node : ITreeNode) (instance : GeometryInstance) =
                        AtomicOperation.ofList [ node, Alloc(instance, true) ]
                            
                    let mutable lastView = Trafo3d.Identity
                    let mutable quality = 1.0 //0.1

                    //let caller = AdaptiveObject()
                    //let sub = caller.AddMarkingCallback (fun () -> notConverged.Set())

                    let subs =
                        Dict.ofList [
                            view :> IAdaptiveObject, view.AddMarkingCallback (fun () -> notConverged.Set())
                            proj :> IAdaptiveObject, proj.AddMarkingCallback (fun () -> notConverged.Set())
                            reader :> IAdaptiveObject, reader.AddMarkingCallback (fun () -> notConverged.Set())
                        ]


                    try 
                        while not shutdown.IsCancellationRequested do
                            timer.Wait()
                            notConverged.Wait(shutdown.Token)
                            //caller.EvaluateAlways AdaptiveToken.Top (fun token ->
                            let view = view.GetValue AdaptiveToken.Top
                            let proj = proj.GetValue AdaptiveToken.Top
                            let ops = reader.GetOperations AdaptiveToken.Top
                            
                            let deltas = 
                                if HDeltaSet.isEmpty ops then
                                    let viewChanged = V3d.ApproxEqual(lastView.Backward.C3.XYZ, view.Backward.C3.XYZ, 1E-7) |> not
                                    lastView <- view
                                    //if viewChanged then quality <- 0.1
                                    
                                    let startTime = time()

                                    let predictedView = view

                                    let predictView (node : ITreeNode) =
                                        let tLoad = getLoadTime node.DataSource node.DataSize
                                        let tActivate = max (20*ms) inner.AverageRenderTime
                                        let tUpload = updateTime.Average |> MicroTime.FromMilliseconds
                                        let tRender = inner.AverageRenderTime
                                        let loadTime = tLoad + tActivate + tUpload + tRender
                                        predictedTrafo.Predict loadTime |> Option.defaultValue predictedView
                            
                                    let newState = 
                                        baseState |> HMap.map (fun o t -> 
                                            let m = getRootTrafo(o.Root)

                                            subs.GetOrCreate(m, fun _ -> m.AddMarkingCallback(notConverged.Set)) |> ignore

                                            let m = m.GetValue(AdaptiveToken.Top)

                                            let predictedView = m * predictedView
                                            let predictView n = m * predictView n
                                                
                                            MaterializedTree.expand quality predictView predictedView proj t
                                        )

                                    let deltas = computeDeltas loadingState newState
                                    loadingState <- newState

                                    let dt = time() - startTime
                                    expandTime.Add(dt.TotalMilliseconds)

                                    deltas

                                else 
                                    let mutable newState = loadingState
                                    for o in ops do
                                        match o with
                                        | Add(_,(root, u)) -> 
                                            rootUniforms <- HMap.add root u rootUniforms
                                            let id = getRootId root
                                            let r = MaterializedTree.ofNode id root
                                            
                                            baseState <- HMap.add root r baseState
                                            newState <- HMap.add root r newState

                                        | Rem(_,(root,_)) -> 
                                            rootUniforms <- HMap.remove root rootUniforms
                                            match subs.TryRemove (getRootTrafo root) with
                                            | (true, s) -> s.Dispose()
                                            | _ -> ()
                                            freeRootId root

                                            baseState <- HMap.remove root baseState
                                            newState <- HMap.remove root newState

                                    let deltas = computeDeltas loadingState newState
                                    loadingState <- newState

                                    baseVersion <- newVersion()
                                    deltas
                              
                     
                            if HMap.isEmpty deltas then
                                if renderingConverged = 1 && runningTasks = 0 then
                                    if baseVersion = loadingVersion && quality >= 1.0 then
                                        notConverged.Reset()
                                    else
                                        baseState <- loadingState
                                        baseVersion <- loadingVersion
                                        quality <- min 1.0 (quality + 0.01)
                            else
                                renderingConverged <- 0
                                loadingVersion <- newVersion()

                                for (v, (rootId, delta)) in deltas do
                                    stop v
                                    

                                    match delta with
                                        | NodeOperation.Remove children ->
                                            let op = remove v children
                                            lock renderingStateLock (fun () ->
                                                submit op
                                            )

                                        | NodeOperation.Split ->
                                            let c = new CancellationTokenSource()
                                        
                                            let loadChildren = 
                                                v.Children |> Seq.toList |> List.map (fun v -> 
                                                    let t = load c.Token rootId v (fun _ n l -> (n,l))
                                                    t
                                                )
                                        


                                            Interlocked.Increment(&runningTasks) |> ignore
                                        
                                            v.Children |> Seq.iter (fun ci -> cancel.[ci] <- c)
                                            cancel.[v] <- c

                                            let s = 
                                                c.Token.Register (fun () -> 
                                                    Interlocked.Decrement(&runningTasks) |> ignore
                                                    v.Children |> Seq.iter (fun ci -> cancel.TryRemove ci |> ignore)
                                                    cancel.TryRemove v |> ignore
                                                )
                                    
                                            let myTask = 
                                                Task.WhenAll(loadChildren).ContinueWith (fun (a : Task<array<_>>) ->
                                                    if a.IsCompleted && not a.IsCanceled && not a.IsFaulted then
                                                        let op = split v (HMap.ofArray a.Result)
                                                            
                                                        let cancel = 
                                                            lock renderingStateLock (fun () ->
                                                                if not c.IsCancellationRequested then
                                                                    submit op
                                                                    true
                                                                else
                                                                    false
                                                            )
                                                        if cancel then
                                                            try c.Cancel()
                                                            with _ -> ()

                                                    c.Dispose()
                                                )
                                        
                                            ()
                                        


                                        | NodeOperation.Collapse(children) ->
                                            children |> List.iter stop
                                            let myOp = collapse v children
                                            lock renderingStateLock (fun () ->
                                                submit myOp
                                            )


                                        | NodeOperation.Add ->
                                            let ct = CancellationToken.None
                                            Interlocked.Increment(&runningTasks) |> ignore
     
                                            load ct rootId v (fun _ v l ->
                                                let op = add v l
                                                lock renderingStateLock (fun () ->
                                                    submit op
                                                )
                                                Interlocked.Decrement(&runningTasks) |> ignore
                                            ) |> ignore
                 
                    finally 
                        subs |> Seq.iter (fun s -> s.Value.Dispose())
                )

            

            cameraPrediction, thread
        
        
        member x.UsedMemory : Mem = pool.UsedMemory + inner.UsedMemory
        member x.TotalMemory : Mem = pool.TotalMemory + inner.TotalMemory
        
        override x.Compile(a,b,c) = inner.Compile(a,b,c)
        override x.GetResources() = 
            Seq.concat [ 
                Seq.singleton (activeBuffer :> IResource)
                Seq.singleton (modelViewProjBuffer :> IResource)
                (storageBuffers |> Map.toSeq |> Seq.map snd |> Seq.cast) 
                (inner.Resources :> seq<_>)
            ]
        override x.Release() =
            shutdown.Cancel()
            cameraPrediction.Join()
            thread.Join()
            reader.Dispose()
            inner.Dispose()
            loadTimes.Clear()
            for slot in cache.Values do pool.Free slot
            cache.Clear()
            renderingConverged <- 1
            deltaEmpty <- true
            renderDelta <- AtomicQueue.empty
            storageBuffers |> Map.toSeq |> Seq.iter (fun (_,b) -> b.Dispose())
            activeBuffer.Dispose()

        override x.EntryState = inner.EntryState
        override x.ExitState = inner.ExitState


open Aardvark.Rendering.GL
open Aardvark.Application.Slim
open System
open System.Threading
open System.Threading.Tasks

module StoreTree =
    open Aardvark.Geometry
    open Aardvark.Geometry.Points
    open Aardvark.Data.Points.Import
    open Aardvark.Data.Points
    open Aardvark.Data.Points.Import


    type PointTreeNode(source : Symbol, globalTrafo : Trafo3d, root : Option<PointTreeNode>, parent : Option<PointTreeNode>, level : int, self : PointSetNode) as this =
        let bounds = self.BoundingBoxExact.Transformed globalTrafo
        
        let mutable cache = None

        static let (|Strong|_|) (w : WeakReference<'a>) =
            match w.TryGetTarget() with
                | (true, a) -> Some a
                | _ -> None

        let children =
            if isNull self.Subnodes then
                Seq.empty
            else
                let cache : list<ref<Option<Option<WeakReference<_>>>>> = self.Subnodes |> Seq.toList |> List.map (fun _ -> ref None)
                    
                Seq.delay (fun () ->
                    Seq.zip cache self.Subnodes
                    |> Seq.choose (fun (cache,node) ->
                        match !cache with
                            | Some None ->
                                None
                            | Some (Some (Strong node)) -> 
                                Some node

                            | Some (Some _) ->
                                let n = PointTreeNode(source, globalTrafo, Some this.Root, Some this, level+1, node.Value)
                                cache := Some (Some (WeakReference<_> n))
                                Some n

                            | None ->
                                if isNull node || isNull node.Value then
                                    cache := Some None
                                    None
                                else
                                    let n = PointTreeNode(source, globalTrafo, Some this.Root, Some this, level+1, node.Value)
                                    cache := Some (Some (WeakReference<_> n))
                                    Some n
                    )
                 
                )

        static let heatMapColors =
            let fromInt (i : int) =
                C4b(
                    byte ((i >>> 16) &&& 0xFF),
                    byte ((i >>> 8) &&& 0xFF),
                    byte (i &&& 0xFF),
                    255uy
                )

            Array.map fromInt [|
                0x1639fa
                0x2050fa
                0x3275fb
                0x459afa
                0x55bdfb
                0x67e1fc
                0x72f9f4
                0x72f8d3
                0x72f7ad
                0x71f787
                0x71f55f
                0x70f538
                0x74f530
                0x86f631
                0x9ff633
                0xbbf735
                0xd9f938
                0xf7fa3b
                0xfae238
                0xf4be31
                0xf29c2d
                0xee7627
                0xec5223
                0xeb3b22
            |]

        static let heat (tc : float) =
            let tc = clamp 0.0 1.0 tc
            let fid = tc * float heatMapColors.Length - 0.5

            let id = int (floor fid)
            if id < 0 then 
                heatMapColors.[0]
            elif id >= heatMapColors.Length - 1 then
                heatMapColors.[heatMapColors.Length - 1]
            else
                let c0 = heatMapColors.[id].ToC4f()
                let c1 = heatMapColors.[id + 1].ToC4f()
                let t = fid - float id
                (c0 * (1.0 - t) + c1 * t).ToC4b()



        let levelColor = heat(float level / 10.0).ToC3f()
        let overlayLevel (c : C4b) =
            let t = 0.0
            let c = levelColor * t + c.ToC3f() * (1.0 - t)
            c.ToC4b()

        let loadSphere =
            async {
                do! Async.SwitchToThreadPool()
                match cache with
                    | Some data -> 
                        return data
                    | _ ->
                        let! ct = Async.CancellationToken
                        let center = self.Center
                        let positions = 
                            let inline fix (p : V3f) = globalTrafo.Forward.TransformPos (V3d p + center) |> V3f
                            if self.HasLodPositions then self.LodPositions.GetValue(ct)  |> Array.map fix
                            elif self.HasPositions then self.Positions.GetValue(ct) |> Array.map fix
                            else [| V3f(System.Single.NaN, System.Single.NaN, System.Single.NaN) |]

                        let colors = 
                            if self.HasLodColors then self.LodColors.GetValue(ct)
                            elif self.HasColors then self.Colors.GetValue(ct) 
                            else positions |> Array.map (fun _ -> C4b.White)
                    
                        let normals = 
                            if self.HasLodNormals then self.LodNormals.GetValue(ct)
                            elif self.HasNormals then self.Normals.GetValue(ct) 
                            else positions |> Array.map (fun _ -> V3f.OOI)

                        let geometry = IndexedGeometryPrimitives.Sphere.solidPhiThetaSphere (Sphere3d(V3d.Zero, (bounds.Size.NormMax / 80.0))) 3 C4b.Red
                        geometry.IndexedAttributes.Remove(DefaultSemantic.Colors) |> ignore
                        geometry.IndexedAttributes.Remove(DefaultSemantic.Normals) |> ignore
                
                        let colors = colors |> Array.map overlayLevel

                        let uniforms =
                            MapExt.ofList [
                                "Colors", colors :> System.Array
                                "Offsets", positions :> System.Array
                                "Normals", normals :> System.Array
                            ]

                        cache <- Some (geometry, uniforms)
                        return geometry, uniforms
            }

        let load (ct : CancellationToken) =
            //async {
            //    do! Async.SwitchToThreadPool()
            //    let! ct = Async.CancellationToken
                let center = self.Center
                let positions = 
                    let inline fix (p : V3f) = globalTrafo.Forward.TransformPos (V3d p + center) |> V3f
                    if self.HasLodPositions then self.LodPositions.GetValue(ct)  |> Array.map fix
                    elif self.HasPositions then self.Positions.GetValue(ct) |> Array.map fix
                    else [| V3f(System.Single.NaN, System.Single.NaN, System.Single.NaN) |]

                let colors = 
                    if self.HasLodColors then self.LodColors.GetValue(ct)
                    elif self.HasColors then self.Colors.GetValue(ct) 
                    else positions |> Array.map (fun _ -> C4b.White)
                    
                let normals = 
                    try
                        if self.HasLodNormals then self.LodNormals.GetValue(ct)
                        elif self.HasNormals then self.Normals.GetValue(ct) 
                        else 
                            Array.create positions.Length V3f.OOI
                            //if self.HasKdTree then 
                            //    let tree = self.KdTree.Value
                            //    Aardvark.Geometry.Points.Normals.EstimateNormals(positions, tree, 17)
                            //elif self.HasLodKdTree then 
                            //    let tree = self.LodKdTree.Value
                            //    Aardvark.Geometry.Points.Normals.EstimateNormals(positions, tree, 17)
                            //else 
                            //    Aardvark.Geometry.Points.Normals.EstimateNormals(positions, 17)
                        with e ->
                            Log.warn "%A" e
                            Array.create positions.Length V3f.OOI

                let geometry =
                    IndexedGeometry(
                        Mode = IndexedGeometryMode.PointList,
                        IndexedAttributes =
                            SymDict.ofList [
                                DefaultSemantic.Positions, positions :> System.Array
                                DefaultSemantic.Colors, colors :> System.Array
                                DefaultSemantic.Normals, normals :> System.Array
                            ]
                    )
                
                let uniforms =
                    MapExt.ofList [
                        "TreeLevel", [| float32 level |] :> System.Array
                        "AvgPointDistance", [| float32 (bounds.Size.NormMax / 40.0) |] :> System.Array
                    ]


                geometry, uniforms
            //}

        let angle (view : Trafo3d) =
            let cam = view.Backward.C3.XYZ

            let avgPointDistance = bounds.Size.NormMax / 40.0

            let minDist = bounds.GetMinimalDistanceTo(cam)
            let minDist = max 0.01 minDist

            let angle = Constant.DegreesPerRadian * atan2 avgPointDistance minDist

            let factor = 1.0 //(minDist / 0.01) ** 0.1

            angle / factor

        member x.Root : PointTreeNode =
            match root with
            | Some r -> r
            | None -> x

        member x.Children = children

        member x.Id = self.Id

        member x.GetData(ct) = 
            load ct
            
        member x.ShouldSplit (quality : float, view : Trafo3d, proj : Trafo3d) =
            angle view > 0.4

        member x.ShouldCollapse (quality : float, view : Trafo3d, proj : Trafo3d) =
            angle view < 0.3

        member x.DataSource = source

        override x.ToString() = 
            sprintf "%s[%d]" (string x.Id) level

        interface Bla.ITreeNode with
            member x.Root = x.Root :> Bla.ITreeNode
            member x.Level = level
            member x.Name = string x.Id
            member x.DataSource = source
            member x.Parent = parent |> Option.map (fun n -> n :> Bla.ITreeNode)
            member x.Children = x.Children |> Seq.map (fun n -> n :> Bla.ITreeNode)
            member x.ShouldSplit(q,v,p) = x.ShouldSplit(q,v,p)
            member x.ShouldCollapse(q,v,p) = x.ShouldCollapse(q,v,p)
            member x.DataSize = int self.LodPointCount
            member x.GetData(ct) = x.GetData(ct)
            member x.BoundingBox = bounds

        override x.GetHashCode() = 
            HashCode.Combine(x.DataSource.GetHashCode(), self.Id.GetHashCode())

        override x.Equals o =
            match o with
                | :? PointTreeNode as o -> x.DataSource = o.DataSource && self.Id = o.Id
                | _ -> false

    let load (uniforms : list<string * IMod>) (sourceName : string) (trafo : Trafo3d) (folder : string) (key : string) =
        let store = PointCloud.OpenStore folder
        let points = store.GetPointSet(key, CancellationToken.None)
        
        
        let targetSize = 100.0
        let bounds = points.BoundingBox

        let trafo =
            Trafo3d.Translation(-bounds.Center) *
            Trafo3d.Scale(targetSize / bounds.Size.NormMax) * 
            trafo

        let source = Symbol.Create sourceName
        let root = PointTreeNode(source, trafo, None, None, 0, points.Root.Value) :> Bla.ITreeNode
        root, MapExt.ofList uniforms

    let importAscii (sourceName : string) (file : string) (store : string) (uniforms : list<string * IMod>) =
        let fmt = [| Ascii.Token.PositionX; Ascii.Token.PositionY; Ascii.Token.PositionZ; Ascii.Token.ColorR; Ascii.Token.ColorG; Ascii.Token.ColorB |]
        
        let store = PointCloud.OpenStore store
        let key = System.IO.Path.GetFileNameWithoutExtension(file).ToLower()
        let set = store.GetPointSet(key, CancellationToken.None)

        let points = 
            if isNull set then
                let cfg = 
                    ImportConfig.Default
                        .WithStorage(store)
                        .WithKey(key)
                        .WithVerbose(true)
                        .WithMaxChunkPointCount(10000000)
                        .WithEstimateNormals(Func<_,_>(fun a -> Aardvark.Geometry.Points.Normals.EstimateNormals(Seq.toArray a, 17) :> System.Collections.Generic.IList<V3f>))
                        
                let chunks = Import.Ascii.Chunks(file, fmt, cfg)
                let res = PointCloud.Chunks(chunks, cfg)
                store.Flush()
                res
            else
                set

        let targetSize = 100.0
        let bounds = points.BoundingBox

        let trafo =
            Trafo3d.Translation(-bounds.Center) *
            Trafo3d.Scale(targetSize / bounds.Size.NormMax)

        let source = Symbol.Create sourceName
        let root = PointTreeNode(source, trafo, None, None, 0, points.Root.Value) :> Bla.ITreeNode
        root, MapExt.ofList uniforms

    let import (sourceName : string) (file : string) (store : string) (uniforms : list<string * IMod>) =
        do Aardvark.Data.Points.Import.Pts.PtsFormat |> ignore
        do Aardvark.Data.Points.Import.E57.E57Format |> ignore
        
        let store = PointCloud.OpenStore store
            

        let key = System.IO.Path.GetFileNameWithoutExtension(file).ToLower()
        let set = store.GetPointSet(key, CancellationToken.None)

        let points = 
            if isNull set then
                let config3 = 
                    Aardvark.Data.Points.ImportConfig.Default
                        .WithStorage(store)
                        .WithKey(key)
                        .WithVerbose(true)
                        .WithMaxChunkPointCount(10000000)
                        .WithEstimateNormals(Func<_,_>(fun a -> Aardvark.Geometry.Points.Normals.EstimateNormals(Seq.toArray a, 17) :> System.Collections.Generic.IList<V3f>))
        
                let res = PointCloud.Import(file,config3)
                store.Flush()
                res
            else
                set

        let targetSize = 100.0
        let bounds = points.BoundingBox

        let trafo =
            Trafo3d.Translation(-bounds.Center) *
            Trafo3d.Scale(targetSize / bounds.Size.NormMax) 

        let source = Symbol.Create sourceName
        let root = PointTreeNode(source, trafo, None, None, 0, points.Root.Value) :> Bla.ITreeNode
        root, MapExt.ofList uniforms




type StupidOctreeNode(root : Option<StupidOctreeNode>, parent : Option<StupidOctreeNode>, level : int, bounds : Box3d) as this =
    static let DataSource = Symbol.Create "CPU"
    static let rand = RandomSystem()
    

    let color =     
        rand.UniformC3f().ToC4b()

    let children =
        lazy [
            let half = bounds.Size / 2.0
            for x in 0 .. 1 do
                for y in 0 .. 1 do
                    for z in 0 .. 1 do
                        let off = bounds.Min + V3d(x,y,z) * half
                        yield StupidOctreeNode(Some this.Root, Some this, level + 1, Box3d.FromMinAndSize(off, half))
        ]

    let getAngle (view : Trafo3d) =
        if level >= 6 then
            0.0
        else
            let cam = view.Backward.TransformPos V3d.Zero

            let avgPointDistance = bounds.Size.NormMax / 20.0

            let distRange =
                bounds.ComputeCorners() 
                    |> Seq.map (fun p -> V3d.Distance(p, cam))
                    |> Range1d

            let distRange = 
                Range1d(max 1.0 distRange.Min, max 1.0 distRange.Max)

            Constant.DegreesPerRadian * atan2 avgPointDistance distRange.Min

    let shouldSplit (view : Trafo3d) (proj : Trafo3d) =
        getAngle view > 0.5
        
    let shouldCollapse (view : Trafo3d) (proj : Trafo3d) =
        getAngle view < 0.3

    let data =
        lazy (
            let positions =
                [|
                    let off = bounds.Min
                    let s = bounds.Size
                    for x in 0 .. 9 do
                        for  y in 0 .. 9 do
                            for z in 0 .. 9 do
                                yield V3f (off + (V3d(x,y,z)/10.0) * s)
                |]
            let geometry = 
                IndexedGeometry(
                    Mode = IndexedGeometryMode.PointList,
                    IndexedAttributes =
                        SymDict.ofList [
                            DefaultSemantic.Positions, positions :> Array
                        ]
                )
            geometry, MapExt.ofList ["Colors", [| color |] :> System.Array; "Normals", [| V3f.OOI |] :> System.Array]
        )

    let getData (ct) = data.Value
        
        
    member x.Root =
        match root with
        | Some r -> r
        | None -> x 

    override x.ToString() = string bounds
    interface Bla.ITreeNode with
        member x.Root = x.Root :> Bla.ITreeNode
        member x.Level = level
        member x.Name = string bounds
        member x.DataSource = DataSource
        member x.Parent = parent |> Option.map (fun n -> n :> Bla.ITreeNode)
        member x.Children = children.Value |> Seq.map (fun n -> n :> Bla.ITreeNode)
        member x.ShouldSplit (_,v,p) = shouldSplit v p
        member x.ShouldCollapse (_,v,p) = shouldCollapse v p
        member x.DataSize = 1024
        member x.GetData(ct) = getData (ct)
        member x.BoundingBox = bounds



module Shader =
    open FShade

    type UniformScope with
        member x.Overlay : V4d[] = x?StorageBuffer?Overlay
        member x.ModelTrafos : M44d[] = x?StorageBuffer?ModelTrafos
        member x.ModelViewTrafos : M44d[] = x?StorageBuffer?ModelViewTrafos

    type Vertex =
        {
            [<Position>] pos : V4d
            [<Normal>] n : V3d
            [<Semantic("Offsets")>] offset : V3d
        }


    let offset ( v : Vertex) =
        vertex {
            return  { v with pos = v.pos + V4d(v.offset, 0.0)}
        }
        
    
    type PointVertex =
        {
            [<Position>] pos : V4d
            [<Color>] col : V4d
            [<Normal>] n : V3d
            [<Semantic("ViewPosition")>] vp : V3d
            [<Semantic("AvgPointDistance")>] dist : float
            [<Semantic("DepthRange")>] depthRange : float
            [<PointSize>] s : float
            [<PointCoord>] c : V2d
            [<FragCoord>] fc : V4d
            [<Semantic("TreeId")>] id : int
        }

    let lodPointSize (v : PointVertex) =
        vertex { 
            let mv = uniform.ModelViewTrafos.[v.id]
            let ovp = mv * v.pos

            let vp = ovp + V4d(0.0, 0.0, 0.5*v.dist, 0.0)
            let vp1 = ovp + V4d(0.5 * v.dist, 0.0, 0.0, 0.0)

            let pp = uniform.ProjTrafo * vp
            let pp1 = uniform.ProjTrafo * vp1

            let ndcDist = abs (pp.X / pp.W - pp1.X / pp1.W)
            let depthRange = abs (pp.Z / pp.W - pp1.Z / pp1.W)

            let pixelDist = ndcDist * float uniform.ViewportSize.X
            let n = mv * V4d(v.n, 0.0) |> Vec.xyz |> Vec.normalize
            
            let pixelDist = 
                if pp.Z < -pp.W then -1.0
                else uniform.PointSize * pixelDist

            let o = uniform.Overlay.[v.id]
            let col = o.W * o.XYZ + (1.0 - o.W) * v.col.XYZ

            return { v with s = pixelDist; pos = pp; depthRange = depthRange; n = n; vp = ovp.XYZ; col = V4d(col, v.col.W) }
        }



    type Fragment =
        {
            [<Color>] c : V4d
            [<Depth(DepthWriteMode.OnlyGreater)>] d : float
        }

    let lodPointCircular (v : PointVertex) =
        fragment {
            let c = v.c * 2.0 - V2d.II
            let f = Vec.dot c c
            if f > 1.0 then discard()


            let t = 1.0 - sqrt (1.0 - f)
            let depth = v.fc.Z
            let outDepth = depth + v.depthRange * t
            

            return { c = v.col; d = outDepth }
        }

    let cameraLight (v : PointVertex) =
        fragment {
            let vn = Vec.normalize v.n
            let vd = Vec.normalize v.vp 

            let diffuse = Vec.dot vn vd |> abs
            return V4d(v.col.XYZ * diffuse, v.col.W)
        }




    let normalColor ( v : Vertex) =
        fragment {
            let mutable n = Vec.normalize v.n
            if n.Z < 0.0 then n <- -n

            let n = (n + V3d.III) * 0.5
            return V4d(n, 1.0)
        }

module Sg =
    open Aardvark.Base.Ag
    open Aardvark.SceneGraph
    open Aardvark.SceneGraph.Semantics

    type LodNode(culling : bool, time : IMod<DateTime>, clouds : aset<Bla.ITreeNode * MapExt<string, IMod>>) =
        member x.Culling = culling
        member x.Time = time
        member x.Clouds = clouds
        interface ISg
      
    [<Semantic>]
    type Sem() =
        member x.RenderObjects(sg : LodNode) =
            let scope = Ag.getContext()
            let state = PipelineState.ofScope scope
            let surface = sg.Surface
            let pass = sg.RenderPass

            let model = sg.ModelTrafo
            let view = sg.ViewTrafo
            let proj = sg.ProjTrafo

            let id = newId()
            let obj =
                { new ICustomRenderObject with
                    member x.Id = id
                    member x.AttributeScope = scope
                    member x.RenderPass = pass
                    member x.Create(r, fbo) = 
                        match r with
                        | :? Aardvark.Rendering.GL.Runtime as r ->
                            let preparedState = Aardvark.Rendering.GL.PreparedPipelineState.ofPipelineState fbo r.ResourceManager surface state
                            new Bla.LodRenderer(r.Context, preparedState, pass, sg.Culling, sg.Clouds, sg.Time, model, view, proj) :> IPreparedRenderObject
                        | _ ->
                            failwithf "[LoD] no vulkan backend atm."
                }

            ASet.single (obj :> IRenderObject)




[<EntryPoint>]
let main argv = 
    //System.Runtime.GCSettings.LatencyMode <- System.Runtime.GCLatencyMode.LowLatency

    System.Threading.ThreadPool.SetMinThreads(8,8) |> printfn "set: %A"
    System.Threading.ThreadPool.SetMaxThreads(8,8) |> printfn "set: %A"
    Ag.initialize()
    Aardvark.Init()


    let win =
        window {
            backend Backend.GL
            device DeviceKind.Dedicated
            display Display.Mono
            debug false
        }

    let pointSize = Mod.init 1.0
    let overlayAlpha = Mod.init 0.0
         
    let c0 = Mod.init V4d.IOOI
    let c1 = Mod.init V4d.OIOI
    let active0 = Mod.init true
    let active1 = Mod.init true

    let c0WithAlpha = Mod.map2 (fun (c : V4d) (a : float) -> V4d(c.XYZ, a)) c0 overlayAlpha
    let c1WithAlpha = Mod.map2 (fun (c : V4d) (a : float) -> V4d(c.XYZ, a)) c1 overlayAlpha

    let oktogon =
        StoreTree.import
            "ssd"
            @"\\euclid\rmDATA\Data\Schottenring_2018_02_23\Laserscans\2018-02-27_BankAustria\export\oktogon\Punktwolke\BLKgesamt.e57"
            @"C:\Users\Schorsch\Development\WorkDirectory\BLK"
            [
                "Overlay", c0WithAlpha :> IMod
                "TreeActive", active0 :> IMod
            ]
            
    let kaunertal =
        StoreTree.importAscii
            "ssd"
            @"C:\Users\Schorsch\Development\WorkDirectory\Kaunertal.txt"
            @"C:\Users\Schorsch\Development\WorkDirectory\KaunertalNormals"
            [
                "Overlay", c1WithAlpha :> IMod
                "TreeActive", active1 :> IMod
            ]




    let trafo = Mod.init Trafo3d.Identity

    let thread = 
        startThread (fun () ->  
            let mm = new MultimediaTimer.Trigger(10)
            let sw = System.Diagnostics.Stopwatch.StartNew()
            let mutable lastTime = sw.MicroTime
            while true do
                mm.Wait()
                let now = sw.MicroTime
                let dt = now - lastTime
                lastTime <- now

                transact (fun () -> trafo.Value <- trafo.Value * Trafo3d.RotationZ(dt.TotalSeconds * 0.1))


        )

    let koeln =
        StoreTree.import
            "ssd"
            @"C:\Users\Schorsch\Development\WorkDirectory\3278_5514_0_10\3278_5514_0_10"
            @"C:\Users\Schorsch\Development\WorkDirectory\3278_5514_0_10\pointcloud\"
            [
                "Overlay", c0WithAlpha :> IMod
                "ModelTrafo", trafo :> IMod
            ]

    //let koeln2 =
    //    StoreTree.import
    //        "disk"
    //        (Trafo3d.Translation(0.0, 0.0, 0.0)) 
    //        @"C:\Users\Schorsch\Development\WorkDirectory\3278_5514_0_10\3277_5514_0_10"
    //        @"D:\cells\3277_5514_0_10\pointcloud\"
    //        [
    //            "Overlay", c1WithAlpha :> IMod
    //        ]
        

    let pcs =
        ASet.ofList [
            //yield oktogon
            //yield kaunertal
            yield koeln
            //yield koeln2
        ]

    

    let sg =
        Sg.LodNode(true, win.Time, pcs) :> ISg
        |> Sg.uniform "PointSize" pointSize
        |> Sg.uniform "ViewportSize" win.Sizes
        |> Sg.shader {
            do! Shader.lodPointSize
            //do! Shader.cameraLight
            do! Shader.lodPointCircular
        }

    win.Keyboard.DownWithRepeats.Values.Add(fun k ->
        match k with
        | Keys.O -> transact (fun () -> pointSize.Value <- pointSize.Value / 1.3)
        | Keys.P -> transact (fun () -> pointSize.Value <- pointSize.Value * 1.3)
        | Keys.Subtract -> transact (fun () -> overlayAlpha.Value <- max 0.0 (overlayAlpha.Value - 0.1))
        | Keys.Add -> transact (fun () -> overlayAlpha.Value <- min 1.0 (overlayAlpha.Value + 0.1))

        | Keys.Left -> transact (fun () -> trafo.Value <- trafo.Value * Trafo3d.Translation(-20.0, 0.0, 0.0))
        | Keys.Right -> transact (fun () -> trafo.Value <- trafo.Value * Trafo3d.Translation(20.0, 0.0, 0.0))

        | Keys.D1 -> transact (fun () -> active0.Value <- not active0.Value); printfn "active0: %A" active0.Value
        | Keys.D2 -> transact (fun () -> active1.Value <- not active1.Value); printfn "active1: %A" active1.Value
        | Keys.Space -> 
            transact (fun () -> 
                let v = c0.Value
                c0.Value <- c1.Value
                c1.Value <- v
            )

        | _ -> ()
    )
    

    win.Scene <- sg
    win.Run()

    0
