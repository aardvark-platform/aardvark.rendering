namespace Aardvark.Rendering.Vulkan

open Aardvark.Base

[<AutoOpen>]
module private PipelineQueryHelpers =

    let base2VulkanFlags =
        LookupTable.lookupTable [
            InputAssemblyVertices,                  VkQueryPipelineStatisticFlags.InputAssemblyVerticesBit
            InputAssemblyPrimitives,                VkQueryPipelineStatisticFlags.InputAssemblyPrimitivesBit
            VertexShaderInvocations,                VkQueryPipelineStatisticFlags.VertexShaderInvocationsBit
            GeometryShaderInvocations,              VkQueryPipelineStatisticFlags.GeometryShaderInvocationsBit
            GeometryShaderPrimitives,               VkQueryPipelineStatisticFlags.GeometryShaderPrimitivesBit
            ClippingInputPrimitives,                VkQueryPipelineStatisticFlags.ClippingInvocationsBit
            ClippingOutputPrimitives,               VkQueryPipelineStatisticFlags.ClippingPrimitivesBit
            FragmentShaderInvocations,              VkQueryPipelineStatisticFlags.FragmentShaderInvocationsBit
            TesselationControlShaderPatches,        VkQueryPipelineStatisticFlags.TessellationControlShaderPatchesBit
            TesselationEvaluationShaderInvocations, VkQueryPipelineStatisticFlags.TessellationEvaluationShaderInvocationsBit
            ComputeShaderInvocations,               VkQueryPipelineStatisticFlags.ComputeShaderInvocationsBit
        ]

    // Computes a vulkan bit field based on the set of flags
    let getVulkanFlags (flags : Set<PipelineStatistics>) =
        flags |> Set.fold (fun accum f -> accum ||| base2VulkanFlags f) VkQueryPipelineStatisticFlags.None

    // Returns the index of the statistic in the query result buffer
    let getFlagIndex (f : PipelineStatistics) (flags : Set<PipelineStatistics>) =
        flags |> Set.toList |> List.sortBy base2VulkanFlags |> List.tryFindIndex ((=) f)


type PipelineQuery(device : Device, enabledStatistics : Set<PipelineStatistics>) =
    inherit Query(device, QueryType.PipelineStatistics (getVulkanFlags enabledStatistics, Set.count enabledStatistics), 1)

    let sumArrays (arrays : uint64[][]) =
        arrays |> Array.fold (fun sum x ->
            Array.map2 (+) sum x
        ) (Array.zeroCreate enabledStatistics.Count)

    let compute (statistics : Set<PipelineStatistics>) (data : uint64[]) =
        statistics |> Seq.map (fun s ->
            let value =
                match enabledStatistics |> getFlagIndex s with
                | Some i -> data.[i]
                | None -> 0UL

            s, value
        )
        |> Map.ofSeq

    interface IPipelineQuery with

        member x.HasResult() =
            x.TryGetResults(false) |> Option.isSome

        member x.TryGetResult(statistics : Set<PipelineStatistics>, reset : bool) =
            x.TryGetResults(reset) |> Option.map (sumArrays >> compute statistics)

        member x.GetResult(statistics : Set<PipelineStatistics>, reset : bool) =
            x.GetResults(reset) |> sumArrays |> compute statistics

        member x.Statistics = enabledStatistics