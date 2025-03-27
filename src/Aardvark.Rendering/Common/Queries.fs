namespace Aardvark.Rendering

open System.Runtime.CompilerServices

/// Interface for GPU queries.
type IQuery =

    /// Resets the query manually.
    abstract member Reset : unit -> unit

    /// Prepares the query to be used.
    abstract member Begin : unit -> unit

    /// Finishes the query.
    abstract member End : unit -> unit

[<AbstractClass; Sealed; Extension>]
type IQueryExtensions private() =

    /// Resets the queries manually.
    [<Extension>]
    static member inline Reset(queries : IQuery seq) =
        for q in queries do q.Reset()

    /// Prepares the queries to be used.
    [<Extension>]
    static member inline Begin(queries : IQuery seq) =
        for q in queries do q.Begin()

    /// Finishes the queries.
    [<Extension>]
    static member inline End(queries : IQuery seq) =
        for q in queries do q.End()