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

        pUniformProvider : IUniformProvider

        pFramebufferSignature : IFramebufferSignature
        pLastTextureSlot : int
        pProgram : IResource<Program, int>
        pProgramInterface : GLSLProgramInterface
        pUniformBuffers : Map<int, IResource<UniformBufferView, int>>
        pStorageBuffers : Map<int, IResource<Buffer, int>>
        pUniforms : Map<int, IResource<UniformLocation, nativeint>>
        pTextures : IResource<TextureBinding, TextureBinding>
        
        pDepthTestMode : IResource<DepthTestInfo, DepthTestInfo>
        pDepthBias : IResource<DepthBiasInfo, DepthBiasInfo>
        pCullMode : IResource<int, int>
        pFrontFace : IResource<int, int>
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
            yield x.pDepthBias :> _
            yield x.pCullMode :> _
            yield x.pFrontFace :> _
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
                    x.DrawBufferManager.CreateConfig(set) |> Some
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
        let depthBias = x.CreateDepthBias rj.DepthBias
        let cullMode = x.CreateCullMode rj.CullMode
        let frontFace = x.CreateFrontFace rj.FrontFace
        let polygonMode = x.CreatePolygonMode rj.FillMode
        let blendMode = x.CreateBlendMode rj.BlendMode
        let stencilMode = x.CreateStencilMode rj.StencilMode
        let conservativeRaster = x.CreateFlag rj.ConservativeRaster
        let multisample = x.CreateFlag rj.Multisample
        
        {
            pUniformProvider = rj.Uniforms
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
            pDepthBias = depthBias
            pCullMode = cullMode
            pFrontFace = frontFace
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
                |> List.choose (fun (_,buf) ->
                    
                    let buffer = 
                        match rj.globalUniforms.TryGetUniform(Ag.emptyScope, Symbol.Create buf.ssbName) with
                            | Some (:? IMod<IBuffer> as b) ->
                                x.CreateBuffer(b) |> Some
                            | Some m ->
                                let o = toBufferCache.Invoke(m)
                                x.CreateBuffer(o) |> Some
                            | _ ->
                                None //failwithf "[GL] could not find storage buffer %A" buf.ssbName

                    match buffer with
                    | Some buffer -> 
                        Some (buf.ssbBinding, buffer)
                    | None ->
                        None
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
                    x.DrawBufferManager.CreateConfig(set) |> Some
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
        let depthBias = x.CreateDepthBias rj.depthBias
        let cullMode = x.CreateCullMode rj.cullMode
        let frontFace = x.CreateFrontFace rj.frontFace
        let polygonMode = x.CreatePolygonMode rj.fillMode
        let blendMode = x.CreateBlendMode rj.blendMode
        let stencilMode = x.CreateStencilMode rj.stencilMode
        let conservativeRaster = x.CreateFlag (Mod.constant false)
        let multisample = x.CreateFlag rj.multisample
        
        
        {
            pUniformProvider = rj.globalUniforms
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
            pDepthBias = depthBias
            pCullMode = cullMode
            pFrontFace = frontFace
            pPolygonMode = polygonMode
            pBlendMode = blendMode
            pStencilMode = stencilMode
            pConservativeRaster = conservativeRaster
            pMultisample = multisample
        }
  

type NativeStats =
    struct
        val mutable public InstructionCount : int
        static member Zero = NativeStats()
        static member (+) (l : NativeStats, r : NativeStats) = NativeStats(InstructionCount = l.InstructionCount + r.InstructionCount)
        static member (-) (l : NativeStats, r : NativeStats) = NativeStats(InstructionCount = l.InstructionCount - r.InstructionCount)
    end


[<AutoOpen>]
module PreparedPipelineStateAssembler =
    
    let private usedClipPlanes (iface : GLSLProgramInterface) =
        let candidates = [FShade.ShaderStage.Geometry; FShade.ShaderStage.TessEval; FShade.ShaderStage.Vertex ]
        let beforeRasterize = candidates |> List.tryPick (fun s -> MapExt.tryFind s iface.shaders)
        match beforeRasterize with
        | Some shader ->
            match MapExt.tryFind "gl_ClipDistance" shader.shaderBuiltInOutputs with
            | Some t ->
                let cnt = 
                    match t with
                    | GLSLType.Array(len,_,_) -> len
                    | _ -> 8
                Seq.init cnt id |> Set.ofSeq
            | None ->
                Set.empty
        | None ->
            Set.empty

    type ICommandStream with
    
        member x.SetPipelineState(s : CompilerInfo, me : PreparedPipelineState) : NativeStats =

            let mutable icnt = 0 // counting dynamic instructions

            x.SetDepthMask(me.pDepthBufferMask)
            x.SetStencilMask(me.pStencilBufferMask)
            match me.pDrawBuffers with
                | None ->
                    x.SetDrawBuffers(s.drawBufferCount, s.drawBuffers)
                | Some b ->
                    x.SetDrawBuffers(b.Count, NativePtr.toNativeInt b.Buffers)
                                       
            x.SetDepthTest(me.pDepthTestMode)  
            x.SetDepthBias(me.pDepthBias)
            x.SetPolygonMode(me.pPolygonMode)
            x.SetCullMode(me.pCullMode)
            x.SetFrontFace(me.pFrontFace)
            x.SetBlendMode(me.pBlendMode)
            x.SetStencilMode(me.pStencilMode)
            x.SetMultisample(me.pMultisample)
            
            let myProg = me.pProgram.Handle.GetValue()
            x.UseProgram(me.pProgram)
            if myProg.WritesPointSize then
                x.Enable(int OpenTK.Graphics.OpenGL4.EnableCap.ProgramPointSize)
            else
                x.Disable(int OpenTK.Graphics.OpenGL4.EnableCap.ProgramPointSize)

            let meUsed = usedClipPlanes me.pProgramInterface
            for i in meUsed do
                x.Enable(int OpenTK.Graphics.OpenGL4.EnableCap.ClipDistance0 + i)
                icnt <- icnt + 1
            

            // bind all uniform-buffers (if needed)
            for (id,ub) in Map.toSeq me.pUniformBuffers do
                x.BindUniformBufferView(id, ub)
                icnt <- icnt + 1

            for (id, ssb) in Map.toSeq me.pStorageBuffers do
                x.BindStorageBuffer(id, ssb)
                icnt <- icnt + 1

            // bind all textures/samplers (if needed)

            let binding = me.pTextures.Handle.GetValue()
            
            x.BindTexturesAndSamplers(me.pTextures)

            // bind all top-level uniforms (if needed)
            for (id,u) in Map.toSeq me.pUniforms do
                x.BindUniformLocation(id, u)
                icnt <- icnt + 1

            NativeStats(InstructionCount = icnt + 14) // 14 fixed instruction 
            

        member x.SetPipelineState(s : CompilerInfo, me : PreparedPipelineState, prev : PreparedPipelineState) : NativeStats =
            
            let mutable icnt = 0

            if prev.pDepthBufferMask <> me.pDepthBufferMask then
                x.SetDepthMask(me.pDepthBufferMask)
                icnt <- icnt + 1

            if prev.pStencilBufferMask <> me.pStencilBufferMask then
                x.SetStencilMask(me.pStencilBufferMask)
                icnt <- icnt + 1

            if prev.pDrawBuffers <> me.pDrawBuffers then
                match me.pDrawBuffers with
                    | None ->
                        x.SetDrawBuffers(s.drawBufferCount, s.drawBuffers)
                    | Some b ->
                        x.SetDrawBuffers(b.Count, NativePtr.toNativeInt b.Buffers)
                icnt <- icnt + 1
                   
            if prev.pDepthTestMode <> me.pDepthTestMode then
                x.SetDepthTest(me.pDepthTestMode)  
                icnt <- icnt + 1

            if prev.pDepthBias <> me.pDepthBias then
                x.SetDepthBias(me.pDepthBias)  
                icnt <- icnt + 1
                
            if prev.pPolygonMode <> me.pPolygonMode then
                x.SetPolygonMode(me.pPolygonMode)
                icnt <- icnt + 1
                
            if prev.pCullMode <> me.pCullMode then
                x.SetCullMode(me.pCullMode)
                icnt <- icnt + 1
            
            if prev.pFrontFace <> me.pFrontFace then
                x.SetFrontFace(me.pFrontFace)
                icnt <- icnt + 1

            if prev.pBlendMode <> me.pBlendMode then
                x.SetBlendMode(me.pBlendMode)
                icnt <- icnt + 1

            if prev.pStencilMode <> me.pStencilMode then
                x.SetStencilMode(me.pStencilMode)
                icnt <- icnt + 1

            if prev.pMultisample <> me.pMultisample then
                x.SetMultisample(me.pMultisample)
                icnt <- icnt + 1

            if prev.pProgram <> me.pProgram then
                let myProg = me.pProgram.Handle.GetValue()
                x.UseProgram(me.pProgram)
                icnt <- icnt + 1

                if obj.ReferenceEquals(prev.pProgram, null) || prev.pProgram.Handle.GetValue().WritesPointSize <> myProg.WritesPointSize then
                    if myProg.WritesPointSize then
                        x.Enable(int OpenTK.Graphics.OpenGL4.EnableCap.ProgramPointSize)
                    else
                        x.Disable(int OpenTK.Graphics.OpenGL4.EnableCap.ProgramPointSize)
                    icnt <- icnt + 1
            

            let prevUsed = usedClipPlanes prev.pProgramInterface
            let meUsed = usedClipPlanes me.pProgramInterface

            if prevUsed <> meUsed then
                for i in meUsed do
                    x.Enable(int OpenTK.Graphics.OpenGL4.EnableCap.ClipDistance0 + i)
                    icnt <- icnt + 1


            // bind all uniform-buffers (if needed)
            for (id,ub) in Map.toSeq me.pUniformBuffers do
                //do! useUniformBufferSlot id
                
                match Map.tryFind id prev.pUniformBuffers with
                    | Some old when old = ub -> 
                        // the same UniformBuffer has already been bound
                        ()
                    | _ -> 
                        x.BindUniformBufferView(id, ub)
                        icnt <- icnt + 1

            for (id, ssb) in Map.toSeq me.pStorageBuffers do
                match Map.tryFind id prev.pStorageBuffers with
                    | Some old when old = ssb -> 
                        // the same UniformBuffer has already been bound
                        ()
                    | _ -> 
                        x.BindStorageBuffer(id, ssb)
                        icnt <- icnt + 1

            // bind all textures/samplers (if needed)

            let binding = me.pTextures.Handle.GetValue()
            
            if prev.pTextures <> me.pTextures then
                x.BindTexturesAndSamplers(me.pTextures)
                icnt <- icnt + 1

            // bind all top-level uniforms (if needed)
            for (id,u) in Map.toSeq me.pUniforms do
                match Map.tryFind id prev.pUniforms with
                    | Some old when old = u -> ()
                    | _ -> x.BindUniformLocation(id, u); icnt <- icnt + 1

            NativeStats(InstructionCount = icnt)

        member x.SetPipelineState(s : CompilerInfo, me : PreparedPipelineState, prev : Option<PreparedPipelineState>) : NativeStats =
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
            | _ -> 
                let all = x.GetResources() |> Seq.toArray
                resources <- Some all
                all
        )

    let getStats (x : PreparedCommand) =
        lock x (fun () ->
            match resourceStats with
            | Some s -> s
            | _ ->
                let res = getResources x
                let cnt = res.Length
                let counts = res |> Seq.countBy (fun r -> r.Kind) |> Map.ofSeq
                resourceStats <- Some (cnt, counts)
                (cnt, counts)
        )
        
    abstract member GetResources : unit -> seq<IResource>
    abstract member Release : unit -> unit
    abstract member Compile : info : CompilerInfo * stream : ICommandStream * prev : Option<PreparedCommand> -> NativeStats
    abstract member EntryState : Option<PreparedPipelineState>
    abstract member ExitState : Option<PreparedPipelineState>
    
    member x.AddCleanup(clean : unit -> unit) =
        cleanup <- clean :: cleanup

    member x.Id = id
    member x.Pass = renderPass
    member x.IsDisposed = refCount = 0
    member x.Context = ctx

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
                        x.Release()
                        cleanup <- []
                        resourceStats <- None
                        resources <- None
                    finally
                        token.Dispose()
                | None ->
                    //OpenGL died
                    ()
            )

    interface IRenderObject with
        member x.AttributeScope = Ag.emptyScope
        member x.Id = x.Id
        member x.RenderPass = renderPass

    interface IPreparedRenderObject with
        member x.Update(t,rt) = x.Update(t, rt)
        member x.Original = None
        member x.Dispose() = x.Dispose()

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
        let beginMode = x.CreateBeginMode(program.Handle, rj.Mode)
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
    
    type ICommandStream with
        member x.Render(s : CompilerInfo, me : PreparedObjectInfo) : NativeStats =
        
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

            NativeStats(InstructionCount = 2)

        member x.Render(s : CompilerInfo, me : PreparedObjectInfo, prev : PreparedObjectInfo) : NativeStats =
        
            let mutable icnt = 0

            // bind the VAO (if needed)
            if prev.oVertexInputBinding <> me.oVertexInputBinding then
                x.BindVertexAttributes(s.contextHandle, me.oVertexInputBinding)
                icnt <- icnt + 1

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

            NativeStats(InstructionCount = icnt + 1)

        member x.Render(s : CompilerInfo, me : PreparedObjectInfo, prev : Option<PreparedObjectInfo>) : NativeStats =
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
        for i in 0 .. 7 do
            stream.Disable(int OpenTK.Graphics.OpenGL4.EnableCap.ClipDistance0 + i)
        NativeStats(InstructionCount = 13)

    override x.EntryState = None
    override x.ExitState = None

