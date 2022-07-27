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

        static member BindRaytracingPipeline(pipeline : RaytracingPipeline, descriptorSets : DescriptorSetBinding) =
            { new Command() with
                member x.Compatible = QueueFlags.Compute
                member x.Enqueue cmd =
                    cmd.AppendCommand()
                    VkRaw.vkCmdBindPipeline(cmd.Handle, VkPipelineBindPoint.RayTracingKhr, pipeline.Handle)

                    cmd.AppendCommand()
                    VkRaw.vkCmdBindDescriptorSets(
                        cmd.Handle, VkPipelineBindPoint.RayTracingKhr, descriptorSets.Layout,
                        uint32 descriptorSets.FirstIndex, uint32 descriptorSets.Count, descriptorSets.Sets,
                        0u, NativePtr.zero
                    )

                    []
            }

        static member TraceRays(shaderBindingTable : ShaderBindingTable,
                                width : uint32, height : uint32, depth : uint32) =
            { new Command() with
                member x.Compatible = QueueFlags.Compute
                member x.Enqueue cmd =
                    cmd.AppendCommand()
                    native {
                        let! pRaygenAddress   = shaderBindingTable.RaygenTable.AddressRegion
                        let! pMissAddress     = shaderBindingTable.MissTable.AddressRegion
                        let! pHitAddress      = shaderBindingTable.HitGroupTable.AddressRegion
                        let! pCallableAddress = shaderBindingTable.CallableTable.AddressRegion
                        VkRaw.vkCmdTraceRaysKHR(cmd.Handle, pRaygenAddress, pMissAddress, pHitAddress, pCallableAddress, width, height, depth)
                    }

                    []
            }

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module RaytracingCommand =

        let toCommand (shaderBindingTable : ShaderBindingTable) = function
            | RaytracingCommand.TraceRaysCmd(size) ->
                Command.TraceRays(shaderBindingTable, uint32 size.X, uint32 size.Y, uint32 size.Z)

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

    type CompiledCommand(shaderBindingTable : aval<ShaderBindingTable>, commands : aval<RaytracingCommand list>) =
        inherit AdaptiveObject()

        let mutable compiled = []

        new (shaderBindingTable : aval<ShaderBindingTable>, commands : alist<RaytracingCommand>) =
            CompiledCommand(shaderBindingTable, commands |> AList.toAVal |> AVal.map IndexList.toList)

        member x.Commands =
            compiled

        member x.Update(token : AdaptiveToken) =
            x.EvaluateIfNeeded token false (fun token ->
                let sbt = shaderBindingTable.GetValue(token)
                let commands = commands.GetValue(token)

                let updated = commands |> List.map (RaytracingCommand.toCommand sbt)

                if updated <> compiled then
                    compiled <- updated
                    true
                else
                    false
            )


type RaytracingTask(manager : ResourceManager, pipeline : RaytracingPipelineState, commands : alist<RaytracingCommand>) =
    inherit AdaptiveObject()

    let device = manager.Device
    let user = manager.ResourceUser

    let pool = device.ComputeFamily.CreateCommandPool(CommandPoolFlags.ResetBuffer)
    let cmd = pool.CreateCommandBuffer(CommandBufferLevel.Primary)

    let resources = ResourceLocationSet(user)

    let preparedPipeline = manager.PrepareRaytracingPipeline(pipeline)
    do for r in preparedPipeline.Resources do
        resources.Add(r)

    let compiled = CompiledCommand(preparedPipeline.ShaderBindingTable, commands)

    member x.Run(token : AdaptiveToken, queries : IQuery) =
        x.EvaluateAlways token (fun token ->

            let vulkanQueries = queries.ToVulkanQuery()

            use tt = device.Token

            let rt = RenderToken.Empty |> RenderToken.withQuery queries
            use __ = rt.Use()

            let resourcesChanged =
                resources.Update(token, rt)

            tt.Sync()

            let commandChanged =
                compiled.Update(token)

            if commandChanged || resourcesChanged then
                if RuntimeConfig.ShowRecompile then
                    let cause =
                        String.concat "; " [
                            if commandChanged then yield "content"
                            if resourcesChanged then yield "resources"
                        ]
                        |> sprintf "{ %s }"

                    Log.line "[Raytracing] recompile commands: %s" cause


            let pipeline = preparedPipeline.Pipeline.Update(token, rt)
            let descriptorSets = preparedPipeline.DescriptorSets.Update(token, rt)

            cmd.Begin(CommandBufferUsage.OneTimeSubmit)

            for q in vulkanQueries do
                q.Begin cmd

            cmd.enqueue {
                do! Command.BindRaytracingPipeline(pipeline.handle, descriptorSets.handle)

                for cmd in compiled.Commands do
                    do! cmd
            }

            for q in vulkanQueries do
                q.End cmd

            cmd.End()

            device.ComputeFamily.RunSynchronously(cmd)
        )

    member x.Dispose() =
        transact (fun () ->
            for r in preparedPipeline.Resources do
                resources.Remove(r)

            preparedPipeline.Dispose()
            cmd.Dispose()
            pool.Dispose()
        )

    interface IRaytracingTask with
        member x.Run(token, query) = x.Run(token, query)

    interface IDisposable with
        member x.Dispose() = x.Dispose()