namespace Aardvark.Rendering.Vulkan.Temp

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Sorting
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop
open FSharp.Data.Traceable
open FSharp.Data.Adaptive
open System.Collections.Generic
open System.Threading.Tasks

#nowarn "9"
// #nowarn "51"

// ===========================================================================================
// ResourceManager stuff
// ===========================================================================================

type PreparedPipelineState =
    {
        ppPipeline  : INativeResourceLocation<VkPipeline>
        ppLayout    : PipelineLayout
        ppUniforms  : IUniformProvider
    }

type PreparedGeometry =
    {
        pgOriginal      : Geometry
        pgDescriptors   : INativeResourceLocation<DescriptorSetBinding>
        pgAttributes    : INativeResourceLocation<VertexBufferBinding>
        pgIndex         : Option<INativeResourceLocation<IndexBufferBinding>>
        pgCall          : INativeResourceLocation<DrawCall>
        pgResources     : list<IResourceLocation>
    }

[<AbstractClass; Sealed; Extension>]
type ResourceManagerExtensions private() =
    [<Extension>]
    static member PreparePipelineState (this : ResourceManager, renderPass : RenderPass, surface : Aardvark.Rendering.Surface, state : PipelineState) =
        let layout, program = this.CreateShaderProgram(renderPass, surface, state.Mode)

        let inputs = 
            layout.PipelineInfo.pInputs |> List.map (fun p ->
                let name = Symbol.Create p.paramSemantic
                match Map.tryFind name state.VertexInputTypes with
                    | Some t -> (name, (false, t))
                    | None -> 
                        match Map.tryFind p.paramSemantic state.PerGeometryUniforms with
                            | Some t -> (name, (true, GLSLType.toType p.paramType))
                            | None -> failf "could not get shader input %A" name
            )
            |> Map.ofList

        let inputState =
            this.CreateVertexInputState(layout.PipelineInfo, AVal.constant (VertexInputState.ofTypes inputs))

        let inputAssembly =
            this.CreateInputAssemblyState(state.Mode, program)

        let rasterizerState =
            this.CreateRasterizerState(
                state.DepthState.Clamp, state.DepthState.Bias,
                state.RasterizerState.CullMode, state.RasterizerState.FrontFace, state.RasterizerState.FillMode
            )

        let colorBlendState =
            this.CreateColorBlendState(
                renderPass,
                state.BlendState.ColorWriteMask, state.BlendState.AttachmentWriteMask,
                state.BlendState.Mode, state.BlendState.AttachmentMode, state.BlendState.ConstantColor
            )

        let depthStencilState =
            this.CreateDepthStencilState(
                state.DepthState.Test, state.DepthState.WriteMask,
                state.StencilState.ModeFront, state.StencilState.WriteMaskFront,
                state.StencilState.ModeBack, state.StencilState.WriteMaskBack
            )

        let multisampleState =
            this.CreateMultisampleState(renderPass, state.RasterizerState.Multisample)

        let pipeline =
            this.CreatePipeline(
                program,
                renderPass,
                inputState,
                inputAssembly,
                rasterizerState,
                colorBlendState,
                depthStencilState,
                multisampleState
            )
        {
            ppPipeline  = pipeline
            ppLayout    = layout
            ppUniforms  = state.GlobalUniforms
        }
            
    [<Extension>]
    static member PrepareGeometry(this : ResourceManager, state : PreparedPipelineState, g : Geometry, uniforms : IUniformProvider) : PreparedGeometry =
        let resources = System.Collections.Generic.List<IResourceLocation>()

        let layout = state.ppLayout

        let descriptorSets, additionalResources = 
            this.CreateDescriptorSets(layout, UniformProvider.union uniforms state.ppUniforms)

        resources.AddRange additionalResources

        let vertexBuffers = 
            layout.PipelineInfo.pInputs 
                |> List.sortBy (fun i -> i.paramLocation) 
                |> List.map (fun i ->
                    let sem = Symbol.Create i.paramSemantic 
                    match Map.tryFind sem g.vertexAttributes with
                        | Some b ->
                            this.CreateBuffer(b), 0L
                        | None ->
                            failf "geometry does not have buffer %A" sem
                )

        let dsb = this.CreateDescriptorSetBinding(layout, descriptorSets)
        let vbb = this.CreateVertexBufferBinding(vertexBuffers)

        let isIndexed, ibo =
            match g.indices with
                | Some ib ->
                    let b = this.CreateIndexBuffer ib.Buffer
                    let ibb = this.CreateIndexBufferBinding(b, VkIndexType.ofType ib.ElementType)
                    resources.Add ibb
                    true, ibb |> Some
                | None ->
                    false, None

        let call = this.CreateDrawCall(isIndexed, g.call)

        resources.Add dsb
        resources.Add vbb
        resources.Add call



        {
            pgOriginal      = g
            pgDescriptors   = dsb
            pgAttributes    = vbb
            pgIndex         = ibo
            pgCall          = call
            pgResources     = CSharpList.toList resources
        }
        
// ===========================================================================================
// Command stuff
// ===========================================================================================

