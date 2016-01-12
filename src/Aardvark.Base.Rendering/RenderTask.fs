namespace Aardvark.Base

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Incremental.Operators
open System.Collections.Generic
open Aardvark.Base.Rendering

module ChangeableResources =

    let createTexture (runtime : IRuntime) (samples : IMod<int>) (size : IMod<V2i>) (format : IMod<TextureFormat>) =
        let mutable current = None

        Mod.custom (fun self ->
            let samples = samples.GetValue self
            let size = size.GetValue self
            let format = format.GetValue self

            match current with
                | Some (samples', size', format', c : IBackendTexture) -> 
                    if samples = samples' && size = size' && format = format' then
                        c
                    else
                        runtime.DeleteTexture c
                        let n = runtime.CreateTexture(size, format, 1, samples, 1)
                        current <- Some (samples, size, format, n)
                        n
                | None ->
                    let n = runtime.CreateTexture(size, format, 1, samples, 1)
                    current <- Some (samples, size, format, n)
                    n
        )

    let createRenderbuffer (runtime : IRuntime) (samples : IMod<int>) (size : IMod<V2i>) (format : IMod<RenderbufferFormat>) =
        let mutable current = None

        Mod.custom (fun self ->
            let samples = samples.GetValue self
            let size = size.GetValue self
            let format = format.GetValue self

            match current with
                | Some (samples', size', format', c : IRenderbuffer) -> 
                    if samples = samples' && size = size' && format = format' then
                        c
                    else
                        runtime.DeleteRenderbuffer c
                        let n = runtime.CreateRenderbuffer(size, format, samples)
                        current <- Some (samples, size, format, n)
                        n
                | None ->
                    let n = runtime.CreateRenderbuffer(size, format, samples)
                    current <- Some (samples, size, format, n)
                    n
        )

    let createFramebuffer (runtime : IRuntime) (signature : IFramebufferSignature) (color : Option<IMod<#IFramebufferOutput>>) (depth : Option<IMod<#IFramebufferOutput>>) =
        
        let mutable current = None

        Mod.custom (fun self ->
            let color = 
                match color with
                    | Some c -> Some (c.GetValue self)
                    | None -> None

            let depth =
                match depth with
                    | Some d -> Some (d.GetValue self)
                    | None -> None

            let create (color : Option<#IFramebufferOutput>) (depth : Option<#IFramebufferOutput>) =
                match color, depth with
                    | Some c, Some d -> 
                        runtime.CreateFramebuffer(
                            signature,
                            Map.ofList [
                                DefaultSemantic.Colors, c :> IFramebufferOutput
                                DefaultSemantic.Depth, d :> IFramebufferOutput
                            ]
                        )
                    | Some c, None ->
                        runtime.CreateFramebuffer(
                            signature,
                            Map.ofList [
                                DefaultSemantic.Colors, c :> IFramebufferOutput
                            ]
                        )
                    | None, Some d ->
                        runtime.CreateFramebuffer(
                            signature,
                            Map.ofList [
                                DefaultSemantic.Depth, d :> IFramebufferOutput
                            ]
                        ) 
                    | None, None -> failwith "empty framebuffer"
                            
            match current with
                | Some (c,d,f) ->
                    if c = color && d = depth then
                        f
                    else
                        runtime.DeleteFramebuffer f
                        let n = create color depth
                        current <- Some (color, depth, n)
                        n
                | None -> 
                    let n = create color depth
                    current <- Some (color, depth, n)
                    n
        )

    let createFramebufferFromTexture (runtime : IRuntime) (signature : IFramebufferSignature) (color : IMod<IBackendTexture>) (depth : IMod<IRenderbuffer>) =
        createFramebuffer  runtime signature  ( color |> Mod.map (fun s -> { texture = s; slice = 0; level = 0 } :> IFramebufferOutput) |> Some ) ( Mod.cast depth |> Some )


module RenderTask =

    open ChangeableResources
    
    type private EmptyRenderTask() =
        inherit ConstantObject()
        interface IRenderTask with
            member x.FramebufferSignature = null
            member x.Dispose() = ()
            member x.Run(caller, fbo) = RenderingResult(fbo.framebuffer, FrameStatistics.Zero)
            member x.Runtime = None
            member x.FrameId = 0UL

    type private SequentialRenderTask(f : RenderingResult -> RenderingResult, tasks : IRenderTask[]) as this =
        inherit AdaptiveObject()

        do for t in tasks do t.AddOutput this

        let signature =
            lazy (
                let signatures = tasks |> Array.map (fun t -> t.FramebufferSignature) |> Array.filter (not << isNull)

                if signatures.Length = 0 then null
                elif signatures.Length = 1 then signatures.[0]
                else 
                    let s0 = signatures.[0]
                    let all = signatures |> Array.forall (fun s -> s0.IsAssignableFrom s0)
                    if all then s0
                    else failwithf "cannot compose RenderTasks with different FramebufferSignatures: %A" signatures
            )

        let runtime = tasks |> Array.tryPick (fun t -> t.Runtime)
        let mutable frameId = 0UL

        interface IRenderTask with
            member x.FramebufferSignature = signature.Value
            member x.Run(caller, fbo) =
                x.EvaluateAlways caller (fun () ->
                    let mutable stats = FrameStatistics.Zero
                    for t in tasks do
                        let res = t.Run(x, fbo)
                        frameId <- max frameId t.FrameId
                        stats <- stats + res.Statistics

                    RenderingResult(fbo.framebuffer, stats) |> f
                )

            member x.Dispose() =
                for t in tasks do t.RemoveOutput this

            member x.Runtime = runtime

            member x.FrameId = frameId

        new(tasks : IRenderTask[]) = new SequentialRenderTask(id, tasks)

    type private ModRenderTask(input : IMod<IRenderTask>) as this =
        inherit AdaptiveObject()
        do input.AddOutput this
        let mutable inner : Option<IRenderTask> = None
        let mutable frameId = 0UL

        interface IRenderTask with
            member x.FramebufferSignature = 
                let v = input.GetValue x
                v.FramebufferSignature

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

                            ni.AddOutput x

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
        do reader.AddOutput this

        let mutable signature = null
        let mutable runtime = None
        let tasks = ReferenceCountingSet()

        let mutable frameId = 0UL

        let add (t : IRenderTask) =
            if tasks.Add t then
                match t.Runtime with
                    | Some r -> runtime <- Some r
                    | None -> ()

                let innerSig = t.FramebufferSignature
                
                if isNull innerSig then
                    ()
                elif isNull signature then
                    signature <- innerSig
                elif not (signature.IsAssignableFrom innerSig) then
                    failwithf "cannot compose RenderTasks with different FramebufferSignatures: %A vs. %A" signature innerSig
                    
                t.AddOutput this

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
            member x.FramebufferSignature =
                lock this (fun () -> processDeltas())
                signature

            member x.Run(caller, fbo) =
                x.EvaluateAlways caller (fun () ->
                    processDeltas()

                    // run all tasks
                    let mutable stats = FrameStatistics.Zero

                    // TODO: order may be invalid
                    for (_,t) in reader.Content.All do
                        let res = t.Run(x, fbo)
                        frameId <- max frameId t.FrameId
                        stats <- stats + res.Statistics

                    // return the accumulated statistics
                    RenderingResult(fbo.framebuffer, stats)
                )

            member x.Dispose() =
                reader.RemoveOutput this
                reader.Dispose()

                for i in tasks do
                    i.RemoveOutput x
                tasks.Clear()
                
            member x.Runtime =
                lock this (fun () -> processDeltas())
                runtime

            member x.FrameId = frameId

    type private CustomRenderTask(f : afun<IRenderTask * OutputDescription, RenderingResult>) as this =
        inherit AdaptiveObject()
        do f.AddOutput this
        interface IRenderTask with
            member x.FramebufferSignature = null
            member x.Run(caller, fbo) =
                x.EvaluateAlways caller (fun () ->
                    f.Evaluate (x,(x :> IRenderTask,fbo))
                )

            member x.Dispose() =
                f.RemoveOutput this
                
            member x.Runtime =
                None
            
            member x.FrameId = 0UL


    let empty = new EmptyRenderTask() :> IRenderTask

    let ofAFun (f : afun<IRenderTask * OutputDescription, RenderingResult>) =
        new CustomRenderTask(f) :> IRenderTask

    let custom (f : IRenderTask * OutputDescription -> RenderingResult) =
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


    let private defaultView (m : IMod<IBackendTexture>) =
        m |> Mod.map (fun t ->
            { texture = t; level = 0; slice = 0 }
        )


    let private getResult (sem : Symbol) (t : RenderToFramebufferMod) =
        RenderingResultMod(t, sem) :> IMod<_>

    let renderTo (target : IMod<OutputDescription>) (task : IRenderTask) : RenderToFramebufferMod =
        RenderToFramebufferMod(task, target)

    let renderToColorMS (samples : IMod<int>) (size : IMod<V2i>) (format : IMod<TextureFormat>) (task : IRenderTask) =
        let runtime = task.Runtime.Value
        let signature = task.FramebufferSignature

        //use lock = runtime.ContextLock
        let color = createTexture runtime samples size format //runtime.CreateTexture(size, format, samples, ~~1)
        let depth = createRenderbuffer runtime samples size ~~RenderbufferFormat.DepthComponent32 // runtime.CreateRenderbuffer(size, ~~RenderbufferFormat.Depth24Stencil8, samples)
        let clear = runtime.CompileClear(signature, ~~C4f.Black, ~~1.0)

        let fbo = 
            createFramebuffer runtime signature (Some <| defaultView color) (Some depth)
            |> Mod.map OutputDescription.ofFramebuffer

        new SequentialRenderTask([|clear; task|]) 
            |> renderTo fbo
            |> getResult DefaultSemantic.Colors


    let renderToDepthMS (samples : IMod<int>) (size : IMod<V2i>) (task : IRenderTask) =
        let runtime = task.Runtime.Value
        let signature = task.FramebufferSignature

        //use lock = runtime.ContextLock
        let depth = createTexture runtime samples size ~~TextureFormat.DepthComponent32
        let clear = runtime.CompileClear(signature, ~~Map.empty, ~~(Some 1.0))

        

        let fbo = 
            createFramebuffer runtime signature None (Some <| defaultView depth)
            |> Mod.map OutputDescription.ofFramebuffer


 
        new SequentialRenderTask([|clear; task|]) 
            |> renderTo fbo
            |> getResult DefaultSemantic.Depth


    let renderToColorAndDepthMS (samples : IMod<int>) (size : IMod<V2i>) (format : IMod<TextureFormat>) (task : IRenderTask) =
        let runtime = task.Runtime.Value
        let signature = task.FramebufferSignature

        //use lock = runtime.ContextLock
        let color = createTexture runtime samples size format //runtime.CreateTexture(size, format, samples, ~~1)
        let depth = createTexture runtime samples size ~~TextureFormat.DepthComponent32 // runtime.CreateRenderbuffer(size, ~~RenderbufferFormat.Depth24Stencil8, samples)
        let clear = runtime.CompileClear(signature, ~~C4f.Black, ~~1.0)

        let fbo = 
            createFramebuffer runtime signature (Some <| defaultView color) (Some <| defaultView depth)
            |> Mod.map OutputDescription.ofFramebuffer

        let renderResult = 
            new SequentialRenderTask([|clear; task|]) 
                |> renderTo fbo

        let colorTexture = renderResult |> getResult DefaultSemantic.Colors
        let depthTexture = renderResult |> getResult DefaultSemantic.Depth

        colorTexture, depthTexture

    let inline renderToColor (size : IMod<V2i>) (format : IMod<TextureFormat>) (task : IRenderTask) =
        renderToColorMS ~~1 size format task

    let inline renderToDepth (size : IMod<V2i>) (task : IRenderTask) =
        renderToDepthMS ~~1 size task

    let inline renderToColorAndDepth (size : IMod<V2i>) (format : IMod<TextureFormat>) (task : IRenderTask) =
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
