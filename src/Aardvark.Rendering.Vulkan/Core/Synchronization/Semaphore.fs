namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open System

#nowarn "51"

type Semaphore internal (device: IDevice) =
    let mutable handle = VkSemaphore.Null

    do
        let mutable createInfo = VkSemaphoreCreateInfo.Empty
        use pCreateInfo = fixed &createInfo
        use pHandle = fixed &handle
        VkRaw.vkCreateSemaphore(device.Handle, pCreateInfo, NativePtr.zero, pHandle)
            |> check "could not create semaphore"

        device.Instance.RegisterDebugTrace(handle.Handle)

    member x.Handle = handle
    member internal x.DeviceInterface = device

    member x.Dispose() =
        if handle.IsValid && device.Handle <> 0n then
            VkRaw.vkDestroySemaphore(device.Handle, handle, NativePtr.zero)
            handle <- VkSemaphore.Null

    interface IDeviceObject with
        member x.DeviceInterface = x.DeviceInterface

    interface IDisposable with
        member x.Dispose() = x.Dispose()

[<AutoOpen>]
module DeviceSemaphoreExtensions =

    type IDevice with
        member x.CreateSemaphore() =
            new Semaphore(x)