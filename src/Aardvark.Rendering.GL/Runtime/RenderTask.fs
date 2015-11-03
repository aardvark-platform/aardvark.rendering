namespace Aardvark.Rendering.GL

open System
open System.Collections.Generic
open Aardvark.Base
open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4
open Aardvark.Base.Incremental

[<AutoOpen>]
module RenderTasks =
    open System
    open System.Runtime.InteropServices
    open System.Collections.Generic
    open Aardvark.Rendering.GL
    open Aardvark.Rendering.GL.OpenGl.Enums



//
//    let private configs = Dictionary<uint64, Order>()
//
//    let setPassConfig (pass : uint64, config : Order) =
//        configs.[pass] <- config
//
//    let getPassConfig(pass : uint64) =
//        match configs.TryGetValue pass with
//            | (true, c) -> c
//            | _ -> Order.Unordered


    type SortedList<'k, 'v when 'k : comparison>() =
        let keys = List<'k>()
        let values = List<'v>()

        let rec findIndexForKey' (key : 'k) (l : int) (r : int) =
            if l <= r then
                let m = (l+r) / 2
                let v = keys.[m]
                let c = compare key v
                if c = 0 then
                    Right m
                elif c > 0 then
                    findIndexForKey' key (m+1) r
                else
                    findIndexForKey' key l (m-1)
            else
                Left l

        let findIndexForKey (key : 'k) =
            findIndexForKey' key 0 (keys.Count - 1)

        //Right = found
        //Left = not found

        member x.Add(key : 'k, value : 'v) =
            match findIndexForKey key with
                | Left index ->
                    keys.Insert(index, key)
                    values.Insert(index, value)
                | _ -> failwith "duplicated entry"
               
        member x.Remove(key : 'k) =
            match findIndexForKey key with
                | Right index -> 
                    keys.RemoveAt index
                    values.RemoveAt index 
                    true
                | _ -> false     

        member x.TryGetValue(key : 'k, [<Out>] value : byref<'v>) =
            match findIndexForKey key with
                | Right index -> 
                    value <- values.[index]  
                    true
                | _ -> false       
                
        member x.TryGetNextGreater(key : 'k, [<Out>] value : byref<'v>) =
            let nextIndex = match findIndexForKey key with
                            | Left index -> if index < 0 then 0 else index + 1
                            | Right index -> index + 1
            if nextIndex < values.Count then
                value <- values.[nextIndex]
                true
            else
                false

        member x.TryGetNextSmaller(key : 'k, [<Out>] value : byref<'v>) =
            let prevIndex = match findIndexForKey key with
                            | Left index -> if index >= values.Count then values.Count - 1 else index - 1
                            | Right index -> index - 1
            if prevIndex >= 0 then
                value <- values.[prevIndex]
                true
            else
                false

        member x.Count = keys.Count

        member x.Clear() =
            keys.Clear()
            values.Clear()

        member x.Item
            with get i = values.[i]
            and set i v = values.[i] <- v

        interface IEnumerable<KeyValuePair<'k, 'v>> with
            member x.GetEnumerator() : IEnumerator<KeyValuePair<'k, 'v>> =
                let s = seq { for i in 0..keys.Count-1 do yield KeyValuePair(keys.[i], values.[i]) }
                s.GetEnumerator()

            member x.GetEnumerator() : System.Collections.IEnumerator =
                let s = seq { for i in 0..keys.Count-1 do yield KeyValuePair(keys.[i], values.[i]) }
                (s :> System.Collections.IEnumerable).GetEnumerator()

    type RenderTask(runtime : IRuntime, ctx : Context, manager : ResourceManager, engine : IMod<BackendConfiguration>, set : aset<IRenderObject>) as this =
        inherit AdaptiveObject()
        static let RenderTaskRunProbe = Symbol.Create "[RenderTask] run"

        let mutable currentEngine = engine.GetValue(this)
        let subscriptions = Dictionary()
        let reader = set.GetReader()
        do reader.AddOutput this
           engine.AddOutput this

        let inputs = ReferenceCountingSet<IAdaptiveObject>()
        let mutable programs = Map.empty
        let changer = Mod.init ()

        let mutable additions = 0
        let mutable removals = 0
        let mutable frameId = 0UL

        let renderPassChangers = Dictionary<IRenderObject, IMod<unit> * ref<uint64>>()
        let renderPassChangeSet = MutableVolatileDirtySet<IMod<unit>, unit>(fun m -> m.GetValue this)

        let addInput m =
            if inputs.Add m then
                transact (fun () -> m.AddOutput this)

        let removeInput m =
            if inputs.Remove m then
                transact (fun () -> m.RemoveOutput this)

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
                        manager, addInput, removeInput
                    ) :> IRenderProgram
                | s ->
                    match engine.execution, engine.redundancy with

                        | ExecutionEngine.Native, RedundancyRemoval.None ->
                            Log.line "using unoptimized native program"
                            new Compiler.UnoptimizedProgram<_>(this,
                                engine, Compiler.FragmentHandlers.native, manager, addInput, removeInput
                            ) :> IRenderProgram

                        | ExecutionEngine.Native, _ ->
                            Log.line "using optimized native program"
                            new Compiler.OptimizedProgram<_>(this,
                                engine, Compiler.FragmentHandlers.native, manager, addInput, removeInput
                            ) :> IRenderProgram

                        | ExecutionEngine.Unmanaged, RedundancyRemoval.None ->
                            Log.line "using unoptimized glvm program"
                            new Compiler.UnoptimizedProgram<_>(this,
                                engine, Compiler.FragmentHandlers.glvm, manager, addInput, removeInput
                            ) :> IRenderProgram

                        | ExecutionEngine.Unmanaged, RedundancyRemoval.Runtime ->
                            Log.line "using runtime-optimized glvm program"
                            new Compiler.UnoptimizedProgram<_>(this,
                                engine, Compiler.FragmentHandlers.glvmRuntimeRedundancyChecks, manager, addInput, removeInput
                            ) :> IRenderProgram

                        | ExecutionEngine.Unmanaged, RedundancyRemoval.Static ->
                            Log.line "using optimized glvm program"
                            new Compiler.OptimizedProgram<_>(this,
                                engine, Compiler.FragmentHandlers.glvm, manager, addInput, removeInput
                            ) :> IRenderProgram


                        | ExecutionEngine.Managed, RedundancyRemoval.None ->
                            Log.line "using unoptimized managed program"
                            new Compiler.UnoptimizedProgram<_>(this,
                                engine, Compiler.FragmentHandlers.managed, manager, addInput, removeInput
                            ) :> IRenderProgram

                        | ExecutionEngine.Managed, _->
                            Log.line "using optimized managed program"
                            new Compiler.OptimizedProgram<_>(this,
                                engine, Compiler.FragmentHandlers.managed, manager, addInput, removeInput
                            ) :> IRenderProgram

                        | ExecutionEngine.Debug, _ ->
                            Log.warn "using debug program"

                            new Compiler.DebugProgram(this,
                                manager, addInput, removeInput
                            ) :> IRenderProgram

                        | _ ->
                            failwith "unsupported configuration: %A" engine

           

        let mutable lastScope = None

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

        let dirtyLock = obj()
        let mutable dirtyResources = HashSet<IChangeableResource>()
    
        member private x.UpdateDirty() =
            
            let dirtyResoruces = System.Threading.Interlocked.Exchange(&dirtyResources, HashSet())
            if dirtyResoruces.Count > 0 then
                let mutable count = 0

                let mutable counts = Map.empty

                let updateSw = System.Diagnostics.Stopwatch()
                updateSw.Start()
                System.Threading.Tasks.Parallel.ForEach(dirtyResoruces, fun (d : IChangeableResource) ->
                    lock d (fun () ->
                        if d.OutOfDate then
                            let cnt = match Map.tryFind d.Kind counts with | Some v -> v | None -> 0.0
                            counts <- Map.add d.Kind (cnt + 1.0) counts
                            count <- count + 1

                            d.UpdateCPU(x)
                            //d.UpdateGPU(renderTask)
                        else
                            d.Outputs.Add x |> ignore
                    )
                ) |> ignore
                updateSw.Stop()

                Log.line "CPU update took: %.3fµs per resource" (1000.0 * updateSw.Elapsed.TotalMilliseconds / float dirtyResoruces.Count)

                updateSw.Restart()
                for d in dirtyResoruces do
                    lock d (fun () ->
                        if d.OutOfDate then
        //                    let cnt = match Map.tryFind d.Kind counts with | Some v -> v | None -> 0.0
        //                    counts <- Map.add d.Kind (cnt + 1.0) counts
        //                    count <- count + 1
        //
        //                    d.UpdateCPU(renderTask)
                            d.UpdateGPU(x)
                    )
                updateSw.Stop()
                Log.line "GPU update took: %.3fµs per resource" (1000.0 * updateSw.Elapsed.TotalMilliseconds / float dirtyResoruces.Count)





        member private x.Add(pass : uint64, rj : IRenderObject) =
            additions <- additions + 1
            let program = getProgramForPass pass rj.AttributeScope
            program.Add rj

        member private x.Remove(pass : uint64, rj : IRenderObject) =
            removals <- removals + 1
            match tryGetProgramForPass pass with
                | Some p -> p.Remove rj
                | None -> ()

        member x.Runtime = runtime
        member x.Manager = manager

        override x.InputChanged(o : IAdaptiveObject) =
            match o with
                | :? IChangeableResource as o ->
                    lock dirtyLock (fun () ->
                        dirtyResources.Add(o) |> ignore
                    )
                | _ -> ()

        member x.ProcessDeltas (deltas : list<Delta<IRenderObject>>) =
            let mutable additions = 0
            let mutable removals = 0
            for d in deltas do
                match d with
                    | Add a ->
                        if a.RenderPass <> null then 
                            let currentPass = ref System.UInt64.MaxValue
                            let changed =
                                a.RenderPass |> Mod.map (fun p ->
                                    let old = !currentPass
                                    if old <> p then
                                        currentPass := p
                                        x.Add(p,a)
                                        x.Remove(old, a)
                                )
                            changed.GetValue(x)
                            renderPassChangers.Add(a, (changed, currentPass))
                            renderPassChangeSet.Add changed
//
//                            let s = a.RenderPass |> Mod.unsafeRegisterCallbackKeepDisposable (fun k ->
//                                if !oldPass <> k  // phantom change here might lead to duplicate additions.
//                                    then
//                                        oldPass := k
//                                        x.Add(k,a)
//
//                                        match subscriptions.TryGetValue a with
//                                            | (true,(s,old)) ->
//                                                x.Remove(old, a)
//                                                subscriptions.[a] <- (s,k)
//                                            | _ -> ()
//                                    else 
//                                        printfn "changed pass to old value (phantom)"
//                            
//                            )
//                            let sortKey = a.RenderPass.GetValue(x)
//                            subscriptions.[a] <- (s, sortKey)
                        else
                            x.Add(0UL, a)

                        additions <- additions + 1
                    | Rem a ->    
                        if a.RenderPass <> null then
                            match renderPassChangers.TryGetValue a with
                                | (true, (changer, current)) ->
                                    x.Remove(!current, a)
                                    renderPassChangeSet.Remove changer
                                    renderPassChangers.Remove a |> ignore
                                | _ -> 
                                    ()
//                            match subscriptions.TryGetValue a with
//                                | (true,(d,k)) ->
//                                    x.Remove(k, a)
//                                    d.Dispose()
//                                    subscriptions.Remove a |> ignore
//                                | _ -> ()
                        else
                            x.Remove(0UL, a)
                        removals <- removals + 1
            (additions, removals)

        member x.Run (caller : IAdaptiveObject, fbo : IFramebuffer) =
            x.EvaluateAlways caller (fun () ->
                Telemetry.timed RenderTaskRunProbe (fun () ->
                    using ctx.ResourceLock (fun _ ->
                        x.UpdateDirty()

                        setExecutionEngine (engine.GetValue(x))

                        let wasEnabled = GL.IsEnabled EnableCap.DebugOutput
                        if currentEngine.useDebugOutput 
                        then
                            match ContextHandle.Current with
                             | Some v -> v.AttachDebugOutputIfNeeded()
                             | None -> Report.Warn("No active context handle in RenderTask.Run")
                            GL.Enable EnableCap.DebugOutput

                        let old = Array.create 4 0
                        let mutable oldFbo = 0
                        OpenTK.Graphics.OpenGL.GL.GetInteger(OpenTK.Graphics.OpenGL.GetPName.Viewport, old)
                        OpenTK.Graphics.OpenGL.GL.GetInteger(OpenTK.Graphics.OpenGL.GetPName.FramebufferBinding, &oldFbo)

                        let handle = fbo.Handle |> unbox<int> 

                        if ExecutionContext.framebuffersSupported then
                            GL.BindFramebuffer(OpenTK.Graphics.OpenGL4.FramebufferTarget.Framebuffer, handle)
                            GL.Check "could not bind framebuffer"
                        elif handle <> 0 then
                            failwithf "cannot render to texture on this OpenGL driver"


                        GL.Viewport(0, 0, fbo.Size.X, fbo.Size.Y)
                        GL.Check "could not set viewport"
                    
                        let additions, removals =
                            if reader.OutOfDate then
                                x.ProcessDeltas (reader.GetDelta(x))
                            else
                                0,0

                        renderPassChangeSet.Evaluate() |> ignore

                        let mutable resourceCount = 0
                        let mutable stats = FrameStatistics.Zero
                        let contextHandle = ContextHandle.Current.Value

                        //render
                        for (KeyValue(_,p)) in programs do
                            stats <- stats + p.Run(handle, contextHandle)
                            resourceCount <- resourceCount + p.Resources.Entries.Count

                        if ExecutionContext.framebuffersSupported then
                            GL.BindFramebuffer(OpenTK.Graphics.OpenGL4.FramebufferTarget.Framebuffer, oldFbo)
                            GL.Check "could not bind framebuffer"
                        GL.Viewport(old.[0], old.[1], old.[2], old.[3])
                        GL.Check "could not set viewport"
                    
                        if wasEnabled <> currentEngine.useDebugOutput 
                        then
                            if wasEnabled then GL.Enable EnableCap.DebugOutput
                            else GL.Disable EnableCap.DebugOutput

                        let stats = 
                            { stats with 
                                AddedRenderObjects = float additions
                                RemovedRenderObjects = float removals
                                ResourceCount = float resourceCount 
                            }

                        frameId <- frameId + 1UL

                        RenderingResult(fbo, stats)
                    )
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

            member x.Runtime = runtime |> Some

            member x.FrameId = frameId


    type ClearTask(runtime : IRuntime, color : IMod<C4f>, depth : IMod<float>, ctx : Context) as this =
        inherit AdaptiveObject()
        do color.AddOutput this
           depth.AddOutput this

        let mutable frameId = 0UL

        member x.Run(caller : IAdaptiveObject, fbo : IFramebuffer) =
            using ctx.ResourceLock (fun _ ->
                x.EvaluateAlways caller (fun () ->
                    let old = Array.create 4 0
                    let mutable oldFbo = 0
                    OpenTK.Graphics.OpenGL.GL.GetInteger(OpenTK.Graphics.OpenGL.GetPName.Viewport, old)
                    OpenTK.Graphics.OpenGL.GL.GetInteger(OpenTK.Graphics.OpenGL.GetPName.FramebufferBinding, &oldFbo)

                    let handle = fbo.Handle |> unbox<int>

                    if ExecutionContext.framebuffersSupported then
                        GL.BindFramebuffer(OpenTK.Graphics.OpenGL4.FramebufferTarget.Framebuffer, handle)
                        GL.Check "could not bind framebuffer"
                    elif handle <> 0 then
                        failwithf "cannot render to texture on this OpenGL driver"

                    GL.Viewport(0, 0, fbo.Size.X, fbo.Size.Y)
                    GL.Check "could not bind framebuffer"

                    let c = color.GetValue x
                    let d = depth.GetValue x
                    GL.ClearColor(c.R, c.G, c.B, c.A)
                    GL.ClearDepth(d)
                    GL.Clear(ClearBufferMask.ColorBufferBit ||| ClearBufferMask.DepthBufferBit)

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
            member x.Runtime = runtime |> Some
            member x.Run(caller, fbo) =
                x.Run(caller, fbo)

            member x.Dispose() =
                x.Dispose()

            member x.FrameId = frameId
