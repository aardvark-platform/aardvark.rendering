namespace Aardvark.Rendering.GL

open Aardvark.Base.Incremental
open Aardvark.Base
open Aardvark.Base.Rendering

module Instructions =
    
    let setDepthTest (m : IMod<DepthTestMode>) =
        m |> Mod.map (fun dt ->
            if dt <> DepthTestMode.None then
                [ 
                    Instruction.Enable(int OpenGl.Enums.State.DepthTest)
                    Instruction.DepthFunc(Translations.toGLComparison dt) 
                ]
            else
                [ Instruction.Disable(int OpenGl.Enums.State.DepthTest) ]
        )

    let setFillMode (m : IMod<FillMode>) =
        m |> Mod.map (fun fm -> 
            [ Instruction.PolygonMode (int OpenGl.Enums.Face.FrontAndBack) (Translations.toGLPolygonMode fm) ]
        )

    let setCullMode (m : IMod<CullMode>) =
        m |> Mod.map (fun cm -> 
            if cm <> CullMode.None then
                [ 
                    Instruction.Enable(int OpenGl.Enums.State.CullFace)
                    Instruction.CullFace(Translations.toGLFace cm) 
                ]
            else
                [ Instruction.Disable(int OpenGl.Enums.State.CullFace) ]
        )

    let setBlendMode (m : IMod<BlendMode>) =
        m |> Mod.map (fun bm -> 
            //TODO: actually depending on the Framebuffer (premultiplied alpha)
            if bm.Enabled then
                let src = Translations.toGLFactor bm.SourceFactor
                let dst = Translations.toGLFactor bm.DestinationFactor
                let op = Translations.toGLOperation bm.Operation

                let srcA = Translations.toGLFactor bm.SourceAlphaFactor
                let dstA = Translations.toGLFactor bm.DestinationAlphaFactor
                let opA = Translations.toGLOperation bm.AlphaOperation

                [ 
                    Instruction.Enable(int OpenGl.Enums.State.Blend)
                    Instruction.BlendFuncSeparate src dst srcA dstA
                    Instruction.BlendEquationSeparate op opA
                ]
            else
                [ Instruction.Disable(int OpenGl.Enums.State.Blend) ]
        )

    let setStencilMode (m : IMod<StencilMode>) =
        m |> Mod.map (fun sm -> 
            //TODO: actually depending on the Framebuffer (premultiplied alpha)
            if sm.IsEnabled then
                let cmpFront = Translations.toGLFunction sm.CompareFront.Function
                let cmpBack= Translations.toGLFunction sm.CompareBack.Function
                let opFrontSF = Translations.toGLStencilOperation sm.OperationFront.StencilFail
                let opBackSF = Translations.toGLStencilOperation sm.OperationBack.StencilFail
                let opFrontDF = Translations.toGLStencilOperation sm.OperationFront.DepthFail
                let opBackDF = Translations.toGLStencilOperation sm.OperationBack.DepthFail
                let opFrontP = Translations.toGLStencilOperation sm.OperationFront.DepthPass
                let opBackP = Translations.toGLStencilOperation sm.OperationBack.DepthPass

                [ 
                    Instruction.Enable(int OpenGl.Enums.State.StencilTest) 
                    Instruction.StencilFuncSeparate(int OpenGl.Enums.Face.Front) cmpFront sm.CompareFront.Reference (int sm.CompareFront.Mask)
                    Instruction.StencilFuncSeparate(int OpenGl.Enums.Face.Back) cmpBack sm.CompareBack.Reference (int sm.CompareBack.Mask)
                    Instruction.StencilOpSeparate(int OpenGl.Enums.Face.Front) opFrontSF opFrontDF opFrontP
                    Instruction.StencilOpSeparate(int OpenGl.Enums.Face.Back) opBackSF opBackDF opBackP
                ]
            else
                [ Instruction.Disable(int OpenGl.Enums.State.StencilTest) ]
        )

    let bindProgram (p : ChangeableResource<Program>) =
        p.Resource |> Mod.map (fun r -> Instruction.BindProgram(r.Handle))

    let bindUniformBuffer (index : int) (u : ChangeableResource<UniformBuffer>) =   
        u.Resource |> Mod.map (fun r -> 
            //ExecutionContext.bindUniformBuffer index r
            Instruction.BindBufferRange (int OpenGl.Enums.BufferTarget.UniformBuffer) index r.Handle 0n (nativeint r.Size)
        )

    let setActiveTexture (index : int) =
        Instruction.ActiveTexture ((int OpenGl.Enums.TextureUnit.Texture0) + index)

    let bindSampler (index : int) (sampler : ChangeableResource<Sampler>) =
        sampler.Resource |> Mod.map (fun r -> [Instruction.BindSampler index r.Handle])

    let bindTexture (tex : ChangeableResource<Texture>) =
        tex.Resource |> Mod.map(fun r -> 
            let target = Translations.toGLTarget r.Dimension r.IsArray r.Multisamples
            [ Instruction.BindTexture target r.Handle ]
        )

    let bindVertexArray (vao : ChangeableResource<VertexArrayObject>) =
        fun (ctx : ContextHandle) -> Instruction.BindVertexArray(vao.Resource.GetValue().Handle)

    let draw (program : Program) (indexArray : IMod<System.Array>) (call : IMod<DrawCallInfo>) (isActive : IMod<bool>) =
        let hasTess = program.Shaders |> List.exists (fun s -> s.Stage = ShaderStage.TessControl)

        let indexType = 
            if indexArray <> null then
                indexArray |> Mod.map (fun ia -> (ia <> null, if ia <> null then ia.GetType().GetElementType() else typeof<obj>))
            else
                Mod.constant (false, typeof<obj>)

        let patchSize (mode : IndexedGeometryMode) =
            match mode with
                | IndexedGeometryMode.LineList -> 2
                | IndexedGeometryMode.PointList -> 1
                | IndexedGeometryMode.TriangleList -> 3
                | m -> failwithf "unsupported patch-mode: %A" m

        let instruction =
            adaptive {
                let! (indexed, indexType) = indexType
                let! (call, isActive) = call, isActive

                let faceVertexCount =
                    if isActive then call.FaceVertexCount
                    else 0

                let mode =
                    if hasTess then int OpenGl.Enums.DrawMode.Patches
                    else 
                        let realMode = 
                            match program.SupportedModes with
                                | Some set ->
                                    if Set.contains call.Mode set then 
                                        call.Mode
                                    elif Set.contains IndexedGeometryMode.PointList set then
                                        IndexedGeometryMode.PointList
                                    else failwith "invalid mode for program: %A (should be in: %A)" call.Mode set
                                | None -> 
                                    call.Mode

                        Translations.toGLMode realMode

                if indexed then
                    let offset = nativeint (call.FirstIndex * indexType.GLSize)

                    let indexType =
                        if indexType = typeof<byte> then int OpenGl.Enums.IndexType.UnsignedByte
                        elif indexType = typeof<uint16> then int OpenGl.Enums.IndexType.UnsignedShort
                        elif indexType = typeof<uint32> then int OpenGl.Enums.IndexType.UnsignedInt
                        elif indexType = typeof<sbyte> then int OpenGl.Enums.IndexType.UnsignedByte
                        elif indexType = typeof<int16> then int OpenGl.Enums.IndexType.UnsignedShort
                        elif indexType = typeof<int32> then int OpenGl.Enums.IndexType.UnsignedInt
                        else failwithf "unsupported index type: %A"  indexType

                    match call.InstanceCount with
                        | 1 -> return call.Mode, Instruction.DrawElements mode faceVertexCount indexType offset
                        | n -> return call.Mode, Instruction.DrawElementsInstanced mode faceVertexCount indexType offset n
                else
                    match call.InstanceCount with
                        | 1 -> return call.Mode, Instruction.DrawArrays mode call.FirstIndex faceVertexCount
                        | n -> return call.Mode, Instruction.DrawArraysInstanced mode call.FirstIndex faceVertexCount n
            }

        instruction |> Mod.map (fun (mode,i) ->
            if hasTess then
                let size = patchSize mode
                [ 
                    Instruction.PatchParameter (int OpenTK.Graphics.OpenGL4.PatchParameterInt.PatchVertices) size
                    i
                ]
            else
                [i]
        )

