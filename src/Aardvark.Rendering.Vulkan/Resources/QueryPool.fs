namespace Aardvark.Rendering.Vulkan

open FShade
open FShade.Imperative
open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop
open System.Collections.Generic

#nowarn "9"
#nowarn "51"


type QueryPool =
    class
        val mutable public Device : Device
        val mutable public Handle : VkQueryPool
        val mutable public Count : int

        new(d,h,c) = { Device = d; Handle = h; Count = c }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module QueryPool =
    let create (cnt : int) (device : Device) =
        let mutable info = 
            VkQueryPoolCreateInfo(
                VkStructureType.QueryPoolCreateInfo, 0n,
                VkQueryPoolCreateFlags.MinValue,
                VkQueryType.Timestamp,
                uint32 cnt,
                VkQueryPipelineStatisticFlags.None
            )

        let mutable handle = VkQueryPool.Null
        VkRaw.vkCreateQueryPool(device.Handle, &&info, NativePtr.zero, &&handle)
            |> check "could not create query pool"

        QueryPool(device, handle, cnt)

    let delete (pool : QueryPool) =
        VkRaw.vkDestroyQueryPool(pool.Device.Handle, pool.Handle, NativePtr.zero)

    let get (pool : QueryPool) =
        let data : int64[] = Array.zeroCreate pool.Count
        let gc = GCHandle.Alloc(data, GCHandleType.Pinned)
        try
            let result = 
                VkRaw.vkGetQueryPoolResults(pool.Device.Handle, pool.Handle, 0u, uint32 pool.Count, uint64 (data.Length * 8), gc.AddrOfPinnedObject(), 8UL, VkQueryResultFlags.WaitBit ||| VkQueryResultFlags.PartialBit ||| VkQueryResultFlags.D64Bit)

            match result with
                | VkResult.VkNotReady -> ()
                | res -> res |> check "could not get query results"

            data
        finally
            gc.Free()

[<AutoOpen>]
module QueryCommandExtensions =
    type Command with
        static member Reset(pool : QueryPool) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue(cmd) =
                    cmd.AppendCommand()
                    VkRaw.vkCmdResetQueryPool(cmd.Handle, pool.Handle, 0u, uint32 pool.Count)

                    Disposable.Empty
            }
        static member BeginQuery(pool : QueryPool, index : int) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue(cmd) =
                    cmd.AppendCommand()
                    VkRaw.vkCmdBeginQuery(cmd.Handle, pool.Handle, uint32 index, VkQueryControlFlags.PreciseBit)

                    Disposable.Empty
            }
        static member EndQuery(pool : QueryPool, index : int) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue(cmd) =
                    cmd.AppendCommand()
                    VkRaw.vkCmdEndQuery(cmd.Handle, pool.Handle, uint32 index)

                    Disposable.Empty
            }

        static member CopyQueryResults(pool : QueryPool, target : Buffer) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue(cmd) =
                    cmd.AppendCommand()
                    VkRaw.vkCmdCopyQueryPoolResults(cmd.Handle, pool.Handle, 0u, uint32 pool.Count, target.Handle, 0UL, 8UL, VkQueryResultFlags.D64Bit ||| VkQueryResultFlags.WaitBit ||| VkQueryResultFlags.PartialBit)

                    Disposable.Empty
            }
      
[<AbstractClass; Sealed; Extension>]      
type DeviceQueryPoolExtensions private() =
    [<Extension>]
    static member inline CreateQueryPool(device : Device, count : int) =
        device |> QueryPool.create count

    [<Extension>]
    static member inline Delete(device : Device, pool : QueryPool) =
        pool |> QueryPool.delete

    [<Extension>]
    static member inline GetResults(device : Device, pool : QueryPool) =
        pool |> QueryPool.get