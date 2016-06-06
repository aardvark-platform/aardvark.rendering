namespace Aardvark.Rendering.GL

open System.Threading
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4

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

type OpenGlStopwatch() =
    static let current = new ThreadLocal<Option<OpenGlStopwatch>>(fun () -> None)

    let mutable query = -1
    let mutable running = false
    let mutable offset = 0L
    let mutable parent : Option<OpenGlStopwatch> = None
    let mutable children = []

    let cpu = System.Diagnostics.Stopwatch()

    member private x.DoStop() =
        GL.EndQuery(QueryTarget.TimeElapsed)
        cpu.Stop()

    member private x.DoStart() =
        cpu.Start()
        GL.BeginQuery(QueryTarget.TimeElapsed, query)


    member private x.Add(other : OpenGlStopwatch) =
        children <- other::children

    member x.Start() =
        cpu.Start()
        if not running then
            if query < 0 then 
                query <- GL.GenQuery()

            match current.Value with
                | Some p -> 
                    p.DoStop()
                    parent <- Some p
                | None ->
                    parent <- None
            
            GL.BeginQuery(QueryTarget.TimeElapsed, query)
            running <- true
            children <- []
            current.Value <- Some x

    member x.Restart() =
        cpu.Stop()
        cpu.Reset()
        if running then 
            GL.EndQuery(QueryTarget.TimeElapsed)
            running <- false
            current.Value <- parent

        if query >= 0 then 
            GL.DeleteQuery query
            query <- -1

        x.Start()

    member x.Stop() =
        cpu.Stop()
        if running then
            GL.EndQuery(QueryTarget.TimeElapsed)

            current.Value <- parent
            match parent with
                | Some p -> 
                    parent <- None
                    p.Add(x)
                    p.DoStart()
                | None -> ()

            running <- false

    member x.ElapsedGPU =
        let mutable ns = 0L
        GL.GetQueryObject(query, GetQueryObjectParam.QueryResult, &ns)
        let childTime = children |> List.sumBy (fun c -> c.ElapsedGPU)
        MicroTime(ns) + childTime

    member x.ElapsedCPU =
        cpu.Elapsed |> MicroTime
  
type OpenGlQuery(target : QueryTarget) =
    
    static let active = new ThreadLocal<Map<QueryTarget, OpenGlQuery>>(fun () -> Map.empty)

    static let getActive (target : QueryTarget) =
        Map.tryFind target active.Value

    static let setActive (target : QueryTarget) (v : Option<OpenGlQuery>) =
        match v with
            | Some v -> active.Value <- Map.add target v active.Value
            | None -> active.Value <- Map.remove target active.Value

    let mutable query = -1
    let mutable running = false
    let mutable parent = None
    let mutable children = []

    member private x.Push() =
        let current = getActive target
        match current with
            | Some current -> current.DoStop()
            | None -> ()
        parent <- current

    member private x.Pop() =
        match parent with
            | Some p -> p.DoStart()
            | None -> ()

        setActive target parent
        parent <- None

    member private x.DoStop() =
        GL.EndQuery(target)

    member private x.DoStart() =
        GL.BeginQuery(target, query)

    member private x.Add(q : OpenGlQuery) =
        children <- q :: children

    member x.Start() =
        if not running then
            running <- true
            children <- []
            if query < 0 then query <- GL.GenQuery()
            x.Push()
            x.DoStart()

    member x.Stop() =
        if running then
            running <- false
            GL.EndQuery(target)
            x.Pop()

    member x.Reset() =
        x.Stop()
        if query >= 0 then 
            GL.DeleteQuery(query)
            query <- -1

    member x.Restart() =
        x.Reset()
        x.Start()

    member x.Value =
        let mutable res = 0L
        GL.GetQueryObject(query, GetQueryObjectParam.QueryResult, &res)
        res + (children |> List.sumBy (fun q -> q.Value))

            
type RenderTaskScope =
    {
        currentContext  : IMod<ContextHandle>
        stats           : ref<FrameStatistics>
    }

