namespace Aardvark.Rendering.GL

#nowarn "9"

open System
open Aardvark.Base.Incremental
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.ShaderReflection
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL
open Aardvark.Base.Runtime
open FShade.GLSL
open System.Threading

[<AutoOpen>]
module private Hacky =
    let toBufferCache = 
        UnaryCache<IMod, IMod<IBuffer>>(fun m ->
            Mod.custom (fun t -> 
                match m.GetValue t with
                    | :? Array as a -> ArrayBuffer(a) :> IBuffer
                    | :? IBuffer as b -> b
                    | _ -> failwith "invalid storage buffer content"
            )
        )

type PreparedPipelineState =
    {
        pContext : Context

        pFramebufferSignature : IFramebufferSignature
        pLastTextureSlot : int
        pProgram : IResource<Program, int>
        pProgramInterface : GLSLProgramInterface
        pUniformBuffers : Map<int, IResource<UniformBufferView, int>>
        pStorageBuffers : Map<int, IResource<Buffer, int>>
        pUniforms : Map<int, IResource<UniformLocation, nativeint>>
        pTextures : IResource<TextureBinding, TextureBinding>
        
        pDepthTestMode : IResource<DepthTestInfo, DepthTestInfo>
        pCullMode : IResource<int, int>
        pPolygonMode : IResource<int, int>
        pBlendMode : IResource<GLBlendMode, GLBlendMode>
        pStencilMode : IResource<GLStencilMode, GLStencilMode>
        pConservativeRaster : IResource<bool, int>
        pMultisample : IResource<bool, int>

        pColorAttachmentCount : int
        pDrawBuffers : Option<DrawBufferConfig>
        pColorBufferMasks : Option<list<V4i>>
        pDepthBufferMask : bool
        pStencilBufferMask : bool
        
    } 

    member x.Resources =
        seq {
            yield x.pProgram :> IResource
            for (_,b) in Map.toSeq x.pUniformBuffers do
                yield b :> _
                
            for (_,b) in Map.toSeq x.pStorageBuffers do
                yield b :> _

            for (_,u) in Map.toSeq x.pUniforms do
                yield u :> _

            yield x.pTextures :> _
            yield x.pConservativeRaster :> _
            yield x.pMultisample :> _
            yield x.pDepthTestMode :> _
            yield x.pCullMode :> _
            yield x.pPolygonMode :> _
            yield x.pBlendMode :> _
            yield x.pStencilMode :> _
        }

    //member x.Update(caller : AdaptiveToken, token : RenderToken) =
    //    use ctxToken = x.pContext.ResourceLock

    //    x.pProgram.Update(caller, token)

    //    for (_,ub) in x.pUniformBuffers |> Map.toSeq do
    //        ub.Update(caller, token)
            
    //    for (_,ub) in x.pStorageBuffers |> Map.toSeq do
    //        ub.Update(caller, token)

    //    for (_,ul) in x.pUniforms |> Map.toSeq do
    //        ul.Update(caller, token)

    //    x.pTextures.Update(caller, token)
        
        
    //    x.pDepthTestMode.Update(caller, token)
    //    x.pCullMode.Update(caller, token)
    //    x.pPolygonMode.Update(caller, token)
    //    x.pBlendMode.Update(caller, token)
    //    x.pStencilMode.Update(caller, token)
    //    x.pConservativeRaster.Update(caller, token)
    //    x.pMultisample.Update(caller, token)

    member x.Dispose() =
        lock x (fun () -> 
            // ObjDisposed might occur here if GL is dead already and render objects get disposed nondeterministically by finalizer thread.
            let resourceLock = try Some x.pContext.ResourceLock with :? ObjectDisposedException as o -> None

            match resourceLock with
                | None ->
                    // OpenGL already dead
                    ()
                | Some l -> 
                    use resourceLock = l

                    OpenTK.Graphics.OpenGL4.GL.UnbindAllBuffers()

                    match x.pDrawBuffers with
                        | Some b -> b.RemoveRef()
                        | _ -> ()

                            

                    x.pTextures.Dispose()
                    x.pUniforms |> Map.iter (fun _ (ul) -> ul.Dispose())
                    x.pUniformBuffers |> Map.iter (fun _ (ub) -> ub.Dispose())
                    x.pStorageBuffers |> Map.iter (fun _ (ub) -> ub.Dispose())
                    x.pProgram.Dispose()
                    
                    x.pDepthTestMode.Dispose()
                    x.pCullMode.Dispose()
                    x.pPolygonMode.Dispose()
                    x.pBlendMode.Dispose()
                    x.pStencilMode.Dispose()
                    x.pConservativeRaster.Dispose()
                    x.pMultisample.Dispose()
        )

    interface IDisposable with
        member x.Dispose() = x.Dispose()
 

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PreparedPipelineState =

    let ofRenderObject (fboSignature : IFramebufferSignature) (x : ResourceManager) (rj : RenderObject) =
        // use a context token to avoid making context current/uncurrent repeatedly
        use token = x.Context.ResourceLock
        
        let iface, program = x.CreateSurface(fboSignature, rj.Surface, rj.Mode)

        GL.Check "[Prepare] Create Surface"
        
        // create all UniformBuffers requested by the program
        let uniformBuffers =
            iface.uniformBuffers
                |> MapExt.toList
                |> List.map (fun (_,block) ->
                    block.ubBinding, x.CreateUniformBuffer(rj.AttributeScope, block, rj.Uniforms)
                   )
                |> Map.ofList

        GL.Check "[Prepare] Uniform Buffers"

        let storageBuffers = 
            iface.storageBuffers
                |> MapExt.toList
                |> List.map (fun (_,buf) ->
                    
                    let buffer = 
                        match rj.Uniforms.TryGetUniform(rj.AttributeScope, Symbol.Create buf.ssbName) with
                            | Some (:? IMod<IBuffer> as b) ->
                                x.CreateBuffer(b)
                            | Some m ->
                                let o = toBufferCache.Invoke(m)
                                x.CreateBuffer(o)
                            | _ ->
                                failwithf "[GL] could not find storage buffer %A" buf.ssbName

                    buf.ssbBinding, buffer
                )
                |> Map.ofList



        // create all requested Textures
        let lastTextureSlot = ref -1

        let samplerModifier = 
            match rj.Uniforms.TryGetUniform(rj.AttributeScope, DefaultSemantic.SamplerStateModifier) with
                | Some(:? IMod<Symbol -> SamplerStateDescription -> SamplerStateDescription> as mode) ->
                    Some mode
                | _ ->
                    None

        let textures =
            iface.samplers
                |> MapExt.toList
                |> List.collect (fun (_,u) ->
                    u.samplerTextures |> List.mapi (fun i  info ->
                        u, i, info
                    )
                   )
                |> List.choose (fun (sampler, index, (texName, samplerState)) ->
                    let name = sampler.samplerName
                    let samplerInfo = { textureName = Symbol.Create texName; samplerState = x.GetSamplerStateDescription(samplerState) } // SamplerStateDescription ARE BUILT OVER AND OVER AGAIN?
                    //let samplerInfo = { textureName = Symbol.Create texName; samplerState = samplerState.SamplerStateDescription } // SamplerStateDescription ARE BUILT OVER AND OVER AGAIN?

                    let sem = samplerInfo.textureName
                    let samplerState = samplerInfo.samplerState

                    match rj.Uniforms.TryGetUniform(rj.AttributeScope, sem) with
                        | Some tex ->
        
                            let sammy =
                                match samplerModifier with
                                    | Some modifier -> 
                                        modifier |> Mod.map (fun f -> f sem samplerState) // NEW MOD SHOULD BE CREAED CACHED BASED ON (tex, modifier)
                                    | None -> 
                                        Mod.constant samplerState

                            let xx = sammy
                            let s = x.CreateSampler(sammy)

                            match tex with
                                | :? IMod<ITexture> as value ->
                                    let t = x.CreateTexture(value)
                                    lastTextureSlot := sampler.samplerBinding + index
                                    Some (!lastTextureSlot, (t, s))

                                | :? IMod<ITexture[]> as values ->
                                    let t = x.CreateTexture(values |> Mod.map (fun arr -> arr.[index])) // SHOULD BE CACHED BASED ON INPUT MOD
                                    lastTextureSlot := sampler.samplerBinding + index
                                    Some (!lastTextureSlot, (t, s))

                                | _ ->
                                    Log.warn "unexpected texture type %A: %A" sem tex
                                    None
                        | _ ->
                            Log.warn "texture %A not found" sem
                            None
                    )
                |> Map.ofList

        let textureBinding = 
            x.CreateTextureBinding(textures)

        
        textures |> Map.iter (fun _ (t,s) ->
            t.RemoveRef()
            s.RemoveRef()
        )
        GL.Check "[Prepare] Textures"
        
        let attachments = fboSignature.ColorAttachments |> Map.toList
        let attachmentCount = if attachments.Length > 0 then 1 + (attachments |> List.map (fun (i,_) -> i) |> List.max) else 0

        let colorMasks =
            match rj.WriteBuffers with
                | Some b ->
                    let isAll = fboSignature.ColorAttachments |> Map.toSeq |> Seq.forall (fun (_,(sem,_)) -> Set.contains sem b)
                    if isAll then
                        None
                    else
                        let masks = Array.zeroCreate attachmentCount
                        for (index, (sem, att)) in attachments do
                            if Set.contains sem b then
                                masks.[index] <- V4i.IIII
                            else
                                masks.[index] <- V4i.OOOO

                        Some (Array.toList masks)
                | _ ->
                    None

        let drawBuffers = 
            match rj.WriteBuffers with
                | Some set -> 
                    let man = DrawBufferManager.Get(fboSignature)
                    man.CreateConfig(set) |> Some
                | _ -> None

        let depthMask =
            match rj.WriteBuffers with
                | Some b -> Set.contains DefaultSemantic.Depth b
                | None -> true

        let stencilMask =
            match rj.WriteBuffers with
                | Some b -> Set.contains DefaultSemantic.Stencil b
                | None -> true
                
        let depthTest = x.CreateDepthTest rj.DepthTest
        let cullMode = x.CreateCullMode rj.CullMode
        let polygonMode = x.CreatePolygonMode rj.FillMode
        let blendMode = x.CreateBlendMode rj.BlendMode
        let stencilMode = x.CreateStencilMode rj.StencilMode
        let conservativeRaster = x.CreateFlag rj.ConservativeRaster
        let multisample = x.CreateFlag rj.Multisample
        
        {
            pContext = x.Context
            pFramebufferSignature = fboSignature
            pLastTextureSlot = !lastTextureSlot
            pProgram = program
            pProgramInterface = iface
            pStorageBuffers = storageBuffers
            pUniformBuffers = uniformBuffers
            pUniforms = Map.empty
            pTextures = textureBinding
            pColorAttachmentCount = attachmentCount
            pDrawBuffers = drawBuffers
            pColorBufferMasks = colorMasks
            pDepthBufferMask = depthMask
            pStencilBufferMask = stencilMask
            pDepthTestMode = depthTest
            pCullMode = cullMode
            pPolygonMode = polygonMode
            pBlendMode = blendMode
            pStencilMode = stencilMode
            pConservativeRaster = conservativeRaster
            pMultisample = multisample
        }

    let ofPipelineState (fboSignature : IFramebufferSignature) (x : ResourceManager) (surface : Surface) (rj : PipelineState) =
        // use a context token to avoid making context current/uncurrent repeatedly
        use token = x.Context.ResourceLock
        
        let iface, program = x.CreateSurface(fboSignature, surface, rj.geometryMode)

        GL.Check "[Prepare] Create Surface"
        
        // create all UniformBuffers requested by the program
        let uniformBuffers =
            iface.uniformBuffers
                |> MapExt.toList
                |> List.map (fun (_,block) ->
                    block.ubBinding, x.CreateUniformBuffer(Ag.emptyScope, block, rj.globalUniforms)
                   )
                |> Map.ofList

        GL.Check "[Prepare] Uniform Buffers"

        let storageBuffers = 
            iface.storageBuffers
                |> MapExt.toList
                |> List.map (fun (_,buf) ->
                    
                    let buffer = 
                        match rj.globalUniforms.TryGetUniform(Ag.emptyScope, Symbol.Create buf.ssbName) with
                            | Some (:? IMod<IBuffer> as b) ->
                                x.CreateBuffer(b)
                            | Some m ->
                                let o = toBufferCache.Invoke(m)
                                x.CreateBuffer(o)
                            | _ ->
                                failwithf "[GL] could not find storage buffer %A" buf.ssbName

                    buf.ssbBinding, buffer
                )
                |> Map.ofList



        // create all requested Textures
        let lastTextureSlot = ref -1

        let samplerModifier = 
            match rj.globalUniforms.TryGetUniform(Ag.emptyScope, DefaultSemantic.SamplerStateModifier) with
                | Some(:? IMod<Symbol -> SamplerStateDescription -> SamplerStateDescription> as mode) ->
                    Some mode
                | _ ->
                    None

        let textures =
            iface.samplers
                |> MapExt.toList
                |> List.collect (fun (_,u) ->
                    u.samplerTextures |> List.mapi (fun i  info ->
                        u, i, info
                    )
                   )
                |> List.choose (fun (sampler, index, (texName, samplerState)) ->
                    let name = sampler.samplerName
                    let samplerInfo = { textureName = Symbol.Create texName; samplerState = x.GetSamplerStateDescription(samplerState) } // SamplerStateDescription ARE BUILT OVER AND OVER AGAIN?
                    //let samplerInfo = { textureName = Symbol.Create texName; samplerState = samplerState.SamplerStateDescription } // SamplerStateDescription ARE BUILT OVER AND OVER AGAIN?

                    let sem = samplerInfo.textureName
                    let samplerState = samplerInfo.samplerState

                    match rj.globalUniforms.TryGetUniform(Ag.emptyScope, sem) with
                        | Some tex ->
        
                            let sammy =
                                match samplerModifier with
                                    | Some modifier -> 
                                        modifier |> Mod.map (fun f -> f sem samplerState) // NEW MOD SHOULD BE CREAED CACHED BASED ON (tex, modifier)
                                    | None -> 
                                        Mod.constant samplerState

                            let xx = sammy
                            let s = x.CreateSampler(sammy)

                            match tex with
                                | :? IMod<ITexture> as value ->
                                    let t = x.CreateTexture(value)
                                    lastTextureSlot := sampler.samplerBinding + index
                                    Some (!lastTextureSlot, (t, s))

                                | :? IMod<ITexture[]> as values ->
                                    let t = x.CreateTexture(values |> Mod.map (fun arr -> arr.[index])) // SHOULD BE CACHED BASED ON INPUT MOD
                                    lastTextureSlot := sampler.samplerBinding + index
                                    Some (!lastTextureSlot, (t, s))

                                | _ ->
                                    Log.warn "unexpected texture type %A: %A" sem tex
                                    None
                        | _ ->
                            Log.warn "texture %A not found" sem
                            None
                    )
                |> Map.ofList

        let textureBinding = 
            x.CreateTextureBinding(textures)

        
        textures |> Map.iter (fun _ (t,s) ->
            t.RemoveRef()
            s.RemoveRef()
        )
        GL.Check "[Prepare] Textures"
        
        let attachments = fboSignature.ColorAttachments |> Map.toList
        let attachmentCount = if attachments.Length > 0 then 1 + (attachments |> List.map (fun (i,_) -> i) |> List.max) else 0

        let colorMasks =
            match rj.writeBuffers with
                | Some b ->
                    let isAll = fboSignature.ColorAttachments |> Map.toSeq |> Seq.forall (fun (_,(sem,_)) -> Set.contains sem b)
                    if isAll then
                        None
                    else
                        let masks = Array.zeroCreate attachmentCount
                        for (index, (sem, att)) in attachments do
                            if Set.contains sem b then
                                masks.[index] <- V4i.IIII
                            else
                                masks.[index] <- V4i.OOOO

                        Some (Array.toList masks)
                | _ ->
                    None

        let drawBuffers = 
            match rj.writeBuffers with
                | Some set -> 
                    let man = DrawBufferManager.Get(fboSignature)
                    man.CreateConfig(set) |> Some
                | _ -> None

        let depthMask =
            match rj.writeBuffers with
                | Some b -> Set.contains DefaultSemantic.Depth b
                | None -> true

        let stencilMask =
            match rj.writeBuffers with
                | Some b -> Set.contains DefaultSemantic.Stencil b
                | None -> true
                
        let depthTest = x.CreateDepthTest rj.depthTest
        let cullMode = x.CreateCullMode rj.cullMode
        let polygonMode = x.CreatePolygonMode rj.fillMode
        let blendMode = x.CreateBlendMode rj.blendMode
        let stencilMode = x.CreateStencilMode rj.stencilMode
        let conservativeRaster = x.CreateFlag (Mod.constant false)
        let multisample = x.CreateFlag rj.multisample
        
        {
            pContext = x.Context
            pFramebufferSignature = fboSignature
            pLastTextureSlot = !lastTextureSlot
            pProgram = program
            pProgramInterface = iface
            pStorageBuffers = storageBuffers
            pUniformBuffers = uniformBuffers
            pUniforms = Map.empty
            pTextures = textureBinding
            pColorAttachmentCount = attachmentCount
            pDrawBuffers = drawBuffers
            pColorBufferMasks = colorMasks
            pDepthBufferMask = depthMask
            pStencilBufferMask = stencilMask
            pDepthTestMode = depthTest
            pCullMode = cullMode
            pPolygonMode = polygonMode
            pBlendMode = blendMode
            pStencilMode = stencilMode
            pConservativeRaster = conservativeRaster
            pMultisample = multisample
        }
        

