open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application

#nowarn "9"

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
                match g.IndexedAttributes.TryGetValue sym with
                    | (true, arr) ->
                        assert(not (isNull arr))
                        let t = arr.GetType().GetElementType()
                        attributeTypes <- MapExt.add i.paramSemantic t attributeTypes
                    | _ -> 
                        match MapExt.tryFind i.paramSemantic uniforms with
                            | Some arr when not (isNull arr) ->
                                let t = arr.GetType().GetElementType()
                                uniformTypes <- MapExt.add i.paramSemantic (i.paramType, t) uniformTypes
                            | _ ->
                                match g.SingleAttributes.TryGetValue sym with
                                    | (true, uniform) ->
                                        assert(not (isNull uniform))
                                        let t = uniform.GetType()
                                        uniformTypes <- MapExt.add i.paramSemantic (i.paramType, t) uniformTypes
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

    type Regression2d(maxSamples : int) =
        let samples : array<V2i * MicroTime> = Array.zeroCreate maxSamples
        let mutable count = 0
        let mutable index = 0
        let mutable model : float[] = null
        
            
        let getModel() =
            if count <= 0  then
                [| |]
            elif count = 1 then
                let (x,y) = samples.[0]
                [| 0.0; y.TotalSeconds / float x.X; y.TotalSeconds / float x.Y |]
            else

                let points = samples |> Array.take count |> Array.map (fun (c,t) -> V3d(float c.X, float c.Y, t.TotalSeconds))
                

                let degree = min (count - 1) 1
                let arr = 
                    Array2D.init count 3 (fun r c ->
                        let (s,_) = samples.[r]
                        if c = 0 then 1.0
                        elif c = 1 then float s.X
                        else float s.Y
                    )

                let r = samples |> Array.take count |> Array.map (fun (_,t) -> t.TotalSeconds)

                let diag = arr.QrFactorize()
                let res = arr.QrSolve(diag, r)
                res

        member private x.GetModel() = 
            lock x (fun () ->
                if isNull model then model <- getModel()
                model
            )
            
        member x.Add(size : V2i, value : MicroTime) =
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
            
        member x.Add(a : int, b : int, value : MicroTime) =
            x.Add(V2i(a,b), value)
            
        member x.Evaluate(size : V2i) =
            let model = x.GetModel()
            if model.Length = 3 then
                model.[0] + model.[1] * float size.X + model.[2] * float size.Y |> MicroTime.FromSeconds
            else 
                MicroTime.Zero
                
        member x.Evaluate(a : int, b : int) =
            x.Evaluate(V2i(a,b))




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
                    let ptr = ctx.MapBufferRange(buffer, offset, size, BufferAccessMask.MapWriteBit)
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
                    let ptr = ctx.MapBufferRange(buffer, offset, size, BufferAccessMask.MapWriteBit)

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
                let res = new VertexBuffer(ctx, semantics, int size)
                Interlocked.Add(&totalMemory.contents, res.TotalSize) |> ignore
                res

            let mfree (ptr : VertexBuffer) (size : nativeint) =
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
                let res = new InstanceBuffer(ctx, semantics, int size)
                Interlocked.Add(&totalMemory.contents, res.TotalSize) |> ignore
                res

            let mfree (ptr : InstanceBuffer) (size : nativeint) =
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

    type IndirectBuffer(ctx : Context, indexed : bool, initialCapacity : int, usedMemory : ref<int64>, totalMemory : ref<int64>) =
        static let es = sizeof<DrawCallInfo>

        let initialCapacity = Fun.NextPowerOfTwo initialCapacity
        let adjust (call : DrawCallInfo) =
            if indexed then
                let mutable c = call
                Fun.Swap(&c.BaseVertex, &c.FirstInstance)
                c
            else
                call

        let drawIndices = Dict<DrawCallInfo, int>()
        let mutable capacity = initialCapacity
        let mutable mem : nativeptr<DrawCallInfo> = NativePtr.alloc (es * capacity)
        let mutable buffer = ctx.CreateBuffer (es * capacity)
        let mutable dirty = RangeSet.empty
        let mutable count = 0
        do Interlocked.Add(&totalMemory.contents, int64 (es * capacity)) |> ignore

        let resize (newCount : int) =
            let newCapacity = max initialCapacity (Fun.NextPowerOfTwo newCount)
            if newCapacity <> capacity then
                Interlocked.Add(&totalMemory.contents, int64 (es * (newCapacity - capacity))) |> ignore
                let ob = buffer
                let om = mem
                let nb = ctx.CreateBuffer (es * newCapacity)
                let nm = NativePtr.alloc (es * newCapacity)

                Marshal.Copy(NativePtr.toNativeInt om, NativePtr.toNativeInt nm, nativeint count * nativeint es)
                
                mem <- nm
                buffer <- nb
                capacity <- newCapacity
                dirty <- RangeSet.ofList [Range1i(0, count - 1)]
                
                NativePtr.free om
                ctx.Delete ob

        

        member x.Count = count

        member x.Add(call : DrawCallInfo) =
            if drawIndices.ContainsKey call then
                false
            else
                if count < capacity then
                    let id = count
                    drawIndices.[call] <- id
                    NativePtr.set mem id (adjust call)
                    count <- count + 1
                    Interlocked.Add(&usedMemory.contents, int64 es) |> ignore
                    dirty <- RangeSet.insert (Range1i(id,id)) dirty
                    true
                else
                    resize (count + 1)
                    x.Add(call)

        member x.Remove(call : DrawCallInfo) =
            match drawIndices.TryRemove call with
                | (true, oid) ->
                    let last = count - 1
                    count <- count - 1
                    Interlocked.Add(&usedMemory.contents, int64 -es) |> ignore

                    if oid <> last then
                        let lc = NativePtr.get mem last
                        drawIndices.[lc] <- oid
                        NativePtr.set mem oid lc
                        NativePtr.set mem last Unchecked.defaultof<DrawCallInfo>
                        dirty <- RangeSet.insert (Range1i(oid,oid)) dirty
                        
                    resize last

                    true
                | _ ->
                    false
        
        member x.Flush() =
            use __ = ctx.ResourceLock
            let toUpload = dirty
            dirty <- RangeSet.empty

            if not (Seq.isEmpty toUpload) then
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

        member x.Dispose() =
            Interlocked.Add(&usedMemory.contents, int64 (-es * count)) |> ignore
            Interlocked.Add(&totalMemory.contents, int64 (-es * capacity)) |> ignore
            NativePtr.free mem
            ctx.Delete buffer
            capacity <- 0
            mem <- NativePtr.zero
            buffer <- new Buffer(ctx, 0n, 0)
            dirty <- RangeSet.empty
            count <- 0
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
            use __ = ctx.ResourceLock

            match ib with
                | Some ib -> 
                    let gc = GCHandle.Alloc(g.IndexArray, GCHandleType.Pinned)
                    try ctx.UploadRange(ib.Memory.Value, gc.AddrOfPinnedObject(), int ib.Offset, int ib.Size)
                    finally gc.Free()
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
        static let instanceChunkSize = 1024
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
            //Log.warn "alloc %A" slot.Memory
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

  
    open Aardvark.Rendering.GL.Compiler
    open Aardvark.Rendering.GL.RenderTasks

    type DrawPool(ctx : Context, state : CompilerInfo -> PreparedPipelineState, pass : RenderPass) as this =
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
        
        let query : nativeptr<int> = NativePtr.allocArray [| 0 |]
        let startTime : nativeptr<uint64> = NativePtr.allocArray [| 0UL |]
        let endTime : nativeptr<uint64> = NativePtr.allocArray [| 0UL |]
        
        let mutable pProgramInterface = Unchecked.defaultof<GLSLProgramInterface>
        let stateCache = Dict<CompilerInfo, PreparedPipelineState>()
        let getState (info : CompilerInfo) = 
            stateCache.GetOrCreate(info, fun info ->
                
                //Log.start "info"
                //Log.warn "contextHandle:    %A" (info.contextHandle)
                //Log.warn "runtimeStats:     %A" (info.runtimeStats)
                //Log.warn "currentContext:   %A" (info.currentContext.GetHashCode())
                //Log.warn "drawBuffers:      %A" (info.drawBuffers)
                //Log.warn "drawBufferCount:  %A" (info.drawBufferCount)
                //Log.warn "structuralChange: %A" (info.structuralChange.GetHashCode())
                //Log.warn "usedTextureSlots: %A" (info.usedTextureSlots.GetHashCode())
                //Log.warn "usedUBSlots:      %A" (info.usedUniformBufferSlots.GetHashCode())
                //Log.warn "task:             %A" (info.task.GetHashCode())
                //Log.warn "tags:             %A" (info.tags.GetHashCode())
                //Log.stop()

                state info
            )


        let usedMemory = ref 0L
        let totalMemory = ref 0L
        let avgRenderTime = RunningMean(10)

        let compile (indexType : Option<DrawElementsType>, mode : nativeptr<GLBeginMode>, a : VertexInputBindingHandle, ib : nativeptr<V2i>) (s : IAssemblerStream) =
            s.BindVertexAttributes(contextHandle, a)
            match indexType with
                | Some indexType ->
                    s.DrawElementsIndirect(runtimeStats, isActive, mode, int indexType, ib)
                | _ -> 
                    s.DrawArraysIndirect(runtimeStats, isActive, mode, ib)
        
        let indirects = Dict<_, IndirectBuffer>()
        let isOutdated = NativePtr.allocArray [| 1 |]
        let updateFun = Marshal.PinDelegate(System.Action(this.Update))
        let mutable oldCalls : list<Option<DrawElementsType> * nativeptr<GLBeginMode> * VertexInputBindingHandle * nativeptr<V2i>> = []
        let program = new ChangeableNativeProgram<_>(compile)
        let puller = AdaptiveObject()
        let sub = puller.AddMarkingCallback (fun () -> NativePtr.write isOutdated 1)
        let tasks = System.Collections.Generic.HashSet<IRenderTask>()

        let mark() = transact (fun () -> puller.MarkOutdated())
        

        let getIndirectBuffer(slot : PoolSlot) =
            let key = getKey slot
            indirects.GetOrCreate(key, fun _ ->
                new IndirectBuffer(ctx, Option.isSome slot.IndexType, initialIndirectSize, usedMemory, totalMemory)
            )

        let tryGetIndirectBuffer(slot : PoolSlot) =
            let key = getKey slot
            match indirects.TryGetValue key with
                | (true, ib) -> Some ib
                | _ -> None
                

        member x.Add(ref : PoolSlot) =
            let ib = getIndirectBuffer ref
            if ib.Add ref.DrawCallInfo then
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

        member x.AverageRenderTime = MicroTime(int64 (1000000.0 * avgRenderTime.Average))

        member x.Update() =
            puller.EvaluateAlways AdaptiveToken.Top (fun token ->
                puller.OutOfDate <- true
                x.Evaluate(token, pProgramInterface)
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
                                            param.paramLocation, {
                                                Type = typ
                                                Content = Left vb
                                                Frequency = AttributeFrequency.PerVertex
                                                Normalized = false
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

                        let indirect = 
                            let ptr = NativePtr.alloc 1
                            NativePtr.write ptr (V2i(db.Buffer.Buffer.Handle, db.Count))
                            ptr

                        indexType, beginMode, bufferBinding, indirect
                    )

                program.Clear()
                for a in calls do program.Add a |> ignore
            
                oldCalls |> List.iter (fun (_,beginMode,bufferBinding,indirect) -> 
                    NativePtr.free beginMode; ctx.Delete bufferBinding; NativePtr.free indirect
                )
                oldCalls <- calls

                NativePtr.write isOutdated 0

                for t in tasks do
                    puller.Outputs.Add t |> ignore

                x.AfterUpdate()
            )

        override x.Compile(info : CompilerInfo, stream : IAssemblerStream, last : Option<PreparedCommand>) =
            lock puller (fun () ->
                if tasks.Add info.task then
                    assert (info.task.OutOfDate)
                    puller.AddOutput(info.task) |> ignore
            )
            
            let state = getState info
            pProgramInterface <- state.pProgramInterface

            let lastState = last |> Option.bind (fun l -> l.ExitState info)

            let label = stream.NewLabel()
            stream.Cmp(NativePtr.toNativeInt isOutdated, 0)
            stream.Jump(JumpCondition.Equal, label)
            stream.BeginCall(0)
            stream.Call(updateFun.Pointer)
            stream.Mark(label)
            
            let taskStats = NativePtr.toNativeInt info.runtimeStats
            let localStats = NativePtr.toNativeInt runtimeStats

            let taskCtx = NativePtr.toNativeInt info.contextHandle
            let localCtx = NativePtr.toNativeInt contextHandle

            stream.Copy(taskStats, localStats, true)
            stream.Copy(taskCtx, localCtx, sizeof<nativeint> = 8)
            
            stream.SetPipelineState(info, state, lastState)

            stream.QueryTimestamp(query, startTime)
            stream.BeginCall(0)
            stream.CallIndirect(program.EntryPointer)
            stream.QueryTimestamp(query, endTime)

            stream.Copy(localStats, taskStats, true)

        override x.Release(s) =
            match stateCache.TryRemove s with
                | (true, state) ->
                    state.Dispose()
                    
                    if stateCache.Count = 0 then
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
                | _ ->
                    ()

        override x.GetResources(s) = getState(s).Resources
        override x.EntryState s = Some (getState s)
        override x.ExitState s = Some (getState s)

    type ManyGeomtries(ctx : Context, state : CompilerInfo -> PreparedPipelineState, pass : RenderPass, geometries : aset<IndexedGeometry * MapExt<string, Array>>) as this =
        inherit PreparedCommand(ctx, pass)

        let pool = GeometryPool.Get(ctx)

        let sw = System.Diagnostics.Stopwatch.StartNew()
        let reader = geometries.GetReader()
        let cache = Dict<IndexedGeometry * MapExt<string, Array>, PoolSlot>()
        let inactive = Dict<IndexedGeometry * MapExt<string, Array>, MicroTime * PoolSlot>()


        let evaluate (draws : DrawPool) (token : AdaptiveToken) (iface : GLSLProgramInterface) =
            let ops = reader.GetOperations token

            ops |> HDeltaSet.iter (fun op ->
                match op with
                    | Add(_, (g,u)) ->
                        match inactive.TryRemove ((g,u)) with
                            | (true, (t,slot)) ->
                                Log.warn "revived after %A (%A)" (sw.MicroTime - t) pool.TotalMemory
                                cache.[(g,u)] <- slot
                                draws.Add slot
                            | _ -> 
                                let slot = pool.Alloc(iface, g, u)
                                cache.[(g,u)] <- slot
                                draws.Add slot

                    | Rem(_, (g,u)) ->
                        match cache.TryRemove((g,u)) with
                            | (true, slot) ->
                                draws.Remove(slot) |> ignore
                                //inactive.[(g,u)] <- (sw.MicroTime, slot)
                                pool.Free slot
                            | _ ->  
                                ()
            )

        let inner =
            { new DrawPool(ctx, state, pass) with
                override x.Evaluate(token : AdaptiveToken, iface : GLSLProgramInterface) = evaluate x token iface
                override __.AfterUpdate() = 
                    let used = this.UsedMemory 
                    let total = this.TotalMemory
                    if total <> Mem.Zero then
                        Log.warn "%A %A (%.2f%%)" used total (100.0 * float used.Bytes / float total.Bytes)
                    else
                        Log.warn "no memory"
            }
            
        member x.UsedMemory : Mem = pool.UsedMemory + inner.UsedMemory
        member x.TotalMemory : Mem = pool.TotalMemory + inner.TotalMemory
        
        override x.Compile(a,b,c) = inner.Compile(a,b,c)
        override x.GetResources(s) = inner.GetResources(s)
        override x.Release(s) = 
            inactive.Values |> Seq.iter (snd >> pool.Free)
            cache.Values |> Seq.iter pool.Free
            reader.Dispose()
            cache.Clear()
            inactive.Clear()
            inner.Dispose(s)

        override x.EntryState s = inner.EntryState s
        override x.ExitState s = inner.ExitState s


    open System.Threading.Tasks

    

    type ITreeNode =
        abstract member Level : int
        abstract member Name : string
        abstract member Parent : Option<ITreeNode>
        abstract member Children : seq<ITreeNode>

        abstract member DataSource : Symbol
        abstract member DataSize : int
        abstract member GetData : CancellationToken -> Task<IndexedGeometry * MapExt<string, Array>>

        abstract member ShouldSplit : Trafo3d * Trafo3d -> bool

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
            
    type MaterializedTree =
        {
            original    : ITreeNode
            children    : list<MaterializedTree>
        }

    [<RequireQualifiedAccess>]
    type RenderOperation =
        | Split of children : hmap<ITreeNode, IndexedGeometry * MapExt<string, Array>>
        | Collapse of maxDepth : int * geometry : IndexedGeometry * uniforms : MapExt<string, Array> * remove : list<ITreeNode>
        | Add of geometry : IndexedGeometry * uniforms : MapExt<string, Array>
        | Remove of children : list<ITreeNode>
        
    module RenderOperation =
        let toString (op : RenderOperation) =
            match op with
                | RenderOperation.Add _             -> "add"
                | RenderOperation.Split _           -> "split"
                | RenderOperation.Collapse(d,_,_,_) -> sprintf "collapse(%d)" d
                | RenderOperation.Remove _          -> "remove"

    [<RequireQualifiedAccess>]
    type NodeOperation =
        | Split
        | Collapse of maxDepth : int * children : list<ITreeNode>
        | Add
        | Remove of children : list<ITreeNode>


    module MaterializedTree =
        
        let inline original (node : MaterializedTree) = node.original
        let inline children (node : MaterializedTree) = node.children

        let ofNode (node : ITreeNode) =
            {
                original = node
                children = []
            }

        
        let rec allNodes (node : MaterializedTree) =
            Seq.append 
                (Seq.singleton node)
                (node.children |> Seq.collect allNodes)

        let allChildren (node : MaterializedTree) =
            node.children |> Seq.collect allNodes

        let rec tryExpand (predictView : ITreeNode -> Trafo3d) (view : Trafo3d) (proj : Trafo3d) (t : MaterializedTree) =
            let node = t.original

            let inline tryExpandMany (ls : list<MaterializedTree>) =
                let mutable changed = false
                let newCs = 
                    ls |> List.map (fun c ->
                        match tryExpand predictView view proj c with
                            | Some newC -> 
                                changed <- true
                                newC
                            | None ->
                                c
                    )
                if changed then Some newCs
                else None
                
            if node.ShouldSplit(view, proj) && node.ShouldSplit(predictView node, proj) then
                match t.children with
                    | [] ->
                        Some { t with children = node.Children |> Seq.toList |> List.map ofNode }
                    | children ->
                        match tryExpandMany children with
                            | Some newChildren -> Some { t with children = newChildren }
                            | _ -> None
            else
                match t.children with
                    | [] ->
                        None
                    | children ->
                        Some { t with children = [] }

        let expand (predictView : ITreeNode -> Trafo3d) (view : Trafo3d) (proj : Trafo3d) (t : MaterializedTree) =
            match tryExpand predictView view proj t with
                | Some n -> n
                | None -> t

        let rec computeDelta (acc : hmap<ITreeNode, NodeOperation>) (o : MaterializedTree) (n : MaterializedTree) =
            if System.Object.ReferenceEquals(o,n) then
                acc
            else

                let rec computeChildDeltas (acc : hmap<ITreeNode, NodeOperation>) (os : list<MaterializedTree>) (ns : list<MaterializedTree>) =
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
                        | [], _     -> HMap.add n.original NodeOperation.Split acc
                        | oc, []    -> 
                            let children = allChildren o |> Seq.map original |> Seq.toList
                            let maxDepth = children |> Seq.map (fun c -> c.Level - o.original.Level) |> Seq.max
                            HMap.add n.original (NodeOperation.Collapse(maxDepth, children)) acc
                        | os, ns    -> computeChildDeltas acc os ns
                else
                    failwith "inconsistent child values"




    type LodRenderer(ctx : Context, state : CompilerInfo -> PreparedPipelineState, pass : RenderPass, roots : aset<ITreeNode>, renderTime : IMod<_>, view : IMod<Trafo3d>, proj : IMod<Trafo3d>)  =
        inherit PreparedCommand(ctx, pass)

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
        let mutable renderDelta : hmap<ITreeNode, RenderOperation> = HMap.empty
        let mutable deltaEmpty = true

        let run (token : AdaptiveToken) (maxMem : Mem) (maxTime : MicroTime) (calls : DrawPool) (iface : GLSLProgramInterface) =
            let sw = System.Diagnostics.Stopwatch.StartNew()
            
            let free (node : ITreeNode) =
                match cache.TryRemove node with
                    | (true, slot) ->
                        calls.Remove slot |> ignore
                        pool.Free slot
                    | _ ->
                        ()
                

            let rec run (sinceSync : Mem) (totalSize : Mem) (nodeCount : int) (totalNodeSize : int) =
                let mem = totalSize > maxMem
                let time = sw.MicroTime > maxTime
            
                let sinceSync = 
                    if sinceSync.Bytes > 262144L then
                        GL.Sync()
                        Mem.Zero
                    else
                        sinceSync

                if mem || time then
                    GL.Sync()
                    if totalNodeSize > 0 && nodeCount > 0 then
                        updateTime.Add(sw.MicroTime.TotalMilliseconds)
                    renderTime.GetValue token |> ignore
                else
                    let dequeued = 
                        lock renderingStateLock (fun () ->
                            if HMap.isEmpty renderDelta then
                                deltaEmpty <- true
                                None
                            else
                                let (node, op) = Seq.head renderDelta
                                renderDelta <- HMap.remove node renderDelta
                                Some (node, op)
                        )
                    match dequeued with
                    | None -> 
                        GL.Sync()
                        if totalNodeSize > 0 && nodeCount > 0 then
                            updateTime.Add(sw.MicroTime.TotalMilliseconds)

                    | Some(node, op) ->
                        let mutable cnt = 0
                        let mutable nodeSize = 0
                        let mutable uploadSize = Mem.Zero
                        
                        match op with
                            | RenderOperation.Remove children ->
                                free node
                                children |> List.iter free

                            | RenderOperation.Split children ->
                                free node
                                for (child, (g, u)) in children do
                                    if not (cache.ContainsKey child) then
                                        let slot = pool.Alloc(iface, g, u)
                                        calls.Add slot
                                        cache.[child] <- slot
                                        uploadSize <- uploadSize + slot.Memory
                                        nodeSize <- nodeSize + child.DataSize
                                        cnt <- cnt + 1

                            | RenderOperation.Add(g,u) ->
                                if not (cache.ContainsKey node) then
                                    let slot = pool.Alloc(iface, g, u)
                                    calls.Add slot
                                    cache.[node] <- slot
                                    uploadSize <- uploadSize + slot.Memory
                                    nodeSize <- nodeSize + node.DataSize
                                    cnt <- cnt + 1

                            | RenderOperation.Collapse(_, g, u, oldChildren) ->
                                if not (cache.ContainsKey node) then
                                    let slot = pool.Alloc(iface, g, u)
                                    calls.Add slot
                                    cache.[node] <- slot
                                    uploadSize <- uploadSize + slot.Memory
                                    nodeSize <- nodeSize + node.DataSize
                                    cnt <- cnt + 1
                                    
                                oldChildren |> Seq.iter free
                   
                        run (sinceSync + uploadSize) (totalSize + uploadSize) (nodeCount + cnt) (totalNodeSize + nodeSize)
            run Mem.Zero Mem.Zero 0 0

        let evaluate (calls : DrawPool) (token : AdaptiveToken) (iface : GLSLProgramInterface) =
            needUpdate.GetValue(token)
            let maxTime = max (16 * ms) calls.AverageRenderTime
            let maxMem = Mem (3L <<< 30)
            run token maxMem maxTime calls iface
            
        let inner =
            let mutable lastUsed = Mem.Zero
            let mutable lastTotal = Mem.Zero
            { new DrawPool(ctx, state, pass) with
                override x.Evaluate(token : AdaptiveToken, iface : GLSLProgramInterface) =
                    evaluate x token iface
            }

        

        let thread =
            let prediction = Prediction.euclidean (MicroTime(TimeSpan.FromMilliseconds 55.0))
            let predictedTrafo = prediction |> Prediction.map Trafo3d
    
            let mutable baseState : hmap<ITreeNode, MaterializedTree> = HMap.empty
            let cameraPrediction =
                startThread (fun () ->
                    let mutable lastTime = time()
                    let timer = new MultimediaTimer.Trigger(10)

                    let mutable lastReport = time()


                    while true do
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

                            Log.line "m: %A r: %A l : %s e: %A u: %A" (pool.UsedMemory + inner.UsedMemory) inner.AverageRenderTime loads e u

                )

            let submit (node : ITreeNode) (op : RenderOperation) = //(op : list<ITreeNode * Option<IndexedGeometry * MapExt<string, Array>>>) =  
                let update (o : Option<RenderOperation>) =
                    match o, op with
                        | None, _  ->
                            Log.warn "%s: %s" node.Name (RenderOperation.toString op)
                            Some op
                            
                        | Some (RenderOperation.Collapse(1,_,_,_) as o), RenderOperation.Split _
                        | Some (RenderOperation.Split _ as o), RenderOperation.Collapse _ ->
                            Log.warn "cancelled %s: %s vs %s" node.Name (RenderOperation.toString o) (RenderOperation.toString op)
                            None

                        | Some o, _ ->
                            Log.warn "conflict %s: %s vs %s" node.Name (RenderOperation.toString o) (RenderOperation.toString op)
                            Some op
                            


                
                renderDelta <- HMap.alter node update renderDelta
                deltaEmpty <- HMap.isEmpty renderDelta

            let mutable converged = false

            let thread = 
                startThread (fun () ->
                    
                    let mutable loadingState = HMap.empty
                    let cancel = System.Collections.Concurrent.ConcurrentDictionary<ITreeNode, CancellationTokenSource>()
                    
                    let isNotConverged = new ManualResetEventSlim(true)
                    let sa = view.AddMarkingCallback (fun () -> converged <- false; isNotConverged.Set())
                    let sb = proj.AddMarkingCallback (fun () -> converged <- false; isNotConverged.Set())

                    
                    let timer = new MultimediaTimer.Trigger(10)
                    while true do
                        timer.Wait()
                        isNotConverged.Wait()
                        view.GetValue() |> ignore
                        proj.GetValue() |> ignore

                        

                        if cancel.Count = 0 && deltaEmpty then
                            if baseState = loadingState then
                                if not converged then
                                    converged <- true
                                    isNotConverged.Reset()
                            else
                                baseState <- loadingState
                       
                       
                        //let total = baseState |> Seq.sumBy (fun (_,s) -> s.Count)
                        
                        //Log.warn "expand: %A" (MicroTime(TimeSpan.FromMilliseconds mean.Average))
                        //let mutable deltas = HDeltaSet.empty
                        for o in reader.GetOperations AdaptiveToken.Top do
                            match o with
                            | Add(_,root) -> 
                                baseState <- HMap.add root (MaterializedTree.ofNode root) baseState

                                //deltas <- HDeltaSet.add (Add root) deltas
                                //baseState <- HMap.add root (HSet.ofList [root]) baseState
                                //loadingState <- HMap.add root (HSet.ofList [root]) loadingState

                            | Rem(_,root) -> 
                                baseState <- HMap.remove root baseState

                                //match HMap.tryRemove root loadingState with
                                //    | Some (set, newState) ->
                                //        loadingState <- newState
                                //        baseState <- HMap.remove root baseState
                                //        deltas <- set |> Seq.fold (fun d n -> HDeltaSet.add (Rem n) d) deltas
                                //    | None ->
                                //        ()

                        let startTime = time()



                        let dBase = 0.5 * MicroTime.FromMilliseconds expandTime.Average

                        //let targetState, deltas = getDelta predictedTrafo dBase baseState loadingState AdaptiveToken.Top

                        //let deltas = deltas |> List.fold HDeltaSet.combine HDeltaSet.empty
                        //let merge _ (l : Option<hset<ITreeNode>>) (delta : Option<hdeltaset<ITreeNode>>) =
                        //    let l = l |> Option.defaultValue HSet.empty
                        //    let delta = delta |> Option.defaultValue HDeltaSet.empty

                        //    let s, _ = HRefSet.applyDelta (HRefSet.ofSeq l) delta
                        //    HSet.ofSeq s |> Some

                        //let res = HMap.choose2 merge loadingState (baseState |> HMap.map (fun _ _ -> deltas))

                        
                        //if res <> targetState then
                        //    Log.warn "fail"
                        
                        let predictedView = 
                            let view = view.GetValue()
                            view

                        let predictView (node : ITreeNode) =
                            let tLoad = getLoadTime node.DataSource node.DataSize
                            let tActivate = max (20*ms) inner.AverageRenderTime
                            let tUpload = updateTime.Average |> MicroTime.FromMilliseconds
                            let tRender = inner.AverageRenderTime
                            let loadTime = tLoad + tActivate + tUpload + tRender
                            predictedTrafo.Predict loadTime |> Option.defaultValue predictedView
                            

                        let newState = 
                            baseState |> HMap.map (fun _ s -> s |> MaterializedTree.expand predictView predictedView (proj.GetValue()))

                        let deltas = 
                            HMap.choose2 (fun _ l r -> Some (l, r)) loadingState newState
                            |> Seq.fold (fun delta (_,(l, r)) ->
                                match l, r with
                                    | None, None -> delta
                                    | Some l, Some r -> MaterializedTree.computeDelta delta l r
                                    | Some l, None -> 
                                        let children = MaterializedTree.allChildren l |> Seq.map MaterializedTree.original |> Seq.toList
                                        HMap.add l.original (NodeOperation.Remove children) delta
                                    | None, Some r -> HMap.add r.original NodeOperation.Add delta
                                        
                            ) HMap.empty
                            
                        let dt = time() - startTime
                        loadingState <- newState
                        
                        expandTime.Add(dt.TotalMilliseconds)
                        
                        let stop (node : ITreeNode) =
                            match cancel.TryRemove node with
                                | (true, c) -> c.Cancel()
                                | _ -> ()

                        let load (node : ITreeNode) (cont : CancellationToken -> ITreeNode -> IndexedGeometry -> MapExt<string, Array> -> 'r) =
                            stop node

                            let c = new CancellationTokenSource()
                            cancel.[node] <- c
                            node.GetData(c.Token).ContinueWith (fun (t : Task<_>) ->
                                cancel.TryRemove node |> ignore
                                if t.IsCompletedSuccessfully then
                                    let (g,u) = t.Result
                                    let endTime = time()
                                    addLoadTime node.DataSource node.DataSize (endTime - startTime)

                                    if not c.IsCancellationRequested then
                                        cont c.Token node g u
                                    else
                                        raise <| OperationCanceledException()
                                else
                                    raise <| OperationCanceledException()
                            )

                        if not (HMap.isEmpty deltas) then
                            for (v, delta) in deltas do
                                stop v

                                match delta with
                                    | NodeOperation.Remove children ->
                                        lock renderingStateLock (fun () ->
                                            submit v (RenderOperation.Remove children)
                                        )

                                    | NodeOperation.Split ->
                                        let c = new CancellationTokenSource()
                                        cancel.[v] <- c
                                        let startTime = time()
                                        let loadChildren = 
                                            v.Children |> Seq.toList |> List.map (fun v -> 
                                                load v (fun _ n g u -> (n,(g,u)))
                                            )

                                        Task.WhenAll(loadChildren).ContinueWith (fun (t : Task<array<_>>) ->
                                            lock renderingStateLock (fun () ->
                                                if not c.IsCancellationRequested then
                                                    cancel.TryRemove v |> ignore
                                                    submit v (RenderOperation.Split (t.Result |> HMap.ofArray))
                                            )
                                        ) |> ignore

                                    | NodeOperation.Collapse(maxDepth, children) ->
                                        children |> Seq.iter stop
                                        load v (fun c v g u ->
                                            lock renderingStateLock (fun () ->
                                                if not c.IsCancellationRequested then
                                                    submit v (RenderOperation.Collapse (maxDepth, g, u, children))
                                            )
                                        ) |> ignore


                                    | NodeOperation.Add ->
                                        load v (fun c v g u ->
                                            lock renderingStateLock (fun () ->
                                                if not c.IsCancellationRequested then
                                                    submit v (RenderOperation.Add (g, u))
                                            )
                                        ) |> ignore

                    sa.Dispose()
                    sb.Dispose()
                )

            

            thread
        
        
        member x.UsedMemory : Mem = pool.UsedMemory + inner.UsedMemory
        member x.TotalMemory : Mem = pool.TotalMemory + inner.TotalMemory
        
        override x.Compile(a,b,c) = inner.Compile(a,b,c)
        override x.GetResources(s) = inner.Resources s :> seq<_>
        override x.Release(s) = 
            reader.Dispose()
            inner.Dispose(s)

        override x.EntryState s = inner.EntryState s
        override x.ExitState s = inner.ExitState s


open Aardvark.Rendering.GL
open Aardvark.Application.Slim
open System
open System.Threading
open System.Threading.Tasks

module StoreTree =
    open Aardvark.Geometry
    open Aardvark.Geometry.Points




    type PointTreeNode(source : Symbol, globalTrafo : Trafo3d, parent : Option<PointTreeNode>, level : int, self : PointSetNode) as this =
        let bounds = self.BoundingBoxExact.Transformed globalTrafo
        
        let mutable cache = None

        let (|Strong|_|) (w : WeakReference<'a>) =
            match w.TryGetTarget() with
                | (true, a) -> Some a
                | _ -> None

        let children =
            if isNull self.Subnodes then
                Seq.empty
            else
                let cache : list<ref<Option<WeakReference<_>>>> =  self.Subnodes |> Seq.toList |> List.map (fun _ -> ref None)
                    
                Seq.delay (fun () ->
                    Seq.zip cache self.Subnodes
                    |> Seq.choose (fun (cache,node) ->
                        match !cache with
                            | Some (Strong node) -> 
                                Some node
                            | _ ->
                                if isNull node || isNull node.Value then
                                    None
                                else
                                    let n = PointTreeNode(source, globalTrafo, Some this, level+1, node.Value)
                                    cache := Some (WeakReference<_> n)
                                    Some n
                    )
                 
                )

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
                
                        let uniforms =
                            MapExt.ofList [
                                "Colors", colors :> System.Array
                                "Offsets", positions :> System.Array
                                "Normals", normals :> System.Array
                            ]

                        cache <- Some (geometry, uniforms)
                        return geometry, uniforms
            }

        let load =
            async {
                do! Async.SwitchToThreadPool()
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

                let geometry =
                    IndexedGeometry(
                        Mode = IndexedGeometryMode.PointList,
                        IndexedAttributes =
                            SymDict.ofList [
                                DefaultSemantic.Positions, positions :> System.Array
                                DefaultSemantic.Colors, colors :> System.Array
                            ]
                    )
                
                let uniforms =
                    MapExt.ofList [
                        "TreeLevel", [| float32 level |] :> System.Array
                        "AvgPointDistance", [| float32 (bounds.Size.NormMax / 20.0) |] :> System.Array
                    ]


                return geometry, uniforms
            }

        member x.Children = children

        member x.Id = self.Id

        member x.GetData(ct : CancellationToken) = 
            Async.StartAsTask(loadSphere, cancellationToken = ct)
            
        member x.ShouldSplit (view : Trafo3d, proj : Trafo3d) =
            let cam = view.Backward.C3.XYZ

            let avgPointDistance = bounds.Size.NormMax / 40.0

            let distRange =
                bounds.ComputeCorners() 
                    |> Seq.map (fun p -> V3d.Distance(p, cam))
                    |> Range1d

            let distRange = 
                Range1d(max 1.0 distRange.Min, max 1.0 distRange.Max)

            let angle = Constant.DegreesPerRadian * atan2 avgPointDistance distRange.Min

            angle > 1.5
        member x.DataSource = source

        interface Bla.ITreeNode with
            member x.Level = level
            member x.Name = string x.Id
            member x.DataSource = source
            member x.Parent = parent |> Option.map (fun n -> n :> Bla.ITreeNode)
            member x.Children = x.Children |> Seq.map (fun n -> n :> Bla.ITreeNode)
            member x.ShouldSplit(v,p) = x.ShouldSplit(v,p)
            member x.DataSize = int self.LodPointCount
            member x.GetData(ct : CancellationToken) = x.GetData(ct)

        override x.GetHashCode() = 
            HashCode.Combine(x.DataSource.GetHashCode(), self.Id.GetHashCode())

        override x.Equals o =
            match o with
                | :? PointTreeNode as o -> x.DataSource = o.DataSource && self.Id = o.Id
                | _ -> false

    let load (sourceName : string) (trafo : Trafo3d) (folder : string) (key : string) =
        let store = PointCloud.OpenStore folder
        let points = store.GetPointSet(key, CancellationToken.None)
        
        
        let targetSize = 100.0
        let bounds = points.BoundingBox

        let trafo =
            Trafo3d.Translation(-bounds.Center) *
            Trafo3d.Scale(targetSize / bounds.Size.NormMax) * 
            trafo

        let source = Symbol.Create sourceName
        PointTreeNode(source, trafo, None, 0, points.Root.Value)

    let import (sourceName : string) (trafo : Trafo3d) (file : string) (store : string) =
        do Aardvark.Data.Points.Import.Pts.PtsFormat |> ignore

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
        
                let res = PointCloud.Import(file,config3)
                store.Flush()
                res
            else
                set

        let targetSize = 100.0
        let bounds = points.BoundingBox

        let trafo =
            Trafo3d.Translation(-bounds.Center) *
            Trafo3d.Scale(targetSize / bounds.Size.NormMax) * 
            trafo

        let source = Symbol.Create sourceName
        PointTreeNode(source, trafo, None, 0, points.Root.Value)





type StupidOctreeNode(parent : Option<StupidOctreeNode>, level : int, bounds : Box3d) as this =
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
                        yield StupidOctreeNode(Some this, level + 1, Box3d.FromMinAndSize(off, half))
        ]

    let shouldSplit (view : Trafo3d) (proj : Trafo3d) =
        if level >= 6 then
            false
        else
            //let pp = view.Forward.TransformPos(bounds.Center)
            let viewProj = view * proj
            let cam = view.Backward.TransformPos V3d.Zero

            let avgPointDistance = bounds.Size.NormMax / 20.0

            let distRange =
                bounds.ComputeCorners() 
                    |> Seq.map (fun p -> V3d.Distance(p, cam))
                    |> Range1d

            let distRange = 
                Range1d(max 1.0 distRange.Min, max 1.0 distRange.Max)

            let angle = Constant.DegreesPerRadian * atan2 avgPointDistance distRange.Min

            angle > 0.5


            ////let (vmin, vmax) = bounds.GetMinMaxInDirection(-view.Backward.C2.XYZ)
            ////let pp = viewProj.Forward.TransformPosProj vmin
            //let projectedLength (b : Box3d) (t : Trafo3d) =
            //    let ssb = b.ComputeCorners() |> Array.map (t.Forward.TransformPosProj) |> Box3d
            //    max ssb.Size.X ssb.Size.Y

            //let len = projectedLength bounds viewProj
            //len > 0.5//0.3
            ////level < 5 // || len > 0.3
            ////if pp.Z >= -1.0 then
            ////    pp.Z > 0.0
            ////else
            ////    false
        

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
            geometry, MapExt.ofList ["Colors", [| color |] :> System.Array]
        )

    let getData (ct : CancellationToken) =
        async {
            do! Async.SwitchToThreadPool()
            return data.Value
        }

    interface Bla.ITreeNode with
        member x.Level = level
        member x.Name = string bounds
        member x.DataSource = DataSource
        member x.Parent = parent |> Option.map (fun n -> n :> Bla.ITreeNode)
        member x.Children = children.Value |> Seq.map (fun n -> n :> Bla.ITreeNode)
        member x.ShouldSplit (v,p) = shouldSplit v p
        member x.DataSize = 1024
        member x.GetData(ct : CancellationToken) = Async.StartAsTask (getData ct, cancellationToken = ct)


