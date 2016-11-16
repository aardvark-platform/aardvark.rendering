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

module RenderTasks =
    
    [<AbstractClass>]
    type AbstractVulkanRenderTask(manager : ResourceManager, renderPass : RenderPass, config : IMod<BackendConfiguration>, shareTextures : bool, shareBuffers : bool) =
        inherit AbstractRenderTask()

        let fboSignature = renderPass :> IFramebufferSignature
        let device = manager.Device
        let renderTaskLock = RenderTaskLock()
        let manager = ResourceManager(manager, manager.Device, Some (fboSignature, renderTaskLock), shareTextures, shareBuffers)
        let runtimeStats = NativePtr.alloc 1
        let mutable isDisposed = false

        let scope =
            {
                runtimeStats = runtimeStats
            }

        member x.Scope = scope


        abstract member Perform : Framebuffer -> FrameStatistics
        abstract member Release : unit -> unit

        member x.Config = config
        member x.Device = device
        member x.RenderTaskLock = renderTaskLock
        member x.ResourceManager = manager

        override x.Dispose() =
            if not isDisposed then
                isDisposed <- true
                x.Release()

        override x.FramebufferSignature = Some fboSignature
        override x.Runtime = Some device.Runtime

        override x.Run(desc : OutputDescription) =
            let fbo = desc.framebuffer // TODO: fix outputdesc
            if not <| fboSignature.IsAssignableFrom fbo.Signature then
                failwithf "incompatible FramebufferSignature\nexpected: %A but got: %A" fboSignature fbo.Signature

            let fbo =
                match fbo with
                    | :? Framebuffer as fbo -> fbo
                    | _ -> failwithf "unsupported framebuffer: %A" fbo

            let innerStats = 
                renderTaskLock.Run (fun () -> 
                    NativePtr.write runtimeStats V2i.Zero
                    let r = x.Perform fbo
                    let rt = NativePtr.read runtimeStats
                    { r with DrawCallCount = float rt.X; EffectiveDrawCallCount = float rt.Y }
                )

            innerStats

    [<AbstractClass>]
    type DependentCommandBuffer(pool : CommandPool) =
        inherit DirtyTrackingAdaptiveObject<IResource>()

        let objects = CSet.empty
        let mutable initialized = false
        let buffers = Dict<Framebuffer, CommandBuffer>()
        let dependentBuffers = Dict<Framebuffer, IMod<CommandBuffer * FrameStatistics>>()

        member private x.init() =
            if not initialized then
                initialized <- true
                x.Init objects

        member x.Add (o : PreparedMultiRenderObject) =
            x.init()
            transact (fun () ->
                o.Update(x) |> ignore
                objects.Add o |> ignore
            )

        member x.Remove(o : PreparedMultiRenderObject) =
            x.init()
            transact (fun () ->
                objects.Remove o |> ignore
            )

        abstract member Init : aset<PreparedMultiRenderObject> -> unit
        abstract member UpdateProgram : unit -> FrameStatistics
        abstract member Fill : Framebuffer * CommandBuffer -> FrameStatistics
        abstract member Release : unit -> unit

        member x.Dispose() =
            if initialized then
                initialized <- false
                x.Release()
                transact (fun () -> objects.Clear())
                buffers.Values |> Seq.iter (fun cmd -> cmd.Dispose())
                buffers.Clear()
                dependentBuffers.Clear()

        member x.Update(caller : IAdaptiveObject) =
            x.init()
            x.EvaluateIfNeeded' caller FrameStatistics.Zero (fun dirty ->
                let mutable stats = FrameStatistics.Zero
                for d in dirty do
                    stats <- stats + d.Update x

                let s = x.UpdateProgram()
                stats + s
            )

        member x.GetCommandBuffer(fbo : Framebuffer) =
            x.init()
            dependentBuffers.GetOrCreate(
                fbo,
                fun fbo ->
                    let cmd = pool.CreateCommandBuffer(CommandBufferLevel.Primary)
                    buffers.[fbo] <- cmd
                    Mod.custom (fun self ->
                        let mutable stats = x.Update(self)
                        cmd.Begin(CommandBufferUsage.None)
                        stats <- stats + x.Fill(fbo, cmd)
                        cmd.End()
                        cmd, stats
                    )   
            )

        interface IDisposable with
            member x.Dispose() = x.Dispose()



    type SortKey = list<int>

    type ProjectionComparer(projections : list<RenderObject -> IMod>) =

        let rec getRenderObject (ro : IRenderObject) =
            match ro with
                | :? RenderObject as ro -> ro
                | :? MultiRenderObject as ro -> ro.Children |> List.head |> getRenderObject
                | :? PreparedRenderObject as ro -> ro.original
                | :? PreparedMultiRenderObject as ro -> ro.First.original
                | _ -> failwithf "[ProjectionComparer] unknown RenderObject: %A" ro

        let ids = ConditionalWeakTable<IMod, ref<int>>()
        let mutable currentId = 0
        let getId (m : IMod) =
            match ids.TryGetValue m with
                | (true, r) -> !r
                | _ ->
                    let id = Interlocked.Increment &currentId
                    ids.Add(m, ref id)
                    id

        let maxKey = Int32.MaxValue :: (projections |> List.map (fun _ -> Int32.MaxValue))

        let keys = ConditionalWeakTable<IRenderObject, SortKey>()
        let project (ro : IRenderObject) =
            let ro = getRenderObject ro

            match keys.TryGetValue ro with
                | (true, key) -> key
                | _ ->
                    if ro.Id < 0 then
                        maxKey
                    else
                        let key = projections |> List.map (fun p -> p ro |> getId)
                        keys.Add(ro, key)
                        key


        interface IComparer<IRenderObject> with
            member x.Compare(l : IRenderObject, r : IRenderObject) =
                let left = project l
                let right = project r
                compare left right

    type StaticOrderCommandBuffer(renderPass : RenderPass, config : IMod<BackendConfiguration>, pool : CommandPool) =
        inherit DependentCommandBuffer(pool)
        let mutable objectsWithKeys = Unchecked.defaultof<aset<IRenderObject * PreparedMultiRenderObject>>
        let mutable scope = { runtimeStats = NativePtr.zero }

        let compile (prev : Option<PreparedMultiRenderObject>) (self : PreparedMultiRenderObject) =
            let code = Compiler.compile scope prev self

            { new IAdaptiveCode<Compiler.Instruction> with
                member x.Content = [Mod.constant code]
                member x.Dispose() = ()
            }

        let mutable hasProgram = false
        let mutable program = Unchecked.defaultof<IAdaptiveProgram<VkCommandBuffer>>
        let mutable currentConfig = BackendConfiguration.Default

        let reinit (self : StaticOrderCommandBuffer) (config : BackendConfiguration) =
            // if the config changed or we never compiled a program
            // we need to do something
            if config <> currentConfig || not hasProgram then

                // if we have a program we'll dispose it now
                if hasProgram then program.Dispose()

                // use the config to create a comparer for IRenderObjects
                let comparer =
                    match config.sorting with
                        | RenderObjectSorting.Grouping projections -> 
                            ProjectionComparer(projections) :> IComparer<_>

                        | RenderObjectSorting.Static comparer -> 
                            { new IComparer<_> with 
                                member x.Compare(l, r) =
                                    if l.Id = r.Id then 0
                                    elif l.Id < 0 then -1
                                    elif r.Id < 0 then 1
                                    else comparer.Compare(l,r)
                            }

                        | Arbitrary ->
                            { new IComparer<_> with 
                                member x.Compare(l, r) =
                                    if l.Id < 0 then -1
                                    elif r.Id < 0 then 1
                                    else 0
                            }

                        | RenderObjectSorting.Dynamic create ->
                            failwith "[AbstractRenderTask] dynamic sorting not implemented"



                // create the new program
                let newProgram =
                    AdaptiveProgram.nativeDifferential 6 comparer compile objectsWithKeys

                // finally we store the current config/program and set hasProgram to true
                program <- newProgram
                hasProgram <- true
                currentConfig <- config

        override x.Init(objects : aset<PreparedMultiRenderObject>) =
            scope <- { runtimeStats = NativePtr.alloc 1 }
            objectsWithKeys <- objects |> ASet.map (fun o -> (o :> IRenderObject, o))
            let config = config.GetValue x
            reinit x config

        override x.UpdateProgram() =
            let config = config.GetValue x
            reinit x config
            program.Update x |> ignore
            FrameStatistics.Zero

        override x.Fill(fbo : Framebuffer, cmd : CommandBuffer) =
            let bounds = Box2i(V2i.Zero, fbo.Size - V2i.II)
            let viewports = Array.create renderPass.AttachmentCount bounds

            NativePtr.write scope.runtimeStats V2i.Zero

            cmd.BeginPass(renderPass, fbo)
            cmd.SetViewports(viewports)
            cmd.SetScissors(viewports)
            program.Run(cmd.Handle)
            cmd.EndPass()

            let calls = NativePtr.read scope.runtimeStats

            { FrameStatistics.Zero with
                DrawCallCount = float calls.X
                EffectiveDrawCallCount = float calls.Y
            }

        override x.Release() =
            if not (NativePtr.isNull scope.runtimeStats) then
                NativePtr.free scope.runtimeStats
                scope <- { runtimeStats = NativePtr.zero }

            if hasProgram then
                program.Dispose()
                program <- Unchecked.defaultof<_>
                hasProgram <- false


            objectsWithKeys <- Unchecked.defaultof<_>

    type RenderTask(man : ResourceManager, renderPass : RenderPass, objects : aset<IRenderObject>, config : IMod<BackendConfiguration>, shareTextures : bool, shareBuffers : bool) as this =
        inherit AbstractVulkanRenderTask(man, renderPass, config, shareTextures, shareBuffers)

        let device = man.Device
        let preparedObjects = objects |> ASet.mapUse (fun o -> this.ResourceManager.PrepareRenderObject(renderPass, o))
        let preparedObjectReader = preparedObjects.GetReader()

        let pool = device.GraphicsFamily.CreateCommandPool()
        let mutable commandBuffers = Map.empty

        let getCommandBuffer (pass : Aardvark.Base.Rendering.RenderPass) : DependentCommandBuffer =
            match Map.tryFind pass commandBuffers with
                | Some task -> task
                | _ ->
                    let task = 
                        match pass.Order with
                            | RenderPassOrder.Arbitrary ->
                                new StaticOrderCommandBuffer(renderPass, config, pool) :> DependentCommandBuffer

                            | order ->
                                failf "sorting not implemented"

                    commandBuffers <- Map.add pass task commandBuffers
                    task

        let update ( x : AbstractVulkanRenderTask ) =
            let mutable stats = FrameStatistics.Zero
            let deltas = preparedObjectReader.GetDelta x

            for d in deltas do 
                match d with
                    | Add v ->
                        let task = getCommandBuffer v.RenderPass
                        task.Add v
                    | Rem v ->
                        let task = getCommandBuffer v.RenderPass
                        task.Remove v

            stats




        override x.Use (f : unit -> 'a) =
            lock x (fun () ->
                x.RenderTaskLock.Run (fun () ->
                    f()
                )
            )

        override x.Update() =
            let mutable stats = update x

            let mutable runStats = []
            for (_,t) in Map.toSeq commandBuffers do
                stats <- stats + t.Update(x)

            stats

        override x.Perform (fbo : Framebuffer) =
            let mutable stats = update x
            
            for (_,dep) in Map.toSeq commandBuffers do
                let cmd, s = 
                    using device.ResourceToken (fun token ->
                        dep.GetCommandBuffer(fbo).GetValue(x)
                    )
                device.GraphicsFamily.RunSynchronously(cmd)
                stats <- stats + s

            stats

        override x.Release() =
            commandBuffers |> Map.iter (fun _ c -> c.Dispose())
            pool.Dispose()
            preparedObjectReader.Dispose()
            commandBuffers <- Map.empty


    type ClearTask(manager : ResourceManager, renderPass : RenderPass, clearColors : Map<Symbol, IMod<C4f>>, clearDepth : IMod<Option<float>>, clearStencil : Option<IMod<uint32>>) =
        inherit AdaptiveObject()
        let device = manager.Device
        let pool = device.GraphicsFamily.CreateCommandPool()
        let cmd = pool.CreateCommandBuffer(CommandBufferLevel.Primary)

        let clearColors = 
            clearColors |> Map.map (fun i c ->
                c |> Mod.map (fun c -> 
                    VkClearColorValue(float32 = V4f(c.R, c.G, c.B, c.A))
                )
            )

        let clearDepthStencil =
            if Option.isSome renderPass.DepthStencilAttachment then
                match clearDepth, clearStencil with
                    | d, Some s ->
                        Mod.map2 (fun d s -> 
                            let d = defaultArg d 1.0
                            VkImageAspectFlags.DepthBit ||| VkImageAspectFlags.StencilBit,
                            VkClearDepthStencilValue(float32 d, s)
                        ) d s |> Some

                    | d, None ->
                        d |> Mod.map (fun d -> 
                            let d = defaultArg d 1.0
                            VkImageAspectFlags.DepthBit,
                            VkClearDepthStencilValue(float32 d, 0u)

                        ) |> Some

            else
                None


        let clearImage (image : Image) (cmd : CommandBuffer) (real : CommandBuffer -> unit) =
            let old = image.Layout
            let clear =
                command {
                    do! Command.TransformLayout(image, VkImageLayout.TransferDstOptimal)
                    image.Layout <- VkImageLayout.TransferDstOptimal
                    do! {
                        new Command<unit>() with
                            member x.Enqueue cmd = real cmd
                            member x.Dispose() = ()
                    }
                    do! Command.TransformLayout(image, old)
                }
            clear.Enqueue(cmd)
            image.Layout <- old

        member x.Run(caller : IAdaptiveObject, outputs : OutputDescription) =
            x.EvaluateAlways caller (fun () ->
                let fbo = unbox<Framebuffer> outputs.framebuffer

                cmd.Begin(CommandBufferUsage.OneTimeSubmit)
                let mutable rect = VkRect3D(VkOffset3D(0,0,0), VkExtent3D(fbo.Size.X, fbo.Size.Y,1))
                for (sem, view) in Map.toSeq fbo.Attachments do
                    let image = view.Image
                    let isDepth =
                        match image.Format with
                            | VkFormat.D16Unorm 
                            | VkFormat.D16UnormS8Uint
                            | VkFormat.D24UnormS8Uint
                            | VkFormat.X8D24UnormPack32
                            | VkFormat.D32Sfloat
                            | VkFormat.D32SfloatS8Uint -> true
                            | _ -> false

                    if isDepth then
                        match clearDepthStencil with
                            | Some cd ->
                                let aspect, value = cd.GetValue(x)

                                let mutable clearValue = value
                                let mutable range = VkImageSubresourceRange(aspect, 0u, 1u, 0u, 1u)
                                clearImage image cmd (fun cmd ->
                                    VkRaw.vkCmdClearDepthStencilImage(
                                        cmd.Handle, image.Handle, VkImageLayout.TransferDstOptimal, &&clearValue, 1u, &&range
                                    )
                                )
                            | None ->
                                ()
                    else
                        let mutable clearValue = clearColors.[sem].GetValue()
                        let mutable range = VkImageSubresourceRange(VkImageAspectFlags.ColorBit, 0u, 1u, 0u, 1u)
                        clearImage image cmd (fun cmd ->
                            VkRaw.vkCmdClearColorImage(
                                cmd.Handle, image.Handle, VkImageLayout.TransferDstOptimal, &&clearValue, 1u, &&range
                            )
                        )

                //cmd.EndPass()
                cmd.End()

                device.GraphicsFamily.RunSynchronously cmd
                FrameStatistics.Zero
            )

        interface IRenderTask with
            member x.Update(c) = FrameStatistics.Zero
            member x.Run(c,o) = x.Run(c,o)
            member x.Dispose() = ()
            member x.FrameId = 0UL
            member x.FramebufferSignature = Some (renderPass :> _)
            member x.Runtime = Some device.Runtime
            member x.Use f = lock x f

