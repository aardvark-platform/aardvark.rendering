namespace Aardvark.Rendering.GL.Compiler

open System
open System.Collections.Generic

open Aardvark.Base.Incremental
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.GL

type SortedProgram<'f when 'f :> IDynamicFragment<'f> and 'f : null>
        (newHandler : unit -> IFragmentHandler<'f>, 
         newSorter : unit -> IDynamicRenderObjectSorter,
         manager : ResourceManager, 
         addInput : IAdaptiveObject -> unit, 
         removeInput : IAdaptiveObject -> unit) =
    
    let sw = System.Diagnostics.Stopwatch()
    let currentContext = Mod.init (match ContextHandle.Current with | Some ctx -> ctx | None -> null)
    let handler = newHandler()
    let changeSet = ChangeSet(addInput, removeInput)
    let resourceSet = ResourceSet(addInput, removeInput)
    let statistics = Mod.init FrameStatistics.Zero

    let ctx = { statistics = statistics; handler = handler; manager = manager; currentContext = currentContext; resourceSet = resourceSet }

    let sorter = newSorter()
    
    let fragments = Dict<RenderObject, UnoptimizedRenderObjectFragment<'f>>()
    let sortedRenderObjects = sorter.SortedList
    do addInput sortedRenderObjects

    let mutable prolog = new UnoptimizedRenderObjectFragment<'f>(handler.Prolog, ctx)
    let mutable epilog = new UnoptimizedRenderObjectFragment<'f>(handler.Epilog, ctx)
    let mutable run = handler.Compile ()



    member x.Dispose() =
        run <- fun _ -> failwith "cannot run disposed program"

        for (KeyValue(_,f)) in fragments do
            changeSet.Unlisten f.Changer
            f.Dispose()

        fragments.Clear()
        
        handler.Dispose()

        handler.Delete prolog.Fragment
        handler.Delete epilog.Fragment
        prolog <- null
        epilog <- null

    member x.Add (unsorted : IRenderObject) =
        1
        let unsorted = unbox unsorted
        let rj = unsorted |> sorter.ToSortedRenderObject
        sorter.Add rj

        // create a new RenderObjectFragment and link it
        let fragment = new UnoptimizedRenderObjectFragment<'f>(rj, ctx)
        fragments.[rj] <- fragment
        
        // listen to changes
        changeSet.Listen fragment.Changer

    member x.Remove (rj : IRenderObject) =
        1
        let rj = unbox rj
        match fragments.TryRemove rj with
            | (true, f) ->
                sorter.Remove rj

                // detach the fragment
                f.Prev.Next <- f.Next
                f.Next.Prev <- f.Prev
                
                // no longer listen for changes
                changeSet.Unlisten f.Changer

                // finally dispose the fragment
                f.Dispose()

            | _ ->
                failwithf "cannot remove unknown renderobject: %A" rj

    member x.Run(fbo : int, ctx : ContextHandle) =
        // change the current context if necessary
        if ctx <> currentContext.UnsafeCache then
            transact (fun () -> Mod.change currentContext ctx)

        let applySorting =
            async {
                sw.Restart()
                let sorted = sortedRenderObjects |> Mod.force

                let prev = ref prolog
                for rj in sorted do
                    match fragments.TryGetValue rj with
                        | (true, f) ->
                            prev.Value.Next <- f
                            f.Prev <- !prev
                            prev := f
                        | _ ->
                            Log.warn "sorter returned unknown renderobject"

                prev.Value.Next <- epilog
                epilog.Prev <- !prev
                sw.Stop()
                return sw.Elapsed
            } |> Async.StartAsTask


        // update resources and instructions
        let resourceUpdates, resourceCounts, resourceUpdateTime = 
            resourceSet.Update()

        let instructionUpdates, instructionUpdateTime, createStats = 
            changeSet.Update() 

        // wait for the sorting
        let sortingTime = applySorting.Result

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
                ResourceUpdateCounts = resourceCounts
                ResourceUpdateTime = resourceUpdateTime 
                ExecutionTime = sw.Elapsed
                SortingTime = sortingTime
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

