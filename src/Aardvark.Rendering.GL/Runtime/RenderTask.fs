namespace Aardvark.Rendering.GL

open System
open System.Runtime.InteropServices
open System.Collections.Generic
open Aardvark.Base
open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4
open Aardvark.Base.Incremental
open Aardvark.Rendering.GL.OpenGl.Enums
open System.Linq

type RenderTaskInputSet(target : IRenderTask) =
    inherit Compiler.InputSet(target)

    let allPools = ReferenceCountingSet<UniformBufferPool>()
    let resources = ReferenceCountingSet<IChangeableResource>()

    member x.AllPools = allPools
    member x.Resources = resources

    override x.Add(o : IAdaptiveObject) =
        match o with
            | :? ChangeableResource<UniformBufferView> as r ->
                if resources.Add r then
                    allPools.Add (r.Resource.GetValue().Pool) |> ignore
                    target.InputChanged r
            | :? IChangeableResource as r ->
                resources.Add r |> ignore
            | _ -> ()

        base.Add(o)
            
    override x.Remove(o : IAdaptiveObject) =
        match o with
            | :? ChangeableResource<UniformBufferView> as r ->
                if resources.Remove r then
                    allPools.Remove (r.Resource.GetValue().Pool) |> ignore
            | :? IChangeableResource as r ->
                resources.Remove r |> ignore
            | _ -> ()

        base.Remove(o)

