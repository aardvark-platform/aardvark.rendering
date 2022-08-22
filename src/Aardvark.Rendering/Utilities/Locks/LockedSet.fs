namespace Aardvark.Rendering

open System.Collections.Generic

type LockedSet<'T>(elements : seq<'T>) =
    let mutable store = HashSet<'T>(elements)

    new() =
        LockedSet<'T>(Seq.empty)

    /// Gets the number of elements that are contained in the set.
    member x.Count =
        store.Count

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

    /// Locks the set and performs the given action.
    member x.Lock(action : HashSet<'T> -> 'U) =
        lock x (fun _ -> action store)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module LockedSet =

    /// Locks the set and performs the given action.
    let lock (action : HashSet<'T> -> 'U) (set : LockedSet<'T>) =
        set.Lock action