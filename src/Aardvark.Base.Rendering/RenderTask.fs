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
        member x.GetValue(c : IAdaptiveObject, t : RenderToken) =
            match x with
                | :? IOutputMod<'a> as x -> x.GetValue(c, t)
                | _ -> x.GetValue(c)

    type AdaptiveTexture(runtime : IRuntime, format : TextureFormat, samples : int, size : IMod<V2i>) =
        inherit AbstractOutputMod<ITexture>()

        let mutable handle : Option<IBackendTexture> = None

        override x.Create() = ()
        override x.Destroy() =
            match handle with
                | Some h ->
                    runtime.DeleteTexture(h)
                    handle <- None
                | None ->
                    ()

        override x.Compute(t : RenderToken) =
            let size = size.GetValue(x)

            match handle with
                | Some h when h.Size.XY = size -> 
                    h :> ITexture

                | Some h -> 
                    t.ReplacedResource(ResourceKind.Texture)
                    runtime.DeleteTexture(h)
                    let tex = runtime.CreateTexture(size, format, 1, samples, 1)
                    handle <- Some tex
                    tex :> ITexture

                | None ->
                    t.CreatedResource(ResourceKind.Texture)
                    let tex = runtime.CreateTexture(size, format, 1, samples, 1)
                    handle <- Some tex
                    tex :> ITexture
         
    type AdapiveCubeTexture(runtime : IRuntime, format : TextureFormat, samples : int, size : IMod<V2i>) =
        inherit AbstractOutputMod<ITexture>()

        let mutable handle : Option<IBackendTexture> = None

        override x.Create() = ()
        override x.Destroy() =
            match handle with
                | Some h ->
                    runtime.DeleteTexture(h)
                    handle <- None
                | None ->
                    ()

        override x.Compute(t : RenderToken) =
            let size = size.GetValue(x)

            match handle with
                | Some h when h.Size.XY = size -> 
                    h :> ITexture

                | Some h -> 
                    t.ReplacedResource(ResourceKind.Texture)
                    runtime.DeleteTexture(h)
                    let tex = runtime.CreateTextureCube(size, format, 1, samples)
                    handle <- Some tex
                    tex :> ITexture

                | None ->
                    t.CreatedResource(ResourceKind.Texture)
                    let tex = runtime.CreateTextureCube(size, format, 1, samples)
                    handle <- Some tex
                    tex :> ITexture

    type AdaptiveRenderbuffer(runtime : IRuntime, format : RenderbufferFormat, samples : int, size : IMod<V2i>) =  
        inherit AbstractOutputMod<IRenderbuffer>()

        let mutable handle : Option<IRenderbuffer> = None

        override x.Create() = ()
        override x.Destroy() =
            match handle with
                | Some h ->
                    runtime.DeleteRenderbuffer(h)
                    handle <- None
                | None ->
                    ()

        override x.Compute(t : RenderToken) =
            let size = size.GetValue(x)

            match handle with
                | Some h when h.Size = size -> 
                    h

                | Some h -> 
                    t.ReplacedResource(ResourceKind.Renderbuffer)
                    runtime.DeleteRenderbuffer(h)
                    let tex = runtime.CreateRenderbuffer(size, format, samples)
                    handle <- Some tex
                    tex

                | None ->
                    t.CreatedResource(ResourceKind.Renderbuffer)
                    let tex = runtime.CreateRenderbuffer(size, format, samples)
                    handle <- Some tex
                    tex
    
    [<AbstractClass>]
    type AbstractAdaptiveFramebufferOutput(resource : IOutputMod) =
        inherit AbstractOutputMod<IFramebufferOutput>()

        override x.Create() = resource.Acquire()
        override x.Destroy() = resource.Release()
    
    type AdaptiveTextureAttachment(texture : IOutputMod<ITexture>, slice : int) =
        inherit AbstractAdaptiveFramebufferOutput(texture)
        override x.Compute(t : RenderToken) =
            let tex = texture.GetValue(x, t)
            { texture = unbox tex; slice = slice; level = 0 } :> IFramebufferOutput

    type AdaptiveRenderbufferAttachment(renderbuffer : IOutputMod<IRenderbuffer>) =
        inherit AbstractAdaptiveFramebufferOutput(renderbuffer)
        override x.Compute(t : RenderToken) =
            let rb = renderbuffer.GetValue(x, t)
            rb :> IFramebufferOutput

    type IRuntime with
        member x.CreateTexture(format : TextureFormat, samples : int, size : IMod<V2i>) =
            AdaptiveTexture(x, format, samples, size) :> IOutputMod<ITexture>

        member x.CreateTextureCube(format : TextureFormat, samples : int, size : IMod<V2i>) =
            AdapiveCubeTexture(x, format, samples, size) :> IOutputMod<ITexture>

        member x.CreateRenderbuffer(format : RenderbufferFormat, samples : int, size : IMod<V2i>) =
            AdaptiveRenderbuffer(x, format, samples, size) :> IOutputMod<IRenderbuffer>

        member x.CreateTextureAttachment(texture : IOutputMod<ITexture>, slice : int) =
            AdaptiveTextureAttachment(texture, slice) :> IOutputMod<_>

        member x.CreateRenderbufferAttachment(renderbuffer : IOutputMod<IRenderbuffer>) =
            AdaptiveRenderbufferAttachment(renderbuffer) :> IOutputMod<_>



    type AdaptiveFramebuffer(runtime : IRuntime, signature : IFramebufferSignature, textures : Set<Symbol>, size : IMod<V2i>) =
        inherit AbstractOutputMod<IFramebuffer>()

        let createAttachment (sem : Symbol) (att : AttachmentSignature) =
            let isTexture = Set.contains sem textures
            if isTexture then
                let tex = runtime.CreateTexture(unbox (int att.format), att.samples, size)
                runtime.CreateTextureAttachment(tex, 0)
            else
                let rb = runtime.CreateRenderbuffer(att.format, att.samples, size)
                runtime.CreateRenderbufferAttachment(rb)

        let attachments = SymDict.empty
        let mutable handle : Option<IFramebuffer> = None

        do 
            match signature.DepthAttachment with
                | Some d -> 
                    attachments.[DefaultSemantic.Depth] <- createAttachment DefaultSemantic.Depth d
                | None -> 
                    ()

            for (index, (sem, att)) in Map.toSeq signature.ColorAttachments do
                let a = createAttachment sem att
                attachments.[sem] <- a

        override x.Create() =
            for att in attachments.Values do att.Acquire()

        override x.Destroy() =
            for att in attachments.Values do att.Release()
            match handle with
                | Some h -> 
                    runtime.DeleteFramebuffer(h)
                    handle <- None
                | None -> ()
        override x.Compute(t : RenderToken) =
            let att = 
                attachments
                    |> SymDict.toMap 
                    |> Map.map (fun sem att -> att.GetValue(x, t))

            match handle with
                | Some h -> 
                    runtime.DeleteFramebuffer(h)
                    t.ReplacedResource(ResourceKind.Framebuffer)
                | None ->
                    t.CreatedResource(ResourceKind.Framebuffer)

            let fbo = runtime.CreateFramebuffer(signature, att)
            handle <- Some fbo
            fbo

    type AdaptiveFramebufferCube(runtime : IRuntime, signature : IFramebufferSignature, textures : Set<Symbol>, size : IMod<V2i>) =
        inherit AbstractOutputMod<IFramebuffer[]>()

        let store = SymDict.empty

        let createAttachment (sem : Symbol) (face : CubeSide) (att : AttachmentSignature) =
            let isTexture = Set.contains sem textures
            if isTexture then
                
                let tex = 
                    store.GetOrCreate(sem, fun sem ->
                        runtime.CreateTextureCube(unbox (int att.format), att.samples, size) :> IOutputMod
                    ) |> unbox<IOutputMod<ITexture>>

                runtime.CreateTextureAttachment(tex, int face)
            else
                let rb = 
                    store.GetOrCreate(sem, fun sem ->
                        runtime.CreateRenderbuffer(att.format, att.samples, size) :> IOutputMod
                    ) |> unbox<IOutputMod<IRenderbuffer>>

                runtime.CreateRenderbufferAttachment(rb)

        let mutable handle : Option<IFramebuffer>[] = Array.zeroCreate 6

        let attachments =
            Array.init 6 (fun face ->
                let face = unbox<CubeSide> face
                let attachments = SymDict.empty
                match signature.DepthAttachment with
                    | Some d -> 
                        attachments.[DefaultSemantic.Depth] <- createAttachment DefaultSemantic.Depth face d
                    | None -> 
                        ()

                for (index, (sem, att)) in Map.toSeq signature.ColorAttachments do
                    let a = createAttachment sem face att
                    attachments.[sem] <- a

                attachments
            )

        override x.Create() =
            for face in 0 .. 5 do
                for att in attachments.[face].Values do att.Acquire()

        override x.Destroy() =
            for face in 0 .. 5 do
                for att in attachments.[face].Values do att.Release()
                match handle.[face] with
                    | Some h -> 
                        runtime.DeleteFramebuffer(h)
                        handle.[face] <- None
                    | None -> ()

        override x.Compute(t : RenderToken) =
            attachments |> Array.mapi (fun i attachments ->
                let att = 
                    attachments
                        |> SymDict.toMap 
                        |> Map.map (fun sem att -> att.GetValue(x, t))


                match handle.[i] with
                    | Some h -> 
                        runtime.DeleteFramebuffer(h)
                        t.ReplacedResource(ResourceKind.Framebuffer)
                    | None ->
                        t.CreatedResource(ResourceKind.Framebuffer)

                let fbo = runtime.CreateFramebuffer(signature, att)
                handle.[i] <- Some fbo
                fbo
            )

    type AdaptiveRenderingResult(task : IRenderTask, target : IOutputMod<IFramebuffer>) =
        inherit AbstractOutputMod<IFramebuffer>()

        override x.Compute(t : RenderToken) =
            let fbo = target.GetValue(x, t)
            task.Run(x, t, OutputDescription.ofFramebuffer fbo)
            fbo

        override x.Inputs =
            seq {
                yield task :> _
                yield target :> _
            }

        override x.Create() =
            Log.line "result created"
            target.Acquire()

        override x.Destroy() =
            Log.line "result deleted"
            target.Release()

    type AdaptiveOutputTexture(semantic : Symbol, res : IOutputMod<IFramebuffer>) =
        inherit AbstractOutputMod<ITexture>()

        override x.Compute(t : RenderToken) =
            let res = res.GetValue(x, t)

            match Map.tryFind semantic res.Attachments with
                | Some (:? BackendTextureOutputView as t) ->
                    t.texture :> ITexture
                | _ ->
                    failwithf "could not get result for semantic %A as texture" semantic

        override x.Inputs =
            Seq.singleton (res :> _)

        override x.Create() =
            Log.line "texture created"
            res.Acquire()

        override x.Destroy() =
            Log.line "texture deleted"
            res.Release()
 

    