type StupidQuadTreeNode(parent : Option<StupidQuadTreeNode>, level : int, bounds : Box3d) as this =
    static let DataSource = Symbol.Create "CPU"
    static let rand = RandomSystem()

    let color = rand.UniformC3f().ToC4b()

    let children =
        lazy [
            let half = bounds.Size / V3d(2.0, 2.0, 1.0)
            for x in 0 .. 1 do
                for y in 0 .. 1 do
                    let off = bounds.Min + V3d(x,y,0) * half
                    yield StupidQuadTreeNode(Some this, level + 1, Box3d.FromMinAndSize(off, half))
        ]

    let shouldSplit (view : Trafo3d) (proj : Trafo3d) =
        if level >= 10 then
            false
        else
            let viewProj = view * proj
            let mutable cam = view.Backward.TransformPos V3d.Zero
            cam.Z <- 0.0

            let dist = bounds.GetMinimalDistanceTo(cam) |> max 1.0
            dist < 30.0
        

    let getData (ct : CancellationToken) =
        async {
            do! Async.SwitchToThreadPool()

            let positions =
                [|
                    let off = bounds.Min
                    let s = bounds.Size
                    for x in 0 .. 4 do
                        for  y in 0 .. 4 do
                            yield V3f (off + (V3d(x,y,0)/5.0) * s)
                |]
            do! Async.Sleep(10)
            let geometry = 
                IndexedGeometry(
                    Mode = IndexedGeometryMode.PointList,
                    IndexedAttributes =
                        SymDict.ofList [
                            DefaultSemantic.Positions, positions :> Array
                        ]
                )
            return geometry, MapExt.ofList ["Colors", [| color |] :> System.Array]
        }

    interface Bla.ITreeNode with
        member x.Level = level
        member x.Name = string bounds
        member x.DataSource = DataSource
        member x.Parent = parent |> Option.map (fun n -> n :> Bla.ITreeNode)
        member x.Children = children.Value |> Seq.map (fun n -> n :> Bla.ITreeNode)
        member x.ShouldSplit (v,p) = shouldSplit v p
        member x.DataSize = 1024
        member x.GetData(ct : CancellationToken) = Async.StartAsTask (getData ct, cancellationToken = ct)

