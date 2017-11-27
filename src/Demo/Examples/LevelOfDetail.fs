namespace Examples


open System
open System.IO
open Aardvark.Base
open Aardvark.Base.Incremental

open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering
open Aardvark.Base.ShaderReflection
open Aardvark.Rendering.Text
open System.Runtime.InteropServices
open Aardvark.SceneGraph
open Aardvark.SceneGraph.IO
open System.Threading
open System.Threading.Tasks


module LevelOfDetail =


    type Node<'a> =
        abstract member Load : Option<Async<'a>>

    type Node<'s, 'a when 's :> Node<'s, 'a>> =
        inherit Node<'a>
        abstract member Children : MapExt<int, 's>

//        
//
//    and TreeView<'s, 'a when 's :> Node<'s, 'a>> =
//        {
//            root        : 's
//            visible     : 's -> bool
//            descend     : 's -> bool
//        }
//
//        interface Node<'a> with
//            member x.Load = x.root.Load
//
//        interface Node<TreeView<'s, 'a>, 'a> with
//            member x.Children = x.root.Children |> MapExt.map (fun _ c -> { x with root = c })


    type TreeRenderConfig =
        {
            showInner   : bool
            asyncLoad   : bool 
            asyncUpdate : bool
        }

    type LoaderConfig<'s, 'a, 'b when 's :> Node<'s, 'a>> =
        {
            prepare     : 'a -> 'b
            delete      : 'b -> unit
            visible     : IMod<'s -> bool>
            descend     : IMod<'s -> bool>
        }

    type Loader<'n> =
        abstract member Start : activate : ('n -> unit) * deactivate : ('n -> unit) * flush : (unit -> unit) -> IDisposable


    module Loader = 
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
                    prepare     : 'a -> 'b
                    destroy     : 'b -> unit
                    visible     : 'i -> bool
                    descend     : 'i -> bool
                }

            module LoadedTree = 
                type LoadedNode<'s, 'a, 'b when 's :> Node<'s, 'a>> =
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

                let rec load<'s, 'a, 'b when 's :> Node<'s, 'a> and 's : not struct> (state : LoadTraversal<'s, 'a, 'b>) (ready : MVar<unit>) (current : Option<LoadedNode<'s, 'a, 'b>>) (node : Option<'s>) =
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


                type LoadedTree<'s, 'a, 'b when 's :> Node<'s, 'a> and 's : not struct>(create : 'a -> 'b, destroy : 'b -> unit, visible : IMod<'s -> bool>, descend : IMod<'s -> bool>, root : IMod<Option<'s>>) =
                    inherit Mod.AbstractMod<Option<LoadedNode<'s, 'a, 'b>>>()

                    let mutable current = None

                    let readyState = MVar.create()
                    let dead = System.Collections.Generic.List<'b>()

                    let traversal (token : AdaptiveToken) =
                        {
                            prepare     = create
                            destroy     = fun b -> lock dead (fun () -> dead.Add b)
                            visible     = visible.GetValue token
                            descend     = descend.GetValue token
                        }
        
                    let s0 = visible.AddMarkingCallback (MVar.put readyState)
                    let s1 = descend.AddMarkingCallback (MVar.put readyState)
                    let s2 = root.AddMarkingCallback (MVar.put readyState)


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
                        let root = root.GetValue token
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


                let rec snapshot (l : Option<LoadedTree.LoadedNode<'i, 'a, 'b>>) =
                    match l with
                        | None ->
                            None
                        | Some l ->
                            match l.task with
                                | Some t ->
                                    let children = l.children |> MapExt.choose (fun _ c -> snapshot (Some c))

                                    let allReady = children.Count > 0 && children.Count = l.children.Count

                                    if allReady then
                                        Some { pvalue = None; pchildren = children }
                                    else
                                        if t.HasValue then
                                            Some { pvalue = Some t.Value; pchildren = MapExt.empty }
                                        else
                                            None
                                | None ->
                                    let children = l.children |> MapExt.choose (fun _ c -> snapshot (Some c))
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

            type Loader<'s, 'a, 'b when 's :> Node<'s, 'a> and 's : not struct>(config : LoaderConfig<'s, 'a, 'b>, activate : 'b -> unit, deactivate : 'b -> unit, flush : unit -> unit, root : IMod<Option<'s>>) =

                let loaded = LoadedTree.LoadedTree(config.prepare, config.delete, config.visible, config.descend, root)

                let prep =        
                    {
                        invoke = activate
                        revoke  = deactivate
                    }

             

                let run (o : obj) =
                    let mutable current = None
                    while true do
                        MVar.take loaded.ReadyState
                        let v = loaded.GetValue AdaptiveToken.Top


                        let n = PreparedTree.snapshot v
                        PreparedTree.delta prep current n
                        current <- n

                        flush()

                        loaded.KillUnused()
                    

                let thread = Thread(ThreadStart(run), IsBackground = true)
                do thread.Start()

                member x.Dispose() =
                    ()

                interface IDisposable with
                    member x.Dispose() = x.Dispose()

        let create (config : LoaderConfig<'s, 'a, 'b>) (root : IMod<Option<'s>>) =
            { new Loader<'b> with
                member x.Start(activate, deactivate, flush) =
                    new Loader<'s, 'a, 'b>(config, activate, deactivate, flush, root) :> IDisposable
            }

        let inline start (activate : 'n -> unit)  (deactivate : 'n -> unit) (flush : unit -> unit) (l : Loader<'n>) = l.Start(activate, deactivate, flush)




    type Octree(bounds : Box3d) =
        let children = 
            lazy (
                let min = bounds.Min
                let max = bounds.Max
                let c = bounds.Center
                MapExt.ofList [
                    0, Octree(Box3d(min.X, min.Y, min.Z, c.X, c.Y, c.Z))
                    1, Octree(Box3d(min.X, min.Y, c.Z, c.X, c.Y, max.Z))
                    2, Octree(Box3d(min.X, c.Y, min.Z, c.X, max.Y, c.Z))
                    3, Octree(Box3d(min.X, c.Y, c.Z, c.X, max.Y, max.Z))
                    4, Octree(Box3d(c.X, min.Y, min.Z, max.X, c.Y, c.Z))
                    5, Octree(Box3d(c.X, min.Y, c.Z, max.X, c.Y, max.Z))
                    6, Octree(Box3d(c.X, c.Y, min.Z, max.X, max.Y, c.Z))
                    7, Octree(Box3d(c.X, c.Y, c.Z, max.X, max.Y, max.Z))
                ]
            )

        let data =
            async {
                do! Async.Sleep 100
                return bounds
            }

        member x.Bounds = bounds

        interface Node<Octree, Box3d> with
            member x.Load = Some data
            member x.Children = children.Value
 
    let runTest() =

        let tree = Octree(Box3d(-V3d.III, V3d.III))

        let desiredLen = Mod.init 1.0

        let visible (b : Octree) = true
        let descend (l : float) (b : Octree) = b.Bounds.Size.Length > l
        

        let root = Mod.constant (Some tree)

        let existing = System.Collections.Concurrent.ConcurrentHashSet<Box3d>()
        let boxes = System.Collections.Generic.HashSet<Box3d>()

        let thread =
            root
            |> Loader.create {
                prepare     = fun b -> existing.Add b |> ignore; b
                delete      = fun b -> existing.Remove b |> ignore
                visible     = Mod.constant visible
                descend     = desiredLen |> Mod.map descend
            }
            |> Loader.start (boxes.Add >> ignore) (boxes.Remove >> ignore) id


        let print =
            async {
                do! Async.SwitchToNewThread()
                let mutable oldCount = (-1, -1)
                while true do
                    Thread.Sleep 5
                    let cnt = (boxes.Count, existing.Count)
                    if cnt <> oldCount then
                        let (active, existing) = cnt
                        Log.line "count: %A (%A)" active existing
                        oldCount <- cnt

            }

        Async.Start print

        while true do
            let line = Console.ReadLine()
            match line with
                | "+" ->
                    transact (fun () -> desiredLen.Value <- desiredLen.Value / 2.0)
                | _ ->
                    transact (fun () -> desiredLen.Value <- desiredLen.Value * 2.0)
            Log.line "len: %A" desiredLen.Value



        ()





    open Aardvark.Rendering.Vulkan

    type GeometryTree(bounds : Box3d) =
        let children = 
            lazy (
                let min = bounds.Min
                let max = bounds.Max
                let c = bounds.Center
                MapExt.ofList [
                    0, GeometryTree(Box3d(min.X, min.Y, min.Z, c.X, c.Y, c.Z))
                    1, GeometryTree(Box3d(min.X, min.Y, c.Z, c.X, c.Y, max.Z))
                    2, GeometryTree(Box3d(min.X, c.Y, min.Z, c.X, max.Y, c.Z))
                    3, GeometryTree(Box3d(min.X, c.Y, c.Z, c.X, max.Y, max.Z))
                    4, GeometryTree(Box3d(c.X, min.Y, min.Z, max.X, c.Y, c.Z))
                    5, GeometryTree(Box3d(c.X, min.Y, c.Z, max.X, c.Y, max.Z))
                    6, GeometryTree(Box3d(c.X, c.Y, min.Z, max.X, max.Y, c.Z))
                    7, GeometryTree(Box3d(c.X, c.Y, c.Z, max.X, max.Y, max.Z))
                ]
            )

        let data =
            async {
                do! Async.SwitchToThreadPool()
                let sphere = Primitives.unitSphere 5
                let trafo = Trafo3d.Scale(0.5 * bounds.Size) * Trafo3d.Translation(bounds.Center)
                let uniforms = Map.ofList ["ModelTrafo", Mod.constant trafo :> IMod]

                return Geometry.ofIndexedGeometry uniforms sphere
            }

        member x.Bounds = bounds

        interface Node<GeometryTree, Geometry> with
            member x.Load = Some data
            member x.Children = children.Value
        

    let run() =

        let app = new VulkanApplication(true)

        let win = app.CreateSimpleRenderWindow(8) 

        let view =
            CameraView.lookAt (V3d(6,6,6)) V3d.Zero V3d.OOI
                |> DefaultCameraController.control win.Mouse win.Keyboard win.Time
                |> Mod.map CameraView.viewTrafo

        let proj =
            win.Sizes 
                |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 1000.0 (float s.X / float s.Y))
                |> Mod.map Frustum.projTrafo


        let mutable frozen = false
        let views = DefaultingModRef (view |> Mod.map Array.singleton)
        let projs = DefaultingModRef (proj |> Mod.map Array.singleton)

        let toggleFreeze() =
            transact (fun () ->
                if frozen then
                    views.Reset()
                    projs.Reset()
                else
                    views.Value <- Mod.force views
                    projs.Value <- Mod.force projs
                frozen <- not frozen
            )

        win.Keyboard.KeyDown(Keys.Space).Values.Add toggleFreeze



        let tree = GeometryTree(Box3d(-10.0 * V3d.III, 10.0 * V3d.III))


        let viewProj = Mod.map2 (Array.map2 (*)) views projs


        let visible =
            viewProj |> Mod.map (fun (vps : Trafo3d[]) (node : GeometryTree) ->
                vps |> Array.exists (ViewProjection.intersects node.Bounds)
            )

        let descend =
            viewProj |> Mod.map (fun (vps : Trafo3d[]) (node : GeometryTree) ->
                let projectedLength (b : Box3d) (t : Trafo3d) =
                    let ssb = b.ComputeCorners() |> Array.map (t.Forward.TransformPosProj) |> Box3d
                    max ssb.Size.X ssb.Size.Y

                let len = vps |> Array.map (projectedLength node.Bounds) |> Array.max
                len > 1.0
            )
            


        let active = CSet.empty

        let root = Mod.constant (Some tree)

        let mutable pending = ref HDeltaSet.empty

        let flush () =
            transact (fun () ->
                let pending = !Interlocked.Exchange(&pending, ref HDeltaSet.empty)
                for d in pending do
                    match d with
                        | Add(_,v) -> active.Add v |> ignore
                        | Rem(_,v) -> active.Remove v |> ignore

            )

        let activate (g : Geometry) =
            pending := HDeltaSet.add (Add g) !pending
            //transact (fun () -> active.Add g |> ignore)
            
        let deactivate (g : Geometry) =
            pending := HDeltaSet.add (Rem g) !pending
            //transact (fun () -> active.Remove g |> ignore)

        let thread =
            root
            |> Loader.create {
                prepare     = id
                delete      = ignore
                visible     = visible
                descend     = descend
            }
            |> Loader.start activate deactivate id


        let runtime = win.Runtime |> unbox<Runtime>
        let device = runtime.Device

        let effect =
            FShade.Effect.compose [
                toEffect DefaultSurfaces.trafo
                toEffect (DefaultSurfaces.constantColor C4f.Red)
                toEffect DefaultSurfaces.simpleLighting
            ]


        let surface = Aardvark.Base.Surface.FShadeSimple effect

        let state =
            {
                depthTest           = Mod.constant DepthTestMode.LessOrEqual
                cullMode            = Mod.constant CullMode.None
                blendMode           = Mod.constant BlendMode.None
                fillMode            = Mod.constant FillMode.Fill
                stencilMode         = Mod.constant StencilMode.Disabled
                multisample         = Mod.constant true
                writeBuffers        = None
                globalUniforms      = 
                    UniformProvider.ofList [
                        "ViewTrafo", view :> IMod
                        "ProjTrafo", proj :> IMod
                        "LightLocation", view |> Mod.map (fun v -> v.Backward.C3.XYZ) :> IMod
                        "CameraLocation", view |> Mod.map (fun v -> v.Backward.C3.XYZ) :> IMod
                    ]

                geometryMode        = IndexedGeometryMode.TriangleList
                vertexInputTypes    = Map.ofList [ DefaultSemantic.Positions, typeof<V3f>; DefaultSemantic.Normals, typeof<V3f> ]
                perGeometryUniforms = Map.ofList [ "ModelTrafo", typeof<Trafo3d> ]
            }

        let task = new RenderTask.CommandTask(device, unbox win.FramebufferSignature, RuntimeCommand.Geometries(surface, state, active))


        win.RenderTask <- task




        win.Run()
