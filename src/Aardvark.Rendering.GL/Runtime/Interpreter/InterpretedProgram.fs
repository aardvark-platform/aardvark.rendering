namespace Aardvark.Rendering.GL

open System
open System.Collections.Generic
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4

type GLDepthTestMode = { IsEnabled : bool; DepthTest : OpenGl.Enums.CompareFunction }
type GLCullMode = { IsEnabled : bool; CullFace : OpenGl.Enums.Face }
type GLBlendMode = { IsEnabled : bool; Source : OpenGl.Enums.BlendFactor; Dest : OpenGl.Enums.BlendFactor; SourceAlpha : OpenGl.Enums.BlendFactor; DestAlpha : OpenGl.Enums.BlendFactor; Op : OpenGl.Enums.BlendOperation; OpAlpha : OpenGl.Enums.BlendOperation }
type GLFillMode = OpenGl.Enums.PolygonMode
type GLStencilMode = { 
    IsEnabled : bool; 
    CompareFront : OpenGl.Enums.CompareFunction; 
    CompareBack : OpenGl.Enums.CompareFunction; 

    FrontRef : int
    FrontMask : int

    BackRef : int
    BackMask : int

    OpFrontSF : OpenGl.Enums.StencilOperation; 
    OpFrontDF : OpenGl.Enums.StencilOperation; 
    OpFrontDP : OpenGl.Enums.StencilOperation; 

    OpBackSF : OpenGl.Enums.StencilOperation; 
    OpBackDF : OpenGl.Enums.StencilOperation; 
    OpBackDP : OpenGl.Enums.StencilOperation;  
}

type CompiledRenderJob = {
    id : int
    attributeScope : obj

    isActive : IMod<bool>
    renderPass : IMod<uint64>

    drawCallInfo : IMod<DrawCallInfo>
    surface : ChangeableResource<Program>

    depthTest : IMod<GLDepthTestMode>
    cullMode : IMod<GLCullMode>
    blendMode : IMod<GLBlendMode>
    fillMode : IMod<GLFillMode>
    stencilMode : IMod<GLStencilMode>

    indexBuffer : IMod<Array>
    vertexArray : ChangeableResource<VertexArrayObject>
    uniformBuffers : list<int * ChangeableResource<UniformBuffer>>
    textures : list<int * OpenGl.Enums.TextureTarget * ChangeableResource<Texture> * ChangeableResource<Sampler>>
    resources : HashSet<IChangeableResource>

} with
    member x.Dispose() =
        for r in x.resources do
            r.Dispose()
        x.resources.Clear()

