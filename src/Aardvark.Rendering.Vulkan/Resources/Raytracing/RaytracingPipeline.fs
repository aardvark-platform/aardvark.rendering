namespace Aardvark.Rendering.Vulkan.Raytracing

#nowarn "9"

open Microsoft.FSharp.NativeInterop

open Aardvark.Base
open Aardvark.Rendering.Vulkan
open Aardvark.Rendering.Vulkan.KHRRayTracingPipeline
open Aardvark.Rendering.Vulkan.KHRDeferredHostOperations

type RaytracingPipelineDescription = {
    Program : RaytracingProgram
    MaxRecursionDepth : uint32
}

type RaytracingPipeline(device : Device, handle : VkPipeline, description : RaytracingPipelineDescription) =
    inherit Resource<VkPipeline>(device, handle)

    member x.Description        = description
    member x.Program            = description.Program
    member x.MaxRecursionDepth  = description.MaxRecursionDepth
    member x.Layout             = description.Program.Layout

    override x.Destroy() =
        if x.Handle.IsValid then
            VkRaw.vkDestroyPipeline(x.Device.Handle, x.Handle, NativePtr.zero)
            x.Handle <- VkPipeline.Null


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RaytracingPipeline =

    let private pMain = CStr.malloc "main"

    let private getStageIndex (stage : Option<RaytracingStageInfo>) =
        match stage with
        | Some s -> s.Index
        | _ -> VkShaderUnusedKhr

    let private getHitGroupType (group : HitGroup<'T>) =
        match group.Intersection with
        | None -> VkRayTracingShaderGroupTypeKHR.TrianglesHitGroup
        | _ -> VkRayTracingShaderGroupTypeKHR.ProceduralHitGroup

    let create (device : Device) (basePipeline : RaytracingPipeline option) (description : RaytracingPipelineDescription) =
        let groups =
            description.Program.Groups |> List.toArray

        let stages =
            description.Program.Groups
            |> List.collect ShaderGroup.toList
            |> List.sortBy (fun i -> i.Index)
            |> List.toArray

        let handle =
            native {
                let! pStages = stages |> Array.map (fun s ->
                    VkPipelineShaderStageCreateInfo(
                        VkPipelineShaderStageCreateFlags.None,
                        VkShaderStageFlags.ofShaderStage s.Module.Stage,
                        s.Module.Handle, pMain, NativePtr.zero
                    )
                )

                let! pGroups = groups |> Array.map (fun g ->
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

                let basePipeline, derivativeFlag =
                    match basePipeline with
                    | None -> VkPipeline.Null, VkPipelineCreateFlags.None
                    | Some x -> x.Handle, VkPipelineCreateFlags.DerivativeBit

                let maxRecursion =
                    match device.PhysicalDevice.Limits.Raytracing with
                    | Some limits -> min limits.MaxRayRecursionDepth description.MaxRecursionDepth
                    | _ -> description.MaxRecursionDepth

                let! pInfo =
                    VkRayTracingPipelineCreateInfoKHR(
                        VkPipelineCreateFlags.AllowDerivativesBit ||| derivativeFlag,
                        uint32 stages.Length, pStages,
                        uint32 groups.Length, pGroups,
                        maxRecursion,
                        NativePtr.zero, NativePtr.zero, NativePtr.zero,
                        description.Program.Layout.Handle,
                        basePipeline, 0
                    )

                let! pHandle = VkPipeline.Null
                VkRaw.vkCreateRayTracingPipelinesKHR(device.Handle, VkDeferredOperationKHR.Null, VkPipelineCache.Null, 1u, pInfo, NativePtr.zero, pHandle)
                    |> check "[Raytracing] Failed to create pipeline"

                return !!pHandle
            }

        new RaytracingPipeline(device, handle, description)