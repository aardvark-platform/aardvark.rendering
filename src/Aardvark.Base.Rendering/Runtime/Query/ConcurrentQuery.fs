namespace Aardvark.Base

open System
open System.Threading
open System.Collections.Generic
open Aardvark.Base

/// Base class for reference counted queries.
[<AbstractClass>]
type RefCountedQuery<'Result>() =

    let mutable refCount = 0

    /// Indicates whether the query is currently in use (i.e. reference counter is greater than zero).
    member x.InUse =
        refCount > 0

    /// Increments the reference count.
    member x.Acquire() =
        inc &refCount

    /// Decrements the reference count.
    member x.Release() =
        dec &refCount

    /// Sets the reference count to zero.
    member x.ReleaseAll() =
        refCount <- 0

    /// Returns whether the query is active.
    abstract member IsActive : bool

    /// Resets the query so it can be reused.
    abstract member Reset : unit -> unit

    /// Blocks to retrieve the query results.
    abstract member GetResults : unit -> 'Result

    /// Retrieves the query results if available.
    abstract member TryGetResults : unit -> 'Result option

    /// Disposes the query and all handles.
    abstract member Dispose : unit -> unit

    interface IDisposable with
        member x.Dispose() = x.Dispose()


/// Base class for queries that manage multiple reference counted queries to support
/// the concurrent retrieval of results.
[<AbstractClass>]
type ConcurrentQuery<'Query, 'Result when 'Query :> RefCountedQuery<'Result>>() =

    // List of all queries that have been allocated.
    let queries = List<'Query>()

    // Flag to indicate if query was inactive when Begin() was called.
    let mutable wasInactive = true

    // The currently active query
    let mutable activeQuery : 'Query option = None

    // The current level of the queue, i.e. of the nested Begin-End pairs
    let mutable currentLevel = 0

    // Returns whether the query is active.
    let isActive() =
        activeQuery
        |> Option.map (fun q -> q.IsActive)
        |> Option.defaultValue false

    /// Creates a new query.
    abstract member CreateQuery : unit -> 'Query

    /// Called when the top-level begin-end pair is entered.
    abstract member BeginQuery : 'Query -> unit
    default x.BeginQuery(_ : 'Query) = ()

    /// Called when the top-level begin-end pair is left.
    abstract member EndQuery : 'Query -> unit
    default x.EndQuery(_ : 'Query) = ()

    // Creates or gets a query that is currently unused.
    member private x.GetQuery() =
        let reused =
            queries |> Seq.tryFind (fun q -> not q.InUse)

        match reused with
        | Some q ->
            q.Reset()
            q
        | None ->
            let q = x.CreateQuery()
            queries.Add q
            q

    /// Waits until there is an active query, increments the ref counter, releases the global lock,
    /// calls the function on the query, and finally decrements the ref counter.
    member private x.UseActiveQuery(reset : bool, f : 'Query -> 'a) =
        let query =
            lock x (fun _ ->
                while not (isActive()) do
                    Monitor.Wait x |> ignore

                let q = activeQuery.Value
                q.Acquire()

                if reset then
                    activeQuery <- None

                q
            )

        try
            f query
        finally
            lock x query.Release

    /// Returns the active query.
    member x.ActiveQuery =
        activeQuery

    /// Resets the query.
    member x.Reset() =
        lock x (fun _ -> activeQuery <- None)

    /// Locks the query and resets it.
    member x.Begin() =
        Monitor.Enter x
        inc &currentLevel

        // Just entered the top-level begin-end pair -> reset
        if currentLevel = 1 then
            wasInactive <- not (isActive())
            activeQuery <- Some <| x.GetQuery()
            x.BeginQuery activeQuery.Value

    /// Unlocks the query.
    member x.End() =
        dec &currentLevel

        // We just left the top-level begin-end pair
        if currentLevel = 0 && isActive() then
            x.EndQuery activeQuery.Value

            // If the query was previously inactive, we can notify other
            // threads to try query results again.
            if wasInactive then
                Monitor.PulseAll x

        Monitor.Exit x

    /// Trys to get the result from the queries.
    member x.TryGetResults(reset : bool) =
        x.UseActiveQuery(reset, fun query ->
            query.TryGetResults()
        )

    /// Blocks until the result of every query is available.
    member x.GetResults(reset : bool) =
        x.UseActiveQuery(reset, fun query ->
            query.GetResults()
        )

    /// Deletes the queries.
    member x.Dispose() =
        lock x (fun _ ->
            queries |> Seq.iter (fun x -> x.Dispose())
        )

    interface IDisposable with
        member x.Dispose() = x.Dispose()