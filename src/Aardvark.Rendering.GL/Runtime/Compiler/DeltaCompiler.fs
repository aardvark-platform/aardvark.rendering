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

    let useResource (r : ChangeableResource<'a>) : Compiled<unit> =
        { runCompile = 
            fun s -> 
                if s.useResources then
                    r.IncrementRefCount () |> ignore
                    { s with resources = (r :> _) :: s.resources }, ()
                else
                    s, ()
        }

    let useResources (resources : seq<ChangeableResource<'a>>) : Compiled<unit> =
        { runCompile = 
            fun s -> 
                if s.useResources then
                    for r in resources do
                        r.IncrementRefCount () |> ignore
                    { s with resources = (resources |> Seq.cast |> Seq.toList) @ s.resources }, ()
                else
                    s, ()
        }

    let internal compileDeltaInternal (prev : PreparedRenderObject) (me : PreparedRenderObject) =
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
        
            do! useResource me.Program

            // bind the program (if needed)
            if prev.Program <> me.Program then
                yield Instructions.bindProgram me.Program

            // bind all uniform-buffers (if needed)
            for (id,ub) in Map.toSeq me.UniformBuffers do
                do! useResource ub
                match Map.tryFind id prev.UniformBuffers with
                    | Some old when old = ub -> 
                        // the same UniformBuffer has already been bound
                        ()
                    | _ -> 
                        yield Instructions.bindUniformBuffer id ub

            // bind all textures/samplers (if needed)
            let latestSlot = ref prev.LastTextureSlot
            for (id,(tex,sam)) in Map.toSeq me.Textures do
                do! useResource tex
                do! useResource sam
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
                do! useResource u
                match Map.tryFind id prev.Uniforms with
                    | Some old when old = u -> ()
                    | _ ->
                        // TODO: UniformLocations cannot change structurally atm.
                        yield ExecutionContext.bindUniformLocation id (u.Resource.GetValue())

            do! me.Buffers |> Map.toSeq |> Seq.map (fun (_,(b,_)) -> b) |> useResources
            do! useResource me.VertexArray

            // bind the VAO (if needed)
            if prev.VertexArray <> me.VertexArray then
                yield Instructions.bindVertexArray me.VertexArray

            // draw the thing
            // TODO: surface assumed to be constant here
            let prog = me.Program.Resource.GetValue()
            yield Instructions.draw prog me.Original.Indices me.DrawCallInfo me.Mode me.IsActive

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
    let compileDelta (manager : ResourceManager) (currentContext : IMod<ContextHandle>) (prev : PreparedRenderObject ) (rj : PreparedRenderObject) =
        let c = compileDeltaInternal prev rj
        let (s,()) =
            c.runCompile {
                currentContext = currentContext
                manager = manager
                useResources = true
                instructions = []
                resources = []
                resourceCreateTime = System.Diagnostics.Stopwatch()
            }

        AdaptiveCode(s.instructions, s.resources), stateToStats s

    /// <summary>
    /// compileFull compiles all instructions needed to render [rj] 
    /// making no assumpltions about the previous GL state.
    /// </summary>
    let compileFull (manager : ResourceManager) (currentContext : IMod<ContextHandle>) (rj : PreparedRenderObject) =
        let c = compileDeltaInternal PreparedRenderObject.Empty rj

        let (s,()) =
            c.runCompile {
                currentContext = currentContext
                manager = manager
                useResources = true
                instructions = []
                resources = []
                resourceCreateTime = System.Diagnostics.Stopwatch()
            }

        AdaptiveCode(s.instructions, s.resources), stateToStats s


module internal DeltaCompilerDebug =
    open DeltaCompiler

    let compileDeltaDebugNoResources (manager : ResourceManager) (currentContext : IMod<ContextHandle>) (prev : PreparedRenderObject ) (rj : PreparedRenderObject) =
        let c = compileDeltaInternal prev rj
        let (s,()) =
            c.runCompile {
                currentContext = currentContext
                manager = manager
                useResources = false
                instructions = []
                resources = []
                resourceCreateTime = System.Diagnostics.Stopwatch()
            }

        s.instructions |> List.collect (fun mi ->
            match mi with
                | FixedInstruction l -> l
                | AdaptiveInstruction l -> l.GetValue()
        )

    let compileFullDebugNoResources (manager : ResourceManager) (currentContext : IMod<ContextHandle>) (rj : PreparedRenderObject) =
        let c = compileDeltaInternal PreparedRenderObject.Empty rj

        let (s,()) =
            c.runCompile {
                currentContext = currentContext
                manager = manager
                useResources = false
                instructions = []
                resources = []
                resourceCreateTime = System.Diagnostics.Stopwatch()
            }

        s.instructions |> List.collect (fun mi ->
            match mi with
                | FixedInstruction l -> l
                | AdaptiveInstruction l -> l.GetValue()
        )
