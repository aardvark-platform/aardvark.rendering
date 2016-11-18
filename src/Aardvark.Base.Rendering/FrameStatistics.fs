namespace Aardvark.Base

open System

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


module private Map =
    let unionWith (f : Map<'k,'v0>) (g : Map<'k,'v1>) (fuse : 'v0 -> 'v1 -> 'v0) (zero : 'v0) =
        let mutable result = f
        for (k,right) in g |> Map.toSeq do
            let newValue = 
                match Map.tryFind k result with
                    | Some v -> fuse v right
                    | None -> fuse zero right

            result <- result |> Map.add k newValue
        result

    let alter (key : 'k) (f : Option<'a> -> Option<'a>) (m : Map<'k, 'a>) =
        let input = Map.tryFind key m
        let result = f input
        match result with
            | Some r -> Map.add key r m
            | None ->
                match input with
                    | Some _ -> Map.remove key m
                    | None -> m

    let inline increment (key : 'k) (value : 'x) (m : Map<'k, 'v>) : Map<'k, 'v> =
        let newValue = 
            match Map.tryFind key m with
                | Some i -> i + value
                | None -> LanguagePrimitives.GenericZero<'v> + value
        if newValue = LanguagePrimitives.GenericZero<'v> then
            Map.remove key m
        else
            Map.add key newValue m

    let inline decrement (key : 'k) (value : 'x) (m : Map<'k, 'v>) =
        let newValue = 
            match Map.tryFind key m with
                | Some i -> i - value
                | None -> LanguagePrimitives.GenericZero<'v> - value
        if newValue = LanguagePrimitives.GenericZero<'v> then
            Map.remove key m
        else
            Map.add key newValue m


type ResourceDelta =
    struct
        val mutable public Created          : float
        val mutable public Deleted          : float
        val mutable public InPlace          : float
        val mutable public Replaced         : float
        val mutable public MemoryDelta      : Mem

        static member Zero = ResourceDelta(0.0,0.0,0.0,0.0,Mem.Zero)

        static member (~-) (v : ResourceDelta) =
            ResourceDelta(
                -v.Created,
                -v.Deleted,
                -v.InPlace,
                -v.Replaced,
                -v.MemoryDelta
            )

        static member (+) (l : ResourceDelta, r : ResourceDelta) =
            ResourceDelta(
                l.Created + r.Created,
                l.Deleted + r.Deleted,
                l.InPlace + r.InPlace,
                l.Replaced + r.Replaced,
                l.MemoryDelta + r.MemoryDelta
            )
                
        static member (-) (l : ResourceDelta, r : ResourceDelta) =
            ResourceDelta(
                l.Created - r.Created,
                l.Deleted - r.Deleted,
                l.InPlace - r.InPlace,
                l.Replaced - r.Replaced,
                l.MemoryDelta - r.MemoryDelta
            )   
                 
        static member (*) (l : ResourceDelta, r : float) =
            ResourceDelta(
                l.Created * r,
                l.Deleted * r,
                l.InPlace * r,
                l.Replaced * r,
                l.MemoryDelta * r
            )

        static member (*) (l : float, r : ResourceDelta) =
            ResourceDelta(
                l * r.Created,
                l * r.Deleted,
                l * r.InPlace,
                l * r.Replaced,
                l * r.MemoryDelta
            )                      
 
        static member (/) (l : ResourceDelta, r : float) =
            ResourceDelta(
                l.Created / r,
                l.Deleted / r,
                l.InPlace / r,
                l.Replaced / r,
                l.MemoryDelta / r
            )
                        
        new(created, deleted, inPlace, replaced, memDelta) =
            { Created = created; Deleted = deleted; InPlace = inPlace; Replaced = replaced; MemoryDelta = memDelta }

    end

type ResourceCount =
    struct
        val mutable public Count            : float
        val mutable public Memory           : Mem

        static member Zero = ResourceCount(0.0, Mem.Zero)

        static member (+) (l : ResourceCount, r : ResourceCount) =
            ResourceCount(
                l.Count +  r.Count,
                l.Memory + r.Memory
            )

        static member (-) (l : ResourceCount, r : ResourceCount) =
            ResourceCount(
                l.Count - r.Count,
                l.Memory - r.Memory
            )

        static member (+) (l : ResourceCount, r : ResourceDelta) =
            ResourceCount(
                l.Count + r.Created - r.Deleted,
                l.Memory + r.MemoryDelta
            )

        static member (-) (l : ResourceCount, r : ResourceDelta) =
            ResourceCount(
                l.Count - r.Created + r.Deleted,
                l.Memory - r.MemoryDelta
            )

        static member (*) (l : ResourceCount, r : float) =
            ResourceCount(
                l.Count * r,
                l.Memory * r
            )

        static member (*) (l : float, r : ResourceCount) =
            ResourceCount(
                l * r.Count,
                l * r.Memory
            )

        static member (/) (l : ResourceCount, r : float) =
            ResourceCount(
                l.Count / r,
                l.Memory / r
            )
        new(cnt, mem) = { Count = cnt; Memory = mem }



    end

type ResourceDeltas (total : ResourceDelta, store : Map<ResourceKind, ResourceDelta>) =
    static let zero = ResourceDeltas(ResourceDelta.Zero, Map.empty)

    member x.Total = total
    member x.Map = store

    static member Zero = zero

    static member (+) (l : ResourceDeltas, r : ResourceKind * ResourceDelta) =
        let k, d = r
        ResourceDeltas(l.Total + d, Map.increment k d l.Map)

    static member (-) (l : ResourceDeltas, r : ResourceKind * ResourceDelta) =
        let k, d = r
        ResourceDeltas(l.Total - d, Map.decrement k d l.Map)
 
    static member (+) (l : ResourceDeltas, r : ResourceDeltas) =
        ResourceDeltas(l.Total + r.Total, Map.unionWith l.Map r.Map (+) ResourceDelta.Zero)

    static member (-) (l : ResourceDeltas, r : ResourceDeltas) =
        ResourceDeltas(l.Total - r.Total, Map.unionWith l.Map r.Map (-) ResourceDelta.Zero)
          
    static member (*) (l : ResourceDeltas, r : float) =
         ResourceDeltas(l.Total * r, l.Map |> Map.map (fun _ v -> v * r))

    static member (/) (l : ResourceDeltas, r : float) =
         ResourceDeltas(l.Total / r, l.Map |> Map.map (fun _ v -> v / r))
                   
    new (kind : ResourceKind, delta : ResourceDelta) =
        ResourceDeltas(delta, Map.ofList [kind, delta])
               
type ResourceCounts (total : ResourceCount, store : Map<ResourceKind, ResourceCount>) =
    static let zero = ResourceCounts(ResourceCount.Zero, Map.empty)
        
    member x.Total = total
    member x.Map = store

    static member Zero = zero

    static member (+) (l : ResourceCounts, r : ResourceCounts) =
        ResourceCounts(l.Total + r.Total, Map.unionWith l.Map r.Map (+) ResourceCount.Zero)

    static member (-) (l : ResourceCounts, r : ResourceCounts) =
        ResourceCounts(l.Total + r.Total, Map.unionWith l.Map r.Map (-) ResourceCount.Zero)

    static member (+) (l : ResourceCounts, r : ResourceDeltas) =
        ResourceCounts(l.Total + r.Total, Map.unionWith l.Map r.Map (+) ResourceCount.Zero)


    static member (+) (l : ResourceCounts, r : ResourceKind * ResourceDelta) =
        let k, d = r
        ResourceCounts(l.Total + d, Map.increment k d l.Map)

    static member (-) (l : ResourceCounts, r : ResourceKind * ResourceDelta) =
        let k, d = r
        ResourceCounts(l.Total - d, Map.decrement k d l.Map)

    static member (*) (l : ResourceCounts, r : float) =
         ResourceCounts(l.Total * r, l.Map |> Map.map (fun _ v -> v * r))

    static member (/) (l : ResourceCounts, r : float) =
         ResourceCounts(l.Total / r, l.Map |> Map.map (fun _ v -> v / r))

type FrameStatistics =
    {
        /// the number of render passes executed
        RenderPassCount : float

        /// the number of instructions contained
        InstructionCount : float

        /// the number of issued driver instructions
        ActiveInstructionCount : float

        /// the number of calls to draw-instructions
        DrawCallCount : float

        /// the effective number of draw-calls (including indirect calls)
        EffectiveDrawCallCount : float

        /// the number of updated resources
        ResourceDeltas : ResourceDeltas

        /// the number of primitives rendered
        PrimitiveCount : float

        /// the time spent in sorting RenderObjects (view-depenent, etc.)
        SortingTime : MicroTime

        /// the time spent in program updates (compiling instructions)
        /// NOTE: includes the SortingTime
        ProgramUpdateTime : MicroTime

        /// the time spent in the submission of resource-update commands (CPU)
        ResourceUpdateSubmissionTime : MicroTime

        /// the time spent in resource-updates (GPU)
        ResourceUpdateTime : MicroTime

        /// the time spent in the submission of rendering-commands (CPU)
        SubmissionTime : MicroTime

        /// the time spent in rendering (GPU)
        ExecutionTime : MicroTime

        /// the total number of physical resources (IResource)
        PhysicalResourceCount : float

        /// the total number of resource-references
        VirtualResourceCount : float
        
        /// used resources by their kind
        ResourceCounts : ResourceCounts

        /// the total number of added/removed renderobjects in one frame
        AddedRenderObjects : float
        RemovedRenderObjects : float

        // historical
        JumpDistance : float
        ProgramSize : uint64
    } with

    static member Zero =
        {
            RenderPassCount = 0.0
            InstructionCount = 0.0
            ActiveInstructionCount = 0.0
            DrawCallCount = 0.0
            EffectiveDrawCallCount = 0.0
            ResourceDeltas = ResourceDeltas.Zero
            PrimitiveCount = 0.0
            JumpDistance = 0.0
            VirtualResourceCount = 0.0
            PhysicalResourceCount = 0.0
            AddedRenderObjects = 0.0
            RemovedRenderObjects = 0.0
            SortingTime = MicroTime.Zero
            ProgramUpdateTime = MicroTime.Zero
            ResourceUpdateSubmissionTime = MicroTime.Zero
            ResourceUpdateTime = MicroTime.Zero
            SubmissionTime = MicroTime.Zero
            ExecutionTime = MicroTime.Zero
            ProgramSize = 0UL
            ResourceCounts = ResourceCounts.Zero
        }

    static member DivideByInt(l : FrameStatistics, r : int) =
        l / float r

    static member (+) (l : FrameStatistics, r : FrameStatistics) =
        {
            RenderPassCount = l.RenderPassCount + r.RenderPassCount
            InstructionCount = l.InstructionCount + r.InstructionCount
            ActiveInstructionCount = l.ActiveInstructionCount + r.ActiveInstructionCount
            DrawCallCount = l.DrawCallCount + r.DrawCallCount
            EffectiveDrawCallCount = l.EffectiveDrawCallCount + r.EffectiveDrawCallCount
            ResourceDeltas = l.ResourceDeltas + r.ResourceDeltas
            PrimitiveCount = l.PrimitiveCount + r.PrimitiveCount
            JumpDistance = l.JumpDistance + r.JumpDistance
            VirtualResourceCount = l.VirtualResourceCount + r.VirtualResourceCount
            PhysicalResourceCount = l.PhysicalResourceCount + r.PhysicalResourceCount
            ResourceCounts = l.ResourceCounts + r.ResourceCounts
            AddedRenderObjects = l.AddedRenderObjects + r.AddedRenderObjects
            RemovedRenderObjects = l.RemovedRenderObjects + r.RemovedRenderObjects
            SortingTime = l.SortingTime + r.SortingTime
            ProgramUpdateTime = l.ProgramUpdateTime + r.ProgramUpdateTime
            ResourceUpdateSubmissionTime = l.ResourceUpdateSubmissionTime + r.ResourceUpdateSubmissionTime
            ResourceUpdateTime = l.ResourceUpdateTime + r.ResourceUpdateTime
            SubmissionTime = l.SubmissionTime + r.SubmissionTime
            ExecutionTime = l.ExecutionTime + r.ExecutionTime
            ProgramSize = l.ProgramSize + r.ProgramSize
        }

    static member (-) (l : FrameStatistics, r : FrameStatistics) =
        {
            RenderPassCount = l.RenderPassCount - r.RenderPassCount
            InstructionCount = l.InstructionCount - r.InstructionCount
            ActiveInstructionCount = l.ActiveInstructionCount - r.ActiveInstructionCount
            DrawCallCount = l.DrawCallCount - r.DrawCallCount
            EffectiveDrawCallCount = l.EffectiveDrawCallCount - r.EffectiveDrawCallCount
            ResourceDeltas = l.ResourceDeltas - r.ResourceDeltas
            PrimitiveCount = l.PrimitiveCount - r.PrimitiveCount
            JumpDistance = l.JumpDistance - r.JumpDistance
            VirtualResourceCount = l.VirtualResourceCount - r.VirtualResourceCount
            PhysicalResourceCount = l.PhysicalResourceCount - r.PhysicalResourceCount
            ResourceCounts = l.ResourceCounts - r.ResourceCounts
            AddedRenderObjects = l.AddedRenderObjects - r.AddedRenderObjects
            RemovedRenderObjects = l.RemovedRenderObjects - r.RemovedRenderObjects
            SortingTime = l.SortingTime - r.SortingTime
            ProgramUpdateTime = l.ProgramUpdateTime - r.ProgramUpdateTime
            ResourceUpdateSubmissionTime = l.ResourceUpdateSubmissionTime - r.ResourceUpdateSubmissionTime
            ResourceUpdateTime = l.ResourceUpdateTime - r.ResourceUpdateTime
            SubmissionTime = l.SubmissionTime - r.SubmissionTime
            ExecutionTime = l.ExecutionTime - r.ExecutionTime
            ProgramSize = l.ProgramSize - r.ProgramSize
        }

    static member (/) (l : FrameStatistics, r : float) =
        {
            RenderPassCount = l.RenderPassCount / r
            InstructionCount = l.InstructionCount / r
            ActiveInstructionCount = l.ActiveInstructionCount / r
            DrawCallCount = l.DrawCallCount / r
            EffectiveDrawCallCount = l.EffectiveDrawCallCount / r
            ResourceDeltas = l.ResourceDeltas / r
            PrimitiveCount = l.PrimitiveCount / r
            JumpDistance = l.JumpDistance / r
            VirtualResourceCount = l.VirtualResourceCount / r
            PhysicalResourceCount = l.PhysicalResourceCount / r
            ResourceCounts = l.ResourceCounts / r
            AddedRenderObjects = l.AddedRenderObjects / r
            RemovedRenderObjects = l.RemovedRenderObjects / r
            SortingTime = l.SortingTime / r
            ProgramUpdateTime = l.ProgramUpdateTime / r
            ResourceUpdateSubmissionTime = l.ResourceUpdateSubmissionTime / r
            ResourceUpdateTime = l.ResourceUpdateTime / r
            SubmissionTime = l.SubmissionTime / r
            ExecutionTime = l.ExecutionTime / r
            ProgramSize = uint64 (float l.ProgramSize / r)
        }

