namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Collections.Generic
open Aardvark.Base
open Aardvark.Base.Incremental
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL.Compiler
open System.Runtime.CompilerServices

[<AbstractClass>]
type AbstractRenderTask(ctx : Context, fboSignature : IFramebufferSignature, debug : bool) =
    inherit AdaptiveObject()

    let mutable debug = debug
    let mutable frameId = 0UL

    let pushDebugOutput() =
        let wasEnabled = GL.IsEnabled EnableCap.DebugOutput
        if debug then
            match ContextHandle.Current with
                | Some v -> v.AttachDebugOutputIfNeeded()
                | None -> Report.Warn("No active context handle in RenderTask.Run")
            GL.Enable EnableCap.DebugOutput

        wasEnabled

    let popDebugOutput(wasEnabled : bool) =
        if wasEnabled <> debug then
            if wasEnabled then GL.Enable EnableCap.DebugOutput
            else GL.Disable EnableCap.DebugOutput

    let pushFbo (fbo : Framebuffer) =
        let old = Array.create 4 0
        let mutable oldFbo = 0
        OpenTK.Graphics.OpenGL.GL.GetInteger(OpenTK.Graphics.OpenGL.GetPName.Viewport, old)
        OpenTK.Graphics.OpenGL.GL.GetInteger(OpenTK.Graphics.OpenGL.GetPName.FramebufferBinding, &oldFbo)

        let handle = fbo.Handle |> unbox<int> 

        if ExecutionContext.framebuffersSupported then
            GL.BindFramebuffer(OpenTK.Graphics.OpenGL4.FramebufferTarget.Framebuffer, handle)
            let drawBuffers = Array.init fbo.Attachments.Count (fun i -> int DrawBuffersEnum.ColorAttachment0 + i |> unbox<DrawBuffersEnum>)
            GL.DrawBuffers(drawBuffers.Length, drawBuffers)
            GL.Check "could not bind framebuffer"
        elif handle <> 0 then
            failwithf "cannot render to texture on this OpenGL driver"


        GL.Viewport(0, 0, fbo.Size.X, fbo.Size.Y)
        GL.Check "could not set viewport"


        oldFbo, old

    let popFbo (oldFbo : int, old : int[]) =
        if ExecutionContext.framebuffersSupported then
            GL.BindFramebuffer(OpenTK.Graphics.OpenGL4.FramebufferTarget.Framebuffer, oldFbo)
            GL.Check "could not bind framebuffer"

        GL.Viewport(old.[0], old.[1], old.[2], old.[3])
        GL.Check "could not set viewport"

    member x.Run(caller : IAdaptiveObject, fbo : IFramebuffer) =
        x.EvaluateAlways caller (fun () ->
            use token = ctx.ResourceLock 
            let fbo =
                match fbo with
                    | :? Framebuffer as fbo -> fbo
                    | _ -> failwithf "unsupported framebuffer: %A" fbo

            let debugState = pushDebugOutput()
            let fboState = pushFbo fbo


            let stats = x.Run fbo

            popFbo fboState
            popDebugOutput debugState

            frameId <- frameId + 1UL
            RenderingResult(fbo, stats)
        )

    abstract member Run : Framebuffer -> FrameStatistics
    abstract member Dispose : unit -> unit

    member x.Runtime = ctx.Runtime
    member x.FramebufferSignature = fboSignature
    member x.FrameId = frameId

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface IRenderTask with
        member x.Runtime = Some ctx.Runtime
        member x.FramebufferSignature = fboSignature
        member x.Run(caller, fbo) = x.Run(caller, fbo)
        member x.FrameId = frameId


