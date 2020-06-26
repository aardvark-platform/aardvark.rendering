namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open FShade
open FShade.GLSL
open OpenTK.Graphics.OpenGL4
open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Rendering
open Aardvark.Base.Management
open Aardvark.Base.Runtime
open Aardvark.Rendering.GL

#nowarn "9"



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
            
    [<StructLayout(LayoutKind.Sequential)>]
    type CullingInfo =
        struct
            val mutable public Min : V4f
            val mutable public Max : V4f
            val mutable public CellMin : V4f
            val mutable public CellMax : V4f
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
                    
                if isActive.[rootId] <> 0 && CullingInfo.intersectsViewProj viewProjs.[rootId] b then
                    infos.[id].InstanceCount <- CullingInfo.instanceCount b
                else
                    infos.[id].InstanceCount <- 0
        }

    type UniformScope with
        member x.Bounds : CullingInfo[] = uniform?StorageBuffer?Bounds 
        member x.ViewProjs : M44d[] = uniform?StorageBuffer?ViewProjs 

    type Vertex =
        {
            [<InstanceId>] id : int
            [<VertexId>] vid : int
            [<Position>] pos : V4d
        }

    let data =
        [|
            V3d.OOO; V3d.IOO
            V3d.OOI; V3d.IOI
            V3d.OIO; V3d.IIO
            V3d.OII; V3d.III
                
            V3d.OOO; V3d.OIO
            V3d.OOI; V3d.OII
            V3d.IOO; V3d.IIO
            V3d.IOI; V3d.III
                
            V3d.OOO; V3d.OOI
            V3d.OIO; V3d.OII
            V3d.IOO; V3d.IOI
            V3d.IIO; V3d.III
        |]

    let renderBounds (v : Vertex) =
        vertex {
            let bounds = uniform.Bounds.[v.id]
            let rootId = int (bounds.Max.W + 0.5f)
                    
            let off = V3d bounds.CellMin.XYZ
            let size = V3d bounds.CellMax.XYZ - off

            let p = data.[v.vid]

            let wp = off + p * size
            let p = uniform.ViewProjs.[rootId] * V4d(wp, 1.0)
            return { v with pos = p }
        }


type InstanceSignature = MapExt<string, GLSLType * Type>
type VertexSignature = MapExt<string, Type>

type GeometryPoolSignature =
    {
        mode            : IndexedGeometryMode
        indexType       : Option<Type>
        uniformTypes    : InstanceSignature
        attributeTypes  : VertexSignature
    }

module GeometryPoolSignature =
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


type private Regression(degree : int, maxSamples : int) =
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

[<AutoOpen>]
module private ContextMappingExtensions = 
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

                let buffer =
                    if count = 0 then new Aardvark.Rendering.GL.Buffer(ctx, 0n, 0)
                    else ctx.CreateBuffer(elemSize * count)

                buffer, elemSize, write
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
        buffers |> MapExt.iter (fun _ (b,_,_) -> if b.SizeInBytes > 0n then ctx.Delete b)

    interface IDisposable with
        member x.Dispose() = x.Dispose()