type RenderTask(runtime : IRuntime, fboSignature : IFramebufferSignature, ctx : Context, manager : ResourceManager, engine : IMod<BackendConfiguration>, set : aset<IRenderObject>) as this =
    inherit AdaptiveObject()

    let mutable currentEngine = engine.GetValue(this)
    let reader = set.GetReader()
    do reader.AddOutput this
       engine.AddOutput this

    let mutable programs = Map.empty
    let changer = Mod.init ()

    let mutable additions = 0
    let mutable removals = 0
    let mutable frameId = 0UL
    let mutable lastScope = None

    let dirtyLock = obj()
    let mutable dirtyUniformViews = HashSet<ChangeableResource<UniformBufferView>>()
    let mutable dirtyResources = HashSet<IChangeableResource>()
    let mutable dirtyPoolIds = Array.empty

    let inputSet = RenderTaskInputSet(this)

    let tryGetProgramForPass (pass : uint64) =
        Map.tryFind pass programs

    let newProgram (scope : Ag.Scope) (engine : BackendConfiguration) : IRenderProgram =
            
        match engine.sorting with
            | RenderObjectSorting.Dynamic newSorter ->
                // TODO: respect mode here
                    
                Log.line "using GLVM sorted program"
                new Compiler.SortedProgram<_>(this,
                    Compiler.FragmentHandlers.glvmRuntimeRedundancyChecks, 
                    (fun () -> newSorter scope),
                    manager, inputSet
                ) :> IRenderProgram
            | s ->
                match engine.execution, engine.redundancy with

                    | ExecutionEngine.Native, RedundancyRemoval.None ->
                        Log.line "using unoptimized native program"
                        new Compiler.UnoptimizedProgram<_>(this,
                            engine, Compiler.FragmentHandlers.native, manager, inputSet
                        ) :> IRenderProgram

                    | ExecutionEngine.Native, _ ->
                        Log.line "using optimized native program"
                        new Compiler.OptimizedProgram<_>(this,
                            engine, Compiler.FragmentHandlers.native, manager, inputSet
                        ) :> IRenderProgram

                    | ExecutionEngine.Unmanaged, RedundancyRemoval.None ->
                        Log.line "using unoptimized glvm program"
                        new Compiler.UnoptimizedProgram<_>(this,
                            engine, Compiler.FragmentHandlers.glvm, manager, inputSet
                        ) :> IRenderProgram

                    | ExecutionEngine.Unmanaged, RedundancyRemoval.Runtime ->
                        Log.line "using runtime-optimized glvm program"
                        new Compiler.UnoptimizedProgram<_>(this,
                            engine, Compiler.FragmentHandlers.glvmRuntimeRedundancyChecks, manager, inputSet
                        ) :> IRenderProgram

                    | ExecutionEngine.Unmanaged, RedundancyRemoval.Static ->
                        Log.line "using optimized glvm program"
                        new Compiler.OptimizedProgram<_>(this,
                            engine, Compiler.FragmentHandlers.glvm, manager, inputSet
                        ) :> IRenderProgram


                    | ExecutionEngine.Managed, RedundancyRemoval.None ->
                        Log.line "using unoptimized managed program"
                        new Compiler.UnoptimizedProgram<_>(this,
                            engine, Compiler.FragmentHandlers.managed, manager, inputSet
                        ) :> IRenderProgram

                    | ExecutionEngine.Managed, _->
                        Log.line "using optimized managed program"
                        new Compiler.OptimizedProgram<_>(this,
                            engine, Compiler.FragmentHandlers.managed, manager, inputSet
                        ) :> IRenderProgram

                    | ExecutionEngine.Debug, _ ->
                        Log.warn "using debug program"

                        new Compiler.DebugProgram(this,
                            manager, inputSet
                        ) :> IRenderProgram

                    | _ ->
                        failwithf "unsupported configuration: %A" engine

    let getProgramForPass (pass : uint64) (scope : Ag.Scope) =
        match lastScope with | Some v -> () | None -> lastScope <- Some scope
        match Map.tryFind pass programs with
            | Some p -> p
            | _ -> 
                let program = newProgram scope (engine.GetValue(this))

                programs <- Map.add pass program programs
                program

    let setExecutionEngine (newEngine : BackendConfiguration) =
        if currentEngine <> newEngine then
            currentEngine <- newEngine

            let scope = match lastScope with | Some v -> v | None -> failwith "no last scope set"

            let newPrograms =
                programs |> Map.map (fun pass v ->
                    let program = newProgram scope newEngine

                    for rj in v.RenderObjects do
                        program.Add rj

                    program
                )

            let old = System.Threading.Interlocked.Exchange(&programs, newPrograms)

            for (_,o) in Map.toSeq old do
                o.Dispose()




    let updateCPUTime = System.Diagnostics.Stopwatch()
    let updateGPUTime = System.Diagnostics.Stopwatch()
    let executionTime = System.Diagnostics.Stopwatch()
    
    let addIfNotZero (key : 'a) (cnt : int) (m : Map<'a, float>) =
        if cnt <> 0 then
            match Map.tryFind key m with
                | Some v -> Map.add key (v + float cnt) m
                | None -> Map.add key (float cnt) m
        else
            m

    member x.Runtime = runtime
    member x.Manager = manager



    override x.InputChanged(o : IAdaptiveObject) =
        match o with
            | :? ChangeableResource<UniformBufferView> as o ->
                lock dirtyLock (fun () ->
                    dirtyUniformViews.Add(o) |> ignore
                )

            | :? IChangeableResource as o ->
                lock dirtyLock (fun () ->
                    dirtyResources.Add(o) |> ignore
                )
            | _ -> ()

    member private x.UpdateDirtyUniformBufferViews() =
        let dirtyBufferViews = System.Threading.Interlocked.Exchange(&dirtyUniformViews, HashSet())
        let mutable viewUpdateCount = 0
        updateCPUTime.Restart()
                
        
        let totalPoolCount = 1 + ctx.MaxUniformBufferPoolId
        if dirtyPoolIds.Length <> totalPoolCount then
            dirtyPoolIds <- Array.init (1 + ctx.MaxUniformBufferPoolId) (fun _ -> ref [])

        if dirtyBufferViews.Count > 0 then
            System.Threading.Tasks.Parallel.ForEach(dirtyBufferViews, fun (d : ChangeableResource<UniformBufferView>) ->
                d.UpdateCPU(x)
                d.UpdateGPU(x) |> ignore

                let view = d.Resource.GetValue()
                System.Threading.Interlocked.Change(dirtyPoolIds.[view.Pool.PoolId], fun l -> view::l) |> ignore
                System.Threading.Interlocked.Increment &viewUpdateCount |> ignore
            ) |> ignore

        updateCPUTime.Stop()

        updateGPUTime.Restart()

        let mutable dirtyPoolCount = 0
        for pool in inputSet.AllPools do
            let r = dirtyPoolIds.[pool.PoolId]
            match !r with
                | [] -> ()
                | dirty ->
                    pool.Upload (List.toArray dirty)
                    dirtyPoolCount <- dirtyPoolCount + 1
                    r := []

        updateGPUTime.Stop()
 
            
        let time = updateCPUTime.Elapsed + updateGPUTime.Elapsed


        dirtyPoolCount, viewUpdateCount, time

    member private x.UpdateDirtyResources() =
        let mutable stats = FrameStatistics.Zero
