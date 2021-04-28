namespace Aardvark.Rendering.Vulkan

open System
open Aardvark.Base
open Aardvark.Rendering

type TimeQuery(device : Device) =
    inherit Query(device, QueryType.Timestamp, 2)

    // Nanoseconds per timestamp tick
    let period = device.PhysicalDevice.Limits.Precision.TimestampPeriod

    // Computes the duration based on the query results
    let compute (data : uint64[][]) =
        let toNanoSeconds (t : uint64[]) =
             (period * float (t.[1] - t.[0])) |> int64 |> MicroTime.FromNanoseconds

        data |> Array.sumBy toNanoSeconds

    // Writes the time stamp value to the first query
    override x.Begin (cmd : CommandBuffer, pool : QueryPool) =
        cmd.Enqueue <| Command.WriteTimestamp(pool, VkPipelineStageFlags.BottomOfPipeBit, 0)

    // Writes the time stamp value to the second query
    override x.End (cmd : CommandBuffer, pool : QueryPool) =
        cmd.Enqueue <| Command.WriteTimestamp(pool, VkPipelineStageFlags.BottomOfPipeBit, 1)

    interface ITimeQuery with

        member x.HasResult() = x.TryGetResults(false) |> Option.isSome

        member x.TryGetResult(_ : unit, reset : bool) = x.TryGetResults(reset) |> Option.map compute

        member x.GetResult(_ : unit, reset : bool) = compute <| x.GetResults(reset)

    interface IDisposable with
        member x.Dispose() = x.Dispose()