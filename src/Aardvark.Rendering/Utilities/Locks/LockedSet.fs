namespace Aardvark.Rendering

open System.Collections.Generic

type internal LockedSet<'T>() =
    let mutable store = HashSet<'T>()

    /// Adds the given element and returns if it was newly added.
    member inline x.Add(item : 'T) =
        lock x (fun _ -> store.Add item)

    /// Removes the given element and returns if it was actually removed.
    member inline x.Remove(item : 'T) =
        lock x (fun _ -> store.Remove item)

    /// Removes all elements from the set and returns them.
    member inline x.GetAndClear() =
        lock x (fun _ ->
            let res = store
            store <- HashSet<'T>()
            res
        )

    /// Removes all elements from the set.
    member inline x.Clear() =
        lock x store.Clear