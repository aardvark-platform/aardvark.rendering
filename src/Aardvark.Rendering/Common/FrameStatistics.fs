namespace Aardvark.Rendering

open System.Runtime.CompilerServices
open Aardvark.Base

[<AutoOpen>]
module private FrameStatisticsHelpers =
    let inline add (cell : byref<'a>) (value : 'a) =
        cell <- cell + value

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
    | AccelerationStructure = 13

type FrameStatistics =
    class
        val mutable public InPlaceUpdates : int
        val mutable public ReplacedResources : int
        val mutable public CreatedResources : int
        val mutable public UpdateCounts : Dict<ResourceKind, int>

        val mutable public RenderPasses : int
        val mutable public TotalInstructions : int
        val mutable public ActiveInstructions : int
        val mutable public DrawCallCount : int
        val mutable public EffectiveDrawCallCount : int
        val mutable public SortingTime : MicroTime
        val mutable public DrawUpdateTime : MicroTime

        val mutable public AddedRenderObjects : int
        val mutable public RemovedRenderObjects : int

        static member inline Zero = FrameStatistics()

        new() =
            {
                InPlaceUpdates = 0
                ReplacedResources = 0
                CreatedResources = 0
                UpdateCounts = Dict()
                RenderPasses = 0
                TotalInstructions = 0
                ActiveInstructions = 0
                DrawCallCount = 0
                EffectiveDrawCallCount = 0
                SortingTime = MicroTime.Zero
                DrawUpdateTime = MicroTime.Zero
                AddedRenderObjects = 0
                RemovedRenderObjects = 0
            }
            
    end

[<AbstractClass; Sealed; Extension>]
type FrameStatisticsExtensions private() =
    [<Extension>]
    static member InPlaceResourceUpdate(this : FrameStatistics, kind : ResourceKind) =
        inc &this.InPlaceUpdates
        this.UpdateCounts.[kind] <- 1 + this.UpdateCounts.GetOrDefault(kind)

    [<Extension>]
    static member ReplacedResource(this : FrameStatistics, kind : ResourceKind) =
        inc &this.ReplacedResources
        this.UpdateCounts.[kind] <- 1 + this.UpdateCounts.GetOrDefault(kind)

    [<Extension>]
    static member CreatedResource(this : FrameStatistics, kind : ResourceKind) =
        inc &this.CreatedResources
        this.UpdateCounts.[kind] <- 1 + this.UpdateCounts.GetOrDefault(kind)

    [<Extension>]
    static member AddInstructions(this : FrameStatistics, total : int, active : int) =
        add &this.TotalInstructions total
        add &this.ActiveInstructions active

    [<Extension>]
    static member AddDrawCalls(this : FrameStatistics, count : int, effective : int) =
        add &this.DrawCallCount count
        add &this.EffectiveDrawCallCount effective

    [<Extension>]
    static member AddSubTask(this : FrameStatistics, sorting : MicroTime, update : MicroTime) =
        inc &this.RenderPasses
        add &this.SortingTime sorting
        add &this.DrawUpdateTime update

    [<Extension>]
    static member RenderObjectDeltas(this : FrameStatistics, added : int, removed : int) =
        add &this.AddedRenderObjects added
        add &this.RemovedRenderObjects removed