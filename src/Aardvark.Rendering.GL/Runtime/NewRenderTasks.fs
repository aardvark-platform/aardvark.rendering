namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Collections.Generic
open Aardvark.Base
open Aardvark.Base.Runtime
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

[<AbstractClass>]
type AdaptiveProgramRenderTask(objects : aset<IRenderObject>, manager : ResourceManager, fboSignature : IFramebufferSignature, config : BackendConfiguration) as this =
    inherit AbstractRenderTask(
        manager.Context,
        fboSignature, 
        config.useDebugOutput
    )

    let ctx = manager.Context
    let prepared = Dictionary<IRenderObject, PreparedRenderObject>()

    let prepareRenderObject (ro : IRenderObject) =
        match ro with
            | :? RenderObject as r ->
                manager.Prepare(fboSignature, r)

            | :? PreparedRenderObject as prep ->
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
    let mutable oneTimeStatistics = FrameStatistics.Zero

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

    let mutable hasProgram = false
    let mutable program = Unchecked.defaultof<_> // ( this.CreateProgram preparedObjects)

    let executionTime = System.Diagnostics.Stopwatch()

    member x.SetHandler (handler : unit -> FragmentHandler<unit, PreparedRenderObject, Instruction, 'a>) =
//        let handlerWithStats () =
//            let baseHandler = handler()
//            {
//                baseHandler with
//                    compileDelta = fun l r ->
//                        let code = baseHandler.compileDelta l r
//                        code
//            }
        
        hasProgram <- true
        program <- preparedObjects |> AdaptiveProgram.custom Comparer.Default handler

    member x.CurrentContext =
        currentContext :> IMod<_>

    member x.AddInput(i : IAdaptiveObject) =
        inputSet.Add i

    member x.RemoveInput(i : IAdaptiveObject) =
        inputSet.Remove i

    member x.AddOneTimeStats (f : FrameStatistics) =
        oneTimeStatistics <- oneTimeStatistics + f

    member x.AddStats (f : FrameStatistics) =
        frameStatistics <- frameStatistics + f

    member x.RemoveStats (f : FrameStatistics) = 
        frameStatistics <- frameStatistics - f

    abstract member Init : unit -> unit
    default x.Init() = ()
    abstract member AdjustStatistics : FrameStatistics -> FrameStatistics
    default x.AdjustStatistics a = a

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

        { FrameStatistics.Zero with 
            ResourceUpdateCount = float (dirtyPoolCount + viewUpdateCount)
            ResourceUpdateTime = time
            ResourceUpdateCounts = 
                Map.ofList [
                    ResourceKind.UniformBufferView, float viewUpdateCount
                    ResourceKind.UniformBuffer, float dirtyPoolCount
            ]
        }

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

        { stats with
            ResourceUpdateCount = stats.ResourceUpdateCount + float count
            ResourceUpdateCounts = counts
            ResourceUpdateTime = updateCPUTime.Elapsed + updateGPUTime.Elapsed
        }



    override x.Run(fbo) =
        if not hasProgram then 
            x.Init()
            hasProgram <- true

        if currentContext.UnsafeCache <> ctx.CurrentContextHandle.Value then
            transact (fun () -> Mod.change currentContext ctx.CurrentContextHandle.Value)

        let mutable stats = oneTimeStatistics
        oneTimeStatistics <- FrameStatistics.Zero

        stats <- stats + x.UpdateDirtyResources()

        if hasProgram then
            let programUpdateStats = program.Update x
            stats <- { 
                stats with 
                    AddedRenderObjects = float programUpdateStats.AddedFragmentCount
                    RemovedRenderObjects = float programUpdateStats.RemovedFragmentCount
                    InstructionUpdateCount = 0.0 // TODO!!
                    InstructionUpdateTime = 
                        programUpdateStats.DeltaProcessTime +
                        programUpdateStats.WriteTime +
                        programUpdateStats.CompileTime
            }

        stats <- stats + x.UpdateDirtyUniformBufferViews()

        executionTime.Restart()
        if hasProgram then
            program.Run()
        GL.Sync()
        executionTime.Stop()

        stats <- stats + frameStatistics

        x.AdjustStatistics { 
            stats with 
                ResourceCount = float inputSet.Resources.Count 
                ExecutionTime = executionTime.Elapsed
        }

    override x.Dispose() =
        program.Dispose()


module private GLFragmentHandlers =
    open System.Threading.Tasks

    let instructionToCall (i : Instruction) : NativeCall =
        let compiled = ExecutionContext.compile i
        compiled.functionPointer, compiled.args
 

    let nativeOptimized (compileDelta : Option<PreparedRenderObject> -> PreparedRenderObject -> IAdaptiveCode<Instruction>) =
        let inner = FragmentHandler.native 6
        FragmentHandler.warpDifferential instructionToCall compileDelta inner

    let nativeUnoptimized (compile : PreparedRenderObject -> IAdaptiveCode<Instruction>) =
        let inner = FragmentHandler.native 6
        FragmentHandler.wrapSimple instructionToCall compile inner


    let private glvmBase (mode : VMMode) (vmStats : ref<VMStats>) (compileDelta : Option<PreparedRenderObject> -> PreparedRenderObject -> IAdaptiveCode<Instruction>) () =
        GLVM.vmInit()

        let prolog = GLVM.vmCreate()
        let epilog = GLVM.vmCreate()

        let getArgs (o : Instruction) =
            o.Arguments |> Array.map (fun arg ->
                match arg with
                    | :? int as i -> nativeint i
                    | :? nativeint as i -> i
                    | :? float32 as f -> BitConverter.ToInt32(BitConverter.GetBytes(f), 0) |> nativeint
                    | _ -> failwith "invalid argument"
            )

        let appendToBlock (frag : FragmentPtr) (id : int) (instructions : seq<Instruction>) =
            for i in instructions do
                match getArgs i with
                    | [| a |] -> GLVM.vmAppend1(frag, id, i.Operation, a)
                    | [| a; b |] -> GLVM.vmAppend2(frag, id, i.Operation, a, b)
                    | [| a; b; c |] -> GLVM.vmAppend3(frag, id, i.Operation, a, b, c)
                    | [| a; b; c; d |] -> GLVM.vmAppend4(frag, id, i.Operation, a, b, c, d)
                    | [| a; b; c; d; e |] -> GLVM.vmAppend5(frag, id, i.Operation, a, b, c, d, e)
                    | _ -> failwithf "invalid instruction: %A" i

        {
            compileNeedsPrev = true
            nativeCallCount = ref 0
            jumpDistance = ref 0
            prolog = prolog
            epilog = epilog
            compileDelta = compileDelta
            startDefragmentation = fun _ _ _ -> Task.FromResult TimeSpan.Zero
            run = fun() -> 
                GLVM.vmRun(prolog, mode, &vmStats.contents)
            memorySize = fun () -> 0L
            alloc = fun code -> 
                let ptr = GLVM.vmCreate()
                let id = GLVM.vmNewBlock ptr
                appendToBlock ptr id code
                ptr
            free = GLVM.vmDelete
            write = fun ptr code ->
                GLVM.vmClear ptr
                let id = GLVM.vmNewBlock ptr
                appendToBlock ptr id code
                false

            writeNext = fun prev next -> GLVM.vmLink(prev, next); 0
            isNext = fun prev frag -> GLVM.vmGetNext prev = frag
            dispose = fun () -> GLVM.vmDelete prolog; GLVM.vmDelete epilog
        }

    let glvmOptimized (vmStats : ref<VMStats>) (compile : Option<PreparedRenderObject> -> PreparedRenderObject -> IAdaptiveCode<Instruction>) () =
        glvmBase VMMode.None vmStats compile ()

    let glvmRuntime (vmStats : ref<VMStats>) (compile : PreparedRenderObject -> IAdaptiveCode<Instruction>) () =
        { glvmBase VMMode.RuntimeRedundancyChecks vmStats (fun _ v -> compile v) () with compileNeedsPrev = false }

    let glvmUnoptimized (vmStats : ref<VMStats>) (compile : PreparedRenderObject -> IAdaptiveCode<Instruction>) () =
        { glvmBase VMMode.None vmStats (fun _ v -> compile v) () with compileNeedsPrev = false }

    [<AllowNullLiteral>]
    type ManagedFragment =
        class
            val mutable public Next : ManagedFragment
            val mutable public Instructions : Instruction[]

            new(next, instructions) = { Next = next; Instructions = instructions }
            new(instructions) = { Next = null; Instructions = instructions }
        end

    let managedOptimized (compileDelta : Option<PreparedRenderObject> -> PreparedRenderObject -> IAdaptiveCode<Instruction>) () =
        let prolog = ManagedFragment [||]
        let epilog = ManagedFragment [||]

        let run (f : ManagedFragment) =
            let rec all (f : ManagedFragment) =
                if isNull f then 
                    Seq.empty
                else
                    seq {
                        yield f.Instructions
                        yield! all f.Next
                    }

            let all = all f
            for part in all do
                for i in part do
                    ExecutionContext.debug i

        {
            compileNeedsPrev = true
            nativeCallCount = ref 0
            jumpDistance = ref 0
            prolog = prolog
            epilog = epilog
            compileDelta = compileDelta
            startDefragmentation = fun _ _ _ -> Task.FromResult TimeSpan.Zero
            run = fun () -> run prolog
            memorySize = fun () -> 0L
            alloc = fun code -> ManagedFragment(code)
            free = ignore
            write = fun ptr code -> ptr.Instructions <- code; false
            writeNext = fun prev next -> prev.Next <- next; 0
            isNext = fun prev frag -> prev.Next = frag
            dispose = fun () -> ()
        }  

    let managedUnoptimized (compile : PreparedRenderObject -> IAdaptiveCode<Instruction>) () =
        { managedOptimized (fun _ v -> compile v) () with compileNeedsPrev = false }


type NewRenderTask(objects : aset<IRenderObject>, manager : ResourceManager, fboSignature : IFramebufferSignature, config : BackendConfiguration) =
    inherit AdaptiveProgramRenderTask(
        objects,
        manager,
        fboSignature, 
        config
    )

    let vmStats = ref <| VMStats()

    let compileDelta (this : NewRenderTask) (left : Option<PreparedRenderObject>) (right : PreparedRenderObject) =
        
        let code, createStats =
            match left with
                | Some left -> Aardvark.Rendering.GL.Compiler.DeltaCompiler.compileDelta manager this.CurrentContext left right
                | None -> Aardvark.Rendering.GL.Compiler.DeltaCompiler.compileFull manager this.CurrentContext right

        for r in code.Resources do
            this.AddInput r

        this.AddOneTimeStats createStats

        let mutable stats = FrameStatistics.Zero

        let calls =
            code.Instructions
                |> List.map (fun i ->
                    match i with
                        | FixedInstruction i -> 
                            let dStats = List.sumBy InstructionStatistics.toStats i
                            stats <- stats + dStats
                            this.AddStats dStats

                            Mod.constant i

                        | AdaptiveInstruction i -> 
                            let mutable oldStats = FrameStatistics.Zero
                            i |> Mod.map (fun i -> 
                                let newStats = List.sumBy InstructionStatistics.toStats i
                                let dStats = newStats - oldStats
                                stats <- stats + newStats - oldStats
                                this.AddStats dStats

                                oldStats <- newStats
                                i
                            )
                )


        { new Aardvark.Base.Runtime.IAdaptiveCode<Instruction> with
            member x.Content = calls
            member x.Dispose() =
                for r in code.Resources do
                    this.RemoveInput r
                    r.Dispose()

                this.RemoveStats stats

        }

    let compile (this : NewRenderTask) (right : PreparedRenderObject) =
        compileDelta this None right

    override x.Init() =
        match config.execution, config.redundancy with
            | ExecutionEngine.Native, RedundancyRemoval.Static ->
                Log.line "using optimized native program"
                x.SetHandler(GLFragmentHandlers.nativeOptimized (compileDelta x))

            | ExecutionEngine.Native, RedundancyRemoval.None ->
                Log.line "using unoptimized native program"
                x.SetHandler(GLFragmentHandlers.nativeUnoptimized (compile x))

            | ExecutionEngine.Native, RedundancyRemoval.Runtime ->
                Log.line "using unoptimized native program"
                x.SetHandler(GLFragmentHandlers.nativeUnoptimized (compile x))


            | ExecutionEngine.Unmanaged, RedundancyRemoval.Static ->
                Log.line "using optimized GLVM program"
                x.SetHandler(GLFragmentHandlers.glvmOptimized vmStats (compileDelta x))

            | ExecutionEngine.Unmanaged, RedundancyRemoval.None ->
                Log.line "using unoptimized GLVM program"
                x.SetHandler(GLFragmentHandlers.glvmUnoptimized vmStats (compile x))

            | ExecutionEngine.Unmanaged, RedundancyRemoval.Runtime ->
                Log.line "using runtime-optimized GLVM program"
                x.SetHandler(GLFragmentHandlers.glvmRuntime vmStats (compile x))


            | ExecutionEngine.Managed, RedundancyRemoval.Static ->
                Log.line "using optimized managed program"
                x.SetHandler(GLFragmentHandlers.managedOptimized (compileDelta x))

            | ExecutionEngine.Managed, RedundancyRemoval.None ->
                Log.line "using unoptimized managed program"
                x.SetHandler(GLFragmentHandlers.managedUnoptimized (compile x))

            | ExecutionEngine.Managed, RedundancyRemoval.Runtime ->
                Log.line "using unoptimized managed program"
                x.SetHandler(GLFragmentHandlers.managedUnoptimized (compile x))

            | _ ->
                failwithf "unknown backend configuration: %A/%A" config.execution config.redundancy

    override x.AdjustStatistics(stats) =
        { stats with
            ActiveInstructionCount = stats.ActiveInstructionCount - float vmStats.Value.RemovedInstructions
        }