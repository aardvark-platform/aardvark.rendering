namespace Aardvark.Base

open System

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
        SortingTime : TimeSpan
        InstructionUpdateTime : TimeSpan
        ResourceUpdateTime : TimeSpan
        ExecutionTime : TimeSpan
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
            SortingTime = TimeSpan.Zero
            InstructionUpdateTime = TimeSpan.Zero
            ResourceUpdateTime = TimeSpan.Zero
            ExecutionTime = TimeSpan.Zero
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
            SortingTime = l.SortingTime + r.SortingTime
            InstructionUpdateTime = l.InstructionUpdateTime + r.InstructionUpdateTime
            ResourceUpdateTime = l.ResourceUpdateTime + r.ResourceUpdateTime
            ExecutionTime = l.ExecutionTime + r.ExecutionTime
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
            SortingTime = l.SortingTime - r.SortingTime
            InstructionUpdateTime = l.InstructionUpdateTime - r.InstructionUpdateTime
            ResourceUpdateTime = l.ResourceUpdateTime - r.ResourceUpdateTime
            ExecutionTime = l.ExecutionTime - r.ExecutionTime
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
            SortingTime = TimeSpan.FromTicks(int64 (float l.SortingTime.Ticks / r))
            InstructionUpdateTime = TimeSpan.FromTicks(int64 (float l.InstructionUpdateTime.Ticks / r))
            ResourceUpdateTime = TimeSpan.FromTicks(int64 (float l.ResourceUpdateTime.Ticks / r))
            ExecutionTime = TimeSpan.FromTicks(int64 (float l.ExecutionTime.Ticks / r))
        }


