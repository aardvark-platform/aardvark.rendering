namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open System

#nowarn "51"

type Event internal (device: IDevice) =
    let mutable handle = VkEvent.Null

    do
        let mutable createInfo = VkEventCreateInfo.Empty
        VkRaw.vkCreateEvent(device.Handle, &&createInfo, NativePtr.zero, &&handle)
            |> check "could not create event"

        device.Instance.RegisterDebugTrace(handle.Handle)

    member x.Handle = handle
    member internal x.DeviceInterface = device

    member x.IsSet =
        if handle.IsValid then
            match VkRaw.vkGetEventStatus(device.Handle, handle) with
            | VkResult.EventSet -> true
            | VkResult.EventReset -> false
            | err -> err |> checkForFault device "could not get event status" |> unbox
        else
            failf "could not get event status"

    member x.Set() =
        VkRaw.vkSetEvent(device.Handle, handle)
            |> check "could not set event"

    member x.Reset() =
        VkRaw.vkResetEvent(device.Handle, handle)
            |> check "could not set event"

    member x.Dispose() =
        if handle.IsValid && device.Handle <> 0n then
            VkRaw.vkDestroyEvent(device.Handle, handle, NativePtr.zero)
            handle <- VkEvent.Null

    interface IDeviceObject with
        member x.DeviceInterface = x.DeviceInterface

    interface IDisposable with
        member x.Dispose() = x.Dispose()

[<AutoOpen>]
module DeviceEventExtensions =

    type IDevice with
        member x.CreateEvent() =
            new Event(x)