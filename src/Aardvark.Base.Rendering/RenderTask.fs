namespace Aardvark.Base

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Incremental.Operators
open System.Collections.Generic
open Aardvark.Base.Rendering
open System.Runtime.CompilerServices
open System.Threading

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

    let createFramebuffer (runtime : IRuntime) (signature : IFramebufferSignature) (color : Option<IMod<#IFramebufferOutput>>) (depth : Option<IMod<#IFramebufferOutput>>) (stencil : Option<IMod<#IFramebufferOutput>>) =
        
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

            let stencil =
                match stencil with
                    | Some s -> Some (s.GetValue self)
                    | None -> None

            let create (color : Option<#IFramebufferOutput>) (depth : Option<#IFramebufferOutput>) (stencil : Option<#IFramebufferOutput>) =
                match color, depth, stencil with
                    | Some c, Some d, None -> 
                        runtime.CreateFramebuffer(
                            signature,
                            Map.ofList [
                                DefaultSemantic.Colors, c :> IFramebufferOutput
                                DefaultSemantic.Depth, d :> IFramebufferOutput
                            ]
                        )
                    | Some c, None, None ->
                        runtime.CreateFramebuffer(
                            signature,
                            Map.ofList [
                                DefaultSemantic.Colors, c :> IFramebufferOutput
                            ]
                        )
                    | None, Some d, None ->
                        runtime.CreateFramebuffer(
                            signature,
                            Map.ofList [
                                DefaultSemantic.Depth, d :> IFramebufferOutput
                            ]
                        ) 

                    | Some c, Some d, Some s ->
                        runtime.CreateFramebuffer(
                            signature,
                            Map.ofList [
                                DefaultSemantic.Colors, c :> IFramebufferOutput
                                DefaultSemantic.Depth, d :> IFramebufferOutput
                                DefaultSemantic.Stencil, s :> IFramebufferOutput
                            ]
                        )


                    | None, Some d, Some s ->
                        runtime.CreateFramebuffer(
                            signature,
                            Map.ofList [
                                DefaultSemantic.Depth, d :> IFramebufferOutput
                                DefaultSemantic.Stencil, s :> IFramebufferOutput
                            ]
                        )

                    | Some c, None, Some s ->
                        runtime.CreateFramebuffer(
                            signature,
                            Map.ofList [
                                DefaultSemantic.Colors, c :> IFramebufferOutput
                                DefaultSemantic.Stencil, s :> IFramebufferOutput
                            ]
                        )



                    | None, None, Some s ->
                        runtime.CreateFramebuffer(
                            signature,
                            Map.ofList [
                                DefaultSemantic.Stencil, s :> IFramebufferOutput
                            ]
                        )

                    | None, None, None -> failwith "empty framebuffer"
                            
            match current with
                | Some (c,d,s,f) ->
                    if c = color && d = depth && s = stencil then
                        f
                    else
                        runtime.DeleteFramebuffer f
                        let n = create color depth stencil
                        current <- Some (color, depth, stencil, n)
                        n
                | None -> 
                    let n = create color depth stencil
                    current <- Some (color, depth, stencil, n)
                    n
        )

    let createFramebufferFromTexture (runtime : IRuntime) (signature : IFramebufferSignature) (color : IMod<IBackendTexture>) (depth : IMod<IRenderbuffer>) =
        createFramebuffer  runtime signature  ( color |> Mod.map (fun s -> { texture = s; slice = 0; level = 0 } :> IFramebufferOutput) |> Some ) ( Mod.cast depth |> Some )


type IOutputMod<'a> =
    inherit IMod<'a>
    abstract member LastStatistics : FrameStatistics
    abstract member Acquire : unit -> unit
    abstract member Release : unit -> unit

[<AutoOpen>]
module private RefCountedResources = 
    type ChangeableFramebuffer(runtime : IRuntime, signature : IFramebufferSignature, textures : Set<Symbol>, size : IMod<V2i>) =
        inherit Mod.AbstractMod<IFramebuffer>()

        let mutable refCount = 0

        let mutable colors = Map.empty
        let mutable depth = None
        let mutable stencil = None
        let mutable handle = None

        // TODO: create renderbuffers where textures are not needed (specified by textures-set)

        let createTexture (size : IMod<V2i>) (att : AttachmentSignature) =
            let mutable old = None
            Mod.custom (fun self ->
                let s = size.GetValue self
                let tex = runtime.CreateTexture(s, unbox (int att.format), 1, att.samples, 1)

                match old with
                    | Some o -> runtime.DeleteTexture(o)
                    | None -> ()

                old <- Some tex

                tex
            )



        let create() =
            Log.line "framebuffer created"
            let colorTextures = 
                signature.ColorAttachments 
                    |> Map.toSeq
                    |> Seq.map (fun (idx, (sem,att)) -> sem, createTexture size att)
                    |> Map.ofSeq

            let depthTexture =
                match signature.DepthAttachment with
                    | Some att -> createTexture size att |> Some
                    | None -> None

            let stencilTexture =
                match signature.StencilAttachment with
                    | Some att -> createTexture size att |> Some
                    | None -> None
       
            let mutable current = None
            let fbo = 
                Mod.custom (fun self ->
                    let attachments = colorTextures |> Map.map (fun _ v -> { texture = v.GetValue self; slice = 0; level = 0 } :> IFramebufferOutput)

                    let attachments =
                        match depthTexture with
                            | Some v -> Map.add DefaultSemantic.Depth ({ texture = v.GetValue self; slice = 0; level = 0 } :> IFramebufferOutput) attachments
                            | None -> attachments

                    let attachments =
                        match stencilTexture with
                            | Some v -> Map.add DefaultSemantic.Stencil ({ texture = v.GetValue self; slice = 0; level = 0 } :> IFramebufferOutput) attachments
                            | None -> attachments

                    match current with
                        | Some old -> runtime.DeleteFramebuffer(old)
                        | None -> ()

                    let v = runtime.CreateFramebuffer(signature, attachments)
                    current <- Some v
                    v
                )

            colors <- colorTextures
            depth <- depthTexture
            stencil <- stencilTexture
            handle <- Some fbo

            fbo


        let destroy() =
            Log.line "framebuffer deleted"

            handle |> Option.iter (fun v -> runtime.DeleteFramebuffer (v |> unbox<Mod.AbstractMod<IFramebuffer>>).cache)
            colors |> Map.iter (fun _ v -> runtime.DeleteTexture (v |> unbox<Mod.AbstractMod<IBackendTexture>>).cache)
            depth |> Option.iter (fun v -> runtime.DeleteTexture (v |> unbox<Mod.AbstractMod<IBackendTexture>>).cache)
            stencil |> Option.iter (fun v -> runtime.DeleteTexture (v |> unbox<Mod.AbstractMod<IBackendTexture>>).cache)
       
            handle <- None
            colors <- Map.empty
            depth <- None
            stencil <- None


        member x.Acquire() =
            Interlocked.Increment(&refCount) |> ignore

        member x.Release() =
            if Interlocked.Decrement(&refCount) = 0 then
                lock x destroy
                transact (fun () -> x.MarkOutdated())

        override x.Compute() =
            if refCount = 0 then
                failwith "pull on ChangeableFramebuffer without reference!!"

            match handle with
                | Some h ->
                    h.GetValue x

                | None ->
                    let h = create()
                    h.GetValue x

        override x.Inputs =
            seq {
                yield! colors |> Map.toSeq |> Seq.map snd |> Seq.cast
                match depth with
                    | Some d -> yield d :> _
                    | None -> ()

                match stencil with
                    | Some s -> yield s :> _
                    | None -> ()
            }

        interface IOutputMod<IFramebuffer> with
            member x.LastStatistics = FrameStatistics.Zero
            member x.Acquire() = x.Acquire()
            member x.Release() = x.Release()

    type AdaptiveRenderingResult(task : IRenderTask, target : IMod<IFramebuffer>) =
        inherit Mod.AbstractMod<RenderingResult>()

        let mutable refCount = 0
        let mutable stats = FrameStatistics.Zero

        let targetRef = 
            match target with
                | :? IOutputMod<IFramebuffer> as t -> Some t
                | _ -> None

        override x.Compute() =
            if refCount = 0 then
                failwith "pull on AdaptiveRenderingResult without reference!!"

            let fbo = target.GetValue x
            let res =task.Run(x, OutputDescription.ofFramebuffer fbo)
            stats <- res.Statistics
            res

        override x.Inputs =
            seq {
                yield task :> _
                yield target :> _
            }

        member x.Acquire() =
            if Interlocked.Increment(&refCount) = 1 then
                Log.line "result created"
                match targetRef with
                    | Some t -> t.Acquire()
                    | None -> ()

        member x.Release() =
            if Interlocked.Decrement(&refCount) = 0 then
                Log.line "result deleted"
                match targetRef with
                    | Some t -> t.Release()
                    | None -> ()

        interface IOutputMod<RenderingResult> with
            member x.LastStatistics = 
                match targetRef with 
                    | Some m -> stats + m.LastStatistics 
                    | None -> stats

            member x.Acquire() = x.Acquire()
            member x.Release() = x.Release()

    type AdaptiveOutputTexture(semantic : Symbol, res : IMod<RenderingResult>) =
        inherit Mod.AbstractMod<ITexture>()

        let mutable refCount = 0

        let resultRef =
            match res with
                | :? IOutputMod<RenderingResult> as r -> Some r
                | _ -> None

        override x.Compute() =
            if refCount = 0 then
                failwith "pull on AdaptiveOutputTexture without reference!!"

            let res = res.GetValue x

            match Map.tryFind semantic res.Framebuffer.Attachments with
                | Some (:? BackendTextureOutputView as t) ->
                    t.texture :> ITexture
                | _ ->
                    failwithf "could not get result for semantic %A as texture" semantic

        override x.Inputs =
            Seq.singleton (res :> _)

        member x.Acquire() =
            if Interlocked.Increment &refCount = 1 then
                Log.line "texture created"
                match resultRef with
                    | Some r -> r.Acquire()
                    | None -> ()

        member x.Release() =
            if Interlocked.Decrement &refCount = 0 then
                Log.line "texture deleted"
                match resultRef with
                    | Some r -> r.Release()
                    | None -> ()

        interface IOutputMod<ITexture> with
            member x.LastStatistics =
                match resultRef with
                    | Some r -> r.LastStatistics
                    | None -> FrameStatistics.Zero

            member x.Acquire() = x.Acquire()
            member x.Release() = x.Release()
            

[<AbstractClass; Sealed; Extension>]
type RuntimeFramebufferExtensions private() =

    [<Extension>]
    static member CreateFramebuffer (this : IRuntime, signature : IFramebufferSignature, textures : Set<Symbol>, size : IMod<V2i>) : IMod<IFramebuffer> =
        ChangeableFramebuffer(this, signature, textures, size) :> IMod<IFramebuffer>

    [<Extension>]
    static member CreateFramebuffer (this : IRuntime, signature : IFramebufferSignature, size : IMod<V2i>) : IMod<IFramebuffer> =
        let sems =
            Set.ofList [
                yield! signature.ColorAttachments |> Map.toSeq |> Seq.map snd |> Seq.map fst
                if Option.isSome signature.DepthAttachment then yield DefaultSemantic.Depth
                if Option.isSome signature.StencilAttachment then yield DefaultSemantic.Stencil
            ]
        
        ChangeableFramebuffer(this, signature, sems, size) :> IMod<IFramebuffer>

    [<Extension>]
    static member RenderTo(this : IRenderTask, output : IMod<IFramebuffer>) =
        AdaptiveRenderingResult(this, output) :> IMod<_>

    [<Extension>]
    static member GetOutputTexture (this : IMod<RenderingResult>, semantic : Symbol) =
        AdaptiveOutputTexture(semantic, this) :> IMod<_>

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


    let getResult (sem : Symbol) (t : IMod<RenderingResult>) =
        t.GetOutputTexture sem

    let renderTo (target : IMod<IFramebuffer>) (task : IRenderTask) : IMod<RenderingResult> =
        task.RenderTo target

    let renderSemantics (sem : Set<Symbol>) (size : IMod<V2i>) (task : IRenderTask) =
        let runtime = task.Runtime.Value
        let signature = task.FramebufferSignature

        let clear = runtime.CompileClear(signature, ~~C4f.Black, ~~1.0)
        let fbo = runtime.CreateFramebuffer(signature, sem, size)

        let res = 
            new SequentialRenderTask([|clear; task|]) |> renderTo fbo
        

        sem |> Seq.map (fun k -> k, getResult k res) |> Map.ofSeq


    let renderToColor (size : IMod<V2i>) (task : IRenderTask) =
        task |> renderSemantics (Set.singleton DefaultSemantic.Colors) size |> Map.find DefaultSemantic.Colors

    let renderToDepth (size : IMod<V2i>) (task : IRenderTask) =
        task |> renderSemantics (Set.singleton DefaultSemantic.Depth) size |> Map.find DefaultSemantic.Depth

    let renderToDepthAndStencil (size : IMod<V2i>) (task : IRenderTask) =
        let map = task |> renderSemantics (Set.singleton DefaultSemantic.Depth) size
        (Map.find DefaultSemantic.Depth map, Map.find DefaultSemantic.Stencil map)

    let renderToColorAndDepth (size : IMod<V2i>) (task : IRenderTask) =
        let map = task |> renderSemantics (Set.singleton DefaultSemantic.Depth) size
        (Map.find DefaultSemantic.Colors map, Map.find DefaultSemantic.Depth map)


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
