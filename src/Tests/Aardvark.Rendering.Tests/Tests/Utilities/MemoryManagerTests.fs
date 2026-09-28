namespace Aardvark.Rendering.Tests.Utilities

open System
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

        let private release (manager : ChunkedMemoryManager<BackingAllocation>) viaRealloc alignment block context =
            if viaRealloc then manager.Realloc(block, alignment, 0n)
            else manager.Free block
            Expect.isTrue block.IsFree $"{context}: released handle"
            Expect.equal (block.Offset, block.Size) (-1n, 0n) $"{context}: released range"
            Expect.isNull block.Prev $"{context}: detached predecessor"
            Expect.isNull block.Next $"{context}: detached successor"

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

    let tests (target: TestTarget) =
        [
            "Contiguous manager updates size when shrinking", Cases.contiguous
            "Chunked manager updates size when shrinking",    Cases.chunked

            "Chunked freed handles adopt fresh chunk storage",                    Cases.reviveFresh
            "Chunked freed handles adopt another live chunk's free space",        Cases.reviveShared
            "Chunked ordinary allocation and active reallocation retain storage", Cases.trackedActiveRealloc
        ]
        |> prepareCasesCpu "MemoryManager" target
