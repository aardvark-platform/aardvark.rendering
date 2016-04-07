namespace Aardvark.Rendering.Vulkan

#nowarn "9"
#nowarn "51"

open System
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Rendering
open System.Collections.Generic
open System.Threading
open Aardvark.Rendering.Vulkan
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base.Runtime

[<AutoOpen>]
module private Compiler = 

    let instructionToCall (i : Instruction) : NativeCall =
        i.FunctionPointer, i.Arguments

    let callToInstruction (ctx : InstructionContext) (ptr : nativeint, args : obj[]) : Instruction =
        new Instruction(ctx, ptr, args)


    let compile (ctx : InstructionContext) (prev : Option<PreparedRenderObject>) (self : PreparedRenderObject) =
        let code =
            [
                let prog = self.program.Handle.GetValue()
                match prev with
                    | None ->
                        yield self.pipeline.Handle |> Mod.map (ctx.BindPipeline)

                        yield self.descriptorSets |> List.map (fun d -> d.Handle) |> Mod.mapN (fun handles ->
                            ctx.BindDescriptorSets(prog.PipelineLayout, Seq.toArray handles, 0)
                        )

                        yield self.vertexBuffers |> Array.map (fun (v,_) -> v.Handle) |> Mod.mapN (fun handles ->
                            ctx.BindVertexBuffers(Seq.toArray handles, 0)
                        )

                        match self.indexBuffer with
                            | Some ib ->
                                yield ib.Handle |> Mod.map (fun ib -> ctx.BindIndexBuffer(ib, 0))
                                yield self.DrawCallInfos |> Mod.map (List.collect (fun draw ->
                                    //ctx.DrawIndexed(draw.FirstIndex, draw.FaceVertexCount, draw.FirstInstance, draw.InstanceCount)
                                    ctx.DrawIndexed(draw.FirstIndex, draw.FaceVertexCount, draw.FirstInstance, draw.InstanceCount, 0)
                                ))
                            | _ ->
                                yield self.DrawCallInfos |> Mod.map (List.collect (fun draw ->
                                    ctx.Draw(draw.FirstIndex, draw.FaceVertexCount, draw.FirstInstance, draw.InstanceCount)
                                ))

                    | Some prev ->
                        if prev.pipeline <> self.pipeline then
                            yield self.pipeline.Handle |> Mod.map (ctx.BindPipeline)

                        if prev.descriptorSets <> self.descriptorSets then
                            yield self.descriptorSets |> List.map (fun d -> d.Handle) |> Mod.mapN (fun handles ->
                                ctx.BindDescriptorSets(prog.PipelineLayout, Seq.toArray handles, 0)
                            )
                        if prev.vertexBuffers <> self.vertexBuffers then
                            yield self.vertexBuffers |> Array.map (fun (v,_) -> v.Handle) |> Mod.mapN (fun handles ->
                                ctx.BindVertexBuffers(Seq.toArray handles, 0)
                            )

                        match self.indexBuffer with
                            | Some ib ->
                                yield ib.Handle |> Mod.map (fun ib -> ctx.BindIndexBuffer(ib, 0))
                                yield self.DrawCallInfos |> Mod.map (List.collect (fun draw ->
                                    ctx.DrawIndexed(draw.FirstIndex, draw.FaceVertexCount, draw.FirstInstance, draw.InstanceCount, 0)
                                ))
                            | _ ->
                                yield self.DrawCallInfos |> Mod.map (List.collect (fun draw ->
                                    ctx.Draw(draw.FirstIndex, draw.FaceVertexCount, draw.FirstInstance, draw.InstanceCount)
                                ))
            ]

        new AdaptiveCode<Instruction>(code) :> IAdaptiveCode<_>


type ClearTask(manager : ResourceManager, renderPass : RenderPass, clearColors : list<IMod<C4f>>, clearDepth : IMod<Option<float>>, clearStencil : Option<IMod<uint32>>) as this =
    inherit AdaptiveObject()
    let context = manager.Context
    let pool = context.Device.CreateCommandPool(context.DefaultQueue.Family)
    let cmd = pool.CreateCommandBuffer()

    let clearColors = 
        clearColors |> List.toArray |> Array.mapi (fun i c ->
            c |> Mod.map (fun c -> 
                VkClearColorValue(float32 = V4f(c.R, c.G, c.B, c.A))
            )
        )

    let clearDepthStencil =
        if renderPass.HasDepth then
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

    do 
        for c in clearColors do c.AddOutput this
        match clearDepthStencil with
            | Some cd -> cd.AddOutput this
            | _ -> ()

    member x.Run(caller : IAdaptiveObject, outputs : OutputDescription) =
        x.EvaluateAlways caller (fun () ->
            let fbo = unbox<Framebuffer> outputs.framebuffer

            cmd.Begin()
//            cmd.BeginPass(renderPass, fbo)

            let mutable rect = VkRect3D(VkOffset3D(0,0,0), VkExtent3D(fbo.Size.X, fbo.Size.Y,1))
            for i in 0..fbo.Attachments.Length-1 do
                let image = fbo.Attachments.[i].Image
                
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
                            VkRaw.vkCmdClearDepthStencilImage(
                                cmd.Handle, image.Handle, image.Layout, &&clearValue, 1u, &&range
                            )
                        | None ->
                            ()
                else
                    let mutable clearValue = clearColors.[i].GetValue()
                    let mutable range = VkImageSubresourceRange(VkImageAspectFlags.ColorBit, 0u, 1u, 0u, 1u)
                    VkRaw.vkCmdClearColorImage(
                        cmd.Handle, image.Handle, image.Layout, &&clearValue, 1u, &&range
                    )

            //cmd.EndPass()
            cmd.End()

            context.DefaultQueue.SubmitAndWait [| cmd |]

            RenderingResult(fbo, FrameStatistics.Zero)
        )

    interface IRenderTask with
        member x.Run(c,o) = x.Run(c,o)
        member x.Dispose() = ()
        member x.FrameId = 0UL
        member x.FramebufferSignature = renderPass :> _
        member x.Runtime = Some manager.Runtime

