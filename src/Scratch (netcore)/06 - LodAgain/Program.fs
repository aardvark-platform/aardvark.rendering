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
            uniformTypes    : InstanceSignature
            attributeTypes  : VertexSignature
        }

    module GeometrySignature =
        let ofGeometry (iface : GLSLProgramInterface) (g : IndexedGeometry) =
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
                        match g.SingleAttributes.TryGetValue sym with
                            | (true, uniform) ->
                                assert(not (isNull uniform))
                                let t = uniform.GetType()
                                uniformTypes <- MapExt.add i.paramSemantic (i.paramType, t) uniformTypes
                            | _ ->
                                ()
              
            {
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
        
        member x.Upload(index : int, data : MapExt<string, obj>) =
            lock x (fun () ->
                use __ = ctx.ResourceLock
                buffers |> MapExt.iter (fun sem (buffer, elemSize, write) ->
                    let offset = nativeint index * nativeint elemSize
                    let ptr = ctx.MapBufferRange(buffer, offset, nativeint elemSize, BufferAccessMask.MapWriteBit)
                    match MapExt.tryFind sem data with
                        | Some data ->  write.WriteUnsafeValue(data, ptr)
                        | _ -> Marshal.Set(ptr, 0, elemSize)
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

    type IndirectBuffer(ctx : Context, initialCapacity : int) =
        static let es = sizeof<DrawCallInfo>
        
        let drawIndices = Dict<DrawCallInfo, int>()
        let mutable capacity = Fun.NextPowerOfTwo initialCapacity
        let mutable mem : nativeptr<DrawCallInfo> = NativePtr.alloc (es * capacity)
        let mutable buffer = ctx.CreateBuffer (es * capacity)
        let mutable dirty = RangeSet.empty
        let mutable count = 0

        let resize (newCount : int) =
            let newCapacity = max 1024 (Fun.NextPowerOfTwo newCount)
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
                    NativePtr.set mem id call
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

        new(ctx : Context) = new IndirectBuffer(ctx, 1024)



    type PoolSlot(ctx : Context, signature : GeometrySignature, ib : Block<InstanceBuffer>, vb : Block<VertexBuffer>) = 
        let fvc = int vb.Size
        
        member x.Signature = signature
        member x.VertexBuffer = vb
        member x.InstanceBuffer = ib

        member x.Upload(g : IndexedGeometry) =
            let instanceValues =
                signature.uniformTypes |> MapExt.choose (fun name _ ->
                    match g.SingleAttributes.TryGetValue(Symbol.Create name) with
                        | (true, v) -> Some v
                        | _ -> None
                )
            let vertexArrays =
                signature.attributeTypes |> MapExt.choose (fun name _ ->
                    match g.IndexedAttributes.TryGetValue(Symbol.Create name) with
                        | (true, v) -> Some v
                        | _ -> None
                )
            use __ = ctx.ResourceLock
            ib.Memory.Value.Upload(int ib.Offset, instanceValues)
            vb.Memory.Value.Write(int vb.Offset, vertexArrays)

        member x.DrawCallInfo =
            DrawCallInfo(
                FaceVertexCount = fvc,
                FirstIndex = int vb.Offset,
                InstanceCount = 1,
                FirstInstance = int ib.Offset
            )

    type GeometryPool private(ctx : Context) =
        static let instanceChunkSize = 1024
        static let vertexChunkSize = 1 <<< 20
        static let pools = System.Collections.Concurrent.ConcurrentDictionary<Context, GeometryPool>()
        
        let totalMemory = ref 0L
        let instanceManagers = System.Collections.Concurrent.ConcurrentDictionary<InstanceSignature, InstanceManager>()
        let vertexManagers = System.Collections.Concurrent.ConcurrentDictionary<VertexSignature, VertexManager>()

        let getVertexManager (signature : VertexSignature) = vertexManagers.GetOrAdd(signature, fun signature -> new VertexManager(ctx, signature, vertexChunkSize, totalMemory))
        let getInstanceManager (signature : InstanceSignature) = instanceManagers.GetOrAdd(signature, fun signature -> new InstanceManager(ctx, signature, vertexChunkSize, totalMemory))
        
        static member Get(ctx : Context) =
            pools.GetOrAdd(ctx, fun ctx ->
                new GeometryPool(ctx)
            )      
            
        member x.TotalMemory = Mem !totalMemory

        member x.Alloc(signature : GeometrySignature, vertexCount : int) =
            let vm = getVertexManager signature.attributeTypes
            let im = getInstanceManager signature.uniformTypes

            let ib = im.Alloc(1)
            let vb = vm.Alloc(vertexCount)
            PoolSlot(ctx, signature, ib, vb)

        member x.Free(slot : PoolSlot) =
            let signature = slot.Signature
            let vm = getVertexManager signature.attributeTypes
            let im = getInstanceManager signature.uniformTypes
            im.Free slot.InstanceBuffer
            vm.Free slot.VertexBuffer
  
    open Aardvark.Rendering.GL.Compiler
    open Aardvark.Rendering.GL.RenderTasks



    type DrawPool(ctx : Context, state : PreparedPipelineState) as this =
        let indirects = Dict<InstanceBuffer * VertexBuffer, IndirectBuffer>()

        let isActive = NativePtr.allocArray [| 1 |]
        let beginMode = NativePtr.allocArray [| GLBeginMode(int BeginMode.Points, 1) |]
        let isOutdated = NativePtr.allocArray [| 1 |]

        let myFun = Marshal.PinDelegate(System.Action(this.Update))

        // TODO
        let calls = MList.empty
        let program = NativeProgram.simple (fun _ _ -> ()) calls

        let getIndirectBuffer(slot : PoolSlot) =
            let key = slot.InstanceBuffer.Memory.Value, slot.VertexBuffer.Memory.Value
            indirects.GetOrCreate(key, fun _ ->
                new IndirectBuffer(ctx)
            )

        let tryGetIndirectBuffer(slot : PoolSlot) =
            let key = slot.InstanceBuffer.Memory.Value, slot.VertexBuffer.Memory.Value
            match indirects.TryGetValue key with
                | (true, ib) -> Some ib
                | _ -> None

        member x.Add(ref : PoolSlot) =
            let ib = getIndirectBuffer ref
            ib.Add ref.DrawCallInfo

        member x.Remove(ref : PoolSlot) =
            match tryGetIndirectBuffer ref with
                | Some ib -> 
                    if ib.Remove(ref.DrawCallInfo) then
                        if ib.Count = 0 then
                            let key = ref.InstanceBuffer.Memory.Value, ref.VertexBuffer.Memory.Value
                            indirects.Remove(key) |> ignore
                            ib.Dispose()
                        true
                    else
                        false
                | None ->
                    false

        member x.Update() =
            let things = 
                PList.ofList [
                    for ((ib, vb), db) in Dict.toSeq indirects do 
                        //VertexInputBinding()

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

                        let vbb = ctx.CreateVertexInputBinding(None, attributes)
                
                        let pdb : nativeptr<V2i> = NativePtr.alloc 1
                        NativePtr.write pdb (V2i(db.Buffer.Buffer.Handle, db.Count))
                        yield vbb, pdb
                ]

            transact (fun () -> calls.Update things)

            NativePtr.write isOutdated 0
            ()

        member x.Compile(info : CompilerInfo, stream : IAssemblerStream, last : Option<PreparedCommand>) =
            let amd = unbox<AMD64.AssemblerStream> stream
            stream.SetPipelineState(info, state)
            
            let label = amd.NewLabel()
            amd.Load(AMD64.Register.Rax, isOutdated)
            amd.Cmp(AMD64.Register.Rax, 0u)
            amd.Jump(JumpCondition.Equal, label)
            stream.BeginCall(0)
            stream.Call(myFun.Pointer)
            amd.Mark(label)
            
            stream.BeginCall(0)
            stream.CallIndirect(program.EntryPointer)
            

        //member x.Calls : array<nativeptr<V2i> * VertexInputBindingHandle> =
        //    indirects 
        //    |> Dict.toArray
        //    |> Array.map (fun ((ib,vb), db) ->
        //        let instanceBuffers = ib.Buffers
        //        let vertexBuffers = vb.Buffers
        //        let indirect = db.Buffer
                
        //        failwith ""
        //    )

        member x.Dispose() =
            for ib in indirects.Values do ib.Dispose()
            indirects.Clear()

        interface IDisposable with 
            member x.Dispose() = x.Dispose()


[<EntryPoint>]
let main argv = 
    
    Ag.initialize()
    Aardvark.Init()

    let win =
        window {
            backend Backend.Vulkan
            display Display.Mono
            debug false
            samples 8
        }

    let box = Box3d(-V3d.III, V3d.III)
    let color = C4b.Red

    let sg = 
        // create a red box with a simple shader
        Sg.box (Mod.constant color) (Mod.constant box)
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.simpleLighting
            }
    
    // show the window
    win.Scene <- sg
    win.Run()

    0
