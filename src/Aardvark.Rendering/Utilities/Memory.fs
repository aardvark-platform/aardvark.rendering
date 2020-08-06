namespace Aardvark.Rendering

open System
open System.Threading
open Aardvark.Base

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
        open System.Runtime.InteropServices

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

    type nref<'a>(value : 'a) =
        static let mutable currentId = 0

        let mutable value = value
        let id = Interlocked.Increment(&currentId)

        member private x.Id = id
        member x.Value
            with get() = value
            and set v = value <- v

        override x.GetHashCode() = id
        override x.Equals o =
            match o with
                | :? nref<'a> as o -> id = o.Id
                | _ -> false

        interface IComparable with
            member x.CompareTo o =
                match o with
                    | :? nref<'a> as o -> compare id o.Id
                    | _ -> failwith "uncomparable"


        interface IComparable<nref<'a>> with
            member x.CompareTo o = compare id o.Id

    let inline private (!) (r : nref<'a>) =
        r.Value

    let inline private (:=) (r : nref<'a>) (value : 'a) =
        r.Value <- value

    [<AllowNullLiteral>]
    type Block<'a> =
        class
            val mutable public Parent : IMemoryManager<'a>
            val mutable public Memory : nref<'a>
            val mutable public Next : Block<'a>
            val mutable public Prev : Block<'a>
            val mutable public Offset : nativeint
            val mutable public Size : nativeint
            val mutable public IsFree : bool

            override x.ToString() =
                sprintf "[%d,%d)" x.Offset (x.Offset + x.Size)

            new(parent, m, o, s, f, p, n) = { Parent = parent; Memory = m; Offset = o; Size = s; IsFree = f; Prev = p; Next = n }
            new(parent, m, o, s, f) = { Parent = parent; Memory = m; Offset = o; Size = s; IsFree = f; Prev = null; Next = null }

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
                        else
                            let c = compare l.Offset r.Offset
                            if c <> 0 then c
                            else compare l.Memory r.Memory
            }

        let store = SortedSetExt<Block<'a>>(Seq.empty, comparer)

        static let next (align : nativeint) (v : nativeint) =
            if v % align = 0n then v
            else v + (align - v % align)


        member x.TryGetGreaterOrEqual(size : nativeint) =
            let query = Block(Unchecked.defaultof<_>, Unchecked.defaultof<_>, -1n, size, true)
            let (_, _, r) = store.FindNeighbours(query)
            if r.HasValue then
                let r = r.Value
                store.Remove r |> ignore
                Some r
            else
                None

        member x.TryGetAligned(align : nativeint, size : nativeint) =
            let min = Block(Unchecked.defaultof<_>, Unchecked.defaultof<_>, -1n, size, true)
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

    and IMemoryManager<'a> =
        interface end

    and MemoryManager<'a>(mem : Memory<'a>, initialCapacity : nativeint) as this =

        let free = FreeList<'a>()

        let store = nref <| mem.malloc initialCapacity
        let mutable capacity = initialCapacity
        let mutable first = Block<'a>(this, store, 0n, initialCapacity, true)
        let mutable last = first
        do free.Insert(first)

        static let next (align : nativeint) (v : nativeint) =
            if v % align = 0n then v
            else v + (align - v % align)

        let rw = new ReaderWriterLockSlim()

        let changeCapacity (newCapacity : nativeint) =
            let newCapacity = max newCapacity initialCapacity
            let oldCapacity = capacity
            if newCapacity <> oldCapacity then
                ReaderWriterLock.write rw (fun () ->
                    let o = !store
                    let n = mem.mrealloc o oldCapacity newCapacity
                    store := n
                    capacity <- newCapacity
                    let o = ()

                    let additional = newCapacity - oldCapacity
                    if additional > 0n then
                        if last.IsFree then
                            free.Remove(last) |> ignore
                            last.Size <- last.Size + additional
                            free.Insert(last)
                        else
                            let newFree = Block<'a>(this, store, oldCapacity, additional, true, last, null)
                            last.Next <- newFree
                            last <- newFree
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
            if size = 0n then
                Block<'a>(x, store, 0n, 0n, true, null, null)
            else
                lock free (fun () ->
                    match free.TryGetAligned(align, size) with
                        | Some b ->
                            let alignedOffset = next align b.Offset
                            let alignedSize = b.Size - (alignedOffset - b.Offset)
                            if alignedOffset > b.Offset then
                                let l = Block<'a>(x, store, b.Offset, alignedOffset - b.Offset, true, b.Prev, b)
                                if isNull l.Prev then first <- l
                                else l.Prev.Next <- l
                                b.Prev <- l
                                free.Insert(l)
                                b.Offset <- alignedOffset
                                b.Size <- alignedSize

                            if alignedSize > size then
                                let r = Block<'a>(x, store, alignedOffset + size, alignedSize - size, true, b, b.Next)
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
            if size = 0n then
                Block<'a>(x, store, 0n, 0n, true, null, null)
            else
                lock free (fun () ->
                    match free.TryGetGreaterOrEqual size with
                        | Some b ->
                            if b.Size > size then
                                let rest = Block<'a>(x, store, b.Offset + size, b.Size - size, true, b, b.Next)

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
                    if not b.IsFree then
                        let old = b

                        let b = Block(x, store, b.Offset, b.Size, b.IsFree, b.Prev, b.Next)
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
                            let r = Block(x, store, b.Offset + size, b.Size - size, false, b, b.Next)
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
                            mem.mcopy !store b.Offset !store n.Offset b.Size
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
                action !store b.Offset b.Size
            )

        member x.Use(action : 'a -> 'r) =
            ReaderWriterLock.read rw (fun () ->
                action !store
            )

        member x.Dispose() =
            rw.Dispose()
            mem.mfree !store capacity
            first <- null
            last <- null
            free.Clear()
            capacity <- -1n

        member x.UnsafePointer = store.Value

        interface IDisposable with
            member x.Dispose() = x.Dispose()

        interface IMemoryManager<'a>

    and ChunkedMemoryManager<'a>(mem : Memory<'a>, chunkSize : nativeint) as this =

        let empty = Block<'a>(this, nref (mem.malloc 0n), 0n, 0n, true)

        let free = FreeList<'a>()
        let allocated = Dict<'a, nativeint>()
        let mutable usedMemory = 0n

//        do
//            let store = mem.malloc chunkSize
//            free.Insert(Block<'a>(this, nref store, 0n, chunkSize, true))
//            allocated.Add (store, chunkSize) |> ignore
//            usedMemory <- chunkSize

        static let next (align : nativeint) (v : nativeint) =
            if v % align = 0n then v
            else v + (align - v % align)

        let rw = new ReaderWriterLockSlim()

        let grow (additional : nativeint) =
            let blockCap = max chunkSize additional
            usedMemory <- usedMemory + blockCap
            let newMem = mem.malloc blockCap
            allocated.Add(newMem, blockCap) |> ignore
            free.Insert(Block<'a>(this, nref newMem, 0n, blockCap, true))

        let freed(block : Block<'a>) =
            if isNull block.Prev && isNull block.Next then
                match allocated.TryRemove !block.Memory with
                    | (true, size) ->
                        usedMemory <- usedMemory - size
                        mem.mfree !block.Memory size
                    | _ ->
                        failwith "bad inconsistent hate"
            else
                free.Insert(block)

        member x.Alloc(align : nativeint, size : nativeint) =
            if size = 0n then
                empty
            else
                lock free (fun () ->
                    match free.TryGetAligned(align, size) with
                        | Some b ->
                            let alignedOffset = next align b.Offset
                            let alignedSize = b.Size - (alignedOffset - b.Offset)
                            if alignedOffset > b.Offset then
                                let l = Block<'a>(x, b.Memory, b.Offset, alignedOffset - b.Offset, true, b.Prev, b)
                                if not (isNull l.Prev) then l.Prev.Next <- l
                                b.Prev <- l
                                free.Insert(l)
                                b.Offset <- alignedOffset
                                b.Size <- alignedSize

                            if alignedSize > size then
                                let r = Block<'a>(x, b.Memory, alignedOffset + size, alignedSize - size, true, b, b.Next)
                                if not (isNull r.Next) then r.Next.Prev <- r
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
            if size = 0n then
                empty
            else
                lock free (fun () ->
                    match free.TryGetGreaterOrEqual size with
                        | Some b ->
                            if b.Size > size then
                                let rest = Block<'a>(x, b.Memory, b.Offset + size, b.Size - size, true, b, b.Next)

                                if not (isNull rest.Next) then rest.Next.Prev <- rest
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

                    let b = Block(x, b.Memory, b.Offset, b.Size, b.IsFree, b.Prev, b.Next)
                    if not (isNull b.Prev) then b.Prev.Next <- b

                    if not (isNull b.Next) then b.Next.Prev <- b

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
                        if not (isNull prev.Prev) then prev.Prev.Next <- b

                        b.Offset <- prev.Offset
                        b.Size <- b.Size + prev.Size

                    if not (isNull next) && next.IsFree then
                        free.Remove(next) |> ignore
                        b.Next <- next.Next
                        if not (isNull next.Next) then next.Next.Prev <- b
                        b.Next <- next.Next

                        b.Size <- b.Size + next.Size


                    b.IsFree <- true
                    freed(b)

                )

        member x.Realloc(b : Block<'a>, align : nativeint, size : nativeint) =
            if b.Size <> size then
                lock free (fun () ->
                    if size = 0n then
                        x.Free b
                    elif b.IsFree then
                        let n = x.Alloc(align, size)

                        b.Prev <- n.Prev
                        b.Next <- n.Next
                        b.Size <- n.Size
                        b.Offset <- n.Offset
                        b.IsFree <- false

                        if not (isNull b.Prev) then b.Prev.Next <- b
                        if not (isNull b.Next) then b.Next.Prev <- b

                    elif b.Size > size then
                        if size = 0n then
                            x.Free(b)
                        else
                            let r = Block(x, b.Memory, b.Offset + size, b.Size - size, false, b, b.Next)
                            b.Next <- r
                            if not (isNull r.Next) then r.Next.Prev <- r
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
                                if not (isNull b.Next) then b.Next.Prev <- b
                                b.Size <- size


                        else
                            failwithf "[MemoryManager] cannot realloc when no mcopy given"
                )

        member x.Realloc(b : Block<'a>, size : nativeint) =
            x.Realloc(b, 1n, size)

        member x.Capactiy = lock free (fun () -> usedMemory)

        member x.Use(b : Block<'a>, action : 'a -> nativeint -> nativeint -> 'r) =
            if b.IsFree && b.Size > 0n then failwith "cannot use free block"
            ReaderWriterLock.read rw (fun () ->
                action !b.Memory b.Offset b.Size
            )

        member x.Dispose() =
            rw.Dispose()
            for (KeyValue(a, s)) in allocated do mem.mfree a s
            allocated.Clear()
            free.Clear()

        interface IDisposable with
            member x.Dispose() = x.Dispose()

        interface IMemoryManager<'a>

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MemoryManager =
        let createNop() = new MemoryManager<_>(Memory.nop, 16n)

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module ChunkedMemoryManager =
        let createNop() = new ChunkedMemoryManager<_>(Memory.nop, 16n)