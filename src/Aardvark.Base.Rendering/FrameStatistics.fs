namespace Aardvark.Base

open System

type ResourceKind =
    | Unknown = 0
    | Buffer = 1
    | VertexArrayObject = 2
    | Texture = 3
    | UniformBuffer = 4
    | UniformBufferView = 5
    | UniformLocation = 6
    | SamplerState = 7
    | ShaderProgram = 8
    | Renderbuffer = 9
    | Framebuffer = 10
    | StreamingTexture = 11


module Map =
    let unionWith (f : Map<'k,'v>) (g : Map<'k,'v>) (fuse : 'v -> 'v -> 'v) (zero : 'v) =
        let mutable result = f
        for (k,right) in g |> Map.toSeq do
            let left = 
                match Map.tryFind k result with
                    | Some v ->  v
                    | None -> zero
            result <- Map.add k (fuse left right) result
        result

type ITimeProbe =
    abstract member Value : TimeSpan

type private AddTimeProbe(l : ITimeProbe, r : ITimeProbe) =
    interface ITimeProbe with
        member x.Value = l.Value + r.Value

type private SubTimeProbe(l : ITimeProbe, r : ITimeProbe) =
    interface ITimeProbe with
        member x.Value = l.Value - r.Value

type private DivTimeProbe(l : ITimeProbe, r : float) =
    interface ITimeProbe with
        member x.Value = TimeSpan.FromTicks(int64 (float l.Value.Ticks / r))

type FrameStatistics =
    {
        Programs : float
        InstructionCount : float
        ActiveInstructionCount : float
        DrawCallCount : float
        InstructionUpdateCount : float
        ResourceUpdateCount : float
        PrimitiveCount : float
        JumpDistance : float
        ResourceCount : float
        ResourceUpdateCounts : Map<ResourceKind,float>
        AddedRenderObjects : float
        RemovedRenderObjects : float
        SortingTime : TimeSpan
        InstructionUpdateTime : TimeSpan
        ResourceUpdateTime : TimeSpan
        ExecutionTime : TimeSpan
        ProgramSize : uint64
    } with

    static member Zero =
        {
            Programs = 0.0
            InstructionCount = 0.0
            ActiveInstructionCount = 0.0
            DrawCallCount = 0.0
            InstructionUpdateCount = 0.0
            ResourceUpdateCount = 0.0
            PrimitiveCount = 0.0
            JumpDistance = 0.0
            ResourceCount = 0.0
            ResourceUpdateCounts = Map.empty
            AddedRenderObjects = 0.0
            RemovedRenderObjects = 0.0
            SortingTime = TimeSpan.Zero
            InstructionUpdateTime = TimeSpan.Zero
            ResourceUpdateTime = TimeSpan.Zero
            ExecutionTime = TimeSpan.Zero
            ProgramSize = 0UL
        }

    static member DivideByInt(l : FrameStatistics, r : int) =
        l / float r

    static member (+) (l : FrameStatistics, r : FrameStatistics) =
        {
            Programs = l.Programs + r.Programs
            InstructionCount = l.InstructionCount + r.InstructionCount
            ActiveInstructionCount = l.ActiveInstructionCount + r.ActiveInstructionCount
            DrawCallCount = l.DrawCallCount + r.DrawCallCount
            InstructionUpdateCount = l.InstructionUpdateCount + r.InstructionUpdateCount
            ResourceUpdateCount = l.ResourceUpdateCount + r.ResourceUpdateCount
            PrimitiveCount = l.PrimitiveCount + r.PrimitiveCount
            JumpDistance = l.JumpDistance + r.JumpDistance
            ResourceCount = l.ResourceCount + r.ResourceCount
            ResourceUpdateCounts = Map.unionWith l.ResourceUpdateCounts r.ResourceUpdateCounts (+) 0.0
            AddedRenderObjects = l.AddedRenderObjects + r.AddedRenderObjects
            RemovedRenderObjects = l.RemovedRenderObjects + r.RemovedRenderObjects
            SortingTime = l.SortingTime + r.SortingTime
            InstructionUpdateTime = l.InstructionUpdateTime + r.InstructionUpdateTime
            ResourceUpdateTime = l.ResourceUpdateTime + r.ResourceUpdateTime
            ExecutionTime = l.ExecutionTime + r.ExecutionTime
            ProgramSize = l.ProgramSize + r.ProgramSize
        }

    static member (-) (l : FrameStatistics, r : FrameStatistics) =
        {
            Programs = l.Programs - r.Programs
            InstructionCount = l.InstructionCount - r.InstructionCount
            ActiveInstructionCount = l.ActiveInstructionCount - r.ActiveInstructionCount
            DrawCallCount = l.DrawCallCount - r.DrawCallCount
            InstructionUpdateCount = l.InstructionUpdateCount - r.InstructionUpdateCount
            ResourceUpdateCount = l.ResourceUpdateCount - r.ResourceUpdateCount
            PrimitiveCount = l.PrimitiveCount - r.PrimitiveCount
            JumpDistance = l.JumpDistance - r.JumpDistance
            ResourceCount = l.ResourceCount - r.ResourceCount
            ResourceUpdateCounts = Map.unionWith l.ResourceUpdateCounts r.ResourceUpdateCounts (-) 0.0
            AddedRenderObjects = l.AddedRenderObjects - r.AddedRenderObjects
            RemovedRenderObjects = l.RemovedRenderObjects - r.RemovedRenderObjects
            SortingTime = l.SortingTime - r.SortingTime
            InstructionUpdateTime = l.InstructionUpdateTime - r.InstructionUpdateTime
            ResourceUpdateTime = l.ResourceUpdateTime - r.ResourceUpdateTime
            ExecutionTime = l.ExecutionTime - r.ExecutionTime
            ProgramSize = l.ProgramSize - r.ProgramSize
        }

    static member (/) (l : FrameStatistics, r : float) =
        {
            Programs = l.Programs / r
            InstructionCount = l.InstructionCount / r
            ActiveInstructionCount = l.ActiveInstructionCount / r
            DrawCallCount = l.DrawCallCount / r
            InstructionUpdateCount = l.InstructionUpdateCount / r
            ResourceUpdateCount = l.ResourceUpdateCount / r
            PrimitiveCount = l.PrimitiveCount / r
            JumpDistance = l.JumpDistance / r
            ResourceCount = l.ResourceCount / r
            ResourceUpdateCounts = Map.map (fun k v -> v / r) l.ResourceUpdateCounts
            AddedRenderObjects = l.AddedRenderObjects / r
            RemovedRenderObjects = l.RemovedRenderObjects / r
            SortingTime = TimeSpan.FromTicks(int64 (float l.SortingTime.Ticks / r))
            InstructionUpdateTime = TimeSpan.FromTicks(int64 (float l.InstructionUpdateTime.Ticks / r))
            ResourceUpdateTime = TimeSpan.FromTicks(int64 (float l.ResourceUpdateTime.Ticks / r))
            ExecutionTime = TimeSpan.FromTicks(int64 (float l.ExecutionTime.Ticks / r))
            ProgramSize = uint64 (float l.ProgramSize / r)
        }


