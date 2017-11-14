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

#nowarn "9"

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
            mrealloc : 'a -> nativeint -> nativeint -> 'a
        }

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Memory =
        open System.IO.MemoryMappedFiles

        let hglobal =
            {
                malloc = Marshal.AllocHGlobal
                mfree = fun ptr _ -> Marshal.FreeHGlobal ptr
                mcopy = fun src srcOff dst dstOff size -> Marshal.Copy(src + srcOff, dst + dstOff, size)
                mrealloc = fun ptr _ n -> Marshal.ReAllocHGlobal(ptr, n)
            }

        let cotask =
            {
                malloc = fun s -> Marshal.AllocCoTaskMem(int s)
                mfree = fun ptr _ -> Marshal.FreeCoTaskMem ptr
                mcopy = fun src srcOff dst dstOff size -> Marshal.Copy(src + srcOff, dst + dstOff, size)
                mrealloc = fun ptr _ n -> Marshal.ReAllocCoTaskMem(ptr, int n)
            }
        
        let array<'a> =
            {
                malloc = fun s -> Array.zeroCreate<'a> (int s)

                mfree = fun a s -> 
                    ()

                mcopy = fun src srcOff dst dstOff size -> 
                    Array.Copy(src, int64 srcOff, dst, int64 dstOff, int64 size)

                mrealloc = fun ptr o n -> 
                    let mutable ptr = ptr
                    Array.Resize(&ptr, int n)
                    ptr
            }

        let nop =
            {
                malloc = fun _ -> ()
                mfree = fun _ _ -> ()
                mrealloc = fun _ _ _ -> ()
                mcopy = fun _ _ _ _ _ -> ()
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
                    let n = mem.mrealloc o oldCapacity newCapacity
                    store <- n
                    capacity <- newCapacity
                    let o = ()

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

   
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MemoryManager =
        let createNop() = new MemoryManager<_>(Memory.nop, 16n) 


    let testMem<'a> : Memory<'a[]> = 
        {
            malloc = fun s -> 
                Array.zeroCreate (int s)

            mfree = fun a s -> 
                ()

            mcopy = fun src srcOff dst dstOff size -> 
                Array.Copy(src, int64 srcOff, dst, int64 dstOff, int64 size)

            mrealloc = fun ptr o n -> 
                let mutable ptr = ptr
                Array.Resize(&ptr, int n)
                ptr
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


module AdaptiveResources9000 =
    let mutable currentResourceVersion = 0L

    [<StructuralEquality; StructuralComparison>]
    type ResourceVersion =
        struct
            val mutable private Value : int64

            static member Zero = ResourceVersion(0L)
            static member New = ResourceVersion(Interlocked.Increment(&currentResourceVersion))

            private new(v) = { Value = v }
        end

    type ResourceInfo(v : ResourceVersion, h : obj, l : hset<ILockedResource>) =

        static let empty = ResourceInfo(ResourceVersion.Zero, null, HSet.empty)

        static member Empty = empty

        member x.Version = v
        member x.Handle = h
        member x.Locked = l

    type ResourceInfo<'h>(v : ResourceVersion, h : 'h, l : hset<ILockedResource>) =
        inherit ResourceInfo(v, h :> obj, l)
        member x.Handle = h

    type IResourceDescription<'a, 'h> =
        abstract member Create : 'a -> ResourceInfo<'h>
        abstract member Update : ResourceInfo<'h> * 'a -> ResourceInfo<'h>
        abstract member Delete : ResourceInfo<'h> -> unit


    type ResourceDescription<'a, 'h> =
        {
            create : 'a -> ResourceInfo<'h>
            update : 'a -> ResourceInfo<'h> -> ResourceInfo<'h>
            delete : ResourceInfo<'h> -> unit
        }


    type IResource =
        inherit IAdaptiveObject
        abstract member Force : AdaptiveToken -> ResourceInfo
    
    type IResource<'h> =
        inherit IResource
        abstract member Force : AdaptiveToken -> ResourceInfo<'h>

    type ResourceReference(set : ResourceSet, r : IResource) =
        let mutable refCount = 0
        let mutable currentInfo = ResourceInfo.Empty


        member x.Resource = r
        member x.Set = set
        member x.CurrentInfo = currentInfo

        member x.Update(token : AdaptiveToken) =
            let info = r.Force(token)
            currentInfo <- info
            info

        member internal x.Acquire() = Interlocked.Increment(&refCount) = 1
        member internal x.Release() = Interlocked.Decrement(&refCount) = 0

    and ResourceReference<'h>(set : ResourceSet, r : IResource<'h>) =
        inherit ResourceReference(set, r :> IResource)
        member x.Resource = r
        
        member x.Update(token : AdaptiveToken) =
            let info = base.Update(token) |> unbox<ResourceInfo<'h>>
            x.WriteHandle info.Handle
            info

        abstract member WriteHandle : 'h -> unit
        default x.WriteHandle _ = ()
            
    and ResourceReference<'h, 'n when 'n : unmanaged>(set : ResourceSet, r : IResource<'h>, view : 'h -> 'n) =
        inherit ResourceReference<'h>(set, r)

        let mutable ptr : nativeptr<'n> = NativePtr.alloc 1

        member x.Pointer = ptr

        override x.WriteHandle(h : 'h) : unit =
            NativePtr.write ptr (view h)

    and private ResourceStore =
        class
            val mutable public Reference : ResourceReference
            val mutable public ResourceInfo : ResourceInfo

            member x.UpdateInfo(info : ResourceInfo) =
                if info.Version > x.ResourceInfo.Version then
                    let delta = HSet.computeDelta x.ResourceInfo.Locked info.Locked
                    x.ResourceInfo <- info
                    Some delta
                else
                    None

            new(r) = { Reference = r; ResourceInfo = r.CurrentInfo }
        end

    and ResourceSet() =
        inherit AdaptiveObject() 

        let refStore = System.Collections.Concurrent.ConcurrentDictionary<IResource, ResourceStore>()
        let dirty = System.Collections.Generic.HashSet<ResourceStore>()

        let tryGetStore(r : IResource) =
            match refStore.TryGetValue r with
                | (true, s) -> Some s
                | _ -> None

        let mutable lockedResources : hrefset<ILockedResource> = HRefSet.empty

        let addDirty o = lock dirty (fun () -> dirty.Add o |> ignore)
        let remDirty o = lock dirty (fun () -> dirty.Remove o |> ignore)
        let consumeDirty() =
            lock dirty (fun () ->
                let arr = dirty |> Seq.toArray
                dirty.Clear()
                arr
            )

        let mutable maxVersion = ResourceVersion.New

        override x.InputChanged(_,o) =
            match o with
                | :? IResource as r ->
                    match tryGetStore r with
                        | Some s -> addDirty s
                        | None -> ()
                | _ ->
                    ()


        member x.Add(r : IResource<'h>, view : 'h -> 'n) : ResourceReference<'h, 'n> =
            lock x (fun () ->
                let store = refStore.GetOrAdd(r :> IResource, fun _ -> ResourceStore(ResourceReference<'h, 'n>(x, r, view))) 
                let ref = store.Reference |> unbox<ResourceReference<'h, 'n>>

                if ref.Acquire() then
                    lock r (fun () ->
                        if r.OutOfDate then
                            addDirty store
                            // mark x outofdate????
                        else
                            r.Outputs.Add(x) |> ignore
                            x.Level <- max x.Level (r.Level + 1)
                            maxVersion <- max maxVersion ref.CurrentInfo.Version
                            match store.UpdateInfo ref.CurrentInfo with
                                | Some deltas ->
                                    let (l', _) = lockedResources.ApplyDelta(deltas)
                                    lockedResources <- l'

                                | None ->
                                    Log.warn "strange case"

                    )
                    maxVersion <- ResourceVersion.New
            
                ref
            )

        member x.Add(r : IResource<'h>) : ResourceReference<'h> =
            x.Add(r, fun _ -> 0) :> ResourceReference<_>

        member x.Remove(r : IResource) =
            lock x (fun () ->
                match refStore.TryGetValue(r) with
                    | (true, store) ->
                        let res = store.Reference.Resource
                        if store.Reference.Release() then
                            lock res (fun () ->
                                res.Outputs.Remove x |> ignore
                                let deltas = HSet.computeDelta store.ResourceInfo.Locked HSet.empty
                                let (l', _) = lockedResources.ApplyDelta(deltas)
                                lockedResources <- l'
                                remDirty store |> ignore
                            )
                            refStore.TryRemove r |> ignore
                            maxVersion <- ResourceVersion.New
                    | _ ->
                        ()
            )

        member x.Use(token : AdaptiveToken, action : ResourceVersion -> 'r) =
            x.EvaluateAlways token (fun token ->
                let rec run() =
                    let dirty = consumeDirty()
                    if dirty.Length > 0 then
                        for store in dirty do
                            let info = store.Reference.Update(token)
                            maxVersion <- max maxVersion info.Version
                            match store.UpdateInfo info with
                                | Some deltas ->
                                    let (l', _) = lockedResources.ApplyDelta(deltas)
                                    lockedResources <- l'

                                | None ->
                                    Log.warn "strange case"

                        run()
                run()

                let mine = lockedResources
                for l in mine do l.Lock.Enter(ResourceUsage.Render, l.OnLock)
                try 
                    action maxVersion
                finally 
                    for l in mine do l.Lock.Exit(l.OnUnlock)
            )



module AdaptiveResourcesEager =
    open System.Collections.Generic
    open System.Linq

    let mutable private currentTime = 0L
    let private newTime() = Interlocked.Increment(&currentTime)

    type IRefCounted =
        abstract member Acquire : unit -> unit
        abstract member Release : unit -> unit

    type IResource =
        inherit IRefCounted
        abstract member AddOutput : IResource -> unit
        abstract member RemoveOutput : IResource -> unit
        abstract member Outputs : seq<IResource>
        abstract member Level : int
        abstract member Update : unit -> int64

    type IResource<'h> =
        inherit IResource
        abstract member Handle : 'h

    [<AbstractClass>]
    type AbstractResource<'h>(level : int) =
        let mutable refCount = 0

        let mutable updateTime = -1L
        let mutable currentHandle : Option<'h> = None
        let mutable outputs : hset<IResource> = HSet.empty

        abstract member Create : unit -> 'h
        abstract member Update : 'h -> bool * 'h
        abstract member Destroy : 'h -> unit

        member x.Acquire() = 
            if Interlocked.Increment(&refCount) = 1 then
                updateTime <- newTime()
                ()

        member x.Release() = 
            if Interlocked.Decrement(&refCount) = 0 then
                match currentHandle with
                    | Some h -> 
                        x.Destroy h
                        currentHandle <- None
                        updateTime <- newTime()
                    | None ->
                        ()

        member x.Level = level

        member x.Outputs = outputs

        member x.Handle = 
            match currentHandle with
                | Some h -> h
                | _ -> failwith "[Resource] does not have a handle"

        member x.AddOutput (r : IResource) =
            assert(r.Level > x.Level)
            outputs <- HSet.add r outputs

        member x.RemoveOutput (r : IResource) =
            assert(r.Level > x.Level)
            outputs <- HSet.remove r outputs

        member x.Update() =
            lock x (fun () ->
                if refCount = 0 then failwith "[Resource] cannot update without reference"

                let (changed, handle) = 
                    match currentHandle with
                        | Some h -> x.Update(h)
                        | None -> true, x.Create()

                if changed then 
                    updateTime <- newTime()

                currentHandle <- Some handle
                updateTime
            )

        interface IRefCounted with
            member x.Acquire() = x.Acquire()
            member x.Release() = x.Release()

        interface IResource with
            member x.Level = x.Level
            member x.AddOutput o = x.AddOutput o
            member x.RemoveOutput o = x.RemoveOutput o
            member x.Outputs = x.Outputs :> seq<_>
            member x.Update() = x.Update()

        interface IResource<'h> with
            member x.Handle = x.Handle

    module Resource =
        let mapN (update : Option<'b> -> 'h[] -> bool * 'b) (destroy : 'b -> unit) (inputs : IResource<'h>[]) =
            let level = inputs |> Seq.map (fun r -> r.Level) |> Seq.max
            {
                new AbstractResource<'b>(1 + level) with
                    override x.Update(old) =
                        let handles = inputs |> Array.map (fun r -> r.Handle)
                        update (Some old) handles

                    override x.Create() =
                        for i in inputs do 
                            i.Acquire()
                            i.AddOutput x

                        let handles = inputs |> Array.map (fun r -> r.Handle)
                        let _, h = update None handles
                        h

                    override x.Destroy h =
                        for i in inputs do 
                            i.Release()
                            i.RemoveOutput x

                        destroy h

            } :> IResource<_>
        
        let map (update : Option<'b> -> 'h -> bool * 'b) (destroy : 'b -> unit) (input : IResource<'h>) =
            {
                new AbstractResource<'b>(1 + input.Level) with
                    override x.Update(old) =
                        update (Some old) input.Handle

                    override x.Create() =
                        input.Acquire()
                        input.AddOutput x
                        let _, h = update None input.Handle
                        h

                    override x.Destroy h =
                        input.Release()
                        input.RemoveOutput x
                        destroy h

            } :> IResource<_>

    type ResourceSet() =
        let all = ReferenceCountingSet<IResource>()

        let mutable maxTime = -1L

        let compareLevel =
            Func<IResource, IResource, int>(fun l r ->
                compare l.Level r.Level
            )

        member x.Add(r : IResource) =
            if all.Add r then
                r.Acquire()

        member x.Remove(r : IResource) =
            if all.Remove r then
                r.Release()

        member x.Update(dirty : seq<IResource>) =
            let heap = List<IResource>()
            for r in dirty do
                if all.Contains r then
                    heap.HeapEnqueue(compareLevel, r)

            while heap.Count > 0 do
                let r = heap.HeapDequeue(compareLevel)
                let t = r.Update()
                if t > maxTime then
                    for o in r.Outputs do
                        if all.Contains o then
                            heap.HeapEnqueue(compareLevel, o)

                    maxTime <- t

            maxTime


open Aardvark.Application.WinForms

[<EntryPoint>]
[<STAThread>]
let main args =
    //Management.run()

    let useVulkan = true

    Ag.initialize()
    Aardvark.Init()

    //Aardvark.Application.OpenVR.UnhateTest.run()

    //Examples.Tessellation.run()
    //Examples.GeometryComposition.run()
    //Examples.Stereo.runNew()
    //Examples.ComputeShader.run()
    Examples.CommandTest.run()
    System.Environment.Exit 0

    //colorLockTest()
    let app,win =
        if useVulkan then
            let app = new Aardvark.Application.WinForms.VulkanApplication()
            let win = app.CreateSimpleRenderWindow()
            app :> Aardvark.Application.IApplication,win :> Aardvark.Application.IRenderWindow
        else
            let app = new Aardvark.Application.WinForms.OpenGlApplication()
            let win = app.CreateGameWindow()
            app:> Aardvark.Application.IApplication,win :> Aardvark.Application.IRenderWindow
    CullingTest.run app win |> ignore
    //CullingTest.runInstanced () |> ignore
    //CullingTest.runStructural app win|> ignore
    System.Environment.Exit 0


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
    //Examples.LoD.run()
    //Examples.Shadows.run()
    //Examples.AssimpInterop.run() 
    //Examples.ShaderSignatureTest.run()
    //Examples.Polygons.run()           attention: this one is currently broken due to package refactoring
    //Examples.TicTacToe.run()          attention: this one is currently broken due to package refactoring
    0
