namespace Aardvark.Rendering.Vulkan.Temp

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop
open Aardvark.Base.Incremental
open System.Diagnostics
open System.Collections.Generic
open Aardvark.Base.Runtime
open Aardvark.Rendering.Vulkan

#nowarn "9"
#nowarn "51"

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
    static member PreparePipelineState (this : ResourceManager, renderPass : RenderPass, surface : Aardvark.Base.Surface, state : PipelineState) =
        let layout, program = this.CreateShaderProgram(renderPass, surface)

        let inputs = 
            layout.PipelineInfo.pInputs |> List.map (fun p ->
                let name = Symbol.Create p.name
                match Map.tryFind name state.vertexInputTypes with
                    | Some t -> (name, (false, t))
                    | None -> failf "could not get shader input %A" name
            )
            |> Map.ofList

        let inputState =
            this.CreateVertexInputState(layout.PipelineInfo, Mod.constant (VertexInputState.ofTypes inputs))

        let inputAssembly =
            this.CreateInputAssemblyState(Mod.constant state.geometryMode, program)

        let rasterizerState =
            this.CreateRasterizerState(state.depthTest, state.cullMode, state.fillMode)

        let colorBlendState =
            this.CreateColorBlendState(renderPass, state.writeBuffers, state.blendMode)

        let depthStencil =
            let depthWrite = 
                match state.writeBuffers with
                    | None -> true
                    | Some s -> Set.contains DefaultSemantic.Depth s
            this.CreateDepthStencilState(depthWrite, state.depthTest, state.stencilMode)

        let pipeline = 
            this.CreatePipeline(
                program,
                renderPass,
                inputState,
                inputAssembly,
                rasterizerState,
                colorBlendState,
                depthStencil,
                state.writeBuffers
            )
        {
            ppPipeline  = pipeline
            ppLayout    = layout
            ppUniforms  = state.globalUniforms
        }
            
    [<Extension>]
    static member PrepareGeometry(this : ResourceManager, state : PreparedPipelineState, g : Geometry) : PreparedGeometry =
        let resources = System.Collections.Generic.List<IResourceLocation>()

        let layout = state.ppLayout

        let descriptorSets, additionalResources = 
            this.CreateDescriptorSets(layout, UniformProvider.union (UniformProvider.ofMap g.uniforms) state.ppUniforms)

        resources.AddRange additionalResources

        let vertexBuffers = 
            layout.PipelineInfo.pInputs 
                |> List.sortBy (fun i -> i.location) 
                |> List.map (fun i ->
                    let sem = i.semantic 
                    match Map.tryFind sem g.vertexAttributes with
                        | Some b ->
                            this.CreateBuffer(b), 0L
                        | None ->
                            failf "geometry does not have buffer %A" sem
                )

        let dsb = this.CreateDescriptorSetBinding(layout, Array.toList descriptorSets)
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
        let consume (r : HashSet<'a>) =
            let arr = HashSet.toArray r
            r.Clear()
            arr

        let dispose (d : #IDisposable) =
            d.Dispose()

        module HDeltaSet =
            let iter (add : 'a -> unit) (rem : 'a -> unit) (set : hdeltaset<'a>) =
                set |> Seq.iter (function Add(_,o) -> add o | _ -> ())
                set |> Seq.iter (function Rem(_,o) -> rem o | _ -> ())

        module PDeltaList =
            let iter (set : Index -> 'a -> unit) (remove : Index -> unit) (deltas : pdeltalist<'a>) =
                deltas |> PDeltaList.toSeq |> Seq.iter (function (i,Set v) -> set i v | _ -> ())
                deltas |> PDeltaList.toSeq |> Seq.iter (function (i,Remove) -> remove i | _ -> ())


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




        type BufferMemoryManager(device : Device, instanceTypes : Map<string, Type>, vertexTypes : Map<string, Type>, instanceCount : int, vertexCount : int) =
            


            let instanceManager = DeviceMemoryManager
            let vertexManager = MemoryManager.createNop()

            let vBuffers =
                vertexTypes |> Map.map (fun name t ->
                    let sizeInBytes = int64 (Marshal.SizeOf t) * int64 vertexCount
                    device.DeviceMemory |> Buffer.createConcurrent true (VkBufferUsageFlags.TransferDstBit ||| VkBufferUsageFlags.VertexBufferBit) sizeInBytes
                )

            let iBuffers =
                instanceTypes |> Map.map (fun name t ->
                    let sizeInBytes = int64 (Marshal.SizeOf t) * int64 instanceCount
                    device.DeviceMemory |> Buffer.createConcurrent true (VkBufferUsageFlags.TransferDstBit ||| VkBufferUsageFlags.VertexBufferBit) sizeInBytes
                )

            member x.TryAlloc(vertexCount : int) =
                let index = instanceManager.Alloc(1n)
            




    [<AbstractClass>]
    type PreparedCommand() =
        inherit AdaptiveObject()

        let stream = new VKVM.CommandStream()

        let mutable prev : Option<PreparedCommand> = None
        let mutable next : Option<PreparedCommand> = None

        member x.Prev
            with get() = prev
            and set p = prev <- p

        member x.Next 
            with get() = next
            and set n =
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

        abstract member Compile : AdaptiveToken * VKVM.CommandStream -> unit
        abstract member Free : unit -> unit

        member x.Stream = stream

        member x.Update(t : AdaptiveToken) =
            x.EvaluateIfNeeded t () (fun t ->
                x.Compile(t, stream)
            ) 

        member x.Dispose() =
            x.Free()
            stream.Dispose()

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

        member x.GroupKey =
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
            let o = compiler.manager.PrepareRenderObject(compiler.renderPass, o)
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

    /// Rendering a set of IRenderObjects using an optimized order (grouped by Pipeline)
    and UnorderedRenderObjectCommand(compiler : Compiler, objects : aset<IRenderObject>) =
        inherit PreparedCommand()

        let reader = objects.GetReader()

        let firstCommand =
            { new PreparedCommand() with
                override x.Free() = ()
                override x.Compile(_,_) = ()
            }

        let trie = Trie<PreparedCommand>()
        do trie.Add([], firstCommand)
            
        let cache = Dict<IRenderObject, RenderObjectCommand>()
        let dirty = HashSet<RenderObjectCommand>()

        let insert (token : AdaptiveToken) (o : IRenderObject) =
            // insert cmd into trie (link programs)
            let cmd = new RenderObjectCommand(compiler, o) 
            cmd.Update token
            trie.Add(cmd.GroupKey, cmd)
            cache.[o] <- cmd
            

        let remove (o : IRenderObject) =
            match cache.TryRemove(o) with
                | (true, cmd) ->
                    // unlink stuff (remove from trie)
                    trie.Remove(cmd.GroupKey) |> ignore
                    lock dirty (fun () -> dirty.Remove cmd |> ignore)
                    cmd.Dispose()
                | _ ->
                    ()
            

        override x.InputChanged(_,i) =
            match i with
                | :? RenderObjectCommand as c ->
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
            trie.Clear()
            reader.Dispose()

            // free the entry-command
            firstCommand.Dispose()

        override x.Compile(token, stream) =
            let deltas = reader.GetOperations token

            // process all pending deltas ensuring that all Adds are processed before all Rems 
            // allowing resources to 'survive' the update
            deltas |> HDeltaSet.iter (insert token) (remove)

            // get and update the dirty RenderObjectCommands (actually should be empty since they're unchangeable)
            assert (dirty.Count = 0)
            let dirty = lock dirty (fun () -> consume dirty)
            for d in dirty do d.Update(token)

            // rebuild the top-level stream
            stream.Clear()
            stream.Call(firstCommand.Stream) |> ignore

    /// Clearing the current Framebuffer using the supplied values
    and ClearCommand(compiler : Compiler, colors : Map<Symbol, IMod<C4f>>, depth : Option<IMod<float>>, stencil : Option<IMod<uint32>>) =
        inherit PreparedCommand()

        override x.Compile(token, stream) =
            // figure out a viewport
            // NOTE that currently all devices clear everything in multi-GPU setups
            let viewport = compiler.viewports.GetValue(token).[0]

            // check if the RenderPass actually has a depth-stencil attachment
            let hasDepth = Option.isSome compiler.renderPass.DepthStencilAttachment

            // create an array containing the depth-clear (if any)
            let depthClears =
                if hasDepth then
                    match depth, stencil with
                        | Some d, Some s ->
                            let d = d.GetValue token
                            let s = s.GetValue token

                            [|
                                VkClearAttachment(
                                    VkImageAspectFlags.DepthBit ||| VkImageAspectFlags.StencilBit, 
                                    0u,
                                    VkClearValue(depthStencil = VkClearDepthStencilValue(float32 d, s))
                                )
                            |]

                        | Some d, None ->   
                            let d = d.GetValue token
                        
                            [|
                                VkClearAttachment(
                                    VkImageAspectFlags.DepthBit, 
                                    0u,
                                    VkClearValue(depthStencil = VkClearDepthStencilValue(float32 d, 0u))
                                )
                            |]
                             
                        | None, Some s ->
                            let s = s.GetValue token

                            [|
                                VkClearAttachment(
                                    VkImageAspectFlags.StencilBit, 
                                    0u,
                                    VkClearValue(depthStencil = VkClearDepthStencilValue(1.0f, s))
                                )
                            |]

                        | None, None ->
                            [||]
                else
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
                                    uint32 (if hasDepth then 1 + i else i),
                                    VkClearValue(color = VkClearColorValue(float32 = value.GetValue(token).ToV4f()))
                                )
                            Some res
                        | None ->
                            None
                ) |> Seq.toArray

            // submit the clear to the stream (after resetting it)
            stream.Clear()
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

        let reader = commands.GetReader()

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
                    | Some(_,l) -> l.Next <- Some cmd
                    | None -> first.Next <- Some cmd.Stream

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

        override x.InputChanged(_,i) =
            match i with
                | :? PreparedCommand as c ->
                    lock dirty (fun () -> dirty.Add c |> ignore)
                | _ ->
                    ()

        override x.Compile(token, stream) =
            let deltas = reader.GetOperations token

            // process all operations making sure that all Sets are performed 
            // before all Removes, allowing resources 'survive' the update
            deltas |> PDeltaList.iter (set token) (remove)

            // update all dirty inner commands
            let dirty = lock dirty (fun () -> consume dirty)
            for d in dirty do d.Update(token)

            // refill the top-level stream
            stream.Clear()
            stream.Call(first) |> ignore

        override x.Free() =
            reader.Dispose()
            cache |> Seq.iter (fun (KeyValue(_,v)) -> v.Dispose())
            cache.Clear()
            dirty.Clear()
            first.Dispose()

    /// Conditionally dispatching between two commands (while keeping both updated)
    and IfThenElseCommand(compiler : Compiler, condition : IMod<bool>, ifTrue : RuntimeCommand, ifFalse : RuntimeCommand) =
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
    and StructuralIfThenElseCommand(compiler : Compiler, condition : IMod<bool>, ifTrue : RuntimeCommand, ifFalse : RuntimeCommand) =
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
    and GeometryCommand(compiler : Compiler, pipeline : PreparedPipelineState, geometry : Geometry) =
        inherit PreparedCommand()

        static let alwaysActive =
            let ptr = NativePtr.alloc 1
            NativePtr.write ptr 1
            ptr

        let mutable prepared : Option<PreparedGeometry> = None

        override x.Free() =
            match prepared with
                | Some pg ->
                    for r in pg.pgResources do
                        compiler.resources.Remove r
                    prepared <- None
                | None ->
                    ()

        override x.Compile(_, stream) =
            // never gets re-executed so the stream does not need to be cleared
            let pg = compiler.manager.PrepareGeometry(pipeline, geometry)
            prepared <- Some pg

            for r in pg.pgResources do
                compiler.resources.Add r
                
            stream.IndirectBindDescriptorSets(pg.pgDescriptors.Pointer) |> ignore

            match pg.pgIndex with
                | Some index ->
                    stream.IndirectBindIndexBuffer(index.Pointer) |> ignore
                | None ->
                    ()

            stream.IndirectBindVertexBuffers(pg.pgAttributes.Pointer) |> ignore
            stream.IndirectDraw(compiler.stats, alwaysActive, pg.pgCall.Pointer) |> ignore

    /// Rendering a set of Geometries using the given Surface
    and GroupedCommand(compiler : Compiler, surface : Aardvark.Base.Surface, state : PipelineState, geometries : aset<Geometry>) =
        inherit PreparedCommand()
            
        let reader = geometries.GetReader()

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
                    let pipeline = compiler.manager.PreparePipelineState(compiler.renderPass, surface, state)
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
            let cmd = new GeometryCommand(compiler, pipeline, g)
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
            reader.Dispose()
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
            let ops = reader.GetOperations token
            ops |> HDeltaSet.iter (add token pipeline) (remove)

    and LodTreeCommand(compiler : Compiler, surface : Aardvark.Base.Surface, state : PipelineState, loader : LodTreeLoader<Geometry>) as this =
        inherit PreparedCommand()

        let mutable preparedPipeline : Option<PreparedPipelineState> = None
            
        let first = new VKVM.CommandStream()
        let mutable last = first

        let mutable thread = { new IDisposable with member x.Dispose() = () }

        let toDelete = System.Collections.Generic.List<GeometryCommand>()


//            let innerCompiler =
//                let user = 
//                    { new IResourceUser with 
//                        member x.AddLocked(_) = () 
//                        member x.RemoveLocked(_) = ()
//                    }
//                { compiler with resources = ResourceLocationSet user }

        let prepare pipeline (g : Geometry) =
            use t = compiler.manager.Device.Token
            let cmd = new GeometryCommand(compiler, pipeline, g)
            cmd.Update(AdaptiveToken.Top)
            cmd

        let delete (cmd : GeometryCommand) =
            lock toDelete (fun () -> toDelete.Add cmd)

        let activate (cmd : GeometryCommand) =
            lock first (fun () ->
                let s = cmd.Stream
                s.Next <- None
                last.Next <- Some s
                last <- s
            )

        let deactivate (cmd : GeometryCommand) =
            lock first (fun () ->
                let s = cmd.Stream
                let prev = s.Prev.Value

                match s.Next with
                    | Some n ->
                        prev.Next <- Some n
                    | None ->
                        last <- prev
                        prev.Next <- None

            )

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
            match preparedPipeline with
                | None -> 
                    let pipeline = compiler.manager.PreparePipelineState(compiler.renderPass, surface, state)
                    compiler.resources.Add pipeline.ppPipeline
                    first.IndirectBindPipeline(pipeline.ppPipeline.Pointer) |> ignore
                    preparedPipeline <- Some pipeline

                    stream.Clear()
                    stream.Call(first) |> ignore
                    thread <- loader.Start (config pipeline)

                    ()
                | Some _ ->
                    ()
                            
            let dead =
                lock toDelete (fun () -> 
                    let arr = CSharpList.toArray toDelete
                    toDelete.Clear()
                    arr
                )
            for d in dead do d.Dispose()

            //innerCompiler.resources.Update(token) |> ignore
            ()

    and SimpleLodCommand(compiler : Compiler, effect : FShade.Effect, state : PipelineState, loader : LodTreeLoader<IndexedGeometry>) =
        inherit PreparedCommand()

        static let noDisposable = { new IDisposable with member x.Dispose() = () }

        // entry and exit streams for the entire GroupedCommand
        let first = new VKVM.CommandStream()
        let mutable last = first

        // caches
        let cache = Dict<Geometry, GeometryCommand>()
        let mutable loadThread = noDisposable
        let mutable initialized = false

        let effect = Effect.withInstanceUniforms state.perGeometryUniforms effect

        let pipeline = 
            let surface = Aardvark.Base.Surface.FShadeSimple effect
            compiler.manager.PreparePipelineState(compiler.renderPass, surface, state)

        let pipelineInfo = pipeline.ppLayout.PipelineInfo

        let instanceInputs =
            pipelineInfo.pInputs |> List.choose (fun i -> match Map.tryFind i.name state.perGeometryUniforms with | Some typ -> Some(i.name, (i, typ)) | _ -> None) |> Map.ofList
            
        let vertexInputs =
            pipelineInfo.pInputs |> List.choose (fun i -> match Map.tryFind (Symbol.Create i.name) state.vertexInputTypes with | Some typ -> Some(Symbol.Create i.name, (i, typ)) | _ -> None) |> Map.ofList

        let instanceWriters =
            instanceInputs |> Map.map (fun name (i, typ) ->
                UniformWriters.getWriter 0 (UniformType.Primitive(i.shaderType, 0, 0)) typ
            )
  





        let prepare (g : IndexedGeometry) =
            state.uni
            
            ()


        let init (stream : VKVM.CommandStream) =
            if not initialized then
                initialized <- true

                // acquire the PreparedPipelineState
                compiler.resources.Add pipeline.ppPipeline

                // adjust the first-command to bind the pipeline
                first.IndirectBindPipeline(pipeline.ppPipeline.Pointer) |> ignore

                // the main-stream just needs to call the first stream
                stream.Clear()
                stream.Call(first) |> ignore


        override x.Free() = 
            ()

        override x.Compile(token : AdaptiveToken, stream : VKVM.CommandStream) =
            init stream
            ()


    and Compiler =
        {
            resources       : ResourceLocationSet
            manager         : ResourceManager
            renderPass      : RenderPass
            stats           : nativeptr<V2i>
            viewports       : IMod<Box2i[]>
            scissors        : IMod<Box2i[]>
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
                        

                | RuntimeCommand.DispatchCmd _  ->
                    failwith "[Vulkan] compute commands not implemented"

type CommandTask(device : Device, renderPass : RenderPass, command : RuntimeCommand) =
    inherit AbstractRenderTask()

    let pool = device.GraphicsFamily.CreateCommandPool()
    let viewports = Mod.init [||]
    let scissors = Mod.init [||]

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

    let compiler =
        {
            RuntimeCommands.resources       = resources
            RuntimeCommands.manager         = manager
            RuntimeCommands.renderPass      = renderPass
            RuntimeCommands.stats           = stats
            RuntimeCommands.viewports       = viewports
            RuntimeCommands.scissors        = scissors
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

    override x.Perform(token : AdaptiveToken, rt : RenderToken, desc : OutputDescription) =
        x.OutOfDate <- true

        let fbo =
            match desc.framebuffer with
                | :? Framebuffer as fbo -> fbo
                | fbo -> failwithf "unsupported framebuffer: %A" fbo
   
        let sc =
            if device.AllCount > 1u then
                if renderPass.LayerCount > 1 then
                    [| desc.viewport |]
                else
                    let range = 
                        { 
                            frMin = desc.viewport.Min; 
                            frMax = desc.viewport.Max;
                            frLayers = Range1i(0,renderPass.LayerCount-1)
                        }
                    range.Split(int device.AllCount) 
                        |> Array.map (fun { frMin = min; frMax = max } -> Box2i(min, max))
                        
            else
                [| desc.viewport |]

        let vp = Array.create sc.Length desc.viewport

        let viewportChanged =
            if viewports.Value = vp && scissors.Value = sc then
                false
            else
                transact (fun () -> viewports.Value <- vp; scissors.Value <- sc)
                true

        use tt = device.Token
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

        if viewportChanged || commandChanged || resourcesChanged then
            let cause =
                String.concat "; " [
                    if commandChanged then yield "content"
                    if resourcesChanged then yield "resources"
                    if viewportChanged then yield "viewport"
                ]
                |> sprintf "{ %s }"

            Log.line "[Vulkan] recompile commands: %s" cause

            inner.Reset()
            inner.Begin(renderPass, CommandBufferUsage.RenderPassContinue)

            inner.enqueue {
                do! Command.SetViewports(vp)
                do! Command.SetScissors(sc)
            }

            inner.AppendCommand()
            compiled.Stream.Run(inner.Handle)
                
            inner.End()


        tt.Sync()

        cmd.Reset()
        cmd.Begin(renderPass, CommandBufferUsage.OneTimeSubmit)
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

            for i in 0 .. fbo.ImageViews.Length - 1 do
                let img = fbo.ImageViews.[i].Image
                do! Command.TransformLayout(img, oldLayouts.[i])
        }   
        cmd.End()

        device.GraphicsFamily.RunSynchronously cmd

