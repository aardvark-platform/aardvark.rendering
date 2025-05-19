namespace Aardvark.Rendering.Vulkan.Raytracing

open System

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open Aardvark.Rendering.Vulkan.Raytracing
open Aardvark.Rendering.Raytracing
open KHRRayTracingPipeline
open FShade

open FSharp.Data.Adaptive
open Microsoft.FSharp.NativeInterop

#nowarn "9"

[<AutoOpen>]
module private RaytracingTaskInternals =
    open System.Collections
    open System.Collections.Generic

    type private CommandList(pShaderBindingTable : nativeptr<ShaderBindingTableHandle>) =
        let cmds = List<Command>()
        let pRaygenAddress : nativeptr<VkStridedDeviceAddressRegionKHR> = NativePtr.cast pShaderBindingTable
        let pMissAddress = pRaygenAddress |> NativePtr.step 1
        let pHitAddress = pRaygenAddress |> NativePtr.step 2
        let pCallableAddress = pRaygenAddress |> NativePtr.step 3

        member x.Clear() = cmds.Clear()

        member x.BindPipeline(pPipeline : nativeptr<VkPipeline>) =
            cmds.Add (
                { new Command() with
                    member x.Compatible = QueueFlags.Compute
                    member x.Enqueue cmd =
                        cmd.AppendCommand()
                        VkRaw.vkCmdBindPipeline(cmd.Handle, VkPipelineBindPoint.RayTracingKhr, pPipeline.Value)
                }
            )

        member x.BindDescriptorSets(pDescriptorSets : nativeptr<DescriptorSetBinding>) =
            cmds.Add (
                { new Command() with
                    member x.Compatible = QueueFlags.Compute
                    member x.Enqueue cmd =
                        let descriptorSets = NativePtr.toByRef pDescriptorSets

                        cmd.AppendCommand()
                        VkRaw.vkCmdBindDescriptorSets(
                            cmd.Handle, VkPipelineBindPoint.RayTracingKhr, descriptorSets.Layout,
                            uint32 descriptorSets.FirstIndex, uint32 descriptorSets.Count, descriptorSets.Sets,
                            0u, NativePtr.zero
                        )
                }
            )

        member x.TraceRays(count : V3i) =
            cmds.Add (
                { new Command() with
                    member x.Compatible = QueueFlags.Compute
                    member x.Enqueue cmd =
                        cmd.AppendCommand()

                        VkRaw.vkCmdTraceRaysKHR(
                            cmd.Handle, pRaygenAddress, pMissAddress, pHitAddress, pCallableAddress,
                            uint32 count.X, uint32 count.Y, uint32 count.Z
                        )
                }
            )

        member x.Sync(buffer : Buffer, srcAccess : ResourceAccess, dstAccess : ResourceAccess) =
            cmds.Add <| Command.Sync(buffer, VkAccessFlags.ofResourceAccess srcAccess, VkAccessFlags.ofResourceAccess dstAccess)

        member x.Sync(image : ImageSubresourceRange, layout : TextureLayout, srcAccess : ResourceAccess, dstAccess : ResourceAccess) =
            let layout = VkImageLayout.ofTextureLayout layout
            let srcAccess = VkAccessFlags.ofResourceAccess srcAccess
            let dstAccess = VkAccessFlags.ofResourceAccess dstAccess
            cmds.Add <| Command.Sync(image, layout, srcAccess, dstAccess)

        member x.TransformLayout(image : ImageSubresourceRange, source : VkImageLayout, target : VkImageLayout) =
            if source <> target then
                cmds.Add <| Command.TransformLayout(image, source, target)

        member x.Enqueue(cmd : RaytracingCommand) =
            match cmd with
            | RaytracingCommand.TraceRaysCmd count ->
                x.TraceRays(count)

            | RaytracingCommand.SyncBufferCmd (buffer, srcAccess, dstAccess) ->
                let buffer = unbox<Buffer> buffer
                x.Sync(buffer, srcAccess, dstAccess)

            | RaytracingCommand.SyncTextureCmd (range, layout, srcAccess, dstAccess) ->
                x.Sync(
                    ImageSubresourceRange.ofTextureRange range,
                    layout, srcAccess, dstAccess
                )

            | RaytracingCommand.TransformLayoutCmd (range, srcLayout, dstLayout) ->
                x.TransformLayout(
                    ImageSubresourceRange.ofTextureRange range,
                    VkImageLayout.ofTextureLayout srcLayout, VkImageLayout.ofTextureLayout dstLayout
                )

        interface IEnumerable with
            member x.GetEnumerator() = cmds.GetEnumerator()

        interface IEnumerable<Command> with
            member x.GetEnumerator() = cmds.GetEnumerator()

    type CompiledCommand(manager : ResourceManager, resources : ResourceLocationSet, pipeline : RaytracingPipelineState, input : alist<RaytracingCommand>) =
        let reader = input.GetReader()
        let preparedPipeline = manager.PrepareRaytracingPipeline(pipeline)

        do for r in preparedPipeline.Resources do
            resources.Add(r)

        let compiled = CommandList preparedPipeline.ShaderBindingTable.Pointer

        member x.Effect = pipeline.Effect

        member x.Commands = compiled :> seq<_>

        member x.Update(token : AdaptiveToken) =
            let deltas = reader.GetChanges(token)

            if deltas.Count > 0 then
                compiled.Clear()
                compiled.BindPipeline preparedPipeline.Pipeline.Pointer
                compiled.BindDescriptorSets preparedPipeline.DescriptorSets.Pointer

                for cmd in reader.State do
                    compiled.Enqueue cmd

                true
            else
                false

        member x.Dispose() =
            for r in preparedPipeline.Resources do
                resources.Remove(r)

            preparedPipeline.Dispose()

        interface IDisposable with
            member x.Dispose() = x.Dispose()