[<AbstractClass; Sealed; Extension>]
type RuntimeFramebufferExtensions private() =

    [<Extension>]
    static member CreateFramebuffer (this : IRuntime, signature : IFramebufferSignature, textures : Set<Symbol>, size : IMod<V2i>) : IOutputMod<IFramebuffer> =
        AdaptiveFramebuffer(this, signature, textures, size) :> IOutputMod<IFramebuffer>
    
    [<Extension>]
    static member CreateFramebufferCube (this : IRuntime, signature : IFramebufferSignature, textures : Set<Symbol>, size : IMod<V2i>) : IOutputMod<IFramebuffer[]> =
        AdaptiveFramebufferCube(this, signature, textures, size) :> IOutputMod<IFramebuffer[]>

    [<Extension>]
    static member CreateFramebuffer (this : IRuntime, signature : IFramebufferSignature, size : IMod<V2i>) : IOutputMod<IFramebuffer> =
        let sems =
            Set.ofList [
                yield! signature.ColorAttachments |> Map.toSeq |> Seq.map snd |> Seq.map fst
                if Option.isSome signature.DepthAttachment then yield DefaultSemantic.Depth
                if Option.isSome signature.StencilAttachment then yield DefaultSemantic.Stencil
            ]
        
        AdaptiveFramebuffer(this, signature, sems, size) :> IOutputMod<IFramebuffer>

    [<Extension>]
    static member RenderTo(this : IRenderTask, output : IOutputMod<IFramebuffer>) =
        AdaptiveRenderingResult(this, output) :> IOutputMod<_>

    [<Extension>]
    static member GetOutputTexture (this : IOutputMod<IFramebuffer>, semantic : Symbol) =
        AdaptiveOutputTexture(semantic, this) :> IOutputMod<_>

