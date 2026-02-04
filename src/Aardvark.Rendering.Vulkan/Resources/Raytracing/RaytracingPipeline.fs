namespace Aardvark.Rendering.Vulkan.Raytracing

#nowarn "9"
#nowarn "51"

open Aardvark.Base
open Aardvark.Rendering.Vulkan
open KHRRayTracingPipeline
open KHRDeferredHostOperations
open EXTOpacityMicromap

type RaytracingPipelineDescription = {
    Program : RaytracingProgram
    MaxRecursionDepth : uint32
}

type RaytracingPipeline(device : Device, handle : VkPipeline, description : RaytracingPipelineDescription) =
    inherit Resource<VkPipeline>(device, handle)

    member x.Description        = description
    member x.Program            = description.Program
    member x.MaxRecursionDepth  = description.MaxRecursionDepth
    member x.Layout             = description.Program.PipelineLayout

    override x.Destroy() =
        if x.Handle.IsValid then
            VkRaw.vkDestroyPipeline(x.Device.Handle, x.Handle, NativePtr.zero)
            x.Handle <- VkPipeline.Null


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RaytracingPipeline =

    let private getStageIndex (stage : Option<RaytracingStageInfo>) =
        match stage with
        | Some s -> s.Index
        | _ -> VkShaderUnusedKhr

    let private getHitGroupType (group : HitGroup<'T>) =
        match group.Intersection with
        | None -> VkRayTracingShaderGroupTypeKHR.TrianglesHitGroup
        | _ -> VkRayTracingShaderGroupTypeKHR.ProceduralHitGroup

    let create (device : Device) (basePipeline : RaytracingPipeline voption) (description : RaytracingPipelineDescription) =
        let groups =
            description.Program.Groups |> List.toArray

        let stages =
            description.Program.Groups
            |> List.collect ShaderGroup.toList
            |> List.sortBy _.Index
            |> List.toArray

        CStr.using "main" (fun pMain ->
            use pStages = fixed stages |> Array.map (fun s ->
                VkPipelineShaderStageCreateInfo(
                    VkPipelineShaderStageCreateFlags.None,
                    VkShaderStageFlags.ofShaderStage s.Module.Stage,
                    s.Module.Handle, pMain, NativePtr.zero
                )
            )

            use pGroups = fixed groups |> Array.map (fun g ->
                match g with
                | ShaderGroup.General s ->
                    VkRayTracingShaderGroupCreateInfoKHR(
                        VkRayTracingShaderGroupTypeKHR.General,
                        s.Value.Index, VkShaderUnusedKhr, VkShaderUnusedKhr, VkShaderUnusedKhr, 0n
                    )

                | ShaderGroup.HitGroup g ->
                    VkRayTracingShaderGroupCreateInfoKHR(
                        getHitGroupType g, VkShaderUnusedKhr,
                        g.ClosestHit |> getStageIndex,
                        g.AnyHit |> getStageIndex,
                        g.Intersection |> getStageIndex, 0n
                    )
            )

            let createFlags =
                if device.EnabledFeatures.Raytracing.Micromap then
                    VkPipelineCreateFlags.RayTracingOpacityMicromapBitExt ||| VkPipelineCreateFlags.AllowDerivativesBit
                else
                    VkPipelineCreateFlags.AllowDerivativesBit

            let basePipeline, derivativeFlag =
                match basePipeline with
                | ValueNone -> VkPipeline.Null, VkPipelineCreateFlags.None
                | ValueSome x -> x.Handle, VkPipelineCreateFlags.DerivativeBit

            let maxRecursion =
                match device.PhysicalDevice.Limits.Raytracing with
                | Some limits -> min limits.MaxRayRecursionDepth description.MaxRecursionDepth
                | _ -> description.MaxRecursionDepth

            let mutable createInfo =
                VkRayTracingPipelineCreateInfoKHR(
                    createFlags ||| derivativeFlag,
                    uint32 stages.Length, pStages,
                    uint32 groups.Length, pGroups,
                    maxRecursion,
                    NativePtr.zero, NativePtr.zero, NativePtr.zero,
                    description.Program.PipelineLayout.Handle,
                    basePipeline, 0
                )

            let mutable handle = VkPipeline.Null
            VkRaw.vkCreateRayTracingPipelinesKHR(
                device.Handle, VkDeferredOperationKHR.Null, device.PipelineCache.Handle,
                1u, &&createInfo, NativePtr.zero, &&handle
            )
            |> check "Failed to create raytracing pipeline"

            new RaytracingPipeline(device, handle, description)
        )