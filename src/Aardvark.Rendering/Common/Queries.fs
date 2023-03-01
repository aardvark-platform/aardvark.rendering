namespace Aardvark.Rendering

/// Interface for GPU queries.
type IQuery =

    /// Resets the query manually.
    abstract member Reset : unit -> unit

    /// Prepares the query to be used.
    abstract member Begin : unit -> unit

    /// Finishes the query.
    abstract member End : unit -> unit


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

    /// Returns true if there are no queries
    member x.IsEmpty = queries.IsEmpty

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

    /// Add seq of queries.
    let addSeq (qu : seq<IQuery>) (queries : Queries) =
        let mutable qres = queries
        for qx in qu do
            qres <- qres.Add qx
        qres

    /// Add seq of queries
    let addList (qu : list<IQuery>) (queries : Queries) =
        let mutable qres = queries
        for qx in qu do
            qres <- qres.Add qx
        qres

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

    /// Combine two queries.
    let combine (query1 : IQuery) (query2 : IQuery) : IQuery =
        match query1 with
        | :? Queries as q1 -> 
            if q1.IsEmpty then 
                query2
            else
                match query2 with 
                | :? Queries as q2 -> 
                    if q2.IsEmpty then
                        query1
                    else
                        q1 |> addList q2.AsList :> IQuery // combine both lists of queries
                | _ -> q1 |> add query2 :> IQuery // add single query q2 to Queries q1
        | _ -> ofList [query1; query2] // combine 2 single queries
