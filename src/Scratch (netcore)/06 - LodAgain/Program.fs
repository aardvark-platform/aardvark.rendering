open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.Base.Incremental.Operators
open Aardvark.SceneGraph
open Aardvark.Rendering.Text
open Aardvark.Application

#nowarn "9"


module KdTree =
    open System
    open System.Collections.Generic
    open System.Threading.Tasks

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

module KdTreeTest = 
    open System
    open KdTree
    open Aardvark.Geometry

    let timed (name : string) (f : unit -> int) =
        let sw = System.Diagnostics.Stopwatch.StartNew()
        let iter =  f()
        sw.Stop()
        Log.line "%s: %A" name (sw.MicroTime / iter)


    let run() =

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




module StoreTree =
    open System
    open System.Collections.Generic
    open System.Threading
    open System.Threading.Tasks
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

    type LodNode(quality : IModRef<float>, maxQuality : IModRef<float>, budget : IMod<int64>, culling : bool, renderBounds : IMod<bool>, maxSplits : IMod<int>, time : IMod<System.DateTime>, clouds : aset<LodTreeInstance>) =
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
                        r.CreateLodRenderer(fbo, surface, state, pass, model, view, proj, sg.Quality, sg.MaxQuality, sg.Budget, sg.RenderBounds, sg.MaxSplits, sg.Time, sg.Clouds)
                }

            ASet.single (obj :> IRenderObject)


[<EntryPoint>]
let main argv = 

    Ag.initialize()
    Aardvark.Init()
    
    let path, key =
        if argv.Length < 2 then
            @"C:\Users\Schorsch\Development\WorkDirectory\jb", @"C:\Users\Schorsch\Development\WorkDirectory\Laserscan-P20_Beiglboeck-2015.pts"
        else
            argv.[0], argv.[1]
            

            
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

        | Keys.C -> transact (fun () -> budget.Value <- 2L * budget.Value); Log.line "budget: %A" budget.Value
        | Keys.X -> transact (fun () -> budget.Value <- max (budget.Value / 2L) (256L <<< 10)); Log.line "budget: %A" budget.Value
        
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
