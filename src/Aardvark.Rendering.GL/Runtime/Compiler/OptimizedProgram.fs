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
        inputSet : InputSet
    }

[<AllowNullLiteral>]
type private OptimizedRenderObjectFragment<'f when 'f :> IDynamicFragment<'f> and 'f : null>
    private (precompiled : Option<'f>, rj : PreparedRenderObject, ctx : CompileContext<'f>) =

    let mutable next : OptimizedRenderObjectFragment<'f> = null
    let mutable prev : OptimizedRenderObjectFragment<'f> = null
    let mutable currentProgram : Option<AdaptiveCode> = None
    let mutable currentChanger = Mod.constant FrameStatistics.Zero
    let mutable frag : 'f = match precompiled with | Some p -> p | None -> null
    let mutable lastPrev = None

    let hasProgram =
        match precompiled with
         | Some v -> false 
         | _ -> true

    let recompile() =
        let prevRj =
            match prev with
                | null -> PreparedRenderObject.Empty
                | _ -> prev.RenderObject

        match lastPrev with
            | Some (s,p) when s = prev.RenderObject && p = rj.Program.Resource.GetValue() -> FrameStatistics.Zero
            | _ ->
                lastPrev <- Some (prev.RenderObject, rj.Program.Resource.GetValue())

                match frag with
                    | null -> frag <- ctx.handler.Create []
                    | _ ->  frag.Clear()

                let prog, resTime = DeltaCompiler.compileDelta ctx.manager ctx.currentContext prevRj rj
                let changer = AdaptiveCode.writeTo prog frag
            
                // remove old resources/changers
                match currentProgram with
                    | Some old ->
                        for r in old.Resources do
                            r.Dispose()
                            ctx.inputSet.Remove r
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
                    ctx.inputSet.Add r

                // store everything
                currentChanger <- changer
                currentProgram <- Some prog
                resTime

    let changer = 
        if hasProgram
        then
            let self = ref Unchecked.defaultof<_>
            self :=
                Mod.custom (fun s ->
                    if prev <> null then
                        let oldStats = match frag with | null -> FrameStatistics.Zero | frag -> frag.Statistics
                        currentChanger.RemoveOutput !self
                        let resTime = recompile()
                        let _ = currentChanger.GetValue(s)
                        currentChanger.AddOutput !self
                        let newStats = match frag with | null -> FrameStatistics.Zero | frag -> frag.Statistics
                        transact (fun () ->
                            Mod.change ctx.statistics (ctx.statistics.Value + newStats - oldStats)
                        )
                        resTime
                    else FrameStatistics.Zero
                )
            rj.Program.AddOutput !self
            !self
        else Mod.init FrameStatistics.Zero :> IMod<_>

    member x.Dispose() =
        match frag with
            | null -> ()
            | _ -> 
                rj.Program.RemoveOutput changer 

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
                    ctx.inputSet.Remove r
                    r.Dispose()

                currentProgram <- None
            | None ->
                ()


        currentChanger.RemoveOutput changer
        currentChanger <- Mod.constant FrameStatistics.Zero
        lastPrev <- None
        prev <- null
        next <- null

    member x.Changer = 
        match precompiled with
            | None -> changer
            | _ -> currentChanger

    member x.RenderObject : PreparedRenderObject = rj

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
        and set (n : OptimizedRenderObjectFragment<'f>) = 
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
        and set (p : OptimizedRenderObjectFragment<'f>) = 
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

    new(rj : PreparedRenderObject, ctx : CompileContext<'f>) = new OptimizedRenderObjectFragment<'f>(None, rj, ctx)
    new(precompiled : 'f, ctx : CompileContext<'f>) = new OptimizedRenderObjectFragment<'f>(Some precompiled, PreparedRenderObject.Empty, ctx)


type OptimizedProgram<'f when 'f :> IDynamicFragment<'f> and 'f : null>
    (parent : IRenderTask, config : BackendConfiguration, newHandler : unit -> IFragmentHandler<'f>, manager : ResourceManager, inputSet : InputSet) =
    
    let sorter = RenderObjectSorters.ofSorting config.sorting
    let currentContext = Mod.init (match ContextHandle.Current with | Some ctx -> ctx | None -> null)
    let handler = newHandler()
    let changeSet = ChangeSet(parent, inputSet.Add, inputSet.Remove)
    let statistics = Mod.init FrameStatistics.Zero

    let ctx = { statistics = statistics; handler = handler; manager = manager; currentContext = currentContext; inputSet = inputSet }

    let sortedFragments = SortedDictionaryExt<IRenderObject, OptimizedRenderObjectFragment<'f>>(curry sorter.Compare)
    let fragments = Dict<IRenderObject, OptimizedRenderObjectFragment<'f>>()
    let preparedRenderObjects = Dict<RenderObject,PreparedRenderObject>()

    let mutable prolog = new OptimizedRenderObjectFragment<'f>(handler.Prolog, ctx)
    let mutable epilog = new OptimizedRenderObjectFragment<'f>(handler.Epilog, ctx)
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

    member x.Add (rj : IRenderObject) =

        let prep = 
            match rj with
              | :? PreparedRenderObject as p -> p
              | :? RenderObject as rj -> 
                    preparedRenderObjects.GetOrCreate(rj,fun rj -> manager.Prepare rj)
              | _ -> failwith "unsupported IRenderObject"

        sorter.Add rj

        // create a new RenderObjectFragment and link it
        let fragment = 
            sortedFragments |> SortedDictionary.setWithNeighbours rj (fun l s r -> 
                match s with
                    | Some f ->
                        failwithf "duplicated renderobject: %A" f.RenderObject
                    | None ->
                        let l = match l with | Some (_,l) -> l | None -> prolog
                        let r = match r with | Some (_,r) -> r | None -> epilog

                        let f = new OptimizedRenderObjectFragment<'f>(prep, ctx)
                        f.Prev <- l
                        l.Next <- f

                        f.Next <- r
                        r.Prev <- f

                        f
            ) 

        fragments.[rj] <- fragment
        
        handler.Hint(AddRenderObject 1)
        // listen to changes
        changeSet.Listen fragment.Changer

    member x.Remove (rj : IRenderObject) =
        let prep = 
            match rj with 
              | :? PreparedRenderObject as p -> p
              | :? RenderObject as rj -> 
                 match preparedRenderObjects.TryRemove rj with
                  | (true,v)-> v
                  | _ -> failwith "could not find associated prepared render object"
              | _ -> failwith "unsupported IRenderObject"

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
                handler.Hint(RemoveRenderObject 1)

            | _ ->
                failwithf "cannot remove unknown renderobject: %A" rj

    member x.Run(fbo : int, ctx : ContextHandle) =
        // change the current context if necessary
        if ctx <> currentContext.UnsafeCache then
            transact (fun () -> Mod.change currentContext ctx)

        let instructionUpdates, instructionUpdateTime, createStats = 
            changeSet.Update() 

        handler.Hint(RunProgram)

        // run everything
        run prolog.Fragment


        let fragmentStats = Mod.force statistics
        let programStats = 
            { FrameStatistics.Zero with 
                Programs = 1.0 
                InstructionUpdateCount = float instructionUpdates
                InstructionUpdateTime = instructionUpdateTime - createStats.ResourceUpdateTime
            }

        fragmentStats + programStats + createStats |> handler.AdjustStatistics

    member x.Disassemble() =
        let mutable fragment = prolog.Next
        let mutable last = PreparedRenderObject.Empty
        let result = System.Collections.Generic.List()
        while fragment <> epilog do
            let current = fragment.RenderObject

            let instructions = DeltaCompilerDebug.compileDeltaDebugNoResources manager currentContext last current
            result.AddRange instructions

            last <- current
            fragment <- fragment.Next
        
        result :> seq<_>


    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface IRenderProgram with
        member x.Disassemble() = x.Disassemble()
        //member x.Resources = inputSet.Resources
        member x.RenderObjects = fragments.Keys
        member x.Add rj = x.Add rj
        member x.Remove rj = x.Remove rj
        member x.Run (fbo, ctx) = x.Run(fbo, ctx)

