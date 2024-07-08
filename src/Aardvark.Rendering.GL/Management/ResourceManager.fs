namespace Aardvark.Rendering.GL

#nowarn "9"

open System
open System.Collections.Concurrent
open Microsoft.FSharp.NativeInterop
open Aardvark.Base

open Aardvark.Rendering
open FSharp.Data.Adaptive
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.ShaderReflection
open Aardvark.Rendering.GL

type CastResource<'a, 'b when 'a : equality and 'b : equality>(inner : IResource<'a>) =
    inherit AdaptiveObject()
    static let th = typeof<'b>
    let handle = inner.Handle |> AVal.map unbox

    member x.Inner = inner

    override x.GetHashCode() = inner.GetHashCode()
    override x.Equals o = 
        match o with
            | :? CastResource<'a,'b> as o -> inner.Equals o.Inner
            | _ -> false

    member x.Update(token : AdaptiveToken, rt : RenderToken) =
        x.EvaluateIfNeeded token () (fun token ->
            inner.Update(token, rt)
        )

    interface IResource with  
        member x.Id = inner.Id
        member x.HandleType = th
        member x.Dispose() = inner.Dispose()
        member x.AddRef() = inner.AddRef()
        member x.RemoveRef() = inner.RemoveRef()
        member x.Update(caller, token) = x.Update(caller, token)
        member x.Info = inner.Info
        member x.IsDisposed = inner.IsDisposed
        member x.Kind = inner.Kind

    interface IResource<'b> with
        member x.Handle = handle

[<Struct>]
type private AttachmentConfig<'a> =
    {
        signature    : IFramebufferSignature
        defaultValue : aval<'a>
        attachments  : aval<Map<Symbol, 'a>>
    }

[<Struct>]
type TextureBinding =
    {
        texture : int
        target : int
    }

[<Struct>]
type TextureArrayBinding =
    {
        offset : int
        count : int
        targets : nativeptr<int>
        textures : nativeptr<int>
        samplers : nativeptr<int>
    }

[<Struct>]
type ImageBinding =
    {
        image : int
        level : int
        layered : int
        layer : int
        format : SizedInternalFormat
    }

type AdaptiveAttributeBuffer =
    {
        Type        : Type
        Frequency   : AttributeFrequency
        Format      : VertexAttributeFormat
        Stride      : int
        Offset      : int
        Resource    : IResource<Buffer>
    }

    member inline x.GetValue(t : AdaptiveToken, rt : RenderToken) =
        { Type = x.Type
          Frequency = x.Frequency
          Format = x.Format
          Stride = x.Stride
          Offset = x.Offset
          Buffer = x.Resource.Handle.GetValue(t, rt) }

    member inline x.Dispose() =
        x.Resource.Dispose()

[<RequireQualifiedAccess>]
type AdaptiveAttribute =
    | Value  of value: IAdaptiveValue * format : VertexAttributeFormat
    | Buffer of buffer: AdaptiveAttributeBuffer

    member x.ElementType =
        match x with
        | Value (v, _) -> v.ContentType
        | Buffer b -> b.Type

    member inline x.GetValue(t : AdaptiveToken, rt : RenderToken) =
        match x with
        | AdaptiveAttribute.Value (aval, format) ->
            let value = aval.GetValueUntyped t
            Attribute.Value (value, format)

        | AdaptiveAttribute.Buffer buffer ->
            Attribute.Buffer <| buffer.GetValue(t, rt)

    member inline x.Dispose() =
        match x with
        | Buffer b -> b.Dispose()
        | _ -> ()

type IndexBinding =
    {
        IndexType : OpenGl.Enums.IndexType
        Buffer    : IResource<Buffer>
    }

    member x.Dispose() =
        x.Buffer.Dispose()

type InterfaceSlots = 
    {
        // interface definition sorted by slot
        samplers : (string * FShade.GLSL.GLSLSampler)[]
        uniformBuffers : (string * FShade.GLSL.GLSLUniformBuffer)[]
        storageBuffers : (string * FShade.GLSL.GLSLStorageBuffer)[]
        images : (string * FShade.GLSL.GLSLImage)[]
    }

[<AllowNullLiteral>]
type ResourceManager private (parent : Option<ResourceManager>, ctx : Context, renderTaskLock : Option<RenderTaskLock>) =

    let derivedCache (f : ResourceManager -> ResourceCache<'a, 'b>) =
        ResourceCache<'a, 'b>(Option.map f parent, renderTaskLock)

    let bufferManager =
        match parent with
        | Some p -> p.BufferManager
        | None -> BufferManager(ctx)

    let textureManager =
        match parent with
        | Some p -> p.TextureManager
        | None -> TextureManager(ctx)

    let uniformBufferManager =
        new UniformBufferManager(ctx)

    let bufferCache             = derivedCache (fun m -> m.BufferCache)
    let textureCache            = derivedCache (fun m -> m.TextureCache)
    let indirectBufferCache     = derivedCache (fun m -> m.IndirectBufferCache)
    let programHandleCache      = ResourceCache<Program, int>(None, renderTaskLock)
    let samplerCache            = derivedCache (fun m -> m.SamplerCache)
    let vertexInputCache        = derivedCache (fun m -> m.VertexInputCache)
    let isActiveCache           = derivedCache (fun m -> m.IsActiveCache)
    let beginModeCache          = derivedCache (fun m -> m.BeginModeCache)
    let drawCallInfoCache       = derivedCache (fun m -> m.DrawCallInfoCache)
    let depthTestCache          = derivedCache (fun m -> m.DepthTestCache)
    let depthBiasCache          = derivedCache (fun m -> m.DepthBiasCache)
    let cullModeCache           = derivedCache (fun m -> m.CullModeCache)
    let frontFaceCache          = derivedCache (fun m -> m.FrontFaceCache)
    let polygonModeCache        = derivedCache (fun m -> m.PolygonModeCache)
    let blendModeCache          = derivedCache (fun m -> m.BlendModeCache)
    let colorMaskCache          = derivedCache (fun m -> m.ColorMaskCache)
    let stencilModeCache        = derivedCache (fun m -> m.StencilModeCache)
    let stencilMaskCache        = derivedCache (fun m -> m.StencilMaskCache)
    let flagCache               = derivedCache (fun m -> m.FlagCache)
    let colorCache              = derivedCache (fun m -> m.ColorCache)
    let textureBindingCache     = derivedCache (fun m -> m.TextureBindingCache)
    let imageBindingCache       = derivedCache (fun m -> m.ImageBindingCache)

    let blendModeConfigCache =
        ConcurrentDictionary<AttachmentConfig<BlendMode>, aval<GLBlendMode[]>>()

    let colorMaskConfigCache =
        ConcurrentDictionary<AttachmentConfig<ColorMask>, aval<GLColorMask[]>>()

    let hasTessDrawModeCache =
        ConcurrentDictionary<IndexedGeometryMode, UnaryCache<aval<Program>, aval<GLBeginMode>>>()

    let getTessDrawModeCache (mode : IndexedGeometryMode) =
        hasTessDrawModeCache.GetOrAdd(mode, fun mode ->
            UnaryCache(AVal.map (fun t -> ctx.ToBeginMode(mode, t.HasTessellation)))
        )

    let ifaceSlotCache = ConcurrentDictionary<FShade.GLSL.GLSLProgramInterface, InterfaceSlots>()

    let textureArrayCache =
        UnaryCache<aval<ITexture[]>, _>(
            fun _arr -> ConcurrentDictionary<int * TextureProperties, Lazy<IResource<Texture, TextureBinding>[]>>()
        )

    let staticSamplerStateCache = ConcurrentDictionary<FShade.SamplerState, aval<SamplerState>>()
    let dynamicSamplerStateCache = ConcurrentDictionary<Symbol * SamplerState, UnaryCache<aval<(Symbol -> SamplerState -> SamplerState)>, aval<SamplerState>>>()
    let samplerDescriptionCache = ConcurrentDictionary<FShade.SamplerState, SamplerState>() 

    member private x.BufferManager = bufferManager
    member private x.TextureManager = textureManager

    member private x.BufferCache              : ResourceCache<Buffer, int>                               = bufferCache
    member private x.TextureCache             : ResourceCache<Texture, TextureBinding>                   = textureCache
    member private x.IndirectBufferCache      : ResourceCache<GLIndirectBuffer, IndirectDrawArgs>        = indirectBufferCache
    member private x.SamplerCache             : ResourceCache<Sampler, int>                              = samplerCache
    member private x.VertexInputCache         : ResourceCache<VertexInputBindingHandle, int>             = vertexInputCache
    member private x.IsActiveCache            : ResourceCache<bool, int>                                 = isActiveCache
    member private x.BeginModeCache           : ResourceCache<GLBeginMode, GLBeginMode>                  = beginModeCache
    member private x.DrawCallInfoCache        : ResourceCache<DrawCallInfoList, DrawCallInfoList>        = drawCallInfoCache
    member private x.DepthTestCache           : ResourceCache<int, int>                                  = depthTestCache
    member private x.DepthBiasCache           : ResourceCache<DepthBiasInfo, DepthBiasInfo>              = depthBiasCache
    member private x.CullModeCache            : ResourceCache<int, int>                                  = cullModeCache
    member private x.FrontFaceCache           : ResourceCache<int, int>                                  = frontFaceCache
    member private x.PolygonModeCache         : ResourceCache<int, int>                                  = polygonModeCache
    member private x.BlendModeCache           : ResourceCache<nativeptr<GLBlendMode>, nativeint>         = blendModeCache
    member private x.ColorMaskCache           : ResourceCache<nativeptr<GLColorMask>, nativeint>         = colorMaskCache
    member private x.StencilModeCache         : ResourceCache<GLStencilMode, GLStencilMode>              = stencilModeCache
    member private x.StencilMaskCache         : ResourceCache<uint32, uint32>                            = stencilMaskCache
    member private x.FlagCache                : ResourceCache<bool, int>                                 = flagCache
    member private x.ColorCache               : ResourceCache<C4f, C4f>                                  = colorCache
    member private x.TextureBindingCache      : ResourceCache<TextureArrayBinding, TextureArrayBinding>  = textureBindingCache
    member private x.ImageBindingCache        : ResourceCache<ImageBinding, ImageBinding>                = imageBindingCache

    new(parent, lock) = new ResourceManager(Some parent, parent.Context, lock)
    new(ctx, lock) = new ResourceManager(None, ctx, lock)

    member x.Context = ctx

    member x.CreateBuffer(data : aval<#IBuffer>) =
        bufferCache.GetOrCreate<#IBuffer>(data, fun () -> {
            create = fun b      -> bufferManager.Create b
            update = fun h b    -> bufferManager.Update(h, b)
            delete = fun h      -> bufferManager.Delete h
            unwrap = fun b      -> BufferManager.TryUnwrap b
            info =   fun h      -> h.SizeInBytes |> Mem |> ResourceInfo
            view =   fun h      -> h.Handle
            kind = ResourceKind.Buffer
        })

    member x.CreateBuffer(data : aval<Array>) =
        let buffer = data |> AdaptiveResource.mapNonAdaptive ArrayBuffer
        x.CreateBuffer(buffer)

    member private x.CreateTexture(data : aval<#ITexture>, properties : TextureProperties) : IResource<Texture, TextureBinding> =
        textureCache.GetOrCreate(data, [properties :> obj], fun () -> {
            create = fun b      -> textureManager.Create(b, properties)
            update = fun h b    -> textureManager.Update(h, b, properties)
            delete = fun h      -> textureManager.Delete h
            unwrap = fun _      -> ValueNone
            info =   fun h      -> h.SizeInBytes |> Mem |> ResourceInfo
            view =   fun r      -> { texture = r.Handle; target = Translations.toGLTarget r.Dimension r.IsArray r.Multisamples }
            kind = ResourceKind.Texture
        })

    member x.CreateTexture(data : aval<#ITexture>, samplerType : FShade.GLSL.GLSLSamplerType) : IResource<Texture, TextureBinding> =
        x.CreateTexture(data, samplerType.Properties)

    // Workaround for some APIs accepting texture levels as sampler input (e.g. GPGPU image reduce).
    // GL cannot directly bind specific texture levels and ranges to samplers.
    // We would have to use texture views, which are not guaranteed to be supported (MacOS does not obviously).
    // Here we just bind the whole texture as usual and do some sanity checks.
    member x.CreateTexture(data : aval<ITextureLevel>, samplerType : FShade.GLSL.GLSLSamplerType) =
        let data =
            data |> AdaptiveResource.mapNonAdaptive (fun l ->
                if l.Level <> 0 then
                    failf "cannot bind texture level %d (must be zero)" l.Level

                let wholeRange = Range1i(0, l.Texture.Slices - 1)
                if l.Slices <> wholeRange then
                    failf "cannot bind texture range %A (must be %A)" l.Slices wholeRange

                l.Texture
            )

        x.CreateTexture(data, samplerType.Properties)

    member x.CreateIndirectBuffer(indexed : bool, data : aval<IndirectBuffer>) =
        
        // TODO: proper cache for transformIndirectData -> if transform necessary duplicates are uploaded at the moment

        let transformIndirectData (buffer : IBuffer) =
            match buffer with
            | :? ArrayBuffer as ab ->
                match ab.Data with
                | :? (DrawCallInfo[]) as data -> 
                    let transformed = data |> Array.map (fun dc -> 
                                DrawCallInfo(
                                    FaceVertexCount = dc.FaceVertexCount,
                                    InstanceCount = dc.InstanceCount,
                                    FirstIndex = dc.FirstIndex,
                                    BaseVertex = dc.FirstInstance,
                                    FirstInstance = dc.BaseVertex
                                ))
                    ArrayBuffer(transformed) :> IBuffer
                | _ -> failwith "[GL] IndirectBuffer data supposed to be DrawCallInfo[]"
            | _ -> 
                buffer

        indirectBufferCache.GetOrCreate<IndirectBuffer>(data, [indexed :> obj], fun () -> {
            create = fun b ->
                let (buffer, own) =
                    match BufferManager.TryUnwrap b.Buffer with
                    | ValueSome h ->
                        if b.Indexed <> indexed then
                            if b.Indexed then failwithf "[GL] Expected non-indexed data but indirect buffer contains indexed data."
                            else failwithf "[GL] Expected indexed data but indirect buffer contains non-indexed data."

                        h, false

                    | _ ->
                        let layoutedData = if b.Indexed <> indexed then transformIndirectData b.Buffer else b.Buffer
                        bufferManager.Create(layoutedData), true

                GLIndirectBuffer(buffer, b.Count, b.Stride, indexed, own)

            update = fun h b ->
                if h.Indexed <> b.Indexed then
                    failwith "[GL] cannot change Indexed option of IndirectBuffer"

                let buffer =
                    match BufferManager.TryUnwrap b.Buffer with
                    | ValueSome v ->
                        if h.OwnResource then
                            failwith "[GL] cannot change IndirectBuffer type"
                        v

                    | _ ->
                        if not h.OwnResource then
                            failwith "[GL] cannot change IndirectBuffer type"
                        let layoutedData = if b.Indexed <> indexed then transformIndirectData b.Buffer else b.Buffer
                        bufferManager.Update(h.Buffer, layoutedData)

                if h.Buffer = buffer && h.Count = b.Count && h.Stride = b.Stride then // OwnResource and Indexed cannot change
                    h // return old to remain reference equal -> will be counted as InPlaceUpdate (no real performance difference)
                else
                    GLIndirectBuffer(buffer, b.Count, b.Stride, indexed, h.OwnResource)

            delete = fun h   -> if h.OwnResource then bufferManager.Delete(h.Buffer)
            unwrap = fun _   -> ValueNone
            info =   fun h   -> h.Buffer.SizeInBytes |> Mem |> ResourceInfo
            view =   fun h   -> IndirectDrawArgs(h.Buffer.Handle, h.Count, h.Stride)
            kind = ResourceKind.IndirectBuffer
        })

    member x.GetSamplerStateDescription(samplerState : FShade.SamplerState) =
        samplerDescriptionCache.GetOrAdd(samplerState, fun sam -> sam.SamplerState)

    member x.GetDynamicSamplerState(texName : Symbol, samplerState : SamplerState, modifier : aval<(Symbol -> SamplerState -> SamplerState)>) : aval<SamplerState> =
        dynamicSamplerStateCache.GetOrAdd((texName, samplerState), fun (sym, sam) ->
            UnaryCache(fun modi -> modi |> AVal.map (fun f -> f sym sam))
        ).Invoke(modifier)

    member x.GetStaticSamplerState(samplerState : FShade.SamplerState) =
        staticSamplerStateCache.GetOrAdd(samplerState, fun sam -> AVal.constant (sam.SamplerState))

    member x.GetInterfaceSlots(iface : FShade.GLSL.GLSLProgramInterface) = 
        ifaceSlotCache.GetOrAdd(iface, (fun iface ->
                { samplers = iface.samplers |> MapExt.toSeq |> Seq.sortBy (fun (_, sam) -> sam.samplerBinding) |> Seq.toArray
                  uniformBuffers = iface.uniformBuffers |> MapExt.toSeq |> Seq.sortBy (fun (_, ub) -> ub.ubBinding) |> Seq.toArray
                  storageBuffers = iface.storageBuffers |> MapExt.toSeq |> Seq.sortBy (fun (_, sb) -> sb.ssbBinding) |> Seq.toArray 
                  images = iface.images |> MapExt.toSeq |> Seq.sortBy (fun (_, img) -> img.Dimension) |> Seq.toArray }
            ))

    member x.CreateSurface(signature : IFramebufferSignature, surface : Aardvark.Rendering.Surface, topology : IndexedGeometryMode) =

        let (iface, result) = ctx.CreateProgram(signature, surface, topology)

        let programHandle = 
            programHandleCache.GetOrCreate<Program>(result, fun () -> {
                create = fun b      -> b
                update = fun h b    -> b
                delete = fun h      -> ()
                unwrap = fun _      -> ValueNone
                info =   fun h      -> ResourceInfo.Zero
                view =   fun h      -> h.Handle
                kind = ResourceKind.ShaderProgram
            })

        iface, programHandle

    member x.CreateTextureArray(slotCount : int, textureArray : aval<ITexture[]>, samplerType : FShade.GLSL.GLSLSamplerType) : IResource<Texture, TextureBinding>[] =
        let innerCache = textureArrayCache.Invoke(textureArray)
        let properties = samplerType.Properties

        innerCache.GetOrAdd((slotCount, properties), fun _ ->
            lazy (
                Array.init slotCount (fun i ->
                    let arr =
                        textureArray |> AVal.map (fun t ->
                            if i < t.Length then
                                t.[i]
                            else
                                nullTexture
                        )

                    x.CreateTexture(arr, properties)
                )
            )
        ).Value

    member x.CreateSampler (sam : aval<SamplerState>) =
        samplerCache.GetOrCreate<SamplerState>(sam, fun () -> {
            create = fun b      -> ctx.CreateSampler b
            update = fun h b    -> ctx.Update(h,b); h
            delete = fun h      -> ctx.Delete h
            unwrap = fun _      -> ValueNone
            info =   fun h      -> ResourceInfo.Zero
            view =   fun h      -> h.Handle
            kind = ResourceKind.SamplerState
        })

    member x.CreateTextureBinding(slots : Range1i, bindings : (IResource<Texture, TextureBinding> * IResource<Sampler, int>)[]) =
        let slotCount = slots.Size + 1
        if slotCount <= 0 then
            failf "invalid slot range"

        if bindings.Length < slotCount then
            failf "not enough bindings"

        textureBindingCache.GetOrCreate(
            [slots :> obj; bindings :> obj],
            fun () ->
                { new Resource<TextureArrayBinding, TextureArrayBinding>(ResourceKind.Unknown) with
                    member x.View a = a
                    member x.GetInfo _ = ResourceInfo.Zero

                    member x.Create (token : AdaptiveToken, renderToken : RenderToken, old : Option<TextureArrayBinding>) =
                        let bindingHandle =
                            match old with
                            | Some o -> o
                            | _ ->
                                for (t, s) in bindings do
                                    t.AddRef()
                                    s.AddRef()

                                let offset = slots.Min
                                let count = slotCount
                                let targets = NativePtr.alloc slotCount
                                let samplers = NativePtr.alloc slotCount
                                let textures = NativePtr.alloc slotCount

                                {
                                    offset = offset
                                    count = count
                                    targets = targets
                                    samplers = samplers
                                    textures = textures
                                }

                        for slot = 0 to slotCount - 1 do
                            let texture, sampler = bindings.[slot]
                            texture.Update(token, renderToken)
                            sampler.Update(token, renderToken)

                            let tt = NativePtr.read texture.Pointer
                            let ss = NativePtr.read sampler.Pointer
                            NativePtr.set bindingHandle.textures slot tt.texture
                            NativePtr.set bindingHandle.targets slot tt.target
                            NativePtr.set bindingHandle.samplers slot ss

                        bindingHandle

                    member x.Destroy (b : TextureArrayBinding) =
                        if slots.Size >= 0 then
                            NativePtr.free b.targets
                            NativePtr.free b.textures
                            NativePtr.free b.samplers

                            for t, s in bindings do
                                t.RemoveRef()
                                s.RemoveRef()
                }
        )

    member x.CreateTextureBinding(slots : Range1i, textures : IResource<Texture, TextureBinding>[], sampler : IResource<Sampler, int>) =
        let slotCount = slots.Size + 1
        if slotCount <= 0 then
            failf "invalid slot range"

        if textures.Length < slotCount then
            failf "not enough bindings"

        textureBindingCache.GetOrCreate(
            [slots :> obj; textures :> obj; sampler :> obj],
            fun () ->
                { new Resource<TextureArrayBinding, TextureArrayBinding>(ResourceKind.Unknown) with
                    member x.View a = a
                    member x.GetInfo _ = ResourceInfo.Zero

                    member x.Create (token : AdaptiveToken, renderToken : RenderToken, old : Option<TextureArrayBinding>) =
                        let bindingHandle =
                            match old with
                            | Some o -> o
                            | _ ->
                                sampler.AddRef()

                                for t in textures do
                                    t.AddRef()

                                let offset = slots.Min
                                let count = slotCount
                                let targets = NativePtr.alloc slotCount
                                let samplers = NativePtr.alloc slotCount
                                let textures = NativePtr.alloc slotCount

                                {
                                    offset = offset
                                    count = count
                                    targets = targets
                                    samplers = samplers
                                    textures = textures
                                }

                        sampler.Update(token, renderToken)
                        let ss = NativePtr.read sampler.Pointer

                        for slot = 0 to slotCount - 1 do
                            let texture = textures.[slot]
                            texture.Update(token, renderToken)

                            let tt = NativePtr.read texture.Pointer
                            NativePtr.set bindingHandle.textures slot tt.texture
                            NativePtr.set bindingHandle.targets slot tt.target
                            NativePtr.set bindingHandle.samplers slot ss

                        bindingHandle

                    member x.Destroy (b : TextureArrayBinding) =
                        if slots.Size >= 0 then
                            NativePtr.free b.targets
                            NativePtr.free b.textures
                            NativePtr.free b.samplers

                            sampler.RemoveRef()

                            for t in textures do
                                t.RemoveRef()
                }
        )

    member private x.CreateImageBinding(input : aval<#ITexture>, level : aval<int>, layers : aval<Range1i>, properties : TextureProperties) =
        use textureResource = x.CreateTexture(input, properties)

        imageBindingCache.GetOrCreate(
            [textureResource :> obj; level :> obj; layers :> obj],
            fun () ->
                { new Resource<ImageBinding, ImageBinding>(ResourceKind.Unknown) with
                    member x.View b = b
                    member x.GetInfo _ = ResourceInfo.Zero
                    member x.Create (t, rt, old) =
                        match old with
                        | None -> textureResource.AddRef()
                        | _ -> ()

                        textureResource.Update(t, rt)
                        let texture = textureResource.Handle.GetValue()
                        let level = level.GetValue(t, rt)
                        let layers = layers.GetValue(t, rt)
                        let format = TextureFormat.toSizedInternalFormat texture.Format

                        let layered =
                            if layers.Size > 1 then 1
                            else 0

                        if layered = 1 && layers.Min <> 0 then
                            failf "cannot bind texture layers starting at %d to image (must be 0 or non-layered binding)" layers.Min

                        { image   = texture.Handle
                          level   = level
                          layered = layered
                          layer   = layers.Min
                          format  = format }

                    member x.Destroy _ =
                        textureResource.RemoveRef()
                }
        )

    member x.CreateImageBinding(input : aval<ITexture>, imageType : FShade.GLSL.GLSLImageType) =
        let level = AVal.constant 0
        let layers = AVal.constant <| Range1i()
        x.CreateImageBinding(input, level, layers, imageType.Properties)

    member x.CreateImageBinding(input : aval<ITextureLevel>, imageType : FShade.GLSL.GLSLImageType) =
        let texture = input |> AdaptiveResource.mapNonAdaptive (fun l -> l.Texture)
        let level = input |> AVal.mapNonAdaptive (fun l -> l.Level)
        let layers = input |> AVal.mapNonAdaptive (fun l -> l.Slices)
        x.CreateImageBinding(texture, level, layers, imageType.Properties)

    member x.CreateVertexInputBinding(bindings : (int * AdaptiveAttribute)[], index : IndexBinding option) =
        vertexInputCache.GetOrCreate(
            [ bindings :> obj; index :> obj ],
            fun () ->
                { new Resource<VertexInputBindingHandle, int>(ResourceKind.VertexArrayObject) with
                    member x.View a = 0
                    member x.GetInfo _ = ResourceInfo.Zero

                    member x.Create (t : AdaptiveToken, rt : RenderToken, old : Option<VertexInputBindingHandle>) =
                        let attributes =
                            bindings |> Array.map (fun (i, a) -> i, a.GetValue(t, rt))

                        let index =
                            match index with
                            | Some i -> i.Buffer.Handle.GetValue(t, rt) |> Some
                            | _ -> None

                        match old with
                        | Some old ->
                            ctx.Update(old, index, attributes)
                            old
                        | None ->
                            ctx.CreateVertexInputBinding(index, attributes)

                    member x.Destroy vao =
                        ctx.Delete vao
                }
        )

    member x.CreateUniformBuffer(scope : Ag.Scope, layout : FShade.GLSL.GLSLUniformBuffer, uniforms : IUniformProvider) =
        uniformBufferManager.CreateUniformBuffer(layout, scope, uniforms)
 
 
      
    member x.CreateIsActive(value : aval<bool>) =
        isActiveCache.GetOrCreate(value, fun () -> {
            create = fun b      -> b
            update = fun h b    -> b
            delete = fun h      -> ()
            unwrap = fun _      -> ValueNone
            info =   fun h      -> ResourceInfo.Zero
            view =   fun h      -> if h then 1 else 0
            kind = ResourceKind.Unknown
        })
      
    member x.CreateBeginMode(prog : aval<Program>, drawMode : IndexedGeometryMode) =
        let mode = getTessDrawModeCache(drawMode).Invoke(prog)
        beginModeCache.GetOrCreate(mode, fun () -> {
            create = fun b      -> b
            update = fun h b    -> b
            delete = fun h      -> ()
            unwrap = fun _      -> ValueNone
            info =   fun h      -> ResourceInfo.Zero
            view = id
            kind = ResourceKind.Unknown
        })

    member x.CreateDrawCallInfoList(value : aval<list<DrawCallInfo>>) =
        drawCallInfoCache.GetOrCreate(value, fun () -> {
            create = fun b      -> ctx.CreateDrawCallInfoList(List.toArray b)
            update = fun h b    -> ctx.Update(h,List.toArray b)
            delete = fun h      -> ctx.Delete h
            unwrap = fun _      -> ValueNone
            info =   fun h      -> ResourceInfo.Zero
            view = id
            kind = ResourceKind.Unknown
        })

    member x.CreateDepthTest(value : aval<DepthTest>) =
        depthTestCache.GetOrCreate(value, fun () -> {
            create = fun b      -> ctx.ToDepthTest b
            update = fun h b    -> ctx.ToDepthTest b
            delete = fun h      -> ()
            unwrap = fun _      -> ValueNone
            info =   fun h      -> ResourceInfo.Zero
            view = id
            kind = ResourceKind.Unknown
        })

    member x.CreateDepthBias(value : aval<DepthBias>) =
        depthBiasCache.GetOrCreate(value, fun () -> {
            create = fun b      -> ctx.ToDepthBias b
            update = fun h b    -> ctx.ToDepthBias b
            delete = fun h      -> ()
            unwrap = fun _      -> ValueNone
            info =   fun h      -> ResourceInfo.Zero
            view = id
            kind = ResourceKind.Unknown
        })

    member x.CreateCullMode(value : aval<CullMode>) =
        cullModeCache.GetOrCreate(value, fun () -> {
            create = fun b      -> ctx.ToCullMode b
            update = fun h b    -> ctx.ToCullMode b
            delete = fun h      -> ()
            unwrap = fun _      -> ValueNone
            info =   fun h      -> ResourceInfo.Zero
            view = id
            kind = ResourceKind.Unknown
        })

    member x.CreateFrontFace(mode : aval<WindingOrder>) =
        frontFaceCache.GetOrCreate(mode, fun () -> {
            create = fun b      -> ctx.ToFrontFace b
            update = fun h b    -> ctx.ToFrontFace b
            delete = fun h      -> ()
            unwrap = fun _      -> ValueNone
            info =   fun h      -> ResourceInfo.Zero
            view = id
            kind = ResourceKind.Unknown
        })

    member x.CreatePolygonMode(value : aval<FillMode>) =
        polygonModeCache.GetOrCreate(value, fun () -> {
            create = fun b      -> ctx.ToPolygonMode(b)
            update = fun h b    -> ctx.ToPolygonMode(b)
            delete = fun h      -> ()
            unwrap = fun _      -> ValueNone
            info =   fun h      -> ResourceInfo.Zero
            view =  id
            kind = ResourceKind.Unknown
        })

    member private x.CreatePerAttachmentValues<'T, 'U>(cache : ConcurrentDictionary<AttachmentConfig<'T>, aval<'U[]>>, mapping : 'T -> 'U,
                                                       signature : IFramebufferSignature, def : aval<'T>, attachments : aval<Map<Symbol, 'T>>) =
        let config = { signature = signature; defaultValue = def; attachments = attachments }

        cache.GetOrAdd(config, fun cfg ->
            let attachments = cfg.signature.ColorAttachments
            let slots = cfg.signature.ColorAttachmentSlots

            (cfg.defaultValue, cfg.attachments) ||> AVal.map2 (fun def modes ->
                Array.init slots (fun i ->
                    attachments
                    |> Map.tryFind i
                    |> Option.bind (fun att -> modes |> Map.tryFind att.Name)
                    |> Option.defaultValue def
                    |> mapping          
                )
            )
        )

    member x.CreateBlendModes(signature : IFramebufferSignature, mode : aval<BlendMode>, attachments : aval<Map<Symbol, BlendMode>>) =
        let value =
            x.CreatePerAttachmentValues(blendModeConfigCache, ctx.ToBlendMode, signature, mode, attachments)

        blendModeCache.GetOrCreate(value, fun () -> {
            create = fun b      -> NativePtr.allocArray b
            update = fun h b    -> h |> NativePtr.setArray b; h
            delete = fun h      -> NativePtr.free h
            unwrap = fun _      -> ValueNone
            info =   fun h      -> ResourceInfo.Zero
            view = NativePtr.toNativeInt
            kind = ResourceKind.Unknown
        })

    member x.CreateColorMasks(signature : IFramebufferSignature, mask : aval<ColorMask>, attachments : aval<Map<Symbol, ColorMask>>) =
        let value =
            x.CreatePerAttachmentValues(colorMaskConfigCache, ctx.ToColorMask, signature, mask, attachments)

        colorMaskCache.GetOrCreate(value, fun () -> {
            create = fun b      -> NativePtr.allocArray b
            update = fun h b    -> h |> NativePtr.setArray b; h
            delete = fun h      -> NativePtr.free h
            unwrap = fun _      -> ValueNone
            info =   fun h      -> ResourceInfo.Zero
            view = NativePtr.toNativeInt
            kind = ResourceKind.Unknown
        })

    member x.CreateStencilMode(value : aval<StencilMode>) =
        stencilModeCache.GetOrCreate(value, fun () -> {
            create = fun b      -> ctx.ToStencilMode b
            update = fun h b    -> ctx.ToStencilMode b
            delete = fun h      -> ()
            unwrap = fun _      -> ValueNone
            info =   fun h      -> ResourceInfo.Zero
            view = id
            kind = ResourceKind.Unknown
        })

    member x.CreateStencilMask(value : aval<StencilMask>) =
        stencilMaskCache.GetOrCreate(value, fun () -> {
            create = fun b      -> uint32 b
            update = fun h b    -> uint32 b
            delete = fun h      -> ()
            unwrap = fun _      -> ValueNone
            info =   fun h      -> ResourceInfo.Zero
            view = id
            kind = ResourceKind.Unknown
        })

    member x.CreateFlag (value : aval<bool>) =
        flagCache.GetOrCreate(value, fun () -> {
            create = fun b      -> b
            update = fun h b    -> b
            delete = fun h      -> ()
            unwrap = fun _      -> ValueNone
            info =   fun h      -> ResourceInfo.Zero
            view =   fun v -> if v then 1 else 0
            kind = ResourceKind.Unknown
        })

    member x.CreateColor (value : aval<C4f>) =
        colorCache.GetOrCreate(value, fun () -> {
            create = fun b      -> b
            update = fun h b    -> b
            delete = fun h      -> ()
            unwrap = fun _      -> ValueNone
            info =   fun h      -> ResourceInfo.Zero
            view =   id
            kind = ResourceKind.Unknown
        })

    member x.Dispose() =
        uniformBufferManager.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()