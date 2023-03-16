namespace Aardvark.Rendering.Vulkan.Raytracing

open System

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open Aardvark.Rendering.Vulkan.Raytracing
open Aardvark.Rendering.Vulkan.KHRRayTracingPipeline
open Aardvark.Rendering.Vulkan.KHRAccelerationStructure
open Aardvark.Rendering.Vulkan.KHRBufferDeviceAddress
open Aardvark.Rendering.Raytracing

open FSharp.Data.Adaptive
open Microsoft.FSharp.NativeInterop

#nowarn "9"

[<AutoOpen>]
module private ``Trace Command Extensions`` =

    type Command with

        static member BindRaytracingPipeline(pPipeline : nativeptr<VkPipeline>, pDescriptorSets : nativeptr<DescriptorSetBinding>) =
            { new Command() with
                member x.Compatible = QueueFlags.Compute
                member x.Enqueue cmd =
                    let descriptorSets = NativePtr.toByRef pDescriptorSets

                    cmd.AppendCommand()
                    VkRaw.vkCmdBindPipeline(cmd.Handle, VkPipelineBindPoint.RayTracingKhr, pPipeline.Value)

                    cmd.AppendCommand()
                    VkRaw.vkCmdBindDescriptorSets(
                        cmd.Handle, VkPipelineBindPoint.RayTracingKhr, descriptorSets.Layout,
                        uint32 descriptorSets.FirstIndex, uint32 descriptorSets.Count, descriptorSets.Sets,
                        0u, NativePtr.zero
                    )

                    []
            }

        static member TraceRays(pShaderBindingTable : nativeptr<ShaderBindingTableHandle>, width : uint32, height : uint32, depth : uint32) =
            { new Command() with
                member x.Compatible = QueueFlags.Compute
                member x.Enqueue cmd =
                    cmd.AppendCommand()

                    let pRaygenAddress : nativeptr<VkStridedDeviceAddressRegionKHR> = NativePtr.cast pShaderBindingTable
                    let pMissAddress = pRaygenAddress |> NativePtr.step 1
                    let pHitAddress = pRaygenAddress |> NativePtr.step 2
                    let pCallableAddress = pRaygenAddress |> NativePtr.step 3
                    VkRaw.vkCmdTraceRaysKHR(cmd.Handle, pRaygenAddress, pMissAddress, pHitAddress, pCallableAddress, width, height, depth)

                    []
            }

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module RaytracingCommand =

        let toCommand (pShaderBindingTable : nativeptr<ShaderBindingTableHandle>) = function
            | RaytracingCommand.TraceRaysCmd(size) ->
                Command.TraceRays(pShaderBindingTable, uint32 size.X, uint32 size.Y, uint32 size.Z)

            | RaytracingCommand.SyncBufferCmd(buffer, src, dst) ->
                let buffer = unbox<Buffer> buffer
                Command.Sync(buffer, VkAccessFlags.ofResourceAccess src, VkAccessFlags.ofResourceAccess dst)

            | RaytracingCommand.SyncTextureCmd(texture, src, dst) ->
                let image = unbox<Image> texture
                Command.Sync(image.[TextureAspect.Color], image.Layout, VkAccessFlags.ofResourceAccess src, VkAccessFlags.ofResourceAccess dst)

            | RaytracingCommand.TransformLayoutCmd(texture, src, dst) ->
                let image = unbox<Image> texture
                let aspect = VkFormat.toAspect image.Format
                let levels = Range1i(0, image.MipMapLevels - 1)
                let slices = Range1i(0, image.Layers - 1)

                Command.TransformLayout(
                    image.[unbox (int aspect), levels.Min .. levels.Max, slices.Min .. slices.Max],
                    VkImageLayout.ofTextureLayout src, VkImageLayout.ofTextureLayout dst
                )


[<AutoOpen>]
module private RaytracingTaskInternals =

    type CompiledCommand(manager : ResourceManager, resources : ResourceLocationSet, pipelineState : RaytracingPipelineState, input : alist<RaytracingCommand>) =
        let reader = input.GetReader()
        let mutable compiled = [||]

        let preparedPipeline = manager.PrepareRaytracingPipeline(pipelineState)

        do for r in preparedPipeline.Resources do
            resources.Add(r)

        member x.Commands = compiled

        member x.Update(token : AdaptiveToken) =
            let deltas = reader.GetChanges(token)

            if deltas.Count > 0 then
                let commands = reader.State.AsArray

                Array.Resize(&compiled, commands.Length + 1)
                compiled.[0] <- Command.BindRaytracingPipeline(preparedPipeline.Pipeline.Pointer, preparedPipeline.DescriptorSets.Pointer)

                for i = 0 to commands.Length - 1 do
                    compiled.[i + 1] <- commands.[i] |> RaytracingCommand.toCommand preparedPipeline.ShaderBindingTable.Pointer

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

    let pool = family.CreateCommandPool(CommandPoolFlags.ResetBuffer)
    let cmd = pool.CreateCommandBuffer(CommandBufferLevel.Primary)
    let inner = pool.CreateCommandBuffer(CommandBufferLevel.Secondary)

    let resources = ResourceLocationSet()
    let compiled = new CompiledCommand(manager, resources, pipeline, commands)

    let updateCommandResources (t : AdaptiveToken) (rt : RenderToken) =
        use tt = getDeviceToken()

        resources.Use(t, rt, fun resourcesChanged ->
            let commandChanged =
                compiled.Update t

            tt.Sync()

            if commandChanged || resourcesChanged then
                if device.DebugConfig.PrintRenderTaskRecompile then
                    let cause =
                        String.concat "; " [
                            if commandChanged then yield "content"
                            if resourcesChanged then yield "resources"
                        ]
                        |> sprintf "{ %s }"

                    Log.line "[Raytracing] recompile commands: %s" cause

                inner.Begin CommandBufferUsage.None

                inner.enqueue {
                    for cmd in compiled.Commands do
                        do! cmd
                }

                inner.End()
        )

    member x.Update(token : AdaptiveToken, renderToken : RenderToken) =
        x.EvaluateIfNeeded token () (fun token ->
            use __ = renderToken.Use()
            updateCommandResources token renderToken
        )

    member x.Run(token : AdaptiveToken, renderToken : RenderToken) =
        x.EvaluateAlways token (fun token ->
            use __ = renderToken.Use()
            updateCommandResources token renderToken

            let vulkanQueries = renderToken.GetVulkanQueries(onlyTimeQueries = true)
            cmd.Begin CommandBufferUsage.OneTimeSubmit

            for q in vulkanQueries do
                q.Begin cmd

            cmd.enqueue {
                do! Command.Execute inner
            }

            for q in vulkanQueries do
                q.End cmd

            cmd.End()

            family.RunSynchronously(cmd)
        )

    member x.Dispose() =
        transact (fun _ ->
            compiled.Dispose()
            cmd.Dispose()
            inner.Dispose()
            pool.Dispose()
        )

    interface IRaytracingTask with
        member x.Update(t, rt) = x.Update(t, rt)
        member x.Run(t, rt) = x.Run(t, rt)

    interface IDisposable with
        member x.Dispose() = x.Dispose()