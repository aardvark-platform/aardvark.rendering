namespace Aardvark.Rendering.Vulkan

open Aardvark.Base

type OcclusionQuery(device : Device, precise : bool) =
    inherit VulkanQuery(device, VkQueryType.Occlusion, VkQueryPipelineStatisticFlags.None, 1, 1)

    // Sums the subquery results
    let compute (data : List<uint64[]>) =
        data |> List.sumBy Array.head

    override x.Begin (cmd : CommandBuffer, pool : QueryPool) =
        cmd.Enqueue <| Command.BeginQuery(pool, 0, if precise then VkQueryControlFlags.PreciseBit else VkQueryControlFlags.None)

    interface IOcclusionQuery with

        member x.HasResult() =
            x.TryGetResults(false) |> Option.isSome

        member x.TryGetResult(_ : unit, reset : bool) =
            x.TryGetResults(reset) |> Option.map compute

        member x.GetResult(_ : unit, reset : bool) =
            compute <| x.GetResults(reset)

        member x.IsPrecise = precise