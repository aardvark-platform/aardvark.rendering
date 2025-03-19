namespace Aardvark.Rendering.Vulkan

open System.Runtime.InteropServices
open Aardvark.Base
open System
open System.Collections.Generic
open Microsoft.FSharp.Core

#nowarn "51"

[<Flags>]
type CommandPoolFlags =
    | None          = 0
    | Transient     = 1
    | ResetBuffer   = 2
    | Protected     = 4

type CommandPool internal(queueFamily: IDeviceQueueFamily, [<Optional; DefaultParameterValue(CommandPoolFlags.None)>] flags: CommandPoolFlags) =
    let device = queueFamily.DeviceInterface
    let mutable handle = VkCommandPool.Null

    do
        let mutable createInfo =
            VkCommandPoolCreateInfo(
                flags |> int |> unbox,
                uint32 queueFamily.Info.index
            )
        VkRaw.vkCreateCommandPool(device.Handle, &&createInfo, NativePtr.zero, &&handle)
            |> check "could not create command pool"

        device.Instance.RegisterDebugTrace(handle.Handle)

    let buffers = HashSet<CommandBuffer>()

    member internal x.DeviceInterface = device
    member internal x.QueueFamilyInterface = queueFamily
    member x.Handle = handle

    member x.Reset() =
        VkRaw.vkResetCommandPool(device.Handle, handle, VkCommandPoolResetFlags.None)
            |> check "failed to reset command pool"

        for cmd in buffers do
            cmd.Reset(resetByPool = true)

    member x.Dispose() =
        if handle.IsValid && device.Handle <> 0n then
            for cmd in buffers do
                cmd.Dispose()
            buffers.Clear()

            VkRaw.vkDestroyCommandPool(device.Handle, handle, NativePtr.zero)
            handle <- VkCommandPool.Null

    member private x.RemoveCommandBuffer(buffer: CommandBuffer) =
        buffers.Remove buffer |> ignore

    member x.CreateCommandBuffer(level : CommandBufferLevel) =
        let buffer = new CommandBuffer(x, level, x.RemoveCommandBuffer)
        buffers.Add buffer |> ignore
        buffer

    interface ICommandPool with
        member x.DeviceInterface = x.DeviceInterface
        member x.Handle = x.Handle

    interface IDisposable with
        member x.Dispose() = x.Dispose()

[<AutoOpen>]
module CommandPoolExtensions =

    type CommandBuffer with
        member x.Pool = x.PoolInterface :?> CommandPool

    type IDeviceQueueFamily with
        member x.CreateCommandPool([<Optional; DefaultParameterValue(CommandPoolFlags.None)>] flags: CommandPoolFlags) =
            new CommandPool(x, flags)