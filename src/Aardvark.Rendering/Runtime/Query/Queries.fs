namespace Aardvark.Rendering

/// Struct for combining multiple queries into a single one.
[<Struct>]
type Queries(queries : IQuery seq) =

    new(query : IQuery) =
        Queries(Seq.singleton query)

    /// Resets all queries manually.
    member x.Reset() =
        for q in queries do q.Reset()

    /// Prepares the queries to be used.
    member x.Begin() =
        for q in queries do q.Begin()

    /// Finishes the queries.
    member x.End() =
        for q in queries do q.End()

    /// Adds list of queries.
    member x.Add(other : IQuery seq) =
        Queries(Seq.append queries other)

    /// Adds a single query.
    member x.Add(other : IQuery) =
        Queries(Seq.append queries (Seq.singleton other))

    /// Transforms the queries.
    member x.Map(f : IQuery -> 'a) =
        queries |> Seq.map f

    /// Returns the queries as a sequence.
    member x.AsSeq = queries

    /// Returns the queries as a list.
    member x.AsList = queries |> List.ofSeq

    /// Returns the queries as an array.
    member x.AsArray = queries |> Array.ofSeq

    interface IQuery with

        member x.Reset() = x.Reset()

        member x.Begin() = x.Begin()

        member x.End() = x.End()


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Queries =

    let empty = Queries(Seq.empty)

    let create (queries : #IQuery seq) =
        Queries(queries |> Seq.map unbox<IQuery>)

    let single (query : IQuery) =
        create <| Seq.singleton query

    /// Adds a query.
    let add (x : IQuery) (queries : Queries) =
        queries.Add x

    /// Resets all queries manually.
    let reset (queries : Queries) =
        queries.Reset()

    /// Prepares the queries to be used.
    let start (queries : Queries) =
        queries.Begin()

    /// Finishes the queries.
    let stop (queries : Queries) =
        queries.End()

    /// Returns the queries as a sequence.
    let toSeq (queries : Queries) =
        queries.AsSeq

    /// Returns the queries as a list.
    let toList (queries : Queries) =
        queries.AsList

    /// Returns the queries as an array.
    let toArray (queries : Queries) =
        queries.AsArray

    /// Transforms the queries.
    let map (f : IQuery -> 'a) =
        toSeq >> Seq.map f