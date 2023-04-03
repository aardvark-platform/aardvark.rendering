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
        Queries : IQuery list

        /// Optional runtime statistics.
        Statistics : Option<FrameStatistics>
    }

    static member Empty = RenderTokenEmpty.Empty

    static member inline Zero =
        {
            Queries = []
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

and [<Sealed; AbstractClass>] private RenderTokenEmpty() =
    static let empty =
        { Queries = []
          Statistics = None }

    static member Empty = empty


module RenderTokenInternals =

    [<Struct>]
    type QueryUseDisposable =
        val Queries : IQuery list

        interface IDisposable with
            member x.Dispose() =
                for q in x.Queries do q.End()

        new(queries : IQuery list) =
            { Queries = queries }


[<AbstractClass; Sealed; Extension>]
type RenderTokenExtensions private() =

    /// Begins the queries of the token and returns an IDisposable that
    /// ends the queries when disposed.
    [<Extension>]
    static member inline Use(this : RenderToken) : RenderTokenInternals.QueryUseDisposable =
        for q in this.Queries do q.Begin()
        new RenderTokenInternals.QueryUseDisposable(this.Queries)

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