module Resources =
    let private createAndAddResource (f : ResourceManager -> ChangeableResource<'a>) =
        { runCompile = fun s ->
            let r = f s.manager
            { s with resources = (r:> IChangeableResource)::s.resources }, r
        }

    let createProgram surface =
        createAndAddResource (fun m -> m.CreateSurface(surface))

    let createUniformBuffer scope block surface provider =
        { runCompile = fun s ->
            let mutable values = []
            let r = s.manager.CreateUniformBuffer(scope, block, surface, provider, &values)
            {s with resources = (r:> IChangeableResource)::s.resources}, (r,values)
        }

    let createUniformLocation scope u provider =
        { runCompile = fun s ->
            let mutable values = []
            let r = s.manager.CreateUniformLocation(scope, provider, u)
            {s with resources = (r:> IChangeableResource)::s.resources}, (r,values)
        }

    let createTexture texture =
        createAndAddResource (fun m -> m.CreateTexture texture)

    let createSampler sampler =
        createAndAddResource (fun m -> m.CreateSampler sampler)

    let private viewCache = System.Collections.Concurrent.ConcurrentDictionary<BufferView * ChangeableResource<Aardvark.Rendering.GL.Buffer>, IMod<AttributeDescription>>()

    let private createView (frequency : AttributeFrequency) (m : BufferView) (b : ChangeableResource<Aardvark.Rendering.GL.Buffer>) =
        viewCache.GetOrAdd(((m,b)), (fun (m,b) ->
            b.Resource |> Mod.map (fun b ->
                { Type = m.ElementType; Frequency = frequency; Normalized = false; Stride = m.Stride; Offset = m.Offset; Buffer = b }
            )
        ))

    let createVertexArrayObject (program : Program) (rj : RenderJob) =
        { runCompile = fun s -> 
            let manager = s.manager

            let buffers = System.Collections.Generic.List<IChangeableResource>()
            let bindings = System.Collections.Generic.Dictionary()
            for v in program.Inputs do
                match rj.VertexAttributes.TryGetAttribute (v.semantic |> Symbol.Create) with
                    | Some value ->
                        let dep = manager.CreateBuffer(value.Buffer)
                        buffers.Add dep
                        bindings.[v.attributeIndex] <- createView AttributeFrequency.PerVertex value dep
                    | _  -> 
                        match rj.InstanceAttributes with
                            | null -> failwithf "could not get attribute %A (not found in vertex attributes, and instance attributes is null) for rj: %A" v.semantic rj
                            | _ -> 
                                printfn "looking up %s in instance attribs" v.semantic
                                match rj.InstanceAttributes.TryGetAttribute (v.semantic |> Symbol.Create) with
                                    | Some value ->
                                        let dep = manager.CreateBuffer(value.Buffer)
                                        buffers.Add dep
                                        bindings.[v.attributeIndex] <- createView (AttributeFrequency.PerInstances 1) value dep
                                    | _ -> failwithf "could not get attribute %A" v.semantic

            //create the index-buffer if desired
            let index = ref None
            if rj.Indices <> null then
                let dep = manager.CreateBuffer(rj.Indices)
                buffers.Add dep
                index := Some dep


            let bindings = bindings |> Seq.map (fun (KeyValue(k,v)) -> k,v) |> Seq.toList
            //create a vertex-array-object
            let vao = manager.CreateVertexArrayObject(bindings, !index)

            let newResources = (vao :> IChangeableResource)::(buffers |> Seq.toList)

            { s with resources = List.append newResources s.resources }, vao
        }

    let runLocal (c : Compiled<'a>) =
        { runCompile = fun s ->
            let (rs,rv) = c.runCompile s
            (s,rv)
        }


type AdaptiveCode(instructions : list<AdaptiveInstruction>, resources : list<IChangeableResource>) =
    member x.Instructions = instructions
    member x.Resources = resources

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AdaptiveCode =
    
    let writeTo (c : AdaptiveCode) (f : IDynamicFragment<'f>) =
        let changers = 
            c.Instructions |> List.choose (fun i ->
                let c = AdaptiveInstruction.writeTo i f
                if c.IsConstant then None
                else Some c
            )  
            
        if List.isEmpty changers then
            Mod.constant ()
        else
            changers |> Mod.mapN (fun _ -> ())  
    

module DeltaCompiler =


    let rec private allUniformsEqual (rj : RenderJob) (values : list<string * IMod>) =
        match values with
            | (k,v)::xs -> 
                match rj.Uniforms.TryGetUniform(rj.AttributeScope, Symbol.Create k) with
                    | Some c ->
                        if c = v then
                            allUniformsEqual rj xs
                        else
                            false

                    | _ -> false

            | [] -> true

    /// <summary>
    /// compileDeltaInternal compiles all instructions needed to render [next] 
    /// assuming [prev] was rendered immediately before.
    /// This function is the core-ingredient making our rendering-system
    /// fast as hell \o/.
    /// </summary>
    let compileDeltaInternal (prev : RenderJob) (next : RenderJob) =
        compiled {
                
            //set all modes if needed
            if prev.DepthTest <> next.DepthTest && next.DepthTest <> null then
                yield Instructions.setDepthTest next.DepthTest

            if prev.FillMode <> next.FillMode && next.FillMode <> null then
                yield Instructions.setFillMode next.FillMode

            if prev.CullMode <> next.CullMode && next.CullMode <> null then
                yield Instructions.setCullMode next.CullMode

            if prev.BlendMode <> next.BlendMode && next.BlendMode <> null then
                yield Instructions.setBlendMode next.BlendMode

            if prev.StencilMode <> next.StencilMode && next.StencilMode <> null then
                yield Instructions.setStencilMode next.StencilMode


            //bind the program
            let! program = Resources.createProgram next.Surface
            let programEqual = prev.Surface = next.Surface

            if prev.Surface <> next.Surface then
                yield Instructions.bindProgram program

                

            //create and bind all needed uniform-buffers
            let program = program.Resource.GetValue()

            for b in program.UniformBlocks do
                let! (uniformBuffer, values) = Resources.createUniformBuffer next.AttributeScope b program next.Uniforms

                //ISSUE: what if the programs are not equal but use the same uniform buffers?
                if not programEqual || not (allUniformsEqual prev values) then
                    yield Instructions.bindUniformBuffer b.index uniformBuffer

            //create and bind all textures/samplers
            for uniform in program.Uniforms do
                let nextTexture = next.Uniforms.TryGetUniform(next.AttributeScope, uniform.semantic |> Symbol.Create)
                let prevTexture = prev.Uniforms.TryGetUniform(prev.AttributeScope, uniform.semantic |> Symbol.Create)


                match uniform.uniformType with
                    | SamplerType ->
                        let sampler =
                            match program.SamplerStates.TryGetValue (Symbol.Create uniform.semantic) with
                                | (true, sampler) -> sampler
                                | _ -> 
                                    match uniform.samplerState with
                                        | Some sam ->
                                            match program.SamplerStates.TryGetValue (Symbol.Create sam) with
                                                | (true, sampler) -> sampler
                                                | _ -> SamplerStateDescription()
                                        | None ->
                                            SamplerStateDescription()

                        match nextTexture with
                            | Some value ->
                                match value with
                                    | :? IMod<ITexture> as value ->
                                        let! texture = Resources.createTexture value
                                        let! sampler = Resources.createSampler (Mod.constant sampler)
                              
                                        //ISSUE:      
                                        //there is a special case when the prev renderjob has the same texture but binds it to
                                        //a different slot!!!
                                        match prevTexture with
                                            | Some old when old = (value :> IMod) ->
                                                ()
                                            | _ ->
                                                yield Instructions.setActiveTexture uniform.index
                                                yield Instructions.bindSampler uniform.index sampler
                                                yield Instructions.bindTexture texture

                                    | _ ->
                                        Log.warn "Urdar: using default texture since none was found"
                            | _ ->
                                Log.warn "using default texture since none was found"
                                yield Instructions.setActiveTexture uniform.index 
                                //let tex = Texture(program.Context, 0, TextureDimension.Texture2D, 1, V3i(1,1,0), 1, ChannelType.RGBA8)
                                yield Instruction.BindTexture 0x0DE1 0 //bindTexture tex

                    | _ ->
                        match prevTexture, nextTexture with
                            | Some p, Some n when p = n -> ()
                            | _ ->
                                let! (loc,_) = Resources.createUniformLocation next.AttributeScope uniform next.Uniforms
                                let l = ExecutionContext.bindUniformLocation uniform.location (loc.Resource.GetValue())
                                yield l

                    | _ ->
                        Log.warn "trying to set unknown top-level uniform: %A" uniform

            let! vao = Resources.createVertexArrayObject program next
            if prev <> RenderJob.Empty then
                if prev.Surface = next.Surface then
                    let! vaoPrev = Resources.runLocal (Resources.createVertexArrayObject program prev)
                    vaoPrev.Dispose()
                    if vao <> vaoPrev then
                        yield Instructions.bindVertexArray vao
                else
                    yield Instructions.bindVertexArray vao
            else
                yield Instructions.bindVertexArray vao

            yield Instructions.draw program next.Indices next.DrawCallInfo next.IsActive 
        }

    let compileDelta (manager : ResourceManager) (currentContext : IMod<ContextHandle>) (prev : RenderJob) (next : RenderJob) =
        let c = compileDeltaInternal prev next

        let (s,()) =
            c.runCompile {
                currentContext = currentContext
                manager = manager
                instructions = []
                resources = []
            }

        AdaptiveCode(s.instructions, s.resources)