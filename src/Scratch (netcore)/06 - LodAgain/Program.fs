open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.Base.Incremental.Operators
open Aardvark.SceneGraph
open Aardvark.Rendering.Text
open Aardvark.Application

#nowarn "9"

open System.Threading.Tasks
open System
open System.Collections.Generic
open System.Threading

[<AutoOpen>]
module ``Stopwatch Extensions`` =
    open System.Diagnostics

    type TimeSpan with
        member x.MicroTime = MicroTime(x.Ticks * (1000000000L / TimeSpan.TicksPerSecond))

    type Stopwatch with
        member x.MicroTime = x.Elapsed.MicroTime 

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

    abstract member ShouldSplit : float * Trafo3d * Trafo3d -> bool
    abstract member ShouldCollapse : float * Trafo3d * Trafo3d -> bool
        
    abstract member SplitQuality : Trafo3d * Trafo3d -> float
    abstract member CollapseQuality : Trafo3d * Trafo3d -> float

    abstract member BoundingBox : Box3d
    abstract member CellBoundingBox : Box3d
    abstract member Cell : Cell

    abstract member Acquire : unit -> unit
    abstract member Release : unit -> unit

type LodTreeInstance =
    {
        root        : ILodTreeNode
        uniforms    : MapExt<string, IMod>
    }

[<AutoOpen>]
module Tools = 
    [<Struct; StructuredFormatDisplay("{AsString}")>]
    type Num(v : int64) =
        member x.Value = v

        member private x.AsString = x.ToString()

        override x.ToString() =
            let a = abs v
            if v = 0L then "0"
            elif a >= 1_000_000_000L then sprintf "%.3fB" (float v / 1000000000.0)
            elif a >= 1_000_000L then sprintf "%.2fM" (float v / 1000000.0)
            elif a >= 1_000L then sprintf "%.1fk" (float v / 1000.0)
            else sprintf "%d" v

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
        if o.alloc > 0 then Alloc(o.value.Value, o.active)
        elif o.alloc < 0 then Free(o.active)
        elif o.active > 0 then Activate
        elif o.active < 0 then Deactivate
        else Nop
        
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



