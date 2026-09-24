namespace Aardvark.Rendering

open FSharp.Data.Adaptive

open System
open System.Collections.Generic

[<AutoOpen>]
module CompactASetExtensions =

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module ASet =

        // Initial and oversized batches allocate exactly; smaller growth is geometric.
        // Shrinking only at quarter-full avoids repeated copies around a count boundary.
        // Nonempty capacity stays below four times the logical count. Keep speculative
        // doubling within the conservative CLR array-length limit; beyond it use exact
        // sizing rather than overflow or reject an otherwise allocatable logical count.
        let private capacityForCount (capacity : int) (count : int) =
            if count = 0 then 0
            elif count > capacity then
                if capacity > 0x7FEFFFFF / 2 then count
                else max count (2 * capacity)
            elif count <= capacity / 4 then count
            else capacity

        /// Assigns an index within [0, n - 1] to each element in the input set, resulting in a compact array layout.
        /// If a removal of elements results in holes, they are filled by moving elements from the end.
        /// Storage growth is amortized; spare capacity does not affect indices and is released when empty.
        let compact (input : aset<'T>) =

            // Between evaluations, only [0, indices.Count) is active, not the whole capacity.
            // Inactive slots are default-valued, including references inside struct keys.
            let mutable keys : 'T[] = Array.empty

            let reader = input.GetReader()

            AMap.custom (fun token indices ->
                let ops = reader.GetChanges token

                // Keep scratch collections local so they cannot retain removed keys across evaluations.
                let added = List(ops.Count)
                let removed = List(ops.Count)

                for o in ops do
                    match o with
                    | Add(_, value) -> value |> added.Add |> ignore
                    | Rem(_, value) -> value |> removed.Add |> ignore

                let delta = added.Count - removed.Count
                let oldCount = indices.Count
                let newCount = oldCount + delta

                // If we remove more values than we add, we have to move some elements from the end (potentially all of them).
                let moving = HashSet([newCount .. newCount - (delta + 1)])

                let free = Queue<int>(removed.Count)
                let deltas = List<'T * ElementOperation<int>>(added.Count + removed.Count + moving.Count)

                // Remove
                for key in removed do
                    deltas.Add(key, Remove)

                    // If the index of the removed element is within the new range, we have a hole to fill.
                    // If it is out of range, the index was marked to be moved but no longer has to be.
                    let index = indices.[key]
                    if index < newCount then
                        free.Enqueue(index)
                    else
                        moving.Remove(index) |> ignore

                // Move
                for i in moving do
                    let newIndex = free.Dequeue()
                    let key = keys.[i]

                    deltas.Add(key, Set newIndex)
                    keys.[newIndex] <- key

                // Move before reallocating, then copy only the surviving prefix. A fresh
                // array already has cleared spare slots; a retained array needs its old tail cleared.
                if delta <> 0 then
                    let capacity = capacityForCount keys.Length newCount
                    if newCount = 0 then
                        keys <- Array.empty
                    elif capacity <> keys.Length then
                        if oldCount = keys.Length then
                            // A full old array has no inactive suffix to copy (including initial/bulk construction).
                            Array.Resize(&keys, capacity)
                        else
                            let storage = Array.zeroCreate<'T> capacity
                            Array.Copy(keys, storage, min oldCount newCount)
                            keys <- storage
                    elif newCount < oldCount then
                        Array.Clear(keys, newCount, oldCount - newCount)

                for i in oldCount .. newCount - 1 do
                    free.Enqueue i

                // Add elements
                for key in added do
                    let i = free.Dequeue()
                    deltas.Add(key, Set i)
                    keys.[i] <- key

                assert(free.Count = 0)

                HashMapDelta.ofSeq deltas
            )