[<AutoOpen>]
module PreparedPipelineStateAssembler =
    
    type IAssemblerStream with
    
        member x.SetPipelineState(s : CompilerInfo, me : PreparedPipelineState) =
            x.SetDepthMask(me.pDepthBufferMask)
            x.SetStencilMask(me.pStencilBufferMask)
            match me.pDrawBuffers with
                | None ->
                    x.SetDrawBuffers(s.drawBufferCount, s.drawBuffers)
                | Some b ->
                    x.SetDrawBuffers(b.Count, NativePtr.toNativeInt b.Buffers)
                   
            x.SetDepthTest(me.pDepthTestMode)  
            x.SetPolygonMode(me.pPolygonMode)
            x.SetCullMode(me.pCullMode)
            x.SetBlendMode(me.pBlendMode)
            x.SetStencilMode(me.pStencilMode)
            x.SetMultisample(me.pMultisample)
            
            let myProg = me.pProgram.Handle.GetValue()
            x.UseProgram(me.pProgram)
            if myProg.WritesPointSize then
                x.Enable(int OpenTK.Graphics.OpenGL4.EnableCap.ProgramPointSize)
            else
                x.Disable(int OpenTK.Graphics.OpenGL4.EnableCap.ProgramPointSize)
            

            // bind all uniform-buffers (if needed)
            for (id,ub) in Map.toSeq me.pUniformBuffers do
                x.BindUniformBufferView(id, ub)

            for (id, ssb) in Map.toSeq me.pStorageBuffers do
                x.BindStorageBuffer(id, ssb)

            // bind all textures/samplers (if needed)

            let binding = me.pTextures.Handle.GetValue()
            
            x.BindTexturesAndSamplers(me.pTextures)

            // bind all top-level uniforms (if needed)
            for (id,u) in Map.toSeq me.pUniforms do
                x.BindUniformLocation(id, u)

        member x.SetPipelineState(s : CompilerInfo, me : PreparedPipelineState, prev : PreparedPipelineState) =
            if prev.pDepthBufferMask <> me.pDepthBufferMask then
                x.SetDepthMask(me.pDepthBufferMask)

            if prev.pStencilBufferMask <> me.pStencilBufferMask then
                x.SetStencilMask(me.pStencilBufferMask)

            if prev.pDrawBuffers <> me.pDrawBuffers then
                match me.pDrawBuffers with
                    | None ->
                        x.SetDrawBuffers(s.drawBufferCount, s.drawBuffers)
                    | Some b ->
                        x.SetDrawBuffers(b.Count, NativePtr.toNativeInt b.Buffers)
                   
            if prev.pDepthTestMode <> me.pDepthTestMode then
                x.SetDepthTest(me.pDepthTestMode)  
                
            if prev.pPolygonMode <> me.pPolygonMode then
                x.SetPolygonMode(me.pPolygonMode)
                
            if prev.pCullMode <> me.pCullMode then
                x.SetCullMode(me.pCullMode)

            if prev.pBlendMode <> me.pBlendMode then
                x.SetBlendMode(me.pBlendMode)

            if prev.pStencilMode <> me.pStencilMode then
                x.SetStencilMode(me.pStencilMode)

            if prev.pMultisample <> me.pMultisample then
                x.SetMultisample(me.pMultisample)

            if prev.pProgram <> me.pProgram then
                let myProg = me.pProgram.Handle.GetValue()
                x.UseProgram(me.pProgram)

                if obj.ReferenceEquals(prev.pProgram, null) || prev.pProgram.Handle.GetValue().WritesPointSize <> myProg.WritesPointSize then
                    if myProg.WritesPointSize then
                        x.Enable(int OpenTK.Graphics.OpenGL4.EnableCap.ProgramPointSize)
                    else
                        x.Disable(int OpenTK.Graphics.OpenGL4.EnableCap.ProgramPointSize)
            

            // bind all uniform-buffers (if needed)
            for (id,ub) in Map.toSeq me.pUniformBuffers do
                //do! useUniformBufferSlot id
                
                match Map.tryFind id prev.pUniformBuffers with
                    | Some old when old = ub -> 
                        // the same UniformBuffer has already been bound
                        ()
                    | _ -> 
                        x.BindUniformBufferView(id, ub)

            for (id, ssb) in Map.toSeq me.pStorageBuffers do
                match Map.tryFind id prev.pStorageBuffers with
                    | Some old when old = ssb -> 
                        // the same UniformBuffer has already been bound
                        ()
                    | _ -> 
                        x.BindStorageBuffer(id, ssb)

            // bind all textures/samplers (if needed)

            let binding = me.pTextures.Handle.GetValue()
            
            if prev.pTextures <> me.pTextures then
                x.BindTexturesAndSamplers(me.pTextures)

            // bind all top-level uniforms (if needed)
            for (id,u) in Map.toSeq me.pUniforms do
                match Map.tryFind id prev.pUniforms with
                    | Some old when old = u -> ()
                    | _ -> x.BindUniformLocation(id, u)

        member x.SetPipelineState(s : CompilerInfo, me : PreparedPipelineState, prev : Option<PreparedPipelineState>) =
            match prev with
                | Some prev -> x.SetPipelineState(s, me, prev)
                | None -> x.SetPipelineState(s, me)


