namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Microsoft.FSharp.NativeInterop

#nowarn "9"
// #nowarn "51"

type ResourceInfo<'a> =
    {
        handle      : 'a
        version     : int
    }

type ImmutableResourceDescription<'a, 'b> =
    {
        icreate          : 'a -> 'b
        idestroy         : 'b -> unit
        ieagerDestroy    : bool
    }

type MutableResourceDescription<'a, 'b> =
    {
        mcreate          : 'a -> 'b
        mdestroy         : 'b -> unit
        mtryUpdate       : 'b -> 'a -> bool
    }

type IResourceLocation =
    inherit IAdaptiveObject
    abstract member Update : AdaptiveToken -> ResourceInfo<obj>
    abstract member Acquire : unit -> unit
    abstract member Release : unit -> unit
    abstract member ReleaseAll : unit -> unit
    abstract member ReferenceCount : int
    abstract member Key : list<obj>
    abstract member Owner : IResourceCache
    

and IResourceLocation<'a> =
    inherit IResourceLocation
    abstract member Update : AdaptiveToken -> ResourceInfo<'a>

and IResourceUser =
    abstract member AddLocked       : ILockedResource -> unit
    abstract member RemoveLocked    : ILockedResource -> unit
    
and IResourceCache =
    inherit IResourceUser
    abstract member Remove          : key : list<obj> -> unit

type INativeResourceLocation<'a when 'a : unmanaged> = 
    inherit IResourceLocation<'a>
    abstract member Pointer : nativeptr<'a>


[<AbstractClass>]
type AbstractResourceLocation<'a>(owner : IResourceCache, key : list<obj>) =
    inherit AdaptiveObject()
    
    let mutable refCount = 0

    abstract member Create : unit -> unit
    abstract member Destroy : unit -> unit
    abstract member GetHandle : AdaptiveToken -> ResourceInfo<'a>

    member x.RefCount = refCount

    member x.Acquire() =
        lock x (fun () ->
            inc &refCount
            if refCount = 1 then
                x.Create()
        )

    member x.Release() =
        lock x (fun () ->
            dec &refCount
            if refCount = 0 then
                owner.Remove key
                x.Destroy()
                x.Outputs.Clear()
                x.OutOfDate <- true
        )
  
    member x.ReleaseAll() =
        lock x (fun () ->
            refCount <- 0
            owner.Remove key
            x.Destroy()
            x.Outputs.Clear()
            x.OutOfDate <- true
        )

    member x.Update(token : AdaptiveToken) =
        x.EvaluateAlways token (fun token ->
            if refCount <= 0 then failwithf "[Resource] no ref count"
            x.GetHandle token
        )
        
    interface IResourceLocation with
        member x.ReferenceCount = refCount
        member x.Update t = 
            let res = x.Update t
            { handle = res :> obj; version = res.version }

        member x.Acquire() = x.Acquire()
        member x.Release() = x.Release()
        member x.ReleaseAll() = x.ReleaseAll()
        member x.Owner = owner
        member x.Key = key

    interface IResourceLocation<'a> with
        member x.Update t = x.Update t
//   
//[<AbstractClass>]
//type AbstractNativeResourceLocation<'a when 'a : unmanaged>(owner : IResourceCache, key : list<obj>) =
//    inherit AbstractResourceLocation<'a>(owner, key)
//    abstract member Pointer : nativeptr<'a>
//
//    interface INativeResourceLocation<'a> with
//        member x.Pointer = x.Pointer
//