module CommandStreamGenerator =
    open System.Reflection
    open Aardvark.Rendering.GL

    type GLFunc =
        {
            decl    : string
            name    : string
            args    : list<string * Type>
        }

    module GLFunc =

        let types =
            LookupTable.lookupTable [
                typeof<Aardvark.Rendering.GL.EXT_direct_state_access.GL>, "Aardvark.Rendering.GL.EXT_direct_state_access.GL"
                typeof<OpenTK.Graphics.OpenGL4.GL>, "OpenTK.Graphics.OpenGL4.GL"
                typeof<OpenTK.Graphics.OpenGL4.GL.Arb>, "OpenTK.Graphics.OpenGL4.GL.Arb"
                typeof<OpenTK.Graphics.OpenGL4.GL.Ext>, "OpenTK.Graphics.OpenGL4.GL.Ext"
            ]

        let ofMethod (m : MethodInfo) =
            {
                decl = types m.DeclaringType
                name = m.Name
                args = m.GetParameters() |> Array.toList |> List.map (fun p -> p.Name, p.ParameterType)
            }

    let generateCommandStream() =
        let builder = new System.Text.StringBuilder()

        let printfn fmt = 
            Printf.kprintf (fun str ->
                builder.AppendLine str |> ignore
            ) fmt

        let score (m : MethodInfo) =
            let offset = if m.ReturnType <> typeof<System.Void> then -1 else 0
            let argScore = 
                m.GetParameters() |> Array.sumBy (fun p -> 
                    if p.ParameterType = typeof<uint32> then 
                        0
                    elif p.ParameterType.IsArray || p.ParameterType.IsByRef then 
                        -1
                    else 
                        1
                )
            (offset, argScore)

        let all =
            Array.concat [
                typeof<OpenTK.Graphics.OpenGL4.GL>.GetMethods(BindingFlags.Public ||| BindingFlags.Static)
                typeof<OpenTK.Graphics.OpenGL4.GL.Arb>.GetMethods(BindingFlags.Public ||| BindingFlags.Static)
                typeof<OpenTK.Graphics.OpenGL4.GL.Ext>.GetMethods(BindingFlags.Public ||| BindingFlags.Static)
                //typeof<GL>.GetMethods(BindingFlags.Public ||| BindingFlags.Static)
            ]

        let methods = 
            all
            |> Array.filter (fun m -> not m.IsGenericMethod)
            |> Array.groupBy (fun m -> m.Name)
            |> Array.map (fun (name, meths) ->
                if meths.Length = 1 then 
                    meths.[0]
                else
                    meths |> Array.maxBy score
            )
            |> Array.filter (fun m -> 
                m.ReturnType = typeof<System.Void> &&
                m.GetParameters() |> Array.forall (fun p -> 
                    not p.ParameterType.IsByRef && 
                    not p.ParameterType.IsArray && 
                    p.ParameterType <> typeof<string> &&
                    not (typeof<Delegate>.IsAssignableFrom p.ParameterType)
                )
            )
            |> Array.sortBy (fun m -> m.Name)

        let methods =
            methods |> Array.map GLFunc.ofMethod

        printfn "namespace Aardvark.Rendering.GL"
        printfn ""
        printfn "open System"
        printfn "open Aardvark.Base"
        printfn "open Aardvark.Base.Rendering"
        printfn "open System.Runtime.InteropServices"
        printfn "open Microsoft.FSharp.NativeInterop"
        printfn "open OpenTK"
        printfn "open OpenTK.Graphics.OpenGL4"
        printfn ""
        printfn "#nowarn \"9\""
        printfn "#nowarn \"44\""
        printfn ""
        printfn ""


        printfn "type InstructionCode = "
        let mutable i = 0
        for m in methods do
            printfn "    | %s = %d" m.name i
            i <- i + 1

        let typeNames =
            LookupTable.lookupTable' [
                typeof<uint8>, "byte"
                typeof<int8>, "int8"
                typeof<uint16>, "uint16"
                typeof<int16>, "int16"
                typeof<uint32>, "uint32"
                typeof<int32>, "int"
                typeof<uint64>, "uint64"
                typeof<int64>, "int64"
                typeof<float32>, "float32"
                typeof<float>, "float"

                typeof<nativeint>, "nativeint"
                typeof<unativeint>, "unativeint"
                typeof<bool>, "bool"
            ]

        let rec getTypeName (t : Type) =
            match typeNames t with
                | Some name -> name
                | None -> 
                    if t.IsPointer then 
                        let et = t.GetElementType()
                        if et = typeof<System.Void> then
                            "nativeint"
                        else 
                            sprintf "nativeptr<%s>" (getTypeName et)
                    elif t.IsByRef then
                        let et = t.GetElementType()
                        sprintf "byref<%s>" (getTypeName et)
                    elif typeof<Delegate>.IsAssignableFrom t then
                        "nativeint"
                    else
                        t.Name


        let reserved =
            Set.ofList ["type"; "signature"; "fun"; "function"; "class"; "x"; "end"; "params"; "val"]

        let name (name : string) =
            if Set.contains name reserved then "_" + name
            else name

        printfn ""
        printfn ""
        
        printfn "type ICommandStream ="
        printfn "    inherit IDisposable"
        for m in methods do
            let pars = 
                match m.args with
                    | [] -> "unit"
                    | _ -> m.args |> List.map (fun (n,t) -> sprintf "%s : %s" (name n) (getTypeName t)) |> String.concat " * "
            printfn "    abstract member %s : %s -> unit" m.name pars
            
        printfn "    abstract member Run : unit -> unit"
        printfn "    abstract member Count : int"
        printfn "    abstract member Clear : unit -> unit"

        printfn ""
        printfn ""
        printfn "[<AutoOpen>]"
        printfn "module private NativeHelpers ="
        printfn "    let inline nsize<'a> = nativeint sizeof<'a>"
        printfn "    let inline read<'a when 'a : unmanaged> (ptr : byref<nativeint>) : 'a ="
        printfn "        let a = NativePtr.read (NativePtr.ofNativeInt ptr)"
        printfn "        ptr <- ptr + nsize<'a>"
        printfn "        a"
        printfn ""
        printfn ""
        printfn "type NativeCommandStream(initialSize : nativeint) = "
        printfn "    let initialSize = nativeint (max (Fun.NextPowerOfTwo (int64 initialSize)) 128L)"
        printfn "    static let ptrSize = nativeint sizeof<nativeint>"
        printfn "    let mutable capacity = initialSize"
        printfn "    let mutable mem = Marshal.AllocHGlobal capacity"
        printfn "    let mutable offset = 0n"
        printfn "    let mutable count = 0"
        printfn ""
        printfn "    let resize (minSize : nativeint) ="
        printfn "        let newCapacity = nativeint (Fun.NextPowerOfTwo(int64 minSize)) |> max initialSize"
        printfn "        if capacity <> newCapacity then"
        printfn "            mem <- Marshal.ReAllocHGlobal(mem, newCapacity)"
        printfn "            capacity <- newCapacity"

        printfn "    member x.Memory = mem"
        printfn "    member x.Size = offset"

        for argCount in 0 .. 15 do
            let tnames = List.init argCount (fun i -> sprintf "%c" ('a' + char i))
            let targs = List.init argCount (fun i -> sprintf "'%c" ('a' + char i))

            let args = "code : InstructionCode" :: (targs |> List.mapi (sprintf "arg%d : %s"))

            printfn "    member inline private x.Append(%s) =" (String.concat ", " args)
            for t in tnames do
                printfn "        let s%s = nativeint sizeof<'%s>" t t

            let sizes = "8n" :: (tnames |> List.map (sprintf "s%s"))
            printfn "        let size = %s" (String.concat "+" sizes)
            printfn "        if offset + size > capacity then resize (offset + size)"
            printfn "        let mutable ptr = mem + offset"
            printfn "        NativePtr.write (NativePtr.ofNativeInt ptr) (int size); ptr <- ptr + 4n"
            printfn "        NativePtr.write (NativePtr.ofNativeInt ptr) (int code); ptr <- ptr + 4n"

            let mutable i = 0
            for t in tnames do
                printfn "        NativePtr.write (NativePtr.ofNativeInt ptr) arg%d; ptr <- ptr + s%s" i t
                i <- i + 1
            printfn "        offset <- offset + size"
            printfn "        count <- count + 1"

            ()


        printfn "    member x.Dispose() ="
        printfn "        Marshal.FreeHGlobal mem"
        printfn "        capacity <- 0n"
        printfn "        mem <- 0n"
        printfn "        offset <- 0n"
        printfn "        count <- 0"
        
        printfn "    member x.Clear() ="
        printfn "        resize initialSize"
        printfn "        offset <- 0n"
        printfn "        count <- 0"
        
        printfn "    member x.Count = count"
        

        for m in methods do
            let pars = m.args |> List.map (fun (n,t) -> sprintf "%s : %s" (name n) (getTypeName t)) |> String.concat ", "
            let args = 
                m.args |> List.map (fun (n,t) -> 
                    if t = typeof<bool> then sprintf "(if %s then 1 else 0)" (name n)
                    else sprintf "%s" (name n)
                )

            let args = sprintf "InstructionCode.%s" m.name :: args


            printfn "    member x.%s(%s) =" m.name pars
            printfn "        x.Append(%s)" (String.concat ", " args)


        printfn ""
        printfn ""
        printfn "    static member RunInstruction(ptr : byref<nativeint>) = "
        printfn "        let s : int = NativePtr.read (NativePtr.ofNativeInt ptr)"
        printfn "        let fin = ptr + nativeint s"
        printfn "        ptr <- ptr + 4n"
        printfn "        let c : InstructionCode = NativePtr.read (NativePtr.ofNativeInt ptr)"
        printfn "        ptr <- ptr + 4n"
        printfn ""
        printfn "        match c with"
        for m in methods do
            let args = 
                m.args |> List.map (fun (n,t) -> 
                    if t = typeof<bool> then
                        "(read<int> &ptr = 1)"
                    else
                        sprintf "read<%s> &ptr" (getTypeName t)
                )
            printfn "        | InstructionCode.%s ->" m.name
            printfn "            %s.%s(%s)" m.decl m.name (String.concat ", " args)
            

        printfn "        | code -> "
        printfn "            Log.warn \"unknown instruction: %%A\" code"
        printfn ""
        printfn "        ptr <- fin"
        printfn ""
        printfn ""
        printfn "    member x.Run() = "
        printfn "        let mutable ptr = mem"
        printfn "        let e = ptr + offset"
        printfn "        while ptr <> e do"
        printfn "            NativeCommandStream.RunInstruction(&ptr)"
        printfn ""
        printfn ""
        printfn "    interface IDisposable with"
        printfn "        member x.Dispose() = x.Dispose()"

        printfn "    interface ICommandStream with"
        for m in methods do
            let args = m.args |> List.map (fun (n,t) -> name n) |> String.concat ", "
            printfn "        member x.%s(%s) = x.%s(%s)" m.name args m.name args
        printfn "        member x.Run() = x.Run()"
        printfn "        member x.Clear() = x.Clear()"
        printfn "        member x.Count = x.Count"

        printfn ""
        printfn ""
        let code = builder.ToString()
        File.writeAllText (System.IO.Path.Combine(__SOURCE_DIRECTORY__, "NewCommandStream.fs")) code