type RaytracingTask(manager : ResourceManager, pipeline : RaytracingPipelineState, commands : alist<RaytracingCommand>) =
    inherit AdaptiveObject()

    let device = manager.Device

    // Use graphics queue family if it supports compute (which is guaranteed by Vulkan spec).
    // Otherwise, we need queue family ownership transfers which are not implemented.
    // On NVIDIA GPUs we can get away with using a dedicated compute family without any ownership transfers.
    let family, getDeviceToken =
        if device.GraphicsFamily.Flags.HasFlag QueueFlags.Compute then
            device.GraphicsFamily, fun () -> device.Token
        else
            device.ComputeFamily, fun () -> device.ComputeToken

    let pool = family.CreateCommandPool()
    let inner = pool.CreateCommandBuffer(CommandBufferLevel.Secondary)

    let effect =
        pipeline.Effect
        |> ShaderDebugger.tryRegisterRaytracingEffect
        |> Option.defaultWith (fun _ -> AVal.constant pipeline.Effect)

    let resources = ResourceLocationSet()
    let mutable compiled = new CompiledCommand(manager, resources, pipeline, commands)
    let mutable currentEffect = compiled.Effect

    let updateCommandResources (t : AdaptiveToken) (rt : RenderToken) =
        let effect = effect.GetValue t

        if effect <> currentEffect then
            currentEffect <- effect

            try
                let pipeline = { pipeline with Effect = effect }
                let recompiled = new CompiledCommand(manager, resources, pipeline, commands)
                compiled.Dispose()
                compiled <- recompiled
            with _ ->
                Log.warn $"[Vulkan] Failed to update raytracing effect"

        resources.Use(t, rt, fun resourcesChanged ->
            let commandChanged =
                compiled.Update t

            if commandChanged || resourcesChanged then
                if device.DebugConfig.PrintRenderTaskRecompile then
                    let cause =
                        String.concat "; " [
                            if commandChanged then yield "content"
                            if resourcesChanged then yield "resources"
                        ]
                        |> sprintf "{ %s }"

                    Log.line "[Raytracing] recompile commands: %s" cause

                pool.Reset()
                inner.Begin(CommandBufferUsage.None, true)

                for cmd in compiled.Commands do
                    inner.Enqueue cmd

                inner.End()
        )

    member val Name : string = null with get, set

    member x.Update(token : AdaptiveToken, renderToken : RenderToken) =
        x.EvaluateIfNeeded token () (fun token ->
            use _ = renderToken.Use()
            use _ = getDeviceToken()
            updateCommandResources token renderToken
        )

    member x.Run(token : AdaptiveToken, renderToken : RenderToken) =
        x.EvaluateAlways token (fun token ->
            use _ = renderToken.Use()
            use dt = getDeviceToken()
            updateCommandResources token renderToken

            let vulkanQueries = renderToken.GetVulkanQueries(onlyTimeQueries = true)

            dt.perform {
                do! Command.BeginLabel(x.Name |?? "Raytracing Task", DebugColor.RaytracingTask)

                for q in vulkanQueries do
                    do! Command.Begin q

                do! Command.Execute inner

                for q in vulkanQueries do
                    do! Command.End q

                do! Command.EndLabel()
            }
        )

    member x.Dispose() =
        transact (fun _ ->
            compiled.Dispose()
            inner.Dispose()
            pool.Dispose()
        )

    interface IRaytracingTask with
        member x.Name with get() = x.Name and set name = x.Name <- name
        member x.Update(t, rt) = x.Update(t, rt)
        member x.Run(t, rt) = x.Run(t, rt)

    interface IDisposable with
        member x.Dispose() = x.Dispose()