[<AbstractClass>]
type PreparedCommand(ctx : Context, renderPass : RenderPass) =
    
    let mutable refCount = 1
    let id = newId()

    let mutable cleanup : list<unit -> unit> = []
    
    let mutable resourceStats = None
    let mutable resources = None
    
    let getResources (x : PreparedCommand) =
        lock x (fun () ->
            match resources with
            | Some res -> res
            | None -> 
                let all = x.GetResources() |> Seq.toArray
                resources <- Some all
                all
        )

    let getStats (x : PreparedCommand) =
        lock x (fun () ->
            match resourceStats with
            | Some s -> s
            | None ->
                let res = getResources x
                let cnt = res.Length
                let counts = res |> Seq.countBy (fun r -> r.Kind) |> Map.ofSeq
                resourceStats <- Some (cnt, counts)
                (cnt, counts)
        )
        
    abstract member GetResources : unit -> seq<IResource>
    abstract member Release : unit -> unit
    abstract member Compile : info : CompilerInfo * stream : Aardvark.Base.Runtime.IAssemblerStream * prev : Option<PreparedCommand> -> unit
    abstract member EntryState : Option<PreparedPipelineState>
    abstract member ExitState : Option<PreparedPipelineState>
    
    member x.AddCleanup(clean : unit -> unit) =
        cleanup <- clean :: cleanup

    member x.Id = id
    member x.Pass = renderPass
    member x.IsDisposed = refCount = 0
    
    member x.Resources = getResources x
        
    member x.ResourceCount =
        let (cnt,_) = getStats x
        cnt

    member x.ResourceCounts =
        let (_,cnts) = getStats x
        cnts

    member x.AddReference() =
        Interlocked.Increment(&refCount) |> ignore

    member x.Update(token : AdaptiveToken, rt : RenderToken) =
        for r in x.Resources do r.Update(token, rt)

    member x.Dispose() =
        if Interlocked.Decrement(&refCount) = 0 then
            lock x (fun () ->
                let token = try Some ctx.ResourceLock with :? ObjectDisposedException -> None
            
                match token with
                | Some token ->
                    try
                        cleanup |> List.iter (fun f -> f())
                        cleanup <- []

                        x.Release()
                        resourceStats <- Some (0, Map.empty)
                    finally
                        token.Dispose()
                | None ->
                    //OpenGL died
                    ()
            )

    interface IDisposable with
        member x.Dispose() = x.Dispose()
        
    interface IRenderObject with
        member x.AttributeScope = Ag.emptyScope
        member x.Id = x.Id
        member x.RenderPass = renderPass

    interface IPreparedRenderObject with
        member x.Update(token, rt) = x.Update(token, rt)
        member x.Original = None

