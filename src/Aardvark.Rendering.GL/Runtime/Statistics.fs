namespace Aardvark.Rendering.GL

open Aardvark.Base
open Aardvark.Rendering

module InstructionStatistics =
    open OpenGl

    let private (|DrawArrays|_|) (i : Instruction) =
        match i.Operation with
            | InstructionCode.DrawArrays ->
                Some <| DrawArrays(i.Arguments.[0] |> unbox<DrawMode>, i.Arguments.[1] |> unbox<int>, i.Arguments.[2] |> unbox<int>)
            | _ ->
                None

    let private (|DrawElements|_|) (i : Instruction) =
        match i.Operation with
            | InstructionCode.DrawElements ->
                Some <| DrawElements(i.Arguments.[0] |> unbox<DrawMode>, i.Arguments.[1] |> unbox<int>, i.Arguments.[2] |> unbox<int>)
            | _ ->
                None

    let private (|DrawArraysInstanced|_|) (i : Instruction) =
        match i.Operation with
            | InstructionCode.DrawArraysInstanced ->
                Some <| DrawArraysInstanced(i.Arguments.[0] |> unbox<DrawMode>, i.Arguments.[1] |> unbox<int>, i.Arguments.[2] |> unbox<int>, i.Arguments.[3] |> unbox<int>)
            | _ ->
                None

    let private (|DrawElementsInstanced|_|) (i : Instruction) =
        match i.Operation with
            | InstructionCode.DrawElementsInstanced ->
                Some <| DrawElementsInstanced(i.Arguments.[0] |> unbox<DrawMode>, i.Arguments.[1] |> unbox<int>, i.Arguments.[2] |> unbox<int>, i.Arguments.[4] |> unbox<int>)
            | _ ->
                None


    let private getPrimitiveCount (m : DrawMode) (count : int) =
        match m with
            | DrawMode.Points -> count
            | DrawMode.Lines -> count / 2
            | DrawMode.Triangles -> count / 3
            | DrawMode.LineStrip -> count - 1
            | DrawMode.TriangleStrip -> count - 2
            | DrawMode.Patches -> count / 3 //TODO: better approximation ;-)
            | _ -> failwith "unsupported drawmode"

    let toStats (i : Instruction) =
        let isDraw = i.Operation = InstructionCode.DrawArrays  || i.Operation = InstructionCode.DrawArraysInstanced  || 
                     i.Operation = InstructionCode.DrawElements || i.Operation = InstructionCode.DrawElementsInstanced ||
                     i.Operation = InstructionCode.MultiDrawArraysIndirect || i.Operation = InstructionCode.MultiDrawElementsIndirect

        let primitiveCount =
            match i with
                | DrawArrays(t,_,c) -> getPrimitiveCount t c
                | DrawElements(t,c,_) -> getPrimitiveCount t c
                | DrawArraysInstanced(t,_,c,p) -> p * (getPrimitiveCount t c)
                | DrawElementsInstanced(t,c,_,p) -> p * (getPrimitiveCount t c)
                | _ -> 0

        let drawCallCount = if isDraw then 1 else 0

        { FrameStatistics.Zero with
            InstructionCount = 1.0
            ActiveInstructionCount = 1.0
            DrawCallCount = float drawCallCount
            PrimitiveCount = float primitiveCount
        }

    let add (stats : EventSource<FrameStatistics>) (i : Instruction) =
        stats.Emit(stats.Latest + (toStats i))

    let remove (stats : EventSource<FrameStatistics>) (i : Instruction) =
        stats.Emit(stats.Latest - (toStats i))

    let replace (stats : EventSource<FrameStatistics>) (o : Instruction) (n : Instruction) =
        remove stats o
        add stats n

    let addList (stats : EventSource<FrameStatistics>) (i : list<Instruction>) =
        i |> List.iter (add stats)

    let removeList (stats : EventSource<FrameStatistics>) (i : list<Instruction>) =
        i |> List.iter (remove stats)

    let replaceList (stats : EventSource<FrameStatistics>) (o : list<Instruction>) (n : list<Instruction>) =
        o |> List.iter (remove stats)
        n |> List.iter (add stats)