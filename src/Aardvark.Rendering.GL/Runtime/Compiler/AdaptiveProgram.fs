namespace Aardvark.Rendering.GL.Compiler

open System
open System.Collections.Generic

open Aardvark.Base.Incremental
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.GL

type ChangeSet(addInput : IAdaptiveObject -> unit, removeInput : IAdaptiveObject -> unit) =
    let l = obj()
    let all = HashSet<IMod<unit>>()
    let set = HashSet<IMod<unit>>()
    let callbacks = Dictionary<IMod<unit>, (unit -> unit)>()

    let dirty (m : IMod<unit>) () =
        lock l (fun () -> 
            callbacks.Remove m |> ignore
            set.Add m |> ignore
        )

    member x.Listen(m : IMod<unit>) =
        if not m.IsConstant then
            lock m (fun () -> 
                lock l (fun () ->
                    if all.Add m then addInput m

                    if m.OutOfDate then
                        set.Add m |> ignore
                    else
                        let cb = dirty m
                        callbacks.[m] <- cb
                        m.MarkingCallbacks.Add cb |> ignore
                )
            )
        else
            m |> Mod.force

    member x.Unlisten (m : IMod<unit>) =
        if not m.IsConstant then
            lock m (fun () ->
                lock l (fun () ->
                    if all.Remove m then removeInput m

                    set.Remove m |> ignore
                    match callbacks.TryGetValue m with
                        | (true, cb) ->
                            callbacks.Remove m |> ignore
                            m.MarkingCallbacks.Remove cb |> ignore
                        | _ ->
                            ()
                )
            )

    member x.Update() =
        let dirtySet = 
            lock l (fun () ->
                let dirty = set |> Seq.toArray
                set.Clear()
                dirty
            )

        for d in dirtySet do
            d |> Mod.force
            let cb = dirty d
            callbacks.[d] <- cb
            d.MarkingCallbacks.Add cb |> ignore

type ResourceSet(addInput : IAdaptiveObject -> unit, removeInput : IAdaptiveObject -> unit) =
    let l = obj()
    let all = ReferenceCountingSet<IChangeableResource>()
    let set = HashSet<IChangeableResource>()
    let callbacks = Dictionary<IChangeableResource, (unit -> unit)>()

    let dirty (m : IChangeableResource) () =
        lock l (fun () -> 
            callbacks.Remove m |> ignore
            set.Add m |> ignore
        )

    member x.Listen(m : IChangeableResource) =
        lock m (fun () -> 
            lock l (fun () ->
                if all.Add m then
                    addInput m
                    if m.OutOfDate then
                        set.Add m |> ignore
                    else
                        let cb = dirty m
                        callbacks.[m] <- cb
                        m.MarkingCallbacks.Add cb |> ignore
            )
        )

    member x.Unlisten (m : IChangeableResource) =
        lock m (fun () ->
            lock l (fun () ->
                if all.Remove m then
                    removeInput m
                    set.Remove m |> ignore
                    match callbacks.TryGetValue m with
                        | (true, cb) ->
                            callbacks.Remove m |> ignore
                            m.MarkingCallbacks.Remove cb |> ignore
                        | _ ->
                            ()
            )
        )

    member x.Update() =
        let dirtyResoruces = 
            lock l (fun () ->
                let dirty = set |> Seq.toArray
                set.Clear()
                dirty
            )

        for d in dirtyResoruces do
            d.UpdateCPU()
            d.UpdateGPU()

            let cb = dirty d
            callbacks.[d] <- cb
            d.MarkingCallbacks.Add cb |> ignore