type PreparedObjectInfo =
    {   
        oContext : Context
        oActivation : IDisposable
        oFramebufferSignature : IFramebufferSignature
        oBeginMode : IResource<GLBeginMode, GLBeginMode>
        oBuffers : list<int * BufferView * AttributeFrequency * IResource<Buffer, int>>
        oIndexBuffer : Option<OpenGl.Enums.IndexType * IResource<Buffer, int>>
        oIsActive : IResource<bool, int>
        oDrawCallInfos : IResource<DrawCallInfoList, DrawCallInfoList>
        oIndirectBuffer : Option<IResource<IndirectBuffer, V2i>>
        oVertexInputBinding : IResource<VertexInputBindingHandle, int>  
    }
    
    member x.Dispose() =
        x.oBeginMode.Dispose()

        for (_,_,_,b) in x.oBuffers do b.Dispose()
        match x.oIndexBuffer with
            | Some (_,b) -> b.Dispose()
            | _ -> ()

        x.oIsActive.Dispose()
        match x.oIndirectBuffer with
            | Some i -> i.Dispose()
            | None -> x.oDrawCallInfos.Dispose()

        x.oVertexInputBinding.Dispose()

    member x.Resources =
        seq {
            yield x.oBeginMode :> IResource

            for (_,_,_,b) in x.oBuffers do yield b :>_
            match x.oIndexBuffer with
                | Some (_,b) -> yield b :>_
                | _ -> ()

            yield x.oIsActive :> _
            match x.oIndirectBuffer with
                | Some i -> yield i :> _
                | None -> yield x.oDrawCallInfos :> _

            yield x.oVertexInputBinding :> _
        }

module PreparedObjectInfo =
    open FShade.GLSL

    let private (|Floaty|_|) (t : GLSLType) =
        match t with
            | GLSLType.Float 32 -> Some ()
            | GLSLType.Float 64 -> Some ()
            | _ -> None
        

    let private getExpectedType (t : GLSLType) =
        match t with
            | GLSLType.Void -> typeof<unit>
            | GLSLType.Bool -> typeof<int>
            | Floaty -> typeof<float32>
            | GLSLType.Int(true, 32) -> typeof<int>
            | GLSLType.Int(false, 32) -> typeof<uint32>
             
            | GLSLType.Vec(2, Floaty) -> typeof<V2f>
            | GLSLType.Vec(3, Floaty) -> typeof<V3f>
            | GLSLType.Vec(4, Floaty) -> typeof<V4f>
            | GLSLType.Vec(2, Int(true, 32)) -> typeof<V2i>
            | GLSLType.Vec(3, Int(true, 32)) -> typeof<V3i>
            | GLSLType.Vec(4, Int(true, 32)) -> typeof<V4i>
            | GLSLType.Vec(3, Int(false, 32)) -> typeof<C3ui>
            | GLSLType.Vec(4, Int(false, 32)) -> typeof<C4ui>


            | GLSLType.Mat(2,2,Floaty) -> typeof<M22f>
            | GLSLType.Mat(3,3,Floaty) -> typeof<M33f>
            | GLSLType.Mat(4,4,Floaty) -> typeof<M44f>
            | GLSLType.Mat(3,4,Floaty) -> typeof<M34f>
            | GLSLType.Mat(4,3,Floaty) -> typeof<M34f>
            | GLSLType.Mat(2,3,Floaty) -> typeof<M23f>

            | _ -> failwithf "[GL] unexpected vertex type: %A" t


    let ofRenderObject (fboSignature : IFramebufferSignature) (x : ResourceManager) (iface : GLSLProgramInterface) (program : IResource<Program, int>) (rj : RenderObject) =

        // use a context token to avoid making context current/uncurrent repeatedly
        use token = x.Context.ResourceLock

        let activation = rj.Activate()

        // create all requested vertex-/instance-inputs
        let buffers =
            iface.inputs
                |> List.choose (fun v ->
                     if v.paramLocation >= 0 then
                        let expected = getExpectedType v.paramType
                        let sem = v.paramName |> Symbol.Create
                        match rj.VertexAttributes.TryGetAttribute sem with
                            | Some value ->
                                let dep = x.CreateBuffer(value.Buffer)
                                Some (v.paramLocation, value, AttributeFrequency.PerVertex, dep)
                            | _  -> 
                                match rj.InstanceAttributes with
                                    | null -> failwithf "could not get attribute %A (not found in vertex attributes, and instance attributes is null) for rj: %A" sem rj
                                    | _ -> 
                                        match rj.InstanceAttributes.TryGetAttribute sem with
                                            | Some value ->
                                                let dep = x.CreateBuffer(value.Buffer)
                                                Some(v.paramLocation, value, (AttributeFrequency.PerInstances 1), dep)
                                            | _ -> 
                                                failwithf "could not get attribute %A" sem
                        else
                            None
                   )

        GL.Check "[Prepare] Buffers"

        // create the index buffer (if present)
        let index =
            match rj.Indices with
                | Some i -> 
                    let buffer = x.CreateBuffer i.Buffer
                    let indexType =
                        let indexType = i.ElementType
                        if indexType = typeof<byte> then OpenGl.Enums.IndexType.UnsignedByte
                        elif indexType = typeof<uint16> then OpenGl.Enums.IndexType.UnsignedShort
                        elif indexType = typeof<uint32> then OpenGl.Enums.IndexType.UnsignedInt
                        elif indexType = typeof<sbyte> then OpenGl.Enums.IndexType.UnsignedByte
                        elif indexType = typeof<int16> then OpenGl.Enums.IndexType.UnsignedShort
                        elif indexType = typeof<int32> then OpenGl.Enums.IndexType.UnsignedInt
                        else failwithf "unsupported index type: %A"  indexType
                    Some(indexType, buffer)

                | None -> None


        GL.Check "[Prepare] Indices"

        let indirect =
            if isNull rj.IndirectBuffer then None
            else x.CreateIndirectBuffer(Option.isSome rj.Indices, rj.IndirectBuffer) |> Some

        GL.Check "[Prepare] Indirect Buffer"

        // create the VertexArrayObject
        let vibh =
            x.CreateVertexInputBinding(buffers, index)

        GL.Check "[Prepare] VAO"

        let attachments = fboSignature.ColorAttachments |> Map.toList
        let attachmentCount = if attachments.Length > 0 then 1 + (attachments |> List.map (fun (i,_) -> i) |> List.max) else 0

        let isActive = x.CreateIsActive rj.IsActive
        let beginMode = 
            let hasTessMod = program.Handle |> Mod.map (fun p -> p.HasTessellation)
            x.CreateBeginMode(hasTessMod, rj.Mode)
        let drawCalls = if isNull rj.DrawCallInfos then Unchecked.defaultof<_> else x.CreateDrawCallInfoList rj.DrawCallInfos


        // finally return the PreparedRenderObject
        
        {
            oContext = x.Context
            oActivation = activation
            oFramebufferSignature = fboSignature
            oBuffers = buffers
            oIndexBuffer = index
            oIndirectBuffer = indirect
            oVertexInputBinding = vibh
            oIsActive = isActive
            oBeginMode = beginMode
            oDrawCallInfos = drawCalls
        }
            

