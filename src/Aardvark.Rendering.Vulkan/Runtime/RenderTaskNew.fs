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

    type CommandStreamResource(owner, key, o : IRenderObject, resources : ResourceSet, manager : ResourceManager, renderPass : RenderPass, stats : nativeptr<V2i>) =
        inherit AbstractResourceLocation<VKVM.CommandStream>(owner, key)
         
        let mutable stream = Unchecked.defaultof<VKVM.CommandStream>
        let mutable prep : PreparedMultiRenderObject = Unchecked.defaultof<_>

        let compile (o : IRenderObject) =
            let o = manager.PrepareRenderObject(renderPass, o)
            for o in o.Children do
                for r in o.resources do resources.Add r
                                
                stream.IndirectBindPipeline(o.pipeline.Pointer) |> ignore
                stream.IndirectBindDescriptorSets(o.descriptorSets.Pointer) |> ignore

                match o.indexBuffer with
                    | Some ib ->
                        stream.IndirectBindIndexBuffer(ib.Pointer) |> ignore
                    | None ->
                        ()

                stream.IndirectBindVertexBuffers(o.vertexBuffers.Pointer) |> ignore
                stream.IndirectDraw(stats, o.isActive.Pointer, o.drawCalls.Pointer) |> ignore
            o

        member x.Stream = stream
        member x.Object = prep

        override x.Create() =
            stream <- new VKVM.CommandStream()
            let p = compile o
            prep <- p

        override x.Destroy() = 
            stream.Dispose()
            for o in prep.Children do
                for r in o.resources do resources.Remove r
            prep <- Unchecked.defaultof<_>

        override x.GetHandle _ = 
            { handle = stream; version = 0 }   

    type RenderObjectCompiler(manager : ResourceManager, renderPass : RenderPass) =
        inherit ResourceSet()

        let stats : nativeptr<V2i> = NativePtr.alloc 1
        let cache = ResourceLocationCache<VKVM.CommandStream>(manager.ResourceUser)
        let mutable version = 0

        override x.InputChanged(t ,i) =
            base.InputChanged(t, i)
            match i with
                | :? IResourceLocation<UniformBuffer> -> ()
                | :? IResourceLocation -> version <- version + 1
                | _ -> ()
 
        member x.Dispose() =
            cache.Clear()

        member x.Compile(o : IRenderObject) : CommandStreamResource =
            let call = 
                cache.GetOrCreate([o :> obj], fun owner key ->
                    new CommandStreamResource(owner, key, o, x, manager, renderPass, stats)
                )
            call.Acquire()
            call |> unbox<CommandStreamResource>
            
        member x.CurrentVersion = version

    type IToken = interface end

    type private OrderToken(s : VKVM.CommandStream, r : Option<CommandStreamResource>) =
        member x.Stream = s
        member x.Resource = r
        interface IToken

    [<AbstractClass>]
    type AbstractChangeableCommandBuffer(manager : ResourceManager, pool : CommandPool, renderPass : RenderPass, viewports : IMod<Box2i[]>) =
        inherit Mod.AbstractMod<CommandBuffer>()

        let device = pool.Device
        let compiler = RenderObjectCompiler(manager, renderPass)
        let mutable resourceVersion = 0
        let mutable cmdVersion = -1
        let mutable cmdViewports = [||]

        let cmdBuffer = pool.CreateCommandBuffer(CommandBufferLevel.Secondary)
        let dirty = HashSet<CommandStreamResource>()

        abstract member Release : unit -> unit
        abstract member Prolog : VKVM.CommandStream
        abstract member Sort : AdaptiveToken -> bool
        default x.Sort _ = false

        override x.InputChanged(t : obj, o : IAdaptiveObject) =
            match o with
                | :? CommandStreamResource as r ->
                    lock dirty (fun () -> dirty.Add r |> ignore)
                | _ ->
                    ()

        member x.Compile(o : IRenderObject) =
            let res = compiler.Compile(o)
            x.EvaluateAlways AdaptiveToken.Top (fun t ->
                let stream = res.Update(t).handle
                res
            )

        member x.Changed() =
            cmdVersion <- -1
            x.MarkOutdated()
            

        member x.Destroy(r : CommandStreamResource) =
            lock dirty (fun () -> dirty.Remove r |> ignore)
            r.Release()

        member x.Dispose() =
            compiler.Dispose()
            dirty.Clear()
            cmdBuffer.Dispose()

        override x.Compute (t : AdaptiveToken) =
            x.EvaluateAlways t (fun t ->
            
                        
                // update all dirty programs 
                let dirty =
                    lock dirty (fun () ->
                        let res = dirty |> HashSet.toArray
                        dirty.Clear()
                        res
                    )

                for d in dirty do
                    d.Update(t) |> ignore

                // update all resources
                compiler.Update t |> ignore
                resourceVersion <- compiler.CurrentVersion

                // refill the CommandBuffer (if necessary)
                let vps = viewports.GetValue t
                let contentChanged      = cmdVersion < 0 || dirty.Length > 0
                let viewportChanged     = cmdViewports <> vps
                let versionChanged      = cmdVersion >= 0 && resourceVersion <> cmdVersion
                let orderChanged        = x.Sort t

                if contentChanged || versionChanged || viewportChanged || orderChanged then
                    let first = x.Prolog
                    let cause =
                        String.concat "; " [
                            if contentChanged then yield "content"
                            if versionChanged then yield "resources"
                            if viewportChanged then yield "viewport"
                            if orderChanged then yield "order"
                        ]
                        |> sprintf "{ %s }"

                    Log.line "[Vulkan] recompile commands: %s" cause
                    cmdViewports <- vps
                    cmdVersion <- resourceVersion

                    if viewportChanged then
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
            
    [<AbstractClass>]
    type AbstractChangeableSetCommandBuffer(manager : ResourceManager, pool : CommandPool, renderPass : RenderPass, viewports : IMod<Box2i[]>) =
        inherit AbstractChangeableCommandBuffer(manager, pool, renderPass, viewports)

        abstract member Add : IRenderObject -> bool
        abstract member Remove : IRenderObject -> bool


    type ChangeableUnorderedCommandBuffer(manager : ResourceManager, pool : CommandPool, renderPass : RenderPass, viewports : IMod<Box2i[]>) =
        inherit AbstractChangeableSetCommandBuffer(manager, pool, renderPass, viewports)

        let first = new VKVM.CommandStream()
        let trie = Trie<VKVM.CommandStream>()
        do trie.Add([], first)

        let cache = Dict<IRenderObject, CommandStreamResource>()

        static let key (s : CommandStreamResource) =
            let ro = s.Object.Children.[0]
            [ ro.pipeline :> obj; ro.original.Id :> obj ]
 
        override x.Prolog = first

        override x.Release() =
            cache.Clear()
            first.Dispose()
            trie.Clear()

        override x.Add(o : IRenderObject) =
            if not (cache.ContainsKey o) then
                let resource = x.Compile o
                let key = key resource
                trie.Add(key, resource.Stream)
                cache.[o] <- resource
                true
            else
                false

        override x.Remove(o : IRenderObject) =
            match cache.TryRemove o with
                | (true, r) ->
                    let key = key r
                    trie.Remove key |> ignore
                    x.Destroy r 
                    true
                | _ ->
                    false

    type ChangeableOrderedCommandBuffer(manager : ResourceManager, pool : CommandPool, renderPass : RenderPass, viewports : IMod<Box2i[]>, sorter : IMod<Box3d[] -> int[]>) =
        inherit AbstractChangeableSetCommandBuffer(manager, pool, renderPass, viewports)
        
        let first = new VKVM.CommandStream()

        let cache = Dict<IRenderObject, IMod<Box3d> * CommandStreamResource>()

        static let rec boundingBox (o : IRenderObject) : IMod<Box3d> =
            match o with
                | :? RenderObject as o ->
                    match Ag.tryGetAttributeValue o.AttributeScope "GlobalBoundingBox" with
                        | Success box -> box
                        | _ -> failwith "[Vulkan] could not get BoundingBox for RenderObject"
                    
                | :? MultiRenderObject as o ->
                    o.Children |> List.map boundingBox |> Mod.mapN Box3d

                | :? PreparedMultiRenderObject as o ->
                    o.Children |> List.map boundingBox |> Mod.mapN Box3d
                    
                | :? PreparedRenderObject as o ->
                    boundingBox o.original

                | _ ->
                    failf "invalid renderobject %A" o

        override x.Add(o : IRenderObject) =
            if not (cache.ContainsKey o) then
                let res = x.Compile o
                let bb = boundingBox res.Object
                cache.[o] <- (bb, res)
                x.Changed()
                true
            else
                false

        override x.Remove(o : IRenderObject) =
            match cache.TryRemove o with
                | (true, (_,res)) -> 
                    x.Destroy res
                    x.Changed()
                    true
                | _ -> 
                    false

        override x.Prolog = first

        override x.Release() =
            first.Dispose()

        override x.Sort t =
            let sorter = sorter.GetValue t
            let all = cache.Values |> Seq.toArray

            let boxes = Array.zeroCreate all.Length
            let streams = Array.zeroCreate all.Length
            for i in 0 .. all.Length - 1 do
                let (bb, s) = all.[i]
                let bb = bb.GetValue t
                boxes.[i] <- bb
                streams.[i] <- s.Stream

            let perm = sorter boxes
            let mutable last = first
            for i in perm do
                let s = streams.[i]
                last.Next <- Some s
            last.Next <- None


            true


    type ChangeableStaticallyOrderedCommandBuffer(manager : ResourceManager, pool : CommandPool, renderPass : RenderPass, viewports : IMod<Box2i[]>) =
        inherit AbstractChangeableCommandBuffer(manager, pool, renderPass, viewports)

        let mutable last = new VKVM.CommandStream()
        let mutable first = new VKVM.CommandStream(Next = Some last)

        let lastToken = OrderToken(last, None) :> IToken
        let firstToken = OrderToken(first, None) :> IToken

        override x.Release() =
            first.Dispose()
            last.Dispose()

        override x.Prolog = first

        member x.First = firstToken
        member x.Last = lastToken

        member x.Remove(f : IToken) =
            let f = unbox<OrderToken> f
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

            match f.Resource with
                | Some r -> x.Destroy r
                | _ -> ()

            x.Changed()

        member x.InsertAfter(t : IToken, o : IRenderObject) =
            let t = unbox<OrderToken> t
            let res = x.Compile o
            let stream = res.Stream

            let prev = t.Stream
            let next =
                match prev.Next with
                    | Some n -> n
                    | None -> last

            prev.Next <- Some stream
            stream.Next <- Some next

            x.Changed()
            OrderToken(stream, Some res) :> IToken

        member x.InsertBefore(t : IToken, o : IRenderObject) =
            let t = unbox<OrderToken> t
            let res = x.Compile o
            let stream = res.Stream

            let next = t.Stream
            let prev = 
                match next.Prev with
                    | Some n -> n
                    | None -> first

            prev.Next <- Some stream
            stream.Next <- Some next

            x.Changed()
            OrderToken(stream, Some res) :> IToken

        member inline x.Prepend(o : IRenderObject) =
            x.InsertAfter(firstToken, o)

        member inline x.Append(o : IRenderObject) =
            x.InsertBefore(lastToken, o)

    type RenderTask(device : Device, renderPass : RenderPass, shareTextures : bool, shareBuffers : bool) =
        inherit AbstractRenderTask()

        let pool = device.GraphicsFamily.CreateCommandPool()
        let passes = SortedDictionary<Aardvark.Base.Rendering.RenderPass, AbstractChangeableSetCommandBuffer>()
        let viewports = Mod.init [||]
        
        let cmd = pool.CreateCommandBuffer(CommandBufferLevel.Primary)

        let locks = ReferenceCountingSet<ILockedResource>()

        let user =
            { new IResourceUser with
                member x.AddLocked l = lock locks (fun () -> locks.Add l |> ignore)
                member x.RemoveLocked l = lock locks (fun () -> locks.Remove l |> ignore)
            }

        let manager = new ResourceManager(user, device)

        member x.Add(o : IRenderObject) =
            let key = o.RenderPass
            let cmd =
                match passes.TryGetValue key with
                    | (true,c) -> c
                    | _ ->
                        let c = 
                            match key.Order with
                                | RenderPassOrder.BackToFront -> failwith ""
                                | RenderPassOrder.FrontToBack -> failwith ""
                                | _ -> ChangeableUnorderedCommandBuffer(manager, pool, renderPass, viewports) :> AbstractChangeableSetCommandBuffer
                        passes.[key] <- c
                        x.MarkOutdated()
                        c
            cmd.Add(o)

        member x.Remove(o : IRenderObject) =
            let key = o.RenderPass
            match passes.TryGetValue key with
                | (true,c) -> 
                    c.Remove o
                | _ ->
                    false

        member x.Clear() =
            for c in passes.Values do
                c.Dispose()
            passes.Clear()

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
            






