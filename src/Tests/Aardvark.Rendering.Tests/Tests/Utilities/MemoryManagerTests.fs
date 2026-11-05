namespace Aardvark.Rendering.Tests.Utilities

open System
open System.Collections.Concurrent
open System.Reflection
open System.Threading
open Aardvark.Rendering.Management
open Aardvark.Rendering.Tests
open Expecto

module MemoryManager =

    type private TestAllocator =
        {
            Alloc : nativeint -> nativeint -> Block<byte[]>
            Realloc : Block<byte[]> -> nativeint -> nativeint -> unit
            Free : Block<byte[]> -> unit
            Use : Block<byte[]> -> (byte[] -> nativeint -> nativeint -> unit) -> unit
            Capacity : unit -> nativeint
        }

    module Cases =

        let private validateShrink (allocator : TestAllocator) (releasedCapacity : nativeint) =
            let inspect (block : Block<byte[]>) =
                let mutable memory = Unchecked.defaultof<byte[]>
                let mutable offset = 0n
                let mutable size = 0n
                allocator.Use block (fun m o s ->
                    memory <- m
                    offset <- o
                    size <- s
                )
                memory, offset, size

            let read (block : Block<byte[]>) =
                let mutable result = Array.empty<byte>
                allocator.Use block (fun memory offset size ->
                    result <- Array.init (int size) (fun index -> memory.[int offset + index])
                )
                result

            let write (block : Block<byte[]>) (values : byte[]) =
                allocator.Use block (fun memory offset size ->
                    Expect.equal (int size) values.Length "Write size"
                    Array.Copy(values, 0, memory, int offset, values.Length)
                )

            let prefixGuard = allocator.Alloc 1n 1n
            let alignmentGuard = allocator.Alloc 1n 7n
            let owner = allocator.Alloc 8n 10n
            let blocker = allocator.Alloc 1n 2n
            let expectedPrefix = [| 11uy; 12uy; 13uy; 14uy; 15uy |]

            write prefixGuard [| 97uy |]
            write alignmentGuard [| 91uy; 92uy; 93uy; 94uy; 95uy; 96uy; 98uy |]
            write owner [| 11uy; 12uy; 13uy; 14uy; 15uy; 16uy; 17uy; 18uy; 19uy; 20uy |]
            write blocker [| 101uy; 102uy |]

            let ownerMemory, ownerOffset, _ = inspect owner
            Expect.equal (ownerOffset % 8n) 0n "Initial alignment"

            allocator.Realloc owner 8n 5n

            Expect.equal owner.Offset ownerOffset "Shrinking must preserve the block offset"
            Expect.equal owner.Size 5n "Shrinking must update the reported block size"

            let resizedMemory, resizedOffset, resizedSize = inspect owner
            Expect.isTrue (Object.ReferenceEquals(ownerMemory, resizedMemory)) "Shrinking must preserve the backing memory"
            Expect.equal resizedOffset ownerOffset "Use must receive the preserved offset"
            Expect.equal resizedSize 5n "Use must receive the requested size"
            Expect.equal (read owner) expectedPrefix "Shrinking must preserve the retained prefix"
            Expect.equal (read prefixGuard) [| 97uy |] "Shrinking must not modify preceding allocations"
            Expect.equal (read alignmentGuard) [| 91uy; 92uy; 93uy; 94uy; 95uy; 96uy; 98uy |] "Shrinking must preserve alignment padding allocations"
            Expect.equal (read blocker) [| 101uy; 102uy |] "Shrinking must not modify following allocations"

            let tail = allocator.Alloc 1n 5n
            let tailMemory, tailOffset, tailSize = inspect tail
            Expect.isTrue (Object.ReferenceEquals(ownerMemory, tailMemory)) "The released tail must remain in the same backing allocation"
            Expect.equal tailOffset (ownerOffset + owner.Size) "The released tail must be immediately reusable"
            Expect.equal tailSize 5n "The reused tail size"
            Expect.isLessThanOrEqual (ownerOffset + owner.Size) tailOffset "Live ranges must not overlap after tail reuse"

            write tail [| 201uy; 202uy; 203uy; 204uy; 205uy |]
            Expect.equal (read owner) expectedPrefix "Writing the reused tail must not modify the retained prefix"
            Expect.equal (read blocker) [| 101uy; 102uy |] "Writing the reused tail must not modify the following allocation"

            allocator.Free owner
            allocator.Free tail

            let coalesced = allocator.Alloc 1n 10n
            let coalescedMemory, coalescedOffset, coalescedSize = inspect coalesced
            Expect.isTrue (Object.ReferenceEquals(ownerMemory, coalescedMemory)) "Freed neighboring ranges must remain in the same backing allocation"
            Expect.equal coalescedOffset ownerOffset "Freed neighboring ranges must coalesce"
            Expect.equal coalescedSize 10n "Coalesced range size"

            allocator.Free coalesced
            allocator.Free blocker
            allocator.Free alignmentGuard
            allocator.Free prefixGuard
            Expect.equal (allocator.Capacity()) releasedCapacity "Capacity after freeing the fully coalesced allocation"

            let full = allocator.Alloc 1n 64n
            let _, fullOffset, fullSize = inspect full
            Expect.equal fullOffset 0n "A full-capacity allocation must start at zero"
            Expect.equal fullSize 64n "A full-capacity allocation must use the complete range"
            allocator.Free full
            Expect.equal (allocator.Capacity()) releasedCapacity "Capacity after freeing the full allocation"

        let contiguous() =
            use manager = new MemoryManager<byte[]>(Memory.array<byte>, 16n)
            validateShrink
                {
                    Alloc = fun align size -> manager.Alloc(align, size)
                    Realloc = fun block align size -> manager.Realloc(block, align, size)
                    Free = manager.Free
                    Use = fun block action -> manager.Use(block, action)
                    Capacity = fun () -> manager.Capactiy
                }
                16n

        let chunked() =
            use manager = new ChunkedMemoryManager<byte[]>(Memory.array<byte>, 64n)
            validateShrink
                {
                    Alloc = fun align size -> manager.Alloc(align, size)
                    Realloc = fun block align size -> manager.Realloc(block, align, size)
                    Free = manager.Free
                    Use = fun block action -> manager.Use(block, action)
                    Capacity = fun () -> manager.Capactiy
                }
                0n

        [<ReferenceEquality>]
        type private BackingAllocation =
            { Id : int; Data : byte[]; mutable Frees : int }

        let private trackedMemory() =
            let allocations = ResizeArray<BackingAllocation>()
            let memory =
                {
                    malloc = fun size ->
                        let allocation = { Id = allocations.Count; Data = Array.zeroCreate<byte> (int size); Frees = 0 }
                        allocations.Add allocation
                        allocation
                    mfree = fun allocation size ->
                        Expect.equal allocation.Frees 0 $"Allocation {allocation.Id} must be freed only once"
                        Expect.equal (nativeint allocation.Data.Length) size $"Allocation {allocation.Id} release size"
                        allocation.Frees <- allocation.Frees + 1
                    mcopy = fun _ _ _ _ _ -> failtest "Chunked reallocation must not copy backing storage"
                    mrealloc = fun _ _ _ -> failtest "Chunked reallocation must not resize backing storage"
                }
            memory, allocations

        let private same expected actual message =
            Expect.isTrue (Object.ReferenceEquals(expected, actual)) message

        let private checkBlock (manager : ChunkedMemoryManager<BackingAllocation>) (block : Block<BackingAllocation>) storage offset size alignment context =
            let actual, actualOffset, actualSize = manager.Use(block, fun m o s -> m, o, s)
            same storage actual $"{context}: expected backing allocation {storage.Id}, got {actual.Id}"
            Expect.equal actual.Frees 0 $"{context}: backing allocation {actual.Id} must be live"
            Expect.isFalse block.IsFree $"{context}: caller's block is active"
            same manager block.Parent $"{context}: parent is retained"
            Expect.equal (block.Offset, block.Size) (offset, size) $"{context}: block range"
            Expect.equal (actualOffset, actualSize) (offset, size) $"{context}: Use range"
            Expect.equal (offset % alignment) 0n $"{context}: alignment {alignment}"
            Expect.isGreaterThanOrEqual offset 0n $"{context}: nonnegative offset"
            Expect.isLessThanOrEqual (offset + size) (nativeint actual.Data.Length) $"{context}: range fits backing allocation"
            if not (isNull block.Prev) then
                same block block.Prev.Next $"{context}: predecessor links to the caller's block"
                same block.Memory block.Prev.Memory $"{context}: predecessor shares the memory reference"
                Expect.equal (block.Prev.Offset + block.Prev.Size) offset $"{context}: predecessor is adjacent"
            if not (isNull block.Next) then
                same block block.Next.Prev $"{context}: successor links to the caller's block"
                same block.Memory block.Next.Memory $"{context}: successor shares the memory reference"
                Expect.equal block.Next.Offset (offset + size) $"{context}: successor is adjacent"

        let private write (manager : ChunkedMemoryManager<BackingAllocation>) block value =
            manager.Use(block, fun memory offset size ->
                Expect.equal memory.Frees 0 $"Cannot write released allocation {memory.Id}"
                Array.Fill(memory.Data, value, int offset, int size)
            )

        let private read (manager : ChunkedMemoryManager<BackingAllocation>) block =
            manager.Use(block, fun memory offset size ->
                Expect.equal memory.Frees 0 $"Cannot read released allocation {memory.Id}"
                Array.sub memory.Data (int offset) (int size)
            )

        let private detached (block : Block<BackingAllocation>) context =
            Expect.isTrue block.IsFree $"{context}: released handle"
            Expect.equal (block.Offset, block.Size) (-1n, 0n) $"{context}: released range"
            Expect.isNull block.Prev $"{context}: detached predecessor"
            Expect.isNull block.Next $"{context}: detached successor"

        let private release (manager : ChunkedMemoryManager<BackingAllocation>) viaRealloc alignment block context =
            if viaRealloc then manager.Realloc(block, alignment, 0n)
            else manager.Free block
            detached block context

        let private balanced (manager : ChunkedMemoryManager<BackingAllocation>) allocations expectedCount context =
            Expect.equal manager.Capactiy 0n $"{context}: final freeing releases all capacity"
            // The manager's zero-sized sentinel is not a positive-sized backing allocation.
            let positive = allocations |> Seq.filter (fun a -> a.Data.Length > 0) |> Seq.toArray
            Expect.equal positive.Length expectedCount $"{context}: positive-sized allocation count"
            for allocation in positive do
                Expect.equal allocation.Frees 1 $"{context}: allocation {allocation.Id} must be released exactly once"

        let reviveFresh() =
            for viaRealloc in [false; true] do
                for alignment in [1n; 3n; 8n; 32n; 64n] do
                    let memory, allocations = trackedMemory()
                    use manager = new ChunkedMemoryManager<_>(memory, 64n)
                    let block = manager.Alloc 7n
                    for size in [5n; 64n; 97n; 9n] do
                        let context = $"fresh chunk, Realloc(0)={viaRealloc}, alignment={alignment}, size={size}"
                        let oldReference = block.Memory
                        let oldStorage = oldReference.Value
                        release manager viaRealloc alignment block context
                        Expect.equal oldStorage.Frees 1 $"{context}: old chunk was released"
                        Expect.equal manager.Capactiy 0n $"{context}: no old capacity remains"

                        manager.Realloc(block, alignment, size)
                        let storage = allocations.[allocations.Count - 1]
                        checkBlock manager block storage 0n size alignment context
                        Expect.isFalse (Object.ReferenceEquals(oldReference, block.Memory)) $"{context}: adopt a new memory reference"
                        same oldStorage oldReference.Value $"{context}: do not retarget the old memory reference"
                        Expect.equal manager.Capactiy (max 64n size) $"{context}: replacement chunk capacity"
                        write manager block 0x5Auy
                        Expect.equal (read manager block) (Array.create (int size) 0x5Auy) $"{context}: revived payload"
                    manager.Free block
                    balanced manager allocations 5 $"fresh chunks, Realloc(0)={viaRealloc}, alignment={alignment}"

        let reviveShared() =
            for viaRealloc in [false; true] do
                for keepOldChunk in [false; true] do
                    for alignment in [1n; 3n; 8n; 32n; 64n] do
                        let context = $"shared chunk, Realloc(0)={viaRealloc}, keepOldChunk={keepOldChunk}, alignment={alignment}"
                        let memory, allocations = trackedMemory()
                        use manager = new ChunkedMemoryManager<_>(memory, 128n)
                        let block = manager.Alloc(if keepOldChunk then 120n else 128n)
                        let oldReference = block.Memory
                        let oldStorage = oldReference.Value
                        let keeper = if keepOldChunk then Some (manager.Alloc 8n) else None
                        keeper |> Option.iter (fun b -> write manager b 0xC3uy)
                        let prefix = manager.Alloc 5n
                        let hole = manager.Alloc 91n
                        let suffix = manager.Alloc 32n
                        let replacementReference = hole.Memory
                        Expect.isFalse (Object.ReferenceEquals(oldReference, replacementReference)) $"{context}: distinct fixture chunks"
                        write manager prefix 0xA1uy
                        write manager suffix 0xB2uy
                        manager.Free hole
                        release manager viaRealloc alignment block context
                        Expect.equal oldStorage.Frees (if keepOldChunk then 0 else 1) $"{context}: old chunk liveness"

                        // The 91-byte hole wins over the old chunk's 120-byte hole, if that chunk remains live.
                        let offset = ((5n + alignment - 1n) / alignment) * alignment
                        for size in [17n; 9n; 23n] do
                            let context = $"{context}, revived size={size}"
                            manager.Realloc(block, alignment, size)
                            checkBlock manager block replacementReference.Value offset size alignment context
                            same replacementReference block.Memory $"{context}: adopt the replacement's memory reference"
                            same oldStorage oldReference.Value $"{context}: preserve the old chunk reference"
                            Expect.equal manager.Capactiy (if keepOldChunk then 256n else 128n) $"{context}: existing capacity is reused"
                            write manager block (byte size)
                            Expect.equal (read manager block) (Array.create (int size) (byte size)) $"{context}: revived payload"
                            Expect.equal (read manager prefix) (Array.create 5 0xA1uy) $"{context}: preceding payload is isolated"
                            Expect.equal (read manager suffix) (Array.create 32 0xB2uy) $"{context}: following payload is isolated"
                            keeper |> Option.iter (fun b -> Expect.equal (read manager b) (Array.create 8 0xC3uy) $"{context}: old chunk payload is isolated")
                            release manager viaRealloc alignment block context

                        let coalesced = manager.Alloc 91n
                        checkBlock manager coalesced replacementReference.Value 5n 91n 1n context
                        manager.Free coalesced
                        manager.Free prefix
                        manager.Free suffix
                        Expect.equal replacementReference.Value.Frees 1 $"{context}: replacement chunk is released"
                        Expect.equal manager.Capactiy (if keepOldChunk then 128n else 0n) $"{context}: only the keeper may remain"
                        keeper |> Option.iter manager.Free
                        balanced manager allocations 2 context

        let private freeMonitor (manager : ChunkedMemoryManager<BackingAllocation>) =
            // Synchronize on the real allocator monitor without exposing a production test hook.
            typeof<ChunkedMemoryManager<BackingAllocation>>.GetFields(BindingFlags.Instance ||| BindingFlags.NonPublic)
            |> Array.filter (fun field -> field.FieldType = typeof<FreeList<BackingAllocation>>)
            |> Array.exactlyOne
            |> fun field -> field.GetValue manager

        let private freeUnderMonitor (manager : ChunkedMemoryManager<BackingAllocation>)
                                     (blocks : Block<BackingAllocation>[]) waitForMonitor owner context =
            let gate = freeMonitor manager
            let errors = ConcurrentQueue<exn>()
            let ready = Array.zeroCreate<int> blocks.Length
            let workers = blocks |> Array.mapi (fun index block ->
                Thread(ThreadStart(fun () ->
                    // No event waits or other blocking operations occur before Free.
                    Volatile.Write(&ready.[index], 1)
                    try manager.Free block
                    with error -> errors.Enqueue error
                ), IsBackground = true)
            )
            let started = ResizeArray<Thread>()
            let timeout = TimeSpan.FromSeconds 10.0
            let mutable drained = true
            Monitor.Enter gate
            try
                for worker in workers do
                    worker.Start()
                    started.Add worker
                if waitForMonitor then
                    for index in 0 .. workers.Length - 1 do
                        let worker = workers.[index]
                        let blocked = SpinWait.SpinUntil((fun () ->
                            Volatile.Read(&ready.[index]) = 1 &&
                            (worker.ThreadState &&& ThreadState.WaitSleepJoin) <> enum<ThreadState> 0
                        ), timeout)
                        Expect.isTrue blocked $"{context}: worker {index} must pass the outer check and block on the free-list monitor"
                    owner |> Option.iter manager.Free
                else
                    // Already-free callers must complete even while another thread holds the monitor.
                    for index in 0 .. workers.Length - 1 do
                        Expect.isTrue (workers.[index].Join timeout) $"{context}: duplicate {index} must retain the unlocked fast path"
            finally
                Monitor.Exit gate
                for worker in started do
                    if not (worker.Join timeout) then
                        try worker.Interrupt() with :? ThreadStateException -> ()
                        drained <- worker.Join(TimeSpan.FromSeconds 2.0) && drained
            Expect.isTrue drained $"{context}: all workers must finish within the timeout"
            errors.ToArray()

        let private noFreeErrors (errors : exn[]) context =
            if errors.Length > 0 then failtestf "%s: Free must complete without exceptions: %A" context errors

        let private competingFree manager block context =
            freeUnderMonitor manager [| block; block |] true (Some block) context

        let private duplicateFree (manager : ChunkedMemoryManager<BackingAllocation>) block context =
            manager.Free block
            for _ in 1 .. 8 do manager.Free block
            freeUnderMonitor manager [| block; block |] false None context

        let private isolatedFree freeBlock chunkSize size context =
            let memory, allocations = trackedMemory()
            use manager = new ChunkedMemoryManager<_>(memory, chunkSize)
            let block = manager.Alloc size
            let reference = block.Memory
            let storage = reference.Value
            write manager block 0xB1uy
            checkBlock manager block storage 0n size 1n context
            let errors = freeBlock manager block context
            noFreeErrors errors context
            detached block context
            same reference block.Memory $"{context}: old storage reference is retained"
            Expect.equal storage.Frees 1 $"{context}: isolated backing is retired exactly once"
            Expect.equal manager.Capactiy 0n $"{context}: isolated capacity is released"

            let fresh = manager.Alloc chunkSize
            Expect.isFalse (Object.ReferenceEquals(storage, fresh.Memory.Value)) $"{context}: released backing is not reused"
            checkBlock manager fresh fresh.Memory.Value 0n chunkSize 1n context
            Expect.equal manager.Capactiy chunkSize $"{context}: full range can be allocated again"
            write manager fresh 0xD4uy
            Expect.equal (read manager fresh) (Array.create (int chunkSize) 0xD4uy) $"{context}: fresh payload"
            manager.Free fresh
            balanced manager allocations 2 context

        let private sharedFree freeBlock chunkSize position context =
            let memory, allocations = trackedMemory()
            use manager = new ChunkedMemoryManager<_>(memory, chunkSize)
            let size = chunkSize / 4n
            let blocks = Array.init 3 (fun _ -> manager.Alloc size)
            let subject = blocks.[position]
            let reference = subject.Memory
            let storage = reference.Value
            for index in 0 .. blocks.Length - 1 do
                write manager blocks.[index] (byte (0xA0 + index))
            let siblings = blocks |> Array.indexed |> Array.filter (fun (index, _) -> index <> position)
            let checkSiblings() =
                for index, block in siblings do
                    let context = $"{context}, sibling={index}"
                    checkBlock manager block storage (nativeint index * size) size 1n context
                    Expect.equal (read manager block) (Array.create (int size) (byte (0xA0 + index))) $"{context}: intact sibling payload"
            let errors = freeBlock manager subject context
            Expect.equal storage.Frees 0 $"{context}: competing frees must not retire backing with live siblings"
            noFreeErrors errors context
            detached subject context
            same reference subject.Memory $"{context}: released handle retains its storage reference"
            Expect.equal manager.Capactiy chunkSize $"{context}: live chunk retains its capacity"
            checkSiblings()

            let reused = manager.Alloc size
            same reference reused.Memory $"{context}: the freed range is reused in the same chunk"
            checkBlock manager reused storage (nativeint position * size) size 1n context
            Expect.equal manager.Capactiy chunkSize $"{context}: reusing the freed range needs no additional capacity"
            write manager reused 0xD4uy
            Expect.equal (read manager reused) (Array.create (int size) 0xD4uy) $"{context}: reused payload"
            checkSiblings()
            manager.Free reused
            checkSiblings()
            for _, block in siblings do manager.Free block
            balanced manager allocations 1 context

        let competingIsolatedFrees() =
            for chunkSize in [16n; 64n; 256n] do
                for size in [chunkSize / 4n; chunkSize] do
                    isolatedFree competingFree chunkSize size $"competing isolated frees, chunk={chunkSize}, size={size}"

        let competingSiblingFrees() =
            for chunkSize in [16n; 64n; 256n] do
                for position in [0; 1; 2] do
                    sharedFree competingFree chunkSize position $"competing sibling frees, chunk={chunkSize}, position={position}"

        let sequentialDuplicateFrees() =
            for chunkSize in [16n; 64n; 256n] do
                isolatedFree duplicateFree chunkSize (chunkSize / 4n) $"sequential isolated duplicates, chunk={chunkSize}"
                for position in [0; 1; 2] do
                    sharedFree duplicateFree chunkSize position $"sequential sibling duplicates, chunk={chunkSize}, position={position}"

        let concurrentDistinctFrees() =
            for chunkSize in [16n; 64n; 256n] do
                let context = $"concurrent distinct frees, chunk={chunkSize}"
                let memory, allocations = trackedMemory()
                use manager = new ChunkedMemoryManager<_>(memory, chunkSize)
                let blocks = Array.init 3 (fun _ -> manager.Alloc(chunkSize / 4n))
                let storage = blocks.[0].Memory.Value
                let errors = freeUnderMonitor manager blocks true None context
                noFreeErrors errors context
                for index, block in Array.indexed blocks do detached block $"{context}, block={index}"
                Expect.equal storage.Frees 1 $"{context}: the final distinct free retires backing once"
                balanced manager allocations 1 context

        let trackedActiveRealloc() =
            for alignment in [1n; 3n; 8n; 32n; 64n] do
                let memory, allocations = trackedMemory()
                use manager = new ChunkedMemoryManager<_>(memory, 128n)
                let prefix = manager.Alloc 5n
                let block = manager.Alloc(alignment, 24n)
                let reference = block.Memory
                let offset = ((5n + alignment - 1n) / alignment) * alignment
                let context = $"active block, alignment={alignment}"
                checkBlock manager block reference.Value offset 24n alignment context
                write manager prefix 0xA1uy
                write manager block 0xD4uy
                for size in [12n; 32n; 32n; 128n - offset] do
                    let context = $"{context}, size={size}"
                    let retained = read manager block |> Array.take (int (min block.Size size))
                    manager.Realloc(block, alignment, size)
                    checkBlock manager block reference.Value offset size alignment context
                    same reference block.Memory $"{context}: active storage reference is unchanged"
                    Expect.equal (read manager block |> Array.take retained.Length) retained $"{context}: retained payload"
                    Expect.equal (read manager prefix) (Array.create 5 0xA1uy) $"{context}: neighbor payload"
                    Expect.equal manager.Capactiy 128n $"{context}: active reallocation retains capacity"
                    write manager block 0xD4uy
                Expect.isNull block.Next $"{context}: growth consumes the complete trailing free range"
                manager.Free block
                manager.Free prefix
                balanced manager allocations 1 context

    module private Reallocation =
        type private Handle = Live | Empty | Freed | Zeroed

        [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
        let private throwAllocator (error : exn) = raise error

        let private withAllocator chunked aligned capacity onCall action =
            let calls = Array.zeroCreate<int> 4
            let mark index = calls.[index] <- calls.[index] + 1; onCall index
            let source = Memory.array<byte>
            let memory =
                { malloc = fun size ->
                    mark 0
                    // Original-source probes must remain bounded managed allocations.
                    if size < 0n || size > 4096n then failtestf "Unexpected backing allocation %d" size
                    source.malloc size
                  mfree = fun data size -> mark 1; source.mfree data size
                  mcopy = fun src srcOffset dst dstOffset size -> mark 2; source.mcopy src srcOffset dst dstOffset size
                  mrealloc = fun data oldSize newSize ->
                    mark 3
                    if newSize < 0n || newSize > 4096n then failtestf "Unexpected backing resize %d" newSize
                    source.mrealloc data oldSize newSize }
            let counts() = Array.toList calls
            if chunked then
                use manager = new ChunkedMemoryManager<byte[]>(memory, capacity)
                action
                    { Alloc = fun align size -> manager.Alloc(align, size)
                      Realloc = fun block align size -> if aligned then manager.Realloc(block, align, size) else manager.Realloc(block, size)
                      Free = manager.Free
                      Use = fun block callback -> manager.Use(block, callback)
                      Capacity = fun () -> manager.Capactiy }
                    counts
            else
                use manager = new MemoryManager<byte[]>(memory, capacity)
                action
                    { Alloc = fun align size -> manager.Alloc(align, size)
                      Realloc = fun block align size -> if aligned then manager.Realloc(block, align, size) else manager.Realloc(block, size)
                      Free = manager.Free
                      Use = fun block callback -> manager.Use(block, callback)
                      Capacity = fun () -> manager.Capactiy }
                    counts

        let private same expected actual context =
            Expect.isTrue (Object.ReferenceEquals(expected, actual)) context

        let private snapshot (block : Block<byte[]>) =
            let parent, memory, previous, next = block.Parent, block.Memory, block.Prev, block.Next
            let offset, size, isFree = block.Offset, block.Size, block.IsFree
            let backing = memory.Value
            let payload = Array.copy backing
            fun context ->
                same parent block.Parent (context + ": parent identity")
                same memory block.Memory (context + ": storage reference identity")
                same backing block.Memory.Value (context + ": backing array identity")
                same previous block.Prev (context + ": predecessor identity")
                same next block.Next (context + ": successor identity")
                Expect.equal (block.Offset, block.Size, block.IsFree) (offset, size, isFree) (context + ": range and liveness")
                Expect.equal backing payload (context + ": complete backing payload")

        let private neighborhood (blocks : Block<byte[]>[]) =
            blocks |> Array.collect (fun b -> [| b; b.Prev; b.Next |])
            |> Array.filter (isNull >> not) |> Array.distinct |> Array.map snapshot

        let private read (allocator : TestAllocator) block =
            let mutable result = Array.empty<byte>
            allocator.Use block (fun data offset size ->
                Expect.isGreaterThanOrEqual offset 0n "Bounded managed read offset"
                Expect.isGreaterThanOrEqual size 0n "Bounded managed read size"
                Expect.isLessThanOrEqual (offset + size) (nativeint data.Length) "Bounded managed read endpoint"
                result <- Array.sub data (int offset) (int size))
            result

        let private fill (allocator : TestAllocator) block value =
            allocator.Use block (fun data offset size ->
                Expect.isGreaterThanOrEqual offset 0n "Bounded managed write offset"
                Expect.isGreaterThanOrEqual size 0n "Bounded managed write size"
                Expect.isLessThanOrEqual (offset + size) (nativeint data.Length) "Bounded managed write endpoint"
                Array.Fill(data, value, int offset, int size))

        let private collect (failures : ResizeArray<string>) context action =
            try action() with error -> failures.Add($"{context}: {error.Message}")

        let private negative handles =
            let failures = ResizeArray<string>()
            for chunked in [false; true] do
                for aligned in [false; true] do
                    for alignment in [1n; 3n; 8n] do
                        for handle in handles do
                            for size in [-1n; -4n; -17n; nativeint Int32.MinValue; IntPtr.MinValue] |> List.distinct do
                                let context = $"chunked={chunked}, aligned overload={aligned}, alignment={alignment}, handle={handle}, size={size}"
                                withAllocator chunked aligned 64n ignore (fun allocator counts ->
                                    let prefix = allocator.Alloc 1n 8n
                                    let block = allocator.Alloc alignment (if handle = Empty then 0n else 4n)
                                    let suffix = allocator.Alloc 1n 8n
                                    if handle = Freed then allocator.Free block
                                    if handle = Zeroed then allocator.Realloc block alignment 0n
                                    fill allocator prefix 0xA1uy
                                    fill allocator suffix 0xB2uy
                                    if handle = Live then fill allocator block 0xC3uy
                                    let state = neighborhood [| prefix; block; suffix |]
                                    let capacity, callbacks = allocator.Capacity(), counts()
                                    let error = try allocator.Realloc block alignment size; None with error -> Some error
                                    collect failures (context + ": rejection") (fun () ->
                                        match error with
                                        | Some (:? ArgumentOutOfRangeException as error) -> Expect.equal error.ParamName "size" "Negative size parameter"
                                        | _ -> failtestf "Expected ArgumentOutOfRangeException(size), got %A" error)
                                    collect failures (context + ": unchanged") (fun () ->
                                        Expect.equal (allocator.Capacity()) capacity "Capacity after rejection"
                                        Expect.equal (counts()) callbacks "No allocator callback after rejection"
                                        for check in state do check context)
                                    // The concrete -4 bug creates a reusable range overlapping a live guard.
                                    // Its original-source addresses remain within the 64-byte managed backing.
                                    if size = -4n then
                                        collect failures (context + ": subsequent allocation") (fun () ->
                                            let fresh = allocator.Alloc 1n 8n
                                            fill allocator fresh 0xD4uy
                                            Expect.equal (read allocator prefix) (Array.create 8 0xA1uy) "Allocation after rejection preserves the preceding live guard"
                                            Expect.equal (read allocator suffix) (Array.create 8 0xB2uy) "Allocation after rejection preserves the following live guard"
                                            if handle = Live then Expect.equal (read allocator block) (Array.create 4 0xC3uy) "Allocation after rejection preserves the live owner"))
            Expect.isEmpty failures (String.concat "\n" failures)

        let negativeLive() = negative [Live]
        let negativeInactive() = negative [Empty; Freed; Zeroed]

        let successfulResizes() =
            for chunked in [false; true] do
                for aligned in [false; true] do
                    for alignment in [1n; 3n; 8n; 32n] do
                        for size in [4n; 17n; 32n] do
                            let context = $"chunked={chunked}, aligned overload={aligned}, alignment={alignment}, initial size={size}"
                            withAllocator chunked aligned 256n ignore (fun allocator counts ->
                                let prefix = allocator.Alloc 1n alignment
                                let block = allocator.Alloc alignment size
                                let holeSize = size + 8n
                                let hole = allocator.Alloc 1n holeSize
                                let suffix = allocator.Alloc 1n 7n
                                allocator.Free hole
                                fill allocator prefix 0xA1uy
                                fill allocator suffix 0xB2uy
                                fill allocator block 0xC3uy
                                let reference, storage, offset = block.Memory, block.Memory.Value, block.Offset
                                let capacity, callbacks = allocator.Capacity(), counts()
                                let unchanged = neighborhood [| prefix; block; suffix |]
                                allocator.Realloc block alignment size
                                for check in unchanged do check (context + ": same size")
                                for requested in [size / 2n; size + 4n; size + holeSize] do
                                    let retained = read allocator block |> Array.take (int (min block.Size requested))
                                    allocator.Realloc block alignment requested
                                    same reference block.Memory (context + ": in-place storage reference")
                                    same storage block.Memory.Value (context + ": in-place backing identity")
                                    Expect.equal (block.Offset, block.Size, block.IsFree) (offset, requested, false) (context + ": retained handle range")
                                    Expect.equal (offset % alignment) 0n (context + ": alignment")
                                    Expect.equal (read allocator block |> Array.take retained.Length) retained (context + ": retained prefix")
                                    fill allocator block 0xC3uy
                                same block suffix.Prev (context + ": in-place growth consumes the free neighbor")
                                allocator.Realloc block alignment 0n
                                Expect.equal (block.Offset, block.Size, block.IsFree) (-1n, 0n, true) (context + ": zero frees the handle")
                                Expect.isNull block.Prev (context + ": detached predecessor")
                                Expect.isNull block.Next (context + ": detached successor")
                                same reference block.Memory (context + ": zero retains the storage reference")
                                let zero = neighborhood [| prefix; block; suffix |]
                                allocator.Realloc block alignment 0n
                                for check in zero do check (context + ": repeated zero")
                                Expect.equal (allocator.Capacity()) capacity (context + ": capacity")
                                Expect.equal (counts()) callbacks (context + ": same-size, shrink, in-place growth and zero need no backing callbacks")
                                let reused = allocator.Alloc alignment (size + holeSize)
                                Expect.equal reused.Offset offset (context + ": freed range is reusable with the original alignment")
                                fill allocator reused 0xD4uy
                                Expect.equal (read allocator prefix) (Array.create (int alignment) 0xA1uy) (context + ": preceding guard")
                                Expect.equal (read allocator suffix) (Array.create 7 0xB2uy) (context + ": following guard"))

        let failurePropagation() =
            for aligned in [false; true] do
                for callback in [2; 3] do
                    let context = $"aligned overload={aligned}, copy/realloc callback={callback}"
                    let expected = ApplicationException(context)
                    let mutable armed = false
                    withAllocator false aligned 64n (fun index -> if armed && index = callback then throwAllocator expected) (fun allocator _ ->
                        let block = allocator.Alloc 1n 4n
                        allocator.Alloc 1n 8n |> ignore
                        armed <- true
                        let actual = try allocator.Realloc block 8n (if callback = 2 then 16n else 128n); None with error -> Some error
                        match actual with
                        | Some error ->
                            same expected error (context + ": original callback error")
                            Expect.stringContains error.StackTrace "throwAllocator" (context + ": original throw site")
                        | None -> failtest (context + ": callback failure was lost"))
                withAllocator true aligned 64n ignore (fun allocator counts ->
                    let block = allocator.Alloc 1n 4n
                    let suffix = allocator.Alloc 1n 8n
                    let unchanged, callbacks = neighborhood [| block; suffix |], counts()
                    let actual = try allocator.Realloc block 8n 16n; None with error -> Some error
                    match actual with
                    | Some error -> Expect.equal error.Message "[MemoryManager] cannot realloc when no mcopy given" "Existing chunked relocation error"
                    | None -> failtest "Blocked chunked growth must retain its existing failure"
                    for check in unchanged do check "Blocked chunked growth"
                    Expect.equal (counts()) callbacks "Blocked chunked growth does not call the allocator")

    let tests (target: TestTarget) =
        [
            "Contiguous manager updates size when shrinking", Cases.contiguous
            "Chunked manager updates size when shrinking",    Cases.chunked

            "Chunked freed handles adopt fresh chunk storage",                    Cases.reviveFresh
            "Chunked freed handles adopt another live chunk's free space",        Cases.reviveShared
            "Chunked ordinary allocation and active reallocation retain storage", Cases.trackedActiveRealloc

            "Chunked competing frees retire isolated backing once",        Cases.competingIsolatedFrees
            "Chunked competing frees preserve live siblings",              Cases.competingSiblingFrees
            "Chunked sequential duplicate frees retain the fast path",     Cases.sequentialDuplicateFrees
            "Chunked concurrent distinct frees coalesce and retire once",  Cases.concurrentDistinctFrees

            "Reallocation.Negative live sizes preserve owners",    Reallocation.negativeLive
            "Reallocation.Negative inactive sizes preserve state", Reallocation.negativeInactive
            "Reallocation.Successful resizes retain behavior",     Reallocation.successfulResizes
            "Reallocation.Valid failures retain their errors",     Reallocation.failurePropagation
        ]
        |> prepareCasesCpu "MemoryManager" target
