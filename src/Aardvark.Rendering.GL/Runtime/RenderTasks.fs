namespace Aardvark.Rendering.GL

open System
open System.Linq
open System.Threading
open System.Collections.Generic
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Runtime
open Aardvark.Base.Incremental
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL.Compiler
open System.Runtime.CompilerServices

[<AbstractClass>]
type AbstractRenderTask(ctx : Context, fboSignature : IFramebufferSignature, renderTaskLock : RenderTaskLock, debug : bool) =
    inherit AdaptiveObject()
    let currentContext = Mod.init Unchecked.defaultof<ContextHandle>

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

    let pushFbo (desc : OutputDescription) =
        let fbo = desc.framebuffer |> unbox<Framebuffer>
        let old = Array.create 4 0
        let mutable oldFbo = 0
        OpenTK.Graphics.OpenGL.GL.GetInteger(OpenTK.Graphics.OpenGL.GetPName.Viewport, old)
        OpenTK.Graphics.OpenGL.GL.GetInteger(OpenTK.Graphics.OpenGL.GetPName.FramebufferBinding, &oldFbo)

        let handle = fbo.Handle |> unbox<int> 

        if ExecutionContext.framebuffersSupported then
            GL.BindFramebuffer(OpenTK.Graphics.OpenGL4.FramebufferTarget.Framebuffer, handle)
            GL.Check "could not bind framebuffer"
            let attachmentCount = fbo.Attachments.Count
            let drawBuffers = Array.init attachmentCount (fun i -> int DrawBuffersEnum.ColorAttachment0 + i |> unbox<DrawBuffersEnum>)
            GL.DepthMask(true)

            for (index,(sem,_)) in fbo.Signature.ColorAttachments |> Map.toSeq do
                match Map.tryFind sem desc.colorWrite with
                    | Some v -> 
                        GL.ColorMask(
                            index, 
                            (v &&& ColorWriteMask.Red)   <> ColorWriteMask.None, 
                            (v &&& ColorWriteMask.Green) <> ColorWriteMask.None,
                            (v &&& ColorWriteMask.Blue)  <> ColorWriteMask.None, 
                            (v &&& ColorWriteMask.Alpha) <> ColorWriteMask.None
                        )
                    | None -> GL.ColorMask(index, true, true, true, true)

            GL.DrawBuffers(drawBuffers.Length, drawBuffers)
            GL.Check "DrawBuffers errored"
        elif handle <> 0 then
            failwithf "cannot render to texture on this OpenGL driver"

        GL.Viewport(desc.viewport.Min.X, desc.viewport.Min.Y, desc.viewport.SizeX, desc.viewport.SizeY)
        GL.Check "could not set viewport"

       

        oldFbo, old

    let popFbo (oldFbo : int, old : int[]) =
        if ExecutionContext.framebuffersSupported then
            GL.BindFramebuffer(OpenTK.Graphics.OpenGL4.FramebufferTarget.Framebuffer, oldFbo)
            GL.Check "could not bind framebuffer"

        GL.Viewport(old.[0], old.[1], old.[2], old.[3])
        GL.Check "could not set viewport"

    member x.CurrentContext =
        currentContext :> IMod<_>

    member x.Run(caller : IAdaptiveObject, desc : OutputDescription) =
        let fbo = desc.framebuffer // TODO: fix outputdesc
        if not <| fboSignature.IsAssignableFrom fbo.Signature then
            failwithf "incompatible FramebufferSignature\nexpected: %A but got: %A" fboSignature fbo.Signature

        x.EvaluateAlways caller (fun () ->
            use token = ctx.ResourceLock 
            if currentContext.UnsafeCache <> ctx.CurrentContextHandle.Value then
                transact (fun () -> Mod.change currentContext ctx.CurrentContextHandle.Value)

            let fbo =
                match fbo with
                    | :? Framebuffer as fbo -> fbo
                    | _ -> failwithf "unsupported framebuffer: %A" fbo


            let debugState = pushDebugOutput()
            let fboState = pushFbo desc

            let stats = renderTaskLock.Run (fun () -> 
                x.Run fbo
            )

            popFbo fboState
            popDebugOutput debugState

            GL.BindVertexArray 0
            GL.BindBuffer(BufferTarget.DrawIndirectBuffer,0)
            

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
type AbstractRenderTaskWithResources(manager : ResourceManager, fboSignature : IFramebufferSignature, renderTaskLock : RenderTaskLock, debug : bool) as this =
    inherit AbstractRenderTask(manager.Context, fboSignature, renderTaskLock, debug)

    let ctx = manager.Context

    let resources = new Aardvark.Base.Rendering.ResourceInputSet()
    let inputSet = InputSet(this) 

    let updateResourceTime = System.Diagnostics.Stopwatch()
    let executionTime = System.Diagnostics.Stopwatch()

    let mutable frameStatistics = FrameStatistics.Zero
    let mutable oneTimeStatistics = FrameStatistics.Zero

    let taskScheduler = new CustomTaskScheduler(Environment.ProcessorCount)
    let threadOpts = System.Threading.Tasks.ParallelOptions(TaskScheduler = taskScheduler)

    member x.Manager = manager

    member x.AddInput(i : IAdaptiveObject) =
        match i with
            | :? IResource as r -> resources.Add r
            | _ -> inputSet.Add i

    member x.RemoveInput(i : IAdaptiveObject) =
        match i with
            | :? IResource as r -> resources.Remove r
            | _ -> inputSet.Remove i

    member x.AddOneTimeStats (f : FrameStatistics) =
        oneTimeStatistics <- oneTimeStatistics + f

    member x.AddStats (f : FrameStatistics) =
        frameStatistics <- frameStatistics + f

    member x.RemoveStats (f : FrameStatistics) = 
        frameStatistics <- frameStatistics - f

    member x.UpdateDirtyResources(level : int) =

        updateResourceTime.Restart()
        let stats = resources.Update(x)

     
        if level = 0 && Config.SyncUploadsAndFrames && stats.ResourceUpdateCount > 0.0 then
            OpenTK.Graphics.OpenGL4.GL.Sync()

        updateResourceTime.Stop()

        { stats with
            ResourceUpdateTime = updateResourceTime.Elapsed
        }


    member x.GetStats() =
        let res = oneTimeStatistics + frameStatistics
        oneTimeStatistics <- FrameStatistics.Zero
        { res with
            ResourceCount = float resources.Count 
        }


module private RenderTaskUtilities =
    
    let compileDelta (this : AbstractRenderTaskWithResources) (left : Option<PreparedMultiRenderObject>) (right : PreparedMultiRenderObject) =
        
        let mutable last =
            match left with
                | Some left -> Some left.Last
                | None -> None

        let code = 
            [ for r in right.Children do
                match last with
                    | Some last -> yield! Aardvark.Rendering.GL.Compiler.DeltaCompiler.compileDelta this.CurrentContext last r
                    | None -> yield! Aardvark.Rendering.GL.Compiler.DeltaCompiler.compileFull this.CurrentContext r
                last <- Some r
            ]

        let mutable stats = FrameStatistics.Zero

        let calls =
            code
                |> List.map (fun i ->
                    match i.IsConstant with
                        | true -> 
                            let i = i.GetValue()
                            let dStats = List.sumBy InstructionStatistics.toStats i
                            stats <- stats + dStats
                            this.AddStats dStats

                            Mod.constant i

                        | false -> 
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
                for o in code do
                    for i in o.Inputs do
                        i.RemoveOutput o

                this.RemoveStats stats

        }

    let compile (this : AbstractRenderTaskWithResources) (right : PreparedMultiRenderObject) =
        compileDelta this None right


module GroupedRenderTask =

    module private GLFragmentHandlers =
        open System.Threading.Tasks

        let instructionToCall (i : Instruction) : NativeCall =
            let compiled = ExecutionContext.compile i
            compiled.functionPointer, compiled.args
 

        let nativeOptimized (compileDelta : Option<PreparedMultiRenderObject> -> PreparedMultiRenderObject -> IAdaptiveCode<Instruction>) =
            let inner = FragmentHandler.native 6
            FragmentHandler.warpDifferential instructionToCall ExecutionContext.callToInstruction compileDelta inner

        let nativeUnoptimized (compile : PreparedMultiRenderObject -> IAdaptiveCode<Instruction>) =
            let inner = FragmentHandler.native 6
            FragmentHandler.wrapSimple instructionToCall ExecutionContext.callToInstruction compile inner


        let private glvmBase (mode : VMMode) (vmStats : ref<VMStats>) (compileDelta : Option<PreparedMultiRenderObject> -> PreparedMultiRenderObject -> IAdaptiveCode<Instruction>) () =
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
                disassemble = fun f -> []
            }

        let glvmOptimized (vmStats : ref<VMStats>) (compile : Option<PreparedMultiRenderObject> -> PreparedMultiRenderObject -> IAdaptiveCode<Instruction>) () =
            glvmBase VMMode.None vmStats compile ()

        let glvmRuntime (vmStats : ref<VMStats>) (compile : PreparedMultiRenderObject -> IAdaptiveCode<Instruction>) () =
            { glvmBase VMMode.RuntimeRedundancyChecks vmStats (fun _ v -> compile v) () with compileNeedsPrev = false }

        let glvmUnoptimized (vmStats : ref<VMStats>) (compile : PreparedMultiRenderObject -> IAdaptiveCode<Instruction>) () =
            { glvmBase VMMode.None vmStats (fun _ v -> compile v) () with compileNeedsPrev = false }

        [<AllowNullLiteral>]
        type ManagedFragment =
            class
                val mutable public Next : ManagedFragment
                val mutable public Instructions : Instruction[]

                new(next, instructions) = { Next = next; Instructions = instructions }
                new(instructions) = { Next = null; Instructions = instructions }
            end

        let managedOptimized (compileDelta : Option<PreparedMultiRenderObject> -> PreparedMultiRenderObject -> IAdaptiveCode<Instruction>) () =
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
                disassemble = fun f -> f.Instructions |> Array.toList
            }  

        let managedUnoptimized (compile : PreparedMultiRenderObject -> IAdaptiveCode<Instruction>) () =
            { managedOptimized (fun _ v -> compile v) () with compileNeedsPrev = false }


    type RenderTask(objects : aset<IRenderObject>, manager : ResourceManager, fboSignature : IFramebufferSignature, config : IMod<BackendConfiguration>) as this =
        inherit AbstractRenderTaskWithResources(
            manager,
            fboSignature, 
            manager.RenderTaskLock.Value,       // manager for tasks requires 
            config.GetValue().useDebugOutput
        )

        let ctx = manager.Context


        let add (ro : PreparedRenderObject) = 
            let all = ro.Resources |> Seq.toList
            for r in all do this.AddInput(r)

            let old = ro.Activation
            ro.Activation <- 
                { new IDisposable with
                    member x.Dispose() =
                        old.Dispose()
                        for r in all do this.RemoveInput(r)
                }

            ro

        let rec prepareRenderObject (ro : IRenderObject) =
            match ro with
                | :? RenderObject as r ->
                    new PreparedMultiRenderObject([manager.Prepare(fboSignature, r) |> add])

                | :? PreparedRenderObject as prep ->
                    new PreparedMultiRenderObject([prep |> PreparedRenderObject.clone |> add])

                | :? MultiRenderObject as seq ->
                    let all = seq.Children |> List.collect(fun o -> (prepareRenderObject o).Children)
                    new PreparedMultiRenderObject(all)

                | :? PreparedMultiRenderObject as seq ->
                    new PreparedMultiRenderObject (seq.Children |> List.map (PreparedRenderObject.clone >> add))

                | _ ->
                    failwithf "[RenderTask] unsupported IRenderObject: %A" ro

        let mutable currentId = 0L
        let idCache = ConditionalWeakTable<IMod, ref<uint64>>()

        let mutable currentConfig = config.GetValue()

        let getId (m : IMod) =
            match idCache.TryGetValue m with
                | (true, r) -> !r :> IComparable
                | _ ->
                    let r = ref (Interlocked.Increment &currentId |> uint64)
                    idCache.Add(m, r)
                    !r :> IComparable

        let createSortKey (prep : PreparedMultiRenderObject) =
            match config.GetValue().sorting with
                | Grouping projections ->
                    let ids = projections |> List.map (fun f -> f prep.Original |> getId)
                    (prep.RenderPass :> IComparable)::ids

                | s ->
                    failwithf "[RenderTask] unsupported sorting: %A" s

        let vmStats = ref <| VMStats()

       
        // TODO: find a way to destroy sortKeys appropriately without using ConditionalWeakTable
        let preparedObjects = objects |> ASet.mapUse prepareRenderObject |> ASet.map (fun prep -> createSortKey prep, prep)

        let mutable hasProgram = false
        let mutable program : IAdaptiveProgram<unit> = Unchecked.defaultof<_> // ( this.CreateProgram preparedObjects)

        let executionTime = System.Diagnostics.Stopwatch()

        let setHandler (handler : unit -> FragmentHandler<unit, PreparedMultiRenderObject, Instruction, 'a>) =
            if hasProgram then
                program.Dispose()

            hasProgram <- true
            program <- preparedObjects |> AdaptiveProgram.custom Comparer.Default handler
            program.AutoDefragmentation <- false

        let init(x : AbstractRenderTaskWithResources) =
            let config = config.GetValue(x)
            currentConfig <- config
            match config.execution, config.redundancy with
                | ExecutionEngine.Native, RedundancyRemoval.Static ->
                    Log.line "using optimized native program"
                    setHandler(GLFragmentHandlers.nativeOptimized (RenderTaskUtilities.compileDelta x))

                | ExecutionEngine.Native, RedundancyRemoval.None ->
                    Log.line "using unoptimized native program"
                    setHandler(GLFragmentHandlers.nativeUnoptimized (RenderTaskUtilities.compile x))

                | ExecutionEngine.Native, RedundancyRemoval.Runtime ->
                    Log.line "using unoptimized native program"
                    setHandler(GLFragmentHandlers.nativeUnoptimized (RenderTaskUtilities.compile x))


                | ExecutionEngine.Unmanaged, RedundancyRemoval.Static ->
                    Log.line "using optimized GLVM program"
                    setHandler(GLFragmentHandlers.glvmOptimized vmStats (RenderTaskUtilities.compileDelta x))

                | ExecutionEngine.Unmanaged, RedundancyRemoval.None ->
                    Log.line "using unoptimized GLVM program"
                    setHandler(GLFragmentHandlers.glvmUnoptimized vmStats (RenderTaskUtilities.compile x))

                | ExecutionEngine.Unmanaged, RedundancyRemoval.Runtime ->
                    Log.line "using runtime-optimized GLVM program"
                    setHandler(GLFragmentHandlers.glvmRuntime vmStats (RenderTaskUtilities.compile x))


                | ExecutionEngine.Managed, RedundancyRemoval.Static ->
                    Log.line "using optimized managed program"
                    setHandler(GLFragmentHandlers.managedOptimized (RenderTaskUtilities.compileDelta x))

                | ExecutionEngine.Managed, RedundancyRemoval.None ->
                    Log.line "using unoptimized managed program"
                    setHandler(GLFragmentHandlers.managedUnoptimized (RenderTaskUtilities.compile x))

                | ExecutionEngine.Managed, RedundancyRemoval.Runtime ->
                    Log.line "using unoptimized managed program"
                    setHandler(GLFragmentHandlers.managedUnoptimized (RenderTaskUtilities.compile x))

                | _ ->
                    failwithf "unknown backend configuration: %A/%A" config.execution config.redundancy

        member x.Program = program


        override x.Run(fbo) =
            let c = config.GetValue(x)
            if not hasProgram || c <> currentConfig then 
                init x

            let mutable stats = FrameStatistics.Zero

            stats <- stats + x.UpdateDirtyResources(0)

            if hasProgram then
                let programUpdateStats = program.Update x
                stats <- stats + { 
                    FrameStatistics.Zero with 
                        AddedRenderObjects = float programUpdateStats.AddedFragmentCount
                        RemovedRenderObjects = float programUpdateStats.RemovedFragmentCount
                        InstructionUpdateCount = 0.0 // TODO!!
                        InstructionUpdateTime = 
                            programUpdateStats.DeltaProcessTime +
                            programUpdateStats.WriteTime +
                            programUpdateStats.CompileTime
                }

            executionTime.Restart()
            if hasProgram then
                program.Run()
            GL.Sync()
            executionTime.Stop()

            stats <- stats + x.GetStats()


            { stats with 
                ExecutionTime = executionTime.Elapsed 
                ActiveInstructionCount = stats.ActiveInstructionCount - float vmStats.Value.RemovedInstructions
            }

        override x.Dispose() =
            program.Dispose()


module YetAnotherRenderTaskImpl =
    open System.Collections.Generic

    type SortKey = RenderPass * list<int>

    type ProjectionComparer(projections : list<RenderObject -> IMod>) =

        let rec getRenderObject (ro : IRenderObject) =
            match ro with
                | :? RenderObject as ro -> ro
                | :? MultiRenderObject as ro -> ro.Children |> List.head |> getRenderObject
                | :? PreparedRenderObject as ro -> ro.Original
                | :? PreparedMultiRenderObject as ro -> ro.First.Original
                | _ -> failwithf "[ProjectionComparer] unknown RenderObject: %A" ro

        let ids = ConditionalWeakTable<IMod, ref<int>>()
        let mutable currentId = 0
        let getId (m : IMod) =
            match ids.TryGetValue m with
                | (true, r) -> !r
                | _ ->
                    let id = Interlocked.Increment &currentId
                    ids.Add(m, ref id)
                    id


        let keys = ConditionalWeakTable<IRenderObject, SortKey>()
        let project (ro : IRenderObject) =
            let ro = getRenderObject ro

            match keys.TryGetValue ro with
                | (true, key) -> key
                | _ ->
                    let projected = projections |> List.map (fun p -> p ro |> getId)
                    let pass = ro.RenderPass

                    let key = (pass, projected)
                    keys.Add(ro, key)
                    key


        interface IComparer<IRenderObject> with
            member x.Compare(l : IRenderObject, r : IRenderObject) =
                let left = project l
                let right = project r
                compare left right
 


    [<AbstractClass>]
    type AbstractRenderTask(ctx : Context, fboSignature : IFramebufferSignature, renderTaskLock : RenderTaskLock, config : IMod<BackendConfiguration>) as this =
        inherit AdaptiveObject()
        let currentContext = Mod.init Unchecked.defaultof<ContextHandle>

        let mutable frameId = 0UL
        let mutable stats = FrameStatistics.Zero

        let drawBuffers = 
            fboSignature.ColorAttachments 
                |> Map.toList 
                |> List.map (fun (i,_) -> int DrawBuffersEnum.ColorAttachment0 + i |> unbox<DrawBuffersEnum>)
                |> List.toArray

        let pushDebugOutput() =
            let wasEnabled = GL.IsEnabled EnableCap.DebugOutput
            let c = config.GetValue this
            if c.useDebugOutput then
                if frameId = 0UL then
                    Log.warn "debug output enabled"
                match ContextHandle.Current with
                    | Some v -> v.AttachDebugOutputIfNeeded()
                    | None -> Report.Warn("No active context handle in RenderTask.Run")
                GL.Enable EnableCap.DebugOutput

            wasEnabled

        let popDebugOutput(wasEnabled : bool) =
            let c = config.GetValue this
            if wasEnabled <> c.useDebugOutput then
                if wasEnabled then GL.Enable EnableCap.DebugOutput
                else GL.Disable EnableCap.DebugOutput

        let pushFbo (desc : OutputDescription) =
            let fbo = desc.framebuffer |> unbox<Framebuffer>
            let old = Array.create 4 0
            let mutable oldFbo = 0
            OpenTK.Graphics.OpenGL.GL.GetInteger(OpenTK.Graphics.OpenGL.GetPName.Viewport, old)
            OpenTK.Graphics.OpenGL.GL.GetInteger(OpenTK.Graphics.OpenGL.GetPName.FramebufferBinding, &oldFbo)

            let handle = fbo.Handle |> unbox<int> 

            if ExecutionContext.framebuffersSupported then
                GL.BindFramebuffer(OpenTK.Graphics.OpenGL4.FramebufferTarget.Framebuffer, handle)
                GL.Check "could not bind framebuffer"
        
                if handle <> 0 then
                    GL.DrawBuffers(drawBuffers.Length, drawBuffers)
                    GL.Check "DrawBuffers errored"


                GL.DepthMask(true)

                for (index,(sem,_)) in fbo.Signature.ColorAttachments |> Map.toSeq do
                    match Map.tryFind sem desc.colorWrite with
                        | Some v -> 
                            GL.ColorMask(
                                index, 
                                (v &&& ColorWriteMask.Red)   <> ColorWriteMask.None, 
                                (v &&& ColorWriteMask.Green) <> ColorWriteMask.None,
                                (v &&& ColorWriteMask.Blue)  <> ColorWriteMask.None, 
                                (v &&& ColorWriteMask.Alpha) <> ColorWriteMask.None
                            )
                        | None -> 
                            GL.ColorMask(index, true, true, true, true)


            elif handle <> 0 then
                failwithf "cannot render to texture on this OpenGL driver"

            GL.Viewport(desc.viewport.Min.X, desc.viewport.Min.Y, desc.viewport.SizeX, desc.viewport.SizeY)
            GL.Check "could not set viewport"

       

            oldFbo, old

        let popFbo (oldFbo : int, old : int[]) =
            if ExecutionContext.framebuffersSupported then
                GL.BindFramebuffer(OpenTK.Graphics.OpenGL4.FramebufferTarget.Framebuffer, oldFbo)
                GL.Check "could not bind framebuffer"

            GL.Viewport(old.[0], old.[1], old.[2], old.[3])
            GL.Check "could not set viewport"

        member x.AddStats (s : FrameStatistics) =
            stats <- stats + s

        member x.RemoveStats (s : FrameStatistics) =
            stats <- stats - s

        member x.CurrentContext =
            currentContext :> IMod<_>

        member x.Run(caller : IAdaptiveObject, desc : OutputDescription) =
            let fbo = desc.framebuffer // TODO: fix outputdesc
            if not <| fboSignature.IsAssignableFrom fbo.Signature then
                failwithf "incompatible FramebufferSignature\nexpected: %A but got: %A" fboSignature fbo.Signature

            x.EvaluateAlways caller (fun () ->
                use token = ctx.ResourceLock 
                if currentContext.UnsafeCache <> ctx.CurrentContextHandle.Value then
                    transact (fun () -> Mod.change currentContext ctx.CurrentContextHandle.Value)

                let fbo =
                    match fbo with
                        | :? Framebuffer as fbo -> fbo
                        | _ -> failwithf "unsupported framebuffer: %A" fbo


                let debugState = pushDebugOutput()
                let fboState = pushFbo desc

                let innerStats = 
                    renderTaskLock.Run (fun () -> 
                        x.Run fbo
                    )

                popFbo fboState
                popDebugOutput debugState

                

                GL.BindVertexArray 0
                GL.BindBuffer(BufferTarget.DrawIndirectBuffer,0)
            

                frameId <- frameId + 1UL
                RenderingResult(fbo, innerStats + stats)
            )

        abstract member Run : Framebuffer -> FrameStatistics
        abstract member Dispose : unit -> unit
        abstract member Add : PreparedMultiRenderObject -> unit
        abstract member Remove : PreparedMultiRenderObject -> unit

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




    module private Compiler =
        let compileDelta (this : AbstractRenderTask) (left : Option<PreparedMultiRenderObject>) (right : PreparedMultiRenderObject) =
        
            let mutable last =
                match left with
                    | Some left -> Some left.Last
                    | None -> None

            let code = 
                [ for r in right.Children do
                    match last with
                        | Some last -> yield! Aardvark.Rendering.GL.Compiler.DeltaCompiler.compileDelta this.CurrentContext last r
                        | None -> yield! Aardvark.Rendering.GL.Compiler.DeltaCompiler.compileFull this.CurrentContext r
                    last <- Some r
                ]

            let mutable stats = FrameStatistics.Zero

            let calls =
                code
                    |> List.map (fun i ->
                        match i.IsConstant with
                            | true -> 
                                let i = i.GetValue()
                                let dStats = List.sumBy InstructionStatistics.toStats i
                                stats <- stats + dStats
                                this.AddStats dStats

                                Mod.constant i

                            | false -> 
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
                    for o in code do
                        for i in o.Inputs do
                            i.RemoveOutput o

                    this.RemoveStats stats

            }

        let compile (this : AbstractRenderTask) (right : PreparedMultiRenderObject) =
            compileDelta this None right

    module private GLFragmentHandlers =
        open System.Threading.Tasks

        let instructionToCall (i : Instruction) : NativeCall =
            let compiled = ExecutionContext.compile i
            compiled.functionPointer, compiled.args
 

        let nativeOptimized (compileDelta : Option<PreparedMultiRenderObject> -> PreparedMultiRenderObject -> IAdaptiveCode<Instruction>) =
            let inner = FragmentHandler.native 6
            FragmentHandler.warpDifferential instructionToCall ExecutionContext.callToInstruction compileDelta inner

        let nativeUnoptimized (compile : PreparedMultiRenderObject -> IAdaptiveCode<Instruction>) =
            let inner = FragmentHandler.native 6
            FragmentHandler.wrapSimple instructionToCall ExecutionContext.callToInstruction compile inner


        let private glvmBase (mode : VMMode) (vmStats : ref<VMStats>) (compileDelta : Option<PreparedMultiRenderObject> -> PreparedMultiRenderObject -> IAdaptiveCode<Instruction>) () =
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
                disassemble = fun f -> []
            }

        let glvmOptimized (vmStats : ref<VMStats>) (compile : Option<PreparedMultiRenderObject> -> PreparedMultiRenderObject -> IAdaptiveCode<Instruction>) () =
            glvmBase VMMode.None vmStats compile ()

        let glvmRuntime (vmStats : ref<VMStats>) (compile : PreparedMultiRenderObject -> IAdaptiveCode<Instruction>) () =
            { glvmBase VMMode.RuntimeRedundancyChecks vmStats (fun _ v -> compile v) () with compileNeedsPrev = false }

        let glvmUnoptimized (vmStats : ref<VMStats>) (compile : PreparedMultiRenderObject -> IAdaptiveCode<Instruction>) () =
            { glvmBase VMMode.None vmStats (fun _ v -> compile v) () with compileNeedsPrev = false }

        [<AllowNullLiteral>]
        type ManagedFragment =
            class
                val mutable public Next : ManagedFragment
                val mutable public Instructions : Instruction[]

                new(next, instructions) = { Next = next; Instructions = instructions }
                new(instructions) = { Next = null; Instructions = instructions }
            end

        let managedOptimized (compileDelta : Option<PreparedMultiRenderObject> -> PreparedMultiRenderObject -> IAdaptiveCode<Instruction>) () =
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
                disassemble = fun f -> f.Instructions |> Array.toList
            }  

        let managedUnoptimized (compile : PreparedMultiRenderObject -> IAdaptiveCode<Instruction>) () =
            { managedOptimized (fun _ v -> compile v) () with compileNeedsPrev = false }
                   


    type StaticOrderRenderTask(ctx : Context, fboSignature : IFramebufferSignature, config : IMod<BackendConfiguration>) =
        inherit AbstractRenderTask(ctx, fboSignature, RenderTaskLock(), config)

        let objects = CSet.empty

        let mutable hasProgram = false
        let mutable currentConfig = BackendConfiguration.Default
        let mutable program : IAdaptiveProgram<unit> = Unchecked.defaultof<_>
        let vmStats = ref (VMStats())

        // TODO: add AdaptiveProgram creator not taking a separate key but simply comparing the values
        let objectsWithKeys = objects |> ASet.map (fun o -> (o :> IRenderObject, o))

        let reinit (self : StaticOrderRenderTask) (config : BackendConfiguration) =
            // if the config changed or we never compiled a program
            // we need to do something
            if config <> currentConfig || not hasProgram then
                vmStats := VMStats()

                // if we have a program we'll dispose it now
                if hasProgram then program.Dispose()

                // use the config to create a comparer for IRenderObjects
                let comparer =
                    match config.sorting with
                        | RenderObjectSorting.Grouping projections -> 
                            ProjectionComparer(projections) :> IComparer<_>

                        | RenderObjectSorting.Static comparer -> 
                            comparer

                        | RenderObjectSorting.Dynamic create ->
                            failwith "[AbstractRenderTask] dynamic sorting not implemented"

                // create the new program
                let newProgram = 
                    match config.execution, config.redundancy with

                        | ExecutionEngine.Native, RedundancyRemoval.Static -> 
                            Log.line "using optimized native program"
                            let handler = GLFragmentHandlers.nativeOptimized (Compiler.compileDelta self)
                            AdaptiveProgram.custom comparer handler objectsWithKeys

                        | ExecutionEngine.Native, RedundancyRemoval.None -> 
                            Log.line "using unoptimized native program"
                            let handler = GLFragmentHandlers.nativeUnoptimized (Compiler.compile self)
                            AdaptiveProgram.custom comparer handler objectsWithKeys

                        | ExecutionEngine.Managed, RedundancyRemoval.Static -> 
                            Log.line "using optimized managed program"
                            let handler = GLFragmentHandlers.managedOptimized (Compiler.compileDelta self)
                            AdaptiveProgram.custom comparer handler objectsWithKeys

                        | ExecutionEngine.Managed, RedundancyRemoval.None -> 
                            Log.line "using unoptimized managed program"
                            let handler = GLFragmentHandlers.managedUnoptimized (Compiler.compile self)
                            AdaptiveProgram.custom comparer handler objectsWithKeys

                        | ExecutionEngine.Unmanaged, RedundancyRemoval.Static -> 
                            Log.line "using optimized unmanaged program"
                            let handler = GLFragmentHandlers.glvmOptimized vmStats (Compiler.compileDelta self)
                            AdaptiveProgram.custom comparer handler objectsWithKeys

                        | ExecutionEngine.Unmanaged, RedundancyRemoval.Runtime -> 
                            Log.line "using runtime-optimized unmanaged program"
                            let handler = GLFragmentHandlers.glvmRuntime vmStats (Compiler.compile self)
                            AdaptiveProgram.custom comparer handler objectsWithKeys

                        | ExecutionEngine.Unmanaged, RedundancyRemoval.None -> 
                            Log.line "using unoptimized unmanaged program"
                            let handler = GLFragmentHandlers.glvmUnoptimized vmStats (Compiler.compile self)
                            AdaptiveProgram.custom comparer handler objectsWithKeys

                        | t ->
                            failwithf "[GL] unsupported backend configuration: %A" t


                // finally we store the current config/ program and set hasProgram to true
                program <- newProgram
                hasProgram <- true
                currentConfig <- config
            
             
            
        override x.Run(fbo) =
            let config = config.GetValue x
            reinit x config

            let updateStats = program.Update(x)

            program.Run()

            // TODO: proper statistics here
            { FrameStatistics.Zero with
                ActiveInstructionCount = float -vmStats.Value.RemovedInstructions
                AddedRenderObjects = float updateStats.AddedFragmentCount
                RemovedRenderObjects = float updateStats.RemovedFragmentCount
            }
               
        override x.Dispose() =
            if hasProgram then
                hasProgram <- false
                program.Dispose()
                objects.Clear()
        
        override x.Add(o) = transact (fun () -> objects.Add o |> ignore)
        override x.Remove(o) = transact (fun () -> objects.Remove o |> ignore)



                
    [<AllowNullLiteral>]
    type AdaptiveGLVMFragment(obj : PreparedMultiRenderObject, adaptiveCode : IAdaptiveCode<Instruction>) =
        inherit AdaptiveObject()

        let boundingBox : IMod<Box3d> =
            match Ag.tryGetAttributeValue obj.First.Original.AttributeScope "GlobalBoundingBox" with
                | Success box -> box
                | _ -> failwith "[GL] could not get BoundingBox for RenderObject"
        let mutable currentBox = Box3d.Invalid

        let mutable prev : AdaptiveGLVMFragment = null
        let mutable next : AdaptiveGLVMFragment = null

        let code = List.toArray adaptiveCode.Content
        let frag = GLVM.vmCreate()
        let blocksWithContent = code |> Array.map (fun content -> (GLVM.vmNewBlock frag, content))

        let blockTable =
            code 
                |> Array.mapi (fun i m ->
                    if m.IsConstant then 
                        None
                    else
                        Some (m :> IAdaptiveObject, blocksWithContent.[i])
                   )
                |> Array.choose id
                |> Dictionary.ofArray

        let getArgs (o : Instruction) =
            o.Arguments |> Array.map (fun arg ->
                match arg with
                    | :? int as i -> nativeint i
                    | :? int64 as i -> nativeint i
                    | :? nativeint as i -> i
                    | :? float32 as f -> BitConverter.ToInt32(BitConverter.GetBytes(f), 0) |> nativeint
                    | :? PtrArgument as p ->
                        match p with
                            | Ptr32 p -> p
                            | Ptr64 p -> p
                    | _ -> failwith "invalid argument"
            )

        let writeBlock (id : int) (instructions : seq<Instruction>) =
            GLVM.vmClearBlock(frag, id)
            for i in instructions do
                match getArgs i with
                    | [| a |] -> GLVM.vmAppend1(frag, id, i.Operation, a)
                    | [| a; b |] -> GLVM.vmAppend2(frag, id, i.Operation, a, b)
                    | [| a; b; c |] -> GLVM.vmAppend3(frag, id, i.Operation, a, b, c)
                    | [| a; b; c; d |] -> GLVM.vmAppend4(frag, id, i.Operation, a, b, c, d)
                    | [| a; b; c; d; e |] -> GLVM.vmAppend5(frag, id, i.Operation, a, b, c, d, e)
                    | _ -> failwithf "invalid instruction: %A" i

        let dirtyBlocks = HashSet blocksWithContent
        
        override x.InputChanged (o : IAdaptiveObject) =
            match blockTable.TryGetValue o with
                | (true, dirty) -> lock dirtyBlocks (fun () -> dirtyBlocks.Add dirty |> ignore)
                | _ -> ()

        member x.Object = obj

        member x.BoundingBox = currentBox

        member x.Update(caller : IAdaptiveObject) =
            x.EvaluateIfNeeded caller () (fun () ->
                let blocks = 
                    lock dirtyBlocks (fun () ->
                        let all = Seq.toList dirtyBlocks
                        dirtyBlocks.Clear()
                        all
                    )

                for (block, content) in blocks do
                    let c = content.GetValue x
                    writeBlock block c

                currentBox <- boundingBox.GetValue x

            )

        member x.Handle = frag

        member x.Next
            with get() = next
            and set v = 
                next <- v
                if isNull v then GLVM.vmLink(frag, 0n)
                else GLVM.vmLink(frag, v.Handle)

        member x.Prev
            with get() = prev
            and set v = prev <- v

        member x.Dispose() =
            adaptiveCode.Dispose()
            if not (isNull prev) then prev.Next <- next
            if not (isNull next) then next.Prev <- prev
            GLVM.vmDelete frag

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    type CameraSortedRenderTask(order : RenderPassOrder, ctx : Context, fboSignature : IFramebufferSignature, config : IMod<BackendConfiguration>) as this =
        inherit AbstractRenderTask(ctx, fboSignature, RenderTaskLock(), config)
        do GLVM.vmInit()

        let mutable hasCameraView = false
        let mutable cameraView = Mod.constant Trafo3d.Identity
        
        let objects = CSet.empty
        let mutable vmStats = VMStats()   


        let fragments = objects |> ASet.mapUse (fun o -> new AdaptiveGLVMFragment(o, Compiler.compile this o))
        let fragmentReader = fragments.GetReader()
        let mutable dirtyFragments = HashSet<AdaptiveGLVMFragment>()


        let sort (f : seq<AdaptiveGLVMFragment>) : list<AdaptiveGLVMFragment> =
            let cam = cameraView.GetValue this
            let pos = cam.GetViewPosition()

            match order with
                | RenderPassOrder.BackToFront ->
                    f |> Seq.sortByDescending (fun f -> f.BoundingBox.GetMinimalDistanceTo pos) |> Seq.toList
                | _ ->
                    f |> Seq.sortBy (fun f -> f.BoundingBox.GetMinimalDistanceTo pos) |> Seq.toList
                    

        override x.InputChanged(i : IAdaptiveObject) =
            match i with
                | :? AdaptiveGLVMFragment as f -> lock dirtyFragments (fun () -> dirtyFragments.Add f |> ignore)
                | _ -> ()

        override x.Run(fbo) =
            let deltas = fragmentReader.GetDelta()

            let dirty = 
                lock dirtyFragments (fun () -> 
                    let old = dirtyFragments
                    dirtyFragments <- HashSet()
                    old
                )

            for d in deltas do
                match d with
                    | Add f -> dirty.Add f |> ignore
                    | Rem f -> dirty.Remove f |> ignore

            for d in dirty do d.Update x


            let ordered = sort fragmentReader.Content

            let mutable first = null
            let mutable last = null
            for f in ordered do
                f.Prev <- last
                if isNull last then first <- f
                else last.Next <- f
                last <- f

            if not (isNull first) then
                last.Next <- null
                GLVM.vmRun(first.Handle, VMMode.RuntimeRedundancyChecks, &vmStats)
            else
                vmStats.TotalInstructions <- 0
                vmStats.RemovedInstructions <- 0


            { FrameStatistics.Zero with
                InstructionCount = float vmStats.TotalInstructions
                ActiveInstructionCount = float (vmStats.TotalInstructions - vmStats.RemovedInstructions)
            }


        override x.Dispose() =
            fragmentReader.Dispose()
            objects.Clear()
            hasCameraView <- false
            cameraView <- Mod.constant Trafo3d.Identity
        
        override x.Add(o) = 
            if not hasCameraView then
                let o = o.First.Original
                match o.Uniforms.TryGetUniform (o.AttributeScope, Symbol.Create "ViewTrafo") with
                    | Some (:? IMod<Trafo3d> as view) -> 
                        hasCameraView <- true
                        cameraView <- view
                    | _ -> ()

            transact (fun () -> objects.Add o |> ignore)

        override x.Remove(o) = 
            transact (fun () -> objects.Remove o |> ignore)
                



    type RenderTask(manager : ResourceManager, fboSignature : IFramebufferSignature, objects : aset<IRenderObject>, config : IMod<BackendConfiguration>) as this =
        inherit AdaptiveObject()

        let mutable frameId = 0UL
        let ctx = manager.Context
        let resources = new Aardvark.Base.Rendering.ResourceInputSet()
        let inputSet = InputSet(this) 


        let add (ro : PreparedRenderObject) = 
            let all = ro.Resources |> Seq.toList
            for r in all do resources.Add r

            let old = ro.Activation
            ro.Activation <- 
                { new IDisposable with
                    member x.Dispose() =
                        old.Dispose()
                        for r in all do resources.Remove r
                }

            ro

        let rec prepareRenderObject (ro : IRenderObject) =
            match ro with
                | :? RenderObject as r ->
                    new PreparedMultiRenderObject([manager.Prepare(fboSignature, r) |> add])

                | :? PreparedRenderObject as prep ->
                    new PreparedMultiRenderObject([prep |> PreparedRenderObject.clone |> add])

                | :? MultiRenderObject as seq ->
                    let all = seq.Children |> List.collect(fun o -> (prepareRenderObject o).Children)
                    new PreparedMultiRenderObject(all)

                | :? PreparedMultiRenderObject as seq ->
                    new PreparedMultiRenderObject (seq.Children |> List.map (PreparedRenderObject.clone >> add))

                | _ ->
                    failwithf "[RenderTask] unsupported IRenderObject: %A" ro

        let preparedObjects = objects |> ASet.mapUse prepareRenderObject
        let preparedObjectReader = preparedObjects.GetReader()

        let mutable subtasks = Map.empty

        let getSubTask (pass : RenderPass) : AbstractRenderTask =
            match Map.tryFind pass subtasks with
                | Some task -> task
                | _ ->
                    let task = 
                        match pass.Order with
                            | RenderPassOrder.Arbitrary ->
                                new StaticOrderRenderTask(ctx, fboSignature, config) :> AbstractRenderTask

                            | order ->
                                new CameraSortedRenderTask(order, ctx, fboSignature, config) :> AbstractRenderTask

                    subtasks <- Map.add pass task subtasks
                    task

        member x.Run(caller : IAdaptiveObject, output : OutputDescription) =
            x.EvaluateAlways caller (fun () ->
                let mutable stats = FrameStatistics.Zero

                let deltas = preparedObjectReader.GetDelta x

                stats <- stats + resources.Update(x)

                for d in deltas do 
                    match d with
                        | Add v ->
                            let task = getSubTask v.RenderPass
                            task.Add v
                        | Rem v ->
                            let task = getSubTask v.RenderPass
                            task.Remove v

                for (_,t) in Map.toSeq subtasks do
                    stats <- stats + t.Run(x, output).Statistics

                frameId <- frameId + 1UL

                GL.Sync()

                RenderingResult(output.framebuffer, stats)
            )

        member x.Dispose() =
            preparedObjectReader.Dispose()
            resources.Dispose()
            for (_,t) in Map.toSeq subtasks do
                t.Dispose()

            subtasks <- Map.empty

        interface IDisposable with
            member x.Dispose() = x.Dispose()

        interface IRenderTask with
            member x.Run(a,b) = x.Run(a,b)
            member x.FrameId = frameId
            member x.FramebufferSignature = fboSignature
            member x.Runtime = Some ctx.Runtime
            











