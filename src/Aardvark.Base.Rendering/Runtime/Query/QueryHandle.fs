namespace Aardvark.Base

open System

/// Base type for query handles that allow retrieving and caching query results.
[<AbstractClass>]
type AbstractQueryHandle<'Handle, 'Result>(handle : 'Handle) =

    let mutable cached : 'Result option = None

    /// Blocks to retrieve results from a query handle.
    abstract member GetValues : 'Handle -> 'Result

    /// Retrieves the results from a query handle if available.
    abstract member TryGetValues : 'Handle -> 'Result option

    /// Deletes a query handle.
    abstract member DeleteHandle : 'Handle -> unit

    /// Clears the cached result.
    member x.Reset() =
        cached <- None

    /// Blocks and gets the query result.
    member x.GetResult() =
        match cached with
        | Some x -> x
        | None ->
            let rs = x.GetValues handle
            cached <- Some rs
            rs

    /// Gets the query result if available.
    member x.TryGetResult() =
        match cached with
        | Some _ as x -> x
        | None ->
            let rs = x.TryGetValues handle
            cached <- rs
            rs

    /// The native handle of the query.
    member x.Native =
        handle

    /// Disposes of the query handle.
    member x.Dispose() =
        x.DeleteHandle handle

    interface IDisposable with
        member x.Dispose() = x.Dispose()