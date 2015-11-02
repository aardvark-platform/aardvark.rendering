namespace Aardvark.Base

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Incremental.Operators
open System.Collections.Generic
open Aardvark.Base.Rendering


module RenderTask =
    
    type private EmptyRenderTask() =
        inherit ConstantObject()
        interface IRenderTask with
            member x.Dispose() = ()
            member x.Run(caller, fbo) = RenderingResult(fbo, FrameStatistics.Zero)
            member x.Runtime = None
            member x.FrameId = 0UL

    type private SequentialRenderTask(f : RenderingResult -> RenderingResult, tasks : IRenderTask[]) as this =
        inherit AdaptiveObject()

        do for t in tasks do t.AddOutputNew this

        let runtime = tasks |> Array.tryPick (fun t -> t.Runtime)
        let mutable frameId = 0UL

        interface IRenderTask with
            member x.Run(caller, fbo) =
                x.EvaluateAlways caller (fun () ->
                    let mutable stats = FrameStatistics.Zero
                    for t in tasks do
                        let res = t.Run(x, fbo)
                        frameId <- max frameId t.FrameId
                        stats <- stats + res.Statistics

                    RenderingResult(fbo, stats) |> f
                )

            member x.Dispose() =
                for t in tasks do t.RemoveOutput this

            member x.Runtime = runtime

            member x.FrameId = frameId

        new(tasks : IRenderTask[]) = new SequentialRenderTask(id, tasks)

    type private ModRenderTask(input : IMod<IRenderTask>) as this =
        inherit AdaptiveObject()
        do input.AddOutputNew this
        let mutable inner : Option<IRenderTask> = None
        let mutable frameId = 0UL

        interface IRenderTask with
            member x.Run(caller, fbo) =
                x.EvaluateAlways caller (fun () ->
                    x.OutOfDate <- true
                    let ni = input.GetValue x

                    match inner with
                        | Some oi when oi = ni -> ()
                        | _ ->
                            match inner with
                                | Some oi -> oi.RemoveOutput x
                                | _ -> ()

                            ni.AddOutputNew x

                    inner <- Some ni
                    frameId <- ni.FrameId
                    ni.Run(x, fbo)
                )

            member x.Dispose() =
                input.RemoveOutput x
                match inner with
                    | Some i -> 
                        i.RemoveOutput x
                        inner <- None
                    | _ -> ()

            member x.Runtime = input.GetValue(x).Runtime
            
            member x.FrameId = frameId

    type private AListRenderTask(tasks : alist<IRenderTask>) as this =
        inherit AdaptiveObject()
        let reader = tasks.GetReader()
        do reader.AddOutputNew this

        let mutable runtime = None
        let tasks = ReferenceCountingSet()

        let mutable frameId = 0UL

        let add (t : IRenderTask) =
            if tasks.Add t then
                match t.Runtime with
                    | Some r -> runtime <- Some r
                    | None -> ()
                t.AddOutputNew this

        let remove (t : IRenderTask) =
            if tasks.Remove t then
                t.RemoveOutput this

        let processDeltas() =
            // TODO: EvaluateAlways should ensure that self is OutOfDate since
            //       when its not we need a transaction to add outputs
            let wasOutOfDate = this.OutOfDate
            this.OutOfDate <- true

            // adjust the dependencies
            for d in reader.GetDelta(this) do
                match d with
                    | Add(_,t) -> add t
                    | Rem(_,t) -> remove t

            this.OutOfDate <- wasOutOfDate

        interface IRenderTask with
            member x.Run(caller, fbo) =
                x.EvaluateAlways caller (fun () ->
                    processDeltas()

                    // run all tasks
                    let mutable stats = FrameStatistics.Zero
                    for (_,t) in reader.Content do
                        let res = t.Run(x, fbo)
                        frameId <- max frameId t.FrameId
                        stats <- stats + res.Statistics

                    // return the accumulated statistics
                    RenderingResult(fbo, stats)
                )

            member x.Dispose() =
                reader.RemoveOutput this
                reader.Dispose()

                for i in tasks do
                    i.RemoveOutput x
                tasks.Clear()
                
            member x.Runtime =
                processDeltas()
                runtime

            member x.FrameId = frameId

    type private CustomRenderTask(f : afun<IFramebuffer, RenderingResult>) as this =
        inherit AdaptiveObject()
        do f.AddOutputNew this
        interface IRenderTask with
            member x.Run(caller, fbo) =
                x.EvaluateAlways caller (fun () ->
                    f.Evaluate (x,fbo)
                )

            member x.Dispose() =
                f.RemoveOutput this
                
            member x.Runtime =
                None
            
            member x.FrameId = 0UL


    let empty = new EmptyRenderTask() :> IRenderTask

    let ofAFun (f : afun<IFramebuffer, RenderingResult>) =
        new CustomRenderTask(f) :> IRenderTask

    let custom (f : IFramebuffer -> RenderingResult) =
        new CustomRenderTask(AFun.create f) :> IRenderTask


    let ofMod (m : IMod<IRenderTask>) : IRenderTask =
        new ModRenderTask(m) :> IRenderTask

    let bind (f : 'a -> IRenderTask) (m : IMod<'a>) : IRenderTask =
        new ModRenderTask(Mod.map f m) :> IRenderTask

    let ofSeq (s : seq<IRenderTask>) =
        new SequentialRenderTask(Seq.toArray s) :> IRenderTask

    let ofList (s : list<IRenderTask>) =
        new SequentialRenderTask(List.toArray s) :> IRenderTask

    let ofArray (s : IRenderTask[]) =
        new SequentialRenderTask(s) :> IRenderTask

    let ofAList (s : alist<IRenderTask>) =
        new AListRenderTask(s) :> IRenderTask

    let ofASet (s : aset<IRenderTask>) =
        new AListRenderTask(s |> ASet.sortWith (fun a b -> compare a.Id b.Id)) :> IRenderTask

    let mapResult (f : RenderingResult -> RenderingResult) (t : IRenderTask) =
        new SequentialRenderTask(f, [|t|]) :> IRenderTask

    let mapStatistics (f : FrameStatistics -> FrameStatistics) (t : IRenderTask) =
        t |> mapResult (fun r -> RenderingResult(r.Framebuffer, f r.Statistics))


    // rendering to textures

    let renderTo (target : IFramebuffer) (task : IRenderTask) =
        let runtime = task.Runtime.Value
        [task :> IAdaptiveObject] 
            |> Mod.mapCustom(fun s ->
                task.Run(s, target) |> ignore
                target
            )

    let renderToColorMS (samples : IMod<int>) (size : IMod<V2i>) (format : IMod<PixFormat>) (task : IRenderTask) =
        let runtime = task.Runtime.Value

        //use lock = runtime.ContextLock
        let color = runtime.CreateTexture(size, format, samples, ~~1)
        let depth = runtime.CreateRenderbuffer(size, ~~RenderbufferFormat.Depth24Stencil8, samples)
        let clear = runtime.CompileClear(~~C4f.Black, ~~1.0)

        let fbo = 
            runtime.CreateFramebuffer(
                Map.ofList [
                    DefaultSemantic.Colors, ~~({ texture = color; level = 0; slice = 0 } :> IFramebufferOutput)
                    DefaultSemantic.Depth, ~~(depth :> IFramebufferOutput)
                ]
            )


        new SequentialRenderTask([|clear; task|]) 
            |> renderTo fbo
            |> Mod.map (fun _ -> color :> ITexture)

    let renderToDepthMS (samples : IMod<int>) (size : IMod<V2i>) (task : IRenderTask) =
        let runtime = task.Runtime.Value

        //use lock = runtime.ContextLock
        let depth = runtime.CreateTexture(size, ~~PixFormat.FloatGray, samples, ~~1)
        let clear = runtime.CompileClear(~~C4f.Black, ~~1.0)

        let fbo = 
            runtime.CreateFramebuffer(
                Map.ofList [
                    DefaultSemantic.Depth, ~~({ texture = depth; level = 0; slice = 0 } :> IFramebufferOutput)
                ]
            )


        new SequentialRenderTask([|clear; task|]) 
            |> renderTo fbo
            |> Mod.map (fun _ -> depth :> ITexture)

    let renderToColorAndDepthMS (samples : IMod<int>) (size : IMod<V2i>) (format : IMod<PixFormat>) (task : IRenderTask) =
        let runtime = task.Runtime.Value

        //use lock = runtime.ContextLock
        let color = runtime.CreateTexture(size, format, samples, ~~1)
        let depth = runtime.CreateTexture(size, ~~PixFormat.FloatGray, samples, ~~1)
        let clear = runtime.CompileClear(~~C4f.Black, ~~1.0)

        let fbo = 
            runtime.CreateFramebuffer(
                Map.ofList [
                    DefaultSemantic.Colors, ~~({ texture = color; level = 0; slice = 0 } :> IFramebufferOutput)
                    DefaultSemantic.Depth, ~~({ texture = depth; level = 0; slice = 0 } :> IFramebufferOutput)
                ]
            )

        let result = 
            new SequentialRenderTask([|clear; task|]) 
                |> renderTo fbo

        (Mod.map (fun _ -> color :> ITexture) result, Mod.map (fun _ -> depth :> ITexture) result)


    let inline renderToColor (size : IMod<V2i>) (format : IMod<PixFormat>) (task : IRenderTask) =
        renderToColorMS ~~1 size format task

    let inline renderToDepth (size : IMod<V2i>) (task : IRenderTask) =
        renderToDepthMS ~~1 size task

    let inline renderToColorAndDepth (size : IMod<V2i>) (format : IMod<PixFormat>) (task : IRenderTask) =
        renderToColorAndDepthMS ~~1 size format task

[<AutoOpen>]
module ``RenderTask Builder`` =

    type RenderTaskBuilder() =
        member x.Bind(m : IMod<'a>, f : 'a -> alist<IRenderTask>) =
            alist.Bind(m, f)

        member x.For(s : alist<'a>, f : 'a -> alist<IRenderTask>) =
            alist.For(s,f)


        member x.Yield(t : IRenderTask) = 
            alist.Yield(t)

        member x.YieldFrom(l : alist<IRenderTask>) =
            alist.YieldFrom(l)

        member x.Yield(m : IMod<IRenderTask>) =
            m |> RenderTask.ofMod |> alist.Yield

        member x.Combine(l : alist<IRenderTask>, r : alist<IRenderTask>) =
            alist.Combine(l,r)

        member x.Delay(f : unit -> alist<IRenderTask>) = 
            alist.Delay(f)

        member x.Zero() =
            alist.Zero()

        member x.Run(l : alist<IRenderTask>) =
            RenderTask.ofAList l


    let rendertask = RenderTaskBuilder()
