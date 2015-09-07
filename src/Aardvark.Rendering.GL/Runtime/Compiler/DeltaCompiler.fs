namespace Aardvark.Rendering.GL.Compiler

open Aardvark.Base.Incremental
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.GL

type AdaptiveCode(instructions : list<MetaInstruction>, resources : list<IChangeableResource>) =
    member x.Instructions = instructions
    member x.Resources = resources

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AdaptiveCode =
    
    let writeTo (c : AdaptiveCode) (f : IDynamicFragment<'f>) =
        let changers = 
            c.Instructions |> List.choose (fun i ->
                let c = MetaInstruction.appendTo i f
                if c.IsConstant then None
                else Some c
            )  
            
        if List.isEmpty changers then
            Mod.constant FrameStatistics.Zero
        else
            changers |> Mod.mapN (fun _ -> FrameStatistics.Zero)  
    

module DeltaCompiler =

    /// determines if all uniforms (given in values) are equal to the uniforms
    /// of the given RenderObject
    let rec private allUniformsEqual (rj : RenderObject) (values : list<string * IMod>) =
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
    let private compileDeltaInternal (prev : RenderObject) (next : RenderObject) =
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
                                        //there is a special case when the prev renderobject has the same texture but binds it to
                                        //a different slot!!!
                                        match prevTexture with
                                            | Some old when old = (value :> IMod) ->
                                                ()
                                            | _ ->
                                                yield Instructions.setActiveTexture uniform.index
                                                yield Instructions.bindSampler uniform.index sampler
                                                yield Instructions.bindTexture texture

                                    | _ ->
                                        Log.warn "unexpected texture type %s: %A" uniform.semantic value
                            | _ ->
                                Log.warn "texture %s not found" uniform.semantic
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
            if prev <> RenderObject.Empty then
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


    let private compileDeltaInternalPrepared (prev : PreparedRenderObject) (me : PreparedRenderObject) =
        compiled {
            //set all modes if needed
            if prev.DepthTest <> me.DepthTest && me.DepthTest <> null then
                yield Instructions.setDepthTest me.DepthTest

            if prev.FillMode <> me.FillMode && me.FillMode <> null then
                yield Instructions.setFillMode me.FillMode

            if prev.CullMode <> me.CullMode && me.CullMode <> null then
                yield Instructions.setCullMode me.CullMode

            if prev.BlendMode <> me.BlendMode && me.BlendMode <> null then
                yield Instructions.setBlendMode me.BlendMode

            if prev.StencilMode <> me.StencilMode && me.StencilMode <> null then
                yield Instructions.setStencilMode me.StencilMode
        
            // bind the program (if needed)
            if prev.Program <> me.Program then
                yield Instructions.bindProgram me.Program

            // bind all uniform-buffers (if needed)
            for (id,ub) in Map.toSeq me.UniformBuffers do
                match Map.tryFind id prev.UniformBuffers with
                    | Some old when old = ub -> 
                        // the same UniformBuffer has already been bound
                        ()
                    | _ -> 
                        yield Instructions.bindUniformBuffer id ub

            // bind all textures/samplers (if needed)
            let latestSlot = ref prev.LastTextureSlot
            for (id,(tex,sam)) in Map.toSeq me.Textures do
                let texEqual, samEqual =
                    match Map.tryFind id prev.Textures with
                        | Some (ot, os) -> (ot = tex), (os = sam)
                        | _ -> false, false


                if id <> !latestSlot then
                    yield Instructions.setActiveTexture id
                    latestSlot := id 

                if not samEqual then
                    yield Instructions.bindSampler id sam

                if not texEqual then 
                    yield Instructions.bindTexture tex

            // bind all top-level uniforms (if needed)
            for (id,u) in Map.toSeq me.Uniforms do
                match Map.tryFind id prev.Uniforms with
                    | Some old when old = u -> ()
                    | _ ->
                        // TODO: UniformLocations cannot change structurally atm.
                        yield ExecutionContext.bindUniformLocation id (u.Resource.GetValue())
          
            // bind the VAO (if needed)
            if prev.VertexArray <> me.VertexArray then
                yield Instructions.bindVertexArray me.VertexArray

            // draw the thing
            // TODO: surface assumed to be constant here
            let prog = me.Program.Resource.GetValue()
            yield Instructions.draw prog me.Original.Indices me.DrawCallInfo me.IsActive

        }   


    let private stateToStats (s : CompilerState) = 
        { FrameStatistics.Zero with 
            ResourceUpdateTime = s.resourceCreateTime.Elapsed
            ResourceUpdateCounts = 
                 s.resources |> Seq.countBy (fun r -> r.Kind) |> Seq.map (fun (k,v) -> k,float v) |> Map.ofSeq 
        }

    /// <summary>
    /// compileDelta compiles all instructions needed to render [rj] 
    /// assuming [prev] was rendered immediately before.
    /// This function is the core-ingredient making our rendering-system
    /// fast as hell \o/.
    /// </summary>
    let compileDelta (manager : ResourceManager) (currentContext : IMod<ContextHandle>) (prev : RenderObject) (rj : RenderObject) =
        let c = compileDeltaInternal prev rj
        let (s,()) =
            c.runCompile {
                currentContext = currentContext
                manager = manager
                instructions = []
                resources = []
                resourceCreateTime = System.Diagnostics.Stopwatch()
            }

        AdaptiveCode(s.instructions, s.resources), stateToStats s

    /// <summary>
    /// compileFull compiles all instructions needed to render [rj] 
    /// making no assumpltions about the previous GL state.
    /// </summary>
    let compileFull (manager : ResourceManager) (currentContext : IMod<ContextHandle>) (rj : RenderObject) =
        let c = compileDeltaInternal RenderObject.Empty rj

        let (s,()) =
            c.runCompile {
                currentContext = currentContext
                manager = manager
                instructions = []
                resources = []
                resourceCreateTime = System.Diagnostics.Stopwatch()
            }

        AdaptiveCode(s.instructions, s.resources), stateToStats s