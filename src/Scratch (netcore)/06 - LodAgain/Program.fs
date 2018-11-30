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

    //type IndexBuffer(ctx : Context, t : Type, count : int) =
        

    type VertexManager(ctx : Context, semantics : MapExt<string, Type>, chunkSize : int, totalMemory : ref<int64>) =

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


        let manager = new ChunkedMemoryManager<VertexBuffer>(mem, nativeint chunkSize)
        
        member x.Alloc(count : int) = manager.Alloc(nativeint count)
        member x.Free(b : Block<VertexBuffer>) = manager.Free b
        member x.Dispose() = manager.Dispose()
        interface IDisposable with member x.Dispose() = x.Dispose()
        
    type InstanceManager(ctx : Context, semantics : MapExt<string, GLSLType * Type>, chunkSize : int, totalMemory : ref<int64>) =
     
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
        
        member x.Alloc(count : int) = manager.Alloc(nativeint count)
        member x.Free(b : Block<InstanceBuffer>) = manager.Free b
        member x.Dispose() = manager.Dispose()
        interface IDisposable with member x.Dispose() = x.Dispose()

    type IndexManager(ctx : Context, chunkSize : int, totalMemory : ref<int64>) =

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
        
        member x.Alloc(t : Type, count : int) = manager.Alloc(nativeint (Marshal.SizeOf t) * nativeint count)
        member x.Free(b : Block<Buffer>) = manager.Free b
        member x.Dispose() = manager.Dispose()
        interface IDisposable with member x.Dispose() = x.Dispose()

    type IndirectBuffer(ctx : Context, indexed : bool, initialCapacity : int) =
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

        let resize (newCount : int) =
            let newCapacity = max initialCapacity (Fun.NextPowerOfTwo newCount)
            if newCapacity <> capacity then
                let ob = buffer
                let om = mem
                let nb = ctx.CreateBuffer (es * capacity)
                let nm = NativePtr.alloc (es * capacity)

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

        new(ctx : Context, indexed : bool) = new IndirectBuffer(ctx, indexed, 1024)
        
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

        member x.IndexType = indexType
        member x.Signature = signature
        member x.VertexBuffer = vb
        member x.InstanceBuffer = ub
        member x.IndexBuffer = ib

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
        
        let totalMemory = ref 0L
        let instanceManagers = System.Collections.Concurrent.ConcurrentDictionary<InstanceSignature, InstanceManager>()
        let vertexManagers = System.Collections.Concurrent.ConcurrentDictionary<VertexSignature, VertexManager>()

        let getVertexManager (signature : VertexSignature) = vertexManagers.GetOrAdd(signature, fun signature -> new VertexManager(ctx, signature, vertexChunkSize, totalMemory))
        let getInstanceManager (signature : InstanceSignature) = instanceManagers.GetOrAdd(signature, fun signature -> new InstanceManager(ctx, signature, instanceChunkSize, totalMemory))
        
        let indexManager = new IndexManager(ctx, vertexChunkSize, totalMemory)

        static member Get(ctx : Context) =
            pools.GetOrAdd(ctx, fun ctx ->
                new GeometryPool(ctx)
            )      
            
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

            PoolSlot(ctx, signature, ub, vb, ib)

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

    type DrawPool(ctx : Context, state : PreparedPipelineState, pass : RenderPass) as this =
        inherit PreparedCommand(ctx, pass)

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

        
        let compile (indexType : Option<DrawElementsType>, mode : nativeptr<GLBeginMode>, a : VertexInputBindingHandle, ib : nativeptr<V2i>) (s : IAssemblerStream) =
            s.BindVertexAttributes(contextHandle, a)
            match indexType with
                | Some indexType ->
                    s.DrawElementsIndirect(runtimeStats, isActive, mode, int indexType, ib)
                | _ -> 
                    s.DrawArraysIndirect(runtimeStats, isActive, mode, ib)
        
        let indirects = Dict<_, IndirectBuffer>()
        let isOutdated = NativePtr.allocArray [| 1 |]
        let myFun = Marshal.PinDelegate(System.Action(this.Update))
        let mutable oldCalls : list<Option<DrawElementsType> * nativeptr<GLBeginMode> * VertexInputBindingHandle * nativeptr<V2i>> = []
        let program = new ChangeableNativeProgram<_>(compile)
        let puller = AdaptiveObject()
        let sub = puller.AddMarkingCallback (fun () -> NativePtr.write isOutdated 1)
        let tasks = System.Collections.Generic.HashSet<IRenderTask>()

        let mark() = transact (fun () -> puller.MarkOutdated())

        let getIndirectBuffer(slot : PoolSlot) =
            let key = getKey slot
            indirects.GetOrCreate(key, fun _ ->
                new IndirectBuffer(ctx, Option.isSome slot.IndexType)
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

        abstract member Evaluate : AdaptiveToken -> unit
        default x.Evaluate _ = ()

        member x.Update() =
            puller.EvaluateAlways AdaptiveToken.Top (fun token ->
                puller.OutOfDate <- true
                x.Evaluate token

                let calls = 
                    Dict.toList indirects |> List.map (fun ((mode, ib, vb, typeAndIndex), db) ->
                        let indexType = typeAndIndex |> Option.map fst
                        let index = typeAndIndex |> Option.map snd
                        db.Flush()

                        let attributes = 
                            state.pProgramInterface.inputs |> List.map (fun param ->
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
            )

        override x.Compile(info : CompilerInfo, stream : IAssemblerStream, last : Option<PreparedCommand>) =
            lock puller (fun () ->
                if tasks.Add info.task then
                    assert (info.task.OutOfDate)
                    puller.AddOutput(info.task) |> ignore
            )

            stream.SetPipelineState(info, state)
            
            let label = stream.NewLabel()
            stream.Cmp(NativePtr.toNativeInt isOutdated, 0)
            stream.Jump(JumpCondition.Equal, label)
            stream.BeginCall(0)
            stream.Call(myFun.Pointer)
            stream.Mark(label)
            
            let taskStats = NativePtr.toNativeInt info.runtimeStats
            let localStats = NativePtr.toNativeInt runtimeStats

            let taskCtx = NativePtr.toNativeInt info.contextHandle
            let localCtx = NativePtr.toNativeInt contextHandle

            stream.Copy(taskStats, localStats, true)
            stream.Copy(taskCtx, localCtx, sizeof<nativeint> = 8)

            stream.BeginCall(0)
            stream.CallIndirect(program.EntryPointer)
            
            stream.Copy(localStats, taskStats, true)

        override x.Release() =
            state.Dispose()
            for ib in indirects.Values do ib.Dispose()
            indirects.Clear()
            myFun.Dispose()
            NativePtr.free isActive
            NativePtr.free isOutdated
            program.Dispose()
            //compilerInfo <- None
            oldCalls <- []

        override x.GetResources() = state.Resources
        override x.EntryState = Some state
        override x.ExitState = Some state

    type ManyGeomtries(ctx : Context, state : PreparedPipelineState, pass : RenderPass, geometries : aset<IndexedGeometry * MapExt<string, Array>>) =
        inherit PreparedCommand(ctx, pass)

        let pool = GeometryPool.Get(ctx)

        let sw = System.Diagnostics.Stopwatch.StartNew()
        let reader = geometries.GetReader()
        let cache = Dict<IndexedGeometry * MapExt<string, Array>, PoolSlot>()
        let inactive = Dict<IndexedGeometry * MapExt<string, Array>, MicroTime * PoolSlot>()


        let evaluate (draws : DrawPool) (token : AdaptiveToken) =
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
                                let slot = pool.Alloc(state.pProgramInterface, g, u)
                                cache.[(g,u)] <- slot
                                draws.Add slot

                    | Rem(_, (g,u)) ->
                        match cache.TryRemove((g,u)) with
                            | (true, slot) ->
                                draws.Remove(slot) |> ignore
                                inactive.[(g,u)] <- (sw.MicroTime, slot)
                                //pool.Free slot
                            | _ ->  
                                ()
            )

        let inner =
            { new DrawPool(ctx, state, pass) with
                override x.Evaluate(token : AdaptiveToken) = evaluate x token
            }

        override x.Compile(a,b,c) = inner.Compile(a,b,c)
        override x.GetResources() = inner.GetResources()
        override x.Release() = 
            inactive.Values |> Seq.iter (snd >> pool.Free)
            cache.Values |> Seq.iter pool.Free
            reader.Dispose()
            cache.Clear()
            inactive.Clear()
            inner.Dispose()

        override x.EntryState = inner.EntryState
        override x.ExitState = inner.ExitState

    

open Aardvark.Rendering.GL
open Aardvark.Application.Slim

[<EntryPoint>]
let main argv = 
    
    Ag.initialize()
    Aardvark.Init()

    use app = new OpenGlApplication(true, true)
    use win = app.CreateGameWindow(8)

    let box = Box3d(-V3d.III, V3d.III)
    let color = C4b.Red

    let runtime = app.Runtime
    let ctx = runtime.Context

    let view = 
        CameraView.lookAt V3d.III V3d.Zero V3d.OOI
            |> DefaultCameraController.control win.Mouse win.Keyboard win.Time
            |> Mod.map CameraView.viewTrafo
    let proj =
        win.Sizes
            |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y))
            |> Mod.map Frustum.projTrafo

    let uniforms =
        let cam = view |> Mod.map (fun v -> v.Backward.C3.XYZ)
        [
            "ModelTrafo", Mod.constant Trafo3d.Identity :> IMod
            "ViewTrafo", view :> IMod
            "ProjTrafo", proj :> IMod 
            "PointSize", Mod.constant 5.0 :> IMod
            "CameraLocation", cam :> IMod
            "LightLocation", cam :> IMod
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
                DefaultSurfaces.instanceTrafo |> FShade.Effect.ofFunction
                DefaultSurfaces.trafo |> FShade.Effect.ofFunction
                DefaultSurfaces.constantColor C4f.Red |> FShade.Effect.ofFunction
                DefaultSurfaces.simpleLighting |> FShade.Effect.ofFunction
            ]
        )

    let man = runtime.ResourceManager
    let preparedState =
        PreparedPipelineState.ofPipelineState win.FramebufferSignature man surface state

    //let pool    = Bla.GeometryPool.Get(ctx)
    //let draws   = new Bla.DrawPool(ctx, preparedState, RenderPass.main)

    let sphereGeometry      = IndexedGeometryPrimitives.solidPhiThetaSphere Sphere3d.Unit 32 C4b.Red
    let boxGeometry         = IndexedGeometryPrimitives.Box.solidBox Box3d.Unit C4b.Red |> IndexedGeometry.toNonIndexed
    let wireBoxGeometry     = IndexedGeometryPrimitives.Box.wireBox Box3d.Unit C4b.Red
    let donutGeometry       = IndexedGeometryPrimitives.solidTorus (Torus3d(V3d.Zero, V3d.OOI, 1.0, 0.05)) C4b.Red 32 32

    let boxUniforms =
        MapExt.ofList [
            "InstanceTrafo",    [| Trafo3d.Translation( 2.0,0.0,0.0); Trafo3d.Translation( 4.0,0.0,0.0); Trafo3d.Translation( 6.0,0.0,0.0) |] :> System.Array
            "InstanceTrafoInv", [| Trafo3d.Translation(-2.0,0.0,0.0); Trafo3d.Translation(-4.0,0.0,0.0); Trafo3d.Translation(-6.0,0.0,0.0) |] :> System.Array
        ]

    let donutUniforms =
        MapExt.ofList [
            "InstanceTrafo",    [| Trafo3d.Translation(0.0,0.0, 2.0); Trafo3d.Translation( 0.0,0.0,4.0)  |] :> System.Array
            "InstanceTrafoInv", [| Trafo3d.Translation(0.0,0.0,-2.0); Trafo3d.Translation( 0.0,0.0,-4.0) |] :> System.Array
        ]
        
    sphereGeometry.SingleAttributes <- SymDict.ofList [DefaultSemantic.InstanceTrafo, Trafo3d.Identity :> obj; DefaultSemantic.InstanceTrafoInv, Trafo3d.Identity :> obj] 
    wireBoxGeometry.SingleAttributes <- SymDict.ofList [DefaultSemantic.InstanceTrafo, Trafo3d.Translation(V3d(0,-2,0)) :> obj; DefaultSemantic.InstanceTrafoInv, Trafo3d.Translation(V3d(0,2,0)) :> obj] 
    //donutGeometry.SingleAttributes <- SymDict.ofList [DefaultSemantic.InstanceTrafo, Trafo3d.Translation(V3d(0,0,2)) :> obj; DefaultSemantic.InstanceTrafoInv, Trafo3d.Translation(V3d(0,0,-2)) :> obj] 
    
    let geometries =
        CSet.ofList [
            sphereGeometry, MapExt.empty
            wireBoxGeometry, MapExt.empty
            boxGeometry, boxUniforms
        ]


    let draws = new Bla.ManyGeomtries(ctx, preparedState, RenderPass.main, geometries)

    //let box = pool.Alloc(preparedState.pProgramInterface, boxGeometry, boxUniforms)
    //let sphere = pool.Alloc(preparedState.pProgramInterface, sphereGeometry)
    //let wireBox = pool.Alloc(preparedState.pProgramInterface, wireBoxGeometry)
    //let donut = pool.Alloc(preparedState.pProgramInterface, donutGeometry)
    
    //draws.Add box |> ignore
    //draws.Add sphere |> ignore
    //draws.Add wireBox |> ignore
    
    win.Keyboard.KeyDown(Keys.Enter).Values.Add(fun () ->
        transact (fun () -> geometries.Add (donutGeometry, donutUniforms) |> ignore)
    )
    win.Keyboard.KeyDown(Keys.Back).Values.Add(fun () ->
        transact (fun () -> geometries.Remove (donutGeometry, donutUniforms) |> ignore)
    )

    win.RenderTask <- app.Runtime.CompileRender(win.FramebufferSignature, ASet.ofList [draws :> IRenderObject])
    win.Run()

    0
