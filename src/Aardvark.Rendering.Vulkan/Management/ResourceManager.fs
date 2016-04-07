namespace Aardvark.Rendering.Vulkan

#nowarn "9"
#nowarn "51"

open System
open System.Threading
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Collections.Concurrent
open Microsoft.FSharp.NativeInterop
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.Rendering.Vulkan

type IResource =
    inherit IAdaptiveObject
    inherit IDisposable
    abstract member AddRef : unit -> unit
    abstract member Update : IAdaptiveObject -> Command<unit>

[<AbstractClass>]
type Resource<'h when 'h : equality>(cache : ResourceCache<'h>) as this =
    inherit AdaptiveObject()


    let handle = Mod.init None
    let h = 
        Mod.custom (fun self ->
            match handle.Value with
                | Some h -> h
                | None ->
                    let cmd = this.Update(null)
                    cache.Context.DefaultQueue.RunSynchronously cmd
                    handle.Value.Value
        )
    
    let mutable refCount = 0
    let mutable key = []
        


    abstract member Create  : Option<'h> -> 'h * Command<unit>
    abstract member Destroy : 'h -> unit
   
    member x.Handle = h

    member x.Key
        with get() = key
        and set k = key <- k

    member x.AddRef() =
        Interlocked.Increment(&refCount) |> ignore

    member x.RemoveRef() =
        if Interlocked.Decrement(&refCount) = 0 then
            cache.Remove x.Key
            x.Kill()

    member x.Update(caller : IAdaptiveObject) =
        x.EvaluateIfNeeded caller Command.nop (fun () ->
            if refCount <= 0 then
                failf "cannot update disposed resource"

            let old = handle.Value
            let h, update = x.Create old

            match old with
                | Some o when o = h -> 
                    update

                | Some o ->
                    command {
                        try do! update
                        finally
                            transact (fun () -> handle.Value <- Some h)
                            x.Destroy o
                    }
                    
                | None ->
                    transact (fun () -> handle.Value <- Some h)
                    update


        )
            
    member x.Dispose() = x.RemoveRef()

    member internal x.Kill() =
        refCount <- 0
        key <- []
        match handle.Value with
            | Some h -> 
                transact (fun () -> handle.Value <- None)
                x.Destroy h

            | None -> 
                ()


    interface IDisposable with 
        member x.Dispose() = x.Dispose()

    interface IResource with
        member x.AddRef() = x.AddRef()
        member x.Update(caller) = x.Update(caller)
                
and ResourceCache<'h when 'h : equality>(context : Context) =
    let cache = ConcurrentDictionary<list<obj>, Resource<'h>>()

    member x.Context : Context = context

    member x.Remove (key : list<obj>) : unit =
        match cache.TryRemove key with
            | (true, res) -> res.Key <- []
            | _ -> ()

    member x.GetOrCreate(key : list<obj>, f : unit -> Resource<'h>) =
        let res = 
            cache.GetOrAdd(key, fun _ -> 
                let res = f()
                res.Key <- key
                res
            ) 
        res.AddRef()
        res

    member x.Clear() =
        cache.Values |> Seq.iter (fun r -> r.Kill())
        cache.Clear()
 
    
type ResourceManager(runtime : IRuntime, ctx : Context) =
    inherit Resource(ctx)

    let descriptorPool = ctx.CreateDescriptorPool (1 <<< 20)

    let bufferCache = ResourceCache<Buffer>(ctx)
    let bufferViewCache = ResourceCache<BufferView>(ctx)
    let imageCache = ResourceCache<Image>(ctx)
    let imageViewCache = ResourceCache<ImageView>(ctx)
    let samplerCache = ResourceCache<Sampler>(ctx)
    let shaderProgramCache = ResourceCache<ShaderProgram>(ctx)
    let pipelineCache = ResourceCache<Pipeline>(ctx)
    let uniformBufferCache = ResourceCache<UniformBuffer>(ctx)
    let descriptorSetCache = ResourceCache<DescriptorSet>(ctx)

    override x.Release() =

        ctx.Delete descriptorPool

        bufferCache.Clear()
        bufferViewCache.Clear()
        imageCache.Clear()
        imageViewCache.Clear()
        samplerCache.Clear()
        shaderProgramCache.Clear()
        pipelineCache.Clear()
        uniformBufferCache.Clear()
        descriptorSetCache.Clear()

    member x.Context = ctx
    member x.Runtime = runtime

    // buffers

    member x.CreateBuffer(data : IMod<IBuffer>, usage : VkBufferUsageFlags) =
        bufferCache.GetOrCreate(
            [data; usage],
            fun () ->
                let mutable created = false
                { new Resource<Buffer>(bufferCache) with
                    member x.Create(old) =
                        let input = data.GetValue(x)

                        match input with
                            | :? Buffer as b ->
                                if created then 
                                    old |> Option.iter ctx.Delete
                                    created <- false

                                b, Command.nop

                            | :? ArrayBuffer as ab ->
                                let data = ab.Data
                                let es = Marshal.SizeOf (data.GetType().GetElementType())
                                let size = data.Length * es |> int64
                                            
                                match old with
                                    | Some old when created && old.Size = size ->
                                        old, old.Upload(data, 0, data.Length)

                                    | _ ->
                                        if created then old |> Option.iter ctx.Delete
                                        else created <- true

                                        let b = ctx.CreateBuffer(size, usage)
                                        b, b.Upload(data, 0, data.Length)
                                
                            | :? INativeBuffer as nb ->
                                let size = nb.SizeInBytes |> int64

                                let upload (b : Buffer) =
                                    command {
                                        let ptr = nb.Pin()
                                        try do! b.Upload(ptr, size)
                                        finally nb.Unpin()
                                    }

                                match old with
                                    | Some old when created && old.Size = size ->   
                                        old, upload old

                                    | _ ->
                                        if created then old |> Option.iter ctx.Delete
                                        else created <- true

                                        let b = ctx.CreateBuffer(size, usage)
                                        b, upload b
                                                     
                            | b ->
                                failf "unsupported buffer type: %A" b

                    member x.Destroy(h) =
                        ctx.Delete h
                        created <- false
                }

        )

    member x.CreateBufferView(elementType : Type, offset : int64, size : int64, buffer : Resource<Buffer>) =
        let format = elementType.GetVertexInputFormat()
        bufferViewCache.GetOrCreate(
            [elementType; offset; size; buffer],
            fun () ->
                { new Resource<BufferView>(bufferViewCache) with
                    member x.Create(old) =
                        old |> Option.iter ctx.Delete

                        let buffer = buffer.Handle.GetValue(x)
                        let size =
                            if size < 0L then buffer.Size - offset
                            else size

                        let view =
                            ctx.CreateBufferView(
                                buffer, 
                                format,
                                offset,
                                size
                            )

                        view, Command.nop

                    member x.Destroy(h) =
                        ctx.Delete(h)
                }
        )

    member x.CreateBufferView(view : Aardvark.Base.BufferView, buffer : Resource<Buffer>) =
        x.CreateBufferView(view.ElementType, int64 view.Offset, -1L, buffer)

    member x.CreateBuffer(data : IMod<Array>, usage : VkBufferUsageFlags) =
        x.CreateBuffer(data |> Mod.map (fun a -> ArrayBuffer(a) :> IBuffer), usage)

    member x.CreateBuffer(data : IMod<IBuffer>) =
        x.CreateBuffer(data, VkBufferUsageFlags.VertexBufferBit)

    member x.CreateIndexBuffer(data : IMod<IBuffer>) =
        x.CreateBuffer(data, VkBufferUsageFlags.IndexBufferBit)


    // textures

    member x.CreateImage(data : IMod<ITexture>) =
        imageCache.GetOrCreate(
            [data],
            fun () ->
                let mutable created = false
                { new Resource<Image>(imageCache) with
                    member x.Create old =
                        let tex = data.GetValue(x)

                        match tex with
                            | :? Image as t ->
                                if created then 
                                    old |> Option.iter ctx.Delete
                                    created <- false
                                t, Command.nop

                            | :? FileTexture as ft ->
                                if created then old |> Option.iter ctx.Delete
                                else created <- true

                                let vol, fmt, release = ImageSubResource.loadFile ft.FileName
                                    
                                let size = V2i(int vol.Info.Size.X, int vol.Info.Size.Y)
                                let levels =
                                    if ft.TextureParams.wantMipMaps then 
                                        Fun.Log2(max size.X size.Y |> float) |> ceil |> int
                                    else 
                                        1

                                if ft.TextureParams.wantSrgb || ft.TextureParams.wantCompressed then
                                    warnf "compressed / srgb textures not implemented atm."

        
                                let img =
                                    ctx.CreateImage2D(
                                        fmt,
                                        size,
                                        levels,
                                        VkImageUsageFlags.SampledBit ||| VkImageUsageFlags.TransferDstBit
                                    )

                                let update =
                                    command {
                                        do! img.UploadLevel(0, vol, fmt)
                                        do! Command.barrier MemoryTransfer

                                        if ft.TextureParams.wantMipMaps then
                                            do! img.GenerateMipMaps()

                                        do! img.ToLayout(VkImageLayout.ShaderReadOnlyOptimal)
                                    }

                                img, update

                            | :? PixTexture2d as pt ->
                                if created then old |> Option.iter ctx.Delete
                                else created <- true

                                let data = pt.PixImageMipMap
                                let l0 = data.[0]

                                let generate, levels =
                                    if pt.TextureParams.wantMipMaps then
                                        if data.LevelCount > 1 then 
                                            false, data.LevelCount
                                        else 
                                            true, Fun.Log2(max l0.Size.X l0.Size.Y |> float) |> ceil |> int
                                    else 
                                        false, 1

                                let format =
                                    VkFormat.ofPixFormat l0.PixFormat pt.TextureParams

                                let img =
                                    ctx.CreateImage2D(
                                        format,
                                        l0.Size,
                                        levels,
                                        VkImageUsageFlags.SampledBit ||| VkImageUsageFlags.TransferDstBit
                                    )

                                let update =
                                    command {
                                        do! img.UploadLevel(0, l0)

                                        if generate then
                                            do! img.GenerateMipMaps()
                                        else
                                            for l in 1 .. data.LevelCount-1 do
                                                do! img.UploadLevel(l, data.[l])

                                        do! img.ToLayout(VkImageLayout.ShaderReadOnlyOptimal)
                                    }

                                img, update

                            | t ->
                                failf "unknown texture type: %A" t

                    member x.Destroy h =
                        ctx.Delete h
                        created <- false
                }
        )

    member x.CreateImageView(image : Resource<Image>) =
        imageViewCache.GetOrCreate(
            [image],
            fun () ->
                { new Resource<ImageView>(imageViewCache) with
                    member x.Create old =
                        let img = image.Handle.GetValue(x)
                        let view = ctx.CreateImageView(img)
                        view, Command.nop

                    member x.Destroy h = 
                        ctx.Delete h
                }
        )

    member x.CreateSampler(sampler : IMod<SamplerStateDescription>) =
        samplerCache.GetOrCreate(
            [sampler],
            fun () ->
                { new Resource<Sampler>(samplerCache) with
                    member x.Create old =
                        old |> Option.iter ctx.Delete
                        
                        let desc = sampler.GetValue(x)
                        let sam = ctx.CreateSampler(desc)

                        sam, Command.nop

                    member x.Destroy h =
                        ctx.Delete h
                }
        )

    member x.CreateSampler(sampler : SamplerStateDescription) =
        x.CreateSampler(Mod.constant sampler)


    // shaders/pipelines

    member x.CreateShaderProgram(pass : RenderPass, surface : IMod<ISurface>) =
        shaderProgramCache.GetOrCreate(
            [pass; surface],
            fun () ->
                let device = ctx.Device
                { new Resource<ShaderProgram>(shaderProgramCache) with
                    member x.Create old =
                        old |> Option.iter device.Delete
                        let s = surface.GetValue(x)
                        let prog = device.CreateShaderProgram(runtime, s, pass)

                        prog, Command.nop

                    member x.Destroy h =
                        device.Delete h
                }
        )

    member x.CreatePipeline(pass            : RenderPass,
                            program         : Resource<ShaderProgram>,
                            inputs          : Map<string, bool * Type>,
                            geometryMode    : IMod<IndexedGeometryMode>,
                            fillMode        : IMod<FillMode>,
                            cullMode        : IMod<CullMode>,
                            blendMode       : IMod<BlendMode>,
                            depthTest       : IMod<DepthTestMode>,
                            stencilMode     : IMod<StencilMode>) =

        pipelineCache.GetOrCreate(
            [pass; program; inputs; geometryMode; cullMode; fillMode; blendMode; depthTest; stencilMode],
            fun () ->
                let attachments = pass.ColorAttachments.Length

                let samples = 
                    if attachments > 0 then
                        let sem, t = pass.ColorAttachments.[0]
                        t.samples
                    else
                        let t = pass.DepthAttachment.Value
                        t.samples

                let vertexInputState = 
                    VertexInputState.create inputs

                let inputAssembly = 
                    geometryMode |> Mod.map (fun m ->
                        let top =
                            match m with
                                | IndexedGeometryMode.LineList -> VkPrimitiveTopology.LineList
                                | IndexedGeometryMode.LineStrip -> VkPrimitiveTopology.LineStrip
                                | IndexedGeometryMode.PointList -> VkPrimitiveTopology.PointList
                                | IndexedGeometryMode.TriangleList -> VkPrimitiveTopology.TriangleList
                                | IndexedGeometryMode.TriangleStrip -> VkPrimitiveTopology.TriangleStrip
                                | m -> failf "unsupported geometry mode: %A" m
                        { restartEnable = true; topology = top }
                    )

                let rasterizerState = 
                    Mod.map2 RasterizerState.create cullMode fillMode

                let colorBlendState =
                    blendMode |> Mod.map (ColorBlendState.create attachments) 

                let multisampleState =
                    MultisampleState.create samples

                let depthState =
                    depthTest |> Mod.map DepthState.create

                let stencilState =
                    stencilMode |> Mod.map StencilState.create

                { new Resource<Pipeline>(pipelineCache) with
                    member x.Create old =
                        let pipeline =
                            ctx.CreateGraphicsPipeline {
                                renderPass              = pass
                                shaderProgram           = program.Handle.GetValue(x)
                                vertexInputState        = vertexInputState
                                inputAssembly           = inputAssembly.GetValue(x)
                                rasterizerState         = rasterizerState.GetValue(x)
                                colorBlendState         = colorBlendState.GetValue(x)
                                multisampleState        = multisampleState 
                                depthState              = depthState.GetValue(x)
                                stencilState            = stencilState.GetValue(x)
                                dynamicStates           = [| VkDynamicState.Viewport; VkDynamicState.Scissor |]
                            }
                        
                        pipeline, Command.nop

                    member x.Destroy h =
                        ctx.Delete h
                }
        )


    // uniform buffers

    member x.CreateUniformBuffer(layout : UniformBufferLayout, provider : IUniformProvider) =
        uniformBufferCache.GetOrCreate(
            [layout; provider],
            fun () ->
                let fieldNames = layout.fieldTypes |> Map.toSeq |> Seq.map fst |> Seq.toList
                let values = 
                    fieldNames 
                        |> List.map (fun n ->
                            match provider.TryGetUniform(Ag.emptyScope, Symbol.Create n) with
                                | Some value -> Symbol.Create n, (value :> IAdaptiveObject)
                                | None -> failwithf "could not get required uniform-value: %A" n
                           )
                        |> Map.ofList

                { new Resource<UniformBuffer>(uniformBufferCache) with
                    member x.Create old =
                        let buffer = 
                            match old with
                                | None -> ctx.CreateUniformBuffer(layout)
                                | Some o -> o

                        let writers = UniformWriters.writers layout values


                        let update =
                            command {
                                writers |> List.iter (fun (_,w) -> w.Write(x, buffer.Storage.Pointer))
                                buffer.IsDirty <- true
                                do! ctx.Upload(buffer)
                            }

                        buffer, update

                    member x.Destroy h =
                        ctx.Delete h
                }
        )


    // descriptor sets

    member x.CreateDescriptorSet(layout : DescriptorSetLayout,
                                 buffers : Map<int, Resource<UniformBuffer>>, 
                                 images : Map<int, Resource<ImageView> * Resource<Sampler>>) =
        descriptorSetCache.GetOrCreate(
            [layout; buffers; images],
            fun () ->
                { new Resource<DescriptorSet>(descriptorSetCache) with
                    member x.Create old =
                        let desc =
                            match old with
                                | Some o -> o
                                | None -> descriptorPool.CreateDescriptorSet(layout)
                        

                        let buffers = 
                            buffers |> Map.map (fun _ r -> 
                                UniformBuffer(r.Handle.GetValue(x))
                            )

                        let images = 
                            images |> Map.map (fun _ (v,s) ->
                                SampledImage(v.Handle.GetValue(x), s.Handle.GetValue(x))
                            )
                        
                        desc.Update(Map.union buffers images)
                        desc, Command.nop


                    member x.Destroy h =
                        descriptorPool.Delete h
                }
        )