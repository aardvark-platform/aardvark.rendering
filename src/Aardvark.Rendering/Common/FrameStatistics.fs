namespace Aardvark.Rendering

open Aardvark.Base

type ResourceKind =
    | Unknown               = 0
    | Buffer                = 1
    | VertexArrayObject     = 2
    | Texture               = 3
    | UniformBuffer         = 4
    | UniformLocation       = 5
    | Sampler               = 6
    | ShaderProgram         = 7
    | Renderbuffer          = 8
    | Framebuffer           = 9
    | IndirectBuffer        = 10
    | AccelerationStructure = 11

module internal ResourceKind =
    let all = System.Enum.GetValues(typeof<ResourceKind>) :?> ResourceKind[]

[<AllowNullLiteral>]
type FrameStatistics =
    val mutable public InPlaceUpdates         : Dict<ResourceKind, int>
    val mutable public ReplacedResources      : Dict<ResourceKind, int>
    val mutable public CreatedResources       : Dict<ResourceKind, int>

    val mutable public RenderPasses           : int
    val mutable public TotalInstructions      : int
    val mutable public ActiveInstructions     : int
    val mutable public DrawCallCount          : int
    val mutable public EffectiveDrawCallCount : int
    val mutable public SortingTime            : MicroTime
    val mutable public DrawUpdateTime         : MicroTime

    val mutable public AddedRenderObjects     : int
    val mutable public RemovedRenderObjects   : int

    static member inline Zero = FrameStatistics()

    new() =
        {
            InPlaceUpdates         = Dict()
            ReplacedResources      = Dict()
            CreatedResources       = Dict()
            RenderPasses           = 0
            TotalInstructions      = 0
            ActiveInstructions     = 0
            DrawCallCount          = 0
            EffectiveDrawCallCount = 0
            SortingTime            = MicroTime.Zero
            DrawUpdateTime         = MicroTime.Zero
            AddedRenderObjects     = 0
            RemovedRenderObjects   = 0
        }

    member this.TotalInplaceUpdates =
        let mutable count = 0
        for kind in ResourceKind.all do &count += this.InPlaceUpdates.GetOrDefault kind
        count

    member this.TotalReplacedResources =
        let mutable count = 0
        for kind in ResourceKind.all do &count += this.ReplacedResources.GetOrDefault kind
        count

    member this.TotalCreatedResources =
        let mutable count = 0
        for kind in ResourceKind.all do &count += this.CreatedResources.GetOrDefault kind
        count

    member this.UpdateCounts =
        let result = Dict()
        for kind in ResourceKind.all do
            let inPlaceUpdates = this.InPlaceUpdates.GetOrDefault kind
            let replacedResources = this.ReplacedResources.GetOrDefault kind
            let createdResources = this.CreatedResources.GetOrDefault kind
            let count = inPlaceUpdates + replacedResources + createdResources
            if count > 0 then result.[kind] <- count
        result

    member inline this.InPlaceResourceUpdate(kind: ResourceKind) =
        this.InPlaceUpdates.[kind] <- 1 + this.InPlaceUpdates.GetOrDefault kind

    member inline this.ReplacedResource(kind: ResourceKind) =
        this.ReplacedResources.[kind] <- 1 + this.ReplacedResources.GetOrDefault kind

    member inline this.CreatedResource(kind: ResourceKind) =
        this.CreatedResources.[kind] <- 1 + this.CreatedResources.GetOrDefault kind

    member inline this.AddInstructions(total: int, active: int) =
        &this.TotalInstructions += total
        &this.ActiveInstructions += active

    member inline this.AddDrawCalls(count: int, effective: int) =
        &this.DrawCallCount += count
        &this.EffectiveDrawCallCount += effective

    member inline this.AddSubTask(sorting: MicroTime, update: MicroTime) =
        inc &this.RenderPasses
        &this.SortingTime += sorting
        &this.DrawUpdateTime += update

    member inline this.RenderObjectDeltas(added: int, removed: int) =
        &this.AddedRenderObjects += added
        &this.RemovedRenderObjects += removed