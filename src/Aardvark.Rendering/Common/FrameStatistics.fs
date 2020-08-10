namespace Aardvark.Rendering

open System
open System.Collections.Generic
open System.Runtime.CompilerServices
open Aardvark.Base

type ResourceKind =
    | Unknown = 0
    | Buffer = 1
    | VertexArrayObject = 2
    | Texture = 3
    | UniformBuffer = 4
    | UniformLocation = 5
    | SamplerState = 6
    | ShaderProgram = 7
    | Renderbuffer = 8
    | Framebuffer = 9
    | IndirectBuffer = 10
    | DrawCall = 11
    | IndexBuffer = 12


[<AutoOpen>]
module private IntHelpers =
    let inline add (cell : byref<'a>) (value : 'a) =
        cell <- cell + value

    let inline notNull (v : 'a) = not (isNull v)

    let inline dictadd (l : Dict<'k, int>) (r : Dict<'k, int>) =
        let res = Dict.empty
        for (KeyValue(k,lv)) in l do
            match r.TryGetValue(k) with
                | (true, rv) -> res.[k] <- lv + rv
                | _ -> res.[k] <- lv

        for (KeyValue(k,rv)) in r do
            if not (l.ContainsKey k) then
                res.[k] <- rv

        res

    let inline dictsub (l : Dict<'k, int>) (r : Dict<'k, int>) =
        let res = Dict.empty
        for (KeyValue(k,lv)) in l do
            match r.TryGetValue(k) with
                | (true, rv) -> res.[k] <- lv - rv
                | _ -> res.[k] <- lv

        for (KeyValue(k,rv)) in r do
            if not (l.ContainsKey k) then
                res.[k] <- -rv

        res

    let inline dictneg (l : Dict<'k, int>)  =
        let res = Dict.empty
        for (KeyValue(k,lv)) in l do
            res.[k] <- -lv
        res


[<AllowNullLiteral>]
type RenderToken =
    class
        val mutable public Parent : RenderToken
        val mutable public InPlaceUpdates : int
        val mutable public ReplacedResources : int
        val mutable public CreatedResources : int
        val mutable public UpdateSubmissionTime : MicroTime
        val mutable public UpdateExecutionTime : MicroTime
        val mutable public UpdateCounts : Dict<ResourceKind, int>

        val mutable public RenderPasses : int
        val mutable public TotalInstructions : int
        val mutable public ActiveInstructions : int
        val mutable public DrawCallCount : int
        val mutable public EffectiveDrawCallCount : int
        val mutable public SortingTime : MicroTime
        val mutable public DrawUpdateTime : MicroTime
        val mutable public DrawSubmissionTime : MicroTime
        val mutable public DrawExecutionTime : MicroTime
        val mutable public PrimitiveCount : int64

        val mutable public AddedRenderObjects : int
        val mutable public RemovedRenderObjects : int

        static member inline Empty : RenderToken = null

        static member inline Zero = RenderToken()

        static member (~-) (x : RenderToken) =
            if isNull x then null
            else
                RenderToken(
                    InPlaceUpdates = -x.InPlaceUpdates,
                    ReplacedResources = -x.ReplacedResources,
                    CreatedResources = -x.CreatedResources,
                    UpdateSubmissionTime = -x.UpdateSubmissionTime,
                    UpdateExecutionTime = -x.UpdateExecutionTime,
                    UpdateCounts = dictneg x.UpdateCounts,
                    RenderPasses = -x.RenderPasses,
                    TotalInstructions = -x.TotalInstructions,
                    ActiveInstructions = -x.ActiveInstructions,
                    DrawCallCount = -x.DrawCallCount,
                    EffectiveDrawCallCount = -x.EffectiveDrawCallCount,
                    SortingTime = -x.SortingTime,
                    DrawUpdateTime = -x.DrawUpdateTime,
                    DrawSubmissionTime = -x.DrawSubmissionTime,
                    DrawExecutionTime = -x.DrawExecutionTime,
                    PrimitiveCount = -x.PrimitiveCount,
                    AddedRenderObjects = -x.AddedRenderObjects,
                    RemovedRenderObjects = -x.RemovedRenderObjects
                )

        static member (+) (l : RenderToken, r : RenderToken) =
            if isNull l then r
            elif isNull r then l
            else
                RenderToken(
                    InPlaceUpdates = l.InPlaceUpdates + r.InPlaceUpdates,
                    ReplacedResources = l.ReplacedResources + r.ReplacedResources,
                    CreatedResources = l.CreatedResources + r.CreatedResources,
                    UpdateSubmissionTime = l.UpdateSubmissionTime + r.UpdateSubmissionTime,
                    UpdateExecutionTime = l.UpdateExecutionTime + r.UpdateExecutionTime,
                    UpdateCounts = dictadd l.UpdateCounts r.UpdateCounts,
                    RenderPasses = l.RenderPasses + r.RenderPasses,
                    TotalInstructions = l.TotalInstructions + r.TotalInstructions,
                    ActiveInstructions = l.ActiveInstructions + r.ActiveInstructions,
                    DrawCallCount = l.DrawCallCount + r.DrawCallCount,
                    EffectiveDrawCallCount = l.EffectiveDrawCallCount + r.EffectiveDrawCallCount,
                    SortingTime = l.SortingTime + r.SortingTime,
                    DrawUpdateTime = l.DrawUpdateTime + r.DrawUpdateTime,
                    DrawSubmissionTime = l.DrawSubmissionTime + r.DrawSubmissionTime,
                    DrawExecutionTime = l.DrawExecutionTime + r.DrawExecutionTime,
                    PrimitiveCount = l.PrimitiveCount + r.PrimitiveCount,
                    AddedRenderObjects = l.AddedRenderObjects + r.AddedRenderObjects,
                    RemovedRenderObjects = l.RemovedRenderObjects + r.RemovedRenderObjects
                )

        static member (-) (l : RenderToken, r : RenderToken) =
            if isNull r then l
            elif isNull l then -r
            else
                RenderToken(
                    InPlaceUpdates = l.InPlaceUpdates - r.InPlaceUpdates,
                    ReplacedResources = l.ReplacedResources - r.ReplacedResources,
                    CreatedResources = l.CreatedResources - r.CreatedResources,
                    UpdateSubmissionTime = l.UpdateSubmissionTime - r.UpdateSubmissionTime,
                    UpdateExecutionTime = l.UpdateExecutionTime - r.UpdateExecutionTime,
                    UpdateCounts = dictsub l.UpdateCounts r.UpdateCounts,
                    RenderPasses = l.RenderPasses - r.RenderPasses,
                    TotalInstructions = l.TotalInstructions - r.TotalInstructions,
                    ActiveInstructions = l.ActiveInstructions - r.ActiveInstructions,
                    DrawCallCount = l.DrawCallCount - r.DrawCallCount,
                    EffectiveDrawCallCount = l.EffectiveDrawCallCount - r.EffectiveDrawCallCount,
                    SortingTime = l.SortingTime - r.SortingTime,
                    DrawUpdateTime = l.DrawUpdateTime - r.DrawUpdateTime,
                    DrawSubmissionTime = l.DrawSubmissionTime - r.DrawSubmissionTime,
                    DrawExecutionTime = l.DrawExecutionTime - r.DrawExecutionTime,
                    PrimitiveCount = l.PrimitiveCount - r.PrimitiveCount,
                    AddedRenderObjects = l.AddedRenderObjects - r.AddedRenderObjects,
                    RemovedRenderObjects = l.RemovedRenderObjects - r.RemovedRenderObjects
                )

        new(p) =
            {
                Parent = p
                InPlaceUpdates = 0
                ReplacedResources = 0
                CreatedResources = 0
                UpdateSubmissionTime = MicroTime.Zero
                UpdateExecutionTime = MicroTime.Zero
                UpdateCounts = Dict()
                RenderPasses = 0
                TotalInstructions = 0
                ActiveInstructions = 0
                DrawCallCount = 0
                EffectiveDrawCallCount = 0
                SortingTime = MicroTime.Zero
                DrawUpdateTime = MicroTime.Zero
                DrawSubmissionTime = MicroTime.Zero
                DrawExecutionTime = MicroTime.Zero
                PrimitiveCount = 0L
                AddedRenderObjects = 0
                RemovedRenderObjects = 0
            }

        new() = RenderToken(null)
            
    end


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RenderToken =

    let inline forall (f : RenderToken -> unit) (t : RenderToken) =
        let mutable current = t
        while notNull current do
            f current
            current <- current.Parent

    let inline isEmpty (t : RenderToken) = isNull t
    let inline isValid (t : RenderToken) = not (isNull t)

[<AbstractClass; Sealed; Extension>]
type RenderTokenExtensions private() =
    [<Extension>]
    static member InPlaceResourceUpdate(this : RenderToken, kind : ResourceKind) =
        this |> RenderToken.forall (fun x -> 
            inc &x.InPlaceUpdates
            x.UpdateCounts.[kind] <- 1 + x.UpdateCounts.GetOrDefault(kind)
        )

    [<Extension>]
    static member ReplacedResource(this : RenderToken, kind : ResourceKind) =
        this |> RenderToken.forall (fun x -> 
            inc &x.ReplacedResources
            x.UpdateCounts.[kind] <- 1 + x.UpdateCounts.GetOrDefault(kind)
        )

    [<Extension>]
    static member CreatedResource(this : RenderToken, kind : ResourceKind) =
        this |> RenderToken.forall (fun x -> 
            inc &x.CreatedResources
            x.UpdateCounts.[kind] <- 1 + x.UpdateCounts.GetOrDefault(kind)
        )

    [<Extension>]
    static member AddInstructions(this : RenderToken, total : int, active : int) =
        this |> RenderToken.forall (fun x -> 
            add &x.TotalInstructions total
            add &x.ActiveInstructions active
        )

    [<Extension>]
    static member AddDrawCalls(this : RenderToken, count : int, effective : int) =
        this |> RenderToken.forall (fun x -> 
            add &x.DrawCallCount count
            add &x.EffectiveDrawCallCount effective
        )

    [<Extension>]
    static member AddSubTask(this : RenderToken, sorting : MicroTime, update : MicroTime, execution : MicroTime, submission : MicroTime) =
        this |> RenderToken.forall (fun x -> 
            inc &x.RenderPasses
            add &x.SortingTime sorting
            add &x.DrawUpdateTime update
            add &x.DrawSubmissionTime submission
            add &x.DrawExecutionTime execution
        )

    [<Extension>]
    static member AddResourceUpdate(this : RenderToken, submission : MicroTime, execution : MicroTime) =
        this |> RenderToken.forall (fun x -> 
            add &x.UpdateSubmissionTime submission
            add &x.UpdateExecutionTime execution
        )

    [<Extension>]
    static member AddPrimitiveCount(this : RenderToken, cnt : int64) =
        this |> RenderToken.forall (fun x -> 
            add &x.PrimitiveCount cnt
        )


    [<Extension>]
    static member RenderObjectDeltas(this : RenderToken, added : int, removed : int) =
        this |> RenderToken.forall (fun x -> 
            add &x.AddedRenderObjects added
            add &x.RemovedRenderObjects removed
        )
