﻿namespace Aardvark.Rendering.GL

open FSharp.Data.Traceable

#nowarn "9"

open System
open System.Diagnostics
open System.Collections.Generic
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Assembler
open FSharp.Data.Adaptive
open OpenTK.Graphics.OpenGL4
open Microsoft.FSharp.NativeInterop
open Aardvark.Rendering.GL
open FShade

module RenderTasks =

    module private FramebufferSignature =

        let validateCompability (fbo : IFramebuffer) (signature : IFramebufferSignature) =
            if not <| signature.IsAssignableTo fbo then
                failwithf "[GL] render task signature is not compatible with the framebuffer signature\ntask signature:\n%A\n\nframebuffer signature:\n%A" signature.Layout fbo.Signature.Layout

    module private Framebuffer =

        let draw (signature : IFramebufferSignature) (fbo : Framebuffer) (viewport : Box2i) (f : unit -> 'T) =

            let oldVp = Array.create 4 0
            let mutable oldFbo = 0
            GL.GetInteger(GetPName.Viewport, oldVp)
            GL.GetInteger(GetPName.DrawFramebufferBinding, &oldFbo)

            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, fbo.Handle)
            GL.Check "could not bind framebuffer"

            GL.Viewport(viewport.Min.X, viewport.Min.Y, viewport.SizeX + 1, viewport.SizeY + 1)
            GL.Check "could not set viewport"

            try
                if fbo.Handle = 0 then
                    GL.DrawBuffer(DrawBufferMode.BackLeft)
                else
                    let drawBuffers = DrawBuffers.ofSignature signature
                    GL.DrawBuffers(drawBuffers.Length, drawBuffers);
                GL.Check "could not set draw buffers"

                f()

            finally
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, oldFbo)
                GL.Check "could reset framebuffer"

                GL.Viewport(oldVp.[0], oldVp.[1], oldVp.[2], oldVp.[3])
                GL.Check "could reset viewport"

    [<AbstractClass>]
    type AbstractOpenGlRenderTask(manager : ResourceManager, signature : IFramebufferSignature) as this =
        inherit AbstractRenderTask()

        let ctx = manager.Context
        let renderTaskLock = RenderTaskLock()
        let manager = new ResourceManager(manager, Some renderTaskLock)
        let structureChanged = AVal.custom ignore
        let runtimeStats = NativePtr.alloc 1
        let resources = new ResourceInputSet()

        let currentContext = AVal.init Unchecked.defaultof<ContextHandle>
        let contextHandle = NativePtr.alloc 1
        do NativePtr.write contextHandle 0n


        let scope =
            {
                resources = resources
                runtimeStats = runtimeStats
                currentContext = currentContext
                contextHandle = contextHandle
                drawBufferCount = signature.ColorAttachmentSlots
                usedTextureSlots = RefRef CountingHashSet.empty
                usedUniformBufferSlots = RefRef CountingHashSet.empty
                structuralChange = structureChanged
                task = this
                tags = Map.empty
            }

        let beforeRender = new Event<unit>()
        let afterRender = new Event<unit>()

        member x.Resources = resources

        member x.BeforeRender = beforeRender.Publish :> IObservable<_>
        member x.AfterRender = afterRender.Publish :> IObservable<_>

        member x.StructureChanged() =
            transact (fun () -> structureChanged.MarkOutdated())

        abstract member ProcessDeltas : AdaptiveToken * RenderToken -> unit
        abstract member UpdateResources : AdaptiveToken * RenderToken -> unit
        abstract member Perform : AdaptiveToken * RenderToken * Framebuffer * OutputDescription -> unit
        abstract member Update :  AdaptiveToken * RenderToken -> unit
        abstract member Release2 : unit -> unit

        member x.Context = ctx
        member x.Scope = scope
        member x.RenderTaskLock = renderTaskLock
        member x.ResourceManager = manager

        override x.Runtime = Some ctx.Runtime
        override x.FramebufferSignature = Some signature

        override x.Release() =
            currentContext.Outputs.Clear()
            x.Release2()
            contextHandle |> NativePtr.free
            runtimeStats |> NativePtr.free
            resources.Dispose()
            manager.Dispose()

        override x.PerformUpdate(token, renderToken) =
            use __ = ctx.ResourceLock
            use __ = GlobalResourceLock.lock()

            x.ProcessDeltas(token, renderToken)
            x.UpdateResources(token, renderToken)

            renderTaskLock.Run (fun () ->
                x.Update(token, renderToken)
            )

        override x.Perform(token : AdaptiveToken, renderToken : RenderToken, desc : OutputDescription) =
            use __ = ctx.ResourceLock
            use __ = GlobalResourceLock.lock()

            GL.Check "[RenderTask.Run] Entry"

            let fbo = desc.framebuffer // TODO: fix outputdesc
            signature |> FramebufferSignature.validateCompability fbo

            if currentContext.Value <> ContextHandle.Current.Value then
                let intCtx = ContextHandle.Current.Value.Handle |> unbox<OpenTK.Graphics.IGraphicsContextInternal>
                NativePtr.write contextHandle intCtx.Context.Handle
                transact (fun () -> currentContext.Value <- ContextHandle.Current.Value)

            let fbo =
                match fbo with
                    | :? Framebuffer as fbo -> fbo
                    | _ -> failwithf "unsupported framebuffer: %A" fbo

            x.ProcessDeltas(token, renderToken)
            x.UpdateResources(token, renderToken)

            Framebuffer.draw signature fbo desc.viewport (fun _ ->
                renderTaskLock.Run (fun () ->
                    beforeRender.Trigger()
                    NativePtr.write runtimeStats V2i.Zero

                    x.Perform(token, renderToken, fbo, desc)
                    GL.Check "[RenderTask.Run] Perform"

                    afterRender.Trigger()
                    let rt = NativePtr.read runtimeStats
                    renderToken.AddDrawCalls(rt.X, rt.Y)
                )

                GL.BindVertexArray 0
                GL.BindBuffer(BufferTarget.DrawIndirectBuffer, 0)
            )

    [<AbstractClass>]
    type AbstractSubTask() =
        static let nop = System.Lazy<unit>(id)

        let programUpdateWatch  = Stopwatch()
        let sortWatch           = Stopwatch()

        member x.ProgramUpdate (renderToken : RenderToken, f : unit -> 'a) =
            if renderToken.Statistics.IsNone then
                f()
            else
                programUpdateWatch.Restart()
                let res = f()
                programUpdateWatch.Stop()
                res

        member x.Sorting (renderToken : RenderToken, f : unit -> 'a) =
            if renderToken.Statistics.IsNone then
                f()
            else
                sortWatch.Restart()
                let res = f()
                sortWatch.Stop()
                res

        member x.Execution (t : RenderToken, f : unit -> 'a) =
            f()
                
        abstract member Update : AdaptiveToken * RenderToken -> unit
        abstract member Perform : AdaptiveToken * RenderToken -> unit
        abstract member Dispose : unit -> unit
        abstract member Add : PreparedCommand -> unit
        abstract member Remove : PreparedCommand -> unit

        member x.Run(token : AdaptiveToken, renderToken : RenderToken, output : OutputDescription) =

            x.Perform(token, renderToken)
            if renderToken.Statistics.IsNone then
                nop
            else
                lazy (
                    renderToken.AddSubTask(
                        MicroTime sortWatch.Elapsed,
                        MicroTime programUpdateWatch.Elapsed
                    )
                )

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    type NativeRenderProgram(cmp : IComparer<PreparedCommand>, scope : CompilerInfo, content : aset<PreparedCommand>, debug : bool) =
        inherit AdaptiveObject()
        //inherit NativeProgram<PreparedCommand, NativeStats>(
        //                ASet.sortWith (curry cmp.Compare) content, 
        //                (fun l r s -> 
        //                        let asm =  AssemblerCommandStream(s) :> ICommandStream
        //                        let stream = if debug then DebugCommandStream(asm) :> ICommandStream else asm
        //                        r.Compile(scope, stream, l)
        //                    ),
        //                NativeStats.Zero, (+), (-))
        let mutable reader = content.GetReader()
        let mutable state = 
            SortedSetExt<PreparedCommand * Aardvark.Assembler.Fragment<PreparedCommand>> {
                new IComparer<PreparedCommand * Aardvark.Assembler.Fragment<PreparedCommand>> with
                    member x.Compare((a,_), (b,_)) =
                        cmp.Compare(a, b)
            }

        let compile (l : option<PreparedCommand>) (self : PreparedCommand) (ass : IAssemblerStream) =
            let asm =  AssemblerCommandStream(ass) :> ICommandStream
            let stream = if debug then DebugCommandStream(asm) :> ICommandStream else asm
            self.Compile(scope, stream, l) |> ignore // TODO: stats

        let mutable program = new Aardvark.Assembler.FragmentProgram<PreparedCommand>(compile)

        member x.Update(token : AdaptiveToken) =
            x.EvaluateIfNeeded token () (fun token ->
                let ops = reader.GetChanges token
                for op in ops do
                    match op with
                    | Add(_, cmd) ->
                        let (struct(hasL, hasV, _), l, _, _) = state.FindNeighboursV((cmd, null))
                        if hasV then
                            Log.warn "[NativeRenderProgram] duplicate add of: %A" cmd
                        else
                            let l = if hasL then snd l else null
                            let self = program.InsertAfter(l, cmd)
                            state.Add((cmd, self)) |> ignore
                    | Rem(_, cmd) ->
                        let (hasValue, value) = state.FindValue((cmd, null))
                        if hasValue then
                            let _, f = value
                            f.Dispose()
                            state.Remove(cmd, null) |> ignore
                        else
                            Log.warn "[NativeRenderProgram] removal of unknown command: %A" cmd

                program.Update()
            )

        member x.Run() =
            program.Run()

        member x.Dispose() =
            if not (isNull state) then
                program.Dispose()
                state <- null
                reader <- Unchecked.defaultof<_>


    type StaticOrderSubTask(ctx : Context, scope : CompilerInfo, debug : bool) =
        inherit AbstractSubTask()
        let objects : cset<PreparedCommand> = cset [new EpilogCommand(ctx) :> PreparedCommand]

        let mutable hasProgram = false
        let mutable program : NativeRenderProgram = Unchecked.defaultof<_>


        let reinit (self : StaticOrderSubTask) =
            // if the config changed or we never compiled a program
            // we need to do something
            if not hasProgram then

                // if we have a program we'll dispose it now
                if hasProgram then program.Dispose()

                // use the config to create a comparer for IRenderObjects
                let comparer =
                    { new IComparer<PreparedCommand> with 
                        member x.Compare(l, r) =
                            match l, r with
                                | :? EpilogCommand, :? EpilogCommand -> compare l.Id r.Id
                                | :? EpilogCommand, _ -> 1
                                | _, :? EpilogCommand -> -1
                                | _ -> 
                                    match l.EntryState, r.EntryState with
                                        | None, None -> compare l.Id r.Id
                                        | None, Some _ -> -1
                                        | Some _, None -> 1
                                        | Some ls, Some rs ->
                                            let cmp = compare ls.pProgram.Id rs.pProgram.Id
                                            if cmp <> 0 then cmp
                                            else // efficient texture sorting requires that slots are ordered [Global, PerMaterial, PerInstance] -> currently alphabetic based on SamplerName !
                                                let mutable cmp = 0
                                                let mutable i = 0
                                                let texCnt = min ls.pTextureBindings.Length rs.pTextureBindings.Length
                                                while cmp = 0 && i < texCnt do
                                                    let struct (sltl, bndl) = ls.pTextureBindings.[i]
                                                    let struct (sltr, bndr) = ls.pTextureBindings.[i]
                                                    if (sltl <> sltr) then
                                                        cmp <- compare l.Id r.Id
                                                    else 
                                                        let leftTexId = match bndl with | ArrayBinding ab -> ab.Id; | SingleBinding (t, s) -> t.Id
                                                        let rigthTexId = match bndr with | ArrayBinding ab -> ab.Id; | SingleBinding (t, s) -> t.Id
                                                        cmp <- compare leftTexId rigthTexId
                                                    i <- i + 1
                                                if cmp <> 0 then cmp
                                                else compare l.Id r.Id
                    }

                // create the new program
                let newProgram = 
                    Log.line "using optimized native program"
                    new NativeRenderProgram(comparer, scope, objects, debug) 


                // finally we store the current config/ program and set hasProgram to true
                program <- newProgram
                hasProgram <- true

        override x.Update(token, renderToken) =
            reinit x

            //TODO
            let programStats = x.ProgramUpdate (renderToken, fun () -> program.Update AdaptiveToken.Top)
            ()

        override x.Perform(token, renderToken) =
            x.Update(token, renderToken) |> ignore
            x.Execution (renderToken, fun () -> program.Run())
            //let ic = program.Stats.InstructionCount
            //renderToken.AddInstructions(ic, 0) // don't know active
               

        override x.Dispose() =
            if hasProgram then
                hasProgram <- false
                program.Dispose()

                (objects :> aset<_>).History.Value.Outputs.Clear()

                objects.Clear()
        
        override x.Add(o) = 
            transact (fun () -> 
                scope.structuralChange.MarkOutdated()
                objects.Add o |> ignore
            )

        override x.Remove(o) = 
            transact (fun () -> 
                scope.structuralChange.MarkOutdated()
                objects.Remove o |> ignore
            )


    
    type NewRenderTask(man : ResourceManager, fboSignature : IFramebufferSignature, objects : aset<IRenderObject>, debug : bool) as this =
        inherit AbstractOpenGlRenderTask(man, fboSignature)

        let rec hook (r : IRenderObject) : IRenderObject =
            match r with
            | :? HookedRenderObject as o -> HookedRenderObject.map this.HookRenderObject o
            | :? RenderObject as o -> this.HookRenderObject o
            | :? MultiRenderObject as o -> MultiRenderObject(o.Children |> List.map hook)
            | _ -> r

        let mainCommand = Command.ofRenderObjects fboSignature this.ResourceManager debug (ASet.map hook objects)
        
        override x.Use(action : unit -> 'a) = action()

        override x.ProcessDeltas(token : AdaptiveToken, renderToken : RenderToken) =
            x.Resources.Update(token, renderToken)
            mainCommand.Update(token, x.Scope)
            
        override x.Update(token : AdaptiveToken, renderToken : RenderToken) =
            x.Resources.Update(token, renderToken)
            mainCommand.Update(token, x.Scope)
            
        override x.UpdateResources(token : AdaptiveToken, renderToken : RenderToken) =
            x.Resources.Update(token, renderToken)
            mainCommand.Update(token, x.Scope)

            
        override x.Release2() =
            mainCommand.Free(x.Scope)

        override x.Perform(token : AdaptiveToken, renderToken : RenderToken, fbo : Framebuffer, output : OutputDescription) =
            mainCommand.Update(token, x.Scope)
            mainCommand.Run()

            if RuntimeConfig.SyncUploadsAndFrames then
                GL.Sync()


    type RenderTask(man : ResourceManager, fboSignature : IFramebufferSignature, objects : aset<IRenderObject>, debug : bool) as this =
        inherit AbstractOpenGlRenderTask(man, fboSignature)
        
        let ctx = man.Context
        let deltaWatch = Stopwatch()
        let subTaskResults = List<Lazy<unit>>()     
        
        let add (self : RenderTask) (ro : PreparedCommand) = 
            let all = ro.Resources
            for r in all do self.Resources.Add(r)
            ro.AddCleanup(fun () -> for r in all do self.Resources.Remove r)
            ro
            
        let rec hook (r : IRenderObject) : IRenderObject =
            match r with
            | :? HookedRenderObject as o -> HookedRenderObject.map this.HookRenderObject o
            | :? RenderObject as o -> this.HookRenderObject o
            | :? MultiRenderObject as o -> MultiRenderObject(o.Children |> List.map hook)
            | _ -> r

        let preparedObjects = objects |> ASet.map (hook >> PreparedCommand.ofRenderObject fboSignature this.ResourceManager >> add this)
        let preparedObjectReader = preparedObjects.GetReader()

        let mutable subtasks = Map.empty

        let getSubTask (pass : RenderPass) : AbstractSubTask =
            match Map.tryFind pass subtasks with
                | Some task -> task
                | _ ->
                    let task = 
                        match pass.Order with
                            | RenderPassOrder.Arbitrary ->
                                new StaticOrderSubTask(ctx, this.Scope, debug) :> AbstractSubTask

                            | order ->
                                Log.warn "[GL] no sorting"
                                new StaticOrderSubTask(ctx, this.Scope, debug) :> AbstractSubTask //new CameraSortedSubTask(order, this) :> AbstractSubTask

                    subtasks <- Map.add pass task subtasks
                    task

        override x.ProcessDeltas(token, renderToken) =
            deltaWatch.Restart()
            
            let deltas = preparedObjectReader.GetChanges token
            
            if not (HashSetDelta.isEmpty deltas) then
                x.StructureChanged()
            
            let mutable added = 0
            let mutable removed = 0
            for d in deltas do 
                match d with
                    | Add(_,v) ->
                        let task = getSubTask v.Pass
                        added <- added + 1
                        task.Add v
            
                    | Rem(_,v) ->
                        let task = getSubTask v.Pass
                        removed <- removed + 1
                        task.Remove v   
                        v.Dispose()
                                    
            if added > 0 || removed > 0 then
                Log.line "[GL] RenderObjects: +%d/-%d (%dms)" added removed deltaWatch.ElapsedMilliseconds
            renderToken.RenderObjectDeltas(added, removed)


        override x.UpdateResources(token, renderToken) =
            x.Resources.Update(token, renderToken)


        override x.Perform(token : AdaptiveToken, renderToken : RenderToken, fbo : Framebuffer, output : OutputDescription) =
            subtasks |> Map.iter (fun _ t ->
                    let s = t.Run(token, renderToken, output)
                    subTaskResults.Add(s)
                )

            if RuntimeConfig.SyncUploadsAndFrames then
                GL.Sync()

        override x.Update(token, renderToken) = 
            subtasks |> Map.iter (fun _ t ->
                    t.Update(token, renderToken)
                )

        override x.Release2() =
            for ro in preparedObjectReader.State do
                ro.Dispose()

            subtasks |> Map.iter (fun _ t -> t.Dispose())
            subtasks <- Map.empty

        override x.Use (f : unit -> 'a) =
            lock x (fun () ->
                x.RenderTaskLock.Run (fun () ->
                    lock x.Resources (fun () ->
                        f()
                    )
                )
            )

    type ClearTask(runtime : IRuntime, ctx : Context, signature : IFramebufferSignature, values : aval<ClearValues>) =
        inherit AbstractRenderTask()

        override x.PerformUpdate(token, t) = ()
        override x.Perform(token : AdaptiveToken, renderToken : RenderToken, desc : OutputDescription) =
            let fbo = desc.framebuffer |> unbox<Framebuffer>
            signature |> FramebufferSignature.validateCompability fbo

            Operators.using ctx.ResourceLock (fun _ ->
                Framebuffer.draw signature fbo desc.viewport (fun _ ->

                    let values = values.GetValue token
                    let depthValue = values.Depth
                    let stencilValue = values.Stencil

                    // Clear color attachments
                    for KeyValue(i, att) in signature.ColorAttachments do
                        match values.[att.Name] with
                        | Some c ->
                            if att.Format.IsIntegerFormat then
                                GL.ClearBuffer(ClearBuffer.Color, i, c.Integer.ToArray())
                            else
                                GL.ClearBuffer(ClearBuffer.Color, i, c.Float.ToArray())
                            GL.Check "could not clear buffer"

                        | None ->
                            ()

                    // Clear depth-stencil if it necessary         
                    let mask =
                        let depthMask =
                            match signature.DepthStencilAttachment, depthValue with
                            | Some fmt, Some value when fmt.HasDepth ->
                                GL.ClearDepth(float value)
                                ClearBufferMask.DepthBufferBit

                            | _ ->
                                ClearBufferMask.None

                        let stencilMask =
                            match signature.DepthStencilAttachment, stencilValue with
                            | Some fmt, Some value when fmt.HasStencil ->
                                GL.ClearStencil(int value)
                                ClearBufferMask.StencilBufferBit

                            | _ ->
                                ClearBufferMask.None

                        depthMask ||| stencilMask

                    if mask <> ClearBufferMask.None then
                        GL.Clear(mask)
                        GL.Check "could not clear depth stencil"
                )
            )

        override x.Release() =
            values.Outputs.Remove x |> ignore

        override x.FramebufferSignature = signature |> Some
        override x.Runtime = runtime |> Some

        override x.Use f = lock x f


