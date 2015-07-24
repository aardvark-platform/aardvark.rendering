namespace Aardvark.Rendering.GL.Compiler

open System
open System.Collections.Generic

open Aardvark.Base.Incremental
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.GL

type CompileContext<'f when 'f :> IDynamicFragment<'f>> =
    {
        statistics : ModRef<FrameStatistics>
        handler : IFragmentHandler<'f>
        manager : ResourceManager
        currentContext : IMod<ContextHandle>
        resourceSet : ResourceSet
    }

[<AllowNullLiteral>]
type private OptimizedRenderJobFragment<'f when 'f :> IDynamicFragment<'f> and 'f : null>
    private (precompiled : Option<'f>, rj : RenderJob, ctx : CompileContext<'f>) =

    let mutable next : OptimizedRenderJobFragment<'f> = null
    let mutable prev : OptimizedRenderJobFragment<'f> = null
    let mutable currentProgram : Option<AdaptiveCode> = None
    let mutable currentChanger = Mod.constant ()
    let mutable frag : 'f = match precompiled with | Some p -> p | None -> null
    let mutable lastPrevAndSurface = None

    let recompile() =
        let prevRj =
            match prev with
                | null -> RenderJob.Empty
                | _ -> prev.RenderJob

        let currentSurface = rj.Surface.GetValue()
        let changed =
            match lastPrevAndSurface with
                | Some (p, s) when p = prev && s = currentSurface -> false
                | _ -> true

        if changed then
            lastPrevAndSurface <- Some (prev, currentSurface)

            match frag with
                | null -> frag <- ctx.handler.Create []
                | _ ->  frag.Clear()

            let prog = DeltaCompiler.compileDelta ctx.manager ctx.currentContext prevRj rj
            let changer = AdaptiveCode.writeTo prog frag
            
            // remove old resources/changers
            match currentProgram with
                | Some old ->
                    for r in old.Resources do
                        r.Dispose()
                        ctx.resourceSet.Unlisten r
                    currentProgram <- None
                | None ->
                    ()

            // link the fragments
            let pf = prev.Fragment
            pf.Next <- frag
            frag.Prev <- pf
                
            match next.FragmentOption with
                | Some nf ->
                    nf.Prev <- frag
                    frag.Next <- nf
                | None ->
                    ()


            // listen to changes
            for r in prog.Resources do
                ctx.resourceSet.Listen r

            // store everything
            currentChanger <- changer
            currentProgram <- Some prog

    let changer = 
        let self = ref Unchecked.defaultof<_>
        self :=
            Mod.custom (fun () ->
                if prev <> null then
                    let oldStats = match frag with | null -> FrameStatistics.Zero | frag -> frag.Statistics
                    currentChanger.RemoveOutput !self
                    recompile()
                    currentChanger |> Mod.force
                    currentChanger.AddOutput !self
                    let newStats = frag.Statistics
                    transact (fun () ->
                        Mod.change ctx.statistics (ctx.statistics.Value + newStats - oldStats)
                    )
            )
        rj.Surface.AddOutput !self
        !self

    member x.Dispose() =
        match frag with
            | null -> ()
            | _ -> 
                rj.Surface.RemoveOutput changer
                match frag.Next with
                    | null -> ()
                    | n ->  n.Prev <- frag.Prev

                match frag.Prev with
                    | null -> ()
                    | p ->  p.Next <- frag.Next

                let stats = frag.Statistics
                transact (fun () ->
                    Mod.change ctx.statistics (ctx.statistics.Value - stats)
                )

                ctx.handler.Delete frag
                frag <- null

        match currentProgram with
            | Some prog ->
                for r in prog.Resources do
                    ctx.resourceSet.Unlisten r
                    r.Dispose()

                currentProgram <- None
            | None ->
                ()


        currentChanger.RemoveOutput changer
        currentChanger <- Mod.constant ()
        lastPrevAndSurface <- None
        prev <- null
        next <- null

    member x.Changer = 
        match precompiled with
            | None -> changer
            | _ -> currentChanger

    member x.RenderJob = rj

    member x.Fragment : 'f = 
        match precompiled with
            | Some p -> p
            | None ->
                Mod.force changer
                frag

    member private x.FragmentOption : Option<'f> = 
        match precompiled with
            | Some p -> Some p
            | None ->
                match frag with
                    | null -> None
                    | _ -> Some frag

    member x.Next
        with get() = next
        and set (n : OptimizedRenderJobFragment<'f>) = 
            match frag with
                | null -> ()
                | _ ->
                    match n.FragmentOption with
                        | Some n -> 
                            frag.Next <- n
                            n.Prev <- frag
                        | None -> ()
            next <- n

    member x.Prev
        with get() = prev
        and set (p : OptimizedRenderJobFragment<'f>) = 
            match frag with
                | null -> ()
                | _ ->
                    match p.FragmentOption with
                        | Some p -> 
                            frag.Prev <- p
                            p.Next <- frag
                        | None -> ()
            prev <- p
            transact (fun () -> changer.MarkOutdated())

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    new(rj : RenderJob, ctx : CompileContext<'f>) = new OptimizedRenderJobFragment<'f>(None, rj, ctx)
    new(precompiled : 'f, ctx : CompileContext<'f>) = new OptimizedRenderJobFragment<'f>(Some precompiled, RenderJob.Empty, ctx)


type OptimizedProgram<'f when 'f :> IDynamicFragment<'f> and 'f : null>
    (config : BackendConfiguration, newHandler : unit -> IFragmentHandler<'f>, manager : ResourceManager, addInput : IAdaptiveObject -> unit, removeInput : IAdaptiveObject -> unit) =
    
    let sorter = RenderJobSorters.ofSorting config.sorting
    let sw = System.Diagnostics.Stopwatch()
    let currentContext = Mod.init (match ContextHandle.Current with | Some ctx -> ctx | None -> null)
    let handler = newHandler()
    let changeSet = ChangeSet(addInput, removeInput)
    let resourceSet = ResourceSet(addInput, removeInput)
    let statistics = Mod.init FrameStatistics.Zero

    let ctx = { statistics = statistics; handler = handler; manager = manager; currentContext = currentContext; resourceSet = resourceSet }

    let sortedFragments = SortedDictionaryExt<RenderJob, OptimizedRenderJobFragment<'f>>(curry sorter.Compare)
    let fragments = Dict<RenderJob, OptimizedRenderJobFragment<'f>>()

    let mutable prolog = new OptimizedRenderJobFragment<'f>(handler.Prolog, ctx)
    let mutable epilog = new OptimizedRenderJobFragment<'f>(handler.Epilog, ctx)
    let mutable run = handler.Compile ()


    member x.Dispose() =
        handler.Dispose()
        run <- fun _ -> failwith "cannot run disposed program"

        for (KeyValue(_,f)) in fragments do
            changeSet.Unlisten f.Changer
            f.Dispose()

        fragments.Clear()
        sortedFragments.Clear()

        handler.Delete prolog.Fragment
        handler.Delete epilog.Fragment
        prolog <- null
        epilog <- null

    member x.Add (rj : RenderJob) =

        sorter.Add rj

        // create a new RenderJobFragment and link it
        let fragment = 
            sortedFragments |> SortedDictionary.setWithNeighbours rj (fun l s r -> 
                match s with
                    | Some f ->
                        failwithf "duplicated renderjob: %A" f.RenderJob
                    | None ->
                        let l = match l with | Some (_,l) -> l | None -> prolog
                        let r = match r with | Some (_,r) -> r | None -> epilog

                        let f = new OptimizedRenderJobFragment<'f>(rj, ctx)
                        f.Prev <- l
                        l.Next <- f

                        f.Next <- r
                        r.Prev <- f

                        f
            ) 

        fragments.[rj] <- fragment
        
        handler.Hint(AddRenderJob 1)
        // listen to changes
        changeSet.Listen fragment.Changer

    member x.Remove (rj : RenderJob) =
        match fragments.TryRemove rj with
            | (true, f) ->
                
                sortedFragments |> SortedDictionary.remove rj |> ignore

                // detach the fragment
                f.Prev.Next <- f.Next
                f.Next.Prev <- f.Prev
                
                // no longer listen for changes
                changeSet.Unlisten f.Changer

                // finally dispose the fragment
                f.Dispose()

                sorter.Remove rj
                handler.Hint(RemoveRenderJob 1)

            | _ ->
                failwithf "cannot remove unknown renderjob: %A" rj

    member x.Run(fbo : int, ctx : ContextHandle) =
        // change the current context if necessary
        if ctx <> currentContext.UnsafeCache then
            transact (fun () -> Mod.change currentContext ctx)

        // update resources and instructions
        let resourceUpdates, resourceUpdateTime = 
            resourceSet.Update()

        let instructionUpdates, instructionUpdateTime = 
            changeSet.Update() 

        handler.Hint(RunProgram)

        sw.Restart()
        // run everything
        run prolog.Fragment
        sw.Stop()

        let stats = 
            { Mod.force statistics with 
                Programs = 1.0 
                InstructionUpdateCount = float instructionUpdates
                InstructionUpdateTime = instructionUpdateTime
                ResourceUpdateCount = float resourceUpdates
                ResourceUpdateTime = resourceUpdateTime
                ExecutionTime = sw.Elapsed
            }

        stats |> handler.AdjustStatistics

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface IProgram with
        member x.RenderJobs = fragments.Keys
        member x.Add rj = x.Add rj
        member x.Remove rj = x.Remove rj
        member x.Run (fbo, ctx) = x.Run(fbo, ctx)

