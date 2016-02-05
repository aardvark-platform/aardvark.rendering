namespace Aardvark.Rendering.GL

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open Aardvark.Base
[<AutoOpen>]
module Memory =

    [<AllowNullLiteral>]
    [<StructuredFormatDisplay("{AsString}")>]
    type Block(mem : MemoryManager, ptr : nativeint, size : int) =
        let mutable ptr = ptr
        let mutable size = size
        let mutable next : Block = null
        let mutable prev : Block = null
        let mutable free = true
        let mutable tag : obj = null

        member x.AsString = sprintf "(%d, %d)" ptr size

        member x.Tag
            with get() : obj = tag
            and set (t : obj) = lock mem.ChangeLock (fun () -> tag <- t)

        member x.IsFree
            with get() = free
            and set f = lock mem.ChangeLock (fun () -> free <- f)

        member x.Prev
            with get() = prev
            and set p = lock mem.ChangeLock (fun () -> prev <- p)

        member x.Next
            with get() = next
            and set p = lock mem.ChangeLock (fun () -> next <- p)

        member x.Split(position : int) =
            lock mem.ChangeLock (fun () -> 
                if position < size then
                    let rest = new Block(mem, ptr + (nativeint position), size - position)
                    rest.Prev <- x
                    rest.Next <- next
                    if next <> null then next.Prev <- rest
                    next <- rest
                    size <- position
                    rest.IsFree <- free
                    Some rest
                elif position = size then
                    None
                else
                    failwithf "cannot split block of size %A at position %A" size position
            )

        member x.Memory = mem
        member x.Pointer
            with get() = ptr
            and set v = 
                lock mem.ChangeLock (fun () -> 
                        
                    ptr <- v
                )

        member x.RealPointer = mem.GetRealPointer(ptr)
        member x.Size
            with get() = size
            and set s = 
                lock mem.ChangeLock (fun () -> 
                    size <- s
                    if free then failwith "ERROR: changing pointer of free-block"
                )

        member x.Resize(newSize : int) =
            lock mem.ChangeLock (fun () -> mem.Realloc(x, newSize))

        member x.Dispose() =
            lock mem.ChangeLock (fun () -> 
                if not free then
                    mem.Free x
                    free <- true
            )

        interface IDisposable with
            member x.Dispose() = x.Dispose()

        member x.ReadIntPtr(offset : int) =
            if offset < 0 || offset > size - System.IntPtr.Size then
                failwith "offset out of bounds"

            mem.ReadIntPtr(ptr + (nativeint offset))

        member x.WriteIntPtr(offset : int, value : nativeint) =
            if offset < 0 || offset > size - System.IntPtr.Size then
                failwith "offset out of bounds"

            mem.WriteIntPtr(ptr + (nativeint offset), value)

        member x.ReadInt32(offset : int) =
            if offset < 0 || offset > size - 4 then
                failwith "offset out of bounds"

            mem.ReadInt32(ptr + (nativeint offset))

        member x.WriteInt32(offset : int, value : int) =
            if offset < 0 || offset > size - 4 then
                failwith "offset out of bounds"

            mem.WriteInt32(ptr + (nativeint offset), value)

        member x.Read(offset : int, size : int) =
            if offset < 0 || offset + size > x.Size then
                failwith "offset out of bounds"

            mem.Read(ptr + (nativeint offset), size)

        member x.Write(offset : int, arr : byte[]) =
            if offset < 0 || offset + arr.Length > x.Size then
                failwith "offset out of bounds"

            mem.Write(ptr + (nativeint offset), arr)

        member x.Move(targetOffset : int, sourceOffset : int, size : int) =
            if targetOffset + size > x.Size || sourceOffset + size > x.Size then
                failwith "offset out of bounds"

            mem.Move(ptr + (nativeint targetOffset), ptr + (nativeint sourceOffset), size)

        member x.ValidateNoOverlap() =
            if prev <> null && prev.Pointer + (nativeint prev.Size) <> ptr then
                printfn "ERROR: overlap with prev block"
                false
            elif next <> null && next.Pointer <> ptr + (nativeint size) then
                printfn "ERROR: overlap with next block"
                false
            else
                true

    and [<StructuredFormatDisplay("{AsString}")>] private BlockGroup(size : int, blocks : HashSet<Block>) =
        member x.Size = size       
        member x.Blocks = blocks 

        member x.AsString =
            //let blocks = blocks |> Seq.map (fun b -> sprintf "%A" b) |> String.concat "; "
            sprintf "G(%d, %d)" size blocks.Count

        new(b : Block) = 
            let set = HashSet()
            set.Add b |> ignore
            BlockGroup(b.Size, set)

    and MemoryManager(initialCapacity : int) as this =
            
        let ptrLock = obj()
        let changeLock = obj()

        let alloc (size : int) =
            ExecutableMemory.alloc size
            //let ptr = Kernel32.VirtualAlloc(0n, (UIntPtr (uint32 size)), Kernel32.AllocationType.Commit, Kernel32.MemoryProtection.ExecuteReadWrite)
            //ptr

        let free (ptr : nativeint) (size : int) =
            ExecutableMemory.free ptr size
            //Kernel32.VirtualFree(ptr, (UIntPtr (uint32 size)), Kernel32.FreeType.Decommit) |> ignore

        let mutable capacity = initialCapacity
        let mutable ptr = alloc capacity

        let cmp = fun (a : BlockGroup) (b : BlockGroup) -> a.Size.CompareTo b.Size
        let freeBlocks = AVL.custom cmp

        let insertFree (tree : AVL.Tree<BlockGroup>) (b : Block) = 
            if not b.IsFree then b.IsFree <- true
            if b.Tag <> null then b.Tag <- null
            let existing = tree |> AVL.find (fun g -> b.Size.CompareTo g.Size)
            match existing with
                | Some e -> 
                    if not <| e.Blocks.Add b then
                        failwith "could not insert block since it is already free"

                | None -> AVL.insert tree (BlockGroup(b)) |> ignore
        
        let removeFree (tree : AVL.Tree<BlockGroup>) (b : Block) =
                
            let existing = tree |> AVL.find (fun g -> b.Size.CompareTo g.Size)
            match existing with
                | Some e ->
                    if e.Blocks.Remove b then
                        if e.Blocks.Count = 0 then
                            if not <| AVL.remove tree e then
                                failwith "error removing blockgroup from tree"
                        b.IsFree <- false
                        true
                    else
                        failwith "block not present in group"

                | None ->
                    false

        let initial = new Block(this, 0n, capacity)
        do insertFree freeBlocks initial
        let mutable firstBlock = initial
        let mutable lastBlock = initial

            
        let validateMemory() =
            let mutable current = firstBlock

            if firstBlock.Pointer <> 0n then
                printfn "ERROR: first block does not start at 0"

            //validate linking and memory-adjacency
            while current <> null do
                let next = current.Next
                if next <> null then
                    if next.Pointer <> current.Pointer + (nativeint current.Size) then
                        printfn "ERROR: adjacent blocks are not adjacent in memory: %A %A" current next
                    if next.Prev <> current then
                        printfn "ERROR: block has invalid prev-pointer: %A.Prev = %A but should be %A" next next.Prev current
                    if next.IsFree && current.IsFree then
                        printfn "ERROR: two adjacent free-blocks found (should be collapsed)"
                else
                    let e = (int current.Pointer) + current.Size
                    if e <> capacity then
                        printfn "ERROR: last block's end does not match capacity: %A vs. %d" current capacity
                    if current <> lastBlock then
                        printfn "ERROR: invalid last block: %A vs. %A" current lastBlock



                current <- next

            //validate free-flags
            let mutable current = firstBlock
            while current <> null do
                if current.IsFree then
                    let b = current
                    match freeBlocks |> AVL.find (fun g -> compare b.Size g.Size) with
                        | Some g ->
                            if not <| g.Blocks.Contains current then
                                printfn "ERROR: block is marked free but not in AVL-Tree %A" current
                        | None ->
                            printfn "ERROR: block is marked free but not in AVL-Tree %A" current
                else
                    let b = current
                    match freeBlocks |> AVL.find (fun g -> compare b.Size g.Size) with
                        | Some g ->
                            if g.Blocks.Contains current then
                                printfn "ERROR: block is marked non-free but in AVL-Tree %A" current
                        | None -> ()
                current <- current.Next



        let realloc(additionalSize : int) : unit =
            lock changeLock (fun () ->
                //realloc the "real" memory
                let oldCap,newCap = 
                    lock ptrLock (fun () ->
                        let oldCap = capacity
                        let newCap = Fun.NextPowerOfTwo(capacity + additionalSize)
                        let oldPtr = ptr
                        let newPtr = alloc newCap
                        Marshal.Copy(ptr, newPtr, Fun.Min(oldCap, newCap))
                        //MSVCRT.memcpy(newPtr, ptr, Fun.Min(oldCap, newCap))

                        ptr <- newPtr
                        capacity <- newCap
                        free oldPtr oldCap
                        oldCap,newCap
                    )

                let newMemory = new Block(this, nativeint oldCap, newCap - oldCap)

                newMemory.Prev <- lastBlock
                lastBlock.Next <- newMemory
                newMemory.IsFree <- false
                lastBlock <- newMemory
                this.Free(newMemory)

            )

        member x.Capacity = capacity

        member x.ChangeLock = changeLock
        member x.PointerLock = ptrLock

        member x.NotifyCreated(b : Block) =
            if b.Prev = null then firstBlock <- b
            if b.Next = null then lastBlock <- b


        member x.PrintFree() =
            AVL.print freeBlocks

        member x.GetRealPointer(offset) =
            lock ptrLock (fun () -> ptr + offset)

        member x.AllocSpecific(b : Block) : bool =
            lock changeLock (fun () ->
                match freeBlocks |> AVL.find (fun bi -> compare b.Size bi.Size) with
                    | Some free ->
                        if free.Blocks.Remove(b) then
                            if free.Blocks.Count = 0 then
                                AVL.remove freeBlocks free |> ignore
                            b.IsFree <- false
                            true
                        else
                            false


                    | None ->
                        false
            )

        member x.Alloc(size : int) : Block =
            lock changeLock (fun () ->
                match freeBlocks |> AVL.findMinimalWhere (fun b -> b.Size >= size) with
                    | Some free ->
                        let fst = free.Blocks |> Seq.head
                        free.Blocks.Remove(fst) |> ignore
                        fst.IsFree <- false

                        if free.Blocks.Count = 0 then
                            AVL.remove freeBlocks free |> ignore

                        match fst.Split size with
                            | Some rest -> x.Free(rest)
                            | None -> ()

                        fst

                    | None ->
                        realloc size
                        x.Alloc size
            )

        member x.Free(b : Block) =
            if not b.IsFree then
                lock changeLock (fun () ->
                    let tree = freeBlocks

                    let n = b.Next
                    let p = b.Prev

                    if p <> null && n <> null && p.IsFree && n.IsFree then
                        removeFree tree n |> ignore
                        removeFree tree p |> ignore

                        let union = new Block(x, p.Pointer, p.Size + b.Size + n.Size)
                        union.Prev <- p.Prev
                        union.Next <- n.Next

                        if n.Next <> null then n.Next.Prev <- union
                        else lastBlock <- union

                        if p.Prev <> null then p.Prev.Next <- union
                        else firstBlock <- union

                        insertFree tree union

                    elif n <> null && n.IsFree then
                        removeFree tree n |> ignore

                        let union = new Block(x, b.Pointer, b.Size + n.Size)
                        union.Prev <- b.Prev
                        union.Next <- n.Next

                        if b.Prev <> null then b.Prev.Next <- union
                        else firstBlock <- union

                        if n.Next <> null then n.Next.Prev <- union
                        else lastBlock <- union


                        insertFree tree union


                    elif p <> null && p.IsFree then
                        removeFree tree p |> ignore

                        let union = new Block(x, p.Pointer, p.Size + b.Size)
                        union.Prev <- p.Prev
                        union.Next <- b.Next

                        if p.Prev <> null then p.Prev.Next <- union
                        else firstBlock <- union

                        if b.Next <> null then b.Next.Prev <- union
                        else lastBlock <- union

                        insertFree tree union

                    else
                        if b.Prev = null then firstBlock <- b
                        if b.Next = null then lastBlock <- b

                        insertFree tree b


                )
            else
                failwith "block was already freed"

        member x.Realloc(b : Block, newSize : int) =
            lock changeLock (fun () ->
                let tree = freeBlocks

                let additional = newSize - b.Size
                if additional < 0 then
                    //printfn "shrink"
                    match b.Split newSize with
                        | Some rest -> x.Free(rest)
                        | None -> ()
                    true
                elif additional > 0 then

                    if b.Next <> null && b.Next.IsFree && b.Next.Size >= additional then
                        //printfn "grow to right"
                        let n = b.Next
                        if not <| removeFree tree n then failwith "could not remove block from freeBlocks"

                        match n.Split additional with
                            | Some rest -> x.Free(rest)
                            | None -> ()



                        //union both blocks
                        b.Size <- newSize
                        b.Next <- n.Next
                        if n.Next <> null then n.Next.Prev <- b
                        else lastBlock <- b

                        true
//                        elif b.Prev <> null && b.Prev.IsFree && b.Prev.Size >= additional then
//                            //printfn "grow to left"
//                            let p = b.Prev
//                            if not <| removeFree tree p then failwith "could not remove block from freeBlocks"
//
//                            let rest = p.Split(p.Size - additional).Value
//                            x.Free(p)
//
//                            x.Move(rest.Pointer, b.Pointer, b.Size) |> ignore
//
//                            b.Pointer <- rest.Pointer
//                            b.Size <- newSize
//                            b.Prev <- rest.Prev
//                            if rest.Prev <> null then rest.Prev.Next <- b
//                            else firstBlock <- b
//
//                            false
                    else
                        //printfn "grow (hard)"
                        let newBlock = x.Alloc newSize
                        x.Copy(newBlock.Pointer, b.Pointer, b.Size)

                        let dummyBlock = new Block(x, b.Pointer, b.Size)
                        dummyBlock.IsFree <- false
                        dummyBlock.Prev <- b.Prev
                        dummyBlock.Next <- b.Next
                        if b.Next <> null then b.Next.Prev <- dummyBlock
                        if b.Prev <> null then b.Prev.Next <- dummyBlock

                        dummyBlock.Dispose()

                        b.IsFree <- false
                        b.Size <- newBlock.Size
                        b.Pointer <- newBlock.Pointer
                        b.Prev <- newBlock.Prev
                        b.Next <- newBlock.Next
                        if newBlock.Next <> null then newBlock.Next.Prev <- b
                        else lastBlock <- b

                        if newBlock.Prev <> null then newBlock.Prev.Next <- b
                        else firstBlock <- b

                        false
                else
                    true
            )

        member x.GetSequentialBlockOfMinimalSize(firstBlockExclusive : Block, minimalSize : int, lockFun : Block -> unit) =
            lock changeLock (fun () ->
                let mutable current = firstBlockExclusive.Next
                    
                let totalAvailable = capacity - (int current.Pointer)
                if totalAvailable < minimalSize then
                    realloc(minimalSize - totalAvailable)
                    x.GetSequentialBlockOfMinimalSize(firstBlockExclusive, minimalSize, lockFun)
                else
                    let mutable size = 0


                    while size < minimalSize && current <> null do
                        //if the block is free simply allocate it
                        if current.IsFree && not <| x.AllocSpecific current then
                            failwith "invalid freeBlocks tree"

                        lockFun current
                        size <- size + current.Size
                        current <- current.Next

                    if size < minimalSize then
                        failwith "should be impossible"

                    let aggregated = new Block(x, firstBlockExclusive.Next.Pointer, size)
                    aggregated.Prev <- firstBlockExclusive
                    firstBlockExclusive.Next <- aggregated
                    aggregated.Next <- current
                    if current <> null then current.Prev <- aggregated

                    if aggregated.Prev = null then firstBlock <- aggregated
                    if aggregated.Next = null then lastBlock <- aggregated


                    aggregated

            )

        member x.ReadIntPtr(address : nativeint) =
            lock ptrLock (fun () -> Marshal.ReadIntPtr(ptr + address))

        member x.WriteIntPtr(address : nativeint, value : nativeint) =
            lock ptrLock (fun () -> Marshal.WriteIntPtr(ptr + address, value))

        member x.ReadInt32(address : nativeint) =
            lock ptrLock (fun () -> Marshal.ReadInt32(ptr + address))

        member x.WriteInt32(address : nativeint, value : int) =
            lock ptrLock (fun () -> Marshal.WriteInt32(ptr + address, value))

        member x.Read(address : nativeint, size : int) =
            lock ptrLock (fun () -> 
                let arr : byte[] = Array.zeroCreate size
                Marshal.Copy(ptr + address, arr, 0, size)
                arr
            )

        member x.Write(address : nativeint, arr : byte[]) =
            lock ptrLock (fun () -> Marshal.Copy(arr, 0, ptr + address, arr.Length))

        member x.Copy(target : nativeint, source : nativeint, size : int) =
            lock ptrLock (fun () -> Marshal.Copy(ptr + source, ptr + target, size))

        member x.Move(target : nativeint, source : nativeint, size : int) =
            lock ptrLock (fun () -> Marshal.Move(ptr + source, ptr + target, size))

        member x.Dispose() =
            lock ptrLock (fun () -> 
                free ptr capacity
                ptr <- 0n
                capacity <- 0
            )

        member x.Validate() =
            validateMemory()
            


        interface IDisposable with
            member x.Dispose() = x.Dispose()


        new() = new MemoryManager(16)

    let runTests() =
        let man = new MemoryManager()

        let r = Random()
        let firstBlock = man.Alloc 1
        let blocks = Array.init 10000 (fun i -> man.Alloc(r.Next(100) + 1))


        let allUnchanged (b : Block) (f : unit -> 'a) : 'a =
            let o = blocks |> Seq.filter (fun bi -> bi <> b) |> Seq.map (fun b -> b.Read(0,b.Size)) |> Seq.toArray 
            let result = f()
            let n = blocks |> Seq.filter (fun bi -> bi <> b) |> Seq.map (fun b -> b.Read(0,b.Size)) |> Seq.toArray 

            let allEqual = Array.forall2 (Array.forall2 (=)) o n
            if not allEqual then
                printfn "ERROR: blocks changed"

            result

        let checkManager() =
            man.Validate()
            let consistent = blocks |> Array.forall (fun b -> not b.IsFree)
            if not consistent then
                printfn "ERROR: blocks are marked as free but are actually not"

        checkManager()

        for b in blocks do
            b.Write(0, Array.zeroCreate b.Size)

        for i in 0..10000 do
            let b = blocks.[r.Next(blocks.Length)]

            let v = r.Next(256) |> byte
            b.Write(0,Array.create b.Size v)
            checkManager()
            let r = b.Read(0, b.Size)
            if r |> Array.forall ((=) v) |> not then
                printfn "ERROR on read: %A" r


            
        for i in 0..10000 do
            let b = blocks.[r.Next(blocks.Length)]
            let oldSize = b.Size
            let newSize = r.Next(2 * b.Size)
            let minSize = Fun.Min(oldSize, newSize)
            let old = b.Read(0, minSize)

            //printfn "resize (%d,%d) to %d" b.Pointer b.Size newSize
            b.Resize(newSize) |> ignore

            let n = b.Read(0, minSize)
            if Seq.zip old n |> Seq.forall (fun (a,b) -> a = b) |> not then
                printfn "ERROR: resize destroyed data: %A" n

            allUnchanged b (fun () ->
                let v = r.Next(256) |> byte
                b.Write(0, Array.create b.Size v)
            )
            checkManager()

    let test() =
        let man = new MemoryManager()





        let b0 = man.Alloc(2)
        let b1 = man.Alloc(2)
        let b2 = man.Alloc(2)
        let b3 = man.Alloc(2)
        let b4 = man.Alloc(2)
        let b5 = man.Alloc(2)
        let b6 = man.Alloc(2)
        let b7 = man.Alloc(2)
        let b8 = man.Alloc(2)



        b1.Dispose()
        b3.Dispose()
        b5.Dispose()
        b7.Dispose()
        man.PrintFree()

        let b1 = man.Alloc(2)
        man.PrintFree()

        b1.Dispose()
        man.PrintFree()

        b2.Dispose()
        man.PrintFree()

        b6.Dispose()
        man.PrintFree()

        b0.Dispose()
        b4.Dispose()
        man.PrintFree()

        b8.Dispose()
        man.PrintFree()

        ()