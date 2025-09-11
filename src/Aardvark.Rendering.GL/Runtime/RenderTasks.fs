namespace Aardvark.Rendering.GL

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

module RenderTasks =

    module private FramebufferSignature =

        let validateCompability (fbo : IFramebuffer) (signature : IFramebufferSignature) =
            if not <| signature.IsAssignableTo fbo then
                failwithf "[GL] render task signature is not compatible with the framebuffer signature\ntask signature:\n%A\n\nframebuffer signature:\n%A" signature.Layout fbo.Signature.Layout

    module private Framebuffer =

        let draw (signature : IFramebufferSignature) (fbo : Framebuffer) (viewport : Box2i) (scissor : Box2i) (perform : unit -> 'T) =
            let oldViewport = NativePtr.stackalloc<int> 4
            let oldScissorBox = NativePtr.stackalloc<int> 4
            let mutable oldScissorTest = false
            let mutable oldFbo = 0

            GL.GetInteger(GetPName.Viewport, oldViewport)
            GL.GetBoolean(GetPName.ScissorTest, &oldScissorTest)
            if oldScissorTest then GL.GetInteger(GetPName.ScissorBox, oldScissorBox)
            GL.GetInteger(GetPName.DrawFramebufferBinding, &oldFbo)
            GL.Check "could not get currrent viewport, scissor, or framebuffer"

            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, fbo.Handle)
            GL.Check "could not bind framebuffer"

            GL.Viewport(viewport.Min.X, viewport.Min.Y, viewport.SizeX, viewport.SizeY)
            GL.Check "could not set viewport"

            GL.Enable(EnableCap.ScissorTest)
            GL.Scissor(scissor.Min.X, scissor.Min.Y, scissor.SizeX, scissor.SizeY)
            GL.Check "could not set scissor"

            try
                if fbo.Handle = 0 then
                    GL.DrawBuffer(DrawBufferMode.BackLeft)
                else
                    let drawBuffers = DrawBuffers.ofSignature signature
                    GL.DrawBuffers(drawBuffers.Length, drawBuffers);
                GL.Check "could not set draw buffers"

                perform()

            finally
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, oldFbo)
                GL.Check "could not reset framebuffer"

                GL.Viewport(oldViewport.[0], oldViewport.[1], oldViewport.[2], oldViewport.[3])
                GL.Check "could not reset viewport"

                if oldScissorTest then
                    GL.Scissor(oldScissorBox.[0], oldScissorBox.[1], oldScissorBox.[2], oldScissorBox.[3])
                    GL.Check "could not reset scissor"
                else
                    GL.Disable(EnableCap.ScissorTest)
                    GL.Check "could not disable scissor"

    [<AbstractClass>]
    type AbstractOpenGlRenderTask(manager : ResourceManager, signature : IFramebufferSignature) as this =
        inherit AbstractRenderTask()

        let ctx = manager.Context
        let renderTaskLock = RenderTaskLock()
        let manager = new ResourceManager(manager, Some renderTaskLock)
        let structureChanged = AVal.custom ignore
        let runtimeStats = NativePtr.alloc 1
        let resources = new ResourceInputSet()

        let contextHandle = NativePtr.alloc<nativeint> 1
        do NativePtr.write contextHandle 0n

        let writeCurrentContextHandle() =
            let ctx = ContextHandle.Current.Value.Handle |> unbox<OpenTK.Graphics.IGraphicsContextInternal>
            NativePtr.write contextHandle ctx.Context.Handle

        let scope =
            {
                resources = resources
                runtimeStats = runtimeStats
                contextHandle = contextHandle
                drawBufferCount = signature.ColorAttachmentSlots
                usedTextureSlots = RefRef CountingHashSet.empty
                usedUniformBufferSlots = RefRef CountingHashSet.empty
                structuralChange = structureChanged
                task = this
                tags = Map.empty
            }

        let beforeRender = Event<unit>()
        let afterRender = Event<unit>()

        member x.Resources = resources

        member x.BeforeRender = beforeRender.Publish :> IObservable<_>
        member x.AfterRender = afterRender.Publish :> IObservable<_>

        member x.StructureChanged() =
            transact (fun () -> structureChanged.MarkOutdated())

        abstract member ProcessDeltas : AdaptiveToken * RenderToken -> unit
        abstract member UpdateResources : AdaptiveToken * RenderToken -> unit
        abstract member PerformInner : AdaptiveToken * RenderToken * OutputDescription -> unit
        abstract member Update :  AdaptiveToken * RenderToken -> unit

        member x.Context = ctx
        member x.Scope = scope
        member x.RenderTaskLock = renderTaskLock
        member x.ResourceManager = manager

        override x.Runtime = Some ctx.Runtime
        override x.FramebufferSignature = Some signature

        override x.Release() =
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

        override x.Perform(token : AdaptiveToken, renderToken : RenderToken, output : OutputDescription) =
            use __ = ctx.ResourceLock
            use __ = GlobalResourceLock.lock()
            writeCurrentContextHandle()

            GL.Check "[RenderTask.Run] Entry"
            ctx.PushDebugGroup(x.Name ||? "Render Task")

            let fbo =
                match output.Framebuffer with
                | :? Framebuffer as fbo -> fbo
                | fbo -> failf "unsupported framebuffer: %A" fbo

            signature |> FramebufferSignature.validateCompability fbo

            x.ProcessDeltas(token, renderToken)
            x.UpdateResources(token, renderToken)

            Framebuffer.draw signature fbo output.Viewport output.Scissor (fun _ ->
                renderTaskLock.Run (fun () ->
                    beforeRender.Trigger()
                    NativePtr.write runtimeStats V2i.Zero

                    x.PerformInner(token, renderToken, output)
                    GL.Check "[RenderTask.Run] PerformInner"

                    afterRender.Trigger()
                    let rt = NativePtr.read runtimeStats
                    renderToken.AddDrawCalls(rt.X, rt.Y)
                )

                GL.BindVertexArray 0
                GL.BindBuffer(BufferTarget.DrawIndirectBuffer, 0)
            )

            ctx.PopDebugGroup()

    type RenderTask(man : ResourceManager, fboSignature : IFramebufferSignature, objects : aset<IRenderObject>, debug : bool) as this =
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

        override x.Release() =
            mainCommand.Free(x.Scope)
            base.Release()

        override x.PerformInner(token : AdaptiveToken, _ : RenderToken, _ : OutputDescription) =
            mainCommand.Update(token, x.Scope)
            mainCommand.Run()

            if RuntimeConfig.SyncUploadsAndFrames then
                GL.Sync()

    type ClearTask(runtime : IRuntime, ctx : Context, signature : IFramebufferSignature, values : aval<ClearValues>) =
        inherit AbstractRenderTask()

        override x.PerformUpdate(_, _) = ()
        override x.Perform(token : AdaptiveToken, _ : RenderToken, output : OutputDescription) =
            let fbo = output.Framebuffer |> unbox<Framebuffer>
            signature |> FramebufferSignature.validateCompability fbo

            Operators.using ctx.ResourceLock (fun _ ->
                Framebuffer.draw signature fbo output.Viewport output.Scissor (fun _ ->
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