type NopCommand(ctx : Context, pass : RenderPass) =
    inherit PreparedCommand(ctx, pass) 

    override x.GetResources() = Seq.empty
    override x.Release() = ()
    override x.Compile(_,_,_) = NativeStats.Zero
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

    override x.Compile(s : CompilerInfo, stream : ICommandStream, prev : Option<PreparedCommand>) : NativeStats =
        let prevInfo =
            match prev with
                | Some (:? PreparedObjectCommand as p) -> Some p.Info
                | _ -> None

        let prevState =
            match prev with
            | Some p -> p.ExitState
            | _ -> None

            
        let stats = stream.SetPipelineState(s, state, prevState)
        stats + stream.Render(s, info, prevInfo)

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
        let mutable s = NativeStats.Zero
        for c in cmds do
            s <- s + c.Compile(info, stream, prev)
            prev <- Some c
        s

    override x.EntryState = first |> Option.bind (fun first -> first.EntryState)
    override x.ExitState = last |> Option.bind (fun last -> last.ExitState)


module PreparedCommand =

    let ofRenderObject (fboSignature : IFramebufferSignature) (x : ResourceManager) (o : IRenderObject) =

        let rec ofRenderObject (owned : bool) (fboSignature : IFramebufferSignature) (x : ResourceManager) (o : IRenderObject) =
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
                            ofRenderObject owned fboSignature x o

                        | l -> 
                            new MultiCommand(x.Context, l |> List.map (ofRenderObject owned fboSignature x), pass) :> PreparedCommand

                | :? PreparedCommand as cmd ->
                    if not owned then cmd.AddReference()
                    cmd

                | :? ICustomRenderObject as o ->
                    o.Create(fboSignature.Runtime, fboSignature) |> ofRenderObject true fboSignature x

                | _ ->
                    failwithf "bad object: %A" o

        ofRenderObject false fboSignature x o