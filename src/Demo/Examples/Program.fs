module Program

open System
open System.IO
open Rendering.Examples

open System.Runtime.InteropServices
open System.Diagnostics
open System.Threading
open Microsoft.FSharp.NativeInterop
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.SceneGraph

module TreeDiff =
    open System.Collections.Generic

    type IUpdater<'op> =
        inherit IAdaptiveObject
        inherit IDisposable
        abstract member Update : AdaptiveToken -> 'op

    type Neighbourhood<'a> =
        {
            prev : Option<'a>
            self : Option<'a>
            next : Option<'a>
        }

    let (|InsertAfter|InsertBefore|InsertFirst|Update|) (n : Neighbourhood<'a>) =
        match n.self with
            | Some s -> Update s
            | None -> 
                match n.prev, n.next with
                    | Some p, _ -> InsertAfter(p)
                    | _, Some n -> InsertBefore(n)
                    | None, None -> InsertFirst

    
    [<Struct; CustomEquality; CustomComparison>]
    type Id private(value : int) =
        
        static let mutable currentValue = 0

        member private x.Value = value


        override x.ToString() = 
            match value with
                | -1 -> "invalid"
                | 0 -> "root"
                | _ -> "n" + string value

        override x.GetHashCode() = value
        override x.Equals o =
            match o with
                | :? Id as o -> o.Value = value
                | _ -> false
                
        member x.CompareTo (o : Id) =
            compare value o.Value

        interface IComparable with
            member x.CompareTo o =
                match o with
                    | :? Id as o -> compare value o.Value
                    | _ -> failwithf "[Id] cannot compare to %A" o
                    
        interface IComparable<Id> with
            member x.CompareTo o = x.CompareTo o
    
        static member New = Id(Interlocked.Increment(&currentValue))
        static member Root = Id(0)
        static member Invalid = Id(-1)


    type Path =
        | Node of Id
        | Field of path : Path * name : string
        | Item of path : Path * index : int

    type Operation<'a> =
        | Update of path : Path * value : 'a
        | InsertAt of node : Id * index : int * value : 'a
        | RemoveAt of node : Id * index : int
        | InsertAfter of anchor : Id * id : Id * value : 'a
        | InsertBefore of anchor : Id * id : Id * value : 'a
        | AppendChild of parent : Id * id : Id * value : 'a


    type IOperationReader<'a> = IOpReader<list<Operation<'a>>>

    module List =
        let monoid<'a> =
            {
                mempty = List.empty<'a>
                mappend = List.append
                misEmpty = List.isEmpty
            }

    

    type AListReader<'a>(input : alist<'a>, id : Id) =
        inherit AbstractReader<list<Operation<'a>>>(Ag.emptyScope, List.monoid)

        let reader = input.GetReader()
        
        override x.Release() =
            reader.Dispose()

        override x.Compute(token : AdaptiveToken) =
            let mutable old = reader.State
            let ops = reader.GetOperations token

            let self = Node id
            ops |> PDeltaList.toList |> List.collect (fun (i,op) ->
                let index = old.AsMap |> MapExt.reference i
                        
                match op with
                    | Set n ->
                        match index with
                            | MapExtImplementation.Existing(i, o) -> 
                                if Unchecked.equals o n then
                                    []
                                else
                                    [ Update(Item(self, i), n) ]

                            | MapExtImplementation.NonExisting i -> 
                                [ InsertAt(id, i, n) ]

                    | Remove -> 
                        match index with
                            | MapExtImplementation.Existing(i, o) ->
                                [ RemoveAt(id, i) ]
                            | _ ->
                                []
            )



    [<AbstractClass>]
    type AListUpdater<'a, 'op>(l : alist<'a>, m : Monoid<'op>) =
        inherit AdaptiveObject()

        let reader = l.GetReader()
        let updaters = Dict<Index, IUpdater<'op>>()

        abstract member Invoke : Neighbourhood<'a> * 'a -> 'op * Option<IUpdater<'op>>
        abstract member Revoke : Neighbourhood<'a> -> 'op

        member x.Dispose() =
            lock x (fun () ->
                updaters.Values |> Seq.iter (fun u -> u.Outputs.Remove x |> ignore)
                reader.Dispose()
                updaters.Clear()
                let mutable foo = 0
                x.Outputs.Consume(&foo) |> ignore
            )

        member x.Update(token) =
            x.EvaluateIfNeeded token m.mempty (fun token ->
                let mutable old = reader.State
                let ops = reader.GetOperations token

                let ops =
                    ops |> PDeltaList.toList |> List.map (fun (i,op) ->
                        let (l,s,r) = MapExt.neighbours i old.AsMap
                        let l = l |> Option.map snd
                        let s = s |> Option.map snd
                        let r = r |> Option.map snd
                        match op with
                            | Set v -> 
                                old <- PList.set i v old
                                let op, updater = x.Invoke({ prev = l; self = s; next = r }, v)

                                match updaters.TryRemove i with
                                    | (true, o) -> o.Dispose() 
                                    | _ -> ()
                                
                                match updater with
                                    | Some u ->
                                        updaters.[i] <- u
                                    | None ->
                                        ()

                                op
                            | Remove -> 
                                old <- PList.remove i old
                                x.Revoke { prev = l; self = s; next = r }
                    )

                let updates = updaters.Values |> Seq.map (fun u -> u.Update token) |> Seq.fold m.mappend m.mempty

                let ops = ops |> List.fold m.mappend m.mempty
                m.mappend ops updates
            )
            
        interface IUpdater<'op> with
            member x.Dispose() = x.Dispose()
            member x.Update t = x.Update t
    
    [<AbstractClass>]
    type ValueUpdater<'a, 'op>(input : IMod<'a>, m : Monoid<'op>) =
        inherit AdaptiveObject()
        let mutable last = None

        abstract member Invoke : Option<'a> * 'a -> 'op
        
        member x.Dispose() =
            lock x (fun () ->
                last <- None
                input.Outputs.Remove x |> ignore
                let mutable foo = 0
                x.Outputs.Consume(&foo) |> ignore
            )

        member x.Update token =
            x.EvaluateIfNeeded token m.mempty (fun token ->
                let n = input.GetValue token
                let res = x.Invoke(last, n)
                last <- Some n
                res
            )

        interface IUpdater<'op> with
            member x.Dispose() = x.Dispose()
            member x.Update t = x.Update t


    type NodeDescription =
        {
            key         : string
            title       : IMod<string>
            isFolder    : IMod<bool>
        }

    type Node(desc : NodeDescription, children : alist<Node>) =
        member x.Description = desc
        member x.Children = children

    type Tree = { roots : alist<Node> }

    type TreeOperation =
        | AddNode of parentKey : string * leftKey : string * key : string
        | RemNode of key : string
        | UpdateNode of oldKey : string * newKey : string
        | SetTitle of key : string * title : string
        | SetIsFolder of key : string * isFolder : bool

    type NodeUpdater(parent : string, n : Node) =
        inherit AdaptiveObject()

        let childUpdater = ChildrenUpdater(n.Description.key, n.Children)
        let mutable lastTitle = None
        let mutable isFolder = None

        
        member x.Description = n.Description

        member x.Kill() =
            lock x (fun () ->
                let mutable foo = 0
                x.Outputs.Consume(&foo) |> ignore
                childUpdater.Kill()
            )

        member x.Remove(parent : string) =
            lock x (fun () ->
                let mutable foo = 0
                x.Outputs.Consume(&foo) |> ignore
                childUpdater.Kill()
                [RemNode n.Description.key]
            )

        member x.Update(token : AdaptiveToken) =
            x.EvaluateIfNeeded token [] (fun token ->
                [
                    match lastTitle, n.Description.title.GetValue token with
                        | Some o, n when o = n -> ()
                        | _, t ->
                            lastTitle <- Some t
                            yield SetTitle(n.Description.key, t)

                    match isFolder, n.Description.isFolder.GetValue token with
                        | Some o, n when o = n -> ()
                        | _, t ->
                            isFolder <- Some t
                            yield SetIsFolder(n.Description.key, t)

                    yield! childUpdater.Update(token)
                ]
            )

    and ChildrenUpdater(parent : string, children : alist<Node>) =
        inherit AdaptiveObject()
        let updaters = children |> AList.map (fun n -> NodeUpdater(parent, n))
        let reader = updaters.GetReader()
        
        member x.Kill() =
            reader.State |> Seq.iter (fun u -> u.Kill())
            reader.Dispose()


        member x.Update(token : AdaptiveToken) =
            x.EvaluateIfNeeded token [] (fun token ->
                let old = reader.State.AsMap
                let ops = reader.GetOperations token

                let deltas =
                    ops |> PDeltaList.toList |> List.collect (fun (i, op) ->
                        let l, s, r = MapExt.neighbours i old

                        match op with
                            | Set v ->
                                match s with
                                    | Some(_,s) -> 
                                        if s = v then
                                            v.Update(token)
                                        else
                                            let desc = s.Description
                                            [UpdateNode(v.Description.key, desc.key)]
                                    | None ->
                                        let desc = v.Description
                                        let lKey = 
                                            match l with
                                                | Some(_,l) -> l.Description.key
                                                | _ -> ""
                                        [AddNode(parent, lKey, desc.key)]
                            | Remove ->
                                match s with
                                    | Some(_,s) -> s.Remove(parent)
                                    | None -> []
                    )

                deltas @ (reader.State |> PList.toList |> List.collect (fun n -> n.Update(token)))

            )

    type Tree with
        member x.GetUpdater() =
            ChildrenUpdater("", x.roots)


        
