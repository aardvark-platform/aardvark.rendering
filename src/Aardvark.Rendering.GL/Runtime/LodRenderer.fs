namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Collections.Generic
open System.Threading.Tasks
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open FShade
open FShade.GLSL
open OpenTK.Graphics.OpenGL4
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Base.Management
open Aardvark.Base.Runtime
open Aardvark.Rendering.GL

#nowarn "9"




module LodTreeHelpers =

    module HMap =
        let keys (m : hmap<'a, 'b>) =
            HSet.ofSeq (Seq.map fst (HMap.toSeq m))

        let applySetDelta (set : hdeltaset<'a>) (value : 'b) (m : hmap<'a, 'b>) =
            let delta = 
                set |> HDeltaSet.toHMap |> HMap.map (fun e r ->
                    if r > 0 then Set value
                    else Remove
                )
            HMap.applyDelta m delta |> fst



    [<StructuredFormatDisplay("{AsString}")>]
    type Operation<'a> =
        {
            alloc   : int
            active  : int
            value   : Option<'a>
        }


        member x.Inverse =
            {
                alloc = -x.alloc
                active = -x.active
                value = x.value
            }
        
        member x.ToString(name : string) =
            if x.alloc > 0 then 
                if x.active > 0 then sprintf "alloc(%s, +1)" name
                elif x.active < 0 then sprintf "alloc(%s, -1)" name
                else sprintf "alloc(%s)" name
            elif x.alloc < 0 then sprintf "free(%s)" name
            elif x.active > 0 then sprintf "activate(%s)" name
            elif x.active < 0 then sprintf "deactivate(%s)" name
            else sprintf "nop(%s)" name

        override x.ToString() =
            if x.alloc > 0 then 
                if x.active > 0 then sprintf "alloc(%A, +1)" x.value.Value
                elif x.active < 0 then sprintf "alloc(%A, -1)" x.value.Value
                else sprintf "alloc(%A)" x.value.Value
            elif x.alloc < 0 then "free"
            elif x.active > 0 then "activate"
            elif x.active < 0 then "deactivate"
            else "nop"

        member private x.AsString = x.ToString()

        static member Zero : Operation<'a> = { alloc = 0; active = 0; value = None }

        static member Nop : Operation<'a> = { alloc = 0; active = 0; value = None }
        static member Alloc(value, active) : Operation<'a> = { alloc = 1; active = (if active then 1 else 0); value = Some value }
        static member Free : Operation<'a> = { alloc = -1; active = -1; value = None }
        static member Activate : Operation<'a> = { alloc = 0; active = 1; value = None }
        static member Deactivate : Operation<'a> = { alloc = 0; active = -1; value = None }

        static member (+) (l : Operation<'a>, r : Operation<'a>) =
            {
                alloc = l.alloc + r.alloc
                active = l.active + r.active
                value = match r.value with | Some v -> Some v | None -> l.value
            }

    let Nop<'a> = Operation<'a>.Nop
    let Alloc(v,a) = Operation.Alloc(v,a)
    let Free<'a> = Operation<'a>.Free
    let Activate<'a> = Operation<'a>.Activate
    let Deactivate<'a> = Operation<'a>.Deactivate

    let (|Nop|Alloc|Free|Activate|Deactivate|) (o : Operation<'a>) =
        if o.alloc > 0 then 
            if o.alloc > 1 then Log.warn "alloc(%d, %d)" o.alloc o.active
            Alloc(o.value.Value, o.active)
        elif o.alloc < 0 then 
            if -o.alloc > 1 then Log.warn "free(%d, %d)" -o.alloc o.active
            Free(o.active)
        elif o.active > 0 then 
            if o.active > 1 then Log.warn "activate %d" o.active
            Activate
        elif o.active < 0 then 
            if -o.active > 1 then Log.warn "deactivate %d" -o.active
            Deactivate
        else 
            Nop
        
    [<StructuredFormatDisplay("{AsString}")>]
    type AtomicOperation<'a, 'b> =
        {
            keys : hset<'a>
            ops : hmap<'a, Operation<'b>>
        }
            
        override x.ToString() =
            x.ops 
            |> Seq.map (fun (a, op) -> op.ToString(sprintf "%A" a)) 
            |> String.concat "; " |> sprintf "atomic [%s]"

        member private x.AsString = x.ToString()

        member x.Inverse =
            {
                keys = x.keys
                ops = x.ops |> HMap.map (fun _ o -> o.Inverse)
            }

        static member Empty : AtomicOperation<'a, 'b> = { keys = HSet.empty; ops = HMap.empty }
        static member Zero : AtomicOperation<'a, 'b> = { keys = HSet.empty; ops = HMap.empty }

        static member (+) (l : AtomicOperation<'a, 'b>, r : AtomicOperation<'a, 'b>) =
            let merge (key : 'a) (l : Option<Operation<'b>>) (r : Option<Operation<'b>>) =
                match l with
                | None -> r
                | Some l ->
                    match r with
                    | None -> Some l
                    | Some r -> 
                        match l + r with
                        | Nop -> None
                        | op -> Some op

            let ops = HMap.choose2 merge l.ops r.ops 
            let keys = HMap.keys ops
            { ops = ops; keys = keys }
            
        member x.IsEmpty = HMap.isEmpty x.ops
            
    module AtomicOperation =

        let empty<'a, 'b> = AtomicOperation<'a, 'b>.Empty
        
        let ofHMap (ops : hmap<'a, Operation<'b>>) =
            let keys = HMap.keys ops
            { ops = ops; keys = keys }

        let ofSeq (s : seq<'a * Operation<'b>>) =
            let ops = HMap.ofSeq s
            let keys = HMap.keys ops
            { ops = ops; keys = keys }
                
        let ofList (l : list<'a * Operation<'b>>) = ofSeq l
        let ofArray (a : array<'a * Operation<'b>>) = ofSeq a

    type AtomicQueue<'a, 'b> private(classId : uint64, classes : hmap<'a, uint64>, values : MapExt<uint64, AtomicOperation<'a, 'b>>) =
        let classId = if HMap.isEmpty classes then 0UL else classId

        static let empty = AtomicQueue<'a, 'b>(0UL, HMap.empty, MapExt.empty)

        static member Empty = empty

        member x.Enqueue(op : AtomicOperation<'a, 'b>) =
            if not op.IsEmpty then
                let clazzes = op.keys |> HSet.choose (fun k -> HMap.tryFind k classes)

                if clazzes.Count = 0 then
                    let id = classId
                    let classId = id + 1UL
                    let classes = op.keys |> Seq.fold (fun c k -> HMap.add k id c) classes
                    let values = MapExt.add id op values
                    AtomicQueue(classId, classes, values)
                        
                else
                    let mutable values = values
                    let mutable classes = classes
                    let mutable result = AtomicOperation.empty
                    for c in clazzes do
                        match MapExt.tryRemove c values with
                        | Some (o, rest) ->
                            values <- rest
                            classes <- op.keys |> HSet.fold (fun cs c -> HMap.remove c cs) classes
                            // may not overlap here
                            result <- { ops = HMap.union result.ops o.ops; keys = HSet.union result.keys o.keys } //result + o

                        | None ->
                            ()

                    let result = result + op
                    if result.IsEmpty then
                        AtomicQueue(classId, classes, values)
                    else
                        let id = classId
                        let classId = id + 1UL

                        let classes = result.keys |> HSet.fold (fun cs c -> HMap.add c id cs) classes
                        let values = MapExt.add id result values
                        AtomicQueue(classId, classes, values)
                            
            else
                x
            
        member x.TryDequeue() =
            match MapExt.tryMin values with
            | None ->
                None
            | Some clazz ->
                let v = values.[clazz]
                let values = MapExt.remove clazz values
                let classes = v.keys |> HSet.fold (fun cs c -> HMap.remove c cs) classes
                let newQueue = AtomicQueue(classId, classes, values)
                Some (v, newQueue)

        member x.Dequeue() =
            match x.TryDequeue() with
            | None -> failwith "empty AtomicQueue"
            | Some t -> t

        member x.IsEmpty = MapExt.isEmpty values

        member x.Count = values.Count

        member x.UnionWith(other : AtomicQueue<'a, 'b>) =
            if x.Count < other.Count then
                other.UnionWith x
            else
                other |> Seq.fold (fun (s : AtomicQueue<_,_>) e -> s.Enqueue e) x

        static member (+) (s : AtomicQueue<'a, 'b>, a : AtomicOperation<'a, 'b>) = s.Enqueue a

        interface System.Collections.IEnumerable with
            member x.GetEnumerator() = new AtomicQueueEnumerator<_,_>((values :> seq<_>).GetEnumerator()) :> _
                
        interface IEnumerable<AtomicOperation<'a, 'b>> with
            member x.GetEnumerator() = new AtomicQueueEnumerator<_,_>((values :> seq<_>).GetEnumerator()) :> _

    and private AtomicQueueEnumerator<'a, 'b>(e : IEnumerator<KeyValuePair<uint64, AtomicOperation<'a, 'b>>>) =
        interface System.Collections.IEnumerator with
            member x.MoveNext() = e.MoveNext()
            member x.Current = e.Current.Value :> obj
            member x.Reset() = e.Reset()

        interface IEnumerator<AtomicOperation<'a, 'b>> with
            member x.Dispose() = e.Dispose()
            member x.Current = e.Current.Value

    module AtomicQueue =

        [<GeneralizableValue>]
        let empty<'a, 'b> = AtomicQueue<'a, 'b>.Empty

        let inline isEmpty (queue : AtomicQueue<'a, 'b>) = queue.IsEmpty
        let inline count (queue : AtomicQueue<'a, 'b>) = queue.Count
        let inline enqueue (v : AtomicOperation<'a, 'b>) (queue : AtomicQueue<'a, 'b>) = queue.Enqueue v
        let inline tryDequeue (queue : AtomicQueue<'a, 'b>) = queue.TryDequeue()
        let inline dequeue (queue : AtomicQueue<'a, 'b>) = queue.Dequeue()
        let inline combine (l : AtomicQueue<'a, 'b>) (r : AtomicQueue<'a, 'b>) = l.UnionWith r
            
        let enqueueMany (v : #seq<AtomicOperation<'a, 'b>>) (queue : AtomicQueue<'a, 'b>) = v |> Seq.fold (fun s e -> enqueue e s) queue
        let ofSeq (s : seq<AtomicOperation<'a, 'b>>) = s |> Seq.fold (fun q e -> enqueue e q) empty
        let ofList (l : list<AtomicOperation<'a, 'b>>) = l |> List.fold (fun q e -> enqueue e q) empty
        let ofArray (a : array<AtomicOperation<'a, 'b>>) = a |> Array.fold (fun q e -> enqueue e q) empty
                
        let toSeq (queue : AtomicQueue<'a, 'b>) = queue :> seq<_>
        let toList (queue : AtomicQueue<'a, 'b>) = queue |> Seq.toList
        let toArray (queue : AtomicQueue<'a, 'b>) = queue |> Seq.toArray
        
        let toOperation (queue : AtomicQueue<'a, 'b>) =
            queue |> Seq.sum


    type MaterializedTree =
        {
            rootId      : int
            original    : ILodTreeNode
            children    : list<MaterializedTree>
        }

    type internal TaskTreeState =
        {
            trigger         : MVar<unit>
            runningTasks    : ref<int>
            ready           : ref<int>
            totalNodes      : ref<int>
            version         : ref<int>
            splits          : ref<int>
            collapses       : ref<int>
            dataSize        : ref<int64>
        }
        

        member x.AddNode() = 
            lock x (fun () -> 
                x.totalNodes := !x.totalNodes + 1
            )

        member x.RemoveNode() = 
            lock x (fun () -> 
                x.totalNodes := !x.totalNodes - 1
            )

        member x.AddRunning() = 
            lock x (fun () -> 
                x.runningTasks := !x.runningTasks + 1
            )

        member x.RemoveRunning() = 
            lock x (fun () -> 
                x.runningTasks := !x.runningTasks - 1
            )
        

        member x.AddReady() = 
            lock x (fun () -> 
                x.ready := !x.ready + 1
                x.version := !x.version + 1
                Monitor.PulseAll x
            )

        member x.TakeReady(minCount : int) =
            lock x (fun () -> 
                while !x.ready < minCount do
                    Monitor.Wait x |> ignore

                let t = !x.ready
                x.ready := 0
                t
            )

    type TreeNode<'a> =
        {
            original : ILodTreeNode
            value : 'a
            children : list<TreeNode<'a>>
        }


    module SimplePickTree =
        
        let rec ofTreeNode (trafo : IMod<Trafo3d>) (v : TreeNode<GeometryPoolInstance>) =
            let positions = v.value.geometry.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
            let bounds = v.original.WorldCellBoundingBox
            
            SimplePickTree (
                v.original,
                bounds,
                positions,
                trafo,
                v.original.Root.DataTrafo,
                v.value.geometry.IndexedAttributes |> SymDict.toSeq |> MapExt.ofSeq,
                v.value.uniforms,
                lazy (v.children |> List.map (ofTreeNode trafo))
            )

    module TreeHelpers =

        let private cmp = Func<struct (float * _ * _), struct (float * _ * _), int>(fun (struct(a,_,_)) (struct(b,_,_)) -> compare a b)

        let viewTime = System.Diagnostics.Stopwatch()
        let splitTime = System.Diagnostics.Stopwatch()
        let splitQualityTime = System.Diagnostics.Stopwatch()

        let inline stop (sw : System.Diagnostics.Stopwatch) (f : unit -> 'a) =
            sw.Start()
            try f()
            finally sw.Stop()

        let inline private enqueue (t : ILodTreeNode) (s : float) (view : ILodTreeNode -> Trafo3d) (proj : Trafo3d) (queue : List<struct (float * int64 * ILodTreeNode)>) =
            let view = stop viewTime (fun () -> view t.Root)
            if stop splitTime (fun () -> t.ShouldSplit(s,1.0, view, proj)) then
                let q = stop splitQualityTime (fun () -> t.SplitQuality(s,view, proj))
                let childSize = t.Children |> Seq.sumBy (fun c -> int64 c.DataSize)
                queue.HeapEnqueue(cmp, struct (q, childSize, t))

        let getMaxQuality (maxSize : int64) splitfactor (ts : seq<ILodTreeNode>) (view : ILodTreeNode -> Trafo3d) (proj : Trafo3d) =

            let queue = List<struct (float * int64 * ILodTreeNode)>(65536)
            let mutable size = 0L
            let mutable quality = 0.0
            let mutable cnt = 0

            let dead = HashSet<ILodTreeNode>()

            for t in ts do 
                size <- size + int64 t.DataSize
                dead.Add t |> ignore
                enqueue t splitfactor view proj queue

            let inline s (struct (a,b,c)) = b

            let mutable iters = 0
            let sw = System.Diagnostics.Stopwatch.StartNew()

            viewTime.Reset()
            splitTime.Reset()
            splitQualityTime.Reset()


            let enqueueWatch = System.Diagnostics.Stopwatch()

            while queue.Count > 0 && size + s queue.[0] <= maxSize do
                let struct (q,s,e) = queue.HeapDequeue(cmp)
                dead.Remove e |> ignore
                iters <- iters + 1
                quality <- q
                size <- size + s
                for c in e.Children do
                    enqueueWatch.Start()
                    dead.Add c |> ignore
                    enqueue c splitfactor view proj queue
                    enqueueWatch.Stop()

            sw.Stop()
            if sw.Elapsed.TotalMilliseconds > 400.0 then
                Log.warn "traverse:    %A (%d)" sw.MicroTime iters
                Log.warn "enqueue:     %A" enqueueWatch.MicroTime
                Log.warn "view:        %A" viewTime.MicroTime
                Log.warn "split:       %A" splitTime.MicroTime
                Log.warn "quality:     %A" splitQualityTime.MicroTime
                Log.warn "per element: %A" (sw.MicroTime / iters)
            
            let sw = System.Diagnostics.Stopwatch.StartNew()
            for d in dead do d.Release()
            sw.Stop()
            if sw.Elapsed.TotalMilliseconds > 400.0 then
                Log.warn "kill:        %A (%d)" sw.MicroTime dead.Count
                Log.warn "per element: %A" (sw.MicroTime / dead.Count)


            if queue.Count = 0 then
                1.0, size
            else
                let struct (qn,_,_) = queue.HeapDequeue(cmp)
                0.5 * (qn + quality), size
   
    type TaskTreeNode<'a> internal(state : TaskTreeState, mapping : CancellationToken -> ILodTreeNode -> Task<'a>, rootId : int, original : ILodTreeNode) =
        static let neverTask = TaskCompletionSource<'a>().Task
        static let cmp = Func<float * _, float * _, int>(fun (a,_) (b,_) -> compare a b)

        let mutable cancel = Some (new CancellationTokenSource())
        let mutable task =
            state.AddNode()
            state.AddRunning()
            let c = cancel.Value
            let s = c.Token.Register(fun _ -> state.RemoveRunning())
            (mapping c.Token original).ContinueWith(fun (t : Task<'a>) -> 
                c.Cancel()
                s.Dispose()
                c.Dispose()
                cancel <- None
                t.Result
            )

        let mutable children : list<TaskTreeNode<'a>> = []
        do Interlocked.Add(&state.dataSize.contents, int64 original.DataSize) |> ignore

        member x.Task = task

        member x.Destroy() : unit =
            lock x (fun () ->
                Interlocked.Add(&state.dataSize.contents, -int64 original.DataSize) |> ignore
                state.RemoveNode()
                original.Release()
                try cancel |> Option.iter (fun c -> c.Cancel()); cancel <- None
                with _ -> ()
                children |> List.iter (fun c -> c.Destroy())
                children <- []
                cancel <- None
                task <- neverTask
            )
            



        member x.BuildQueue(node : ILodTreeNode, depth : int, quality : float, splitfactor : float, collapseIfNotSplit : bool, view : Trafo3d, proj : Trafo3d, queue : List<float * TaskTreeOp<'a>>) =
            if node <> original then failwith "[Tree] inconsistent path"

            match children with
            | [] ->
                if node.ShouldSplit(splitfactor, quality, view, proj) then
                    let qs = node.SplitQuality(splitfactor, view, proj)
                    queue.HeapEnqueue(cmp, (qs, TSplit x))
                elif node.Level = 0 then
                    Log.warn "happy with root %A %A" view.Backward.C3.XYZ (node.WorldBoundingBox.Transformed(node.DataTrafo.Inverse))
            | o ->
                let collapse =
                    if collapseIfNotSplit then not (node.ShouldSplit(splitfactor, quality, view, proj))
                    else node.ShouldCollapse(splitfactor, quality, view, proj)

                if collapse then
                    let qc = 
                        if collapseIfNotSplit then node.SplitQuality(splitfactor, view, proj)
                        else node.CollapseQuality(splitfactor, view, proj)
                    queue.HeapEnqueue(cmp, (qc - quality, TCollapse x))

                else
                    let o = List.toArray o
                    let n = Seq.toArray node.Children
                    if o.Length <> n.Length then
                        Log.warn "the very bad happened %d vs %d" o.Length n.Length
                    else
                        for (o, n) in Array.zip o n do
                            o.BuildQueue(n, depth, quality, splitfactor, collapseIfNotSplit, view, proj, queue)

        member x.StartSplit(trigger : MVar<unit>) =
            let n = original.Children |> Seq.toList
            let childTasks = System.Collections.Generic.List<Task<_>>()
            children <- n |> List.map (fun n ->
                
                let node = TaskTreeNode(state, mapping, rootId, n)
                childTasks.Add(node.Task)
                node
            )

            let childrenReady (t : Task<_>) =
                if t.IsCompleted && not t.IsFaulted && not t.IsCanceled then
                    state.AddReady()
                    MVar.put trigger ()

            Task.WhenAll(childTasks).ContinueWith(childrenReady, TaskContinuationOptions.OnlyOnRanToCompletion) |> ignore
            
        member x.TotalSize =
            int64 original.DataSize +
            List.sumBy (fun (c : TaskTreeNode<_>) -> c.TotalSize) children

        member x.Collapse(trigger : MVar<unit>) =
            children |> List.iter (fun c -> c.Destroy())
            children <- []
            MVar.put trigger ()
            
        member x.Original = original
        member x.Children = children

        member x.TryValue =
            lock x (fun () ->
                if task.IsCompleted && not task.IsFaulted && not task.IsCanceled then
                    Some task.Result
                else
                    None
            )


        member x.HasValue = lock x (fun () -> task.IsCompleted && not task.IsFaulted && not task.IsCanceled)
        //member x.Value = task.Result

        member x.TryGetValue() =
            if task.IsCompleted && not task.IsFaulted && not task.IsCanceled then
                Some task.Result
            else
                None

        member x.HasChildren = not (List.isEmpty children)
        member x.AllChildrenHaveValues = children |> List.forall (fun c -> c.HasValue)

    and TaskTreeOp<'a> =
        | TAdd of (unit -> unit)
        | TRem of (unit -> unit)
        | TSplit of TaskTreeNode<'a>
        | TCollapse of TaskTreeNode<'a>

        member inline x.Important =
            match x with
            | TAdd _ | TRem _ | TCollapse _ -> true
            | _ -> false

    type TaskTree<'a>(mapping : CancellationToken -> ILodTreeNode -> Task<'a>, rootId : int) =
        static let cmp = Func<float * _, float * _, int>(fun (a,_) (b,_) -> compare a b)
        static let cmpNode = Func<float * ILodTreeNode, float * ILodTreeNode, int>(fun (a,_) (b,_) -> compare a b)

        let mutable root : Option<TaskTreeNode<'a>> = None

        
        //member x.RunningTasks = state.runningTasks

        //member x.Version = state.version
        member x.RootId = rootId
        member x.Root = root

        member internal x.BuildQueue(state : TaskTreeState, collapseIfNotSplit : bool, t : ILodTreeNode, quality : float, splitfactor : float, view : ILodTreeNode -> Trafo3d, proj : Trafo3d, queue : List<float * TaskTreeOp<'a>>) =
            match root with
            //| None -> 
            //    Log.warn "empty tree"
            //    ()

            //| Some r, None -> 
            //    let destroy() =
            //        r.Destroy()
            //        root <- None
            //        MVar.put state.trigger ()
            //    queue.HeapEnqueue(cmp, (-1.0, TaskTreeOp.TRem destroy))
            //    //r.Destroy(); root <- None

            | None -> 
                let create () =
                    let n = TaskTreeNode(state, mapping, rootId, t)
                    root <- Some n
                    n.Task.ContinueWith (fun (t : Task<_>) -> MVar.put state.trigger ()) |> ignore

                queue.HeapEnqueue(cmp, (-1.0, TaskTreeOp.TAdd create))
                
            | Some r -> 
                r.BuildQueue(t, 0, quality, splitfactor, collapseIfNotSplit, view r.Original, proj, queue)



        static member internal ProcessQueue(state : TaskTreeState, queue : List<float * TaskTreeOp<'a>>, quality : float, splitfactor : float, view : ILodTreeNode -> Trafo3d, proj : Trafo3d, maxOps : int) =
            let mutable lastQ = 0.0
            let mutable cnt = 0
            let mutable finIfEmpty = true


            while queue.Count > 0 && (!state.runningTasks < maxOps || (queue.Count > 0 && (snd queue.[0]).Important)) do
                let q, op = queue.HeapDequeue(cmp)
                match op with
                | TAdd create ->
                    create()
                    finIfEmpty <- false

                | TRem destroy ->
                    destroy()

                | TSplit n -> 
                    n.StartSplit(state.trigger)
                    for c in n.Children do
                        let r = c.Original.Root
                        let view = view r
                        if c.Original.ShouldSplit(splitfactor, quality, view, proj) then
                            let qs = c.Original.SplitQuality(splitfactor, view, proj)
                            queue.HeapEnqueue(cmp, (qs, TSplit c))
                    Interlocked.Increment(&state.splits.contents) |> ignore
                    lastQ <- q

                | TCollapse n -> 
                    n.Collapse(state.trigger)
                    Interlocked.Increment(&state.collapses.contents) |> ignore
                    lastQ <- q + quality

                cnt <- cnt + 1

            if queue.Count = 0 then 
                quality, finIfEmpty
            else 
                let qnext, _ = queue.HeapDequeue(cmp)
                if cnt = 0 then qnext, false
                else 0.5 * (qnext + lastQ), false
                    

    type TaskTreeReader<'a>(tree : TaskTree<'a>) =
        let mutable state : Option<TreeNode<'a>> = None
        let mutable destroyed = false
        let mutable mutex = 0
        let mutable currentCaller = ""

        let check (caller : string) (f : unit -> 'x) =
            let c = Interlocked.Increment(&mutex)
            try
                if c > 1 then Log.warn "race %s vs %s" currentCaller caller
                else currentCaller <- caller
                f()
            finally
                currentCaller <- ""
                Interlocked.Decrement(&mutex) |> ignore

        let rec allNodes (t : TreeNode<'a>) =
            Seq.append (Seq.singleton t.original) (t.children |> Seq.collect allNodes)
            
        let rec kill (t : TreeNode<'a>) =
            match t.children with
            | [] -> 
                AtomicOperation.ofList [t.original, { alloc = -1; active = -1; value = None }]
            | cs ->
                let mutable op = AtomicOperation.ofList [t.original, { alloc = -1; active = 0; value = None }]
                for c in cs do
                    op <- op + kill c
                op

        let rec snap (n : TaskTreeNode<'a>) =
            match n.TryValue with
            | Some v -> 
                let nc = n.Children
                let allReady = nc |> List.map snap
                if List.forall Option.isSome allReady then
                    let cs = allReady |> List.map Option.get
                    Some {
                        original = n.Original
                        value = v
                        children = cs
                    }
                else
                    Some {
                        original = n.Original
                        value = v
                        children = []
                    }

            | None ->
                None

        let rec traverse2 (q : ref<AtomicQueue<ILodTreeNode, 'a>>) (o : Option<TreeNode<'a>>) (n : Option<TreeNode<'a>>) =
            match o, n with
            | None, None ->
                ()
            | Some o, None ->
                let op = kill o
                lock q (fun () -> q := AtomicQueue.enqueue op !q)
            | None, Some n ->
                let qc = ref AtomicQueue.empty
                n.children |> List.iter (fun c -> traverse2 qc None (Some c))
                let op = 
                    AtomicQueue.toOperation !qc +
                    AtomicOperation.ofList [ n.original, Operation.Alloc(n.value, List.isEmpty n.children) ]
                         
                lock q (fun () -> q := AtomicQueue.enqueue op !q) 
            | Some o, Some n ->
                assert (Unchecked.equals o.original n.original)
                match o.children, n.children with
                | [], [] -> 
                    ()
                | oc, [] ->
                    let qc = ref AtomicQueue.empty
                    oc |> List.iter (fun c -> traverse2 qc (Some c) None)
                    let op = 
                        AtomicQueue.toOperation !qc +
                        AtomicOperation.ofList [ n.original, Operation.Activate ]
                        
                    lock q (fun () -> q := AtomicQueue.enqueue op !q) 

                | [], nc ->
                    let qc = ref AtomicQueue.empty
                    nc |> List.iter (fun c -> traverse2 qc None (Some c))
                    let op = 
                        AtomicQueue.toOperation !qc +
                        AtomicOperation.ofList [ n.original, Operation.Deactivate ]
                        
                    lock q (fun () -> q := AtomicQueue.enqueue op !q) 

                | oc, nc ->
                    List.iter2 (fun o n -> traverse2 q (Some o) (Some n)) oc nc



        let traverse (q : ref<AtomicQueue<ILodTreeNode, 'a>>) (o : Option<TreeNode<'a>>) (n : Option<TaskTreeNode<'a>>) =
            let snap = n |> Option.bind snap

            match snap, n with
            | None, Some t -> Log.warn "empty snap: %A" t.Original
            | _ -> ()

            traverse2 q o snap
            snap
            //match o, n with
            //    | None, None -> 
            //        o

            //    | Some o, None ->
            //        let op = kill o
            //        lock q (fun () -> q := AtomicQueue.enqueue op !q)
            //        None

            //    | None, Some n ->
            //        match n.TryValue with
            //        | Some v ->
            //            let mutable qc = ref AtomicQueue.empty
            //            let mutable worked = true
            //            let children = System.Collections.Generic.List<_>()
            //            let nc = n.Children
            //            use e = (nc :> seq<_>).GetEnumerator()
            //            while e.MoveNext() && worked do
            //                let cn = traverse qc None (Some e.Current)
            //                match cn with
            //                | Some cn -> 
            //                    children.Add cn
            //                | None ->
            //                    worked <- false


            //            if worked && not (List.isEmpty nc) then
            //                let ops = 
            //                    AtomicQueue.toOperation !qc +
            //                    AtomicOperation.ofList [ n.Original, Operation.Alloc(v, false) ]
                            
            //                let value = 
            //                    Some {
            //                        original = n.Original
            //                        value = v
            //                        children = Seq.toList children
            //                    }

            //                lock q (fun () -> q := AtomicQueue.enqueue ops !q)

            //                value

            //            else
            //                let value = 
            //                    Some {
            //                        original = n.Original
            //                        value = v
            //                        children = []
            //                    }
            //                let op = AtomicOperation.ofList [ n.Original, Operation.Alloc(v, true) ]
            //                lock q (fun () -> q := AtomicQueue.enqueue op !q)
            //                value
            //        | None ->
            //            None


            //    | Some o, Some n -> 
            //        let nc = n.Children
            //        let anyNonReady = nc |> List.exists (fun n -> not n.HasValue)
            //        match o.children, nc with
            //        | [], [] ->
            //            Some o
                        
            //        | [], _n when anyNonReady ->
            //            Some o

            //        | cs, _n when anyNonReady ->
            //            Log.warn "fischig"

            //            let op = cs |> List.sumBy kill 
            //            lock q (fun () -> q := AtomicQueue.enqueue op !q)
            //            Some { o with children = [] }
                        

            //        | [], ns ->
            //            let mutable worked = true
            //            let childQueue = ref AtomicQueue.empty
            //            let children =
            //                ns |> List.map (fun c ->
            //                    match traverse childQueue None (Some c) with
            //                    | Some c ->
            //                        c
            //                    | None ->
            //                        worked <- false
            //                        Unchecked.defaultof<_>
            //                )
            //            if worked then
            //                let op = 
            //                    AtomicQueue.toOperation !childQueue + 
            //                    AtomicOperation.ofList [ o.original, Operation.Deactivate ]
                        
            //                let value =
            //                    Some {
            //                        original = o.original
            //                        value = o.value
            //                        children = children
            //                    }

            //                lock q (fun () -> q := AtomicQueue.enqueue op !q)

            //                value
            //            else
            //                Log.warn "the impossible happened (no worries)"
            //                Some o

            //        | os, [] ->
            //            let op = 
            //                AtomicOperation.ofList [ o.original, Operation.Activate ] +
            //                List.sumBy kill os
                            
            //            let value =
            //                Some {
            //                    original = o.original
            //                    value = o.value
            //                    children = []
            //                }

            //            lock q (fun () -> q := AtomicQueue.enqueue op !q)
            //            value

                    
            //        | os, ns ->
            //            //assert (ns |> List.forall (fun n -> n.HasValue))
            //            let mutable worked = true
            //            let children = 
            //                List.zip os ns |> List.map (fun (o,n) ->
            //                    match traverse q (Some o) (Some n) with
            //                    | Some nn ->
            //                        nn
            //                    | None ->
            //                        worked <- false
            //                        Unchecked.defaultof<_>
            //                )
                            
            //            if worked then
            //                let value =
            //                    Some {
            //                        original = o.original
            //                        value = o.value
            //                        children = children
            //                    }
                            
            //                value
            //            else
            //                Log.warn "the impossible happened (no worries)"
            //                Some o

        member x.Tree = tree
        //member x.Root = state |> Option.map (fun r -> r.original)
        member x.State = state

        member x.Update(q : ref<AtomicQueue<ILodTreeNode, 'a>>) =   
            check "Update" (fun () ->
                if not destroyed then
                    let s = state
                    let newState = traverse q s tree.Root

                    //match s, newState with 
                    //| None, Some r ->
                    //    Log.line "boot %A" r.original
                    //| Some o, None ->
                    //    Log.line "unboot %A" o.original
                        
                    //| None, None ->
                    //    Log.line "!!!!!empty"
                        

                    //| _ ->
                    //    ()


                    assert (state == s)
                    state <- newState
                else 
                    Log.warn "[Lod] update of dead tree"
            )
            
        member x.Destroy(q : ref<AtomicQueue<ILodTreeNode, 'a>>) =
            check "Destroy" (fun () ->
                destroyed <- true
                match tree.Root with
                | Some r -> 
                    //Log.line "destroy %A" r.Original
                    r.Destroy()
                | None -> ()
                match state with
                | Some s -> 
                    let res = kill s
                    lock q (fun () -> q := AtomicQueue.enqueue res !q)
                    state <- None
                | None ->
                    ()
            )


    //[<RequireQualifiedAccess>]
    //type NodeOperation =
    //    | Split
    //    | Collapse of children : list<ILodTreeNode>
    //    | Add
    //    | Remove of children : list<ILodTreeNode>

    //type Delta =
    //    {
    //        deltas          : hmap<ILodTreeNode, int * NodeOperation>
    //        splitCount      : int
    //        collapseCount   : int
    //        allocSize       : int64
    //        freeSize        : int64
    //    }

    //    static member Empty =
    //        {
    //            deltas = HMap.empty; splitCount = 0; collapseCount = 0; allocSize = 0L; freeSize = 0L
    //        }

    //module MaterializedTree =
        
    //    let inline original (node : MaterializedTree) = node.original
    //    let inline children (node : MaterializedTree) = node.children

    //    let ofNode (id : int) (node : ILodTreeNode) =
    //        {
    //            rootId = id
    //            original = node
    //            children = []
    //        }

        
    //    let rec allNodes (node : MaterializedTree) =
    //        Seq.append 
    //            (Seq.singleton node)
    //            (node.children |> Seq.collect allNodes)

    //    let allChildren (node : MaterializedTree) =
    //        node.children |> Seq.collect allNodes

    //    let qualityHistogram splitfactor (histo : SortedDictionary<float, ref<int>>) (predictView : ILodTreeNode -> Trafo3d) (view : Trafo3d) (proj : Trafo3d) (t : MaterializedTree) (state : MaterializedTree) =
    //        let rec run (t : MaterializedTree) (state : MaterializedTree) =
    //            let node = t.original
                
    //            if List.isEmpty t.children && node.ShouldSplit(splitfactor, 1.0, view, proj) then //&& node.ShouldSplit(predictView node, proj) then
    //                if List.isEmpty state.children then
    //                    let minQ = node.SplitQuality(splitfactor, view, proj)
    //                    match histo.TryGetValue minQ with
    //                    | (true, r) -> 
    //                        r := !r + 1
    //                    | _ -> 
    //                        let r = ref 1
    //                        histo.[minQ] <- r

    //            elif not (List.isEmpty t.children) && node.ShouldCollapse(splitfactor, 1.0, view, proj) then
    //                ()

    //            else
    //                match t.children, state.children with
    //                    | [], [] ->
    //                        ()

    //                    | [], _ -> ()
    //                    | _, [] -> ()

    //                    | l,r ->
    //                        List.iter2 run l r
            
    //        run t state


        


    //    let rec tryExpand (splitfactor : float)(quality : float)(predictView : ILodTreeNode -> Trafo3d) (view : Trafo3d) (proj : Trafo3d) (t : MaterializedTree) =
    //        let node = t.original

    //        let inline tryExpandMany (ls : list<MaterializedTree>) =
    //            let mutable changed = false
    //            let newCs = 
    //                ls |> List.map (fun c ->
    //                    match tryExpand splitfactor quality predictView view proj c with
    //                        | Some newC -> 
    //                            changed <- true
    //                            newC
    //                        | None ->
    //                            c
    //                )
    //            if changed then Some newCs
    //            else None
                
    //        if List.isEmpty t.children && node.ShouldSplit(splitfactor, quality, view, proj) then //&& node.ShouldSplit(predictView node, proj) then
    //            Some { t with children = node.Children |> Seq.toList |> List.map (ofNode t.rootId) }

    //        elif not (List.isEmpty t.children) && node.ShouldCollapse(splitfactor, quality, view, proj) then
    //            Some { t with children = [] }

    //        else
    //            match t.children with
    //                | [] ->
    //                    None

    //                | children ->
    //                    match tryExpandMany children with
    //                        | Some newChildren -> Some { t with children = newChildren }
    //                        | _ -> None

    //    let expand splitfactor (quality : float)(predictView : ILodTreeNode -> Trafo3d) (view : Trafo3d) (proj : Trafo3d) (t : MaterializedTree) =
    //        match tryExpand splitfactor quality predictView view proj t with
    //            | Some n -> n
    //            | None -> t

    //    let rec computeDelta (acc : Delta) (o : MaterializedTree) (n : MaterializedTree) =
    //        if System.Object.ReferenceEquals(o,n) then
    //            acc
    //        else

    //            let rec computeChildDeltas (acc : Delta) (os : list<MaterializedTree>) (ns : list<MaterializedTree>) =
    //                match os, ns with
    //                    | [], [] -> 
    //                        acc
    //                    | o :: os, n :: ns ->
    //                        let acc = computeDelta acc o n
    //                        computeChildDeltas acc os ns
    //                    | _ ->
    //                        failwith "inconsistent child count"
                            
    //            if o.original = n.original then
    //                match o.children, n.children with
    //                    | [], []    -> 
    //                        acc

    //                    | [], _     ->
    //                        { acc with
    //                            deltas = HMap.add n.original (n.rootId, NodeOperation.Split) acc.deltas
    //                            splitCount = 1 + acc.splitCount
    //                            allocSize = int64 n.original.DataSize + acc.allocSize
    //                        }
    //                    | oc, []    -> 
    //                        let children = allChildren o |> Seq.map original |> Seq.toList
    //                        { acc with
    //                            deltas = HMap.add n.original (n.rootId, NodeOperation.Collapse(children)) acc.deltas
    //                            collapseCount = 1 + acc.collapseCount
    //                            freeSize = children |> List.fold (fun s c -> s + int64 c.DataSize) acc.freeSize
    //                        }
    //                    | os, ns    -> 
    //                        computeChildDeltas acc os ns
    //            else
    //                failwith "inconsistent child values"



    type LimitedConcurrencyLevelTaskScheduler (priority : ThreadPriority, maxDegreeOfParallelism : int) as this =
        inherit TaskScheduler()

        let sem = new SemaphoreSlim(0)
        let queue = ConcurrentHashQueue<Task>()
        let shutdown = new CancellationTokenSource()
        //let mutable activeCount = 0


        let run() =
            let mutable item = null
            try
                while not shutdown.IsCancellationRequested do
                    sem.Wait(shutdown.Token)
                    if queue.TryDequeue(&item) then
                        this.TryExecuteTask(item) |> ignore
                        item <- null
            with :? OperationCanceledException ->
                ()

        //let mutable waitCallback = Unchecked.defaultof<WaitCallback>

        //let runItem (state : obj) =
        //    let task = unbox<Task> state
        //    this.TryExecuteTask task |> ignore
        //    if Interlocked.Decrement(&activeCount) < maxDegreeOfParallelism then
        //        match queue.TryDequeue() with
        //            | (true, item) -> 
        //                ThreadPool.UnsafeQueueUserWorkItem(waitCallback, task) |> ignore
        //            | _ -> 
        //                ()

        //do waitCallback <- WaitCallback(runItem)

        let workers =
            Array.init maxDegreeOfParallelism (fun i ->
                let t = Thread(ThreadStart(run))
                t.IsBackground <- true
                t.Priority <- priority
                t.Name <- sprintf "Worker%d" i
                t.Start()
                t
            )

        member x.TryExecuteTask(item) : bool = base.TryExecuteTask(item)

        override x.QueueTask(task : Task) = 
            //ThreadPool.UnsafeQueueUserWorkItem(WaitCallback(runItem), (x,task)) |> ignore
            if queue.Enqueue(task) then
                sem.Release() |> ignore

        override x.GetScheduledTasks() = 
            Seq.empty

        override x.TryExecuteTaskInline(task : Task, taskWasPreviouslyQueued : bool) =
            if not taskWasPreviouslyQueued then
                x.TryExecuteTask task |> ignore
                true
            else
                if queue.Remove task then
                    x.TryExecuteTask task |> ignore
                    true
                else
                    false

        override x.TryDequeue(task : Task) =
            if queue.Remove(task) then
                true
            else
                false

        override x.MaximumConcurrencyLevel = 
            maxDegreeOfParallelism

    type IPrediction<'a> =
        abstract member Predict : dt : MicroTime -> Option<'a>
        abstract member WithOffset : offset : MicroTime -> IPrediction<'a>

    type Prediction<'a>(span : MicroTime, interpolate2 : float -> 'a -> 'a -> 'a) =
        let sw = System.Diagnostics.Stopwatch.StartNew()
        let now () = sw.MicroTime

        let mutable history : MapExt<MicroTime, 'a> = MapExt.empty
        
        let prune (t : MicroTime) =
            let _,_,r = history |> MapExt.split (t - span)
            history <- r
            
        let interpolate (arr : array<MicroTime * 'a>) (t : MicroTime) =
            match arr.Length with
                | 0 ->
                    None

                | 1 -> 
                    arr.[0] |> snd |> Some

                | _ ->
                    let (t0, p0) = arr.[0]
                    let (t1, p1) = arr.[arr.Length - 1]
                    let p = (t - t0) / (t1 - t0)
                    interpolate2 p p0 p1 |> Some

        member x.WithOffset(offset : MicroTime) =
            { new IPrediction<'a> with 
                member __.WithOffset(o) = x.WithOffset(offset + o)
                member __.Predict(dt) = x.Predict(offset + dt)
            }

        member x.Add(cam : 'a) =
            lock x (fun () ->
                let t = now()
                history <- MapExt.add t cam history
                prune t
            )

        member x.Predict(dt : MicroTime) =
            lock x (fun () ->
                let t = now()
                prune t
                let future = t + dt
                let arr = MapExt.toArray history
                interpolate arr future
            )

        interface IPrediction<'a> with
            member x.Predict(dt) = x.Predict(dt)
            member x.WithOffset(o) = x.WithOffset o

    module Prediction =
        let rec map (mapping : 'a -> 'b) (p : IPrediction<'a>) =
            { new IPrediction<'b> with
                member x.Predict(dt) = p.Predict(dt) |> Option.map mapping
                member x.WithOffset(o) = p.WithOffset(o) |> map mapping
            }

        let euclidean (span : MicroTime) =
            Prediction<Euclidean3d>(
                span, 
                fun (t : float) (a : Euclidean3d) (b : Euclidean3d) ->
                    let delta = b * a.Inverse

                    let dRot = Rot3d.FromAngleAxis(delta.Rot.ToAngleAxis() * t)
                    let dTrans = delta.Rot.InvTransformDir(delta.Trans) * t
                    let dScaled = Euclidean3d(dRot, dRot.TransformDir dTrans)

                    dScaled * a
            )
            



    type RenderState =
        {
            iface : GLSLProgramInterface
            calls : DrawPool
            mutable allocs : int
            mutable uploadSize : Mem
            mutable nodeSize : int
            mutable count : int
        }

    let inline (%*) a b = Mod.map2 (*) a b



open LodTreeHelpers

[<AutoOpen>]
module private Assertions =
    
    let inline ensure action fmt = Printf.kprintf (fun str -> if not (action()) then Report.Warn("[Lod] {0}", str)) fmt
    let inline bad fmt = Printf.kprintf (fun str -> Report.Warn("[Lod] {0}", str)) fmt

type LodRenderingInfo =
    {
        quality         : IModRef<float>
        maxQuality      : IModRef<float>
        renderBounds    : IMod<bool>
    }

type UniqueTree(id : Guid, root : Option<UniqueTree>, parent : Option<ILodTreeNode>, inner : ILodTreeNode) =
    //let root = defaultArg root this

    member x.Id = id
    member x.Inner = inner

    override x.GetHashCode() =
        HashCode.Combine(id.GetHashCode(), inner.GetHashCode())
        
    override x.Equals o =
        match o with
        | :? UniqueTree as o -> id = o.Id && inner = o.Inner
        | _ -> false

    override x.ToString() = 
        sprintf "U(%A, %A)" id inner
        
    member x.Root = match root with | Some r -> r | None -> x 
    interface ILodTreeNode with
        member x.Level = inner.Level
        member x.Id = inner.Id
        member x.Name = inner.Name
        member x.Root = x.Root :> ILodTreeNode
        member x.Parent = parent
        member x.Children = inner.Children |> Seq.map (fun c -> UniqueTree(id, Some x.Root, Some (x :> ILodTreeNode), c) :> ILodTreeNode)

        member x.DataSource = inner.DataSource
        member x.DataSize = inner.DataSize
        member x.TotalDataSize = inner.TotalDataSize
        member x.GetData (ct : CancellationToken, inputs : MapExt<string, Type>) = inner.GetData(ct, inputs)

        member x.ShouldSplit(a,b,c,d) = inner.ShouldSplit(a,b,c,d)
        member x.ShouldCollapse(a,b,c,d)  = inner.ShouldCollapse(a,b,c,d)
        
        member x.SplitQuality(a,b,c) = inner.SplitQuality(a,b,c) 
        member x.CollapseQuality(a,b,c) = inner.CollapseQuality(a,b,c)

        member x.WorldBoundingBox = inner.WorldBoundingBox
        member x.WorldCellBoundingBox = inner.WorldCellBoundingBox
        member x.Cell = inner.Cell

        member x.DataTrafo = inner.DataTrafo

        member x.Acquire() = inner.Acquire()
        member x.Release() = inner.Release()

type LodRenderer(ctx : Context, manager : ResourceManager, state : PreparedPipelineState, config : LodRendererConfig, roots : aset<LodTreeInstance>)  =
    inherit PreparedCommand(ctx, config.pass)
    
    static let scheduler = new LimitedConcurrencyLevelTaskScheduler(ThreadPriority.BelowNormal, Environment.ProcessorCount)// max 2 (Environment.ProcessorCount - 3))
            
    static let startTask (ct : CancellationToken) (f : unit -> 'a) =
        Task.Factory.StartNew(Func<'a>(f), ct, TaskCreationOptions.None, scheduler)
                
    let signature = state.pProgramInterface

    let timeWatch = System.Diagnostics.Stopwatch.StartNew()
    let time() = timeWatch.MicroTime
        
    let pool = GeometryPool.Get ctx
        

    let reader =
        let roots = roots |> ASet.map (fun r -> { r with root = UniqueTree(Guid.NewGuid(), None, None, r.root) :> ILodTreeNode })
        roots.GetReader()
    let euclideanView = config.view |> Mod.map Euclidean3d

  
    let loadTimes = System.Collections.Concurrent.ConcurrentDictionary<Symbol, Regression>()
    let expandTime = RunningMean(5)
    let maxQualityTime = RunningMean(5)
    let updateTime = RunningMean(4)

    let addLoadTime (kind : Symbol) (size : int) (t : MicroTime) =
        let mean = loadTimes.GetOrAdd(kind, fun _ -> Regression(1, 100))
        lock mean (fun () -> mean.Add(size, t))

    let getLoadTime (kind : Symbol) (size : int) =
        match loadTimes.TryGetValue kind with
            | (true, mean) -> 
                lock mean (fun () -> mean.Evaluate size)
            | _ -> 
                MicroTime.FromMilliseconds 200.0


    let cache = Dict<ILodTreeNode, PoolSlot>()

    let needUpdate = Mod.init ()
    let renderDelta : ref<AtomicQueue<ILodTreeNode, GeometryPoolInstance>> = ref AtomicQueue.empty

    let pRenderBounds : nativeptr<int> = 
        NativePtr.allocArray [| (if config.renderBounds.GetValue() then 1 else 0) |]

    let rootIdsLock = obj()
    let rootIds : ModRef<hmap<ILodTreeNode, int>> = Mod.init HMap.empty

    let mutable rootUniforms : hmap<ILodTreeNode, MapExt<string, IMod>> = HMap.empty
    let toFreeUniforms : ref<hset<ILodTreeNode>> = ref HSet.empty
        
    let rootUniformCache = System.Collections.Concurrent.ConcurrentDictionary<ILodTreeNode, System.Collections.Concurrent.ConcurrentDictionary<string, Option<IMod>>>()
    let rootTrafoCache = System.Collections.Concurrent.ConcurrentDictionary<ILodTreeNode, IMod<Trafo3d>>()
    let rootTrafoWorldCache = System.Collections.Concurrent.ConcurrentDictionary<ILodTreeNode, IMod<Trafo3d>>()
    



    let getRootTrafo (root : ILodTreeNode) =
        let root = root.Root
        rootTrafoCache.GetOrAdd(root, fun root ->
            match HMap.tryFind root rootUniforms with
            | Some table -> 
                match MapExt.tryFind "ModelTrafo" table with
                | Some (:? IMod<Trafo3d> as m) -> (m |> Mod.map ( fun m -> root.DataTrafo * m )) %* config.model
                | _ -> config.model |> Mod.map ( fun m -> root.DataTrafo * m )
            | None ->
                Log.error "bad trafo"
                config.model |> Mod.map ( fun m -> root.DataTrafo * m )
        )
    let getRootUniform (name : string) (root : ILodTreeNode) : Option<IMod> =
        let root = root.Root
        let rootCache = rootUniformCache.GetOrAdd(root, fun root -> System.Collections.Concurrent.ConcurrentDictionary())
        rootCache.GetOrAdd(name, fun name ->
            match name with
            | "ModelTrafos"              -> getRootTrafo root :> IMod |> Some
            | "ModelTrafosInv"           -> getRootTrafo root |> Mod.map (fun t -> t.Inverse) :> IMod |> Some

            | "ModelViewTrafos"          -> Mod.map2 (fun a b -> a * b) (getRootTrafo root) config.view :> IMod |> Some
            | "ModelViewTrafosInv"       -> getRootTrafo root %* config.view |> Mod.map (fun t -> t.Inverse) :> IMod |> Some

            | "ModelViewProjTrafos"      -> getRootTrafo root %* config.view %* config.proj :> IMod |> Some
            | "ModelViewProjTrafosInv"   -> getRootTrafo root %* config.view %* config.proj |> Mod.map (fun t -> t.Inverse) :> IMod |> Some

            | "NormalMatrices"           -> getRootTrafo root |> Mod.map (fun t -> M33d.op_Explicit t.Backward.Transposed):> IMod |> Some
            | "NormalMatricesInv"        -> getRootTrafo root |> Mod.map (fun t -> M33d.op_Explicit t.Forward.Transposed):> IMod |> Some
            | _ -> 
                match HMap.tryFind root rootUniforms with
                | Some table -> MapExt.tryFind name table
                | None -> None
        )

    let getRootId (root : ILodTreeNode) =   
        let root = root.Root
        match HMap.tryFind root rootIds.Value with
        | Some id -> 
            id
        | None ->
            transact (fun () -> 
                lock rootIdsLock (fun () ->
                    let ids = Set.ofSeq (Seq.map snd (HMap.toSeq rootIds.Value))
                    let free = Seq.initInfinite id |> Seq.find (fun i -> not (Set.contains i ids))
                    let n = HMap.add root free rootIds.Value
                    rootIds.Value <- n
                    free
                )
            )

    let freeRootId (root : ILodTreeNode) =
        let root = root.Root
        rootUniformCache.TryRemove root |> ignore
        rootTrafoCache.TryRemove root |> ignore
        transact (fun () ->
            lock rootIdsLock (fun () ->
                rootIds.Value <- HMap.remove root rootIds.Value
            )
        )

    let contents =
        state.pProgramInterface.storageBuffers |> MapExt.toSeq |> Seq.choose (fun (name, buffer) ->
            if state.pStorageBuffers |> Array.exists (fun struct (id, _) -> id = buffer.ssbBinding) then
                None
            else
                let typ = GLSLType.toType buffer.ssbType
                let conv = PrimitiveValueConverter.convert typ
                    
                let content =
                    Mod.custom (fun t ->
                        let ids = rootIds.GetValue t
                        if HMap.isEmpty ids then
                            ArrayBuffer (System.Array.CreateInstance(typ, 0)) :> IBuffer
                        else
                            let maxId = ids |> HMap.toSeq |> Seq.map snd |> Seq.max
                            let data = System.Array.CreateInstance(typ, 1 + maxId)
                            ids |> HMap.iter (fun root id ->
                                match getRootUniform name root with
                                | Some v ->
                                    let vc = v.GetValue(t) |> conv
                                    data.SetValue(vc, id)
                                | None ->
                                    ()
                            )
                            ArrayBuffer data :> IBuffer
                    )
                Some (buffer.ssbBinding, content)
        )
        |> Map.ofSeq
            
    let storageBuffers =
        contents |> Map.map (fun _ content ->
            let b = manager.CreateBuffer(content)
            b.AddRef()
            b.Update(AdaptiveToken.Top, RenderToken.Empty)
            b
        )

    let activeBuffer =
        let data = 
            Mod.custom (fun t ->
                let ids = rootIds.GetValue t
                if HMap.isEmpty ids then
                    ArrayBuffer (Array.empty<int>) :> IBuffer
                else
                    let maxId = ids |> HMap.toSeq |> Seq.map snd |> Seq.max
                    let data : int[] = Array.zeroCreate (1 + maxId)
                    ids |> HMap.iter (fun root id ->
                        match getRootUniform "TreeActive" root with
                        | Some v ->
                            match v.GetValue(t) with
                            | :? bool as b ->
                                data.[id] <- (if b then 1 else 0)
                            | _ ->
                                data.[id] <- 1
                        | None ->
                            data.[id] <- 1
                    )
                    ArrayBuffer data :> IBuffer
            )
        manager.CreateBuffer data

    let modelViewProjBuffer =
        let data = 
            Mod.custom (fun t ->
                let ids = rootIds.GetValue t
                if HMap.isEmpty ids then
                    ArrayBuffer (Array.empty<M44f>) :> IBuffer
                else
                    let maxId = ids |> HMap.toSeq |> Seq.map snd |> Seq.max
                    let data : M44f[] = Array.zeroCreate (1 + maxId)
                    ids |> HMap.iter (fun root id ->
                        match getRootUniform "ModelViewProjTrafos" root with
                        | Some v ->
                            match v.GetValue(t) with
                            | :? Trafo3d as b ->
                                data.[id] <- M44f.op_Explicit b.Forward
                            | _ ->
                                failwith "bad anarchy"
                        | None ->
                                failwith "bad anarchy"
                    )
                    ArrayBuffer data :> IBuffer
            )
        manager.CreateBuffer data
            
    let allocWatch = System.Diagnostics.Stopwatch()
    let uploadWatch = System.Diagnostics.Stopwatch()
    let activateWatch = System.Diagnostics.Stopwatch()
    let freeWatch = System.Diagnostics.Stopwatch()
    let deactivateWatch = System.Diagnostics.Stopwatch()
        
    let alloc (state : RenderState) (node : ILodTreeNode) (g : GeometryPoolInstance) =
        ensure 
            (fun () -> not (cache.ContainsKey node))
            "%A already existing" node 

        cache.GetOrCreate(node, fun node ->
            let slot = pool.Alloc(g.signature, g.instanceCount, g.indexCount, g.vertexCount)
            slot.Upload(g.geometry, g.uniforms)

            state.uploadSize <- state.uploadSize + slot.Memory
            state.nodeSize <- state.nodeSize + node.DataSize
            state.count <- state.count + 1
            slot
        )
            
    let performOp (state : RenderState) (parentOp : AtomicOperation<ILodTreeNode, GeometryPoolInstance>) (node : ILodTreeNode) (op : Operation<GeometryPoolInstance>) =
        let rootId = 
            match HMap.tryFind node.Root rootIds.Value with
            | Some id -> id
            | _ -> -1
            
        match op with
            | Alloc(instance, active) ->
                allocWatch.Start()
                inc &state.allocs
                let slot = alloc state node instance
                allocWatch.Stop()

                if active > 0 then 
                    activateWatch.Start()
                    let b = node.WorldBoundingBox.Transformed(node.DataTrafo.Inverse)
                    let cb = node.WorldCellBoundingBox.Transformed(node.DataTrafo.Inverse)
                    let w = state.calls.Add(slot, b, cb, rootId)
                    if not w then bad "alloc cannot activate %s (was already active)" node.Name
                    activateWatch.Stop()

                elif active < 0 then 
                    deactivateWatch.Start()
                    let w = state.calls.Remove slot
                    if not w then bad "alloc cannot deactivate %s (was already inactive)" node.Name
                    deactivateWatch.Stop()

            | Free ac ->
                if node.Level = 0 then
                    lock toFreeUniforms (fun () ->
                        toFreeUniforms := HSet.add node !toFreeUniforms
                    )
                    //rootUniforms <- HMap.remove node rootUniforms
                    //freeRootId node

                match cache.TryRemove node with
                    | (true, slot) -> 
                        if slot.IsDisposed then
                            bad "cannot free %s (was already free)" node.Name
                        else
                            if ac < 0 then 
                                deactivateWatch.Start()
                                let w = state.calls.Remove slot
                                if not w then bad "free cannot deactivate %s (was already inactive)" node.Name
                                deactivateWatch.Stop()
                            else 
                                ensure (fun () -> not (state.calls.Remove slot)) "free must deactivate before deleting %s" node.Name

                    
                            freeWatch.Start()
                            pool.Free slot
                            freeWatch.Stop()

                    | _ ->
                        bad "cannot free %s" node.Name
                            

            | Activate ->
                match cache.TryGetValue node with
                    | (true, slot) ->
                        activateWatch.Start()
                        let b = node.WorldBoundingBox.Transformed(node.DataTrafo.Inverse)
                        let cb = node.WorldCellBoundingBox.Transformed(node.DataTrafo.Inverse)
                        if not (state.calls.Add(slot, b, cb, rootId)) then bad "%s already active" node.Name
                        activateWatch.Stop()
                    | _ ->
                        bad "cannot activate %A %A" node (Option.isSome op.value)

                    

            | Deactivate ->
                match cache.TryGetValue node with
                    | (true, slot) ->
                        deactivateWatch.Start()
                        if not (state.calls.Remove slot) then bad "%s not active" node.Name
                        deactivateWatch.Stop()
                    | _ ->
                        bad "cannot deactivate %A %A" node (Option.isSome op.value)
                    
            | Nop ->
                ()
            
    let perform (state : RenderState) (op : AtomicOperation<ILodTreeNode, GeometryPoolInstance>) =
        op.ops |> HMap.iter (performOp state op)

    let rec enter (l : obj) =
        let gotLock = Monitor.TryEnter(l, 5)
        if not gotLock then
            enter l

    let sync() =
        GL.Flush()
        GL.Finish()

    let run (token : AdaptiveToken) (maxMem : Mem) (maxTime : MicroTime) (calls : DrawPool) (iface : GLSLProgramInterface) =
        sync()

            
        let state =
            {
                iface = iface
                calls = calls
                allocs = 0
                uploadSize = Mem.Zero
                nodeSize = 0
                count = 0
            }

        allocWatch.Reset()
        uploadWatch.Reset()
        activateWatch.Reset()
        freeWatch.Reset()
        deactivateWatch.Reset()

        let sw = System.Diagnostics.Stopwatch.StartNew()
      
        let rec run (cnt : int)  =
            let mem = state.uploadSize > maxMem
            let time = sw.MicroTime > maxTime 

            if mem || time then
                if state.nodeSize > 0 && state.count > 0 then
                    updateTime.Add(sw.MicroTime.TotalMilliseconds)

                //Log.line "rerun: %d (%d)" cnt renderDelta.Value.Count
                config.time.GetValue token |> ignore
            else
                let dequeued = 
                    enter renderDelta
                    try
                        match AtomicQueue.tryDequeue !renderDelta with
                        | Some (ops, rest) ->
                            renderDelta := rest
                            Some ops
                        | None ->
                            None
                    finally
                        Monitor.Exit renderDelta

                match dequeued with
                | None -> 
                    if state.nodeSize > 0 && state.count > 0 then
                        updateTime.Add(sw.MicroTime.TotalMilliseconds)
  
                    //Log.line "done: %d (%d)" cnt renderDelta.Value.Count

                | Some ops ->
                    perform state ops
                    sync()
                    run (cnt + 1)
                        
        run 0

    let evaluate (calls : DrawPool) (token : AdaptiveToken) (iface : GLSLProgramInterface) =
        //Log.line "update"
        needUpdate.GetValue(token)

        NativePtr.write pRenderBounds (if config.renderBounds.GetValue(token) then 1 else 0)

        let maxTime = max (MicroTime.FromMilliseconds 1.0) calls.AverageRenderTime
        let maxMem = Mem (3L <<< 30)
        run token maxMem maxTime calls iface
        sync()
            
    let inner =
        { new DrawPool(ctx, config.alphaToCoverage, true, pRenderBounds, activeBuffer.Pointer, modelViewProjBuffer.Pointer, state, config.pass) with
            override x.Evaluate(token : AdaptiveToken, iface : GLSLProgramInterface) =
                evaluate x token iface

            override x.BeforeRender(stream : ICommandStream) =
                for (slot, b) in Map.toSeq storageBuffers do 
                    stream.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, slot, b.Pointer)
        }

        
    let shutdown = new CancellationTokenSource()
        
    let knownInputs =
        Map.ofList [
            "TreeId", None
                
            //"ModelTrafos", Some ("ModelTrafos", typeof<Trafo3d>)
            "ModelTrafosInv", Some ("ModelTrafos", typeof<Trafo3d>)

            "ModelViewTrafos", Some ("ModelTrafos", typeof<Trafo3d>)
            "ModelViewTrafosInv", Some ("ModelTrafos", typeof<Trafo3d>)

            "ModelViewProjTrafos", Some ("ModelTrafos", typeof<Trafo3d>)
            "ModelViewProjTrafosInv", Some ("ModelTrafos", typeof<Trafo3d>)

            "NormalMatrices", Some ("ModelTrafos", typeof<Trafo3d>)
            "NormalMatricesInv", Some ("ModelTrafos", typeof<Trafo3d>)
        ]

    let filterInputs (m : MapExt<string, Type>) =
        knownInputs |> Map.fold (fun m k v ->
            match MapExt.tryRemove k m with
            | Some (_, r) ->
                match v with
                | Some(n,t) -> MapExt.add n t r
                | None -> r
            | None ->
                m
        ) m

    let wantedInputs =
        let inputs =
            state.pProgramInterface.inputs 
                |> List.map (fun p -> p.paramSemantic, GLSLType.toType p.paramType)
                |> MapExt.ofList
        let perTreeUniforms =
            state.pProgramInterface.storageBuffers
                |> MapExt.toSeq
                |> Seq.map (fun (_,b) -> b.ssbName, GLSLType.toType b.ssbType)
                |> MapExt.ofSeq
        let res = MapExt.union inputs perTreeUniforms |> filterInputs
        Log.warn "%A" res
        res

    let cameraPrediction, puller, thread =
        let prediction = Prediction.euclidean (MicroTime(TimeSpan.FromMilliseconds 55.0))
        let rootLock = obj()
        let mutable roots : hmap<ILodTreeNode, TaskTree<GeometryPoolInstance>> = HMap.empty
        let mutable lastQ = 0.0
        let mutable lastRoots = HMap.empty
        let mutable readers : hmap<ILodTreeNode, TaskTreeReader<_>> = HMap.empty
            
        let changesPending = MVar.create ()

        let state =
            {
                trigger         = changesPending
                runningTasks    = ref 0
                ready           = ref 0
                totalNodes      = ref 0
                version         = ref 0
                splits          = ref 0
                collapses       = ref 0
                dataSize        = ref 0L
            }

                

        let cameraPrediction =
            startThread (fun () ->
                let mutable lastTime = time()
                let mutable lastReport = time()
                let timer = new MultimediaTimer.Trigger(5)
                    
                while not shutdown.IsCancellationRequested do
                    timer.Wait()
                    let view = euclideanView.GetValue()
                    prediction.Add(view)

                    let flushTime = max (MicroTime.FromMilliseconds 10.0) inner.AverageRenderTime
                    let now = time()
                    if not (AtomicQueue.isEmpty !renderDelta) && (now - lastTime > flushTime) then
                        lastTime <- now
                        //Log.line "trigger: %A" renderDelta.Value.Count
                        transact (fun () -> needUpdate.MarkOutdated())
                            

                    if now - lastReport > MicroTime.FromSeconds 2.0 then
                        lastReport <- now

                        let linearRegressionStr (parameter : string) (r : Regression) =
                            let d = r.Evaluate(0)
                            let k = r.Evaluate(1) - d
                            match d = MicroTime.Zero, k = MicroTime.Zero with
                                | true, true    -> sprintf "0" 
                                | true, false   -> sprintf "%s*%A" parameter k
                                | false, true   -> sprintf "%A" d
                                | false, false  -> sprintf "%s*%A+%A" parameter k d
                                

                        let collapses = Interlocked.Exchange(&state.collapses.contents, 0)
                        let splits = Interlocked.Exchange(&state.splits.contents, 0)
                        let tasks = !state.runningTasks
                        let points = !state.dataSize

                        let loads = 
                            loadTimes 
                            |> Seq.map (fun (KeyValue(n,r)) -> 
                                sprintf "%s: %s" (string n) (linearRegressionStr "s" r)
                            )
                            |> String.concat "; "
                            |> sprintf "[%s]"
                                
                        let e = expandTime.Average |> MicroTime.FromMilliseconds
                        let u = updateTime.Average |> MicroTime.FromMilliseconds
                        let q = maxQualityTime.Average |> MicroTime.FromMilliseconds
                        ()

                        //let roots = readers |> HMap.keys |> Seq.map string |> String.concat ", "
                        //Log.line "%s" roots

                        Log.line "q: %.2f m: %A (%A) r: %A l : %s mq: %A e: %A u: %A c: %d s: %d t: %d d: %A" 
                                    lastQ 
                                    (pool.UsedMemory + inner.UsedMemory) 
                                    (pool.TotalMemory + inner.TotalMemory) 
                                    inner.AverageRenderTime 
                                    loads q e u 
                                    collapses splits tasks points

            )
                
        //let rootDeltas = ref HDeltaSet.empty

        let puller =
            startThread (fun () ->
                let timer = new MultimediaTimer.Trigger(20)
                    
                let pickTrees = config.pickTrees
                while not shutdown.IsCancellationRequested do
                    timer.Wait()
                    MVar.take changesPending
                    
                    // atomic fetch
                    let readers, removed = 
                        lock rootLock (fun () ->
                            let roots = roots
                            let removed = List<Option<ILodTreeNode> * TaskTreeReader<_>>()

                            let delta = HMap.computeDelta lastRoots roots
                            lastRoots <- roots

                            for k, op in delta do
                                match op with
                                | Remove ->
                                    match HMap.tryRemove k readers with
                                    | Some (r, rs) ->
                                        readers <- rs
                                        removed.Add((Some k, r))
                                    | None ->
                                        Log.error "[Lod] free of unknown: %A" (k.GetHashCode())
                                | Set v ->
                                    match HMap.tryFind k readers with
                                    | Some o ->
                                        Log.error "[Lod] double add %A" k
                                        removed.Add (None, o)
                                        let r = TaskTreeReader(v)
                                        //rootUniformCache.TryRemove k |> ignore
                                        //rootTrafoCache.TryRemove k |> ignore
                                        //transact (fun () -> rootIds.MarkOutdated())
                                        readers <- HMap.add k r readers

                                    | None ->
                                        let r = TaskTreeReader(v)
                                        readers <- HMap.add k r readers
                            readers, removed
                        )

                    for (root, r) in removed do
                        match root with
                        | Some root -> 
                            match pickTrees with
                            | Some mm -> transact (fun () -> mm.Value <- mm.Value |> HMap.remove root)
                            | None -> ()
                        | None ->
                            ()
                        r.Destroy(renderDelta)

                    for (root,r) in readers do
                        r.Update(renderDelta)
                        match pickTrees with
                        | Some mm ->
                            let trafo = getRootTrafo root
                            let picky = r.State |> Option.map (fun s -> SimplePickTree.ofTreeNode trafo s)
                            transact (fun () -> 
                                match picky with
                                | Some picky -> mm.Value <- mm.Value |> HMap.add root picky
                                | None -> mm.Value <- mm.Value |> HMap.remove root
                            )
                        | None ->
                            Log.warn "empty tree: %A" root
                            ()
                    

                for (root,r) in readers do
                    match pickTrees with
                    | Some mm -> 
                        transact (fun () -> mm.Value <- mm.Value |> HMap.remove root)
                    | None -> ()
                    r.Destroy(renderDelta)
            )
                
        let thread = 
            startThread (fun () ->
                let notConverged = MVar.create () //new ManualResetEventSlim(true)

                let cancel = System.Collections.Concurrent.ConcurrentDictionary<ILodTreeNode, CancellationTokenSource>()
                let timer = new MultimediaTimer.Trigger(10)
                    
                let stop (node : ILodTreeNode) =
                    match cancel.TryRemove node with
                        | (true, c) -> 
                            try c.Cancel()
                            with :? ObjectDisposedException -> ()
                        | _ -> 
                            ()
         
                let load (ct : CancellationToken) (rootId : int) (node : ILodTreeNode) (cont : CancellationToken -> ILodTreeNode -> GeometryPoolInstance -> 'r) =
                    startTask ct (fun () ->
                        let startTime = time()
                        let (g,u) = node.GetData(ct, wantedInputs)

                        let cnt = 
                            match Seq.tryHead u with
                            | Some (KeyValue(_, (v : Array) )) -> v.Length
                            | _ -> 1

                        let u = MapExt.add "TreeId" (Array.create cnt rootId :> System.Array) u
                        let loaded = GeometryPoolInstance.ofGeometry signature g u
                                
                        let endTime = time()
                        addLoadTime node.DataSource node.DataSize (endTime - startTime)

                        

                        if not ct.IsCancellationRequested then
                            let res = cont ct node loaded
                            res
                        else
                            raise <| OperationCanceledException()
                    )

                      
                let subs =
                    Dict.ofList [
                        config.view :> IAdaptiveObject, config.view.AddMarkingCallback (MVar.put notConverged)
                        config.proj :> IAdaptiveObject, config.proj.AddMarkingCallback (MVar.put notConverged)
                        config.maxSplits :> IAdaptiveObject, config.maxSplits.AddMarkingCallback (MVar.put notConverged)
                        config.budget :> IAdaptiveObject, config.budget.AddMarkingCallback (MVar.put notConverged)
                        reader :> IAdaptiveObject, reader.AddMarkingCallback (MVar.put notConverged)
                        config.splitfactor :> IAdaptiveObject, config.splitfactor.AddMarkingCallback (MVar.put notConverged)
                    ]

                let mutable lastMaxQ = 0.0
                try 
                    let mutable deltas = HDeltaSet.empty
                    let reg = shutdown.Token.Register (System.Action(MVar.put notConverged))
                    while not shutdown.IsCancellationRequested do
                        timer.Wait()
                        MVar.take notConverged
                        shutdown.Token.ThrowIfCancellationRequested()

                        //caller.EvaluateAlways AdaptiveToken.Top (fun token ->
                        let view = config.view.GetValue AdaptiveToken.Top
                        let proj = config.proj.GetValue AdaptiveToken.Top
                        let maxSplits = config.maxSplits.GetValue AdaptiveToken.Top
                          
                        deltas <-   
                            let ops = reader.GetOperations AdaptiveToken.Top
                            HDeltaSet.combine deltas ops

                        let toFree = 
                            lock toFreeUniforms (fun () ->
                                let r = !toFreeUniforms
                                toFreeUniforms := HSet.empty
                                r
                            )

                        toFree |> HSet.iter ( fun node -> 
                            freeRootId node
                            rootUniforms <- HMap.remove node rootUniforms
                        )
                        

                        if maxSplits > 0 then
                            let ops =
                                let d = deltas
                                deltas <- HDeltaSet.empty
                                d

                            //if ops.Count > 0 then
                            //    lock rootDeltas (fun () ->
                            //        rootDeltas := HDeltaSet.combine !rootDeltas ops
                            //    )
                            //    MVar.put changesPending ()

                            //if ops.Count > 0 then 
                            //    for o in ops do
                            //        match o with
                            //        | Add(_,v) ->
                            //            Log.warn "add %A" v.root
                            //        | Rem(_,v) ->
                            //            Log.warn "rem %A" v.root

                            let roots = 
                                if ops.Count > 0 then
                                    lock rootLock (fun () ->
                                        for o in ops do
                                            match o with
                                            | Add(_,i) ->
                                                let r = i.root
                                                match HMap.tryFind r roots with
                                                | Some o ->
                                                    Log.error "[Lod] add of existing root %A" i.root
                                                | None -> 
                                                    let u = i.uniforms
                                                    rootUniforms <- HMap.add r u rootUniforms
                                                    let rid = getRootId r
                                                    let load ct n = load ct rid n (fun _ _ r -> r)
                                                    roots <- HMap.add r (TaskTree(load, rid)) roots

                                            | Rem(_,i) ->
                                                let r = i.root
                                                match HMap.tryRemove r roots with
                                                | Some (_, rest) ->
                                                    
                                                    roots <- rest
                                                | None ->
                                                    Log.error "[Lod] remove of nonexisting root %A" i.root

                                        MVar.put changesPending ()
                                        roots
                                    )
                                else
                                    lock rootLock (fun () -> roots)
                                

                            let modelView (r : ILodTreeNode) =
                                let t = getRootTrafo r
                                subs.GetOrCreate(t, fun t -> t.AddMarkingCallback (MVar.put notConverged)) |> ignore
                                let m = t.GetValue()
                                m * view
                                
                            let budget = config.budget.GetValue()
                            let splitfactor = config.splitfactor.GetValue()

                            let start = time()
                            let maxQ =
                                if budget < 0L then 1.0
                                else fst (TreeHelpers.getMaxQuality budget splitfactor (Seq.map fst roots) modelView proj)

                            let dt = time() - start
                            maxQualityTime.Add(dt.TotalMilliseconds)


                            
                            let collapseIfNotSplit = maxQ < 1.0
                            let start = time()
                            let queue = List()
                            for (k,v) in roots do
                                v.BuildQueue(state, collapseIfNotSplit, k, maxQ, splitfactor, modelView, proj, queue)
            
                            let q, fin = TaskTree<_>.ProcessQueue(state, queue, maxQ, splitfactor, modelView, proj, maxSplits)
                            lastQ <- q
                            let dt = time() - start
                            expandTime.Add dt.TotalMilliseconds

                            transact (fun () -> 
                                config.stats.Value <-
                                    {
                                        quality = lastQ
                                        maxQuality = maxQ
                                        totalPrimitives = !state.dataSize
                                        totalNodes = !state.totalNodes
                                        usedMemory = pool.UsedMemory + inner.UsedMemory
                                        allocatedMemory = pool.TotalMemory + inner.TotalMemory 
                                        renderTime = inner.AverageRenderTime 
                                    }
                            )
                            if not fin then MVar.put notConverged ()


                finally 
                    subs |> Seq.iter (fun s -> s.Value.Dispose())
            )

        cameraPrediction, puller, thread
        
    member x.UsedMemory : Mem = pool.UsedMemory + inner.UsedMemory
    member x.TotalMemory : Mem = pool.TotalMemory + inner.TotalMemory
        
    override x.Compile(a,b,c) = inner.Compile(a,b,c)
    override x.GetResources() = 
        Seq.concat [ 
            Seq.singleton (activeBuffer :> IResource)
            Seq.singleton (modelViewProjBuffer :> IResource)
            (storageBuffers |> Map.toSeq |> Seq.map snd |> Seq.cast) 
            (inner.Resources :> seq<_>)
        ]

    override x.Release() =
        shutdown.Cancel()
        cameraPrediction.Join()
        thread.Join()
        puller.Join()
        reader.Dispose()
        inner.Dispose()
        loadTimes.Clear()
        for slot in cache.Values do pool.Free slot
        cache.Clear()
        renderDelta := AtomicQueue.empty
        storageBuffers |> Map.toSeq |> Seq.iter (fun (_,b) -> b.Dispose())
        activeBuffer.Dispose()

    override x.EntryState = inner.EntryState
    override x.ExitState = inner.ExitState

