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
        let culling (infos : DrawInfo[]) (bounds : CullingInfo[]) (count : int) (viewProj : M44d) =
            compute {
                let id = getGlobalId().X
                if id < count then
                    let b = bounds.[id]
                    //let s = 0.2f * (b.Max.XYZ - b.Min.XYZ)
                    
                    //b.Min <- V4f(b.Min.XYZ + s, b.Min.W)
                    //b.Max <- V4f(b.Max.XYZ - s, b.Max.W)

                    if CullingInfo.intersectsViewProj viewProj b then
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

    [<StructLayout(LayoutKind.Sequential)>]
    type BoundingBox =
        struct
            val mutable public Min : V4f
            val mutable public Max : V4f
        end

    type IndirectBuffer(ctx : Context, bounds : bool, indexed : bool, initialCapacity : int, usedMemory : ref<int64>, totalMemory : ref<int64>) =
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
                Fun.Swap(&c.BaseVertex, &c.FirstInstance)
                c
            else
                call

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

        member x.Add(call : DrawCallInfo, box : Box3d) =
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
                                Max = V4f(V3f box.Max, 0.0f)
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
                    x.Add(call, box)

        member x.Add(call : DrawCallInfo) =
            x.Add(call, Unchecked.defaultof<Box3d>)

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

       

        member x.CompileRender(s : IAssemblerStream, useCulling : nativeptr<int>, mvp : nativeptr<M44f>, indexType : Option<_>, runtimeStats : nativeptr<_>, isActive : nativeptr<_>, mode : nativeptr<_>) =
            if bounds then
                let infoSlot = culling.Buffers |> List.pick (fun (a,b,c) -> if b = "infos" then Some a else None)
                let boundSlot = culling.Buffers |> List.pick (fun (a,b,c) -> if b = "bounds" then Some a else None)
                let uniformBlock = culling.UniformBlocks |> List.head
                let viewProjField = uniformBlock.ubFields |> List.find (fun f -> f.ufName = "cs_viewProj")
                let countField = uniformBlock.ubFields |> List.find (fun f -> f.ufName = "cs_count")
                //let activeField = uniformBlock.ubFields |> List.find (fun f -> f.ufName = "cs_active")

                let l = s.NewLabel()

                s.Cmp(NativePtr.toNativeInt useCulling, 0)
                s.Jump(JumpCondition.Equal, l)
                s.NamedBufferSubData(ub.Handle, nativeint viewProjField.ufOffset, 64n, NativePtr.toNativeInt mvp)
                s.NamedBufferSubData(ub.Handle, nativeint countField.ufOffset, 4n, NativePtr.toNativeInt bufferHandles + 8n)

                s.Get(GetPName.CurrentProgram, oldProgram)
                s.Get(GetIndexedPName.UniformBufferBinding, uniformBlock.ubBinding, oldUB)
                s.GetPointer(GetIndexedPName.UniformBufferStart, uniformBlock.ubBinding, oldUBOffset)
                s.GetPointer(GetIndexedPName.UniformBufferSize, uniformBlock.ubBinding, oldUBSize)

                s.UseProgram(culling.Handle)
                s.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, infoSlot, NativePtr.ofNativeInt (NativePtr.toNativeInt bufferHandles + 0n))
                s.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, boundSlot, NativePtr.ofNativeInt (NativePtr.toNativeInt bufferHandles + 4n))
                s.BindBufferBase(BufferRangeTarget.UniformBuffer, uniformBlock.ubBinding, ub.Handle)
                s.DispatchCompute computeSize

                s.UseProgram(oldProgram)
                s.BindBufferRangeIndirect(BufferRangeTarget.UniformBuffer, uniformBlock.ubBinding, oldUB, oldUBOffset, oldUBSize)
                s.Mark l


            match indexType with
                | Some indexType ->
                    s.DrawElementsIndirect(runtimeStats, isActive, mode, int indexType, indirectHandle)
                | _ -> 
                    s.DrawArraysIndirect(runtimeStats, isActive, mode, indirectHandle)


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

    type DrawPool(ctx : Context, bounds : bool, useCulling : IMod<bool>, state : CompilerInfo -> PreparedPipelineState, pass : RenderPass) as this =
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
        let mvp : nativeptr<M44f> = NativePtr.alloc 1
        let cullActive = NativePtr.allocArray [| (if useCulling.GetValue() then 1 else 0) |]

        let mutable pProgramInterface = Unchecked.defaultof<GLSLProgramInterface>
        let stateCache = Dict<CompilerInfo, PreparedPipelineState>()
        let mvpCache = Dict<CompilerInfo, Resource<Trafo3d, M44f>>()
        let getState (info : CompilerInfo) = 
            stateCache.GetOrCreate(info, fun info ->
                state info
            )  
            
        let mvpResource (info : CompilerInfo) =
            mvpCache.GetOrCreate(info, fun info ->
                let s = getState info

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

                res
            )


        let query : nativeptr<int> = NativePtr.allocArray [| 0 |]
        let startTime : nativeptr<uint64> = NativePtr.allocArray [| 0UL |]
        let endTime : nativeptr<uint64> = NativePtr.allocArray [| 0UL |]
        



        let usedMemory = ref 0L
        let totalMemory = ref 0L
        let avgRenderTime = RunningMean(10)

        let compile (indexType : Option<DrawElementsType>, mode : nativeptr<GLBeginMode>, a : VertexInputBindingHandle, ib : IndirectBuffer) (s : IAssemblerStream) =
            s.BindVertexAttributes(contextHandle, a)
            ib.CompileRender(s, cullActive, mvp, indexType, runtimeStats, isActive, mode)

        let indirects = Dict<_, IndirectBuffer>()
        let isOutdated = NativePtr.allocArray [| 1 |]
        let updateFun = Marshal.PinDelegate(System.Action(this.Update))
        let mutable oldCalls : list<Option<DrawElementsType> * nativeptr<GLBeginMode> * VertexInputBindingHandle * IndirectBuffer> = []
        let program = new ChangeableNativeProgram<_>(compile)
        let puller = AdaptiveObject()
        let sub = puller.AddMarkingCallback (fun () -> NativePtr.write isOutdated 1)
        let tasks = System.Collections.Generic.HashSet<IRenderTask>()

        let mark() = transact (fun () -> puller.MarkOutdated())
        

        let getIndirectBuffer(slot : PoolSlot) =
            let key = getKey slot
            indirects.GetOrCreate(key, fun _ ->
                new IndirectBuffer(ctx, bounds, Option.isSome slot.IndexType, initialIndirectSize, usedMemory, totalMemory)
            )

        let tryGetIndirectBuffer(slot : PoolSlot) =
            let key = getKey slot
            match indirects.TryGetValue key with
                | (true, ib) -> Some ib
                | _ -> None
                
                
        member x.Add(ref : PoolSlot, bounds : Box3d) =
            let ib = getIndirectBuffer ref
            if ib.Add(ref.DrawCallInfo, bounds) then
                mark()

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

                if useCulling.GetValue token then NativePtr.write cullActive 1
                else NativePtr.write cullActive 0

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
            )

        override x.Compile(info : CompilerInfo, stream : IAssemblerStream, last : Option<PreparedCommand>) =
            lock puller (fun () ->
                if tasks.Add info.task then
                    assert (info.task.OutOfDate)
                    puller.AddOutput(info.task) |> ignore
            )
            
            let state = getState info
            let mvpRes = mvpResource info

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

            let mutable src = NativePtr.toNativeInt (mvpRes :> IResource<_,_>).Pointer
            let mutable dst = NativePtr.toNativeInt mvp
            for i in 1 .. 16 do
                stream.Copy(src, dst, false)
                src <- src + 4n
                dst <- dst + 4n

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

        override x.GetResources(s) = 
            let res = getState(s).Resources
            let mvp = mvpResource s

            Seq.append (Seq.singleton (mvp :> IResource)) res

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
            { new DrawPool(ctx, false, Mod.constant false, state, pass) with
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
        abstract member ShouldCollapse : Trafo3d * Trafo3d -> bool

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


    open System.Collections.Generic
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
            original    : ITreeNode
            children    : list<MaterializedTree>
        }

    //[<RequireQualifiedAccess>]
    //type RenderOperation =
    //    | Split of children : hmap<ITreeNode, GeometryInstance>
    //    | Collapse of geometry : GeometryInstance * removes : list<ITreeNode>
    //    | Add of geometry : GeometryInstance
    //    | Remove of removes : list<ITreeNode>
 
    //    member x.removes =
    //        match x with
    //        | Collapse(_,r) -> r
    //        | Remove r -> r
    //        | _ -> []

    //module RenderOperation =
    //    let toString (op : RenderOperation) =
    //        match op with
    //            | RenderOperation.Add _             -> "add"
    //            | RenderOperation.Split _           -> "split"
    //            | RenderOperation.Collapse _        -> "collapse"
    //            | RenderOperation.Remove _          -> "remove"

    [<RequireQualifiedAccess>]
    type NodeOperation =
        | Split
        | Collapse of children : list<ITreeNode>
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
                
            if List.isEmpty t.children && node.ShouldSplit(view, proj) then //&& node.ShouldSplit(predictView node, proj) then
                Some { t with children = node.Children |> Seq.toList |> List.map ofNode }

            elif not (List.isEmpty t.children) && node.ShouldCollapse(view, proj) then
                Some { t with children = [] }

            else
                match t.children with
                    | [] ->
                        None

                    | children ->
                        match tryExpandMany children with
                            | Some newChildren -> Some { t with children = newChildren }
                            | _ -> None

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
                            HMap.add n.original (NodeOperation.Collapse(children)) acc
                        | os, ns    -> computeChildDeltas acc os ns
                else
                    failwith "inconsistent child values"



    type RenderState =
        {
            iface : GLSLProgramInterface
            calls : DrawPool
            mutable uploadSize : Mem
            mutable nodeSize : int
            mutable count : int
        }



    type LodRenderer(ctx : Context, signature : GLSLProgramInterface, state : CompilerInfo -> PreparedPipelineState, pass : RenderPass, useCulling : IMod<bool>, roots : aset<ITreeNode>, renderTime : IMod<_>, view : IMod<Trafo3d>, proj : IMod<Trafo3d>)  =
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
        let active = Dict<PoolSlot, ref<int>>()
        
        let addActive(s : PoolSlot) =
            let r = active.GetOrCreate(s, fun _ -> ref 0)
            let o = !r
            if o = -1 then 
                active.Remove s |> ignore
                false
            else
                r := o + 1
                !r > 0

        let removeActive(s : PoolSlot) =
            let r = active.GetOrCreate(s, fun _ -> ref 0)
            let o = !r
            if o = 1 then 
                active.Remove s |> ignore
                true
            else
                r := o - 1
                !r <= 0

        let needUpdate = Mod.init ()
        let renderingStateLock = obj()

        let mutable renderingConverged = 1

        let mutable renderDelta : AtomicQueue<ITreeNode, GeometryInstance> = AtomicQueue.empty
        let mutable deltaEmpty = true

        let alloc (state : RenderState) (node : ITreeNode) (g : GeometryInstance) =
            cache.GetOrCreate(node, fun node ->
                let slot = pool.Alloc(g.signature, g.instanceCount, g.indexCount, g.vertexCount)
                slot.Upload(g.geometry, g.uniforms)
                
                state.uploadSize <- state.uploadSize + slot.Memory
                state.nodeSize <- state.nodeSize + node.DataSize
                state.count <- state.count + 1
                slot
            )

        //let frees = Dict<ITreeNode, ref<AtomicOperation<ITreeNode, GeometryInstance>>>()

        let performOp (state : RenderState) (parentOp : AtomicOperation<ITreeNode, GeometryInstance>) (node : ITreeNode) (op : Operation<GeometryInstance>) =
            match op with
                | Alloc(instance, active) ->
                    let slot = alloc state node instance
                    if active > 0 then state.calls.Add(slot, node.BoundingBox) |> ignore
                    elif active < 0 then state.calls.Remove slot |> ignore
                    //frees.Remove(node) |> ignore

                | Free ->
                    match cache.TryRemove node with
                        | (true, slot) -> 
                            state.calls.Remove slot |> ignore
                            pool.Free slot
                            //let r = frees.GetOrCreate(node, fun _ -> ref Unchecked.defaultof<_>)
                            //r := parentOp
                        | _ ->
                            ()
                            //Log.warn "cannot free %s" node.Name

                | Activate ->
                    match cache.TryGetValue node with
                        | (true, slot) ->
                            state.calls.Add(slot, node.BoundingBox) |> ignore
                        | _ ->
                            ()
                            //match frees.TryGetValue(node) with
                            //    | (true, r) ->
                            //        let evilOp = !r
                            //        Log.warn "cannot activate %A %A (%A)" node (Option.isSome op.value) evilOp 
                            //    | _ ->
                            //        Log.warn "cannot activate %A %A (no reason)" node (Option.isSome op.value)
                            
                | Deactivate ->
                    match cache.TryGetValue node with
                        | (true, slot) ->
                            state.calls.Remove slot |> ignore
                        | _ ->
                            ()
                | Nop ->
                    ()
            
        let perform (state : RenderState) (op : AtomicOperation<ITreeNode, GeometryInstance>) =
            op.ops |> HMap.iter (performOp state op)

        let run (token : AdaptiveToken) (maxMem : Mem) (maxTime : MicroTime) (calls : DrawPool) (iface : GLSLProgramInterface) =
            let sw = System.Diagnostics.Stopwatch.StartNew()
      
            let state =
                {
                    iface = iface
                    calls = calls
                    uploadSize = Mem.Zero
                    nodeSize = 0
                    count = 0
                }

            let rec run (sinceSync : Mem)  =
                let mem = state.uploadSize > maxMem
                let time = sw.MicroTime > maxTime
            
                let sinceSync = 
                    if sinceSync.Bytes > 262144L then
                        GL.Sync()
                        Mem.Zero
                    else
                        sinceSync

                if mem || time then
                    GL.Sync()
                    if state.nodeSize > 0 && state.count > 0 then
                        updateTime.Add(sw.MicroTime.TotalMilliseconds)
                    renderTime.GetValue token |> ignore
                else
                    let dequeued = 
                        lock renderingStateLock (fun () ->
                            match AtomicQueue.tryDequeue renderDelta with
                            | Some (ops, rest) ->
                                renderDelta <- rest
                                deltaEmpty <- AtomicQueue.isEmpty rest
                                Some ops
                            | None ->
                                None
                        )
                    match dequeued with
                    | None -> 
                        GL.Sync()
                        if state.nodeSize > 0 && state.count > 0 then
                            updateTime.Add(sw.MicroTime.TotalMilliseconds)

                        renderingConverged <- 1

                    | Some ops ->
                        let oldSize = state.uploadSize
                        perform state ops
                        run (sinceSync + state.uploadSize - oldSize)

            run Mem.Zero

        let evaluate (calls : DrawPool) (token : AdaptiveToken) (iface : GLSLProgramInterface) =
            needUpdate.GetValue(token)
            let maxTime = max (1 * ms) calls.AverageRenderTime
            let maxMem = Mem (3L <<< 30)
            run token maxMem maxTime calls iface
            
        let inner =
            let mutable lastUsed = Mem.Zero
            let mutable lastTotal = Mem.Zero
            { new DrawPool(ctx, true, useCulling, state, pass) with
                override x.Evaluate(token : AdaptiveToken, iface : GLSLProgramInterface) =
                    evaluate x token iface
            }

        

        let thread =
            let prediction = Prediction.euclidean (MicroTime(TimeSpan.FromMilliseconds 55.0))
            let predictedTrafo = prediction |> Prediction.map Trafo3d
    
            let mutable v = 0
            let newVersion() = Interlocked.Increment(&v)
        
            let mutable baseState : hmap<ITreeNode, MaterializedTree> = HMap.empty
            let mutable baseVersion = newVersion()

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

                            Log.line "m: %A (%A) r: %A l : %s e: %A u: %A" (pool.UsedMemory + inner.UsedMemory) (pool.TotalMemory + inner.TotalMemory) inner.AverageRenderTime loads e u

                )

            let submit (op : AtomicOperation<ITreeNode, GeometryInstance>) =  
                renderDelta <- AtomicQueue.enqueue op renderDelta
                deltaEmpty <- AtomicQueue.isEmpty renderDelta

            let computeDeltas o n =
                HMap.choose2 (fun _ l r -> Some (l, r)) o n
                |> Seq.fold (fun delta (_,(l, r)) ->
                    match l, r with
                        | None, None -> 
                            delta

                        | Some l, Some r -> 
                            MaterializedTree.computeDelta delta l r

                        | Some l, None -> 
                            let children = MaterializedTree.allChildren l |> Seq.map MaterializedTree.original |> Seq.toList
                            HMap.add l.original (NodeOperation.Remove children) delta

                        | None, Some r -> 
                            assert (r.original.Level = 0)
                            HMap.add r.original NodeOperation.Add delta
                    
                ) HMap.empty


           
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
                                c.Cancel()
                            | _ -> 
                                ()
         
                    let load (ct : CancellationToken) (node : ITreeNode) (cont : CancellationToken -> ITreeNode -> GeometryInstance -> 'r) =
                        let startTime = time()

                        node.GetData(ct).ContinueWith (fun (t : Task<_>) ->
                            if t.IsCompletedSuccessfully then
                                let (g,u) = t.Result
                                let loaded = GeometryInstance.ofGeometry signature g u

                                let endTime = time()
                                addLoadTime node.DataSource node.DataSize (endTime - startTime)


                                if not ct.IsCancellationRequested then
                                    cont ct node loaded
                                else
                                    raise <| OperationCanceledException()
                            else
                                raise <| OperationCanceledException()
                        )

                    let subs =
                        [
                            view.AddMarkingCallback (fun () -> notConverged.Set())
                            proj.AddMarkingCallback (fun () -> notConverged.Set())
                            reader.AddMarkingCallback (fun () -> notConverged.Set())
                        ]

                    while true do
                        timer.Wait()
                        notConverged.Wait()

                        view.GetValue() |> ignore
                        proj.GetValue() |> ignore
                        let ops = reader.GetOperations AdaptiveToken.Top
                        
                        let deltas = 
                            if HDeltaSet.isEmpty ops then
                                let startTime = time()

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
                                    baseState |> HMap.map (fun _ -> MaterializedTree.expand predictView predictedView (proj.GetValue()))

                                let deltas = computeDeltas loadingState newState
                                loadingState <- newState

                                let dt = time() - startTime
                                expandTime.Add(dt.TotalMilliseconds)

                                deltas

                            else 
                                let mutable newState = loadingState
                                for o in ops do
                                    match o with
                                    | Add(_,root) -> 
                                        let r = MaterializedTree.ofNode root
                                        baseState <- HMap.add root r baseState
                                        newState <- HMap.add root r newState

                                    | Rem(_,root) -> 
                                        baseState <- HMap.remove root baseState
                                        newState <- HMap.remove root newState

                                let deltas = computeDeltas loadingState newState
                                loadingState <- newState

                                baseVersion <- newVersion()
                                deltas
                              
                     
                        if HMap.isEmpty deltas then
                            if renderingConverged = 1 && runningTasks = 0 then
                                if baseVersion = loadingVersion then
                                    notConverged.Reset()
                                else
                                    baseState <- loadingState
                                    baseVersion <- loadingVersion
                        else
                            renderingConverged <- 0
                            loadingVersion <- newVersion()

                            for (v, delta) in deltas do
                                stop v

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




                                match delta with
                                    | NodeOperation.Remove children ->
                                        lock renderingStateLock (fun () ->
                                            submit (remove v children)
                                        )

                                    | NodeOperation.Split ->
                                        let c = new CancellationTokenSource()
                                        
                                        let loadChildren = 
                                            v.Children |> Seq.toList |> List.map (fun v -> 
                                                let t = load c.Token v (fun _ n l -> (n,l))
                                                t
                                            )
                                        
                                        Interlocked.Increment(&runningTasks) |> ignore
                                        let s = c.Token.Register (fun () -> Interlocked.Decrement(&runningTasks) |> ignore)
                                    
                                        let myTask = 
                                            Task.WhenAll(loadChildren).ContinueWith (fun (a : Task<array<_>>) ->
                                                if a.IsCompletedSuccessfully then
                                                    lock renderingStateLock (fun () ->
                                                        if not c.IsCancellationRequested then
                                                            cancel.TryRemove v |> ignore
                                                            let op = split v (HMap.ofArray a.Result)
                                                            submit op
                                                            c.Cancel()
                                                    )
                                            )
                                        
                                        v.Children |> Seq.iter (fun ci -> cancel.[ci] <- c)
                                        cancel.[v] <- c
                                        


                                    | NodeOperation.Collapse(children) ->
                                        children |> List.iter stop
                                        lock renderingStateLock (fun () ->
                                            let myOp = collapse v children
                                            submit myOp
                                        )


                                    | NodeOperation.Add ->
                                        let ct = CancellationToken.None
                                        Interlocked.Increment(&runningTasks) |> ignore
     
                                        load ct v (fun _ v l ->
                                            lock renderingStateLock (fun () ->
                                                submit (add v l)
                                                Interlocked.Decrement(&runningTasks) |> ignore
                                            )
                                        ) |> ignore

                    for s in subs do s.Dispose()
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
    open Aardvark.Data.Points.Import
    open Aardvark.Data.Points
    open Aardvark.Data.Points.Import


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
                    
                let normals = 
                    if self.HasLodNormals then self.LodNormals.GetValue(ct)
                    elif self.HasNormals then self.Normals.GetValue(ct) 
                    else positions |> Array.map (fun _ -> V3f.OOI)

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


                return geometry, uniforms
            }

        member x.Children = children

        member x.Id = self.Id

        member x.GetData(ct : CancellationToken) = 
            Async.StartAsTask(load, cancellationToken = ct)
            
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

        member x.ShouldCollapse (view : Trafo3d, proj : Trafo3d) =
            let cam = view.Backward.C3.XYZ

            let avgPointDistance = bounds.Size.NormMax / 40.0

            let distRange =
                bounds.ComputeCorners() 
                    |> Seq.map (fun p -> V3d.Distance(p, cam))
                    |> Range1d

            let distRange = 
                Range1d(max 1.0 distRange.Min, max 1.0 distRange.Max)

            let angle = Constant.DegreesPerRadian * atan2 avgPointDistance distRange.Min

            angle < 1.0

        member x.DataSource = source

        override x.ToString() = 
            sprintf "%s[%d]" (string x.Id) level

        interface Bla.ITreeNode with
            member x.Level = level
            member x.Name = string x.Id
            member x.DataSource = source
            member x.Parent = parent |> Option.map (fun n -> n :> Bla.ITreeNode)
            member x.Children = x.Children |> Seq.map (fun n -> n :> Bla.ITreeNode)
            member x.ShouldSplit(v,p) = x.ShouldSplit(v,p)
            member x.ShouldCollapse(v,p) = x.ShouldCollapse(v,p)
            member x.DataSize = int self.LodPointCount
            member x.GetData(ct : CancellationToken) = x.GetData(ct)
            member x.BoundingBox = bounds

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

    let importAscii (sourceName : string) (trafo : Trafo3d) (file : string) (store : string) =
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
            Trafo3d.Scale(targetSize / bounds.Size.NormMax) * 
            trafo

        let source = Symbol.Create sourceName
        PointTreeNode(source, trafo, None, 0, points.Root.Value) :> Bla.ITreeNode

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
            Trafo3d.Scale(targetSize / bounds.Size.NormMax) * 
            trafo

        let source = Symbol.Create sourceName
        PointTreeNode(source, trafo, None, 0, points.Root.Value) :> Bla.ITreeNode





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

    let getData (ct : CancellationToken) =
        async {
            do! Async.SwitchToThreadPool()
            return data.Value
        }
        
    override x.ToString() = string bounds
    interface Bla.ITreeNode with
        member x.Level = level
        member x.Name = string bounds
        member x.DataSource = DataSource
        member x.Parent = parent |> Option.map (fun n -> n :> Bla.ITreeNode)
        member x.Children = children.Value |> Seq.map (fun n -> n :> Bla.ITreeNode)
        member x.ShouldSplit (v,p) = shouldSplit v p
        member x.ShouldCollapse (v,p) = shouldCollapse v p
        member x.DataSize = 1024
        member x.GetData(ct : CancellationToken) = Async.StartAsTask (getData ct, cancellationToken = ct)
        member x.BoundingBox = bounds


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
        
    override x.ToString() = string bounds
    interface Bla.ITreeNode with
        member x.Level = level
        member x.Name = string bounds
        member x.DataSource = DataSource
        member x.Parent = parent |> Option.map (fun n -> n :> Bla.ITreeNode)
        member x.Children = children.Value |> Seq.map (fun n -> n :> Bla.ITreeNode)
        member x.ShouldSplit (v,p) = shouldSplit v p
        member x.ShouldCollapse (v,p) = not (shouldSplit v p)
        member x.DataSize = 1024
        member x.GetData(ct : CancellationToken) = Async.StartAsTask (getData ct, cancellationToken = ct)
        member x.BoundingBox = bounds

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
            [<Color>] color : V4d
            [<Normal>] n : V3d
            [<Semantic("ViewPosition")>] vp : V3d
            [<Semantic("AvgPointDistance")>] dist : float
            [<Semantic("DepthRange")>] depthRange : float
            [<PointSize>] s : float
            [<PointCoord; Interpolation(InterpolationMode.Sample)>] c : V2d
            [<FragCoord>] fc : V4d
        }

    let lodPointSize (v : PointVertex) =
        vertex { 
            let ovp = uniform.ModelViewTrafo * v.pos 

            let vp = ovp + V4d(0.0, 0.0, 0.5*v.dist, 0.0)
            let vp1 = ovp + V4d(0.5 * v.dist, 0.0, 0.0, 0.0)

            let pp = uniform.ProjTrafo * vp
            let pp1 = uniform.ProjTrafo * vp1

            let ndcDist = abs (pp.X / pp.W - pp1.X / pp1.W)
            let depthRange = abs (pp.Z / pp.W - pp1.Z / pp1.W)

            let pixelDist = ndcDist * float uniform.ViewportSize.X
            let n = uniform.ModelViewTrafo * V4d(v.n, 0.0) |> Vec.xyz |> Vec.normalize
            
            let pp = 
                if pp.Z < -pp.W then V4d(0,0,2,1)
                else pp

            return { v with s = pixelDist; pos = pp; depthRange = depthRange; n = n; vp = vp.XYZ }
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
            

            return { c = v.color; d = outDepth }
        }

    let cameraLight (v : PointVertex) =
        fragment {
            let vn = Vec.normalize v.n
            let vd = Vec.normalize v.vp 

            let diffuse = Vec.dot vn vd |> abs
            return V4d(v.color.XYZ * diffuse, v.color.W)
        }




    let normalColor ( v : Vertex) =
        fragment {
            let mutable n = Vec.normalize v.n

            let vn = uniform.ViewTrafo * V4d(n, 0.0)
            if vn.Z < 0.0 then n <- -n

            let n = (n + V3d.III) * 0.5
            return V4d(n, 1.0)
        }