type InterpretedProgram(manager : ResourceManager, add : IAdaptiveObject -> unit, remove : IAdaptiveObject -> unit)  =
    inherit AdaptiveObject()

    let viewCache = System.Collections.Concurrent.ConcurrentDictionary<BufferView * ChangeableResource<Aardvark.Rendering.GL.Buffer>, IMod<AttributeDescription>>()

    let (=?) (a : Option<'a>) (b : 'a) =
        match a with
            | Some a when a = b -> true
            | _ -> false

    let (<>?) (a : Option<'a>) (b : 'a) =
        match a with
            | Some a when a = b -> false
            | _ -> true

    let compile (rj : RenderJob) =
        let resources = HashSet<IChangeableResource>()

        let newResource a =
            resources.Add a |>  ignore
            add a
            a

        let program = newResource <| manager.CreateSurface rj.Surface
        let programHandle = program.Resource.GetValue()

        
        let bufferViews = 
            programHandle.Inputs |> List.map (fun i ->
                match rj.VertexAttributes.TryGetAttribute (Symbol.Create i.semantic) with
                    | (true, v) ->
                        let buffer = newResource <| manager.CreateBuffer(v.Buffer)

                        let view = 
                            viewCache.GetOrAdd((v,buffer), fun (v,buffer) ->
                                buffer.Resource |> Mod.map (fun b ->
                                    { Type = v.ElementType
                                      Frequency = AttributeFrequency.PerVertex
                                      Normalized = false
                                      Stride = v.Stride
                                      Offset = v.Offset
                                      Buffer = b
                                    }
                                )
                            )

                        i.attributeIndex, view
                    | _ ->
                        failwithf "attribute %A not found" i.semantic
            )

        let vao =
            if rj.Indices <> null then 
                let ibo = newResource <| manager.CreateBuffer rj.Indices
                newResource <| manager.CreateVertexArrayObject(bufferViews, ibo)
            else
                newResource <| manager.CreateVertexArrayObject bufferViews

        let uniformBuffers =
            programHandle.UniformBlocks |> List.map (fun block ->
                
                let mutable used = []
                let buffer = newResource <| manager.CreateUniformBuffer(block, rj.Surface, rj.Uniforms, &used)

                for (_,u) in used do add u

                block.binding, buffer
            )

        let textures =
            programHandle.Uniforms |> List.choose (fun u ->
                match u.uniformType with
                    | SamplerType -> 
                        
                        match rj.Uniforms.TryGetUniform (Symbol.Create u.semantic) with
                            | (true, (:? IMod<ITexture> as tex)) ->


                                let sampler =
                                    match programHandle.SamplerStates.TryGetValue (Symbol.Create u.semantic) with
                                        | (true, sampler) -> sampler
                                        | _ -> 
                                            match u.samplerState with
                                                | Some sam ->
                                                    match programHandle.SamplerStates.TryGetValue (Symbol.Create sam) with
                                                        | (true, sampler) -> sampler
                                                        | _ -> SamplerStateDescription()
                                                | None ->
                                                    SamplerStateDescription()



                                let tex = newResource <| manager.CreateTexture(tex)
                                let sampler = newResource <| manager.CreateSampler(Mod.initConstant sampler)
                                let r = tex.Resource.GetValue()
                                let target = Translations.toGLTarget r.Dimension r.IsArray r.Multisamples

                                Some (u.index, unbox<OpenGl.Enums.TextureTarget> target, tex, sampler)

                             | _ -> None

                    | _ -> None
            
            )

        add rj.DepthTest
        add rj.CullMode
        add rj.BlendMode
        add rj.FillMode
        add rj.StencilMode
        add rj.DrawCallInfo
        add rj.IsActive

        { id = rj.Id
          attributeScope = rj.AttributeScope

          isActive = rj.IsActive
          renderPass = rj.RenderPass

          drawCallInfo = rj.DrawCallInfo
          surface = program 

          depthTest = rj.DepthTest |> Mod.map (fun m -> { IsEnabled = (match m with | DepthTestMode.None -> false | _ -> true); DepthTest = unbox <| Translations.toGLComparison m })
          cullMode = rj.CullMode |> Mod.map (fun m -> { IsEnabled = (match m with | CullMode.None -> false | _ -> true); CullFace = unbox <| Translations.toGLFace m })
          blendMode = 
            rj.BlendMode |> Mod.map (fun m -> 
                { IsEnabled = m.Enabled
                  Source = m.SourceFactor |> Translations.toGLFactor |> unbox
                  Dest = m.DestinationFactor |> Translations.toGLFactor |> unbox
                  SourceAlpha = m.SourceAlphaFactor |> Translations.toGLFactor |> unbox
                  DestAlpha = m.DestinationAlphaFactor |> Translations.toGLFactor |> unbox
                  Op = m.Operation |> Translations.toGLOperation |> unbox
                  OpAlpha = m.AlphaOperation |> Translations.toGLOperation |> unbox
                }
            )

          fillMode = rj.FillMode |> Mod.map (Translations.toGLPolygonMode >> unbox)
          stencilMode = 
            rj.StencilMode |> Mod.map (fun m ->
                { IsEnabled = m.IsEnabled
                  CompareFront = m.CompareFront.Function |> Translations.toGLFunction |> unbox
                  CompareBack = m.CompareBack.Function |> Translations.toGLFunction |> unbox

                  FrontRef = m.CompareFront.Reference
                  BackRef = m.CompareBack.Reference
                  FrontMask = int m.CompareFront.Mask
                  BackMask = int m.CompareBack.Mask

                  OpFrontSF = m.OperationFront.StencilFail |> Translations.toGLStencilOperation |> unbox
                  OpFrontDF = m.OperationFront.DepthFail |> Translations.toGLStencilOperation |> unbox
                  OpFrontDP = m.OperationFront.DepthPass |> Translations.toGLStencilOperation |> unbox

                  OpBackSF = m.OperationBack.StencilFail |> Translations.toGLStencilOperation |> unbox
                  OpBackDF = m.OperationBack.DepthFail |> Translations.toGLStencilOperation |> unbox
                  OpBackDP = m.OperationBack.DepthPass |> Translations.toGLStencilOperation |> unbox
                }
            )

          indexBuffer = rj.Indices
          vertexArray = vao
          uniformBuffers = uniformBuffers
          textures = textures

          resources = resources
        }

    let compileCache = Cache(Ag.getContext(), compile)
    let content = HashSet<CompiledRenderJob>()


    let setDepthTest (mode : GLDepthTestMode) =
        if mode.IsEnabled then
            OpenGl.Unsafe.Enable (int OpenGl.Enums.State.DepthTest)
            OpenGl.Unsafe.DepthFunc (int mode.DepthTest)
        else
            OpenGl.Unsafe.Disable (int OpenGl.Enums.State.DepthTest)

    let setFillMode (mode : GLFillMode) =
        OpenGl.Unsafe.PolygonMode (int OpenGl.Enums.Face.FrontAndBack) (int mode)

    let setCullMode (mode : GLCullMode) =
        if mode.IsEnabled then
            OpenGl.Unsafe.Enable (int OpenGl.Enums.State.CullFace)
            OpenGl.Unsafe.CullFace (int mode.CullFace)
        else
            OpenGl.Unsafe.Disable (int OpenGl.Enums.State.CullFace)

    let setBlendMode (mode : GLBlendMode) =
        if mode.IsEnabled then
            OpenGl.Unsafe.Enable (int OpenGl.Enums.State.Blend)
            OpenGl.Unsafe.BlendFuncSeparate (int mode.Source) (int mode.Dest) (int mode.SourceAlpha) (int mode.DestAlpha)
            OpenGl.Unsafe.BlendEquationSeparate (int mode.Op) (int mode.OpAlpha)
        else
            OpenGl.Unsafe.Disable (int OpenGl.Enums.State.Blend)

    let setStencilMode (mode : GLStencilMode) =
        if mode.IsEnabled then
            OpenGl.Unsafe.Enable (int OpenGl.Enums.State.StencilTest)
            OpenGl.Unsafe.StencilFuncSeparate (int OpenGl.Enums.Face.Front) (int mode.CompareFront) mode.FrontRef mode.FrontMask
            OpenGl.Unsafe.StencilFuncSeparate (int OpenGl.Enums.Face.Back) (int mode.CompareBack) mode.BackRef mode.BackMask
            OpenGl.Unsafe.StencilOpSeparate (int OpenGl.Enums.Face.Front) (int mode.OpFrontSF) (int mode.OpFrontDF) (int mode.OpFrontDP)
            OpenGl.Unsafe.StencilOpSeparate (int OpenGl.Enums.Face.Back) (int mode.OpBackSF) (int mode.OpBackDF) (int mode.OpBackDP)
        else
            OpenGl.Unsafe.Disable (int OpenGl.Enums.State.StencilTest)

    interface IProgram with
        member x.Add rj = 
            content.Add (compileCache.Invoke rj) |> ignore

        member x.Remove rj =
            let cc = compileCache.Revoke rj
            content.Remove cc |> ignore
            for r in cc.resources do
                remove r
            cc.Dispose()
            

        member x.Update rj = ()

        member x.Run(fbo,ctx) =
            let mutable currentProgram = None
            let mutable currentVAO = None
            let mutable currentDepthTest = None
            let mutable currentFillMode = None
            let mutable currentCullMode = None
            let mutable currentBlendMode = None
            let mutable currentStencilMode = None
            let mutable currentUniformBuffers = Map.empty
            let mutable currentTextures = Map.empty

            for c in content do
                let isActive = c.isActive.GetValue()

                if isActive then
                    for r in c.resources do 
                        if r.OutOfDate then
                            r.UpdateCPU()
                            r.UpdateGPU()
     
                    // set all modes if needed
                    let depthTest = c.depthTest.GetValue()
                    if currentDepthTest <>? depthTest then
                        setDepthTest depthTest
                        currentDepthTest <- Some depthTest

                    let fillMode = c.fillMode.GetValue()
                    if currentFillMode <>? fillMode then
                        setFillMode fillMode
                        currentFillMode <- Some fillMode

                    let cullMode = c.cullMode.GetValue()
                    if currentCullMode <>? cullMode then
                        setCullMode cullMode
                        currentCullMode <- Some cullMode

                    let blendMode = c.blendMode.GetValue()
                    if currentBlendMode <>? blendMode then
                        setBlendMode blendMode
                        currentBlendMode <- Some blendMode

                    let stencilMode = c.stencilMode.GetValue()
                    if currentStencilMode <>? stencilMode then
                        setStencilMode stencilMode
                        currentStencilMode <- Some stencilMode
                        

                    // bind the program
                    let program = c.surface.Resource.GetValue()
                    if currentProgram <>? program then
                        OpenGl.Unsafe.BindProgram(program.Handle)
                        GL.Check "could not bind program"

                        currentProgram <- Some program

                    // bind all needed uniform-buffers
                    for (index, buffer) in c.uniformBuffers do
                        let buffer = buffer.Resource.GetValue()

                        match Map.tryFind index currentUniformBuffers with
                            | Some b when b = buffer -> ()
                            | _ -> 
                                OpenGl.Unsafe.BindBufferRange (int OpenGl.Enums.BufferTarget.UniformBuffer) index buffer.Handle 0n (nativeint buffer.Size)
                                GL.Check "could not bind uniform buffer"
                                currentUniformBuffers <- Map.add index buffer currentUniformBuffers

                    // bind all textures/samplers
                    for (index, target, texture, sampler) in c.textures do
                        let texture = texture.Resource.GetValue()
                        let sampler = sampler.Resource.GetValue()

                        match Map.tryFind index currentTextures with
                            | Some t when t = texture -> ()
                            | _ -> 
                                OpenGl.Unsafe.ActiveTexture (int OpenGl.Enums.TextureUnit.Texture0 + index)
                                OpenGl.Unsafe.BindTexture (int target) texture.Handle
                                GL.Check "could not bind texture"
                                OpenGl.Unsafe.BindSampler index sampler.Handle
                                GL.Check "could not bind sampler"
                                currentTextures <- Map.add index texture currentTextures

                    // bind the VAO
                    let vao = c.vertexArray.Resource.GetValue()
                    if currentVAO <>? vao then
                        OpenGl.Unsafe.BindVertexArray(vao.Handle)
                        GL.Check "could not bind vertex array"
                        currentVAO <- Some vao

                    // draw
                    let drawInfo = c.drawCallInfo.GetValue()
                    let mode = int (Translations.toGLMode drawInfo.Mode)

                    if c.indexBuffer <> null then
                        let indexType = c.indexBuffer.GetValue().GetType().GetElementType()
                        let indexType =
                            if indexType = typeof<byte> then int OpenGl.Enums.IndexType.UnsignedByte
                            elif indexType = typeof<uint16> then int OpenGl.Enums.IndexType.UnsignedShort
                            elif indexType = typeof<uint32> then int OpenGl.Enums.IndexType.UnsignedInt
                            elif indexType = typeof<sbyte> then int OpenGl.Enums.IndexType.UnsignedByte
                            elif indexType = typeof<int16> then int OpenGl.Enums.IndexType.UnsignedShort
                            elif indexType = typeof<int32> then int OpenGl.Enums.IndexType.UnsignedInt
                            else failwithf "unsupported index type: %A"  indexType

                        match drawInfo.InstanceCount with
                            | 1 -> OpenGl.Unsafe.DrawElements mode drawInfo.FaceVertexCount indexType 0n
                            | n -> OpenGl.Unsafe.DrawElementsInstanced mode drawInfo.FaceVertexCount indexType 0n n

                    else
                        match drawInfo.InstanceCount with
                            | 1 -> OpenGl.Unsafe.DrawArrays mode 0 drawInfo.FaceVertexCount
                            | n -> OpenGl.Unsafe.DrawArraysInstanced mode 0 drawInfo.FaceVertexCount n
                    GL.Check "could not draw"

            FrameStatistics.Zero
                        

        member x.Dispose() = ()


