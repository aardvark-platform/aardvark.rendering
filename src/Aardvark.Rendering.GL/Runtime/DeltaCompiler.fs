namespace Aardvark.Rendering.GL.Compiler

open Aardvark.Base.Incremental
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.GL

type CompilerInfo =
    {
        stats : ref<FrameStatistics>
        currentContext : IMod<ContextHandle>
        drawBuffers : nativeint
        drawBufferCount : int
    }
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

            if prev.StencilBufferMask <> me.StencilBufferMask then
                yield Instructions.setStencilMask me.StencilBufferMask

            if prev.DrawBufferSet <> me.DrawBufferSet then
                if me.DrawBufferCount < 0 then
                    let! s = compilerState
                    yield Instruction.DrawBuffers s.allDrawBuffersCount s.allDrawBuffers
                else
                    yield Instruction.DrawBuffers me.DrawBufferCount me.DrawBuffers

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
                    yield Instructions.draw prog me.Original.Indices me.DrawCallInfos.Handle me.Mode me.IsActive

        }   

    let internal compileEpilogInternal =
        compiled {

            yield Instruction.DepthMask 1
            yield Instruction.StencilMask 0xFFFFFFFF

            let! s = compilerState
            yield Instruction.DrawBuffers s.allDrawBuffersCount s.allDrawBuffers
//
//            for (i,_) in Map.toSeq signature.ColorAttachments do
//                yield Instruction.ColorMask i 1 1 1 1

            // TODO: find max used texture-unit
            for i in 0..15 do
                yield Instructions.setActiveTexture i
                yield Instruction.BindSampler i 0
                yield Instruction.BindTexture (int OpenGl.Enums.TextureTarget.Texture2D) 0

            for i in 0..15 do
                yield Instruction.BindBufferBase (int OpenGl.Enums.BufferTarget.UniformBuffer) i 0


            yield Instruction.BindVertexArray 0
            yield Instruction.BindProgram 0
            yield Instruction.BindBuffer (int OpenTK.Graphics.OpenGL4.BufferTarget.DrawIndirectBuffer) 0

            
        }    


    /// <summary>
    /// compileDelta compiles all instructions needed to render [rj] 
    /// assuming [prev] was rendered immediately before.
    /// This function is the core-ingredient making our rendering-system
    /// fast as hell \o/.
    /// </summary>
    let compileDelta (info : CompilerInfo) (prev : PreparedRenderObject ) (rj : PreparedRenderObject) =
        let c = compileDeltaInternal prev rj
        let (s,()) =
            c.runCompile {
                currentContext = info.currentContext
                allDrawBuffers = info.drawBuffers
                allDrawBuffersCount = info.drawBufferCount
                instructions = []
            }

        s.instructions

    /// <summary>
    /// compileFull compiles all instructions needed to render [rj] 
    /// making no assumpltions about the previous GL state.
    /// </summary>
    let compileFull (info : CompilerInfo) (rj : PreparedRenderObject) =
        let c = compileDeltaInternal PreparedRenderObject.empty rj

        let (s,()) =
            c.runCompile {
                currentContext = info.currentContext
                allDrawBuffers = info.drawBuffers
                allDrawBuffersCount = info.drawBufferCount
                instructions = []
            }

        s.instructions

    let compileEpilog (info : CompilerInfo) =
        let c = compileEpilogInternal

        let (s,()) =
            c.runCompile {
                currentContext = info.currentContext
                allDrawBuffers = info.drawBuffers
                allDrawBuffersCount = info.drawBufferCount
                instructions = []
            }

        s.instructions