module Bla =
    open FShade
    open FShade.GLSL
    open OpenTK.Graphics.OpenGL4
    open Aardvark.Rendering.GL
    open System.Runtime.InteropServices
    open Aardvark.Base.Management
    open Microsoft.FSharp.NativeInterop
    open Aardvark.Base.Runtime


    [<ReflectedDefinition>]
    module CullingShader =
        open FShade

        //typedef  struct {
        //    uint  count;
        //    uint  primCount;
        //    uint  firstIndex;
        //    uint  baseVertex;
        //    uint  baseInstance;
        //} DrawElementsIndirectCommand;
        type DrawInfo =
            struct
                val mutable public FaceVertexCount : int
                val mutable public InstanceCount : int
                val mutable public FirstIndex : int
                val mutable public BaseVertex : int
                val mutable public FirstInstance : int
            end
            
        [<StructLayout(LayoutKind.Sequential)>]
        type CullingInfo =
            struct
                val mutable public Min : V4f
                val mutable public Max : V4f
                val mutable public CellMin : V4f
                val mutable public CellMax : V4f
            end

        module CullingInfo =
            let instanceCount (i : CullingInfo) =
                int i.Min.W
                
            let getMinMaxInDirection (v : V3d) (i : CullingInfo) =
                let mutable l = V3d.Zero
                let mutable h = V3d.Zero

                if v.X >= 0.0 then
                    l.X <- float i.Min.X
                    h.X <- float i.Max.X
                else
                    l.X <- float i.Max.X
                    h.X <- float i.Min.X
                    
                if v.Y >= 0.0 then
                    l.Y <- float i.Min.Y
                    h.Y <- float i.Max.Y
                else
                    l.Y <- float i.Max.Y
                    h.Y <- float i.Min.Y
                    
                if v.Z >= 0.0 then
                    l.Z <- float i.Min.Z
                    h.Z <- float i.Max.Z
                else
                    l.Z <- float i.Max.Z
                    h.Z <- float i.Min.Z

                (l,h)

            let onlyBelow (plane : V4d) (i : CullingInfo) =
                let l, h = i |> getMinMaxInDirection plane.XYZ
                Vec.dot l plane.XYZ + plane.W < 0.0 && Vec.dot h plane.XYZ + plane.W < 0.0

            let intersectsViewProj (viewProj : M44d) (i : CullingInfo) =
                let r0 = viewProj.R0
                let r1 = viewProj.R1
                let r2 = viewProj.R2
                let r3 = viewProj.R3

                if  onlyBelow (r3 + r0) i || onlyBelow (r3 - r0) i ||
                    onlyBelow (r3 + r1) i || onlyBelow (r3 - r1) i ||
                    onlyBelow (r3 + r2) i || onlyBelow (r3 - r2) i then
                    false
                else
                    true

        [<LocalSize(X = 64)>]
        let culling (infos : DrawInfo[]) (bounds : CullingInfo[]) (isActive : int[]) (count : int) (viewProjs : M44d[]) =
            compute {
                let id = getGlobalId().X
                if id < count then
                    let b = bounds.[id]
                    let rootId = int (b.Max.W + 0.5f)
                    
                    if isActive.[rootId] <> 0 && CullingInfo.intersectsViewProj viewProjs.[rootId] b then
                        infos.[id].InstanceCount <- CullingInfo.instanceCount b
                    else
                        infos.[id].InstanceCount <- 0
            }

        type UniformScope with
            member x.Bounds : CullingInfo[] = uniform?StorageBuffer?Bounds 
            member x.ViewProjs : M44d[] = uniform?StorageBuffer?ViewProjs 

        type Vertex =
            {
                [<InstanceId>] id : int
                [<VertexId>] vid : int
                [<Position>] pos : V4d
            }

        let data =
            [|
                V3d.OOO; V3d.IOO
                V3d.OOI; V3d.IOI
                V3d.OIO; V3d.IIO
                V3d.OII; V3d.III
                
                V3d.OOO; V3d.OIO
                V3d.OOI; V3d.OII
                V3d.IOO; V3d.IIO
                V3d.IOI; V3d.III
                
                V3d.OOO; V3d.OOI
                V3d.OIO; V3d.OII
                V3d.IOO; V3d.IOI
                V3d.IIO; V3d.III
            |]

        let renderBounds (v : Vertex) =
            vertex {
                let bounds = uniform.Bounds.[v.id]
                let rootId = int (bounds.Max.W + 0.5f)
                    
                let off = V3d bounds.CellMin.XYZ
                let size = V3d bounds.CellMax.XYZ - off

                let p = data.[v.vid]

                let wp = off + p * size
                let p = uniform.ViewProjs.[rootId] * V4d(wp, 1.0)
                return { v with pos = p }
            }


    type InstanceSignature = MapExt<string, GLSLType * Type>
    type VertexSignature = MapExt<string, Type>

    type GeometrySignature =
        {
            mode            : IndexedGeometryMode
            indexType       : Option<Type>
            uniformTypes    : InstanceSignature
            attributeTypes  : VertexSignature
        }

    module GeometrySignature =
        let ofGeometry (iface : GLSLProgramInterface) (uniforms : MapExt<string, Array>) (g : IndexedGeometry) =
            let mutable uniformTypes = MapExt.empty
            let mutable attributeTypes = MapExt.empty

            for i in iface.inputs do
                let sym = Symbol.Create i.paramSemantic
                match MapExt.tryFind i.paramSemantic uniforms with
                    | Some arr when not (isNull arr) ->
                        let t = arr.GetType().GetElementType()
                        uniformTypes <- MapExt.add i.paramSemantic (i.paramType, t) uniformTypes
                    | _ ->
                        let t = if isNull g.SingleAttributes then (false, Unchecked.defaultof<_>) else g.SingleAttributes.TryGetValue sym
                        match t with
                            | (true, uniform) ->
                                assert(not (isNull uniform))
                                let t = uniform.GetType()
                                uniformTypes <- MapExt.add i.paramSemantic (i.paramType, t) uniformTypes
                            | _ ->
                                match g.IndexedAttributes.TryGetValue sym with
                                    | (true, arr) ->
                                        assert(not (isNull arr))
                                        let t = arr.GetType().GetElementType()
                                        attributeTypes <- MapExt.add i.paramSemantic t attributeTypes
                                    | _ -> 
                                        ()
              
            let indexType =
                if isNull g.IndexArray then
                    None
                else
                    let t = g.IndexArray.GetType().GetElementType()
                    Some t

            {
                mode = g.Mode
                indexType = indexType
                uniformTypes = uniformTypes
                attributeTypes = attributeTypes
            }


    type Regression(degree : int, maxSamples : int) =
        let samples : array<int * MicroTime> = Array.zeroCreate maxSamples
        let mutable count = 0
        let mutable index = 0
        let mutable model : float[] = null

        let getModel() =
            if count <= 0  then
                [| |]
            elif count = 1 then
                let (x,y) = samples.[0]
                [| 0.0; y.TotalSeconds / float x |]
            else
                let degree = min (count - 1) degree
                let arr = 
                    Array2D.init count (degree + 1) (fun r c ->
                        let (s,_) = samples.[r]
                        float s ** float c
                    )

                let r = samples |> Array.take count |> Array.map (fun (_,t) -> t.TotalSeconds)

                let diag = arr.QrFactorize()
                arr.QrSolve(diag, r)

        member private x.GetModel() = 
            lock x (fun () ->
                if isNull model then model <- getModel()
                model
            )
            
        member x.Add(size : int, value : MicroTime) =
            lock x (fun () ->
                let mutable found = false
                let mutable i = (maxSamples + index - count) % maxSamples
                while not found && i <> index do
                    let (x,y) = samples.[i]
                    if x = size then
                        if y <> value then model <- null
                        samples.[i] <- (size, value)
                        found <- true
                    i <- (i + 1) % maxSamples

                if not found then
                    samples.[index] <- (size,value)
                    index <- (index + 1) % maxSamples
                    if count < maxSamples then count <- count + 1
                    model <- null
            )

        member x.Evaluate(size : int) =
            let model = x.GetModel()
            if model.Length > 0 then
                Polynomial.Evaluate(model, float size) |> MicroTime.FromSeconds
            else 
                MicroTime.Zero

    type Context with
        member x.MapBufferRange(b : Buffer, offset : nativeint, size : nativeint, access : BufferAccessMask) =
            let ptr = GL.MapNamedBufferRange(b.Handle, offset, size, access)
            if ptr = 0n then 
                let err = GL.GetError()
                failwithf "[GL] cannot map buffer %d: %A" b.Handle err
            ptr

        member x.UnmapBuffer(b : Buffer) =
            let worked = GL.UnmapNamedBuffer(b.Handle)
            if not worked then failwithf "[GL] cannot unmap buffer %d" b.Handle

    type InstanceBuffer(ctx : Context, semantics : MapExt<string, GLSLType * Type>, count : int) =
        let buffers, totalSize =
            let mutable totalSize = 0L
            let buffers = 
                semantics |> MapExt.map (fun sem (glsl, input) ->
                    let elemSize = GLSLType.sizeof glsl
                    let write = UniformWriters.getWriter 0 glsl input
                    totalSize <- totalSize + int64 count * int64 elemSize
                    ctx.CreateBuffer(elemSize * count), elemSize, write
                )
            buffers, totalSize
            


        member x.TotalSize = totalSize
        member x.ElementSize = totalSize / int64 count
        member x.Context = ctx
        member x.Data = buffers
        member x.Buffers = buffers |> MapExt.map (fun _ (b,_,_) -> b)
        
        member x.Upload(index : int, count : int, data : MapExt<string, Array>) =
            lock x (fun () ->
                use __ = ctx.ResourceLock
                buffers |> MapExt.iter (fun sem (buffer, elemSize, write) ->
                    let offset = nativeint index * nativeint elemSize
                    let size = nativeint count * nativeint elemSize
                    let ptr = ctx.MapBufferRange(buffer, offset, size, BufferAccessMask.MapWriteBit ||| BufferAccessMask.MapInvalidateRangeBit ||| BufferAccessMask.MapUnsynchronizedBit)
                    match MapExt.tryFind sem data with
                        | Some data ->
                            let mutable ptr = ptr
                            for i in 0 .. count - 1 do
                                write.WriteUnsafeValue(data.GetValue i, ptr)
                                ptr <- ptr + nativeint elemSize
                        | _ -> 
                            Marshal.Set(ptr, 0, elemSize)
                    ctx.UnmapBuffer(buffer)
                )
            )

        static member Copy(src : InstanceBuffer, srcOffset : int, dst : InstanceBuffer, dstOffset : int, count : int) =
            // TODO: locking????
            use __ = src.Context.ResourceLock
            src.Data |> MapExt.iter (fun sem (srcBuffer, elemSize, _) ->
                let (dstBuffer,_,_) = dst.Data.[sem]
                let srcOff = nativeint srcOffset * nativeint elemSize
                let dstOff = nativeint dstOffset * nativeint elemSize
                let s = nativeint elemSize * nativeint count
                GL.NamedCopyBufferSubData(srcBuffer.Handle, dstBuffer.Handle, srcOff, dstOff, s)
            )

        member x.Dispose() =
            use __ = ctx.ResourceLock
            buffers |> MapExt.iter (fun _ (b,_,_) -> ctx.Delete b)

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    type VertexBuffer(ctx : Context, semantics : MapExt<string, Type>, count : int) =

        let totalSize, buffers =
            let mutable totalSize = 0L
            let buffers = 
                semantics |> MapExt.map (fun sem typ ->
                    let elemSize = Marshal.SizeOf typ
                    totalSize <- totalSize + int64 elemSize * int64 count
                    ctx.CreateBuffer(elemSize * count), elemSize, typ
                )
            totalSize, buffers
            
        member x.ElementSize = totalSize / int64 count
        member x.TotalSize = totalSize
        member x.Context = ctx
        member x.Data = buffers
        member x.Buffers = buffers |> MapExt.map (fun _ (b,_,t) -> b,t)
        
        member x.Write(startIndex : int, data : MapExt<string, Array>) =
            lock x (fun () ->
                use __ = ctx.ResourceLock
            
                let count = data |> MapExt.toSeq |> Seq.map (fun (_,a) -> a.Length) |> Seq.min

                buffers |> MapExt.iter (fun sem (buffer, elemSize,_) ->
                    let offset = nativeint startIndex * nativeint elemSize
                    let size = nativeint count * nativeint elemSize
                    let ptr = ctx.MapBufferRange(buffer, offset, size, BufferAccessMask.MapWriteBit ||| BufferAccessMask.MapInvalidateRangeBit ||| BufferAccessMask.MapUnsynchronizedBit)

                    match MapExt.tryFind sem data with
                        | Some data ->  
                            let gc = GCHandle.Alloc(data, GCHandleType.Pinned)
                            try Marshal.Copy(gc.AddrOfPinnedObject(), ptr, size)
                            finally gc.Free()
                        | _ -> 
                            Marshal.Set(ptr, 0, size)
                    ctx.UnmapBuffer(buffer)
                )
            )

        static member Copy(src : VertexBuffer, srcOffset : int, dst : VertexBuffer, dstOffset : int, count : int) =
            // TODO: locking???
            use __ = src.Context.ResourceLock
            src.Data |> MapExt.iter (fun sem (srcBuffer, elemSize,_) ->
                let (dstBuffer,_,_) = dst.Data.[sem]
                let srcOff = nativeint srcOffset * nativeint elemSize
                let dstOff = nativeint dstOffset * nativeint elemSize
                let s = nativeint elemSize * nativeint count
                GL.NamedCopyBufferSubData(srcBuffer.Handle, dstBuffer.Handle, srcOff, dstOff, s)
            )

        member x.Dispose() =
            use __ = ctx.ResourceLock
            buffers |> MapExt.iter (fun _ (b,_,_) -> ctx.Delete b)

        interface IDisposable with
            member x.Dispose() = x.Dispose()


    type VertexManager(ctx : Context, semantics : MapExt<string, Type>, chunkSize : int, usedMemory : ref<int64>, totalMemory : ref<int64>) =
        let elementSize =
            semantics |> MapExt.toSeq |> Seq.sumBy (fun (_,t) -> int64 (Marshal.SizeOf t))

        let mem : Memory<VertexBuffer> =
            let malloc (size : nativeint) =
                Log.warn "alloc VertexBuffer"
                let res = new VertexBuffer(ctx, semantics, int size)
                Interlocked.Add(&totalMemory.contents, res.TotalSize) |> ignore
                res

            let mfree (ptr : VertexBuffer) (size : nativeint) =
                Log.warn "free VertexBuffer"
                Interlocked.Add(&totalMemory.contents, -ptr.TotalSize) |> ignore
                ptr.Dispose()

            {
                malloc = malloc
                mfree = mfree
                mcopy = fun _ _ _ _ _ -> failwith "cannot copy"
                mrealloc = fun _ _ _ -> failwith "cannot realloc"
            }
            
        let mutable used = 0L

        let addMem (v : int64) =
            Interlocked.Add(&usedMemory.contents, v) |> ignore
            Interlocked.Add(&used, v) |> ignore
            

        let manager = new ChunkedMemoryManager<VertexBuffer>(mem, nativeint chunkSize)
        
        member x.Alloc(count : int) = 
            addMem (elementSize * int64 count) 
            manager.Alloc(nativeint count)

        member x.Free(b : Block<VertexBuffer>) = 
            if not b.IsFree then
                addMem (elementSize * int64 -b.Size) 
                manager.Free b

        member x.Dispose() = 
            addMem (-used)
            manager.Dispose()

        interface IDisposable with 
            member x.Dispose() = x.Dispose()
        
    type InstanceManager(ctx : Context, semantics : MapExt<string, GLSLType * Type>, chunkSize : int, usedMemory : ref<int64>, totalMemory : ref<int64>) =
        let elementSize =
            semantics |> MapExt.toSeq |> Seq.sumBy (fun (_,(t,_)) -> int64 (GLSLType.sizeof t))

        let mem : Memory<InstanceBuffer> =
            let malloc (size : nativeint) =
                Log.warn "alloc InstanceBuffer"
                let res = new InstanceBuffer(ctx, semantics, int size)
                Interlocked.Add(&totalMemory.contents, res.TotalSize) |> ignore
                res

            let mfree (ptr : InstanceBuffer) (size : nativeint) =
                Log.warn "free InstanceBuffer"
                Interlocked.Add(&totalMemory.contents, -ptr.TotalSize) |> ignore
                ptr.Dispose()

            {
                malloc = malloc
                mfree = mfree
                mcopy = fun _ _ _ _ _ -> failwith "cannot copy"
                mrealloc = fun _ _ _ -> failwith "cannot realloc"
            }

        let manager = new ChunkedMemoryManager<InstanceBuffer>(mem, nativeint chunkSize)
        let mutable used = 0L

        let addMem (v : int64) =
            Interlocked.Add(&usedMemory.contents, v) |> ignore
            Interlocked.Add(&used, v) |> ignore
            

        member x.Alloc(count : int) = 
            addMem (int64 count * elementSize)
            manager.Alloc(nativeint count)

        member x.Free(b : Block<InstanceBuffer>) = 
            if not b.IsFree then
                addMem (int64 -b.Size * elementSize)
                manager.Free b

        member x.Dispose() = 
            addMem -used
            manager.Dispose()

        interface IDisposable with 
            member x.Dispose() = x.Dispose()

    type IndexManager(ctx : Context, chunkSize : int, usedMemory : ref<int64>, totalMemory : ref<int64>) =

        let mem : Memory<Buffer> =
            let malloc (size : nativeint) =
                let res = ctx.CreateBuffer(int size)
                Interlocked.Add(&totalMemory.contents, int64 res.SizeInBytes) |> ignore
                res

            let mfree (ptr : Buffer) (size : nativeint) =
                Interlocked.Add(&totalMemory.contents, -int64 ptr.SizeInBytes) |> ignore
                ctx.Delete ptr

            {
                malloc = malloc
                mfree = mfree
                mcopy = fun _ _ _ _ _ -> failwith "cannot copy"
                mrealloc = fun _ _ _ -> failwith "cannot realloc"
            }
            
        let manager = new ChunkedMemoryManager<Buffer>(mem, nativeint (sizeof<int> * chunkSize))
        
        let mutable used = 0L

        let addMem (v : int64) =
            Interlocked.Add(&usedMemory.contents, v) |> ignore
            Interlocked.Add(&used, v) |> ignore
            
        member x.Alloc(t : Type, count : int) = 
            let size = nativeint (Marshal.SizeOf t) * nativeint count
            addMem (int64 size)
            manager.Alloc(size)

        member x.Free(b : Block<Buffer>) = 
            if not b.IsFree then
                addMem (int64 -b.Size)
                manager.Free b

        member x.Dispose() = 
            addMem -used
            manager.Dispose()

        interface IDisposable with 
            member x.Dispose() = x.Dispose()


    type IndirectBuffer(ctx : Context, renderBounds : nativeptr<int>, signature : IFramebufferSignature, bounds : bool, active : nativeptr<int>, modelViewProjs : nativeptr<int>, indexed : bool, initialCapacity : int, usedMemory : ref<int64>, totalMemory : ref<int64>) =
        static let es = sizeof<DrawCallInfo>
        static let bs = sizeof<CullingShader.CullingInfo>

        static let ceilDiv (a : int) (b : int) =
            if a % b = 0 then a / b
            else 1 + a / b

        static let cullingCache = System.Collections.Concurrent.ConcurrentDictionary<Context, ComputeShader>()
        static let boundCache = System.Collections.Concurrent.ConcurrentDictionary<Context, Program>()
        
        let initialCapacity = Fun.NextPowerOfTwo initialCapacity
        let adjust (call : DrawCallInfo) =
            if indexed then
                let mutable c = call
                Fun.Swap(&c.BaseVertex, &c.FirstInstance)
                c
            else
                let mutable c = call
                c

        let drawIndices = Dict<DrawCallInfo, int>()
        let mutable capacity = initialCapacity
        let mutable mem : nativeptr<DrawCallInfo> = NativePtr.alloc capacity
        let mutable bmem : nativeptr<CullingShader.CullingInfo> = if bounds then NativePtr.alloc capacity else NativePtr.zero


        let mutable buffer = ctx.CreateBuffer (es * capacity)
        let mutable bbuffer = if bounds then ctx.CreateBuffer(bs * capacity) else new Buffer(ctx, 0n, 0)

        let ub = ctx.CreateBuffer(128)

        let mutable dirty = RangeSet.empty
        let mutable count = 0

        let bufferHandles = NativePtr.allocArray [| V3i(buffer.Handle, bbuffer.Handle, count) |]
        let indirectHandle = NativePtr.allocArray [| V2i(buffer.Handle, 0) |]
        let computeSize = NativePtr.allocArray [| V3i.Zero |]

        let updatePointers() =
            NativePtr.write bufferHandles (V3i(buffer.Handle, bbuffer.Handle, count))
            NativePtr.write indirectHandle (V2i(buffer.Handle, count))
            NativePtr.write computeSize (V3i(ceilDiv count 64, 1, 1))

        let oldProgram = NativePtr.allocArray [| 0 |]
        let oldUB = NativePtr.allocArray [| 0 |]
        let oldUBOffset = NativePtr.allocArray [| 0n |]
        let oldUBSize = NativePtr.allocArray [| 0n |]

        do let es = if bounds then es + bs else es
           Interlocked.Add(&totalMemory.contents, int64 (es * capacity)) |> ignore

        let culling =
            if bounds then 
                cullingCache.GetOrAdd(ctx, fun ctx ->
                    let cs = ComputeShader.ofFunction (V3i(1024, 1024, 1024)) CullingShader.culling
                    let shader = ctx.CompileKernel cs
                    shader 
                )
            else
                Unchecked.defaultof<ComputeShader>

        let boxShader =
            if bounds then
                boundCache.GetOrAdd(ctx, fun ctx ->
                    let effect =
                        FShade.Effect.compose [
                            Effect.ofFunction CullingShader.renderBounds
                            Effect.ofFunction (DefaultSurfaces.constantColor C4f.Red)
                        ]

                    let shader =
                        lazy (
                            let cfg = signature.EffectConfig(Range1d(-1.0, 1.0), false)
                            effect
                            |> Effect.toModule cfg
                            |> ModuleCompiler.compileGLSL430
                        )

                    match ctx.TryCompileProgram(effect.Id, signature, shader) with
                    | Success v -> v
                    | Error e -> failwith e
                )
            else
                Unchecked.defaultof<Program>



        let infoSlot = culling.Buffers |> List.pick (fun (a,b,c) -> if b = "infos" then Some a else None)
        let boundSlot = culling.Buffers |> List.pick (fun (a,b,c) -> if b = "bounds" then Some a else None)
        let activeSlot = culling.Buffers |> List.pick (fun (a,b,c) -> if b = "isActive" then Some a else None)
        let viewProjSlot = culling.Buffers |> List.pick (fun (a,b,c) -> if b = "viewProjs" then Some a else None)
        let uniformBlock = culling.UniformBlocks |> List.head
        let countField = uniformBlock.ubFields |> List.find (fun f -> f.ufName = "cs_count")
             
        let boxBoundSlot = boxShader.InterfaceNew.storageBuffers |> Seq.pick (fun (KeyValue(a,b)) -> if a = "Bounds" then Some b.ssbBinding else None)
        let boxViewProjSlot = boxShader.InterfaceNew.storageBuffers |> Seq.pick (fun (KeyValue(a,b)) -> if a = "ViewProjs" then Some b.ssbBinding else None)

        let boxMode = NativePtr.allocArray [| GLBeginMode(int BeginMode.Lines, 2) |]
        
        let pCall =
            NativePtr.allocArray [|
                DrawCallInfo(
                    FaceVertexCount = 24,
                    InstanceCount = 0
                )
            |]

        let boxDraw = 
            NativePtr.allocArray [| new DrawCallInfoList(1, pCall) |]

        let resize (newCount : int) =
            let newCapacity = max initialCapacity (Fun.NextPowerOfTwo newCount)
            if newCapacity <> capacity then
                let ess = if bounds then es + bs else es
                Interlocked.Add(&totalMemory.contents, int64 (ess * (newCapacity - capacity))) |> ignore
                let ob = buffer
                let obb = bbuffer
                let om = mem
                let obm = bmem
                let nb = ctx.CreateBuffer (es * newCapacity)
                let nbb = if bounds then ctx.CreateBuffer (bs * newCapacity) else new Buffer(ctx, 0n, 0)
                let nm = NativePtr.alloc newCapacity
                let nbm = if bounds then NativePtr.alloc newCapacity else NativePtr.zero

                Marshal.Copy(NativePtr.toNativeInt om, NativePtr.toNativeInt nm, nativeint count * nativeint es)
                if bounds then Marshal.Copy(NativePtr.toNativeInt obm, NativePtr.toNativeInt nbm, nativeint count * nativeint bs)

                mem <- nm
                bmem <- nbm
                buffer <- nb
                bbuffer <- nbb
                capacity <- newCapacity
                dirty <- RangeSet.ofList [Range1i(0, count - 1)]
                
                NativePtr.free om
                ctx.Delete ob
                if bounds then 
                    NativePtr.free obm
                    ctx.Delete obb
        
        member x.Count = count

        member x.Add(call : DrawCallInfo, box : Box3d, cellBounds : Box3d, rootId : int) =
            if drawIndices.ContainsKey call then
                false
            else
                if count < capacity then
                    let id = count
                    drawIndices.[call] <- id
                    NativePtr.set mem id (adjust call)
                    if bounds then
                        let bounds =
                            CullingShader.CullingInfo(
                                Min = V4f(V3f box.Min, float32 call.InstanceCount),
                                Max = V4f(V3f box.Max, float32 rootId),
                                CellMin = V4f(V3f cellBounds.Min, 0.0f),
                                CellMax = V4f(V3f cellBounds.Max, 0.0f)
                            )
                        NativePtr.set bmem id bounds
                    count <- count + 1
                    let ess = if bounds then es + bs else es
                    Interlocked.Add(&usedMemory.contents, int64 ess) |> ignore
                    dirty <- RangeSet.insert (Range1i(id,id)) dirty
                    
                    updatePointers()
                    true
                else
                    resize (count + 1)
                    x.Add(call, box, cellBounds, rootId)
                    
        member x.Remove(call : DrawCallInfo) =
            match drawIndices.TryRemove call with
                | (true, oid) ->
                    let last = count - 1
                    count <- count - 1
                    let ess = if bounds then es + bs else es
                    Interlocked.Add(&usedMemory.contents, int64 -ess) |> ignore

                    if oid <> last then
                        let lc = NativePtr.get mem last
                        drawIndices.[lc] <- oid
                        NativePtr.set mem oid lc
                        NativePtr.set mem last Unchecked.defaultof<DrawCallInfo>
                        if bounds then
                            let lb = NativePtr.get bmem last
                            NativePtr.set bmem oid lb
                        dirty <- RangeSet.insert (Range1i(oid,oid)) dirty
                        
                    resize last
                    updatePointers()

                    true
                | _ ->
                    false
        
        member x.Flush() =
            use __ = ctx.ResourceLock
            let toUpload = dirty
            dirty <- RangeSet.empty

            if not (Seq.isEmpty toUpload) then
                if bounds then
                    let ptr = ctx.MapBufferRange(buffer, 0n, nativeint (count * es), BufferAccessMask.MapWriteBit ||| BufferAccessMask.MapFlushExplicitBit)
                    let bptr = ctx.MapBufferRange(bbuffer, 0n, nativeint (count * bs), BufferAccessMask.MapWriteBit ||| BufferAccessMask.MapFlushExplicitBit)
                    for r in toUpload do
                        let o = r.Min * es |> nativeint
                        let s = (1 + r.Max - r.Min) * es |> nativeint
                        Marshal.Copy(NativePtr.toNativeInt mem + o, ptr + o, s)
                        GL.FlushMappedNamedBufferRange(buffer.Handle, o, s)
                        
                        let o = r.Min * bs |> nativeint
                        let s = (1 + r.Max - r.Min) * bs |> nativeint
                        Marshal.Copy(NativePtr.toNativeInt bmem + o, bptr + o, s)
                        GL.FlushMappedNamedBufferRange(bbuffer.Handle, o, s)

                    ctx.UnmapBuffer(buffer)
                    ctx.UnmapBuffer(bbuffer)
                else 
                    let size = count * es
                    let ptr = ctx.MapBufferRange(buffer, 0n, nativeint size, BufferAccessMask.MapWriteBit ||| BufferAccessMask.MapFlushExplicitBit)
                    for r in toUpload do
                        let o = r.Min * es |> nativeint
                        let s = (1 + r.Max - r.Min) * es |> nativeint
                        Marshal.Copy(NativePtr.toNativeInt mem + o, ptr + o, s)
                        GL.FlushMappedNamedBufferRange(buffer.Handle, o, s)
                    ctx.UnmapBuffer(buffer)


        member x.Buffer =
            Aardvark.Rendering.GL.IndirectBufferExtensions.IndirectBuffer(buffer, count, sizeof<DrawCallInfo>, false)

        member x.BoundsBuffer =
            bbuffer

       

        member x.CompileRender(s : ICommandStream, before : ICommandStream -> unit, mvp : nativeptr<M44f>, indexType : Option<_>, runtimeStats : nativeptr<_>, isActive : nativeptr<_>, mode : nativeptr<_>) =
            if bounds then
                //s.NamedBufferSubData(ub.Handle, nativeint viewProjField.ufOffset, 64n, NativePtr.toNativeInt mvp)
                s.NamedBufferSubData(ub.Handle, nativeint countField.ufOffset, 4n, NativePtr.toNativeInt bufferHandles + 8n)

                s.Get(GetPName.CurrentProgram, oldProgram)
                s.Get(GetIndexedPName.UniformBufferBinding, uniformBlock.ubBinding, oldUB)
                s.Get(GetIndexedPName.UniformBufferStart, uniformBlock.ubBinding, oldUBOffset)
                s.Get(GetIndexedPName.UniformBufferSize, uniformBlock.ubBinding, oldUBSize)

                s.UseProgram(culling.Handle)
                s.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, infoSlot, NativePtr.ofNativeInt (NativePtr.toNativeInt bufferHandles + 0n))
                s.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, boundSlot, NativePtr.ofNativeInt (NativePtr.toNativeInt bufferHandles + 4n))
                s.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, activeSlot, active)
                s.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, viewProjSlot, modelViewProjs)
                s.BindBufferBase(BufferRangeTarget.UniformBuffer, uniformBlock.ubBinding, ub.Handle)
                s.DispatchCompute computeSize
                
                s.Conditional(renderBounds, fun s ->
                    let pCnt : nativeptr<int> = NativePtr.ofNativeInt (NativePtr.toNativeInt bufferHandles + 8n)
                    let pInstanceCnt : nativeptr<int> = NativePtr.ofNativeInt (NativePtr.toNativeInt pCall + 4n)
                    s.Copy(pCnt, pInstanceCnt)
                    s.UseProgram(boxShader.Handle)
                    s.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, boxBoundSlot, NativePtr.ofNativeInt (NativePtr.toNativeInt bufferHandles + 4n))
                    s.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, boxViewProjSlot, modelViewProjs)
                    s.DrawArrays(runtimeStats, isActive, boxMode, boxDraw)
                )

                s.UseProgram(oldProgram)
                s.BindBufferRange(BufferRangeTarget.UniformBuffer, uniformBlock.ubBinding, oldUB, oldUBOffset, oldUBSize)
                s.MemoryBarrier(MemoryBarrierFlags.CommandBarrierBit)

            let h = NativePtr.read indirectHandle
            if h.Y > 0 then
                before(s)
                match indexType with
                    | Some indexType ->
                        s.DrawElementsIndirect(runtimeStats, isActive, mode, int indexType, indirectHandle)
                    | _ -> 
                        s.DrawArraysIndirect(runtimeStats, isActive, mode, indirectHandle)
            else
                Log.warn "empty indirect call"

        member x.Dispose() =
            let ess = if bounds then es + bs else es
            Interlocked.Add(&usedMemory.contents, int64 (-ess * count)) |> ignore
            Interlocked.Add(&totalMemory.contents, int64 (-ess * capacity)) |> ignore
            NativePtr.free mem
            ctx.Delete buffer
            if bounds then
                NativePtr.free bmem
                ctx.Delete bbuffer
            capacity <- 0
            mem <- NativePtr.zero
            buffer <- new Buffer(ctx, 0n, 0)
            dirty <- RangeSet.empty
            count <- 0
            NativePtr.free indirectHandle
            NativePtr.free computeSize
            NativePtr.free boxMode
            NativePtr.free pCall
            NativePtr.free boxDraw

            drawIndices.Clear()

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    type PoolSlot(ctx : Context, signature : GeometrySignature, ub : Block<InstanceBuffer>, vb : Block<VertexBuffer>, ib : Option<Block<Buffer>>) = 
        let fvc =
            match signature.indexType, ib with
                | Some it, Some ib -> int ib.Size / Marshal.SizeOf it
                | _ -> int vb.Size
        
        static let getIndexType =
            LookupTable.lookupTable [
                typeof<uint8>, DrawElementsType.UnsignedByte
                typeof<int8>, DrawElementsType.UnsignedByte
                typeof<uint16>, DrawElementsType.UnsignedShort
                typeof<int16>, DrawElementsType.UnsignedShort
                typeof<uint32>, DrawElementsType.UnsignedInt
                typeof<int32>, DrawElementsType.UnsignedInt
            ]

        let indexType = signature.indexType |> Option.map getIndexType 

        member x.Memory = 
            Mem (
                int64 ub.Size * ub.Memory.Value.ElementSize +
                int64 vb.Size * vb.Memory.Value.ElementSize +
                (match ib with | Some ib -> int64 ib.Size | _ -> 0L)
            )

        member x.IndexType = indexType
        member x.Signature = signature
        member x.VertexBuffer = vb
        member x.InstanceBuffer = ub
        member x.IndexBuffer = ib

        member x.IsDisposed = vb.IsFree

        member x.Upload(g : IndexedGeometry, uniforms : MapExt<string, Array>) =
            let instanceValues =
                signature.uniformTypes |> MapExt.choose (fun name (glslType, typ) ->
                    match MapExt.tryFind name uniforms with
                        | Some att -> Some att
                        | None -> 
                            match g.SingleAttributes.TryGetValue(Symbol.Create name) with
                                | (true, v) -> 
                                    let arr = Array.CreateInstance(typ, 1) //Some ([| v |] :> Array)
                                    arr.SetValue(v, 0)
                                    Some arr
                                | _ -> 
                                    None
                )
            let vertexArrays =
                signature.attributeTypes |> MapExt.choose (fun name _ ->
                    match g.IndexedAttributes.TryGetValue(Symbol.Create name) with
                        | (true, v) -> Some v
                        | _ -> None
                )

            match ib with
                | Some ib -> 
                    let gc = GCHandle.Alloc(g.IndexArray, GCHandleType.Pinned)
                    try 
                        let ptr = ctx.MapBufferRange(ib.Memory.Value, ib.Offset, ib.Size, BufferAccessMask.MapWriteBit ||| BufferAccessMask.MapInvalidateRangeBit ||| BufferAccessMask.MapUnsynchronizedBit)
                        Marshal.Copy(gc.AddrOfPinnedObject(), ptr,ib.Size )
                        ctx.UnmapBuffer(ib.Memory.Value)
                    finally
                        gc.Free()
                | None ->
                    ()

            ub.Memory.Value.Upload(int ub.Offset, int ub.Size, instanceValues)
            vb.Memory.Value.Write(int vb.Offset, vertexArrays)

        member x.Upload(g : IndexedGeometry) = x.Upload(g, MapExt.empty)

        member x.Mode = signature.mode

        member x.DrawCallInfo =
            match ib with
                | Some ib ->
                    DrawCallInfo(
                        FaceVertexCount = fvc,
                        FirstIndex = int ib.Offset / Marshal.SizeOf(signature.indexType.Value),
                        InstanceCount = int ub.Size,
                        FirstInstance = int ub.Offset,
                        BaseVertex = int vb.Offset
                    )

                | None -> 
                    DrawCallInfo(
                        FaceVertexCount = fvc,
                        FirstIndex = int vb.Offset,
                        InstanceCount = int ub.Size,
                        FirstInstance = int ub.Offset
                    )

    type GeometryPool private(ctx : Context) =
        static let instanceChunkSize = 1 <<< 20
        static let vertexChunkSize = 1 <<< 20
        static let pools = System.Collections.Concurrent.ConcurrentDictionary<Context, GeometryPool>()

        let usedMemory = ref 0L
        let totalMemory = ref 0L
        let instanceManagers = System.Collections.Concurrent.ConcurrentDictionary<InstanceSignature, InstanceManager>()
        let vertexManagers = System.Collections.Concurrent.ConcurrentDictionary<VertexSignature, VertexManager>()

        let getVertexManager (signature : VertexSignature) = vertexManagers.GetOrAdd(signature, fun signature -> new VertexManager(ctx, signature, vertexChunkSize, usedMemory, totalMemory))
        let getInstanceManager (signature : InstanceSignature) = instanceManagers.GetOrAdd(signature, fun signature -> new InstanceManager(ctx, signature, instanceChunkSize, usedMemory, totalMemory))
        let indexManager = new IndexManager(ctx, vertexChunkSize, usedMemory, totalMemory)

        static member Get(ctx : Context) =
            pools.GetOrAdd(ctx, fun ctx ->
                new GeometryPool(ctx)
            )      
            
        member x.UsedMemory = Mem !usedMemory
        member x.TotalMemory = Mem !totalMemory

        member x.Alloc(signature : GeometrySignature, instanceCount : int, indexCount : int, vertexCount : int) =
            let vm = getVertexManager signature.attributeTypes
            let im = getInstanceManager signature.uniformTypes

            let ub = im.Alloc(instanceCount)
            let vb = vm.Alloc(vertexCount)

            let ib = 
                match signature.indexType with
                    | Some t -> indexManager.Alloc(t, indexCount) |> Some
                    | None -> None

            let slot = PoolSlot(ctx, signature, ub, vb, ib)
            slot

        member x.Alloc(signature : GLSLProgramInterface, geometry : IndexedGeometry, uniforms : MapExt<string, Array>) =
            let signature = GeometrySignature.ofGeometry signature uniforms geometry

            let instanceCount =
                if MapExt.isEmpty uniforms then
                    1
                else
                    uniforms |> MapExt.toSeq |> Seq.map (fun (_,arr) -> arr.Length) |> Seq.min

            let vertexCount, indexCount = 
                if isNull geometry.IndexArray then
                    geometry.FaceVertexCount, 0
                else
                    let vc = geometry.IndexedAttributes.Values |> Seq.map (fun v -> v.Length) |> Seq.min
                    let fvc = geometry.IndexArray.Length
                    vc, fvc

            let slot = x.Alloc(signature, instanceCount, indexCount, vertexCount)
            slot.Upload(geometry, uniforms)
            slot
            
        member x.Alloc(signature : GLSLProgramInterface, geometry : IndexedGeometry) =
            x.Alloc(signature, geometry, MapExt.empty)
           

        member x.Free(slot : PoolSlot) =
            //Log.warn "free %A" slot.Memory
            let signature = slot.Signature
            let vm = getVertexManager signature.attributeTypes
            let im = getInstanceManager signature.uniformTypes
            im.Free slot.InstanceBuffer
            vm.Free slot.VertexBuffer
            match slot.IndexBuffer with
                | Some ib -> indexManager.Free ib
                | None -> ()

    type DrawPool(ctx : Context, bounds : bool, renderBounds : nativeptr<int>, activeBuffer : nativeptr<int>, modelViewProjs : nativeptr<int>, state : PreparedPipelineState, pass : RenderPass) as this =
        inherit PreparedCommand(ctx, pass)

        static let initialIndirectSize = 256

        static let getKey (slot : PoolSlot) =
            slot.Mode,
            slot.InstanceBuffer.Memory.Value, 
            slot.VertexBuffer.Memory.Value,
            slot.IndexBuffer |> Option.map (fun b -> slot.IndexType.Value, b.Memory.Value)

        static let beginMode =
            LookupTable.lookupTable [
                IndexedGeometryMode.PointList, BeginMode.Points
                IndexedGeometryMode.LineList, BeginMode.Lines
                IndexedGeometryMode.LineStrip, BeginMode.LineStrip
                IndexedGeometryMode.LineAdjacencyList, BeginMode.LinesAdjacency
                IndexedGeometryMode.TriangleList, BeginMode.Triangles
                IndexedGeometryMode.TriangleStrip, BeginMode.TriangleStrip
                IndexedGeometryMode.TriangleAdjacencyList, BeginMode.TrianglesAdjacency
            ]

        let isActive = NativePtr.allocArray [| 1 |]
        let runtimeStats : nativeptr<V2i> = NativePtr.alloc 1
        let contextHandle : nativeptr<nativeint> = NativePtr.alloc 1

        let pProgramInterface = state.pProgramInterface

        let mvpResource=
            let s = state

            let viewProj =
                match Uniforms.tryGetDerivedUniform "ModelViewProjTrafo" s.pUniformProvider with
                | Some (:? IMod<Trafo3d> as mvp) -> mvp
                | _ -> 
                    match s.pUniformProvider.TryGetUniform(Ag.emptyScope, Symbol.Create "ModelViewProjTrafo") with
                    | Some (:? IMod<Trafo3d> as mvp) -> mvp
                    | _ -> Mod.constant Trafo3d.Identity

            let res = 
                { new Resource<Trafo3d, M44f>(ResourceKind.UniformLocation) with
                    member x.Create(t, rt, o) = viewProj.GetValue(t)
                    member x.Destroy _ = ()
                    member x.View t = t.Forward |> M44f.op_Explicit
                    member x.GetInfo _ = ResourceInfo.Zero
                }

            res.AddRef()
            res.Update(AdaptiveToken.Top, RenderToken.Empty)

            res :> IResource<_,_>


        let query : nativeptr<int> = NativePtr.allocArray [| 0 |]
        let startTime : nativeptr<uint64> = NativePtr.allocArray [| 0UL |]
        let endTime : nativeptr<uint64> = NativePtr.allocArray [| 0UL |]
        



        let usedMemory = ref 0L
        let totalMemory = ref 0L
        let avgRenderTime = RunningMean(10)

        let compile (indexType : Option<DrawElementsType>, mode : nativeptr<GLBeginMode>, a : VertexInputBindingHandle, ib : IndirectBuffer) (s : ICommandStream) =
            s.BindVertexAttributes(contextHandle, a)
            ib.CompileRender(s, this.BeforeRender, mvpResource.Pointer, indexType, runtimeStats, isActive, mode)

        let indirects = Dict<_, IndirectBuffer>()
        let isOutdated = NativePtr.allocArray [| 1 |]
        let updateFun = Marshal.PinDelegate(new System.Action(this.Update))
        let mutable oldCalls : list<Option<DrawElementsType> * nativeptr<GLBeginMode> * VertexInputBindingHandle * IndirectBuffer> = []
        let program = new ChangeableNativeProgram<_>(fun a s -> compile a (AssemblerCommandStream s))
        let puller = AdaptiveObject()
        let sub = puller.AddMarkingCallback (fun () -> NativePtr.write isOutdated 1)
        let tasks = System.Collections.Generic.HashSet<IRenderTask>()

        let mark() = transact (fun () -> puller.MarkOutdated())
        

        let getIndirectBuffer(slot : PoolSlot) =
            let key = getKey slot
            indirects.GetOrCreate(key, fun _ ->
                new IndirectBuffer(ctx, renderBounds, state.pFramebufferSignature, bounds, activeBuffer, modelViewProjs, Option.isSome slot.IndexType, initialIndirectSize, usedMemory, totalMemory)
            )

        let tryGetIndirectBuffer(slot : PoolSlot) =
            let key = getKey slot
            match indirects.TryGetValue key with
                | (true, ib) -> Some ib
                | _ -> None
                
                
        member x.Add(ref : PoolSlot, bounds : Box3d, cellBounds : Box3d, rootId : int) =
            let ib = getIndirectBuffer ref
            if ib.Add(ref.DrawCallInfo, bounds, cellBounds, rootId) then
                mark()
                true
            else
                false

        member x.Add(ref : PoolSlot, rootId : int) =
            let ib = getIndirectBuffer ref
            if ib.Add(ref.DrawCallInfo, Unchecked.defaultof<Box3d>, Unchecked.defaultof<Box3d>, rootId) then
                mark()
                true
            else
                false

        member x.Remove(ref : PoolSlot) =
            match tryGetIndirectBuffer ref with
                | Some ib -> 
                    if ib.Remove(ref.DrawCallInfo) then
                        if ib.Count = 0 then
                            let key = getKey ref
                            indirects.Remove(key) |> ignore
                            ib.Dispose()
                            
                        mark()
                        true
                    else
                        false
                | None ->
                    false
                    
        member x.UsedMemory = Mem !totalMemory
        member x.TotalMemory = Mem !totalMemory

        abstract member Evaluate : AdaptiveToken * GLSLProgramInterface -> unit
        default x.Evaluate(_,_) = ()

        abstract member AfterUpdate : unit -> unit
        default x.AfterUpdate () = ()

        abstract member BeforeRender : ICommandStream -> unit
        default x.BeforeRender(_) = ()

        member x.AverageRenderTime = MicroTime(int64 (1000000.0 * avgRenderTime.Average))

        member x.Update() =
            puller.EvaluateAlways AdaptiveToken.Top (fun token ->   

                puller.OutOfDate <- true
                
                x.Evaluate(token, pProgramInterface)
                
                let rawResult = NativePtr.read endTime - NativePtr.read startTime
                let ms = float rawResult / 1000000.0
                avgRenderTime.Add ms



                let calls = 
                    Dict.toList indirects |> List.map (fun ((mode, ib, vb, typeAndIndex), db) ->
                        let indexType = typeAndIndex |> Option.map fst
                        let index = typeAndIndex |> Option.map snd
                        db.Flush()

                        let attributes = 
                            pProgramInterface.inputs |> List.map (fun param ->
                                match MapExt.tryFind param.paramSemantic ib.Buffers with
                                    | Some ib -> 
                                        param.paramLocation, {
                                            Type = GLSLType.toType param.paramType
                                            Content = Left ib
                                            Frequency = AttributeFrequency.PerInstances 1
                                            Normalized = false
                                            Stride = GLSLType.sizeof param.paramType
                                            Offset = 0
                                        }

                                    | None ->   
                                        match MapExt.tryFind param.paramSemantic vb.Buffers with
                                        | Some (vb, typ) ->
                                            let norm = if typ = typeof<C4b> then true else false
                                            param.paramLocation, {
                                                Type = typ
                                                Content = Left vb
                                                Frequency = AttributeFrequency.PerVertex
                                                Normalized = norm
                                                Stride = Marshal.SizeOf typ
                                                Offset = 0
                                            }

                                        | None ->
                                            param.paramLocation, {
                                                Type = GLSLType.toType param.paramType
                                                Content = Right V4f.Zero
                                                Frequency = AttributeFrequency.PerVertex
                                                Normalized = false
                                                Stride = GLSLType.sizeof param.paramType
                                                Offset = 0
                                            }
                            )

                        let bufferBinding = ctx.CreateVertexInputBinding(index, attributes)
                
                        let beginMode = 
                            let bm = beginMode mode
                            NativePtr.allocArray [| GLBeginMode(int bm, 1) |]
                            

                        indexType, beginMode, bufferBinding, db
                    )

                program.Clear()
                for a in calls do program.Add a |> ignore
            
                oldCalls |> List.iter (fun (_,beginMode,bufferBinding,indirect) -> 
                    NativePtr.free beginMode; ctx.Delete bufferBinding
                )
                oldCalls <- calls

                NativePtr.write isOutdated 0

                for t in tasks do
                    puller.Outputs.Add t |> ignore

                x.AfterUpdate()
                

            )

        override x.Compile(info : CompilerInfo, stream : ICommandStream, last : Option<PreparedCommand>) =
            lock puller (fun () ->
                if tasks.Add info.task then
                    assert (info.task.OutOfDate)
                    puller.AddOutput(info.task) |> ignore
            )
            
            let mvpRes = mvpResource
            let lastState = last |> Option.bind (fun l -> l.ExitState)

            stream.ConditionalCall(isOutdated, updateFun.Pointer)
            
            stream.Copy(info.runtimeStats, runtimeStats)
            stream.Copy(info.contextHandle, contextHandle)
            
            stream.SetPipelineState(info, state, lastState)
            
            stream.QueryTimestamp(query, startTime)
            stream.CallIndirect(program.EntryPointer)
            stream.QueryTimestamp(query, endTime)

            stream.Copy(runtimeStats, info.runtimeStats)

        override x.Release() =
            state.Dispose()
            for ib in indirects.Values do ib.Dispose()
            indirects.Clear()
            updateFun.Dispose()
            NativePtr.free isActive
            NativePtr.free isOutdated
            NativePtr.free runtimeStats
            NativePtr.free contextHandle

            NativePtr.free startTime
            NativePtr.free endTime
            NativePtr.free query

            program.Dispose()
            oldCalls <- []
            

        override x.GetResources() = 
            Seq.append (Seq.singleton (mvpResource :> IResource)) state.Resources

        override x.EntryState = Some state
        override x.ExitState = Some state



    [<StructuredFormatDisplay("AsString")>]
    type GeometryInstance =
        {
            signature       : GeometrySignature
            instanceCount   : int
            indexCount      : int
            vertexCount     : int
            geometry        : IndexedGeometry
            uniforms        : MapExt<string, Array>
        }

        override x.ToString() =
            if x.instanceCount > 1 then
                if x.indexCount > 0 then
                    sprintf "gi(%d, %d, %d)" x.instanceCount x.indexCount x.vertexCount
                else
                    sprintf "gi(%d, %d)" x.instanceCount x.vertexCount
            else
                if x.indexCount > 0 then
                    sprintf "g(%d, %d)" x.indexCount x.vertexCount
                else
                    sprintf "g(%d)" x.vertexCount
              
        member private x.AsString = x.ToString()

    module GeometryInstance =

        let inline signature (g : GeometryInstance) = g.signature
        let inline instanceCount (g : GeometryInstance) = g.instanceCount
        let inline indexCount (g : GeometryInstance) = g.indexCount
        let inline vertexCount (g : GeometryInstance) = g.vertexCount
        let inline geometry (g : GeometryInstance) = g.geometry
        let inline uniforms (g : GeometryInstance) = g.uniforms

        let ofGeometry (iface : GLSLProgramInterface) (g : IndexedGeometry) (u : MapExt<string, Array>) =
            let instanceCount =
                if MapExt.isEmpty u then 1
                else u |> MapExt.toSeq |> Seq.map (fun (_,a) -> a.Length) |> Seq.min

            let indexCount, vertexCount =
                if g.IsIndexed then
                    let i = g.IndexArray.Length
                    let v = 
                        if g.IndexedAttributes.Count = 0 then 0
                        else g.IndexedAttributes.Values |> Seq.map (fun a -> a.Length) |> Seq.min
                    i, v
                else
                    0, g.FaceVertexCount

            {
                signature = GeometrySignature.ofGeometry iface u g
                instanceCount = instanceCount
                indexCount = indexCount
                vertexCount = vertexCount
                geometry = g
                uniforms = u
            }

        let load (iface : GLSLProgramInterface) (load : Set<string> -> IndexedGeometry *  MapExt<string, Array>) =
            let wanted = iface.inputs |> List.map (fun p -> p.paramSemantic) |> Set.ofList
            let (g,u) = load wanted

            ofGeometry iface g u

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

    module internal TreeHelpers =
        let private cmp = Func<struct (float * _ * _), struct (float * _ * _), int>(fun (struct(a,_,_)) (struct(b,_,_)) -> compare a b)

        let inline private enqueue (q : float) (t : ILodTreeNode) (view : ILodTreeNode -> Trafo3d) (proj : Trafo3d) (queue : List<struct (float * int64 * ILodTreeNode)>) =
            let view = view t.Root
            if t.ShouldSplit(q, view, proj) then
                let q = t.SplitQuality(view, proj)
                let childSize = t.Children |> Seq.sumBy (fun c -> int64 c.DataSize)
                queue.HeapEnqueue(cmp, struct (q, childSize, t))

        let getMaxQuality (maxSize : int64) (ts : seq<ILodTreeNode>) (view : ILodTreeNode -> Trafo3d) (proj : Trafo3d) =

            let queue = List<struct (float * int64 * ILodTreeNode)>(1 <<< 20)
            let mutable size = 0L
            let mutable quality = 1.0
            let mutable cnt = 0
            for t in ts do 
                size <- size + int64 t.DataSize
                enqueue 1.0 t view proj queue

            let inline s (struct (a,b,c)) = b

            while queue.Count > 0 && size + s queue.[0] <= maxSize do
                let struct (q,s,e) = queue.HeapDequeue(cmp)
                quality <- q
                size <- size + s
                for c in e.Children do
                    enqueue 1.0 c view proj queue

            if queue.Count = 0 then
                1.0, size
            else
                let struct (qn,_,_) = queue.HeapDequeue(cmp)
                0.5 * (qn + quality), size
   
    type TaskTreeNode<'a> internal(state : TaskTreeState, mapping : CancellationToken -> ILodTreeNode -> Task<'a>, rootId : int, original : ILodTreeNode) =
        static let neverTask = TaskCompletionSource<'a>().Task
        static let cmp = Func<float * _, float * _, int>(fun (a,_) (b,_) -> compare a b)
        do original.Acquire()

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
            Interlocked.Add(&state.dataSize.contents, -int64 original.DataSize) |> ignore
            state.RemoveNode()
            original.Release()
            try cancel |> Option.iter (fun c -> c.Cancel()); cancel <- None
            with _ -> ()
            children |> List.iter (fun c -> c.Destroy())
            children <- []
            cancel <- None
            task <- neverTask
            



        member x.BuildQueue(node : ILodTreeNode, depth : int, quality : float, collapseIfNotSplit : bool, view : Trafo3d, proj : Trafo3d, queue : List<float * SetOperation<TaskTreeNode<'a>>>) =
            if node <> original then failwith "[Tree] inconsistent path"

            match children with
            | [] ->
                if node.ShouldSplit(quality, view, proj) then
                    let qs = node.SplitQuality(view, proj)
                    queue.HeapEnqueue(cmp, (qs, Add x))
            | o ->
                let collapse =
                    if collapseIfNotSplit then not (node.ShouldSplit(quality, view, proj))
                    else node.ShouldCollapse(quality, view, proj)

                if collapse then
                    let qc = 
                        if collapseIfNotSplit then node.SplitQuality(view, proj)
                        else node.CollapseQuality(view, proj)
                    queue.HeapEnqueue(cmp, (qc - quality, Rem x))

                else
                    for (o, n) in Seq.zip o node.Children do
                        o.BuildQueue(n, depth, quality, collapseIfNotSplit, view, proj, queue)




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
        member x.HasValue = task.IsCompleted && not task.IsFaulted && not task.IsCanceled
        member x.Value = task.Result

        member x.TryGetValue() =
            if task.IsCompleted && not task.IsFaulted && not task.IsCanceled then
                Some task.Result
            else
                None

        member x.HasChildren = not (List.isEmpty children)
        member x.AllChildrenHaveValues = children |> List.forall (fun c -> c.HasValue)

    type TaskTree<'a>(mapping : CancellationToken -> ILodTreeNode -> Task<'a>, rootId : int) =
        static let cmp = Func<float * _, float * _, int>(fun (a,_) (b,_) -> compare a b)
        static let cmpNode = Func<float * ILodTreeNode, float * ILodTreeNode, int>(fun (a,_) (b,_) -> compare a b)

        let mutable root : Option<TaskTreeNode<'a>> = None

        
        //member x.RunningTasks = state.runningTasks

        //member x.Version = state.version
        member x.RootId = rootId
        member x.Root = root

        member internal x.BuildQueue(state : TaskTreeState, collapseIfNotSplit : bool, t : Option<ILodTreeNode>, quality : float, view : ILodTreeNode -> Trafo3d, proj : Trafo3d, queue : List<float * SetOperation<TaskTreeNode<'a>>>) =
            match root, t with
            | None, None -> 
                ()

            | Some r, None -> 
                r.Destroy(); root <- None

            | None, Some n -> 
                let r = TaskTreeNode(state, mapping, rootId, n)
                root <- Some r
                r.BuildQueue(n, 0, quality, collapseIfNotSplit, view r.Original, proj, queue)
                
            | Some r, Some t -> 
                r.BuildQueue(t, 0, quality, collapseIfNotSplit, view r.Original, proj, queue)



        static member internal ProcessQueue(state : TaskTreeState, queue : List<float * SetOperation<TaskTreeNode<'a>>>, quality : float, view : ILodTreeNode -> Trafo3d, proj : Trafo3d, maxOps : int) =
            let mutable lastQ = 0.0
            let mutable cnt = 0
            while queue.Count > 0 && (!state.runningTasks < maxOps || (queue.Count > 0 && (snd queue.[0]).Count < 0)) do
                let q, op = queue.HeapDequeue(cmp)
                match op with
                | Add(_,n) -> 
                    n.StartSplit(state.trigger)
                    for c in n.Children do
                        let r = c.Original.Root
                        let view = view r
                        if c.Original.ShouldSplit(quality, view , proj) then
                            let qs = c.Original.SplitQuality(view, proj)
                            queue.HeapEnqueue(cmp, (qs, Add c))
                    Interlocked.Increment(&state.splits.contents) |> ignore
                    lastQ <- q

                | Rem(_,n) -> 
                    n.Collapse(state.trigger)
                    Interlocked.Increment(&state.collapses.contents) |> ignore
                    lastQ <- q + quality

                cnt <- cnt + 1

            if queue.Count = 0 then 
                quality
            else 
                let qnext, _ = queue.HeapDequeue(cmp)
                if cnt = 0 then qnext
                else 0.5 * (qnext + lastQ)
                    

    type TaskTreeReader<'a>(tree : TaskTree<'a>) =
        let mutable state : Option<TreeNode<'a>> = None

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

        let rec traverse (q : ref<AtomicQueue<ILodTreeNode, 'a>>) (o : Option<TreeNode<'a>>) (n : Option<TaskTreeNode<'a>>) =
            match o, n with
                | None, None -> 
                    o

                | Some o, None ->
                    let op = kill o
                    lock q (fun () -> q := AtomicQueue.enqueue op !q)
                    None

                | None, Some n ->
                    if n.HasValue then
                        let v = n.Value

                        let mutable qc = ref AtomicQueue.empty
                        let mutable worked = true
                        let children = System.Collections.Generic.List<_>()
                        let nc = n.Children
                        use e = (nc :> seq<_>).GetEnumerator()
                        while e.MoveNext() && worked do
                            let cn = traverse qc None (Some e.Current)
                            match cn with
                            | Some cn -> 
                                children.Add cn
                            | None ->
                                worked <- false


                        if worked && not (List.isEmpty nc) then
                            let ops = 
                                AtomicQueue.toOperation !qc +
                                AtomicOperation.ofList [ n.Original, Operation.Alloc(v, false) ]
                            
                            let value = 
                                Some {
                                    original = n.Original
                                    value = v
                                    children = Seq.toList children
                                }

                            lock q (fun () -> q := AtomicQueue.enqueue ops !q)

                            value

                        else
                            let value = 
                                Some {
                                    original = n.Original
                                    value = v
                                    children = []
                                }
                            let op = AtomicOperation.ofList [ n.Original, Operation.Alloc(v, true) ]
                            lock q (fun () -> q := AtomicQueue.enqueue op !q)
                            value
                    else
                        None


                | Some o, Some n ->
                    match o.children, n.Children with
                    | [], [] ->
                        Some o
                        
                    | _, n when n |> List.exists (fun n -> not n.HasValue) ->
                        Some o
                        

                    | [], ns ->
                        let mutable worked = true
                        let childQueue = ref AtomicQueue.empty
                        let children =
                            ns |> List.map (fun c ->
                                match traverse childQueue None (Some c) with
                                | Some c ->
                                    c
                                | None ->
                                    worked <- false
                                    Unchecked.defaultof<_>
                            )
                        if worked then
                            let op = 
                                AtomicQueue.toOperation !childQueue + 
                                AtomicOperation.ofList [ o.original, Operation.Deactivate ]
                        
                            let value =
                                Some {
                                    original = o.original
                                    value = o.value
                                    children = children
                                }

                            lock q (fun () -> q := AtomicQueue.enqueue op !q)

                            value
                        else
                            Log.warn "the impossible happened (no worries)"
                            Some o

                    | os, [] ->
                        let op = 
                            AtomicOperation.ofList [ o.original, Operation.Activate ] +
                            List.sumBy kill os
                            
                        let value =
                            Some {
                                original = o.original
                                value = o.value
                                children = []
                            }

                        lock q (fun () -> q := AtomicQueue.enqueue op !q)
                        value

                    
                    | os, ns ->
                        //assert (ns |> List.forall (fun n -> n.HasValue))
                        let mutable worked = true
                        let children = 
                            List.zip os ns |> List.map (fun (o,n) ->
                                match traverse q (Some o) (Some n) with
                                | Some nn ->
                                    nn
                                | None ->
                                    worked <- false
                                    Unchecked.defaultof<_>
                            )
                            
                        if worked then
                            let value =
                                Some {
                                    original = o.original
                                    value = o.value
                                    children = children
                                }
                            
                            value
                        else
                            Log.warn "the impossible happened (no worries)"
                            Some o

        member x.Update(q : ref<AtomicQueue<ILodTreeNode, 'a>>) =
            let newState = traverse q state tree.Root
            state <- newState
            
        member x.Destroy(q : ref<AtomicQueue<ILodTreeNode, 'a>>) =
            match state with
            | Some s -> 
                q := AtomicQueue.enqueue (kill s) !q
                state <- None
            | None ->
                ()


    [<RequireQualifiedAccess>]
    type NodeOperation =
        | Split
        | Collapse of children : list<ILodTreeNode>
        | Add
        | Remove of children : list<ILodTreeNode>

    type Delta =
        {
            deltas          : hmap<ILodTreeNode, int * NodeOperation>
            splitCount      : int
            collapseCount   : int
            allocSize       : int64
            freeSize        : int64
        }

        static member Empty =
            {
                deltas = HMap.empty; splitCount = 0; collapseCount = 0; allocSize = 0L; freeSize = 0L
            }

    module MaterializedTree =
        
        let inline original (node : MaterializedTree) = node.original
        let inline children (node : MaterializedTree) = node.children

        let ofNode (id : int) (node : ILodTreeNode) =
            {
                rootId = id
                original = node
                children = []
            }

        
        let rec allNodes (node : MaterializedTree) =
            Seq.append 
                (Seq.singleton node)
                (node.children |> Seq.collect allNodes)

        let allChildren (node : MaterializedTree) =
            node.children |> Seq.collect allNodes

        let qualityHistogram (histo : SortedDictionary<float, ref<int>>) (predictView : ILodTreeNode -> Trafo3d) (view : Trafo3d) (proj : Trafo3d) (t : MaterializedTree) (state : MaterializedTree) =
            let rec run (t : MaterializedTree) (state : MaterializedTree) =
                let node = t.original
                
                if List.isEmpty t.children && node.ShouldSplit(1.0, view, proj) then //&& node.ShouldSplit(predictView node, proj) then
                    if List.isEmpty state.children then
                        let minQ = node.SplitQuality(view, proj)
                        match histo.TryGetValue minQ with
                        | (true, r) -> 
                            r := !r + 1
                        | _ -> 
                            let r = ref 1
                            histo.[minQ] <- r

                elif not (List.isEmpty t.children) && node.ShouldCollapse(1.0, view, proj) then
                    ()

                else
                    match t.children, state.children with
                        | [], [] ->
                            ()

                        | [], _ -> ()
                        | _, [] -> ()

                        | l,r ->
                            List.iter2 run l r
            
            run t state


        


        let rec tryExpand (quality : float)(predictView : ILodTreeNode -> Trafo3d) (view : Trafo3d) (proj : Trafo3d) (t : MaterializedTree) =
            let node = t.original

            let inline tryExpandMany (ls : list<MaterializedTree>) =
                let mutable changed = false
                let newCs = 
                    ls |> List.map (fun c ->
                        match tryExpand quality predictView view proj c with
                            | Some newC -> 
                                changed <- true
                                newC
                            | None ->
                                c
                    )
                if changed then Some newCs
                else None
                
            if List.isEmpty t.children && node.ShouldSplit(quality, view, proj) then //&& node.ShouldSplit(predictView node, proj) then
                Some { t with children = node.Children |> Seq.toList |> List.map (ofNode t.rootId) }

            elif not (List.isEmpty t.children) && node.ShouldCollapse(quality, view, proj) then
                Some { t with children = [] }

            else
                match t.children with
                    | [] ->
                        None

                    | children ->
                        match tryExpandMany children with
                            | Some newChildren -> Some { t with children = newChildren }
                            | _ -> None

        let expand (quality : float)(predictView : ILodTreeNode -> Trafo3d) (view : Trafo3d) (proj : Trafo3d) (t : MaterializedTree) =
            match tryExpand quality predictView view proj t with
                | Some n -> n
                | None -> t

        let rec computeDelta (acc : Delta) (o : MaterializedTree) (n : MaterializedTree) =
            if System.Object.ReferenceEquals(o,n) then
                acc
            else

                let rec computeChildDeltas (acc : Delta) (os : list<MaterializedTree>) (ns : list<MaterializedTree>) =
                    match os, ns with
                        | [], [] -> 
                            acc
                        | o :: os, n :: ns ->
                            let acc = computeDelta acc o n
                            computeChildDeltas acc os ns
                        | _ ->
                            failwith "inconsistent child count"
                            
                if o.original = n.original then
                    match o.children, n.children with
                        | [], []    -> 
                            acc

                        | [], _     ->
                            { acc with
                                deltas = HMap.add n.original (n.rootId, NodeOperation.Split) acc.deltas
                                splitCount = 1 + acc.splitCount
                                allocSize = int64 n.original.DataSize + acc.allocSize
                            }
                        | oc, []    -> 
                            let children = allChildren o |> Seq.map original |> Seq.toList
                            { acc with
                                deltas = HMap.add n.original (n.rootId, NodeOperation.Collapse(children)) acc.deltas
                                collapseCount = 1 + acc.collapseCount
                                freeSize = children |> List.fold (fun s c -> s + int64 c.DataSize) acc.freeSize
                            }
                        | os, ns    -> 
                            computeChildDeltas acc os ns
                else
                    failwith "inconsistent child values"

    type RenderState =
        {
            iface : GLSLProgramInterface
            calls : DrawPool
            mutable allocs : int
            mutable uploadSize : Mem
            mutable nodeSize : int
            mutable count : int
        }


    type LodRenderingInfo =
        {
            quality         : IModRef<float>
            maxQuality      : IModRef<float>
            renderBounds    : IMod<bool>
        }

    type LodRenderer(ctx : Context, state : PreparedPipelineState, pass : RenderPass, info : LodRenderingInfo, useCulling : bool, maxSplits : IMod<int>, roots : aset<LodTreeInstance>, renderTime : IMod<_>, model : IMod<Trafo3d>, view : IMod<Trafo3d>, proj : IMod<Trafo3d>, budget : IMod<int64>)  =
        inherit PreparedCommand(ctx, pass)

        static let scheduler = new LimitedConcurrencyLevelTaskScheduler(ThreadPriority.BelowNormal, max 2 (Environment.ProcessorCount - 3))
            
        static let startTask (ct : CancellationToken) (f : unit -> 'a) =
            Task.Factory.StartNew(Func<'a>(f), ct, TaskCreationOptions.None, scheduler)
                
        let manager = (unbox<Runtime> ctx.Runtime).ResourceManager
        let signature = state.pProgramInterface

        let timeWatch = System.Diagnostics.Stopwatch.StartNew()
        let time() = timeWatch.MicroTime
        
        let pool = GeometryPool.Get ctx
        

        let reader = roots.GetReader()
        let euclideanView = view |> Mod.map Euclidean3d

  
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
        let mutable renderingConverged = 1
        let renderDelta : ref<AtomicQueue<ILodTreeNode, GeometryInstance>> = ref AtomicQueue.empty

        let pRenderBounds : nativeptr<int> = 
            NativePtr.allocArray [| (if info.renderBounds.GetValue() then 1 else 0) |]

        let rootIdsLock = obj()
        let rootIds : ModRef<hmap<ILodTreeNode, int>> = Mod.init HMap.empty

        let mutable rootUniforms : hmap<ILodTreeNode, MapExt<string, IMod>> = HMap.empty
        
        let rootUniformCache = System.Collections.Concurrent.ConcurrentDictionary<ILodTreeNode, System.Collections.Concurrent.ConcurrentDictionary<string, Option<IMod>>>()
        let rootTrafoCache = System.Collections.Concurrent.ConcurrentDictionary<ILodTreeNode, IMod<Trafo3d>>()

        let getRootTrafo (root : ILodTreeNode) =
            rootTrafoCache.GetOrAdd(root, fun root ->
                match HMap.tryFind root rootUniforms with
                | Some table -> 
                    match MapExt.tryFind "ModelTrafo" table with
                    | Some (:? IMod<Trafo3d> as m) -> model %* m
                    | _ -> model
                | None ->
                    model
            )
                
        let getRootUniform (name : string) (root : ILodTreeNode) : Option<IMod> =
            let rootCache = rootUniformCache.GetOrAdd(root, fun root -> System.Collections.Concurrent.ConcurrentDictionary())
            rootCache.GetOrAdd(name, fun name ->
                match name with
                | "ModelTrafos"              -> getRootTrafo root :> IMod |> Some
                | "ModelTrafosInv"           -> getRootTrafo root |> Mod.map (fun t -> t.Inverse) :> IMod |> Some

                | "ModelViewTrafos"          -> Mod.map2 (fun a b -> a * b) (getRootTrafo root) view :> IMod |> Some
                | "ModelViewTrafosInv"       -> getRootTrafo root %* view |> Mod.map (fun t -> t.Inverse) :> IMod |> Some

                | "ModelViewProjTrafos"      -> getRootTrafo root %* view %* proj :> IMod |> Some
                | "ModelViewProjTrafosInv"   -> getRootTrafo root %* view %* proj |> Mod.map (fun t -> t.Inverse) :> IMod |> Some

                | "NormalMatrices"           -> getRootTrafo root |> Mod.map (fun t -> M33d.op_Explicit t.Backward.Transposed):> IMod |> Some
                | "NormalMatricesInv"        -> getRootTrafo root |> Mod.map (fun t -> M33d.op_Explicit t.Forward.Transposed):> IMod |> Some
                | _ -> 
                    match HMap.tryFind root rootUniforms with
                    | Some table -> MapExt.tryFind name table
                    | None -> None
            )

        let getRootId (root : ILodTreeNode) =
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
            rootUniformCache.TryRemove root |> ignore
            rootTrafoCache.TryRemove root |> ignore
            transact (fun () ->
                lock rootIdsLock (fun () ->
                    rootIds.Value <- HMap.remove root rootIds.Value
                )
            )

        let contents =
            state.pProgramInterface.storageBuffers |> MapExt.toSeq |> Seq.choose (fun (name, buffer) ->
                if Map.containsKey buffer.ssbBinding state.pStorageBuffers then
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
                                        let v = v.GetValue(t) |> conv
                                        data.SetValue(v, id)
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
        

        let alloc (state : RenderState) (node : ILodTreeNode) (g : GeometryInstance) =
            cache.GetOrCreate(node, fun node ->
                let slot = pool.Alloc(g.signature, g.instanceCount, g.indexCount, g.vertexCount)
                slot.Upload(g.geometry, g.uniforms)

                state.uploadSize <- state.uploadSize + slot.Memory
                state.nodeSize <- state.nodeSize + node.DataSize
                state.count <- state.count + 1
                slot
            )
            
        let performOp (state : RenderState) (parentOp : AtomicOperation<ILodTreeNode, GeometryInstance>) (node : ILodTreeNode) (op : Operation<GeometryInstance>) =
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
                        let w = state.calls.Add(slot, node.BoundingBox, node.CellBoundingBox, rootId)
                        if not w then Log.warn "[Lod] alloc cannot activate %s (was already active)" node.Name
                        activateWatch.Stop()

                    elif active < 0 then 
                        deactivateWatch.Start()
                        let w = state.calls.Remove slot
                        if not w then Log.warn "[Lod] alloc cannot deactivate %s (was already inactive)" node.Name
                        deactivateWatch.Stop()

                | Free ac ->
                    match cache.TryRemove node with
                        | (true, slot) -> 
                            if ac < 0 then 
                                deactivateWatch.Start()
                                let w = state.calls.Remove slot
                                if not w then Log.warn "[Lod] free cannot deactivate %s (was already inactive)" node.Name
                                deactivateWatch.Stop()

                            if slot.IsDisposed then
                                Log.warn "[Lod] cannot free %s (was already free)" node.Name
                            else
                                freeWatch.Start()
                                pool.Free slot
                                freeWatch.Stop()

                        | _ ->
                            Log.warn "[Lod] cannot free %s" node.Name
                            

                | Activate ->
                    match cache.TryGetValue node with
                        | (true, slot) ->
                            activateWatch.Start()
                            state.calls.Add(slot, node.BoundingBox, node.CellBoundingBox, rootId) |> ignore
                            activateWatch.Stop()
                        | _ ->
                            Log.warn "[Lod] cannot activate %A %A" node (Option.isSome op.value)

                    

                | Deactivate ->
                    match cache.TryGetValue node with
                        | (true, slot) ->
                            deactivateWatch.Start()
                            state.calls.Remove slot |> ignore
                            deactivateWatch.Stop()
                        | _ ->
                            Log.warn "[Lod] cannot deactivate %A %A" node (Option.isSome op.value)
                    
                | Nop ->
                    ()
            
        let perform (state : RenderState) (op : AtomicOperation<ILodTreeNode, GeometryInstance>) =
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

                    renderTime.GetValue token |> ignore
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
  
                        renderingConverged <- 1

                    | Some ops ->
                        perform state ops
                        sync()
                        run (cnt + 1)
                        
            run 0

        let evaluate (calls : DrawPool) (token : AdaptiveToken) (iface : GLSLProgramInterface) =
            needUpdate.GetValue(token)

            NativePtr.write pRenderBounds (if info.renderBounds.GetValue(token) then 1 else 0)

            let maxTime = max (MicroTime.FromMilliseconds 1.0) calls.AverageRenderTime
            let maxMem = Mem (3L <<< 30)
            run token maxMem maxTime calls iface
            sync()
            
        let inner =
            { new DrawPool(ctx, useCulling, pRenderBounds, activeBuffer.Pointer, modelViewProjBuffer.Pointer, state, pass) with
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
            let mutable roots : hmap<ILodTreeNode, TaskTree<GeometryInstance>> = HMap.empty
            
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

            let mutable lastQ = 0.0

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

                            Log.line "q: %.2f m: %A (%A) r: %A l : %s mq: %A e: %A u: %A c: %d s: %d t: %d d: %A" 
                                        lastQ 
                                        (pool.UsedMemory + inner.UsedMemory) 
                                        (pool.TotalMemory + inner.TotalMemory) 
                                        inner.AverageRenderTime 
                                        loads q e u 
                                        collapses splits tasks (Num points)

                )
                
            let puller =
                startThread (fun () ->
                    let timer = new MultimediaTimer.Trigger(20)
                    
                    let mutable lastRoots = HMap.empty
                    let mutable readers : hmap<ILodTreeNode, TaskTreeReader<_>> = HMap.empty

                    while not shutdown.IsCancellationRequested do
                        //timer.Wait()
                        MVar.take changesPending

                        let readers, removed = 
                            lock rootLock (fun () ->
                                let removed = List<TaskTreeReader<_>>()

                                let delta = HMap.computeDelta lastRoots roots
                                lastRoots <- roots

                                for k, op in delta do
                                    match op with
                                    | Remove ->
                                        match HMap.tryRemove k readers with
                                        | Some (r, rs) ->
                                            readers <- rs
                                            removed.Add(r)
                                        | None ->
                                            ()
                                    | Set v ->
                                        let r = TaskTreeReader(v)
                                        readers <- HMap.add k r readers
                                readers, removed
                            )

                        for r in removed do
                            r.Destroy(renderDelta)

                        for (_,r) in readers do
                            r.Update(renderDelta)
                    
                    for (_,r) in readers do
                        r.Destroy(renderDelta)
                )
                
            let thread = 
                startThread (fun () ->
                    let notConverged = new ManualResetEventSlim(true)

                    let cancel = System.Collections.Concurrent.ConcurrentDictionary<ILodTreeNode, CancellationTokenSource>()
                    let timer = new MultimediaTimer.Trigger(10)
                    
                    let stop (node : ILodTreeNode) =
                        match cancel.TryRemove node with
                            | (true, c) -> 
                                try c.Cancel()
                                with :? ObjectDisposedException -> ()
                            | _ -> 
                                ()
         
                    let load (ct : CancellationToken) (rootId : int) (node : ILodTreeNode) (cont : CancellationToken -> ILodTreeNode -> GeometryInstance -> 'r) =
                        startTask ct (fun () ->
                            let startTime = time()
                            let (g,u) = node.GetData(ct, wantedInputs)

                            let cnt = 
                                match Seq.tryHead u with
                                | Some (KeyValue(_, (v : Array) )) -> v.Length
                                | _ -> 1

                            let u = MapExt.add "TreeId" (Array.create cnt rootId :> System.Array) u
                            let loaded = GeometryInstance.ofGeometry signature g u
                                
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
                            view :> IAdaptiveObject, view.AddMarkingCallback (fun () -> notConverged.Set())
                            proj :> IAdaptiveObject, proj.AddMarkingCallback (fun () -> notConverged.Set())
                            reader :> IAdaptiveObject, reader.AddMarkingCallback (fun () -> notConverged.Set())
                            maxSplits :> IAdaptiveObject, maxSplits.AddMarkingCallback (fun () -> notConverged.Set())
                            budget :> IAdaptiveObject, budget.AddMarkingCallback (fun () -> notConverged.Set())
                        ]

                    let mutable lastMaxQ = 0.0
                    try 
                        while not shutdown.IsCancellationRequested do
                            timer.Wait()
                            notConverged.Wait(shutdown.Token)
                            //caller.EvaluateAlways AdaptiveToken.Top (fun token ->
                            let view = view.GetValue AdaptiveToken.Top
                            let proj = proj.GetValue AdaptiveToken.Top
                            let ops = reader.GetOperations AdaptiveToken.Top
                            let maxSplits = maxSplits.GetValue AdaptiveToken.Top
                          
                            for o in ops do
                                match o with
                                | Add(_,i) ->
                                    let r = i.root
                                    let u = i.uniforms
                                    let rid = getRootId r
                                    rootUniforms <- HMap.add r u rootUniforms
                                    let load ct n = load ct rid n (fun _ _ r -> r)
                                    lock rootLock (fun () ->
                                        roots <- HMap.add r (TaskTree(load, rid)) roots
                                    )

                                | Rem(_,i) ->
                                    let r = i.root
                                    let u = i.uniforms
                                    rootUniforms <- HMap.remove r rootUniforms
                                    freeRootId r
                                    lock rootLock (fun () ->
                                        roots <- HMap.remove r roots
                                    )
                                    MVar.put changesPending ()

                            let modelView (r : ILodTreeNode) =
                                let t = getRootTrafo r
                                subs.GetOrCreate(t, fun t -> t.AddMarkingCallback (fun () -> notConverged.Set())) |> ignore
                                let m = t.GetValue()
                                m * view
                                


                            let budget = budget.GetValue()
                            let roots = lock rootLock (fun () -> roots)

                            let start = time()
                            //let maxQ, dataSize = TreeHelpers.getMaxQuality lastQ budget (Seq.map fst roots) modelView proj
                            let maxQ = 1.0
                            let dt = time() - start
                            maxQualityTime.Add(dt.TotalMilliseconds)


                            let collapseIfNotSplit = maxQ < 1.0
                            
                            let start = time()
                            let queue = List()
                            for (k,v) in roots do
                                v.BuildQueue(state, collapseIfNotSplit, Some k, maxQ, modelView, proj, queue)
            
                            let q = TaskTree<_>.ProcessQueue(state, queue, maxQ, modelView, proj, maxSplits)
                            lastQ <- q
                            let dt = time() - start
                            expandTime.Add dt.TotalMilliseconds

                            transact (fun () -> 
                                info.quality.Value <- q
                                info.maxQuality.Value <- maxQ
                            )

                            //if maxQ <> lastMaxQ then
                            //    lastMaxQ <- maxQ
                            //    Log.warn "maxQ: %.3f (%A / %A)" maxQ (Num dataSize)(Num !state.dataSize)


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
            renderingConverged <- 1
            renderDelta := AtomicQueue.empty
            storageBuffers |> Map.toSeq |> Seq.iter (fun (_,b) -> b.Dispose())
            activeBuffer.Dispose()

        override x.EntryState = inner.EntryState
        override x.ExitState = inner.ExitState




module StoreTree =
    open Aardvark.Geometry
    open Aardvark.Geometry.Points
    open Aardvark.Data.Points
    open Aardvark.Data.Points.Import


    type PointTreeNode(cache : LruDictionary<string, obj>, source : Symbol, globalTrafo : Similarity3d, root : Option<PointTreeNode>, parent : Option<PointTreeNode>, level : int, self : PointSetNode) as this =
        let globalTrafoTrafo = Trafo3d globalTrafo
        let bounds = self.BoundingBoxExact.Transformed globalTrafoTrafo
        let cellBounds = self.BoundingBox.Transformed globalTrafoTrafo
        let cell = self.Cell
        let isLeaf = self.IsLeaf
        let id = self.Id

        let mutable refCount = 0
        let mutable livingChildren = 0
        let mutable children : Option<list<ILodTreeNode>> = None
 
        static let nodeId (n : PointSetNode) =
            string n.Id + "PointTreeNode"
            
        static let cacheId (n : PointSetNode) =
            string n.Id + "GeometryData"
            
        let cmp = Func<float,float,int>(compare)
        let getAverageDistance (original : V3f[]) (positions : V3f[]) (tree : PointRkdTreeD<_,_>) =
            let heap = List<float>(positions.Length)
            //let mutable sum = 0.0
            //let mutable cnt = 0
            for i in 0 .. original.Length - 1 do
                let q = tree.CreateClosestToPointQuery(Double.PositiveInfinity, 25)
                let l = tree.GetClosest(q, original.[i])
                if l.Count > 1 then
                    let mutable minDist = Double.PositiveInfinity
                    for l in l do
                        let dist = V3f.Distance(positions.[int l.Index], positions.[i])
                        if dist > 0.0f then
                            minDist <- min (float dist) minDist
                    if not (Double.IsInfinity minDist) then
                        heap.HeapEnqueue(cmp, minDist)
                    //sum <- sum + float dist
                    //cnt <- cnt + 1
            if heap.Count > 0 then
                let fstThrd = heap.Count / 3
                let real = heap.Count - 2 * heap.Count / 3
                for i in 1 .. fstThrd do heap.HeapDequeue(cmp) |> ignore

                let mutable sum = 0.0
                for i in 1 .. real do
                    sum <- sum + heap.HeapDequeue(cmp)
                    
                sum / float real
            elif original.Length > 2 then
                Log.error "empty heap (%d)" original.Length
                0.0
            else 
                0.0

        let load (ct : CancellationToken) (ips : MapExt<string, Type>) =
            cache.GetOrCreate(cacheId self, fun () ->
                let center = self.Center
                let attributes = SymbolDict<Array>()
                let mutable uniforms = MapExt.empty


                let original =
                    if self.HasLodPositions then self.LodPositions.Value
                    elif self.HasPositions then self.Positions.Value
                    else [| V3f(System.Single.NaN, System.Single.NaN, System.Single.NaN) |]
                    
                let globalTrafo1 = globalTrafo * Euclidean3d(Rot3d.Identity, center)
                let positions = 
                    let inline fix (p : V3f) = globalTrafo1.TransformPos (V3d p) |> V3f
                    original |> Array.map fix
                attributes.[DefaultSemantic.Positions] <- positions
                
                if MapExt.containsKey "Colors" ips then
                    let colors = 
                        if self.HasLodColors then self.LodColors.Value
                        elif self.HasColors then self.Colors.Value
                        else Array.create original.Length C4b.White
                    attributes.[DefaultSemantic.Colors] <- colors
           
                if MapExt.containsKey "Normals" ips then
                    let normals = 
                        //if self.HasNormals then self.Normals.Value
                        //elif self.HasLodNormals then self.LodNormals.Value
                        //else Array.create original.Length V3f.OOO
                        if self.HasKdTree then 
                            let tree = self.KdTree.Value
                            Aardvark.Geometry.Points.Normals.EstimateNormals(original, tree, 17)
                        elif self.HasLodKdTree then 
                            let tree = self.LodKdTree.Value
                            Aardvark.Geometry.Points.Normals.EstimateNormals(original, tree, 17)
                        else
                            Array.create original.Length V3f.OOO

                    let normals =
                        let normalMat = (Trafo3d globalTrafo.EuclideanTransformation).Backward.Transposed |> M33d.op_Explicit
                        let inline fix (p : V3f) = normalMat * (V3d p) |> V3f
                        normals |> Array.map fix

                    attributes.[DefaultSemantic.Normals] <- normals


                
                if MapExt.containsKey "AvgPointDistance" ips then
                    let dist = 
                        if self.HasKdTree then 
                            let tree = self.KdTree.Value
                            getAverageDistance original positions tree

                        elif self.HasLodKdTree then 
                            let tree = self.LodKdTree.Value
                            getAverageDistance original positions tree
                        else 
                            bounds.Size.NormMax / 40.0

                    let avgDist = 
                        //bounds.Size.NormMax / 40.0
                        if dist <= 0.0 then bounds.Size.NormMax / 40.0 else dist

                    uniforms <- MapExt.add "AvgPointDistance" ([| float32 avgDist |] :> System.Array) uniforms
                    
                if MapExt.containsKey "TreeLevel" ips then    
                    let arr = [| float32 level |] :> System.Array
                    uniforms <- MapExt.add "TreeLevel" arr uniforms
                    
                if MapExt.containsKey "MaxTreeDepth" ips then    
                    let arr = [| self.GetMaximiumTreeDepth(true) |] :> System.Array
                    uniforms <- MapExt.add "MaxTreeDepth" arr uniforms
                    
                if MapExt.containsKey "MinTreeDepth" ips then    
                    let arr = [| self.GetMinimumTreeDepth(true) |] :> System.Array
                    uniforms <- MapExt.add "MinTreeDepth" arr uniforms

                let geometry =
                    IndexedGeometry(
                        Mode = IndexedGeometryMode.PointList,
                        IndexedAttributes = attributes
                    )
                
                let mem = int64 positions.Length * 28L
                let res = geometry, uniforms
                struct (res :> obj, mem)
            )
            |> unbox<IndexedGeometry * MapExt<string, Array>>

        let angle (view : Trafo3d) =
            let cam = view.Backward.C3.XYZ

            let avgPointDistance = bounds.Size.NormMax / 40.0

            let minDist = bounds.GetMinimalDistanceTo(cam)
            let minDist = max 0.01 minDist

            let angle = Constant.DegreesPerRadian * atan2 avgPointDistance minDist

            let factor = 1.0 //(minDist / 0.01) ** 0.05

            angle / factor
        
        member x.AcquireChild() =
            Interlocked.Increment(&livingChildren) |> ignore
    
        member x.ReleaseChild() =
            let destroy = Interlocked.Change(&livingChildren, fun o -> max 0 (o - 1), o = 1)
            if destroy then
                let children = 
                    lock x (fun () ->
                        let c = children
                        children <- None
                        c
                    ) 
                match children with
                | Some c ->
                    for c in c do 
                        let c = unbox<PointTreeNode> c
                        cache.Add(nodeId c.Original, c, 1L <<< 10) |> ignore
                | None ->
                    ()


        member x.Acquire() =
            if Interlocked.Increment(&refCount) = 1 then
                match parent with
                | Some p -> p.AcquireChild()
                | None -> ()

        member x.Release() =
            let destroy = Interlocked.Change(&refCount, fun o -> max 0 (o - 1), o = 1)
            if destroy then
                match parent with
                | Some p -> p.ReleaseChild()
                | None -> ()

        member x.Original = self

        member x.Root : PointTreeNode =
            match root with
            | Some r -> r
            | None -> x

        member x.Children  =
            if livingChildren > 0 then
                match children with
                | Some c -> c :> seq<_>
                | None ->
                    lock x (fun () ->
                        match children with
                        | Some c -> c :> seq<_>
                        | None ->
                            let c = 
                                if isNull self.Subnodes then
                                    []
                                else
                                    self.Subnodes |> Seq.toList |> List.choose (fun c ->
                                        if isNull c then
                                            None
                                        else
                                            let c = c.Value
                                            if isNull c then
                                                None
                                            else
                                                let id = nodeId c
                                                match cache.TryGetValue id with
                                                | (true, n) ->
                                                    cache.Remove id |> ignore
                                                    unbox<ILodTreeNode> n |> Some
                                                | _ -> 
                                                    PointTreeNode(cache, source, globalTrafo, Some this.Root, Some this, level + 1, c) :> ILodTreeNode |> Some
                                    )
                            children <- Some c
                            c :> seq<_>
                                    
                    )
            else
                if isNull self.Subnodes then
                    Seq.empty
                else
                    self.Subnodes |> Seq.choose (fun c ->
                        if isNull c then
                            None
                        else
                            let c = c.Value
                            if isNull c then
                                None
                            else
                                cache.GetOrCreate(nodeId c, fun () ->
                                    let n = PointTreeNode(cache, source, globalTrafo, Some this.Root, Some this, level + 1, c)
                                    struct (n :> obj, 1L <<< 10)
                                )
                                |> unbox<ILodTreeNode> |> Some
                    )

        member x.Id = id

        member x.GetData(ct, ips) = 
            load ct ips
            
        member x.ShouldSplit (quality : float, view : Trafo3d, proj : Trafo3d) =
            not isLeaf && angle view > 0.4 / quality

        member x.ShouldCollapse (quality : float, view : Trafo3d, proj : Trafo3d) =
            angle view < 0.3 / quality
            
        member x.SplitQuality (view : Trafo3d, proj : Trafo3d) =
            0.4 / angle view

        member x.CollapseQuality (view : Trafo3d, proj : Trafo3d) =
            0.3 / angle view

        member x.DataSource = source

        override x.ToString() = 
            sprintf "%s[%d]" (string x.Id) level

        interface ILodTreeNode with
            member x.Root = x.Root :> ILodTreeNode
            member x.Level = level
            member x.Name = x.ToString()
            member x.DataSource = source
            member x.Parent = parent |> Option.map (fun n -> n :> ILodTreeNode)
            member x.Children = x.Children 
            member x.ShouldSplit(q,v,p) = x.ShouldSplit(q,v,p)
            member x.ShouldCollapse(q,v,p) = x.ShouldCollapse(q,v,p)
            member x.SplitQuality(v,p) = x.SplitQuality(v,p)
            member x.CollapseQuality(v,p) = x.CollapseQuality(v,p)
            member x.DataSize = int self.LodPointCount
            member x.TotalDataSize = int self.PointCountTree
            member x.GetData(ct, ips) = x.GetData(ct, ips)
            member x.BoundingBox = bounds
            member x.CellBoundingBox = cellBounds
            member x.Cell = cell
            member x.Acquire() = ()
            member x.Release() = ()

        override x.GetHashCode() = 
            HashCode.Combine(x.DataSource.GetHashCode(), self.Id.GetHashCode())

        override x.Equals o =
            match o with
                | :? PointTreeNode as o -> x.DataSource = o.DataSource && self.Id = o.Id
                | _ -> false
    
    module IndexedGeometry =
        module private Arr = 
            let append (l : Array) (r : Array) =
                let et = l.GetType().GetElementType()
                let res = Array.CreateInstance(et, l.Length + r.Length)
                l.CopyTo(res, 0)
                r.CopyTo(res, l.Length)
                res
            
            let concat (l : seq<Array>) =
                let l = Seq.toList l
                match l with
                | [] -> Array.CreateInstance(typeof<int>, 0)
                | [a] -> a
                | f :: _ ->
                    let len = l |> List.sumBy (fun a -> a.Length)
                    let et = f.GetType().GetElementType()
                    let res = Array.CreateInstance(et, len)
                    let mutable offset = 0
                    for a in l do
                        a.CopyTo(res, offset)
                        offset <- offset + a.Length
                    res

        let union (l : IndexedGeometry) (r : IndexedGeometry) =
            assert (l.Mode = r.Mode)
            assert (isNull l.IndexArray = isNull r.IndexArray)

            let index =
                if isNull l.IndexArray then null
                else Arr.append l.IndexArray r.IndexArray

            let atts =
                l.IndexedAttributes |> Seq.choose (fun (KeyValue(sem, l)) ->
                    match r.IndexedAttributes.TryGetValue(sem) with
                    | (true, r) -> Some (sem, Arr.append l r)
                    | _ -> None
                ) |> SymDict.ofSeq

            IndexedGeometry(
                Mode = l.Mode,
                IndexArray = index,
                IndexedAttributes = atts
            )


        let unionMany (s : seq<IndexedGeometry>) =
            use e = s.GetEnumerator()
            if e.MoveNext() then
                let mutable res = e.Current
                while e.MoveNext() do
                    res <- union res e.Current
                res

            else
                IndexedGeometry()

    type TreeViewNode(inner : ILodTreeNode, limit : int, parent : Option<ILodTreeNode>, root : Option<ILodTreeNode>) as this =
        let root = match root with | Some r -> r | None -> this :> ILodTreeNode

        let isLeaf = inner.TotalDataSize <= limit
                
        member x.ShouldSplit(q,v,p) =
            not isLeaf && inner.ShouldSplit(q,v,p)

        member x.GetData(ct : CancellationToken, ips : MapExt<string, Type>) =
            if isLeaf then
                let rec traverse (n : ILodTreeNode) =
                    match Seq.toList n.Children with
                    | [] -> [inner.GetData(ct, ips)]
                    | cs -> cs |> List.collect traverse

                let datas = traverse inner
                match datas with
                    | (_,u) :: _ ->
                        Log.warn "merge %d" (List.length datas)
                        let g = datas |> List.map fst |> IndexedGeometry.unionMany
                        g,u
                    | _ -> 
                        failwith ""
            else
                inner.GetData(ct, ips)
        
        interface ILodTreeNode with
            member x.Root = root
            member x.Level = inner.Level
            member x.Name = inner.Name
            member x.DataSource = inner.DataSource
            member x.Parent = parent
            member x.Children = 
                if isLeaf then Seq.empty
                else inner.Children |> Seq.map (fun n -> TreeViewNode(n, limit, Some (this :> ILodTreeNode), Some root) :> ILodTreeNode)
            member x.ShouldSplit(q,v,p) = x.ShouldSplit(q,v,p)
            member x.ShouldCollapse(q,v,p) = inner.ShouldCollapse(q,v,p)
            member x.SplitQuality(v,p) = inner.SplitQuality(v,p)
            member x.CollapseQuality(v,p) = inner.CollapseQuality(v,p)
            member x.DataSize = if isLeaf then inner.TotalDataSize else inner.DataSize
            member x.TotalDataSize = inner.TotalDataSize
            member x.GetData(ct, ips) = x.GetData(ct, ips)
            member x.BoundingBox = inner.BoundingBox
            member x.CellBoundingBox = inner.CellBoundingBox
            member x.Cell = inner.Cell
            member x.Acquire() = inner.Acquire()
            member x.Release() = inner.Release()



    type InCoreStructureTree(inner : ILodTreeNode, parent : Option<ILodTreeNode>, root : Option<ILodTreeNode>) as this =
        let root = match root with | Some r -> r | None -> this :> ILodTreeNode
        let mutable children = [] //inner.Children |> Seq.toList |> List.map (fun n -> InCoreStructureTree(n, Some (this :> ILodTreeNode), Some root) :> ILodTreeNode)

        member private x.Build(nodeCount : ref<int>) =
            inc &nodeCount.contents
            children <- 
                inner.Children |> Seq.toList |> List.map (fun n -> 
                    let t = InCoreStructureTree(n, Some (this :> ILodTreeNode), Some root)
                    t.Build(nodeCount)
                    t :> ILodTreeNode
                )

        member x.Build() =
            let cnt = ref 0 
            x.Build(cnt)
            !cnt


        interface ILodTreeNode with
            member x.Root = root
            member x.Level = inner.Level
            member x.Name = inner.Name
            member x.DataSource = inner.DataSource
            member x.Parent = parent
            member x.Children = children :> seq<_>
            member x.ShouldSplit(q,v,p) = inner.ShouldSplit(q,v,p)
            member x.ShouldCollapse(q,v,p) = inner.ShouldCollapse(q,v,p)
            member x.SplitQuality(v,p) = inner.SplitQuality(v,p)
            member x.CollapseQuality(v,p) = inner.CollapseQuality(v,p)
            member x.DataSize = inner.DataSize
            member x.TotalDataSize = inner.TotalDataSize
            member x.GetData(ct, ips) = inner.GetData(ct, ips)
            member x.BoundingBox = inner.BoundingBox
            member x.CellBoundingBox = inner.CellBoundingBox
            member x.Cell = inner.Cell
            member x.Acquire() = inner.Acquire()
            member x.Release() = inner.Release()



    //let private cache = LruDictionary(1L <<< 30)


    let gc (input : string) (key : string) (output : string) =
        do Aardvark.Data.Points.Import.Pts.PtsFormat |> ignore
        do Aardvark.Data.Points.Import.E57.E57Format |> ignore
        
        use output = PointCloud.OpenStore(output, LruDictionary(1L <<< 30))
        use input = PointCloud.OpenStore(input, LruDictionary(1L <<< 30))
        let set = input.GetPointSet(key)   
       
        let storeStructure (node : PointSetNode) =
            let queue = Queue<PointSetNode>()
            queue.Enqueue(node)

            let mutable i = 0
            while queue.Count > 0 do
                let n = queue.Dequeue()
                output.Add(string n.Id, n)

                if i % 100000 = 0 then
                    Log.line "%d nodes" i
                    output.Flush()

                if not (isNull n.Subnodes) then
                    for c in n.Subnodes do
                        if not (isNull c) then
                            let c = c.Value
                            if not (isNull c) then queue.Enqueue c

                i <- i + 1

        let storeAttributes (node : PointSetNode) =
            let queue = Queue<PointSetNode>()
            queue.Enqueue(node)
            
            let mutable i = 0
            while queue.Count > 0 do
                let n = queue.Dequeue()
                
                if n.HasPositions then output.Add(n.Positions.Id, n.Positions.Value)
                if n.HasNormals then output.Add(n.Normals.Id, n.Normals.Value)
                if n.HasColors then output.Add(n.Colors.Id, n.Colors.Value)
                if n.HasKdTree then output.Add(n.KdTree.Id, n.KdTree.Value.Data)
                if n.HasIntensities then output.Add(n.Intensities.Id, n.Intensities.Value)
                if n.HasClassifications then output.Add(n.Classifications.Id, n.Classifications.Value)
                
                if n.HasLodPositions then output.Add(n.LodPositions.Id, n.LodPositions.Value)
                if n.HasLodNormals then output.Add(n.LodNormals.Id, n.LodNormals.Value)
                if n.HasLodColors then output.Add(n.LodColors.Id, n.LodColors.Value)
                if n.HasLodKdTree then output.Add(n.LodKdTree.Id, n.LodKdTree.Value.Data)
                if n.HasLodIntensities then output.Add(n.LodIntensities.Id, n.LodIntensities.Value)
                if n.HasLodClassifications then output.Add(n.LodClassifications.Id, n.LodClassifications.Value)
                
                if i % 1000 = 0 then
                    Log.line "%d datas" i
                    output.Flush()

                if not (isNull n.Subnodes) then
                    for c in n.Subnodes do
                        if not (isNull c) then
                            let c = c.Value
                            if not (isNull c) then queue.Enqueue c
                            
                i <- i + 1

        let root = set.Root.Value

        output.Add(key, set)
        storeStructure root
        storeAttributes root

    let withInCoreStructure (n : LodTreeInstance) =
        match n.root with
        | :? InCoreStructureTree -> n
        | _ -> 
            let root = InCoreStructureTree(n.root, None, None)
            let cnt = root.Build()
            Log.warn "loaded %d nodes" cnt
            { n with root = root }
        
    let withSplitLimit (limit : int) (n : LodTreeInstance) =
        { n with root = TreeViewNode(n.root, limit, None, None) }
        

    let import (sourceName : string) (file : string) (store : string) (uniforms : list<string * IMod>) =
        do Aardvark.Data.Points.Import.Pts.PtsFormat |> ignore
        do Aardvark.Data.Points.Import.E57.E57Format |> ignore
        
        let store = PointCloud.OpenStore(store, LruDictionary(1L <<< 30))
            
        let key = System.IO.Path.GetFileNameWithoutExtension(file).ToLower()
        let set = store.GetPointSet(key)

        let points = 
            if isNull set then
                let config3 = 
                    Aardvark.Data.Points.ImportConfig.Default
                        .WithStorage(store)
                        .WithKey(key)
                        .WithVerbose(true)
                        .WithMaxChunkPointCount(10000000)
                        .WithEstimateNormals(Func<_,_>(fun a -> Aardvark.Geometry.Points.Normals.EstimateNormals(Seq.toArray a, 17) :> System.Collections.Generic.IList<V3f>))
        
                let res = PointCloud.Import(file,config3)
                store.Flush()
                res
            else
                set
               

        let root = points.Root.Value
        let bounds = root.Cell.BoundingBox
        let trafo = 
        
            Similarity3d(1.0, Euclidean3d(Rot3d(V3d.OOI, Constant.PiHalf), V3d.Zero)) *
           // Similarity3d(1.0 / 100.0, Euclidean3d.Identity) * 
            Similarity3d(1.0, Euclidean3d(Rot3d.Identity, -bounds.Center))
            //Trafo3d.Translation(-bounds.Center) *
            //Trafo3d.Scale(1.0 / 100.0)

        Log.warn "bounds: %A" bounds.Size

        let source = Symbol.Create sourceName
        let root = PointTreeNode(store.Cache, source, trafo, None, None, 0, root) :> ILodTreeNode
        { 
            root = root
            uniforms = MapExt.ofList uniforms
        }
    
    let importAscii (sourceName : string) (file : string) (store : string) (uniforms : list<string * IMod>) =
        let fmt = [| Ascii.Token.PositionX; Ascii.Token.PositionY; Ascii.Token.PositionZ; Ascii.Token.ColorR; Ascii.Token.ColorG; Ascii.Token.ColorB |]
        let cache = LruDictionary(1L <<< 30)
        let store = PointCloud.OpenStore(store, cache)
        let key = System.IO.Path.GetFileNameWithoutExtension(file).ToLower()
        let set = store.GetPointSet(key)

        let points = 
            if isNull set then
                let cfg = 
                    ImportConfig.Default
                        .WithStorage(store)
                        .WithKey(key)
                        .WithVerbose(true)
                        .WithMaxChunkPointCount(10000000)
                        .WithEstimateNormals(Func<_,_>(fun a -> Aardvark.Geometry.Points.Normals.EstimateNormals(Seq.toArray a, 17) :> System.Collections.Generic.IList<V3f>))
                        
                let chunks = Import.Ascii.Chunks(file, fmt, cfg)
                let res = PointCloud.Chunks(chunks, cfg)
                store.Flush()
                res
            else
                set
                
        let bounds = points.Bounds

        Log.error "bounds: %A" points.Root.Value.BoundingBoxExact

        let trafo = 
            Similarity3d(1.0, Euclidean3d(Rot3d.Identity, -bounds.Center))

        Log.warn "points: %d" points.PointCount
        let source = Symbol.Create sourceName
        let root = PointTreeNode(cache, source, trafo, None, None, 0, points.Root.Value) :> ILodTreeNode
        { 
            root = root
            uniforms = MapExt.ofList uniforms
        }

    let normalize (maxSize : float) (instance : LodTreeInstance) =
        let tree = instance.root
        let uniforms = instance.uniforms

        let bounds = tree.BoundingBox
        let t = 
            Trafo3d.Translation(-bounds.Center) * 
            Trafo3d.Scale(maxSize / bounds.Size.NormMax)

        {
            root = tree
            uniforms = MapExt.add "ModelTrafo" (Mod.constant t :> IMod) uniforms
        }
        
    let translate (shift : V3d) (instance : LodTreeInstance) =
        let tree = instance.root
        let uniforms = instance.uniforms

        let bounds = tree.CellBoundingBox
        //Log.warn "%A %A" tree.BoundingBox tree.CellBoundingBox
        let t = Trafo3d.Translation(shift)

        {
            root = tree
            uniforms = MapExt.add "ModelTrafo" (Mod.constant t :> IMod) uniforms
        }

    let trafo (t : IMod<Trafo3d>) (instance : LodTreeInstance) =
        let old =
            match MapExt.tryFind "ModelTrafo" instance.uniforms with
            | Some (:? IMod<Trafo3d> as t) -> t
            | _ -> Mod.constant Trafo3d.Identity

        let trafo = Mod.map2 (*) old t

        { instance with uniforms = MapExt.add "ModelTrafo" (trafo :> IMod) instance.uniforms }





[<ReflectedDefinition>]
module Shader =
    open FShade


    let constantColor (c : C4f) (v : Effects.Vertex) =
        let c = c.ToV4d()
        vertex {
            return { v with c = c }
        }


    let heatMapColors =
        let fromInt (i : int) =
            C4b(
                byte ((i >>> 16) &&& 0xFF),
                byte ((i >>> 8) &&& 0xFF),
                byte (i &&& 0xFF),
                255uy
            ).ToC4f().ToV4d()

        Array.map fromInt [|
            0x1639fa
            0x2050fa
            0x3275fb
            0x459afa
            0x55bdfb
            0x67e1fc
            0x72f9f4
            0x72f8d3
            0x72f7ad
            0x71f787
            0x71f55f
            0x70f538
            0x74f530
            0x86f631
            0x9ff633
            0xbbf735
            0xd9f938
            0xf7fa3b
            0xfae238
            0xf4be31
            0xf29c2d
            0xee7627
            0xec5223
            0xeb3b22
        |]

    let heat (tc : float) =
        let tc = clamp 0.0 1.0 tc
        let fid = tc * float heatMapColors.Length - 0.5

        let id = int (floor fid)
        if id < 0 then 
            heatMapColors.[0]
        elif id >= heatMapColors.Length - 1 then
            heatMapColors.[heatMapColors.Length - 1]
        else
            let c0 = heatMapColors.[id]
            let c1 = heatMapColors.[id + 1]
            let t = fid - float id
            (c0 * (1.0 - t) + c1 * t)


    type UniformScope with
        member x.Overlay : V4d[] = x?StorageBuffer?Overlay
        member x.ModelTrafos : M44d[] = x?StorageBuffer?ModelTrafos
        member x.ModelViewTrafos : M44d[] = x?StorageBuffer?ModelViewTrafos

    type Vertex =
        {
            [<Position>] pos : V4d
            [<Normal>] n : V3d
            [<Semantic("Offsets")>] offset : V3d
        }


    let offset ( v : Vertex) =
        vertex {
            return  { v with pos = v.pos + V4d(v.offset, 0.0)}
        }
        
    
    type PointVertex =
        {
            [<Position>] pos : V4d
            [<Color>] col : V4d
            [<Normal>] n : V3d
            [<Semantic("ViewCenter"); Interpolation(InterpolationMode.Flat)>] vc : V3d
            [<Semantic("ViewPosition")>] vp : V3d
            [<Semantic("AvgPointDistance")>] dist : float
            [<Semantic("DepthRange")>] depthRange : float
            [<PointSize>] s : float
            [<PointCoord>] c : V2d
            [<FragCoord>] fc : V4d
            [<Semantic("TreeId")>] id : int
            [<Semantic("MaxTreeDepth")>] treeDepth : int
        }

    let lodPointSize (v : PointVertex) =
        vertex { 
            let mv = uniform.ModelViewTrafos.[v.id]
            let ovp = mv * v.pos
            let vp = ovp + V4d(0.0, 0.0, 0.5*v.dist, 0.0)

            
            let ppz = uniform.ProjTrafo * ovp
            let pp1 = uniform.ProjTrafo * (vp - V4d(0.5 * v.dist, 0.0, 0.0, 0.0))
            let pp2 = uniform.ProjTrafo * (vp + V4d(0.5 * v.dist, 0.0, 0.0, 0.0))
            let pp3 = uniform.ProjTrafo * (vp - V4d(0.0, 0.5 * v.dist, 0.0, 0.0))
            let pp4 = uniform.ProjTrafo * (vp + V4d(0.0, 0.5 * v.dist, 0.0, 0.0))

            let pp = uniform.ProjTrafo * vp
            
            let ppz = ppz.XYZ / ppz.W
            let pp0 = pp.XYZ / pp.W
            let d1 = pp1.XYZ / pp1.W - pp0 |> Vec.length
            let d2 = pp2.XYZ / pp2.W - pp0 |> Vec.length
            let d3 = pp3.XYZ / pp3.W - pp0 |> Vec.length
            let d4 = pp4.XYZ / pp4.W - pp0 |> Vec.length

            let ndcDist = 0.25 * (d1 + d2 + d3 + d4)
            let depthRange = abs (ppz.Z - pp0.Z)

            let pixelDist = ndcDist * float uniform.ViewportSize.X

            let s = mv * V4d(1.0, 0.0, 0.0, 0.0) |> Vec.xyz |> Vec.length

            let n = (mv * V4d(v.n, 0.0)) / s |> Vec.xyz
            
            let pixelDist = 
                if pp.Z < -pp.W then -1.0
                //elif abs pp.Z > 6.0 then min 30.0 (uniform.PointSize * pixelDist)
                else uniform.PointSize * pixelDist

            //let h = heat (float v.treeDepth / 6.0)
            //let o = uniform.Overlay.[v.id]
            //let col = o.W * h.XYZ + (1.0 - o.W) * v.col.XYZ
            let col = v.col.XYZ

            //let pixelDist = 
            //    if pixelDist > 30.0 then -1.0
            //    else pixelDist //min pixelDist 30.0

            return { v with s = pixelDist; pos = pp; depthRange = depthRange; n = n; vp = ovp.XYZ; vc = ovp.XYZ; col = V4d(col, v.col.W) }
        }



    type Fragment =
        {
            [<Color>] c : V4d
            [<Depth(DepthWriteMode.OnlyGreater)>] d : float
        }

    let lodPointCircular (v : PointVertex) =
        fragment {
            let c = v.c * 2.0 - V2d.II
            let f = Vec.dot c c
            if f > 1.0 then discard()


            let t = 1.0 - sqrt (1.0 - f)
            let depth = v.fc.Z
            let outDepth = depth + v.depthRange * t
            

            return { c = v.col; d = outDepth }
        }

    let cameraLight (v : PointVertex) =
        fragment {
            let lvn = Vec.length v.n
            let vn = v.n / lvn
            let vd = Vec.normalize v.vp 

            let c = v.c * V2d(2.0, 2.0) + V2d(-1.0, -1.0)
            let f = Vec.dot c c
            let z = sqrt (1.0 - f)
            let sn = V3d(c.X, c.Y, -z)


            let dSphere = Vec.dot sn vd |> abs
            let dPlane = Vec.dot vn vd |> abs

            let t = lvn
            let pp : float = uniform?Planeness
            let t = 1.0 - (1.0 - t) ** pp
            let color = heat t

            let diffuse = (1.0 - t) * dSphere + t * dPlane

            return V4d(v.col.XYZ * diffuse, v.col.W)
        }




    let normalColor ( v : Vertex) =
        fragment {
            let mutable n = Vec.normalize v.n
            if n.Z < 0.0 then n <- -n

            let n = (n + V3d.III) * 0.5
            return V4d(n, 1.0)
        }

module Sg =
    open Aardvark.Base.Ag
    open Aardvark.SceneGraph
    open Aardvark.SceneGraph.Semantics

    type LodNode(quality : IModRef<float>, maxQuality : IModRef<float>, budget : IMod<int64>, culling : bool, renderBounds : IMod<bool>, maxSplits : IMod<int>, time : IMod<DateTime>, clouds : aset<LodTreeInstance>) =
        member x.Culling = culling
        member x.Time = time
        member x.Clouds = clouds
        member x.MaxSplits = maxSplits

        member x.Quality = quality
        member x.MaxQuality = maxQuality
        member x.RenderBounds = renderBounds
        member x.Budget = budget
        interface ISg
      
    [<Semantic>]
    type Sem() =
        member x.RenderObjects(sg : LodNode) =
            let scope = Ag.getContext()
            let state = PipelineState.ofScope scope
            let surface = sg.Surface
            let pass = sg.RenderPass

            let model = sg.ModelTrafo
            let view = sg.ViewTrafo
            let proj = sg.ProjTrafo

            let id = newId()
            let obj =
                { new ICustomRenderObject with
                    member x.Id = id
                    member x.AttributeScope = scope
                    member x.RenderPass = pass
                    member x.Create(r, fbo) = 
                        match r with
                        | :? Aardvark.Rendering.GL.Runtime as r ->
                            let preparedState = Aardvark.Rendering.GL.PreparedPipelineState.ofPipelineState fbo r.ResourceManager surface state
                            
                            let info : Bla.LodRenderingInfo = 
                                {
                                    Bla.LodRenderingInfo.quality = sg.Quality
                                    Bla.LodRenderingInfo.maxQuality = sg.MaxQuality
                                    Bla.LodRenderingInfo.renderBounds = sg.RenderBounds
                                }
                            
                            new Bla.LodRenderer(r.Context, preparedState, pass, info, sg.Culling, sg.MaxSplits, sg.Clouds, sg.Time, model, view, proj, sg.Budget) :> IPreparedRenderObject
                        | _ ->
                            failwithf "[Lod] no vulkan backend atm."
                }

            ASet.single (obj :> IRenderObject)


module KdTree =
    
    type ClosestPointQuery =
        {
            point   : V3d
            maxDist : float
            count   : int
        }

    [<CompilationRepresentation(CompilationRepresentationFlags.UseNullAsTrueValue)>]
    type KdNode<'a> =
        | Empty
        | Inner of axis : int * point : V3d * value : 'a * left : KdNode<'a> * eq : KdNode<'a> * right : KdNode<'a> * count : int
        | Leaf of axis : int * points : array<struct (V3d * 'a)> * count : int

    type private KdNodeEnumerator<'a>(root : KdNode<'a>) =
        let mutable stack = [root]
        let mutable hasCurrentPoint : bool = false
        let mutable currentPoint : V3d = Unchecked.defaultof<_>
        let mutable currentValue : 'a = Unchecked.defaultof<_>
        let mutable currentArr : array<struct(V3d * 'a)> = null
        let mutable currentArrCnt : int = -1
        let mutable currentArrIndex : int = -1

        let push (e : KdNode<'a>) =
            match e with
            | Empty -> ()
            | _ -> stack <- e :: stack

        member x.Reset() =
            stack <- [root]
            hasCurrentPoint <- false
            currentArr <- null
            currentArrIndex <- -1
            
        member x.Dispose() =
            stack <- []
            hasCurrentPoint <- false
            currentArr <- null
            currentArrIndex <- -1

        member x.MoveNext() =
            if not (isNull currentArr) && not hasCurrentPoint then
                let n = 1 + currentArrIndex
                if n < currentArrCnt then
                    currentArrIndex <- n
                    true
                else
                    currentArr <- null
                    currentArrIndex <- -1
                    x.MoveNext()
            else
                match stack with
                | h :: rest ->
                    stack <- rest
                    match h with
                    | Inner(_,p,v,l,e,r,_) -> 
                        currentPoint <- p
                        currentValue <- v
                        hasCurrentPoint <- true
                        currentArr <- null
                        currentArrIndex <- -1
                        push r
                        push e
                        push l
                    | Leaf(_,pts,c) ->
                        currentArr <- pts
                        currentArrIndex <- 0
                        currentArrCnt <- c
                        hasCurrentPoint <- false
                    | _ ->
                        ()
                    true
                | [] ->
                    false

        member x.Current =
            if hasCurrentPoint then currentPoint, currentValue
            elif not (isNull currentArr) then 
                let struct (p,v) = currentArr.[currentArrIndex]
                (p,v)
            else failwith ""

        interface System.Collections.IEnumerator with
            member x.MoveNext() = x.MoveNext()
            member x.Reset() = x.Reset()
            member x.Current = x.Current :> obj
            
        interface System.Collections.Generic.IEnumerator<V3d * 'a> with
            member x.Current = x.Current
            member x.Dispose() = x.Dispose()
            
    type private KdNodeKeyEnumerator<'a>(root : KdNode<'a>) =
        let mutable stack = [root]
        let mutable hasCurrentPoint : bool = false
        let mutable currentPoint : V3d = Unchecked.defaultof<_>
        let mutable currentArr : array<struct(V3d * 'a)> = null
        let mutable currentArrCnt : int = -1
        let mutable currentArrIndex : int = -1

        let push (e : KdNode<'a>) =
            match e with
            | Empty -> ()
            | _ -> stack <- e :: stack

        member x.Reset() =
            stack <- [root]
            hasCurrentPoint <- false
            currentArr <- null
            currentArrIndex <- -1
            
        member x.Dispose() =
            stack <- []
            hasCurrentPoint <- false
            currentArr <- null
            currentArrIndex <- -1

        member x.MoveNext() =
            if not (isNull currentArr) && not hasCurrentPoint then
                let n = 1 + currentArrIndex
                if n < currentArrCnt then
                    currentArrIndex <- n
                    true
                else
                    currentArr <- null
                    currentArrIndex <- -1
                    x.MoveNext()
            else
                match stack with
                | h :: rest ->
                    stack <- rest
                    match h with
                    | Inner(_,p,v,l,e,r,_) -> 
                        currentPoint <- p
                        hasCurrentPoint <- true
                        currentArr <- null
                        currentArrIndex <- -1
                        push r
                        push e
                        push l
                    | Leaf(_,pts,c) ->
                        currentArr <- pts
                        currentArrIndex <- 0
                        currentArrCnt <- c
                        hasCurrentPoint <- false
                    | _ ->
                        ()
                    true
                | [] ->
                    false

        member x.Current =
            if hasCurrentPoint then currentPoint
            elif not (isNull currentArr) then 
                let struct (p,v) = currentArr.[currentArrIndex]
                p
            else failwith ""

        interface System.Collections.IEnumerator with
            member x.MoveNext() = x.MoveNext()
            member x.Reset() = x.Reset()
            member x.Current = x.Current :> obj
            
        interface System.Collections.Generic.IEnumerator<V3d> with
            member x.Current = x.Current
            member x.Dispose() = x.Dispose()

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module private KdNode =
        open Microsoft.FSharp.NativeInterop
        open Aardvark.Base.Sorting
        open Aardvark.Base.MultimethodTest
        open System.Runtime.InteropServices
        
        let isEmpty (node : KdNode<'a>) =
            match node with
            | Empty -> true
            | _ -> false

        let count (node : KdNode<'a>) =
            match node with
            | Empty -> 0
            | Leaf(_,_,c) -> c
            | Inner(_,_,_,_,_,_,c) -> c

        let leafLimit = 63

        let cmp<'a> = Func<struct(float*struct(V3d * 'a)), struct(float*struct(V3d*'a)), int>(fun struct(l,_) struct(r,_) -> compare r l)

        let inline enqueue (maxDist : ref<float>) (query : ClosestPointQuery) (heap : List<struct (float * struct(V3d*'a))>) (e : struct(V3d*'a)) =
            let struct (p,_) = e
            let d = V3d.Distance(p, query.point)

            if d <= !maxDist then
                heap.HeapEnqueue(cmp, struct(d,e))
                if heap.Count > query.count then 
                    heap.HeapDequeue(cmp) |> ignore
                    let struct(m,_) = heap.[0]
                    maxDist := m
                        
        let rec findArray (maxDist : ref<float>) (query : ClosestPointQuery) (heap : List<struct (float * struct(V3d*'a))>) (a : int) (l : int) (r : int) (arr : array<struct (V3d * 'a)>) =
            let c = r - l

            if c = 0 then
                ()

            elif c = 1 then
                enqueue maxDist query heap arr.[l]

            else
                let m = (l + r) / 2
                let e = arr.[m]
                let struct (p, _) = e
                let dimDist = query.point.[a] - p.[a]

                let na = if a = 2 then 0 else a + 1

                if dimDist > !maxDist then
                    findArray maxDist query heap na (m+1) r arr
                elif dimDist < -(!maxDist) then
                    findArray maxDist query heap na l m arr
                else
                    enqueue maxDist query heap e

                    if dimDist < 0.0 then
                        findArray maxDist query heap na l m arr
                        if dimDist >= -(!maxDist) then
                            findArray maxDist query heap na (m+1) r arr
                    else 
                        findArray maxDist query heap na (m+1) r arr
                        if dimDist <= !maxDist then
                            findArray maxDist query heap na l m arr
                   
        let rec find (maxDist : ref<float>) (query : ClosestPointQuery) (heap : List<struct (float * struct(V3d*'a))>) (node : KdNode<'a>) =
            match node with
            | Empty -> 
                ()

            | Leaf(a,p,c) ->
                findArray maxDist query heap a 0 c p

            | Inner(a,p,v,l,e,r,_) ->
                let dimDist = query.point.[a] - p.[a]

                if dimDist > !maxDist then
                    find maxDist query heap r

                elif dimDist < -(!maxDist) then
                    find maxDist query heap l

                else
                    enqueue maxDist query heap (struct(p,v))
                    if dimDist < 0.0 then
                        find maxDist query heap l
                        if dimDist >= -(!maxDist) then
                            find maxDist query heap e
                            if dimDist >= -(!maxDist) then
                                find maxDist query heap r
                    else 
                        find maxDist query heap r
                        if dimDist <= !maxDist then
                            find maxDist query heap e
                            if dimDist <= !maxDist then
                                find maxDist query heap l
            

        let private getX<'a> = Func<struct(V3d*'a), float>(fun struct (v,_) -> v.X)
        let private getY<'a> = Func<struct(V3d*'a), float>(fun struct (v,_) -> v.Y)
        let private getZ<'a> = Func<struct(V3d*'a), float>(fun struct (v,_) -> v.Z)

        let inline get (i : int) =
            match i with
            | 0 -> getX
            | 1 -> getY
            | _ -> getZ


        let rec buildArray (a : int) (o : int) (e : int) (arr : array<struct (V3d*'a)>) =
            let c = e - o
            if c > 1 then
                let m = (o + e) / 2
                // TODO: filter equal
                arr.QuickMedianAscending(get a, int64 o, int64 e, int64 m)
                let na = if a = 2 then 0 else a + 1
                buildArray na o m arr
                buildArray na (m+1) e arr

        let rec build (par : int) (a : int) (data : array<struct (V3d*'a)>) (o : int) (e : int) =
            let c = e - o
            if c > leafLimit then
                let m = (o + e) / 2
                data.QuickMedianAscending(get a, int64 o, int64 e, int64 m)
                let struct (p,value) = data.[m]
                let v = p.[a]
                    
                let mutable eq = null
                let mo = m
                let mutable m = m
                let mutable ec = 0
                let mutable rc = 0
                let mutable shift = 0
                for i in o .. e - 1 do
                    let struct (pt,vp) = data.[i]
                    if pt.[a] = v then 
                        if pt <> p then 
                            if isNull eq then eq <- Array.zeroCreate 8
                            elif ec >= eq.Length then System.Array.Resize(&eq, eq.Length * 2)
                            eq.[ec] <- struct(pt,vp); ec <- ec + 1
                        if i < mo then m <- m - 1
                        shift <- shift + 1
                    else 
                        if shift <> 0 then
                            data.[i - shift] <- data.[i]
                        rc <- rc + 1

                            
                let na = if a = 2 then 0 else a + 1 //((a + 1) % 3)

                let e = build (par - 1) na eq 0 ec
                let mutable l = Empty
                let mutable r = Empty

                if par > 0 then
                    Parallel.Invoke [|
                        Action(fun () -> l <- build (par - 1) na data o m)
                        Action(fun () -> r <- build (par - 1) na data m (o + rc))
                    |]
                else
                    l <- build (par - 1) na data o m
                    r <- build (par - 1) na data m (o + rc)

                Inner(
                    a, p, value,
                    l, e, r,
                    1 + count l + count e + count r
                )
            elif c > 0 then
                let set = HashSet<V3d>(c)
                let arr = Array.zeroCreate (e - o)
                let mutable j = 0
                for i in o .. e - 1 do
                    let p = data.[i]
                    let struct (pt,_) = p
                    if set.Add pt then
                        arr.[j] <- p
                        j <- j + 1
                    
                //let arr = Array.sub data o (e - o)
                buildArray a 0 j arr
                Leaf(a, arr, j)
            else
                Empty
                
        let rec private fillArray (arr : array<struct (V3d * 'a)>) (i : int) (n : KdNode<'a>) =
            match n with
            | Empty -> ()
            | Leaf(_,p,c) -> 
                let mutable i = i
                for j in 0 .. c - 1 do
                    arr.[i] <- p.[j]
                    i <- i + 1

            | Inner(_,p,v,l,e,r,c) ->
                let lc = count l
                let ec = count e
                arr.[i] <- struct(p,v)
                fillArray arr (i + 1) l
                fillArray arr (i + 1 + lc) e
                fillArray arr (i + 1 + ec + lc) r

        let toArray (n : KdNode<'a>) =
            let cnt = count n
            let res = Array.zeroCreate cnt
            fillArray res 0 n
            res

        let toArrayMany (ns : list<KdNode<'a>>) =
            let cnt = ns |> List.sumBy count
            let arr = Array.zeroCreate cnt

            let mutable offset = 0
            for n in ns do
                fillArray arr offset n
                offset <- offset + count n
            arr

        let rec join (a : int) (pt : V3d) (value : 'a) (l : KdNode<'a>) (e : KdNode<'a>) (r : KdNode<'a>) =
            let lc = count l
            let rc = count r
            let ec = count e
            let cnt = ec + lc + rc + 1

            if (lc > 2*rc + 1) || (rc > 2*lc + 1) then
                let arr = Array.zeroCreate cnt
                arr.[0] <- struct(pt,value)
                fillArray arr 1 l
                fillArray arr (1 + lc) r
                fillArray arr (1 + lc + rc) e
                build 0 a arr 0 arr.Length
            else
                Inner(a, pt, value, l, e, r, cnt)
                
        let rec tryFindArray (a : int) (l : int) (r : int) (pt : V3d) (arr : array<struct(V3d*'a)>) =
            let c = r - l
            if c = 0 then
                -1
            elif c = 1 then
                let struct (p,_) = arr.[l]
                if p = pt then l
                else -1
            else
                let m = (l + r) / 2
                let struct(pm,_) = arr.[m]
                let cmp = compare pt.[a] pm.[a]
                let na = if a = 2 then 0 else a + 1
                if cmp = 0 then
                    if pm = pt then  
                        m
                    else
                        let li = tryFindArray na l m pt arr
                        if li < 0 then tryFindArray na (m+1) r pt arr
                        else li
                elif cmp > 0 then
                    tryFindArray na (m+1) r pt arr
                else
                    tryFindArray na l m pt arr

        let rec contains (pt : V3d) (node : KdNode<'a>) =
            match node with
            | Empty -> 
                false
            | Leaf(a,pts,c) ->
                let id = tryFindArray a 0 c pt pts
                id >= 0
            | Inner(a,p,v,l,e,r,_) ->
                if p = pt then 
                    true
                else
                    let cmp = compare pt.[a] p.[a]
                    if cmp = 0 then contains pt e
                    elif cmp > 0 then contains pt r
                    else contains pt l

        let rec add (a : int) (pt : V3d) (value : 'a) (node : KdNode<'a>) =
            match node with
            | Empty ->
                Leaf(a, [|struct(pt,value)|], 1)

            | Leaf(a,pts,c) ->
                let id = tryFindArray a 0 c pt pts
                if id < 0 then
                    let n = Array.zeroCreate (c + 1)
                    for i in 0 .. c - 1 do n.[i] <- pts.[i]
                    n.[c] <- struct(pt,value)
                    if pts.Length < leafLimit then
                        buildArray a 0 n.Length n
                        Leaf(a, n, n.Length)
                    else
                        build 0 a n 0 n.Length
                else
                    node
               
            | Inner(a, p, v, l, e, r, c) ->
                let cmp = compare pt.[a] p.[a]
                let na = if a = 2 then 0 else a + 1
                if cmp > 0 then
                    let r' = add na pt value r
                    if r == r' then node
                    else join a p v l e r'
                elif cmp < 0 then
                    let l' = add na pt value l
                    if l == l' then node
                    else join a p v l' e r
                elif p = pt then
                    if Unchecked.equals v value then node
                    else Inner(a,p,value,l,e,r,c)
                else
                    let e' = add na pt value e
                    if e == e' then node
                    else join a p v l e' r    

        let rec remove (a : int) (pt : V3d) (node : KdNode<'a>) =
            match node with
            | Empty ->
                node

            | Leaf(a,pts,c) ->
                let id = tryFindArray a 0 c pt pts
                if id < 0 then
                    node
                else
                    if c > 1 then
                        let res = Array.zeroCreate (c - 1)
                        let mutable i = 0
                        let mutable j = 0
                        while i < pts.Length do
                            if i <> id then
                                res.[j] <- pts.[i]
                                j <- j + 1
                            i <- i + 1
                        buildArray a 0 res.Length res
                        Leaf(a, res, res.Length)
                    else
                        Empty
               
            | Inner(a, p, v, l, e, r, c) ->
                let cmp = compare pt.[a] p.[a]
                let na = if a = 2 then 0 else a + 1
                if cmp > 0 then
                    let r' = remove na pt r
                    if r == r' then node
                    else join a p v l e r'
                elif cmp < 0 then
                    let l' = remove na pt l
                    if l == l' then node
                    else join a p v l' e r
                elif p = pt then
                    let arr = toArrayMany [l;e;r]
                    build 0 a arr 0 arr.Length
                else
                    let e' = remove na pt e
                    if e == e' then node
                    else join a p v l e' r    


    type KdDict<'a>(root : KdNode<'a>) =
        static let empty : KdDict<'a> = KdDict(KdNode.Empty)

        static member Empty = empty

        member x.IsEmpty = KdNode.isEmpty root
        member x.Count = KdNode.count root
        member internal x.Root = root
        interface System.Collections.IEnumerable with
            member x.GetEnumerator() = new KdNodeEnumerator<'a>(root) :> _
            
        interface System.Collections.Generic.IEnumerable<V3d * 'a> with
            member x.GetEnumerator() = new KdNodeEnumerator<'a>(root) :> _

    module KdDict =
        let empty<'a> = KdDict<'a>.Empty

        let isEmpty (t : KdDict<'a>) =
            KdNode.isEmpty t.Root

        let count (t : KdDict<'a>) =
            KdNode.count t.Root

        let inline private ofArrayInPlace (pts : array<struct(V3d*'a)>) =
            KdDict(KdNode.build 4 0 pts 0 pts.Length)
            
        let ofSeq (pts : seq<V3d * 'a>) =
            let pts = pts |> Seq.map (fun (p,v) -> struct(p,v)) |> Seq.toArray
            ofArrayInPlace pts

        let ofList (pts : list<V3d * 'a>) =
            let pts = pts |> List.map (fun (p,v) -> struct(p,v)) |> List.toArray
            ofArrayInPlace pts
            
        let ofArray (pts : array<V3d * 'a>) =
            let pts = pts |> Array.map (fun (p,v) -> struct(p,v))
            ofArrayInPlace pts
          
        let toSeq (t : KdDict<'a>) = t :> seq<_>
        let toList (t : KdDict<'a>) = t |> Seq.toList
        let toArray (t : KdDict<'a>) = t |> Seq.toArray
        
        let add (pt : V3d) (value : 'a) (tree : KdDict<'a>) =
            let res = KdNode.add 0 pt value tree.Root
            if res == tree.Root then tree
            else KdDict(res)
            
        let remove (pt : V3d) (tree : KdDict<'a>) =
            let res = KdNode.remove 0 pt tree.Root
            if res == tree.Root then tree
            else KdDict(res)

        let contains (pt : V3d) (tree : KdDict<'a>) =
            KdNode.contains pt tree.Root

        let findClosest (query : ClosestPointQuery) (tree : KdDict<'a>) =
            let maxDist = ref query.maxDist
            let heap = List<struct (float * struct(V3d * 'a))>(1 + query.count)
            KdNode.find maxDist query heap tree.Root

            let arr = Array.zeroCreate heap.Count
            for i in 1 .. arr.Length do
                let j = arr.Length - i
                let struct(d,struct(p,v)) = heap.HeapDequeue(KdNode.cmp)
                arr.[j] <- p,v

            arr


    type KdSet(root : KdNode<int>) =
        static let empty : KdSet = KdSet(KdNode.Empty)

        static member Empty = empty
        member internal x.Root = root
        
        member x.IsEmpty = KdNode.isEmpty root
        member x.Count = KdNode.count root
        interface System.Collections.IEnumerable with
            member x.GetEnumerator() = new KdNodeKeyEnumerator<int>(root) :> _
            
        interface System.Collections.Generic.IEnumerable<V3d> with
            member x.GetEnumerator() = new KdNodeKeyEnumerator<int>(root) :> _

    module KdSet =
        let empty = KdSet.Empty
        

        let isEmpty (t : KdSet) =
            KdNode.isEmpty t.Root

        let count (t : KdSet) =
            KdNode.count t.Root

        let inline private ofArrayInPlace (pts : array<struct(V3d * int)>) =
            KdSet(KdNode.build 4 0 pts 0 pts.Length)
            
        let ofSeq (pts : seq<V3d>) =
            let pts = pts |> Seq.mapi (fun i p -> struct(p,i)) |> Seq.toArray
            ofArrayInPlace pts

        let ofList (pts : list<V3d>) =
            let pts = pts |> List.mapi (fun i p -> struct(p,i)) |> List.toArray
            ofArrayInPlace pts
            
        let ofArray (pts : array<V3d>) =
            let pts = pts |> Array.mapi (fun i p -> struct(p,i))
            ofArrayInPlace pts

        let toSeq (t : KdSet) = t :> seq<_>
        let toList (t : KdSet) = t |> Seq.toList
        let toArray (t : KdSet) = t |> Seq.toArray
        
        let add (pt : V3d) (tree : KdSet) =
            let res = KdNode.add 0 pt 0 tree.Root
            if res == tree.Root then tree
            else KdSet(res)
            
        let remove (pt : V3d) (tree : KdSet) =
            let res = KdNode.remove 0 pt tree.Root
            if res == tree.Root then tree
            else KdSet(res)

        let contains (pt : V3d) (tree : KdSet) =
            KdNode.contains pt tree.Root

        let findClosest (query : ClosestPointQuery) (tree : KdSet) =
            let maxDist = ref query.maxDist
            let heap = List<struct (float * struct(V3d * int))>(1 + query.count)
            KdNode.find maxDist query heap tree.Root

            let arr = Array.zeroCreate heap.Count
            for i in 1 .. arr.Length do
                let j = arr.Length - i
                let struct(d,struct(p,v)) = heap.HeapDequeue(KdNode.cmp)
                arr.[j] <- p

            arr


open KdTree
open Aardvark.Geometry

let timed (name : string) (f : unit -> int) =
    let sw = System.Diagnostics.Stopwatch.StartNew()
    let iter =  f()
    sw.Stop()
    Log.line "%s: %A" name (sw.MicroTime / iter)

[<EntryPoint>]
let main argv = 

    System.Threading.ThreadPool.SetMinThreads(16,16) |> ignore
    System.Threading.ThreadPool.SetMaxThreads(16,16) |> ignore

    let n = 500000
    let buildIter = 6
    let findIter = 1000000
    let k = 5

    let rand = RandomSystem()
    let box = Box3d(-1000.0 * V3d.III, 1000.0 * V3d.III)
    let data = Array.init n (fun i -> rand.UniformV3d(box))
    //let simpleData = Array.map fst data
    //let data = Array.append data data
    let mutable a = KdSet.ofArray data
    timed "build mine" (fun () ->
        for i in 1 .. buildIter do
            a <- KdSet.ofArray data
        buildIter
    )
    
    let mutable ref = data.CreateKdTree(Metric.Euclidean, 1E-40)
    timed "build rft " (fun () ->
        for i in 1 .. buildIter do
            ref <- data.CreateKdTree(Metric.Euclidean, 1E-40)
        buildIter
    )
    
    let q = { count = k; maxDist = Double.PositiveInfinity; point = V3d.Zero }
    let mutable mine = [||]
    let mutable rft = Unchecked.defaultof<_>

    for i in 1 .. 8 do
        mine <- KdSet.findClosest q a
        
    let queryPoints = Array.init findIter (fun _ -> rand.UniformV3d(box))

    timed "search mine" (fun () ->
        for i in 0 .. findIter - 1 do
            mine <- KdSet.findClosest { q with point = queryPoints.[i] } a
        findIter
    )
       
    timed "search rft " (fun () ->
        for i in 0 .. findIter - 1 do
            rft <- ref.GetClosest(ref.CreateClosestToPointQuery(Double.PositiveInfinity, k), queryPoints.[i])
        findIter
    )
 
    let rft = rft |> Seq.sortBy (fun id -> id.Dist) |> Seq.map (fun id -> data.[int id.Index]) |> Seq.toArray
    
    if mine <> rft then
        Log.warn "ERROR"

    Log.start "mine"
    for (m) in mine do
        Log.line "%A (%f)" m (V3d.Distance(m, queryPoints.[queryPoints.Length - 1]))
    Log.stop()

    Log.start "rft"
    for (m) in rft do
        Log.line "%A (%f)" m (V3d.Distance(m, queryPoints.[queryPoints.Length - 1]))
    Log.stop()

    //let test() =
    //    let pt = rand.UniformV3d(box)

    //    let mine = KdTree.find { count = k; maxDist = Double.PositiveInfinity; point = pt } a
    //    let rft = ref.GetClosest(ref.CreateClosestToPointQuery(Double.PositiveInfinity, k), pt)
    //    let rft = rft |> Seq.sortBy (fun id -> id.Dist) |> Seq.map (fun id -> data.[int id.Index]) |> Seq.toArray

    //    if mine <> rft then
    //        Log.warn "bad %A vs %A" mine rft
    //    else
    //        Log.line "good"
    //for i in 1 .. 100 do
    //    test()

    //LodAgain.FileDict.test()
    System.Environment.Exit 0
    //System.Runtime.GCSettings.LatencyMode <- System.Runtime.GCLatencyMode.LowLatency


    let path, key =
        if argv.Length < 2 then
            @"C:\Users\Schorsch\Development\WorkDirectory\jb", @"C:\Users\Schorsch\Development\WorkDirectory\Laserscan-P20_Beiglboeck-2015.pts"
        else
            argv.[0], argv.[1]
            

            
    Ag.initialize()
    Aardvark.Init()
    
    let win =
        window {
            backend Backend.GL
            device DeviceKind.Dedicated
            display Display.Mono
            debug false
        }

    let pointSize = Mod.init 1.0
    let overlayAlpha = Mod.init 0.0
         
    let c0 = Mod.init V4d.IOOI
    let c1 = Mod.init V4d.OIOI
    let active0 = Mod.init true
    let active1 = Mod.init true
    let maxSplits = Mod.init 8
    let renderBounds = Mod.init true

    let c0WithAlpha = Mod.map2 (fun (c : V4d) (a : float) -> V4d(c.XYZ, a)) c0 overlayAlpha
    let c1WithAlpha = Mod.map2 (fun (c : V4d) (a : float) -> V4d(c.XYZ, a)) c1 overlayAlpha

    let trafo = Mod.init Trafo3d.Identity
    let trafo2 = Mod.init (Trafo3d.Translation(V3d(100,0,0)))
    let trafo3 = Mod.init (Trafo3d.Translation(V3d(0,100,0)))

    //let oktogon =
    //    StoreTree.import
    //        "ssd"
    //        @"\\euclid\rmDATA\Data\Schottenring_2018_02_23\Laserscans\2018-02-27_BankAustria\export\oktogon\Punktwolke\BLKgesamt.e57"
    //        @"C:\Users\Schorsch\Development\WorkDirectory\BLK"
    //        [
    //            "Overlay", c0WithAlpha :> IMod
    //            "ModelTrafo", trafo2 :> IMod
    //            "TreeActive", active0 :> IMod
    //        ]
            
    //let kaunertal =
    //    StoreTree.importAscii
    //        "ssd"
    //        @"C:\Users\Schorsch\Development\WorkDirectory\Kaunertal.txt"
    //        @"\\euclid\InOut\haaser\KaunertalNormals"
    //        [
    //            "Overlay", c1WithAlpha :> IMod
    //            "ModelTrafo", trafo :> IMod
    //            "TreeActive", active1 :> IMod
    //        ]
    //let kaunertal =
    //    StoreTree.import
    //        "ssd1"
    //        key
    //        path
    //        [
    //            "Overlay", c1WithAlpha :> IMod
    //            "ModelTrafo", trafo :> IMod
    //            "TreeActive", active1 :> IMod
    //        ]
               
    let jb1 =
        StoreTree.import
            "ssd2"
            @"C:\Users\Schorsch\Development\WorkDirectory\Laserscan-P20_Beiglboeck-2015.pts"
            @"C:\Users\Schorsch\Development\WorkDirectory\jb"
            [
                "Overlay", c0WithAlpha :> IMod
                "ModelTrafo", trafo2 :> IMod
                "TreeActive", active0 :> IMod
            ]
               
                         
    let jb2 =
        StoreTree.import
            "ssd1"
            @"C:\Users\Schorsch\Development\WorkDirectory\Laserscan-P20_Beiglboeck-2015.pts"
            @"C:\Users\Schorsch\Development\WorkDirectory\jb2"
            [
                "Overlay", c1WithAlpha :> IMod
                "ModelTrafo", trafo :> IMod
                "TreeActive", active1 :> IMod
            ]
                      
    //let supertoll =
    //    StoreTree.importAscii
    //        "ssd"
    //        @"C:\Users\Schorsch\Development\WorkDirectory\supertoll.txt"
    //        @"C:\Users\Schorsch\Development\WorkDirectory\supertoll"
    //        [
    //            "Overlay", c0WithAlpha :> IMod
    //            "ModelTrafo", trafo :> IMod
    //            "TreeActive", active1 :> IMod
    //        ]

    //Log.startTimed "asdasdasd"
    //StoreTree.gc 
    //    @"C:\Users\Schorsch\Development\WorkDirectory\KaunertalNormals"
    //    "kaunertal"
    //    @"\\euclid\InOut\haaser\KaunertalNormals"

    //Log.stop()
    //System.Environment.Exit 0

    //let technologiezentrum =
    //    StoreTree.import
    //        "ssd"
    //        @"C:\Users\Schorsch\Development\WorkDirectory\Technologiezentrum_Teil1.pts"
    //        @"C:\Users\Schorsch\Development\WorkDirectory\Technologiezentrum2"
    //        [
    //            "Overlay", c0WithAlpha :> IMod
    //            "ModelTrafo", trafo3 :> IMod
    //            "TreeActive", active1 :> IMod
    //        ]
            
    //let thread = 
    //    startThread (fun () ->  
    //        let mm = new MultimediaTimer.Trigger(1)
    //        let sw = System.Diagnostics.Stopwatch.StartNew()
    //        let mutable lastTime = sw.MicroTime
    //        while true do
    //            mm.Wait()
    //            let now = sw.MicroTime
    //            let dt = now - lastTime
    //            lastTime <- now

    //            transact (fun () -> trafo.Value <- trafo.Value * Trafo3d.RotationZ(dt.TotalSeconds * 0.1))
    //    )
    //let koelnNet =
    //    StoreTree.import
    //        "net"
    //        @"\\heap.vrvis.lan\haaser\koeln\cells\3277_5518_0_10\3277_5518_0_10"
    //        @"\\heap.vrvis.lan\haaser\koeln\cells\3277_5518_0_10\pointcloud\"
    //        [
    //            "Overlay", c0WithAlpha :> IMod
    //            "ModelTrafo", trafo2 :> IMod
    //            "TreeActive", active0 :> IMod
    //        ]
        

    //let koeln =
    //    StoreTree.import
    //        "ssd"
    //        @"C:\Users\Schorsch\Development\WorkDirectory\3278_5514_0_10\3278_5514_0_10"
    //        @"C:\Users\Schorsch\Development\WorkDirectory\3278_5514_0_10\pointcloud\"
    //        [
    //            "Overlay", c0WithAlpha :> IMod
    //            "ModelTrafo", trafo :> IMod
    //            "TreeActive", active0 :> IMod
    //        ]

    //let jb =
    //    StoreTree.import
    //        "ssd"
    //        @"C:\Users\Schorsch\Development\WorkDirectory\Laserscan-P20_Beiglboeck-2015.pts"
    //        @"C:\Users\Schorsch\Development\WorkDirectory\jb"
    //        [
    //            "Overlay", c0WithAlpha :> IMod
    //            "ModelTrafo", trafo :> IMod
    //            "TreeActive", active0 :> IMod
    //        ]
    //let rec traverse (n : ILodTreeNode) =
    //    n.Acquire()
    //    n.Children |> Seq.iter traverse

    //traverse technologiezentrum.root



    //let allKoeln =
    //    let rx = System.Text.RegularExpressions.Regex @"^(?<x>[0-9]+)_(?<y>[0-9]+)_(?<z>[0-9]+)_(?<w>[0-9]+)$"
        
    //    let stores = 
    //        System.IO.Directory.GetDirectories @"\\heap.vrvis.lan\haaser\koeln\cells"
    //        |> Seq.skip 100
    //        |> Seq.atMost 20
    //        |> Seq.choose (fun path ->
    //            let name = System.IO.Path.GetFileNameWithoutExtension path
    //            let m = rx.Match name
    //            if m.Success then
    //                let x = m.Groups.["x"].Value |> int64
    //                let y = m.Groups.["y"].Value |> int64
    //                let z = m.Groups.["z"].Value |> int64
    //                let exp = m.Groups.["w"].Value |> int
    //                Log.warn "%d_%d_%d_%d" x y z exp
    //                let cell = Cell(x,y,z,exp)
    //                Some(cell, name, System.IO.Path.Combine(path, "pointcloud"))
    //            else
    //                None
    //        )
    //        |> Seq.toList

    //    let bounds = stores |> Seq.map (fun (v,_,_) -> v.BoundingBox) |> Box3d

    //    let rand = RandomSystem()
    //    stores |> List.choose (fun (cell,name,path) ->
    //        let color = rand.UniformC3f().ToV4d()
    //        let col = Mod.map (fun (a : float) -> V4d(color.XYZ, a)) overlayAlpha

    //        try
    //            let tree, uniforms = 
    //                StoreTree.import
    //                    "net"
    //                    name
    //                    path
    //                    [
    //                        "Overlay", col :> IMod
    //                        //"ModelTrafo", Mod.constant trafo :> IMod
    //                        "TreeActive", Mod.constant true :> IMod
    //                    ]

    //            let box = tree.Cell.BoundingBox

    //            Log.warn "box: %A" box.Size
    //            let trafo = 
    //                Trafo3d.Scale(100.0) *
    //                Trafo3d.Translation(box.Center - bounds.Center) *
    //                Trafo3d.Scale(1000.0 / bounds.Size.NormMax)
    //                //Trafo3d.Scale(0.05)
    //                //Trafo3d.Translation(0.0, 0.0, box.Center.Z * 0.05)
                
    //            let uniforms = MapExt.add "ModelTrafo" (Mod.constant trafo :> IMod) uniforms

    //            Some (tree, uniforms)
    //        with _ ->
    //            None
    //    )


    


    let center = jb1.root.BoundingBox.Center

    let pcs = 
        //ASet.ofList allKoeln
        ASet.ofList [
            yield StoreTree.translate (-center) jb1
                    //|> StoreTree.trafo trafo
            yield StoreTree.translate (-center) jb2
                    //|> StoreTree.trafo trafo
            //yield StoreTree.normalize 100.0 koelnNet
            //yield StoreTree.normalize 100.0 kaunertal
            //yield StoreTree.normalize 100.0 technologiezentrum |> StoreTree.trafo trafo
            //yield oktogon
        ]
        
    let budget = Mod.init (1L <<< 30)
    let quality = Mod.init 0.0
    let maxQuality = Mod.init 1.0

    let heatMapColors =
        let fromInt (i : int) =
            C4b(
                byte ((i >>> 16) &&& 0xFF),
                byte ((i >>> 8) &&& 0xFF),
                byte (i &&& 0xFF),
                255uy
            )

        Array.map fromInt [|
            0x1639fa
            0x2050fa
            0x3275fb
            0x459afa
            0x55bdfb
            0x67e1fc
            0x72f9f4
            0x72f8d3
            0x72f7ad
            0x71f787
            0x71f55f
            0x70f538
            0x74f530
            0x86f631
            0x9ff633
            0xbbf735
            0xd9f938
            0xf7fa3b
            0xfae238
            0xf4be31
            0xf29c2d
            0xee7627
            0xec5223
            0xeb3b22
        |]

    let heat (tc : float) =
        let tc = clamp 0.0 1.0 tc
        let fid = tc * float heatMapColors.Length - 0.5

        let id = int (floor fid)
        if id < 0 then 
            heatMapColors.[0]
        elif id >= heatMapColors.Length - 1 then
            heatMapColors.[heatMapColors.Length - 1]
        else
            let c0 = heatMapColors.[id].ToC4f()
            let c1 = heatMapColors.[id + 1].ToC4f()
            let t = fid - float id
            (c0 * (1.0 - t) + c1 * t).ToC4b()


    let overlay =
        let p1 = RenderPass.after "p1" RenderPassOrder.Arbitrary RenderPass.main
        let p2 = RenderPass.after "p2" RenderPassOrder.Arbitrary p1
        let p3 = RenderPass.after "p3" RenderPassOrder.Arbitrary p2
        
        let color = quality |> Mod.map (fun q -> heat (1.0 - q))

        let scale = Trafo3d.Scale(0.3, 0.05, 0.05)
        Sg.ofList [
            Sg.box color (Mod.constant Box3d.Unit)
                |> Sg.trafo (quality |> Mod.map (fun q -> scale * Trafo3d.Scale(q, 1.0, 1.0))) 
                |> Sg.translate -1.0 -1.0 0.0
                |> Sg.viewTrafo (Mod.constant Trafo3d.Identity)
                |> Sg.projTrafo (Mod.constant Trafo3d.Identity)
                |> Sg.pass p3
                |> Sg.depthTest (Mod.constant DepthTestMode.None)
                
            Sg.box' (C4b(25,25,25,255)) Box3d.Unit
                |> Sg.trafo (maxQuality |> Mod.map (fun q -> scale * Trafo3d.Scale(q, 1.0, 1.0))) 
                |> Sg.translate -1.0 -1.0 0.0
                |> Sg.viewTrafo (Mod.constant Trafo3d.Identity)
                |> Sg.projTrafo (Mod.constant Trafo3d.Identity)
                |> Sg.pass p2
                |> Sg.depthTest (Mod.constant DepthTestMode.None)

            Sg.box' C4b.Gray Box3d.Unit
                |> Sg.transform scale
                |> Sg.translate -1.0 -1.0 0.0
                |> Sg.viewTrafo (Mod.constant Trafo3d.Identity)
                |> Sg.projTrafo (Mod.constant Trafo3d.Identity)
                |> Sg.pass p1
                |> Sg.depthTest (Mod.constant DepthTestMode.None)
        ]
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.vertexColor
        }
    let planeness = Mod.init 1.0
    let sg =
        Sg.LodNode(quality, maxQuality, budget, true, renderBounds, maxSplits, win.Time, pcs) :> ISg
        |> Sg.uniform "PointSize" pointSize
        |> Sg.uniform "ViewportSize" win.Sizes
        |> Sg.uniform "Planeness" planeness
        |> Sg.shader {
            //do! Shader.constantColor C4f.White
            do! Shader.lodPointSize
            do! Shader.cameraLight
            do! Shader.lodPointCircular
        }
        |> Sg.andAlso overlay

    win.Keyboard.DownWithRepeats.Values.Add(fun k ->
        match k with
        | Keys.O -> transact (fun () -> pointSize.Value <- pointSize.Value / 1.3)
        | Keys.P -> transact (fun () -> pointSize.Value <- pointSize.Value * 1.3)
        | Keys.Subtract | Keys.OemMinus -> transact (fun () -> overlayAlpha.Value <- max 0.0 (overlayAlpha.Value - 0.1))
        | Keys.Add | Keys.OemPlus -> transact (fun () -> overlayAlpha.Value <- min 1.0 (overlayAlpha.Value + 0.1))

        | Keys.Left -> transact (fun () -> trafo.Value <- trafo.Value * Trafo3d.Translation(-20.0, 0.0, 0.0))
        | Keys.Right -> transact (fun () -> trafo.Value <- trafo.Value * Trafo3d.Translation(20.0, 0.0, 0.0))

        | Keys.D1 -> transact (fun () -> active0.Value <- not active0.Value); printfn "active0: %A" active0.Value
        | Keys.D2 -> transact (fun () -> active1.Value <- not active1.Value); printfn "active1: %A" active1.Value

        | Keys.Up -> transact (fun () -> maxSplits.Value <- maxSplits.Value + 1); printfn "splits: %A" maxSplits.Value
        | Keys.Down -> transact (fun () -> maxSplits.Value <- max 1 (maxSplits.Value - 1)); printfn "splits: %A" maxSplits.Value

        | Keys.C -> transact (fun () -> budget.Value <- 2L * budget.Value); Log.line "budget: %A" (Num budget.Value)
        | Keys.X -> transact (fun () -> budget.Value <- max (budget.Value / 2L) (256L <<< 10)); Log.line "budget: %A" (Num budget.Value)
        
        | Keys.G -> transact (fun () -> planeness.Value <- planeness.Value * 1.15); Log.line "planeness: %f" (planeness.Value)
        | Keys.H -> transact (fun () -> planeness.Value <- planeness.Value / 1.15); Log.line "planeness: %f" (planeness.Value)


        | Keys.B -> transact (fun () -> renderBounds.Value <- not renderBounds.Value); Log.line "bounds: %A" renderBounds.Value

        | Keys.Space -> 
            transact (fun () -> 
                let v = c0.Value
                c0.Value <- c1.Value
                c1.Value <- v
            )

        | _ -> 
            ()
    )
    

    win.Scene <- sg
    win.Run()

    0