[<AutoOpen>]
module PreparedObjectInfoAssembler =
    
    type IAssemblerStream with
        member x.Render(s : CompilerInfo, me : PreparedObjectInfo) =
        
            // bind the VAO (if needed)
            x.BindVertexAttributes(s.contextHandle, me.oVertexInputBinding)

            // draw the thing
            let isActive = me.oIsActive
            let beginMode = me.oBeginMode

            match me.oIndirectBuffer with
                | Some indirect ->
                    match me.oIndexBuffer with
                        | Some (it,_) ->
                            x.DrawElementsIndirect(s.runtimeStats, isActive, beginMode, int it, indirect)
                        | None ->
                            x.DrawArraysIndirect(s.runtimeStats, isActive, beginMode, indirect)

                | None ->
                    match me.oIndexBuffer with
                        | Some (it,_) ->
                            x.DrawElements(s.runtimeStats, isActive, beginMode, (int it), me.oDrawCallInfos)
                        | None ->
                            x.DrawArrays(s.runtimeStats, isActive, beginMode, me.oDrawCallInfos)

        member x.Render(s : CompilerInfo, me : PreparedObjectInfo, prev : PreparedObjectInfo) =
        
            // bind the VAO (if needed)
            if prev.oVertexInputBinding <> me.oVertexInputBinding then
                x.BindVertexAttributes(s.contextHandle, me.oVertexInputBinding)

            // draw the thing
            let isActive = me.oIsActive
            let beginMode = me.oBeginMode

            match me.oIndirectBuffer with
                | Some indirect ->
                    match me.oIndexBuffer with
                        | Some (it,_) ->
                            x.DrawElementsIndirect(s.runtimeStats, isActive, beginMode, int it, indirect)
                        | None ->
                            x.DrawArraysIndirect(s.runtimeStats, isActive, beginMode, indirect)

                | None ->
                    match me.oIndexBuffer with
                        | Some (it,_) ->
                            x.DrawElements(s.runtimeStats, isActive, beginMode, (int it), me.oDrawCallInfos)
                        | None ->
                            x.DrawArrays(s.runtimeStats, isActive, beginMode, me.oDrawCallInfos)

        member x.Render(s : CompilerInfo, me : PreparedObjectInfo, prev : Option<PreparedObjectInfo>) =
            match prev with
                | Some prev -> x.Render(s, me, prev)
                | None -> x.Render(s, me)
    
            
type EpilogCommand(ctx : Context) =
    inherit PreparedCommand(ctx, RenderPass.main) 

    override x.GetResources() = Seq.empty
    override x.Release() = ()
    override x.Compile(s, stream, prev) = 
        stream.SetDepthMask(true)
        stream.SetStencilMask(true)
        stream.SetDrawBuffers(s.drawBufferCount, s.drawBuffers)
        stream.UseProgram(0)
        stream.BindBuffer(int OpenTK.Graphics.OpenGL4.BufferTarget.DrawIndirectBuffer, 0)
        
    override x.EntryState = None
    override x.ExitState = None

type NopCommand(ctx : Context, pass : RenderPass) =
    inherit PreparedCommand(ctx, pass) 

    override x.GetResources() = Seq.empty
    override x.Release() = ()
    override x.Compile(_,_,_) = ()
    override x.EntryState = None
    override x.ExitState = None

type PreparedObjectCommand(state : PreparedPipelineState, info : PreparedObjectInfo, renderPass : RenderPass) =
    inherit PreparedCommand(state.pContext, renderPass)

    member x.Info = info

    override x.Release() =
        state.Dispose()
        info.Dispose()

    override x.GetResources() =
        seq {
            yield! state.Resources
            yield! info.Resources
        }

    override x.Compile(s : CompilerInfo, stream : IAssemblerStream, prev : Option<PreparedCommand>) =
        let prevInfo =
            match prev with
                | Some (:? PreparedObjectCommand as p) -> Some p.Info
                | _ -> None

        let prevState =
            match prev with
            | Some p -> p.ExitState
            | _ -> None

            
        stream.SetPipelineState(s, state, prevState)
        stream.Render(s, info, prevInfo)

    override x.EntryState = Some state
    override x.ExitState = Some state

type MultiCommand(ctx : Context, cmds : list<PreparedCommand>, renderPass : RenderPass) =
    inherit PreparedCommand(ctx, renderPass)
    
    let first   = List.tryHead cmds
    let last    = List.tryLast cmds

    override x.Release() =
        cmds |> List.iter (fun c -> c.Dispose())
        
    override x.GetResources() =
        cmds |> Seq.collect (fun c -> c.Resources)

    override x.Compile(info, stream, prev) =
        let mutable prev = prev
        for c in cmds do
            c.Compile(info, stream, prev)
            prev <- Some c

    override x.EntryState = first |> Option.bind (fun first -> first.EntryState)
    override x.ExitState = last |> Option.bind (fun last -> last.ExitState)


module PreparedCommand =

    let rec ofRenderObject (fboSignature : IFramebufferSignature) (x : ResourceManager) (o : IRenderObject) =
        let pass = o.RenderPass
        match o with
            | :? RenderObject as o ->
                let state = PreparedPipelineState.ofRenderObject fboSignature x o
                let info = PreparedObjectInfo.ofRenderObject fboSignature x state.pProgramInterface state.pProgram o
                new PreparedObjectCommand(state, info, pass) :> PreparedCommand

            | :? MultiRenderObject as o ->
                match o.Children with
                    | [] -> 
                        new NopCommand(x.Context, pass) :> PreparedCommand
                    | [o] -> 
                        ofRenderObject fboSignature x o

                    | l -> 
                        new MultiCommand(x.Context, l |> List.map (ofRenderObject fboSignature x), pass) :> PreparedCommand

            | :? PreparedCommand as cmd ->
                cmd.AddReference()
                cmd

            | _ ->
                failwithf "bad object: %A" o









//[<CustomEquality;CustomComparison>]
//type PreparedRenderObject =
//    {
//        mutable Activation : IDisposable
//        Context : Context
//        Parent : Option<PreparedRenderObject>
//        Original : RenderObject
//        FramebufferSignature : IFramebufferSignature
//        LastTextureSlot : int
//        Program : IResource<Program, int>
//        UniformBuffers : Map<int, IResource<UniformBufferView, int>>
//        StorageBuffers : Map<int, IResource<Buffer, int>>
//        Uniforms : Map<int, IResource<UniformLocation, nativeint>>
//        Textures : IResource<TextureBinding, TextureBinding>
//        Buffers : list<int * BufferView * AttributeFrequency * IResource<Buffer, int>>
//        IndexBuffer : Option<OpenGl.Enums.IndexType * IResource<Buffer, int>>
        