[<EntryPoint>]
let main argv = 

    Ag.initialize()
    Aardvark.Init()

    use app = new OpenGlApplication(false, false)
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
                //Shader.offset |> FShade.Effect.ofFunction
                //DefaultSurfaces.trafo |> FShade.Effect.ofFunction
                
                Shader.lodPointSize  |> FShade.Effect.ofFunction
                //DefaultSurfaces.pointSprite |> FShade.Effect.ofFunction
                Shader.cameraLight |> FShade.Effect.ofFunction
                Shader.lodPointCircular |> FShade.Effect.ofFunction
                //Shader.normalColor |> FShade.Effect.ofFunction
                //DefaultSurfaces.pointSpriteFragment |> FShade.Effect.ofFunction
                //DefaultSurfaces.simpleLighting |> FShade.Effect.ofFunction
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

    let defaultState =
        PreparedPipelineState.ofPipelineState win.FramebufferSignature man surface state

    let preparedState (info : CompilerInfo) = 
        printfn "compile"
        if fst = None || fst = Some info.task then
            fst <- Some info.task
            defaultState
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

            
            //yield StoreTree.load "local" (Trafo3d.Translation(150.0, 0.0, 0.0)) @"C:\Users\Schorsch\Development\WorkDirectory\KaunertalStore" "kaunertal" :> Bla.ITreeNode
            
            // let import (sourceName : string) (trafo : Trafo3d) (file : string) (store : string) (key : string
            //yield StoreTree.import 
            //    "blibb" 
            //    (Trafo3d.Translation(0.0, 0.0, 0.0)) 
            //    @"C:\Users\Schorsch\Development\WorkDirectory\Kindergarten.pts" 
            //    @"C:\Users\Schorsch\Development\WorkDirectory\blubber" 

            yield StoreTree.importAscii
                "bla"
                (Trafo3d.Translation(0.0, 0.0, 0.0)) 
                @"C:\Users\Schorsch\Development\WorkDirectory\Kaunertal.txt"
                @"C:\Users\Schorsch\Development\WorkDirectory\KaunertalNormals"
                
            yield StoreTree.import
                "bla"
                (Trafo3d.Translation(100.0, 0.0, 0.0)) 
                @"C:\Users\Schorsch\Development\WorkDirectory\Technologiezentrum_Teil1.pts"
                @"C:\Users\Schorsch\Development\WorkDirectory\Technologiezentrum"


            //for x in 0 .. 4 do
            //    for y in 0 .. 4 do
            //        let offset = V3d(x,y,0) * 150.0
            //        let box = Box3d(offset + V3d(-50.0, -50.0, 100.0), offset + V3d(50.0, 50.0, 200.0))
            //        yield StupidOctreeNode(None,0,box) :> Bla.ITreeNode
        ]

    let useCulling = Mod.init false
    win.Keyboard.KeyDown(Keys.C).Values.Add(fun () ->
        transact (fun () -> useCulling.Value <- not useCulling.Value)
        Log.warn "culling: %A" useCulling.Value
    )

    win.Keyboard.KeyDown(Keys.X).Values.Add(fun () ->
        win.RenderAsFastAsPossible <- not win.RenderAsFastAsPossible
    )

    let lod = new Bla.LodRenderer(ctx, defaultState.pProgramInterface, preparedState, RenderPass.main, useCulling, stupids, win.Time, lodView, proj) 
  
    
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
