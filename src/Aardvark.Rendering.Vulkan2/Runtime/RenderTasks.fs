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
        let manager = new ResourceManager(manager, manager.Device, Some (fboSignature, renderTaskLock), shareTextures, shareBuffers)
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
                manager.Dispose()
                x.Release()
                NativePtr.free runtimeStats

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
    type DependentCommandBuffer(renderPass : RenderPass, pool : CommandPool) as this =
        inherit DirtyTrackingAdaptiveObject<IResource>()

        let objects = CSet.empty
        let mutable initialized = false
        let cmd = pool.CreateCommandBuffer CommandBufferLevel.Secondary

        let mutable renderStats = FrameStatistics.Zero
        let mutable lastViewports = [||]
        let mutable version = 0
        let mutable commandVersion = -1

        member private x.init() =
            if not initialized then
                initialized <- true
                x.Init objects

        member x.Add (o : PreparedMultiRenderObject) =
            x.init()
            version <- version + 1
            transact (fun () ->
                o.Update(x) |> ignore
                objects.Add o |> ignore
            )

        member x.Remove(o : PreparedMultiRenderObject) =
            x.init()
            version <- version + 1
            transact (fun () ->
                objects.Remove o |> ignore
            )

        abstract member Init : aset<PreparedMultiRenderObject> -> unit
        abstract member UpdateProgram : unit -> FrameStatistics
        abstract member Fill : CommandBuffer -> FrameStatistics
        abstract member Release : unit -> unit

        member x.Dispose() =
            if initialized then
                initialized <- false
                x.Release()
                transact (fun () -> objects.Clear())
                cmd.Dispose()

        member x.Update(caller : IAdaptiveObject) =
            x.init()
            x.EvaluateIfNeeded' caller FrameStatistics.Zero (fun dirty ->
                let mutable stats = FrameStatistics.Zero
                for d in dirty do
                    if not d.IsDisposed then
                        stats <- stats + d.Update x

                let s = x.UpdateProgram()
                stats + s
            )

        member x.GetCommandBuffer(caller : IAdaptiveObject, viewports : Box2i[]) =
            let updateStats = x.Update caller

            let deltas              = updateStats.ResourceDeltas.Total
            let handlesChanged      = (deltas.Created + deltas.Replaced) <> 0.0
            let versionChanged      = version <> commandVersion
            let viewportChanged     = lastViewports <> viewports

            if handlesChanged || versionChanged || viewportChanged then
                Log.line "{ handles: %A; version: %A; viewport: %A }" handlesChanged versionChanged viewportChanged
                cmd.Begin(renderPass, CommandBufferUsage.RenderPassContinue)
                cmd.enqueue {
                    do! Command.SetViewports viewports
                    do! Command.SetScissors viewports
                }
                renderStats <- this.Fill(cmd)
                cmd.End()

                lastViewports <- viewports
                commandVersion <- version

            (cmd, updateStats + renderStats)

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
        inherit DependentCommandBuffer(renderPass, pool)
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

        override x.Fill(cmd : CommandBuffer) =
            NativePtr.write scope.runtimeStats V2i.Zero

            program.Run(cmd.Handle)

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

        let prepare (o : IRenderObject) =
            
            this.ResourceManager.PrepareRenderObject(renderPass, o)

        let device = man.Device
        let preparedObjects = objects |> ASet.mapUse prepare
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
                                VkRaw.warn "sorting not implemented"
                                new StaticOrderCommandBuffer(renderPass, config, pool) :> DependentCommandBuffer

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
            use token = device.Token


            let bounds = Box2i(V2i.Zero, fbo.Size - V2i.II)
            let vp = Array.create renderPass.AttachmentCount bounds

            let mutable stats = update x

            let commandBuffers =
                commandBuffers 
                    |> Map.toList 
                    |> List.map snd
                    |> List.map (fun dc ->
                        let cmd, s = dc.GetCommandBuffer(x, vp)
                        stats <- stats + s
                        cmd
                    )

            token.enqueue {
                for cmd in commandBuffers do
                    do! Command.Barrier

                    do! Command.BeginPass (renderPass, fbo, false)
                    do! Command.Execute cmd
                    do! Command.EndPass
            }

            // really run the stuff
            x.RenderTaskLock.Run token.Sync

            stats

        override x.Release() =
            commandBuffers |> Map.iter (fun _ c -> c.Dispose())
            pool.Dispose()
            preparedObjectReader.Dispose()
            commandBuffers <- Map.empty


    type ClearTask(manager : ResourceManager, renderPass : RenderPass, clearColors : Map<Symbol, IMod<C4f>>, clearDepth : IMod<Option<float>>, clearStencil : Option<IMod<uint32>>) =
        inherit AdaptiveObject()
        static let depthStencilFormats =
            HashSet.ofList [
                RenderbufferFormat.Depth24Stencil8
                RenderbufferFormat.Depth32fStencil8
                RenderbufferFormat.DepthStencil
            ]
        
        let device = manager.Device
        let pool = device.GraphicsFamily.CreateCommandPool()
        let cmd = pool.CreateCommandBuffer(CommandBufferLevel.Primary)

        let clearColors =
            renderPass.ColorAttachments |> Map.toSeq |> Seq.choose (fun (i, (s,_)) -> 
                match Map.tryFind s clearColors with
                    | Some c -> Some (i,c)
                    | None -> None
            )
            |> Seq.toArray

        let renderPassDepthAspect =
            match renderPass.DepthStencilAttachment with
                | Some signature ->
                    if depthStencilFormats.Contains signature.format then
                        ImageAspect.DepthStencil
                    else
                        ImageAspect.Depth
                | _ ->
                    ImageAspect.None

        member x.Run(caller : IAdaptiveObject, outputs : OutputDescription) =
            x.EvaluateAlways caller (fun () ->
                let fbo = unbox<Framebuffer> outputs.framebuffer
                use token = device.Token

                let colors = clearColors |> Array.map (fun (i,c) -> i, c.GetValue x)
                let depth = clearDepth.GetValue x
                let stencil = match clearStencil with | Some c -> c.GetValue(x) |> Some | _ -> None


                token.enqueue {
                    let views = fbo.ImageViews
                    for (index, color) in colors do
                        let image = views.[index].Image
                        do! Command.ClearColor(image.[ImageAspect.Color], color)

                    if renderPassDepthAspect <> ImageAspect.None then
                        let image = views.[views.Length-1].Image
                        match depth, stencil with
                            | Some d, Some s    -> do! Command.ClearDepthStencil(image.[renderPassDepthAspect], d, s)
                            | Some d, None      -> do! Command.ClearDepthStencil(image.[ImageAspect.Depth], d, 0u)
                            | None, Some s      -> do! Command.ClearDepthStencil(image.[ImageAspect.Stencil], 0.0, s)
                            | None, None        -> ()
                }
                token.Sync()
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
