module EagerResource

open System
open System.Threading
open System.Collections.Generic
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop

open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental

#nowarn "9"

[<AllowNullLiteral>]
type LevelQueueNode<'a> =
    class
        val mutable public Level : int
        val mutable public Content : HashSet<'a>
        val mutable public Prev : LevelQueueNode<'a>
        val mutable public Next : LevelQueueNode<'a>


        new(l,p,n) = { Level = l; Content = HashSet(); Prev = p; Next = n }
    end

type LevelQueue<'a>() =
    let mutable first : LevelQueueNode<'a> = null
    let mutable last : LevelQueueNode<'a> = null
    let store = SortedDictionaryExt<int, LevelQueueNode<'a>>(Comparer<int>.Default)

    member x.IsEmpty : bool =
        isNull first

    member x.Dequeue() : HashSet<'a> =
        assert (not (isNull first))
        let c = first.Content
        first <- first.Next
        if isNull first then last <- null
        else first.Prev <- null
        c


    member x.Add(level : int, value : 'a) : bool =
        let (p,s,n) = SortedDictionary.neighbourhood level store
        match s with
            | Some s -> 
                s.Content.Add value

            | None ->
                let p = match p with | Some(_,p) -> p | _ -> null
                let n = match n with | Some(_,n) -> n | _ -> null

                let node = LevelQueueNode<'a>(level, p, n)
                node.Content.Add value |> ignore
                store.[level] <- node

                if isNull p then first <- node
                else p.Next <- node

                if isNull n then last <- node
                else n.Prev <- node

                true
                
    member x.Remove(level : int, value : 'a) : bool =
        match store.TryGetValue level with
            | (true, node) ->
                if node.Content.Remove value then
                    if node.Content.Count = 0 then
                        let p = node.Prev
                        let n = node.Next

                        if isNull p then first <- n
                        else p.Next <- n

                        if isNull n then last <- p
                        else n.Prev <- p

                        store.Remove level |> ignore

                    true

                else
                    false
            | _ ->
                false



type UpdateKind =
    | Untouched = 0
    | ContentChanged = 1
    | HandleChanged = 2

type IResource =
    inherit IDisposable

    abstract member Level : int
    abstract member Inputs : list<IResource>
    abstract member Outputs : ref<hset<IResource>>
    abstract member Update : unit -> UpdateKind
    abstract member NeedsUpdate : input : IResource * inputFlags : UpdateKind -> bool
    abstract member LockedInputs : hset<ILockedResource>

    [<CLIEvent>]
    abstract member OnDispose : IEvent<EventHandler, EventArgs>
    abstract member AddReference : unit -> unit

type IResource<'h> =
    inherit IResource
    abstract member Handle : 'h

type INativeResource<'n when 'n : unmanaged> =
    inherit IResource
    abstract member Pointer : nativeptr<'n>

type IResource<'h, 'n when 'n : unmanaged> =
    inherit IResource<'h>
    inherit INativeResource<'n>


type IUserResource =
    inherit IResource
    abstract member Subscribe : (unit -> unit) -> IDisposable

[<AbstractClass; Sealed; Extension>]
type IResourceExtensions private() =
    [<Extension>]
    static member AddOutput(this : IResource, output : IResource) =
        Interlocked.Change(&this.Outputs.contents, fun o -> HSet.add output o) |> ignore
        
    [<Extension>]
    static member RemoveOutput(this : IResource, output : IResource) =
        Interlocked.Change(&this.Outputs.contents, fun o -> HSet.remove output o) |> ignore




[<AbstractClass>]
type AbstractResource private(level : int, inputs : list<IResource>) as this =
    let outputs = ref HSet.empty

    let onDispose = Event<EventHandler, EventArgs>()

    let mutable level = level
    let mutable needsUpdate = false
    let mutable refCount = 0

    do for i in inputs do
        if i.Level >= level then level <- 1 + i.Level
        i.AddOutput this


    member x.Level = level
    member x.Inputs = inputs
    member x.Outputs = outputs

    member x.AddReference() = Interlocked.Increment(&refCount) |> ignore

    abstract member LockedInputs : hset<ILockedResource>
    abstract member InputChanged : input : IResource * inputFlags : UpdateKind -> bool
    abstract member PerformUpdate : unit -> UpdateKind
    abstract member Destroy : unit -> unit
    
    [<CLIEvent>]
    member x.OnDispose = onDispose.Publish

    member x.Update() =
        if needsUpdate then
            needsUpdate <- false
            x.PerformUpdate()
        else
            UpdateKind.Untouched

    member x.NeedsUpdate(input : IResource, inputFlags : UpdateKind) =
        let n = x.InputChanged(input, inputFlags)
        needsUpdate <- needsUpdate || n
        needsUpdate

    member x.AddOutput(o : IResource) =
        outputs := HSet.add o !outputs

    member private x.Dispose(disposing : bool) =
        if disposing then GC.SuppressFinalize x
        else Log.warn "%A was not disposed properly" x

        if Interlocked.Decrement(&refCount) = 0 then
            for i in inputs do i.RemoveOutput x
            outputs := HSet.empty
            onDispose.Trigger(null, null)
            x.Destroy()

    member x.Dispose() = x.Dispose true
    override x.Finalize() = x.Dispose false

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface IResource with
        member x.Level = x.Level
        member x.Inputs = inputs
        member x.Outputs = x.Outputs
        member x.Update() = x.Update()
        member x.NeedsUpdate(i,f) = x.NeedsUpdate(i,f)
        member x.LockedInputs = x.LockedInputs
        
        [<CLIEvent>]
        member x.OnDispose = x.OnDispose
        member x.AddReference() = x.AddReference()

    new(inputs : seq<IResource>) =
        let inputs = Seq.toList inputs
        new AbstractResource(0, inputs)

    new(level : int) =
        new AbstractResource(level, [])

[<AbstractClass>]
type AbstractResource<'h> =
    class
        inherit AbstractResource

        abstract member Handle : 'h

        interface IResource<'h> with
            member x.Handle = x.Handle

        new(level : int) = { inherit AbstractResource(level) }
        new(inputs : seq<IResource>) = { inherit AbstractResource(inputs) }
    end

[<AbstractClass>]
type AbstractResource<'h, 'n when 'n : unmanaged> =
    class
        inherit AbstractResource<'h>
        
        interface INativeResource<'n> with
            member x.Pointer = x.Pointer

        interface IResource<'h, 'n>

        abstract member Pointer : nativeptr<'n>

        new(level : int) = { inherit AbstractResource<'h>(level) }
        new(inputs : seq<IResource>) = { inherit AbstractResource<'h>(inputs) }
    end


[<AbstractClass>]
type AdaptiveResource() =
    inherit AbstractResource(0)

    [<ThreadStatic; DefaultValue>]
    static val mutable private currentToken : Option<AdaptiveToken>

    let self = AdaptiveObject()
    let callbacks = HashSet<unit -> unit>()

    let marked() =
        lock callbacks (fun () -> 
            for cb in callbacks do cb()
        )

    
    let mutable subscription = None //self.AddMarkingCallback marked

    let subscribe(cb : unit -> unit) =
        lock callbacks (fun () ->
            if callbacks.Add cb then
                if callbacks.Count = 1 then 
                    subscription <- Some (self.AddMarkingCallback marked)
        )

    let unsubscribe(cb : unit -> unit) =
        lock callbacks (fun () ->
            if callbacks.Remove cb then
                if callbacks.Count = 0 then
                    subscription.Value.Dispose()
                    subscription <- None
        )

    static member CurrentToken =
        match AdaptiveResource.currentToken with
            | Some t -> t
            | None -> AdaptiveToken.Top

    static member Evaluate(t : AdaptiveToken, f : unit -> 'a) =
        let old = AdaptiveResource.currentToken
        AdaptiveResource.currentToken <- Some t 
        try f()
        finally AdaptiveResource.currentToken <- old

    member x.DestroyCallbacks() =
        lock callbacks (fun () -> 
            match subscription with
                | Some s -> 
                    s.Dispose()
                    callbacks.Clear()
                | _ ->
                    ()
        )

        

    // abstract member LockedInputs : hset<ILockedResource>
    // abstract member Destroy : unit -> unit
    abstract member PerformUpdate : AdaptiveToken -> UpdateKind

    override x.PerformUpdate() =
        self.EvaluateIfNeeded AdaptiveResource.CurrentToken UpdateKind.Untouched (fun t ->
            x.PerformUpdate t
        )

    override x.InputChanged(_,_) =
        failwith "[AdaptiveResource] cannot be output of another resource"

    interface IUserResource with
        member x.Subscribe(cb : unit -> unit) =
            let s = subscribe cb
            lock self (fun () ->
                if self.OutOfDate then cb()
            )

            { new IDisposable with member x.Dispose() = unsubscribe cb }

[<AbstractClass>]
type AdaptiveResource<'h>() =
    inherit AdaptiveResource()

    abstract member Handle : 'h
    interface IResource<'h> with
        member x.Handle = x.Handle

[<AbstractClass>]
type AdaptiveResource<'h, 'n when 'n : unmanaged>() =
    inherit AdaptiveResource<'h>()

    abstract member Pointer : nativeptr<'n>
    interface INativeResource<'n> with
        member x.Pointer = x.Pointer
        
    interface IResource<'h, 'n>

module NativePtr =
    let create (value : 'a) =
        let ptr = NativePtr.alloc 1
        NativePtr.write ptr value
        ptr

open Aardvark.Rendering.Vulkan

type AdaptiveDescriptor =
    | AdaptiveUniformBuffer of int * IResource<UniformBuffer>
    | AdaptiveCombinedImageSampler of int * array<Option<IResource<ImageView> * IResource<Sampler>>>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AdaptiveDescriptor =
    let resources (d : AdaptiveDescriptor) =
        match d with
            | AdaptiveUniformBuffer(_,r) -> Seq.singleton (r :> IResource)
            | AdaptiveCombinedImageSampler(_,arr) ->
                arr |> Seq.collect (function Some(i,s) -> seq { yield i :> IResource; yield s :> IResource } | _ -> Seq.empty)






// level 0 resources

[<AbstractClass>]
type AdaptiveRecreateResource<'a, 'h, 'n when 'h : equality and 'n : unmanaged>(input : IMod<'a>) as this =
    inherit AdaptiveResource<'h, 'n>()
    
    let mutable pointer = NativePtr.create Unchecked.defaultof<'n>
    let mutable last : Option<'a * 'h> = None
    let mutable locks = HSet.empty
    let mutable ownsHandle = false

    do this.update AdaptiveResource.CurrentToken |> ignore           

    member private x.update(token : AdaptiveToken) : UpdateKind =
        let current = input.GetValue token
        let currentObj = current :> obj

        match currentObj with
            | :? ILockedResource as l ->
                locks <- HSet.ofList [l]
            | _ ->
                locks <- HSet.empty

        match currentObj with
            | :? 'h as handle ->
                match last with
                    | Some (_,o) when o = handle && not ownsHandle ->
                        UpdateKind.Untouched

                    | Some (_,o) -> 
                        if ownsHandle then
                            x.Destroy o
                            ownsHandle <- false
                        last <- Some (current, handle)
                        NativePtr.write pointer (x.View handle)
                        UpdateKind.HandleChanged

                    | None ->
                        ownsHandle <- false
                        last <- Some (current, handle)
                        NativePtr.write pointer (x.View handle)
                        UpdateKind.HandleChanged
            | _ ->
                match last with
                    | None ->
                        let n = x.Create current
                        ownsHandle <- true
                        last <- Some (current, n)
                        NativePtr.write pointer (x.View n)
                        UpdateKind.HandleChanged
                    | Some (oi, oh) when Unchecked.equals oi current && ownsHandle ->
                        UpdateKind.Untouched
                    | Some (oi, oh) ->
                        if ownsHandle then x.Destroy oh

                        let n = x.Create current
                        ownsHandle <- true
                        last <- Some (current, n)
                        NativePtr.write pointer (x.View n)

                        UpdateKind.HandleChanged
             
    abstract member Destroy : 'h -> unit
    abstract member Create : 'a -> 'h
    abstract member View : 'h -> 'n

    override x.LockedInputs = locks
    override x.Handle =
        match last with
            | Some (_,h) -> h
            | None -> failwith "cannot access disposed resource"

    override x.Pointer = pointer
        
    override x.Destroy() =
        match last with
            | Some(_,h) ->
                if ownsHandle then x.Destroy h
                last <- None
                locks <- HSet.empty
                ownsHandle <- false
                NativePtr.free pointer
            | _ ->
                ()

    override x.PerformUpdate t = x.update t
  
[<AbstractClass>]
type AdaptiveRecreateResource<'a, 'b, 'h, 'n when 'h : equality and 'n : unmanaged>(ia : IMod<'a>, ib : IMod<'b>) as this =
    inherit AdaptiveResource<'h, 'n>()
    
    let mutable pointer = NativePtr.create Unchecked.defaultof<'n>
    let mutable last : Option<'a * 'b * 'h> = None
    do this.update AdaptiveResource.CurrentToken |> ignore           

    member private x.update(token : AdaptiveToken) : UpdateKind =
        let ca = ia.GetValue token
        let cb = ib.GetValue token

        match last with
            | None ->
                let n = x.Create(ca, cb)
                last <- Some (ca, cb, n)
                NativePtr.write pointer (x.View n)
                UpdateKind.HandleChanged
            | Some (oa, ob, oh) when Unchecked.equals oa ca && Unchecked.equals ob cb ->
                UpdateKind.Untouched
            | Some (oa, ob, oh) ->
                x.Destroy oh
                let n = x.Create(ca, cb)
                last <- Some (ca, cb, n)
                NativePtr.write pointer (x.View n)

                UpdateKind.HandleChanged
             
    abstract member Destroy : 'h -> unit
    abstract member Create : 'a * 'b -> 'h
    abstract member View : 'h -> 'n

    override x.LockedInputs = HSet.empty
    override x.Handle =
        match last with
            | Some (_,_,h) -> h
            | None -> failwith "cannot access disposed resource"

    override x.Pointer = pointer
        
    override x.Destroy() =
        match last with
            | Some(_,_,h) ->
                x.Destroy h
                last <- None
                NativePtr.free pointer
            | _ ->
                ()

    override x.PerformUpdate t = x.update t
   
[<AbstractClass>]
type AdaptiveRecreateResource<'a, 'b, 'c, 'h, 'n when 'h : equality and 'n : unmanaged>(ia : IMod<'a>, ib : IMod<'b>, ic : IMod<'c>) as this =
    inherit AdaptiveResource<'h, 'n>()
    
    let mutable pointer = NativePtr.create Unchecked.defaultof<'n>
    let mutable last : Option<'a * 'b * 'c * 'h> = None
    do this.update AdaptiveResource.CurrentToken |> ignore           

    member private x.update(token : AdaptiveToken) : UpdateKind =
        let ca = ia.GetValue token
        let cb = ib.GetValue token
        let cc = ic.GetValue token

        match last with
            | None ->
                let n = x.Create(ca, cb, cc)
                last <- Some (ca, cb, cc, n)
                NativePtr.write pointer (x.View n)
                UpdateKind.HandleChanged
            | Some (oa, ob, oc, oh) when Unchecked.equals oa ca && Unchecked.equals ob cb && Unchecked.equals oc cc ->
                UpdateKind.Untouched
            | Some (_,_,_,oh) ->
                x.Destroy oh
                let n = x.Create(ca, cb, cc)
                last <- Some (ca, cb, cc, n)
                NativePtr.write pointer (x.View n)

                UpdateKind.HandleChanged
             
    abstract member Destroy : 'h -> unit
    abstract member Create : 'a * 'b * 'c -> 'h
    abstract member View : 'h -> 'n

    override x.LockedInputs = HSet.empty
    override x.Handle =
        match last with
            | Some (_,_,_,h) -> h
            | None -> failwith "cannot access disposed resource"

    override x.Pointer = pointer
        
    override x.Destroy() =
        match last with
            | Some(_,_,_,h) ->
                x.Destroy h
                last <- None
                NativePtr.free pointer
            | _ ->
                ()

    override x.PerformUpdate t = x.update t 

type AdaptiveBufferResource(device : Device, usage : VkBufferUsageFlags, input : IMod<IBuffer>) =
    inherit AdaptiveRecreateResource<IBuffer, Buffer, VkBuffer>(input) 
    
    override x.Create (b : IBuffer) = device.CreateBuffer(usage, b)
    override x.Destroy (b : Buffer) = device.Delete b
    override x.View (b : Buffer) = b.Handle

type AdaptiveIndirectBufferResource(device : Device, indexed : bool, input : IMod<IIndirectBuffer>) =
    inherit AdaptiveRecreateResource<IIndirectBuffer, IndirectBuffer, VkBuffer>(input) 

    override x.Create (b : IIndirectBuffer) = device.CreateIndirectBuffer(indexed, b)
    override x.Destroy (b : IndirectBuffer) = device.Delete b
    override x.View (b : IndirectBuffer) = b.Handle


type AdaptiveUniformBufferResource(device : Device, layout : UniformBufferLayout, writers : list<IMod * UniformWriters.IWriter>) =
    inherit AdaptiveResource<UniformBuffer, VkBuffer>()

    let buffer = device.CreateUniformBuffer(layout)
    do  let t = AdaptiveResource.CurrentToken
        for (m,w) in writers do w.Write(t, m, buffer.Storage.Pointer)

    let pointer = NativePtr.create buffer.Handle

    override x.LockedInputs = HSet.empty
    override x.Handle = buffer
    override x.Pointer = pointer
    override x.PerformUpdate(t : AdaptiveToken) =
        for (m,w) in writers do w.Write(t, m, buffer.Storage.Pointer)
        UpdateKind.ContentChanged

    override x.Destroy() =
        device.Delete buffer

type AdaptiveImageResource(device : Device, input : IMod<ITexture>) =
    inherit AdaptiveRecreateResource<ITexture, Image, VkImage>(input) 
    
    override x.Create (b : ITexture) = device.CreateImage(b)
    override x.Destroy (b : Image) = device.Delete b
    override x.View (b : Image) = b.Handle

type AdaptiveSamplerResource(device : Device, input : IMod<SamplerStateDescription>) =
    inherit AdaptiveRecreateResource<SamplerStateDescription, Sampler, VkSampler>(input) 
    
    override x.Create (b : SamplerStateDescription) = device.CreateSampler(b)
    override x.Destroy (b : Sampler) = device.Delete b
    override x.View (b : Sampler) = b.Handle

type AdaptiveShaderProgramResource(device : Device, renderPass : RenderPass, input : IMod<ISurface>) =
    inherit AdaptiveRecreateResource<ISurface, ShaderProgram, nativeint>(input) 
        
    override x.Create (b : ISurface) = device.CreateShaderProgram(renderPass, b)
    override x.Destroy (b : ShaderProgram) = device.Delete b
    override x.View (b : ShaderProgram) = 0n


type AdaptiveInputAssemblyStateResource(input : IMod<IndexedGeometryMode>) =
    inherit AdaptiveRecreateResource<IndexedGeometryMode, VkPipelineInputAssemblyStateCreateInfo, VkPipelineInputAssemblyStateCreateInfo>(input)

    override x.Create m = 
        let res = InputAssemblyState.ofIndexedGeometryMode m
        VkPipelineInputAssemblyStateCreateInfo(
            VkStructureType.PipelineInputAssemblyStateCreateInfo, 0n,
            VkPipelineInputAssemblyStateCreateFlags.MinValue,
            res.topology,
            (if res.restartEnable then 1u else 0u)
        )
        
    override x.Destroy _ = ()
    override x.View h = h
    
type AdaptiveDepthStencilStateResource(depthWrite : bool, depth : IMod<DepthTestMode>, stencil : IMod<StencilMode>) =
    inherit AdaptiveRecreateResource<DepthTestMode, StencilMode, VkPipelineDepthStencilStateCreateInfo, VkPipelineDepthStencilStateCreateInfo>(depth, stencil)

    override x.Create(depth, stencil) =
        let depth = DepthState.create depthWrite depth
        let stencil = StencilState.create stencil

        VkPipelineDepthStencilStateCreateInfo(
            VkStructureType.PipelineDepthStencilStateCreateInfo, 0n,
            VkPipelineDepthStencilStateCreateFlags.MinValue,
            (if depth.testEnabled then 1u else 0u),
            (if depth.writeEnabled then 1u else 0u),
            depth.compare,
            (if depth.boundsTest then 1u else 0u),
            (if stencil.enabled then 1u else 0u),
            stencil.front,
            stencil.back,
            float32 depth.depthBounds.Min,
            float32 depth.depthBounds.Max
        )

    override x.Destroy _ = ()
    override x.View a = a

type AdaptiveRasterizerStateResource(depth : IMod<DepthTestMode>, cull : IMod<CullMode>, fill : IMod<FillMode>) =
    inherit AdaptiveRecreateResource<DepthTestMode, CullMode, FillMode, VkPipelineRasterizationStateCreateInfo, VkPipelineRasterizationStateCreateInfo>(depth, cull, fill)

    override x.Create(depth, cull, fill) =
        let state = RasterizerState.create false depth cull fill

        VkPipelineRasterizationStateCreateInfo(
            VkStructureType.PipelineRasterizationStateCreateInfo, 0n,
            VkPipelineRasterizationStateCreateFlags.MinValue,
            (if state.depthClampEnable then 1u else 0u),
            0u,
            state.polygonMode,
            state.cullMode,
            state.frontFace,
            (if state.depthBiasEnable then 1u else 0u),
            float32 state.depthBiasConstantFactor,
            float32 state.depthBiasClamp,
            float32 state.depthBiasSlopeFactor,
            float32 state.lineWidth
        )

    override x.Destroy _ = ()
    override x.View a = a

type AdaptiveColorBlendStateResource(writeMasks : bool[], blend : IMod<BlendMode>) =
    inherit AdaptiveRecreateResource<BlendMode, VkPipelineColorBlendStateCreateInfo, VkPipelineColorBlendStateCreateInfo>(blend)

    override x.Create(blend) =
        let state = ColorBlendState.create writeMasks writeMasks.Length blend
        let pAttStates = NativePtr.alloc writeMasks.Length

        for i in 0 .. state.attachmentStates.Length - 1 do
            let s = state.attachmentStates.[i]
            let att = 
                VkPipelineColorBlendAttachmentState(
                    (if s.enabled then 1u else 0u),
                    s.srcFactor,
                    s.dstFactor,
                    s.operation,
                    s.srcFactorAlpha,
                    s.dstFactorAlpha,
                    s.operationAlpha,
                    s.colorWriteMask
                )
            NativePtr.set pAttStates i att


        VkPipelineColorBlendStateCreateInfo(
            VkStructureType.PipelineColorBlendStateCreateInfo, 0n,
            VkPipelineColorBlendStateCreateFlags.MinValue,
            (if state.logicOpEnable then 1u else 0u),
            state.logicOp,
            uint32 writeMasks.Length,
            pAttStates,
            state.constants
        )

    override x.Destroy h =
        NativePtr.free h.pAttachments

    override x.View h = h

type AdaptiveDirectDrawCallResource(indexed : bool, calls : IMod<list<DrawCallInfo>>) =
    inherit AdaptiveRecreateResource<list<DrawCallInfo>, DrawCall, DrawCall>(calls)

    override x.Create(calls : list<DrawCallInfo>) =
        DrawCall.Direct(indexed, List.toArray calls)

    override x.Destroy h = h.Dispose()
    override x.View a = a
    

// derived resources


type DescriptorSetResource(pool : DescriptorPool, layout : DescriptorSetLayout, bindings : AdaptiveDescriptor[]) =
    inherit AbstractResource<DescriptorSet, VkDescriptorSet>(bindings |> Seq.collect AdaptiveDescriptor.resources)

    let mutable set = pool.Alloc(layout)
    let mutable pointer = NativePtr.alloc 1
    do NativePtr.write pointer set.Handle

    let write() =
        let descriptors =
            bindings |> Array.map (fun b ->
                match b with
                    | AdaptiveUniformBuffer(slot, b) ->
                        Descriptor.UniformBuffer(slot, b.Handle)
                    | AdaptiveCombinedImageSampler(slot, args) ->
                        let args = args |> Array.map (Option.map (fun (i,s) -> i.Handle, s.Handle))
                        Descriptor.CombinedImageSampler(slot, args)
            )
        pool.Update(set, descriptors)

    do write()

    override x.Handle = set

    override x.Pointer = pointer

    override x.LockedInputs =
        HSet.empty

    override x.Destroy() =
        pool.Free(set)
        set <- Unchecked.defaultof<_>
        NativePtr.free pointer
        pointer <- NativePtr.zero


    override x.InputChanged(input : IResource, kind : UpdateKind) =
        kind = UpdateKind.HandleChanged

    override x.PerformUpdate() =
        write()
        // CMD buffers need to be re-filled when descriptorsets get updated
        UpdateKind.HandleChanged

type PipelineResource(  program : IResource<ShaderProgram>, 
                        vertexInputState : Map<Symbol, VertexInputDescription>, 
                        inputState : INativeResource<VkPipelineVertexInputStateCreateInfo>,
                        inputAssembly : INativeResource<VkPipelineInputAssemblyStateCreateInfo>,
                        rasterizerState : INativeResource<VkPipelineRasterizationStateCreateInfo>,
                        colorBlendState : INativeResource<VkPipelineColorBlendStateCreateInfo>,
                        multisample : MultisampleState,
                        depthStencil : INativeResource<VkPipelineDepthStencilStateCreateInfo>
                     ) =
    inherit AbstractResource<Pipeline, VkPipeline>(Seq.ofList [program :> IResource; inputState :> _; inputAssembly :> _; rasterizerState :> _; colorBlendState :> _; depthStencil :> _])
    static let check str err =
        if err <> VkResult.VkSuccess then failwithf "[Vulkan] %s" str


    let device = program.Handle.Device

    let create () =
        let prog = program.Handle

        prog.ShaderCreateInfos |> NativePtr.withA (fun pShaderCreateInfos ->

            let mutable viewportState =
                let vp = prog.RenderPass.AttachmentCount
                VkPipelineViewportStateCreateInfo(
                    VkStructureType.PipelineViewportStateCreateInfo, 0n,
                    VkPipelineViewportStateCreateFlags.MinValue,
                
                    uint32 vp,
                    NativePtr.zero,

                    uint32 vp,
                    NativePtr.zero
                )

            let pSampleMasks = NativePtr.pushStackArray multisample.sampleMask
            let mutable multisampleState =
                let ms = multisample
                VkPipelineMultisampleStateCreateInfo(
                    VkStructureType.PipelineMultisampleStateCreateInfo, 0n,
                    VkPipelineMultisampleStateCreateFlags.MinValue,
                
                    unbox ms.samples,
                    (if ms.sampleShadingEnable then 1u else 0u),
                    float32 ms.minSampleShading,
                    pSampleMasks,
                    (if ms.alphaToCoverageEnable then 1u else 0u),
                    (if ms.alphaToOneEnable then 1u else 0u)
                )
            
            let dynamicStates = [| VkDynamicState.Viewport; VkDynamicState.Scissor |]
        
            let pDynamicStates = NativePtr.pushStackArray dynamicStates
            
            let mutable dynamicStates =
                VkPipelineDynamicStateCreateInfo(
                    VkStructureType.PipelineDynamicStateCreateInfo, 0n,
                    VkPipelineDynamicStateCreateFlags.MinValue, 

                    uint32 dynamicStates.Length,
                    pDynamicStates
                )

            // TODO: tessellation input-patch-size

            let mutable desc =
                VkGraphicsPipelineCreateInfo(
                    VkStructureType.GraphicsPipelineCreateInfo, 0n,
                    VkPipelineCreateFlags.None,
                    uint32 prog.ShaderCreateInfos.Length,
                    pShaderCreateInfos,
                    inputState.Pointer,
                    inputAssembly.Pointer,
                    NativePtr.zero, // tessellation
                    &&viewportState,
                    rasterizerState.Pointer,
                    &&multisampleState,
                    depthStencil.Pointer,
                    colorBlendState.Pointer,
                    &&dynamicStates, //dynamic
                    prog.PipelineLayout.Handle,
                    prog.RenderPass.Handle,
                    0u,
                    VkPipeline.Null,
                    0
                )

            let mutable handle = VkPipeline.Null
            VkRaw.vkCreateGraphicsPipelines(device.Handle, VkPipelineCache.Null, 1u, &&desc, NativePtr.zero, &&handle)
                |> check "could not create pipeline"

            Pipeline(device, handle, Unchecked.defaultof<_>)
        )

    let mutable current = create()
    let pointer = NativePtr.create current.Handle

    override x.PerformUpdate() =
        device.Delete current
        current <- create()
        NativePtr.write pointer current.Handle
        UpdateKind.HandleChanged

    override x.Destroy() =
        device.Delete current
        NativePtr.free pointer

    override x.Handle = current
    override x.Pointer = pointer

    override x.InputChanged(i : IResource, k : UpdateKind) =
        true

    override x.LockedInputs = HSet.empty

type IndirectDrawCallResource(indexed : bool, calls : IResource<IndirectBuffer>) =
    inherit AbstractResource<DrawCall, DrawCall>([calls :> IResource])

    let mutable call = DrawCall.Indirect(indexed, calls.Handle)
    let mutable pointer = NativePtr.create call

    override x.PerformUpdate() =
        call.Dispose()
        call <- DrawCall.Indirect(indexed, calls.Handle)
        NativePtr.write pointer call
        UpdateKind.HandleChanged

    override x.Destroy() = 
        call.Dispose()
        NativePtr.free pointer

    override x.Handle = call
    override x.Pointer = pointer

    override x.LockedInputs = HSet.empty
    override x.InputChanged(_,_) = true

type BufferBindingResource(buffers : list<IResource<Buffer> * int64>) =
    inherit AbstractResource<VertexBufferBinding, VertexBufferBinding>(buffers |> Seq.map fst |> Seq.cast)

    let create() =
        let bindings = buffers |> List.map (fun (r,o) -> r.Handle, o) |> List.toArray
        new VertexBufferBinding(0, bindings)

    let mutable handle = create()
    let pointer = NativePtr.create handle

    override x.PerformUpdate() =
        handle.Dispose()
        handle <- create()
        NativePtr.write pointer handle
        UpdateKind.HandleChanged

    override x.Destroy() =
        handle.Dispose()
        NativePtr.free pointer

    override x.InputChanged(r : IResource, kind : UpdateKind) =
        kind = UpdateKind.HandleChanged

    override x.LockedInputs = HSet.empty
    override x.Handle = handle
    override x.Pointer = pointer

type DescriptorSetBindingResource(layout : PipelineLayout, sets : list<IResource<DescriptorSet>>) =
    inherit AbstractResource<DescriptorSetBinding, DescriptorSetBinding>(Seq.cast sets)

    let create() =
        let sets = sets |> List.map (fun r -> r.Handle) |> List.toArray
        new DescriptorSetBinding(layout, 0, sets)

    let mutable handle = create()
    let pointer = NativePtr.create handle

    override x.PerformUpdate() =
        handle.Dispose()
        handle <- create()
        NativePtr.write pointer handle
        UpdateKind.HandleChanged

    override x.Destroy() =
        handle.Dispose()
        NativePtr.free pointer

    override x.InputChanged(_,_) = true

    override x.Handle = handle
    override x.Pointer = pointer
    override x.LockedInputs = HSet.empty
 
type IndexBufferBindingResource(indexType : VkIndexType, index : IResource<Buffer>) =
    inherit AbstractResource<IndexBufferBinding, IndexBufferBinding>([index :> IResource])

    let mutable handle = IndexBufferBinding(index.Handle.Handle, indexType)
    let pointer = NativePtr.create handle

    override x.PerformUpdate() =
        handle <- IndexBufferBinding(index.Handle.Handle, indexType)
        NativePtr.write pointer handle
        UpdateKind.HandleChanged

    override x.Destroy() =
        NativePtr.free pointer

    override x.InputChanged(r : IResource, k : UpdateKind) =
        k = UpdateKind.HandleChanged

    override x.Handle = handle
    override x.Pointer = pointer
    override x.LockedInputs = HSet.empty

type ResourceCache() =
    let entries = System.Collections.Concurrent.ConcurrentDictionary<list<obj>, IResource>()

    member x.GetOrCreate(key : list<obj>, create : unit -> #IResource<'h, 'n>) =
        let result = 
            entries.GetOrAdd(key, fun _ ->
                create() :> IResource
            )

        result.AddReference()
        result.OnDispose.Add(fun _ -> entries.TryRemove key |> ignore)
        result |> unbox<IResource<'h, 'n>>


type ResourceManager(device : Device) =
    let descriptorPool = device.CreateDescriptorPool(1 <<< 22, 1 <<< 22)

    let bufferCache             = ResourceCache()
    let indirectBufferCache     = ResourceCache()
    let indexBufferCache        = ResourceCache()
    let descriptorSetCache      = ResourceCache()
    let uniformBufferCache      = ResourceCache()
    let imageCache              = ResourceCache()
    let samplerCache            = ResourceCache()
    let programCache            = ResourceCache()

    let inputAssemblyCache      = ResourceCache()
    let depthStencilCache       = ResourceCache()
    let rasterizerStateCache    = ResourceCache()
    let colorBlendStateCache    = ResourceCache()
    let pipelineCache           = ResourceCache()

    let drawCallCache           = ResourceCache()
    let bufferBindingCache      = ResourceCache()
    let descriptorBindingCache  = ResourceCache()
    let indexBindingCache       = ResourceCache()



    member private x.DescriptorPool : DescriptorPool = descriptorPool

    member x.Device = device

    member x.CreateRenderPass(signature : Map<Symbol, AttachmentSignature>) =
        device.CreateRenderPass(signature)

    member x.CreateBuffer(input : IMod<IBuffer>) =
        bufferCache.GetOrCreate([input :> obj], fun () -> new AdaptiveBufferResource(device, VkBufferUsageFlags.VertexBufferBit, input)) :> IResource<_>

    member x.CreateIndexBuffer(input : IMod<IBuffer>) =
        indexBufferCache.GetOrCreate([input :> obj], fun () -> new AdaptiveBufferResource(device, VkBufferUsageFlags.IndexBufferBit, input)) :> IResource<_>
        
    member x.CreateIndirectBuffer(indexed : bool, input : IMod<IIndirectBuffer>) =
        indirectBufferCache.GetOrCreate([indexed :> obj; input :> obj], fun () -> new AdaptiveIndirectBufferResource(device, indexed, input)) :> IResource<_>

    member x.CreateImage(data : IMod<ITexture>) =
        imageCache.GetOrCreate([data :> obj], fun () -> new AdaptiveImageResource(device, data)) :> IResource<_>
        
    member x.CreateSampler(data : IMod<SamplerStateDescription>) =
        samplerCache.GetOrCreate([data :> obj], fun () -> new AdaptiveSamplerResource(device, data)) :> IResource<_>
        
    member x.CreateShaderProgram(renderPass : RenderPass, data : IMod<ISurface>) =
        programCache.GetOrCreate([data :> obj], fun () -> new AdaptiveShaderProgramResource(device, renderPass, data)) :> IResource<_>

    member x.CreateUniformBuffer(scope : Ag.Scope, layout : UniformBufferLayout, u : IUniformProvider, additional : SymbolDict<IMod>) =
        let values =
            layout.fields 
            |> List.map (fun (f) ->
                let sem = Symbol.Create f.name
                match Uniforms.tryGetDerivedUniform f.name u with
                    | Some r -> f, r
                    | None -> 
                        match u.TryGetUniform(scope, sem) with
                            | Some v -> f, v
                            | None -> 
                                match additional.TryGetValue sem with
                                    | (true, m) -> f, m
                                    | _ -> failwithf "[Vulkan] could not get uniform: %A" f
            )

        let writers = 
            values |> List.map (fun (target, m) ->
                match m.GetType() with
                    | ModOf tSource -> m, UniformWriters.getWriter target.offset target.fieldType tSource
                    | t -> failwithf "[UniformBuffer] unexpected input-type %A" t
            )

        let key = (layout :> obj) :: (values |> List.map (fun (_,v) -> v :> obj))
        
        uniformBufferCache.GetOrCreate(key, fun () -> new AdaptiveUniformBufferResource(device, layout, writers)) :> IResource<_>

    member x.CreateDescriptorSet(layout : DescriptorSetLayout, bindings : list<AdaptiveDescriptor>) =
        descriptorSetCache.GetOrCreate([layout :> obj; bindings :> obj], fun () -> new DescriptorSetResource(descriptorPool, layout, List.toArray bindings)) :> IResource<_>

    member x.CreateInputAssemblyState(mode : IMod<IndexedGeometryMode>) =
        inputAssemblyCache.GetOrCreate([mode :> obj], fun () -> new AdaptiveInputAssemblyStateResource(mode)) :> INativeResource<_>
        
    member x.CreateDepthStencilState(depthWrite : bool, depth : IMod<DepthTestMode>, stencil : IMod<StencilMode>) =
        depthStencilCache.GetOrCreate([depthWrite :> obj; depth :> obj; stencil :> obj], fun () -> new AdaptiveDepthStencilStateResource(depthWrite, depth, stencil)) :> INativeResource<_>

    member x.CreateRasterizerState(depth : IMod<DepthTestMode>, cull : IMod<CullMode>, fill : IMod<FillMode>) =
        rasterizerStateCache.GetOrCreate([depth :> obj; cull :> obj; fill :> obj], fun () -> new AdaptiveRasterizerStateResource(depth, cull, fill)) :> INativeResource<_>
        
    member x.CreateColorBlendState(pass : RenderPass, writeBuffers : Option<Set<Symbol>>, blend : IMod<BlendMode>) =
        colorBlendStateCache.GetOrCreate(
            [pass :> obj; writeBuffers :> obj; blend :> obj], 
            fun () -> 
                let writeBuffers =
                    match writeBuffers with
                        | Some set -> 
                            if Set.isSuperset set pass.Semantics then pass.Semantics
                            else set
                        | None ->
                            pass.Semantics

                let writeMasks = Array.zeroCreate pass.ColorAttachmentCount
                for (i, (sem,_)) in Map.toSeq pass.ColorAttachments do 
                    if Set.contains sem writeBuffers then writeMasks.[i] <- true
                    else writeMasks.[i] <- false

                new AdaptiveColorBlendStateResource(writeMasks, blend)
        ) :> INativeResource<_>

    member x.CreatePipeline(program         : IResource<ShaderProgram>, 
                            inputs          : Map<Symbol, bool * Aardvark.Base.BufferView>,
                            inputState      : INativeResource<VkPipelineVertexInputStateCreateInfo>,
                            inputAssembly   : INativeResource<VkPipelineInputAssemblyStateCreateInfo>,
                            rasterizerState : INativeResource<VkPipelineRasterizationStateCreateInfo>,
                            colorBlendState : INativeResource<VkPipelineColorBlendStateCreateInfo>,
                            depthStencil    : INativeResource<VkPipelineDepthStencilStateCreateInfo>,
                            writeBuffers    : Option<Set<Symbol>>
                        ) =
        let pass = program.Handle.RenderPass

        let anyAttachment = 
            match pass.ColorAttachments |> Map.toSeq |> Seq.tryHead with
                | Some (_,(_,a)) -> a
                | None -> pass.DepthStencilAttachment |> Option.get

        let inputs = VertexInputState.create inputs
        let ms = MultisampleState.create program.Handle.SampleShading anyAttachment.samples
        let key = [ program :> obj; inputs :> obj; inputState :> obj; inputAssembly :> obj; rasterizerState :> obj; colorBlendState :> obj; ms :> obj; depthStencil :> obj ]
        pipelineCache.GetOrCreate(
            key,
            fun () ->
                new PipelineResource(
                    program,
                    inputs,
                    inputState,
                    inputAssembly,
                    rasterizerState,
                    colorBlendState,
                    ms,
                    depthStencil
                )

        ) :> INativeResource<_>

    member x.CreateDrawCall(indexed : bool, calls : IMod<list<DrawCallInfo>>) =
        drawCallCache.GetOrCreate([indexed :> obj; calls :> obj], fun () -> new AdaptiveDirectDrawCallResource(indexed, calls)) :> INativeResource<_>

    member x.CreateDrawCall(indexed : bool, calls : IResource<IndirectBuffer>) =
        drawCallCache.GetOrCreate([indexed :> obj; calls :> obj], fun () -> new IndirectDrawCallResource(indexed, calls)) :> INativeResource<_>

    member x.CreateVertexBufferBinding(buffers : list<IResource<Buffer> * int64>) =
        bufferBindingCache.GetOrCreate([buffers :> obj], fun () -> new BufferBindingResource(buffers)) :> INativeResource<_>

    member x.CreateDescriptorSetBinding(layout : PipelineLayout, bindings : list<IResource<DescriptorSet>>) =
        descriptorBindingCache.GetOrCreate([layout :> obj; bindings :> obj], fun () -> new DescriptorSetBindingResource(layout, bindings)) :> INativeResource<_>

    member x.CreateIndexBufferBinding(binding : IResource<Buffer>, t : VkIndexType) =
        indexBindingCache.GetOrCreate([binding :> obj; t :> obj], fun () -> new IndexBufferBindingResource(t, binding)) :> INativeResource<_>

type ResourceState() =
    let locked = ReferenceCountingSet<ILockedResource>()

    member x.ReplaceLocks(o : hset<ILockedResource>, n : hset<ILockedResource>) =
        match HSet.isEmpty o, HSet.isEmpty n with
            | true, true -> ()
            | true, false -> locked.UnionWith n
            | false, true -> locked.ExceptWith o
            | false, false ->
                locked.ExceptWith o
                locked.UnionWith n

    member x.AddLocks(o : hset<ILockedResource>) =
        x.ReplaceLocks(HSet.empty, o)

    member x.RemoveLocks(o : hset<ILockedResource>) =
        x.ReplaceLocks(o, HSet.empty)

type ResourceSet() =
    inherit AdaptiveObject()

    let all = ReferenceCountingSet<IResource>()
    let state = ResourceState()

    let subscriptions = Dictionary<IUserResource, IDisposable>()
    let mutable dirtyLevel0 = HashSet<IResource>()

    let trigger (r : IResource) =
        lock subscriptions (fun () -> dirtyLevel0.Add r |> ignore)

    let update () =
        let queue = LevelQueue<IResource>()
        let dirty = lock subscriptions (fun () -> Interlocked.Exchange(&dirtyLevel0, HashSet()))
        for d in dirty do
            queue.Add(d.Level, d) |> ignore

        while not queue.IsEmpty do
            let elements = queue.Dequeue()

            for e in elements do
                let ol = e.LockedInputs
                let flags = e.Update()
                let nl = e.LockedInputs
                match flags with
                    | UpdateKind.HandleChanged -> 
                        state.ReplaceLocks(ol,nl)
                    | _ ->
                        ()

                for o in !e.Outputs do
                    if o.NeedsUpdate(e, flags) then
                        if all.Contains o then
                            queue.Add(o.Level, o) |> ignore
    
    member x.Use(t : AdaptiveToken, action : unit -> 'r) =
        x.EvaluateAlways t (fun t ->
            AdaptiveResource.Evaluate(t, action)
        )

    member x.Update(t : AdaptiveToken) =
        x.EvaluateIfNeeded t (fun t -> 
            AdaptiveResource.Evaluate(t, fun () ->
                update()
            )
        )

    member x.Add(r : IResource) =
        if all.Add r then
            state.AddLocks r.LockedInputs

            match r with
                | :? IUserResource as r ->
                    subscriptions.[r] <- r.Subscribe(fun () -> trigger r)
                | _ ->
                    ()

    member x.Remove(r : IResource) =
        if all.Remove r then
            state.RemoveLocks r.LockedInputs

            match r with
                | :? IUserResource as r ->
                    subscriptions.[r].Dispose()
                    subscriptions.Remove r |> ignore
                | _ ->
                    ()

                    










