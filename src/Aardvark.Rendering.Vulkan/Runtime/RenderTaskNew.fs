namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop
open Aardvark.Base.Incremental
open System.Diagnostics
open System.Collections.Generic
open Aardvark.Base.Runtime

#nowarn "9"
#nowarn "51"

module RenderTaskNew =
    
    type ResourceKind =
        | Pipeline = 0
        | IndexBuffer = 1
        | DescriptorBinding = 2
        | VertexBuffers = 3
        | DrawCalls = 4

    type RenderObjectCompiler(device : Device, renderPass : RenderPass) =
        inherit ResourceSet()
        
        static let get (t : AdaptiveToken) (r : INativeResourceLocation<'a>) =
            r.Update(t).handle

        let stats : nativeptr<V2i> = NativePtr.alloc 1
        let locks = ReferenceCountingSet<ILockedResource>()

        let user =
            { new IResourceUser with
                member x.AddLocked l = lock locks (fun () -> locks.Add l |> ignore)
                member x.RemoveLocked l = lock locks (fun () -> locks.Remove l |> ignore)
            }

        let manager = new ResourceManager(user, device)
        let cache = ResourceLocationCache<VKVM.CommandStream>(user)
        let mutable version = 0

        override x.InputChanged(t ,i) =
            base.InputChanged(t, i)
            match i with
                | :? IResourceLocation<UniformBuffer> -> ()
                | :? IResourceLocation -> version <- version + 1
                | _ -> ()
 
        member x.Compile(o : IRenderObject) : IResourceLocation<VKVM.CommandStream> =
            let call = 
                cache.GetOrCreate([o :> obj], fun owner key ->
                    let mutable stream = Unchecked.defaultof<VKVM.CommandStream> //new VKVM.CommandStream()
                    let mutable prep : Option<PreparedMultiRenderObject> = None
                
                    let compile (o : IRenderObject) =
                        x.EvaluateAlways AdaptiveToken.Top (fun t ->
                            let o = manager.PrepareRenderObject(t, renderPass, o)
                            for o in o.Children do
                                for r in o.resources do x.Add r

                                stream.IndirectBindPipeline(get t o.pipeline) |> ignore
                                stream.IndirectBindDescriptorSets(get t o.descriptorSets) |> ignore

                                match o.indexBuffer with
                                    | Some ib ->
                                        stream.IndirectBindIndexBuffer(get t ib) |> ignore
                                    | None ->
                                        ()

                                stream.IndirectBindVertexBuffers(get t o.vertexBuffers) |> ignore
                                stream.IndirectDraw(stats, get t o.isActive, get t o.drawCalls) |> ignore
                            o
                        )

                    { new AbstractResourceLocation<VKVM.CommandStream>(owner, key) with

                        member x.Create() =
                            stream <- new VKVM.CommandStream()

                        member y.Destroy() = 
                            stream.Dispose()
                            match prep with
                                | Some p -> 
                                    for o in p.Children do
                                        for r in o.resources do x.Remove r
                                    prep <- None
                                | None -> 
                                    ()

                        member x.GetHandle _ = 
                            match prep with
                                | Some p -> 
                                    { handle = stream; version = 0 }   

                                | None ->
                                    let p = compile o
                                    prep <- Some p
                                    { handle = stream; version = 0 }   

                    }
                )
            call.Acquire()
            call
            
        member x.CurrentVersion = version

    type OrderToken internal(s : VKVM.CommandStream) =
        member internal x.Stream = s

    type ChangeableCommandBuffer(pool : CommandPool, renderPass : RenderPass, viewports : IMod<Box2i[]>) =
        inherit Mod.AbstractMod<CommandBuffer>()

        let device = pool.Device
        let compiler = RenderObjectCompiler(device, renderPass)
        let mutable resourceVersion = 0
        let mutable cmdVersion = -1
        let mutable cmdViewports = [||]
        let mutable last = new VKVM.CommandStream()
        let mutable first = new VKVM.CommandStream(Next = Some last)

        let lastToken = OrderToken(last)
        let firstToken = OrderToken(first)

        let cmdBuffer = pool.CreateCommandBuffer(CommandBufferLevel.Secondary)

        member x.UpdateResources(t : AdaptiveToken) =
            x.EvaluateIfNeeded t () (fun t ->
                compiler.Update t |> ignore
                resourceVersion <- compiler.CurrentVersion
            )

        member x.First = firstToken
        member x.Last = lastToken

        member x.Remove(f : OrderToken) =
            let stream = f.Stream
            let prev = 
                match stream.Prev with
                    | Some p -> p
                    | None -> first 

            let next = 
                match stream.Next with
                    | Some n -> n
                    | None -> last
                    
            prev.Next <- Some next
            stream.Dispose()
            cmdVersion <- -1
            x.MarkOutdated()

        member x.InsertAfter(t : OrderToken, o : IRenderObject) =
            let res = compiler.Compile(o)
            let stream = res.Update(AdaptiveToken.Top).handle

            let prev = t.Stream
            let next =
                match prev.Next with
                    | Some n -> n
                    | None -> last

            prev.Next <- Some stream
            stream.Next <- Some next

            cmdVersion <- -1
            x.MarkOutdated()
            OrderToken(stream)

        member x.InsertBefore(t : OrderToken, o : IRenderObject) =
            let res = compiler.Compile(o)
            let stream = res.Update(AdaptiveToken.Top).handle

            let next = t.Stream
            let prev = 
                match next.Prev with
                    | Some n -> n
                    | None -> first

            prev.Next <- Some stream
            stream.Next <- Some next

            cmdVersion <- -1
            x.MarkOutdated()
            OrderToken(stream)

        member x.Append(o : IRenderObject) =
            x.InsertBefore(lastToken, o)

        override x.Compute (t : AdaptiveToken) =
            x.EvaluateAlways t (fun t -> 
                compiler.Update t |> ignore
                resourceVersion <- compiler.CurrentVersion

                let vps = viewports.GetValue t

                if resourceVersion <> cmdVersion || cmdViewports <> vps then
                    cmdViewports <- vps
                    cmdVersion <- resourceVersion

                    first.SeekToBegin()
                    first.SetViewport(0u, vps |> Array.map (fun b -> VkViewport(float32 b.Min.X, float32 b.Min.X, float32 (1 + b.SizeX), float32 (1 + b.SizeY), 0.0f, 1.0f))) |> ignore
                    first.SetScissor(0u, vps |> Array.map (fun b -> VkRect2D(VkOffset2D(b.Min.X, b.Min.X), VkExtent2D(1 + b.SizeX, 1 + b.SizeY)))) |> ignore

                    cmdBuffer.Reset()
                    cmdBuffer.Begin(renderPass, CommandBufferUsage.RenderPassContinue)
                    cmdBuffer.AppendCommand()
                    first.Run(cmdBuffer.Handle)
                    cmdBuffer.End()

                cmdBuffer
            )

    type IRenderTaskObjectToken =
        interface end

    type private RenderTaskObjectToken(cmd : ChangeableCommandBuffer, t : OrderToken) =
        member x.Buffer = cmd
        member x.Token = t
        interface IRenderTaskObjectToken

    type RenderTask(device : Device, renderPass : RenderPass, shareTextures : bool, shareBuffers : bool) =
        inherit AbstractRenderTask()

        let pool = device.GraphicsFamily.CreateCommandPool()
        let passes = SortedDictionary<Aardvark.Base.Rendering.RenderPass, ChangeableCommandBuffer>()
        let viewports = Mod.init [||]
        
        let cmd = pool.CreateCommandBuffer(CommandBufferLevel.Primary)

        member x.Add(o : IRenderObject) =
            let key = o.RenderPass
            let cmd =
                match passes.TryGetValue key with
                    | (true,c) -> c
                    | _ ->
                        let c = ChangeableCommandBuffer(pool, renderPass, viewports)
                        passes.[key] <- c
                        x.MarkOutdated()
                        c
            let t = cmd.Append(o)
            RenderTaskObjectToken(cmd, t) :> IRenderTaskObjectToken

        member x.Remove(t : IRenderTaskObjectToken) =
            let t = unbox<RenderTaskObjectToken> t
            t.Buffer.Remove(t.Token)

        override x.Dispose() = ()

        override x.FramebufferSignature = Some (renderPass :> _)

        override x.Runtime = None

        override x.PerformUpdate(token : AdaptiveToken, rt : RenderToken) =
            ()

        override x.Use(f : unit -> 'r) =
            f()

        override x.Perform(token : AdaptiveToken, rt : RenderToken, desc : OutputDescription) =
            x.OutOfDate <- true
            let vp = Array.create renderPass.AttachmentCount desc.viewport
            transact (fun () -> viewports.Value <- vp)

            let fbo =
                match desc.framebuffer with
                    | :? Framebuffer as fbo -> fbo
                    | fbo -> failwithf "unsupported framebuffer: %A" fbo

            use tt = device.Token
            let passCmds = passes.Values |> Seq.map (fun p -> p.GetValue(token)) |> Seq.toList
            tt.Sync()

            cmd.Reset()
            cmd.Begin(renderPass, CommandBufferUsage.OneTimeSubmit)
            cmd.enqueue {
                let oldLayouts = Array.zeroCreate fbo.ImageViews.Length
                for i in 0 .. fbo.ImageViews.Length - 1 do
                    let img = fbo.ImageViews.[i].Image
                    oldLayouts.[i] <- img.Layout
                    if VkFormat.hasDepth img.Format then
                        do! Command.TransformLayout(img, VkImageLayout.DepthStencilAttachmentOptimal)
                    else
                        do! Command.TransformLayout(img, VkImageLayout.ColorAttachmentOptimal)

                do! Command.BeginPass(renderPass, fbo, false)
                do! Command.ExecuteSequential passCmds
                do! Command.EndPass

                for i in 0 .. fbo.ImageViews.Length - 1 do
                    let img = fbo.ImageViews.[i].Image
                    do! Command.TransformLayout(img, oldLayouts.[i])
            }   
            cmd.End()

            device.GraphicsFamily.RunSynchronously cmd
            