//        IsActive : IResource<bool, int>
//        BeginMode : IResource<GLBeginMode, GLBeginMode>
//        DrawCallInfos : IResource<DrawCallInfoList, DrawCallInfoList>
//        IndirectBuffer : Option<IResource<IndirectBuffer, V2i>>
//        DepthTestMode : IResource<DepthTestInfo, DepthTestInfo>
//        CullMode : IResource<int, int>
//        PolygonMode : IResource<int, int>
//        BlendMode : IResource<GLBlendMode, GLBlendMode>
//        StencilMode : IResource<GLStencilMode, GLStencilMode>
//        ConservativeRaster : IResource<bool, int>
//        Multisample : IResource<bool, int>

//        VertexInputBinding : IResource<VertexInputBindingHandle, int>
        
//        ColorAttachmentCount : int
//        DrawBuffers : Option<DrawBufferConfig>
//        ColorBufferMasks : Option<list<V4i>>
//        DepthBufferMask : bool
//        StencilBufferMask : bool

//        mutable ResourceCount : int
//        mutable ResourceCounts : Map<ResourceKind, int>

//        mutable IsDisposed : bool
//    } 

//    interface IRenderObject with
//        member x.Id = x.Original.Id
//        member x.RenderPass = x.RenderPass
//        member x.AttributeScope = x.AttributeScope

//    interface IPreparedRenderObject with
//        member x.Update(caller, token) = x.Update(caller, token) |> ignore
//        member x.Original = Some x.Original

//    member x.Id = x.Original.Id
//    member x.CreationPath = x.Original.Path
//    member x.AttributeScope = x.Original.AttributeScope
//    member x.RenderPass = x.Original.RenderPass

//    member x.Resources =
//        seq {
//            yield x.Program :> IResource
//            for (_,b) in Map.toSeq x.UniformBuffers do
//                yield b :> _
                
//            for (_,b) in Map.toSeq x.StorageBuffers do
//                yield b :> _

//            for (_,u) in Map.toSeq x.Uniforms do
//                yield u :> _

//            yield x.Textures :> _

//            for (_,_,_,b) in x.Buffers do
//                yield b :> _

//            match x.IndexBuffer with
//                | Some (_,ib) -> yield ib :> _
//                | _ -> ()

//            match x.IndirectBuffer with
//                | Some ib -> yield ib :> _
//                | _ -> yield x.DrawCallInfos :> _


//            yield x.VertexInputBinding :> _ 
//            yield x.IsActive :> _
//            yield x.BeginMode :> _
//            yield x.ConservativeRaster :> _
//            yield x.Multisample :> _
//            yield x.DepthTestMode :> _
//            yield x.CullMode :> _
//            yield x.PolygonMode :> _
//            yield x.BlendMode :> _
//            yield x.StencilMode :> _
//        }

//    member x.Update(caller : AdaptiveToken, token : RenderToken) =
//        use ctxToken = x.Context.ResourceLock

//        x.Program.Update(caller, token)

//        for (_,ub) in x.UniformBuffers |> Map.toSeq do
//            ub.Update(caller, token)
            
//        for (_,ub) in x.StorageBuffers |> Map.toSeq do
//            ub.Update(caller, token)

//        for (_,ul) in x.Uniforms |> Map.toSeq do
//            ul.Update(caller, token)

//        x.Textures.Update(caller, token)

//        for (_,_,_,b) in x.Buffers  do
//            b.Update(caller, token)

//        match x.IndexBuffer with
//            | Some (_,ib) -> ib.Update(caller, token)
//            | _ -> ()

//        match x.IndirectBuffer with
//            | Some ib -> ib.Update(caller, token)
//            | _ -> x.DrawCallInfos.Update(caller, token)

//        x.VertexInputBinding.Update(caller, token)


//        x.IsActive.Update(caller, token)
//        x.BeginMode.Update(caller, token)
//        x.DepthTestMode.Update(caller, token)
//        x.CullMode.Update(caller, token)
//        x.PolygonMode.Update(caller, token)
//        x.BlendMode.Update(caller, token)
//        x.StencilMode.Update(caller, token)
//        x.ConservativeRaster.Update(caller, token)
//        x.Multisample.Update(caller, token)

//    member x.Dispose() =
//        lock x (fun () -> 
//            if not x.IsDisposed then
//                x.IsDisposed <- true

//                // ObjDisposed might occur here if GL is dead already and render objects get disposed nondeterministically by finalizer thread.
//                let resourceLock = try Some x.Context.ResourceLock with :? ObjectDisposedException as o -> None

//                match resourceLock with
//                    | None ->
//                        // OpenGL already dead
//                        ()
//                    | Some l -> 
//                        use resourceLock = l

//                        OpenTK.Graphics.OpenGL4.GL.UnbindAllBuffers()
//                        x.Activation.Dispose()
//                        match x.DrawBuffers with
//                            | Some b -> b.RemoveRef()
//                            | _ -> ()
//                        x.VertexInputBinding.Dispose() 
//                        x.Buffers |> List.iter (fun (_,_,_,b) -> b.Dispose())
//                        x.IndexBuffer |> Option.iter (fun (_,b) -> b.Dispose())
//                        match x.IndirectBuffer with
//                            | Some b -> b.Dispose()
//                            | None -> x.DrawCallInfos.Dispose()
                            

//                        x.Textures.Dispose()
//                        x.Uniforms |> Map.iter (fun _ (ul) -> ul.Dispose())
//                        x.UniformBuffers |> Map.iter (fun _ (ub) -> ub.Dispose())
//                        x.StorageBuffers |> Map.iter (fun _ (ub) -> ub.Dispose())
//                        x.Program.Dispose()

//                        x.IsActive.Dispose()
//                        x.BeginMode.Dispose()
//                        x.DepthTestMode.Dispose()
//                        x.CullMode.Dispose()
//                        x.PolygonMode.Dispose()
//                        x.BlendMode.Dispose()
//                        x.StencilMode.Dispose()
//                        x.ConservativeRaster.Dispose()
//                        x.Multisample.Dispose()
//        )
        
             

//    interface IDisposable with
//        member x.Dispose() = x.Dispose()

//    interface IComparable with
//        member x.CompareTo o =
//            match o with
//                | :? PreparedRenderObject as o ->
//                    compare x.Original o.Original
//                | _ ->
//                    failwith "uncomparable"

//    override x.GetHashCode() = x.Original.GetHashCode()
//    override x.Equals o =
//        match o with
//            | :? PreparedRenderObject as o -> x.Original = o.Original
//            | _ -> false

//[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
//module PreparedRenderObject =
//    let empty = 
//        {
//            Activation = { new IDisposable with member x.Dispose() = () }
//            Context = Unchecked.defaultof<_>
//            Original = RenderObject.Empty
//            Parent = None
//            FramebufferSignature = null
//            LastTextureSlot = -1
//            Program = Unchecked.defaultof<_>
//            StorageBuffers = Map.empty
//            UniformBuffers = Map.empty
//            Uniforms = Map.empty
//            Textures = Unchecked.defaultof<_>
//            Buffers = []
//            IndexBuffer = None
//            IndirectBuffer = None
//            VertexInputBinding = Unchecked.defaultof<_>
//            ColorAttachmentCount = 0
//            DrawBuffers = None
//            ColorBufferMasks = None
//            DepthBufferMask = true
//            StencilBufferMask = true
//            IsDisposed = false
//            ResourceCount = 0
//            ResourceCounts = Map.empty
//            IsActive = Unchecked.defaultof<_>
//            BeginMode = Unchecked.defaultof<_>
//            DrawCallInfos = Unchecked.defaultof<_>
//            DepthTestMode = Unchecked.defaultof<_>
//            CullMode = Unchecked.defaultof<_>
//            PolygonMode = Unchecked.defaultof<_>
//            BlendMode = Unchecked.defaultof<_>
//            StencilMode = Unchecked.defaultof<_>
//            ConservativeRaster = Unchecked.defaultof<_>
//            Multisample = Unchecked.defaultof<_>
//        }  

