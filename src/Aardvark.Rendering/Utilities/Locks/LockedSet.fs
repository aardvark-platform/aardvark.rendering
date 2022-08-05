namespace Aardvark.Rendering

open System.Collections.Generic

type LockedSet<'T>(elements : seq<'T>) =
    let mutable store = HashSet<'T>(elements)

    new() =
        LockedSet<'T>(Seq.empty)

    /// Adds the given element and returns if it was newly added.
    member x.Add(item : 'T) =
        lock x (fun _ -> store.Add item)

    /// Removes the given element and returns if it was actually removed.
    member x.Remove(item : 'T) =
        lock x (fun _ -> store.Remove item)

    /// Removes all elements from the set and returns them.
    member x.GetAndClear() =
        lock x (fun _ ->
            let res = store
            store <- HashSet<'T>()
            res
        )

    /// Removes all elements from the set.
    member x.Clear() =
        lock x store.Clear