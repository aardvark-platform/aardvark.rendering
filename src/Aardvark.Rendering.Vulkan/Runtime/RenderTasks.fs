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
    type AbstractVulkanRenderTask(device : Device, renderPass : RenderPass, config : IMod<BackendConfiguration>, shareTextures : bool, shareBuffers : bool) =
        inherit AbstractRenderTask()

        let locks = ReferenceCountingSet<ILockedResource>()

        let user =
            { new IResourceUser with
                member x.AddLocked l = lock locks (fun () -> locks.Add l |> ignore)
                member x.RemoveLocked l = lock locks (fun () -> locks.Remove l |> ignore)
            }

        let manager = new ResourceManager(user, device)

        let fboSignature = renderPass :> IFramebufferSignature
        let device = device
        let manager = manager
        let runtimeStats = NativePtr.alloc 1
        let mutable isDisposed = false

        let scope =
            {
                runtimeStats = runtimeStats
            }

        member x.Scope = scope


        abstract member Perform : AdaptiveToken * RenderToken * Framebuffer -> unit
        abstract member Release : unit -> unit

        member x.UseRender (action : unit -> 'r) =
            let locks = lock locks (fun () -> Seq.toList locks)
            for l in locks do l.Lock.Enter(ResourceUsage.Render, l.OnLock)
            try
                action()
            finally 
                for l in locks do l.Lock.Exit(l.OnUnlock)

        member x.Config = config
        member x.Device = device
        member x.ResourceManager = manager

        override x.Dispose() =
            if not isDisposed then
                isDisposed <- true
                //Log.warn "manager dispose not implemented"
                manager.Dispose()
                x.Release()
                NativePtr.free runtimeStats

        override x.FramebufferSignature = Some fboSignature
        override x.Runtime = Some device.Runtime

        override x.Perform(token : AdaptiveToken, rt : RenderToken, desc : OutputDescription) =
            let fbo = desc.framebuffer // TODO: fix outputdesc
            if not <| fboSignature.IsAssignableFrom fbo.Signature then
                failwithf "incompatible FramebufferSignature\nexpected: %A but got: %A" fboSignature fbo.Signature

            let fbo =
                match fbo with
                    | :? Framebuffer as fbo -> fbo
                    | _ -> failwithf "unsupported framebuffer: %A" fbo

            NativePtr.write runtimeStats V2i.Zero
            let r = x.Perform(token, rt, fbo)
            let rts = NativePtr.read runtimeStats
            rt.AddDrawCalls(rts.X, rts.Y)


    [<AbstractClass>]
    type DependentCommandBuffer(renderPass : RenderPass, pool : CommandPool) as this =
        inherit AdaptiveObject()

        let objects = CSet.empty
        let mutable initialized = false
        let cmd = pool.CreateCommandBuffer CommandBufferLevel.Secondary

        let mutable lastViewports = [||]
        let mutable version = 0
        let mutable commandVersion = -1
        let mutable resourceHandlesChanged = false

        let resources = ResourceSet()

        static let inPlaceResources =
            HashSet.ofList [
                typeof<UniformBuffer>
            ]

        member private x.init(token : AdaptiveToken) =
            if not initialized then
                initialized <- true
                x.Init(token, objects)

        member x.Add (token : AdaptiveToken, o : PreparedMultiRenderObject) =
            x.init(token.WithCaller x)
            version <- version + 1

            for co in o.Children do
                for r in co.resources do
                    resources.Add r

            transact (fun () ->
                objects.Add o |> ignore
            )

        member x.Remove(token : AdaptiveToken, o : PreparedMultiRenderObject) =
            x.init(token.WithCaller x)
            version <- version + 1

            for co in o.Children do
                for r in co.resources do
                    resources.Remove r

            transact (fun () ->
                objects.Remove o |> ignore
            )

        abstract member Init : AdaptiveToken * aset<PreparedMultiRenderObject> -> unit
        abstract member UpdateProgram : AdaptiveToken * RenderToken -> unit
        abstract member Fill : AdaptiveToken * RenderToken * CommandBuffer -> unit
        abstract member Release : unit -> unit

        member x.Dispose() =
            if initialized then
                initialized <- false
                x.Release()
                transact (fun () -> objects.Clear())
                cmd.Dispose()

        member x.Update(caller : AdaptiveToken, token : RenderToken) =
            x.EvaluateAlways caller (fun caller ->
                x.init(caller)
                if x.OutOfDate then
                    resourceHandlesChanged <- resources.Update(caller) || resourceHandlesChanged
                    x.UpdateProgram(caller, token)
            )

        member x.GetCommandBuffer(caller : AdaptiveToken, token : RenderToken, viewports : Box2i[]) =
            x.Update(caller, token)
            let versionChanged      = version <> commandVersion
            let viewportChanged     = lastViewports <> viewports

            if resourceHandlesChanged || versionChanged || viewportChanged then
                Log.line "{ handles: %A; version: %A; viewport: %A }" resourceHandlesChanged versionChanged viewportChanged
                resourceHandlesChanged <- false
                cmd.Begin(renderPass, CommandBufferUsage.RenderPassContinue)
                cmd.enqueue {
                    do! Command.SetViewports viewports
                    do! Command.SetScissors viewports
                }
                this.Fill(caller, token, cmd)
                cmd.End()

                lastViewports <- viewports
                commandVersion <- version

            cmd

        interface IDisposable with
            member x.Dispose() = x.Dispose()



    type SortKey = list<int>

    type ProjectionComparer(projections : list<RenderObject -> obj>) =

        let rec getRenderObject (ro : IRenderObject) =
            match ro with
                | :? RenderObject as ro -> ro
                | :? MultiRenderObject as ro -> ro.Children |> List.head |> getRenderObject
                | :? PreparedRenderObject as ro -> ro.original
                | :? PreparedMultiRenderObject as ro -> ro.First.original
                | _ -> failwithf "[ProjectionComparer] unknown RenderObject: %A" ro

        let ids = ConditionalWeakTable<obj, ref<int>>()
        let mutable currentId = 0
        let getId (m : obj) =
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

        override x.Init(token : AdaptiveToken, objects : aset<PreparedMultiRenderObject>) =
            scope <- { runtimeStats = NativePtr.alloc 1 }
            objectsWithKeys <- objects |> ASet.map (fun o -> (o :> IRenderObject, o))
            let config = config.GetValue token
            reinit x config

        override x.UpdateProgram(token : AdaptiveToken, t) =
            let config = config.GetValue token
            reinit x config
            program.Update token |> ignore

        override x.Fill(token : AdaptiveToken, t : RenderToken, cmd : CommandBuffer) =
            NativePtr.write scope.runtimeStats V2i.Zero

            program.Run(cmd.Handle)

            let calls = NativePtr.read scope.runtimeStats
            t.AddDrawCalls(calls.X, calls.Y)


        override x.Release() =
            if not (NativePtr.isNull scope.runtimeStats) then
                NativePtr.free scope.runtimeStats
                scope <- { runtimeStats = NativePtr.zero }

            if hasProgram then
                program.Dispose()
                program <- Unchecked.defaultof<_>
                hasProgram <- false


            objectsWithKeys <- Unchecked.defaultof<_>

    type RenderTask(device : Device, renderPass : RenderPass, objects : aset<IRenderObject>, config : IMod<BackendConfiguration>, shareTextures : bool, shareBuffers : bool) as this =
        inherit AbstractVulkanRenderTask(device, renderPass, config, shareTextures, shareBuffers)


        let mutable currentToken = Unchecked.defaultof<AdaptiveToken>

        let prepare (o : IRenderObject) =
            this.ResourceManager.PrepareRenderObject(currentToken, renderPass, o, this.HookRenderObject)

        let preparedCache = Cache<IRenderObject, PreparedMultiRenderObject>(prepare)
        let objectReader = objects.GetReader()
        //let device = this.ResourceManager

        let preparedObjectReader =
            { new AbstractReader<hdeltaset<PreparedMultiRenderObject>>(Ag.emptyScope, HDeltaSet.monoid) with
                member x.Compute(token) =
                    let deltas = objectReader.GetOperations token

                    deltas |> HDeltaSet.map (fun op ->
                        match op with
                            | Add(_,v) -> 
                                let res = preparedCache.Invoke(v)
                                Add(res)
                            | Rem(_,v) -> 
                                let deleted, res = preparedCache.RevokeAndGetDeleted(v)
                                if deleted then res.Dispose()
                                Rem(res)
                    )

                member x.Release() =
                    preparedCache.Clear(fun o -> o.Dispose())
                    objectReader.Dispose()
            }


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

        let update ( x : AdaptiveToken ) =
            let t = AdaptiveToken(null, HashSet(), null)
            currentToken <- t
            let deltas = preparedObjectReader.GetOperations x

            for d in deltas do 
                match d with
                    | Add(_,v) ->
                        let task = getCommandBuffer v.RenderPass
                        task.Add(x, v)
                    | Rem(_,v) ->
                        let task = getCommandBuffer v.RenderPass
                        task.Remove(x, v)

            for l in t.Locked do t.ExitRead l

        

        override x.Use (f : unit -> 'a) =
            lock x (fun () ->
                x.UseRender (fun () ->
                    f()
                )
            )

        override x.PerformUpdate(caller, token) =
            update caller
            for (_,t) in Map.toSeq commandBuffers do
                t.Update(caller,token)


        override x.Perform (caller : AdaptiveToken, token : RenderToken, fbo : Framebuffer) =
            
            use devToken = device.Token

            let bounds = Box2i(V2i.Zero, fbo.Size - V2i.II)
            let vp = Array.create renderPass.AttachmentCount bounds

            update caller
            let commandBuffers =
                commandBuffers 
                    |> Map.toList 
                    |> List.map snd
                    |> List.map (fun dc ->
                        let cmd = dc.GetCommandBuffer(caller, token, vp)
                        cmd
                    )


            x.UseRender (fun () ->
                devToken.enqueue {
                    
                    let oldLayouts = Array.zeroCreate fbo.ImageViews.Length
                    for i in 0 .. fbo.ImageViews.Length - 1 do
                        let img = fbo.ImageViews.[i].Image
                        oldLayouts.[i] <- img.Layout
                        if VkFormat.hasDepth img.Format then
                            do! Command.TransformLayout(img, VkImageLayout.DepthStencilAttachmentOptimal)
                        else
                            do! Command.TransformLayout(img, VkImageLayout.ColorAttachmentOptimal)

                    for cmd in commandBuffers do
                        do! Command.Barrier
                        do! Command.BeginPass (renderPass, fbo, false)
                        do! Command.Execute cmd
                        do! Command.EndPass

                    for i in 0 .. fbo.ImageViews.Length - 1 do
                        let img = fbo.ImageViews.[i].Image
                        do! Command.TransformLayout(img, oldLayouts.[i])
                }

                // really run the stuff
                devToken.Sync()
            )


        override x.Release() =
            preparedObjectReader.Dispose()
            commandBuffers |> Map.iter (fun _ c -> c.Dispose())
            pool.Dispose()
            commandBuffers <- Map.empty


    type ClearTask(device : Device, renderPass : RenderPass, clearColors : Map<Symbol, IMod<C4f>>, clearDepth : IMod<Option<float>>, clearStencil : Option<IMod<uint32>>) =
        inherit AdaptiveObject()
        static let depthStencilFormats =
            HashSet.ofList [
                RenderbufferFormat.Depth24Stencil8
                RenderbufferFormat.Depth32fStencil8
                RenderbufferFormat.DepthStencil
            ]
        
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

        member x.Run(caller : AdaptiveToken, t : RenderToken, outputs : OutputDescription) =
            x.EvaluateAlways caller (fun caller ->
                let fbo = unbox<Framebuffer> outputs.framebuffer
                use token = device.Token

                let colors = clearColors |> Array.map (fun (i,c) -> i, c.GetValue caller)
                let depth = clearDepth.GetValue caller
                let stencil = match clearStencil with | Some c -> c.GetValue(caller) |> Some | _ -> None


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
            )

        interface IRenderTask with
            member x.Update(c, t) = ()
            member x.Run(c,t,o) = x.Run(c,t,o)
            member x.Dispose() = ()
            member x.FrameId = 0UL
            member x.FramebufferSignature = Some (renderPass :> _)
            member x.Runtime = Some device.Runtime
            member x.Use f = lock x f