//    let clone (o : PreparedRenderObject) =
//        let drawBuffers =
//            match o.DrawBuffers with
//                | Some b ->
//                    b.AddRef()
//                    Some b
//                | _ ->
//                    None

//        let res = 
//            {
//                Activation = { new IDisposable with member x.Dispose() = () }
//                Context = o.Context
//                Original = o.Original
//                Parent = Some o
//                FramebufferSignature = o.FramebufferSignature
//                LastTextureSlot = o.LastTextureSlot
//                Program = o.Program
//                StorageBuffers = o.StorageBuffers
//                UniformBuffers = o.UniformBuffers
//                Uniforms = o.Uniforms
//                Textures = o.Textures
//                Buffers = o.Buffers
//                IndexBuffer = o.IndexBuffer
//                IndirectBuffer = o.IndirectBuffer
//                VertexInputBinding = o.VertexInputBinding
//                ColorAttachmentCount = o.ColorAttachmentCount
//                DrawBuffers = drawBuffers
//                ColorBufferMasks = o.ColorBufferMasks
//                DepthBufferMask = o.DepthBufferMask
//                StencilBufferMask = o.StencilBufferMask
//                IsDisposed = o.IsDisposed
//                ResourceCount = o.ResourceCount
//                ResourceCounts = o.ResourceCounts

//                IsActive  = o.IsActive 
//                BeginMode  = o.BeginMode 
//                DrawCallInfos  = o.DrawCallInfos 
//                DepthTestMode  = o.DepthTestMode 
//                CullMode  = o.CullMode 
//                PolygonMode  = o.PolygonMode 
//                BlendMode  = o.BlendMode 
//                StencilMode  = o.StencilMode 
//                ConservativeRaster = o.ConservativeRaster
//                Multisample = o.Multisample
//            }  

//        for r in res.Resources do
//            r.AddRef()

//        res


//type PreparedMultiRenderObject(children : list<PreparedRenderObject>) =
//    let first =
//        match children with
//            | [] -> failwith "PreparedMultiRenderObject cannot be empty"
//            | h::_ -> h

//    let last = children |> List.last

//    member x.Children = children

//    member x.Dispose() =
//        children |> List.iter (fun c -> c.Dispose())

//    member x.Update(caller : AdaptiveToken, token : RenderToken) =
//        children |> List.iter (fun c -> c.Update(caller, token))
        

//    member x.RenderPass = first.RenderPass
//    member x.Original = first.Original

//    member x.First = first
//    member x.Last = last

//    interface IRenderObject with
//        member x.Id = first.Id
//        member x.AttributeScope = first.AttributeScope
//        member x.RenderPass = first.RenderPass

//    interface IPreparedRenderObject with
//        member x.Original = Some first.Original
//        member x.Update(caller, token) = x.Update(caller, token)

//    interface IDisposable with
//        member x.Dispose() = x.Dispose()


//open FShade.GLSL
//[<Extension; AbstractClass; Sealed>]
//type ResourceManagerExtensions private() =
    
//    static let (|Floaty|_|) (t : GLSLType) =
//        match t with
//            | GLSLType.Float 32 -> Some ()
//            | GLSLType.Float 64 -> Some ()
//            | _ -> None
        

//    static let getExpectedType (t : GLSLType) =
//        match t with
//            | GLSLType.Void -> typeof<unit>
//            | GLSLType.Bool -> typeof<int>
//            | Floaty -> typeof<float32>
//            | GLSLType.Int(true, 32) -> typeof<int>
//            | GLSLType.Int(false, 32) -> typeof<uint32>
             
//            | GLSLType.Vec(2, Floaty) -> typeof<V2f>
//            | GLSLType.Vec(3, Floaty) -> typeof<V3f>
//            | GLSLType.Vec(4, Floaty) -> typeof<V4f>
//            | GLSLType.Vec(2, Int(true, 32)) -> typeof<V2i>
//            | GLSLType.Vec(3, Int(true, 32)) -> typeof<V3i>
//            | GLSLType.Vec(4, Int(true, 32)) -> typeof<V4i>
//            | GLSLType.Vec(3, Int(false, 32)) -> typeof<C3ui>
//            | GLSLType.Vec(4, Int(false, 32)) -> typeof<C4ui>


//            | GLSLType.Mat(2,2,Floaty) -> typeof<M22f>
//            | GLSLType.Mat(3,3,Floaty) -> typeof<M33f>
//            | GLSLType.Mat(4,4,Floaty) -> typeof<M44f>
//            | GLSLType.Mat(3,4,Floaty) -> typeof<M34f>
//            | GLSLType.Mat(4,3,Floaty) -> typeof<M34f>
//            | GLSLType.Mat(2,3,Floaty) -> typeof<M23f>

//            | _ -> failwithf "[GL] unexpected vertex type: %A" t


            

//    [<Extension>]
//    static member Prepare (x : ResourceManager, fboSignature : IFramebufferSignature, rj : RenderObject) : PreparedRenderObject =
//        // use a context token to avoid making context current/uncurrent repeatedly
//        use token = x.Context.ResourceLock

//        let activation = rj.Activate()
        
//        let iface, program = x.CreateSurface(fboSignature, rj.Surface, rj.Mode)

//        GL.Check "[Prepare] Create Surface"

//        let createdViews = System.Collections.Generic.List()

//        // create all UniformBuffers requested by the program
//        let uniformBuffers =
//            iface.uniformBuffers
//                |> MapExt.toList
//                |> List.map (fun (_,block) ->
//                    block.ubBinding, x.CreateUniformBuffer(rj.AttributeScope, block, rj.Uniforms)
//                   )
//                |> Map.ofList

//        GL.Check "[Prepare] Uniform Buffers"

//        let storageBuffers = 
//            iface.storageBuffers
//                |> MapExt.toList
//                |> List.map (fun (_,buf) ->
                    
//                    let buffer = 
//                        match rj.Uniforms.TryGetUniform(rj.AttributeScope, Symbol.Create buf.ssbName) with
//                            | Some (:? IMod<IBuffer> as b) ->
//                                x.CreateBuffer(b)
//                            | Some m ->
//                                let o = toBufferCache.Invoke(m)
//                                x.CreateBuffer(o)
//                            | _ ->
//                                failwithf "[GL] could not find storage buffer %A" buf.ssbName

//                    buf.ssbBinding, buffer
//                )
//                |> Map.ofList



//        // create all requested Textures
//        let lastTextureSlot = ref -1

//        let samplerModifier = 
//            match rj.Uniforms.TryGetUniform(rj.AttributeScope, DefaultSemantic.SamplerStateModifier) with
//                | Some(:? IMod<Symbol -> SamplerStateDescription -> SamplerStateDescription> as mode) ->
//                    Some mode
//                | _ ->
//                    None

//        let textures =
//            iface.samplers
//                |> MapExt.toList
//                |> List.collect (fun (_,u) ->
//                    u.samplerTextures |> List.mapi (fun i  info ->
//                        u, i, info
//                    )
//                   )
//                |> List.choose (fun (sampler, index, (texName, samplerState)) ->
//                    let name = sampler.samplerName
//                    let samplerInfo = { textureName = Symbol.Create texName; samplerState = x.GetSamplerStateDescription(samplerState) } // SamplerStateDescription ARE BUILT OVER AND OVER AGAIN?
//                    //let samplerInfo = { textureName = Symbol.Create texName; samplerState = samplerState.SamplerStateDescription } // SamplerStateDescription ARE BUILT OVER AND OVER AGAIN?

//                    let sem = samplerInfo.textureName
//                    let samplerState = samplerInfo.samplerState

