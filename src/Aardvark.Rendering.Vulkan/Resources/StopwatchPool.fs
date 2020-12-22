﻿namespace Aardvark.Rendering.Vulkan

open System
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop

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
                    VkQueryPoolCreateFlags.None,
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