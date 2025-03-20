namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open System
open System.Runtime.InteropServices
open Microsoft.FSharp.Core

#nowarn "51"

[<AllowNullLiteral>]
type Fence internal (device: IDevice, [<Optional; DefaultParameterValue(false)>] signaled: bool) =
    static let infinite = UInt64.MaxValue

    let mutable fence = VkFence.Null

    do
        let mutable createInfo =
            VkFenceCreateInfo(
                if signaled then VkFenceCreateFlags.SignaledBit
                else VkFenceCreateFlags.None
            )
        VkRaw.vkCreateFence(device.Handle, &&createInfo, NativePtr.zero, &&fence)
            |> check "could not create fence"

        device.Instance.RegisterDebugTrace(fence.Handle)

    member x.Handle = fence
    member internal x.DeviceInterface = device

    static member WaitAll(fences: Fence[]) =
        if fences.Length > 0 then
            let pFences = fences |> NativePtr.stackUseArr _.Handle
            VkRaw.vkWaitForFences(fences.[0].DeviceInterface.Handle, uint32 fences.Length, pFences, 1u, infinite)
                |> check "failed to wait for fences"

    static member WaitAny(fences: Fence[]) =
        if fences.Length > 0 then
            let pFences = fences |> NativePtr.stackUseArr _.Handle
            VkRaw.vkWaitForFences(fences.[0].DeviceInterface.Handle, uint32 fences.Length, pFences, 0u, infinite)
                |> check "failed to wait for fences"

    member x.Signaled =
        if fence.IsValid then
            VkRaw.vkGetFenceStatus(device.Handle, fence) = VkResult.Success
        else
            true

    member x.Completed =
        if fence.IsValid then
            VkRaw.vkGetFenceStatus(device.Handle, fence) <> VkResult.NotReady
        else
            true

    member x.Reset() =
        if fence.IsValid then
            VkRaw.vkResetFences(device.Handle, 1u, &&fence)
                |> check "failed to reset fence"
        else
            failf "cannot reset disposed fence"

    member x.TryWait([<Optional; DefaultParameterValue(~~~0UL)>] timeoutInNanoseconds: uint64) =
        match VkRaw.vkWaitForFences(device.Handle, 1u, &&fence, 1u, timeoutInNanoseconds) with
        | VkResult.Success -> true
        | VkResult.Timeout -> false
        | err -> failf "could not wait for fences: %A" err

    member x.Dispose() =
        if fence.IsValid && device.Handle <> 0n then
            VkRaw.vkDestroyFence(device.Handle, fence, NativePtr.zero)
            fence <- VkFence.Null

    member x.Wait([<Optional; DefaultParameterValue(~~~0UL)>] timeoutInNanoseconds: uint64) =
        if not <| x.TryWait(timeoutInNanoseconds) then
            raise <| TimeoutException()

    interface IDeviceObject with
        member x.DeviceInterface = x.DeviceInterface

    interface IDisposable with
        member x.Dispose() = x.Dispose()

[<AutoOpen>]
module DeviceFenceExtensions =

    type IDevice with
        member x.CreateFence([<Optional; DefaultParameterValue(false)>] signaled: bool) =
            new Fence(x, signaled)