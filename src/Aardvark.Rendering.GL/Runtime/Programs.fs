namespace Aardvark.Rendering.GL

open System
open System.Collections.Generic
open Aardvark.Base
open Aardvark.Rendering

[<AutoOpen>]
module Programs =
    open System
    open System.Collections.Generic
    open Aardvark.Rendering.GL
    open System.Collections.Concurrent
    open Aardvark.Base.Incremental

    let private emptyMod = Mod.initConstant () :> IMod
    let private projections = 
        [| fun (r : RenderJob) -> r.Surface :> IMod

           fun (r : RenderJob) -> 
               match r.Uniforms.TryGetUniform DefaultSemantic.DiffuseColorTexture with
                   | (true,t) -> t
                   | _ -> emptyMod

//               fun (r : RenderJob) -> r.StencilMode.Mod :> IMod
//               fun (r : RenderJob) -> r.BlendMode.Mod :> IMod
//               fun (r : RenderJob) -> r.DepthTest.Mod :> IMod
//               fun (r : RenderJob) -> r.CullMode.Mod :> IMod
        |]

    let private compareMods (l : IMod) (r : IMod) =
        if l = r then 0
        else compare l r

    let rec private compareRenderJobs' (level : int) (l : RenderJob) (r : RenderJob) =
        if level < projections.Length then
            let p = projections.[level] 
            let l' = p l
            let r' = p r
            let c = compareMods l' r' 
            if c <> 0 then c
            else compareRenderJobs' (level + 1) l r
        else
            0//compare l.ID r.ID

    let private compareRenderJobs (l : RenderJob) (r : RenderJob) =
        compareRenderJobs' 0 l r

    type private DependencySet(add : IAdaptiveObject -> unit, remove : IAdaptiveObject -> unit) =
        let resources = ReferenceCountingSet<IChangeableResource>()
        let perContext = HashSet<unit -> unit>()
        let changeSet = HashSet<IChangeableResource>()
        let subscriptions = Dictionary<IChangeableResource, IDisposable>()
        let mutable currentContext : ContextHandle = null

        let addChanged (r : IChangeableResource) =  
            lock changeSet (fun () -> changeSet.Add r |> ignore)

        let removeChanged (r : IChangeableResource) =  
            lock changeSet (fun () -> changeSet.Remove r |> ignore)


        let addResource (r : IChangeableResource) =
            if resources.Add r then
                add r
                if outOfDate r then addChanged r
                subscriptions.Add(r, subscribeDirty r changeSet)

        let removeResource (r : IChangeableResource) =
            if resources.Remove r then
                remove r
                match subscriptions.TryGetValue r with
                    | (true,v) -> 
                        subscriptions.Remove(r) |> ignore
                        v.Dispose()
                    | _ -> failwithf "no subscription found for resource: %A" r

                removeChanged r

        member x.Add(state : InstructionCompiler.CompilerState) =
            state.resources |> List.iter addResource
            state.percontext |> List.iter (fun p -> perContext.Add p |> ignore)
            currentContext <- null //TODO: currentContext should be maintained by the setters themselves
            state.dependencies |> List.iter add

        member x.Remove(state : InstructionCompiler.CompilerState) =
            state.resources |> List.iter removeResource
            state.percontext |> Seq.iter (fun p -> perContext.Remove p |> ignore)
            state.dependencies |> List.iter remove

        member x.GetAndClearChanges() =
            lock changeSet (fun () -> 
                let data = changeSet |> Seq.toArray
                changeSet.Clear()
                data
            )

        member x.SetCurrentContext(context : ContextHandle) =
            if currentContext <> context then
                currentContext <- context
                perContext |> Seq.iter (fun f -> f())

        member x.Dispose() =
            //ISSUE: resources must possibly be disposed multiple times (reference counts)
            subscriptions.Values |> Seq.iter (fun d -> d.Dispose())
            perContext.Clear()
            changeSet.Clear()
            resources |> Seq.iter (fun r -> r.Dispose())
            resources.Clear()
            subscriptions.Clear()
               
                
    [<AllowNullLiteral>]
    type private RenderJobFragment(parent : DependencySet, memory : MemoryManager, manager : ResourceManager, rj : RenderJob) =
        inherit Fragment<RenderJob>(memory, 0, rj)

        let stats = EventSource<FrameStatistics>(FrameStatistics.Zero)
        let mutable statSubscription : Option<IDisposable> = None

        let mutable state : Option<InstructionCompiler.CompilerState> = None

        member x.Statistics = stats :> IEvent<FrameStatistics>

        member private x.Recompile(prev : RenderJob, me : RenderJob) =

            let mutable removeOldState = id
            match state with
                | Some state -> 
                    match statSubscription with | Some s -> s.Dispose(); statSubscription <- None | _ -> ()
                    stats.Emit(FrameStatistics.Zero)
                    removeOldState <- fun () -> parent.Remove(state)
                    state.Dispose()
                    x.Clear()
                | _ -> ()
                
            let newState = InstructionCompiler.compileDelta manager (NativeDynamicFragment(x)) prev me
            state <- Some newState
            let s = newState.statistics.Values.Subscribe(fun s ->
                stats.Emit(s)
            )
            statSubscription <- Some s
            parent.Add newState
            removeOldState()
            ()

        member x.RenderJob = x.Tag

        member x.Dispose() =
            match state with
                | Some state -> 
                    state.Dispose()
                    parent.Remove(state)
                    base.Dispose()
                | _ -> ()
            state <- None

        member x.Next
            with get() = base.Next |> unbox<RenderJobFragment>
            and set (v : RenderJobFragment) = 
                base.Next <- v

        member x.Prev 
            with get() = base.Prev |> unbox<RenderJobFragment>
            and set (v : RenderJobFragment) =
                  base.Prev <- v
                  if v = null then
                      x.Dispose()
                  elif x.Tag.IsValid then 
                      x.Recompile(v.Tag, x.Tag)


    [<AllowNullLiteral>]
    type private CompleteRenderJobFragment(parent : DependencySet, memory : MemoryManager, manager : ResourceManager, rj : RenderJob) =
        inherit Fragment<RenderJob>(memory, 0, rj)

        let stats = EventSource<FrameStatistics>(FrameStatistics.Zero)
        let mutable statSubscription : Option<IDisposable> = None

        let mutable state : Option<InstructionCompiler.CompilerState> = None

        member x.Statistics = stats :> IEvent<FrameStatistics>

        member private x.Compile(me : RenderJob) =
            if me.IsValid then
                let newState = InstructionCompiler.compileDelta manager (NativeDynamicFragment(x)) RenderJob.Empty me
                state <- Some newState
                let s = newState.statistics.Values.Subscribe(fun s ->
                    stats.Emit(s)
                )
                statSubscription <- Some s
                parent.Add newState

        member x.RenderJob = x.Tag

        member x.Dispose() =
            match state with
                | Some state -> 
                    state.Dispose()
                    parent.Remove(state)
                    base.Dispose()
                | _ -> ()
            state <- None

        member x.Next
            with get() = base.Next |> unbox<CompleteRenderJobFragment>
            and set (v : CompleteRenderJobFragment) =  base.Next <- v

        member x.Prev 
            with get() = base.Prev |> unbox<CompleteRenderJobFragment>
            and set (v : CompleteRenderJobFragment) = 
                base.Prev <- v
                match state with
                    | None -> x.Compile(x.Tag)
                    | _ -> ()


    [<AllowNullLiteral>]
    type private ManagedRenderJobFragment(prolog : ManagedDynamicFragment, parent : DependencySet, manager : ResourceManager, rj : RenderJob) =
        inherit ManagedDynamicFragment(prolog)

        let stats = EventSource<FrameStatistics>(FrameStatistics.Zero)
        let mutable statSubscription : Option<IDisposable> = None

        let mutable state : Option<InstructionCompiler.CompilerState> = None

        member x.Statistics = stats :> IEvent<FrameStatistics>

        member private x.Recompile(prev : RenderJob, me : RenderJob) =

            let mutable removeOldState = id
            match state with
                | Some state -> 
                    match statSubscription with | Some s -> s.Dispose(); statSubscription <- None | _ -> ()
                    stats.Emit(FrameStatistics.Zero)
                    removeOldState <- fun () -> parent.Remove(state)
                    state.Dispose()
                    x.Clear()
                | _ -> ()
                
            let newState = InstructionCompiler.compileDelta manager (x) prev me
            state <- Some newState
            let s = newState.statistics.Values.Subscribe(fun s ->
                stats.Emit(s)
            )
            statSubscription <- Some s
            parent.Add newState
            removeOldState()
            ()

        member x.RenderJob = rj

        member x.Dispose() =
            match state with
                | Some state -> 
                    state.Dispose()
                    parent.Remove(state)
                | _ -> ()
            state <- None

        member x.Next
            with get() = base.Next |> unbox<ManagedRenderJobFragment>
            and set (v : ManagedRenderJobFragment) = 
                base.Next <- v

        member x.Prev 
            with get() = base.Prev |> unbox<ManagedRenderJobFragment>
            and set (v : ManagedRenderJobFragment) =
                base.Prev <- v
                if v = null then
                    x.Dispose()
                elif x.RenderJob.IsValid then 
                    x.Recompile(v.RenderJob, x.RenderJob)


    [<AllowNullLiteral>]
    type IProgram =
        inherit IDisposable
        abstract member Add : RenderJob -> unit
        abstract member Remove : RenderJob -> unit
        abstract member Update : RenderJob -> unit
        abstract member Run : Framebuffer * ContextHandle -> FrameStatistics

    type DelayedTask(f : unit -> unit) =
        let f = ref f
        let timerLock = obj()
        let run = fun (o : obj) -> lock timerLock !f
        let timerCallback = System.Threading.TimerCallback(run)
        let mutable timer : Option<System.Threading.Timer> = None

        member x.TrySetDelay(delay : int) =
            match timer with
                | Some t -> t.Change(delay, System.Threading.Timeout.Infinite)
                | None -> let t = new System.Threading.Timer(timerCallback, null, delay, System.Threading.Timeout.Infinite)
                          timer <- Some t
                          true

        member x.Callback
            with get() = !f
            and set v = f := v

        new() = DelayedTask(id)

    [<AutoOpen>]
    module EventAggregation =
        type EventSourceAggregate<'a>(zero : 'a, add : 'a -> 'a -> 'a, sub : 'a -> 'a -> 'a) =
            let aggregate = EventSource<'a>(zero)
            let subscriptions = Dictionary<IEvent<'a>, IDisposable>()

            member x.Add(e : IEvent<'a>) =
                let old = ref e.Latest
                aggregate.Emit(add aggregate.Latest !old)
                let s = e.Values.Subscribe(fun v ->
                    aggregate.Emit(sub (add aggregate.Latest v) !old)
                    old := v
                )

                subscriptions.Add(e, s)

            member x.Remove(e : IEvent<'a>) =
                match subscriptions.TryGetValue e with
                    | (true,s) ->
                        s.Dispose()
                        aggregate.Emit(sub aggregate.Latest e.Latest)
                        subscriptions.Remove(e) |> ignore
                    | _ -> ()

            member x.Latest = aggregate.Latest
            member x.Next = aggregate.Next
            member x.Values = aggregate.Values

            interface IEvent<'a> with
                member x.Latest = aggregate.Latest
                member x.Next = aggregate.Next
                member x.Next = (aggregate :> IEvent).Next
                member x.Values = aggregate.Values
                member x.Values = (aggregate :> IEvent).Values


    [<AllowNullLiteral>]
    type SortedProgram(order : Order, manager : ResourceManager, add : IAdaptiveObject -> unit, remove : IAdaptiveObject -> unit, newSorter : unit -> ISorter) =
             
        let memory = new MemoryManager()
        let fragments = Dictionary<RenderJob, CompleteRenderJobFragment>()

        let stats = EventSourceAggregate<FrameStatistics>(FrameStatistics.Zero, (+), (-))
        let jumpDistance = EventSourceAggregate<int64>(0L, (+),(-))

        let deps = DependencySet(add, remove)
        let sorter = newSorter()
        let sorted = sorter.SortedList
        do add sorted

        let prolog = CompleteRenderJobFragment(deps, memory, manager, RenderJob.Empty)
        do prolog.Append(Assembler.functionProlog 6) |> ignore
           jumpDistance.Add prolog.JumpDistance
        let epilog = CompleteRenderJobFragment(deps, memory, manager, RenderJob.Empty)
        do epilog.Append(Assembler.functionEpilog 6) |> ignore
               
        let mutable run : unit -> unit = id 
        let mutable entryPtr = 0n
            
        let mutable additions = 0
        let mutable removals = 0
            
        member x.Add (r : RenderJob) =
            let sorted = sorter.ToSortedRenderJob order r
            let f = CompleteRenderJobFragment(deps, memory, manager, sorted)
            fragments.Add(r, f)
            sorter.Add r
            stats.Add f.Statistics
            jumpDistance.Add f.JumpDistance
            additions <- additions + 1

        member x.Remove (r : RenderJob) = 
            match fragments.TryGetValue r with
                | (true, f) -> 
                    fragments.Remove r |> ignore
                    stats.Remove f.Statistics
                    jumpDistance.Remove f.JumpDistance
                    sorter.Remove r
                    if f.Prev <> null then f.Prev.Next <- f.Next
                    if f.Next <> null then f.Next.Prev <- f.Prev
                    f.Dispose()
                    removals <- removals + 1
                | _ -> ()

        member x.Run(fbo : Framebuffer, ctx : ContextHandle) = 
                
            let newPerm = 
                lock sorted (fun () ->
                    if sorted.OutOfDate then
                        let permutation = Mod.force sorted
                        Some permutation
                    else 
                        None
                )

            match newPerm with
                | Some permutation ->
                    let mutable lastFragment = prolog
                    for r in permutation do
                        match fragments.TryGetValue r with
                            | (true, f) -> 
                                if lastFragment.Next <> f then
                                    lastFragment.Next <- f
                                if f.Prev <> lastFragment then
                                    f.Prev <- lastFragment

                                lastFragment <- f
                            | _ -> () //printfn "cannot produce new renderjobs in sorter"

                    lastFragment.Next <- epilog
                    epilog.Prev <- lastFragment
                | None -> ()

            //TODO: same as optimized (maybe use inheritance here)
            let resources = deps.GetAndClearChanges()

            let mutable resourceUpdates = 0
            for r in resources do
                if outOfDate r then
                    resourceUpdates <- resourceUpdates + 1
                    r.UpdateCPU()
                    r.UpdateGPU()

            deps.SetCurrentContext(ctx)

            lock memory.PointerLock (fun () ->
                if prolog.RealPointer <> entryPtr then
                    entryPtr <- prolog.RealPointer
                    run <- UnmanagedFunctions.wrap entryPtr
                run()
            )

            let inner = stats.Latest
            let avgJump = float jumpDistance.Latest / float (1 + fragments.Count)

            { inner with
                ResourceUpdateCount = float resourceUpdates
                JumpDistance = avgJump
                AddedRenderJobs = float additions
                RemovedRenderJobs = float removals
            }

        member x.Dispose() =
            remove sorted
            let mutable current = prolog
            while current <> null do
                let next = current.Next
                current.Dispose()
                current <- next

            //list.Clear()
            deps.Dispose()
            memory.Dispose()

        interface IProgram with
            member x.Add r = x.Add r
            member x.Remove r = x.Remove r
            member x.Update r = failwith "notimp"
            member x.Run (fb,ctx) = x.Run(fb,ctx)
            member x.Dispose() = x.Dispose()

    [<AllowNullLiteral>]
    type OptimizedNativeProgram(manager : ResourceManager, add : IAdaptiveObject -> unit, remove : IAdaptiveObject -> unit) =
        let memory = new MemoryManager()

        let stats = EventSourceAggregate<FrameStatistics>(FrameStatistics.Zero, (+), (-))
        let jumpDistance = EventSourceAggregate<int64>(0L, (+),(-))

        let fragments = Dictionary<RenderJob, RenderJobFragment>()
        let sortedFragments = BucketAVL.custom (fun (l : RenderJobFragment) (r : RenderJobFragment) -> compareRenderJobs l.RenderJob r.RenderJob)  //AVL.custom (fun (l : RenderJobFragment) (r : RenderJobFragment) -> compareRenderJobs l.RenderJob r.RenderJob)
           
        let deps = DependencySet(add, remove)

        let prolog = RenderJobFragment(deps, memory, manager, RenderJob.Empty)
        do prolog.Append(Assembler.functionProlog 6) |> ignore
           jumpDistance.Add prolog.JumpDistance
        let epilog = RenderJobFragment(deps, memory, manager, RenderJob.Empty)
        do epilog.Append(Assembler.functionEpilog 6) |> ignore
               
        let mutable run : unit -> unit = id 
        let mutable entryPtr = 0n

            
        let mutable additions = 0
        let mutable removals = 0

        let defragment() =
            Log.startTimed "defragmentation"

            let mutable current = prolog
            current.Freeze()
                
            let mutable index = 0
            while current.Next <> null do
                //printfn "%d" index
                current.DefragmentNext()

                let next = current.Next
                current.Unfreeze()
                //System.Threading.Thread.Sleep(100)
                next.Freeze()

                current <- next
                index <- index + 1

            current.Unfreeze()

            Log.stop()

        let mutable defrag = DelayedTask(defragment)
        let mutable totalChanges = 0
        let mutable ranOnce = false
        let hintDefragmentation(additional : int) = 
            totalChanges <- totalChanges + abs additional
            if ranOnce && totalChanges > 2 then
                if defrag.TrySetDelay(2000) then
                    totalChanges <- 0




        member x.Add(rj : RenderJob) =
            let n = RenderJobFragment(deps, memory, manager, rj)
            fragments.Add(rj, n)
            BucketAVL.insertNeighbourhood sortedFragments n (fun prev next ->
                let prev = defaultArg prev prolog
                let next = defaultArg next epilog

                n.Prev <- prev
                prev.Next <- n
                n.Next <- next
                next.Prev <- n
                hintDefragmentation 1
            ) |> ignore

            stats.Add n.Statistics
            jumpDistance.Add n.JumpDistance
            additions <- additions + 1

        member x.Remove(rj : RenderJob) =
            match fragments.TryGetValue rj with
                | (true,f) ->
                    if not <| fragments.Remove rj then
                        failwith "inconsistent state in NativeOptimizedProgram"

                    let removed = BucketAVL.remove sortedFragments f
                    if removed then
                            
                        stats.Remove f.Statistics
                        jumpDistance.Remove f.JumpDistance

                        if f.Prev <> null then f.Prev.Next <- f.Next
                        if f.Next <> null then f.Next.Prev <- f.Prev
                        f.Dispose()
                        hintDefragmentation -1
                        removals <- removals + 1
                        true
                    else
                        failwith "inconsistent state in NativeOptimizedProgram"
                | _ -> false

        member x.Update(rj : RenderJob) =
            failwith "not implemented"
//            match fragments.TryGetValue rj with
//                | (true,f) ->
//                    if not <| fragments.Remove rj then
//                        failwith "inconsistent state in NativeOptimizedProgram"
//
//                    let removed = BucketAVL.remove sortedFragments f
//                    if removed then
//                            
//                        stats.Remove f.Statistics
//                        jumpDistance.Remove f.JumpDistance
//
//                        if f.Prev <> null then f.Prev.Next <- f.Next
//                        if f.Next <> null then f.Next.Prev <- f.Prev
//
//                        x.Add(rj)
//
//                        f.Dispose()
//                        hintDefragmentation -1
//                        removals <- removals + 1
//                            
//                    else
//                        failwith "inconsistent state in NativeOptimizedProgram"
//                | _ -> ()

                    
        member x.Run(fbo : Framebuffer, context : ContextHandle) =
            let resources = deps.GetAndClearChanges()

            let mutable resourceUpdates = 0
            for r in resources do
                if outOfDate r then
                    resourceUpdates <- resourceUpdates + 1
                    r.UpdateCPU()
                    r.UpdateGPU()
                else
                    printfn "strange"

            deps.SetCurrentContext(context)

            lock memory.PointerLock (fun () ->
                if prolog.RealPointer <> entryPtr then
                    entryPtr <- prolog.RealPointer
                    run <- UnmanagedFunctions.wrap entryPtr
                run()
            )

            ranOnce <- true
            hintDefragmentation 0
            let inner = stats.Latest
            let avgJump = float jumpDistance.Latest / float (1 + fragments.Count)

            { inner with
                ResourceUpdateCount = float resourceUpdates
                JumpDistance = avgJump
                AddedRenderJobs = float additions
                RemovedRenderJobs = float removals
            }

        member x.Dispose() =
            let mutable current = prolog
            while current <> null do
                let next = current.Next
                current.Dispose()
                current <- next

            //list.Clear()
            deps.Dispose()
            memory.Dispose()

        interface IProgram with
            member x.Add rj = x.Add rj
            member x.Remove rj = x.Remove rj |> ignore
            member x.Update rj = x.Update rj
            member x.Run(fbo,ctx) = x.Run(fbo,ctx)
            member x.Dispose() = x.Dispose()

    [<AllowNullLiteral>]
    type OptimizedManagedProgram(manager : ResourceManager, add : IAdaptiveObject -> unit, remove : IAdaptiveObject -> unit) =
        let stats = EventSourceAggregate<FrameStatistics>(FrameStatistics.Zero, (+), (-))

        let fragments = Dictionary<RenderJob, ManagedRenderJobFragment>()
        let sortedFragments = BucketAVL.custom (fun (l : ManagedRenderJobFragment) (r : ManagedRenderJobFragment) -> compareRenderJobs l.RenderJob r.RenderJob)  //AVL.custom (fun (l : RenderJobFragment) (r : RenderJobFragment) -> compareRenderJobs l.RenderJob r.RenderJob)
           
        let deps = DependencySet(add, remove)

        let first = ManagedRenderJobFragment(null, deps, manager, RenderJob.Empty)
        let mutable additions = 0
        let mutable removals = 0

        let sw = new System.Diagnostics.Stopwatch()

        member x.Add(rj : RenderJob) =
            let n = ManagedRenderJobFragment(first, deps, manager, rj)
            fragments.Add(rj, n)
            BucketAVL.insertNeighbourhood sortedFragments n (fun prev next ->
                let prev =
                    match prev with
                        | Some p -> p
                        | None -> first

                let next =
                    match next with
                        | Some p -> p
                        | None -> null

                n.Prev <- prev
                prev.Next <- n
                n.Next <- next
                if next <> null then next.Prev <- n

            ) |> ignore

            stats.Add n.Statistics
            additions <- additions + 1

        member x.Remove(rj : RenderJob) =
            match fragments.TryGetValue rj with
                | (true,f) ->
                    if not <| fragments.Remove rj then
                        failwith "inconsistent state in NativeOptimizedProgram"

                    let removed = BucketAVL.remove sortedFragments f
                    if removed then
                            
                        stats.Remove f.Statistics

                        if f.Prev <> null then f.Prev.Next <- f.Next
                        if f.Next <> null then f.Next.Prev <- f.Prev
                        f.Dispose()
                        removals <- removals + 1
                        true
                    else
                        failwith "inconsistent state in NativeOptimizedProgram"
                | _ -> false

        member x.Update(rj : RenderJob) =
            match fragments.TryGetValue rj with
                | (true,f) ->
                    if not <| fragments.Remove rj then
                        failwith "inconsistent state in NativeOptimizedProgram"

                    let removed = BucketAVL.remove sortedFragments f
                    if removed then
                            
                        stats.Remove f.Statistics

                        if f.Prev <> null then f.Prev.Next <- f.Next
                        if f.Next <> null then f.Next.Prev <- f.Prev

                        x.Add(rj)

                        f.Dispose()
                        removals <- removals + 1
                    else
                        failwith "inconsistent state in NativeOptimizedProgram"
                | _ -> ()

                    
        member x.Run(fbo : Framebuffer, context : ContextHandle) =
            let resources = deps.GetAndClearChanges()

            let mutable resourceUpdates = 0
            for r in resources do
                if outOfDate r then
                    resourceUpdates <- resourceUpdates + 1
                    r.UpdateCPU()
                    r.UpdateGPU()

            deps.SetCurrentContext(context)

            if first <> null then
                first.RunAll()
           
            let inner = stats.Latest

            { inner with
                ResourceUpdateCount = float resourceUpdates
                AddedRenderJobs = float additions
                RemovedRenderJobs = float removals
            }

        member x.Dispose() =

            //list.Clear()
            deps.Dispose()

        interface IProgram with
            member x.Add rj = x.Add rj
            member x.Remove rj = x.Remove rj |> ignore
            member x.Update rj = x.Update rj
            member x.Run(fbo,ctx) = x.Run(fbo,ctx)
            member x.Dispose() = x.Dispose()