type OptimizedNativeRenderTask(objects : aset<IRenderObject>, manager : ResourceManager, fboSignature : IFramebufferSignature, config : BackendConfiguration) as this =
    inherit AbstractRenderTask(
        manager.Context,
        fboSignature, 
        config.useDebugOutput
    )

    let ctx = manager.Context

    let prepareRenderObject (ro : IRenderObject) =
        match ro with
            | :? RenderObject as r ->
                manager.Prepare(fboSignature, r)
            | :? PreparedRenderObject as prep ->
                // TODO: increase refCount for all resources
                prep
            | _ ->
                failwithf "[RenderTask] unsupported IRenderObject: %A" ro

    
    let dirtyLock = obj()
    let mutable dirtyUniformViews = HashSet<ChangeableResource<UniformBufferView>>()
    let mutable dirtyResources = HashSet<IChangeableResource>()
    let mutable dirtyPoolIds = Array.empty
    let inputSet = RenderTaskInputSet(this) 
    let currentContext = Mod.init Unchecked.defaultof<ContextHandle>

    let updateCPUTime = System.Diagnostics.Stopwatch()
    let updateGPUTime = System.Diagnostics.Stopwatch()
    let executionTime = System.Diagnostics.Stopwatch()

    let mutable frameStatistics = FrameStatistics.Zero


    let instructionToCall (i : Instruction) : NativeCall =
        let compiled = ExecutionContext.compile i
        compiled.functionPointer, compiled.args

    let compileDelta (left : Option<PreparedRenderObject>) (right : PreparedRenderObject) =
        
        let code, stats =
            match left with
                | Some left -> Aardvark.Rendering.GL.Compiler.DeltaCompiler.compileDelta manager currentContext left right
                | None -> Aardvark.Rendering.GL.Compiler.DeltaCompiler.compileFull manager currentContext right

        for r in code.Resources do
            inputSet.Add r

        let mutable stats = stats

        let calls =
            code.Instructions
                |> List.map (fun i ->
                    match i with
                        | FixedInstruction i -> 
                            stats <- stats + List.sumBy InstructionStatistics.toStats i
                            Mod.constant (i |> List.map instructionToCall)

                        | AdaptiveInstruction i -> 
                            let mutable oldStats = FrameStatistics.Zero
                            i |> Mod.map (fun i -> 
                                let newStats = List.sumBy InstructionStatistics.toStats i
                                stats <- stats - oldStats + newStats
                                oldStats <- newStats
                                i |> List.map instructionToCall
                            )
                )

        frameStatistics <- frameStatistics + stats

        { new Aardvark.Base.Runtime.AdaptiveCode(calls) with
            override x.Dispose() =
                base.Dispose()
                for r in code.Resources do
                    inputSet.Remove r
                    r.Dispose()

                frameStatistics <- frameStatistics - stats

        }

    let mutable currentId = 0L
    let idCache = ConditionalWeakTable<IMod, ref<uint64>>()

    let getId (m : IMod) =
        match idCache.TryGetValue m with
            | (true, r) -> !r
            | _ ->
                let r = ref (Interlocked.Increment &currentId |> uint64)
                idCache.Add(m, r)
                !r

    let createSortKey (prep : PreparedRenderObject) =
        match config.sorting with
            | Grouping projections ->
                let ids = projections |> List.map (fun f -> f prep.Original |> getId)
                prep.RenderPass::ids

            | _ ->
                failwithf "[RenderTask] unsupported sorting: %A" config.sorting

       
    // TODO: find a way to destroy sortKeys appropriately without using ConditionalWeakTable
    let preparedObjects = objects |> ASet.mapUse prepareRenderObject |> ASet.map (fun prep -> createSortKey prep, prep)

    let program = Runtime.AdaptiveProgram.differential 6 Comparer.Default compileDelta (AMap.ofASet preparedObjects)

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
        let mutable count = 0 
        let counts = Dictionary<ResourceKind, ref<int>>()


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



    override x.Run(fbo) =
        if currentContext.UnsafeCache <> ctx.CurrentContextHandle.Value then
            transact (fun () -> Mod.change currentContext ctx.CurrentContextHandle.Value)

        program.Update x |> ignore

        x.UpdateDirtyResources() |> ignore

        x.UpdateDirtyUniformBufferViews() |> ignore

        program.Run()


        FrameStatistics.Zero

    override x.Dispose() =
        program.Dispose()
        ()