//        let poolUpdateCount, viewUpdateCount, uniformUpdateTime = 
//            x.UpdateDirtyUniformBufferViews()

        let mutable count = 0 //poolUpdateCount + viewUpdateCount
        let counts = Dictionary<ResourceKind, ref<int>>()
//        counts.[ResourceKind.UniformBuffer] <- ref viewUpdateCount
//        counts.[ResourceKind.Buffer] <- ref poolUpdateCount

            
        let dirtyResources = System.Threading.Interlocked.Exchange(&dirtyResources, HashSet())
        if dirtyResources.Count > 0 then
            System.Threading.Tasks.Parallel.ForEach(dirtyResources, fun (d : IChangeableResource) ->
                lock d (fun () ->
                    if d.OutOfDate then
                        d.UpdateCPU(x)
                    else
                        d.Outputs.Add x |> ignore
                )
            ) |> ignore
  
            let mutable cc = Unchecked.defaultof<_>
            for d in dirtyResources do
                lock d (fun () ->
                    if d.OutOfDate then
                        count <- count + 1
                        if counts.TryGetValue(d.Kind, &cc) then
                            cc := !cc + 1
                        else
                            counts.[d.Kind] <- ref 1

                        stats <- stats + d.UpdateGPU(x)
                )

        let counts = counts |> Dictionary.toSeq |> Seq.map (fun (k,v) -> k,float !v) |> Map.ofSeq

        if Config.SyncUploadsAndFrames && count > 0 then
            OpenTK.Graphics.OpenGL4.GL.Sync()

        count, counts, updateCPUTime.Elapsed + updateGPUTime.Elapsed, stats



    member private x.Add(pass : uint64, rj : IRenderObject) =
        additions <- additions + 1
        let program = getProgramForPass pass rj.AttributeScope
        program.Add rj

    member private x.Remove(pass : uint64, rj : IRenderObject) =
        removals <- removals + 1
        match tryGetProgramForPass pass with
            | Some p -> p.Remove rj
            | None -> ()

    member private x.ProcessDeltas (deltas : list<Delta<IRenderObject>>) =
        let mutable additions = 0
        let mutable removals = 0
        for d in deltas do
            match d with
                | Add a ->
                    x.Add(a.RenderPass, a)
                    additions <- additions + 1
                | Rem a ->    
                    x.Remove(a.RenderPass, a)
                    removals <- removals + 1
        (additions, removals)





    member x.Run (caller : IAdaptiveObject, fbo : OutputDescription) =
        let fbo = fbo.framebuffer
        x.EvaluateAlways caller (fun () ->
            using ctx.ResourceLock (fun _ ->

                let wasEnabled = GL.IsEnabled EnableCap.DebugOutput
                if currentEngine.useDebugOutput then
                    match ContextHandle.Current with
                        | Some v -> v.AttachDebugOutputIfNeeded()
                        | None -> Report.Warn("No active context handle in RenderTask.Run")
                    GL.Enable EnableCap.DebugOutput


                let old = Array.create 4 0
                let mutable oldFbo = 0
                OpenTK.Graphics.OpenGL.GL.GetInteger(OpenTK.Graphics.OpenGL.GetPName.Viewport, old)
                OpenTK.Graphics.OpenGL.GL.GetInteger(OpenTK.Graphics.OpenGL.GetPName.FramebufferBinding, &oldFbo)

                let handle = fbo.GetHandle null |> unbox<int> 

                if ExecutionContext.framebuffersSupported then
                    GL.BindFramebuffer(OpenTK.Graphics.OpenGL4.FramebufferTarget.Framebuffer, handle)
                    let drawBuffers = Array.init fbo.Attachments.Count (fun i -> int DrawBuffersEnum.ColorAttachment0 + i |> unbox<DrawBuffersEnum>)
                    GL.DrawBuffers(drawBuffers.Length, drawBuffers)
                    GL.Check "could not bind framebuffer"
                elif handle <> 0 then
                    failwithf "cannot render to texture on this OpenGL driver"


                GL.Viewport(0, 0, fbo.Size.X, fbo.Size.Y)
                GL.Check "could not set viewport"
     
     

                setExecutionEngine (engine.GetValue(x))

                let additions, removals =
                    match reader.GetDelta(x) with
                        | [] -> 0,0
                        | deltas -> x.ProcessDeltas deltas

                let resourceUpdates, resourceCounts, resourceUpdateTime, updateStats = 
                    x.UpdateDirtyResources()

                let mutable stats = FrameStatistics.Zero
                let contextHandle = ContextHandle.Current.Value
                for (KeyValue(_,p)) in programs do
                    stats <- stats + p.Update(handle, contextHandle)
   

                let poolUpdates, viewUpdates, uniformUpdateTime = 
                    x.UpdateDirtyUniformBufferViews()

  
                executionTime.Restart()


                //render
                for (KeyValue(_,p)) in programs do
                    stats <- stats + p.Run(handle, contextHandle)
   
                // TODO: remove if false (would however cause perf-results to be incomparable)
                if false then
                    GL.Sync()

                executionTime.Stop()

                if ExecutionContext.framebuffersSupported then
                    GL.BindFramebuffer(OpenTK.Graphics.OpenGL4.FramebufferTarget.Framebuffer, oldFbo)
                    GL.Check "could not bind framebuffer"

                GL.Viewport(old.[0], old.[1], old.[2], old.[3])
                GL.Check "could not set viewport"
                    
                    

                if wasEnabled <> currentEngine.useDebugOutput then
                    if wasEnabled then GL.Enable EnableCap.DebugOutput
                    else GL.Disable EnableCap.DebugOutput

                let resourceCounts =
                    resourceCounts
                        |> addIfNotZero ResourceKind.UniformBufferView viewUpdates
                        |> addIfNotZero ResourceKind.UniformBuffer poolUpdates


                let stats = 
                    { stats with 
                        ExecutionTime = executionTime.Elapsed
                        ResourceUpdateCount = float (resourceUpdates + viewUpdates + poolUpdates)
                        ResourceUpdateCounts = resourceCounts
                        ResourceUpdateTime = (resourceUpdateTime + uniformUpdateTime)
                        AddedRenderObjects = float additions
                        RemovedRenderObjects = float removals
                        ResourceCount = float inputSet.Resources.Count 
                    }

                frameId <- frameId + 1UL

                RenderingResult(fbo, updateStats + stats)
            )
        )

    member x.Disassemble() =
        programs |> Map.map (fun _ p -> p.Disassemble() |> Seq.toList)

    member x.Dispose() = 
        for _,p in Map.toSeq programs do
            p.Dispose()
        programs <- Map.empty
        reader.RemoveOutput x
        reader.Dispose()

    interface IRenderTask with
        
        member x.Run(caller, fbo) =
            x.Run(caller, fbo)

        member x.Dispose() =
            x.Dispose()

        member x.FramebufferSignature = fboSignature

        member x.Runtime = runtime |> Some

        member x.FrameId = frameId


