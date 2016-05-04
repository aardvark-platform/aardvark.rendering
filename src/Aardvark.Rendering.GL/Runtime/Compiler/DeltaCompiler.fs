namespace Aardvark.Rendering.GL.Compiler

open Aardvark.Base.Incremental
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.GL


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

    let internal compileDeltaInternal (prev : PreparedRenderObject) (me : PreparedRenderObject) =
        compiled {

            // set the output-buffers
            if prev.DepthBufferMask <> me.DepthBufferMask then
                yield Instructions.setDepthMask me.DepthBufferMask

            if prev.ColorBufferMasks <> me.ColorBufferMasks then
                match me.ColorBufferMasks with
                    | Some masks -> yield Instructions.setColorMasks masks
                    | None -> yield Instructions.setColorMasks (List.init me.ColorAttachmentCount (fun _ -> V4i.IIII))

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
                        yield Instructions.bindUniformBufferView id ub

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

                if not texEqual then 
                    yield Instructions.bindTexture tex

                if not samEqual || (not ExecutionContext.samplersSupported && not texEqual) then
                    yield Instructions.bindSampler id sam

            // bind all top-level uniforms (if needed)
            for (id,u) in Map.toSeq me.Uniforms do
                match Map.tryFind id prev.Uniforms with
                    | Some old when old = u -> ()
                    | _ ->
                        // TODO: UniformLocations cannot change structurally atm.
                        yield ExecutionContext.bindUniformLocation id (u.Handle.GetValue())


            // bind the VAO (if needed)
            if prev.VertexArray <> me.VertexArray then
                yield Instructions.bindVertexArray me.VertexArray

            // bind vertex attribute default values
            for (id,v) in Map.toSeq me.VertexAttributeValues do
                match Map.tryFind id prev.VertexAttributeValues with
                    | Some ov when v = ov -> ()
                    | _ -> 
                        yield Instructions.bindVertexAttribValue id v

            // draw the thing
            // TODO: surface assumed to be constant here
            let prog = me.Program.Handle.GetValue()

            match me.IndirectBuffer with
                | Some ib ->
                    yield Instructions.bindIndirectBuffer ib
                    yield Instructions.drawIndirect prog me.Original.Indices ib me.Mode me.IsActive
                | _ ->
                    yield Instructions.draw prog me.Original.Indices me.DrawCallInfos me.Mode me.IsActive

        }   



    /// <summary>
    /// compileDelta compiles all instructions needed to render [rj] 
    /// assuming [prev] was rendered immediately before.
    /// This function is the core-ingredient making our rendering-system
    /// fast as hell \o/.
    /// </summary>
    let compileDelta (currentContext : IMod<ContextHandle>) (prev : PreparedRenderObject ) (rj : PreparedRenderObject) =
        let c = compileDeltaInternal prev rj
        let (s,()) =
            c.runCompile {
                currentContext = currentContext
                instructions = []
            }

        s.instructions

    /// <summary>
    /// compileFull compiles all instructions needed to render [rj] 
    /// making no assumpltions about the previous GL state.
    /// </summary>
    let compileFull (currentContext : IMod<ContextHandle>) (rj : PreparedRenderObject) =
        let c = compileDeltaInternal PreparedRenderObject.empty rj

        let (s,()) =
            c.runCompile {
                currentContext = currentContext
                instructions = []
            }

        s.instructions


module internal DeltaCompilerDebug =
    open DeltaCompiler

    let compileDeltaDebugNoResources (currentContext : IMod<ContextHandle>) (prev : PreparedRenderObject ) (rj : PreparedRenderObject) =
        let c = compileDeltaInternal prev rj
        let (s,()) =
            c.runCompile {
                currentContext = currentContext
                instructions = []
            }

        s.instructions |> List.collect (fun mi -> mi.GetValue())

    let compileFullDebugNoResources (currentContext : IMod<ContextHandle>) (rj : PreparedRenderObject) =
        let c = compileDeltaInternal PreparedRenderObject.empty rj

        let (s,()) =
            c.runCompile {
                currentContext = currentContext
                instructions = []
            }

        
        s.instructions |> List.collect (fun mi -> mi.GetValue())
