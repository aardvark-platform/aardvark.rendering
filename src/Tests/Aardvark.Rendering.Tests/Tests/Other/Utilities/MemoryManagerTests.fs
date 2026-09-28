namespace Aardvark.Rendering.Tests.Utilities

open System
open Aardvark.Rendering.Management
open Expecto

type private TestAllocator =
    {
        Alloc : nativeint -> nativeint -> Block<byte[]>
        Realloc : Block<byte[]> -> nativeint -> nativeint -> unit
        Free : Block<byte[]> -> unit
        Use : Block<byte[]> -> (byte[] -> nativeint -> nativeint -> unit) -> unit
        Capacity : unit -> nativeint
    }

module MemoryManager =

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

    let private contiguous() =
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

    let private chunked() =
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

    let tests =
        testList "MemoryManager" [
            testCase "Contiguous manager updates size when shrinking" contiguous
            testCase "Chunked manager updates size when shrinking" chunked
        ]
