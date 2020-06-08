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
open Microsoft.FSharp.Core.LanguagePrimitives.IntrinsicOperators

#nowarn "9"
// #nowarn "51"


type QueryPool =
    class
        val mutable public Device : Device
        val mutable public Handle : VkQueryPool
        val mutable public Count : int
        val mutable public Type : VkQueryType

        new(d,h,c,t) = { Device = d; Handle = h; Count = c; Type = t }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module QueryPool =
    let create (typ : VkQueryType) (flags : VkQueryPipelineStatisticFlags) (cnt : int) (device : Device) =
        native {
            let! pInfo = 
                VkQueryPoolCreateInfo(
                    VkQueryPoolCreateFlags.MinValue,
                    typ, uint32 cnt, flags
                )

            let! pHandle = VkQueryPool.Null
            VkRaw.vkCreateQueryPool(device.Handle, pInfo, NativePtr.zero, pHandle)
                |> check "could not create query pool"

            return QueryPool(device, !!pHandle, cnt, typ)
        }

    let delete (pool : QueryPool) =
        VkRaw.vkDestroyQueryPool(pool.Device.Handle, pool.Handle, NativePtr.zero)

    let reset (pool : QueryPool) =
        VkRaw.vkResetQueryPool(pool.Device.Handle, pool.Handle, 0u, uint32 pool.Count)

    let private getResults (valuesPerQuery : int) (flags : VkQueryResultFlags) (pool : QueryPool) =
        let bufferLength = pool.Count * valuesPerQuery
        let bufferSizeInBytes = bufferLength * sizeof<uint64>
        let bufferStride = valuesPerQuery * sizeof<uint64>

        let data : uint64[] = Array.zeroCreate bufferLength
        let gc = GCHandle.Alloc(data, GCHandleType.Pinned)

        try
            let result =
                VkRaw.vkGetQueryPoolResults(
                    pool.Device.Handle, pool.Handle, 0u, uint32 pool.Count,
                    uint64 bufferSizeInBytes, gc.AddrOfPinnedObject(),
                    uint64 bufferStride,
                    flags ||| VkQueryResultFlags.D64Bit
                )

            match result with
            | VkResult.VkSuccess -> Some data
            | VkResult.VkNotReady -> None
            | _ -> result |> check "failed to get query results" |> unbox

        finally
            gc.Free()

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

                    Disposable.Empty
            }
        static member BeginQuery(pool : QueryPool, index : int, flags : VkQueryControlFlags) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue(cmd) =
                    cmd.AppendCommand()
                    VkRaw.vkCmdBeginQuery(cmd.Handle, pool.Handle, uint32 index, flags)

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

        static member WriteTimestamp(pool : QueryPool, pipelineFlags : VkPipelineStageFlags, index : int) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue(cmd) =
                    cmd.AppendCommand()
                    VkRaw.vkCmdWriteTimestamp(cmd.Handle, pipelineFlags, pool.Handle, uint32 index)

                    Disposable.Empty
            }
      
[<AbstractClass; Sealed; Extension>]      
type DeviceQueryPoolExtensions private() =
    [<Extension>]
    static member inline CreateQueryPool(device : Device, count : int) =
        device |> QueryPool.create VkQueryType.Timestamp VkQueryPipelineStatisticFlags.None count

    [<Extension>]
    static member inline Delete(device : Device, pool : QueryPool) =
        pool |> QueryPool.delete

    [<Extension>]
    static member inline GetResults(device : Device, pool : QueryPool) =
        pool |> QueryPool.get


type StopwatchPool(handle : VkQueryPool, count : int, accumulate : ComputeShader) =
    let device = accumulate.Device
    let stampCount = 2 * count
    let values = device.CreateBuffer(VkBufferUsageFlags.StorageBufferBit ||| VkBufferUsageFlags.TransferSrcBit ||| VkBufferUsageFlags.TransferDstBit, int64 sizeof<int64> * int64 count)
    let stamps = device.CreateBuffer(VkBufferUsageFlags.StorageBufferBit ||| VkBufferUsageFlags.TransferSrcBit ||| VkBufferUsageFlags.TransferDstBit, int64 sizeof<int64> * int64 stampCount)

    let accumulateIn = 
        let i = ComputeShader.newInputBinding accumulate
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
        native {
            let device = pool.Device
            let stampCount = 2 * count
            let! pInfo = 
                VkQueryPoolCreateInfo(
                    VkQueryPoolCreateFlags.MinValue,
                    VkQueryType.Timestamp,
                    uint32 stampCount,
                    VkQueryPipelineStatisticFlags.None
                )

            let! pHandle = VkQueryPool.Null
            VkRaw.vkCreateQueryPool(device.Handle, pInfo, NativePtr.zero, pHandle)
                |> check "could not create query pool"

            let shader = device |> ComputeShader.ofFunction Kernels.accumulate
            return new StopwatchPool(!!pHandle, count, shader)
        }

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