type IFragmentHandler<'f when 'f :> IDynamicFragment<'f>> =
    inherit IDisposable
    abstract member CreateProlog : unit -> 'f
    abstract member CreateEpilog : unit -> 'f
    abstract member Create : seq<Instruction> -> 'f
    abstract member Delete : 'f -> unit
    abstract member Compile : unit -> ('f -> unit)

type CompileContext<'f when 'f :> IDynamicFragment<'f>> =
    {
        handler : IFragmentHandler<'f>
        manager : ResourceManager
        currentContext : IMod<ContextHandle>
        resourceSet : ResourceSet
    }

[<AllowNullLiteral>]
type RenderJobFragment<'f when 'f :> IDynamicFragment<'f> and 'f : null>
    private (precompiled : Option<'f>, rj : RenderJob, ctx : CompileContext<'f>) =

    let mutable next : RenderJobFragment<'f> = null
    let mutable prev : RenderJobFragment<'f> = null
    let mutable currentProgram : Option<AdaptiveCode> = None
    let mutable currentChanger = Mod.constant ()
    let mutable frag : 'f = match precompiled with | Some p -> p | None -> null
    let mutable lastPrev = None

    let recompile() =
        let prevRj =
            match prev with
                | null -> RenderJob.Empty
                | _ -> prev.RenderJob

        match lastPrev with
            | Some p when p = prev -> ()
            | _ ->
                lastPrev <- Some prev

                match frag with
                    | null -> frag <- ctx.handler.Create []
                    | _ -> frag.Clear()

                let prog = DeltaCompiler.compileDelta ctx.manager ctx.currentContext prevRj rj
                let changer = AdaptiveCode.writeTo prog frag
            
                // remove old resources/changers
                match currentProgram with
                    | Some old ->
                        for r in old.Resources do
                            r.Dispose()
                            ctx.resourceSet.Unlisten r
                        currentProgram <- None
                    | None ->
                        ()

                // link the fragments
                let pf = prev.Fragment
                pf.Next <- frag
                frag.Prev <- pf
                
                match next.FragmentOption with
                    | Some nf ->
                        nf.Prev <- frag
                        frag.Next <- nf
                    | None ->
                        ()


                // listen to changes
                for r in prog.Resources do
                    ctx.resourceSet.Listen r

                // store everything
                currentChanger <- changer
                currentProgram <- Some prog

    let changer = 
        let self = ref Unchecked.defaultof<_>
        self :=
            Mod.custom (fun () ->
                if prev <> null then
                    currentChanger.RemoveOutput !self
                    recompile()
                    currentChanger |> Mod.force
                    currentChanger.AddOutput !self
            )
        !self

    member x.Dispose() =
        match frag with
            | null -> ()
            | _ -> 
                match frag.Next with
                    | null -> ()
                    | n ->  n.Prev <- frag.Prev

                match frag.Prev with
                    | null -> ()
                    | p ->  p.Next <- frag.Next

                ctx.handler.Delete frag
                frag <- null

        match currentProgram with
            | Some prog ->
                for r in prog.Resources do
                    ctx.resourceSet.Unlisten r
                    r.Dispose()

                currentProgram <- None
            | None ->
                ()


        currentChanger.RemoveOutput changer
        currentChanger <- Mod.constant ()
        lastPrev <- None
        prev <- null
        next <- null

    member x.Changer = 
        match precompiled with
            | None -> changer
            | _ -> currentChanger

    member x.RenderJob = rj

    member x.Fragment : 'f = 
        match precompiled with
            | Some p -> p
            | None ->
                Mod.force changer
                frag

    member private x.FragmentOption : Option<'f> = 
        match precompiled with
            | Some p -> Some p
            | None ->
                match frag with
                    | null -> None
                    | _ -> Some frag

    member x.Next
        with get() = next
        and set (n : RenderJobFragment<'f>) = 
            match frag with
                | null -> ()
                | _ ->
                    match n.FragmentOption with
                        | Some n -> 
                            frag.Next <- n
                            n.Prev <- frag
                        | None -> ()
            next <- n

    member x.Prev
        with get() = prev
        and set (p : RenderJobFragment<'f>) = 
            match frag with
                | null -> ()
                | _ ->
                    match p.FragmentOption with
                        | Some p -> 
                            frag.Prev <- p
                            p.Next <- frag
                        | None -> ()
            prev <- p
            transact (fun () -> changer.MarkOutdated())

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    new(rj : RenderJob, ctx : CompileContext<'f>) = new RenderJobFragment<'f>(None, rj, ctx)
    new(precompiled : 'f, ctx : CompileContext<'f>) = new RenderJobFragment<'f>(Some precompiled, RenderJob.Empty, ctx)

module RenderJobSorting =

    let private emptyMod = Mod.constant () :> IMod
    let private projections = 
        [| fun (r : RenderJob) -> r.Surface :> IMod

           fun (r : RenderJob) -> 
               match r.Uniforms.TryGetUniform (r.AttributeScope, DefaultSemantic.DiffuseColorTexture) with
                   | Some t -> t
                   | _ -> emptyMod

           fun (r : RenderJob) -> if r.Indices <> null then r.Indices :> IMod else emptyMod 
        |]

    let project (rj : RenderJob) =
        projections |> Array.map (fun f -> f rj) |> Array.toList

type RedundancyRemovalProgram<'f when 'f :> IDynamicFragment<'f> and 'f : null>
    (newHandler : unit -> IFragmentHandler<'f>, manager : ResourceManager, addInput : IAdaptiveObject -> unit, removeInput : IAdaptiveObject -> unit) =
    
    let currentContext = Mod.init (match ContextHandle.Current with | Some ctx -> ctx | None -> null)
    let handler = newHandler()
    let changeSet = ChangeSet(addInput, removeInput)
    let resourceSet = ResourceSet(addInput, removeInput)


    let ctx = { handler = handler; manager = manager; currentContext = currentContext; resourceSet = resourceSet }

    let mutable currentId = 0
    let idCache = Cache(Ag.emptyScope, fun m -> System.Threading.Interlocked.Increment &currentId)

    let sortedFragments = SortedDictionaryExt<list<int>, RenderJobFragment<'f>>(compare)
    let fragments = Dict<RenderJob, RenderJobFragment<'f>>()

    let mutable prolog = new RenderJobFragment<'f>(handler.CreateProlog(), ctx)
    let mutable epilog = new RenderJobFragment<'f>(handler.CreateEpilog(), ctx)
    let mutable run = handler.Compile ()


    member x.Dispose() =
        run <- fun _ -> failwith "cannot run disposed program"

        for (KeyValue(_,f)) in fragments do
            changeSet.Unlisten f.Changer
            f.Dispose()

        fragments.Clear()
        sortedFragments.Clear()
        handler.Dispose()
        idCache.Clear(ignore)

        handler.Delete prolog.Fragment
        handler.Delete epilog.Fragment
        prolog <- null
        epilog <- null

    member x.Add (rj : RenderJob) =

        let key = (rj |> RenderJobSorting.project |> List.map idCache.Invoke) @ [rj.Id]

        // create a new RenderJobFragment and link it
        let fragment = 
            sortedFragments |> SortedDictionary.setWithNeighbours key (fun l s r -> 
                match s with
                    | Some f ->
                        failwithf "duplicated renderjob: %A" f.RenderJob
                    | None ->
                        let l = match l with | Some (_,l) -> l | None -> prolog
                        let r = match r with | Some (_,r) -> r | None -> epilog

                        let f = new RenderJobFragment<'f>(rj, ctx)
                        f.Prev <- l
                        l.Next <- f

                        f.Next <- r
                        r.Prev <- f

                        f
            ) 

        fragments.[rj] <- fragment
        
        // listen to changes
        changeSet.Listen fragment.Changer

    member x.Remove (rj : RenderJob) =
        match fragments.TryRemove rj with
            | (true, f) ->
                let key = (rj |> RenderJobSorting.project |> List.map idCache.Revoke) @ [rj.Id]

                sortedFragments |> SortedDictionary.remove key |> ignore

                // detach the fragment
                f.Prev.Next <- f.Next
                f.Next.Prev <- f.Prev
                
                // no longer listen for changes
                changeSet.Unlisten f.Changer

                // finally dispose the fragment
                f.Dispose()

            | _ ->
                failwithf "cannot remove unknown renderjob: %A" rj

    member x.Run(fbo : Framebuffer, ctx : ContextHandle) =
        // change the current context if necessary
        if ctx <> currentContext.UnsafeCache then
            transact (fun () -> Mod.change currentContext ctx)

        // update resources and instructions
        resourceSet.Update()
        changeSet.Update()

        // run everything
        run prolog.Fragment

        // TODO: real statistics
        FrameStatistics.Zero

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface IProgram with
        member x.Add rj = x.Add rj
        member x.Remove rj = x.Remove rj
        member x.Run (fbo, ctx) = x.Run(fbo, ctx)
        member x.Update rj = failwith "not implemented"



module FragmentHandlers =
    let native() =
        let manager = new MemoryManager()

        let prolog() =
            let f = new Fragment<unit>(manager, 0)
            f.Append (Assembler.functionProlog 6) |> ignore
            NativeDynamicFragment(f)

        let epilog() =
            let f = new Fragment<unit>(manager, 0)
            f.Append (Assembler.functionEpilog 6) |> ignore
            NativeDynamicFragment(f)

        let create (s : seq<Instruction>) =
            if not (Seq.isEmpty s) then
                failwith "cannot create non-empty fragment"

            let f = new Fragment<unit>(manager, 0)
            NativeDynamicFragment(f)

        { new IFragmentHandler<NativeDynamicFragment<unit>> with
            member x.Dispose() = manager.Dispose()
            member x.CreateProlog() = prolog()
            member x.CreateEpilog() = epilog()
            member x.Create s = create s
            member x.Delete f = f.Fragment.Dispose()
            member x.Compile() =
                let entryPtr = ref 0n
                let run = ref (fun () -> ())
                fun (f : NativeDynamicFragment<unit>) ->
                    let prolog = f.Fragment
                    if prolog.RealPointer <> !entryPtr then
                        entryPtr := prolog.RealPointer
                        run := UnmanagedFunctions.wrap !entryPtr
                    !run ()
        }

    let managed() =
        { new IFragmentHandler<ManagedDynamicFragment> with
            member x.Dispose() = ()
            member x.CreateProlog() = ManagedDynamicFragment()
            member x.CreateEpilog() = ManagedDynamicFragment()
            member x.Create s = ManagedDynamicFragment()
            member x.Delete f = f.Clear()
            member x.Compile() =
                fun (f : ManagedDynamicFragment) -> f.RunAll ()
        }

    let glvm() =
        { new IFragmentHandler<SwitchFragment> with
            member x.Dispose() = ()
            member x.CreateProlog() = new SwitchFragment()
            member x.CreateEpilog() = new SwitchFragment()
            member x.Create s = new SwitchFragment()
            member x.Delete f = f.Dispose()
            member x.Compile() =
                fun (f : SwitchFragment) -> f.RunAll ()
        }