type RenderTask(manager : ResourceManager, fboSignature : RenderPass, objects : aset<IRenderObject>, config : BackendConfiguration) as this =
    inherit AbstractRenderTaskWithResources(
        manager,
        fboSignature
    )

    let ctx = manager.Context
    let ictx = ctx.InstructionContext
    let pool = ctx.Device.CreateCommandPool(ctx.DefaultQueue.Family)

    let getRenderObjectResources (prep : PreparedRenderObject) : seq<IResource> =
        seq {
            yield prep.pipeline :> IResource
            yield! prep.descriptorResources
            yield prep.program :> IResource


            for d in prep.descriptorSets do
                yield d :> IResource

            match prep.indexBuffer with
                | Some ib -> yield ib :> IResource
                | _ -> ()

            for (b,_) in prep.vertexBuffers do
                yield b :> IResource
                
        }


    let prepareRenderObject (ro : IRenderObject) =
        let prepared = 
            match ro with
                | :? RenderObject as r ->
                    manager.PrepareRenderObject(fboSignature, r)

                | :? PreparedRenderObject as prep ->
                    prep
                | _ ->
                    failwithf "[RenderTask] unsupported IRenderObject: %A" ro

        for r in getRenderObjectResources prepared do
            this.AddInput(r)

        prepared

    //TODO: find a way to destroy sortKeys appropriately without using ConditionalWeakTable
    let mutable currentId = 0L
    let idCache = ConditionalWeakTable<IMod, ref<uint64>>()

    let getId (m : IMod) =
        match idCache.TryGetValue m with
            | (true, r) -> !r
            | _ ->
                let r = ref (Interlocked.Increment &currentId |> uint64)
                idCache.Add(m, r)
                !r

    let createSortKey (prep : PreparedRenderObject) =
        match config.sorting with
            | Grouping projections ->
                let ids = projections |> List.map (fun f -> f prep.original |> getId)
                prep.RenderPass::ids

            | _ ->
                failwithf "[RenderTask] unsupported sorting: %A" config.sorting


    let preparedObjects = objects |> ASet.mapUse prepareRenderObject |> ASet.map (fun prep -> createSortKey prep, prep)

    let mutable hasProgram = true
    let executionTime = System.Diagnostics.Stopwatch()



    let program : IAdaptiveProgram<VkCommandBuffer> = 
        let handler = 
            FragmentHandler.warpDifferential 
                instructionToCall 
                (callToInstruction ictx)
                (compile ictx)
                (FragmentHandler.native 8) // at most 8 args (vkBindDescriptorSets)

        AdaptiveProgram.custom 
            Comparer<_>.Default 
            handler 
            preparedObjects


              
              
    let commandBufferCache = Dict<Framebuffer, IMod<CommandBuffer>>()
        
         
    let getOrCreateCommandBuffer (pass : RenderPass) (fbo : Framebuffer) =
        commandBufferCache.GetOrCreate(fbo, fun fbo ->
            let cmd = pool.CreateCommandBuffer(true)
            Mod.custom (fun self ->
                program.Update(self) |> ignore

                cmd.Begin(true)
                cmd.BeginPass(pass, fbo)

                cmd.SetViewport(fbo.Size)
                cmd.SetScissor(fbo.Size)
                cmd.SetBlendColor(C4f.White)
                cmd.SetLineWidth(1.0)
                cmd.SetDepthBias(0.0, 1.0, 0.0)
                cmd.SetDepthBounds(0.0, 1.0)
                cmd.SetStencil(0xffu, 0xffu, 0u)

                    
                let sw = System.Diagnostics.Stopwatch()
                sw.Start()
                program.Run(cmd.Handle)
                Log.line "updated cmd buffer: %.3fms" sw.Elapsed.TotalMilliseconds

                cmd.EndPass()
                cmd.End()
                sw.Stop()


                cmd
            )
        )


    override x.Run(pass, outputs) =

        let mutable stats = x.UpdateDirtyResources()

        if hasProgram then
            let programUpdateStats = program.Update x
            stats <- { 
                stats with 
                    AddedRenderObjects = float programUpdateStats.AddedFragmentCount
                    RemovedRenderObjects = float programUpdateStats.RemovedFragmentCount
                    InstructionUpdateCount = 0.0 // TODO!!
                    InstructionUpdateTime = 
                        programUpdateStats.DeltaProcessTime +
                        programUpdateStats.WriteTime +
                        programUpdateStats.CompileTime
            }

        executionTime.Restart()
        if hasProgram then
            let fbo = outputs.framebuffer |> unbox<Framebuffer>
            let cmd = getOrCreateCommandBuffer pass fbo
            ctx.DefaultQueue.SubmitAndWait [| Mod.force cmd |]

        executionTime.Stop()


        stats <- stats + x.GetStats()


        { stats with 
            ExecutionTime = executionTime.Elapsed 
        }

    override x.Dispose() =
        program.Dispose()


