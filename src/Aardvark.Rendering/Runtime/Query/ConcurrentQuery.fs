namespace Aardvark.Rendering

open System
open System.Threading
open System.Collections.Generic
open Aardvark.Base

/// Base class for queries that manage multiple reference counted queries to support
/// the concurrent retrieval of results.
[<AbstractClass>]
type ConcurrentQuery<'Query, 'Result when 'Query :> IMultiQuery<'Result>>() =

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
            queries.Find(fun q -> not q.IsUsed)

        if not (Object.ReferenceEquals(reused, null)) then
            reused.Reset()
            reused
        else
            let q = x.CreateQuery()
            queries.Add q
            q

    /// If there is an active query, increments the ref counter, releases the global lock,
    /// calls the function on the query, and finally decrements the ref counter.
    /// Otherwise returns None.
    member private x.TryUseActiveQuery(reset : bool, f : 'Query -> 'a) =
        let query =
            lock x (fun _ ->
                if isActive() then
                    let q = activeQuery.Value
                    q.Acquire()

                    if reset then
                        activeQuery <- None

                    Some q

                else
                    None
            )

        try
            query |> Option.map f
        finally
            lock x (fun _ -> query |> Option.iter (fun q -> q.Release()))

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
        x.TryUseActiveQuery(reset, fun query ->
            query.TryGetResults()
        ) |> Option.bind id

    /// Blocks until the result of every query is available.
    member x.GetResults(reset : bool) =
        x.UseActiveQuery(reset, fun query ->
            query.GetResults()
        )

    /// Deletes the queries.
    member x.Dispose() =
        lock x (fun _ ->
            for q in queries do q.Dispose()
        )

    interface IDisposable with
        member x.Dispose() = x.Dispose()