type VertexBuffer(ctx : Context, semantics : MapExt<string, Type>, count : int) =

    let totalSize, buffers =
        let mutable totalSize = 0L
        let buffers = 
            semantics |> MapExt.map (fun sem typ ->
                let elemSize = Marshal.SizeOf typ
                totalSize <- totalSize + int64 elemSize * int64 count
                let buffer =
                    if count = 0 then new Aardvark.Rendering.GL.Buffer(ctx, 0n, 0)
                    else ctx.CreateBuffer(elemSize * count)
                buffer, elemSize, typ
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
                let size = nativeint count * nativeint elemSize
                if size > 0n then
                    let offset = nativeint startIndex * nativeint elemSize
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
        buffers |> MapExt.iter (fun _ (b,_,_) -> if b.SizeInBytes > 0n then ctx.Delete b)

    interface IDisposable with
        member x.Dispose() = x.Dispose()

type VertexManager(ctx : Context, semantics : MapExt<string, Type>, chunkSize : int, usedMemory : ref<int64>, totalMemory : ref<int64>) =
    let elementSize =
        semantics |> MapExt.toSeq |> Seq.sumBy (fun (_,t) -> int64 (Marshal.SizeOf t))

    let mem : Memory<VertexBuffer> =
        let malloc (size : nativeint) =
            //Log.warn "alloc VertexBuffer"
            let res = new VertexBuffer(ctx, semantics, int size)
            Interlocked.Add(&totalMemory.contents, res.TotalSize) |> ignore
            res

        let mfree (ptr : VertexBuffer) (size : nativeint) =
            //Log.warn "free VertexBuffer"
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
            //Log.warn "alloc InstanceBuffer"
            let res = new InstanceBuffer(ctx, semantics, int size)
            Interlocked.Add(&totalMemory.contents, res.TotalSize) |> ignore
            res

        let mfree (ptr : InstanceBuffer) (size : nativeint) =
            //Log.warn "free InstanceBuffer"
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

type IndirectBuffer(ctx : Context, alphaToCoverage : bool, renderBounds : nativeptr<int>, signature : IFramebufferSignature, bounds : bool, active : nativeptr<int>, modelViewProjs : nativeptr<int>, indexed : bool, initialCapacity : int, usedMemory : ref<int64>, totalMemory : ref<int64>) =
    static let es = sizeof<DrawCallInfo>
    static let bs = sizeof<CullingShader.CullingInfo>

    static let ceilDiv (a : int) (b : int) =
        if a % b = 0 then a / b
        else 1 + a / b

    static let cullingCache = System.Collections.Concurrent.ConcurrentDictionary<Context, ComputeShader>()
    static let boundCache = System.Collections.Concurrent.ConcurrentDictionary<Context, Program>()
        
    let initialCapacity = Fun.NextPowerOfTwo initialCapacity
    let adjust (call : DrawCallInfo) =
        if indexed then
            let mutable c = call
            Fun.Swap(&c.BaseVertex, &c.FirstInstance)
            c
        else
            let mutable c = call
            c

    let drawIndices = Dict<DrawCallInfo, int>()
    let mutable capacity = initialCapacity
    let mutable mem : nativeptr<DrawCallInfo> = NativePtr.alloc capacity
    let mutable bmem : nativeptr<CullingShader.CullingInfo> = if bounds then NativePtr.alloc capacity else NativePtr.zero


    let mutable buffer = ctx.CreateBuffer (es * capacity)
    let mutable bbuffer = if bounds then ctx.CreateBuffer(bs * capacity) else new Buffer(ctx, 0n, 0)

    let ub = ctx.CreateBuffer(128)

    let dirty = System.Collections.Generic.HashSet<int>()
    let mutable count = 0
    let mutable stride = 20

    let bufferHandles = NativePtr.allocArray [| V3i(buffer.Handle, bbuffer.Handle, count) |]
    let indirectHandle = NativePtr.allocArray [| IndirectDrawArgs(buffer.Handle, count, stride) |]
    let computeSize = NativePtr.allocArray [| V3i.Zero |]

    let updatePointers() =
        NativePtr.write bufferHandles (V3i(buffer.Handle, bbuffer.Handle, count))
        NativePtr.write indirectHandle (IndirectDrawArgs(buffer.Handle, count, stride))
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

    let boxShader =
        if bounds then
            boundCache.GetOrAdd(ctx, fun ctx ->
                let effect =
                    FShade.Effect.compose [
                        Effect.ofFunction CullingShader.renderBounds
                        Effect.ofFunction (DefaultSurfaces.constantColor C4f.Red)
                    ]

                let shader =
                    lazy (
                        let cfg = signature.EffectConfig(Range1d(-1.0, 1.0), false)
                        effect
                        |> Effect.toModule cfg
                        |> ModuleCompiler.compileGLSL ctx.FShadeBackend
                    )

                match ctx.TryCompileProgram(effect.Id, signature, shader) with
                | Success v -> v
                | Error e -> failwith e
            )
        else
            Unchecked.defaultof<Program>



    let infoSlot = culling.Buffers |> List.pick (fun (a,b,c) -> if b = "infos" then Some a else None)
    let boundSlot = culling.Buffers |> List.pick (fun (a,b,c) -> if b = "bounds" then Some a else None)
    let activeSlot = culling.Buffers |> List.pick (fun (a,b,c) -> if b = "isActive" then Some a else None)
    let viewProjSlot = culling.Buffers |> List.pick (fun (a,b,c) -> if b = "viewProjs" then Some a else None)
    let uniformBlock = culling.UniformBlocks |> List.head
    let countField = uniformBlock.ubFields |> List.find (fun f -> f.ufName = "cs_count")
             
    let boxBoundSlot = boxShader.Interface.storageBuffers |> Seq.pick (fun (KeyValue(a,b)) -> if a = "Bounds" then Some b.ssbBinding else None)
    let boxViewProjSlot = boxShader.Interface.storageBuffers |> Seq.pick (fun (KeyValue(a,b)) -> if a = "ViewProjs" then Some b.ssbBinding else None)

    let boxMode = NativePtr.allocArray [| GLBeginMode(int BeginMode.Lines, 2) |]
        
    let pCall =
        NativePtr.allocArray [|
            DrawCallInfo(
                FaceVertexCount = 24,
                InstanceCount = 0
            )
        |]

    let boxDraw = 
        NativePtr.allocArray [| new DrawCallInfoList(1, pCall) |]

    let resize (newCount : int) =
        let newCapacity = max initialCapacity (Fun.NextPowerOfTwo (max 1 newCount))
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
            dirty.Clear()
            dirty.UnionWith [0..count-1] 
                
            NativePtr.free om
            ctx.Delete ob
            if bounds then 
                NativePtr.free obm
                ctx.Delete obb
        
    member x.Count = count

    member x.Add(call : DrawCallInfo, box : Box3d, cellBounds : Box3d, rootId : int) =
        if call.FaceVertexCount <= 0 || call.InstanceCount <= 0 then
            Log.warn "[IndirectBuffer] adding empty DrawCall: %A" call
            true

        elif drawIndices.ContainsKey call then
            false

        elif count < capacity then
            let id = count
            drawIndices.[call] <- id
            NativePtr.set mem id (adjust call)
            if bounds then
                let bounds =
                    CullingShader.CullingInfo(
                        Min = V4f(V3f box.Min, float32 call.InstanceCount),
                        Max = V4f(V3f box.Max, float32 rootId),
                        CellMin = V4f(V3f cellBounds.Min, 0.0f),
                        CellMax = V4f(V3f cellBounds.Max, 0.0f)
                    )
                NativePtr.set bmem id bounds
            count <- count + 1
            let ess = if bounds then es + bs else es
            Interlocked.Add(&usedMemory.contents, int64 ess) |> ignore
            dirty.Add id |> ignore
                    
            updatePointers()
            true

        else
            resize (count + 1)
            x.Add(call, box, cellBounds, rootId)
                 
    member x.Contains (call : DrawCallInfo) =
        call.FaceVertexCount <= 0 || 
        call.InstanceCount <= 0 || 
        drawIndices.ContainsKey call

    member x.Remove(call : DrawCallInfo) =
        if call.FaceVertexCount <= 0 || call.InstanceCount <= 0 then
            Log.warn "[IndirectBuffer] removing empty DrawCall: %A" call
            true
        else
            match drawIndices.TryRemove call with
            | (true, oid) ->
                let last = count - 1
                count <- last
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
                    dirty.Add oid |> ignore
                
                dirty.Remove last |> ignore
                        
                resize count
                updatePointers()

                true
            | _ ->
                false
        
    member x.Flush() =
        
        let toUpload = dirty |> Seq.toArray
        dirty.Clear()

        let toUpload =
            toUpload |> Seq.choose (fun r ->
                if r < 0 then
                    Log.warn "bad dirty range: %A (count: %A)" r count
                    None
                elif r >= count then
                    Log.warn "bad dirty range: %A (count: %A)" r count
                    None
                else
                    Some r
            ) |> Seq.toArray

        if toUpload.Length > 0 then
            use __ = ctx.ResourceLock
            let ptr = ctx.MapBufferRange(buffer, 0n, nativeint (count * es), BufferAccessMask.MapWriteBit ||| BufferAccessMask.MapFlushExplicitBit)
            for r in toUpload do
                let o = r * es |> nativeint
                let s = es |> nativeint
                Marshal.Copy(NativePtr.toNativeInt mem + o, ptr + o, s)
                GL.FlushMappedNamedBufferRange(buffer.Handle, o, s)
            ctx.UnmapBuffer(buffer)

            if bounds then
                let bptr = ctx.MapBufferRange(bbuffer, 0n, nativeint (count * bs), BufferAccessMask.MapWriteBit ||| BufferAccessMask.MapFlushExplicitBit)
                for r in toUpload do
                    let o = r * bs |> nativeint
                    let s = bs |> nativeint
                    Marshal.Copy(NativePtr.toNativeInt bmem + o, bptr + o, s)
                    GL.FlushMappedNamedBufferRange(bbuffer.Handle, o, s)
                ctx.UnmapBuffer(bbuffer)


    member x.Buffer =
        Aardvark.Base.IndirectBuffer(buffer :> IBuffer, count, sizeof<DrawCallInfo>, false)

    member x.BoundsBuffer =
        bbuffer

       

    member x.CompileRender(s : ICommandStream, before : ICommandStream -> unit, mvp : nativeptr<M44f>, indexType : Option<_>, runtimeStats : nativeptr<_>, isActive : nativeptr<_>, mode : nativeptr<_>) : NativeStats =

        let mutable icnt = 0 // counting dynamic

        if bounds then
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
                
            s.Conditional(renderBounds, fun s ->
                let pCnt : nativeptr<int> = NativePtr.ofNativeInt (NativePtr.toNativeInt bufferHandles + 8n)
                let pInstanceCnt : nativeptr<int> = NativePtr.ofNativeInt (NativePtr.toNativeInt pCall + 4n)
                s.Copy(pCnt, pInstanceCnt)
                s.UseProgram(boxShader.Handle)
                s.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, boxBoundSlot, NativePtr.ofNativeInt (NativePtr.toNativeInt bufferHandles + 4n))
                s.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, boxViewProjSlot, modelViewProjs)
                s.DrawArrays(runtimeStats, isActive, boxMode, boxDraw)
            )

            s.UseProgram(oldProgram)
            s.BindBufferRange(BufferRangeTarget.UniformBuffer, uniformBlock.ubBinding, oldUB, oldUBOffset, oldUBSize)
            s.MemoryBarrier(MemoryBarrierFlags.CommandBarrierBit)

            
        let h = NativePtr.read indirectHandle
        if h.Count > 0 then
            before(s)
            if alphaToCoverage then 
                s.Enable(int EnableCap.SampleAlphaToCoverage)
                s.Enable(int EnableCap.SampleAlphaToOne)

            match indexType with
                | Some indexType ->
                    s.DrawElementsIndirect(runtimeStats, isActive, mode, int indexType, indirectHandle)
                | _ -> 
                    s.DrawArraysIndirect(runtimeStats, isActive, mode, indirectHandle)
            
            if alphaToCoverage then 
                s.Disable(int EnableCap.SampleAlphaToCoverage)
                s.Disable(int EnableCap.SampleAlphaToOne)
                icnt <- icnt + 4 // enable + disable
        else
            Log.warn "empty indirect call"

        NativeStats(InstructionCount = 16 + 5 + icnt) // 16 fixed + 5 conditional + (0 / 2)

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
        dirty.Clear()
        count <- 0
        NativePtr.free indirectHandle
        NativePtr.free computeSize
        NativePtr.free boxMode
        NativePtr.free pCall
        NativePtr.free boxDraw

        drawIndices.Clear()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

