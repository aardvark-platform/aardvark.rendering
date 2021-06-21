namespace Aardvark.Rendering.Vulkan.Raytracing

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

open System

#nowarn "9"

[<AutoOpen>]
module private ``Trace Command Extensions`` =

    type Command with

        static member BindRaytracingPipeline(pipeline : RaytracingPipeline, descriptorSets : DescriptorSetBinding) =
            { new Command() with
                member x.Compatible = QueueFlags.Graphics
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

        static member TraceRays(shaderBindingTable : ShaderBindingTable, raygen : Symbol,
                                width : uint32, height : uint32, depth : uint32) =
            { new Command() with
                member x.Compatible = QueueFlags.Graphics
                member x.Enqueue cmd =
                    cmd.AppendCommand()
                    native {
                        let! pRaygenAddress   = shaderBindingTable.RaygenTable.GetAddressRegion(raygen)
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
            | RaytracingCommand.TraceRaysCmd(raygen, size) ->
                let raygen =
                    raygen |> Option.defaultWith (fun _ ->
                        shaderBindingTable.RaygenTable.Indices |> Map.toList |> List.head |> fst
                    )

                Command.TraceRays(shaderBindingTable, raygen, uint32 size.X, uint32 size.Y, uint32 size.Z)

            | RaytracingCommand.SyncBufferCmd(buffer, src, dst) ->
                Command.Sync(unbox buffer, VkAccessFlags.ofResourceAccess src, VkAccessFlags.ofResourceAccess dst)

            | RaytracingCommand.SyncTextureCmd(texture, src, dst) ->
                let image = unbox<Image> texture
                Command.ImageBarrier(image.[ImageAspect.Color], VkAccessFlags.ofResourceAccess src, VkAccessFlags.ofResourceAccess dst)

            | RaytracingCommand.TransformLayoutCmd(texture, layout) ->
                let image = unbox<Image> texture
                Command.TransformLayout(image, VkImageLayout.ofTextureLayout layout)


[<AutoOpen>]
module private RaytracingTaskInternals =

    type CompiledCommand(shaderBindingTable : aval<ShaderBindingTable>, commands : aval<RaytracingCommand list>) =
        inherit AdaptiveObject()

        let mutable compiled = []

        new (shaderBindingTable : aval<ShaderBindingTable>, commands : alist<RaytracingCommand>) =
            CompiledCommand(shaderBindingTable, commands |> AList.toAVal |> AVal.map IndexList.toList)

        member x.Run(commandBuffer : CommandBuffer) =
            commandBuffer.enqueue {
                for cmd in compiled do
                    do! cmd
            }

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

    let pool = device.GraphicsFamily.CreateCommandPool(CommandPoolFlags.ResetBuffer)
    let cmd = pool.CreateCommandBuffer(CommandBufferLevel.Primary)
    let inner = pool.CreateCommandBuffer(CommandBufferLevel.Secondary)

    let resources = ResourceLocationSet(user)

    let preparedPipeline = manager.PrepareRaytracingPipeline(pipeline)
    do for r in preparedPipeline.Resources do
        resources.Add(r)

    let compiled = CompiledCommand(preparedPipeline.ShaderBindingTable, commands)

    member x.Run(token : AdaptiveToken, queries : IQuery) =
        x.EvaluateIfNeeded token () (fun t ->

            let vulkanQueries = queries.ToVulkanQuery()

            use tt = device.Token

            let commandChanged =
                compiled.Update(token)

            let resourcesChanged =
                resources.Update(token)

            if commandChanged || resourcesChanged then
                let cause =
                    String.concat "; " [
                        if commandChanged then yield "content"
                        if resourcesChanged then yield "resources"
                    ]
                    |> sprintf "{ %s }"

                if Config.showRecompile then
                    Log.line "[Vulkan] recompile commands: %s" cause

                inner.Begin(CommandBufferUsage.None, true)
                compiled.Run(inner)
                inner.End()

            tt.Sync()

            let pipeline = preparedPipeline.Pipeline.Update(token)
            let descriptorSets = preparedPipeline.DescriptorSets.Update(token)

            queries.Begin()
            cmd.Begin(CommandBufferUsage.OneTimeSubmit)

            for q in vulkanQueries do
                q.Begin cmd

            cmd.enqueue {
                do! Command.BindRaytracingPipeline(pipeline.handle, descriptorSets.handle)
                do! Command.Execute [inner]
            }

            for q in vulkanQueries do
                q.End cmd

            cmd.End()
            queries.End()

            device.GraphicsFamily.RunSynchronously(cmd)
        )

    member x.Dispose() =
        transact (fun () ->
            for r in preparedPipeline.Resources do
                resources.Remove(r)

            preparedPipeline.Dispose()
            inner.Dispose()
            cmd.Dispose()
            pool.Dispose()
        )

    interface IRaytracingTask with
        member x.Run(token, query) = x.Run(token, query)

    interface IDisposable with
        member x.Dispose() = x.Dispose()