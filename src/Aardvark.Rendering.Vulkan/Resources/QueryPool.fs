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


type StopwatchPool(pool : DescriptorPool, handle : VkQueryPool, count : int, accumulate : ComputeShader) =
    let device = pool.Device
    let stampCount = 2 * count
    let values = device.CreateBuffer(VkBufferUsageFlags.StorageBufferBit ||| VkBufferUsageFlags.TransferSrcBit ||| VkBufferUsageFlags.TransferDstBit, int64 sizeof<int64> * int64 count)
    let stamps = device.CreateBuffer(VkBufferUsageFlags.StorageBufferBit ||| VkBufferUsageFlags.TransferSrcBit ||| VkBufferUsageFlags.TransferDstBit, int64 sizeof<int64> * int64 stampCount)

    let accumulateIn = 
        let i = pool |> ComputeShader.newInputBinding accumulate
        i.["stamps"] <- stamps
        i.["values"] <- values
        i.Flush()
        i

    let accumulateGroups = 
        if count % 32 = 0 then uint32 (count / 32)
        else uint32 (1 + count / 32)

    let start =
        { new Command() with
            member __.Compatible = QueueFlags.All
            member __.Enqueue cmd =
                cmd.AppendCommand()
                VkRaw.vkCmdResetQueryPool(cmd.Handle, handle, 0u, uint32 stampCount)
                Disposable.Empty
        }
        
    let reset =
        { new Command() with
            member __.Compatible = QueueFlags.All
            member __.Enqueue cmd =
                cmd.AppendCommand()
                VkRaw.vkCmdFillBuffer(cmd.Handle, values.Handle, 0UL, uint64 values.Size, 0u)
                Disposable.Empty
        }

    let stop = 
        { new Command() with
            member __.Compatible = QueueFlags.All
            member __.Enqueue cmd =
                cmd.AppendCommand()
                VkRaw.vkCmdCopyQueryPoolResults(cmd.Handle, handle, 0u, uint32 stampCount, stamps.Handle, 0UL, 8UL, VkQueryResultFlags.D64Bit)
                
                cmd.AppendCommand()
                VkRaw.vkCmdBindPipeline(cmd.Handle, VkPipelineBindPoint.Compute, accumulate.Handle)

                let res = accumulateIn.Bind.Enqueue(cmd)

                cmd.AppendCommand()
                VkRaw.vkCmdDispatch(cmd.Handle, accumulateGroups, 1u, 1u)
                res
        }

    member x.Reset() = reset
    member x.Begin() = start
    member x.End() = stop

    member x.Start(i : int) =
        let i = 2 * i
        { new Command() with
            member x.Compatible = QueueFlags.All
            member x.Enqueue cmd =
                cmd.AppendCommand()
                VkRaw.vkCmdWriteTimestamp(cmd.Handle, VkPipelineStageFlags.BottomOfPipeBit, handle, uint32 i)
                Disposable.Empty
        }

    member x.Stop(i : int) =
        let i = 2 * i + 1
        { new Command() with
            member x.Compatible = QueueFlags.All
            member x.Enqueue cmd =
                cmd.AppendCommand()
                VkRaw.vkCmdWriteTimestamp(cmd.Handle, VkPipelineStageFlags.BottomOfPipeBit, handle, uint32 i)
                Disposable.Empty
        }

    member x.Download() =
        let temp = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferDstBit (int64 sizeof<int64> * int64 count)
        device.perform {
            do! Command.Copy(values, temp)
        }

        let arr : int64[] = Array.zeroCreate count
        temp.Memory.Mapped(fun ptr ->
            let mutable ptr = ptr
            for i in 0 .. count - 1 do
                arr.[i] <- NativeInt.read ptr
                ptr <- ptr + 8n
        )

        arr

    member x.Dispose() =
        device.Delete values
        device.Delete stamps
        accumulateIn.Dispose()
        VkRaw.vkDestroyQueryPool(device.Handle, handle, NativePtr.zero)
    
    interface IDisposable with
        member x.Dispose() = x.Dispose()

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module StopwatchPool =

    module Kernels =
        open FShade

        [<GLSLIntrinsic("({0} + {1})", "GL_ARB_gpu_shader_int64")>]
        let add (a : int64) (b : int64) = a + b

        [<LocalSize(X = 32)>]
        let accumulate (stamps : int64[]) (values : int64[]) =
            compute {
                let vid = getGlobalId().X
                let s = 2 * vid
                let e = s + 1

                let d = stamps.[e] - stamps.[s]
                values.[vid] <- add values.[vid] d
            }

    let create (count : int) (pool : DescriptorPool) =
        let device = pool.Device
        let stampCount = 2 * count
        let mutable info = 
            VkQueryPoolCreateInfo(
                VkStructureType.QueryPoolCreateInfo, 0n,
                VkQueryPoolCreateFlags.MinValue,
                VkQueryType.Timestamp,
                uint32 stampCount,
                VkQueryPipelineStatisticFlags.None
            )

        let mutable handle = VkQueryPool.Null
        VkRaw.vkCreateQueryPool(device.Handle, &&info, NativePtr.zero, &&handle)
            |> check "could not create query pool"

        let shader = device |> ComputeShader.ofFunction Kernels.accumulate
        new StopwatchPool(pool, handle, count, shader)

    let delete (pool : StopwatchPool) =
        pool.Dispose()
        
    let reset (pool : StopwatchPool) = pool.Reset()
    let start (pool : StopwatchPool) = pool.Begin()
    let stop (pool : StopwatchPool) = pool.End()
        
    let download (pool : StopwatchPool) = pool.Download()
          
[<AbstractClass; Sealed; Extension>]      
type DeviceStopwatchPoolExtensions private() =
    [<Extension>]
    static member inline CreateStopwatchPool(pool : DescriptorPool, count : int) =
        pool |> StopwatchPool.create count

    [<Extension>]
    static member inline Delete(device : Device, pool : StopwatchPool) =
        pool |> StopwatchPool.delete

    [<Extension>]
    static member inline Download(device : Device, pool : StopwatchPool) =
        pool |> StopwatchPool.download