open System.Threading.Tasks


let colorLockTest() =
    let l = ColoredLock<int>()
    
//    let mutable counts : int[] = Array.zeroCreate 2
//    let mutable maxCounts : int[] = Array.zeroCreate 2
//    let mutable exCount = 0
//    let mutable bad = false
//
//    let size = 1 <<< 13
//    let start = new ManualResetEventSlim(false)
//    let countDown = new CountdownEvent(size)
//
//    let rand = RandomSystem()
//    for _ in 1 .. size do
//        let a =
//            async {
//                do! Async.SwitchToNewThread()
//                let nested = rand.UniformDouble() > 0.9
//                let isColored = rand.UniformDouble() > 0.2
//                let color = rand.UniformInt(counts.Length)
//                start.Wait()
//                if isColored then
//                    if l.Status <> NotEntered then
//                        bad <- true
//
//                    l.Enter(color)
//                    let cnt = Interlocked.Increment(&counts.[color])
//
//                    if l.Status <> Colored color then
//                        bad <- true
//
//                    if cnt > maxCounts.[color] then
//                        maxCounts.[color] <- cnt
//
//                    if nested then
//                        l.Enter()
//                        if l.Status <> Exclusive then
//                            bad <- true
//                        l.Exit()
//                        if l.Status <> Colored color then
//                            bad <- true
//
//                    Thread.Sleep(0)
//                    Interlocked.Decrement(&counts.[color]) |> ignore
//                    l.Exit()
//
//                    if l.Status <> NotEntered then
//                        bad <- true
//
//                    countDown.Signal() |> ignore
//                else
//                    if l.Status <> NotEntered then
//                        bad <- true
//                    l.Enter()
//                    if l.Status <> Exclusive then
//                        bad <- true
//                    let c = Interlocked.Increment(&exCount)
//                    if c > 1 then bad <- true
//                    Thread.Sleep(0)
//                    Interlocked.Decrement(&exCount) |> ignore
//                    l.Exit()
//                    if l.Status <> NotEntered then
//                        bad <- true
//                    countDown.Signal() |> ignore
//            }
//        Async.Start a
//
//
//    start.Set()
//    countDown.Wait()
//    printfn "count: %A" maxCounts
//    printfn "bad:   %A" bad
//

    let check should =
        if l.Status <> should then printfn "error: %A vs %A" l.Status should
        else printfn "good: %A" should

    check NotEntered
    l.Enter(1)
    check (Colored 1)
    l.Enter(1)
    check (Colored 1)
    l.Enter(2)
    check (Colored 2)
    l.Exit()
    check (Colored 1)


    l.Enter()
    check (Exclusive)
    l.Enter()
    check (Exclusive)
    l.Exit()
    check (Exclusive)
    l.Exit()
    check (Colored 1)
    l.Exit()
    check (Colored 1)
    l.Exit()
    check NotEntered
    Environment.Exit 0