//                    match rj.Uniforms.TryGetUniform(rj.AttributeScope, sem) with
//                        | Some tex ->
        
//                            let sammy =
//                                match samplerModifier with
//                                    | Some modifier -> 
//                                        modifier |> Mod.map (fun f -> f sem samplerState) // NEW MOD SHOULD BE CREAED CACHED BASED ON (tex, modifier)
//                                    | None -> 
//                                        Mod.constant samplerState

//                            let xx = sammy
//                            let s = x.CreateSampler(sammy)

//                            match tex with
//                                | :? IMod<ITexture> as value ->
//                                    let t = x.CreateTexture(value)
//                                    lastTextureSlot := sampler.samplerBinding + index
//                                    Some (!lastTextureSlot, (t, s))

//                                | :? IMod<ITexture[]> as values ->
//                                    let t = x.CreateTexture(values |> Mod.map (fun arr -> arr.[index])) // SHOULD BE CACHED BASED ON INPUT MOD
//                                    lastTextureSlot := sampler.samplerBinding + index
//                                    Some (!lastTextureSlot, (t, s))

//                                | _ ->
//                                    Log.warn "unexpected texture type %A: %A" sem tex
//                                    None
//                        | _ ->
//                            Log.warn "texture %A not found" sem
//                            None
//                    )
//                |> Map.ofList

//        let textureBinding = 
//            x.CreateTextureBinding(textures)

        
//        textures |> Map.iter (fun _ (t,s) ->
//            t.RemoveRef()
//            s.RemoveRef()
//        )
//        GL.Check "[Prepare] Textures"

//        // create all requested vertex-/instance-inputs
//        let buffers =
//            iface.inputs
//                |> List.choose (fun v ->
//                     if v.paramLocation >= 0 then
//                        let expected = getExpectedType v.paramType
//                        let sem = v.paramName |> Symbol.Create
//                        match rj.VertexAttributes.TryGetAttribute sem with
//                            | Some value ->
//                                let dep = x.CreateBuffer(value.Buffer)
//                                Some (v.paramLocation, value, AttributeFrequency.PerVertex, dep)
//                            | _  -> 
//                                match rj.InstanceAttributes with
//                                    | null -> failwithf "could not get attribute %A (not found in vertex attributes, and instance attributes is null) for rj: %A" sem rj
//                                    | _ -> 
//                                        match rj.InstanceAttributes.TryGetAttribute sem with
//                                            | Some value ->
//                                                let dep = x.CreateBuffer(value.Buffer)
//                                                Some(v.paramLocation, value, (AttributeFrequency.PerInstances 1), dep)
//                                            | _ -> 
//                                                failwithf "could not get attribute %A" sem
//                        else
//                            None
//                   )

//        GL.Check "[Prepare] Buffers"

//        // create the index buffer (if present)
//        let index =
//            match rj.Indices with
//                | Some i -> 
//                    let buffer = x.CreateBuffer i.Buffer
//                    let indexType =
//                        let indexType = i.ElementType
//                        if indexType = typeof<byte> then OpenGl.Enums.IndexType.UnsignedByte
//                        elif indexType = typeof<uint16> then OpenGl.Enums.IndexType.UnsignedShort
//                        elif indexType = typeof<uint32> then OpenGl.Enums.IndexType.UnsignedInt
//                        elif indexType = typeof<sbyte> then OpenGl.Enums.IndexType.UnsignedByte
//                        elif indexType = typeof<int16> then OpenGl.Enums.IndexType.UnsignedShort
//                        elif indexType = typeof<int32> then OpenGl.Enums.IndexType.UnsignedInt
//                        else failwithf "unsupported index type: %A"  indexType
//                    Some(indexType, buffer)

//                | None -> None


//        GL.Check "[Prepare] Indices"

//        let indirect =
//            if isNull rj.IndirectBuffer then None
//            else x.CreateIndirectBuffer(Option.isSome rj.Indices, rj.IndirectBuffer) |> Some

//        GL.Check "[Prepare] Indirect Buffer"

//        // create the VertexArrayObject
//        let vibh =
//            x.CreateVertexInputBinding(buffers, index)

//        GL.Check "[Prepare] VAO"

//        let attachments = fboSignature.ColorAttachments |> Map.toList
//        let attachmentCount = if attachments.Length > 0 then 1 + (attachments |> List.map (fun (i,_) -> i) |> List.max) else 0

//        let colorMasks =
//            match rj.WriteBuffers with
//                | Some b ->
//                    let isAll = fboSignature.ColorAttachments |> Map.toSeq |> Seq.forall (fun (_,(sem,_)) -> Set.contains sem b)
//                    if isAll then
//                        None
//                    else
//                        let masks = Array.zeroCreate attachmentCount
//                        for (index, (sem, att)) in attachments do
//                            if Set.contains sem b then
//                                masks.[index] <- V4i.IIII
//                            else
//                                masks.[index] <- V4i.OOOO

//                        Some (Array.toList masks)
//                | _ ->
//                    None

//        let drawBuffers = 
//            match rj.WriteBuffers with
//                | Some set -> x.DrawBufferManager.CreateConfig(set) |> Some
//                | _ -> None

//        let depthMask =
//            match rj.WriteBuffers with
//                | Some b -> Set.contains DefaultSemantic.Depth b
//                | None -> true

//        let stencilMask =
//            match rj.WriteBuffers with
//                | Some b -> Set.contains DefaultSemantic.Stencil b
//                | None -> true


//        let isActive = x.CreateIsActive rj.IsActive
//        let beginMode = 
//            let hasTessMod = program.Handle |> Mod.map (fun p -> p.HasTessellation)
//            x.CreateBeginMode(hasTessMod, rj.Mode)
//        let drawCalls = if isNull rj.DrawCallInfos then Unchecked.defaultof<_> else x.CreateDrawCallInfoList rj.DrawCallInfos
//        let depthTest = x.CreateDepthTest rj.DepthTest
//        let cullMode = x.CreateCullMode rj.CullMode
//        let polygonMode = x.CreatePolygonMode rj.FillMode
//        let blendMode = x.CreateBlendMode rj.BlendMode
//        let stencilMode = x.CreateStencilMode rj.StencilMode
//        let conservativeRaster = x.CreateFlag rj.ConservativeRaster
//        let multisample = x.CreateFlag rj.Multisample


//        // finally return the PreparedRenderObject
//        let res = 
//            {
//                Activation = activation
//                Context = x.Context
//                Original = rj
//                Parent = None
//                FramebufferSignature = fboSignature
//                LastTextureSlot = !lastTextureSlot
//                Program = program
//                StorageBuffers = storageBuffers
//                UniformBuffers = uniformBuffers
//                Uniforms = Map.empty
//                Textures = textureBinding
//                Buffers = buffers
//                IndexBuffer = index
//                IndirectBuffer = indirect
//                VertexInputBinding = vibh
//                //VertexAttributeValues = attributeValues
//                ColorAttachmentCount = attachmentCount
//                DrawBuffers = drawBuffers
//                ColorBufferMasks = colorMasks
//                DepthBufferMask = depthMask
//                StencilBufferMask = stencilMask
//                IsDisposed = false
//                ResourceCount = -1
//                ResourceCounts = Map.empty

//                IsActive = isActive
//                BeginMode = beginMode
//                DrawCallInfos = drawCalls
//                DepthTestMode = depthTest
//                CullMode = cullMode
//                PolygonMode = polygonMode
//                BlendMode = blendMode
//                StencilMode = stencilMode
//                ConservativeRaster = conservativeRaster
//                Multisample = multisample
//            }

//        res.ResourceCount <- res.Resources |> Seq.length
//        res.ResourceCounts <- res.Resources |> Seq.countBy (fun r -> r.Kind) |> Map.ofSeq
//        OpenTK.Graphics.OpenGL4.GL.UnbindAllBuffers()

//        res
