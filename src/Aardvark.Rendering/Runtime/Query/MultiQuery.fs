namespace Aardvark.Base

open System
open System.Collections.Generic
open Aardvark.Base

/// Interface for queries that consist of multiple query handles.
type IMultiQuery<'Result> =
    inherit IDisposable

    /// Indicates whether the query is currently in use (i.e. reference counter is greater than zero).
    abstract member IsUsed : bool

    /// Increments the reference count.
    abstract member Acquire : unit -> unit

    /// Decrements the reference count.
    abstract member Release : unit -> unit

    /// Indicates whether the query is active (i.e. there is at least one query handle).
    abstract member IsActive : bool

    /// Resets the query so it can be reused.
    abstract member Reset : unit -> unit

    /// Blocks to retrieve the query results.
    abstract member GetResults : unit -> 'Result[]

     /// Retrieves the query results if available.
    abstract member TryGetResults : unit -> 'Result[] option


/// Base class for queries that consist of multiple query handles.
[<AbstractClass>]
type MultiQuery<'Handle, 'Result when 'Handle :> IQueryHandle<'Result>>() =

    let mutable refCount = 0

    let handles = List<'Handle>()

    /// Indicates whether the query is currently in use (i.e. reference counter is greater than zero).
    member x.IsUsed =
        refCount > 0

    /// Increments the reference count.
    member x.Acquire() =
        inc &refCount

    /// Decrements the reference count.
    member x.Release() =
        dec &refCount

    /// Indicates whether the query is active.
    member x.IsActive =
        handles.Count > 0

    /// The query handles of the query
    member x.Handles =
        handles

    /// Resets the query so it can be reused.
    abstract member Reset : unit -> unit
    default x.Reset() =
        handles |> Seq.iter (fun h -> h.Reset())
        handles.Clear()

    /// Blocks to retrieve the query results.
    abstract member GetResults : unit -> 'Result[]
    default x.GetResults() =
        handles |> Seq.map (fun h -> h.GetResult()) |> Seq.toArray

    /// Retrieves the query results if available.
    abstract member TryGetResults : unit -> 'Result[] option
    default x.TryGetResults() =
        let r = handles |> Seq.choose (fun h -> h.TryGetResult()) |> Seq.toArray

        if r.Length < handles.Count then
            None
        else
            Some r

    /// Disposes the query and all handles.
    abstract member Dispose : unit -> unit
    default x.Dispose() =
        handles |> Seq.iter (fun x -> x.Dispose())

    interface IMultiQuery<'Result> with

        member x.IsUsed =
            refCount > 0

        member x.Acquire() =
            inc &refCount

        member x.Release() =
            dec &refCount

        member x.IsActive =
            handles.Count > 0

        member x.Reset() =
            x.Reset()

        member x.GetResults() =
            x.GetResults()

        member x.TryGetResults() =
            x.TryGetResults()

    interface IDisposable with
        member x.Dispose() = x.Dispose()