[<AbstractClass>]
type AbstractRenderTask() =
    inherit AdaptiveObject()

    let mutable frameId = 0UL

    let hooks =
        Dictionary.ofList [
            "ViewTrafo", DefaultingModTable<Trafo3d>() :> DefaultingModTable
            "ProjTrafo", DefaultingModTable<Trafo3d>() :> DefaultingModTable
        ]

    let useValues (d : Map<string, obj>) (f : unit -> 'a) =
        let toReset = List()
        for (name, value) in Map.toSeq d do
            match hooks.TryGetValue(name) with
                | (true, table) ->
                    table.Set(value)
                    toReset.Add table
                | _ ->
                    ()
        try
            f()
        finally
            for r in toReset do r.Reset()

    let hookProvider (provider : IUniformProvider) =
        { new IUniformProvider with
            member x.TryGetUniform(scope, name) =
                let res = provider.TryGetUniform(scope, name)
                match res with
                    | Some res ->
                        match hooks.TryGetValue(string name) with
                            | (true, h) -> h.Hook res |> Some
                            | _ -> Some res
                    | None ->
                        None

            member x.Dispose() = 
                provider.Dispose()
        }

    member x.HookRenderObject (ro : RenderObject) =
        { ro with Uniforms = hookProvider ro.Uniforms }
                



    abstract member FramebufferSignature : Option<IFramebufferSignature>
    abstract member Runtime : Option<IRuntime>
    abstract member Update : RenderToken -> unit
    abstract member Run : RenderToken * OutputDescription -> unit
    abstract member Dispose : unit -> unit
    abstract member Use : (unit -> 'a) -> 'a
    

    member x.FrameId = frameId
    member x.Run(caller : IAdaptiveObject, t : RenderToken, out : OutputDescription) =
        x.EvaluateAlways caller (fun () ->
            x.OutOfDate <- true
            useValues out.overrides (fun () ->
                x.Run(t, out)
                frameId <- frameId + 1UL
            )
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
            let ni = input.GetValue(x, t)

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


    let renderTo (target : IOutputMod<IFramebuffer>) (task : IRenderTask) : IOutputMod<IFramebuffer> =
        task.RenderTo target

    let getResult (sem : Symbol) (t : IOutputMod<IFramebuffer>) =
        t.GetOutputTexture sem

    let renderSemantics (sem : Set<Symbol>) (size : IMod<V2i>) (task : IRenderTask) =
        let runtime = task.Runtime.Value
        let signature = task.FramebufferSignature.Value

        let clear = runtime.CompileClear(signature, ~~C4f.Black, ~~1.0)
        let fbo = runtime.CreateFramebuffer(signature, sem, size)

        let res = new SequentialRenderTask([|clear; task|]) |> renderTo fbo
        sem |> Seq.map (fun k -> k, getResult k res) |> Map.ofSeq

    let renderToColor (size : IMod<V2i>) (task : IRenderTask) =
        task |> renderSemantics (Set.singleton DefaultSemantic.Colors) size |> Map.find DefaultSemantic.Colors

    let renderToDepth (size : IMod<V2i>) (task : IRenderTask) =
        task |> renderSemantics (Set.singleton DefaultSemantic.Depth) size |> Map.find DefaultSemantic.Depth

    let renderToDepthAndStencil (size : IMod<V2i>) (task : IRenderTask) =
        let map = task |> renderSemantics (Set.singleton DefaultSemantic.Depth) size
        (Map.find DefaultSemantic.Depth map, Map.find DefaultSemantic.Stencil map)

    let renderToColorAndDepth (size : IMod<V2i>) (task : IRenderTask) =
        let map = task |> renderSemantics (Set.ofList [DefaultSemantic.Depth; DefaultSemantic.Colors]) size
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