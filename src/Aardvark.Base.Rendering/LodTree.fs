namespace Aardvark.Base

open Aardvark.Base.Rendering
open System
open System.Threading
open System.Threading.Tasks
open Aardvark.Base.Incremental


type LodTreeNode<'a> =
    abstract member Load : Option<Async<'a>>

type LodTreeNode<'s, 'a when 's :> LodTreeNode<'s, 'a>> =
    inherit LodTreeNode<'a>
    abstract member Children : MapExt<int, 's>

type LodTreeView<'s, 'a when 's :> LodTreeNode<'s, 'a>> =
    {
        root        : IMod<Option<'s>>
        visible     : IMod<'s -> bool>
        descend     : IMod<'s -> bool>
        showInner   : bool
    }



type LodTreeLoaderConfig<'a, 'b> =
    {
        prepare     : 'a -> 'b
        delete      : 'b -> unit
        activate    : 'b -> unit
        deactivate  : 'b -> unit
        flush       : unit -> unit
    }

type LodTreeLoader<'a> =
    abstract member Start : LodTreeLoaderConfig<'a, 'b> -> IDisposable

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module LodTreeLoader = 
    [<AutoOpen>]
    module private Helpers = 
        module Async =
            let atomically (data : Async<'a>) (create : 'a -> 'b) (destroy : 'b -> unit) =
                async {
                    let mutable res = None
                    let! _ = Async.OnCancel(fun () -> Option.iter destroy res)
                
                    let! data = data
                    let create v =
                        let r = create v
                        res <- Some r
                        r

                    return create data
                }

        [<AbstractClass>]
        type LoadTask() =
            abstract member IsRunning : bool
            abstract member HasValue : bool
            abstract member Cancel : unit -> unit
            abstract member OnCompleted : Microsoft.FSharp.Control.IEvent<unit>

            static member Start(data : Async<'a>, invoke : 'a -> 'b, revoke : 'b -> unit) =
                LoadTask<'a, 'b>(data, invoke, revoke) :> LoadTask<_>

        and [<AbstractClass>] LoadTask<'a>() =
            inherit LoadTask()
            abstract member Value : 'a

        and private LoadTask<'a, 'b>(computation : Async<'a>, invoke : 'a -> 'b, revoke : 'b -> unit) =
            inherit LoadTask<'b>()

            let cancel = new CancellationTokenSource()

            let run =
                Async.atomically
                    computation
                    invoke
                    revoke

            let task = Async.StartAsTask(run, cancellationToken = cancel.Token)
            let completed = Event<unit>()
            let _ =
                task.ContinueWith(fun (t : Task<_>) ->
                    if not t.IsCanceled && not t.IsFaulted then
                        completed.Trigger()
                )

            override x.OnCompleted = completed.Publish

            override x.IsRunning =
                if cancel.IsCancellationRequested then 
                    false
                else
                    task.Status <> TaskStatus.Canceled &&
                    task.Status <> TaskStatus.Faulted &&
                    task.Status <> TaskStatus.RanToCompletion

            override x.HasValue = 
                if cancel.IsCancellationRequested then
                    false
                elif task.IsCompleted then
                    not task.IsCanceled && not task.IsFaulted
                else
                    false

            override x.Cancel() = cancel.Cancel()
            override x.Value = 
                if cancel.IsCancellationRequested then
                    raise <| OperationCanceledException()
                else 
                    task.Result

        type LoadTraversal<'i, 'a, 'b> =
            {
                prepare         : 'a -> 'b
                destroy         : 'b -> unit
                visible         : 'i -> bool
                descend         : 'i -> bool
            }

        module LoadedTree = 
            type LoadedNode<'s, 'a, 'b when 's :> LodTreeNode<'s, 'a>> =
                {
                    original    : 's
                    task        : Option<LoadTask<'b>>
                    children    : MapExt<int, LoadedNode<'s, 'a, 'b>>
                }

            let rec destroy (state : LoadTraversal<'i, 'a, 'b>) (current : LoadedNode<'i, 'a, 'b>) =
                match current.task with
                    | Some t -> t.Cancel()
                    | None -> ()
                for (KeyValue(_,c)) in current.children do
                    destroy state c

            let rec load<'s, 'a, 'b when 's :> LodTreeNode<'s, 'a> and 's : not struct> (state : LoadTraversal<'s, 'a, 'b>) (ready : MVar<unit>) (current : Option<LoadedNode<'s, 'a, 'b>>) (node : Option<'s>) =
                let node =
                    match node with
                        | Some n when state.visible n -> Some n
                        | _ -> None

                match current, node with
                    | None, None ->
                        None

                    | None, Some n ->
                        let descend = state.descend n

                        let task = 
                            n.Load |> Option.map (fun load -> 
                                let task = LoadTask.Start(load, state.prepare, state.destroy)
                                task.OnCompleted.Add ready.Put
                                task
                            )

                        let children = 
                            if descend then n.Children |> MapExt.choose (fun _ c -> load state ready None (Some c))
                            else MapExt.empty

                        Some {
                            original = n
                            task = task
                            children = children
                        }

                    | Some o, None ->
                        destroy state o
                        ready.Put()

                        None

                    | Some o, Some n ->
                        let descend = state.descend n

                        let task = 
                            if o.original == n then 
                                o.task
                            else 
                                match o.task with
                                    | Some t -> 
                                        t.Cancel()
                                        ready.Put()
                                    | None ->
                                        ()

                                n.Load |> Option.map (fun load -> 
                                    let task = LoadTask.Start(load, state.prepare, state.destroy)
                                    task.OnCompleted.Add ready.Put
                                    task
                                )

                        let children = 
                            if descend then 
                                MapExt.choose2 (fun _ -> load state ready) o.children n.Children
                            else
                                o.children |> MapExt.iter (fun _ c -> load state ready (Some c) None |> ignore)
                                MapExt.empty

                        Some {
                            original = n
                            task = task
                            children = children
                        }


            type LoadedTree<'s, 'a, 'b when 's :> LodTreeNode<'s, 'a> and 's : not struct>(create : 'a -> 'b, destroy : 'b -> unit, tree : LodTreeView<'s, 'a>) =
                inherit Mod.AbstractMod<Option<LoadedNode<'s, 'a, 'b>>>()

                let mutable current = None

                let readyState = MVar.create()
                let dead = System.Collections.Generic.List<'b>()

                let traversal (token : AdaptiveToken) =
                    {
                        prepare         = create
                        destroy         = fun b -> lock dead (fun () -> dead.Add b)
                        visible         = tree.visible.GetValue token
                        descend         = tree.descend.GetValue token
                    }
        
                override x.Mark() =
                    MVar.put readyState ()
                    true

                member x.ReadyState = readyState

                member x.KillUnused() =
                    let dead = 
                        lock dead (fun () -> 
                            let arr = CSharpList.toArray dead
                            dead.Clear()
                            arr
                        )
                    dead |> Array.iter destroy

                override x.Compute(token : AdaptiveToken) =
                    let root = tree.root.GetValue token
                    let traversal = traversal token

                    let c = load traversal readyState current root
                    current <- c
                    c

        type PrepareTraveral<'b> =
            {
                revoke      : 'b -> unit
                invoke      : 'b -> unit
            }

        module PreparedTree =
            type PreparedTree<'b> =
                {
                    pvalue : Option<'b>
                    pchildren : MapExt<int, PreparedTree<'b>>
                }


            let rec snapshot (showInner : bool) (l : Option<LoadedTree.LoadedNode<'i, 'a, 'b>>) =
                match l with
                    | None ->
                        None
                    | Some l ->
                        match l.task with
                            | Some t ->
                                if t.HasValue then
                                    let children = l.children |> MapExt.choose (fun _ c -> snapshot showInner (Some c))

                                    if showInner then
                                        Some { pvalue = Some t.Value; pchildren = children }
                                    else
                                        let allReady = children.Count > 0 && children.Count = l.children.Count
                                        if allReady then
                                            Some { pvalue = None; pchildren = children }
                                        else
                                            Some { pvalue = Some t.Value; pchildren = MapExt.empty }

                                else
                                    None

                            | None ->
                                let children = l.children |> MapExt.choose (fun _ c -> snapshot showInner (Some c))
                                Some { pvalue = None; pchildren = children }
                
            let rec delta (state : PrepareTraveral<'b>) (o : Option<PreparedTree<'b>>) (n : Option<PreparedTree<'b>>) =
                match o, n with
                    | None, None -> 
                        ()

                    | Some o, None ->
                        o.pvalue |> Option.iter state.revoke
                        o.pchildren |> MapExt.iter (fun _ c -> delta state (Some c) None)
                
                    | None, Some n ->
                        n.pvalue |> Option.iter state.invoke
                        n.pchildren |> MapExt.iter (fun _ c -> delta state None (Some c))

                    | Some o, Some n ->
                        match o.pvalue, n.pvalue with
                            | Some o, Some n when not (Unchecked.equals o n) ->
                                state.revoke o
                                state.invoke n
                            | None, Some n ->
                                state.invoke n
                            | Some o, None ->
                                state.revoke o
                            | _ ->
                                ()

                        MapExt.choose2 (fun _ o n -> delta state o n; None) o.pchildren n.pchildren |> ignore

        type Loader<'s, 'a, 'b when 's :> LodTreeNode<'s, 'a> and 's : not struct>(tree : LodTreeView<'s, 'a>, config : LodTreeLoaderConfig<'a, 'b>) =

            let loaded = LoadedTree.LoadedTree(config.prepare, config.delete, tree)

            let prep =        
                {
                    invoke = config.activate
                    revoke  = config.deactivate
                }

             

            let run (o : obj) =
                let mutable current = None
                while true do
                    MVar.take loaded.ReadyState


                    try
                        let v = loaded.GetValue AdaptiveToken.Top


                        let n = PreparedTree.snapshot tree.showInner v
                        PreparedTree.delta prep current n
                        current <- n

                        config.flush()

                        loaded.KillUnused()
                    with e ->
                        Log.error "Loader faulted: %A" e
                    

            let thread = Thread(ThreadStart(run), IsBackground = true)
            do thread.Start()

            member x.Dispose() =
                ()

            interface IDisposable with
                member x.Dispose() = x.Dispose()

    let create (view : LodTreeView<'s, 'a>)  =
        { new LodTreeLoader<'a> with
            member x.Start cfg =
                new Loader<'s, 'a, 'b>(view, cfg) :> IDisposable
        }

    let inline start (cfg : LodTreeLoaderConfig<'a, 'b>) (l : LodTreeLoader<'a>) = l.Start(cfg)

//
//type LodTreeRenderObject(scope : Ag.Scope, pass : RenderPass, surface : Surface, state : PipelineState, tree : LodTreeLoader<Geometry>) =
//    let id = newId()
//    
//    member x.Id = id
//    member x.AttributeScope = scope
//    member x.RenderPass = pass
//
//    member x.Surface = surface
//    member x.PipelineState = state
//    member x.Tree = tree
//
//    interface IRenderObject with
//        member x.Id = id
//        member x.AttributeScope = scope
//        member x.RenderPass = pass
//