open OpenTK.Graphics.OpenGL4
open Microsoft.FSharp.NativeInterop


module Shader =
    open FShade

    type Vertex =
        {
            [<Position>] pos : V4d
            [<Semantic("Offsets")>] offset : V3d
        }


    let offset ( v : Vertex) =
        vertex {
            return  { v with pos = v.pos + V4d(v.offset, 0.0)}
        }



[<EntryPoint>]
let main argv = 

    Ag.initialize()
    Aardvark.Init()

    use app = new OpenGlApplication(true, false)
    let win = app.CreateGameWindow(1)
    let runtime = app.Runtime
    let ctx = runtime.Context
    
    let proj =
        win.Sizes
        |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 500.0 (float s.X / float s.Y))
        |> Mod.map Frustum.projTrafo

    let initial = CameraView.lookAt (10.0 * V3d.III) V3d.Zero V3d.OOI
    let mutable lastLod = initial
    let mutable lastSpectator = initial

    let pointSize = Mod.init 4.0


    let lodCam = initial |> DefaultCameraController.control win.Mouse win.Keyboard win.Time
    let cam = lodCam

    let view = cam |> Mod.map (CameraView.viewTrafo)
    let lodView = lodCam |> Mod.map (CameraView.viewTrafo)

    let uniforms =
        let cam = view |> Mod.map (fun v -> v.Backward.C3.XYZ)
        [
            "ModelTrafo", Mod.constant Trafo3d.Identity :> IMod
            "ViewTrafo", view :> IMod
            "ProjTrafo", proj :> IMod 
            "PointSize", Mod.constant 5.0 :> IMod
            "CameraLocation", cam :> IMod
            "LightLocation", cam :> IMod
            "PointSize", pointSize :> IMod
            "ViewportSize", win.Sizes :> IMod
        ]
        
    let state =
        {
            depthTest           = Mod.constant DepthTestMode.LessOrEqual
            cullMode            = Mod.constant CullMode.None
            blendMode           = Mod.constant BlendMode.None
            fillMode            = Mod.constant FillMode.Fill
            stencilMode         = Mod.constant StencilMode.Disabled
            multisample         = Mod.constant true
            writeBuffers        = None
            globalUniforms      = UniformProvider.ofList uniforms
                         
            geometryMode        = IndexedGeometryMode.PointList
            vertexInputTypes    = Map.empty
            perGeometryUniforms = Map.empty
        }
        

    let surface =
        Surface.FShadeSimple (
            FShade.Effect.compose [
                Shader.offset |> FShade.Effect.ofFunction
                DefaultSurfaces.trafo |> FShade.Effect.ofFunction
                
                //Shader.lodPointSize  |> FShade.Effect.ofFunction
                //DefaultSurfaces.pointSprite |> FShade.Effect.ofFunction
                DefaultSurfaces.vertexColor |> FShade.Effect.ofFunction
                //DefaultSurfaces.simpleLighting |> FShade.Effect.ofFunction
                //DefaultSurfaces.pointSpriteFragment |> FShade.Effect.ofFunction
            ]
        )

    let man = runtime.ResourceManager

    let mutable fst = None

    let miniMapView = 
        view |> Mod.map (fun v ->
            let mainCam = v.Backward.C3.XYZ
            CameraView.lookAt (mainCam + V3d(0.0,0.0,200.0 - mainCam.Z)) mainCam V3d.OIO |> CameraView.viewTrafo
        )
        
    let miniMapProj = 
        win.Sizes |> Mod.map (fun s ->
            Frustum.perspective 90.0 10.0 1000.0 (float s.X / float s.Y)
                |> Frustum.projTrafo
        )

    let preparedState (info : CompilerInfo) = 
        printfn "compile"
        if fst = None || fst = Some info.task then
            fst <- Some info.task
            PreparedPipelineState.ofPipelineState win.FramebufferSignature man surface state
        else
            let uniforms =
                UniformProvider.union
                    (
                        UniformProvider.ofList [
                            "ViewTrafo", miniMapView :> IMod
                            "ProjTrafo", miniMapProj :> IMod
                        ]
                    )
                    state.globalUniforms
            PreparedPipelineState.ofPipelineState win.FramebufferSignature man surface { state with globalUniforms = uniforms }
            

    //let box = Box3d(V3d(-50.0, -50.0, -50.0), V3d(50.0, 50.0, 50.0))
    //let stupid = StupidOctreeNode(None,0,box) :> Bla.ITreeNode

    let stupids =
        ASet.ofList [
            //yield StoreTree.load "euclid" Trafo3d.Identity @"\\euclid\rmDATA\Data\KaunertalStore" "kaunertal" :> Bla.ITreeNode

            
            yield StoreTree.load "local" (Trafo3d.Translation(0.0, 0.0, 0.0)) @"C:\Users\Schorsch\Development\WorkDirectory\KaunertalStore" "kaunertal" :> Bla.ITreeNode
            
            // let import (sourceName : string) (trafo : Trafo3d) (file : string) (store : string) (key : string
            yield StoreTree.import "blibb" (Trafo3d.Translation(150.0, 0.0, 0.0)) @"C:\Users\Schorsch\Development\WorkDirectory\Technologiezentrum_Teil1.pts" @"C:\Users\Schorsch\Development\WorkDirectory\blubber" :> Bla.ITreeNode

            //for x in 0 .. 4 do
            //    for y in 0 .. 4 do
            //        let offset = V3d(x,y,0) * 150.0
            //        let box = Box3d(offset + V3d(-50.0, -50.0, -50.0), offset + V3d(50.0, 50.0, 50.0))
            //        yield StupidOctreeNode(None,0,box) :> Bla.ITreeNode
        ]


    let lod = new Bla.LodRenderer(ctx, preparedState, RenderPass.main, stupids, win.Time, lodView, proj) 
  
    
    let lodProj =
        win.Sizes
        |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 20.0 (float s.X / float s.Y))
        |> Mod.map Frustum.projTrafo


    let lodViewProj = Mod.map2 (*) lodView lodProj
    let camera =
        Sg.wireBox' C4b.Red (Box3d(V3d(-1.0, -1.0, -1.0), V3d(1.0, 1.0, 1.0)))
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.thickLine
                do! DefaultSurfaces.constantColor C4f.Red
            }
            |> Sg.uniform "LineWidth" (Mod.constant 3.0)
            |> Sg.trafo (lodViewProj |> Mod.map (fun t -> t.Inverse))
            |> Sg.viewTrafo miniMapView
            |> Sg.projTrafo miniMapProj
            |> Sg.depthTest (Mod.constant DepthTestMode.None)
            |> Sg.compile app.Runtime win.FramebufferSignature
            
    let main() = 
        app.Runtime.CompileRender(win.FramebufferSignature, ASet.ofList [lod :> IRenderObject])


    let overlayTexture =
        let size = win.Sizes |> Mod.map (fun s -> V2i(max 1 (s.X / 4), max 1 (s.Y / 4)))
        let clear = runtime.CompileClear(win.FramebufferSignature, Mod.constant (C4f(0.0, 0.0, 0.0, 0.0)))
        RenderTask.ofList [clear; main(); camera]
        |> RenderTask.renderToColor size


    let overlay =
        Sg.fullScreenQuad
        |> Sg.scale 0.25
        |> Sg.translate 0.75 0.75 0.0
        |> Sg.diffuseTexture overlayTexture
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.diffuseTexture
        }
        |> Sg.depthTest (Mod.constant DepthTestMode.None)
        |> Sg.blendMode (Mod.constant BlendMode.Blend)
        |> Sg.viewTrafo (Mod.constant Trafo3d.Identity)
        |> Sg.projTrafo (Mod.constant Trafo3d.Identity)
        |> Sg.compile app.Runtime win.FramebufferSignature
        

    win.RenderTask <- main() //RenderTask.ofList [main(); overlay]
    win.Run()

    0
