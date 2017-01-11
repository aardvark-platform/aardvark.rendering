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


[<AutoOpen>]
module private RefCountedResources = 

    type IMod<'a> with
        member x.GetValue(t : RenderToken, c : IAdaptiveObject) =
            match x with
                | :? IOutputMod<'a> as x -> x.GetValue(t, c)
                | _ -> x.GetValue(c)

    type ChangeableFramebuffer(runtime : IRuntime, signature : IFramebufferSignature, textures : Set<Symbol>, size : IMod<V2i>) =
        inherit AbstractOutputMod<IFramebuffer>()

        let mutable refCount = 0

        let mutable colors = Map.empty
        let mutable depth = None
        let mutable stencil = None
        let mutable handle = None

        // TODO: create renderbuffers where textures are not needed (specified by textures-set)

        let createTexture (token : RenderToken) (size : IMod<V2i>) (att : AttachmentSignature) =
            let mutable old = None
            Mod.custom (fun self ->
                let s = size.GetValue(token, self)
                let tex = runtime.CreateTexture(s, unbox (int att.format), 1, att.samples, 1)

                match old with
                    | Some o -> 
                        runtime.DeleteTexture(o)
                        token.ReplacedResource(ResourceKind.Texture)
                    | None ->
                        token.CreatedResource(ResourceKind.Texture)

                old <- Some tex

                tex
            )

        let create(t : RenderToken) =
            Log.line "framebuffer created"
            let colorTextures = 
                signature.ColorAttachments 
                    |> Map.toSeq
                    |> Seq.map (fun (idx, (sem,att)) -> sem, createTexture t size att)
                    |> Map.ofSeq

            let depthTexture =
                match signature.DepthAttachment with
                    | Some att -> createTexture t size att |> Some
                    | None -> None

            let stencilTexture =
                match signature.StencilAttachment with
                    | Some att -> createTexture t size att |> Some
                    | None -> None
       
            let mutable current = None
            let fbo = 
                Mod.custom (fun self ->
                    let attachments = colorTextures |> Map.map (fun _ v -> { texture = v.GetValue(t, self); slice = 0; level = 0 } :> IFramebufferOutput)

                    let attachments =
                        match depthTexture with
                            | Some v -> Map.add DefaultSemantic.Depth ({ texture = v.GetValue(t,self); slice = 0; level = 0 } :> IFramebufferOutput) attachments
                            | None -> attachments

                    let attachments =
                        match stencilTexture with
                            | Some v -> Map.add DefaultSemantic.Stencil ({ texture = v.GetValue(t,self); slice = 0; level = 0 } :> IFramebufferOutput) attachments
                            | None -> attachments

                    match current with
                        | Some old -> 
                            runtime.DeleteFramebuffer(old)
                            t.ReplacedResource(ResourceKind.Framebuffer)
                        | None ->
                            t.CreatedResource(ResourceKind.Framebuffer)


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


        override x.Create() = ()
        override x.Destroy() = destroy()

        override x.Compute(t : RenderToken) =
            if refCount = 0 then
                failwith "pull on ChangeableFramebuffer without reference!!"

            match handle with
                | Some h ->
                    h.GetValue x

                | None ->
                    let h = create(t)
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

    type AdaptiveRenderingResult(task : IRenderTask, target : IMod<IFramebuffer>) =
        inherit AbstractOutputMod<IFramebuffer>()

        let targetRef = 
            match target with
                | :? IOutputMod<IFramebuffer> as t -> Some t
                | _ -> None

        override x.Compute(t : RenderToken) =
            let fbo = target.GetValue x
            task.Run(x, t, OutputDescription.ofFramebuffer fbo)
            fbo

        override x.Inputs =
            seq {
                yield task :> _
                yield target :> _
            }

        override x.Create() =
            Log.line "result created"
            match targetRef with
                | Some t -> t.Acquire()
                | None -> ()

        override x.Destroy() =
            Log.line "result deleted"
            match targetRef with
                | Some t -> t.Release()
                | None -> ()

    type AdaptiveOutputTexture(semantic : Symbol, res : IMod<IFramebuffer>) =
        inherit AbstractOutputMod<ITexture>()

        let resultRef =
            match res with
                | :? IOutputMod<IFramebuffer> as r -> Some r
                | _ -> None

        override x.Compute(t : RenderToken) =
            let res = 
                match resultRef with
                    | Some res -> res.GetValue(t, x)
                    | None -> res.GetValue(x)

            match Map.tryFind semantic res.Attachments with
                | Some (:? BackendTextureOutputView as t) ->
                    t.texture :> ITexture
                | _ ->
                    failwithf "could not get result for semantic %A as texture" semantic

        override x.Inputs =
            Seq.singleton (res :> _)

        override x.Create() =
            Log.line "texture created"
            match resultRef with
                | Some r -> r.Acquire()
                | None -> ()

        override x.Destroy() =
            Log.line "texture deleted"
            match resultRef with
                | Some r -> r.Release()
                | None -> ()
 

    



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
    static member GetOutputTexture (this : IMod<IFramebuffer>, semantic : Symbol) =
        AdaptiveOutputTexture(semantic, this) :> IMod<_>

[<AbstractClass>]
type AbstractRenderTask() =
    inherit AdaptiveObject()

    let mutable frameId = 0UL

    abstract member FramebufferSignature : Option<IFramebufferSignature>
    abstract member Runtime : Option<IRuntime>
    abstract member Update : RenderToken -> unit
    abstract member Run : RenderToken * OutputDescription -> unit
    abstract member Dispose : unit -> unit
    abstract member Use : (unit -> 'a) -> 'a
    

    member x.FrameId = frameId
    member x.Run(caller : IAdaptiveObject, t : RenderToken, out : OutputDescription) =
        x.EvaluateAlways caller (fun () ->
            x.Run(t, out)
            frameId <- frameId + 1UL
        )

    member x.Update(caller : IAdaptiveObject, t : RenderToken) =
        x.EvaluateIfNeeded caller () (fun () -> 
            x.Update(t)
        )

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface IRenderTask with
        member x.FramebufferSignature = x.FramebufferSignature
        member x.Runtime = x.Runtime
        member x.FrameId = frameId
        member x.Update(caller,t) = x.Update(caller,t)
        member x.Run(caller, t,out) = x.Run(caller, t,out)
        member x.Use f = x.Use f


module RenderTask =

    open ChangeableResources
    
    type private EmptyRenderTask private() =
        inherit ConstantObject()

        static let instance = new EmptyRenderTask() :> IRenderTask
        static member Instance = instance

        interface IRenderTask with
            member x.FramebufferSignature = None
            member x.Dispose() = ()
            member x.Update(caller,t) = ()
            member x.Run(caller, t, fbo) = ()
            member x.Runtime = None
            member x.FrameId = 0UL
            member x.Use f = f()

    type SequentialRenderTask(tasks : IRenderTask[]) =
        inherit AbstractRenderTask()

        let signature =
            lazy (
                let signatures = tasks |> Array.choose (fun t -> t.FramebufferSignature)

                if signatures.Length = 0 then None
                elif signatures.Length = 1 then Some signatures.[0]
                else 
                    let s0 = signatures.[0]
                    let all = signatures |> Array.forall (fun s -> s0.IsAssignableFrom s0)
                    if all then Some s0
                    else failwithf "cannot compose RenderTasks with different FramebufferSignatures: %A" signatures
            )

        let runtime = tasks |> Array.tryPick (fun t -> t.Runtime)
        member x.Tasks = tasks

        override x.Use(f : unit -> 'a) =
            lock x (fun () ->
                let rec run (i : int) =
                    if i >= tasks.Length then f()
                    else tasks.[i].Use (fun () -> run (i + 1))

                run 0
            )

        override x.Dispose() =
            for t in tasks do t.Dispose()

        override x.Update(token : RenderToken) =
            for t in tasks do
                t.Update(x,token)


        override x.Run(token : RenderToken, output : OutputDescription) =
            for t in tasks do
                t.Run(x, token, output)


        override x.FramebufferSignature = signature.Value
        override x.Runtime = runtime



    type private ModRenderTask(input : IMod<IRenderTask>) =
        inherit AbstractRenderTask()
        let mutable inner : Option<IRenderTask> = None

        let updateInner t x =
            let ni = input.GetValue(t, x)

            match inner with
                | Some oi when oi = ni -> ()
                | _ ->
                    match inner with
                        | Some oi -> oi.Dispose()
                        | _ -> ()

                    ni.AddOutput x

            inner <- Some ni
            ni

        override x.Use(f : unit -> 'a) =
            lock x (fun () ->
                lock input (fun () ->
                    input.GetValue().Use f
                )
            )

        override x.FramebufferSignature = 
            let v = input.GetValue x
            v.FramebufferSignature

        override x.Update(t) =
            let ni = updateInner t x
            ni.Update(x, t)

        override x.Run(t, fbo) =
            let ni = updateInner t x
            ni.Run(x, t, fbo)

        override x.Dispose() =
            input.RemoveOutput x
            match inner with
                | Some i -> 
                    i.Dispose()
                    inner <- None
                | _ -> ()

        override x.Runtime = input.GetValue(x).Runtime
            
    type private AListRenderTask(tasks : alist<IRenderTask>) as this =
        inherit AbstractRenderTask()
        let reader = tasks.GetReader()
        do reader.AddOutput this

        let mutable signature : Option<IFramebufferSignature> = None
        let mutable runtime = None
        let tasks = ReferenceCountingSet()

        let add (t : IRenderTask) =
            if tasks.Add t then
                match t.Runtime with
                    | Some r -> runtime <- Some r
                    | None -> ()

                let innerSig = t.FramebufferSignature
                
                match signature, innerSig with
                    | Some s, Some i -> 
                        if not (s.IsAssignableFrom i) then
                            failwithf "cannot compose RenderTasks with different FramebufferSignatures: %A vs. %A" signature innerSig
                    | _-> signature <- innerSig


        let remove (t : IRenderTask) =
            if tasks.Remove t then
                t.Dispose()

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

        override x.Use (f : unit -> 'a) =
            lock x (fun () ->
                processDeltas()
                let l = reader.Content.All |> Seq.toList
                
                let rec run (l : list<ISortKey * IRenderTask>) =
                    match l with
                        | [] -> f()
                        | (_,h) :: rest -> h.Use (fun () -> run rest)

                run l
            )
        override x.FramebufferSignature =
            lock this (fun () -> processDeltas())
            signature

        override x.Update(token) =
            processDeltas ()
            for (_,t) in reader.Content.All do
                t.Update(x, token)


        override x.Run(token, fbo) =
            processDeltas()

            // TODO: order may be invalid
            for (_,t) in reader.Content.All do
                t.Run(x, token, fbo)


        override x.Dispose() =
            reader.RemoveOutput this
            reader.Dispose()

            for i in tasks do
                i.Dispose()
            tasks.Clear()
                
        override x.Runtime =
            lock this (fun () -> processDeltas())
            runtime

    type private CustomRenderTask(f : afun<IRenderTask * RenderToken * OutputDescription, unit>) as this =
        inherit AbstractRenderTask()

        override x.FramebufferSignature = None
        override x.Run(t, fbo) = f.Evaluate (x,(x :> IRenderTask,t,fbo))
        override x.Dispose() = f.RemoveOutput this 
        override x.Update(t) = ()
        override x.Runtime = None
        override x.Use f = lock x f

    type private FinalizerRenderTask(inner : IRenderTask) =
        inherit AbstractRenderTask()
        let mutable inner = inner

        member private x.Dispose(disposing : bool) =
            let old = Interlocked.Exchange(&inner, EmptyRenderTask.Instance)
            old.Dispose()
            if disposing then GC.SuppressFinalize(x)

        override x.Finalize() =
            try x.Dispose false
            with _ -> ()

        
        override x.Use f = 
            lock x (fun () ->
                inner.Use f
            )

        override x.Dispose() = x.Dispose true
        override x.Run(t, fbo) = inner.Run(x, t, fbo)
        override x.Update(t) = inner.Update(x, t)
        override x.FramebufferSignature = inner.FramebufferSignature
        override x.Runtime = inner.Runtime

    type private BeforeAfterRenderTask(before : Option<unit -> unit>, after : Option<unit -> unit>, inner : IRenderTask) =
        inherit AbstractRenderTask()

        member x.Before = before
        member x.After = after
        member x.Inner = inner

        override x.Use f =
            lock x (fun () ->
                inner.Use f
            )

        override x.FramebufferSignature = inner.FramebufferSignature
        override x.Update(t) = inner.Update(x,t)
        override x.Run(t, fbo) =
            match before with
                | Some before -> before()
                | None -> ()

            let res = inner.Run(x, t, fbo)

            match after with
                | Some after -> after()
                | None -> ()

            res

        override x.Dispose() = inner.Dispose()
        override x.Runtime = inner.Runtime


    let empty = EmptyRenderTask.Instance

    let ofAFun (f : afun<IRenderTask * RenderToken * OutputDescription, unit>) =
        new CustomRenderTask(f) :> IRenderTask

    let custom (f : IRenderTask * RenderToken * OutputDescription -> unit) =
        new CustomRenderTask(AFun.create f) :> IRenderTask

    let before (f : unit -> unit) (t : IRenderTask) =
        match t with
            | :? BeforeAfterRenderTask as t ->
                let before =
                    match t.Before with
                        | None -> f
                        | Some old -> f >> old
                new BeforeAfterRenderTask(Some before, t.After, t.Inner) :> IRenderTask
            | _ ->
                new BeforeAfterRenderTask(Some f, None, t) :> IRenderTask

    let after (f : unit -> unit) (t : IRenderTask) =
        match t with
            | :? BeforeAfterRenderTask as t ->
                let after =
                    match t.After with
                        | None -> f
                        | Some old -> old >> f
                new BeforeAfterRenderTask(t.Before, Some after, t.Inner) :> IRenderTask
            | _ ->
                new BeforeAfterRenderTask(None, Some f, t) :> IRenderTask

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

    let withFinalize (t : IRenderTask) =
        match t with
            | :? FinalizerRenderTask -> t
            | _ -> new FinalizerRenderTask(t) :> IRenderTask


    let renderTo (target : IMod<IFramebuffer>) (task : IRenderTask) : IMod<IFramebuffer> =
        task.RenderTo target

    let getResult (sem : Symbol) (t : IMod<IFramebuffer>) =
        t.GetOutputTexture sem

    let renderSemantics (sem : Set<Symbol>) (size : IMod<V2i>) (task : IRenderTask) =
        let runtime = task.Runtime.Value
        let signature = task.FramebufferSignature.Value

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

    let log fmt =
        Printf.kprintf (fun str -> 
            let task = 
                custom (fun (self, token, out) -> 
                    Log.line "%s" str
                )

            task
        ) fmt

[<AutoOpen>]
module ``RenderTask Builder`` =
    type private Result = list<alist<IRenderTask>>

    type RenderTaskBuilder() =
        member x.Bind(m : IMod<'a>, f : 'a -> Result) : Result =
            [alist.Bind(m, f >> AList.concat')]

        member x.For(s : alist<'a>, f : 'a -> Result): Result =
            [alist.For(s,f >> AList.concat')]

        member x.Bind(f : unit -> unit, c : unit -> Result) : Result =
            let task = 
                RenderTask.custom (fun (self, token, out) -> 
                    f()
                )
            (AList.single task)::c()

        member x.Return(u : unit) : Result =
            []

        member x.Bind(t : IRenderTask, c : unit -> Result) = 
            alist.Yield(t)::c()

        member x.Bind(t : list<IRenderTask>, c : unit -> Result) = 
            (AList.ofList t)::c()

        member x.Bind(l : alist<IRenderTask>, c : unit -> Result) =
            alist.YieldFrom(l)::c()

        member x.Bind(m : IMod<IRenderTask>, c : unit -> Result) =
            let head = m |> RenderTask.ofMod |> alist.Yield
            head::c()

        member x.Combine(l : Result, r : Result) =
            l @ r

        member x.Delay(f : unit -> Result) = 
            f()

        member x.Zero() =
            []

        member x.Run(l : Result) =
            let l = AList.concat' l
            RenderTask.ofAList l


    let rendertask = RenderTaskBuilder()
//
//    let test (renderActive : IMod<bool>) (clear : IMod<IRenderTask>) (render : alist<IRenderTask>) =
//        rendertask {
//            do! RenderTask.log "before clear: %d" 190
//            do! clear
//            do! RenderTask.log "after clear"
//
//            let! active = renderActive
//            if active then
//                do! render
//                do! RenderTask.log "rendered"
//        }