type PoolSlot (ctx : Context, signature : GeometryPoolSignature, ub : Block<InstanceBuffer>, vb : Block<VertexBuffer>, ib : Option<Block<Buffer>>) = 
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

    member x.Alloc(signature : GeometryPoolSignature, instanceCount : int, indexCount : int, vertexCount : int) =
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
        let signature = GeometryPoolSignature.ofGeometry signature uniforms geometry

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

type DrawPool(ctx : Context, alphaToCoverage : bool, bounds : bool, renderBounds : nativeptr<int>, activeBuffer : nativeptr<int>, modelViewProjs : nativeptr<int>, state : PreparedPipelineState, pass : RenderPass) as this =
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
            | Some (:? aval<Trafo3d> as mvp) -> mvp
            | _ -> 
                match s.pUniformProvider.TryGetUniform(Ag.Scope.Root, Symbol.Create "ModelViewProjTrafo") with
                | Some (:? aval<Trafo3d> as mvp) -> mvp
                | _ -> AVal.constant Trafo3d.Identity

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

    let compile (indexType : Option<DrawElementsType>, mode : nativeptr<GLBeginMode>, a : VertexInputBindingHandle, ib : IndirectBuffer) (s : ICommandStream) : NativeStats =
        let stats = NativeStats(InstructionCount = 1)
        s.BindVertexAttributes(contextHandle, a)
        stats + ib.CompileRender(s, this.BeforeRender, mvpResource.Pointer, indexType, runtimeStats, isActive, mode)

    let indirects = Dict<_, IndirectBuffer>()
    let isOutdated = NativePtr.allocArray [| 1 |]
    let updateFun = Marshal.PinDelegate(new System.Action(this.Update))
    let mutable oldCalls : list<Option<DrawElementsType> * nativeptr<GLBeginMode> * VertexInputBindingHandle * IndirectBuffer> = []
    let program = new ChangeableNativeProgram<_, _>((fun a s -> compile a (AssemblerCommandStream s)), NativeStats.Zero, (+), (-))
    let puller = 
        { new AdaptiveObject() with
            override x.MarkObject() =
                NativePtr.write isOutdated 1
                true
        }

    //let sub = puller.AddMarkingCallback (fun () -> NativePtr.write isOutdated 1)
    let tasks = System.Collections.Generic.HashSet<IRenderTask>()

    let mark() = transact (fun () -> puller.MarkOutdated())
        

    let getIndirectBuffer(slot : PoolSlot) =
        let key = getKey slot
        indirects.GetOrCreate(key, fun _ ->
            new IndirectBuffer(ctx, alphaToCoverage, renderBounds, state.pFramebufferSignature, bounds, activeBuffer, modelViewProjs, Option.isSome slot.IndexType, initialIndirectSize, usedMemory, totalMemory)
        )

    let tryGetIndirectBuffer(slot : PoolSlot) =
        let key = getKey slot
        match indirects.TryGetValue key with
            | (true, ib) -> Some ib
            | _ -> None
                
                
    member x.Add(ref : PoolSlot, bounds : Box3d, cellBounds : Box3d, rootId : int) =
        let ib = getIndirectBuffer ref
        if ib.Add(ref.DrawCallInfo, bounds, cellBounds, rootId) then
            mark()
            true
        else
            false

    member x.Add(ref : PoolSlot, rootId : int) =
        let ib = getIndirectBuffer ref
        if ib.Add(ref.DrawCallInfo, Unchecked.defaultof<Box3d>, Unchecked.defaultof<Box3d>, rootId) then
            mark()
            true
        else
            false

    member x.Contains(ref : PoolSlot) =
        match tryGetIndirectBuffer ref with
            | Some ib -> 
                ib.Contains ref.DrawCallInfo
            | None ->
                false

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
                

        )

    override x.Compile(info : CompilerInfo, stream : ICommandStream, last : Option<PreparedCommand>) =
        lock puller (fun () ->
            if tasks.Add info.task then
                assert (info.task.OutOfDate)
                puller.Outputs.Add(info.task) |> ignore
        )
            
        let mvpRes = mvpResource
        let lastState = last |> Option.bind (fun l -> l.ExitState)

        stream.ConditionalCall(isOutdated, updateFun.Pointer)
            
        stream.Copy(info.runtimeStats, runtimeStats)
        stream.Copy(info.contextHandle, contextHandle)
            
        let stats = stream.SetPipelineState(info, state, lastState)
            
        stream.QueryTimestamp(query, startTime)
        stream.CallIndirect(program.EntryPointer)
        stream.QueryTimestamp(query, endTime)

        stream.Copy(runtimeStats, info.runtimeStats)

        stats
        

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


[<StructuredFormatDisplay("AsString")>]
type GeometryPoolInstance =
    {
        signature       : GeometryPoolSignature
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

module GeometryPoolInstance =

    let inline signature (g : GeometryPoolInstance) = g.signature
    let inline instanceCount (g : GeometryPoolInstance) = g.instanceCount
    let inline indexCount (g : GeometryPoolInstance) = g.indexCount
    let inline vertexCount (g : GeometryPoolInstance) = g.vertexCount
    let inline geometry (g : GeometryPoolInstance) = g.geometry
    let inline uniforms (g : GeometryPoolInstance) = g.uniforms

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
            signature = GeometryPoolSignature.ofGeometry iface u g
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