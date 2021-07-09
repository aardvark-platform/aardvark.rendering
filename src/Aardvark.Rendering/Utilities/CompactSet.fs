namespace Aardvark.Rendering

open FSharp.Data.Adaptive

open System
open System.Collections.Generic

[<AutoOpen>]
module CompactASetExtensions =

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module ASet =

        /// Assigns an index within [0, n - 1] to each element in the input set, resulting in a compact array layout.
        /// If a removal of elements results in holes, they are filled by moving elements from the end.
        let compact (input : aset<'T>) =

            let mutable keys : 'T[] = Array.empty

            let reader = input.GetReader()
            let added = List()
            let removed = List()
            let deltas = List<'T * ElementOperation<int>>()
            let free = Queue<int>()

            AMap.custom (fun token indices ->
                added.Clear()
                removed.Clear()
                deltas.Clear()

                let ops = reader.GetChanges token

                for o in ops do
                    match o with
                    | Add(_, value) -> value |> added.Add |> ignore
                    | Rem(_, value) -> value |> removed.Add |> ignore

                let delta = added.Count - removed.Count
                let oldCount = indices.Count
                let newCount = oldCount + delta

                // If we remove more values than we add, we have to move some elements from the end (potentially all of them).
                let moving = HashSet([newCount .. newCount - (delta + 1)])

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

                // Resize the key array
                Array.Resize(&keys, newCount)

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