namespace Aardvark.Rendering.GL.Compiler

open System
open System.Collections.Generic

open Aardvark.Base.Incremental
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.GL

[<AllowNullLiteral>]
type private UnoptimizedRenderObjectFragment<'f when 'f :> IDynamicFragment<'f> and 'f : null>
    private (precompiled : Option<'f>, rj : RenderObject, ctx : CompileContext<'f>) =

    let mutable next : UnoptimizedRenderObjectFragment<'f> = null
    let mutable prev : UnoptimizedRenderObjectFragment<'f> = null
    let mutable currentProgram : Option<AdaptiveCode> = None
    let mutable currentChanger = Mod.constant FrameStatistics.Zero
    let mutable frag : 'f = match precompiled with | Some p -> p | None -> null
    let mutable lastSurface = None

    let recompile() =
        let currentSurface = rj.Surface.GetValue()

        match lastSurface with
            | Some s when s = currentSurface -> FrameStatistics.Zero
            | _ ->
                lastSurface <- Some currentSurface
                match frag with
                    | null -> frag <- ctx.handler.Create []
                    | _ -> frag.Clear()

                let prog, resTime= DeltaCompiler.compileFull ctx.manager ctx.currentContext rj
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
                resTime


    let changer = 
        let self = ref Unchecked.defaultof<_>
        self :=
            Mod.custom (fun () ->
                if prev <> null then
                    let oldStats = match frag with | null -> FrameStatistics.Zero | frag -> frag.Statistics
                    currentChanger.RemoveOutput !self
                    let resTime = recompile()
                    let _ = currentChanger |> Mod.force
                    currentChanger.AddOutput !self
                    let newStats = frag.Statistics
                    transact (fun () ->
                        Mod.change ctx.statistics (ctx.statistics.Value + newStats - oldStats)
                    )
                    resTime
                else FrameStatistics.Zero
            )
        match rj.Surface with
            | null -> ()
            | s -> s.AddOutput !self
        !self

    member x.Dispose() =
        match frag with
            | null -> ()
            | _ -> 
                match rj.Surface with
                    | null -> ()
                    | s -> s.RemoveOutput changer

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
        currentChanger <- Mod.constant FrameStatistics.Zero
        lastSurface <- None
        prev <- null
        next <- null

    member x.Changer = 
        match precompiled with
            | None -> changer
            | _ -> currentChanger

    member x.RenderObject = rj

    member x.Fragment : 'f = 
        match precompiled with
            | Some p -> p
            | None ->
                Mod.force changer |> ignore
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
        and set (n : UnoptimizedRenderObjectFragment<'f>) = 
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
        and set (p : UnoptimizedRenderObjectFragment<'f>) = 
            match frag with
                | null -> ()
                | _ ->
                    match p.FragmentOption with
                        | Some p -> 
                            frag.Prev <- p
                            p.Next <- frag
                        | None -> ()
            prev <- p

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    new(rj : RenderObject, ctx : CompileContext<'f>) = new UnoptimizedRenderObjectFragment<'f>(None, rj, ctx)
    new(precompiled : 'f, ctx : CompileContext<'f>) = new UnoptimizedRenderObjectFragment<'f>(Some precompiled, RenderObject.Empty, ctx)


type UnoptimizedProgram<'f when 'f :> IDynamicFragment<'f> and 'f : null>
        (config : BackendConfiguration,
         newHandler : unit -> IFragmentHandler<'f>, 
         manager : ResourceManager, 
         addInput : IAdaptiveObject -> unit, 
         removeInput : IAdaptiveObject -> unit) =
    
    let sw = System.Diagnostics.Stopwatch()

    let sorter = RenderObjectSorters.ofSorting config.sorting
    let currentContext = Mod.init (match ContextHandle.Current with | Some ctx -> ctx | None -> null)
    let handler = newHandler()
    let changeSet = ChangeSet(addInput, removeInput)
    let resourceSet = ResourceSet(addInput, removeInput)
    let statistics = Mod.init FrameStatistics.Zero

    let ctx = { statistics = statistics; handler = handler; manager = manager; currentContext = currentContext; resourceSet = resourceSet }

    let sortedFragments = SortedDictionaryExt<RenderObject, UnoptimizedRenderObjectFragment<'f>>(curry sorter.Compare)
    let fragments = Dict<RenderObject, UnoptimizedRenderObjectFragment<'f>>()

    let mutable prolog = new UnoptimizedRenderObjectFragment<'f>(handler.Prolog, ctx)
    let mutable epilog = new UnoptimizedRenderObjectFragment<'f>(handler.Epilog, ctx)
    let mutable run = handler.Compile ()


    member x.Dispose() =
        run <- fun _ -> failwith "cannot run disposed program"

        for (KeyValue(_,f)) in fragments do
            changeSet.Unlisten f.Changer
            f.Dispose()

        fragments.Clear()
        sortedFragments.Clear()
        handler.Dispose()

        handler.Delete prolog.Fragment
        handler.Delete epilog.Fragment
        prolog <- null
        epilog <- null

    member x.Add (rj : RenderObject) =
        sorter.Add rj
        // create a new RenderJobFragment and link it
        let fragment = 
            sortedFragments |> SortedDictionary.setWithNeighbours rj (fun l s r -> 
                match s with
                    | Some f ->
                        failwithf "duplicated renderobject: %A" f.RenderObject
                    | None ->
                        let l = match l with | Some (_,l) -> l | None -> prolog
                        let r = match r with | Some (_,r) -> r | None -> epilog

                        let f = new UnoptimizedRenderObjectFragment<'f>(rj, ctx)
                        f.Prev <- l
                        l.Next <- f

                        f.Next <- r
                        r.Prev <- f

                        f
            ) 

        fragments.[rj] <- fragment
        
        // listen to changes
        changeSet.Listen fragment.Changer

    member x.Remove (rj : RenderObject) =
        match fragments.TryRemove rj with
            | (true, f) ->
                sortedFragments |> SortedDictionary.remove rj |> ignore

                // detach the fragment
                f.Prev.Next <- f.Next
                f.Next.Prev <- f.Prev
                
                // no longer listen for changes
                changeSet.Unlisten f.Changer

                
                sorter.Remove rj

                // finally dispose the fragment
                f.Dispose()

            | _ ->
                failwithf "cannot remove unknown renderobject: %A" rj

    member x.Run(fbo : int, ctx : ContextHandle) =
        // change the current context if necessary
        if ctx <> currentContext.UnsafeCache then
            transact (fun () -> Mod.change currentContext ctx)

        // update resources and instructions
        let resourceUpdates, resourceUpdateCounts, resourceUpdateTime = 
            resourceSet.Update()

        let instructionUpdates, instructionUpdateTime, createStats = 
            changeSet.Update() 

        sw.Restart()
        // run everything
        run prolog.Fragment
        sw.Stop()

        let fragmentStats = Mod.force statistics
        let programStats = 
            { FrameStatistics.Zero with 
                Programs = 1.0 
                InstructionUpdateCount = float instructionUpdates
                InstructionUpdateTime = instructionUpdateTime - createStats.ResourceUpdateTime
                ResourceUpdateCount = float resourceUpdates
                ResourceUpdateCounts = resourceUpdateCounts
                ResourceUpdateTime = resourceUpdateTime 
                ExecutionTime = sw.Elapsed
            }

        fragmentStats + programStats + createStats |> handler.AdjustStatistics

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface IProgram with
        member x.Resources = resourceSet.Resources
        member x.RenderObjects = fragments.Keys
        member x.Add rj = x.Add rj
        member x.Remove rj = x.Remove rj
        member x.Run (fbo, ctx) = x.Run(fbo, ctx)