type ClearTask(runtime : IRuntime, fboSignature : IFramebufferSignature, color : IMod<list<Option<C4f>>>, depth : IMod<Option<float>>, ctx : Context) =
    inherit AdaptiveObject()


    let mutable frameId = 0UL

    member x.Run(caller : IAdaptiveObject, desc : OutputDescription) =
        let fbo = desc.framebuffer
        using ctx.ResourceLock (fun _ ->
            x.EvaluateAlways caller (fun () ->

                let old = Array.create 4 0
                let mutable oldFbo = 0
                OpenTK.Graphics.OpenGL.GL.GetInteger(OpenTK.Graphics.OpenGL.GetPName.Viewport, old)
                OpenTK.Graphics.OpenGL.GL.GetInteger(OpenTK.Graphics.OpenGL.GetPName.FramebufferBinding, &oldFbo)

                let handle = fbo.GetHandle null |> unbox<int>

                if ExecutionContext.framebuffersSupported then
                    GL.BindFramebuffer(OpenTK.Graphics.OpenGL4.FramebufferTarget.Framebuffer, handle)
                    GL.Check "could not bind framebuffer"
                elif handle <> 0 then
                    failwithf "cannot render to texture on this OpenGL driver"

                GL.Viewport(0, 0, fbo.Size.X, fbo.Size.Y)
                GL.Check "could not bind framebuffer"

                let depthValue = depth.GetValue x
                let colorValues = color.GetValue x

                match colorValues, depthValue with
                    | [Some c], Some depth ->
                        GL.ClearColor(c.R, c.G, c.B, c.A)
                        GL.ClearDepth(depth)
                        GL.Clear(ClearBufferMask.ColorBufferBit ||| ClearBufferMask.DepthBufferBit)
                        
                    | [Some c], None ->
                        GL.ClearColor(c.R, c.G, c.B, c.A)
                        GL.Clear(ClearBufferMask.ColorBufferBit)

                    | l, Some depth when List.forall Option.isNone l ->
                        GL.ClearDepth(depth)
                        GL.Clear(ClearBufferMask.DepthBufferBit)
                    | l, d ->
                            
                        let mutable i = 0
                        for c in l do
                            match c with
                                | Some c ->
                                    GL.DrawBuffer(int DrawBufferMode.ColorAttachment0 + i |> unbox)
                                    GL.ClearColor(c.R, c.G, c.B, c.A)
                                    GL.Clear(ClearBufferMask.ColorBufferBit)
                                | None ->
                                    ()
                            i <- i + 1

                        match d with
                            | Some depth -> 
                                GL.ClearDepth(depth)
                                GL.Clear(ClearBufferMask.DepthBufferBit)
                            | None ->
                                ()


                if ExecutionContext.framebuffersSupported then
                    GL.BindFramebuffer(OpenTK.Graphics.OpenGL4.FramebufferTarget.Framebuffer, oldFbo)

                GL.Viewport(old.[0], old.[1], old.[2], old.[3])
                GL.Check "could not bind framebuffer"

                frameId <- frameId + 1UL

                RenderingResult(fbo, FrameStatistics.Zero)
            )
        )

    member x.Dispose() =
        color.RemoveOutput x
        depth.RemoveOutput x

    interface IRenderTask with
        member x.FramebufferSignature = fboSignature
        member x.Runtime = runtime |> Some
        member x.Run(caller, fbo) =
            x.Run(caller, fbo)

        member x.Dispose() =
            x.Dispose()

        member x.FrameId = frameId