open Aardvark.Rendering.Interactive

module Management =
    
    type Memory<'a> =
        {
            malloc : nativeint -> 'a
            mfree : 'a -> nativeint -> unit
            mcopy : 'a -> nativeint -> 'a -> nativeint -> nativeint -> unit
        }


    [<AllowNullLiteral>]
    type Block<'a> =
        class
            val mutable public Parent : MemoryManager<'a>
            val mutable public Next : Block<'a>
            val mutable public Prev : Block<'a>
            val mutable public Offset : nativeint
            val mutable public Size : nativeint
            val mutable public IsFree : bool

            override x.ToString() =
                sprintf "[%d,%d)" x.Offset (x.Offset + x.Size)

            new(parent, o, s, f, p, n) = { Parent = parent; Offset = o; Size = s; IsFree = f; Prev = p; Next = n }
            new(parent, o, s, f) = { Parent = parent; Offset = o; Size = s; IsFree = f; Prev = null; Next = null }
//
//            interface IComparable with
//                member x.CompareTo o =
//                    match o with
//                        | :? Block<'a> as o -> compare x.Size o.Size
//                        | _ -> failwith "uncomparable"

        end

    and FreeList<'a>() =
        static let comparer =
            { new System.Collections.Generic.IComparer<Block<'a>> with
                member x.Compare(l : Block<'a>, r : Block<'a>) =
                    if isNull l then
                        if isNull r then 0
                        else 1
                    elif isNull r then
                        -1
                    else
                        let c = compare l.Size r.Size
                        if c <> 0 then c
                        else compare l.Offset r.Offset       
            }

        let store = SortedSetExt<Block<'a>>(Seq.empty, comparer)
        
        static let next (align : nativeint) (v : nativeint) =
            if v % align = 0n then v
            else v + (align - v % align)


        member x.TryGetGreaterOrEqual(size : nativeint) =
            let query = Block(Unchecked.defaultof<_>, -1n, size, true)
            let (_, _, r) = store.FindNeighbours(query)
            if r.HasValue then 
                let r = r.Value
                store.Remove r |> ignore
                Some r
            else 
                None

        member x.TryGetAligned(align : nativeint, size : nativeint) =
            let min = Block(Unchecked.defaultof<_>, -1n, size, true)
            let view = store.GetViewBetween(min, null)

            let res = 
                view |> Seq.tryFind (fun b ->
                    let o = next align b.Offset
                    let s = b.Size - (o - b.Offset)
                    s >= size
                )

            match res with
                | Some res -> 
                    store.Remove res |> ignore
                    Some res
                | None ->
                    None

        member x.Insert(b : Block<'a>) =
            store.Add b |> ignore

        member x.Remove(b : Block<'a>) =
            store.Remove b |> ignore

        member x.Clear() =
            store.Clear()

    and MemoryManager<'a>(mem : Memory<'a>, initialCapacity : nativeint) as this =
        
        let free = FreeList<'a>()
        
        let mutable store = mem.malloc initialCapacity
        let mutable capacity = initialCapacity
        let mutable first = Block<'a>(this, 0n, initialCapacity, true)
        let mutable last = first
        do free.Insert(first)

        static let next (align : nativeint) (v : nativeint) =
            if v % align = 0n then v
            else v + (align - v % align)

        let rw = new ReaderWriterLockSlim()

        let changeCapacity (newCapacity : nativeint) =
            let oldCapacity = capacity
            if newCapacity <> oldCapacity then
                ReaderWriterLock.write rw (fun () ->
                    let o = store
                    let n = mem.malloc newCapacity
                    let copy = min oldCapacity newCapacity

                    mem.mcopy o 0n n 0n copy
                    mem.mfree o oldCapacity
                    store <- n
                    capacity <- newCapacity

                    let additional = newCapacity - oldCapacity
                    if additional > 0n then
                        if last.IsFree then
                            free.Remove(last) |> ignore
                            last.Size <- last.Size + additional
                            free.Insert(last)
                        else
                            let newFree = Block<'a>(this, oldCapacity, additional, true, last, null)
                            last.Next <- newFree
                            free.Insert(newFree)
                    else (* additional < 0 *)
                        let freed = -additional
                        if not last.IsFree  || last.Size < freed then
                            failwith "invalid memory manager state"
        
                        if last.Size > freed then
                            free.Remove(last) |> ignore
                            last.Size <- last.Size - freed
                            free.Insert(last)
                        else (* last.Size = freed *)
                            free.Remove(last) |> ignore
                            let l = last
                            if isNull l.Prev then first <- null
                            else l.Prev.Next <- null
                            last <- l.Prev
                )

        let grow (additional : nativeint) =
            let newCapacity = Fun.NextPowerOfTwo(int64 (capacity + additional)) |> nativeint
            changeCapacity newCapacity
            
        member x.Alloc(align : nativeint, size : nativeint) =
            lock free (fun () ->
                match free.TryGetAligned(align, size) with
                    | Some b ->
                        let alignedOffset = next align b.Offset
                        let alignedSize = b.Size - (alignedOffset - b.Offset)
                        if alignedOffset > b.Offset then
                            let l = Block<'a>(x, b.Offset, alignedOffset - b.Offset, true, b.Prev, b)
                            if isNull l.Prev then first <- l
                            else l.Prev.Next <- l
                            b.Prev <- l
                            free.Insert(l)
                            b.Offset <- alignedOffset
                            b.Size <- alignedSize        
                            
                        if alignedSize > size then
                            let r = Block<'a>(x, alignedOffset + size, alignedSize - size, true, b, b.Next)
                            if isNull r.Next then last <- r
                            else r.Next.Prev <- r
                            b.Next <- r
                            free.Insert(r)
                            b.Size <- size

                        b.IsFree <- false
                        b
                    | None ->
                        grow size
                        x.Alloc(align, size)

            )

        member x.Alloc(size : nativeint) =
            lock free (fun () ->
                match free.TryGetGreaterOrEqual size with
                    | Some b ->
                        if b.Size > size then
                            let rest = Block<'a>(x, b.Offset + size, b.Size - size, true, b, b.Next)
                        
                            if isNull rest.Next then last <- rest
                            else rest.Next.Prev <- rest
                            b.Next <- rest

                            free.Insert(rest)
                            b.Size <- size

                        b.IsFree <- false
                        b
                    | None ->
                        grow size
                        x.Alloc size
            )

        member x.Free(b : Block<'a>) =
            if not b.IsFree then
                lock free (fun () ->
                    let old = b
                    
                    let b = Block(x, b.Offset, b.Size, b.IsFree, b.Prev, b.Next)
                    if isNull b.Prev then first <- b
                    else b.Prev.Next <- b

                    if isNull b.Next then last <- b
                    else b.Next.Prev <- b

                    old.Next <- null
                    old.Prev <- null
                    old.IsFree <- true
                    old.Offset <- -1n
                    old.Size <- 0n


                    let prev = b.Prev
                    let next = b.Next
                    if not (isNull prev) && prev.IsFree then
                        free.Remove(prev) |> ignore
                        
                        b.Prev <- prev.Prev
                        if isNull prev.Prev then first <- b
                        else prev.Prev.Next <- b

                        b.Offset <- prev.Offset
                        b.Size <- b.Size + prev.Size

                    if not (isNull next) && next.IsFree then
                        free.Remove(next) |> ignore
                        b.Next <- next.Next
                        if isNull next.Next then last <- b
                        else next.Next.Prev <- b
                        b.Next <- next.Next

                        b.Size <- b.Size + next.Size


                    b.IsFree <- true
                    free.Insert(b)

                    if last.IsFree then
                        let c = Fun.NextPowerOfTwo (int64 last.Offset) |> nativeint
                        changeCapacity c

                )

        member x.Realloc(b : Block<'a>, align : nativeint, size : nativeint) =
            if b.Size <> size then
                lock free (fun () ->
                    if b.IsFree then
                        let n = x.Alloc(align, size)

                        b.Prev <- n.Prev
                        b.Next <- n.Next
                        b.Size <- n.Size
                        b.Offset <- n.Offset
                        b.IsFree <- false

                        if isNull b.Prev then first <- b
                        else b.Prev.Next <- b
                        if isNull b.Next then last <- b
                        else b.Next.Prev <- b

                    elif b.Size > size then
                        if size = 0n then
                            x.Free(b)
                        else
                            let r = Block(x, b.Offset + size, b.Size - size, false, b, b.Next)
                            b.Next <- r
                            if isNull r.Next then last <- r
                            else r.Next.Prev <- r
                            x.Free(r)

                    elif b.Size < size then
                        let next = b.Next
                        let missing = size - b.Size
                        if not (isNull next) && next.IsFree && next.Size >= missing then
                            free.Remove next |> ignore

                            if missing < next.Size then
                                next.Offset <- next.Offset + missing
                                next.Size <- next.Size - missing
                                b.Size <- size
                                free.Insert(next)

                            else
                                b.Next <- next.Next
                                if isNull b.Next then last <- b
                                else b.Next.Prev <- b
                                b.Size <- size


                        else
                            let n = x.Alloc(align, size)
                            mem.mcopy store b.Offset store n.Offset b.Size
                            x.Free b

                            b.Prev <- n.Prev
                            b.Next <- n.Next
                            b.Size <- n.Size
                            b.Offset <- n.Offset
                            b.IsFree <- false

                            if isNull b.Prev then first <- b
                            else b.Prev.Next <- b
                            if isNull b.Next then last <- b
                            else b.Next.Prev <- b
            
                )

        member x.Realloc(b : Block<'a>, size : nativeint) =
            x.Realloc(b, 1n, size)

        member x.Capactiy = lock free (fun () -> capacity)

        member x.Use(b : Block<'a>, action : 'a -> nativeint -> nativeint -> 'r) =
            if b.IsFree then failwith "cannot use free block"
            ReaderWriterLock.read rw (fun () -> 
                action store b.Offset b.Size
            )

        member x.Dispose() =
            rw.Dispose()
            mem.mfree store capacity
            first <- null
            last <- null
            free.Clear()
            capacity <- -1n

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    let testMem<'a> : Memory<'a[]> = 
        {
            malloc = fun s -> Array.zeroCreate (int s)
            mfree = fun a s -> ()
            mcopy = fun src srcOff dst dstOff size -> Array.Copy(src, int64 srcOff, dst, int64 dstOff, int64 size)
        }

    let run() =
        let manager = new MemoryManager<int[]>(testMem<int>, 16n)
        
        let a = manager.Alloc(8n, 5n)
        printfn "a: %A" a
        
        let b = manager.Alloc(8n, 13n)
        printfn "b: %A" b

        let c = manager.Alloc(8n, 10n)
        printfn "c: %A" c

        manager.Free b
        manager.Realloc(a, 8n, 100n)
        printfn "a: %A" a


        let d = manager.Alloc(8n, 13n)
        printfn "d: %A" d
        let e = manager.Alloc(8n, 3n)
        printfn "e: %A" e

        Environment.Exit 0
        ()






[<EntryPoint>]
[<STAThread>]
let main args =
    //Management.run()

    //colorLockTest()


    //Examples.Tutorial.run()
    //Examples.Instancing.run()
    //Examples.Render2TexturePrimitive.run()
    //Examples.Render2TextureComposable.run()
    //Examples.Render2TexturePrimiviteChangeableSize.run()
    //Examples.Render2TexturePrimitiveFloat.run()
    //Examples.ComputeTest.run()
    //colorLockTest()



    Ag.initialize()
    Aardvark.Init()
    
    Interactive.Renderer <- Vulkan
    
    Aardvark.Rendering.GL.RuntimeConfig.SupressSparseBuffers <- false
    //Examples.PostProcessing.run()
    //Examples.CommandTest.run()
    Examples.LoD.run()
    //Examples.Shadows.run()
    //Examples.AssimpInterop.run() 
    //Examples.ShaderSignatureTest.run()
    //Examples.Polygons.run()           attention: this one is currently broken due to package refactoring
    //Examples.TicTacToe.run()          attention: this one is currently broken due to package refactoring
    0
