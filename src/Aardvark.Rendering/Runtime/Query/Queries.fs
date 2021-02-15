namespace Aardvark.Rendering

/// Struct for combining multiple queries into a single one.
[<Struct>]
type Queries(queries : list<IQuery>) =

    new(query : IQuery) =
        Queries([query])

    /// Resets all queries manually.
    member x.Reset() =
        if not queries.IsEmpty then
            queries |> List.iter (fun q -> q.Reset())

    /// Prepares the queries to be used.
    member x.Begin() =
        if not queries.IsEmpty then
            queries |> List.iter (fun q -> q.Begin())

    /// Finishes the queries.
    member x.End() =
        if not queries.IsEmpty then
            queries |> List.iter (fun q -> q.End())

    /// Adds list of queries.
    member x.Add(other : list<IQuery>) =
        Queries(queries @ other)

    /// Adds a single query.
    member x.Add(other : IQuery) =
        Queries(queries @ [other]) // prepend ??

    /// Transforms the queries.
    member x.Map(f : IQuery -> 'a) =
        queries |> List.map f

    /// Iterates the queries.
    member x.ForEach(f : IQuery -> unit) =
        if not queries.IsEmpty then
            queries |> List.iter f

    /// Returns the queries as a sequence.
    member x.AsSeq = queries |> Seq.ofList

    /// Returns the queries as a list.
    member x.AsList = queries

    /// Returns the queries as an array.
    member x.AsArray = queries |> Array.ofList

    interface IQuery with

        member x.Reset() = x.Reset()

        member x.Begin() = x.Begin()

        member x.End() = x.End()


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Queries =

    let empty = Queries(List.empty)

    let none = empty :> IQuery

    let ofSeq (queries : #IQuery seq) =
        Queries(queries |> Seq.map unbox<IQuery> |> List.ofSeq)

    let ofList (queries : list<#IQuery>) =
        Queries(queries |> List.map unbox<IQuery>)

    let single (query : IQuery) =
        Queries(query)

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
    let map (queries : Queries) (f : IQuery -> 'a) =
        queries.Map(f)

    /// Iterates the queries.
    let iter (queries : Queries) (f : IQuery -> unit) =
        queries.ForEach(f)