[<AbstractClass; Sealed; Extension>]
type ModResourceExtensionStuff() =
    [<Extension>]
    static member inline Acquire(m : IMod<'a>) =
        match m with
            | :? IOutputMod<'a> as o -> o.Acquire()
            | _ -> ()

    [<Extension>]
    static member inline Release(m : IMod<'a>) =
        match m with
            | :? IOutputMod<'a> as o -> o.Release()
            | _ -> ()
            
    [<Extension>]
    static member inline GetLocked(a : 'a) =
        match a :> obj with
            | :? ILockedResource as l -> Some l
            | _ -> None

    [<Extension>]
    static member ReplaceLocked(owner : IResourceUser, o : Option<'a>, n : Option<'a>) =
        match n with
            | Some n -> 
                match n :> obj with
                    | :? ILockedResource as n -> owner.AddLocked n
                    | _ -> ()
            | None ->
                ()

        match o with
            | Some o -> 
                match o :> obj with
                    | :? ILockedResource as o -> owner.RemoveLocked o
                    | _ -> ()
            | None ->
                ()

type ImmutableResourceLocation<'a, 'h>(owner : IResourceCache, key : list<obj>, input : IMod<'a>, desc : ImmutableResourceDescription<'a, 'h>) =
    inherit AbstractResourceLocation<'h>(owner, key)
    
    let mutable handle : Option<'a * 'h> = None

    let recreate (token : AdaptiveToken) =
        let n = input.GetValue token

        match handle with
            | Some(o,h) when Unchecked.equals o n ->
                h
            | Some(o,h) ->
                owner.ReplaceLocked (Some o, Some n)

                desc.idestroy h
                let r = desc.icreate n
                handle <- Some(n,r)
                r
            | None ->
                owner.ReplaceLocked (None, Some n)

                let r = desc.icreate n
                handle <- Some(n,r)
                r
                

    override x.Mark() =
        if desc.ieagerDestroy then 
            match handle with
                | Some(_,h) -> 
                    desc.idestroy h
                    handle <- None
                | None ->
                    ()
        true

    override x.Create() =
        input.Acquire()

    override x.Destroy() =
        input.RemoveOutput x
        match handle with
            | Some(a,h) -> 
                desc.idestroy h
                handle <- None
                owner.ReplaceLocked(Some a, None)
            | None ->
                ()
        input.Release()

    override x.GetHandle(token : AdaptiveToken) =
        if x.OutOfDate then
            let handle = recreate token
            { handle = handle; version = 0 }
        else
            match handle with
                | Some(_,h) -> { handle = h; version = 0 }
                | None -> failwith "[Resource] inconsistent state"

type MutableResourceLocation<'a, 'h>(owner : IResourceCache, key : list<obj>, input : IMod<'a>, desc : MutableResourceDescription<'a, 'h>) =
    inherit AbstractResourceLocation<'h>(owner, key)

    let mutable refCount = 0
    let mutable handle : Option<'a * 'h> = None
    let mutable version = 0


    let recreate (n : 'a) =
        match handle with
            | Some(o,h) ->
                owner.ReplaceLocked (Some o, Some n)

                desc.mdestroy h
                let r = desc.mcreate n
                handle <- Some(n,r)
                r
            | None ->
                owner.ReplaceLocked (None, Some n)

                let r = desc.mcreate n
                handle <- Some(n,r)
                r
                


    let update (token : AdaptiveToken) =
        let n = input.GetValue token

        match handle with
            | None ->
                recreate n

            | Some (oa, oh) when Unchecked.equals oa n ->
                oh

            | Some(oa,oh) ->
                if desc.mtryUpdate oh n then
                    owner.ReplaceLocked(Some oa, Some n)
                    handle <- Some(n, oh)
                    inc &version
                    oh
                else
                    recreate n


    override x.Create() =
        input.Acquire()

    override x.Destroy() =
        input.RemoveOutput x
        match handle with
            | Some(a,h) -> 
                desc.mdestroy h
                handle <- None
                owner.ReplaceLocked(Some a, None)
            | None ->
                ()
        input.Release()

    override x.GetHandle(token : AdaptiveToken) =
        if x.OutOfDate then
            let handle = update token
            { handle = handle; version = version }
        else
            match handle with
                | Some(_,h) -> { handle = h; version = version }
                | None -> failwith "[Resource] inconsistent state"

[<AbstractClass>]
type AbstractPointerResource<'a when 'a : unmanaged>(owner : IResourceCache, key : list<obj>) =
    inherit AbstractResourceLocation<'a>(owner, key)

    let mutable ptr = NativePtr.zero
    let mutable version = 0
    let mutable hasHandle = false

    abstract member Compute : AdaptiveToken -> 'a
    abstract member Free : 'a -> unit
    default x.Free _ = ()

    member x.Pointer = ptr

    member x.HasHandle = hasHandle

    member x.NoChange() =
        dec &version

    interface INativeResourceLocation<'a> with
        member x.Pointer = x.Pointer

    override x.Create() =
        ptr <- NativePtr.alloc 1

    override x.Destroy() =
        if hasHandle then
            let v = NativePtr.read ptr
            x.Free v
            NativePtr.free ptr
            hasHandle <- false

    override x.GetHandle(token : AdaptiveToken) =
        if x.OutOfDate then
            let value = x.Compute token
            if hasHandle then
                let v = NativePtr.read ptr
                x.Free v  

            NativePtr.write ptr value
            hasHandle <- true
            inc &version
            { handle = value; version = version }
        else
            { handle = NativePtr.read ptr; version = version }

[<AbstractClass>]
type AbstractPointerResourceWithEquality<'a when 'a : unmanaged>(owner : IResourceCache, key : list<obj>) =
    inherit AbstractResourceLocation<'a>(owner, key)

    let mutable ptr = NativePtr.zero
    let mutable version = 0
    let mutable hasHandle = false

    abstract member Compute : AdaptiveToken -> 'a
    abstract member Free : 'a -> unit
    default x.Free _ = ()

    member x.Pointer = ptr

    interface INativeResourceLocation<'a> with
        member x.Pointer = x.Pointer

    override x.Create() =
        ptr <- NativePtr.alloc 1

    override x.Destroy() =
        if hasHandle then
            let v = NativePtr.read ptr
            x.Free v
            NativePtr.free ptr
            hasHandle <- false

    override x.GetHandle(token : AdaptiveToken) =
        if x.OutOfDate then
            let value = x.Compute token
            if hasHandle then
                let v = NativePtr.read ptr
                if Unchecked.equals v value then
                    { handle = value; version = version }
                else
                    x.Free v  
                    NativePtr.write ptr value
                    inc &version
                    { handle = value; version = version }
            else
                NativePtr.write ptr value
                hasHandle <- true
                inc &version
                { handle = value; version = version }
        else
            { handle = NativePtr.read ptr; version = version }

type ResourceLocationCache<'h>(user : IResourceUser) =
    let store = System.Collections.Concurrent.ConcurrentDictionary<list<obj>, IResourceLocation<'h>>()

    member x.GetOrCreate(key : list<obj>, create : IResourceCache -> list<obj> -> #IResourceLocation<'h>) =
        let res = 
            store.GetOrAdd(key, fun key -> 
                let res = create x key :> IResourceLocation<_>
                res
            )
        res

    member x.Clear() =
        let res = store.Values |> Seq.toArray
        for r in res do r.ReleaseAll()

    interface IResourceCache with
        member x.AddLocked l = user.AddLocked l
        member x.RemoveLocked l = user.RemoveLocked l
        member x.Remove key = store.TryRemove key |> ignore

type NativeResourceLocationCache<'h when 'h : unmanaged>(user : IResourceUser) =
    let store = System.Collections.Concurrent.ConcurrentDictionary<list<obj>, INativeResourceLocation<'h>>()

    member x.GetOrCreate(key : list<obj>, create : IResourceCache -> list<obj> -> #INativeResourceLocation<'h>) =
        let res = 
            store.GetOrAdd(key, fun key -> 
                let res = create x key :> INativeResourceLocation<_>
                res
            )
        res

    member x.Clear() =
        let res = store.Values |> Seq.toArray
        for r in res do r.ReleaseAll()

    interface IResourceCache with
        member x.AddLocked l = user.AddLocked l
        member x.RemoveLocked l = user.RemoveLocked l
        member x.Remove key = store.TryRemove key |> ignore

open Aardvark.Rendering.Vulkan


module Resources =

    type AdaptiveDescriptor =
        | AdaptiveUniformBuffer of int * IResourceLocation<UniformBuffer>
        | AdaptiveCombinedImageSampler of int * array<Option<IResourceLocation<ImageView> * IResourceLocation<Sampler>>>
        | AdaptiveStorageBuffer of int * IResourceLocation<Buffer>
        | AdaptiveStorageImage of int * IResourceLocation<ImageView>

    type BufferResource(owner : IResourceCache, key : list<obj>, device : Device, usage : VkBufferUsageFlags, input : IMod<IBuffer>) =
        inherit MutableResourceLocation<IBuffer, Buffer>(
            owner, key, 
            input,
            {
                mcreate          = fun (b : IBuffer) -> device.CreateBuffer(usage, b)
                mdestroy         = fun b -> device.Delete b
                mtryUpdate       = fun (b : Buffer) (v : IBuffer) -> Buffer.tryUpdate v b
            }
        )
        
//        inherit ImmutableResourceLocation<IBuffer, Buffer>(
//            owner, key, 
//            input,
//            {
//                icreate = fun (b : IBuffer) -> device.CreateBuffer(usage, b)
//                idestroy = fun b -> device.Delete b
//                ieagerDestroy = true
//            }
//        )

    type IndirectBufferResource(owner : IResourceCache, key : list<obj>, device : Device, indexed : bool, input : IMod<IIndirectBuffer>) =
        inherit ImmutableResourceLocation<IIndirectBuffer, IndirectBuffer>(
            owner, key, 
            input,
            {
                icreate = fun (b : IIndirectBuffer) -> device.CreateIndirectBuffer(indexed, b)
                idestroy = fun b -> device.Delete b
                ieagerDestroy = true
            }
        )

    type UniformBufferResource(owner : IResourceCache, key : list<obj>, device : Device, layout : FShade.GLSL.GLSLUniformBuffer, writers : list<IMod * UniformWriters.IWriter>) =
        inherit AbstractResourceLocation<UniformBuffer>(owner, key)
        
        let mutable handle : UniformBuffer = Unchecked.defaultof<_>
        let mutable version = 0

        member x.Handle = handle

        override x.Create() =
            handle <- device.CreateUniformBuffer(layout)

        override x.Destroy() =
            device.Delete handle
            handle <- Unchecked.defaultof<_>
                
        override x.GetHandle(token : AdaptiveToken) =
            if x.OutOfDate then
                for (m,w) in writers do
                    w.Write(token, m, handle.Storage.Pointer)

                device.Upload handle

                inc &version
                { handle = handle; version = version }
            else
                { handle = handle; version = version }

    type ImageResource(owner : IResourceCache, key : list<obj>, device : Device, input : IMod<ITexture>) =
        inherit ImmutableResourceLocation<ITexture, Image>(
            owner, key, 
            input,
            {
                icreate = fun (b : ITexture) -> device.CreateImage(b)
                idestroy = fun b -> device.Delete b
                ieagerDestroy = true
            }
        )

    type SamplerResource(owner : IResourceCache, key : list<obj>, device : Device, input : IMod<SamplerStateDescription>) =
        inherit ImmutableResourceLocation<SamplerStateDescription, Sampler>(
            owner, key, 
            input,
            {
                icreate = fun (b : SamplerStateDescription) -> device.CreateSampler(b)
                idestroy = fun b -> device.Delete b
                ieagerDestroy = true
            }
        )
        

    type ShaderProgramEffectResource(owner : IResourceCache, key : list<obj>, device : Device, layout : PipelineLayout, input : IMod<FShade.Imperative.Module>) =
        inherit ImmutableResourceLocation<FShade.Imperative.Module, ShaderProgram>(
            owner, key, 
            input,
            {
                icreate = fun (e : FShade.Imperative.Module) -> ShaderProgram.ofModule e device
                idestroy = fun b -> device.Delete b
                ieagerDestroy = false
            }
        )

        
    type ShaderProgramResource(owner : IResourceCache, key : list<obj>, device : Device, signature : IFramebufferSignature, input : ISurface, top : IndexedGeometryMode) =
        inherit ImmutableResourceLocation<ISurface, ShaderProgram>(
            owner, key, 
            Mod.constant input,
            {
                icreate = fun (b : ISurface) -> device.CreateShaderProgram(b)
                idestroy = fun b -> device.Delete b
                ieagerDestroy = false
            }
        )

    type InputAssemblyStateResource(owner : IResourceCache, key : list<obj>, input : IndexedGeometryMode, program : IResourceLocation<ShaderProgram>) =
        inherit AbstractPointerResourceWithEquality<VkPipelineInputAssemblyStateCreateInfo>(owner, key)

        override x.Create() =
            base.Create()
            program.Acquire()

        override x.Destroy() =
            base.Destroy()
            program.Release()

        override x.Compute(token) =
            let m = input
            let p = program.Update token
            let res = 
                if p.handle.HasTessellation then { topology = VkPrimitiveTopology.PatchList; restartEnable = false }
                else InputAssemblyState.ofIndexedGeometryMode m

            VkPipelineInputAssemblyStateCreateInfo(
                VkPipelineInputAssemblyStateCreateFlags.MinValue,
                res.topology,
                (if res.restartEnable then 1u else 0u)
            )

    type VertexInputStateResource(owner : IResourceCache, key : list<obj>, prog : PipelineInfo, input : IMod<Map<Symbol, VertexInputDescription>>) =
        inherit AbstractPointerResource<VkPipelineVertexInputStateCreateInfo>(owner, key)
        static let collecti (f : int -> 'a -> list<'b>) (m : list<'a>) =
            m |> List.indexed |> List.collect (fun (i,v) -> f i v)

        override x.Free(state : VkPipelineVertexInputStateCreateInfo) =
            NativePtr.free state.pVertexAttributeDescriptions
            NativePtr.free state.pVertexBindingDescriptions

        override x.Compute(token) =
            let state = input.GetValue token

            let inputs = prog.pInputs |> List.sortBy (fun p -> p.paramLocation)

            let paramsWithInputs =
                inputs |> List.map (fun p ->
                    let sem = Symbol.Create p.paramSemantic
                    match Map.tryFind sem state with
                        | Some ip -> 
                            p.paramLocation, p, ip
                        | None ->
                            failf "could not get vertex input-type for %A" p
                )

            let inputBindings =
                paramsWithInputs |> List.mapi (fun i (loc, p, ip) ->
                    VkVertexInputBindingDescription(
                        uint32 i,
                        uint32 ip.stride,
                        ip.stepRate
                    )
                ) |> List.toArray

            let inputAttributes =
                paramsWithInputs |> collecti (fun bi (loc, p, ip) ->
                    ip.offsets |> List.mapi (fun i off ->
                        VkVertexInputAttributeDescription(
                            uint32 (loc + i),
                            uint32 bi,
                            ip.inputFormat,
                            uint32 off
                        )
                    )
                ) |> List.toArray

            let pInputBindings = NativePtr.alloc inputBindings.Length
            let pInputAttributes = NativePtr.alloc inputAttributes.Length

            for i in 0 .. inputBindings.Length - 1 do
                NativePtr.set pInputBindings i inputBindings.[i]

            for i in 0 .. inputAttributes.Length - 1 do
                NativePtr.set pInputAttributes i inputAttributes.[i]

            VkPipelineVertexInputStateCreateInfo(
                VkPipelineVertexInputStateCreateFlags.MinValue,

                uint32 inputBindings.Length,
                pInputBindings,

                uint32 inputAttributes.Length,
                pInputAttributes
            )


    type DepthStencilStateResource(owner : IResourceCache, key : list<obj>, depthWrite : bool, depth : IMod<DepthTestMode>, stencil : IMod<StencilMode>) =
        inherit AbstractPointerResourceWithEquality<VkPipelineDepthStencilStateCreateInfo>(owner, key)

        override x.Compute(token) =
            let depth = depth.GetValue token
            let stencil = stencil.GetValue token

            let depth = DepthState.create depthWrite depth
            let stencil = StencilState.create stencil

            VkPipelineDepthStencilStateCreateInfo(
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
            
    type RasterizerStateResource(owner : IResourceCache, key : list<obj>, depth : IMod<DepthTestMode>, bias : IMod<DepthBiasState>, cull : IMod<CullMode>, frontFace : IMod<WindingOrder>, fill : IMod<FillMode>) =
        inherit AbstractPointerResourceWithEquality<VkPipelineRasterizationStateCreateInfo>(owner, key)

        override x.Compute(token) =
            let depth = depth.GetValue token
            let bias = bias.GetValue token
            let cull = cull.GetValue token
            let front = frontFace.GetValue token
            let fill = fill.GetValue token
            let state = RasterizerState.create false depth bias cull front fill

            VkPipelineRasterizationStateCreateInfo(
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
    
    type ColorBlendStateResource(owner : IResourceCache, key : list<obj>, writeMasks : bool[], blend : IMod<BlendMode>) =
        inherit AbstractPointerResourceWithEquality<VkPipelineColorBlendStateCreateInfo>(owner, key)

        override x.Free(h : VkPipelineColorBlendStateCreateInfo) =
            NativePtr.free h.pAttachments
            
        override x.Compute(token) =
            let blend = blend.GetValue token
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
                VkPipelineColorBlendStateCreateFlags.MinValue,
                (if state.logicOpEnable then 1u else 0u),
                state.logicOp,
                uint32 writeMasks.Length,
                pAttStates,
                state.constants
            )
    
    type DirectDrawCallResource(owner : IResourceCache, key : list<obj>, indexed : bool, calls : IMod<list<DrawCallInfo>>) =
        inherit AbstractPointerResourceWithEquality<DrawCall>(owner, key)
        
        override x.Free(call : DrawCall) =
            call.Dispose()

        override x.Compute(token) =
            let calls = calls.GetValue token
            DrawCall.Direct(indexed, List.toArray calls)


    type DescriptorSetResource(owner : IResourceCache, key : list<obj>, layout : DescriptorSetLayout, bindings : AdaptiveDescriptor[]) =
        inherit AbstractResourceLocation<DescriptorSet>(owner, key)

        let mutable handle : Option<DescriptorSet> = None
        let mutable version = 0

        let mutable state = [||]
        let device = layout.Device
        override x.Create() =
            for b in bindings do
                match b with
                    | AdaptiveCombinedImageSampler(_,arr) ->
                        for a in arr do
                            match a with
                                | Some (i,s) -> i.Acquire(); s.Acquire()
                                | None -> ()

                    | AdaptiveStorageBuffer(_,b) ->
                        b.Acquire()

                    | AdaptiveStorageImage(_,v) ->
                        v.Acquire()

                    | AdaptiveUniformBuffer(_,b) ->
                        b.Acquire()

            ()

        override x.Destroy() =
            for b in bindings do
                match b with
                    | AdaptiveCombinedImageSampler(_,arr) ->
                        for a in arr do
                            match a with
                                | Some (i,s) -> i.Release(); s.Release()
                                | None -> ()
                                
                    | AdaptiveStorageImage(_,v) ->
                        v.Release()
                    | AdaptiveStorageBuffer(_,b) ->
                        b.Release()
                    | AdaptiveUniformBuffer(_,b) ->
                        b.Release()

            match handle with
                | Some set -> 
                    device.Delete set
                    handle <- None
                | _ -> ()

        override x.GetHandle(token : AdaptiveToken) =
            if x.OutOfDate then
                
                let bindings =
                    bindings |> Array.map (fun b ->
                        match b with
                            | AdaptiveUniformBuffer(slot, b) ->
                                let handle =
                                    match b with
                                        | :? UniformBufferResource as b -> b.Handle
                                        | b -> b.Update(AdaptiveToken.Top).handle

                                UniformBuffer(slot,  handle)
                                
                            | AdaptiveStorageImage(slot,v) ->
                                let image = v.Update(token).handle
                                StorageImage(slot, image)

                            | AdaptiveStorageBuffer(slot, b) ->
                                let buffer = b.Update(token).handle
                                StorageBuffer(slot, buffer, 0L, buffer.Size)

                            | AdaptiveCombinedImageSampler(slot, arr) ->
                                let arr =
                                    arr |> Array.map (fun o ->
                                        match o with
                                            | Some(s,i) ->
                                                Some(VkImageLayout.ShaderReadOnlyOptimal, s.Update(token).handle, i.Update(token).handle)
                                            | None ->
                                                None
                                    )

                                CombinedImageSampler(slot, arr)
                    )


                let handle =
                    match handle with
                        | Some h -> h
                        | None ->
                            let h = device.CreateDescriptorSet(layout)
                            handle <- Some h
                            h

                if bindings <> state then
                    handle.Update(bindings)
                    state <- bindings
                    inc &version

                { handle = handle; version = version }
            else 
                match handle with
                    | Some h -> { handle = h; version = version }
                    | None -> failwith "[Resource] inconsistent state"

    type PipelineResource(owner : IResourceCache, key : list<obj>, 
                          renderPass : RenderPass,
                          program : IResourceLocation<ShaderProgram>, 
                          inputState : INativeResourceLocation<VkPipelineVertexInputStateCreateInfo>,
                          inputAssembly : INativeResourceLocation<VkPipelineInputAssemblyStateCreateInfo>,
                          rasterizerState : INativeResourceLocation<VkPipelineRasterizationStateCreateInfo>,
                          colorBlendState : INativeResourceLocation<VkPipelineColorBlendStateCreateInfo>,
                          multisample : MultisampleState,
                          depthStencil : INativeResourceLocation<VkPipelineDepthStencilStateCreateInfo>
                         ) =
        inherit AbstractPointerResource<VkPipeline>(owner, key)

        static let check str err =
            if err <> VkResult.VkSuccess then failwithf "[Vulkan] %s" str

        override x.Create() =
            base.Create()
            program.Acquire()
            inputState.Acquire()
            inputAssembly.Acquire()
            rasterizerState.Acquire()
            colorBlendState.Acquire()
            depthStencil.Acquire()

        override x.Destroy() =
            base.Destroy()
            program.Release()
            inputState.Release()
            inputAssembly.Release()
            rasterizerState.Release()
            colorBlendState.Release()
            depthStencil.Release()
            

        override x.Compute(token : AdaptiveToken) =
            let program = program.Update token
                
            let prog = program.handle
            let device = prog.Device

            let pipeline = 
                native {
                    let! pShaderCreateInfos = prog.ShaderCreateInfos
                    
                    let! pViewportState =
                        let vp  =
                            if device.AllCount > 1u then
                                if renderPass.LayerCount > 1 then 1u
                                else device.AllCount
                            else 1u
                        VkPipelineViewportStateCreateInfo(
                            VkPipelineViewportStateCreateFlags.MinValue,
                
                            uint32 vp,
                            NativePtr.zero,

                            uint32 vp,
                            NativePtr.zero
                        )

                    let! pSampleMasks = multisample.sampleMask
                    let! pMultisampleState =
                        let ms = multisample
                        VkPipelineMultisampleStateCreateInfo(
                            VkPipelineMultisampleStateCreateFlags.MinValue,
                
                            unbox ms.samples,
                            (if ms.sampleShadingEnable then 1u else 0u),
                            float32 ms.minSampleShading,
                            pSampleMasks,
                            (if ms.alphaToCoverageEnable then 1u else 0u),
                            (if ms.alphaToOneEnable then 1u else 0u)
                        )
            
                    let dynamicStates = [| VkDynamicState.Viewport; VkDynamicState.Scissor |]
                    let! pDynamicStates = Array.map uint32 dynamicStates
        
                    let! pTessStateInfo = 
                        VkPipelineTessellationStateCreateInfo(
                            VkPipelineTessellationStateCreateFlags.MinValue,
                            uint32 prog.TessellationPatchSize
                        )

                    let pTessState = 
                        if prog.HasTessellation then pTessStateInfo
                        else NativePtr.zero

                    let! pDynamicStates =
                        VkPipelineDynamicStateCreateInfo(
                            VkPipelineDynamicStateCreateFlags.MinValue, 

                            uint32 dynamicStates.Length,
                            NativePtr.cast pDynamicStates
                        )

                    // TODO: tessellation input-patch-size

                    let inputState = inputState.Update(token) |> ignore; inputState.Pointer
                    let inputAssembly = inputAssembly.Update(token) |> ignore; inputAssembly.Pointer
                    let rasterizerState = rasterizerState.Update(token) |> ignore; rasterizerState.Pointer
                    let depthStencil = depthStencil.Update(token) |> ignore; depthStencil.Pointer
                    let colorBlendState = colorBlendState.Update(token) |> ignore; colorBlendState.Pointer

                    let basePipeline, derivativeFlag =
                        if not x.HasHandle then
                            VkPipeline.Null, VkPipelineCreateFlags.None
                        else
                            !!x.Pointer, VkPipelineCreateFlags.DerivativeBit

                    let! pHandle = VkPipeline.Null
                    let! pDesc =
                        VkGraphicsPipelineCreateInfo(
                            VkPipelineCreateFlags.AllowDerivativesBit ||| derivativeFlag,
                            uint32 prog.ShaderCreateInfos.Length,
                            pShaderCreateInfos,
                            inputState,
                            inputAssembly,
                            pTessState,
                            pViewportState,
                            rasterizerState,
                            pMultisampleState,
                            depthStencil,
                            colorBlendState,
                            pDynamicStates, //dynamic
                            prog.PipelineLayout.Handle,
                            renderPass.Handle,
                            0u,
                            basePipeline,
                            0
                        )

                    VkRaw.vkCreateGraphicsPipelines(device.Handle, VkPipelineCache.Null, 1u, pDesc, NativePtr.zero, pHandle)
                        |> check "could not create pipeline"

                    return Pipeline(device, !!pHandle, Unchecked.defaultof<_>)
                }

            pipeline.Handle
    
        override x.Free(p : VkPipeline) =
            VkRaw.vkDestroyPipeline(renderPass.Device.Handle, p, NativePtr.zero)

    type IndirectDrawCallResource(owner : IResourceCache, key : list<obj>, indexed : bool, calls : IResourceLocation<IndirectBuffer>) =
        inherit AbstractPointerResourceWithEquality<DrawCall>(owner, key)

        override x.Create() =
            base.Create()
            calls.Acquire()

        override x.Destroy() =
            base.Destroy()
            calls.Release()

        override x.Compute(token : AdaptiveToken) =
            let calls = calls.Update token
            let call = DrawCall.Indirect(indexed, calls.handle.Handle, calls.handle.Count)
            call

        override x.Free(call : DrawCall) =
            call.Dispose()

    type BufferBindingResource(owner : IResourceCache, key : list<obj>, buffers : list<IResourceLocation<Buffer> * int64>) =
        inherit AbstractPointerResource<VertexBufferBinding>(owner, key)

        let mutable last = []

        override x.Create() =
            base.Create()
            for (b,_) in buffers do b.Acquire()

        override x.Destroy() =
            base.Destroy()
            for (b,_) in buffers do b.Release()

        override x.Compute(token : AdaptiveToken) =
            let calls = buffers |> List.map (fun (b,o) -> b.Update(token).handle.Handle, o) //calls.Update token

            if calls <> last then last <- calls
            else x.NoChange()

            let call = new VertexBufferBinding(0, List.toArray calls)
            call

        override x.Free(b : VertexBufferBinding) =
            b.Dispose()

    type DescriptorSetBindingResource(owner : IResourceCache, key : list<obj>, layout : PipelineLayout, sets : IResourceLocation<DescriptorSet>[]) =
        inherit AbstractPointerResource<DescriptorSetBinding>(owner, key)

        let mutable setVersions = Array.init sets.Length (fun _ -> -1)
        let mutable target : Option<DescriptorSetBinding> = None

        override x.Create() =
            base.Create()
            for s in sets do s.Acquire()

        override x.Destroy() =
            base.Destroy()
            for s in sets do s.Release()
            setVersions <- Array.init sets.Length (fun _ -> -1)
            match target with
                | Some t -> t.Dispose(); target <- None
                | None -> ()
        override x.Compute(token : AdaptiveToken) =
            let mutable changed = false
            let target =
                match target with
                    | Some t -> t
                    | None ->
                        let t = new DescriptorSetBinding(layout.Handle, 0, sets.Length)
                        target <- Some t
                        t
            for i in 0 .. sets.Length - 1 do
                let info = sets.[i].Update(token)
                NativePtr.set target.Sets i info.handle.Handle
                if info.version <> setVersions.[i] then
                    setVersions.[i] <- info.version
                    changed <- true

            if not changed then x.NoChange()

            target

        override x.Free(t : DescriptorSetBinding) =
            () //t.Dispose()
 
    type IndexBufferBindingResource(owner : IResourceCache, key : list<obj>, indexType : VkIndexType, index : IResourceLocation<Buffer>) =
        inherit AbstractPointerResourceWithEquality<IndexBufferBinding>(owner, key)

 
        override x.Create() =
            base.Create()
            index.Acquire()

        override x.Destroy() =
            base.Destroy()
            index.Release()

        override x.Compute(token : AdaptiveToken) =
            let index = index.Update token

            let ibo = IndexBufferBinding(index.handle.Handle, indexType)
            ibo

        override x.Free(ibo : IndexBufferBinding) =
            //ibo.TryDispose()
            ()

    type ImageViewResource(owner : IResourceCache, key : list<obj>, device : Device, samplerType : FShade.GLSL.GLSLSamplerType, image : IResourceLocation<Image>) =
        inherit AbstractResourceLocation<ImageView>(owner, key)

        let mutable handle : Option<ImageView> = None
        let mutable viewVersion = -1

        override x.Create() =
            image.Acquire()

        override x.Destroy() =
            match handle with   
                | Some h -> 
                    device.Delete h
                    handle <- None
                | None -> ()
            image.Release()

        override x.GetHandle(token : AdaptiveToken) =
            if x.OutOfDate then
                let image = image.Update token
                let contentVersion = image.handle.Version.GetValue token

                let isIdentical =
                    match handle with
                        | Some h -> h.Image = image.handle && viewVersion = contentVersion
                        | None -> false

                if isIdentical then
                    { handle = handle.Value; version = 0 }
                else
                    match handle with
                        | Some h -> device.Delete h
                        | None -> ()

                    let h = device.CreateInputImageView(image.handle, samplerType, VkComponentMapping.Identity)
                    handle <- Some h
                    viewVersion <- contentVersion

                    { handle = h; version = 0 }
            else
                match handle with
                    | Some h -> { handle = h; version = 0 }
                    | None -> failwith "[Resource] inconsistent state"
    
    type StorageImageViewResource(owner : IResourceCache, key : list<obj>, device : Device, imageType : FShade.GLSL.GLSLImageType, image : IResourceLocation<Image>) =
        inherit AbstractResourceLocation<ImageView>(owner, key)

        let mutable handle : Option<ImageView> = None
        let mutable viewVersion = -1

        override x.Create() =
            image.Acquire()

        override x.Destroy() =
            match handle with   
                | Some h -> 
                    device.Delete h
                    handle <- None
                | None -> ()
            image.Release()

        override x.GetHandle(token : AdaptiveToken) =
            if x.OutOfDate then
                let image = image.Update token
                let contentVersion = image.handle.Version.GetValue token

                let isIdentical =
                    match handle with
                        | Some h -> h.Image = image.handle && viewVersion = contentVersion
                        | None -> false

                if isIdentical then
                    { handle = handle.Value; version = 0 }
                else
                    match handle with
                        | Some h -> device.Delete h
                        | None -> ()

                    let h = device.CreateStorageView(image.handle, imageType, VkComponentMapping.Identity)
                    handle <- Some h
                    viewVersion <- contentVersion

                    { handle = h; version = 0 }
            else
                match handle with
                    | Some h -> { handle = h; version = 0 }
                    | None -> failwith "[Resource] inconsistent state"
    
    type IsActiveResource(owner : IResourceCache, key : list<obj>, input : IMod<bool>) =
        inherit AbstractPointerResourceWithEquality<int>(owner, key)

        override x.Compute (token : AdaptiveToken) =
            if input.GetValue token then 1 else 0

open Resources
type ResourceManager(user : IResourceUser, device : Device) =
    //let descriptorPool = device.CreateDescriptorPool(1 <<< 22, 1 <<< 22)

    let bufferCache             = ResourceLocationCache<Buffer>(user)
    let indirectBufferCache     = ResourceLocationCache<IndirectBuffer>(user)
    let indexBufferCache        = ResourceLocationCache<Buffer>(user)
    let descriptorSetCache      = ResourceLocationCache<DescriptorSet>(user)
    let uniformBufferCache      = ResourceLocationCache<UniformBuffer>(user)
    let imageCache              = ResourceLocationCache<Image>(user)
    let imageViewCache          = ResourceLocationCache<ImageView>(user)
    let samplerCache            = ResourceLocationCache<Sampler>(user)
    let programCache            = ResourceLocationCache<ShaderProgram>(user)
    let simpleSurfaceCache      = System.Collections.Concurrent.ConcurrentDictionary<obj, ShaderProgram>()
    let fshadeThingCache        = System.Collections.Concurrent.ConcurrentDictionary<obj, PipelineLayout * IMod<FShade.Imperative.Module>>()
    
    let vertexInputCache        = NativeResourceLocationCache<VkPipelineVertexInputStateCreateInfo>(user)
    let inputAssemblyCache      = NativeResourceLocationCache<VkPipelineInputAssemblyStateCreateInfo>(user)
    let depthStencilCache       = NativeResourceLocationCache<VkPipelineDepthStencilStateCreateInfo>(user)
    let rasterizerStateCache    = NativeResourceLocationCache<VkPipelineRasterizationStateCreateInfo>(user)
    let colorBlendStateCache    = NativeResourceLocationCache<VkPipelineColorBlendStateCreateInfo>(user)
    let pipelineCache           = NativeResourceLocationCache<VkPipeline>(user)

    let drawCallCache           = NativeResourceLocationCache<DrawCall>(user)
    let bufferBindingCache      = NativeResourceLocationCache<VertexBufferBinding>(user)
    let descriptorBindingCache  = NativeResourceLocationCache<DescriptorSetBinding>(user)
    let indexBindingCache       = NativeResourceLocationCache<IndexBufferBinding>(user)
    let isActiveCache           = NativeResourceLocationCache<int>(user)
    
    static let toInputTopology =
        LookupTable.lookupTable [
            IndexedGeometryMode.PointList, FShade.InputTopology.Point
            IndexedGeometryMode.LineList, FShade.InputTopology.Line
            IndexedGeometryMode.LineStrip, FShade.InputTopology.Line
            IndexedGeometryMode.LineAdjacencyList, FShade.InputTopology.LineAdjacency
            IndexedGeometryMode.TriangleList, FShade.InputTopology.Triangle
            IndexedGeometryMode.TriangleStrip, FShade.InputTopology.Triangle
            IndexedGeometryMode.TriangleAdjacencyList, FShade.InputTopology.TriangleAdjacency
            IndexedGeometryMode.QuadList, FShade.InputTopology.Patch 4
        ]

    member x.ResourceUser = user

    member x.Dispose() =
        bufferCache.Clear()

        indirectBufferCache.Clear()
        indexBufferCache.Clear()
        descriptorSetCache.Clear()
        uniformBufferCache.Clear()
        imageCache.Clear()
        imageViewCache.Clear()
        samplerCache.Clear()
        programCache.Clear()

        vertexInputCache.Clear()
        inputAssemblyCache.Clear()
        depthStencilCache.Clear()
        rasterizerStateCache.Clear()
        colorBlendStateCache.Clear()
        pipelineCache.Clear()

        drawCallCache.Clear()
        bufferBindingCache.Clear()
        descriptorBindingCache.Clear()
        indexBindingCache.Clear()
        isActiveCache.Clear()



    member x.Device = device

//    member x.CreateRenderPass(signature : Map<Symbol, AttachmentSignature>) =
//        device.CreateRenderPass(signature)

    member x.CreateBuffer(input : IMod<IBuffer>) =
        bufferCache.GetOrCreate([input :> obj], fun cache key -> new BufferResource(cache, key, device, VkBufferUsageFlags.TransferDstBit ||| VkBufferUsageFlags.VertexBufferBit, input))
        
    member x.CreateIndexBuffer(input : IMod<IBuffer>) =
        bufferCache.GetOrCreate([input :> obj], fun cache key -> new BufferResource(cache, key, device, VkBufferUsageFlags.TransferDstBit ||| VkBufferUsageFlags.IndexBufferBit, input))
        
    member x.CreateIndirectBuffer(indexed : bool, input : IMod<IIndirectBuffer>) =
        indirectBufferCache.GetOrCreate([indexed :> obj; input :> obj], fun cache key -> new IndirectBufferResource(cache, key, device, indexed, input))

    member x.CreateImage(input : IMod<ITexture>) =
        imageCache.GetOrCreate([input :> obj], fun cache key -> new ImageResource(cache, key, device, input))
        
    member x.CreateImageView(samplerType : FShade.GLSL.GLSLSamplerType, input : IResourceLocation<Image>) =
        imageViewCache.GetOrCreate([samplerType :> obj; input :> obj], fun cache key -> new ImageViewResource(cache, key, device, samplerType, input))
        
    member x.CreateImageView(imageType : FShade.GLSL.GLSLImageType, input : IResourceLocation<Image>) =
        imageViewCache.GetOrCreate([imageType :> obj; input :> obj], fun cache key -> new StorageImageViewResource(cache, key, device, imageType, input))
        
    member x.CreateSampler(data : IMod<SamplerStateDescription>) =
        samplerCache.GetOrCreate([data :> obj], fun cache key -> new SamplerResource(cache, key, device, data))
        
    member x.CreateShaderProgram(data : ISurface) =
        let programKey = (data) :> obj

        let program = 
            simpleSurfaceCache.GetOrAdd(programKey, fun _ ->
                device.CreateShaderProgram(data)
            )

        let resource = 
            programCache.GetOrCreate([program :> obj], fun cache key -> 
                { new AbstractResourceLocation<ShaderProgram>(cache, key) with
                    override x.Create () = ()
                    override x.Destroy () = ()
                    override x.GetHandle t = { handle = program; version = 0 }
                }
            )
        program.PipelineLayout, resource

    member x.CreateShaderProgram(signature : RenderPass, data : FShade.Effect, top : IndexedGeometryMode) =

        let program = device.CreateShaderProgram(signature, data, top)
         
        if FShade.EffectDebugger.isAttached then
            FShade.EffectDebugger.saveCode data program.Surface

        let resource = 
            programCache.GetOrCreate([program :> obj], fun cache key -> 
                { new AbstractResourceLocation<ShaderProgram>(cache, key) with
                    override x.Create () = ()
                    override x.Destroy () = ()
                    override x.GetHandle t = { handle = program; version = 0 }
                }
            )
        program.PipelineLayout, resource

    member x.CreateShaderProgram(layout : PipelineLayout, data : IMod<FShade.Imperative.Module>) =
        programCache.GetOrCreate([layout :> obj; data :> obj], fun cache key -> 
            let prog = new ShaderProgramEffectResource(cache, key, device, layout, data)
            prog.Acquire()
            prog
        )

    member x.CreateShaderProgram(signature : RenderPass, data : Aardvark.Base.Surface, top : IndexedGeometryMode) =
        match data with
            | Surface.FShadeSimple effect ->
                x.CreateShaderProgram(signature, effect, top)
                //let module_ = signature.Link(effect, Range1d(0.0, 1.0), false, top)
                //let layout = FShade.EffectInputLayout.ofModule module_
                //let layout = device.CreatePipelineLayout(layout, signature.LayerCount, signature.PerLayerUniforms)
                //layout, x.CreateShaderProgram(layout, Mod.constant module_)
            | Surface.FShade(compile) -> 
                let layout, module_ = 
                    fshadeThingCache.GetOrAdd((signature, compile) :> obj, fun _ ->
                        let outputs = 
                            signature.ColorAttachments
                                |> Map.toList
                                |> List.map (fun (idx, (name, att)) -> string name, (att.GetType name, idx))
                                |> Map.ofList
            
                        let layout, module_ = 
                            compile { 
                                PipelineInfo.fshadeConfig with 
                                    outputs = outputs
                            }
                        let layout = device.CreatePipelineLayout(layout, signature.LayerCount, signature.PerLayerUniforms)

                        layout, module_
                    )

                layout, x.CreateShaderProgram(layout, module_)

            | Surface.Backend s -> 
                x.CreateShaderProgram(s)

            | Surface.None -> 
                failwith "[Vulkan] encountered empty surface"

    member x.CreateStorageBuffer(scope : Ag.Scope, layout : FShade.GLSL.GLSLStorageBuffer, u : IUniformProvider, additional : SymbolDict<IMod>) =
        let value =
            let sem = Symbol.Create layout.ssbName

            match Uniforms.tryGetDerivedUniform layout.ssbName u with
                | Some r -> r
                | None -> 
                    match u.TryGetUniform(scope, sem) with
                        | Some v -> v
                        | None -> 
                            match additional.TryGetValue sem with
                                | (true, m) -> m
                                | _ -> failwithf "[Vulkan] could not get storage buffer: %A" layout.ssbName
   


        let usage = VkBufferUsageFlags.TransferSrcBit ||| VkBufferUsageFlags.TransferDstBit ||| VkBufferUsageFlags.StorageBufferBit

        bufferCache.GetOrCreate([usage :> obj; value :> obj], fun cache key ->
           
            let buffer =
                Mod.custom (fun t ->
                    match value.GetValue t with
                    | :? Array as a -> ArrayBuffer(a) :> IBuffer
                    | :? IBuffer as b -> b
                    | _ -> failf "invalid storage buffer"
                )
            new BufferResource(cache, key, device, usage, buffer)
        ) 

    member x.CreateUniformBuffer(scope : Ag.Scope, layout : FShade.GLSL.GLSLUniformBuffer, u : IUniformProvider, additional : SymbolDict<IMod>) =
        let values =
            layout.ubFields 
            |> List.map (fun (f) ->
                let sem = Symbol.Create f.ufName

                match Uniforms.tryGetDerivedUniform f.ufName u with
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
                    | ModOf tSource -> m, UniformWriters.getWriter target.ufOffset target.ufType tSource
                    | t -> failwithf "[UniformBuffer] unexpected input-type %A" t
            )

        let key = (layout :> obj) :: (values |> List.map (fun (_,v) -> v :> obj))
        uniformBufferCache.GetOrCreate(key, fun cache key -> UniformBufferResource(cache, key, device, layout, writers))

    member x.CreateDescriptorSet(layout : DescriptorSetLayout, bindings : AdaptiveDescriptor[]) =
        descriptorSetCache.GetOrCreate([layout :> obj; bindings :> obj], fun cache key -> new DescriptorSetResource(cache, key, layout, bindings))
        
    member x.CreateVertexInputState(program : PipelineInfo, mode : IMod<Map<Symbol, VertexInputDescription>>) =
        vertexInputCache.GetOrCreate([program :> obj; mode :> obj], fun cache key -> new VertexInputStateResource(cache, key, program, mode))

    member x.CreateInputAssemblyState(mode : IndexedGeometryMode, program : IResourceLocation<ShaderProgram>) =
        inputAssemblyCache.GetOrCreate([mode :> obj; program :> obj], fun cache key -> new InputAssemblyStateResource(cache, key, mode, program))

    member x.CreateDepthStencilState(depthWrite : bool, depth : IMod<DepthTestMode>, stencil : IMod<StencilMode>) =
        depthStencilCache.GetOrCreate([depthWrite :> obj; depth :> obj; stencil :> obj], fun cache key -> new DepthStencilStateResource(cache, key, depthWrite, depth, stencil))
        
    member x.CreateRasterizerState(depth : IMod<DepthTestMode>, bias : IMod<DepthBiasState>, cull : IMod<CullMode>, front : IMod<WindingOrder>, fill : IMod<FillMode>) =
        rasterizerStateCache.GetOrCreate([depth :> obj; bias :> obj; cull :> obj; front :> obj, fill :> obj], fun cache key -> new RasterizerStateResource(cache, key, depth, bias, cull, front, fill))

    member x.CreateColorBlendState(pass : RenderPass, writeBuffers : Option<Set<Symbol>>, blend : IMod<BlendMode>) =
        colorBlendStateCache.GetOrCreate(
            [pass :> obj; writeBuffers :> obj; blend :> obj], 
            fun cache key -> 
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

                new ColorBlendStateResource(cache, key, writeMasks, blend)
        )

    member x.CreatePipeline(program         : IResourceLocation<ShaderProgram>, 
                            pass            : RenderPass,
                            inputState      : INativeResourceLocation<VkPipelineVertexInputStateCreateInfo>,
                            inputAssembly   : INativeResourceLocation<VkPipelineInputAssemblyStateCreateInfo>,
                            rasterizerState : INativeResourceLocation<VkPipelineRasterizationStateCreateInfo>,
                            colorBlendState : INativeResourceLocation<VkPipelineColorBlendStateCreateInfo>,
                            depthStencil    : INativeResourceLocation<VkPipelineDepthStencilStateCreateInfo>,
                            writeBuffers    : Option<Set<Symbol>>
                        ) =

        //let programHandle = program.Update(AdaptiveToken.Top).handle

        let anyAttachment = 
            match pass.ColorAttachments |> Map.toSeq |> Seq.tryHead with
                | Some (_,(_,a)) -> a
                | None -> pass.DepthStencilAttachment |> Option.map snd |> Option.get

        //let inputs = VertexInputState.create inputs
        // TODO: sampleShading
        let ms = MultisampleState.create false anyAttachment.samples
        let key = [ program :> obj; inputState :> obj; inputAssembly :> obj; rasterizerState :> obj; colorBlendState :> obj; ms :> obj; depthStencil :> obj ]
        pipelineCache.GetOrCreate(
            key,
            fun cache key ->
                new PipelineResource(
                    cache, key,
                    pass,
                    program,
                    inputState,
                    inputAssembly,
                    rasterizerState,
                    colorBlendState,
                    ms,
                    depthStencil
                )

        )


    member x.CreateDrawCall(indexed : bool, calls : IMod<list<DrawCallInfo>>) =
        drawCallCache.GetOrCreate([indexed :> obj; calls :> obj], fun cache key -> new DirectDrawCallResource(cache, key, indexed, calls))

    member x.CreateDrawCall(indexed : bool, calls : IResourceLocation<IndirectBuffer>) =
        drawCallCache.GetOrCreate([indexed :> obj; calls :> obj], fun cache key -> new IndirectDrawCallResource(cache, key, indexed, calls))
        
    member x.CreateVertexBufferBinding(buffers : list<IResourceLocation<Buffer> * int64>) =
        bufferBindingCache.GetOrCreate([buffers :> obj], fun cache key -> new BufferBindingResource(cache, key, buffers))

    member x.CreateDescriptorSetBinding(layout : PipelineLayout, bindings : IResourceLocation<DescriptorSet>[]) =
        descriptorBindingCache.GetOrCreate([layout :> obj; bindings :> obj], fun cache key -> new DescriptorSetBindingResource(cache, key, layout, bindings))
        
    member x.CreateIndexBufferBinding(binding : IResourceLocation<Buffer>, t : VkIndexType) =
        indexBindingCache.GetOrCreate([binding :> obj; t :> obj], fun cache key -> new IndexBufferBindingResource(cache, key, t, binding))

    member x.CreateIsActive(value : IMod<bool>) =
        isActiveCache.GetOrCreate([value :> obj], fun cache key -> IsActiveResource(cache, key, value))

    interface IDisposable with
        member x.Dispose() = x.Dispose()



open System.Collections.Generic


type ResourceLocationReader(resource : IResourceLocation) =
    inherit AdaptiveObject()

    let mutable lastVersion = 0
    
    let changable =
        match resource with
            | :? IResourceLocation<UniformBuffer> -> false
            | _ -> true

    let priority =
        match resource with
            | :? INativeResourceLocation<DrawCall> -> 1
            | _ -> 0

    member x.Priority = priority
                

    member x.Dispose() =
        lock resource (fun () ->
            resource.RemoveOutput x
        )

    member x.Update(token : AdaptiveToken) =
        x.EvaluateIfNeeded token false (fun t ->
            let info = resource.Update(t)
            if info.version <> lastVersion then
                lastVersion <- info.version
                changable
            else
                false
        )

    interface IDisposable with
        member x.Dispose() = x.Dispose()

[<AutoOpen>]
module ``Resource Reader Extensions`` =
    type IResourceLocation with
        member x.GetReader() = new ResourceLocationReader(x)

type ResourceLocationSet(user : IResourceUser) =
    inherit AdaptiveObject()

    let all = ReferenceCountingSet<IResourceLocation>()
    let readers = Dict<IResourceLocation, ResourceLocationReader>()
    let dirtyCalls = List<ResourceLocationReader>()
    let dirty = List<HashSet<ResourceLocationReader>>()

    let addDirty (r : ResourceLocationReader) =
        let priority = r.Priority

        lock dirty (fun () ->
            while dirty.Count <= priority do
                dirty.Add(HashSet())
                        
            dirty.[priority].Add(r) |> ignore
        )

    let remDirty (r : ResourceLocationReader) =
        lock dirty (fun () -> 
            if dirty.Count > r.Priority then
                dirty.[r.Priority].Remove r |> ignore
        )

    member private x.AddInput(r : IResourceLocation) =
        let reader = r.GetReader()
        lock readers (fun () -> readers.[r] <- reader)
        addDirty reader
        transact (fun () -> x.MarkOutdated())

    member private x.RemoveInput(r : IResourceLocation) =
        match lock readers (fun () -> readers.TryRemove r) with
            | (true, reader) ->
                remDirty reader
                reader.Dispose()
            | _ ->
                ()

    override x.InputChanged(t,i) =
        match i with
            | :? ResourceLocationReader as r ->
                addDirty r
            | _ ->
                ()

    member x.Add(r : IResourceLocation) =
        if lock all (fun () -> all.Add r) then
            lock r r.Acquire
            x.AddInput r

    member x.Remove(r : IResourceLocation) =
        if lock all (fun () -> all.Remove r) then
            lock r r.Release
            x.RemoveInput r



    member x.Update(token : AdaptiveToken) =
        x.EvaluateAlways token (fun t ->
            x.OutOfDate <- true

            let rec run (changed : bool) =
                let mine = 
                    lock dirty (fun () ->
                        if dirty.Count > 0 then
                            let last = dirty.[dirty.Count - 1]
                            dirty.RemoveAt (dirty.Count - 1)
                            last
                        else
                            null
                    )

                if not (isNull mine) then
                    let mutable changed = changed
                    for r in mine do
                        let c = r.Update(t)
                        changed <- changed || c

                    run changed
                else
                    changed

            run false
        )

    interface IResourceUser with
        member x.AddLocked l = user.AddLocked l
        member x.RemoveLocked l = user.RemoveLocked l
//
//
//type ResourceSet() =
//    inherit AdaptiveObject()
//    
//    let all = ReferenceCountingSet<IResourceLocation>()
//    let locked = ReferenceCountingSet<ILockedResource>()
//    let dirty = System.Collections.Generic.HashSet<IResourceLocation>()
//    let dirtyCalls = System.Collections.Generic.HashSet<IResourceLocation>()
//
//    member x.AddLocked(l : ILockedResource) =
//        lock locked (fun () -> locked.Add l |> ignore)
//        
//    member x.RemoveLocked(l : ILockedResource) =
//        lock locked (fun () -> locked.Remove l |> ignore)
//
//    interface IResourceUser with
//        member x.AddLocked l = x.AddLocked l
//        member x.RemoveLocked l = x.RemoveLocked l
//
//    override x.InputChanged(_,i) =
//        match i with
//            | :? INativeResourceLocation<DrawCall> as c -> lock dirty (fun () -> dirtyCalls.Add c |> ignore)
//            | :? IResourceLocation as r -> lock dirty (fun () -> dirty.Add r |> ignore)
//            | _ -> ()
//
//    member x.Add(r : IResourceLocation) =
//        if all.Add r then
//            lock r (fun () ->
//                r.Acquire()
//                if r.OutOfDate then
//                    x.InputChanged(null, r)
//                else
//                    r.Outputs.Add x |> ignore
//            )
//
//    member x.AddAndUpdate(r : IResourceLocation) =
//        x.EvaluateAlways AdaptiveToken.Top (fun t ->
//            if all.Add r then
//                lock r (fun () ->
//                    r.Acquire()
//                )
//            r.Update(t) |> ignore
//        )   
//
//    member x.Remove(r : IResourceLocation) =
//        if all.Remove r then
//            lock r (fun () ->
//                r.Release()
//                r.RemoveOutput x
//                lock dirty (fun () ->
//                    match r with
//                        | :? INativeResourceLocation<DrawCall> as r -> dirtyCalls.Remove r |> ignore
//                        | _ -> dirty.Remove r |> ignore
//                )
//            )
//
//    member x.Update(token : AdaptiveToken) =
//        x.EvaluateAlways token (fun token ->
//            let rec update () =
//                x.OutOfDate <- true
//                let arr = 
//                    lock dirty (fun () -> 
//                        if dirtyCalls.Count = 0 then
//                            let arr = HashSet.toArray dirty
//                            dirty.Clear()
//                            arr
//                        else
//                            let arr = HashSet.toArray dirtyCalls
//                            dirtyCalls.Clear()
//                            arr
//                    )
//
//                if arr.Length > 0 then
//                    let mutable changed = false
//                    for r in arr do
//                        let info = r.Update(token)
//                        changed <- changed || info.version <> -100
//
//                    let rest = update()
//                    changed || rest
//
//                else
//                    false
//
//            update()
//        )
//
//    member x.Use(action : unit -> 'r) =
//        let list = lock locked (fun () -> Seq.toArray locked)
//        for l in list do l.Lock.Enter(ResourceUsage.Render, l.OnLock)
//        try 
//            action()
//        finally 
//            for l in list do l.Lock.Exit(l.OnUnlock)
//
