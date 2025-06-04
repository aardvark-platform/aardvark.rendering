namespace Aardvark.Rendering.GL

#nowarn "9"

open System
open System.Collections.Generic
open FSharp.Data.Adaptive
open Aardvark.Base

open Aardvark.Rendering
open Microsoft.FSharp.NativeInterop
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL
open Aardvark.Assembler
open FShade.GLSL
open System.Threading

// NOTE: Hacky solution for concurrency issues.
// This lock is used for all OpenGL render tasks, basically preventing any concurrency.
// The Vulkan backend has finer grained control over resource ownership.
// When acquiring this lock an OpenGL context should be current, otherwise deadlocks may occur when trying to acquire a resource context later!
module GlobalResourceLock =

    let lockObj = obj()

    [<Struct>]
    type Disposable(lockTaken : bool) =
        member x.Dispose() = if lockTaken then Monitor.Exit lockObj
        interface IDisposable with
            member x.Dispose() = x.Dispose()

    let using (action : unit -> 'T) =
        if RuntimeConfig.AllowConcurrentResourceAccess then action()
        else lock lockObj action

    let inline lock() : Disposable =
        let mutable lockTaken = false

        try
            if not RuntimeConfig.AllowConcurrentResourceAccess then
                Monitor.Enter(lockObj, &lockTaken)

            new Disposable(lockTaken)

        with _ ->
            if lockTaken then Monitor.Exit(lockObj)
            reraise()

type TextureBindingSlot =
    | ArrayBinding of IResource<TextureArrayBinding, TextureArrayBinding>
    | SingleBinding of IResource<Texture, TextureBinding> * IResource<Sampler, int>

type PreparedPipelineState =
    {
        pContext : Context

        pUniformProvider : IUniformProvider

        pFramebufferSignature : IFramebufferSignature
        pProgram : IResource<Program, int>
        pProgramInterface : GLSLProgramInterface
        pUniformBuffers : (struct (int * IResource<UniformBufferView, int>))[] // sorted list of uniform buffers
        pStorageBuffers : (struct (int * IResource<Buffer, int>))[] // sorted list of storage buffers
        pTextureBindings : (struct (Range1i * TextureBindingSlot))[] // sorted list of texture bindings

        pBlendColor : IResource<C4f, C4f>
        pBlendModes : IResource<nativeptr<GLBlendMode>, nativeint>
        pColorMasks : IResource<nativeptr<GLColorMask>, nativeint>

        pDepthTest : IResource<int, int>
        pDepthBias : IResource<DepthBiasInfo, DepthBiasInfo>
        pDepthMask : IResource<bool, int>
        pDepthClamp : IResource<bool, int>

        pStencilModeFront : IResource<GLStencilMode, GLStencilMode>
        pStencilModeBack : IResource<GLStencilMode, GLStencilMode>
        pStencilMaskFront : IResource<uint32, uint32>
        pStencilMaskBack : IResource<uint32, uint32>

        pCullMode : IResource<int, int>
        pFrontFace : IResource<int, int>
        pPolygonMode : IResource<int, int>
        pMultisample : IResource<bool, int>
        pConservativeRaster : IResource<bool, int>
    } 

    member x.Resources =
        seq {
            yield x.pProgram :> IResource
            for struct (_,b) in x.pUniformBuffers do
                yield b :> _

            for struct (_,b) in x.pStorageBuffers do
                yield b :> _

            for struct (_, tb) in x.pTextureBindings do
                match tb with 
                | ArrayBinding ta -> yield ta :> _
                | SingleBinding (tex, sam) -> yield tex :> _; yield sam :> _

            yield x.pBlendColor :> _
            yield x.pBlendModes :> _
            yield x.pColorMasks :> _

            yield x.pDepthTest :> _
            yield x.pDepthBias :> _
            yield x.pDepthMask :> _
            yield x.pDepthClamp :> _

            yield x.pStencilModeFront :> _
            yield x.pStencilModeBack :> _
            yield x.pStencilMaskFront :> _
            yield x.pStencilMaskBack :> _

            yield x.pCullMode :> _
            yield x.pFrontFace :> _
            yield x.pPolygonMode :> _
            yield x.pMultisample :> _
            yield x.pConservativeRaster :> _
        }

    member x.Dispose() =
        lock x (fun () -> 
            // ObjDisposed might occur here if GL is dead already and render objects get disposed nondeterministically by finalizer thread.
            let resourceLock = try Some x.pContext.ResourceLock with :? ObjectDisposedException as o -> None

            match resourceLock with
                | None ->
                    // OpenGL already dead
                    ()
                | Some l -> 
                    use __ = l

                    OpenTK.Graphics.OpenGL4.GL.UnbindAllBuffers()

                    for struct (_, tb) in x.pTextureBindings do
                        match tb with
                        | SingleBinding (tex, sam) -> tex.Dispose(); sam.Dispose()
                        | ArrayBinding ta -> ta.Dispose()

                    x.pUniformBuffers |> Array.iter (fun struct (_, ub) -> ub.Dispose())
                    x.pStorageBuffers |> Array.iter (fun struct (_, sb) -> sb.Dispose())
                    x.pProgram.Dispose()

                    x.pBlendColor.Dispose()
                    x.pBlendModes.Dispose()
                    x.pColorMasks.Dispose()

                    x.pDepthTest.Dispose()
                    x.pDepthBias.Dispose()
                    x.pDepthMask.Dispose()
                    x.pDepthClamp.Dispose()

                    x.pStencilModeFront.Dispose()
                    x.pStencilModeBack.Dispose()
                    x.pStencilMaskFront.Dispose()
                    x.pStencilMaskBack.Dispose()

                    x.pCullMode.Dispose()
                    x.pFrontFace.Dispose()
                    x.pPolygonMode.Dispose()
                    x.pMultisample.Dispose()
                    x.pConservativeRaster.Dispose()
        )

    interface IDisposable with
        member x.Dispose() = x.Dispose()


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PreparedPipelineState =
    open Uniforms.Patterns

    // Utilities for keeping track of resources, so they can be disposed if an exception is raised at some point.
    // Useful when using the shader debugger, where an exception in ofRenderObject does not necessarily lead to program termination.
    [<AutoOpen>]
    module internal ResourceUtilities =

        let inline addResource (resources : List<IDisposable>) (resource : #IDisposable) =
            if resources <> null then resources.Add resource |> ignore
            resource

        let inline removeResource (resources : List<IDisposable>) (resource : IDisposable) =
            if resources <> null then resources.Remove resource |> ignore

    type ResourceManager with
        member x.CreateUniformBuffers(iface : InterfaceSlots, uniforms : IUniformProvider, scope : Ag.Scope, resources : List<IDisposable>) =
            iface.uniformBuffers
            |> Array.map (fun (_, block) ->
                let buffer = x.CreateUniformBuffer(scope, block, uniforms)
                struct (block.ubBinding, buffer |> addResource resources)
            )

        member inline x.CreateUniformBuffers(iface : InterfaceSlots, uniforms : IUniformProvider, scope : Ag.Scope) =
            x.CreateUniformBuffers(iface, uniforms, scope, null)

        member x.CreateStorageBuffers(iface : InterfaceSlots, uniforms : IUniformProvider, scope : Ag.Scope, resources : List<IDisposable>) =
            iface.storageBuffers |> Array.map (fun (_, block) ->
                let bufferName = Sym.ofString block.ssbName

                let buffer =
                    match uniforms.TryGetUniform(scope, bufferName) with
                    | NullUniform ->
                        failf "storage buffer '%A' is null" bufferName

                    | CastUniformResource (buffer : aval<IBuffer>) ->
                        x.CreateStorageBuffer(bufferName, buffer)

                    | CastUniformResource (array : aval<Array>) ->
                        x.CreateStorageBuffer(bufferName, array)

                    | ValueSome value ->
                        failf "invalid type '%A' for storage buffer '%A' (expected a subtype of IBuffer or Array)" value.ContentType bufferName

                    | _ ->
                        failf "could not find storage buffer '%A'" bufferName

                struct (block.ssbBinding, buffer |> addResource resources)
            )

        member inline x.CreateStorageBuffers(iface : InterfaceSlots, uniforms : IUniformProvider, scope : Ag.Scope) =
            x.CreateStorageBuffers(iface, uniforms, scope, null)

        member x.CreateTextureBindings(iface : InterfaceSlots, uniforms : IUniformProvider, scope : Ag.Scope, resources : List<IDisposable>) =
            let createSampler (textureName : Symbol) (samplerState : FShade.SamplerState) =
                let samplerModifier =
                    match uniforms.TryGetUniform(scope, Sym.ofString $"{DefaultSemantic.SamplerStateModifier}_{textureName}") with
                    | ValueSome (:? aval<SamplerState -> SamplerState> as mode) ->
                        ValueSome mode
                    | _ ->
                        ValueNone

                let sampler =
                    match samplerModifier with
                    | ValueSome modifier ->
                        let samplerState = x.GetSamplerStateDescription(samplerState)
                        x.GetDynamicSamplerState(samplerState, modifier)
                    | ValueNone ->
                        x.GetStaticSamplerState(samplerState)

                x.CreateSampler(sampler)

            let createTexture (textureName : Symbol) (samplerType : GLSLSamplerType) =
                match uniforms.TryGetUniform(scope, textureName) with
                | NullUniform ->
                    failf "texture '%A' is null" textureName

                | CastUniformResource (texture : aval<ITexture>) ->
                    x.CreateTexture(textureName, texture, samplerType)

                | CastUniformResource (level : aval<ITextureLevel>) ->
                    x.CreateTexture(textureName, level, samplerType)

                | ValueSome t ->
                    failf "invalid type '%A' for texture '%A' (expected a subtype of ITexture or ITextureLevel)" t.ContentType textureName

                | _ ->
                    failf "could not find texture '%A'" textureName

            iface.samplers
            |> Array.map (fun (_, sam) ->
                let slotRange = Range1i.FromMinAndSize(sam.samplerBinding, sam.samplerCount - 1)

                // Check if we have a sampler array with a single sampler state
                // -> Expect a aval<ITexture[]>
                let arrayBinding =
                    sam.Array |> Option.bind (fun (textureName, samplerState) ->
                        let textureName = Symbol.Create textureName

                        match uniforms.TryGetUniform(Ag.Scope.Root, textureName) with
                        | NullUniform ->
                            failf "texture array '%A' is null" textureName

                        | ValueSome (:? aval<ITexture[]> as textureArray) ->
                            let sampler = createSampler textureName samplerState
                            let textureArray = x.CreateTextureArray(textureName, sam.samplerCount, textureArray, sam.samplerType)
                            let arrayBinding = x.CreateTextureBinding(slotRange, textureArray, sampler)
                            Some <| ArrayBinding (arrayBinding |> addResource resources)

                        | ValueSome t ->
                            failf "invalid type '%A' for texture array '%A' (expected ITexture[])" t.ContentType textureName

                        | _ ->
                            None
                    )

                let binding =
                    arrayBinding |> Option.defaultWith (fun _ ->
                        match sam.samplerTextures with
                        | [] ->
                            failf "could not get sampler information for: %A" sam

                        // Plain single texture with sampler state
                        | [textureName, samplerState] ->
                            let textureName = Symbol.Create textureName
                            let texture = createTexture textureName sam.samplerType |> addResource resources
                            let sampler = createSampler textureName samplerState |> addResource resources
                            SingleBinding (texture, sampler)

                        // Multiple textures with individual sampler states
                        | descriptions ->
                            let textures =
                                descriptions
                                |> List.map (fun (textureName, samplerState) ->
                                    let textureName = Symbol.Create textureName
                                    let texture = createTexture textureName sam.samplerType |> addResource resources
                                    let sampler = createSampler textureName samplerState |> addResource resources
                                    texture, sampler
                                )
                                |> List.toArray

                            let binding = x.CreateTextureBinding(slotRange, textures)

                            // fix texture ref-count
                            for (t, s) in textures do
                                t.RemoveRef()
                                t |> removeResource resources
                                s.RemoveRef()
                                s |> removeResource resources

                            ArrayBinding (binding |> addResource resources)
                    )

                struct (slotRange, binding)
            )

        member x.CreateTextureBindings(iface : InterfaceSlots, uniforms : IUniformProvider, scope : Ag.Scope) =
            x.CreateTextureBindings(iface, uniforms, scope, null)

        member x.CreateImageBindings(iface : InterfaceSlots, uniforms : IUniformProvider, scope : Ag.Scope, resources : List<IDisposable>) =
            iface.images
            |> Array.map (fun (_, image) ->
                let binding =
                    let imageName = Symbol.Create image.imageName

                    match uniforms.TryGetUniform(scope, imageName) with
                    | NullUniform ->
                        failf "storage image '%A' is null" imageName

                    | CastUniformResource (texture : aval<ITexture>) ->
                        x.CreateImageBinding(imageName, texture, image.imageType)

                    | CastUniformResource (level : aval<ITextureLevel>) ->
                        x.CreateImageBinding(imageName, level, image.imageType)

                    | ValueSome i ->
                        failf "invalid type '%A' for storage image '%A' (expected a subtype of ITexture or ITextureLevel)" i.ContentType imageName

                    | ValueNone ->
                        failf "could not find storage image '%A'" imageName

                struct (image.imageBinding, binding |> addResource resources)
            )

        member inline x.CreateImageBindings(iface : InterfaceSlots, uniforms : IUniformProvider, scope : Ag.Scope) =
            x.CreateImageBindings(iface, uniforms, scope, null)

    let ofRenderObject (fboSignature : IFramebufferSignature) (x : ResourceManager) (rj : RenderObject) =
        // use a context token to avoid making context current/uncurrent repeatedly
        use __ = x.Context.ResourceLock
        let resources = List<IDisposable>(capacity = 32)

        try
            let iface, program = x.CreateSurface(fboSignature, rj.Surface, rj.Mode)
            resources.Add program

            let slots = x.GetInterfaceSlots(iface)
            GL.Check "[Prepare] Create Surface"

            // create all UniformBuffers requested by the program
            let uniformBuffers = x.CreateUniformBuffers(slots, rj.Uniforms, rj.AttributeScope, resources)
            GL.Check "[Prepare] Uniform Buffers"

            let storageBuffers = x.CreateStorageBuffers(slots, rj.Uniforms, rj.AttributeScope, resources)
            GL.Check "[Prepare] Storage Buffers"

            let textureBindings = x.CreateTextureBindings(slots, rj.Uniforms, rj.AttributeScope, resources)
            GL.Check "[Prepare] Textures"

            let blendColor = x.CreateColor rj.BlendState.ConstantColor |> addResource resources
            let blendModes = x.CreateBlendModes(fboSignature, rj.BlendState.Mode, rj.BlendState.AttachmentMode) |> addResource resources
            let colorMasks = x.CreateColorMasks(fboSignature, rj.BlendState.ColorWriteMask, rj.BlendState.AttachmentWriteMask) |> addResource resources

            let depthTest = x.CreateDepthTest rj.DepthState.Test |> addResource resources
            let depthBias = x.CreateDepthBias rj.DepthState.Bias |> addResource resources
            let depthMask = x.CreateFlag rj.DepthState.WriteMask |> addResource resources
            let depthClamp = x.CreateFlag rj.DepthState.Clamp |> addResource resources

            let stencilModeFront = x.CreateStencilMode(rj.StencilState.ModeFront) |> addResource resources
            let stencilModeBack = x.CreateStencilMode(rj.StencilState.ModeBack) |> addResource resources
            let stencilMaskFront = x.CreateStencilMask(rj.StencilState.WriteMaskFront) |> addResource resources
            let stencilMaskBack = x.CreateStencilMask(rj.StencilState.WriteMaskBack) |> addResource resources

            let cullMode = x.CreateCullMode rj.RasterizerState.CullMode |> addResource resources
            let frontFace = x.CreateFrontFace rj.RasterizerState.FrontFacing |> addResource resources
            let polygonMode = x.CreatePolygonMode rj.RasterizerState.FillMode |> addResource resources
            let multisample = x.CreateFlag rj.RasterizerState.Multisample |> addResource resources
            let conservativeRaster = x.CreateFlag rj.RasterizerState.ConservativeRaster |> addResource resources

            {
                pUniformProvider = rj.Uniforms
                pContext = x.Context
                pFramebufferSignature = fboSignature
                pProgram = program
                pProgramInterface = iface
                pStorageBuffers = storageBuffers
                pUniformBuffers = uniformBuffers
                pTextureBindings = textureBindings

                pBlendColor = blendColor
                pBlendModes = blendModes
                pColorMasks = colorMasks

                pDepthTest = depthTest
                pDepthBias = depthBias
                pDepthMask = depthMask
                pDepthClamp = depthClamp

                pStencilModeFront = stencilModeFront
                pStencilModeBack = stencilModeBack
                pStencilMaskFront = stencilMaskFront
                pStencilMaskBack = stencilMaskBack

                pCullMode = cullMode
                pFrontFace = frontFace
                pPolygonMode = polygonMode
                pMultisample = multisample
                pConservativeRaster = conservativeRaster
            }

        with _ ->
            for r in resources do r.Dispose()
            reraise()

    let ofPipelineState (fboSignature : IFramebufferSignature) (x : ResourceManager) (surface : Surface) (rj : PipelineState) =
        // use a context token to avoid making context current/uncurrent repeatedly
        use __ = x.Context.ResourceLock
        let resources = List<IDisposable>(capacity = 32)

        try
            let iface, program = x.CreateSurface(fboSignature, surface, rj.Mode)
            resources.Add program

            let slots = x.GetInterfaceSlots(iface)
            GL.Check "[Prepare] Create Surface"

            // create all UniformBuffers requested by the program
            let uniformBuffers = x.CreateUniformBuffers(slots, rj.GlobalUniforms, Ag.Scope.Root, resources)
            GL.Check "[Prepare] Uniform Buffers"

            let storageBuffers = x.CreateStorageBuffers(slots, rj.GlobalUniforms, Ag.Scope.Root, resources)
            GL.Check "[Prepare] Storage Buffers"

            let textureBindings = x.CreateTextureBindings(slots, rj.GlobalUniforms, Ag.Scope.Root, resources)
            GL.Check "[Prepare] Textures"

            let blendColor = x.CreateColor rj.BlendState.ConstantColor |> addResource resources
            let blendModes = x.CreateBlendModes(fboSignature, rj.BlendState.Mode, rj.BlendState.AttachmentMode) |> addResource resources
            let colorMasks = x.CreateColorMasks(fboSignature, rj.BlendState.ColorWriteMask, rj.BlendState.AttachmentWriteMask) |> addResource resources

            let depthTest= x.CreateDepthTest rj.DepthState.Test |> addResource resources
            let depthBias = x.CreateDepthBias rj.DepthState.Bias |> addResource resources
            let depthMask = x.CreateFlag rj.DepthState.WriteMask |> addResource resources
            let depthClamp = x.CreateFlag rj.DepthState.Clamp |> addResource resources

            let stencilModeFront = x.CreateStencilMode(rj.StencilState.ModeFront) |> addResource resources
            let stencilModeBack = x.CreateStencilMode(rj.StencilState.ModeBack) |> addResource resources
            let stencilMaskFront = x.CreateStencilMask(rj.StencilState.WriteMaskFront) |> addResource resources
            let stencilMaskBack = x.CreateStencilMask(rj.StencilState.WriteMaskBack) |> addResource resources

            let cullMode = x.CreateCullMode rj.RasterizerState.CullMode |> addResource resources
            let frontFace = x.CreateFrontFace rj.RasterizerState.FrontFacing |> addResource resources
            let polygonMode = x.CreatePolygonMode rj.RasterizerState.FillMode |> addResource resources
            let multisample = x.CreateFlag rj.RasterizerState.Multisample |> addResource resources
            let conservativeRaster = x.CreateFlag rj.RasterizerState.ConservativeRaster |> addResource resources

            {
                pUniformProvider = rj.GlobalUniforms
                pContext = x.Context
                pFramebufferSignature = fboSignature
                pProgram = program
                pProgramInterface = iface
                pStorageBuffers = storageBuffers
                pUniformBuffers = uniformBuffers
                pTextureBindings = textureBindings

                pBlendColor = blendColor
                pBlendModes = blendModes
                pColorMasks = colorMasks

                pDepthTest = depthTest
                pDepthBias = depthBias
                pDepthMask = depthMask
                pDepthClamp = depthClamp

                pStencilModeFront = stencilModeFront
                pStencilModeBack = stencilModeBack
                pStencilMaskFront = stencilMaskFront
                pStencilMaskBack = stencilMaskBack

                pCullMode = cullMode
                pFrontFace = frontFace
                pPolygonMode = polygonMode
                pMultisample = multisample
                pConservativeRaster = conservativeRaster
            }

        with _ ->
            for r in resources do r.Dispose()
            reraise()


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
        match iface.shaders with
        | GLSLProgramShaders.Graphics { stages = shaders } ->
            let candidates = [FShade.ShaderStage.Geometry; FShade.ShaderStage.TessEval; FShade.ShaderStage.Vertex ]
            let beforeRasterize = candidates |> List.tryPick (fun s -> MapExt.tryFind s shaders)
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
        | _ ->
            Set.empty


    type ICommandStream with
    
        member x.SetPipelineState(s : CompilerInfo, me : PreparedPipelineState) : NativeStats =

            let mutable icnt = 0 // counting dynamic instructions

            x.SetBlendColor(me.pBlendColor)
            x.SetBlendModes(s.drawBufferCount, me.pBlendModes)
            x.SetColorMasks(s.drawBufferCount, me.pColorMasks)

            x.SetDepthTest(me.pDepthTest)
            x.SetDepthBias(me.pDepthBias)
            x.SetDepthMask(me.pDepthMask)
            x.SetDepthClamp(me.pDepthClamp)

            x.SetStencilModes(me.pStencilModeFront, me.pStencilModeBack)
            x.SetStencilMask(StencilFace.Front, me.pStencilMaskFront)
            x.SetStencilMask(StencilFace.Back, me.pStencilMaskBack)

            x.SetPolygonMode(me.pPolygonMode)
            x.SetCullMode(me.pCullMode)
            x.SetFrontFace(me.pFrontFace)
            x.SetMultisample(me.pMultisample)
            x.SetConservativeRaster(GL.NV_conservative_raster, me.pConservativeRaster)
            
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
            for struct (id, ub) in me.pUniformBuffers do
                x.BindUniformBufferView(id, ub)
                icnt <- icnt + 1

            for struct (id, ssb) in me.pStorageBuffers do
                x.BindStorageBuffer(id, ssb)
                icnt <- icnt + 1

            // bind all textures/samplers (if needed)
            for struct (slotRange, binding) in me.pTextureBindings do
                match binding with 
                | SingleBinding (tex, sam) ->
                    x.SetActiveTexture(slotRange.Min)
                    x.BindTexture(tex)
                    x.BindSampler(slotRange.Min, sam)
                    icnt <- icnt + 3
                | ArrayBinding ta ->
                    x.BindTexturesAndSamplers(ta) // internally will use 2 OpenGL calls glBindTextures and glBindSamplers
                    icnt <- icnt + 1 
                    
            NativeStats(InstructionCount = icnt + 17) // 17 fixed instruction 
            

        member x.SetPipelineState(s : CompilerInfo, me : PreparedPipelineState, prev : PreparedPipelineState) : NativeStats =
            
            let mutable icnt = 0

            // Blending
            if prev.pBlendColor <> me.pBlendColor then
                x.SetBlendColor(me.pBlendColor)
                icnt <- icnt + 1

            if prev.pBlendModes <> me.pBlendModes then
                x.SetBlendModes(s.drawBufferCount, me.pBlendModes)
                icnt <- icnt + 1

            if prev.pColorMasks <> me.pColorMasks then
                x.SetColorMasks(s.drawBufferCount, me.pColorMasks)
                icnt <- icnt + 1


            // Depth state
            if prev.pDepthTest <> me.pDepthTest then
                x.SetDepthTest(me.pDepthTest)
                icnt <- icnt + 1

            if prev.pDepthBias <> me.pDepthBias then
                x.SetDepthBias(me.pDepthBias)
                icnt <- icnt + 1

            if prev.pDepthMask <> me.pDepthMask then
                x.SetDepthMask(me.pDepthMask)
                icnt <- icnt + 1

            if prev.pDepthClamp <> me.pDepthClamp then
                x.SetDepthClamp(me.pDepthClamp)
                icnt <- icnt + 1

            // Stencil state
            if prev.pStencilModeFront <> me.pStencilModeFront || prev.pStencilModeBack <> me.pStencilModeBack then
                x.SetStencilModes(me.pStencilModeFront, me.pStencilModeBack)
                icnt <- icnt + 1

            if prev.pStencilMaskFront <> me.pStencilMaskFront then
                x.SetStencilMask(StencilFace.Front, me.pStencilMaskFront)
                icnt <- icnt + 1

            if prev.pStencilMaskBack <> me.pStencilMaskBack then
                x.SetStencilMask(StencilFace.Back, me.pStencilMaskBack)
                icnt <- icnt + 1

            // Rasterizer state
            if prev.pPolygonMode <> me.pPolygonMode then
                x.SetPolygonMode(me.pPolygonMode)
                icnt <- icnt + 1

            if prev.pCullMode <> me.pCullMode then
                x.SetCullMode(me.pCullMode)
                icnt <- icnt + 1

            if prev.pFrontFace <> me.pFrontFace then
                x.SetFrontFace(me.pFrontFace)
                icnt <- icnt + 1

            if prev.pMultisample <> me.pMultisample then
                x.SetMultisample(me.pMultisample)
                icnt <- icnt + 1

            if prev.pConservativeRaster <> me.pConservativeRaster then
                x.SetConservativeRaster(GL.NV_conservative_raster, me.pConservativeRaster)
                icnt <- icnt + 1

            // Program
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
            let mutable j = 0
            for struct (id, ub) in me.pUniformBuffers do
                let mutable i = j
                let mutable old = None
                // compare with prev state in parallel / assuming bindings are sorted
                while i < prev.pUniformBuffers.Length do
                    let struct (slt, bnd) = prev.pUniformBuffers.[i]
                    if slt = id then old <- Some bnd
                    if slt < id then i <- i + 1; j <- j + 1
                    else i <- 9999 // break

                match old with
                | Some old when old = ub -> 
                    () // the same UniformBuffer has already been bound
                | _ -> 
                    x.BindUniformBufferView(id, ub)
                    icnt <- icnt + 1

            let mutable j = 0
            for struct (id, ssb) in me.pStorageBuffers do
                let mutable i = j
                let mutable old = None
                // compare with prev state in parallel / assuming bindings are sorted
                while i < prev.pStorageBuffers.Length do
                    let struct (slt, bnd) = prev.pStorageBuffers.[i]
                    if slt = id then old <- Some bnd
                    if slt < id then i <- i + 1; j <- j + 1
                    else i <- 9999 // break

                match old with
                | Some old when old = ssb -> 
                    // the same UniformBuffer has already been bound
                    ()
                | _ -> 
                    x.BindStorageBuffer(id, ssb)
                icnt <- icnt + 1

            // bind all textures/samplers (if needed)
            let mutable j = 0
            for struct (slotRange, binding) in me.pTextureBindings do
                let mutable i = j
                let mutable old = None
                // compare with prev state in parallel / assuming bindings are sorted
                while i < prev.pTextureBindings.Length do
                    let struct (slt, bnd) = prev.pTextureBindings.[i]
                    if slt = slotRange then old <- Some bnd // ranges must perfectly match / does not support overlap of arrays or single textures
                    if slt.Max < slotRange.Min then i <- i + 1; j <- j + 1
                    else i <- Int32.MaxValue // break
                    
                match old with
                | Some old when old = binding -> () // could use more sophisticated compare to detect array overlaps 
                | _ -> 
                    match binding with 
                    | SingleBinding (tex, sam) ->
                        x.SetActiveTexture(slotRange.Min)
                        x.BindTexture(tex)
                        match old with
                        | Some old ->
                            match old with 
                            | SingleBinding (otex, osam) when Object.ReferenceEquals(osam, sam) -> ()
                            | _ ->
                                x.BindSampler(slotRange.Min, sam); icnt <- icnt + 1
                        | None -> x.BindSampler(slotRange.Min, sam); icnt <- icnt + 1
                        icnt <- icnt + 2
                    | ArrayBinding ta ->
                        x.BindTexturesAndSamplers(ta) // internally will use 2 OpenGL calls glBindTextures and glBindSamplers
                        icnt <- icnt + 1 

            NativeStats(InstructionCount = icnt)

        member x.SetPipelineState(s : CompilerInfo, me : PreparedPipelineState, prev : Option<PreparedPipelineState>) : NativeStats =
            match prev with
                | Some prev -> x.SetPipelineState(s, me, prev)
                | None -> x.SetPipelineState(s, me)


[<AbstractClass>]
type PreparedCommand(ctx : Context, renderPass : RenderPass, renderObject : RenderObject option) =
    
    let mutable refCount = 1
    let id = RenderObjectId.New()

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

    new(ctx : Context, renderPass : RenderPass) =
        new PreparedCommand(ctx, renderPass, None)
        
    abstract member GetResources : unit -> seq<IResource>
    abstract member Release : unit -> unit
    abstract member Compile : info : CompilerInfo * stream : ICommandStream * prev : Option<PreparedCommand> -> NativeStats
    abstract member EntryState : Option<PreparedPipelineState>
    abstract member ExitState : Option<PreparedPipelineState>
    abstract member Signature : IFramebufferSignature option

    member x.IsCompatibleWith(signature: IFramebufferSignature) =
        match x.Signature with
        | Some s -> s.IsCompatibleWith signature
        | _ -> true
    
    member x.AddCleanup(clean : unit -> unit) =
        cleanup <- clean :: cleanup

    member x.Id = id
    member x.Pass = renderPass
    member x.Original = renderObject
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
                use contextLock = ctx.TryResourceLock

                if contextLock.Success then
                    use __ = GlobalResourceLock.lock()
                    cleanup |> List.iter (fun f -> f())
                    x.Release()
                    cleanup <- []
                    resourceStats <- None
                    resources <- None
            )

    interface IRenderObject with
        member x.AttributeScope = Ag.Scope.Root
        member x.Id = x.Id
        member x.RenderPass = renderPass

    interface IPreparedRenderObject with
        member x.Update(t,rt) = x.Update(t, rt)
        member x.Original = x.Original
        member x.Dispose() = x.Dispose()

type PreparedObjectInfo =
    {
        oContext : Context
        oOriginal : RenderObject
        oActivation : IDisposable
        oFramebufferSignature : IFramebufferSignature
        oBeginMode : IResource<GLBeginMode, GLBeginMode>
        oAttributeBuffers : IResource<Buffer>[]
        oIndexBinding : IndexBinding option
        oIsActive : IResource<bool, int>
        oDrawCallInfos : IResource<DrawCallInfoList, DrawCallInfoList>
        oIndirectBuffer : Option<IResource<GLIndirectBuffer, IndirectDrawArgs>>
        oVertexInputBinding : IResource<VertexInputBindingHandle, int>
    }

    member x.Dispose() =
        use __ = x.oContext.ResourceLock
        use __ = GlobalResourceLock.lock()

        if x.oIsActive.IsDisposed then failwith "double free"
        x.oBeginMode.Dispose()

        for b in x.oAttributeBuffers do
            b.Dispose()

        match x.oIndexBinding with
        | Some b -> b.Dispose()
        | _ -> ()

        x.oIsActive.Dispose()

        match x.oIndirectBuffer with
        | Some i -> i.Dispose()
        | None -> x.oDrawCallInfos.Dispose()

        x.oVertexInputBinding.Dispose()

        x.oActivation.Dispose()

    member x.Resources =
        seq {
            yield x.oBeginMode :> IResource

            for b in x.oAttributeBuffers do
                yield b :> _

            match x.oIndexBinding with
            | Some b -> yield b.Buffer :>_
            | _ -> ()

            yield x.oIsActive :> _

            match x.oIndirectBuffer with
            | Some i -> yield i :> _
            | None -> yield x.oDrawCallInfos :> _

            yield x.oVertexInputBinding :> _
        }

    interface IDisposable with
        member x.Dispose() = x.Dispose()

module PreparedObjectInfo =
    open TypeMeta
    open GLSLType.Interop.Patterns
    open PreparedPipelineState

    [<AutoOpen>]
    module private Utilities =

        let (|AttributeType|_|) (t : Type) =
            match t with
            | ColorOf(_, t) | VectorOf(_, t) | MatrixOf(_, t) -> Some t
            | Numeric -> Some t
            | _ -> None

        let getExpectedType (t : GLSLType) =
            try
                GLSLType.toType t
            with _ ->
                failf "unexpected vertex type: %A" t

        let getIndexType =
            let tryGetIndexType =
                LookupTable.tryLookupV [
                    typeof<byte>,   OpenGl.Enums.IndexType.UnsignedByte
                    typeof<uint16>, OpenGl.Enums.IndexType.UnsignedShort
                    typeof<uint32>, OpenGl.Enums.IndexType.UnsignedInt
                    typeof<sbyte>,  OpenGl.Enums.IndexType.UnsignedByte
                    typeof<int16>,  OpenGl.Enums.IndexType.UnsignedShort
                    typeof<int32>,  OpenGl.Enums.IndexType.UnsignedInt
                ]

            fun t ->
                match tryGetIndexType t with
                | ValueSome it -> it
                | _ -> failf "unsupported index type '%A'" t

        let validateDoubleAttribute (name : string) (expected : Type) (input : Type) =
            match input, expected with
            | AttributeType Float64, AttributeType te when te <> typeof<float> ->
                Log.warn "[GL] attribute '%s' expects %A elements but %A elements were provided. Using double-based attribute types may result in degraded performance." name expected input
            | _ ->
                ()

    let ofRenderObject (fboSignature : IFramebufferSignature) (x : ResourceManager)
                       (iface : GLSLProgramInterface) (program : IResource<Program, int>) (rj : RenderObject) =

        let printDoubleAttributeWarning =
            match x.Context.Runtime.DebugConfig with
            | :? DebugConfig as cfg -> cfg.DoubleAttributePerformanceWarning
            | _ -> false

        // use a context token to avoid making context current/uncurrent repeatedly
        use __ = x.Context.ResourceLock
        let resources = List<IDisposable>(capacity = 8)

        try
            let activation = rj.Activate()
            resources.Add activation

            // create all requested vertex-/instance-inputs
            let attributeBindings : struct (int * AdaptiveAttribute) list =
                iface.inputs
                |> List.choose (fun v ->
                    if v.paramLocation >= 0 then
                        let semantic = Symbol.Create v.paramName
                        let expectedType = getExpectedType v.paramType

                        let attribute =
                            let view, frequency =
                                match rj.TryGetAttribute(semantic) with
                                | ValueSome (view, perInstance) ->
                                    view, (if perInstance then AttributeFrequency.PerInstances 1 else AttributeFrequency.PerVertex)
                                | _ ->
                                    failf "could not get attribute '%s'" v.paramName

                            // Check if we got double data even though we don't need it
                            if printDoubleAttributeWarning then
                                (expectedType, view.ElementType) ||> validateDoubleAttribute v.paramName

                            // Treat integral values as normalized or scaled if accessed as floating point
                            let format =
                                match view.ElementType, expectedType with
                                | AttributeType Integral, AttributeType (Float32 | Float64) ->
                                    if view.Normalized then
                                        VertexAttributeFormat.Normalized
                                    else
                                        VertexAttributeFormat.Scaled
                                | _ ->
                                    VertexAttributeFormat.Default

                            match view.Buffer with
                            | :? ISingleValueBuffer as b ->
                                AdaptiveAttribute.Value (b.Value, format)

                            | _ ->
                                let resource = x.CreateVertexBuffer(semantic, view.Buffer) |> addResource resources

                                let buffer = {
                                    Type = view.ElementType
                                    Frequency = frequency
                                    Format = format
                                    Stride = view.Stride
                                    Offset = view.Offset
                                    Resource = resource
                                }

                                AdaptiveAttribute.Buffer buffer


                        Some (v.paramLocation, attribute)
                    else
                        None
                )

            let attributeBuffers =
                attributeBindings |> List.choose (fun struct (_, attr) ->
                    match attr with
                    | AdaptiveAttribute.Buffer b -> Some b.Resource
                    | _ -> None
                )
                |> List.toArray

            GL.Check "[Prepare] Buffers"

            // create the index buffer (if present)
            let index =
                match rj.Indices with
                | Some view ->
                    Some {
                        IndexType = getIndexType view.ElementType
                        Buffer    = x.CreateIndexBuffer view.Buffer |> addResource resources
                    }

                | None -> None

            GL.Check "[Prepare] Indices"

            let indirect =
                match rj.DrawCalls with
                | Indirect indir ->
                    let buffer = x.CreateIndirectBuffer(Option.isSome rj.Indices, indir) |> addResource resources
                    Some buffer

                | _ ->
                    None

            GL.Check "[Prepare] Indirect Buffer"

            // create the VertexArrayObject
            let vibh = x.CreateVertexInputBinding(List.toArray attributeBindings, index) |> addResource resources
            GL.Check "[Prepare] VAO"

            let isActive = x.CreateIsActive rj.IsActive |> addResource resources
            let beginMode = x.CreateBeginMode(program.Handle, rj.Mode) |> addResource resources

            let drawCalls =
                match rj.DrawCalls with
                | Direct dir -> x.CreateDrawCallInfoList dir |> addResource resources
                | _ -> Unchecked.defaultof<_>

            {
                oContext = x.Context
                oOriginal = rj
                oActivation = activation
                oFramebufferSignature = fboSignature
                oAttributeBuffers = attributeBuffers
                oIndexBinding = index
                oIndirectBuffer = indirect
                oVertexInputBinding = vibh
                oIsActive = isActive
                oBeginMode = beginMode
                oDrawCallInfos = drawCalls
            }

        with _ ->
            for r in resources do r.Dispose()
            reraise()

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
                match me.oIndexBinding with
                | Some b ->
                    x.DrawElementsIndirect(s.runtimeStats, isActive, beginMode, int b.IndexType, indirect)
                | None ->
                    x.DrawArraysIndirect(s.runtimeStats, isActive, beginMode, indirect)

            | None ->
                match me.oIndexBinding with
                | Some b ->
                    x.DrawElements(s.runtimeStats, isActive, beginMode, int b.IndexType, me.oDrawCallInfos)
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
                match me.oIndexBinding with
                | Some b ->
                    x.DrawElementsIndirect(s.runtimeStats, isActive, beginMode, int b.IndexType, indirect)
                | None ->
                    x.DrawArraysIndirect(s.runtimeStats, isActive, beginMode, indirect)

            | None ->
                match me.oIndexBinding with
                | Some b ->
                    x.DrawElements(s.runtimeStats, isActive, beginMode, int b.IndexType, me.oDrawCallInfos)
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
        stream.SetColorMask(true, true, true, true)
        stream.UseProgram(0)
        stream.BindBuffer(OpenTK.Graphics.OpenGL4.BufferTarget.DrawIndirectBuffer, 0)
        for i in 0 .. 7 do
            stream.Disable(int OpenTK.Graphics.OpenGL4.EnableCap.ClipDistance0 + i)
        NativeStats(InstructionCount = 13)

    override x.EntryState = None
    override x.ExitState = None
    override x.Signature = None

type NopCommand(ctx : Context, pass : RenderPass) =
    inherit PreparedCommand(ctx, pass) 

    override x.GetResources() = Seq.empty
    override x.Release() = ()
    override x.Compile(_,_,_) = NativeStats.Zero
    override x.EntryState = None
    override x.ExitState = None
    override x.Signature = None

type PreparedObjectCommand(state : PreparedPipelineState, info : PreparedObjectInfo, renderPass : RenderPass) =
    inherit PreparedCommand(state.pContext, renderPass, Some info.oOriginal)

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
    override x.Signature = Some state.pFramebufferSignature

type MultiCommand(ctx : Context, cmds : list<PreparedCommand>, renderPass : RenderPass) =
    inherit PreparedCommand(ctx, renderPass)

    let signature = cmds |> List.tryPick _.Signature
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
    override x.Signature = signature


module PreparedCommand =
    open PreparedPipelineState

    let ofRenderObject (fboSignature : IFramebufferSignature) (x : ResourceManager) (o : IRenderObject) =

        let rec ofRenderObject (owned : bool) (fboSignature : IFramebufferSignature) (x : ResourceManager) (o : IRenderObject) =
            let pass = o.RenderPass
            match o with
            | :? HookedRenderObject as o ->
                try
                    ofRenderObject owned fboSignature x o.Hooked
                with _ ->
                    if o.IsModified then
                        Log.warn $"[GL] Failed to prepare hooked render object, falling back to original"
                        ofRenderObject owned fboSignature x o.Original
                    else
                        reraise()

            | :? RenderObject as o ->
                let resources = List<IDisposable>(capacity = 2)

                try
                    let state = PreparedPipelineState.ofRenderObject fboSignature x o |> addResource resources
                    let info = PreparedObjectInfo.ofRenderObject fboSignature x state.pProgramInterface state.pProgram o |> addResource resources
                    new PreparedObjectCommand(state, info, pass) :> PreparedCommand
                with _ ->
                    for r in resources do r.Dispose()
                    reraise()

            | :? MultiRenderObject as o ->
                match o.Children with
                | [] ->
                    new NopCommand(x.Context, pass) :> PreparedCommand
                | [o] ->
                    ofRenderObject owned fboSignature x o

                | l ->
                    new MultiCommand(x.Context, l |> List.map (ofRenderObject owned fboSignature x), pass) :> PreparedCommand

            | :? PreparedCommand as cmd ->
                if not <| cmd.IsCompatibleWith fboSignature then
                    failf $"Prepared command has framebuffer signature\n\n{cmd.Signature.Value.Layout}\n\nbut expected\n\n{fboSignature.Layout}"

                if not owned then cmd.AddReference()
                cmd

            | :? ILodRenderObject as o ->
                match fboSignature.Runtime with
                | :? ILodRuntime as r ->
                    o.Prepare(r, fboSignature) |> ofRenderObject true fboSignature x
                | _ ->
                    failwithf "expected ILodRuntime for object: %A" o

            | :? CommandRenderObject when not RuntimeConfig.UseNewRenderTask ->
                failwith "[GL] Render commands only supported with RuntimeConfig.UseNewRenderTask = true"

            | _ ->
                failwithf "bad object: %A" o

        ofRenderObject false fboSignature x o


module rec Command =

    [<AbstractClass>]
    type Command() =
        inherit AdaptiveObject()

        let mutable next : option<Command> = None
        let mutable prev : option<Command> = None

        let mutable program : option<FragmentProgram> = None

        abstract member Free : CompilerInfo -> unit
        abstract member PerformUpdate : token : AdaptiveToken * program : FragmentProgram * info : CompilerInfo -> unit

        abstract member PrevChanged : unit -> unit
        default x.PrevChanged() = ()

        member x.Update(token : AdaptiveToken, info : CompilerInfo) =
            x.EvaluateIfNeeded token () (fun token ->
                let p = 
                    match program with
                    | Some p -> p
                    | None -> 
                        let p = new FragmentProgram()
                        program <- Some p
                        p

                p.Update(token)
                x.PerformUpdate(token, p, info)
            )

        member x.Run() =
            match program with
            | Some p -> p.Run()
            | None -> ()

            match next with
            | Some n -> n.Run()
            | None -> ()

        member x.Prev
            with get() = prev
            and set p = 
                prev <- p
                x.PrevChanged()

        member x.Next
            with get() = next
            and set p = next <- p

        member x.Program = program

        interface ILinked<Command> with
            member x.Prev
                with get() = x.Prev
                and set p = x.Prev <- p
            
            member x.Next
                with get() = x.Next
                and set p = x.Next <- p

    type SingleObjectCommand (dirty : System.Collections.Generic.HashSet<SingleObjectCommand>, signature : IFramebufferSignature, manager : ResourceManager, o : IRenderObject, debug : bool) =
        let mutable fragment : option<ProgramFragment> = None
        let mutable o = o
        let mutable prepared = None

        let mutable prev : option<SingleObjectCommand> = None
        let mutable next : option<SingleObjectCommand> = None

        let compile (info : CompilerInfo) (s : IAssemblerStream) (p : IAdaptivePinning) =
            let cmd = 
                match prepared with
                | None ->
                    let cmd = PreparedCommand.ofRenderObject signature manager o
                    for r in cmd.Resources do
                        info.resources.Add r
                    prepared <- Some cmd
                    cmd
                | Some cmd -> 
                    cmd

            let pp = prev |> Option.bind (fun p -> p.PreparedCommand)
            let cs = s |> CommandStream.create debug
            cmd.Compile(info, cs, pp) |> ignore

        member private x.PreparedCommand = prepared
        member internal x.Fragment = fragment

        member x.Prev
            with get() = prev
            and set p =
                prev <- p
                dirty.Add x |> ignore

        member x.Next
            with get() = next
            and set n =
                next <- n
                match n with
                | Some n -> n.Prev <- Some x
                | None -> ()

                match fragment with
                | Some f ->     
                    match n with
                    | Some n -> f.Next <- n.Fragment
                    | None -> f.Next <- None
                | None -> 
                    ()


        member x.Free(info : CompilerInfo) =
            match prepared with
            | Some prep ->
                for r in prep.Resources do info.resources.Remove r
                prep.Dispose()
                prepared <- None
            | None ->
                ()

            match fragment with
            | Some f -> 
                f.Dispose()
                fragment <- None
            | None -> ()

            match prev, next with
            | Some p, _     -> p.Next <- next
            | None, Some n  -> n.Prev <- None
            | _ -> ()

            prev <- None
            next <- None

        member x.Compile(info : CompilerInfo, p : FragmentProgram) =
            let fragment = 
                match fragment with
                    | Some f -> 
                        f.Mutate (compile info)
                        f
                    | None ->
                        let f = p.NewFragment (compile info)
                        fragment <- Some f
                        f
            
            match prev with
            | Some p -> 
                match p.Fragment with
                | Some pf -> pf.Next <- Some fragment
                | None -> ()
            | None -> 
                p.First <- Some fragment

            match next with
            | Some n -> fragment.Next <- n.Fragment
            | None -> fragment.Next <- None

        interface ILinked<SingleObjectCommand> with
            member x.Next
                with get() = x.Next
                and set n = x.Next <- n
            member x.Prev
                with get() = x.Prev
                and set p = x.Prev <- p

    type ManyObjectsCommand(signature : IFramebufferSignature, manager : ResourceManager, o : aset<IRenderObject>, debug : bool) =
        inherit Command()

        let dirty = System.Collections.Generic.HashSet<SingleObjectCommand>()
        let trie = 
            OrderMaintenanceTrie<obj, SingleObjectCommand> (fun level ->
                match level with
                | 0 -> 
                    Some { new System.Collections.Generic.IComparer<obj> with member x.Compare(l, r) = compare (unbox<RenderPass> l) (unbox<RenderPass> r) }
                | _ ->
                    None
            )
        let mutable reader = o.GetReader()

        let cache = Dict<list<obj>, SingleObjectCommand>()

        // We cannot use null as key, so we use some obj() in case
        // there is no surface in an IRenderObject
        static let nullSurf = obj()

        let rec surface (o : IRenderObject) =
            match o with
            | :? RenderObject as o -> o.Surface :> obj
            | :? MultiRenderObject as o -> 
                match List.tryHead o.Children with
                | Some o -> surface o
                | None -> nullSurf
            | :? IPreparedRenderObject as o ->
                match o.Original with
                | Some o -> surface o
                | None -> nullSurf
            | :? CommandRenderObject as o ->
                nullSurf
            | _ ->
                nullSurf

        let key (o : IRenderObject) =
            [
                o.RenderPass :> obj
                surface o
                o.Id :> obj
            ]

        static let compileEpilog (program : FragmentProgram) (info : CompilerInfo) =
            program.NewFragment (fun s p ->
                let stream = AssemblerCommandStream s
                stream.SetDepthMask(true)
                stream.SetStencilMask(true)
                stream.SetColorMask(true, true, true, true)
                stream.UseProgram(0)
                stream.BindBuffer(OpenTK.Graphics.OpenGL4.BufferTarget.DrawIndirectBuffer, 0)
                for i in 0 .. 7 do
                    stream.Disable(int OpenTK.Graphics.OpenGL4.EnableCap.ClipDistance0 + i)

            )

        let mutable epilog : option<ProgramFragment> = None

        override x.PerformUpdate(token, program, info) =
            let ops = reader.GetChanges token
            let removes = System.Collections.Generic.List<IRenderObject>(ops.Count)

            
            let epilog =
                match epilog with
                | Some e -> e
                | None ->
                    let e = compileEpilog program info
                    epilog <- Some e
                    e


            for o in ops do
                match o with
                | Rem _ ->
                    // delay removes s.t. resources may survive reordering
                    removes.Add o.Value

                | Add(_, v) ->
                    let key = key o.Value
                    let ref = 
                        trie.AddOrUpdate(key, fun o ->
                            let cmd = new SingleObjectCommand(dirty, signature, manager, v, debug)
                            // new command needs to be compiled immediately, otherwise fragment pointers cannot be updated
                            cmd.Compile(info, program)
                            cache.[key] <- cmd
                            cmd
                        )

                    match ref.Prev with
                    | ValueSome p -> p.Value.Next <- Some ref.Value
                    | ValueNone -> program.First <- ref.Value.Fragment

                    match ref.Next with
                    | ValueSome n -> ref.Value.Next <- Some n.Value
                    | ValueNone -> 
                        match ref.Value.Fragment with
                        | Some f -> f.Next <- Some epilog  
                        | None -> ()


            for v in removes do
                let key = key v
                match cache.TryRemove key with
                | (true, cmd) ->
                    dirty.Remove cmd |> ignore

                    match trie.TryRemove key with
                    | ValueSome (l, r) ->
                        match l with
                        | ValueSome l ->    
                            match r with
                            | ValueSome r -> l.Value.Next <- Some r.Value
                            | ValueNone -> 
                                match l.Value.Fragment with
                                | Some f -> f.Next <- Some epilog
                                | None -> ()
                        | ValueNone ->
                            match r with
                            | ValueSome r -> program.First <- r.Value.Fragment
                            | ValueNone -> program.First <- None
                    | ValueNone ->
                        ()

                    cmd.Free(info)
                | _ ->
                    ()
                
            for d in dirty do
                d.Compile(info, program)

            dirty.Clear()

            program.First <- 
                match trie.First with
                | ValueSome f -> f.Value.Fragment
                | ValueNone -> None

            match trie.Last with
            | ValueSome l -> 
                match l.Value.Fragment with
                | Some f -> f.Next <- Some epilog
                | None -> ()
            | ValueNone ->
                epilog.Prev <- None


        override x.Free(info : CompilerInfo) =
            // Epilog is not visible in the linked list at the SingleObjectCommand level.
            // Thus, disposing those does not fix the prev pointer of the epilog, leaving it
            // invalid -> exception when trying to dispose epilog afterwards
            match epilog with
            | Some e -> 
                e.Dispose()
                epilog <- None
            | None ->
                ()

            for cmd in cache.Values do
                cmd.Free(info)

            cache.Clear()
            trie.Clear()
            dirty.Clear()
            reader <- Unchecked.defaultof<_>

    type OrderedCommand(o : alist<Command>) =
        inherit Command()

        let reader = o.GetReader()

        let mutable state : IndexList<ProgramFragment * Command> = IndexList.empty

        let dirty = LockedSet<Command>()

        let isValid (cmd : Command) =
            state |> IndexList.exists (fun _ -> snd >> (=) cmd)

        override x.InputChangedObject(_ : obj, input : IAdaptiveObject) =
            match input with
            | :? Command as cmd -> dirty.Add cmd |> ignore
            | _ -> ()

        override x.PerformUpdate(token : AdaptiveToken, program : FragmentProgram, info : CompilerInfo) =
            let ops = reader.GetChanges token

            for i, op in ops do
                let (l, s , r)  = IndexList.neighbours i state
                match op with
                | Set cmd ->
                    cmd.Update(token, info)

                    let fragment =
                        match s with
                        | Some (fragment, old) ->
                            old.Free info
                            old.Outputs.Remove x |> ignore
                            dirty.Remove old |> ignore

                            fragment.Mutate(fun s p ->
                                s.Call(cmd.Program.Value, p)
                            )
                            fragment

                        | None ->
                            let fragment =
                                program.NewFragment (fun s p ->
                                    s.Call(cmd.Program.Value, p)
                                )

                            match r with
                            | Some (_, (r, _)) -> fragment.Next <- Some r
                            | None -> fragment.Next <- None

                            match l with
                            | Some (_, (lf,_)) -> lf.Next <- Some fragment
                            | None -> program.First <- Some fragment

                            fragment

                    state <- IndexList.set i (fragment, cmd) state

                | Remove ->
                    match s with
                    | Some (oldFragment, oldCmd) ->
                        oldCmd.Free info
                        oldCmd.Outputs.Remove x |> ignore
                        dirty.Remove oldCmd |> ignore
                        oldFragment.Dispose()

                    | None ->
                        ()

                    state <- IndexList.remove i state

            // Update all dirty commands
            // Note: In concurrent situations, the dirty set may contain already removed commands.
            // We have to check if the dirty command is actually still valid before updating it.
            //
            // Example with threads A and B:
            // A: Commits transaction, invokes InputChangedObject(_, cmd)
            // B: Removes and frees cmd
            // A: Adds cmd to dirty set
            // B: Retrieves cmd from dirty set and updates it -> fail
            for cmd in dirty.GetAndClear() do
                if isValid cmd then cmd.Update(token, info)

        override x.Free(info : CompilerInfo) =
            for (f, cmd) in state do
                cmd.Free info
                f.Dispose()

            state <- IndexList.empty
            dirty.Clear()

    type ClearCommand(signature : IFramebufferSignature, values : aval<ClearValues>) =
        inherit Command()

        let mutable fragment : option<ProgramFragment> = None

        let compile (info : CompilerInfo) (values : ClearValues) (s : IAssemblerStream) (p : IAdaptivePinning) =
            
            for KeyValue(i, att) in signature.ColorAttachments do
                match values.[att.Name] with
                | Some color ->
                    s.BeginCall(3)
                    
                    if att.Format.IsIntegerFormat then
                        let pValues = p.Pin (AVal.constant color.Integer)
                        s.PushArg(NativePtr.toNativeInt pValues)
                        s.PushArg(i)
                        s.PushArg(int ClearBuffer.Color)
                        s.Call(OpenGl.Pointers.ClearBufferiv)
                    else
                        let pValues = p.Pin (AVal.constant color.Float)
                        s.PushArg(NativePtr.toNativeInt pValues)
                        s.PushArg(i)
                        s.PushArg(int ClearBuffer.Color)
                        s.Call(OpenGl.Pointers.ClearBufferfv)  
                        
                | _ -> ()
            
            let mutable flags = ClearBufferMask.None

            match values.Depth with
            | Some depth ->
                let pDepth = p.Pin (AVal.constant (float depth))
                flags <- flags ||| ClearBufferMask.DepthBufferBit
                s.BeginCall(1)
                s.PushDoubleArg (NativePtr.toNativeInt pDepth)
                s.Call(OpenGl.Pointers.ClearDepth)
            | None ->
                ()

            match values.Stencil with
            | Some stencil ->
                flags <- flags ||| ClearBufferMask.StencilBufferBit
                s.BeginCall(1)
                s.PushArg (int stencil)
                s.Call(OpenGl.Pointers.ClearStencil)
            | None ->
                ()

            s.BeginCall(1)
            s.PushArg (int flags)
            s.Call(OpenGl.Pointers.Clear)

        override x.PerformUpdate(token : AdaptiveToken, program : FragmentProgram, info : CompilerInfo) =
            let values = values.GetValue token

            match fragment with
            | Some f ->
                f.Mutate (compile info values)
            | None ->
                let f = program.NewFragment (compile info values)
                fragment <- Some f
                program.First <- Some f

            program.Update(token)

        override x.Free(info : CompilerInfo) =
            match fragment with
            | Some f -> 
                f.Dispose()
                fragment <- None
            | None ->
                ()

    type IfThenElseCommand(condition : aval<bool>, ifTrue : Command, ifFalse : Command) =
        inherit Command()

        let condition = AVal.map (function true -> 1 | false -> 0) condition
        let mutable fragment : option<ProgramFragment> = None


        override x.PerformUpdate(token : AdaptiveToken, program : FragmentProgram, info : CompilerInfo) =
            ifTrue.Update(token, info)
            ifFalse.Update(token, info)

            match fragment with
            | None ->
                let f = 
                    program.NewFragment(fun s p ->
                        let f = s.NewLabel()
                        let e = s.NewLabel()

                        let pCond = p.Pin condition
                        s.Cmp(NativePtr.toNativeInt pCond, 0)
                        s.Jump(JumpCondition.Equal, f)
                        s.Call(ifTrue.Program.Value, p)
                        s.Jump(e)

                        s.Mark(f)
                        s.Call(ifFalse.Program.Value, p)

                        s.Mark(e)
                        
                    )
                program.First <- Some f
                fragment <- Some f

            | Some _ ->
                ()

            program.Update(token)

        override x.Free(info : CompilerInfo) =
            match fragment with
            | Some f -> f.Dispose()
            | None -> ()
            ifTrue.Free info
            ifFalse.Free info

    type NopCommand private() =
        inherit Command()

        static let instance = NopCommand() :> Command

        static member Instance = instance

        override x.PerformUpdate(_,_,_) = 
            ()

        override x.Free(_) =
            ()

    let rec ofRuntimeCommand (fboSignature : IFramebufferSignature) (manager : ResourceManager) (debug : bool) (cmd : RuntimeCommand) =
        match cmd with
        | RuntimeCommand.EmptyCmd ->
            NopCommand.Instance

        | RuntimeCommand.RenderCmd objects ->
            ManyObjectsCommand(fboSignature, manager, objects, debug)
            :> Command

        | RuntimeCommand.OrderedCmd commands ->
            commands 
            |> AList.map (ofRuntimeCommand fboSignature manager debug)
            |> OrderedCommand
            :> Command

        | RuntimeCommand.ClearCmd values ->
            ClearCommand(fboSignature, values)
            :> Command

        | RuntimeCommand.IfThenElseCmd(cond, ifTrue, ifFalse) ->
            let ifTrue = ofRuntimeCommand fboSignature manager debug ifTrue
            let ifFalse = ofRuntimeCommand fboSignature manager debug ifFalse
            IfThenElseCommand(cond, ifTrue, ifFalse)
            :> Command

        | RuntimeCommand.GeometriesCmd _
        | RuntimeCommand.GeometriesSimpleCmd _
        | RuntimeCommand.DispatchCmd _
        | RuntimeCommand.LodTreeCmd _ ->
            failwith "not implemented"

    let ofRenderObjects (fboSignature : IFramebufferSignature) (manager : ResourceManager) (debug : bool) (objects : aset<IRenderObject>) =
        let special = 
            objects 
            |> ASet.choose (function :? CommandRenderObject as o -> Some o.Command | _ -> None)
            |> ASet.toAList

        let simple =
            objects 
            |> ASet.choose (function :? CommandRenderObject -> None | o -> Some o)

        let simpleCount =
            if simple.IsConstant then ASet.force simple |> HashSet.count |> ValueSome
            else ValueNone
            
        let specialCount =
            if special.IsConstant then AList.force special |> IndexList.count |> ValueSome
            else ValueNone

        match struct (simpleCount, specialCount) with
        | struct(ValueSome 0, ValueSome 0) ->
            NopCommand.Instance

        | struct(ValueSome 0, ValueSome 1) ->
            special |> AList.force |> Seq.head |> ofRuntimeCommand fboSignature manager debug

        | struct(ValueSome 0, _) ->
            RuntimeCommand.OrderedCmd special |> ofRuntimeCommand fboSignature manager debug

        | struct(_, ValueSome 0) ->
            RuntimeCommand.RenderCmd simple |> ofRuntimeCommand fboSignature manager debug

        | _ ->
            AList.append (AList.single (RuntimeCommand.RenderCmd simple)) special
            |> RuntimeCommand.OrderedCmd
            |> ofRuntimeCommand fboSignature manager debug