module private RuntimeCommands =
    [<AutoOpen>]
    module Helpers = 
        let consume (r : System.Collections.Generic.HashSet<'a>) =
            let arr = Aardvark.Base.HashSet.toArray r
            r.Clear()
            arr
            
        let consumeList (r : List<'a>) =
            let arr = CSharpList.toArray r
            r.Clear()
            arr

        let dispose (d : #IDisposable) =
            d.Dispose()

        module HashSetDelta =
            let iter (add : 'a -> unit) (rem : 'a -> unit) (set : HashSetDelta<'a>) =
                set |> Seq.iter (function Add(_,o) -> add o | _ -> ())
                set |> Seq.iter (function Rem(_,o) -> rem o | _ -> ())

        module IndexListDelta =
            let iter (set : Index -> 'a -> unit) (remove : Index -> unit) (deltas : IndexListDelta<'a>) =
                deltas |> IndexListDelta.toSeq |> Seq.iter (function (i,Set v) -> set i v | _ -> ())
                deltas |> IndexListDelta.toSeq |> Seq.iter (function (i,Remove) -> remove i | _ -> ())


        module Shader =
            open Microsoft.FSharp.Quotations
            open FShade.Imperative
            open FShade

            let withInstanceUniforms (set : Map<string, Type>) (shader : Shader) =
                let hasIndexedInput =
                    shader.shaderStage = ShaderStage.Geometry || shader.shaderStage = ShaderStage.TessControl || shader.shaderStage = ShaderStage.TessEval

                // replace all reads
                let shader = 
                    shader |> Shader.substituteReads (fun kind typ name index ->
                        match kind, index, Map.tryFind name set with
                            | ParameterKind.Uniform, None, Some desiredType ->
                                assert(desiredType = typ)
                                if hasIndexedInput then
                                    Expr.ReadInput(ParameterKind.Input, typ, name, Expr.Value(0)) |> Some
                                else
                                    Expr.ReadInput(ParameterKind.Input, typ, name) |> Some
                            | _ ->
                                None
                    )

                let shader =
                    shader |> Shader.substituteWrites (fun values ->
                        
                        let mutable values = values
                        for (name, typ) in Map.toSeq set do
                            if not (Map.containsKey name values) then
                                let value =
                                    if hasIndexedInput then Expr.ReadInput(ParameterKind.Input, typ, name, Expr.Value(0))
                                    else Expr.ReadInput(ParameterKind.Input, typ, name)

                                values <- Map.add name (ShaderOutputValue.ofValue value) values


                        Some (Expr.WriteOutputs values)
                    )

                let adjustParameter (name : string) (p : ParameterDescription) =
                    if Map.containsKey name set then { p with paramInterpolation = InterpolationMode.Flat }
                    else p

                // make all input-interpolation-modes flat
                let shader =
                    { shader with 
                        shaderInputs = shader.shaderInputs |> Map.map adjustParameter
                        shaderOutputs = shader.shaderOutputs |> Map.map adjustParameter
                    }

                shader

        module Effect =
            open Microsoft.FSharp.Quotations
            open FShade.Imperative
            open FShade

            let withInstanceUniforms (set : Map<string, Type>) (effect : Effect) =
                let mapping = 
                    effect.Uniforms |> Map.choose (fun name desc ->
                        match Map.tryFind name set with
                            | Some _ -> Some desc.uniformType
                            | _ -> None
                    )

                //let id = effect.Id + "Instanced" + (String.concat "_" set)
                let effect = effect.Shaders |> Map.map (fun _ s -> Shader.withInstanceUniforms mapping s) |> Effect.ofMap
                effect


        open Management

        type BufferRange internal(buffer : Buffer, offset : int64, size : int64) =
            member x.Buffer = buffer
            member x.Offset = offset
            member x.Size = size

            interface IBufferRange with
                member x.Buffer = buffer :> _
                member x.Offset = nativeint offset
                member x.Size = nativeint size

        type UniformWriter(offset : nativeint, target : Map<string, int * IAdaptiveValue * UniformWriters.IWriter>) =
            inherit AdaptiveObject()

            member x.Write(token : AdaptiveToken, ptr : Map<string, nativeint>) =
                x.EvaluateIfNeeded token () (fun token ->
                    for (name,(es,m,w)) in Map.toSeq target do
                        match Map.tryFind name ptr with
                            | Some ptr -> 
                                w.Write(token, m, ptr + offset * nativeint es)
                            | _ -> ()
                )

        type InstanceBufferSlot internal(manager : MemoryManager<_>, values : Map<string, int * IAdaptiveValue * UniformWriters.IWriter>[], dirtySet : HashSet<UniformWriter>, parent : IAdaptiveObject) =
            
            let mutable block = manager.Alloc(nativeint values.Length)
            let writers = 
                values |> Array.mapi (fun i values ->
                    UniformWriter(block.Offset + nativeint i, values)
                )
            do
                lock dirtySet (fun () -> 
                    for w in writers do
                        dirtySet.Add w |> ignore
                )


            member x.Dispose() =
                manager.Free block
                lock dirtySet (fun () -> 
                    for w in writers do 
                        w.Outputs.Clear()
                        dirtySet.Remove w |> ignore
                )
                block <- Block<_>(manager, nref Unchecked.defaultof<_>, 0n, 0n, false)

            member x.FirstInstance = 
                int block.Offset

            member x.InstanceCount =
                int block.Size

            interface IDisposable with
                member x.Dispose() = x.Dispose()

        type GeometrySlot internal(device : Device, vertexSize : int, vertexBlock : Block<_>, vertexBuffers : Map<Symbol, BufferRange>) =
            let totalVertexSize = int64 vertexBlock.Size * int64 vertexSize

            member x.BlockId = snd vertexBlock.Memory.Value
            member x.VertexBuffers = vertexBuffers

            member x.FirstVertex = int vertexBlock.Offset
            member x.VertexCount = int vertexBlock.Size

            member internal x.VertexBlock = vertexBlock

            member x.WriteVertexAttributes(geometry : IndexedGeometry) =
                let temp = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferSrcBit totalVertexSize

                let copies = List<CopyCommand>()

                temp.Memory.Mapped (fun ptr ->
                    let mutable ptr = ptr
                    let mutable offset = 0L
                    for (name, target) in Map.toSeq vertexBuffers do
                        let size = target.Size
                        match geometry.IndexedAttributes.TryGetValue name with
                            | (true, src) ->
                                let gc = GCHandle.Alloc(src, GCHandleType.Pinned)
                                try Marshal.Copy(gc.AddrOfPinnedObject(), ptr, size)
                                finally gc.Free()

                                copies.Add(CopyCommand.Copy(temp, offset, target.Buffer, target.Offset, target.Size))

                                ptr <- ptr + nativeint size
                                offset <- offset + size

                            | _ ->
                                failf "no attribute %A" name

                )

                //let tcs = System.Threading.Tasks.TaskCompletionSource()
                copies.Add(CopyCommand.Callback (fun () -> Buffer.delete temp device))
                device.CopyEngine.Enqueue copies




        type ResizableIndirectBuffer(device : Device, initialCount : int) =
            
            let mutable count = 0
            let mutable capacity = initialCount

            let mutable ids = Marshal.AllocHGlobal (4n * nativeint initialCount)
            let mutable cpuBuffer = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferSrcBit (20L * int64 capacity)
            let mutable gpuBuffer = device.DeviceMemory |> Buffer.create (VkBufferUsageFlags.IndirectBufferBit ||| VkBufferUsageFlags.TransferDstBit) (20L * int64 capacity)
            let mutable gpuBufferDirty = false

            let dead = List<Buffer>()

            let resize (newCapacity : int) =
                let newCapacity = max newCapacity initialCount
                if capacity <> newCapacity then
                    let copySize = min capacity newCapacity

                    let newCpuBuffer = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferSrcBit (20L * int64 newCapacity)
                    let newGpuBuffer = device.DeviceMemory |> Buffer.create (VkBufferUsageFlags.IndirectBufferBit ||| VkBufferUsageFlags.TransferDstBit) (20L * int64 newCapacity)

                    cpuBuffer.Memory.Mapped(fun src ->
                        newCpuBuffer.Memory.Mapped (fun dst ->
                            Marshal.Copy(src, dst, nativeint copySize * 20n)
                        )
                    )

                    ids <- Marshal.ReAllocHGlobal(ids, 4n * nativeint newCapacity)
                    Buffer.delete cpuBuffer device
                    dead.Add gpuBuffer

                    cpuBuffer <- newCpuBuffer
                    gpuBuffer <- newGpuBuffer
                    gpuBufferDirty <- true
                    capacity <- newCapacity

            let indices = IntDict<int>()

            let mutable currentId = 0

            member x.Add(call : DrawCallInfo) : IDisposable =
                if count < capacity then
                    let cid = Interlocked.Increment(&currentId)

                    let location = count
                    count <- count + 1
                    cpuBuffer.Memory.Mapped (fun ptr ->
                        NativeInt.write (ptr + nativeint location * 20n) call
                    )
                    NativeInt.write (ids + nativeint location * 4n) cid

                    gpuBufferDirty <- true
                    indices.[cid] <- location

                    { new IDisposable with
                        member x.Dispose() =
                            match indices.TryRemove cid with
                                | (true, location) ->
                                    let last = count - 1
                                    if last <> location then
                                        cpuBuffer.Memory.Mapped (fun ptr ->
                                            let cid : int = NativeInt.read (ids + nativeint last * 4n)
                                            let last : DrawCallInfo = NativeInt.read (ptr + nativeint last * 20n)

                                            indices.[cid] <- location
                                            NativeInt.write (ptr + nativeint location * 20n) last
                                            NativeInt.write (ids + nativeint location * 4n) cid
                                        )   
                                        gpuBufferDirty <- true
                                
                                    count <- last
                                    resize (Fun.NextPowerOfTwo count)
                                | _ ->
                                    Log.error "could not remove call"
                    }

                else
                    resize (Fun.NextPowerOfTwo (count + 1))
                    x.Add call

            member x.Upload() =
                for d in consumeList dead do Buffer.delete d device

                if gpuBufferDirty then
                    device.perform {
                        do! Command.Copy(cpuBuffer, 0L, gpuBuffer, 0L, 20L * int64 count)
                    }
                    gpuBufferDirty <- false

            member x.Handle = gpuBuffer.Handle
            member x.Count = count
            member x.Item 
                with get v : DrawCallInfo = cpuBuffer.Memory.Mapped (fun ptr -> NativeInt.read (ptr + 20n * nativeint v))

            member x.Dispose() =
                for d in consumeList dead do Buffer.delete d device
                Buffer.delete cpuBuffer device
                Buffer.delete gpuBuffer device
                Marshal.FreeHGlobal ids
                capacity <- 0
                
            interface IDisposable with
                member x.Dispose() = x.Dispose()

        let private noOwner =
            { new IResourceCache with
                member x.AddLocked _ = ()
                member x.RemoveLocked _ = ()
                member x.Remove _ = ()
            }

        let vulkanHostMem (elementSizes : Map<string, int>) (dead : List<Buffer>) (device : Device) =
            let malloc (size : nativeint) =
                elementSizes |> Map.map (fun _ es ->
                    device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferSrcBit (int64 size * int64 es)
                )

            let mfree (buffer : Map<string, Buffer>) (size : nativeint) =
                lock dead (fun () -> buffer |> Map.iter (fun _ b -> dead.Add b))
                //buffer |> Map.iter (fun _ buffer -> Buffer.delete buffer device)

            let mcopy (src : Map<string, Buffer>) (srcOffset : nativeint) (dst : Map<string, Buffer>) (dstOffset : nativeint) (size : nativeint) =
                for (name, es) in Map.toSeq elementSizes do
                    let src = src.[name]
                    let dst = dst.[name]
                    src.Memory.Mapped(fun src ->
                        dst.Memory.Mapped(fun dst ->
                            Marshal.Copy(src + nativeint es * srcOffset, dst + nativeint es * dstOffset, nativeint es * size)
                        )
                    )

            let mrealloc (o : Map<string, Buffer>) (oSize : nativeint) (nSize : nativeint) =
                let n = malloc nSize
                mcopy o 0n n 0n (min oSize nSize)
                mfree o oSize
                n
                
            {
                malloc = malloc
                mfree = mfree
                mrealloc = mrealloc
                mcopy = mcopy
            }


        type InstanceBufferPool(device : Device, types : Map<string, FShade.GLSL.GLSLParameter * Type>, initialCapacity : int) =
            inherit AbstractResourceLocation<unit>(noOwner, [])

            let elementSizes =
                types |> Map.map (fun name (i,_) ->
                    GLSLType.sizeof i.paramType
                )


            let writers =
                types |> Map.map (fun name (input, typ) ->
                    let size = GLSLType.sizeof input.paramType
                    let writer = UniformWriters.getWriter 0 input.paramType typ
                    writer
                )


            let elementSize = 
                types 
                    |> Map.toSeq 
                    |> Seq.sumBy (fun (_,(i,_)) -> GLSLType.sizeof i.paramType) 
                    |> int64

            let dead = List<Buffer>()

            let manager = new Management.MemoryManager<_>(vulkanHostMem elementSizes dead device, nativeint initialCapacity)


            let mutable bufferCapacity = 0
            let mutable buffer : Map<string, Buffer> = Map.empty
            let getBuffer() =
                if manager.Capactiy <> nativeint bufferCapacity then
                    let newCapacity = manager.Capactiy

                    let newBuffer = 
                        elementSizes |> Map.map (fun _ es ->
                            Buffer.create 
                                (VkBufferUsageFlags.VertexBufferBit ||| VkBufferUsageFlags.TransferDstBit) 
                                (int64 newCapacity * int64 es)
                                device.DeviceMemory
                        )

                    lock dead (fun () -> buffer |> Map.iter (constF dead.Add))
                    buffer <- newBuffer
                    bufferCapacity <- int newCapacity
                    newBuffer
                else
                    buffer

            let dirty = HashSet<UniformWriter>()


            let map (f : Map<string, nativeint> -> 'r) (buffers : Map<string, Buffer>) =
                let rec map (ptrs : Map<string, nativeint>) (bs : list<string * Buffer>) =
                    match bs with
                        | [] -> f ptrs
                        | (name, h) :: rest ->
                            h.Memory.Mapped(fun ptr ->
                                map (Map.add name ptr ptrs) rest
                            )
                map Map.empty (Map.toList buffers)

            override x.InputChangedObject(t,o) =
                base.InputChangedObject(t,o)
                match o with
                    | :? UniformWriter as w -> lock dirty (fun () -> dirty.Add w |> ignore)
                    | _ -> ()

            member x.NewSlot(values : Map<string, IAdaptiveValue>) =
                let values =
                    writers |> Map.map (fun name writer ->
                        match Map.tryFind name values with
                            | Some value -> 
                                let es = elementSizes |> Map.find name
                                (es, value, writer)
                            | None -> failf "no uniform: %A" name
                    )

                new InstanceBufferSlot(manager, [|values|], dirty, x)

            member x.Buffer = getBuffer()


            member x.Kill() =
                manager.Dispose()
                for d in lock dead (fun () -> consumeList dead) do Buffer.delete d device

            override x.Create() =
                ()

            override x.Destroy() = 
                buffer |> Map.iter (fun _ b -> Buffer.delete b device)
                buffer <- Map.empty

            override x.GetHandle(token : AdaptiveToken) =
                use t = device.Token
                for d in lock dead (fun () -> consumeList dead) do Buffer.delete d device

                let buffer = getBuffer()

                // write all uniforms into manager.UnsafePointer
                let dirty = lock dirty (fun () -> consume dirty)
                manager.Use (fun ptr ->
                    if dirty.Length > 0 then
                        ptr |> map (fun ptrs ->
                            for d in dirty do d.Write(token, ptrs)
                        )

                    assert (not (Map.isEmpty buffer))

                    t.enqueue {
                        for (name, es) in Map.toSeq elementSizes do
                            let buffer = buffer.[name]
                            let host = ptr.[name]
                            do! Command.Copy(host, 0L, buffer, 0L, min host.Size buffer.Size)
                    }

                )

                { handle = (); version = 0}

        type BufferMemoryManager(device : Device, vertexTypes : Map<Symbol, FShade.GLSL.GLSLParameter * Type>, vertexCount : int) =
            
            let freeBuffers = System.Collections.Generic.List<Buffer>()
            let freeSlots = System.Collections.Generic.List<GeometrySlot>()
            let vertexBuffers = Dict<int, Map<Symbol, Buffer * int>>()
  
            let vertexSize =
                vertexTypes |> Map.toSeq |> Seq.sumBy (fun (_,(_,t)) -> Marshal.SizeOf t)


            let vertexManager = 
                let mutable currentId = 0
                let mem =
                    {
                        malloc = fun size ->
                            let id = Interlocked.Increment(&currentId)
                            let buffers = 
                                vertexTypes |> Map.map (fun name (_,t) ->
                                    let elementSize = Marshal.SizeOf t
                                    let sizeInBytes = int64 elementSize * int64 vertexCount
                                    let buffer = device.DeviceMemory |> Buffer.createConcurrent true (VkBufferUsageFlags.TransferDstBit ||| VkBufferUsageFlags.VertexBufferBit) sizeInBytes
                                    (buffer, elementSize)
                                )
                            vertexBuffers.[id] <- buffers
                            (buffers, id)
                        mfree = fun (map,id) _ ->
                            lock freeBuffers (fun () -> 
                                map |> Map.iter (fun _ (b,_) -> freeBuffers.Add b)
                                vertexBuffers.Remove id |> ignore
                            )

                        mcopy = fun _ _ _ _ _ -> failf "cannot copy"
                        mrealloc = fun _ _ _ -> failf "cannot realloc"
                    }
                
                new Management.ChunkedMemoryManager<_>(mem, nativeint vertexCount)

            member x.Alloc(vertexCount : int) =
                let vertexBlock = vertexManager.Alloc(nativeint vertexCount)
                let vertexBuffers = 
                    let buffers,_ = vertexBlock.Memory.Value
                    buffers |> Map.map (fun name (b, elementSize) ->
                        let offset = int64 vertexBlock.Offset * int64 elementSize
                        let size = int64 vertexBlock.Size * int64 elementSize
                        BufferRange(b, offset, size)
                    )

                    
                let geometrySlot = GeometrySlot(device, vertexSize, vertexBlock, vertexBuffers)
                geometrySlot

            member x.Free(r : GeometrySlot) =
                lock freeSlots (fun () -> freeSlots.Add r)

            member x.Flush() =
                let freeSlots = lock freeSlots (fun () -> consumeList freeSlots)
                for r in freeSlots do
                    vertexManager.Free(r.VertexBlock)
                    
                let freeBuffers = lock freeBuffers (fun () -> consumeList freeBuffers)
                for b in freeBuffers do
                    Buffer.delete b device

            member x.AllVertexBuffers =
                vertexBuffers |> Dict.toList

            member x.Dispose() =
                vertexManager.Dispose()
                for b in freeBuffers do Buffer.delete b device
                freeBuffers.Clear()
                freeSlots.Clear()
                vertexBuffers.Clear()

            interface IDisposable with
                member x.Dispose() = x.Dispose()

        let startThread (name : string) (run : unit -> unit) =
            let thread = Thread(ThreadStart(run), IsBackground = true, Name = name)
            thread.Start()
            thread

    [<AbstractClass>]
    type PreparedCommand() =
        inherit AdaptiveObject()

        let id = newId()
        let stream = new VKVM.CommandStream()

        let mutable prev : Option<PreparedCommand> = None
        let mutable next : Option<PreparedCommand> = None

        let mutable isDisposed = false

        let check () = if isDisposed then failf "using disposed command"

        member x.Prev
            with get() = check(); prev
            and set p = check(); prev <- p

        member x.Next 
            with get() = check(); next
            and set n =
                check(); 
                next <- n
                match n with
                    | Some n ->
                        stream.Next <- Some n.Stream
                    | None ->
                        stream.Next <- None

        interface ILinked<PreparedCommand> with
            member x.Prev 
                with get() = x.Prev
                and set p = x.Prev <- p

            member x.Next 
                with get() = x.Next
                and set n = x.Next <- n

        abstract member GroupKey : list<obj>
        abstract member Compile : AdaptiveToken * VKVM.CommandStream -> unit
        abstract member Free : unit -> unit

        default x.GroupKey = [id :> obj]

        member x.Id = id
        member x.Stream = check(); stream

        member x.Update(t : AdaptiveToken) =
            check()
            x.EvaluateIfNeeded t () (fun t ->
                x.Compile(t, stream)
            ) 

        member x.Dispose() =
            check()
            isDisposed <- true
            x.Free()
            stream.Dispose()
            prev <- None
            next <- None

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    /// Doing nothing
    and EmptyCommand() =
        inherit PreparedCommand()

        override x.Compile(_,_) = ()
        override x.Free() = ()

    /// Rendering a single IRenderObject
    and RenderObjectCommand(compiler : Compiler, o : IRenderObject) =
        inherit PreparedCommand()

        let mutable prepared : Option<PreparedMultiRenderObject> = None

        let boundingBox =
            lazy (
                match o.AttributeScope.TryGetSynthesized<aval<Box3d>>("GlobalBoundingBox") with
                | Some b -> b
                | _ -> 
                    Log.warn "[Vulkan] no bounding box for Object %A" o.AttributeScope
                    AVal.constant Box3d.Unit
            )

        static let rec hook (task : AbstractRenderTask) (o : IRenderObject) =
            match o with
                | :? RenderObject as o -> task.HookRenderObject(o) :> IRenderObject
                | :? MultiRenderObject as o -> o.Children |> List.map (hook task) |> MultiRenderObject :> IRenderObject
                | o -> o

        member x.Uniforms =
            let rec get (o : IRenderObject) =
                match o with
                    | :? RenderObject as o -> o.Uniforms
                    | :? MultiRenderObject as o -> get (List.head o.Children)
                    | :? IPreparedRenderObject as o -> 
                        match o.Original with
                            | Some o -> get o
                            | None -> failwithf "[Vulkan] no uniforms for object: %A" o
                    | _ ->
                        failwithf "[Vulkan] no uniforms for object: %A" o
            get o

        member x.BoundingBox = boundingBox.Value

        override x.GroupKey =
            match prepared with
                | Some p -> [ p.First.pipeline :> obj; p.Id :> obj ]
                | None -> failwith "inconsistent state"

        override x.Free() =
            match prepared with
                | Some o -> 
                    for o in o.Children do
                        for r in o.resources do compiler.resources.Remove r   
                            
                    prepared <- None
                | None ->
                    ()     

        override x.Compile(_, stream) =
            // never gets re-executed so the stream does not need to be cleared
            
            let o = compiler.manager.PrepareRenderObject(compiler.renderPass, hook compiler.task o)
            for o in o.Children do
                for r in o.resources do compiler.resources.Add r        

                stream.IndirectBindPipeline(o.pipeline.Pointer) |> ignore
                stream.IndirectBindDescriptorSets(o.descriptorSets.Pointer) |> ignore

                match o.indexBuffer with
                    | Some ib ->
                        stream.IndirectBindIndexBuffer(ib.Pointer) |> ignore
                    | None ->
                        ()

                stream.IndirectBindVertexBuffers(o.vertexBuffers.Pointer) |> ignore
                stream.IndirectDraw(compiler.stats, o.isActive.Pointer, o.drawCalls.Pointer) |> ignore

            prepared <- Some o

    and [<AbstractClass>] CommandBucket() =
        inherit AdaptiveObject()

        
        let mutable prev : Option<CommandBucket> = None
        let mutable next : Option<CommandBucket> = None

        member x.Prev
            with get() = prev
            and set v = 
                prev <- v
                let first = x.First
                let prev = prev |> Option.map (fun p -> p.Last)
                first.Prev <- prev
                match prev with
                    | Some p -> p.Next <- Some first
                    | None -> ()
                        
        member x.Next
            with get() = next
            and set v = 
                next <- v
                let last = x.Last
                let next = next |> Option.map (fun n -> n.First)
                last.Next <- next
                match next with
                    | Some n -> n.Prev <- Some last
                    | None -> ()



        abstract member First : PreparedCommand
        abstract member Last : PreparedCommand
        
        abstract member Add : PreparedCommand -> unit
        abstract member Remove : PreparedCommand -> bool

        abstract member Release : unit -> unit

        abstract member PerformUpdate : AdaptiveToken -> unit
        member x.Update(token : AdaptiveToken) =
            x.EvaluateAlways token (fun t ->
                x.PerformUpdate t
            )

        member x.Dispose() =
            x.Release()

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    and UnorderedCommandBucket() =
        inherit CommandBucket()

        let firstCommand =
            { new PreparedCommand() with
                override x.Free() = ()
                override x.Compile(_,_) = ()
            }
        
        let trie = OrderMaintenanceTrie<obj,PreparedCommand>()
        do trie.Set([], firstCommand) |> ignore

        override x.First = trie.First.Value.Value
        override x.Last = trie.Last.Value.Value

        override x.Add(cmd : PreparedCommand) =
            let o : list<obj> = cmd.GroupKey
            let ref = trie.Set(cmd.GroupKey, cmd)
            match ref.Prev with
            | ValueNone -> ()
            | ValueSome v -> 
                v.Value.Next <- Some cmd
                cmd.Prev <- Some v.Value
            match ref.Next with
            | ValueNone -> ()
            | ValueSome next -> 
                next.Value.Prev <- Some cmd
                cmd.Next <- Some next.Value

            match trie.Last with
            | ValueNone -> failwith "[Vulkan] empty CommandBucket"
            | ValueSome l -> 
                let next = x.Next |> Option.map (fun n -> n.First)
                l.Value.Next <- next
                match next with
                | None -> ()
                | Some n -> n.Prev <- Some l.Value

        override x.Remove(cmd : PreparedCommand) =
            let res = trie.TryRemove(cmd.GroupKey)
            match res with
            | ValueNone -> false
            | ValueSome (prev,next) -> 
                match prev,next with
                | ValueSome prev, ValueSome next -> 
                    prev.Value.Next <- Some next.Value
                    next.Value.Prev <- Some prev.Value
                | ValueSome last, ValueNone  -> 
                    let next = x.Next |> Option.map (fun n -> n.First)
                    last.Value.Next <- next
                    match next with
                    | None -> ()
                    | Some n -> n.Prev <- Some last.Value
                | ValueNone, ValueSome next ->
                    firstCommand.Next <- Some next.Value
                    next.Value.Prev <- Some firstCommand
                | _ -> ()
                true


        override x.Release() =
            firstCommand.Dispose()
            trie.Clear()

        override x.PerformUpdate token = 
            
            ()

    and SortedCommandBucket(order : RenderPassOrder) =
        inherit CommandBucket()
        
        let commands = Dict<RenderObjectCommand, aval<float>>()

        let firstCommand =
            { new PreparedCommand() with
                override x.Free() = ()
                override x.Compile(_,_) = ()
            }

        let lastCommand =
            { new PreparedCommand() with
                override x.Free() = ()
                override x.Compile(_,_) = ()
            }

        do firstCommand.Next <- Some lastCommand
           lastCommand.Prev <- Some firstCommand

        override x.First = firstCommand
        override x.Last = lastCommand

        override x.Add(cmd : PreparedCommand) =
            let cmd = unbox<RenderObjectCommand> cmd
            let depth = 
                let u = cmd.Uniforms
                    
                let box = cmd.BoundingBox
                let view = u.TryGetUniform(Ag.Scope.Root, Symbol.Create "ViewTrafo")
                let proj = u.TryGetUniform(Ag.Scope.Root, Symbol.Create "ProjTrafo")
                match view, proj with
                    | Some (:? aval<Trafo3d> as v), Some (:? aval<Trafo3d> as p) ->
                        AVal.custom (fun t ->
                            let v = v.GetValue t
                            let p = p.GetValue t
                            let box = box.GetValue t
                            let pp = box.Center |> v.Forward.TransformPos |> p.Forward.TransformPosProj
                            pp.Z
                        )

                    | Some (:? aval<Trafo3d[]> as v), Some (:? aval<Trafo3d> as p) ->
                        AVal.custom (fun t ->
                            let v = v.GetValue t
                            let p = p.GetValue t
                            let box = box.GetValue t
                            let pp = Array.map (fun (v : Trafo3d) -> box.Center |> v.Forward.TransformPos |> p.Forward.TransformPosProj) v
                            pp |> Seq.map (fun pp -> pp.Z) |> Seq.min
                        )
                        
                    | Some (:? aval<Trafo3d> as v), Some (:? aval<Trafo3d[]> as p) ->
                        AVal.custom (fun t ->
                            let v = v.GetValue t
                            let p = p.GetValue t
                            let box = box.GetValue t
                            let pp = Array.map (fun (p : Trafo3d) -> box.Center |> v.Forward.TransformPos |> p.Forward.TransformPosProj) p
                            pp |> Seq.map (fun pp -> pp.Z) |> Seq.min
                        )

                    | Some (:? aval<Trafo3d[]> as v), Some (:? aval<Trafo3d[]> as p) ->
                        AVal.custom (fun t ->
                            let v = v.GetValue t
                            let p = p.GetValue t
                            let box = box.GetValue t
                            let pp = Array.map2 (fun (v : Trafo3d) (p : Trafo3d) -> box.Center |> v.Forward.TransformPos |> p.Forward.TransformPosProj) v p
                            pp |> Seq.map (fun pp -> pp.Z) |> Seq.min
                        )

                    | _ ->
                        failwithf "[Vulkan] no ViewProjTrafo for object"

            commands.Add(cmd, depth)

        override x.Remove(cmd : PreparedCommand) =
            let cmd = unbox<RenderObjectCommand> cmd
            commands.Remove cmd

        override x.Release() =
            firstCommand.Dispose()
            lastCommand.Dispose()
            commands.Clear()

            
        override x.PerformUpdate token = 
            let boxes = commands |> Dict.toArray |> Array.map (fun (k,v) -> k, v.GetValue token)

            match order with
                | RenderPassOrder.FrontToBack -> boxes.QuickSortAscending snd
                | _ -> boxes.QuickSortDescending snd

            let mutable p = firstCommand

            for (c,_) in boxes do
                p.Next <- Some (c :> PreparedCommand)
                c.Prev <- Some p
                p <- c

            p.Next <- Some lastCommand
            lastCommand.Prev <- Some p


    /// Rendering a set of IRenderObjects using an optimized order (grouped by Pipeline)
    and UnorderedRenderObjectCommand(compiler : Compiler, objects : aset<IRenderObject>) =
        inherit PreparedCommand()

        let mutable reader = objects.GetReader()



        let trie = SortedDictionaryExt<Aardvark.Rendering.RenderPass, CommandBucket>(Comparer<Aardvark.Rendering.RenderPass>.Default)
        let mutable firstBucket : Option<CommandBucket> = None
        let mutable lastBucket : Option<CommandBucket> = None


        let getBucket(pass : Aardvark.Rendering.RenderPass) =
            trie |> SortedDictionary.setWithNeighbours pass (fun l s r ->
                match s with
                    | Some s -> s
                    | None ->
                        let bucket = 
                            match pass.Order with
                                | RenderPassOrder.Arbitrary -> 
                                    new UnorderedCommandBucket() :> CommandBucket
                                | o -> 
                                    Log.warn "[Vulkan] renderpass order %A not implemented" o
                                    new UnorderedCommandBucket() :> CommandBucket

                        let prev = l |> Option.map snd
                        let next = r |> Option.map snd

                        bucket.Prev <- prev
                        match prev with
                            | Some p -> p.Next <- Some bucket
                            | None -> firstBucket <- Some bucket

                        bucket.Next <- next
                        match next with
                            | Some n -> n.Prev <- Some bucket
                            | None -> lastBucket <- Some bucket

                        bucket
            )
            
        let cache = Dict<IRenderObject, PreparedCommand>()
        let dirty = HashSet<PreparedCommand>()

        let compile (o : IRenderObject) =
            match o with
                | :? CommandRenderObject as o -> compiler.Compile o.Command
                | _ -> new RenderObjectCommand(compiler, o) :> PreparedCommand

        let insert (token : AdaptiveToken) (o : IRenderObject) =
            // insert cmd into trie (link programs)
            let cmd = compile o
            cmd.Update token
            
            let bucket = getBucket o.RenderPass
            bucket.Add cmd
            //trie.Add(cmd.GroupKey, cmd)
            cache.[o] <- cmd
            

        let remove (o : IRenderObject) =
            match cache.TryRemove(o) with
                | (true, cmd) ->
                    // unlink stuff (remove from trie)
                    
                    let bucket = getBucket o.RenderPass
                    bucket.Remove(cmd) |> ignore

                    lock dirty (fun () -> dirty.Remove cmd |> ignore)
                    cmd.Dispose()
                | _ ->
                    ()
            

        override x.InputChangedObject(_,i) =
            match i with
                | :? PreparedCommand as c ->
                    // should be unreachable
                    lock dirty (fun () -> dirty.Add c |> ignore)
                | _ ->
                    ()

        override x.Free() =
            // release all RenderObjectCommands
            for cmd in cache.Values do cmd.Dispose()

            // clear all caches
            cache.Clear()
            dirty.Clear()
            reader <- Unchecked.defaultof<_>

            // free the entry-command
            for (KeyValue(_,b)) in trie do b.Dispose()
            trie.Clear()


        override x.Compile(token, stream) =
            let deltas = reader.GetChanges token

            // process all pending deltas ensuring that all Adds are processed before all Rems 
            // allowing resources to 'survive' the update
            deltas |> HashSetDelta.iter (insert token) (remove)

            // get and update the dirty PreparedCommands (can be non-empty due to CommandNode)
            let dirty = lock dirty (fun () -> consume dirty)
            for d in dirty do d.Update(token)

            for (KeyValue(_,b)) in trie do
                b.Update token

            // rebuild the top-level stream
            stream.Clear()
            match firstBucket with
                | Some first ->
                    stream.Call(first.First.Stream) |> ignore
                | None ->
                    ()


    /// Clearing the current Framebuffer using the supplied values
    and ClearCommand(compiler : Compiler, colors : Map<Symbol, aval<C4f>>, depth : Option<aval<float>>, stencil : Option<aval<uint32>>) =
        inherit PreparedCommand()

        override x.Compile(token, stream) =
            // figure out a viewport
            // NOTE that currently all devices clear everything in multi-GPU setups
            let viewport = compiler.viewports.GetValue(token).[0]

            // check if the RenderPass actually has a depth-stencil attachment
            let hasDepth = Option.isSome compiler.renderPass.DepthStencilAttachment

            // create an array containing the depth-clear (if any)
            let depthClears =
                match compiler.renderPass.DepthStencilAttachment with
                    | Some (id,_) ->
                        let id = 0u
                        match depth, stencil with
                            | Some d, Some s ->
                                let d = d.GetValue token
                                let s = s.GetValue token

                                [|
                                    VkClearAttachment(
                                        VkImageAspectFlags.DepthBit ||| VkImageAspectFlags.StencilBit, 
                                        uint32 id,
                                        VkClearValue(depthStencil = VkClearDepthStencilValue(float32 d, s))
                                    )
                                |]

                            | Some d, None ->   
                                let d = d.GetValue token
                        
                                [|
                                    VkClearAttachment(
                                        VkImageAspectFlags.DepthBit, 
                                        uint32 id,
                                        VkClearValue(depthStencil = VkClearDepthStencilValue(float32 d, 0u))
                                    )
                                |]
                             
                            | None, Some s ->
                                let s = s.GetValue token

                                [|
                                    VkClearAttachment(
                                        VkImageAspectFlags.StencilBit, 
                                        uint32 id,
                                        VkClearValue(depthStencil = VkClearDepthStencilValue(1.0f, s))
                                    )
                                |]

                            | None, None ->
                                [||]
                    | _ -> 
                        // if the RenderPass does not contain a depth-stencil attachment we can't clear it
                        [||]

            // create an array containing all color-clears
            let colorClears = 
                compiler.renderPass.ColorAttachments |> Map.toSeq |> Seq.choose (fun (i,(n,_)) ->
                    match Map.tryFind n colors with
                        | Some value -> 
                            let res = 
                                VkClearAttachment(
                                    VkImageAspectFlags.ColorBit, 
                                    uint32 i,
                                    VkClearValue(color = VkClearColorValue(float32 = value.GetValue(token).ToV4f()))
                                )
                            Some res
                        | None ->
                            None
                ) |> Seq.toArray

            // submit the clear to the stream (after resetting it)
            stream.Clear()
            

            //let clears = Array.append depthClears colorClears
            //let rects = 
            //    Array.init clears.Length (fun _ ->
            //        VkClearRect(
            //            VkRect2D(VkOffset2D(viewport.Min.X,viewport.Min.Y), VkExtent2D(uint32 (5 + viewport.SizeX) , uint32 (5+ viewport.SizeY))),
            //            0u,
            //            uint32 compiler.renderPass.LayerCount
            //        )
            //    )

            stream.ClearAttachments(
                Array.append depthClears colorClears,
                [|
                    VkClearRect(
                        VkRect2D(VkOffset2D(viewport.Min.X,viewport.Min.Y), VkExtent2D(uint32 (1 + viewport.SizeX) , uint32 (1 + viewport.SizeY))),
                        0u,
                        uint32 compiler.renderPass.LayerCount
                    )
                |]
            ) |> ignore

        override x.Free() =
            ()

    /// Executing a list of commands sequentially
    and OrderedCommand(compiler : Compiler, commands : alist<RuntimeCommand>) =
        inherit PreparedCommand()

        let mutable reader = commands.GetReader()

        let first = new VKVM.CommandStream()
        let cache = SortedDictionaryExt<Index, PreparedCommand>(compare)
        let dirty = HashSet<PreparedCommand>()

        let destroy (cmd : PreparedCommand) =
            lock dirty (fun () -> dirty.Remove cmd |> ignore)

            match cmd.Prev with
                | Some p -> p.Next <- cmd.Next
                | None -> first.Next <- cmd.Next |> Option.map (fun c -> c.Stream)

            match cmd.Next with
                | Some n -> n.Prev <- cmd.Prev
                | None -> ()

            cmd.Dispose()

        let set (token : AdaptiveToken) (index : Index) (v : RuntimeCommand) =
            cache |> SortedDictionary.setWithNeighbours index (fun l s r ->
                    
                // TODO: move causes destroy/prepare
                match s with
                    | Some cmd -> destroy cmd
                    | None -> ()

                let cmd = compiler.Compile v
                cmd.Update(token)

                match l with
                    | Some(_,l) -> 
                        l.Next <- Some cmd
                        cmd.Prev <- Some l
                    | None -> 
                        first.Next <- Some cmd.Stream
                        cmd.Prev <- None

                match r with
                    | Some(_,r) -> 
                        cmd.Next <- Some r
                        r.Prev <- Some cmd
                    | None -> 
                        cmd.Next <- None

                cmd
            ) |> ignore
                
        let remove (index : Index) =
            match cache.TryGetValue index with
                | (true, cmd) ->
                    cache.Remove index |> ignore
                    destroy cmd
                | _ ->
                    ()

        override x.InputChangedObject(_,i) =
            match i with
                | :? PreparedCommand as c ->
                    lock dirty (fun () -> dirty.Add c |> ignore)
                | _ ->
                    ()

        override x.Compile(token, stream) =
            let deltas = reader.GetChanges token

            // process all operations making sure that all Sets are performed 
            // before all Removes, allowing resources 'survive' the update
            deltas |> IndexListDelta.iter (set token) (remove)

            // update all dirty inner commands
            let dirty = lock dirty (fun () -> consume dirty)
            for d in dirty do d.Update(token)

            // refill the top-level stream
            stream.Clear()
            stream.Call(first) |> ignore

        override x.Free() =
            reader <- Unchecked.defaultof<_>
            cache |> Seq.iter (fun (KeyValue(_,v)) -> v.Dispose())
            cache.Clear()
            dirty.Clear()
            first.Dispose()

    /// Conditionally dispatching between two commands (while keeping both updated)
    and IfThenElseCommand(compiler : Compiler, condition : aval<bool>, ifTrue : RuntimeCommand, ifFalse : RuntimeCommand) =
        inherit PreparedCommand()

        let ifTrue = compiler.Compile ifTrue
        let ifFalse = compiler.Compile ifFalse

        override x.Free() =
            ifTrue.Dispose()
            ifFalse.Dispose()

        override x.Compile(token, stream) =
            // get the condition-value
            let cond = condition.GetValue(token)

            // update both branches (TODO: is that really desired?
            ifTrue.Update(token)
            ifFalse.Update(token)

            // update the stream
            stream.Clear()
            if cond then stream.Call(ifTrue.Stream) |> ignore
            else stream.Call(ifFalse.Stream) |> ignore
            
    /// Conditionally dispatching between two commands (while keeping only the active one alive)
    and StructuralIfThenElseCommand(compiler : Compiler, condition : aval<bool>, ifTrue : RuntimeCommand, ifFalse : RuntimeCommand) =
        inherit PreparedCommand()

        let mutable cache : Option<bool * PreparedCommand> = None

        override x.Free() =
            match cache with
                | Some (_,c) -> 
                    c.Dispose()
                    cache <- None
                | None ->
                    ()

        override x.Compile(token, stream) =
            let cond = condition.GetValue token
            let newCmd = if cond then ifTrue else ifFalse

            match cache with
                | Some(o,cmd) when o = cond ->
                    // if the cached condition-value matches the stream must be up-to-date.
                    cmd.Update(token)

                | _ ->
                    // otherwise the old stream shall be disposed
                    cache |> Option.iter (snd >> dispose)

                    // the new one needs to be compiled (and cached)
                    let newCmd = compiler.Compile(newCmd)
                    newCmd.Update(token)
                    cache <- Some(cond, newCmd)
                    
                    // the stream also needs to be updated
                    stream.Clear()
                    stream.Call(newCmd.Stream) |> ignore

    /// Rendering a single Geometry using the given PreparedPipelineState
    and GeometryCommand(compiler : Compiler, pipeline : PreparedPipelineState, geometry : Geometry, async : bool) =
        inherit PreparedCommand()

        static let alwaysActive =
            let ptr = NativePtr.alloc 1
            NativePtr.write ptr 1
            ptr

        let mutable prepared : Option<PreparedGeometry> = None

        member x.Prepared = prepared

        override x.Free() =
            match prepared with
                | Some pg ->
                    if async then
                        for r in pg.pgResources do r.Release()
                    else
                        for r in pg.pgResources do compiler.resources.Remove r
                    prepared <- None
                | None ->
                    ()

        override x.Compile(_, stream) =
            // never gets re-executed so the stream does not need to be cleared

            let uniforms = compiler.task.HookProvider (UniformProvider.ofMap geometry.uniforms)

            let pg = compiler.manager.PrepareGeometry(pipeline, geometry, uniforms)
            prepared <- Some pg

            if async then
                let updateResources = pg.pgResources |> List.filter (fun r -> r.ReferenceCount = 0)
                for r in pg.pgResources do r.Acquire()
                for r in updateResources do r.Update(AdaptiveToken.Top) |> ignore
            else
                for r in pg.pgResources do compiler.resources.Add r
                
            stream.IndirectBindDescriptorSets(pg.pgDescriptors.Pointer) |> ignore

            match pg.pgIndex with
                | Some index ->
                    stream.IndirectBindIndexBuffer(index.Pointer) |> ignore
                | None ->
                    ()

            stream.IndirectBindVertexBuffers(pg.pgAttributes.Pointer) |> ignore
            stream.IndirectDraw(compiler.stats, alwaysActive, pg.pgCall.Pointer) |> ignore

    /// Rendering a set of Geometries using the given Surface
    and GroupedCommand(compiler : Compiler, surface : Aardvark.Rendering.Surface, state : PipelineState, geometries : aset<Geometry>) =
        inherit PreparedCommand()
            
        let mutable reader = geometries.GetReader()

        // entry and exit streams for the entire GroupedCommand
        let first = new VKVM.CommandStream()
        let mutable last = first

        // caches
        let cache = Dict<Geometry, GeometryCommand>()
        let mutable preparedPipeline = None


        let getPipeline (stream : VKVM.CommandStream) =
            match preparedPipeline with
                | None -> 
                    // create and cache the PreparedPipelineState
                    let pipeline = compiler.manager.PreparePipelineState(compiler.renderPass, surface, { state with GlobalUniforms = compiler.task.HookProvider state.GlobalUniforms })
                    compiler.resources.Add pipeline.ppPipeline
                    preparedPipeline <- Some pipeline

                    // adjust the first-command to bind the pipeline
                    first.IndirectBindPipeline(pipeline.ppPipeline.Pointer) |> ignore

                    // the main-stream just needs to call the first stream
                    stream.Clear()
                    stream.Call(first) |> ignore

                    pipeline
                
                | Some pipeline ->
                    pipeline
            
        let add (token : AdaptiveToken) (pipeline : PreparedPipelineState) (g : Geometry) =
            // create and update a GeometryCommand
            let cmd = new GeometryCommand(compiler, pipeline, g, false)
            cmd.Update token

            // store it in the cache
            cache.[g] <- cmd
                            
            // append it after the current last-command
            let stream = cmd.Stream
            last.Next <- Some stream
            last <- stream
            
        let remove (g : Geometry) =
            // try to remove the GeometryCommand associated with this Geometry
            match cache.TryRemove g with
                | (true, cmd) ->
                    // unlink the stream
                    let stream = cmd.Stream

                    // if the stream had no next (was last) its Prev will be the new last
                    match stream.Next with
                        | Some n -> () // no linking necessary since the setter of Next maintains Prev too.
                        | None -> last <- stream.Prev.Value

                    // if the stream had a Prev pointing to it this Prev now needs to point
                    // to the stream's next
                    match stream.Prev with
                        | Some s -> s.Next <- stream.Next
                        | None -> 
                            // since 'first' cannot be removed (its not associated with a Geometry) this case should be unreachable
                            failf "INVALID: GeometryCommand did not have a proper Prev"


                    cmd.Dispose()

                | _ ->
                    // if no GeometryCommand exists a Geometry is removed that has never been added!
                    failf "removing a Geometry from a GroupedCommand that has never been added"

        override x.Free() =
            reader <- Unchecked.defaultof<_>
            first.Dispose()
            match preparedPipeline with
                | Some p ->
                    compiler.resources.Remove p.ppPipeline
                    preparedPipeline <- None
                | None ->
                    ()

            for cmd in cache.Values do cmd.Dispose()
            cache.Clear()
            last <- Unchecked.defaultof<_>

        override x.Compile(token, stream) =
            let pipeline = getPipeline stream
            let ops = reader.GetChanges token
            ops |> HashSetDelta.iter (add token pipeline) (remove)

    and BindPipelineCommand(compiler : Compiler, surface : Aardvark.Rendering.Surface, state : PipelineState) =
        inherit PreparedCommand()
        
        let mutable preparedPipeline : Option<PreparedPipelineState> = None
            
        member x.PreparedPipeline = preparedPipeline

        override x.Free() =
            match preparedPipeline with
                | Some p -> 
                    compiler.resources.Remove p.ppPipeline
                    preparedPipeline <- None
                | None -> 
                    ()

        override x.Compile(_, stream : VKVM.CommandStream) =
            let pipeline = compiler.manager.PreparePipelineState(compiler.renderPass, surface, { state with GlobalUniforms = compiler.task.HookProvider state.GlobalUniforms })
            compiler.resources.Add pipeline.ppPipeline
            preparedPipeline <- Some pipeline

            stream.Clear()
            stream.IndirectBindPipeline(pipeline.ppPipeline.Pointer) |> ignore


    and LodTreeCommand(compiler : Compiler, surface : Aardvark.Rendering.Surface, state : PipelineState, loader : LodTreeLoader<Geometry>) as this =
        inherit PreparedCommand()

        let first = new BindPipelineCommand(compiler, surface, state)
        let mutable last = first :> PreparedCommand

        let mutable running = false
        let mutable thread = { new IDisposable with member x.Dispose() = () }

        let hinig = List<GeometryCommand>()

        let disposer =
            startThread "Disposer" <| fun () ->
                while true do
                    let hinig = 
                        lock hinig (fun () -> 
                            while hinig.Count = 0 do
                                Monitor.Wait hinig |> ignore
                            consumeList hinig
                        )
                    for h in hinig do h.Dispose()

        let toDelete = System.Collections.Generic.List<GeometryCommand>()

        let prepare pipeline (g : Geometry) =
            let device = compiler.manager.Device

            let old = device.UnsafeCurrentToken
            try
                let token = new DeviceToken(device.GraphicsFamily, ref None) //compiler.manager.Device.Token
                device.UnsafeSetToken (Some token)
            
                let cmd = new GeometryCommand(compiler, pipeline, g, true)
                cmd.Update(AdaptiveToken.Top)

                let task = device.CopyEngine.WaitTask()

                task |> Task.bind (fun () ->
                    token.SyncTask(4).AsTask |> Task.map (fun () ->
                        token.Dispose()
                        cmd
                    )
                )
            finally
                device.UnsafeSetToken old

        let delete (cmd : GeometryCommand) =
            lock toDelete (fun () -> toDelete.Add cmd)

        let activeSet = HashSet<GeometryCommand>()

        let mutable activeDelta = HashSetDelta.empty


        let activate (cmd : GeometryCommand) =
            lock first (fun () ->
                activeDelta <- HashSetDelta.add (Add cmd) activeDelta
            )

        let deactivate (cmd : GeometryCommand) =
            lock first (fun () ->
                activeDelta <- HashSetDelta.add (Rem cmd) activeDelta
            )

        let realActivate (cmd : GeometryCommand) =
            match cmd.Prepared with
                | Some pg ->
                    for r in pg.pgResources do
                        compiler.resources.Add r
                | None ->
                    failf "invalid GeometryCommand (not prepared)"

            if not (activeSet.Add cmd) then Log.error "double add"

            cmd.Next <- None
            cmd.Prev <- Some last

            last.Next <- Some (cmd :> PreparedCommand)
            last <- cmd

        let realDeactivate (cmd : GeometryCommand) =
            match cmd.Prepared with
                | Some pg ->
                    for r in pg.pgResources do compiler.resources.Remove r
                | None ->
                    failf "invalid GeometryCommand (not prepared)"

            if not (activeSet.Remove cmd) then Log.error "double free"
                
            let prev = 
                match cmd.Prev with
                    | Some p -> p
                    | None -> failf "asdasdasdasd"

            match cmd.Next with
                | Some n -> 
                    prev.Next <- Some n
                    n.Prev <- Some prev
                | None -> 
                    prev.Next <- None
                    last <- prev

        let flush() =
            Log.line "flush"
            transact (fun () -> this.MarkOutdated())

        let config pipeline =
            {
                prepare     = prepare pipeline
                delete      = delete
                activate    = activate
                deactivate  = deactivate
                flush       = flush
            }

        override x.Free() =
            ()

        override x.Compile(token, stream) =
            Log.line "update"
            first.Update(token)

            if not running then 
                running <- true
                 
                stream.Clear()
                stream.Call(first.Stream) |> ignore
                thread <- loader.Start (config first.PreparedPipeline.Value)

   
            let activeDelta =
                lock first (fun () ->
                    let res = activeDelta
                    activeDelta <- HashSetDelta.empty
                    res
                )

            activeDelta |> HashSetDelta.iter realActivate realDeactivate
       
            
            let dead = lock toDelete (fun () -> consumeList toDelete)
            lock hinig (fun () -> hinig.AddRange dead; Monitor.PulseAll hinig)

            //innerCompiler.resources.Update(token) |> ignore
            ()

    and Reader<'a> = AdaptiveToken -> HashSet<'a> -> HashSetDelta<'a>

    and private DummyObject() =
        inherit AdaptiveObject()

    and IndirectDrawCommand(compiler : Compiler, effect : FShade.Effect, state : PipelineState, newReader : unit -> IOpReader<HashSetDelta<IndexedGeometry>>) =
        inherit PreparedCommand()

        let mutable reader = newReader()

        // entry and exit streams for the entire GroupedCommand
        let first = new VKVM.CommandStream()
        let drawStream = new VKVM.CommandStream()
        
        do first.Next <- Some drawStream


        // caches
        let mutable initialized = false

        let effect = Effect.withInstanceUniforms state.PerGeometryUniforms effect

        let pipeline = 
            let surface = Aardvark.Rendering.Surface.FShadeSimple effect
            compiler.manager.PreparePipelineState(compiler.renderPass, surface, state)

        let descritorSet, descritorSetResources =
            let sets, resources = compiler.manager.CreateDescriptorSets(pipeline.ppLayout, compiler.task.HookProvider state.GlobalUniforms)
            compiler.manager.CreateDescriptorSetBinding(pipeline.ppLayout, sets), resources

        let pipelineInfo = pipeline.ppLayout.PipelineInfo

        let instanceInputs =
            pipelineInfo.pInputs |> List.choose (fun i -> match Map.tryFind i.paramSemantic state.PerGeometryUniforms with | Some typ -> Some(i.paramName, (i, typ)) | _ -> None) |> Map.ofList
            
        let vertexInputs =
            pipelineInfo.pInputs |> List.choose (fun i -> match Map.tryFind (Symbol.Create i.paramSemantic) state.VertexInputTypes with | Some typ -> Some(Symbol.Create i.paramName, (i, typ)) | _ -> None) |> Map.ofList

  

        let instanceWriters =
            instanceInputs |> Map.map (fun name (i, typ) ->
                UniformWriters.getWriter 0 i.paramType typ
            )

        let slotSems =
            let lastSlot = pipelineInfo.pInputs |> List.map (fun i -> i.paramLocation) |> List.max
            let slots = 1 + lastSlot

            let sems = Array.zeroCreate slots

            for s in pipelineInfo.pInputs do
                sems.[s.paramLocation] <- Symbol.Create s.paramSemantic
            sems

        let vertexChunkSize = 1048576
        let initialInstanceCount = 16384
        let initialIndirectBufferSize = 2048


        let vertexManager = new BufferMemoryManager(compiler.manager.Device, vertexInputs, vertexChunkSize)
        let instanceManager = new InstanceBufferPool(compiler.manager.Device, instanceInputs, initialInstanceCount)


        let slots = Dict<IndexedGeometry, GeometrySlot * InstanceBufferSlot>()
        let activations = Dict<GeometrySlot * InstanceBufferSlot, IDisposable>()

        let indirectBuffers = Dict<int, ResizableIndirectBuffer>()


        let prepare (g : IndexedGeometry) =
            assert (isNull g.IndexArray)
            let fvc = 
                let (KeyValue(_,att)) = g.IndexedAttributes |> Seq.head
                att.Length

            let vSlot = vertexManager.Alloc(fvc)
            vSlot.WriteVertexAttributes(g)

            let uSlot = 
                let uniforms =
                    instanceInputs |> Map.map (fun name _ ->
                        let name = Symbol.Create name
                        match g.SingleAttributes.TryGetValue name with
                            | (true, (:? IAdaptiveValue as a)) -> a
                            | _ -> 
                                match pipeline.ppUniforms.TryGetUniform(Ag.Scope.Root, name) with
                                    | Some a -> a
                                    | None -> failwithf "[Vulkan] could not get uniform %A" name
                    )
                
                instanceManager.NewSlot(uniforms)

            vSlot, uSlot


        let init (stream : VKVM.CommandStream) =
            if not initialized then
                initialized <- true

                // acquire the PreparedPipelineState
                compiler.resources.Add pipeline.ppPipeline
                for r in descritorSetResources do compiler.resources.Add r
                compiler.resources.Add descritorSet

                compiler.resources.Add instanceManager

                // adjust the first-command to bind the pipeline
                first.IndirectBindPipeline(pipeline.ppPipeline.Pointer) |> ignore
                first.IndirectBindDescriptorSets(descritorSet.Pointer) |> ignore

                // the main-stream just needs to call the first stream
                stream.Clear()
                stream.Call(first) |> ignore
        
        let getIndirect (blockId : int) =
            indirectBuffers.GetOrCreate(blockId, fun _ -> new ResizableIndirectBuffer(compiler.manager.Device, initialIndirectBufferSize))

        let active = History<CountingHashSet<_>, HashSetDelta<_>>(CountingHashSet.trace)
        let activeReader = active.NewReader()

        let deltaLock = obj()
        let mutable pendingDeltas = HashSetDelta.empty

        let mutable running = true
        let pending = MVar.create ()

        let puller =
            startThread "Puller" <| fun () ->
                let o = DummyObject()
                use s = o.AddMarkingCallback (MVar.put pending)

                while running do
                    MVar.take pending
                    if running then
                        let ops = 
                            o.EvaluateAlways AdaptiveToken.Top (fun token ->
                                reader.GetChanges token
                            )

                        lock deltaLock (fun () ->
                            pendingDeltas <- HashSetDelta.combine pendingDeltas ops
                            Monitor.PulseAll deltaLock
                        )

        let processor =
            startThread "Processor" <| fun () ->
                let remComplexity = 20
                let deltaBatchComplexity = 10000

                while running do
                    let ops = 
                        lock deltaLock (fun () ->
                            while running && HashSetDelta.isEmpty pendingDeltas do
                                Monitor.Wait deltaLock |> ignore

                            let mutable rest = pendingDeltas
                            let mutable taken = HashSetDelta.empty
                            let mutable complexity = 0
                            use e = (pendingDeltas :> seq<_>).GetEnumerator()
                            while complexity < deltaBatchComplexity && e.MoveNext() do
                                let op = e.Current
                                let opComplexity = 
                                    match op with
                                        | Add(_,g) -> g.FaceVertexCount
                                        | _ -> remComplexity

                                complexity <- complexity + opComplexity
                                taken <- HashSetDelta.add op taken
                                rest <- HashSetDelta.remove op rest

                            pendingDeltas <- rest
                            taken
//                            let mine = Seq.atMost deltaBatchSize pendingDeltas |> Seq.toList
//                            for op in mine do pendingDeltas <- HDeltaSet.remove op pendingDeltas
//                            mine
                        )

                    if running && not (HashSetDelta.isEmpty ops) then
                        let activeOps =
                            ops |> HashSetDelta.map (fun op ->
                                match op with
                                    | Add(_,g) ->
                                        let vSlot, uSlot = prepare g
                                        slots.[g] <- (vSlot, uSlot)
                                        Add(vSlot, uSlot)

                                    | Rem(_,g) ->
                                        match slots.TryRemove g with
                                            | (true, (vSlot, uSlot)) ->
                                                uSlot.Dispose()
                                                vertexManager.Free vSlot
                                                Rem(vSlot, uSlot)
                                            | _ ->
                                                failf "asdasdasdasdasdasdasdasd"
                                    

                            )

                    
                        compiler.manager.Device.CopyEngine.Wait()

                        transact (fun () ->
                            lock active (fun _ -> 
                                active.Perform activeOps |> ignore
                            )
                        )
                ()

        let shutdown() =
            if running then
                Log.startTimed "[IndirectDraw] shutdown"
                running <- false
                MVar.put pending ()
                puller.Join()
                Log.line "[IndirectDraw] puller joined"

                lock deltaLock (fun () -> Monitor.PulseAll deltaLock)
                processor.Join()
                Log.line "[IndirectDraw] processor joined"

                Log.stop()


        override x.Free() = 
            shutdown()
            if initialized then
                compiler.resources.Remove pipeline.ppPipeline
                for r in descritorSetResources do compiler.resources.Remove r
                compiler.resources.Remove descritorSet
                compiler.resources.Remove instanceManager

            first.Dispose()
            drawStream.Dispose()
            vertexManager.Dispose()
            instanceManager.Kill()
            for b in indirectBuffers.Values do b.Dispose()

            indirectBuffers.Clear()
            slots.Clear()
            reader <- Unchecked.defaultof<_>

        override x.Compile(token : AdaptiveToken, stream : VKVM.CommandStream) =
            init stream

            let deltas = activeReader.GetChanges token


            let mutable adds = 0
            let mutable rems = 0

            for op in deltas do
                match op with
                    | Add(_,(vSlot, uSlot)) ->
                        inc &adds

                        let indirectBuffer = getIndirect vSlot.BlockId

                        let rem = 
                            indirectBuffer.Add <|
                                DrawCallInfo(
                                    FaceVertexCount = vSlot.VertexCount,
                                    InstanceCount = uSlot.InstanceCount,
                                    FirstIndex = vSlot.FirstVertex,
                                    FirstInstance = uSlot.FirstInstance
                                )

                        activations.[(vSlot, uSlot)] <- rem

                    | Rem(_,t) ->
                        inc &rems

                        match activations.TryRemove t with
                            | (true, activation) ->
                                activation.Dispose()
                            | _ ->
                                Log.error "not activated"


            if deltas.Count > 0 then
                transact (fun () -> instanceManager.MarkOutdated())


            vertexManager.Flush()

            drawStream.Clear()
            let mutable drawCount = 0
            let mutable totalCount = 0
            for (blockId, vbo) in vertexManager.AllVertexBuffers do

                let buffers = Array.zeroCreate slotSems.Length
                let offsets = Array.zeroCreate slotSems.Length

                for slot in 0 .. slotSems.Length - 1 do
                    let sem = slotSems.[slot]
                    
                    match Map.tryFind (string sem) instanceManager.Buffer with
                        | Some buffer -> 
                            buffers.[slot] <- buffer.Handle
                        | None ->
                            match Map.tryFind sem vbo with
                                | Some (b,_) -> 
                                    buffers.[slot] <- b.Handle
                                | _ -> ()
                    
                match indirectBuffers.TryGetValue blockId with
                    | (true, indirect) -> 
                        if indirect.Count > 0 then
                            drawStream.BindVertexBuffers(0u, buffers, offsets) |> ignore
                            if indirect.Count = 1  then
                                let drawCall = indirect.[0]
                                drawStream.Draw(uint32 drawCall.FaceVertexCount, uint32 drawCall.InstanceCount, 
                                                uint32 drawCall.FirstIndex, uint32 drawCall.FirstInstance) |> ignore
                            else 
                                //indirect.Upload()

                                for a in 0 .. indirect.Count - 1 do
                                    let drawCall = indirect.[a]
                                    drawStream.Draw(uint32 drawCall.FaceVertexCount, uint32 drawCall.InstanceCount, 
                                                uint32 drawCall.FirstIndex, uint32 drawCall.FirstInstance) |> ignore

                                ///drawStream.DrawIndirect(indirect.Handle, 0UL, uint32 indirect.Count, 20u) |> ignore

                        inc &drawCount
                        totalCount <- totalCount + indirect.Count
                    | _ ->
                        ()
    


    and Compiler =
        {
            task            : AbstractRenderTask
            resources       : ResourceLocationSet
            manager         : ResourceManager
            renderPass      : RenderPass
            stats           : nativeptr<V2i>
            viewports       : aval<Box2i[]>
            scissors        : aval<Box2i[]>
        }

        member x.Compile (cmd : RuntimeCommand) : PreparedCommand =
            match cmd with
                | RuntimeCommand.EmptyCmd ->
                    new EmptyCommand()
                        :> PreparedCommand

                | RuntimeCommand.RenderCmd objects ->
                    new UnorderedRenderObjectCommand(x, objects)
                        :> PreparedCommand

                | RuntimeCommand.ClearCmd(colors, depth, stencil) ->
                    new ClearCommand(x, colors, depth, stencil)
                        :> PreparedCommand

                | RuntimeCommand.OrderedCmd commands ->
                    new OrderedCommand(x, commands)
                        :> PreparedCommand

                | RuntimeCommand.IfThenElseCmd(c,i,e) ->
                    new IfThenElseCommand(x, c, i, e)
                        :> PreparedCommand

                | RuntimeCommand.GeometriesCmd(surface, state, geometries) ->
                    new GroupedCommand(x, surface, state, geometries)
                        :> PreparedCommand

                | RuntimeCommand.LodTreeCmd(surface, state, geometries) ->
                    new LodTreeCommand(x, surface, state, geometries)
                        :> PreparedCommand
                        
                | RuntimeCommand.GeometriesSimpleCmd(effect, state, geometries) ->
                    new IndirectDrawCommand(x, effect, state, fun () -> geometries.GetReader() :> _)
                        :> PreparedCommand

                | RuntimeCommand.DispatchCmd _  ->
                    failwith "[Vulkan] compute commands not implemented"

type CommandTask(device : Device, renderPass : RenderPass, command : RuntimeCommand) as this =
    inherit AbstractRenderTask()

    let pool = device.GraphicsFamily.CreateCommandPool(CommandPoolFlags.ResetBuffer)
    let viewports = AVal.init [||]
    let scissors = AVal.init [||]

    let cmd = pool.CreateCommandBuffer(CommandBufferLevel.Primary)
    let inner = pool.CreateCommandBuffer(CommandBufferLevel.Secondary)

    let locks = ReferenceCountingSet<ILockedResource>()

    let user =
        { new IResourceUser with
            member x.AddLocked l = lock locks (fun () -> locks.Add l |> ignore)
            member x.RemoveLocked l = lock locks (fun () -> locks.Remove l |> ignore)
        }

    let stats = NativePtr.alloc 1
    let manager = new ResourceManager(user, device)
    let resources = ResourceLocationSet(user)
    let mutable lastFramebuffer = None

    let compiler =
        {
            RuntimeCommands.Compiler.task            = this
            RuntimeCommands.Compiler.resources       = resources
            RuntimeCommands.Compiler.manager         = manager
            RuntimeCommands.Compiler.renderPass      = renderPass
            RuntimeCommands.Compiler.stats           = stats
            RuntimeCommands.Compiler.viewports       = viewports
            RuntimeCommands.Compiler.scissors        = scissors
        }

    let compiled = compiler.Compile command

    override x.Release() =
        transact (fun () ->
            compiled.Dispose()
            inner.Dispose()
            cmd.Dispose()
            pool.Dispose()
            manager.Dispose()
        )

    override x.FramebufferSignature = Some (renderPass :> _)

    override x.Runtime = Some device.Runtime

    override x.PerformUpdate(token : AdaptiveToken, rt : RenderToken) =
        ()

    override x.Use(f : unit -> 'r) =
        f()

    override x.Perform(token : AdaptiveToken, rt : RenderToken, desc : OutputDescription, queries : IQuery) =
        x.OutOfDate <- true

        let vulkanQueries = queries.ToVulkanQuery()

        let fbo =
            match desc.framebuffer with
                | :? Framebuffer as fbo -> fbo
                | fbo -> failwithf "unsupported framebuffer: %A" fbo

        let ranges =
            let range =
                {
                    frMin = desc.viewport.Min;
                    frMax = desc.viewport.Max;
                    frLayers = Range1i(0,renderPass.LayerCount-1)
                }
            range.Split(int device.AllCount)

        let sc =
            if device.AllCount > 1u then
                if renderPass.LayerCount > 1 then
                    [| desc.viewport |]
                else
                    ranges |> Array.map (fun { frMin = min; frMax = max } -> Box2i(min, max))

            else
                [| desc.viewport |]

        let vp = Array.create sc.Length desc.viewport

        let viewportChanged =
            if viewports.Value = vp && scissors.Value = sc then
                false
            else
                transact (fun () -> viewports.Value <- vp; scissors.Value <- sc)
                true

        let commandChanged =
            lock compiled (fun () ->
                if compiled.OutOfDate then
                    compiled.Update(token)
                    true
                else
                    false
            )

        let resourcesChanged =
            resources.Update(token)

        let framebufferChanged =
            if lastFramebuffer <> Some fbo then
                lastFramebuffer <- Some fbo
                true
            else
                false

        if viewportChanged || commandChanged || resourcesChanged || framebufferChanged then
            let cause =
                String.concat "; " [
                    if commandChanged then yield "content"
                    if resourcesChanged then yield "resources"
                    if viewportChanged then yield "viewport"
                    if framebufferChanged then yield "framebuffer"
                ]
                |> sprintf "{ %s }"

            if Config.showRecompile then
                Log.line "[Vulkan] recompile commands: %s" cause

            inner.Begin(renderPass, fbo, CommandBufferUsage.RenderPassContinue, true)

            inner.enqueue {
                do! Command.SetViewports(vp)
                do! Command.SetScissors(sc)
            }

            inner.AppendCommand()
            compiled.Stream.Run(inner.Handle)

            inner.End()

        queries.Begin()

        cmd.Begin(renderPass, CommandBufferUsage.OneTimeSubmit)

        for q in vulkanQueries do
            q.Begin cmd

        cmd.enqueue {
            let oldLayouts = Array.zeroCreate fbo.ImageViews.Length
            for i in 0 .. fbo.ImageViews.Length - 1 do
                let img = fbo.ImageViews.[i].Image
                oldLayouts.[i] <- img.Layout
                if VkFormat.hasDepth img.Format then
                    do! Command.TransformLayout(img, VkImageLayout.DepthStencilAttachmentOptimal)
                else
                    do! Command.TransformLayout(img, VkImageLayout.ColorAttachmentOptimal)

            do! Command.BeginPass(renderPass, fbo, false)
            do! Command.Execute [inner]
            do! Command.EndPass

            if ranges.Length > 1 then
                let deviceCount = int device.AllCount

                for (sem,a) in Map.toSeq fbo.Attachments do
                    transact (fun () ->
                        let v = a.Image.Version
                        lock v (fun () -> v.Value <- v.Value + 1)
                    )

            for i in 0 .. fbo.ImageViews.Length - 1 do
                let img = fbo.ImageViews.[i].Image
                do! Command.TransformLayout(img, oldLayouts.[i])
        }

        for q in vulkanQueries do
            q.End cmd

        cmd.End()

        queries.End()

        use tt = device.Token
        tt.Sync()

        device.GraphicsFamily.RunSynchronously cmd