//
//module SortedRenderTask =
//    let private getArgs (o : Instruction) =
//        o.Arguments |> Array.map (fun arg ->
//            match arg with
//                | :? int as i -> nativeint i
//                | :? nativeint as i -> i
//                | :? float32 as f -> BitConverter.ToInt32(BitConverter.GetBytes(f), 0) |> nativeint
//                | _ -> failwith "invalid argument"
//        )
//
//    let private appendToBlock (frag : FragmentPtr) (id : int) (instructions : seq<Instruction>) =
//        for i in instructions do
//            match getArgs i with
//                | [| a |] -> GLVM.vmAppend1(frag, id, i.Operation, a)
//                | [| a; b |] -> GLVM.vmAppend2(frag, id, i.Operation, a, b)
//                | [| a; b; c |] -> GLVM.vmAppend3(frag, id, i.Operation, a, b, c)
//                | [| a; b; c; d |] -> GLVM.vmAppend4(frag, id, i.Operation, a, b, c, d)
//                | [| a; b; c; d; e |] -> GLVM.vmAppend5(frag, id, i.Operation, a, b, c, d, e)
//                | _ -> failwithf "invalid instruction: %A" i
//
//    [<AllowNullLiteral>]
//    type private Fragment =
//        class
//            inherit AdaptiveObject
//            val mutable public Handle : FragmentPtr
//            val mutable public RenderObject : PreparedRenderObject
//            val mutable public Code : IAdaptiveCode<Instruction>
//            val mutable public Next : Fragment
//
//            member x.Dispose() =
//                if x.Handle <> 0n then
//                    GLVM.vmDelete x.Handle
//                    x.Handle <- 0n
//
//            member x.Link(caller : IAdaptiveObject) =
//                x.EvaluateAlways caller (fun () ->
//                    if isNull x.Next then GLVM.vmUnlink(x.Handle)
//                    else GLVM.vmLink(x.Handle, x.Next.Handle)
//                )
//
//            member x.Update(caller : IAdaptiveObject) =
//                x.EvaluateIfNeeded caller () (fun () ->
//                    let fullCode = x.Code.Content |> List.collect (fun p -> p.GetValue(x))
//
//                    if x.Handle = 0n then x.Handle <- GLVM.vmCreate()
//                    else GLVM.vmClear x.Handle
//
//                    let id = GLVM.vmNewBlock x.Handle
//                    appendToBlock x.Handle id fullCode
//                )
//
//            interface IDisposable with
//                member x.Dispose() = x.Dispose()
//
//            new(obj, code) = { Handle = 0n; RenderObject = obj; Code = code; Next = null }
//
//        end
//
//    type RenderTask(objects : aset<IRenderObject>, manager : ResourceManager, fboSignature : IFramebufferSignature, config : BackendConfiguration) as this =
//        inherit AbstractRenderTaskWithResources(manager, fboSignature, manager.RenderTaskLock.Value, config.useDebugOutput) // manager needs resourcelock
//
//        let mutable anyScope = Ag.emptyScope
//
//        let sorter =
//            match config.sorting with
//                | Dynamic newSorter -> lazy (newSorter anyScope)
//                | _ ->
//                    failwith "[SortedRenderTask] unexpected RenderObject sorting"
//
//        let toFragment (prep : PreparedRenderObject) =
//            let frag = new Fragment(prep, RenderTaskUtilities.compile this prep)
//            this.InputChanged frag
//            frag
//
//        let fragmentCache = Dictionary<IRenderObject, Fragment>()
//        let prepareRenderObject (ro : IRenderObject) =
//            anyScope <- ro.AttributeScope
//            match ro with
//                | :? RenderObject as r ->
//                    let r = sorter.Value.ToSortedRenderObject r
//                    let frag = manager.Prepare(fboSignature, r) |> toFragment
//                    fragmentCache.[r] <- frag
//                    frag
//
//                | :? PreparedRenderObject as prep ->
//                    Log.warn "[SortedRenderTask] cannot sort PreparedRenderObjects"
//                    let frag = toFragment prep
//                    fragmentCache.[ro] <- frag
//                    frag
//                | _ ->
//                    failwithf "[SortedRenderTask] unsupported IRenderObject: %A" ro
//
//        let preparedObjects = objects |> ASet.mapUse prepareRenderObject
//        let reader = preparedObjects.GetReader()
//        let prolog = new Fragment(PreparedRenderObject.empty, new AdaptiveCode<_>([]))
//
//        let dirtyFragments = HashSet<Fragment>()
//
//        override x.InputChanged(o : IAdaptiveObject) =
//            match o with
//                | :? Fragment as f -> 
//                    lock dirtyFragments (fun () -> dirtyFragments.Add f |> ignore)
//                | _ ->
//                    base.InputChanged o
//
//        override x.Dispose() =
//            reader.Dispose()
//            prolog.Dispose()
//
//        override x.Run fbo =
//            let deltas = reader.GetDelta(x)
//            let sorter = sorter.Value
//            for d in deltas do
//                match d with
//                    | Add prep -> sorter.Add prep.RenderObject.Original
//                    | Rem prep -> sorter.Remove prep.RenderObject.Original
//
//            let sorted = sorter.SortedList.GetValue x
//
//            let mutable stats = x.UpdateDirtyResources(0)
//
//            // update all links and contents
//            let mutable current = prolog
//
//            let dirtyFrags = 
//                lock dirtyFragments (fun () ->
//                    let res = dirtyFragments.ToArray()
//                    dirtyFragments.Clear()
//                    res
//                )
//
//            prolog.Update x
//            for frag in dirtyFrags do
//                frag.Update x
//
//            for ro in sorted do
//                match fragmentCache.TryGetValue ro with
//                    | (true, frag) ->
//                        current.Next <- frag
//                        current.Link x
//                        current <- frag
//                    | _ ->
//                        Log.warn "[SortedRenderTask] skipping unknown RenderObject generated by sorter"
//                        ()
//
//            current.Update x
//            current.Link x
//
//      
//            stats <- stats + x.GetStats()
//
//            let mutable vmStats = VMStats()
//            GLVM.vmRun(prolog.Handle, VMMode.RuntimeRedundancyChecks, &vmStats)
//
//            { stats with ActiveInstructionCount = stats.ActiveInstructionCount - float vmStats.RemovedInstructions }


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
                        GL.Clear(ClearBufferMask.ColorBufferBit ||| ClearBufferMask.DepthBufferBit ||| ClearBufferMask.StencilBufferBit)
                        
                    | [Some c], None ->
                        GL.ClearColor(c.R, c.G, c.B, c.A)
                        GL.Clear(ClearBufferMask.ColorBufferBit)

                    | l, Some depth when List.forall Option.isNone l ->
                        GL.ClearDepth(depth)
                        GL.Clear(ClearBufferMask.DepthBufferBit ||| ClearBufferMask.StencilBufferBit)
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
                                GL.Clear(ClearBufferMask.DepthBufferBit ||| ClearBufferMask.StencilBufferBit)
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