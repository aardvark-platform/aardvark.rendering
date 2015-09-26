namespace Aardvark.Rendering.GL

open System
open System.Runtime.InteropServices
open Aardvark.Base


[<AutoOpen>]
module Fragments =
    open System.Collections.Generic

    let private encodeJump (offset : int) =
        Array.concat [[| 0xE9uy |]; BitConverter.GetBytes(offset - 5)]

    let private decodeJump (arr : byte[]) =
        if arr.Length = 5 && arr.[0] = 0xE9uy then
            BitConverter.ToInt32(arr, 1) + 5
        else
            failwith "is not a jump instruction"

    //Object Layout:
    // | ... PayLoad ... | ... jmp offset ... |
    // |      Size       |       5 bytes      |
    // When no next pointer is set the jump contains a jump to itself (causing non-termination)
    // 
    [<AllowNullLiteral>]
    type Fragment<'a> (manager : MemoryManager, padding : int, tag : 'a) as this =
        static let jumpSize = 8

        // taken from: http://stackoverflow.com/a/12564044
        static let oneByteNop       = [|0x90uy|]
        static let twoByteNop       = [|0x66uy; 0x90uy|]
        static let threeByteNop     = [|0x0Fuy; 0x1Fuy; 0x00uy|]
        static let fourByteNop      = [|0x0Fuy; 0x1Fuy; 0x40uy; 0x00uy|]
        static let fiveByteNop      = [|0x0Fuy; 0x1Fuy; 0x44uy; 0x00uy; 0x00uy|]
        static let eightByteNop     = [|0x0Fuy; 0x1Fuy; 0x84uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy|]

        let modifyLock = obj()
        let mutable isDisposed = false
        let mutable prev : Fragment<'a> = null
        let mutable next : Fragment<'a> = null
        let mutable block = manager.Alloc(jumpSize)
        let mutable currentOffset = -1
        let startOffsets = List<int>()
        let jumpDistance = EventSource(0L)

        let writeJumpStub() =
            match Assembler.cpu with
                | Assembler.AMD64 ->
                    //the jump needs to be 4-byte aligned in order to perform 
                    //atomic writes on its offset.
                    let jumpPosition = block.Size - jumpSize
                    let realJumpPosition = (jumpPosition ||| 3)

                    let before = realJumpPosition - jumpPosition
                    if before > 0 then
                        block.Write(jumpPosition, Array.create before 0x90uy)

                    block.Write(realJumpPosition, [|0xE9uy|])
                    block.WriteInt32(realJumpPosition + 1, -5)
                    let after = 3 - before
                    if after > 0 then
                        block.Write(realJumpPosition + 5, Array.create after 0x90uy) 
                | Assembler.ARM ->
                    //the jump needs to be 4-byte aligned in order to perform 
                    //atomic writes on its offset.
                    let jumpPosition = block.Size - 4


                    let data = 0xEAFFFFFE
                    block.WriteInt32(jumpPosition, data)

                | _ -> failwithf "cannot write jump-stub for cpu: %A" Assembler.cpu

        do //block.Write(0, encodeJump 0)
            writeJumpStub ()
            block.Tag <- this
            startOffsets.Add 0

        member x.Tag = tag

        member x.SizeInBytes =
            block.Size

        member x.Size =
            block.Size - jumpSize

        member x.EntryPointer =
            block.Pointer

        member x.RealPointer =
            block.RealPointer

        member x.Block = block

        member x.Memory = block.Memory

        member private x.WriteJump(offset : int) =
            if manager.Capacity <> 0 then
                match Assembler.cpu with
                    | Assembler.AMD64 ->
                        let jumpPosition = block.Size - jumpSize
                        let realJumpPosition = (jumpPosition ||| 3)
                        if offset = jumpSize then
                            block.Write(jumpPosition, eightByteNop)
                        else
                            if currentOffset = jumpSize then
                                writeJumpStub()
                            //the jump needs to be 4-byte aligned in order to perform 
                            //atomic writes on its offset.
                            block.WriteInt32(realJumpPosition + 1, offset - (5 + realJumpPosition - jumpPosition))

                        // store the current offset
                        currentOffset <- offset

                    | Assembler.ARM ->
                        
                        let jumpPosition = block.Size - 4
                        let data = ((offset / 4 - 2) &&& 0x00FFFFFF) ||| (0xEA000000)
                        block.WriteInt32(jumpPosition, data)

                    | _ -> failwithf "unknown CPU: %A" Assembler.cpu

        member private x.ReadJump() =
            match Assembler.cpu with
                | Assembler.AMD64 ->
                    //the jump needs to be 4-byte aligned in order to perform 
                    //atomic writes on its offset.
                    let jumpPosition = block.Size - jumpSize
                    let realJumpPosition = (jumpPosition ||| 3)
                    let offset = block.ReadInt32(realJumpPosition + 1) + 5
                
                    (realJumpPosition - jumpPosition) + offset
                | Assembler.ARM ->
                    let jumpPosition = block.Size - 4
                    let offset = (block.ReadInt32(jumpPosition) <<< 8) >>> 8
                    (offset + 2) * 4

                | _ -> failwithf "unknown CPU: %A" Assembler.cpu

        member private x.NextPointer
            with get() =
                if manager.Capacity <> 0 then
                    let jumpPosition = block.Size - jumpSize
                    let jumpOffset = x.ReadJump()
                    let targetOffset = jumpPosition + jumpOffset
                    block.Pointer + (nativeint targetOffset)
                else
                    0n

        member x.Freeze() = System.Threading.Monitor.Enter(modifyLock)
        member x.Unfreeze() = System.Threading.Monitor.Exit(modifyLock)

        member f.DefragmentNext() =
            if manager.Capacity <> 0 then
                if isDisposed then
                    failwith "the impossible happened!!! (there is a disposed fragment in the stream)"
                else
                    f.Freeze()
                    let mem = f.Memory
                    let nextFragment : Fragment<'a> = f.Next
                
                    if nextFragment <> null then
                    
                        nextFragment.Freeze()
                        let block = f.Block
                        let nextFragBlock = nextFragment.Block
                        if block.Next <> nextFragBlock then

                            //collect fragments until they have a sufficient size
                            let fragmentsToMove = System.Collections.Generic.List()
                            let freeBlock = 
                                mem.GetSequentialBlockOfMinimalSize(block, nextFragBlock.Size, (fun (b : Block) ->
                                    let f = b.Tag |> unbox<Fragment<'a>>
                                    if f <> null then
                                        f.Freeze()

                                        fragmentsToMove.Add f
                                ))


                            //move the collected fragments to the swap-area
                            for m in fragmentsToMove do
                                let newBlock = mem.Alloc(m.Block.Size)
                                //relocate returns the old-block which will be part of
                                //the freeBlock created below (it is therefore simply ignored here)
                                m.Relocate(newBlock) |> ignore
                                m.Unfreeze()
                            

                            //create a new free block aggregating the collected ones
                            freeBlock.Tag <- nextFragment
                            freeBlock.IsFree <- false


                            //free the unused part of the new freeBlock
                            match freeBlock.Split nextFragBlock.Size with
                                | Some rest -> rest.Dispose()
                                | None -> ()


                            

                            //move nextFragment to freeBlock
                            let oldBlock = nextFragment.Relocate(freeBlock)
                            oldBlock.Dispose()

                        nextFragment.Unfreeze()
                    else
                        () //when there is no next we're done here

                    f.Unfreeze()

        member x.Prev
            with get() = prev
            and set v = lock modifyLock (fun () -> prev <- v)

        member x.Next
            with get() = next
            and set v = 
                lock modifyLock (fun () ->
                    next <- v
                    if next <> null then 
                        let jumpPosition = block.Size - jumpSize
                        let jumpAddress = block.Pointer + (nativeint jumpPosition)
                        let jumpOffset = int (next.EntryPointer - jumpAddress)
                        //jumpDistance.Emit((jumpOffset - jumpSize) |> abs |> int64)
                        x.WriteJump(jumpOffset)
                    else 
                        //jumpDistance.Emit(0L)
                        x.WriteJump(0)
                )

        member x.JumpDistance = jumpDistance :> IEvent<int64>

        member private x.Resize(newSize : int) =
            lock modifyLock (fun () ->
                let newBlockSize = newSize + jumpSize
                let next = x.Next
                let locationStable = block.Resize(newBlockSize)
                writeJumpStub ()
                    
                x.Next <- next

                //if the fragment's location changed
                if not locationStable && prev <> null then
                    //the previous fragment gets a changed next-pointer
                    prev.Next <- x
            )

        //assumes that the caller is holding the modifyLock
        member private x.Relocate(newBlock : Block) : Block =
            if newBlock.Size <> block.Size then
                failwith "newBlock has to match the old one's size"
            if newBlock.Memory <> block.Memory then
                failwith "cannot relocate to different memory"

            //store our nextPointer since the relative offset will change
            //when relocated.
            let next = x.Next
            manager.Copy(newBlock.Pointer, block.Pointer, block.Size)

            let oldBlock = block
            block <- newBlock
            oldBlock.Tag <- null
            block.Tag <- x
                
            //copy the contents and restore the correct nextPointer
            //since the relative offset changes upon relocation.
            x.Next <- next

            //set the entry-pointer for the block
            if prev <> null then prev.Next <- x

            oldBlock
                

        member x.Append (data : byte[]) =
            lock modifyLock (fun () ->
                let oldSize = x.Size
                let newSize = oldSize + data.Length
                if oldSize <> newSize then
                    x.Resize(newSize)
                    block.Write(oldSize, data)

                let id = startOffsets.Count - 1
                startOffsets.Add (newSize)
                id
            )

        member x.Update(id : int, data : byte[]) =
            lock modifyLock (fun () ->
                let start = startOffsets.[id]
                let nextStart = startOffsets.[id + 1]
                let oldSize = nextStart - start

                let delta = data.Length - oldSize
                if delta <> 0 then
                    let oldBlockSize = block.Size
                    if delta > 0 then
                        x.Resize(x.Size + delta)
                        block.Move(nextStart + delta, nextStart, oldBlockSize - jumpSize - nextStart)
                    else
                        block.Move(nextStart + delta, nextStart, oldBlockSize - jumpSize - nextStart)
                        x.Resize(x.Size + delta)

                block.Write(start, data)

                if delta <> 0 then
                    for i in id+1..startOffsets.Count-1 do
                        startOffsets.[i] <- startOffsets.[i] + delta
            )

        member x.Remove(id : int) =
            lock modifyLock (fun () ->
                let start = startOffsets.[id]
                let nextStart = startOffsets.[id + 1]
                let oldSize = nextStart - start

                let oldBlockSize = block.Size
                block.Move(nextStart - oldSize, nextStart, oldBlockSize - jumpSize - nextStart)
                x.Resize(x.Size - oldSize)

                for i in id+1..startOffsets.Count-1 do
                    startOffsets.[i] <- startOffsets.[i] - oldSize
            )

        member x.Clear() =
            lock modifyLock (fun () ->
                x.Resize(0)
                startOffsets.Clear()
                startOffsets.Add 0
            )

        member x.Dispose() =
            lock modifyLock (fun () ->
                if not isDisposed then
                    isDisposed <- true
                    if manager.Capacity <> 0 then
                        block.Dispose()
                    startOffsets.Clear()
                    prev <- null
                    next <- null
            )

        member x.Payload =
            block.Read(0, x.Size)

        member x.Append(calls : seq<nativeint * obj[]>) =
            let data = Amd64.compileCallArray padding calls
            x.Append data

        member x.Update(id : int, calls : seq<nativeint * obj[]>) =
            let data = Amd64.compileCallArray padding calls
            x.Update(id, data)

        member x.FullBinaryCode =
            lock modifyLock (fun () ->
                let codes = List<byte[]>()
                let mutable current = x
                while current <> null do
                    codes.Add current.Payload
                    current <- current.Next
                let data = codes |> Array.concat
                data
            )

        member x.FullInstructions =
            Amd64.Disasm.decompile x.FullBinaryCode

        member x.FullCode =
            x.FullInstructions |> Seq.map (sprintf "%A") |> String.concat "\r\n"

        member private x.Validate (visited : HashSet<Fragment<'a>>, rangeTree : AVL.Tree<Block>) =

            if visited.Contains x then
                printfn "ERROR: cyclic program (non-termination)"
                false
            else
                visited.Add x |> ignore

                if not <| AVL.insert rangeTree block then
                    match AVL.get block rangeTree with
                        | Some r -> printfn "ERROR: found overlapping blocks: %A %A" r block
                        | None -> printfn "ERROR: AVL tree insert failed but could not get overlapping region for: %A" block


                let jumpPosition = block.Size - jumpSize
                let jumpOffset = block.Read(jumpPosition, jumpSize) |> decodeJump
                let isRet = block.Read(jumpPosition - 1, 1).[0] = 0xC3uy

                if jumpOffset = 0 && not isRet then
                        printfn "ERROR: non-termination in jump"
                        false
                else
                    if next <> null then
                        if next.EntryPointer <> x.NextPointer || next.Prev <> x then
                            printfn "ERROR: invalid next/prev pointers in stream"
                            //wiring is incorrect
                            false
                        else next.Validate(visited, rangeTree)
                    else
                        true

        member private x.Validate(calls : array<nativeint * obj[]>, index : int, visited : HashSet<Fragment<'a>>, rangeTree : AVL.Tree<Block>) =
                
            if visited.Contains x then
                printfn "ERROR: cyclic program (non-termination)"
                false
            else
                if not <| AVL.insert rangeTree block then
                    match AVL.get block rangeTree with
                        | Some r -> printfn "ERROR: found overlapping blocks: %A %A" r block
                        | None -> printfn "ERROR: AVL tree insert failed but could not get overlapping region for: %A" block

                visited.Add x |> ignore
                let mutable index = index
                let mutable success = true
                for i in 0..startOffsets.Count - 2 do
                    if index < calls.Length && success then
                        let offset = startOffsets.[i]
                        let size = startOffsets.[i+1] - offset

                        let data = block.Read(offset, size)
                        let cmp = Amd64.compileCall 0 calls.[index]
                        let check = Array.forall2 (=) data cmp 
                        success <- check
                        index <- index + 1

                if success then
                    let jumpPosition = block.Size - jumpSize
                    let jumpOffset = block.Read(jumpPosition, jumpSize) |> decodeJump
                    let isRet = block.Read(jumpPosition - 1, 1).[0] = 0xC3uy
                    if jumpOffset = 0 && not isRet then
                            printfn "ERROR: non-termination in jump"
                            false
                    elif next = null && index < calls.Length then
                        printfn "ERROR: unexpected end of stream"
                        //next is null but there are still calls left
                        false
                    elif next.EntryPointer <> x.NextPointer || next.Prev <> x then
                        printfn "ERROR: invalid next/prev pointers in stream"
                        //wiring is incorrect
                        false
                    elif index < calls.Length then
                        //next is not null and there are calls left
                        next.Validate(calls, index, visited, rangeTree)
                    else 
                        //no calls are left (the stream may go on)
                        true
                else
                    printfn "ERROR: invalid call found in %s:\r\n%s" block.AsString (block.Read(0, block.Size) |> Amd64.Disasm.decompile |> Seq.map (sprintf "%A") |> String.concat "\r\n")
                    false
            
        member x.Validate() = 
            let ranges = AVL.custom (fun (b0 : Block) (b1 : Block) ->
                let s0 = b0.Pointer
                let s1 = b1.Pointer
                let e0 = s0 + nativeint b0.Size - 1n
                let e1 = s1 + nativeint b1.Size - 1n

                if e0 < s1 then -1
                elif s0 > e1 then 1
                else 0
            )
            x.Validate(HashSet(), ranges)
                        
        member x.Validate(calls : array<nativeint * obj[]>) = 
            let ranges = AVL.custom (fun (b0 : Block) (b1 : Block) ->
                let s0 = b0.Pointer
                let s1 = b1.Pointer
                let e0 = s0 + nativeint b0.Size - 1n
                let e1 = s1 + nativeint b1.Size - 1n

                if e0 < s1 then -1
                elif s0 > e1 then 1
                else 0
            )
            x.Validate(calls, 0, HashSet(), ranges)

        member private x.ComputeLengthAndDistance() =
            if next = null then 1,0
            else 
                let c,s = next.ComputeLengthAndDistance()
                let distance = next.EntryPointer - (x.EntryPointer + (nativeint x.Size) + (nativeint jumpSize))
                1+c,(s + Fun.Abs(int distance))

        member x.ComputeAverageJumpDistance() =
            let c,s = x.ComputeLengthAndDistance()
            (float s) / (float c)

        new(manager, padding) = Fragment<'a>(manager, padding, Unchecked.defaultof<'a>)

    (*let testFun : int -> unit = Aardvark.Compiler is dead
        Aardvark.Compiler.C99.compileCode' "test" []
            " #include <stdio.h>
                DLLExport(void) test(int a) 
                {
                    printf(\"test %d\\n\", a);
                    fflush(stdout);
                } " 
                *)

    module FunctionToCall =
        let invocations = System.Collections.Generic.List<int>()

        let reset() =
            invocations.Clear()

        let test(a : int) =
            invocations.Add(a)

        let getCount() =
            invocations.Count


    [<System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)>]
    type IntFun = delegate of int -> unit

    let test() =
        //AVL.test()
        let del = IntFun FunctionToCall.test
        let ptr = Marshal.GetFunctionPointerForDelegate(del)


        if ptr = 0n then printfn "ERROR: could not get function pointer"
        else
            let manager = new MemoryManager()

            let prolog = Fragment(manager, 0)
            let epilog = Fragment(manager, 0)
            let calls = Fragment(manager, 0)

            //let ptr = UnmanagedFunctions.tryFindFunctionPointer(testFun).Value
            calls.Append([ptr,[|10 :> obj|]]) |> ignore
            let id = calls.Append([ptr,[|11 :> obj|]])
            calls.Append([ptr,[|12 :> obj|]]) |> ignore

            

            prolog.Append(Assembler.functionProlog 4) |> ignore
            epilog.Append(Assembler.functionEpilog 4) |> ignore


            prolog.Next <- calls
            calls.Prev <- prolog
            calls.Next <- epilog
            epilog.Prev <- calls

            let f : unit -> unit = UnmanagedFunctions.wrap prolog.RealPointer
            f()
            printfn "%s" prolog.FullCode

            calls.Update(id, [ptr,[|9 :> obj; 12 :> obj|]])
            let f : unit -> unit = UnmanagedFunctions.wrap prolog.RealPointer
            f()
            printfn "%s" prolog.FullCode

            ()

    let private r = Random()


    type IEnumerable<'a> with
        member x.RandomOrder() =
            x |> Seq.map (fun e -> r.Next(), e)
              |> Seq.sortBy fst
              |> Seq.map snd
              |> Seq.cache


    let runTests() =
        let del = IntFun FunctionToCall.test
        let pinned = Marshal.PinDelegate(del)
        let mem = new MemoryManager()
        let ptr = pinned.Pointer

//            Amd64.NativeLogging <- true
//            Amd64.LogFunction <- fun p args ->
//                if p = ptr then
//                    printfn "testFun(%A)" args
//                else
//                    printfn "unknown"

            
            

        if ptr = 0n then printfn "ERROR: could not get function pointer"
        else
            printfn "ptr: %A" ptr

            let prolog = Fragment(mem, 0)
            let epilog = Fragment(mem, 0)
            prolog.Append(Assembler.functionProlog 4) |> ignore
            epilog.Append(Assembler.functionEpilog 4) |> ignore
                
            printfn "creating fragments"
            let frags = Array.init 1000 (fun i -> Fragment(mem, 0, i))
            frags |> Array.iter (fun f -> f.Append([ptr, [|f.Tag :> obj|]]) |> ignore)
            let calls = Array.init frags.Length (fun i -> ptr,[|i :> obj|])
            //let frags = frags.RandomOrder() |> Seq.toArray


            let run() =
                FunctionToCall.reset()

                if not <| prolog.Next.Validate(calls) then
                    printfn "ERROR: invalid stream"
                else
                    UnmanagedFunctions.wrap prolog.RealPointer ()

                    let invocations = FunctionToCall.invocations
                    if calls.Length <> invocations.Count then
                        printfn "ERROR: unexpected call-count: %d" invocations.Count
                    else
                        let c = invocations.Count
                        for i in 0..c-1 do
                            let (p,args) = calls.[i]
                            let arg0 = args.[0] |> unbox<int>
                            if arg0 <> invocations.[i] then
                                printfn "ERROR: unexpected argument: %d (should be %d)" invocations.[i] arg0



            let first = frags.[0]
            let last = frags.[frags.Length - 1]
            first.Prev <- prolog
            prolog.Next <- first
            last.Next <- epilog
            epilog.Prev <- last
            printfn "linking fragments"
            for i in 1..frags.Length-1 do
                let prev = frags.[i-1]
                let next = frags.[i]
                prev.Next <- next
                next.Prev <- prev

            printfn "update tests"
            let r = Random()
            for i in 0..1000 do
                let id = r.Next(frags.Length)
                let f = frags.[id]

                let call = ptr, [|-f.Tag :> obj; f.Tag :> obj|]
                calls.[id] <- call
                f.Clear()
                f.Append([call]) |> ignore
                run()

            printfn "permutation tests"
            for i in 0..100 do
                let perm = Array.init frags.Length id
                let perm = perm.RandomOrder() |> Seq.toArray
                let frags = perm |> Array.map (fun i -> frags.[i])
                let calls = perm |> Array.map (fun i -> calls.[i])

                let first = frags.[0]
                let last = frags.[frags.Length - 1]
                first.Prev <- prolog
                prolog.Next <- first
                last.Next <- epilog
                epilog.Prev <- last
                for i in 1..frags.Length-1 do
                    let prev = frags.[i-1]
                    let next = frags.[i]
                    prev.Next <- next
                    next.Prev <- prev

                prolog.Next.Validate(calls) |> ignore
                mem.Validate()
                let mutable index = 0
                let mutable current = prolog
                while current <> null do
                    current.DefragmentNext()
                    current <- current.Next
                    //printfn "%d" index
                    prolog.Next.Validate(calls) |> ignore
                    index <- index + 1

                mem.Validate();
                    
            printfn "done"
            ()