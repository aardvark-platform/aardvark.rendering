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
        prepare     : 'a -> Task<'b>
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
            let atomically (data : Async<'a>) (create : 'a -> Task<'b>) (destroy : 'b -> unit) =
                async {
                    let mutable res : Option<Task<'b>> = None
                    let! _ = Async.OnCancel(fun () -> match res with | Some t -> t.ContinueWith (fun (t : Task<_>) -> destroy t.Result) |> ignore | _ -> ())
                
                    let! data = data
                    let create v =
                        let r = create v
                        res <- Some r
                        r

                    return! Async.AwaitTask <| create data
                }

        [<AbstractClass>]
        type LoadTask() =
            abstract member IsRunning : bool
            abstract member HasValue : bool
            abstract member Cancel : unit -> unit
            abstract member OnCompleted : Microsoft.FSharp.Control.IEvent<unit>

            static member Start(data : Async<'a>, invoke : 'a -> Task<'b>, revoke : 'b -> unit) =
                LoadTask<'a, 'b>(data, invoke, revoke) :> LoadTask<_>

        and [<AbstractClass>] LoadTask<'a>() =
            inherit LoadTask()
            abstract member Value : 'a

        and private LoadTask<'a, 'b>(computation : Async<'a>, invoke : 'a -> Task<'b>, revoke : 'b -> unit) =
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
                prepare         : 'a -> Task<'b>
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

            let rec load<'s, 'a, 'b when 's :> LodTreeNode<'s, 'a> and 's : not struct> (state : LoadTraversal<'s, 'a, 'b>) (ready : unit -> unit) (current : Option<LoadedNode<'s, 'a, 'b>>) (node : Option<'s>) =
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
                                task.OnCompleted.Add ready
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
                        ready()

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
                                        ready()
                                    | None ->
                                        ()

                                n.Load |> Option.map (fun load -> 
                                    let task = LoadTask.Start(load, state.prepare, state.destroy)
                                    task.OnCompleted.Add ready
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


            type LoadedTree<'s, 'a, 'b when 's :> LodTreeNode<'s, 'a> and 's : not struct>(create : 'a -> Task<'b>, destroy : 'b -> unit, tree : LodTreeView<'s, 'a>) =
                inherit Mod.AbstractMod<Option<LoadedNode<'s, 'a, 'b>>>()

                let mutable current = None

                let readyLock = obj()
                let mutable readyCount = 0

                let dead = System.Collections.Generic.List<'b>()

                let traversal (token : AdaptiveToken) =
                    {
                        prepare         = create
                        destroy         = fun b -> lock dead (fun () -> dead.Add b)
                        visible         = tree.visible.GetValue token
                        descend         = tree.descend.GetValue token
                    }
        
                let trigger() =
                    lock readyLock (fun () ->
                        inc &readyCount
                    )

                override x.Mark() =
                    trigger()
                    //MVar.put readyState ()
                    true

                member x.TakeReady() =
                    lock readyLock (fun () ->
                        let c = readyCount
                        readyCount <- 0
                        c
                    )

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

                    let c = load traversal trigger current root
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
                        false

                    | Some o, None ->
                        o.pvalue |> Option.iter state.revoke
                        o.pchildren |> MapExt.iter (fun _ c -> delta state (Some c) None |> ignore)
                        true
                
                    | None, Some n ->
                        n.pvalue |> Option.iter state.invoke
                        n.pchildren |> MapExt.iter (fun _ c -> delta state None (Some c) |> ignore)
                        true

                    | Some o, Some n ->
                        if o == n then
                            false
                        else
                            let selfChanged = 
                                match o.pvalue, n.pvalue with
                                    | Some o, Some n when not (Unchecked.equals o n) ->
                                        state.revoke o
                                        state.invoke n
                                        true
                                    | None, Some n ->
                                        state.invoke n
                                        true
                                    | Some o, None ->
                                        state.revoke o
                                        true
                                    | _ ->
                                        false

                            let mutable changed = selfChanged

                            let merge _ o n =
                                let c = delta state o n
                                changed <- changed || c
                                None

                            MapExt.choose2 merge o.pchildren n.pchildren |> ignore
                            changed

        type Loader<'s, 'a, 'b when 's :> LodTreeNode<'s, 'a> and 's : not struct>(tree : LodTreeView<'s, 'a>, config : LodTreeLoaderConfig<'a, 'b>) =

            let loaded = LoadedTree.LoadedTree(config.prepare, config.delete, tree)

            let prep =        
                {
                    invoke = config.activate
                    revoke  = config.deactivate
                }

             

            let run (o : obj) =
                use delay = new MultimediaTimer.Trigger(5)

                let mutable current = None
                while true do
                    delay.Wait()
                    let v = loaded.GetValue AdaptiveToken.Top

                    let cnt = loaded.TakeReady()
                    if cnt <> 0 then
                        try
                            let n = PreparedTree.snapshot tree.showInner v
                            let changed = PreparedTree.delta prep current n
                            current <- n

                            if changed then
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







type ILodTreeNode =
    abstract member Level : int
    abstract member Name : string
    abstract member Root : ILodTreeNode
    abstract member Parent : Option<ILodTreeNode>
    abstract member Children : seq<ILodTreeNode>

    abstract member DataSource : Symbol
    abstract member DataSize : int
    abstract member TotalDataSize : int
    abstract member GetData : ct : CancellationToken * inputs : MapExt<string, Type> -> IndexedGeometry * MapExt<string, Array>

    abstract member ShouldSplit : float * float * Trafo3d * Trafo3d -> bool
    abstract member ShouldCollapse : float * float * Trafo3d * Trafo3d -> bool
        
    abstract member SplitQuality : float * Trafo3d * Trafo3d -> float
    abstract member CollapseQuality : float * Trafo3d * Trafo3d -> float

    abstract member WorldBoundingBox : Box3d
    abstract member WorldCellBoundingBox : Box3d
    abstract member Cell : Cell

    abstract member DataTrafo : Trafo3d

    abstract member Acquire : unit -> unit
    abstract member Release : unit -> unit

type SimplePickTree(  _original : ILodTreeNode,
                      _bounds : Box3d,
                      _positions : V3f[],
                      _trafo : IMod<Trafo3d>,
                      _dataTrafo : Trafo3d,
                      _attributes : MapExt<Symbol, Array>,
                      _uniforms : MapExt<string, Array>,
                      _children : Lazy<list<SimplePickTree>>) =

    let _bvh = lazy ( _children.Value |> List.toArray |> Aardvark.Base.Geometry.BvhTree.create (fun c -> c.bounds) )
    member x.original = _original
    member x.bounds = _bounds
    member x.positions = _positions
    member x.attributes = _attributes
    member x.uniforms = _uniforms
    member x.children = _children.Value
    member x.bvh = _bvh.Value
    member x.trafo = _trafo
    member x.dataTrafo = _dataTrafo

type LodTreeInstance =
    {
        root        : ILodTreeNode
        uniforms    : MapExt<string, IMod>
    }