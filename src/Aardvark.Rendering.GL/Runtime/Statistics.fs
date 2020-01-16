namespace Aardvark.Rendering.GL

open System.Threading
open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL

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
        GL.Check "[OpenGlStopwatch.DoStop] EndQuery"
        cpu.Stop()

    member private x.DoStart() =
        cpu.Start()
        GL.BeginQuery(QueryTarget.TimeElapsed, query)
        GL.Check "[OpenGlStopwatch.DoStart] BeginQuery"


    member private x.Add(other : OpenGlStopwatch) =
        children <- other::children

    member x.Start() =
        cpu.Start()
        if not running then
            if query < 0 then 
                query <- GL.GenQuery()
                GL.Check "[OpenGlStopwatch.Start] GenQuery"

            match current.Value with
                | Some p -> 
                    p.DoStop()
                    parent <- Some p
                | None ->
                    parent <- None
            
            GL.BeginQuery(QueryTarget.TimeElapsed, query)
            GL.Check "[OpenGlStopwatch.Start] BeginQuery"

            running <- true
            children <- []
            current.Value <- Some x

    member x.Restart() =
        cpu.Stop()
        cpu.Reset()
        if running then 
            GL.EndQuery(QueryTarget.TimeElapsed)
            GL.Check "[OpenGlStopwatch.Restart] EndQuery"
            running <- false
            current.Value <- parent

        if query >= 0 then 
            GL.DeleteQuery query
            GL.Check "[OpenGlStopwatch.Restart] DeleteQuery"
            query <- -1

        x.Start()

    member x.Stop() =
        cpu.Stop()
        if running then
            GL.EndQuery(QueryTarget.TimeElapsed)
            GL.Check "[OpenGlStopwatch.Stop] EndQuery"

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
        GL.Check "[OpenGlStopwatch.ElapsedGPU] GetQueryObject"
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

            