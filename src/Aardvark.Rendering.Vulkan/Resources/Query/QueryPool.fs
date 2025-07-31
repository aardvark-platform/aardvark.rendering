namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open Aardvark.Rendering.Vulkan
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

#nowarn "51"

type QueryPool =
    class
        inherit Resource<VkQueryPool>

        val public Type : VkQueryType
        val public Count : int

        override this.Destroy() =
            if this.Device.Handle <> 0n && this.Handle.IsValid then
                VkRaw.vkDestroyQueryPool(this.Device.Handle, this.Handle, NativePtr.zero)
                this.Handle <- VkQueryPool.Null

        new (device, handle, typ, count) =
            { inherit Resource<_>(device, handle); Type = typ; Count = count }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module QueryPool =
    let create (typ : VkQueryType) (flags : VkQueryPipelineStatisticFlags) (count : int) (device : Device) =
        let mutable createInfo =
            VkQueryPoolCreateInfo(
                VkQueryPoolCreateFlags.None,
                typ, uint32 count, flags
            )

        let mutable handle = VkQueryPool.Null
        VkRaw.vkCreateQueryPool(device.Handle, &&createInfo, NativePtr.zero, &&handle)
            |> check "could not create query pool"

        new QueryPool(device, handle, typ, count)

    // Needs Vulkan 1.2 or EXTHostQueryReset
    //let reset (pool : QueryPool) =
    //    VkRaw.vkResetQueryPool(pool.Device.Handle, pool.Handle, 0u, uint32 pool.Count)

    let private getResults (valuesPerQuery : int) (flags : VkQueryResultFlags) (pool : QueryPool) =
        let bufferLength = pool.Count * valuesPerQuery
        let bufferSizeInBytes = bufferLength * sizeof<uint64>
        let bufferStride = valuesPerQuery * sizeof<uint64>

        let data : uint64[] = Array.zeroCreate bufferLength
        data |> NativePtr.pinArr (fun ptr ->
            let result =
                VkRaw.vkGetQueryPoolResults(
                    pool.Device.Handle, pool.Handle, 0u, uint32 pool.Count,
                    uint64 bufferSizeInBytes, ptr.Address,
                    uint64 bufferStride,
                    flags ||| VkQueryResultFlags.D64Bit
                )

            match result with
            | VkResult.Success -> Some data
            | VkResult.NotReady -> None
            | _ -> result |> checkForFault pool.Device "failed to get query results" |> unbox
        )

    let tryGetValues (valuesPerQuery : int) (pool : QueryPool) =
        pool |> getResults valuesPerQuery VkQueryResultFlags.None

    let tryGet (pool : QueryPool) =
        pool |> tryGetValues 1

    let getValues (valuesPerQuery : int) (pool : QueryPool) =
        let flags = VkQueryResultFlags.WaitBit
        pool |> getResults valuesPerQuery flags |> Option.get

    let get (pool : QueryPool) =
        pool |> getValues 1

[<AutoOpen>]
module QueryCommandExtensions =
    type Command with
        static member Reset(pool : QueryPool) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue(cmd) =
                    cmd.AppendCommand()
                    VkRaw.vkCmdResetQueryPool(cmd.Handle, pool.Handle, 0u, uint32 pool.Count)
                    cmd.AddResource pool
            }
        static member BeginQuery(pool : QueryPool, index : int, flags : VkQueryControlFlags) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue(cmd) =
                    cmd.AppendCommand()
                    VkRaw.vkCmdBeginQuery(cmd.Handle, pool.Handle, uint32 index, flags)
                    cmd.AddResource pool
            }
        static member EndQuery(pool : QueryPool, index : int) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue(cmd) =
                    cmd.AppendCommand()
                    VkRaw.vkCmdEndQuery(cmd.Handle, pool.Handle, uint32 index)
                    cmd.AddResource pool
            }

        static member CopyQueryResults(pool : QueryPool, target : Buffer) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue(cmd) =
                    cmd.AppendCommand()
                    VkRaw.vkCmdCopyQueryPoolResults(cmd.Handle, pool.Handle, 0u, uint32 pool.Count, target.Handle, 0UL, 8UL, VkQueryResultFlags.D64Bit ||| VkQueryResultFlags.WaitBit ||| VkQueryResultFlags.PartialBit)
                    cmd.AddResource pool
            }

        static member WriteTimestamp(pool : QueryPool, pipelineFlags : VkPipelineStageFlags, index : int) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue(cmd) =
                    cmd.AppendCommand()
                    VkRaw.vkCmdWriteTimestamp(cmd.Handle, pipelineFlags, pool.Handle, uint32 index)
                    cmd.AddResource pool
            }

[<AbstractClass; Sealed; Extension>]
type DeviceQueryPoolExtensions =
    [<Extension>]
    static member inline CreateQueryPool(device : Device, typ : VkQueryType, [<Optional; DefaultParameterValue(1)>] count : int) =
        device |> QueryPool.create typ VkQueryPipelineStatisticFlags.None count

    [<Extension>]
    static member inline CreateQueryPool(device : Device, statistics : VkQueryPipelineStatisticFlags, [<Optional; DefaultParameterValue(1)>] count : int) =
        device |> QueryPool.create VkQueryType.PipelineStatistics statistics count

    [<Extension>]
    static member inline GetResults(pool : QueryPool) =
        pool |> QueryPool.get
