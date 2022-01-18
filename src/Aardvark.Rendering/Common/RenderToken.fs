namespace Aardvark.Rendering

open System.Runtime.CompilerServices
open Aardvark.Base

/// Token for gathering and querying statistics about the rendering process.
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
    let withQuery (query : IQuery) (token : RenderToken) =
        let queries =
            match token.Query with
            | :? Queries as q -> q |> Queries.add query
            | _ -> Queries.ofList [token.Query; query]

        { token with Query = queries }


[<AbstractClass; Sealed; Extension>]
type RenderTokenExtensions private() =

    [<Extension>]
    static member WithQuery(this : RenderToken, query : IQuery) =
        this |> RenderToken.withQuery query

    [<Extension>]
    static member InPlaceResourceUpdate(this : RenderToken, kind : ResourceKind) =
        this.Statistics |> Option.iter (fun stats ->
            stats.InPlaceResourceUpdate(kind)
        )

    [<Extension>]
    static member ReplacedResource(this : RenderToken, kind : ResourceKind) =
        this.Statistics |> Option.iter (fun stats ->
            stats.ReplacedResource(kind)
        )

    [<Extension>]
    static member CreatedResource(this : RenderToken, kind : ResourceKind) =
        this.Statistics |> Option.iter (fun stats ->
            stats.CreatedResource(kind)
        )

    [<Extension>]
    static member AddInstructions(this : RenderToken, total : int, active : int) =
        this.Statistics |> Option.iter (fun stats ->
            stats.AddInstructions(total, active)
        )

    [<Extension>]
    static member AddDrawCalls(this : RenderToken, count : int, effective : int) =
        this.Statistics |> Option.iter (fun stats ->
            stats.AddDrawCalls(count, effective)
        )

    [<Extension>]
    static member AddSubTask(this : RenderToken, sorting : MicroTime, update : MicroTime) =
        this.Statistics |> Option.iter (fun stats ->
            stats.AddSubTask(sorting, update)
        )

    [<Extension>]
    static member RenderObjectDeltas(this : RenderToken, added : int, removed : int) =
        this.Statistics |> Option.iter (fun stats ->
            stats.RenderObjectDeltas(added, removed)
        )
