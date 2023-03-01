namespace Aardvark.Rendering

open System
open System.Runtime.CompilerServices
open Aardvark.Base

/// Token for gathering and querying statistics about the rendering process.
/// It is mutable for construction/mutation in C# code
[<CLIMutable>]
type RenderToken =
    {
        /// User provided GPU queries.
        Query : IQuery

        /// Optional runtime statistics.
        Statistics : Option<FrameStatistics>
    }

    static member Empty =
        {
            Query = Queries.empty
            Statistics = None
        }

    static member Zero =
        {
            Query = Queries.empty
            Statistics = Some <| FrameStatistics()
        }

    member private x.GetStatistic(fallback : 'T, f : FrameStatistics -> 'T) =
        x.Statistics |> Option.map f |> Option.defaultValue fallback 

    member inline private x.GetStatistic(f : FrameStatistics -> 'T) =
        x.GetStatistic(zero, f)   

    member x.InPlaceUpdates         = x.GetStatistic(fun f -> f.InPlaceUpdates)
    member x.ReplacedResources      = x.GetStatistic(fun f -> f.ReplacedResources)
    member x.CreatedResources       = x.GetStatistic(fun f -> f.CreatedResources)
    member x.UpdateCounts           = x.GetStatistic(Dict.empty, fun f -> f.UpdateCounts)

    member x.RenderPasses           = x.GetStatistic(fun f -> f.RenderPasses)
    member x.TotalInstructions      = x.GetStatistic(fun f -> f.TotalInstructions)
    member x.ActiveInstructions     = x.GetStatistic(fun f -> f.ActiveInstructions)
    member x.DrawCallCount          = x.GetStatistic(fun f -> f.DrawCallCount)
    member x.EffectiveDrawCallCount = x.GetStatistic(fun f -> f.EffectiveDrawCallCount)
    member x.SortingTime            = x.GetStatistic(fun f -> f.SortingTime)
    member x.DrawUpdateTime         = x.GetStatistic(fun f -> f.DrawUpdateTime)

    member x.AddedRenderObjects     = x.GetStatistic(fun f -> f.AddedRenderObjects)
    member x.RemovedRenderObjects   = x.GetStatistic(fun f -> f.RemovedRenderObjects)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RenderToken =

    /// Adds the given query to the given render token.
    let withQuery (query : IQuery) (token : RenderToken) : RenderToken =
        match token.Query with
        | :? Queries as tq -> 
            if tq.IsEmpty then // token has no queries
                match query with // query is empty -> return token unmodified
                | :? Queries as q when q.IsEmpty -> token
                | _ -> { token with Query = query } // query not empty -> return token with query
            else // token has some queries -> combine
                match query with 
                | :? Queries as q -> 
                    if q.IsEmpty then // query is empty -> return token unmodified
                        token
                    else
                        { token with Query = tq |> Queries.addList q.AsList } // combine both lists of queries
                | _ -> { token with Query = tq |> Queries.add query } // add single query q2 to Queries q1
        | _ -> { token with Query = Queries.ofList [query; token.Query] }  // combine 2 single queries

module DisposableHelper =
    
    [<Struct>]
    type QueryUseDisposable =

        val Query : IQuery

        interface IDisposable with
            member x.Dispose() = x.Query.End()

        new(query : IQuery) =
            query.Begin()
            { Query = query }


[<AbstractClass; Sealed; Extension>]
type RenderTokenExtensions private() =

    /// Begins the queries of the token and returns an IDisposable that
    /// ends the queries when disposed.
    [<Extension>]
    static member inline Use(this : RenderToken) : DisposableHelper.QueryUseDisposable =
        new DisposableHelper.QueryUseDisposable(this.Query)

    /// Begins the queries of the token, evaluates the given function, and
    /// finally ends the queries.
    [<Extension>]
    static member inline Use(this : RenderToken, f : unit -> 'T) =
        this.Query.Begin()
        let result = f()
        this.Query.End()
        result

    [<Extension>]
    static member WithQuery(this : RenderToken, query : IQuery) =
        this |> RenderToken.withQuery query

    [<Extension>]
    static member InPlaceResourceUpdate(this : RenderToken, kind : ResourceKind) =
        match this.Statistics with
        | Some stats -> stats.InPlaceResourceUpdate(kind)
        | _ -> ()

    [<Extension>]
    static member ReplacedResource(this : RenderToken, kind : ResourceKind) =
        match this.Statistics with
        | Some stats -> stats.ReplacedResource(kind)
        | _ -> ()

    [<Extension>]
    static member CreatedResource(this : RenderToken, kind : ResourceKind) =
        match this.Statistics with
        | Some stats -> stats.CreatedResource(kind)
        | _ -> ()

    [<Extension>]
    static member AddInstructions(this : RenderToken, total : int, active : int) =
        match this.Statistics with
        | Some stats -> stats.AddInstructions(total, active)
        | _ -> ()

    [<Extension>]
    static member AddDrawCalls(this : RenderToken, count : int, effective : int) =
        match this.Statistics with
        | Some stats -> stats.AddDrawCalls(count, effective)
        | _ -> ()

    [<Extension>]
    static member AddSubTask(this : RenderToken, sorting : MicroTime, update : MicroTime) =
        match this.Statistics with
        | Some stats -> stats.AddSubTask(sorting, update)
        | _ -> ()

    [<Extension>]
    static member RenderObjectDeltas(this : RenderToken, added : int, removed : int) =
        match this.Statistics with
        | Some stats -> stats.RenderObjectDeltas(added, removed)
        | _ -> ()
